using Microsoft.Extensions.DependencyInjection;

namespace Loom.Handlers;

/// <summary>
/// Registers Loom handlers into a service collection.
/// </summary>
public static class HandlerServiceCollectionExtensions
{
    /// <summary>
    /// Declares the decorator chain that wraps every handler, and returns the registrar used to add
    /// handlers.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configureChain">
    /// Declares the decorators, outermost first. Called immediately, before this method returns.
    /// </param>
    /// <returns>The registrar used to add handlers.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <example>
    /// <code>
    /// services.AddLoomHandlers(chain => chain.WithValidation())
    ///         .AddHandler&lt;CreateOrderHandler, CreateOrderRequest, CreateOrderResponse&gt;()
    ///         .AddHandler&lt;CancelOrderHandler, CancelOrderRequest&gt;();
    /// </code>
    /// </example>
    public static IHandlerRegistrar AddLoomHandlers(
        this IServiceCollection services,
        Action<IHandlerChainBuilder> configureChain)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureChain);

        HandlerChainBuilder chain = new();
        configureChain(chain);
        return new HandlerRegistrar(services, chain);
    }

    /// <summary>
    /// Returns the registrar used to add handlers, with no decorators.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The registrar used to add handlers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IHandlerRegistrar AddLoomHandlers(this IServiceCollection services) =>
        services.AddLoomHandlers(static _ => { });
}
