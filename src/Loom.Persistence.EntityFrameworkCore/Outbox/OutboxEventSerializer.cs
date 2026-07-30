using System.Collections.Concurrent;
using System.Text.Json;
using Loom.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Converts a deferred domain event to and from its recorded form, for one context's outbox.
/// </summary>
/// <typeparam name="TContext">The context whose outbox is being served.</typeparam>
internal sealed class OutboxEventSerializer<TContext>(OutboxSettings<TContext> settings)
    where TContext : DbContext
{
    // Per instance rather than shared. The cache maps a name to a type, and which type a name resolves
    // to depends on the assemblies this outbox was configured with, so a shared cache would let one
    // context's configuration answer another's lookups.
    private readonly ConcurrentDictionary<string, Type> _resolvedTypes = new();

    internal IDomainEvent Deserialize(OutboxMessage message)
    {
        Type eventType = ResolveType(message.EventType);

        return JsonSerializer.Deserialize(message.Payload, eventType) is IDomainEvent domainEvent
            ? domainEvent
            : throw new InvalidOperationException(
                $"The recorded payload for '{message.EventType}' did not deserialise to a domain event.");
    }

    // Resolved by full name rather than assembly-qualified name, so that a version bump does not
    // orphan messages already recorded.
    private Type ResolveType(string fullName) => _resolvedTypes.GetOrAdd(fullName, name =>
    {
        Type[] matches =
        [
            .. settings.Options.EventAssemblies
                .Select(assembly => assembly.GetType(name, throwOnError: false))
                .Where(type => type is not null)
                .Select(type => type!)
                .Distinct(),
        ];

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No type named '{name}' was found in the assemblies configured for the outbox. Add its "
                + $"assembly to {nameof(OutboxOptions)}.{nameof(OutboxOptions.EventAssemblies)}."),
            _ => throw new InvalidOperationException(
                $"More than one type named '{name}' was found in the assemblies configured for the "
                + "outbox, so the recorded event is ambiguous."),
        };
    });
}
