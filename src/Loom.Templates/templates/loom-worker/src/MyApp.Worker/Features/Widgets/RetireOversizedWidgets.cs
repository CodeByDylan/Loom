using FluentValidation;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Widgets;
using MyApp.Worker.Infrastructure;

namespace MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

// One operation, one file, one namespace — the same slice layout an API uses. A worker has no
// transport, so the handler is the entry point and the loop is the only thing above it.

internal sealed record Request(int LargerThan);

internal sealed record Response(int Retired);

internal sealed class Validator : AbstractValidator<Request>
{
    public Validator() => RuleFor(request => request.LargerThan).GreaterThan(0);
}

internal sealed class Handler(AppDbContext database) : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<Widget> oversized = await database.Widgets
            .Where(widget => !widget.IsRetired && widget.Size > request.LargerThan)
            .ToListAsync(cancellationToken);

        int retired = 0;

        foreach (Widget widget in oversized)
        {
            // The domain decides whether this is allowed. A widget retired by something else between
            // the query and here reports Conflict, which is information rather than a failure of the
            // batch — so the loop keeps going.
            if (widget.Retire().IsSuccess)
            {
                retired++;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return new Response(retired);
    }
}
