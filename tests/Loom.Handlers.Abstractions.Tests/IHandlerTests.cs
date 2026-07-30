using Loom.Results;

namespace Loom.Handlers.Abstractions.Tests;

public class IHandlerTests
{
    [Test]
    public async Task A_Handler_Returning_A_Value_Reports_Success()
    {
        IHandler<string, int> handler = new LengthHandler();

        Result<int> result = await handler.HandleAsync("loom", CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo(4);
    }

    [Test]
    public async Task A_Handler_Can_Return_A_Failure_Without_Throwing()
    {
        IHandler<string, int> handler = new LengthHandler();

        Result<int> result = await handler.HandleAsync(string.Empty, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Invalid);
    }

    [Test]
    public async Task A_Handler_Producing_No_Value_Reports_Success()
    {
        IHandler<string> handler = new RecordingHandler();

        Result result = await handler.HandleAsync("loom", CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task The_Request_Type_Is_Contravariant()
    {
        // `in TRequest` lets a handler written against a general request serve a more specific one,
        // which is what allows a single decorator to wrap many handlers.
        IHandler<object, string> general = new DescribingHandler();
        IHandler<string, string> specific = general;

        Result<string> result = await specific.HandleAsync("loom", CancellationToken.None);

        await Assert.That(result.Value).IsEqualTo("loom");
    }

    private sealed class LengthHandler : IHandler<string, int>
    {
        public Task<Result<int>> HandleAsync(string request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<int>>(
                request.Length is 0
                    ? Errors.Invalid("request.empty", "The request was empty.")
                    : request.Length);
    }

    private sealed class RecordingHandler : IHandler<string>
    {
        public Task<Result> HandleAsync(string request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success);
    }

    private sealed class DescribingHandler : IHandler<object, string>
    {
        public Task<Result<string>> HandleAsync(object request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<string>>(request.ToString()!);
    }
}
