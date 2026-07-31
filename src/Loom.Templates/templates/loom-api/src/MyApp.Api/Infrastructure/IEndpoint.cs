using System.Reflection;

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
            // Not null-forgiving: an explicitly implemented Map has no public static method to find,
            // and skipping it would register nothing while looking like it worked — the failure this
            // whole interface exists to avoid.
            MethodInfo map = type.GetMethod(nameof(IEndpoint.Map))
                ?? throw new InvalidOperationException(
                    $"{type.Name} implements IEndpoint but exposes no public static Map. Implement it "
                    + "implicitly; an explicit implementation cannot be discovered.");

            map.Invoke(null, [routes]);
        }
    }
}
