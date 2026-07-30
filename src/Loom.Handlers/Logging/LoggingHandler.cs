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
    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            Result<TResponse> result = await inner.HandleAsync(request, cancellationToken);

            HandlerLog.Record(
                logger,
                typeof(TRequest).Name,
                result.IsFailure ? result.Error : null,
                HandlerLog.ElapsedMilliseconds(startedAt));

            return result;
        }
        catch (Exception exception)
        {
            HandlerLog.Threw(logger, typeof(TRequest).Name, HandlerLog.ElapsedMilliseconds(startedAt), exception);
            throw;
        }
    }
}

/// <inheritdoc cref="LoggingHandler{TRequest, TResponse}" />
internal sealed class LoggingHandler<TRequest>(
    IHandler<TRequest> inner,
    ILogger<LoggingHandler<TRequest>> logger) : IHandler<TRequest>
{
    public async Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            Result result = await inner.HandleAsync(request, cancellationToken);

            HandlerLog.Record(
                logger,
                typeof(TRequest).Name,
                result.IsFailure ? result.Error : null,
                HandlerLog.ElapsedMilliseconds(startedAt));

            return result;
        }
        catch (Exception exception)
        {
            HandlerLog.Threw(logger, typeof(TRequest).Name, HandlerLog.ElapsedMilliseconds(startedAt), exception);
            throw;
        }
    }
}
