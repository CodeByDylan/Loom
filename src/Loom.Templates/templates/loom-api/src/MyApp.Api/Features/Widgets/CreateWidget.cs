using FluentValidation;
using Loom.Entities;
using Loom.Handlers;
using Loom.Results;
using MyApp.Api.Infrastructure;
using MyApp.Domain.Widgets;

namespace MyApp.Api.Features.Widgets.CreateWidget;

// One operation, one file, one namespace. Request, response, validator, handler and route together,
// so everything an operation needs is in front of you and nothing else can reach it.

internal sealed record Request(string Name, int Size);

internal sealed record Response(Guid WidgetId);

// Shape only. Whether the domain permits this widget is Widget.Create's business.
internal sealed class Validator : AbstractValidator<Request>
{
    public Validator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Size).GreaterThan(0);
    }
}

internal sealed class Handler(AppDbContext database) : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        Result<Widget> created = Widget.Create(request.Name, request.Size);

        if (created.IsFailure)
        {
            return created.Error;
        }

        database.Widgets.Add(created.Value);
        await database.SaveChangesAsync(cancellationToken);

        return new Response(created.Value.Id.Value);
    }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapPost("/widgets", async (
            Request request,
            IHandler<Request, Response> handler,
            CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToHttpResult())
        .AllowAnonymous();
}
