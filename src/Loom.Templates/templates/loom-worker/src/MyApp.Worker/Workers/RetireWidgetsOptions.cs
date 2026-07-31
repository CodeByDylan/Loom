using System.ComponentModel.DataAnnotations;

namespace MyApp.Worker.Workers;

/// <summary>
/// How often widgets are retired, and which ones count as oversized.
/// </summary>
/// <remarks>
/// Both are deployment settings rather than facts about the domain, so neither belongs in the loop as
/// a constant. Validated at startup, so a misconfigured worker fails to boot rather than on its first
/// tick — which, on a five-minute interval, is a long way from the deployment that caused it.
/// </remarks>
internal sealed class RetireWidgetsOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "RetireWidgets";

    /// <summary>Gets how long to wait between passes.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the size above which a widget is retired.</summary>
    [Range(1, int.MaxValue)]
    public int LargerThan { get; init; } = 100;

    /// <summary>Gets the base wait before a transient failure is retried, doubling per attempt.</summary>
    /// <remarks>Zero disables waiting, which is what a test wants and a deployment never does.</remarks>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan RetryBackoff { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets how many widgets one pass may retire.</summary>
    /// <remarks>
    /// Bounded so a backlog is worked through over several ticks instead of loading every matching row
    /// into memory at once.
    /// </remarks>
    [Range(1, 10_000)]
    public int BatchSize { get; init; } = 500;
}
