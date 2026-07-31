using Loom.Specifications;

namespace MyApp.Domain.Widgets;

/// <summary>
/// Widgets above a given size, largest first.
/// </summary>
/// <remarks>
/// A named rule rather than a predicate inlined in the query that needs it, so the definition has one
/// home and reads the same wherever it is applied. Ordering belongs here because it is part of the
/// rule; paging does not, because that is the caller's decision.
/// </remarks>
public sealed class WidgetsLargerThan : Specification<Widget>
{
    public WidgetsLargerThan(int size)
    {
        Where(widget => widget.Size > size);
        OrderByDescending(widget => widget.Size);
    }
}
