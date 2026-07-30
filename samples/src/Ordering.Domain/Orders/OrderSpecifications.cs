using Loom.Entities;
using Loom.Specifications;
using Ordering.Domain.Customers;

namespace Ordering.Domain.Orders;

/// <summary>
/// A customer's orders, newest first, with their lines loaded.
/// </summary>
public sealed class OrdersForCustomer : Specification<Order>
{
    public OrdersForCustomer(Id<Customer> customerId)
    {
        Where(order => order.CustomerId == customerId);
        Include(order => order.Lines);
        OrderByDescending(order => order.PlacedOn);
        ThenBy(order => order.Id);
    }
}

/// <summary>
/// Orders still awaiting shipment.
/// </summary>
public sealed class OpenOrders : Specification<Order>
{
    public OpenOrders() => Where(order => order.Status == OrderStatus.Placed);
}

/// <summary>
/// A customer's orders that are still open — the two rules above, combined at the predicate level
/// because two specifications with their own ordering have no sensible combination.
/// </summary>
public sealed class OpenOrdersForCustomer : Specification<Order>
{
    public OpenOrdersForCustomer(Id<Customer> customerId)
    {
        Where(new OrdersForCustomer(customerId).Criteria!.And(new OpenOrders().Criteria!));
        Include(order => order.Lines);
        OrderByDescending(order => order.PlacedOn);
    }
}
