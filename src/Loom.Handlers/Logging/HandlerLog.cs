using System.Diagnostics;
using Loom.Results;
using Microsoft.Extensions.Logging;

namespace Loom.Handlers;

/// <summary>
/// The log messages the logging decorator writes, and the choice of which one to write.
/// </summary>
/// <remarks>
/// Not generic, and separate from the decorator that calls it, so the source generator only ever sees
/// a plain static partial class. It also puts the level-per-outcome decision in one place, shared by
/// both handler shapes rather than written twice.
/// <para>
/// A request's type name is logged; the request itself never is. A request carries whatever a caller
/// sent — passwords, tokens, personal data — and there is deliberately no option to include it, because
/// an option like that gets switched on in production to debug something.
/// </para>
/// </remarks>
internal static partial class HandlerLog
{
    internal static double ElapsedMilliseconds(long startedAt) =>
        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    /// <summary>
    /// Writes the one line that describes this outcome.
    /// </summary>
    /// <param name="logger">Where to write.</param>
    /// <param name="request">The request's type name, never the request.</param>
    /// <param name="failure">The failure, or <see langword="null" /> if the operation succeeded.</param>
    /// <param name="elapsedMilliseconds">How long the handler took.</param>
    internal static void Record(ILogger logger, string request, Error? failure, double elapsedMilliseconds)
    {
        if (failure is null)
        {
            Succeeded(logger, request, elapsedMilliseconds);
            return;
        }

        // The category decides how loud this is. A refused request is a normal outcome; only one
        // category says something about the system rather than the request.
        if (failure.Category is ErrorCategory.Unavailable)
        {
            Unavailable(logger, request, failure.Code, elapsedMilliseconds);
            return;
        }

        Refused(logger, request, failure.Category, failure.Code, elapsedMilliseconds);
    }

    /// <summary>
    /// A per-operation success line is noise at any real volume, so it is off unless asked for.
    /// </summary>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "{Request} succeeded in {ElapsedMilliseconds}ms")]
    internal static partial void Succeeded(ILogger logger, string request, double elapsedMilliseconds);

    /// <summary>
    /// A refused request is a normal outcome, not a defect. Logging it as a warning would make
    /// ordinary traffic look like trouble.
    /// </summary>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "{Request} was refused as {Category} ({Code}) in {ElapsedMilliseconds}ms")]
    internal static partial void Refused(
        ILogger logger,
        string request,
        ErrorCategory category,
        string code,
        double elapsedMilliseconds);

    /// <summary>
    /// The one category that implicates the system rather than the request.
    /// </summary>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "{Request} could not be served ({Code}) in {ElapsedMilliseconds}ms")]
    internal static partial void Unavailable(
        ILogger logger,
        string request,
        string code,
        double elapsedMilliseconds);

    /// <summary>
    /// Logged and rethrown. An application host usually logs unhandled exceptions too, so this can
    /// duplicate — worth it, because a worker or a command-line tool may have nothing else doing so,
    /// and naming the handler is what makes the entry useful.
    /// </summary>
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "{Request} threw after {ElapsedMilliseconds}ms")]
    internal static partial void Threw(
        ILogger logger,
        string request,
        double elapsedMilliseconds,
        Exception exception);
}
