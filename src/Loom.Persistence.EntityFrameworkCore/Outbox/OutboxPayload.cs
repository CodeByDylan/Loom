using System.Text.Json;
using Loom.Entities;

namespace Loom.Persistence;

/// <summary>
/// Records a deferred domain event in the form it is stored in.
/// </summary>
/// <remarks>
/// Not generic over a context, unlike reading it back. Writing needs no configuration — the event
/// knows its own type — whereas resolving a stored name to a type depends on which assemblies that
/// outbox was told to search.
/// </remarks>
internal static class OutboxPayload
{
    internal static string TypeNameOf(IDeferredDomainEvent domainEvent) =>
        domainEvent.GetType().FullName
        ?? throw new InvalidOperationException("A deferred domain event must be a named type.");

    internal static string Serialize(IDeferredDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
}
