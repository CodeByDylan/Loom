using System.Collections.Concurrent;
using System.Text.Json;
using Loom.Entities;

namespace Loom.Persistence;

/// <summary>
/// Converts a deferred domain event to and from its recorded form.
/// </summary>
internal sealed class OutboxEventSerializer(OutboxOptions options)
{
    private static readonly ConcurrentDictionary<string, Type> ResolvedTypes = new();

    internal static string TypeNameOf(IDeferredDomainEvent domainEvent) =>
        domainEvent.GetType().FullName
        ?? throw new InvalidOperationException("A deferred domain event must be a named type.");

    internal static string Serialize(IDeferredDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

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
    private Type ResolveType(string fullName) => ResolvedTypes.GetOrAdd(fullName, name =>
    {
        Type[] matches =
        [
            .. options.EventAssemblies
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
