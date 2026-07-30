namespace Loom.Results;

/// <summary>
/// Creates <see cref="Error" /> values, one factory per <see cref="ErrorCategory" />.
/// </summary>
public static class Errors
{
    /// <summary>Creates an <see cref="ErrorCategory.Invalid" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error Invalid(string code, string message) => Create(ErrorCategory.Invalid, code, message);

    /// <summary>Creates a <see cref="ErrorCategory.NotFound" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error NotFound(string code, string message) => Create(ErrorCategory.NotFound, code, message);

    /// <summary>Creates a <see cref="ErrorCategory.Conflict" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error Conflict(string code, string message) => Create(ErrorCategory.Conflict, code, message);

    /// <summary>Creates an <see cref="ErrorCategory.Unauthorized" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error Unauthorized(string code, string message) => Create(ErrorCategory.Unauthorized, code, message);

    /// <summary>Creates a <see cref="ErrorCategory.Forbidden" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error Forbidden(string code, string message) => Create(ErrorCategory.Forbidden, code, message);

    /// <summary>Creates an <see cref="ErrorCategory.Unavailable" /> error.</summary>
    /// <param name="code">A stable, machine-readable identifier for the failure.</param>
    /// <param name="message">A human-readable description of what went wrong.</param>
    /// <returns>The created error.</returns>
    public static Error Unavailable(string code, string message) => Create(ErrorCategory.Unavailable, code, message);

    private static Error Create(ErrorCategory category, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Error(category, code, message);
    }
}
