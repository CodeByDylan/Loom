using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders;

/// <summary>
/// How an order and its lines are stored.
/// </summary>
/// <remarks>
/// Host-side and beside the aggregate's slices, so the mapping lives where the aggregate is worked on
/// rather than accumulating in one method that grows with every entity. The domain stays free of
/// persistence: it carries no attributes and no reference to Entity Framework.
/// </remarks>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    void IEntityTypeConfiguration<Order>.Configure(EntityTypeBuilder<Order> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>();
        builder.HasMany(entity => entity.Lines).WithOne().HasForeignKey(line => line.OrderId);
        builder.Ignore(entity => entity.Total);

        // A constraint, not a navigation: an order still reaches its customer by identity only, so
        // the aggregates stay separate. Without this the database would happily hold orders for
        // customers that do not exist. Restricting the delete means a customer with orders cannot
        // be removed silently.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>How a line of an order is stored.</summary>
/// <remarks>Reached through its root; only aggregate roots get a set of their own.</remarks>
internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    void IEntityTypeConfiguration<OrderLine>.Configure(EntityTypeBuilder<OrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Sku).HasMaxLength(64).IsRequired();
    }
}
