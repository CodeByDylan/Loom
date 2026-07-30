using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// Reading and repairing an outbox: what was abandoned, retrying it, and clearing what was delivered.
/// </summary>
public sealed class OutboxAdministrationTests
{
    [Test]
    public async Task An_Abandoned_Message_Is_Found_With_Its_Evidence()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        IReadOnlyList<AbandonedMessage> abandoned = await AdministerAsync(host, a => a.FindAbandonedAsync());

        AbandonedMessage message = abandoned.Single();
        await Assert.That(message.Attempts).IsEqualTo(1);
        await Assert.That(message.LastError).Contains("carrier.down");
        await Assert.That(message.EventType).Contains(nameof(OrderShipped));
    }

    [Test]
    public async Task A_Message_Still_Owed_Is_Not_Reported_As_Abandoned()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 5);
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        // Failed once of five. Still owed, not given up on.
        await Assert.That(await AdministerAsync(host, a => a.CountAbandonedAsync())).IsEqualTo(0);
    }

    [Test]
    public async Task Retrying_Offers_An_Abandoned_Message_To_Delivery_Again()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        AbandonedMessage abandoned = (await AdministerAsync(host, a => a.FindAbandonedAsync())).Single();

        await Assert.That(await AdministerAsync(host, a => a.RetryAsync(abandoned.Id))).IsTrue();

        // Owed again, so a pass picks it up.
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(1);
        await Assert.That(await host.Processor.DeliverPendingAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Retrying_Resets_The_Attempts_But_Keeps_The_Evidence()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        AbandonedMessage abandoned = (await AdministerAsync(host, a => a.FindAbandonedAsync())).Single();
        _ = await AdministerAsync(host, a => a.RetryAsync(abandoned.Id));

        await host.InScopeAsync(async context =>
        {
            OutboxMessage message = await context.Set<OutboxMessage>().AsNoTracking().SingleAsync();

            // Leaving the count would abandon it again on the first failure; clearing the error would
            // destroy the record of what went wrong.
            await Assert.That(message.Attempts).IsEqualTo(0);
            await Assert.That(message.Abandoned).IsFalse();
            await Assert.That(message.LastError).IsNotNull();
        });
    }

    [Test]
    public async Task Retrying_Something_That_Was_Not_Abandoned_Reports_So()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await Assert.That(await AdministerAsync(host, a => a.RetryAsync(Guid.CreateVersion7()))).IsFalse();
    }

    [Test]
    public async Task Retrying_Everything_Recovers_A_Whole_Batch()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();
        await host.RaiseShippedAsync();
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        await Assert.That(await AdministerAsync(host, a => a.CountAbandonedAsync())).IsEqualTo(3);
        await Assert.That(await AdministerAsync(host, a => a.RetryAllAbandonedAsync())).IsEqualTo(3);
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(3);
    }

    [Test]
    public async Task Purging_Removes_A_Delivered_Message()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        int purged = await AdministerAsync(host, a => a.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddMinutes(1)));

        await Assert.That(purged).IsEqualTo(1);
        await host.InScopeAsync(async context =>
            await Assert.That(await context.Set<OutboxMessage>().CountAsync()).IsEqualTo(0));
    }

    [Test]
    public async Task Purging_Leaves_A_Message_Delivered_After_The_Cutoff()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        int purged = await AdministerAsync(host, a => a.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddDays(-1)));

        await Assert.That(purged).IsEqualTo(0);
    }

    [Test]
    public async Task Purging_Never_Removes_What_Is_Still_Owed()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();
        await host.RaiseShippedAsync();

        int purged = await AdministerAsync(host, a => a.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddYears(1)));

        await Assert.That(purged).IsEqualTo(0);
        await Assert.That(await host.CountOwedAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task Purging_Never_Removes_Evidence_Of_A_Failure()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync(failing: true, maximumAttempts: 1);
        await host.RaiseShippedAsync();
        await host.Processor.DeliverPendingAsync();

        // Old enough by any cutoff, and abandoned. Deleting it is how a failure stays unexplained.
        int purged = await AdministerAsync(host, a => a.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddYears(1)));

        await Assert.That(purged).IsEqualTo(0);
        await Assert.That(await AdministerAsync(host, a => a.CountAbandonedAsync())).IsEqualTo(1);
    }

    [Test]
    public async Task A_Nonsense_Limit_Is_Refused()
    {
        await using OutboxHost host = await OutboxHost.CreateAsync();

        await Assert.That(async () => await AdministerAsync(host, a => a.FindAbandonedAsync(limit: 0)))
            .Throws<ArgumentOutOfRangeException>();
    }

    private static async Task<T> AdministerAsync<T>(
        OutboxHost host,
        Func<OutboxAdministration<OutboxDbContext>, Task<T>> work)
    {
        using IServiceScope scope = host.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<OutboxAdministration<OutboxDbContext>>());
    }
}
