using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loom.Entities;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Customers;

namespace Ordering.Api.Tests;

/// <summary>
/// Every slice goes through HTTP rather than calling its handler, because model binding, validation,
/// authorization and result mapping are most of what can break.
/// </summary>
[NotInParallel]
public sealed class OrderLifecycleTests
{
    private OrderingApi _api = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _api = ApiFixture.Api;
        await _api.ResetAsync();
    }

    [Test]
    public async Task Placing_An_Order_Returns_Created_With_Its_Total()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        HttpClient client = _api.ClientFor(customer);

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", NewOrder(("sku-1", 300), ("sku-2", 200)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("total").GetInt32()).IsEqualTo(500);
    }

    [Test]
    public async Task An_Order_With_No_Lines_Is_Rejected_As_A_Validation_Problem()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        HttpClient client = _api.ClientFor(customer);

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", NewOrder());

        // The validation decorator produced this, and its metadata became the problem details errors
        // extension with no translation.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.TryGetProperty("errors", out _)).IsTrue();
    }

    [Test]
    public async Task An_Unknown_Order_Is_Not_Found()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        HttpClient client = _api.ClientFor(customer);

        HttpResponseMessage response = await client.GetAsync($"/orders/{Id<Domain.Orders.Order>.New()}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo("orders.not_found");
    }

    [Test]
    public async Task Another_Customers_Order_Is_Forbidden()
    {
        Id<Customer> mine = await _api.AddCustomerAsync("Mine");
        Id<Customer> theirs = await _api.AddCustomerAsync("Theirs");

        Guid orderId = await PlaceAsync(theirs);

        HttpResponseMessage response = await _api.ClientFor(mine).GetAsync($"/orders/{orderId}");

        // Deciding this needs the order, so it cannot be a policy — the handler reports Forbidden.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task An_Endpoint_Is_Closed_Without_A_Token()
    {
        HttpResponseMessage response = await _api.AnonymousClient().GetAsync("/orders");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Fetching_An_Order_Returns_Its_Lines()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer, ("sku-1", 100), ("sku-2", 250));

        JsonElement body = await _api.ClientFor(customer)
            .GetFromJsonAsync<JsonElement>($"/orders/{orderId}");

        await Assert.That(body.GetProperty("lines").GetArrayLength()).IsEqualTo(2);
        await Assert.That(body.GetProperty("total").GetInt32()).IsEqualTo(350);
        await Assert.That(body.GetProperty("status").GetString()).IsEqualTo("Placed");
    }

    [Test]
    public async Task Listing_Pages_And_Reports_The_Total()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();

        for (int index = 0; index < 5; index++)
        {
            await PlaceAsync(customer, ($"sku-{index}", 10 + index));
        }

        JsonElement body = await _api.ClientFor(customer)
            .GetFromJsonAsync<JsonElement>("/orders?page=1&size=2");

        await Assert.That(body.GetProperty("items").GetArrayLength()).IsEqualTo(2);
        await Assert.That(body.GetProperty("totalCount").GetInt32()).IsEqualTo(5);
        await Assert.That(body.GetProperty("totalPages").GetInt32()).IsEqualTo(3);
        await Assert.That(body.GetProperty("hasNext").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Listing_Only_Returns_The_Callers_Orders()
    {
        Id<Customer> mine = await _api.AddCustomerAsync("Mine");
        Id<Customer> theirs = await _api.AddCustomerAsync("Theirs");

        await PlaceAsync(mine);
        await PlaceAsync(theirs);
        await PlaceAsync(theirs);

        JsonElement body = await _api.ClientFor(mine).GetFromJsonAsync<JsonElement>("/orders");

        await Assert.That(body.GetProperty("totalCount").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task An_Excessive_Page_Size_Is_A_Validation_Problem()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();

        HttpResponseMessage response = await _api.ClientFor(customer).GetAsync("/orders?size=100000");

        // Guarded before the page request is constructed, so an unbounded page size cannot become a
        // query that returns the table.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Cancelling_Succeeds_And_Records_Atomically()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer, ("sku-1", 400));

        HttpResponseMessage response = await _api.ClientFor(customer).PostAsync($"/orders/{orderId}/cancel", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        await _api.InDatabaseAsync(async database =>
        {
            // Written by an ordinary domain event handler, inside the same transaction as the
            // cancellation, without the handler ever calling SaveChanges.
            await Assert.That(await database.CancellationRecords.CountAsync()).IsEqualTo(1);
            await Assert.That((await database.CancellationRecords.SingleAsync()).Total).IsEqualTo(400);
        });
    }

    [Test]
    public async Task Cancelling_A_Shipped_Order_Is_A_Conflict()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer);
        HttpClient client = _api.ClientFor(customer);

        await client.PostAsync($"/orders/{orderId}/ship", null);
        HttpResponseMessage response = await client.PostAsync($"/orders/{orderId}/cancel", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo("orders.already_shipped");
    }

    [Test]
    public async Task Cancelling_Twice_Is_A_Conflict()
    {
        Id<Customer> customer = await _api.AddCustomerAsync();
        Guid orderId = await PlaceAsync(customer);
        HttpClient client = _api.ClientFor(customer);

        await client.PostAsync($"/orders/{orderId}/cancel", null);
        HttpResponseMessage response = await client.PostAsync($"/orders/{orderId}/cancel", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    private async Task<Guid> PlaceAsync(Id<Customer> customer, params (string Sku, int Amount)[] lines)
    {
        (string, int)[] effective = lines.Length is 0 ? [("sku-default", 100)] : lines;

        HttpResponseMessage response = await _api.ClientFor(customer)
            .PostAsJsonAsync("/orders", NewOrder(effective));

        response.EnsureSuccessStatusCode();

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("orderId").GetGuid();
    }

    private static object NewOrder(params (string Sku, int Amount)[] lines) => new
    {
        placedOn = "2026-07-30",
        lines = lines.Select(line => new { sku = line.Sku, amount = line.Amount }).ToArray(),
    };

}
