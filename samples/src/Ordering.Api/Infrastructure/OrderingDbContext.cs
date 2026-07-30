using Loom.Entities;
using Loom.Persistence;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Infrastructure;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(entity => entity.Id);
            order.Property(entity => entity.Status).HasConversion<string>();
            order.HasMany(entity => entity.Lines).WithOne().HasForeignKey(line => line.OrderId);
            order.HasIndex(entity => entity.CustomerId);
            order.Ignore(entity => entity.Total);
        });

        modelBuilder.Entity<OrderLine>(line =>
        {
            line.HasKey(entity => entity.Id);
            line.Property(entity => entity.Sku).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.HasKey(entity => entity.Id);
            customer.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<CancellationRecord>().HasKey(record => record.Id);
        modelBuilder.Entity<ShipmentNotification>().HasKey(notification => notification.Id);

        modelBuilder.AddLoomOutbox();
    }
}

public sealed class CancellationRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Id<Order> OrderId { get; init; }

    public int Total { get; init; }
}

public sealed class ShipmentNotification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Id<Order> OrderId { get; init; }

    public Id<Customer> CustomerId { get; init; }
}
