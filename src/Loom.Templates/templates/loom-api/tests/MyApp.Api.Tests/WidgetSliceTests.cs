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

    private sealed record WidgetResponse(Guid WidgetId);
}
