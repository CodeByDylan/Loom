using FluentValidation;
using FluentValidation.Results;
using Loom.Results;

namespace Loom.Handlers;

/// <summary>
/// Well-known values produced by the validating decorator.
/// </summary>
public static class RequestValidation
{
    /// <summary>
    /// The <see cref="Error.Code" /> carried by the failure returned when a request is invalid.
    /// </summary>
    public const string ErrorCode = "request.invalid";

    /// <summary>
    /// The <see cref="Error.Message" /> carried by the failure returned when a request is invalid.
    /// </summary>
    public const string ErrorMessage = "The request is invalid.";

    /// <summary>
    /// Runs every validator and describes the failures, if any.
    /// </summary>
    /// <typeparam name="TRequest">The type being validated.</typeparam>
    /// <param name="validators">
    /// The validators to run. An empty sequence means there is nothing to validate, which is not a
    /// failure.
    /// </param>
    /// <param name="request">The request to validate.</param>
    /// <param name="cancellationToken">Cancels validation.</param>
    /// <returns>
    /// A <see cref="ValidationError" /> describing every failure, or <see langword="null" /> if the
    /// request is valid.
    /// </returns>
    internal static async Task<Error?> FindFailureAsync<TRequest>(
        IEnumerable<IValidator<TRequest>> validators,
        TRequest request,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure>? failures = null;

        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is null)
        {
            return null;
        }

        var metadata = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return new ValidationError(ErrorCode, ErrorMessage, metadata);
    }
}
