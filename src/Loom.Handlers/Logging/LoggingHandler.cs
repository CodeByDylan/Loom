using System.Diagnostics;
using Loom.Results;
using Microsoft.Extensions.Logging;

namespace Loom.Handlers;

/// <summary>
/// Records what a handler was asked to do and how it went.
/// </summary>
/// <remarks>
/// The level follows the error category, which is the point of that set being closed and semantic: the
/// same six categories already become status codes at an HTTP boundary, exit codes in a command-line
/// tool, and retry decisions in a worker. Here they decide how loud a failure is.
/// </remarks>
internal sealed class LoggingHandler<TRequest, TResponse>(
    IHandler<TRequest, TResponse> inner,
    ILogger<LoggingHandler<TRequest, TResponse>> logger) : IHandler<TRequest, TResponse>
{
    // The full name, not the short one, and computed once per closed generic. Slice contracts are
    // private to their slice, so in a codebase following that rule every request type is called
    // "Request" — a short name would identify every operation in the application identically.
    private static readonly string RequestName = HandlerLog.NameOf<TRequest>();

    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            Result<TResponse> result = await inner.HandleAsync(request, cancellationToken);

            HandlerLog.Record(
                logger,
                RequestName,
                result.IsFailure ? result.Error : null,
                HandlerLog.ElapsedMilliseconds(startedAt));

            return result;
        }
        catch (Exception exception)
        {
            HandlerLog.Threw(logger, RequestName, HandlerLog.ElapsedMilliseconds(startedAt), exception);
            throw;
        }
    }
}

/// <inheritdoc cref="LoggingHandler{TRequest, TResponse}" />
internal sealed class LoggingHandler<TRequest>(
    IHandler<TRequest> inner,
    ILogger<LoggingHandler<TRequest>> logger) : IHandler<TRequest>
{
    private static readonly string RequestName = HandlerLog.NameOf<TRequest>();

    public async Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            Result result = await inner.HandleAsync(request, cancellationToken);

            HandlerLog.Record(
                logger,
                RequestName,
                result.IsFailure ? result.Error : null,
                HandlerLog.ElapsedMilliseconds(startedAt));

            return result;
        }
        catch (Exception exception)
        {
            HandlerLog.Threw(logger, RequestName, HandlerLog.ElapsedMilliseconds(startedAt), exception);
            throw;
        }
    }
}
