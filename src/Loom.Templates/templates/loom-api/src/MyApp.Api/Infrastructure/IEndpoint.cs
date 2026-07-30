namespace MyApp.Api.Infrastructure;

/// <summary>
/// Implemented by a slice to register its own route.
/// </summary>
/// <remarks>
/// Discovery is reflective, so nothing in <c>Program.cs</c> links to an endpoint and a slice that
/// fails to register does not fail to compile. That is what makes the route-table test mandatory
/// rather than nice to have.
/// </remarks>
internal interface IEndpoint
{
    /// <summary>Registers exactly one route.</summary>
    static abstract void Map(IEndpointRouteBuilder routes);
}

/// <summary>Maps every <see cref="IEndpoint" /> in an assembly.</summary>
internal static class EndpointExtensions
{
    /// <summary>Finds and maps every endpoint the assembly declares.</summary>
    public static void MapEndpoints(this IEndpointRouteBuilder routes, System.Reflection.Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (Type type in assembly.GetTypes().Where(candidate =>
            candidate is { IsAbstract: false, IsInterface: false } && candidate.IsAssignableTo(typeof(IEndpoint))))
        {
            type.GetMethod(nameof(IEndpoint.Map))?.Invoke(null, [routes]);
        }
    }
}
