using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Workers;

namespace MyApp.Worker.Tests;

/// <summary>
/// A failing pass must not end the loop, and shutdown must.
/// </summary>
/// <remarks>
/// One guarded iteration is called directly rather than driven through the timer. That the timer
/// fires is <c>PeriodicTimer</c>'s business; what matters here is which exceptions survive it and
/// which do not. No clock, no timer, nothing to wait for.
/// </remarks>
public sealed class ResilienceTests
{
    [Test]
    public async Task A_Throwing_Pass_Is_Swallowed()
    {
        RetireWidgetsWorker worker = Build(() => throw new InvalidOperationException("the dependency exploded"));

        // An exception leaving ExecuteAsync stops the service, in some hosting models silently.
        await worker.RunGuardedAsync(CancellationToken.None);
    }

    [Test]
    public async Task A_Cancellation_That_Is_Not_Shutdown_Is_Swallowed()
    {
        RetireWidgetsWorker worker = Build(() => throw new OperationCanceledException());

        // A timeout inside a pass surfaces the same way shutdown does. Treating it as shutdown would
        // stop the worker permanently over one slow query.
        await worker.RunGuardedAsync(CancellationToken.None);
    }

    [Test]
    public async Task Shutdown_Ends_The_Loop()
    {
        using CancellationTokenSource stopping = new();
        await stopping.CancelAsync();

        RetireWidgetsWorker worker = Build(() => throw new OperationCanceledException());

        await Assert.That(async () => await worker.RunGuardedAsync(stopping.Token))
            .Throws<OperationCanceledException>();
    }

    private static RetireWidgetsWorker Build(Func<Result<Response>> behaviour)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<IHandler<Request, Response>>(_ => new StubHandler(behaviour));

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<RetireWidgetsOptions> options = Options.Create(new RetireWidgetsOptions
        {
            RetryBackoff = TimeSpan.Zero,
        });

        RetireWidgetsPass pass = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            options,
            provider.GetRequiredService<ILogger<RetireWidgetsPass>>());

        return new RetireWidgetsWorker(
            pass,
            TimeProvider.System,
            options,
            provider.GetRequiredService<ILogger<RetireWidgetsWorker>>());
    }

    private sealed class StubHandler(Func<Result<Response>> behaviour) : IHandler<Request, Response>
    {
        public Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult(behaviour());
    }
}
