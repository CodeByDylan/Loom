using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Infrastructure;
using MyApp.Domain.Widgets;

namespace MyApp.Api.Features.Widgets.GetWidget;

internal sealed record Request(Id<Widget> WidgetId);

internal sealed record Response(Guid WidgetId, string Name, int Size);

internal sealed class Handler(AppDbContext database) : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        // Projected in the query rather than materialised and mapped.
        Response? found = await database.Widgets
            .AsNoTracking()
            .Where(widget => widget.Id == request.WidgetId)
            .Select(widget => new Response(widget.Id.Value, widget.Name, widget.Size))
            .SingleOrDefaultAsync(cancellationToken);

        return found is null ? WidgetErrors.NotFound : found;
    }
}

internal sealed class Endpoint : IEndpoint
{
    // Id<Widget> is IParsable, so it binds from the route without a TypeConverter.
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapGet("/widgets/{widgetId}", async (
            Id<Widget> widgetId,
            IHandler<Request, Response> handler,
            CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new Request(widgetId), cancellationToken)).ToHttpResult())
        .AllowAnonymous();
}
