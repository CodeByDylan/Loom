namespace Loom.Entities;

/// <summary>
/// The entry point to an aggregate: the only entity within it that may be loaded or saved directly.
/// </summary>
/// <remarks>
/// Non-generic on purpose. Infrastructure that dispatches domain events has to enumerate changed
/// entities and drain their events without knowing each aggregate's own type, and only a non-generic
/// interface makes that possible. It also gives architecture tests something to assert against —
/// for example, that a queryable set is only exposed for aggregate roots.
/// </remarks>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets the events recorded so far, without removing them.
    /// </summary>
    /// <remarks>For inspection, typically in tests. Infrastructure should drain instead.</remarks>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Returns the events recorded so far and clears them in one step.
    /// </summary>
    /// <returns>The events that had been recorded.</returns>
    /// <remarks>
    /// Reading and clearing separately leaves a window in which a newly raised event is discarded
    /// without ever being dispatched.
    /// </remarks>
    IReadOnlyCollection<IDomainEvent> DequeueDomainEvents();
}
