using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Workers;

namespace MyApp.Worker.Tests;

/// <summary>
/// What one pass does with each outcome.
/// </summary>
/// <remarks>
/// No clock, no timer and no database. Everything here is decided by the result the handler returns,
/// so a stub handler and a zero backoff make every case deterministic — there is nothing to wait for
/// and nothing to advance.
/// </remarks>
public sealed class PassTests
{
    [Test]
    public async Task A_Success_Runs_Once()
    {
        Recorder recorder = new();
        await RunAsync(recorder, _ => Result<Response>.Success(new Response(3)));

        await Assert.That(recorder.Attempts).IsEqualTo(1);
    }

    [Test]
    public async Task A_Transient_Failure_Is_Retried_To_The_Limit()
    {
        Recorder recorder = new();
        await RunAsync(recorder, _ => WidgetErrorsUnavailable);

        // Bounded: the next tick will try again anyway, so retrying forever only turns a permanent
        // failure into an outage.
        await Assert.That(recorder.Attempts).IsEqualTo(RetireWidgetsPass.MaximumAttempts);
    }

    [Test]
    public async Task A_Transient_Failure_That_Clears_Stops_Retrying()
    {
        Recorder recorder = new();
        await RunAsync(recorder, attempt =>
            attempt == 1 ? WidgetErrorsUnavailable : Result<Response>.Success(new Response(1)));

        await Assert.That(recorder.Attempts).IsEqualTo(2);
    }

    [Test]
    public async Task A_Permanent_Failure_Is_Not_Retried()
    {
        Recorder recorder = new();
        await RunAsync(recorder, _ => Errors.Invalid("widgets.nonsense", "Nothing will change this."));

        // Retrying an Invalid or Conflict result produces the identical answer, so it is dead-lettered
        // on the first attempt rather than three times.
        await Assert.That(recorder.Attempts).IsEqualTo(1);
    }

    [Test]
    public async Task Every_Attempt_Gets_Its_Own_Scope()
    {
        Recorder recorder = new();
        await RunAsync(recorder, _ => WidgetErrorsUnavailable);

        await Assert.That(recorder.Attempts).IsEqualTo(RetireWidgetsPass.MaximumAttempts);

        // A DbContext held across attempts accumulates tracked entities and eventually answers from a
        // stale graph, so distinct scopes are asserted rather than assumed.
        await Assert.That(recorder.DistinctScopes).IsEqualTo(recorder.Attempts);
    }

    private static Result<Response> WidgetErrorsUnavailable =>
        Errors.Unavailable("widgets.storage_unavailable", "Widget storage is temporarily unavailable.");

    private static async Task RunAsync(Recorder recorder, Func<int, Result<Response>> behaviour)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(recorder);
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IHandler<Request, Response>>(provider => new StubHandler(
            provider.GetRequiredService<Recorder>(),
            provider.GetRequiredService<ScopeMarker>(),
            behaviour));

        await using ServiceProvider provider = services.BuildServiceProvider();

        RetireWidgetsPass pass = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            // Zero backoff: the retry policy is what is under test, not how long it waits.
            Options.Create(new RetireWidgetsOptions { RetryBackoff = TimeSpan.Zero }),
            provider.GetRequiredService<ILogger<RetireWidgetsPass>>());

        await pass.RunAsync(CancellationToken.None);
    }

    private sealed class StubHandler(Recorder recorder, ScopeMarker scope, Func<int, Result<Response>> behaviour)
        : IHandler<Request, Response>
    {
        public Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult(behaviour(recorder.Record(scope.Id)));
    }

    /// <summary>Scoped, so its identity differs for every scope the pass resolves.</summary>
    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private sealed class Recorder
    {
        private readonly HashSet<Guid> _scopes = [];

        public int Attempts { get; private set; }

        public int DistinctScopes => _scopes.Count;

        /// <summary>Records an attempt and returns its number.</summary>
        public int Record(Guid scopeId)
        {
            Attempts++;
            _scopes.Add(scopeId);

            return Attempts;
        }
    }
}
