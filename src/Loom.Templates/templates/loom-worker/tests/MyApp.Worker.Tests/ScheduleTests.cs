using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Workers;

namespace MyApp.Worker.Tests;

/// <summary>
/// The schedule, tested apart from the work it schedules.
/// </summary>
/// <remarks>
/// No database and no real clock. The interval is five minutes, and a test that waited for it would
/// be useless; advancing a fake clock asserts the same thing in milliseconds. What is under test is
/// that the loop dispatches, resolves a fresh scope each time, and survives a failing iteration.
/// </remarks>
public sealed class ScheduleTests
{
    [Test]
    public async Task The_Loop_Dispatches_When_The_Interval_Elapses()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, () => Result<Response>.Success(new Response(0)));

        await RunUntilDispatchedAsync(provider, recorder);

        await Assert.That(recorder.Dispatches).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Each_Iteration_Gets_Its_Own_Scope()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, () => Result<Response>.Success(new Response(0)));

        await RunUntilDispatchedAsync(provider, recorder, dispatches: 2);

        // Asserted first, because the comparison below holds trivially when nothing ran at all.
        await Assert.That(recorder.Dispatches).IsGreaterThanOrEqualTo(2);

        // A DbContext held across iterations accumulates tracked entities and eventually answers from
        // a stale graph. Distinct scopes are what prevent it, so they are asserted rather than assumed.
        await Assert.That(recorder.DistinctScopes).IsEqualTo(recorder.Dispatches);
    }

    [Test]
    public async Task A_Failing_Iteration_Does_Not_Stop_The_Worker()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(
            recorder,
            () => throw new InvalidOperationException("the dependency exploded"));

        // Two, not one. A single dispatch only proves the handler threw; it is the second that proves
        // the loop was still alive to run it. An exception leaving ExecuteAsync stops the service, in
        // some hosting models silently.
        await RunUntilDispatchedAsync(provider, recorder, dispatches: 2);

        await Assert.That(recorder.Dispatches).IsGreaterThanOrEqualTo(2);
    }

    private static ServiceProvider Build(Recorder recorder, Func<Result<Response>> behaviour)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(recorder);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IHandler<Request, Response>>(provider => new StubHandler(
            provider.GetRequiredService<Recorder>(),
            provider.GetRequiredService<ScopeMarker>(),
            behaviour));

        return services.BuildServiceProvider();
    }

    private static async Task RunUntilDispatchedAsync(
        ServiceProvider provider,
        Recorder recorder,
        int dispatches = 1)
    {
        FakeTimeProvider clock = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        RetireWidgetsWorker worker = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            provider.GetRequiredService<ILogger<RetireWidgetsWorker>>());

        await worker.StartAsync(CancellationToken.None);

        try
        {
            // Advanced repeatedly rather than once, because the loop has to be sitting in
            // WaitForNextTickAsync before a tick means anything, and nothing in BackgroundService says
            // when that is.
            //
            // The millisecond is real and is load-bearing: PeriodicTimer coalesces ticks, so a burst of
            // advances with only Task.Yield between them all collapses into one dispatch and the loop
            // never gets scheduled in between. Yielding to the thread pool for an actual moment is what
            // lets each iteration run. The five-minute interval stays fake — this waits milliseconds,
            // not minutes.
            for (int attempt = 0; attempt < 200 && recorder.Dispatches < dispatches; attempt++)
            {
                clock.Advance(TimeSpan.FromMinutes(5));
                await Task.Delay(TimeSpan.FromMilliseconds(1));
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class StubHandler(Recorder recorder, ScopeMarker scope, Func<Result<Response>> behaviour)
        : IHandler<Request, Response>
    {
        public Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            recorder.Record(scope.Id);

            return Task.FromResult(behaviour());
        }
    }

    /// <summary>Scoped, so its identity differs for every scope the loop resolves.</summary>
    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private sealed class Recorder
    {
        private readonly Lock _gate = new();
        private readonly HashSet<Guid> _scopes = [];
        private int _dispatches;

        public int Dispatches
        {
            get { lock (_gate) { return _dispatches; } }
        }

        public int DistinctScopes
        {
            get { lock (_gate) { return _scopes.Count; } }
        }

        public void Record(Guid scopeId)
        {
            lock (_gate)
            {
                _dispatches++;
                _scopes.Add(scopeId);
            }
        }
    }
}
