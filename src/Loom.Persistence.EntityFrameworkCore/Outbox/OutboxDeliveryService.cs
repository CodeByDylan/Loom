using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Loom.Persistence;

/// <summary>
/// Drives outbox delivery on an interval.
/// </summary>
/// <typeparam name="TContext">The context holding the recorded messages.</typeparam>
internal sealed class OutboxDeliveryService<TContext>(
    OutboxProcessor<TContext> processor,
    OutboxOptions options,
    TimeProvider clock) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.PollingInterval, clock);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keeps going while there is a backlog, rather than delivering one batch per interval.
                while (await processor.DeliverPendingAsync(stoppingToken) == options.BatchSize)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // A failed pass must not stop the service: an unhandled exception out of ExecuteAsync
                // silently ends delivery for the lifetime of the process. Individual message failures
                // are already recorded against the message itself.
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }
}
