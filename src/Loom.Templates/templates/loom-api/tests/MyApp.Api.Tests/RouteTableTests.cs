using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Api.Tests;

/// <summary>
/// The complete set of registered routes, asserted.
/// </summary>
/// <remarks>
/// Mandatory, not optional. Endpoints are discovered by assembly scan, so nothing in Program.cs links
/// to a slice: one that fails to register does not fail to compile, and would 404 in production
/// instead. This turns that into a failing build.
/// <para>
/// When this fails because you added a slice, update the expected set deliberately. Do not delete the
/// assertion.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class RouteTableTests
{
    [Test]
    public async Task The_Route_Table_Is_What_We_Expect()
    {
        EndpointDataSource routes = AppFixture.App.Services.GetRequiredService<EndpointDataSource>();

        string[] actual =
        [
            .. routes.Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
                .Where(pattern => !pattern.StartsWith("/health", StringComparison.Ordinal)
                    && !pattern.StartsWith("/alive", StringComparison.Ordinal))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];

        string[] expected = ["/widgets", "/widgets/{widgetId}"];

        await Assert.That(actual).IsEquivalentTo(expected);
    }
}
