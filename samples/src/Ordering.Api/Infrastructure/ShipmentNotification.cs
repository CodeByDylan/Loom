using Loom.Entities;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// What a deferred domain event handler wrote after a shipment's transaction committed.
/// </summary>
/// <remarks>
/// Stands in for a message actually leaving the system. Deferred delivery is at least once, so this
/// carries a unique index on the order — the handler's own check is a fast path, and the constraint is
/// what makes one notification per order true.
/// </remarks>
public sealed class ShipmentNotification
{
    /// <summary>Gets this notification's identifier.</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Gets the order that shipped. Unique, so a redelivery cannot notify twice.</summary>
    public Id<Order> OrderId { get; init; }

    /// <summary>Gets the customer who would be told.</summary>
    public Id<Customer> CustomerId { get; init; }
}
