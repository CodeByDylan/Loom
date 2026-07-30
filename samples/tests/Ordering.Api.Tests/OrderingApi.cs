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

        WebApplicationFactory<Program> factory = new OrderingFactory(container.GetConnectionString(), SigningKey, Issuer);

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
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private sealed class OrderingFactory(string connectionString, string signingKey, string issuer)
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
            builder.UseEnvironment("Development");

            return base.CreateHost(builder);
        }
    }
}
