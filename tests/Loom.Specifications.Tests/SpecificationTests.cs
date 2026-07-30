namespace Loom.Specifications.Tests;

public sealed class SpecificationTests
{
    private static readonly Order[] Orders =
    [
        new("Acme", 500, new DateOnly(2026, 3, 1)),
        new("Acme", 50, new DateOnly(2026, 1, 1)),
        new("Globex", 300, new DateOnly(2026, 2, 1)),
    ];

    [Test]
    public async Task Criteria_Filters_In_Memory()
    {
        IEnumerable<Order> matched = Orders.Apply(new OrdersForCustomer("Acme"));

        await Assert.That(matched.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Criteria_Filters_A_Query()
    {
        IQueryable<Order> matched = Orders.AsQueryable().Apply(new OrdersOverAmount(100));

        await Assert.That(matched.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task A_Specification_With_No_Criteria_Matches_Everything()
    {
        await Assert.That(Orders.Apply(new MatchEverything()).Count()).IsEqualTo(3);
        await Assert.That(new MatchEverything().IsSatisfiedBy(Orders[0])).IsTrue();
    }

    [Test]
    public async Task IsSatisfiedBy_Tests_One_Item()
    {
        OrdersOverAmount specification = new(100);

        await Assert.That(specification.IsSatisfiedBy(Orders[0])).IsTrue();
        await Assert.That(specification.IsSatisfiedBy(Orders[1])).IsFalse();
    }

    [Test]
    public async Task IsSatisfiedBy_Is_Stable_Across_Calls()
    {
        // The compiled predicate is cached; caching must not change the answer.
        OrdersOverAmount specification = new(100);

        await Assert.That(specification.IsSatisfiedBy(Orders[0])).IsTrue();
        await Assert.That(specification.IsSatisfiedBy(Orders[0])).IsTrue();
        await Assert.That(specification.IsSatisfiedBy(Orders[1])).IsFalse();
    }

    [Test]
    public async Task Criteria_Compose_Across_Specifications()
    {
        IEnumerable<Order> matched = Orders.Apply(new BigOrdersForCustomer("Acme", 100));

        await Assert.That(matched.Single().Total).IsEqualTo(500);
    }

    [Test]
    public async Task Ordering_Is_Applied_In_Memory()
    {
        Order[] ordered = [.. Orders.Apply(new OrdersNewestFirst())];

        await Assert.That(ordered[0].DueOn).IsEqualTo(new DateOnly(2026, 3, 1));
        await Assert.That(ordered[2].DueOn).IsEqualTo(new DateOnly(2026, 1, 1));
    }

    [Test]
    public async Task Ordering_Is_Applied_To_A_Query()
    {
        Order[] ordered = [.. Orders.AsQueryable().Apply(new OrdersNewestFirst())];

        await Assert.That(ordered[0].DueOn).IsEqualTo(new DateOnly(2026, 3, 1));
    }

    [Test]
    public async Task Secondary_Ordering_Breaks_Ties()
    {
        Order[] ordered = [.. Orders.Apply(new OrdersByCustomerThenTotal())];

        await Assert.That(ordered[0].Customer).IsEqualTo("Acme");
        await Assert.That(ordered[0].Total).IsEqualTo(500);
        await Assert.That(ordered[1].Total).IsEqualTo(50);
        await Assert.That(ordered[2].Customer).IsEqualTo("Globex");
    }

    [Test]
    public async Task ThenBy_Without_OrderBy_Is_Rejected()
    {
        await Assert.That(() => new BrokenOrdering()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Applying_A_Specification_With_Eager_Loading_To_A_Plain_Query_Fails_Loudly()
    {
        // Silently skipping eager loading would leave the caller with unloaded relationships and no
        // indication why, so this is rejected rather than ignored.
        await Assert.That(() => Orders.AsQueryable().Apply(new OrdersWithItems()))
            .Throws<NotSupportedException>();

        await Assert.That(() => Orders.AsQueryable().Apply(new OrdersWithNestedItems()))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Eager_Loading_Is_Ignored_In_Memory()
    {
        // An in-memory graph is already loaded, so there is nothing to eagerly load.
        await Assert.That(Orders.Apply(new OrdersWithItems()).Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Eager_Loading_Is_Recorded_For_A_Persistence_Package_To_Apply()
    {
        await Assert.That(new OrdersWithItems().Includes.Count).IsEqualTo(1);
        await Assert.That(new OrdersWithNestedItems().IncludePaths.Single()).IsEqualTo("Items.Product");
    }

    [Test]
    public async Task A_Persistence_Package_Can_Apply_The_Rest_Itself()
    {
        IQueryable<Order> matched = Orders.AsQueryable()
            .ApplyCriteriaAndOrdering(new OrdersWithItemsOverAmount(100));

        await Assert.That(matched.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Null_Arguments_Are_Rejected()
    {
        await Assert.That(() => Orders.AsQueryable().Apply(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => Orders.Apply(null!).ToList()).Throws<ArgumentNullException>();
        await Assert.That(() => new BlankPath()).Throws<ArgumentException>();
    }

    private sealed class BrokenOrdering : Specification<Order>
    {
        public BrokenOrdering() => ThenBy(order => order.Total);
    }

    private sealed class BlankPath : Specification<Order>
    {
        public BlankPath() => Include("   ");
    }

    private sealed class OrdersWithItemsOverAmount : Specification<Order>
    {
        public OrdersWithItemsOverAmount(int amount)
        {
            Where(order => order.Total > amount);
            Include(order => order.Items);
        }
    }
}
