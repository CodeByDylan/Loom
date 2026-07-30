namespace Loom.Persistence;

/// <summary>
/// A deferred domain event recorded for delivery after its transaction commits.
/// </summary>
/// <remarks>
/// Infrastructure rather than domain, so it is a plain class with its own identifier instead of an
/// entity: it has no invariants, no behaviour, and never takes part in a domain rule.
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>Gets the identifier of this message.</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Gets when the event was recorded.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the full name of the event's type, used to reconstruct it.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Gets the serialised event.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Gets when delivery succeeded, or <see langword="null" /> while it is still owed.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Gets how many times delivery has been attempted.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets why the last attempt failed, if it did.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets a value indicating whether delivery has been abandoned after too many attempts.
    /// </summary>
    /// <remarks>
    /// An abandoned message is left in place rather than deleted. A poisoned message is evidence of a
    /// bug, and deleting the evidence makes the bug harder to find.
    /// </remarks>
    public bool Abandoned { get; set; }
}
