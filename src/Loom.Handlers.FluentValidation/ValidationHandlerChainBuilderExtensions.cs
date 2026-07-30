namespace Loom.Handlers;

/// <summary>
/// Adds FluentValidation to a handler decorator chain.
/// </summary>
public static class ValidationHandlerChainBuilderExtensions
{
    /// <summary>
    /// Validates requests before the handler runs, returning an
    /// <see cref="Loom.Results.ErrorCategory.Invalid" /> failure instead of invoking it.
    /// </summary>
    /// <param name="chain">The chain to add to.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chain" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// A request with no registered <c>IValidator&lt;TRequest&gt;</c> passes through untouched. That
    /// keeps handlers with nothing to validate from needing a stub validator, at the cost that a
    /// mistyped validator registration disables validation silently rather than loudly.
    /// </remarks>
    public static IHandlerChainBuilder WithValidation(this IHandlerChainBuilder chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return chain.Use(typeof(ValidatingHandler<,>), typeof(ValidatingHandler<>));
    }
}
