using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public class OutboxTests
{
    [Test]
    public async Task A_Deferred_Event_Is_Recorded_Instead_Of_Dispatched()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await host.InScopeAsync(async context =>
        {
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Ship();
            await context.SaveChangesAsync();
        });

        // Recorded, not delivered: the handler has not run yet.
        await Assert.That(host.Recorder.Handled.Count).IsEqualTo(0);
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task The_Record_Commits_With_The_Change_That_Raised_It()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await host.InScopeAsync(async context =>
        {
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Ship();
            await context.SaveChangesAsync();
        });

        await host.InScopeAsync(async context =>
        {
            // Both present, from one transaction. That atomicity is the entire point of an outbox.
            await Assert.That(await context.Orders.CountAsync()).IsEqualTo(1);
            await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(1);
        });
    }

    [Test]
    public async Task A_Pass_Delivers_What_Is_Owed()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();

        int attempted = await host.Processor.DeliverPendingAsync();

        await Assert.That(attempted).IsEqualTo(1);
        await Assert.That(host.Recorder.Handled).Contains("shipped");
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task A_Delivered_Message_Is_Not_Delivered_Again()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();

        await host.Processor.DeliverPendingAsync();
        int second = await host.Processor.DeliverPendingAsync();

        await Assert.That(second).IsEqualTo(0);
        await Assert.That(host.Recorder.Handled.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_Delivered_Message_Records_When_It_Was_Delivered()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();

        await host.Processor.DeliverPendingAsync();

        await host.InScopeAsync(async context =>
        {
            OutboxMessage message = await context.Set<OutboxMessage>().SingleAsync();
            await Assert.That(message.DeliveredAt).IsNotNull();
            await Assert.That(message.Attempts).IsEqualTo(1);
            await Assert.That(message.LastError).IsNull();
        });
    }

    [Test]
    public async Task A_Failing_Delivery_Is_Retried_And_Records_Why()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true);
        await host.RaiseShippedAsync();

        await host.Processor.DeliverPendingAsync();

        await host.InScopeAsync(async context =>
        {
            OutboxMessage message = await context.Set<OutboxMessage>().SingleAsync();
            await Assert.That(message.DeliveredAt).IsNull();
            await Assert.That(message.Attempts).IsEqualTo(1);
            await Assert.That(message.Abandoned).IsFalse();
            await Assert.That(message.LastError).Contains("carrier.down");
        });
    }

    [Test]
    public async Task Delivery_Is_Abandoned_After_The_Attempt_Limit()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 3);
        await host.RaiseShippedAsync();

        for (int pass = 0; pass < 5; pass++)
        {
            await host.Processor.DeliverPendingAsync();
        }

        await host.InScopeAsync(async context =>
        {
            OutboxMessage message = await context.Set<OutboxMessage>().SingleAsync();

            // Abandoned rather than deleted: a poisoned message is evidence of a bug.
            await Assert.That(message.Abandoned).IsTrue();
            await Assert.That(message.Attempts).IsEqualTo(3);
        });
    }

    [Test]
    public async Task An_Abandoned_Message_Stops_Being_Attempted()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();

        await host.Processor.DeliverPendingAsync();
        int afterAbandonment = await host.Processor.DeliverPendingAsync();

        await Assert.That(afterAbandonment).IsEqualTo(0);
    }

    [Test]
    public async Task An_Unparseable_Payload_Does_Not_Stop_The_Pass()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();

        await host.InScopeAsync(async context =>
        {
            context.Set<OutboxMessage>().Add(new OutboxMessage
            {
                OccurredAt = DateTimeOffset.UnixEpoch,
                EventType = "Nothing.Named.This",
                Payload = "{}",
            });
            await context.SaveChangesAsync();
        });

        int attempted = await host.Processor.DeliverPendingAsync();

        // Both attempted: one poisoned message must not block everything behind it.
        await Assert.That(attempted).IsEqualTo(2);
        await Assert.That(host.Recorder.Handled).Contains("shipped");
    }

    [Test]
    public async Task Nothing_Owed_Is_Not_An_Error()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await Assert.That(await host.Processor.DeliverPendingAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task An_Ordinary_Event_Still_Dispatches_Immediately_When_An_Outbox_Exists()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await host.InScopeAsync(async context =>
        {
            Order order = new(Id<Customer>.New(), 500);
            context.Orders.Add(order);
            order.Cancel();
            await context.SaveChangesAsync();
        });

        // Only deferred events go through the outbox; the choice is per event, not global.
        await Assert.That(host.Recorder.Handled).Contains("cancelled");
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(0);
    }

    internal sealed class ShippedHandler(Recorder recorder) : IDomainEventHandler<OrderShipped>
    {
        public Task<Result> HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken)
        {
            recorder.Record("shipped");
            return Task.FromResult(Result.Success);
        }
    }

    internal sealed class FailingShippedHandler : IDomainEventHandler<OrderShipped>
    {
        public Task<Result> HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(Errors.Unavailable("carrier.down", "The carrier is unreachable.")));
    }

    internal sealed class CancelledHandler(Recorder recorder) : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
        {
            recorder.Record("cancelled");
            return Task.FromResult(Result.Success);
        }
    }
}
