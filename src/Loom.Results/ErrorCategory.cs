namespace Loom.Results;

/// <summary>
/// The semantic kind of a failure.
/// </summary>
/// <remarks>
/// These categories are deliberately transport-agnostic, so that one category can become an HTTP
/// status code in an API, a retry-or-dead-letter decision in a worker, and an exit code in a CLI.
/// <para>
/// The set is closed. A seventh category would mean this enum has become a status-code list; carry
/// case-specific detail as metadata on <see cref="Error{TMetadata}" /> instead.
/// </para>
/// </remarks>
public enum ErrorCategory
{
    /// <summary>The input was malformed, out of range, or internally inconsistent.</summary>
    Invalid = 1,

    /// <summary>The requested thing does not exist.</summary>
    NotFound = 2,

    /// <summary>The request conflicts with the current state, so it cannot be applied.</summary>
    Conflict = 3,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized = 4,

    /// <summary>The caller is authenticated but not permitted to do this.</summary>
    Forbidden = 5,

    /// <summary>A dependency failed or is temporarily unavailable. Retrying may succeed.</summary>
    Unavailable = 6,
}
