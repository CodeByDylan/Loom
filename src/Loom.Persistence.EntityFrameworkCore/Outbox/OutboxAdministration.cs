using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Reads and repairs an outbox: what was abandoned, retrying it, and clearing what was delivered.
/// </summary>
/// <typeparam name="TContext">The context holding the outbox table.</typeparam>
/// <remarks>
/// Operations only. There is deliberately no endpoint, command or dashboard here, because how an
/// operator reaches these depends entirely on how they operate, and that is not yet known. What a
/// retry <em>means</em>, on the other hand, follows from the table's own columns and does not.
/// <para>
/// The alternative is hand-written SQL against a table this package owns, which is easy to get subtly
/// wrong under pressure: clear the error and the evidence is gone, leave the attempt count and the
/// message abandons again on its first failure. These reset exactly the right fields.
/// </para>
/// <para>
/// Every change here takes effect at once — there is no <c>SaveChanges</c> to follow, and none of it
/// passes through the change tracker or the domain event interceptor. That is what makes these safe to
/// run against a live outbox, and it also means they are not part of any surrounding transaction.
/// </para>
/// </remarks>
public sealed class OutboxAdministration<TContext>(TContext context)
    where TContext : DbContext
{
    /// <summary>
    /// Lists messages delivery gave up on, oldest first.
    /// </summary>
    /// <param name="limit">At most how many to return.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>What was abandoned, without the payloads.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit" /> is less than one.</exception>
    public async Task<IReadOnlyList<AbandonedMessage>> FindAbandonedAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        return await context.Set<OutboxMessage>()
            .AsNoTracking()
            .Where(message => message.Abandoned)
            .OrderBy(message => message.OccurredAt)
            .Take(limit)
            .Select(message => new AbandonedMessage(
                message.Id,
                message.EventType,
                message.OccurredAt,
                message.Attempts,
                message.LastError))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts what delivery gave up on.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>How many messages were abandoned.</returns>
    public Task<int> CountAbandonedAsync(CancellationToken cancellationToken = default) =>
        context.Set<OutboxMessage>().CountAsync(message => message.Abandoned, cancellationToken);

    /// <summary>
    /// Offers one abandoned message to delivery again.
    /// </summary>
    /// <param name="id">Which message.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true" /> if a message was retried; <see langword="false" /> if none was abandoned under that identifier.</returns>
    /// <remarks>
    /// The attempt count is reset, or the next failure would abandon it immediately. The last error is
    /// kept: it is the evidence of what went wrong, and a retry does not make that untrue.
    /// </remarks>
    public async Task<bool> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        int changed = await context.Set<OutboxMessage>()
            .Where(message => message.Id == id && message.Abandoned)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(message => message.Abandoned, false)
                    .SetProperty(message => message.Attempts, 0),
                cancellationToken);

        return changed > 0;
    }

    /// <summary>
    /// Offers every abandoned message to delivery again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were retried.</returns>
    /// <remarks>
    /// Safe to call after fixing whatever caused a batch to fail. Delivery still works in batches, so
    /// this does not turn into one enormous burst.
    /// </remarks>
    public Task<int> RetryAllAbandonedAsync(CancellationToken cancellationToken = default) =>
        context.Set<OutboxMessage>()
            .Where(message => message.Abandoned)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(message => message.Abandoned, false)
                    .SetProperty(message => message.Attempts, 0),
                cancellationToken);

    /// <summary>
    /// Deletes messages that were delivered before the given moment.
    /// </summary>
    /// <param name="deliveredBefore">Delete messages delivered strictly before this.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were deleted.</returns>
    /// <remarks>
    /// Nothing else removes a delivered message, so without this the table grows for as long as the
    /// application runs — which happens in every deployment, unlike abandonment, which needs a bug.
    /// <para>
    /// Only delivered messages are ever deleted. An abandoned one is evidence and is left alone even if
    /// it is older, because deleting the record of a failure is how the failure stays unexplained.
    /// </para>
    /// </remarks>
    public Task<int> PurgeDeliveredAsync(
        DateTimeOffset deliveredBefore,
        CancellationToken cancellationToken = default) =>
        context.Set<OutboxMessage>()
            .Where(message => message.DeliveredAt != null && message.DeliveredAt < deliveredBefore)
            .ExecuteDeleteAsync(cancellationToken);
}
