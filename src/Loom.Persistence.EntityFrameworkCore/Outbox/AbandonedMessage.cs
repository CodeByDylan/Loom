namespace Loom.Persistence;

/// <summary>
/// What is known about a message delivery gave up on.
/// </summary>
/// <remarks>
/// A projection rather than the stored row, so reading the outbox for diagnosis cannot accidentally
/// change it. The payload is deliberately absent: it holds whatever the event carried, which may be
/// personal or secret, and nothing about deciding whether to retry needs to see it.
/// </remarks>
/// <param name="Id">Identifies the message, and is what a retry names.</param>
/// <param name="EventType">The full name of the event's type.</param>
/// <param name="OccurredAt">When the event was recorded.</param>
/// <param name="Attempts">How many deliveries were attempted before it was abandoned.</param>
/// <param name="LastError">Why the final attempt failed.</param>
public sealed record AbandonedMessage(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAt,
    int Attempts,
    string? LastError);
