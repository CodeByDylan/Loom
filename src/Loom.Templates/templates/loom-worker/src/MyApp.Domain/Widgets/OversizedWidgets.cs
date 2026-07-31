using Loom.Specifications;

namespace MyApp.Domain.Widgets;

/// <summary>
/// Widgets still in service and larger than a given size.
/// </summary>
/// <remarks>
/// A named rule rather than a predicate inlined in the query that needs it, so the definition of
/// "oversized" has one home and reads the same wherever it is applied.
/// </remarks>
public sealed class OversizedWidgets : Specification<Widget>
{
    public OversizedWidgets(int largerThan) =>
        Where(widget => !widget.IsRetired && widget.Size > largerThan);
}
