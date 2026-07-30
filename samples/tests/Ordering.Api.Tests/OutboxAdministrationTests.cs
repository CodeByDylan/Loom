using System.Net.Http.Json;
using System.Text.Json;
using Loom.Entities;
using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Customers;

namespace Ordering.Api.Tests;

/// <summary>
/// Outbox administration against the real provider.
/// </summary>
/// <remarks>
/// The package's own tests run on SQLite, which is enough for provider-neutral behaviour — but these
/// operations are translated to statements the provider generates, and a bulk update or delete is
/// exactly the kind of thing that differs. This covers them where the sample actually runs.
/// <para>
/// No endpoint or command is exposed for these. How an operator reaches them depends on how they
/// operate, which is not yet known, so the sample demonstrates the operations rather than inventing a
/// surface for them.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class OutboxAdministrationTests
{
    private OrderingApi _api = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _api = ApiFixture.Api;
        await _api.ResetAsync();
    }

    [Test]
    public async Task A_Delivered_Message_Is_Purged_And_An_Owed_One_Is_Not()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);

        // One delivered, one still owed.
        await DeliverAsync();
        await ShipAsync(customer);

        int purged = await AdministerAsync(admin =>
            admin.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddMinutes(1)));

        await Assert.That(purged).IsEqualTo(1);

        await _api.InDatabaseAsync(async database =>
        {
            OutboxMessage remaining = await database.Set<OutboxMessage>().AsNoTracking().SingleAsync();
            await Assert.That(remaining.DeliveredAt).IsNull();
        });
    }

    [Test]
    public async Task Nothing_Is_Abandoned_When_Delivery_Succeeds()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);
        await DeliverAsync();

        await Assert.That(await AdministerAsync(admin => admin.CountAbandonedAsync())).IsEqualTo(0);
        await Assert.That(await AdministerAsync(admin => admin.FindAbandonedAsync())).IsEmpty();
    }

    [Test]
    public async Task An_Abandoned_Message_Can_Be_Found_And_Retried()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);

        // Abandonment needs a handler that refuses, which the running application has none of, so the
        // state is written directly. The operations under test are what matters here, not how a message
        // reached this state — the package's own tests cover that.
        await _api.InDatabaseAsync(async database =>
        {
            OutboxMessage message = await database.Set<OutboxMessage>().SingleAsync();
            message.Abandoned = true;
            message.Attempts = 5;
            message.LastError = "carrier.down: unreachable";
            await database.SaveChangesAsync();
        });

        AbandonedMessage abandoned = (await AdministerAsync(admin => admin.FindAbandonedAsync())).Single();
        await Assert.That(abandoned.Attempts).IsEqualTo(5);
        await Assert.That(abandoned.LastError).Contains("carrier.down");

        await Assert.That(await AdministerAsync(admin => admin.RetryAsync(abandoned.Id))).IsTrue();

        // Owed again, and a real pass now delivers it.
        await Assert.That(await DeliverAsync()).IsEqualTo(1);

        await _api.InDatabaseAsync(async database =>
            await Assert.That(await database.ShipmentNotifications.CountAsync()).IsEqualTo(1));
    }

    [Test]
    public async Task An_Abandoned_Message_Survives_A_Purge()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);
        await DeliverAsync();

        await _api.InDatabaseAsync(async database =>
        {
            OutboxMessage message = await database.Set<OutboxMessage>().SingleAsync();
            message.Abandoned = true;
            message.DeliveredAt = null;
            await database.SaveChangesAsync();
        });

        int purged = await AdministerAsync(admin =>
            admin.PurgeDeliveredAsync(DateTimeOffset.UtcNow.AddYears(1)));

        // Evidence of a failure is never deleted, at any age.
        await Assert.That(purged).IsEqualTo(0);
        await Assert.That(await AdministerAsync(admin => admin.CountAbandonedAsync())).IsEqualTo(1);
    }

    private static async Task<T> AdministerAsync<T>(
        Func<OutboxAdministration<OrderingDbContext>, Task<T>> work)
    {
        using IServiceScope scope = ApiFixture.Api.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<OutboxAdministration<OrderingDbContext>>());
    }

    private async Task<int> DeliverAsync()
    {
        using IServiceScope scope = _api.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxProcessor<OrderingDbContext>>()
            .DeliverPendingAsync();
    }

    private async Task ShipAsync(Id<Customer> customer)
    {
        HttpResponseMessage placed = await _api.ClientFor(customer).PostAsJsonAsync("/orders", new
        {
            placedOn = "2026-07-30",
            lines = new[] { new { sku = "sku-1", amount = 100 } },
        });

        placed.EnsureSuccessStatusCode();
        JsonElement body = await placed.Content.ReadFromJsonAsync<JsonElement>();
        Guid orderId = body.GetProperty("orderId").GetGuid();

        HttpResponseMessage shipped = await _api.ClientFor(customer)
            .PostAsync($"/orders/{orderId}/ship", null);

        shipped.EnsureSuccessStatusCode();
    }
}
