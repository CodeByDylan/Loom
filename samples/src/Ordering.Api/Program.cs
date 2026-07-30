using System.Text;
using FluentValidation;
using Loom.Entities;
using Loom.Handlers;
using Loom.Paging;
using Loom.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ordering.Api.Features.Orders.Events;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Telemetry, health, resilience and service discovery, from the scaffolded defaults project.
builder.AddServiceDefaults();

// Typed options only. IConfiguration appears here and nowhere else.
builder.Services
    .AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
    .ValidateDataAnnotations()
    // The development key is committed, so it is public. Refusing it anywhere else means a deployment
    // that never supplied a real one fails to start, rather than accepting forged tokens quietly.
    .Validate(
        authentication => builder.Environment.IsDevelopment()
            || authentication.SigningKey != AuthenticationOptions.DevelopmentSigningKey,
        "Authentication:SigningKey is still the development key. Supply a real one through user secrets "
        + "or environment configuration.")
    .ValidateOnStart();

// Read once, and refused here rather than inside the options callback, where the failure would surface
// on the first request instead of at startup. The AppHost supplies this in development.
string connectionString = builder.Configuration.GetConnectionString("ordering")
    ?? throw new InvalidOperationException(
        "No 'ordering' connection string was configured. The AppHost provides one when running the "
        + "sample; a deployment supplies it through configuration.");

builder.Services.AddDbContext<OrderingDbContext>((serviceProvider, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

builder.Services.AddLoomPersistence(persistence => persistence
    .UseOutbox<OrderingDbContext>(outbox => outbox.EventAssemblies = [typeof(Order).Assembly]));

// The chain is declared once and applies to every handler, so a slice cannot be registered without
// validation by forgetting a call.
builder.Services
    // Declaration order is nesting order. Logging outermost, so nothing goes unrecorded — including a
    // request refused by validation, which is the outcome most worth seeing. Validation next, so an
    // invalid request never reaches the handler. Failure translation innermost, so it only sees the
    // handler's own save.
    .AddLoomHandlers(chain => chain
        .WithLogging()
        .WithValidation()
        .WithDomainEventFailures())
    .AddHandler<Ordering.Api.Features.Orders.PlaceOrder.Handler,
        Ordering.Api.Features.Orders.PlaceOrder.Request,
        Ordering.Api.Features.Orders.PlaceOrder.Response>()
    .AddHandler<Ordering.Api.Features.Orders.GetOrder.Handler,
        Ordering.Api.Features.Orders.GetOrder.Request,
        Ordering.Api.Features.Orders.GetOrder.Response>()
    .AddHandler<Ordering.Api.Features.Orders.ListOrders.Handler,
        Ordering.Api.Features.Orders.ListOrders.Request,
        Page<Ordering.Api.Features.Orders.ListOrders.Response>>()
    .AddHandler<Ordering.Api.Features.Orders.CancelOrder.Handler,
        Ordering.Api.Features.Orders.CancelOrder.Request>()
    .AddHandler<Ordering.Api.Features.Orders.ShipOrder.Handler,
        Ordering.Api.Features.Orders.ShipOrder.Request>();

// includeInternalTypes matters: slice validators are internal, and without it none are registered.
// The validating decorator treats a missing validator as nothing to validate, so the omission is
// silent — which is why ValidatorRegistrationTests asserts every validator is resolvable.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped, includeInternalTypes: true);

builder.Services.AddScoped<IDomainEventHandler<OrderCancelled>, RecordCancellation>();
builder.Services.AddScoped<IDomainEventHandler<OrderShipped>, NotifyCustomerOfShipment>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCustomer, CurrentCustomer>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured from the validated options rather than by reading the section again. Re-reading it here
// would bypass every check above — and the `?? new AuthenticationOptions()` such a read needs would
// have quietly configured an empty signing key instead of failing.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationOptions>>((jwt, authentication) =>
    {
        AuthenticationOptions settings = authentication.Value;

        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidateLifetime = true,
        };
    });

// The claim must be present *and* parseable. Requiring only its presence would let a malformed value
// through, and the first thing to read it would then throw — turning a request that should be refused
// into a server error.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.OrdersRead, policy => policy.RequireAssertion(CurrentCustomer.IsIdentifiable))
    .AddPolicy(Policies.OrdersWrite, policy => policy.RequireAssertion(CurrentCustomer.IsIdentifiable));

builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);

await app.RunAsync();
