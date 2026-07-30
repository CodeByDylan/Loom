using Loom.Entities;
using Loom.Paging;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public class AsyncPagingTests
{
    [Test]
    public async Task A_Page_Reports_Its_Items_And_The_Total()
    {
        await using SqliteFixture fixture = await SeedAsync(25);
        await using TestDbContext context = fixture.CreateContext();

        Page<Order> page = await context.Orders
            .OrderBy(order => order.Total)
            .ToPageAsync(new PageRequest(2, 10));

        await Assert.That(page.Items.Count).IsEqualTo(10);
        await Assert.That(page.TotalCount).IsEqualTo(25);
        await Assert.That(page.TotalPages).IsEqualTo(3);
        await Assert.That(page.HasNext).IsTrue();
    }

    [Test]
    public async Task The_Last_Page_May_Be_Short()
    {
        await using SqliteFixture fixture = await SeedAsync(25);
        await using TestDbContext context = fixture.CreateContext();

        Page<Order> page = await context.Orders
            .OrderBy(order => order.Total)
            .ToPageAsync(new PageRequest(3, 10));

        await Assert.That(page.Items.Count).IsEqualTo(5);
        await Assert.That(page.HasNext).IsFalse();
    }

    [Test]
    public async Task A_Page_Beyond_The_End_Is_Empty_And_Still_Reports_The_Total()
    {
        await using SqliteFixture fixture = await SeedAsync(5);
        await using TestDbContext context = fixture.CreateContext();

        Page<Order> page = await context.Orders.OrderBy(o => o.Total).ToPageAsync(new PageRequest(4, 10));

        await Assert.That(page.Items.Count).IsEqualTo(0);
        await Assert.That(page.TotalCount).IsEqualTo(5);
    }

    [Test]
    public async Task An_Empty_Table_Yields_An_Empty_Page()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        await using TestDbContext context = fixture.CreateContext();

        Page<Order> page = await context.Orders.OrderBy(o => o.Total).ToPageAsync(new PageRequest(1, 10));

        await Assert.That(page.TotalCount).IsEqualTo(0);
        await Assert.That(page.TotalPages).IsEqualTo(0);
    }

    [Test]
    public async Task Paging_Composes_With_A_Specification()
    {
        await using SqliteFixture fixture = await SeedAsync(25);
        await using TestDbContext context = fixture.CreateContext();

        Page<Order> page = await context.Orders
            .ApplySpecification(new OrdersSmallestFirst())
            .ToPageAsync(new PageRequest(1, 5));

        await Assert.That(page.Items.Count).IsEqualTo(5);
        await Assert.That(page.Items[0].Total).IsEqualTo(1);
        await Assert.That(page.TotalCount).IsEqualTo(25);
    }

    [Test]
    public async Task Null_Arguments_Are_Rejected()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        await using TestDbContext context = fixture.CreateContext();

        await Assert.That(async () => await context.Orders.ToPageAsync(null!))
            .Throws<ArgumentNullException>();
    }

    private static async Task<SqliteFixture> SeedAsync(int count)
    {
        SqliteFixture fixture = await SqliteFixture.CreateAsync();
        await using TestDbContext context = fixture.CreateContext();

        Id<Customer> customerId = Id<Customer>.New();
        for (int total = 1; total <= count; total++)
        {
            context.Orders.Add(new Order(customerId, total));
        }

        await context.SaveChangesAsync();
        return fixture;
    }
}
