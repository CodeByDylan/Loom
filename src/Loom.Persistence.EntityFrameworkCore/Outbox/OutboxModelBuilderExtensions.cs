using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Adds the outbox table to a model.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps <see cref="OutboxMessage" /> so that deferred domain events can be recorded.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// Call this from <c>OnModelCreating</c> when the outbox is in use, then generate a migration for
    /// it as normal. The table is not created implicitly: a package that silently adds tables to
    /// someone else's database is a package that produces migrations nobody expected.
    /// </remarks>
    public static ModelBuilder AddLoomOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable("LoomOutboxMessages");
            message.HasKey(entry => entry.Id);
            message.Property(entry => entry.EventType).IsRequired();
            message.Property(entry => entry.Payload).IsRequired();

            // Stored as ticks so that delivery can be ordered by time on any provider. Some refuse to
            // order by an offset-bearing instant at all.
            message.Property(entry => entry.OccurredAt).HasConversion<UtcTicksConverter>();
            message.Property(entry => entry.DeliveredAt).HasConversion<UtcTicksConverter>();

            // The delivery query filters on exactly these, so it should not scan the whole table once
            // delivered messages accumulate.
            message.HasIndex(entry => new { entry.DeliveredAt, entry.Abandoned, entry.OccurredAt });
        });

        return modelBuilder;
    }
}
