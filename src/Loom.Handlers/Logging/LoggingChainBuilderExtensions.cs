namespace Loom.Handlers;

/// <summary>
/// Adds logging to a handler decorator chain.
/// </summary>
public static class LoggingChainBuilderExtensions
{
    /// <summary>
    /// Records each handler's request type, outcome and duration.
    /// </summary>
    /// <param name="chain">The chain to add to.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chain" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// <strong>Declare this first, so it sits outermost.</strong> Decorators nest in declaration order,
    /// and anything declared before this one goes unrecorded — a request refused by validation would
    /// leave no trace at all, which is exactly the outcome most worth seeing.
    /// <para>
    /// The level follows the failure's category: a refused request is recorded as information, since
    /// being refused is a normal outcome, and only an unavailable dependency is recorded as a warning.
    /// A successful operation is recorded at debug, because one line per operation is noise at volume.
    /// </para>
    /// <para>
    /// The request's type name is recorded and the request itself never is. There is no option to
    /// change that: a request holds whatever a caller sent, and an option to log it is one that gets
    /// enabled in production. A project that truly wants payloads writes its own decorator and owns
    /// that decision.
    /// </para>
    /// <example>
    /// <code>
    /// services.AddLoomHandlers(chain => chain
    ///     .WithLogging()
    ///     .WithValidation()
    ///     .WithDomainEventFailures());
    /// </code>
    /// </example>
    /// </remarks>
    public static IHandlerChainBuilder WithLogging(this IHandlerChainBuilder chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return chain.Use(typeof(LoggingHandler<,>), typeof(LoggingHandler<>));
    }
}
