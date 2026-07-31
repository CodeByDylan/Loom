using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyApp.Worker.Infrastructure;

/// <summary>
/// Builds a context for the migration tooling, without starting the application.
/// </summary>
/// <remarks>
/// Migrations are generated against a connection string that is never connected to, so `dotnet ef`
/// needs neither a running database nor the application's configuration.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    AppDbContext IDesignTimeDbContextFactory<AppDbContext>.CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AppDbContext> builder = new();
        builder.UseNpgsql("Host=design-time;Database=designtime;Username=none;Password=none");

        return new AppDbContext(builder.Options);
    }
}
