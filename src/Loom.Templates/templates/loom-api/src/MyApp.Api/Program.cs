using FluentValidation;
using Loom.Handlers;
using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Telemetry, health, resilience and service discovery, from the scaffolded defaults project.
builder.AddServiceDefaults();

// Read once and refused here, so the failure lands at startup rather than on the first request.
// The AppHost supplies this in development.
string connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException(
        "No 'database' connection string was configured. The AppHost provides one when running locally; "
        + "a deployment supplies it through configuration.");

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

builder.Services.AddLoomPersistence();

// The chain is declared once and applies to every handler, so a slice cannot be registered without
// validation by forgetting a call. Declaration order is nesting order: logging outermost, so nothing
// goes unrecorded — including a request refused by validation, which is the outcome most worth seeing.
builder.Services
    .AddLoomHandlers(chain => chain
        .WithLogging()
        .WithValidation()
        // Innermost, so it only sees the handler's own save. Without it, a domain event handler that
        // reports a failure escapes as an unhandled exception and every such failure surfaces as a
        // 500 — even though nothing exceptional happened.
        .WithDomainEventFailures())
    .AddHandler<MyApp.Api.Features.Widgets.CreateWidget.Handler,
        MyApp.Api.Features.Widgets.CreateWidget.Request,
        MyApp.Api.Features.Widgets.CreateWidget.Response>()
    .AddHandler<MyApp.Api.Features.Widgets.GetWidget.Handler,
        MyApp.Api.Features.Widgets.GetWidget.Request,
        MyApp.Api.Features.Widgets.GetWidget.Response>()
    .AddHandler<MyApp.Api.Features.Widgets.ListWidgets.Handler,
        MyApp.Api.Features.Widgets.ListWidgets.Request,
        Loom.Paging.Page<MyApp.Api.Features.Widgets.ListWidgets.Response>>();

// includeInternalTypes matters: slice validators are internal, and without it none are registered.
// The validating decorator treats a missing validator as nothing to validate, so the omission would
// be silent — which is why RegistrationTests asserts every validator is resolvable.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped, includeInternalTypes: true);

builder.Services.AddProblemDetails();

builder.Services.AddAuthorization();

WebApplication app = builder.Build();

// Both lean on the ProblemDetails service registered above. Without them, only failures that pass
// through ToHttpResult() came back as problem details — an unhandled exception was a bodyless 500 and
// an unmatched route a bodyless 404, breaking the rule that every non-2xx response is ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthorization();

app.MapDefaultEndpoints();

// Every slice is mapped into a group that requires authorization, so the failure you get is "I forgot
// to open this up" rather than the reverse. A slice opts out with AllowAnonymous, as both examples do.
//
// > **UNDECIDED:** which identity provider issues tokens. Until one is chosen no authentication scheme
// > is registered, so an endpoint that does not opt out has nothing to authenticate against.
app.MapGroup(string.Empty)
    .RequireAuthorization()
    .MapEndpoints(typeof(Program).Assembly);

await app.RunAsync();

// Exposed so the test host can reference this assembly through WebApplicationFactory<Program>.
public sealed partial class Program;
