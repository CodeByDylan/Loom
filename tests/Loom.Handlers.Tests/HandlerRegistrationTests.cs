using Loom.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Handlers.Tests;

public sealed class HandlerRegistrationTests
{
    [Test]
    public async Task A_Handler_Resolves_With_No_Decorators()
    {
        await using ServiceProvider provider = BuildProvider(_ => { });

        IHandler<Request, string> handler = Resolve(provider);
        Result<string> result = await handler.HandleAsync(new Request(), CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo("handled");
    }

    [Test]
    public async Task A_Single_Decorator_Wraps_The_Handler()
    {
        await using ServiceProvider provider = BuildProvider(chain =>
            chain.Use(typeof(TagDecorator<,>.First), typeof(TagDecorator<>.First)));

        Result<string> result = await Resolve(provider).HandleAsync(new Request(), CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo("first(handled)");
    }

    [Test]
    public async Task The_First_Decorator_Declared_Is_Outermost()
    {
        await using ServiceProvider provider = BuildProvider(chain => chain
            .Use(typeof(TagDecorator<,>.First), typeof(TagDecorator<>.First))
            .Use(typeof(TagDecorator<,>.Second), typeof(TagDecorator<>.Second)));

        Result<string> result = await Resolve(provider).HandleAsync(new Request(), CancellationToken.None);

        // Reading order is execution order, as with ASP.NET Core middleware.
        await Assert.That(result.Value).IsEqualTo("first(second(handled))");
    }

    [Test]
    public async Task The_Chain_Also_Applies_To_Handlers_Producing_No_Value()
    {
        await using ServiceProvider provider = BuildProvider(chain =>
            chain.Use(typeof(ShortCircuitDecorator<,>), typeof(ShortCircuitDecorator<>)));

        IHandler<Request> handler = provider.GetRequiredService<IHandler<Request>>();
        Result result = await handler.HandleAsync(new Request(), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo("short.circuited");
    }

    [Test]
    public async Task A_Decorator_Can_Short_Circuit_Without_Invoking_The_Handler()
    {
        await using ServiceProvider provider = BuildProvider(chain =>
            chain.Use(typeof(ShortCircuitDecorator<,>), typeof(ShortCircuitDecorator<>)));

        Result<string> result = await Resolve(provider).HandleAsync(new Request(), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(provider.GetRequiredService<InvocationRecorder>().Invocations).IsEqualTo(0);
    }

    [Test]
    public async Task Handlers_Are_Scoped()
    {
        await using ServiceProvider provider = BuildProvider(_ => { });

        using (IServiceScope scope = provider.CreateScope())
        {
            IHandler<Request, string> first = scope.ServiceProvider.GetRequiredService<IHandler<Request, string>>();
            IHandler<Request, string> second = scope.ServiceProvider.GetRequiredService<IHandler<Request, string>>();
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
        }

        using IServiceScope other = provider.CreateScope();
        IHandler<Request, string> third = other.ServiceProvider.GetRequiredService<IHandler<Request, string>>();
        using IServiceScope another = provider.CreateScope();
        IHandler<Request, string> fourth = another.ServiceProvider.GetRequiredService<IHandler<Request, string>>();

        await Assert.That(ReferenceEquals(third, fourth)).IsFalse();
    }

    [Test]
    public async Task Registering_A_Handler_Does_Not_See_Chain_Changes_Made_Afterwards()
    {
        ServiceCollection services = new();
        IHandlerChainBuilder? captured = null;

        IHandlerRegistrar registrar = services.AddLoomHandlers(chain => captured = chain);
        registrar.AddHandler<Handler, Request, string>();
        captured!.Use(typeof(TagDecorator<,>.First), typeof(TagDecorator<>.First));

        await using ServiceProvider provider = services.BuildServiceProvider();
        Result<string> result = await Resolve(provider).HandleAsync(new Request(), CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo("handled");
    }

    [Test]
    public async Task A_Decorator_That_Is_Not_An_Open_Generic_Is_Rejected()
    {
        ServiceCollection services = new();

        await Assert.That(() => services.AddLoomHandlers(chain =>
                chain.Use(typeof(TagDecorator<Request, string>.First), typeof(TagDecorator<>.First))))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task A_Type_That_Is_Not_A_Handler_Is_Rejected()
    {
        ServiceCollection services = new();

        await Assert.That(() => services.AddLoomHandlers(chain =>
                chain.Use(typeof(NotADecorator), typeof(TagDecorator<>.First))))
            .Throws<ArgumentException>();
    }

    private static ServiceProvider BuildProvider(Action<IHandlerChainBuilder> configureChain)
    {
        ServiceCollection services = new();

        // Recorded through an injected singleton, not a static field: TUnit runs tests in parallel.
        services.AddSingleton<InvocationRecorder>();
        services.AddLoomHandlers(configureChain)
            .AddHandler<Handler, Request, string>()
            .AddHandler<CountingHandler, Request>();
        return services.BuildServiceProvider();
    }

    private static IHandler<Request, string> Resolve(ServiceProvider provider) =>
        provider.GetRequiredService<IHandler<Request, string>>();

    internal sealed record Request;

    internal sealed class Handler : IHandler<Request, string>
    {
        public Task<Result<string>> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<string>>("handled");
    }

    internal sealed class InvocationRecorder
    {
        internal int Invocations { get; private set; }

        internal void Record() => Invocations++;
    }

    internal sealed class CountingHandler(InvocationRecorder recorder) : IHandler<Request>
    {
        public Task<Result> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            recorder.Record();
            return Task.FromResult(Result.Success);
        }
    }

    // Nested types keep two distinctly-named decorators available at each arity without inventing
    // four top-level classes.
    internal static class TagDecorator<TRequest, TResponse>
    {
        internal sealed class First(IHandler<TRequest, TResponse> inner) : IHandler<TRequest, TResponse>
        {
            public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct) =>
                Tag("first", await inner.HandleAsync(request, ct));
        }

        internal sealed class Second(IHandler<TRequest, TResponse> inner) : IHandler<TRequest, TResponse>
        {
            public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct) =>
                Tag("second", await inner.HandleAsync(request, ct));
        }

        private static Result<TResponse> Tag(string name, Result<TResponse> result) =>
            result.IsFailure
                ? result
                : (TResponse)(object)$"{name}({result.Value})";
    }

    internal static class TagDecorator<TRequest>
    {
        internal sealed class First(IHandler<TRequest> inner) : IHandler<TRequest>
        {
            public Task<Result> HandleAsync(TRequest request, CancellationToken ct) =>
                inner.HandleAsync(request, ct);
        }

        internal sealed class Second(IHandler<TRequest> inner) : IHandler<TRequest>
        {
            public Task<Result> HandleAsync(TRequest request, CancellationToken ct) =>
                inner.HandleAsync(request, ct);
        }
    }

    internal sealed class ShortCircuitDecorator<TRequest, TResponse> : IHandler<TRequest, TResponse>
    {
        // Accepts the inner handler and deliberately never invokes it — that is what this decorator
        // exists to prove.
        public ShortCircuitDecorator(IHandler<TRequest, TResponse> inner) => _ = inner;

        public Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct) =>
            Task.FromResult(Result<TResponse>.Failure(Errors.Conflict("short.circuited", "Stopped.")));
    }

    internal sealed class ShortCircuitDecorator<TRequest> : IHandler<TRequest>
    {
        public ShortCircuitDecorator(IHandler<TRequest> inner) => _ = inner;

        public Task<Result> HandleAsync(TRequest request, CancellationToken ct) =>
            Task.FromResult(Result.Failure(Errors.Conflict("short.circuited", "Stopped.")));
    }

    internal sealed class NotADecorator;
}
