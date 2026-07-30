using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.ShipOrder;

internal sealed record Request(Id<Order> OrderId);

internal sealed class Handler(OrderingDbContext database) : IHandler<Request>
{
    public async Task<Result> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        Order? order = await database.Orders
            .SingleOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        Result shipped = order.Ship();

        if (shipped.IsFailure)
        {
            return shipped;
        }

        // Ship raises a deferred event. The interceptor records it in the outbox inside this
        // transaction; delivery happens afterwards, so notifying the customer cannot happen for a
        // shipment that was never committed.
        await database.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}

internal sealed class Endpoint : IEndpoint
{
    // Shipping is a warehouse action rather than a customer one, so it is not scoped to the caller's
    // own orders.
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapPost("/orders/{orderId}/ship", async (
            Id<Order> orderId,
            IHandler<Request> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.HandleAsync(new Request(orderId), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Policies.OrdersWrite)
        .WithName("ShipOrder");
}
