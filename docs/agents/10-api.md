<!--LOOM-TEMPLATE Archetype delta: HTTP API. Assembled by scripts/new-agents-md.sh. Does not govern the Loom repo itself. -->
## A. HTTP API archetype

Applies to `src/MyApp.Api/`.

### Endpoints

- **Minimal APIs.** No controllers — a controller groups operations horizontally, which fights the slice layout.
- **Each slice exposes its own route registration.** The slice implements the `IEndpoint` marker and its `Map` method registers exactly one route.
- **Endpoints are discovered by assembly scan at startup.** One call in `Program.cs` maps every `IEndpoint`. This is convenient and it is also invisible — nothing in `Program.cs` links to your endpoint, so a slice that fails to register does not fail to compile.
- **Because discovery is reflective, the route-table snapshot test is mandatory.** One test asserts the complete set of registered routes; a slice that silently fails to register breaks a test rather than 404-ing in production. Do not skip it, and do not "fix" a failure by deleting the assertion — update the snapshot deliberately.
- Reflective discovery means **native AOT and trimming are not available** in this archetype. Do not add `PublishAot`.

```csharp
// Features/Orders/CreateOrder.cs
namespace MyApp.Features.Orders.CreateOrder;

internal sealed record Request(string Sku, int Quantity);
internal sealed record Response(Guid OrderId);

internal sealed class Validator : AbstractValidator<Request> { /* shape only */ }

internal sealed class Handler(AppDbContext db) : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken ct) { /* ... */ }
}

internal sealed class Endpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/orders", async (Request request, IHandler<Request, Response> handler, CancellationToken ct)
            => (await handler.HandleAsync(request, ct)).ToHttpResult());
}
```

### Responses

- **Every non-2xx response is `ProblemDetails`** (RFC 9457). No bespoke error envelopes.
- **Validation failures populate the `errors` extension** rather than inventing a parallel shape.
- **`ToHttpResult()` is the single mapping point** from a `Loom.Results` category to a status code. It lives in one host extension. A slice never writes a status code itself.
- Typed results (`Results<Ok<T>, ProblemHttpResult>`) where they add OpenAPI accuracy; not as ceremony.

### Security

- **Route groups carry `RequireAuthorization()` by default**, with explicit `AllowAnonymous()` as the opt-out. The failure you want is "I forgot to open this up," never the reverse.
- Rate limiting and CORS are configured in `Program.cs`, never per-slice.

### Testing

- `WebApplicationFactory` against a Testcontainers Postgres, Respawn between tests.
- **Every slice gets an integration test that goes through HTTP**, not one that calls the handler directly. Calling the handler skips model binding, validation, authorization, and the result mapping — which is most of what can break.
