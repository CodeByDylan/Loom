using System.Reflection;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// A slice's HTTP surface. One implementation per operation, mapping exactly one route.
/// </summary>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder routes);
}

public static class EndpointRegistration
{
    /// <summary>
    /// Maps every endpoint in the assembly.
    /// </summary>
    /// <remarks>
    /// Discovery is reflective, which is convenient and also invisible: nothing in Program.cs links to
    /// any endpoint, so a slice that fails to register does not fail to compile. That is why the route
    /// table is covered by a snapshot test, and why this project does not enable ahead-of-time
    /// compilation.
    /// </remarks>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder routes, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(assembly);

        IEnumerable<MethodInfo> maps = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IEndpoint).IsAssignableFrom(type))
            .Select(type => type.GetMethod(
                nameof(IEndpoint.Map),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method is not null)
            .Select(method => method!)
            .OrderBy(method => method.DeclaringType!.FullName, StringComparer.Ordinal);

        foreach (MethodInfo map in maps)
        {
            map.Invoke(obj: null, [routes]);
        }

        return routes;
    }
}
