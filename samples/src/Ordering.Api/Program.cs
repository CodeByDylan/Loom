using System.Text;
using FluentValidation;
using Loom.Entities;
using Loom.Handlers;
using Loom.Paging;
using Loom.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
    .ValidateOnStart();

builder.Services.AddDbContext<OrderingDbContext>((serviceProvider, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("ordering"))
    .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

builder.Services.AddLoomPersistence(persistence => persistence
    .UseOutbox<OrderingDbContext>(outbox => outbox.EventAssemblies = [typeof(Order).Assembly]));

// The chain is declared once and applies to every handler, so a slice cannot be registered without
// validation by forgetting a call.
builder.Services
    .AddLoomHandlers(chain => chain.WithValidation())
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
    .AddJwtBearer(options =>
    {
        AuthenticationOptions authentication = builder.Configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authentication.Issuer,
            ValidateAudience = true,
            ValidAudience = authentication.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authentication.SigningKey)),
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.OrdersRead, policy => policy.RequireClaim(CurrentCustomer.ClaimType))
    .AddPolicy(Policies.OrdersWrite, policy => policy.RequireClaim(CurrentCustomer.ClaimType));

builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);

await app.RunAsync();
