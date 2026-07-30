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

/// <summary>
/// A domain event delivered after its transaction commits, at least once.
/// </summary>
/// <remarks>
/// Marks the event as carrying a different contract from an ordinary <see cref="IDomainEvent" />,
/// and the difference is visible where the event is declared rather than buried in configuration:
/// <list type="bullet">
/// <item>
/// An ordinary event is dispatched <em>before</em> the transaction commits. Its handlers may change
/// data atomically with the operation that raised it, and must not reach outside the process — a
/// rollback cannot unsend an email.
/// </item>
/// <item>
/// A deferred event is dispatched <em>after</em> the transaction commits. Its handlers may reach
/// outside the process, and <strong>must be idempotent</strong>, because delivery is at least once
/// and a retry will run them again.
/// </item>
/// </list>
/// Naming the guarantee rather than the mechanism keeps this marker honest: the outbox is one way to
/// honour it, not the definition of it.
/// </remarks>
public interface IDeferredDomainEvent : IDomainEvent;
