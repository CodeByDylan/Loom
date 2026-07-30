using Loom.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Records a deferred domain event so that it can be delivered after the transaction commits.
/// </summary>
/// <remarks>
/// The record is written through the same context as the change that raised the event, so what is
/// owed commits atomically with the change that owed it.
/// </remarks>
public interface IDeferredDomainEventSink
{
    /// <summary>
    /// Records the event for later delivery.
    /// </summary>
    /// <param name="context">The context whose save is in progress.</param>
    /// <param name="domainEvent">What happened.</param>
    void Enqueue(DbContext context, IDeferredDomainEvent domainEvent);
}
