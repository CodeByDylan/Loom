using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Loom.Results;

/// <summary>
/// Translates a result into an HTTP response.
/// </summary>
/// <remarks>
/// The one place a category becomes a status code. A slice never writes a status code itself, which is
/// what makes the closed category set worth having: the same six categories become status codes here,
/// exit codes in a command-line tool, and retry decisions in a worker.
/// <para>
/// There is deliberately no options type. <see cref="ToProblemDetails" /> is public, so a consumer who
/// wants different titles, a different member name for the code, or extra members builds one, changes
/// it, and passes it to <c>TypedResults.Problem</c> — three lines at one call site. An options type
/// would have to be registered, resolved, and threaded into extension methods that otherwise need no
/// request context at all.
/// </para>
/// </remarks>
public static class ResultHttpExtensions
{
    /// <summary>
    /// The member carrying the error's stable code.
    /// </summary>
    public const string CodeMember = "code";

    /// <summary>
    /// Maps a category to the status code that represents it.
    /// </summary>
    /// <param name="category">The category to map.</param>
    /// <returns>The status code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The category is not one of the known six.</exception>
    /// <remarks>
    /// Total over a closed set, and throws rather than falling back to 500 for an unrecognised value.
    /// A category that reaches here unmapped means the set grew without this being updated, and a quiet
    /// 500 would hide that.
    /// </remarks>
    public static int ToStatusCode(this ErrorCategory category) => Transport(category).Status;

    /// <summary>
    /// Describes an error as a problem details payload.
    /// </summary>
    /// <param name="error">The error to describe.</param>
    /// <returns>The payload, which the caller may change before returning it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error" /> is <see langword="null" />.</exception>
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        ProblemDetails problem = new()
        {
            Status = error.Category.ToStatusCode(),
            Title = TitleFor(error.Category),
            Detail = error.Message,
        };

        problem.Extensions[CodeMember] = error.Code;

        return problem;
    }

    /// <summary>
    /// Returns no content on success, or a problem describing the failure.
    /// </summary>
    /// <param name="result">The outcome to translate.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : Problem(result.Error);

    /// <summary>
    /// Returns the value on success, or a problem describing the failure.
    /// </summary>
    /// <typeparam name="T">The type of the value produced on success.</typeparam>
    /// <param name="result">The outcome to translate.</param>
    /// <param name="onSuccess">
    /// Builds the successful response, for an operation that should answer with something other than
    /// 200 — a creation answering 201, for instance. Defaults to 200 with the value as the body.
    /// </param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? TypedResults.Ok(result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        // A validation failure already carries a field-to-messages map, and that is exactly the shape
        // of the errors member, so it needs no translation. This is not configurable because it is not
        // a choice: the metadata was given that shape for this.
        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(validation.Metadata, StringComparer.Ordinal),
                detail: validation.Message,
                title: TitleFor(validation.Category),
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [CodeMember] = validation.Code,
                });
        }

        return TypedResults.Problem(error.ToProblemDetails());
    }

    private static string TitleFor(ErrorCategory category) => Transport(category).Title;

    /// <summary>What a category becomes on the wire: a status code and the title beside it.</summary>
    /// <remarks>
    /// One mapping rather than two switches over the same closed set. Two would let a category gain a
    /// status code and keep a stale title, and each would need its own reminder to stay total.
    /// </remarks>
    private static (int Status, string Title) Transport(ErrorCategory category) => category switch
    {
        ErrorCategory.Invalid => (StatusCodes.Status400BadRequest, "Invalid request"),
        ErrorCategory.Unauthorized => (StatusCodes.Status401Unauthorized, "Not authenticated"),
        ErrorCategory.Forbidden => (StatusCodes.Status403Forbidden, "Not permitted"),
        ErrorCategory.NotFound => (StatusCodes.Status404NotFound, "Not found"),
        ErrorCategory.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
        ErrorCategory.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Temporarily unavailable"),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unmapped error category."),
    };
}
