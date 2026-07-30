using Loom.Results;

namespace Loom.Entities;

/// <summary>
/// Reacts to a domain event.
/// </summary>
/// <typeparam name="TEvent">The event reacted to.</typeparam>
/// <remarks>
/// This is <em>fan-out</em>, and deliberately a different abstraction from a request handler. Any
/// number of these may exist for one event, infrastructure invokes them all, and nobody awaits a
/// value from them. A request handler is the opposite: exactly one, invoked by a caller that wants
/// the result, wrapped in a decorator chain.
/// <para>
/// Keeping the two as separate types is what preserves the rule that request dispatch has no
/// notifications. Collapsing them — letting one request type have many handlers — is the change that
/// would break it.
/// </para>
/// <para>
/// A failure is returned rather than thrown. For an ordinary event that aborts the transaction the
/// operation was part of, so nothing is committed. For a deferred event it marks the delivery as
/// failed and leaves it to be retried.
/// </para>
/// </remarks>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Reacts to the event.
    /// </summary>
    /// <param name="domainEvent">What happened.</param>
    /// <param name="cancellationToken">Cancels the reaction.</param>
    /// <returns>Whether the reaction succeeded.</returns>
    Task<Result> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
