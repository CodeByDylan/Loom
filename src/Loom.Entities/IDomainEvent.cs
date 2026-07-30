namespace Loom.Entities;

/// <summary>
/// Something that happened in the domain, recorded by an aggregate for later dispatch.
/// </summary>
/// <remarks>
/// Deliberately empty. The obvious addition is a timestamp, and it is a trap: populating one means
/// the entity reads the clock, which is untestable and puts a dependency on an entity that must not
/// have one. An event that needs a time takes it as a constructor parameter from the handler, which
/// does have a <see cref="TimeProvider" />.
/// <para>
/// Domain events are value-like, so implementations should be <c>sealed record</c> — the opposite of
/// the rule for entities.
/// </para>
/// </remarks>
public interface IDomainEvent;
