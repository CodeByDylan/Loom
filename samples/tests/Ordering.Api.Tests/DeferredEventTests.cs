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
/// Deferred delivery, driven by hand rather than by waiting for the background service's timer.
/// </summary>
[NotInParallel]
public sealed class DeferredEventTests
{
    private OrderingApi _api = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _api = ApiFixture.Api;
        await _api.ResetAsync();
    }

    [Test]
    public async Task Shipping_Records_The_Event_Rather_Than_Delivering_It()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await ShipAsync(customer);

        await _api.InDatabaseAsync(async database =>
        {
            // Recorded inside the shipping transaction. Not delivered: the notification does not exist
            // yet, because reaching outside the process must wait for the commit.
            await Assert.That(await database.Set<OutboxMessage>().CountAsync()).IsEqualTo(1);
            await Assert.That(await database.ShipmentNotifications.CountAsync()).IsEqualTo(0);
            _ = orderId;
        });
    }

    [Test]
    public async Task A_Delivery_Pass_Notifies_The_Customer()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);

        int attempted = await DeliverAsync();

        await Assert.That(attempted).IsEqualTo(1);

        await _api.InDatabaseAsync(async database =>
        {
            await Assert.That(await database.ShipmentNotifications.CountAsync()).IsEqualTo(1);

            OutboxMessage message = await database.Set<OutboxMessage>().SingleAsync();
            await Assert.That(message.DeliveredAt).IsNotNull();
            await Assert.That(message.Attempts).IsEqualTo(1);
        });
    }

    [Test]
    public async Task Delivery_Does_Not_Repeat_Itself()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);

        await DeliverAsync();
        int second = await DeliverAsync();

        await Assert.That(second).IsEqualTo(0);

        await _api.InDatabaseAsync(async database =>
            await Assert.That(await database.ShipmentNotifications.CountAsync()).IsEqualTo(1));
    }

    [Test]
    public async Task A_Repeated_Delivery_Produces_No_Second_Notification()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        await ShipAsync(customer);
        await DeliverAsync();

        // Delivery is at least once, so a handler has to tolerate running twice. Forcing a redelivery
        // proves this one does.
        await _api.InDatabaseAsync(async database =>
        {
            OutboxMessage message = await database.Set<OutboxMessage>().SingleAsync();
            message.DeliveredAt = null;
            await database.SaveChangesAsync();
        });

        await DeliverAsync();

        await _api.InDatabaseAsync(async database =>
            await Assert.That(await database.ShipmentNotifications.CountAsync()).IsEqualTo(1));
    }

    [Test]
    public async Task An_Ordinary_Event_Does_Not_Go_Through_The_Outbox()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer);

        await _api.ClientFor(customer).PostAsync($"/orders/{orderId}/cancel", null);

        await _api.InDatabaseAsync(async database =>
        {
            // The choice is made per event, so cancelling still dispatches inside the transaction.
            await Assert.That(await database.CancellationRecords.CountAsync()).IsEqualTo(1);
            await Assert.That(await database.Set<OutboxMessage>().CountAsync()).IsEqualTo(0);
        });
    }

    private async Task<int> DeliverAsync()
    {
        using IServiceScope scope = _api.Services.CreateScope();
        OutboxProcessor<OrderingDbContext> processor = scope.ServiceProvider
            .GetRequiredService<OutboxProcessor<OrderingDbContext>>();

        return await processor.DeliverPendingAsync();
    }

    private async Task<Guid> ShipAsync(Id<Customer> customer)
    {
        Guid orderId = await PlaceAsync(customer);
        HttpResponseMessage response = await _api.ClientFor(customer).PostAsync($"/orders/{orderId}/ship", null);
        response.EnsureSuccessStatusCode();
        return orderId;
    }

    private async Task<Guid> PlaceAsync(Id<Customer> customer)
    {
        HttpResponseMessage response = await _api.ClientFor(customer).PostAsJsonAsync("/orders", new
        {
            placedOn = "2026-07-30",
            lines = new[] { new { sku = "sku-1", amount = 100 } },
        });

        response.EnsureSuccessStatusCode();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("orderId").GetGuid();
    }
}
