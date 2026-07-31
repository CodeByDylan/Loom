using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// The container bootstrap a host test needs: an open in-memory SQLite connection, a context
/// registered against it with the domain event interceptor attached, and the schema created.
/// </summary>
/// <remarks>
/// Shared because the two hosts differ only in what they register, and a bootstrap copied per host is
/// the kind of thing that gets fixed in one copy. What varies — the persistence options, the handlers,
/// the context type — is the argument.
/// <para>
/// Distinct from <see cref="SqliteFixture" />, which builds contexts directly and exists precisely to
/// test what happens without a container.
/// </para>
/// </remarks>
internal abstract class SqliteHost : IAsyncDisposable
{
    private SqliteConnection? _connection;
    private ServiceProvider? _provider;

    /// <summary>Opens the database, registers the context and creates the schema.</summary>
    /// <typeparam name="TContext">The context under test.</typeparam>
    /// <param name="configure">Registers whatever else the host needs.</param>
    /// <remarks>
    /// Cleans up after itself if any step throws. The factory that calls this never hands the host back
    /// on failure, so nothing else is in a position to dispose it and the connection would stay open for
    /// the rest of the run. The field is assigned before the connection is opened for the same reason.
    /// </remarks>
    private protected async Task InitialiseAsync<TContext>(Action<IServiceCollection> configure)
        where TContext : DbContext
    {
        // Held locally as well so the registration below closes over the connection rather than over
        // this host.
        SqliteConnection connection = new("Filename=:memory:");
        _connection = connection;

        try
        {
            await connection.OpenAsync();

            ServiceCollection services = new();
            services.AddSingleton<Recorder>();
            services.AddDbContext<TContext>((serviceProvider, options) => options
                .UseSqlite(connection)
                .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

            configure(services);

            _provider = services.BuildServiceProvider();

            using IServiceScope scope = _provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    internal Recorder Recorder => Resolve<Recorder>();

    internal IServiceScope CreateScope() => Provider.CreateScope();

    private ServiceProvider Provider =>
        _provider ?? throw new InvalidOperationException("The host was used before it was initialised.");

    private protected T Resolve<T>()
        where T : notnull => Provider.GetRequiredService<T>();

    /// <summary>Disposes the container, then the connection that holds the database alive.</summary>
    /// <remarks>In that order: a provider disposed after its connection would dispose contexts over a
    /// closed one.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
