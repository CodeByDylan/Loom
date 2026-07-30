using Loom.Results;

namespace Loom.Persistence;

/// <summary>
/// Thrown when a domain event handler reports a failure, abandoning the save it was part of.
/// </summary>
/// <remarks>
/// The handler returned a failure; it did not throw. This exception exists because abandoning a save
/// is only expressible as an exception — there is no way to tell the object-relational mapper to stop
/// by returning a value. Nothing was committed.
/// <para>
/// Callers should not need to catch this. Declaring
/// <see cref="DomainEventFailureChainBuilderExtensions.WithDomainEventFailures" /> in the handler
/// decorator chain turns it back into the failure the event handler reported, so the outcome reaches
/// the caller as a result rather than as an exception. Catch it directly only outside that chain.
/// </para>
/// </remarks>
public sealed class DomainEventDispatchException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="error">The failure a handler reported.</param>
    /// <param name="eventType">The event whose handler failed.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    public DomainEventDispatchException(Error error, Type eventType)
        : base(BuildMessage(error, eventType))
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(eventType);

        Error = error;
        EventType = eventType;
    }

    /// <summary>Creates the exception.</summary>
    public DomainEventDispatchException()
        : base("A domain event handler reported a failure.") => Error = UnknownError;

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Describes the failure.</param>
    public DomainEventDispatchException(string message) : base(message) => Error = UnknownError;

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Describes the failure.</param>
    /// <param name="innerException">What caused it.</param>
    public DomainEventDispatchException(string message, Exception innerException)
        : base(message, innerException) => Error = UnknownError;

    /// <summary>
    /// Gets the failure a handler reported.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Gets the event whose handler failed, if it is known.
    /// </summary>
    public Type? EventType { get; }

    private static Error UnknownError { get; } =
        Errors.Unavailable("domain_events.dispatch_failed", "A domain event handler reported a failure.");

    private static string BuildMessage(Error error, Type eventType) =>
        $"A handler for '{eventType?.Name}' reported '{error?.Code}': {error?.Message} "
        + "Nothing was committed.";
}
