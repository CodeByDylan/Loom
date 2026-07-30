using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public class DomainEventDispatchTests
{
    [Test]
    public async Task A_Handler_Runs_When_The_Aggregate_Is_Saved()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<AuditingHandler>);
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();

        await Assert.That(host.Recorder.Handled).Contains("audited:500");
    }

    [Test]
    public async Task Events_Are_Drained_So_They_Do_Not_Run_Twice()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<AuditingHandler>);
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();
        await context.SaveChangesAsync();

        await Assert.That(host.Recorder.Handled.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_Handler_Change_Commits_In_The_Same_Transaction()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<AuditingHandler>);

        using (IServiceScope scope = host.CreateScope())
        {
            TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Cancel();
            await context.SaveChangesAsync();
        }

        // The handler added its row during SavingChanges and never called SaveChanges itself. This is
        // the atomicity that dispatching before the commit buys.
        using (IServiceScope scope = host.CreateScope())
        {
            TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await Assert.That(await context.AuditEntries.CountAsync()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task A_Failing_Handler_Abandons_The_Whole_Save()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<FailingHandler>);

        using (IServiceScope scope = host.CreateScope())
        {
            TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Cancel();

            await Assert.That(async () => await context.SaveChangesAsync())
                .Throws<DomainEventDispatchException>();
        }

        // The order that raised the event is gone too, not merely the handler's work.
        using (IServiceScope scope = host.CreateScope())
        {
            TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await Assert.That(await context.Orders.CountAsync()).IsEqualTo(0);
            await Assert.That(await context.AuditEntries.CountAsync()).IsEqualTo(0);
        }
    }

    [Test]
    public async Task The_Failure_The_Handler_Reported_Is_Carried_On_The_Exception()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<FailingHandler>);
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();

        DomainEventDispatchException exception =
            await AssertThrowsAsync<DomainEventDispatchException>(() => context.SaveChangesAsync());

        await Assert.That(exception.Error.Code).IsEqualTo("refunds.down");
        await Assert.That(exception.Error.Category).IsEqualTo(ErrorCategory.Unavailable);
        await Assert.That(exception.EventType).IsEqualTo(typeof(OrderCancelled));
    }

    [Test]
    public async Task Every_Handler_For_An_Event_Runs()
    {
        await using TestHost host = await TestHost.CreateAsync(services =>
        {
            services.AddScoped<IDomainEventHandler<OrderCancelled>, AuditingHandler>();
            services.AddScoped<IDomainEventHandler<OrderCancelled>, NotifyingHandler>();
        });

        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();

        await Assert.That(host.Recorder.Handled.Count).IsEqualTo(2);
        await Assert.That(host.Recorder.Handled).Contains("notified:500");
    }

    [Test]
    public async Task An_Event_With_No_Handlers_Is_Not_An_Error()
    {
        await using TestHost host = await TestHost.CreateAsync();
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();

        await Assert.That(await context.Orders.CountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Events_Raised_By_A_Handler_Are_Also_Dispatched()
    {
        await using TestHost host = await TestHost.CreateAsync(services =>
        {
            services.AddScoped<IDomainEventHandler<OrderCancelled>, CascadingHandler>();
            services.AddScoped<IDomainEventHandler<OrderTouched>, TouchRecordingHandler>();
        });

        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();

        // Draining repeats, so an event raised while handling an event is not lost.
        await Assert.That(host.Recorder.Handled).Contains("touched");
    }

    [Test]
    public async Task A_Handler_Cannot_See_Its_Own_Transaction_Committed()
    {
        await using TestHost host = await TestHost.CreateAsync(Handlers<QueryingHandler>);
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Cancel();
        await context.SaveChangesAsync();

        // The documented cost of dispatching before the commit. A handler needing committed state is
        // asking for a deferred event instead.
        await Assert.That(host.Recorder.Handled).Contains("visible:False");
    }

    [Test]
    public async Task A_Deferred_Event_Without_An_Outbox_Fails_Loudly()
    {
        await using TestHost host = await TestHost.CreateAsync();
        using IServiceScope scope = host.CreateScope();
        TestDbContext context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Ship();

        // Dispatching it immediately would quietly downgrade the guarantee the event asked for.
        await Assert.That(async () => await context.SaveChangesAsync()).Throws<InvalidOperationException>();
    }

    private static void Handlers<THandler>(IServiceCollection services)
        where THandler : class, IDomainEventHandler<OrderCancelled> =>
        services.AddScoped<IDomainEventHandler<OrderCancelled>, THandler>();

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException expected)
        {
            return expected;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} but nothing was thrown.");
    }

    private sealed class AuditingHandler(TestDbContext context, Recorder recorder)
        : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            context.AuditEntries.Add(new AuditEntry { Note = $"cancelled {domainEvent.OrderId}" });
            recorder.Record($"audited:{domainEvent.Total}");
            return Task.FromResult(Result.Success);
        }
    }

    private sealed class NotifyingHandler(Recorder recorder) : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            recorder.Record($"notified:{domainEvent.Total}");
            return Task.FromResult(Result.Success);
        }
    }

    private sealed class FailingHandler(TestDbContext context) : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            // Writes before failing, to prove the write is discarded along with everything else.
            context.AuditEntries.Add(new AuditEntry { Note = "should not survive" });
            return Task.FromResult(Result.Failure(Errors.Unavailable("refunds.down", "The refund service is down.")));
        }
    }

    private sealed class CascadingHandler(TestDbContext context) : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            // Found through the change tracker, not a query: nothing is committed yet, so querying
            // the database for the order that raised this event would return nothing.
            Order order = context.ChangeTracker
                .Entries<Order>()
                .Select(entry => entry.Entity)
                .Single(candidate => candidate.Id == domainEvent.OrderId);

            order.Touch();
            return Task.FromResult(Result.Success);
        }
    }

    private sealed class QueryingHandler(TestDbContext context, Recorder recorder)
        : IDomainEventHandler<OrderCancelled>
    {
        public async Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            bool visible = await context.Orders
                .AsNoTracking()
                .AnyAsync(order => order.Id == domainEvent.OrderId, cancellationToken);

            recorder.Record($"visible:{visible}");
            return Result.Success;
        }
    }

    private sealed class TouchRecordingHandler(Recorder recorder) : IDomainEventHandler<OrderTouched>
    {
        public Task<Result> HandleAsync(OrderTouched domainEvent, CancellationToken cancellationToken)
        {
            recorder.Record("touched");
            return Task.FromResult(Result.Success);
        }
    }
}
