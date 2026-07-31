using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Customers;

namespace Ordering.Api.Features.Customers;

/// <summary>How a customer is stored.</summary>
internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    void IEntityTypeConfiguration<Customer>.Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
    }
}
