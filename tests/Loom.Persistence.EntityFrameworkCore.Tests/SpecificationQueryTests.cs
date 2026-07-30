using Loom.Entities;
using Loom.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public sealed class SpecificationQueryTests
{
    [Test]
    public async Task Criteria_Filter_The_Query()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        List<Order> matched = await context.Orders.ApplySpecification(new LargeOrders(100)).ToListAsync();

        await Assert.That(matched.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Ordering_Is_Translated_To_The_Query()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        List<Order> ordered = await context.Orders
            .ApplySpecification(new OrdersSmallestFirst())
            .ToListAsync();

        await Assert.That(ordered[0].Total).IsEqualTo(50);
        await Assert.That(ordered[^1].Total).IsEqualTo(500);
    }

    [Test]
    public async Task Eager_Loading_Is_Applied()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        List<Order> loaded = await context.Orders
            .AsNoTracking()
            .ApplySpecification(new LargeOrdersWithLines(100))
            .ToListAsync();

        // Without the eager loading the collection would be empty, since tracking is off and there is
        // nothing to fix up the relationship afterwards.
        await Assert.That(loaded[0].Lines.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Eager_Loading_Criteria_And_Ordering_Are_Applied_Together()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        List<Order> loaded = await context.Orders
            .AsNoTracking()
            .ApplySpecification(new LargeOrdersWithLines(100))
            .ToListAsync();

        await Assert.That(loaded.Count).IsEqualTo(2);
        await Assert.That(loaded[0].Total).IsEqualTo(500);
        await Assert.That(loaded[0].Lines.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_Nested_Include_Path_Is_Applied()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        List<Order> loaded = await context.Orders
            .AsNoTracking()
            .ApplySpecification(new OrdersWithLinePath())
            .ToListAsync();

        await Assert.That(loaded.Any(order => order.Lines.Count > 0)).IsTrue();
    }

    [Test]
    public async Task The_Specification_Package_Alone_Still_Refuses_Eager_Loading()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        // The unconstrained overload cannot honour eager loading, and says so rather than quietly
        // returning orders with empty collections.
        await Assert.That(() => context.Orders.Apply(new LargeOrdersWithLines(100)))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Null_Arguments_Are_Rejected()
    {
        await using SqliteFixture fixture = await SeedAsync();
        await using TestDbContext context = fixture.CreateContext();

        await Assert.That(() => context.Orders.ApplySpecification<Order>(null!))
            .Throws<ArgumentNullException>();
    }

    private static async Task<SqliteFixture> SeedAsync()
    {
        SqliteFixture fixture = await SqliteFixture.CreateAsync();
        await using TestDbContext context = fixture.CreateContext();

        Id<Customer> customerId = Id<Customer>.New();
        context.Customers.Add(new Customer("Acme"));

        Order large = new(customerId, 500);
        large.AddLine("sku-1");
        large.AddLine("sku-2");

        Order medium = new(customerId, 300);
        medium.AddLine("sku-3");

        Order small = new(customerId, 50);

        context.Orders.AddRange(large, medium, small);
        await context.SaveChangesAsync();

        return fixture;
    }

    private sealed class OrdersWithLinePath : Specification<Order>
    {
        public OrdersWithLinePath() => Include(nameof(Order.Lines));
    }
}
