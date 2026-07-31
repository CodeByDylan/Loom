using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Workers;

namespace MyApp.Worker.Tests;

/// <summary>
/// A pass and the worker above it, over a handler that does whatever the test says.
/// </summary>
/// <remarks>
/// No database, no clock, no timer. The backoff is zero because the retry policy is what is under
/// test, not how long it waits, so nothing here has anything to wait for.
/// </remarks>
internal sealed class PassHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private PassHarness(ServiceProvider provider, AttemptLog attempts, RetireWidgetsPass pass, RetireWidgetsWorker worker)
    {
        _provider = provider;
        Attempts = attempts;
        Pass = pass;
        Worker = worker;
    }

    /// <summary>What the handler was asked to do, and by whom.</summary>
    public AttemptLog Attempts { get; }

    public RetireWidgetsPass Pass { get; }

    public RetireWidgetsWorker Worker { get; }

    /// <summary>
    /// Builds a harness whose handler behaves as <paramref name="behaviour" /> says.
    /// </summary>
    /// <param name="behaviour">
    /// Given the attempt number, returns the result — or throws, which is how the worker's own
    /// guarantees are exercised.
    /// </param>
    /// <param name="maximumAttempts">
    /// How many times a transient failure is retried. Chosen by the caller rather than defaulted, so a
    /// test asserting a count is comparing against a number it picked instead of one the code supplied.
    /// </param>
    public static PassHarness For(Func<int, Result<Response>> behaviour, int maximumAttempts = 3)
    {
        AttemptLog attempts = new();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(attempts);
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IHandler<Request, Response>>(provider => new StubHandler(
            provider.GetRequiredService<AttemptLog>(),
            provider.GetRequiredService<ScopeMarker>(),
            behaviour));

        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<RetireWidgetsOptions> options = Options.Create(new RetireWidgetsOptions
        {
            RetryBackoff = TimeSpan.Zero,
            MaximumAttempts = maximumAttempts,
        });

        RetireWidgetsPass pass = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            options,
            provider.GetRequiredService<ILogger<RetireWidgetsPass>>());

        RetireWidgetsWorker worker = new(
            pass,
            TimeProvider.System,
            options,
            provider.GetRequiredService<ILogger<RetireWidgetsWorker>>());

        return new PassHarness(provider, attempts, pass, worker);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    /// <summary>Counts attempts and the scopes they arrived on.</summary>
    internal sealed class AttemptLog
    {
        private readonly HashSet<Guid> _scopes = [];

        public int Count { get; private set; }

        public int DistinctScopes => _scopes.Count;

        /// <summary>Records an attempt and returns its number.</summary>
        public int Record(Guid scopeId)
        {
            Count++;
            _scopes.Add(scopeId);

            return Count;
        }
    }

    /// <summary>Scoped, so its identity differs for every scope the pass resolves.</summary>
    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private sealed class StubHandler(AttemptLog attempts, ScopeMarker scope, Func<int, Result<Response>> behaviour)
        : IHandler<Request, Response>
    {
        public Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult(behaviour(attempts.Record(scope.Id)));
    }
}
