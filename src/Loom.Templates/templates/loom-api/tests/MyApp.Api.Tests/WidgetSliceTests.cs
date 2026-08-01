using System.Net;
using System.Net.Http.Json;
using MyApp.Api.Infrastructure;

namespace MyApp.Api.Tests;

/// <summary>
/// Every slice is tested through HTTP, not by calling the handler.
/// </summary>
/// <remarks>
/// Calling a handler directly skips model binding, validation and the result mapping — which is most
/// of what can actually break.
/// </remarks>
[NotInParallel]
public sealed class WidgetSliceTests
{
    private TestApp _app = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _app = AppFixture.App;
        await _app.ResetAsync();
    }

    [Test]
    public async Task A_Response_Outside_The_Slices_Is_Still_A_Problem()
    {
        // The rule is every non-2xx response, not every slice failure. A route no slice owns never
        // reaches ToHttpResult(), so only the status-code middleware stands between this request and
        // a bodyless 404 — which is exactly what came back before Program.cs added it.
        using HttpClient client = _app.Client();

        HttpResponseMessage response = await client.GetAsync(new Uri("/no-such-route", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        // The media type is a header; only parsing the body proves a problem document is in it.
        Microsoft.AspNetCore.Mvc.ProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(404);
    }

    [Test]
    public async Task A_Widget_Is_Created_And_Read_Back()
    {
        using HttpClient client = _app.Client();

        HttpResponseMessage created = await client.PostAsJsonAsync("/widgets", new { name = "bolt", size = 3 });
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);

        WidgetResponse? body = await created.Content.ReadFromJsonAsync<WidgetResponse>();
        await Assert.That(body).IsNotNull();

        HttpResponseMessage read = await client.GetAsync(new Uri($"/widgets/{body!.WidgetId}", UriKind.Relative));
        await Assert.That(read.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task An_Invalid_Request_Is_Refused_Before_The_Handler_Runs()
    {
        using HttpClient client = _app.Client();

        // Too long for the validator, but acceptable to Widget.Create — which is the point. An empty
        // name would be refused by the domain as well, so the test would pass with the validating
        // decorator removed and prove nothing. Only the length rule separates the two, so this fails
        // if the decorator is ever dropped from the chain.
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/widgets",
            new { name = new string('w', 201), size = 3 });

        // The decorator turns this into an Invalid failure, which maps to 400 with the errors
        // extension populated. Nothing in the slice writes a status code.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_Missing_Widget_Reports_Not_Found()
    {
        using HttpClient client = _app.Client();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/widgets/{Guid.CreateVersion7()}", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Listing_Pages_And_Filters_By_Size()
    {
        using HttpClient client = _app.Client();

        foreach (int size in (int[])[5, 50, 500])
        {
            HttpResponseMessage created = await client.PostAsJsonAsync(
                "/widgets",
                new { name = $"widget-{size}", size });

            await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        // Two match the specification; one page of one proves the caller decides the size and that the
        // total still counts everything the rule matched.
        HttpResponseMessage response = await client.GetAsync(
            new Uri("/widgets?largerThan=10&number=1&pageSize=1", UriKind.Relative));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        WidgetPage? page = await response.Content.ReadFromJsonAsync<WidgetPage>();

        await Assert.That(page).IsNotNull();
        await Assert.That(page!.TotalCount).IsEqualTo(2);
        await Assert.That(page.Items.Count).IsEqualTo(1);

        // The specification orders largest first, so paging cannot reorder it.
        await Assert.That(page.Items[0].Size).IsEqualTo(500);
    }

    private sealed record WidgetResponse(Guid WidgetId);

    private sealed record WidgetPage(IReadOnlyList<WidgetSummary> Items, int TotalCount, int Number, int Size);

    private sealed record WidgetSummary(Guid WidgetId, string Name, int Size);
}
