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

    /// <summary>What the running framework itself ships, by assembly name.</summary>
    /// <remarks>
    /// Read from the runtime directory rather than matched on a <c>System.</c> prefix, because a NuGet
    /// package can ship under that prefix — <c>System.Data.SqlClient</c> and <c>System.Data.SQLite</c>
    /// both do. A prefix would have admitted a database driver into the domain, which is the exact
    /// dependency this test exists to refuse.
    /// </remarks>
    private static readonly HashSet<string> FrameworkAssemblies = ReadFrameworkAssemblyNames();

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
                    && !FrameworkAssemblies.Contains(name)),
        ];

        await Assert.That(offenders).IsEmpty();
    }

    [Test]
    public async Task Public_Domain_Classes_Are_Sealed()
    {
        ArchTestResult result = Types.InAssembly(Domain)
            .That().AreClasses().And().ArePublic()
            .Should().BeSealed()
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }

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
}
