using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// A failure reported by a domain event handler reaches the caller as a failure, not an exception.
/// </summary>
public sealed class DomainEventFailureTests
{
    [Test]
    public async Task A_Failing_Event_Handler_Becomes_The_Callers_Failure()
    {
        await using FailureHost host = await FailureHost.CreateAsync();

        Result result = await host.CancelAsync();

        // Without the decorator this call would throw and the caller would see a server error, even
        // though nothing exceptional happened — a handler simply reported that it could not proceed.
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo("refunds.down");
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Unavailable);
    }

    [Test]
    public async Task Nothing_Is_Committed_When_An_Event_Handler_Fails()
    {
        await using FailureHost host = await FailureHost.CreateAsync();

        _ = await host.CancelAsync();

        // Translating the exception must not weaken the atomicity it was protecting.
        await host.InScopeAsync(async context =>
            await Assert.That(await context.Orders.CountAsync()).IsEqualTo(0));
    }

    [Test]
    public async Task A_Succeeding_Event_Handler_Is_Unaffected()
    {
        await using FailureHost host = await FailureHost.CreateAsync(failing: false);

        Result result = await host.CancelAsync();

        await Assert.That(result.IsSuccess).IsTrue();

        await host.InScopeAsync(async context =>
            await Assert.That(await context.Orders.CountAsync()).IsEqualTo(1));
    }

    [Test]
    public async Task Other_Exceptions_Are_Left_Alone()
    {
        await using FailureHost host = await FailureHost.CreateAsync(failing: false);

        // Only the abandoned-save exception is translated. Anything else is genuinely exceptional and
        // must keep its stack trace rather than being flattened into a failure.
        await Assert.That(async () => await host.ThrowAsync()).Throws<InvalidOperationException>();
    }

    internal sealed record CancelRequest;

    internal sealed record ThrowRequest;

    internal sealed class CancelHandler(TestDbContext context) : IHandler<CancelRequest>
    {
        public async Task<Result> HandleAsync(CancelRequest request, CancellationToken cancellationToken)
        {
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Cancel();
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success;
        }
    }

    internal sealed class ThrowingHandler : IHandler<ThrowRequest>
    {
        public Task<Result> HandleAsync(ThrowRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Something genuinely unexpected.");
    }

    private sealed class FailureHost : IAsyncDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        private FailureHost(Microsoft.Data.Sqlite.SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
        }

        internal static async Task<FailureHost> CreateAsync(bool failing = true)
        {
            Microsoft.Data.Sqlite.SqliteConnection connection = new("Filename=:memory:");
            await connection.OpenAsync();

            ServiceCollection services = new();
            services.AddLoomPersistence();

            services.AddLoomHandlers(chain => chain.WithDomainEventFailures())
                .AddHandler<CancelHandler, CancelRequest>()
                .AddHandler<ThrowingHandler, ThrowRequest>();

            if (failing)
            {
                services.AddScoped<IDomainEventHandler<OrderCancelled>, RefusingHandler>();
            }

            services.AddDbContext<TestDbContext>((serviceProvider, options) => options
                .UseSqlite(connection)
                .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

            ServiceProvider provider = services.BuildServiceProvider();

            using (IServiceScope scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
            }

            return new FailureHost(connection, provider);
        }

        internal async Task<Result> CancelAsync()
        {
            using IServiceScope scope = _provider.CreateScope();
            return await scope.ServiceProvider
                .GetRequiredService<IHandler<CancelRequest>>()
                .HandleAsync(new CancelRequest(), CancellationToken.None);
        }

        internal async Task<Result> ThrowAsync()
        {
            using IServiceScope scope = _provider.CreateScope();
            return await scope.ServiceProvider
                .GetRequiredService<IHandler<ThrowRequest>>()
                .HandleAsync(new ThrowRequest(), CancellationToken.None);
        }

        internal async Task InScopeAsync(Func<TestDbContext, Task> work)
        {
            using IServiceScope scope = _provider.CreateScope();
            await work(scope.ServiceProvider.GetRequiredService<TestDbContext>());
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private sealed class RefusingHandler : IDomainEventHandler<OrderCancelled>
        {
            public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure(
                    Errors.Unavailable("refunds.down", "The refund service is unreachable.")));
        }
    }
}
