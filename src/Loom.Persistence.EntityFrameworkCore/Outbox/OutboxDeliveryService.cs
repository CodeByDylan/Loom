using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loom.Persistence;

/// <summary>
/// Drives outbox delivery on an interval.
/// </summary>
/// <typeparam name="TContext">The context holding the recorded messages.</typeparam>
internal sealed partial class OutboxDeliveryService<TContext>(
    OutboxProcessor<TContext> processor,
    OutboxSettings<TContext> settings,
    TimeProvider clock,
    ILogger<OutboxDeliveryService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private readonly OutboxOptions _options = settings.Options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_options.PollingInterval, clock);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keeps going while there is a backlog, rather than delivering one batch per interval.
                while (await processor.DeliverPendingAsync(stoppingToken) == _options.BatchSize)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed pass must not stop the service: an unhandled exception out of ExecuteAsync
                // silently ends delivery for the lifetime of the process. Individual message failures
                // are recorded against the message itself, but a pass that fails as a whole — losing a
                // connection, say — leaves no trace anywhere else, so it is logged here.
                DeliveryPassFailed(logger, typeof(TContext).Name, exception);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An outbox delivery pass for {Context} failed. Delivery continues; owed messages remain owed.")]
    private static partial void DeliveryPassFailed(ILogger logger, string context, Exception exception);
}
