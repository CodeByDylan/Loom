using Loom.Entities;
using Ordering.Domain.Orders;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// What an ordinary domain event handler wrote when an order was cancelled.
/// </summary>
/// <remarks>
/// Infrastructure rather than domain: nothing in the domain reads it, and it exists so the tests can
/// prove the handler's write joined the same transaction as the cancellation itself.
/// </remarks>
public sealed class CancellationRecord
{
    /// <summary>Gets this record's identifier.</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Gets the order that was cancelled.</summary>
    public Id<Order> OrderId { get; init; }

    /// <summary>Gets the order's total at the moment it was cancelled.</summary>
    public int Total { get; init; }
}
