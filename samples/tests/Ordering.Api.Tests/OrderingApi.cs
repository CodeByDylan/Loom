using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Loom.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Customers;
using Respawn;
using Testcontainers.PostgreSql;

namespace Ordering.Api.Tests;

/// <summary>
/// One Postgres container and one migrated database for the whole assembly, reset between tests.
/// </summary>
/// <remarks>
/// Respawn rather than a transaction per test: handlers own their transactions, so rollback-based
/// isolation would lie about what was committed — which is exactly what the domain event and outbox
/// tests need to be truthful about.
/// <para>
/// The schema comes from real migrations rather than being created implicitly, so the tests exercise
/// the same path a deployment does.
/// </para>
/// </remarks>
public sealed class OrderingApi : IAsyncDisposable
{
    private const string SigningKey = "integration-test-signing-key-at-least-32-chars";
    private const string Issuer = "ordering-tests";

    private readonly PostgreSqlContainer _container;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Respawner _respawner;
    private readonly NpgsqlConnection _resetConnection;

    // Every factory derived from _factory, so each can be shut down. Tests run in parallel, so the list
    // is guarded.
    private readonly List<WebApplicationFactory<Program>> _derived = [];

    private OrderingApi(
        PostgreSqlContainer container,
        WebApplicationFactory<Program> factory,
        Respawner respawner,
        NpgsqlConnection resetConnection)
    {
        _container = container;
        _factory = factory;
        _respawner = respawner;
        _resetConnection = resetConnection;
    }

    public static async Task<OrderingApi> StartAsync()
    {
        PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

        await container.StartAsync();

        WebApplicationFactory<Program> factory = new OrderingFactory(
            container.GetConnectionString(),
            SigningKey,
            Issuer,
            "Development");

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            OrderingDbContext database = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            await database.Database.MigrateAsync();
        }

        NpgsqlConnection resetConnection = new(container.GetConnectionString());
        await resetConnection.OpenAsync();

        Respawner respawner = await Respawner.CreateAsync(resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
        });

        return new OrderingApi(container, factory, respawner, resetConnection);
    }

    public Task ResetAsync() => _respawner.ResetAsync(_resetConnection);

    public IServiceProvider Services => _factory.Services;

    /// <summary>A client authenticated as the given customer.</summary>
    public HttpClient ClientFor(Id<Customer> customer)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(customer));
        return client;
    }

    /// <summary>A client with no token, for proving endpoints are closed by default.</summary>
    public HttpClient AnonymousClient() => _factory.CreateClient();

    /// <summary>
    /// A client authenticated as the given customer, against a host with extra registrations.
    /// </summary>
    /// <remarks>
    /// Reuses the container and its connection string; only the service graph differs. For a test that
    /// needs a collaborator to misbehave.
    /// </remarks>
    public HttpClient ClientFor(Id<Customer> customer, Action<IServiceCollection> configure)
    {
        WebApplicationFactory<Program> configured = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(configure));

        // Kept, not dropped. This builds a second host with its own server and service provider, and
        // abandoning it leaves both running until the collector happens to reach it — which, across a
        // suite, means several live hosts competing for the same database.
        lock (_derived)
        {
            _derived.Add(configured);
        }

        HttpClient client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(customer));
        return client;
    }

    /// <summary>
    /// A host configured for the given environment and signing key, not yet built.
    /// </summary>
    /// <remarks>
    /// For the startup checks, where what happens <em>while</em> the host is built is the thing under
    /// test. Reuses the container's database so that startup fails for the reason the test intends
    /// rather than for want of somewhere to connect. The caller owns the result.
    /// </remarks>
    public WebApplicationFactory<Program> FactoryFor(string environment, string signingKey) =>
        new OrderingFactory(_container.GetConnectionString(), signingKey, Issuer, environment);

    public async Task InDatabaseAsync(Func<OrderingDbContext, Task> work)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<OrderingDbContext>());
    }

    public async Task<Id<Customer>> AddCustomerAsync(string name = "Acme")
    {
        Customer customer = new(name);
        await InDatabaseAsync(async database =>
        {
            database.Customers.Add(customer);
            await database.SaveChangesAsync();
        });

        return customer.Id;
    }

    private static string TokenFor(Id<Customer> customer)
    {
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Issuer,
            claims: [new Claim(CurrentCustomer.ClaimType, customer.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async ValueTask DisposeAsync()
    {
        await _resetConnection.DisposeAsync();

        WebApplicationFactory<Program>[] derived;
        lock (_derived)
        {
            derived = [.. _derived];
            _derived.Clear();
        }

        // Derived factories first: each was built from the shared one, so disposing that first would
        // pull the ground out from under hosts still being shut down.
        foreach (WebApplicationFactory<Program> configured in derived)
        {
            await configured.DisposeAsync();
        }

        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private sealed class OrderingFactory(
        string connectionString,
        string signingKey,
        string issuer,
        string environment)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ConnectionStrings:ordering", connectionString),
                new KeyValuePair<string, string?>("Authentication:SigningKey", signingKey),
                new KeyValuePair<string, string?>("Authentication:Issuer", issuer),
                new KeyValuePair<string, string?>("Authentication:Audience", issuer),
            ]));

            // Pinned so the route table is deterministic: the health endpoints are development-only.
            // Overridable only so the startup checks can be exercised outside Development.
            builder.UseEnvironment(environment);

            // The outbox delivery worker starts a pass immediately, so left running it could deliver a
            // message between a test recording one and asserting it is still owed. Tests drive delivery
            // themselves through OutboxProcessor, which is the behaviour they mean to check. Only that
            // worker is removed — the test server itself is also a hosted service, and removing every
            // one tears the host down with it.
            builder.ConfigureServices(services =>
            {
                ServiceDescriptor[] outboxWorkers =
                [
                    .. services.Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService)
                        && descriptor.ImplementationType is { IsGenericType: true } implementation
                        && implementation.Name.StartsWith("OutboxDeliveryService", StringComparison.Ordinal)),
                ];

                foreach (ServiceDescriptor worker in outboxWorkers)
                {
                    services.Remove(worker);
                }
            });

            return base.CreateHost(builder);
        }
    }
}
