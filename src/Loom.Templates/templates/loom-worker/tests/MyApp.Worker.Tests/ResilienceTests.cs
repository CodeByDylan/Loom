using Loom.Results;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

namespace MyApp.Worker.Tests;

/// <summary>
/// A failing pass must not end the loop, and shutdown must.
/// </summary>
/// <remarks>
/// One guarded iteration is called directly rather than driven through the timer. That the timer fires
/// is <c>PeriodicTimer</c>'s business; what matters here is which exceptions survive it.
/// </remarks>
public sealed class ResilienceTests
{
    [Test]
    public async Task A_Throwing_Pass_Is_Swallowed()
    {
        await using PassHarness harness = PassHarness.For(
            _ => throw new InvalidOperationException("the dependency exploded"));

        // An exception leaving ExecuteAsync stops the service, in some hosting models silently.
        await harness.Worker.RunGuardedAsync(CancellationToken.None);
    }

    [Test]
    public async Task A_Cancellation_That_Is_Not_Shutdown_Is_Swallowed()
    {
        await using PassHarness harness = PassHarness.For(_ => throw new OperationCanceledException());

        // A timeout inside a pass surfaces the same way shutdown does. Treating it as shutdown would
        // stop the worker permanently over one slow query.
        await harness.Worker.RunGuardedAsync(CancellationToken.None);
    }

    [Test]
    public async Task Shutdown_Ends_The_Loop()
    {
        using CancellationTokenSource stopping = new();
        await stopping.CancelAsync();

        await using PassHarness harness = PassHarness.For(_ => throw new OperationCanceledException());

        await Assert.That(async () => await harness.Worker.RunGuardedAsync(stopping.Token))
            .Throws<OperationCanceledException>();
    }
}
