using Microsoft.Extensions.DependencyInjection;

namespace Loom.Handlers;

/// <summary>
/// Registers handlers, wrapping each in the decorator chain declared alongside it.
/// </summary>
/// <remarks>
/// Obtained from <see cref="HandlerServiceCollectionExtensions.AddLoomHandlers(IServiceCollection, Action{IHandlerChainBuilder})" />.
/// Registration goes through this type rather than through <see cref="IServiceCollection" /> directly
/// so that a handler cannot be registered before the chain has been declared.
/// </remarks>
public interface IHandlerRegistrar
{
    /// <summary>
    /// Gets the service collection being populated, for registrations unrelated to handlers.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers a handler that produces a value.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <typeparam name="TRequest">The type describing the operation.</typeparam>
    /// <typeparam name="TResponse">The type produced on success.</typeparam>
    /// <returns>The same registrar, for chaining.</returns>
    IHandlerRegistrar AddHandler<THandler, TRequest, TResponse>()
        where THandler : class, IHandler<TRequest, TResponse>;

    /// <summary>
    /// Registers a handler that produces no value.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <typeparam name="TRequest">The type describing the operation.</typeparam>
    /// <returns>The same registrar, for chaining.</returns>
    IHandlerRegistrar AddHandler<THandler, TRequest>()
        where THandler : class, IHandler<TRequest>;
}
