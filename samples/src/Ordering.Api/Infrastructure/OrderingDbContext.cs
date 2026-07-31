using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// The one context for the application, holding both aggregates and the outbox.
/// </summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Written by an ordinary domain event handler, inside the same transaction.</summary>
    public DbSet<CancellationRecord> CancellationRecords => Set<CancellationRecord>();

    /// <summary>Written by a deferred domain event handler, after the transaction commits.</summary>
    public DbSet<ShipmentNotification> ShipmentNotifications => Set<ShipmentNotification>();

    // Conventions, not model creation. Property discovery skips types it does not recognise, so an
    // identity that is not a primary key would never enter the model at all.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseLoomIdentities(typeof(Order).Assembly);

    // Discovered rather than listed, so adding an aggregate means adding its configuration beside its
    // slices and nothing here. A mapping written inline would grow this method with every entity and
    // put it a long way from the code that uses it.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);

        modelBuilder.AddLoomOutbox();
    }
}
