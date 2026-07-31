using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Worker.Infrastructure;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace MyApp.Worker.Tests;

/// <summary>
/// One Postgres container and one database for the whole assembly, reset between tests.
/// </summary>
/// <remarks>
/// A worker has no transport, so there is no host to go through and the handler is the entry point.
/// What this provides is the real service graph a scope would resolve at run time — including the
/// decorator chain, which is most of what a test would otherwise skip by calling a handler directly.
/// </remarks>
public sealed class WorkerHost : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private readonly ServiceProvider _provider;
    private readonly Respawner _respawner;
    private readonly NpgsqlConnection _resetConnection;

    private WorkerHost(
        PostgreSqlContainer container,
        ServiceProvider provider,
        Respawner respawner,
        NpgsqlConnection resetConnection)
    {
        _container = container;
        _provider = provider;
        _respawner = respawner;
        _resetConnection = resetConnection;
    }

    public static async Task<WorkerHost> StartAsync()
    {
        PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();

        // The application's own registrations, not a second set written for the tests. A hand-built
        // graph can differ from the real one in exactly the ways that matter — a missing decorator, an
        // unregistered validator — and still pass.
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWorkerServices(container.GetConnectionString());

        ServiceProvider provider = services.BuildServiceProvider();

        using (IServiceScope scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        }

        NpgsqlConnection resetConnection = new(container.GetConnectionString());
        await resetConnection.OpenAsync();

        Respawner respawner = await Respawner.CreateAsync(resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });

        return new WorkerHost(container, provider, respawner, resetConnection);
    }

    public Task ResetAsync() => _respawner.ResetAsync(_resetConnection);

    public IServiceScope CreateScope() => _provider.CreateScope();

    public async Task InDatabaseAsync(Func<AppDbContext, Task> work)
    {
        using IServiceScope scope = CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _resetConnection.DisposeAsync();
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>Starts one container for the whole assembly.</summary>
public static class WorkerFixture
{
    private static WorkerHost? _host;

    public static WorkerHost Host => _host ?? throw new InvalidOperationException("The fixture has not started.");

    [Before(Assembly)]
    public static async Task StartAsync() => _host = await WorkerHost.StartAsync();

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }
}
