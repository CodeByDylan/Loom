using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Api.Tests;

/// <summary>
/// Asserts every slice validator is actually resolvable.
/// </summary>
/// <remarks>
/// The validating decorator treats a missing validator as "nothing to validate", which is right for a
/// slice that has no rules and wrong for one whose validator failed to register. That failure is
/// silent: requests simply stop being validated.
/// <para>
/// It is not hypothetical. This suite caught exactly that — the assembly scan does not include internal
/// types unless asked, and slice validators are internal, so every validator in the application was
/// missing and no test of a valid request would ever have noticed.
/// </para>
/// </remarks>
[NotInParallel]
public sealed class ValidatorRegistrationTests
{
    [Test]
    public async Task Every_Validator_In_The_Application_Is_Registered()
    {
        Type[] declared =
        [
            .. typeof(Program).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .Select(type => new
                {
                    Type = type,
                    Validated = type.BaseType is { IsGenericType: true } baseType
                        && baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>)
                            ? baseType.GetGenericArguments()[0]
                            : null,
                })
                .Where(candidate => candidate.Validated is not null)
                .Select(candidate => candidate.Validated!),
        ];

        // A sanity check on the check: if the scan finds nothing, the assertion below is vacuous.
        await Assert.That(declared.Length).IsGreaterThan(0);

        using IServiceScope scope = ApiFixture.Api.Services.CreateScope();

        foreach (Type validated in declared)
        {
            Type validatorType = typeof(IValidator<>).MakeGenericType(validated);
            object? resolved = scope.ServiceProvider.GetService(validatorType);

            await Assert.That(resolved).IsNotNull()
                .Because($"'{validated.FullName}' declares a validator that is not registered, so its "
                    + "requests are silently not validated.");
        }
    }
}
