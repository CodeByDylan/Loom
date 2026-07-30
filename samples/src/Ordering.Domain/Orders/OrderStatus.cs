namespace Ordering.Domain.Orders;

/// <summary>
/// Where an order has reached. Stored as its name, so inserting a value cannot renumber the others.
/// </summary>
public enum OrderStatus
{
    /// <summary>Accepted and awaiting shipment. Every order starts here.</summary>
    Placed = 1,

    /// <summary>Called off before shipping. Terminal: a cancelled order cannot ship.</summary>
    Cancelled = 2,

    /// <summary>Sent to the customer. Terminal: a shipped order cannot be cancelled.</summary>
    Shipped = 3,
}
