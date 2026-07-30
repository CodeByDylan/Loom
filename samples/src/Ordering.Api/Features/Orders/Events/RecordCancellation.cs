using Loom.Entities;
using Loom.Results;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.Events;

/// <summary>
/// Reacts to an ordinary domain event, so it may change data and must not reach outside the process.
/// </summary>
/// <remarks>
/// It does not call <c>SaveChanges</c>: the save that dispatched this event has not run yet, and will
/// persist this row along with the cancellation itself.
/// </remarks>
internal sealed class RecordCancellation(OrderingDbContext database) : IDomainEventHandler<OrderCancelled>
{
    public Task<Result> HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken)
    {
        database.CancellationRecords.Add(new CancellationRecord
        {
            OrderId = domainEvent.OrderId,
            Total = domainEvent.Total,
        });

        return Task.FromResult(Result.Success);
    }
}
