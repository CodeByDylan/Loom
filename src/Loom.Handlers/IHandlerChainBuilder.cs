namespace Loom.Handlers;

/// <summary>
/// Declares the decorators that wrap every registered handler.
/// </summary>
/// <remarks>
/// Decorators nest in declaration order: the first one declared is the outermost, and runs first.
/// This matches ASP.NET Core middleware, so reading order is execution order.
/// <para>
/// The chain is declared once and applies to every handler, so a handler cannot accidentally be
/// registered without it. There is deliberately no per-handler override and no assembly scanning
/// for decorators: reading the single <c>AddLoomHandlers</c> call must tell you exactly what wraps
/// any given handler.
/// </para>
/// </remarks>
public interface IHandlerChainBuilder
{
    /// <summary>
    /// Adds a decorator to the chain, inside everything already declared.
    /// </summary>
    /// <param name="decoratorWithResponse">
    /// An open generic type with two type parameters implementing
    /// <see cref="IHandler{TRequest, TResponse}" />, such as <c>typeof(MyDecorator&lt;,&gt;)</c>.
    /// </param>
    /// <param name="decoratorWithoutResponse">
    /// An open generic type with one type parameter implementing <see cref="IHandler{TRequest}" />,
    /// such as <c>typeof(MyDecorator&lt;&gt;)</c>.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Either type is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// Either type is not an open generic type of the expected arity, or does not implement the
    /// corresponding handler interface.
    /// </exception>
    IHandlerChainBuilder Use(Type decoratorWithResponse, Type decoratorWithoutResponse);
}
