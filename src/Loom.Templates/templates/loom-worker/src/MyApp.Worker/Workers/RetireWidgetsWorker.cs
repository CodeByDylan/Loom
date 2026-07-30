using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

namespace MyApp.Worker.Workers;

/// <summary>
/// Runs one slice on a schedule.
/// </summary>
/// <remarks>
/// The loop holds no business logic. It occupies exactly the position an endpoint does in an API: it
/// decides when to dispatch, and what a failure means for the schedule, and nothing else.
/// </remarks>
internal sealed partial class RetireWidgetsWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<RetireWidgetsWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>How many times a transient failure is retried before the iteration is abandoned.</summary>
    /// <remarks>
    /// Bounded on purpose. Unbounded retry against a permanent failure is an outage with extra steps,
    /// and the next tick will try again anyway.
    /// </remarks>
    internal const int MaximumAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed iteration must not kill the worker: an exception leaving ExecuteAsync stops
                // the service, and in some hosting models it does so silently.
                Failed(logger, exception);
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            // A scope per iteration, never one held across them. A DbContext kept between iterations
            // accumulates tracked entities and eventually answers from a stale graph.
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();

            IHandler<Request, Response> handler = scope.ServiceProvider
                .GetRequiredService<IHandler<Request, Response>>();

            Result<Response> result = await handler
                .HandleAsync(new Request(LargerThan: 100), cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Retired(logger, result.Value.Retired);
                return;
            }

            // The category decides what happens next, which is the same decision a status code
            // expresses at an HTTP boundary. Only a dependency that might recover is worth retrying;
            // anything the caller got wrong will be just as wrong next time.
            if (result.Error.Category is not ErrorCategory.Unavailable)
            {
                DeadLettered(logger, result.Error.Code, result.Error.Message);
                return;
            }

            if (attempt == MaximumAttempts)
            {
                GaveUp(logger, MaximumAttempts, result.Error.Code);
                return;
            }

            await Task.Delay(Backoff(attempt), clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    [LoggerMessage(Level = LogLevel.Information, Message = "Retired {Count} widget(s).")]
    private static partial void Retired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Iteration abandoned: {Code} {Reason}")]
    private static partial void DeadLettered(ILogger logger, string code, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gave up after {Attempts} attempts: {Code}")]
    private static partial void GaveUp(ILogger logger, int attempts, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "The iteration threw and was swallowed to keep the worker alive.")]
    private static partial void Failed(ILogger logger, Exception exception);
}
