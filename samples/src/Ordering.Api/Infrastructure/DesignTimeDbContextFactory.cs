using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// Builds a context for the migration tooling, without starting the application.
/// </summary>
/// <remarks>
/// Migrations are generated against a connection string that is never connected to, so the tooling
/// does not need a running database or the application's configuration.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<OrderingDbContext> builder = new();
        builder.UseNpgsql("Host=design-time;Database=ordering;Username=none;Password=none");
        return new OrderingDbContext(builder.Options);
    }
}
