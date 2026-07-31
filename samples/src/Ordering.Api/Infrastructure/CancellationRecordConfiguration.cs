using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ordering.Api.Infrastructure;

/// <summary>How a cancellation record is stored.</summary>
internal sealed class CancellationRecordConfiguration : IEntityTypeConfiguration<CancellationRecord>
{
    void IEntityTypeConfiguration<CancellationRecord>.Configure(EntityTypeBuilder<CancellationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(record => record.Id);
    }
}

/// <summary>How a shipment notification is stored.</summary>
internal sealed class ShipmentNotificationConfiguration : IEntityTypeConfiguration<ShipmentNotification>
{
    void IEntityTypeConfiguration<ShipmentNotification>.Configure(EntityTypeBuilder<ShipmentNotification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        // Deferred delivery is at least once, so this handler can run twice. Its own check is a fast
        // path, not a guarantee: two deliveries running at once would both pass it. The constraint is
        // what actually makes one notification per order true.
        builder.HasIndex(entity => entity.OrderId).IsUnique();
    }
}
