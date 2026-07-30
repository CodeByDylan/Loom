using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Widgets;

namespace MyApp.Api.Infrastructure;

/// <summary>
/// The one context for the application.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    // Conventions, not model creation. Property discovery skips types it does not recognise, so an
    // identity that is not a primary key would never enter the model at all.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseLoomIdentities(typeof(Widget).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Widget>(widget =>
        {
            widget.HasKey(entity => entity.Id);
            widget.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        });
    }
}
