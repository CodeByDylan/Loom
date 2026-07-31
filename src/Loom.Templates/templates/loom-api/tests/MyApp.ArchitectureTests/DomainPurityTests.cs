using System.Reflection;
using MyApp.Domain.Widgets;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace MyApp.ArchitectureTests;

/// <summary>
/// The structural rules, asserted rather than reviewed.
/// </summary>
public sealed class DomainPurityTests
{
    private static readonly Assembly Domain = typeof(Widget).Assembly;

    [Test]
    public async Task The_Domain_Depends_On_Nothing_But_Loom_And_The_Base_Class_Library()
    {
        // An allowlist, not a list of things to avoid. A denylist only refuses what someone thought to
        // name — Dapper, Newtonsoft.Json and MediatR would all have passed one — so the rule is stated
        // as what is permitted and everything else fails by default.
        string[] offenders =
        [
            .. Domain.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => !name.StartsWith("Loom.", StringComparison.Ordinal)
                    && !name.StartsWith("System.", StringComparison.Ordinal)
                    && !name.Equals("System", StringComparison.Ordinal)
                    && !name.Equals("netstandard", StringComparison.Ordinal)
                    && !name.Equals("mscorlib", StringComparison.Ordinal)),
        ];

        await Assert.That(offenders).IsEmpty();
    }

    [Test]
    public async Task Entities_Are_Sealed()
    {
        ArchTestResult result = Types.InAssembly(Domain)
            .That().AreClasses().And().ArePublic()
            .Should().BeSealed()
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
}
