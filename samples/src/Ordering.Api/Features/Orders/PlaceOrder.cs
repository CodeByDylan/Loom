using FluentValidation;
using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Ordering.Api.Infrastructure;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Api.Features.Orders.PlaceOrder;

internal sealed record Request(DateOnly PlacedOn, IReadOnlyList<Request.Line> Lines)
{
    internal sealed record Line(string Sku, int Amount);
}

internal sealed record Response(Guid OrderId, int Total);

internal sealed class Validator : AbstractValidator<Request>
{
    public Validator()
    {
        // Request shape only. Whether an order may exist at all is a domain rule and lives on Order.
        RuleFor(request => request.Lines).NotEmpty();
        RuleForEach(request => request.Lines).ChildRules(line =>
        {
            line.RuleFor(entry => entry.Sku).NotEmpty().MaximumLength(64);
            line.RuleFor(entry => entry.Amount).GreaterThan(0);
        });
    }
}

internal sealed class Handler(OrderingDbContext database, ICurrentCustomer customer)
    : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        Id<Customer> customerId = customer.Id;

        Result<Order> placed = Order.Place(
            customerId,
            request.PlacedOn,
            [.. request.Lines.Select(line => (line.Sku, line.Amount))]);

        if (placed.IsFailure)
        {
            return placed.Error;
        }

        database.Orders.Add(placed.Value);
        await database.SaveChangesAsync(cancellationToken);

        return new Response(placed.Value.Id.Value, placed.Value.Total);
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapPost("/orders", async (
            Request request,
            IHandler<Request, Response> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Response> result = await handler.HandleAsync(request, cancellationToken);
            return result.ToHttpResult(response => Results.Created($"/orders/{response.OrderId}", response));
        })
        .RequireAuthorization(Policies.OrdersWrite)
        .WithName("PlaceOrder");
}
