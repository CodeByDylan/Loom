namespace Loom.Entities.Tests;

public class AggregateRootTests
{
    [Test]
    public async Task A_New_Aggregate_Has_No_Events()
    {
        Order order = new();

        await Assert.That(order.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Raising_Records_An_Event()
    {
        Order order = new();

        order.Cancel();

        await Assert.That(order.DomainEvents.Count).IsEqualTo(1);
        await Assert.That(order.DomainEvents.Single()).IsTypeOf<OrderCancelled>();
    }

    [Test]
    public async Task Events_Are_Recorded_In_Order()
    {
        Order order = new();

        order.Cancel();
        order.Touch();

        await Assert.That(order.DomainEvents.First()).IsTypeOf<OrderCancelled>();
        await Assert.That(order.DomainEvents.Last()).IsTypeOf<OrderTouched>();
    }

    [Test]
    public async Task Reading_Events_Does_Not_Drain_Them()
    {
        Order order = new();
        order.Cancel();

        _ = order.DomainEvents;

        await Assert.That(order.DomainEvents.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Dequeuing_Returns_The_Events_And_Clears_Them()
    {
        Order order = new();
        order.Cancel();
        order.Touch();

        IReadOnlyCollection<IDomainEvent> drained = order.DequeueDomainEvents();

        await Assert.That(drained.Count).IsEqualTo(2);
        await Assert.That(order.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dequeuing_Twice_Yields_Nothing_The_Second_Time()
    {
        Order order = new();
        order.Cancel();

        _ = order.DequeueDomainEvents();
        IReadOnlyCollection<IDomainEvent> second = order.DequeueDomainEvents();

        await Assert.That(second.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dequeued_Events_Are_A_Snapshot_Not_A_Live_View()
    {
        Order order = new();
        order.Cancel();

        IReadOnlyCollection<IDomainEvent> drained = order.DequeueDomainEvents();
        order.Touch();

        // Returning the live list would mean a later raise mutates what a dispatcher is iterating.
        await Assert.That(drained.Count).IsEqualTo(1);
    }

    [Test]
    public async Task An_Aggregate_Is_Reachable_Through_The_Non_Generic_Interface()
    {
        // This is what lets infrastructure drain events without knowing each aggregate's own type.
        IAggregateRoot aggregate = new Order();
        ((Order)aggregate).Cancel();

        await Assert.That(aggregate.DequeueDomainEvents().Count).IsEqualTo(1);
    }

    [Test]
    public async Task An_Aggregate_Keeps_Entity_Identity_Semantics()
    {
        Id<Order> id = Id<Order>.New();

        await Assert.That(new Order(id)).IsEqualTo(new Order(id));
    }
}
