namespace Loom.Entities.Tests;

// Deliberately not sealed, so that OrderProxy below can stand in for the subclass an
// object-relational mapper generates for lazy loading.
internal class Order : AggregateRoot<Order>
{
    public Order()
    {
    }

    public Order(Id<Order> id) : base(id)
    {
    }

    public bool IsCancelled { get; private set; }

    public void Cancel()
    {
        IsCancelled = true;
        Raise(new OrderCancelled(Id));
    }

    public void Touch() => Raise(new OrderTouched(Id));

    public void Ship() => Raise(new OrderShipped(Id));
}

internal sealed class OrderProxy(Id<Order> id) : Order(id);

internal sealed class Customer : Entity<Customer>
{
    public Customer()
    {
    }

    public Customer(Id<Customer> id) : base(id)
    {
    }
}

internal sealed record OrderCancelled(Id<Order> OrderId) : IDomainEvent;

internal sealed record OrderTouched(Id<Order> OrderId) : IDomainEvent;

// Deferred: dispatched after the transaction commits, so its handlers may reach outside the process.
internal sealed record OrderShipped(Id<Order> OrderId) : IDeferredDomainEvent;
