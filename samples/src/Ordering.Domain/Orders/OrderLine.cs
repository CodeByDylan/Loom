using Loom.Entities;

namespace Ordering.Domain.Orders;

public sealed class OrderLine : Entity<OrderLine>
{
    internal OrderLine(Id<Order> orderId, string sku, int amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        OrderId = orderId;
        Sku = sku;
        Amount = amount;
    }

    private OrderLine()
    {
    }

    public Id<Order> OrderId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int Amount { get; private set; }
}
