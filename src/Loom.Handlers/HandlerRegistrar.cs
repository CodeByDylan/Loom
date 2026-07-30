using Microsoft.Extensions.DependencyInjection;

namespace Loom.Handlers;

internal sealed class HandlerRegistrar(IServiceCollection services, HandlerChainBuilder chain) : IHandlerRegistrar
{
    public IServiceCollection Services => services;

    public IHandlerRegistrar AddHandler<THandler, TRequest, TResponse>()
        where THandler : class, IHandler<TRequest, TResponse>
    {
        // Snapshot the chain so that configuring it further afterwards cannot retroactively change
        // what this handler is wrapped in.
        Type[] decorators = [.. chain.WithResponse];

        services.AddScoped<THandler>();
        services.AddScoped<IHandler<TRequest, TResponse>>(serviceProvider =>
        {
            IHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<THandler>();

            // Built inside out, so that the first decorator declared ends up outermost.
            for (int i = decorators.Length - 1; i >= 0; i--)
            {
                Type closed = decorators[i].MakeGenericType(typeof(TRequest), typeof(TResponse));
                handler = (IHandler<TRequest, TResponse>)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    closed,
                    handler);
            }

            return handler;
        });

        return this;
    }

    public IHandlerRegistrar AddHandler<THandler, TRequest>()
        where THandler : class, IHandler<TRequest>
    {
        Type[] decorators = [.. chain.WithoutResponse];

        services.AddScoped<THandler>();
        services.AddScoped<IHandler<TRequest>>(serviceProvider =>
        {
            IHandler<TRequest> handler = serviceProvider.GetRequiredService<THandler>();

            for (int i = decorators.Length - 1; i >= 0; i--)
            {
                Type closed = decorators[i].MakeGenericType(typeof(TRequest));
                handler = (IHandler<TRequest>)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    closed,
                    handler);
            }

            return handler;
        });

        return this;
    }
}
