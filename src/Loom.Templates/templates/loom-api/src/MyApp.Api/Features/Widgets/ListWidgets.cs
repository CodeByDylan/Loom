using FluentValidation;
using Loom.Handlers;
using Loom.Paging;
using Loom.Persistence;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Infrastructure;
using MyApp.Domain.Widgets;

namespace MyApp.Api.Features.Widgets.ListWidgets;

internal sealed record Request(int LargerThan, int Number, int PageSize);

internal sealed record Response(Guid WidgetId, string Name, int Size);

internal sealed class Validator : AbstractValidator<Request>
{
    public Validator()
    {
        RuleFor(request => request.LargerThan).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Number).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageRequest.DefaultMaximumSize);
    }
}

internal sealed class Handler(AppDbContext database) : IHandler<Request, Page<Response>>
{
    public async Task<Result<Page<Response>>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The specification carries the rule and its ordering; paging is applied after it, because how
        // many rows the caller wants is not part of what "larger than" means. Projected in the query
        // rather than materialised and mapped.
        return await database.Widgets
            .AsNoTracking()
            .ApplySpecification(new WidgetsLargerThan(request.LargerThan))
            .Select(widget => new Response(widget.Id.Value, widget.Name, widget.Size))
            .ToPageAsync(new PageRequest(request.Number, request.PageSize), cancellationToken);
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapGet("/widgets", async (
            IHandler<Request, Page<Response>> handler,
            CancellationToken cancellationToken,
            int largerThan = 0,
            int number = 1,
            int pageSize = 20) =>
                (await handler.HandleAsync(new Request(largerThan, number, pageSize), cancellationToken))
                    .ToHttpResult())
        .AllowAnonymous();
}
