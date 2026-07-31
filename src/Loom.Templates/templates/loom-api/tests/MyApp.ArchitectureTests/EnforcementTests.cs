using System.Reflection;
using Loom.Entities;
using MyApp.Api.Infrastructure;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace MyApp.ArchitectureTests;

/// <summary>
/// The structural rules the guidance lists, asserted rather than reviewed.
/// </summary>
/// <remarks>
/// A structural rule with no test is a rule that erodes: nothing about a slice reaching into another
/// one fails to compile, and nothing about an entity crossing a contract boundary does either.
/// </remarks>
public sealed class EnforcementTests
{
    private static readonly Assembly Host = typeof(AppDbContext).Assembly;

    private static readonly string SliceRoot = $"{Host.GetName().Name}.Features.";

    [Test]
    public async Task No_Slice_Depends_On_Another_Slice()
    {
        string[] slices = Slices();

        // The count is asserted, not just its being non-empty. With one slice the comparison below has
        // nothing to compare and passes without checking anything, so the number is stated here and a
        // new slice fails this deliberately — the same reason the route table is snapshotted rather
        // than counted. The independence check becomes load-bearing at the second slice.
        await Assert.That(slices.Length).IsEqualTo(3);

        List<string> offenders = [];

        foreach (string slice in slices)
        {
            string[] others = [.. slices.Where(candidate => candidate != slice)];

            if (others.Length is 0)
            {
                continue;
            }

            ArchTestResult result = Types.InAssembly(Host)
                .That().ResideInNamespace(slice)
                .Should().NotHaveDependencyOnAny(others)
                .GetResult();

            offenders.AddRange(result.FailingTypeNames ?? []);
        }

        // Slices are independent by construction, not by convention. One reaching into another is how
        // a vertical slice quietly becomes a layer.
        await Assert.That(offenders).IsEmpty();
    }

    [Test]
    public async Task Domain_Entities_Do_Not_Cross_A_Contract_Boundary()
    {
        Type[] contracts = [.. Host.GetTypes().Where(type => type.Name is "Request" or "Response")];

        await Assert.That(contracts).IsNotEmpty();

        List<string> offenders =
        [
            .. from contract in contracts
               from property in contract.GetProperties()
               where NamesAnEntity(property.PropertyType)
               select $"{contract.FullName}.{property.Name}",
        ];

        // An entity on a request or a response leaks persistence and invariants into the wire format,
        // and couples the two to each other for good.
        await Assert.That(offenders).IsEmpty();
    }

    // A slice is a namespace with a handler in it. The aggregate folder above them holds shared
    // pieces such as the entity configuration and is not one — and namespace matching is by prefix, so
    // counting a parent would report it as depending on its own children.
    private static string[] Slices() =>
        [.. Host.GetTypes()
            .Where(type => type.Name is "Handler"
                && type.Namespace is not null
                && type.Namespace.StartsWith(SliceRoot, StringComparison.Ordinal))
            .Select(type => type.Namespace!)
            .Distinct()
            .Order(StringComparer.Ordinal)];

    private static bool NamesAnEntity(Type type)
    {
        // Id<TEntity> names an entity without being one. Unwrapping through it would flag every
        // correctly typed identifier, which is the opposite of the rule.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Id<>))
        {
            return false;
        }

        // An array is not generic and its base chain runs to Array, so without this a Widget[] on a
        // contract would go unnoticed.
        if (type.IsArray)
        {
            return NamesAnEntity(type.GetElementType()!);
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(NamesAnEntity))
        {
            return true;
        }

        for (Type? candidate = type; candidate is not null; candidate = candidate.BaseType)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}
