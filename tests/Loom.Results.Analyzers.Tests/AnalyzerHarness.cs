using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Loom.Results.Analyzers.Tests;

/// <summary>
/// Compiles a snippet against the real result types and runs the analyzer over it.
/// </summary>
/// <remarks>
/// Hand-rolled rather than taken from the analyzer testing packages, which come in xunit, NUnit and
/// MSTest flavours — all three banned here — and whose framework-agnostic base would still need a
/// verifier written by hand. This is smaller than that adapter would be and owes nothing to a second
/// test framework.
/// </remarks>
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences(withResults: true);
    private static readonly ImmutableArray<MetadataReference> WithoutResults = BuildReferences(withResults: false);

    /// <summary>
    /// Returns the diagnostic identifiers the analyzer reports for the snippet, with their lines.
    /// </summary>
    /// <param name="snippet">The code to analyse.</param>
    /// <param name="referenceResults">
    /// Whether the compilation knows about the result types at all. Omitting them is the only way to
    /// reach the analyzer's early exit: with the reference present the types resolve regardless of what
    /// the snippet mentions, so a snippet that merely avoids them proves nothing about that branch.
    /// </param>
    internal static async Task<IReadOnlyList<string>> RunAsync(string snippet, bool referenceResults = true)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Analysed",
            syntaxTrees: [CSharpSyntaxTree.ParseText(snippet)],
            references: referenceResults ? References : WithoutResults,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // A snippet that does not compile would make any diagnostic meaningless, so that is checked
        // first and reported as itself.
        string[] errors =
        [
            .. compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
                .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}"),
        ];

        if (errors.Length > 0)
        {
            throw new InvalidOperationException("The snippet does not compile: " + string.Join("; ", errors));
        }

        ImmutableArray<Diagnostic> reported = await compilation
            .WithAnalyzers([new DiscardedResultAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        return
        [
            .. reported
                .OrderBy(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
                .Select(diagnostic => diagnostic.Id),
        ];
    }

    private static ImmutableArray<MetadataReference> BuildReferences(bool withResults)
    {
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(Path.Combine(runtime, "System.Private.CoreLib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtime, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtime, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtime, "System.Threading.Tasks.dll")),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location),
        ];

        if (withResults)
        {
            // The real package, so the analyzer matches the types it will actually meet.
            references.Add(MetadataReference.CreateFromFile(typeof(Loom.Results.Result).Assembly.Location));
        }

        return [.. references];
    }
}
