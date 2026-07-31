using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Widgets;

namespace MyApp.Worker.Infrastructure;

/// <summary>
/// The one context for the application.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Gets the widgets. Only aggregate roots get a set of their own.</summary>
    public DbSet<Widget> Widgets => Set<Widget>();

    // Conventions, not model creation. Property discovery skips types it does not recognise, so an
    // identity that is not a primary key would never enter the model at all.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseLoomIdentities(typeof(Widget).Assembly);

    // Discovered rather than listed, so adding an aggregate means adding its configuration beside its
    // slices and nothing here.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
