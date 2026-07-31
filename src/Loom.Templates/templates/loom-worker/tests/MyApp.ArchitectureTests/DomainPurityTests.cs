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
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Npgsql",
            "Microsoft.Extensions.DependencyInjection",
        ];

        ArchTestResult result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        // Named, because "false" does not say which type reached for what.
        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
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
