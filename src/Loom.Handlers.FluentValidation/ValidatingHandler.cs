using FluentValidation;
using Loom.Results;

namespace Loom.Handlers;

/// <remarks>
/// Validators are injected as a sequence rather than as a single optional dependency, because a
/// container always resolves an empty sequence when nothing is registered. A request with no
/// validator is therefore a no-op rather than a resolution failure.
/// </remarks>
internal sealed class ValidatingHandler<TRequest, TResponse>(
    IHandler<TRequest, TResponse> inner,
    IEnumerable<IValidator<TRequest>> validators) : IHandler<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        Error? failure = await RequestValidation.FindFailureAsync(validators, request, cancellationToken);

        // Short-circuits without invoking the inner handler and without throwing. This is the reason
        // Result appears in the handler signature.
        return failure is not null
            ? Result<TResponse>.Failure(failure)
            : await inner.HandleAsync(request, cancellationToken);
    }
}

/// <inheritdoc cref="ValidatingHandler{TRequest, TResponse}" />
internal sealed class ValidatingHandler<TRequest>(
    IHandler<TRequest> inner,
    IEnumerable<IValidator<TRequest>> validators) : IHandler<TRequest>
{
    public async Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        Error? failure = await RequestValidation.FindFailureAsync(validators, request, cancellationToken);

        return failure is not null
            ? Result.Failure(failure)
            : await inner.HandleAsync(request, cancellationToken);
    }
}
