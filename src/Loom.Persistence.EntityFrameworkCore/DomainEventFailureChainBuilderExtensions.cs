using Loom.Handlers;

namespace Loom.Persistence;

/// <summary>
/// Adds domain event failure translation to a handler decorator chain.
/// </summary>
public static class DomainEventFailureChainBuilderExtensions
{
    /// <summary>
    /// Reports a failure from a domain event handler as the handler's own failure, rather than letting
    /// the abandoned save escape as an exception.
    /// </summary>
    /// <param name="chain">The chain to add to.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chain" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// <strong>Declare this last, so it sits innermost.</strong> Decorators nest in declaration order,
    /// and anything outside this one is not the handler's save — an exception from a validator, say,
    /// would come back reported as a domain event failure, which is a confusing thing to diagnose.
    /// Innermost, it only translates failures from the save the handler itself performed.
    /// <example>
    /// <code>
    /// services.AddLoomHandlers(chain => chain
    ///     .WithValidation()
    ///     .WithDomainEventFailures());
    /// </code>
    /// </example>
    /// </remarks>
    public static IHandlerChainBuilder WithDomainEventFailures(this IHandlerChainBuilder chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return chain.Use(typeof(DomainEventFailureHandler<,>), typeof(DomainEventFailureHandler<>));
    }
}
