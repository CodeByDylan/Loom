using Loom.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loom.Handlers.Tests;

public class LoggingHandlerTests
{
    [Test]
    public async Task A_Success_Is_Recorded_At_Debug()
    {
        // Off by default in most configurations, on purpose: one line per successful operation is
        // noise at any real volume.
        Recorder recorder = await RunAsync(Result<string>.Success("done"));

        await Assert.That(recorder.Levels).IsEquivalentTo([LogLevel.Debug]);
    }

    [Test]
    [Arguments(ErrorCategory.Invalid)]
    [Arguments(ErrorCategory.NotFound)]
    [Arguments(ErrorCategory.Conflict)]
    [Arguments(ErrorCategory.Unauthorized)]
    [Arguments(ErrorCategory.Forbidden)]
    public async Task A_Refusal_Is_Recorded_At_Information(ErrorCategory category)
    {
        // Being refused is a normal outcome. Recording it as a warning would make ordinary traffic
        // look like trouble.
        Recorder recorder = await RunAsync(Result<string>.Failure(new Error(category, "a.b", "No.")));

        await Assert.That(recorder.Levels).IsEquivalentTo([LogLevel.Information]);
    }

    [Test]
    public async Task An_Unavailable_Dependency_Is_Recorded_At_Warning()
    {
        // The one category that says something about the system rather than the request.
        Recorder recorder = await RunAsync(
            Result<string>.Failure(Errors.Unavailable("carrier.down", "Unreachable.")));

        await Assert.That(recorder.Levels).IsEquivalentTo([LogLevel.Warning]);
    }

    [Test]
    public async Task A_Thrown_Exception_Is_Recorded_At_Error_And_Rethrown()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, _ => throw new InvalidOperationException("boom"));

        IHandler<Request, string> handler = provider.GetRequiredService<IHandler<Request, string>>();

        await Assert.That(async () => await handler.HandleAsync(new Request(), CancellationToken.None))
            .Throws<InvalidOperationException>();

        await Assert.That(recorder.Levels).IsEquivalentTo([LogLevel.Error]);
        await Assert.That(recorder.Entries.Single().Exception).IsNotNull();
    }

    [Test]
    public async Task The_Request_Type_Is_Recorded_And_The_Request_Is_Not()
    {
        // The whole point of having no option here: a request holds whatever a caller sent.
        Recorder recorder = await RunAsync(Result<string>.Success("done"));

        string message = recorder.Entries.Single().Message;

        await Assert.That(message).Contains(nameof(Request));
        await Assert.That(message).DoesNotContain("hunter2");
    }

    [Test]
    public async Task A_Failure_Records_Its_Code()
    {
        Recorder recorder = await RunAsync(
            Result<string>.Failure(Errors.Conflict("orders.already_shipped", "Already gone.")));

        await Assert.That(recorder.Entries.Single().Message).Contains("orders.already_shipped");
    }

    [Test]
    public async Task The_Outcome_Reaches_The_Caller_Unchanged()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, _ => Result<string>.Success("done"));

        Result<string> result = await provider
            .GetRequiredService<IHandler<Request, string>>()
            .HandleAsync(new Request(), CancellationToken.None);

        // Observing must not alter what is observed.
        await Assert.That(result.Value).IsEqualTo("done");
    }

    [Test]
    public async Task A_Handler_With_No_Response_Is_Recorded_Too()
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, _ => Result<string>.Success("done"));

        Result result = await provider
            .GetRequiredService<IHandler<Command>>()
            .HandleAsync(new Command(), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(recorder.Levels).IsEquivalentTo([LogLevel.Information]);
    }

    private static async Task<Recorder> RunAsync(Result<string> outcome)
    {
        Recorder recorder = new();
        await using ServiceProvider provider = Build(recorder, _ => outcome);

        _ = await provider
            .GetRequiredService<IHandler<Request, string>>()
            .HandleAsync(new Request(), CancellationToken.None);

        return recorder;
    }

    private static ServiceProvider Build(Recorder recorder, Func<Request, Result<string>> behaviour)
    {
        ServiceCollection services = new();
        services.AddSingleton(recorder);
        services.AddSingleton(behaviour);
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new RecordingLoggerProvider(recorder)));

        services.AddLoomHandlers(chain => chain.WithLogging())
            .AddHandler<ScriptedHandler, Request, string>()
            .AddHandler<RefusingCommandHandler, Command>();

        return services.BuildServiceProvider();
    }

    internal sealed record Request(string Secret = "hunter2");

    internal sealed record Command;

    internal sealed class ScriptedHandler(Func<Request, Result<string>> behaviour) : IHandler<Request, string>
    {
        public Task<Result<string>> HandleAsync(Request request, CancellationToken cancellationToken) =>
            Task.FromResult(behaviour(request));
    }

    internal sealed class RefusingCommandHandler : IHandler<Command>
    {
        public Task<Result> HandleAsync(Command request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(Errors.Conflict("command.refused", "No.")));
    }

    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    internal sealed class Recorder
    {
        private readonly List<LogEntry> _entries = [];

        internal IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_entries)
                {
                    return [.. _entries];
                }
            }
        }

        internal IReadOnlyList<LogLevel> Levels => [.. Entries.Select(entry => entry.Level)];

        internal void Add(LogEntry entry)
        {
            lock (_entries)
            {
                _entries.Add(entry);
            }
        }
    }

    private sealed class RecordingLoggerProvider(Recorder recorder) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(recorder);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(Recorder recorder) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);

                // Only the decorator's own entries: a container writes plenty of its own.
                if (eventId.Id is >= 1 and <= 4)
                {
                    recorder.Add(new LogEntry(logLevel, formatter(state, exception), exception));
                }
            }
        }
    }
}
