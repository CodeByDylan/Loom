using System.Linq.Expressions;
using Loom.Entities;
using Loom.Specifications;
using Ordering.Domain.Customers;

namespace Ordering.Domain.Orders;

/// <summary>
/// The predicates the specifications below are built from.
/// </summary>
/// <remarks>
/// Shared as expressions rather than by constructing one specification to read another's
/// <c>Criteria</c>. That built a whole object — ordering and all — to reach one property, and left a
/// reader to work out that only the predicate came across.
/// </remarks>
internal static class OrderCriteria
{
    internal static Expression<Func<Order, bool>> Open =>
        order => order.Status == OrderStatus.Placed;

    internal static Expression<Func<Order, bool>> PlacedBy(Id<Customer> customerId) =>
        order => order.CustomerId == customerId;
}

/// <summary>
/// A customer's orders, newest first.
/// </summary>
/// <remarks>
/// No eager loading. Every caller projects, and Entity Framework Core discards an <c>Include</c> under
/// a projection — so one here would read as a promise the query does not keep. A caller that needs the
/// lines as entities should ask for them itself.
/// </remarks>
public sealed class OrdersForCustomer : Specification<Order>
{
    public OrdersForCustomer(Id<Customer> customerId)
    {
        Where(OrderCriteria.PlacedBy(customerId));
        OrderByDescending(order => order.PlacedOn);
        ThenBy(order => order.Id);
    }
}

/// <summary>
/// Orders still awaiting shipment.
/// </summary>
public sealed class OpenOrders : Specification<Order>
{
    public OpenOrders() => Where(OrderCriteria.Open);
}

/// <summary>
/// A customer's orders that are still open — the two rules above, combined at the predicate level
/// because two specifications with their own ordering have no sensible combination.
/// </summary>
public sealed class OpenOrdersForCustomer : Specification<Order>
{
    public OpenOrdersForCustomer(Id<Customer> customerId)
    {
        Where(OrderCriteria.PlacedBy(customerId).And(OrderCriteria.Open));
        OrderByDescending(order => order.PlacedOn);

        // The same tie-breaker as OrdersForCustomer. Orders placed on the same day would otherwise come
        // back in whatever order the database chose, which a paged reader sees as rows moving between
        // pages.
        ThenBy(order => order.Id);
    }
}
