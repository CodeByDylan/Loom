using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Worker.Tests;

/// <summary>
/// Registers the worker's validators into a test service graph.
/// </summary>
/// <remarks>
/// includeInternalTypes matters: slice validators are internal, and without it none are registered.
/// The validating decorator treats a missing validator as nothing to validate, so the omission would
/// be silent — a test graph that skipped this would pass while the real one refused nothing.
/// </remarks>
internal static class ValidatorRegistration
{
    public static IServiceCollection AddValidatorsFromWorker(this IServiceCollection services) =>
        services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped, includeInternalTypes: true);
}
