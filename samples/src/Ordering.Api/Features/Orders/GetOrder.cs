using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.GetOrder;

internal sealed record Request(Id<Order> OrderId);

internal sealed record Response(Guid OrderId, string Status, int Total, IReadOnlyList<Response.Line> Lines)
{
    internal sealed record Line(string Sku, int Amount);
}

internal sealed class Handler(OrderingDbContext database, ICurrentCustomer customer)
    : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        // Projected in the query rather than materialised and mapped.
        var found = await database.Orders
            .AsNoTracking()
            .Where(order => order.Id == request.OrderId)
            .Select(order => new
            {
                order.Id,
                order.Status,
                order.CustomerId,
                Lines = order.Lines.Select(line => new Response.Line(line.Sku, line.Amount)).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return OrderErrors.NotFound;
        }

        // Needs the order to decide, so it cannot be a policy or an attribute.
        if (found.CustomerId != customer.Id)
        {
            return OrderErrors.NotYours;
        }

        return new Response(
            found.Id.Value,
            found.Status.ToString(),
            found.Lines.Sum(line => line.Amount),
            found.Lines);
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapGet("/orders/{orderId}", async (
            Id<Order> orderId,
            IHandler<Request, Response> handler,
            CancellationToken cancellationToken) =>
        {
            // Bound straight from the route by IParsable, with no type converter.
            Result<Response> result = await handler.HandleAsync(new Request(orderId), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Policies.OrdersRead)
        .WithName("GetOrder");
}
