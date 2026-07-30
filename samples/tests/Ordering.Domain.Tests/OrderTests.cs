using Loom.Entities;
using Loom.Results;
using Loom.Specifications;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Domain.Tests;

public sealed class OrderTests
{
    private static readonly DateOnly Today = new(2026, 7, 30);

    [Test]
    public async Task An_Order_Totals_Its_Lines()
    {
        Order order = Placed(("sku-1", 300), ("sku-2", 200));

        await Assert.That(order.Total).IsEqualTo(500);
    }

    [Test]
    public async Task An_Order_Cannot_Be_Placed_With_No_Lines()
    {
        Result<Order> result = Order.Place(Id<Customer>.New(), Today, []);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo(OrderErrors.NoLines.Code);
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Invalid);
    }

    [Test]
    public async Task A_Placed_Order_Can_Be_Cancelled()
    {
        Order order = Placed(("sku-1", 100));

        Result result = order.Cancel();

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
    }

    [Test]
    public async Task Cancelling_Raises_An_Ordinary_Event()
    {
        Order order = Placed(("sku-1", 100));

        _ = order.Cancel();

        IDomainEvent raised = order.DomainEvents.Single();
        await Assert.That(raised).IsTypeOf<OrderCancelled>();
        await Assert.That(raised is IDeferredDomainEvent).IsFalse();
    }

    [Test]
    public async Task Shipping_Raises_A_Deferred_Event()
    {
        Order order = Placed(("sku-1", 100));

        _ = order.Ship();

        // Notifying the customer reaches outside the process, so the event asks to be delivered after
        // the transaction commits.
        await Assert.That(order.DomainEvents.Single() is IDeferredDomainEvent).IsTrue();
    }

    [Test]
    public async Task A_Shipped_Order_Cannot_Be_Cancelled()
    {
        Order order = Placed(("sku-1", 100));
        _ = order.Ship();

        Result result = order.Cancel();

        // An invariant, reported rather than thrown.
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Conflict);
        await Assert.That(result.Error.Code).IsEqualTo(OrderErrors.AlreadyShipped.Code);
    }

    [Test]
    public async Task A_Cancelled_Order_Cannot_Be_Shipped()
    {
        Order order = Placed(("sku-1", 100));
        _ = order.Cancel();

        Result result = order.Ship();

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Conflict);
        await Assert.That(result.Error.Code).IsEqualTo(OrderErrors.AlreadyCancelled.Code);
    }

    [Test]
    public async Task Cancelling_Twice_Is_A_Conflict()
    {
        Order order = Placed(("sku-1", 100));
        _ = order.Cancel();

        Result result = order.Cancel();

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Conflict);
        await Assert.That(result.Error.Code).IsEqualTo(OrderErrors.AlreadyCancelled.Code);
    }

    [Test]
    public async Task An_Order_With_A_Blank_Or_Worthless_Line_Is_Rejected()
    {
        // Reported, not thrown: a caller handed the factory bad data, which is an expected failure.
        Result<Order> blankSku = Order.Place(Id<Customer>.New(), Today, [("", 100)]);
        Result<Order> zeroAmount = Order.Place(Id<Customer>.New(), Today, [("sku-1", 0)]);

        await Assert.That(blankSku.IsFailure).IsTrue();
        await Assert.That(blankSku.Error.Code).IsEqualTo(OrderErrors.InvalidLine.Code);
        await Assert.That(zeroAmount.IsFailure).IsTrue();
        await Assert.That(zeroAmount.Error.Code).IsEqualTo(OrderErrors.InvalidLine.Code);
    }

    [Test]
    public async Task Lines_Cannot_Be_Mutated_From_Outside()
    {
        Order order = Placed(("sku-1", 100));

        // The view must not be the backing list in disguise, or a caller could cast it and change
        // Total behind the aggregate's back.
        await Assert.That(order.Lines is List<OrderLine>).IsFalse();
    }

    [Test]
    public async Task A_Failed_Transition_Raises_Nothing()
    {
        Order order = Placed(("sku-1", 100));
        _ = order.Ship();
        _ = order.DequeueDomainEvents();

        _ = order.Cancel();

        await Assert.That(order.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task The_Customer_Specification_Matches_Only_That_Customer()
    {
        Id<Customer> mine = Id<Customer>.New();
        Order[] orders = [Placed(mine, ("a", 1)), Placed(Id<Customer>.New(), ("b", 1))];

        await Assert.That(orders.Apply(new OrdersForCustomer(mine)).Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Combined_Specifications_Require_Both_Rules()
    {
        Id<Customer> mine = Id<Customer>.New();

        Order open = Placed(mine, ("a", 1));
        Order shipped = Placed(mine, ("b", 1));
        _ = shipped.Ship();
        Order otherCustomer = Placed(Id<Customer>.New(), ("c", 1));

        Order[] matched = [.. new[] { open, shipped, otherCustomer }.Apply(new OpenOrdersForCustomer(mine))];

        await Assert.That(matched.Length).IsEqualTo(1);
        await Assert.That(matched[0].Status).IsEqualTo(OrderStatus.Placed);
    }

    private static Order Placed(params (string Sku, int Amount)[] lines) =>
        Placed(Id<Customer>.New(), lines);

    private static Order Placed(Id<Customer> customerId, params (string Sku, int Amount)[] lines) =>
        Order.Place(customerId, Today, lines).Value;
}
