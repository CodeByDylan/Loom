using Loom.Handlers;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

namespace MyApp.Worker.Workers;

/// <summary>
/// One pass of the work: a scope, a dispatch, and what the outcome means.
/// </summary>
/// <remarks>
/// Separate from the worker that schedules it, because the two fail in different ways and are worth
/// testing apart. Everything here is decided by the result, so it can be exercised against a stub
/// handler with no clock and no timer — leaving the worker with only "tick, run, do not die".
/// </remarks>
internal sealed partial class RetireWidgetsPass(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<RetireWidgetsOptions> options,
    ILogger<RetireWidgetsPass> logger)
{
    private readonly RetireWidgetsOptions _options = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _options.MaximumAttempts; attempt++)
        {
            // A scope per attempt, never one held across them. A DbContext kept between attempts
            // accumulates tracked entities and eventually answers from a stale graph.
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();

            IHandler<Request, Response> handler = scope.ServiceProvider
                .GetRequiredService<IHandler<Request, Response>>();

            Result<Response> result = await handler
                .HandleAsync(new Request(_options.LargerThan, _options.BatchSize), cancellationToken);

            if (result.IsSuccess)
            {
                Retired(logger, result.Value.Retired);
                return;
            }

            // The category decides what happens next, which is the same decision a status code
            // expresses at an HTTP boundary. Only a dependency that might recover is worth retrying;
            // anything the caller got wrong will be just as wrong next time.
            if (result.Error.Category is ErrorCategory.NotFound)
            {
                // Nothing to do rather than something wrong: a scheduled pass finding no work is the
                // ordinary case on a quiet system, so it is recorded once and not raised as a failure.
                NothingToDo(logger, result.Error.Code);
                return;
            }

            if (result.Error.Category is not ErrorCategory.Unavailable)
            {
                DeadLettered(logger, result.Error.Code, result.Error.Message);
                return;
            }

            if (attempt == _options.MaximumAttempts)
            {
                GaveUp(logger, _options.MaximumAttempts, result.Error.Code);
                return;
            }

            await Task.Delay(Backoff(attempt), clock, cancellationToken);
        }
    }

    private TimeSpan Backoff(int attempt) => _options.RetryBackoff * Math.Pow(2, attempt - 1);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retired {Count} widget(s).")]
    private static partial void Retired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nothing to do: {Code}")]
    private static partial void NothingToDo(ILogger logger, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pass abandoned: {Code} {Reason}")]
    private static partial void DeadLettered(ILogger logger, string code, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gave up after {Attempts} attempts: {Code}")]
    private static partial void GaveUp(ILogger logger, int attempts, string code);
}
