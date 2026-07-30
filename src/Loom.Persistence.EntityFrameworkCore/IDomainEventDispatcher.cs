using Loom.Entities;
using Loom.Results;

namespace Loom.Persistence;

/// <summary>
/// Invokes every handler registered for a domain event.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Invokes every handler for the event.
    /// </summary>
    /// <param name="domainEvent">What happened.</param>
    /// <param name="cancellationToken">Cancels dispatch.</param>
    /// <returns>
    /// The first failure reported by a handler, or success if every handler succeeded or there were
    /// no handlers.
    /// </returns>
    Task<Result> DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
