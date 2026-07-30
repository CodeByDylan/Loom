using Loom.Handlers;
using Loom.Results;

namespace Loom.Persistence;

/// <summary>
/// Turns an abandoned save back into the failure a domain event handler reported.
/// </summary>
/// <remarks>
/// The failure was never exceptional. A handler <em>returned</em> it; the exception exists only
/// because an object-relational mapper offers no way to abandon a save other than throwing. So it is
/// converted back at the earliest point, and the caller sees the outcome it would have seen if the
/// failure had come from the handler directly.
/// <para>
/// Only <see cref="DomainEventDispatchException" /> is caught. A concurrency conflict, a constraint
/// violation or a lost connection are all genuinely exceptional and are left alone.
/// </para>
/// </remarks>
internal sealed class DomainEventFailureHandler<TRequest, TResponse>(IHandler<TRequest, TResponse> inner)
    : IHandler<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (DomainEventDispatchException failed)
        {
            return Result<TResponse>.Failure(failed.Error);
        }
    }
}

/// <inheritdoc cref="DomainEventFailureHandler{TRequest, TResponse}" />
internal sealed class DomainEventFailureHandler<TRequest>(IHandler<TRequest> inner) : IHandler<TRequest>
{
    public async Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.HandleAsync(request, cancellationToken);
        }
        catch (DomainEventDispatchException failed)
        {
            return Result.Failure(failed.Error);
        }
    }
}
