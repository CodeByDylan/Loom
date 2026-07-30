using FluentValidation;
using Loom.Handlers;
using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;
using MyApp.Worker.Infrastructure;
using MyApp.Worker.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Telemetry, health, resilience and service discovery, from the scaffolded defaults project.
builder.AddServiceDefaults();

// Read once and refused here, so the failure lands at startup rather than on the first iteration.
string connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException(
        "No 'database' connection string was configured. The AppHost provides one when running "
        + "locally; a deployment supplies it through configuration.");

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

builder.Services.AddLoomPersistence();

// Injected rather than taken from DateTime.Now, which is also what makes the schedule testable.
builder.Services.AddSingleton(TimeProvider.System);

// The chain is declared once and applies to every handler. Declaration order is nesting order.
builder.Services
    .AddLoomHandlers(chain => chain
        .WithLogging()
        .WithValidation()
        .WithDomainEventFailures())
    .AddHandler<Handler, Request, Response>();

// includeInternalTypes matters: slice validators are internal, and without it none are registered.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped, includeInternalTypes: true);

builder.Services.AddHostedService<RetireWidgetsWorker>();

IHost host = builder.Build();

await host.RunAsync();

// Exposed so the test assembly can name this one.
public partial class Program;
