using System.Reflection;
using Microsoft.Extensions.Configuration;
using MyApp.Worker.Infrastructure;

namespace MyApp.ArchitectureTests;

/// <summary>
/// Configuration is read while the application is composed, never by something the container built.
/// </summary>
/// <remarks>
/// Reading a value in <c>Program.cs</c> or a startup extension is composition. A service taking
/// <see cref="IConfiguration" /> is different in kind: it hides a dependency its constructor does not
/// declare, and it cannot be tested without standing up configuration. Constructor parameters are what
/// separates the two, so that is what this asserts.
/// </remarks>
public sealed class ConfigurationBoundaryTests
{
    [Test]
    public async Task Nothing_Takes_IConfiguration_As_A_Constructor_Parameter()
    {
        string[] offenders =
        [
            .. typeof(AppDbContext).Assembly.GetTypes()
                .SelectMany(type => type.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(constructor => new { type, constructor }))
                .Where(candidate => candidate.constructor.GetParameters()
                    .Any(parameter => typeof(IConfiguration).IsAssignableFrom(parameter.ParameterType)))
                .Select(candidate => candidate.type.FullName ?? candidate.type.Name)
                .Distinct(),
        ];

        await Assert.That(offenders).IsEmpty();
    }
}
