using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence;

/// <summary>
/// Delivers recorded deferred domain events.
/// </summary>
/// <typeparam name="TContext">The context holding the recorded messages.</typeparam>
/// <remarks>
/// Separate from the background service that drives it, so that one pass can be run and asserted on
/// directly without waiting for a timer.
/// </remarks>
public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopes,
    OutboxSettings<TContext> settings,
    TimeProvider clock)
    where TContext : DbContext
{
    private readonly OutboxOptions _options = settings.Options;

    /// <summary>
    /// Attempts delivery of one batch of owed messages.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many messages were attempted.</returns>
    /// <remarks>
    /// Delivery is at least once. A handler that succeeds and then loses the record of having
    /// succeeded — because the process stopped between dispatch and save — will run again, so a
    /// deferred handler must be idempotent.
    /// </remarks>
    public async Task<int> DeliverPendingAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopes.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        IDomainEventDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        OutboxEventSerializer<TContext> serializer = scope.ServiceProvider
            .GetRequiredService<OutboxEventSerializer<TContext>>();

        List<OutboxMessage> owed = await context.Set<OutboxMessage>()
            .Where(message => message.DeliveredAt == null && !message.Abandoned)
            .OrderBy(message => message.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (owed.Count is 0)
        {
            return 0;
        }

        foreach (OutboxMessage message in owed)
        {
            await DeliverAsync(message, dispatcher, serializer, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return owed.Count;
    }

    private async Task DeliverAsync(
        OutboxMessage message,
        IDomainEventDispatcher dispatcher,
        OutboxEventSerializer<TContext> serializer,
        CancellationToken cancellationToken)
    {
        message.Attempts++;

        try
        {
            IDomainEvent domainEvent = serializer.Deserialize(message);
            Result result = await dispatcher.DispatchAsync(domainEvent, cancellationToken);

            if (result.IsSuccess)
            {
                message.DeliveredAt = clock.GetUtcNow();
                message.LastError = null;
                return;
            }

            Fail(message, $"{result.Error.Code}: {result.Error.Message}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A handler that throws, or a payload that will never deserialise, must not stop the
            // whole pass — one poisoned message would otherwise block every message behind it.
            Fail(message, exception.Message);
        }
    }

    private void Fail(OutboxMessage message, string error)
    {
        message.LastError = error;

        if (message.Attempts >= _options.MaximumAttempts)
        {
            message.Abandoned = true;
        }
    }
}
