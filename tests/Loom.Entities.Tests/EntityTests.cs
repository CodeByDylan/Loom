namespace Loom.Entities.Tests;

public class EntityTests
{
    [Test]
    public async Task A_New_Entity_Has_An_Identity_Immediately()
    {
        Customer customer = new();

        // No transient state: an entity has an identity from the moment it exists, which is what
        // removes the "are two unsaved entities equal" problem entirely.
        await Assert.That(customer.Id).IsNotEqualTo(default(Id<Customer>));
    }

    [Test]
    public async Task Two_New_Entities_Are_Not_Equal()
    {
        await Assert.That(new Customer()).IsNotEqualTo(new Customer());
    }

    [Test]
    public async Task Entities_With_The_Same_Identity_Are_Equal()
    {
        Id<Customer> id = Id<Customer>.New();

        Customer first = new(id);
        Customer second = new(id);

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first == second).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }

    [Test]
    public async Task Entities_Are_Equal_Regardless_Of_Their_Other_State()
    {
        Id<Order> id = Id<Order>.New();

        Order untouched = new(id);
        Order cancelled = new(id);
        cancelled.Cancel();

        // Identity equality, not structural equality. This is why an entity is a class and not a
        // record: a record would report these as different.
        await Assert.That(untouched).IsEqualTo(cancelled);
    }

    [Test]
    public async Task A_Proxy_Subclass_Is_Equal_To_Its_Plain_Counterpart()
    {
        Id<Order> id = Id<Order>.New();

        Order plain = new(id);
        Order proxy = new OrderProxy(id);

        // Comparing GetType() would break here, which is the bug lazy-loading proxies cause in most
        // hand-rolled entity bases. Equality is defined against Entity<TSelf>, so TSelf stays Order.
        await Assert.That(plain).IsEqualTo(proxy);
        await Assert.That(proxy).IsEqualTo(plain);
    }

    [Test]
    public async Task An_Entity_Is_Never_Equal_To_Null()
    {
        Customer customer = new();

        await Assert.That(customer.Equals(null)).IsFalse();
        await Assert.That(customer == null).IsFalse();
        await Assert.That(customer != null).IsTrue();
    }

    [Test]
    public async Task Two_Nulls_Are_Equal()
    {
        Customer? left = null;
        Customer? right = null;

        await Assert.That(left == right).IsTrue();
    }

    [Test]
    public async Task Constructing_With_A_Default_Identity_Is_Rejected()
    {
        await Assert.That(() => new Customer(default)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ToString_Names_The_Type_And_Identity()
    {
        Order order = new();

        await Assert.That(order.ToString()).IsEqualTo($"Order {order.Id}");
    }
}
