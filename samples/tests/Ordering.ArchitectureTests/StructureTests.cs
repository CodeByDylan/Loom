using System.Reflection;
using Loom.Entities;
using Microsoft.Extensions.Configuration;
using NetArchTest.Rules;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace Ordering.ArchitectureTests;

/// <summary>
/// The structural rules the guidance describes, asserted rather than reviewed.
/// </summary>
/// <remarks>
/// A structural rule that is not tested is a rule that erodes, because nothing fails when it is
/// broken. These are the five from the shared guidance, minus the one about container registration,
/// which needs a built application and therefore lives with the integration tests.
/// </remarks>
public sealed class StructureTests
{
    private static readonly Assembly Domain = typeof(Order).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly HashSet<string> FrameworkAssemblies = ReadFrameworkAssemblyNames();

    private static HashSet<string> ReadFrameworkAssemblyNames()
    {
        string location = typeof(object).Assembly.Location;
        string? directory = string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);

        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException(
                "Cannot locate the framework directory, so framework assemblies cannot be told from "
                + "packages. This test needs a normal, non-single-file test host.");
        }

        return
        [
            .. Directory.EnumerateFiles(directory, "*.dll")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>(),
        ];
    }

    [Test]
    public async Task The_Domain_References_Only_Loom_And_The_Base_Library()
    {
        // Framework assemblies are read from the runtime directory rather than matched on a System.
        // prefix: a NuGet package can ship under that prefix — System.Data.SqlClient and
        // System.Data.SQLite both do — so a prefix would admit a database driver into the domain.
        string[] offending =
        [
            .. Domain.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => !name.StartsWith("Loom.", StringComparison.Ordinal)
                    && !FrameworkAssemblies.Contains(name)),
        ];

        // The one boundary worth enforcing at compile time. If this list is ever non-empty, the domain
        // has acquired a dependency on infrastructure.
        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task No_Slice_Depends_On_Another_Slice()
    {
        string[] slices = [.. SliceNamespaces()];
        await Assert.That(slices.Length).IsGreaterThan(1);

        foreach (string slice in slices)
        {
            string[] others = [.. slices.Where(other => other != slice)];

            ArchTestResult result = Types.InAssembly(Api)
                .That().ResideInNamespace(slice)
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult();

            await Assert.That(result.IsSuccessful).IsTrue()
                .Because($"'{slice}' reaches into another slice: "
                    + string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Test]
    public async Task No_Domain_Entity_Appears_In_A_Slice_Contract()
    {
        Type[] entities = [typeof(Order), typeof(OrderLine), typeof(Customer)];

        List<string> offending = [];

        foreach (Type contract in Api.GetTypes().Where(IsSliceContract))
        {
            IEnumerable<Type> exposed = contract.GetProperties()
                .Select(property => property.PropertyType)
                .Concat(contract.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                .SelectMany(Unwrap);

            if (exposed.Any(entities.Contains))
            {
                offending.Add(contract.FullName!);
            }
        }

        // An entity crossing the transport boundary couples a client to the domain's shape and lets a
        // rename become a breaking API change.
        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task Configuration_Is_Read_Only_At_Start_Up()
    {
        ArchTestResult result = Types.InAssembly(Api)
            .That().DoNotHaveName("Program")
            .ShouldNot().HaveDependencyOn(typeof(IConfiguration).Namespace)
            .GetResult();

        string[] failing = [.. result.FailingTypeNames ?? []];

        // Anything but start-up reading configuration directly should be a typed options class instead.
        await Assert.That(failing).IsEmpty();
    }

    private static IEnumerable<string> SliceNamespaces() => Api.GetTypes()
        .Select(type => type.Namespace)
        .Where(candidate => candidate is not null
            && candidate.StartsWith("Ordering.Api.Features.", StringComparison.Ordinal)
            && candidate.Count(character => character is '.') is 4)
        .Select(candidate => candidate!)
        .Distinct();

    private static bool IsSliceContract(Type type) =>
        type.Namespace?.StartsWith("Ordering.Api.Features.", StringComparison.Ordinal) is true
        && type.Name is "Request" or "Response";

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        // Arrays carry their element type outside the generic arguments, so an Order[] would otherwise
        // slip past every check below.
        if (type.IsArray && type.GetElementType() is Type element)
        {
            foreach (Type nested in Unwrap(element))
            {
                yield return nested;
            }

            yield break;
        }

        // An identity names the entity it belongs to, and that is the point of it — Id<Order> in a
        // contract is type-safe and binds straight from a route. Only the entity itself must not
        // cross the boundary, so the identity's own argument is not followed.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Id<>))
        {
            yield break;
        }

        foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (Type nested in Unwrap(argument))
            {
                yield return nested;
            }
        }
    }
}
