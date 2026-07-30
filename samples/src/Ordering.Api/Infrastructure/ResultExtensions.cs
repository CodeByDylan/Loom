using Loom.Results;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// The one place a Loom error category becomes an HTTP status code.
/// </summary>
/// <remarks>
/// A slice never writes a status code itself. Keeping the mapping in one place is what makes the
/// category set worth having: the same six categories become status codes here, exit codes in a
/// command-line tool, and retry decisions in a worker.
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value)
            : Problem(result.Error);

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        // A validation failure already carries a field-to-messages map, which is exactly the shape of
        // the problem details errors extension, so it needs no translation.
        if (error is ValidationError validation)
        {
            return Results.ValidationProblem(
                validation.Metadata,
                detail: validation.Message,
                extensions: Extensions(validation));
        }

        return Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Category),
            title: TitleFor(error.Category),
            extensions: Extensions(error));
    }

    private static Dictionary<string, object?> Extensions(Error error) =>
        new(StringComparer.Ordinal) { ["code"] = error.Code };

    private static int StatusCodeFor(ErrorCategory category) => category switch
    {
        ErrorCategory.Invalid => StatusCodes.Status400BadRequest,
        ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategory.NotFound => StatusCodes.Status404NotFound,
        ErrorCategory.Conflict => StatusCodes.Status409Conflict,
        ErrorCategory.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unmapped error category."),
    };

    private static string TitleFor(ErrorCategory category) => category switch
    {
        ErrorCategory.Invalid => "Invalid request",
        ErrorCategory.Unauthorized => "Not authenticated",
        ErrorCategory.Forbidden => "Not permitted",
        ErrorCategory.NotFound => "Not found",
        ErrorCategory.Conflict => "Conflict",
        ErrorCategory.Unavailable => "Temporarily unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unmapped error category."),
    };
}
