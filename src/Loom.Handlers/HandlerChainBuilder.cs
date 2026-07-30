namespace Loom.Handlers;

internal sealed class HandlerChainBuilder : IHandlerChainBuilder
{
    private readonly List<Type> _withResponse = [];
    private readonly List<Type> _withoutResponse = [];

    internal IReadOnlyList<Type> WithResponse => _withResponse;

    internal IReadOnlyList<Type> WithoutResponse => _withoutResponse;

    public IHandlerChainBuilder Use(Type decoratorWithResponse, Type decoratorWithoutResponse)
    {
        EnsureDecorator(decoratorWithResponse, arity: 2, typeof(IHandler<,>), nameof(decoratorWithResponse));
        EnsureDecorator(decoratorWithoutResponse, arity: 1, typeof(IHandler<>), nameof(decoratorWithoutResponse));

        _withResponse.Add(decoratorWithResponse);
        _withoutResponse.Add(decoratorWithoutResponse);
        return this;
    }

    // Checked here rather than at resolution time: a malformed decorator should fail at startup with
    // a message naming the type, not on the first request with an InvalidCastException.
    private static void EnsureDecorator(Type type, int arity, Type handlerInterface, string paramName)
    {
        ArgumentNullException.ThrowIfNull(type, paramName);

        if (!type.IsGenericTypeDefinition || type.GetGenericArguments().Length != arity)
        {
            throw new ArgumentException(
                $"'{type}' must be an open generic type with {arity} type parameter(s).",
                paramName);
        }

        bool implementsHandler = type.GetInterfaces().Any(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface);

        if (!implementsHandler)
        {
            throw new ArgumentException($"'{type}' must implement '{handlerInterface}'.", paramName);
        }
    }
}
