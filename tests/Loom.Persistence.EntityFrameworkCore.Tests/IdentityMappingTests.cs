using Loom.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public sealed class IdentityMappingTests
{
    [Test]
    public async Task An_Identity_Round_Trips_Through_The_Database()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();

        Id<Order> orderId;
        await using (TestDbContext context = fixture.CreateContext())
        {
            Order order = new(Id<Customer>.New(), 500);
            orderId = order.Id;
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        await using (TestDbContext context = fixture.CreateContext())
        {
            Order? stored = await context.Orders.SingleOrDefaultAsync(order => order.Id == orderId);

            await Assert.That(stored).IsNotNull();
            await Assert.That(stored!.Id).IsEqualTo(orderId);
        }
    }

    [Test]
    public async Task An_Identity_Is_Stored_As_Its_Underlying_Value()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        await using TestDbContext context = fixture.CreateContext();

        Order order = new(Id<Customer>.New(), 100);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Read back through raw SQL to prove the column holds the identity's value and not a
        // serialised object.
        Guid[] stored = await context.Database
            .SqlQuery<Guid>($"SELECT Id AS Value FROM Orders")
            .ToArrayAsync();

        await Assert.That(stored.Single()).IsEqualTo(order.Id.Value);
    }

    [Test]
    public async Task A_Foreign_Identity_Of_A_Different_Entity_Also_Converts()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();

        Id<Customer> customerId = Id<Customer>.New();
        await using (TestDbContext context = fixture.CreateContext())
        {
            context.Orders.Add(new Order(customerId, 250));
            await context.SaveChangesAsync();
        }

        await using (TestDbContext context = fixture.CreateContext())
        {
            // Converting only the primary key would leave this property unmapped, so querying on it
            // proves the whole model was walked rather than just the keys.
            Order stored = await context.Orders.SingleAsync(order => order.CustomerId == customerId);

            await Assert.That(stored.CustomerId).IsEqualTo(customerId);
        }
    }

    [Test]
    public async Task An_Identity_Survives_A_Query_Against_A_Constructed_Value()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();

        Guid raw;
        await using (TestDbContext context = fixture.CreateContext())
        {
            Order order = new(Id<Customer>.New(), 10);
            raw = order.Id.Value;
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        await using (TestDbContext context = fixture.CreateContext())
        {
            Id<Order> reconstructed = Id<Order>.From(raw);

            await Assert.That(await context.Orders.AnyAsync(order => order.Id == reconstructed)).IsTrue();
        }
    }
}
