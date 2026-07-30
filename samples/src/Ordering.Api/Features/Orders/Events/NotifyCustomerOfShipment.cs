using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.Events;

/// <summary>
/// Reacts to a deferred domain event, after the shipment has committed.
/// </summary>
/// <remarks>
/// Stands in for sending a message to the customer — the kind of thing that must not happen inside a
/// transaction that might roll back. Delivery is at least once, so this is written to be idempotent:
/// a repeat produces no second notification.
/// </remarks>
internal sealed class NotifyCustomerOfShipment(OrderingDbContext database)
    : IDomainEventHandler<OrderShipped>
{
    public async Task<Result> HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken)
    {
        // A fast path, not a guarantee. Two deliveries running at once would both pass it, so the unique
        // index on the table is what actually makes one notification per order true.
        bool alreadyNotified = await database.ShipmentNotifications
            .AnyAsync(notification => notification.OrderId == domainEvent.OrderId, cancellationToken);

        if (alreadyNotified)
        {
            return Result.Success;
        }

        ShipmentNotification notification = new()
        {
            OrderId = domainEvent.OrderId,
            CustomerId = domainEvent.CustomerId,
        };

        database.ShipmentNotifications.Add(notification);

        try
        {
            // Saved here, because the transaction that raised this event committed long ago.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAnotherNotificationForSameOrder(exception))
        {
            // Another delivery got there first, so the work is done. Only this one violation is
            // success; any other failure propagates and the delivery is retried.
            //
            // The failed insert is detached — not the whole change tracker. The delivery pass shares
            // this scoped context, and clearing it would also detach the outbox bookkeeping, so the
            // message would never be marked delivered and would be redelivered on every interval.
            database.Entry(notification).State = EntityState.Detached;
        }

        return Result.Success;
    }

    // Matched by SQLSTATE and constraint name rather than by exception type alone, so an unrelated
    // persistence failure — or even a different unique violation, such as the primary key's — is
    // never mistaken for idempotent success. The name is pinned by a test against the real schema.
    private static bool IsAnotherNotificationForSameOrder(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_ShipmentNotifications_OrderId",
        };
}
