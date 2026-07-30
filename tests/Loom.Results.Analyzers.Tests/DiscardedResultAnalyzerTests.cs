namespace Loom.Results.Analyzers.Tests;

public class DiscardedResultAnalyzerTests
{
    [Test]
    public async Task A_Discarded_Result_Is_Reported()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("subject.Cancel();"));

        await Assert.That(reported).IsEquivalentTo([DiscardedResultAnalyzer.DiagnosticId]);
    }

    [Test]
    public async Task A_Discarded_Result_Carrying_A_Value_Is_Reported()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("subject.Describe();"));

        await Assert.That(reported).IsEquivalentTo([DiscardedResultAnalyzer.DiagnosticId]);
    }

    [Test]
    public async Task An_Awaited_Result_That_Is_Discarded_Is_Reported()
    {
        // The case that matters most in practice: nearly every handler is asynchronous, and awaiting
        // makes the discard easy to miss because the line looks like work being done.
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("await subject.CancelAsync();"));

        await Assert.That(reported).IsEquivalentTo([DiscardedResultAnalyzer.DiagnosticId]);
    }

    [Test]
    public async Task An_Explicit_Discard_Is_Accepted()
    {
        // The documented way to say the outcome was considered and dismissed.
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("_ = subject.Cancel();"));

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task An_Explicitly_Discarded_Await_Is_Accepted()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("_ = await subject.CancelAsync();"));

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task An_Assigned_Result_Is_Accepted()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(
            Wrap("Result outcome = subject.Cancel(); if (outcome.IsFailure) { return; }"));

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task A_Result_Passed_As_An_Argument_Is_Accepted()
    {
        // Correct by construction today, since only expression statements are examined. Pinned so that
        // broadening what the analyzer looks at cannot start reporting a result that is plainly used.
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(
            Wrap("subject.Observe(subject.Cancel());"));

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task A_Returned_Result_Is_Accepted()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync("""
            using Loom.Results;

            public sealed class Subject
            {
                public Result Cancel() => Result.Success;
                public Result Run() => Cancel();
            }
            """);

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task A_Discarded_Value_Of_Another_Type_Is_Ignored()
    {
        // Deliberately narrow. The compiler's own unused-value rule covers everything and is switched
        // off everywhere for exactly that reason; this one earns its place by staying quiet.
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(Wrap("subject.Count();"));

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task Each_Discard_Is_Reported_Separately()
    {
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(
            Wrap("subject.Cancel(); subject.Cancel();"));

        await Assert.That(reported.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_Compilation_That_Has_Never_Heard_Of_A_Result_Is_Left_Alone()
    {
        // The reference is withheld, which is the only way to reach the analyzer's early exit. With it
        // present the types resolve however little the snippet mentions them, so a snippet that merely
        // avoids results would pass without that branch ever running.
        IReadOnlyList<string> reported = await AnalyzerHarness.RunAsync(
            """
            public sealed class Subject
            {
                public int Count() => 1;
                public void Run() => Count();
            }
            """,
            referenceResults: false);

        await Assert.That(reported).IsEmpty();
    }

    private static string Wrap(string statements) => $$"""
        using System.Threading.Tasks;
        using Loom.Results;

        public sealed class Subject
        {
            public Result Cancel() => Result.Success;
            public Result<string> Describe() => Result<string>.Success("x");
            public Task<Result> CancelAsync() => Task.FromResult(Result.Success);
            public int Count() => 1;
            public void Observe(Result outcome) { }
        }

        public static class Caller
        {
            public static async Task Run(Subject subject)
            {
                {{statements}}
                await Task.CompletedTask;
            }
        }
        """;
}
