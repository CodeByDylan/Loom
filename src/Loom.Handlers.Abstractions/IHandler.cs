using Loom.Results;

namespace Loom.Handlers;

/// <summary>
/// Handles one operation, producing a value on success.
/// </summary>
/// <typeparam name="TRequest">The type describing the operation to perform.</typeparam>
/// <typeparam name="TResponse">The type produced when the operation succeeds.</typeparam>
/// <remarks>
/// The response is a <see cref="Result{T}" /> so that a decorator can return a failure without
/// invoking the handler it wraps and without throwing. That is what makes short-circuiting
/// decorators — validation, authorization, rate limiting — expressible at all.
/// </remarks>
public interface IHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Performs the operation.
    /// </summary>
    /// <param name="request">The operation to perform.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The value produced, or the failure that prevented it.</returns>
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles one operation that produces no value.
/// </summary>
/// <typeparam name="TRequest">The type describing the operation to perform.</typeparam>
public interface IHandler<in TRequest>
{
    /// <summary>
    /// Performs the operation.
    /// </summary>
    /// <param name="request">The operation to perform.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the operation succeeded, or the failure that prevented it.</returns>
    Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
