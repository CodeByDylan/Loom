using FluentValidation;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Handlers.FluentValidation.Tests;

public sealed class ValidatingHandlerTests
{
    [Test]
    public async Task A_Valid_Request_Reaches_The_Handler()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: true);

        Result<string> result = await Handler(provider).HandleAsync(new Request("loom"), CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo("handled loom");
    }

    [Test]
    public async Task An_Invalid_Request_Never_Reaches_The_Handler()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: true);

        Result<string> result = await Handler(provider).HandleAsync(new Request(""), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(Recorder(provider).Invocations).IsEqualTo(0);
    }

    [Test]
    public async Task An_Invalid_Request_Fails_With_The_Invalid_Category()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: true);

        Result<string> result = await Handler(provider).HandleAsync(new Request(""), CancellationToken.None);

        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Invalid);
        await Assert.That(result.Error.Code).IsEqualTo(RequestValidation.ErrorCode);
    }

    [Test]
    public async Task Failures_Are_Grouped_By_Property()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: true);

        Result<string> result = await Handler(provider).HandleAsync(new Request(""), CancellationToken.None);

        // The metadata shape matches the ProblemDetails `errors` extension, so no translation is
        // needed at the transport boundary.
        var error = (ValidationError)result.Error;
        await Assert.That(error.Metadata.ContainsKey(nameof(Request.Name))).IsTrue();
        await Assert.That(error.Metadata[nameof(Request.Name)].Length).IsEqualTo(2);
    }

    [Test]
    public async Task A_Request_With_No_Validator_Passes_Through()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: false);

        Result<string> result = await Handler(provider).HandleAsync(new Request(""), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(Recorder(provider).Invocations).IsEqualTo(1);
    }

    [Test]
    public async Task Validation_Also_Applies_To_Handlers_Producing_No_Value()
    {
        await using ServiceProvider provider = BuildProvider(withValidator: true);

        IHandler<Request> handler = provider.GetRequiredService<IHandler<Request>>();
        Result result = await handler.HandleAsync(new Request(""), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo(RequestValidation.ErrorCode);
    }

    private static ServiceProvider BuildProvider(bool withValidator)
    {
        ServiceCollection services = new();

        // Invocations are recorded through an injected singleton rather than a static field: TUnit
        // runs tests in parallel, so shared mutable static state makes them interfere.
        services.AddSingleton<InvocationRecorder>();

        if (withValidator)
        {
            services.AddScoped<IValidator<Request>, RequestValidator>();
        }

        services.AddLoomHandlers(chain => chain.WithValidation())
            .AddHandler<SpyHandler, Request, string>()
            .AddHandler<CommandHandler, Request>();

        return services.BuildServiceProvider();
    }

    private static IHandler<Request, string> Handler(ServiceProvider provider) =>
        provider.GetRequiredService<IHandler<Request, string>>();

    private static InvocationRecorder Recorder(ServiceProvider provider) =>
        provider.GetRequiredService<InvocationRecorder>();

    internal sealed class InvocationRecorder
    {
        internal int Invocations { get; private set; }

        internal void Record() => Invocations++;
    }

    internal sealed record Request(string Name);

    internal sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty();
            RuleFor(request => request.Name).MinimumLength(2);
        }
    }

    internal sealed class SpyHandler(InvocationRecorder recorder) : IHandler<Request, string>
    {
        public Task<Result<string>> HandleAsync(Request request, CancellationToken cancellationToken)
        {
            recorder.Record();
            return Task.FromResult<Result<string>>($"handled {request.Name}");
        }
    }

    internal sealed class CommandHandler : IHandler<Request>
    {
        public Task<Result> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success);
    }
}
