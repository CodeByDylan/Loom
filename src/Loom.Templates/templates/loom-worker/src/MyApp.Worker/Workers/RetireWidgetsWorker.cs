using Microsoft.Extensions.Options;

namespace MyApp.Worker.Workers;

/// <summary>
/// Runs one pass on a schedule.
/// </summary>
/// <remarks>
/// Holds no business logic and no decision about outcomes — that is <see cref="RetireWidgetsPass" />.
/// What is left is the schedule and one guarantee: a failed pass must not end the loop.
/// </remarks>
internal sealed partial class RetireWidgetsWorker(
    RetireWidgetsPass pass,
    TimeProvider clock,
    IOptions<RetireWidgetsOptions> options,
    ILogger<RetireWidgetsWorker> logger)
    : BackgroundService
{
    private readonly RetireWidgetsOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_options.Interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunGuardedAsync(stoppingToken);
        }
    }

    /// <summary>Runs one pass and refuses to let it end the loop.</summary>
    /// <remarks>
    /// Separate from <see cref="ExecuteAsync" /> so it can be called directly. This is the worker's
    /// only real behaviour, and reaching it through the timer would mean driving a hosted service from
    /// a fake clock — which is a test of <c>PeriodicTimer</c> rather than of this.
    /// </remarks>
    internal async Task RunGuardedAsync(CancellationToken stoppingToken)
    {
        try
        {
            await pass.RunAsync(stoppingToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // An exception leaving ExecuteAsync stops the service, and in some hosting models it does
            // so silently. Cancellation is only fatal when shutdown asked for it: a timeout inside a
            // pass also surfaces as OperationCanceledException, and treating that as shutdown would
            // stop the worker for good over one slow query.
            Failed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A pass threw and was swallowed to keep the worker alive.")]
    private static partial void Failed(ILogger logger, Exception exception);
}
