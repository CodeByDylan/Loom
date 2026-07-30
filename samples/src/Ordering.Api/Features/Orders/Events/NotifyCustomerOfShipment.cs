using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
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

        database.ShipmentNotifications.Add(new ShipmentNotification
        {
            OrderId = domainEvent.OrderId,
            CustomerId = domainEvent.CustomerId,
        });

        try
        {
            // Saved here, because the transaction that raised this event committed long ago.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Either the constraint rejected a duplicate — in which case the work is already done and
            // this delivery has nothing to be sorry about — or something else went wrong and should be
            // reported. Asking the database which it was avoids guessing at provider error codes.
            database.ChangeTracker.Clear();

            bool nowNotified = await database.ShipmentNotifications
                .AnyAsync(notification => notification.OrderId == domainEvent.OrderId, cancellationToken);

            if (!nowNotified)
            {
                throw;
            }
        }

        return Result.Success;
    }
}
