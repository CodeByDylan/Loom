using Loom.Results;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

namespace MyApp.Worker.Tests;

/// <summary>
/// What one pass does with each outcome.
/// </summary>
/// <remarks>
/// No clock, no timer and no database. Everything here is decided by the result the handler returns,
/// so a stub handler and a zero backoff make every case deterministic.
/// </remarks>
public sealed class PassTests
{
    [Test]
    public async Task A_Success_Runs_Once()
    {
        await using PassHarness harness = PassHarness.For(_ => Result<Response>.Success(new Response(3)));

        await harness.Pass.RunAsync(CancellationToken.None);

        await Assert.That(harness.Attempts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_Transient_Failure_Is_Retried_To_The_Limit()
    {
        // Two, not the default three: the count asserted below is one this test chose, so a loop that
        // ignores the setting and retries a fixed number of times fails here.
        await using PassHarness harness = PassHarness.For(_ => StorageUnavailable, maximumAttempts: 2);

        await harness.Pass.RunAsync(CancellationToken.None);

        // Bounded: the next tick will try again anyway, so retrying forever only turns a permanent
        // failure into an outage.
        await Assert.That(harness.Attempts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_Transient_Failure_That_Clears_Stops_Retrying()
    {
        await using PassHarness harness = PassHarness.For(attempt =>
            attempt == 1 ? StorageUnavailable : Result<Response>.Success(new Response(1)));

        await harness.Pass.RunAsync(CancellationToken.None);

        await Assert.That(harness.Attempts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_Permanent_Failure_Is_Not_Retried()
    {
        await using PassHarness harness = PassHarness.For(
            _ => Errors.Invalid("widgets.nonsense", "Nothing will change this."));

        await harness.Pass.RunAsync(CancellationToken.None);

        // Retrying an Invalid or Conflict result produces the identical answer, so it is dead-lettered
        // on the first attempt rather than three times.
        await Assert.That(harness.Attempts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Nothing_To_Do_Is_Not_A_Failure()
    {
        await using PassHarness harness = PassHarness.For(
            _ => Errors.NotFound("widgets.none", "No widgets are oversized."));

        await harness.Pass.RunAsync(CancellationToken.None);

        await Assert.That(harness.Attempts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Every_Attempt_Gets_Its_Own_Scope()
    {
        await using PassHarness harness = PassHarness.For(_ => StorageUnavailable, maximumAttempts: 3);

        await harness.Pass.RunAsync(CancellationToken.None);

        await Assert.That(harness.Attempts.Count).IsEqualTo(3);

        // A DbContext held across attempts accumulates tracked entities and eventually answers from a
        // stale graph, so distinct scopes are asserted rather than assumed.
        await Assert.That(harness.Attempts.DistinctScopes).IsEqualTo(harness.Attempts.Count);
    }

    private static Result<Response> StorageUnavailable =>
        Errors.Unavailable("widgets.storage_unavailable", "Widget storage is temporarily unavailable.");
}
