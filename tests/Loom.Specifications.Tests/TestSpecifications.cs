namespace Loom.Specifications.Tests;

internal sealed record Order(string Customer, int Total, DateOnly DueOn)
{
    internal IReadOnlyList<string> Items { get; init; } = [];
}

// Rules are configured in the constructor, so a specification is fixed once and cannot drift.
internal sealed class OrdersForCustomer : Specification<Order>
{
    public OrdersForCustomer(string customer) => Where(order => order.Customer == customer);
}

internal sealed class OrdersOverAmount : Specification<Order>
{
    public OrdersOverAmount(int amount) => Where(order => order.Total > amount);
}

internal sealed class BigOrdersForCustomer : Specification<Order>
{
    public BigOrdersForCustomer(string customer, int amount) =>
        Where(new OrdersForCustomer(customer).Criteria!.And(new OrdersOverAmount(amount).Criteria!));
}

internal sealed class OrdersNewestFirst : Specification<Order>
{
    public OrdersNewestFirst() => OrderByDescending(order => order.DueOn);
}

internal sealed class OrdersByCustomerThenTotal : Specification<Order>
{
    public OrdersByCustomerThenTotal()
    {
        OrderBy(order => order.Customer);
        ThenByDescending(order => order.Total);
    }
}

internal sealed class MatchEverything : Specification<Order>;

internal sealed class OrdersWithItems : Specification<Order>
{
    public OrdersWithItems() => Include(order => order.Items);
}

internal sealed class OrdersWithNestedItems : Specification<Order>
{
    public OrdersWithNestedItems() => Include("Items.Product");
}
