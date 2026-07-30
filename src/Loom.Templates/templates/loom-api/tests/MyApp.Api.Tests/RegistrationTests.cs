using FluentValidation;
using Loom.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Api.Tests;

/// <summary>
/// Every handler and every validator resolves.
/// </summary>
/// <remarks>
/// A validator that is not registered is silently ignored — the decorator treats a missing one as
/// nothing to validate, which is right for a slice with no rules and disastrous for one with them.
/// Assembly scanning excludes internal types unless asked, and slice validators are internal.
/// </remarks>
[NotInParallel]
public sealed class RegistrationTests
{
    [Test]
    public async Task Every_Validator_In_The_Application_Resolves()
    {
        using IServiceScope scope = AppFixture.App.Services.CreateScope();

        Type[] requestTypes =
        [
            .. typeof(Program).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false }
                    && type.BaseType is { IsGenericType: true } baseType
                    && baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
                .Select(type => type.BaseType!.GetGenericArguments()[0]),
        ];

        await Assert.That(requestTypes).IsNotEmpty();

        foreach (Type requestType in requestTypes)
        {
            Type validatorType = typeof(IValidator<>).MakeGenericType(requestType);
            await Assert.That(scope.ServiceProvider.GetService(validatorType)).IsNotNull();
        }
    }

    [Test]
    public async Task Every_Handler_Resolves_Through_Its_Decorator_Chain()
    {
        using IServiceScope scope = AppFixture.App.Services.CreateScope();

        // Discovered rather than listed. Naming one handler would only ever prove that handler is
        // registered, so a new slice whose AddHandler call was forgotten would still pass — and a
        // handler that is never registered fails at the first request instead of at the first test.
        Type[] handlerInterfaces =
        [
            .. typeof(Program).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .SelectMany(type => type.GetInterfaces())
                .Where(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IHandler<,>))
                .Distinct(),
        ];

        await Assert.That(handlerInterfaces).IsNotEmpty();

        foreach (Type contract in handlerInterfaces)
        {
            await Assert.That(scope.ServiceProvider.GetService(contract)).IsNotNull();
        }
    }
}
