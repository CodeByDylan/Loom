using Loom.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

internal sealed class OutboxSink(TimeProvider clock) : IDeferredDomainEventSink
{
    public void Enqueue(DbContext context, IDeferredDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Added to the context whose save is in flight, so the record of what is owed commits in the
        // same transaction as the change that owed it. Nothing else about an outbox matters as much.
        context.Add(new OutboxMessage
        {
            OccurredAt = clock.GetUtcNow(),
            EventType = OutboxPayload.TypeNameOf(domainEvent),
            Payload = OutboxPayload.Serialize(domainEvent),
        });
    }
}
