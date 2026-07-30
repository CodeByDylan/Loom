using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Loom.Results.Analyzers;

/// <summary>
/// Reports a result that is computed and thrown away.
/// </summary>
/// <remarks>
/// Returning failure instead of throwing it buys a great deal, and costs exactly one thing: a value
/// can be ignored, and nothing about ignoring it looks wrong. This is the check that closes that gap.
/// <para>
/// The compiler's own unused-value rule cannot be used for it. That rule fires on every fluent call
/// and every builder, so it is switched off in most codebases — including this one — which leaves
/// results uncovered along with everything else. This one looks only at results.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The identifier of the diagnostic this reports.
    /// </summary>
    public const string DiagnosticId = "LOOM0001";

    private const string ResultMetadataName = "Loom.Results.Result";
    private const string ResultOfTMetadataName = "Loom.Results.Result`1";

    // A warning rather than an error. Anyone following Loom's guidance builds with warnings as
    // errors, so it fails their build anyway; shipping it as an error would make it something to
    // switch off wholesale rather than something to raise deliberately.
    private static readonly DiagnosticDescriptor DiscardedResult = new(
        DiagnosticId,
        title: "A result is discarded",
        messageFormat: "This {0} is discarded, so a failure would go unnoticed",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Assign the result, return it, or write '_ =' to say the outcome is deliberately ignored.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiscardedResult);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            INamedTypeSymbol? result = start.Compilation.GetTypeByMetadataName(ResultMetadataName);
            INamedTypeSymbol? resultOfT = start.Compilation.GetTypeByMetadataName(ResultOfTMetadataName);

            // Nothing to say about a compilation that has never heard of a result.
            if (result is null && resultOfT is null)
            {
                return;
            }

            start.RegisterOperationAction(
                operation => Analyze(operation, result, resultOfT),
                OperationKind.ExpressionStatement);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol? result,
        INamedTypeSymbol? resultOfT)
    {
        IOperation value = ((IExpressionStatementOperation)context.Operation).Operation;

        // An explicit discard is the documented way to say the outcome was considered and dismissed.
        if (value is ISimpleAssignmentOperation { Target.Kind: OperationKind.Discard })
        {
            return;
        }

        // Awaiting is transparent here: the operation's type is already what the await produced, so
        // `await handler.HandleAsync(...)` on its own reports just as `order.Cancel()` does.
        ITypeSymbol? type = value.Type;

        if (type is null || !IsResult(type, result, resultOfT))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiscardedResult,
            value.Syntax.GetLocation(),
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsResult(ITypeSymbol type, INamedTypeSymbol? result, INamedTypeSymbol? resultOfT)
    {
        if (result is not null && SymbolEqualityComparer.Default.Equals(type, result))
        {
            return true;
        }

        return resultOfT is not null
            && type is INamedTypeSymbol { IsGenericType: true } named
            && SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, resultOfT);
    }
}
