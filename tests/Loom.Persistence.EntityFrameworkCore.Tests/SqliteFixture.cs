using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// An isolated relational database per test, held in memory.
/// </summary>
/// <remarks>
/// SQLite rather than a container: everything this package does — converters, eager loading,
/// interceptors, the outbox — is provider-neutral Entity Framework behaviour, so a real relational
/// provider is enough fidelity and keeps the suite fast and free of a Docker requirement. A consumer
/// application testing its own queries against its real provider is a different concern.
/// <para>
/// The connection is kept open deliberately: an in-memory SQLite database exists only as long as a
/// connection to it does.
/// </para>
/// </remarks>
internal sealed class SqliteFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteFixture(SqliteConnection connection) => _connection = connection;

    internal static async Task<SqliteFixture> CreateAsync(params IInterceptor[] interceptors)
    {
        SqliteConnection connection = new("Filename=:memory:");
        await connection.OpenAsync();

        SqliteFixture fixture = new(connection) { Interceptors = interceptors };

        await using TestDbContext context = fixture.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return fixture;
    }

    private IInterceptor[] Interceptors { get; init; } = [];

    internal TestDbContext CreateContext()
    {
        DbContextOptionsBuilder<TestDbContext> builder = new();
        builder.UseSqlite(_connection);

        if (Interceptors.Length > 0)
        {
            builder.AddInterceptors(Interceptors);
        }

        return new TestDbContext(builder.Options);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
