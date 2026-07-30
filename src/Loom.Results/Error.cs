namespace Loom.Results;

/// <summary>
/// An expected failure. Expected failures are returned as part of a <see cref="Result" /> or
/// <see cref="Result{T}" />, never thrown.
/// </summary>
/// <remarks>
/// This type is deliberately not sealed: consumers derive from it to declare domain-specific
/// errors, and deriving is what lets those errors convert implicitly to a result. It is the
/// documented exception to the seal-by-default rule.
/// </remarks>
/// <param name="Category">The semantic kind of the failure, used to choose a transport response.</param>
/// <param name="Code">
/// A stable, machine-readable identifier such as <c>orders.sku_unknown</c>. Callers discriminate
/// on this rather than on <paramref name="Message" />, which is free to be reworded.
/// </param>
/// <param name="Message">A human-readable description of what went wrong.</param>
public record Error(ErrorCategory Category, string Code, string Message);

/// <summary>
/// An expected failure carrying structured metadata about what went wrong.
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata carried by the error.</typeparam>
/// <param name="Category">The semantic kind of the failure.</param>
/// <param name="Code">A stable, machine-readable identifier for the failure.</param>
/// <param name="Message">A human-readable description of what went wrong.</param>
/// <param name="Metadata">Structured detail about the failure.</param>
public record Error<TMetadata>(ErrorCategory Category, string Code, string Message, TMetadata Metadata)
    : Error(Category, Code, Message);
