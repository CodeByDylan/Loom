using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// The wiring a consumer would use: the context resolved from the container with the interceptor
/// attached, so an event handler receives the same scoped context the save is running on.
/// </summary>
internal sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private TestHost(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    internal static async Task<TestHost> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        SqliteConnection connection = new("Filename=:memory:");
        await connection.OpenAsync();

        ServiceCollection services = new();
        services.AddSingleton<Recorder>();
        services.AddLoomPersistence();
        services.AddDbContext<TestDbContext>((serviceProvider, options) => options
            .UseSqlite(connection)
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

        configure?.Invoke(services);

        ServiceProvider provider = services.BuildServiceProvider();

        using (IServiceScope scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        return new TestHost(connection, provider);
    }

    internal Recorder Recorder => _provider.GetRequiredService<Recorder>();

    internal IServiceScope CreateScope() => _provider.CreateScope();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>Records what handlers did, through the container rather than static state.</summary>
internal sealed class Recorder
{
    private readonly List<string> _handled = [];

    internal IReadOnlyList<string> Handled
    {
        get
        {
            lock (_handled)
            {
                return [.. _handled];
            }
        }
    }

    internal void Record(string what)
    {
        lock (_handled)
        {
            _handled.Add(what);
        }
    }
}
