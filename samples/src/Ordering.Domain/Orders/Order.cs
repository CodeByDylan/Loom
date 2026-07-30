using Loom.Entities;
using Loom.Results;
using Ordering.Domain.Customers;

namespace Ordering.Domain.Orders;

public sealed class Order : AggregateRoot<Order>
{
    private readonly List<OrderLine> _lines = [];

    private Order(Id<Customer> customerId, DateOnly placedOn)
    {
        CustomerId = customerId;
        PlacedOn = placedOn;
    }

    private Order()
    {
    }

    public Id<Customer> CustomerId { get; private set; }

    public DateOnly PlacedOn { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Placed;

    public int Total => _lines.Sum(line => line.Amount);

    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>
    /// Places an order. Construction is a decision, so it returns a result rather than throwing.
    /// </summary>
    public static Result<Order> Place(Id<Customer> customerId, DateOnly placedOn, IReadOnlyList<(string Sku, int Amount)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count is 0)
        {
            return OrderErrors.NoLines;
        }

        Order order = new(customerId, placedOn);

        foreach ((string sku, int amount) in lines)
        {
            order._lines.Add(new OrderLine(order.Id, sku, amount));
        }

        return order;
    }

    /// <summary>
    /// Cancels the order, if its current state permits it.
    /// </summary>
    public Result Cancel()
    {
        if (Status is OrderStatus.Shipped)
        {
            return OrderErrors.AlreadyShipped;
        }

        if (Status is OrderStatus.Cancelled)
        {
            return OrderErrors.AlreadyCancelled;
        }

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id, Total));
        return Result.Success;
    }

    /// <summary>
    /// Ships the order, if its current state permits it.
    /// </summary>
    public Result Ship()
    {
        if (Status is OrderStatus.Cancelled)
        {
            return OrderErrors.AlreadyCancelled;
        }

        if (Status is OrderStatus.Shipped)
        {
            return OrderErrors.AlreadyShipped;
        }

        Status = OrderStatus.Shipped;

        // Deferred: notifying the customer reaches outside the process, which an ordinary event's
        // handler must never do because a rollback cannot unsend a message.
        Raise(new OrderShipped(Id, CustomerId));
        return Result.Success;
    }
}
