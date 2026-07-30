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

        // Saved here, because the transaction that raised this event committed long ago.
        await database.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
