namespace Loom.Results;

/// <summary>
/// A single failure representing an invalid request, carrying one or more messages per field.
/// </summary>
/// <remarks>
/// A request with several problems is one error with structured detail, not several errors — which
/// is why <see cref="Result{T}" /> carries exactly one <see cref="Error" />. The metadata shape
/// matches the <c>errors</c> extension of a problem details response, so no translation is needed.
/// </remarks>
/// <param name="Code">A stable, machine-readable identifier for the failure.</param>
/// <param name="Message">A human-readable description of what went wrong.</param>
/// <param name="Metadata">Failure messages keyed by the field they apply to.</param>
public sealed record ValidationError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Metadata)
    : Error<IReadOnlyDictionary<string, string[]>>(ErrorCategory.Invalid, Code, Message, Metadata);
