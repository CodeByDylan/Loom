using FluentValidation;
using Loom.Handlers;
using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Infrastructure;
using MyApp.Worker.Workers;

namespace MyApp.Worker;

/// <summary>
/// Everything the worker needs, registered in one place.
/// </summary>
/// <remarks>
/// Shared with the test host on purpose. A test that rebuilds the service graph by hand is testing a
/// graph nobody runs: the decorator chain, the validators and the interceptor can all be registered
/// differently there, and the difference only shows up in production. This is the composition root, so
/// reading configuration here is composition rather than a service reaching for it.
/// <para>
/// Configuration is required rather than optional. An overload the test host could omit would leave the
/// settings unbound and unvalidated in exactly the graph that claims to be the real one.
/// </para>
/// </remarks>
internal static class WorkerServices
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        // Injected rather than taken from DateTime.Now, which is also what makes the schedule testable.
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

        services.AddLoomPersistence();

        services.AddSingleton<RetireWidgetsPass>();

        // Declared once and applied to every handler, so a slice cannot be registered without
        // validation by forgetting a call. Declaration order is nesting order.
        services
            .AddLoomHandlers(chain => chain
                .WithLogging()
                .WithValidation()
                .WithDomainEventFailures())
            .AddHandler<Handler, Request, Response>();

        // includeInternalTypes matters: slice validators are internal, and without it none are
        // registered. The validating decorator treats a missing validator as nothing to validate, so
        // the omission would be silent.
        services.AddValidatorsFromAssembly(
            typeof(WorkerServices).Assembly,
            ServiceLifetime.Scoped,
            includeInternalTypes: true);

        services.AddOptions<RetireWidgetsOptions>()
            .Bind(configuration.GetSection(RetireWidgetsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
