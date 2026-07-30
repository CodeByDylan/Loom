using Loom.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Api.Tests;

/// <summary>
/// Asserts every handler in the application is resolvable through its interface.
/// </summary>
/// <remarks>
/// Handlers are registered by hand, one call per slice, so a new slice can be written, wired to an
/// endpoint, and shipped without ever being registered. The endpoint would then fail at the first
/// request rather than at build time. This closes that gap the same way the route table snapshot closes
/// the discovery gap.
/// </remarks>
[NotInParallel]
public sealed class HandlerRegistrationTests
{
    [Test]
    public async Task Every_Handler_Is_Registered()
    {
        (Type Implementation, Type Interface)[] declared =
        [
            .. typeof(Program).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .SelectMany(type => type.GetInterfaces()
                    .Where(contract => contract.IsGenericType
                        && (contract.GetGenericTypeDefinition() == typeof(IHandler<,>)
                            || contract.GetGenericTypeDefinition() == typeof(IHandler<>)))
                    .Select(contract => (Implementation: type, Interface: contract))),
        ];

        await Assert.That(declared.Length).IsGreaterThan(0);

        using IServiceScope scope = ApiFixture.Api.Services.CreateScope();

        foreach ((Type implementation, Type contract) in declared)
        {
            object? resolved = scope.ServiceProvider.GetService(contract);

            await Assert.That(resolved).IsNotNull()
                .Because($"'{implementation.Name}' implements '{contract.Name}' but is not registered, "
                    + "so its endpoint would fail at the first request.");
        }
    }
}
