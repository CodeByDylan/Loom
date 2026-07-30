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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(entity => entity.Id);
            order.Property(entity => entity.Status).HasConversion<string>();
            order.HasMany(entity => entity.Lines).WithOne().HasForeignKey(line => line.OrderId);
            order.Ignore(entity => entity.Total);

            // A constraint, not a navigation: an order still reaches its customer by identity only, so
            // the aggregates stay separate. Without this the database would happily hold orders for
            // customers that do not exist. Restricting the delete means a customer with orders cannot
            // be removed silently.
            order.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(entity => entity.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<ShipmentNotification>(notification =>
        {
            notification.HasKey(entity => entity.Id);

            // Deferred delivery is at least once, so this handler can run twice. Its own check is a
            // fast path, not a guarantee: two deliveries running at once would both pass it. The
            // constraint is what actually makes one notification per order true.
            notification.HasIndex(entity => entity.OrderId).IsUnique();
        });

        modelBuilder.AddLoomOutbox();
    }
}
