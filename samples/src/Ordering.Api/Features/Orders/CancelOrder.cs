using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.CancelOrder;

internal sealed record Request(Id<Order> OrderId);

internal sealed class Handler(OrderingDbContext database, ICurrentCustomer customer) : IHandler<Request>
{
    public async Task<Result> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        Order? order = await database.Orders
            .SingleOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        if (order.CustomerId != customer.Id)
        {
            return OrderErrors.NotYours;
        }

        // The invariant lives on the aggregate and reports a Conflict rather than throwing.
        Result cancelled = order.Cancel();

        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        // The event raised by Cancel is dispatched by the interceptor during this save, so the
        // cancellation record it writes commits in the same transaction.
        await database.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapPost("/orders/{orderId}/cancel", async (
            Id<Order> orderId,
            IHandler<Request> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.HandleAsync(new Request(orderId), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Policies.OrdersWrite)
        .WithName("CancelOrder");
}
