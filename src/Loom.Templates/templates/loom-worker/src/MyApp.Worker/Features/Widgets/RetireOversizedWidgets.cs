using FluentValidation;
using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Widgets;
using MyApp.Worker.Infrastructure;
using Npgsql;

namespace MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

// One operation, one file, one namespace — the same slice layout an API uses. A worker has no
// transport, so the handler is the entry point and the loop is the only thing above it.

internal sealed record Request(int LargerThan, int BatchSize);

internal sealed record Response(int Retired);

internal sealed class Validator : AbstractValidator<Request>
{
    public Validator()
    {
        RuleFor(request => request.LargerThan).GreaterThan(0);
        RuleFor(request => request.BatchSize).InclusiveBetween(1, 10_000);
    }
}

internal sealed class Handler(AppDbContext database) : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Bounded and ordered. A scheduled operation runs against whatever has accumulated since the
        // last tick, so an unbounded query is one backlog away from loading the table into memory;
        // ordering makes which rows a pass takes deterministic rather than whatever the plan returns.
        List<Widget> oversized = await database.Widgets
            .Where(widget => !widget.IsRetired && widget.Size > request.LargerThan)
            .OrderBy(widget => widget.Size)
            .ThenBy(widget => widget.Id)
            .Take(request.BatchSize)
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

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is NpgsqlException { IsTransient: true })
        {
            // Reported rather than thrown, and only when the driver says the failure is transient. This
            // is the one outcome the loop retries with backoff — anything else will fail identically on
            // the next attempt, so it is dead-lettered instead.
            return WidgetErrors.StorageUnavailable;
        }

        return new Response(retired);
    }
}
