using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Api.Infrastructure;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace MyApp.Api.Tests;

/// <summary>
/// One Postgres container and one database for the whole assembly, reset between tests.
/// </summary>
/// <remarks>
/// Respawn rather than a transaction per test: handlers own their transactions, so rollback-based
/// isolation would lie about what was actually committed.
/// </remarks>
public sealed class TestApp : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Respawner _respawner;
    private readonly NpgsqlConnection _resetConnection;

    private TestApp(
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

    public static async Task<TestApp> StartAsync()
    {
        PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();

        WebApplicationFactory<Program> factory = new AppFactory(container.GetConnectionString());

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.EnsureCreatedAsync();
        }

        NpgsqlConnection resetConnection = new(container.GetConnectionString());
        await resetConnection.OpenAsync();

        Respawner respawner = await Respawner.CreateAsync(resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });

        return new TestApp(container, factory, respawner, resetConnection);
    }

    public Task ResetAsync() => _respawner.ResetAsync(_resetConnection);

    public IServiceProvider Services => _factory.Services;

    public HttpClient Client() => _factory.CreateClient();

    public async Task InDatabaseAsync(Func<AppDbContext, Task> work)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _resetConnection.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private sealed class AppFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ConnectionStrings:database", connectionString),
            ]));

            // Pinned so the route table is deterministic: the health endpoints are development-only.
            builder.UseEnvironment("Development");

            return base.CreateHost(builder);
        }
    }
}

/// <summary>Starts one container for the whole assembly.</summary>
public static class AppFixture
{
    private static TestApp? _app;

    public static TestApp App => _app ?? throw new InvalidOperationException("The fixture has not started.");

    [Before(Assembly)]
    public static async Task StartAsync() => _app = await TestApp.StartAsync();

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
