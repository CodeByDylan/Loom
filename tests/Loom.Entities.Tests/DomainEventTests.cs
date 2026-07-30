using Loom.Results;

namespace Loom.Entities.Tests;

public sealed class DomainEventTests
{
    [Test]
    public async Task An_Ordinary_Event_Is_Not_Deferred()
    {
        IDomainEvent domainEvent = new OrderCancelled(Id<Order>.New());

        await Assert.That(domainEvent is IDeferredDomainEvent).IsFalse();
    }

    [Test]
    public async Task A_Deferred_Event_Is_Recognisable_As_A_Domain_Event()
    {
        // Infrastructure sees only IDomainEvent when draining an aggregate, so the deferred contract
        // has to be detectable by a type test at that point.
        IDomainEvent domainEvent = new OrderShipped(Id<Order>.New());

        await Assert.That(domainEvent is IDeferredDomainEvent).IsTrue();
    }

    [Test]
    public async Task An_Aggregate_Can_Raise_Both_Kinds()
    {
        Order order = new();

        order.Cancel();
        order.Ship();

        IReadOnlyCollection<IDomainEvent> drained = order.DequeueDomainEvents();

        await Assert.That(drained.OfType<IDeferredDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(drained.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_Handler_Returns_Success_Or_Failure_Rather_Than_Throwing()
    {
        IDomainEventHandler<OrderCancelled> handler = new RecordingHandler();

        Result result = await handler.HandleAsync(new OrderCancelled(Id<Order>.New()), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task A_Handler_Can_Report_A_Failure()
    {
        IDomainEventHandler<OrderCancelled> handler = new FailingHandler();

        Result result = await handler.HandleAsync(new OrderCancelled(Id<Order>.New()), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Unavailable);
    }

    private sealed class RecordingHandler : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success);
    }

    private sealed class FailingHandler : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(Errors.Unavailable("refunds.down", "The refund service is down.")));
    }
}
