using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Api.Tests;

/// <summary>
/// Asserts the complete set of registered routes.
/// </summary>
/// <remarks>
/// Mandatory, because endpoints are discovered by scanning rather than by being referenced. Nothing
/// links the application's start-up to any endpoint, so a slice that fails to register does not fail
/// to compile — it simply returns 404 in production. This test turns that into a failing build.
/// <para>
/// When a slice is added, update the expected set deliberately. Do not delete the assertion.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class RouteTableTests
{
    private static readonly string[] Expected =
    [
        // Contributed by the scaffolded service defaults, in development only, which is why the test
        // host pins its environment. They answer any verb, hence the marker rather than a method.
        "ANY /health",
        "ANY /alive",
        "POST /orders",
        "POST /orders/{orderId}/cancel",
        "POST /orders/{orderId}/ship",
        "GET /orders",
        "GET /orders/{orderId}",
    ];

    [Test]
    public async Task Every_Expected_Route_Is_Registered_And_No_Others()
    {
        EndpointDataSource endpoints = ApiFixture.Api.Services.GetRequiredService<EndpointDataSource>();

        string[] actual =
        [
            .. endpoints.Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint =>
                    $"{string.Join(',', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"])} "
                    + $"/{endpoint.RoutePattern.RawText?.TrimStart('/')}")
                .Order(StringComparer.Ordinal),
        ];

        await Assert.That(actual).IsEquivalentTo([.. Expected.Order(StringComparer.Ordinal)])
            .Because("the registered routes were: " + string.Join(", ", actual));
    }
}
