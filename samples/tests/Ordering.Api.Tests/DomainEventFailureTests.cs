using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Tests;

/// <summary>
/// A domain event handler that reports a failure produces the status code that failure means, not a
/// server error.
/// </summary>
/// <remarks>
/// Before the translating decorator existed, the abandoned save escaped as an unhandled exception and
/// every such failure surfaced as a 500 — even though nothing exceptional had happened.
/// </remarks>
[NotInParallel]
public sealed class DomainEventFailureTests
{
    private OrderingApi _api = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _api = ApiFixture.Api;
        await _api.ResetAsync();
    }

    [Test]
    public async Task A_Failing_Event_Handler_Answers_Its_Own_Category()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer);

        HttpClient client = _api.ClientFor(customer, services =>
            services.AddScoped<IDomainEventHandler<OrderCancelled>, RefusingHandler>());

        HttpResponseMessage response = await client.PostAsync($"/orders/{orderId}/cancel", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo("refunds.down");
    }

    [Test]
    public async Task Nothing_Is_Committed_When_An_Event_Handler_Fails()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer);

        HttpClient client = _api.ClientFor(customer, services =>
            services.AddScoped<IDomainEventHandler<OrderCancelled>, RefusingHandler>());

        await client.PostAsync($"/orders/{orderId}/cancel", null);

        // Reporting the failure properly must not weaken the atomicity it was protecting: the order is
        // still open, and the cancellation record the other handler writes is absent.
        await _api.InDatabaseAsync(async database =>
        {
            Order order = await database.Orders.AsNoTracking().SingleAsync();
            await Assert.That(order.Status).IsEqualTo(OrderStatus.Placed);
            await Assert.That(await database.CancellationRecords.CountAsync()).IsEqualTo(0);
        });
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

    private sealed class RefusingHandler : IDomainEventHandler<OrderCancelled>
    {
        public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(
                Errors.Unavailable("refunds.down", "The refund service is unreachable.")));
    }
}
