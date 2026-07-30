using FluentValidation;
using Loom.Handlers;
using Loom.Paging;
using Loom.Persistence;
using Loom.Results;
using Loom.Specifications;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.ListOrders;

internal sealed record Request(int Page, int Size, bool OpenOnly);

internal sealed record Response(Guid OrderId, string Status, int Total);

internal sealed class Validator : AbstractValidator<Request>
{
    public Validator()
    {
        // PageRequest guards itself by throwing, which is right for a programming error but wrong for
        // a query string. Validating here turns a bad page size into an Invalid failure instead.
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.Size).InclusiveBetween(1, PageRequest.DefaultMaximumSize);
    }
}

internal sealed class Handler(OrderingDbContext database, ICurrentCustomer customer)
    : IHandler<Request, Page<Response>>
{
    public async Task<Result<Page<Response>>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        PageRequest page = new(request.Page, request.Size);

        // A named rule applied to the query this slice owns. Nothing queries on its behalf.
        ISpecification<Order> specification = request.OpenOnly
            ? new OpenOrdersForCustomer(customer.Id)
            : new OrdersForCustomer(customer.Id);

        Page<Order> orders = await database.Orders
            .AsNoTracking()
            .ApplySpecification(specification)
            .ToPageAsync(page, cancellationToken);

        return new Page<Response>(
            [.. orders.Items.Select(order => new Response(order.Id.Value, order.Status.ToString(), order.Total))],
            orders.TotalCount,
            orders.Number,
            orders.Size);
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapGet("/orders", async (
            IHandler<Request, Page<Response>> handler,
            CancellationToken cancellationToken,
            int page = 1,
            int size = 20,
            bool openOnly = false) =>
        {
            Result<Page<Response>> result =
                await handler.HandleAsync(new Request(page, size, openOnly), cancellationToken);

            return result.ToHttpResult();
        })
        .RequireAuthorization(Policies.OrdersRead)
        .WithName("ListOrders");
}
