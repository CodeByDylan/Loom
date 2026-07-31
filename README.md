# The Loom Project

Loom is a group of foundational packages for multiple project types. Heavily based on Clean
Architecture with vertical slice structure enforcement, but very opinionated in my own vision.

Ten packages, versioned in lockstep, each doing one thing and depending only on packages below it.
The opinions are the point: there is one way to report a failure, one way to run a handler, one way
to turn a failure into a status code. Where a decision is forced, Loom makes it. Where it is taste,
Loom leaves you the object and gets out of the way.

## Installing

Start a whole solution from a template, or add packages to one you have.

```bash
dotnet new install CodeByDylan.Loom.Templates
dotnet new loom-api --name Acme.Billing      # or loom-worker
```

**Identifiers carry a `CodeByDylan.` prefix; namespaces do not:**

```bash
dotnet add package CodeByDylan.Loom.Results
```

```csharp
using Loom.Results;
```

That split is deliberate. Only the identifier needs the prefix, because the identifier is what
nuget.org reserves — and `Loom.` cannot be reserved, being a common word with packages already
published under it by other authors. The namespace stays short because it is the part you type.

## The packages

Each installs as `CodeByDylan.<name>`. Take only the ones you need; nothing pulls in a framework you
did not ask for. `Loom.Templates` is the exception — it is installed with `dotnet new install`, not
referenced by a project.

| Package | What it gives you |
| --- | --- |
| `Loom.Results` | `Result`, `Result<T>`, `Error` and a closed six-member `ErrorCategory`. The vocabulary every other package's signatures are written in. Ships an analyzer that warns when a result is discarded. Depends on nothing. |
| `Loom.Entities` | `Id<TEntity>`, `Entity<TSelf>`, `AggregateRoot<TSelf>`, `IDomainEvent`. Strongly-typed identities over UUID v7, identity equality, and domain event collection. |
| `Loom.Specifications` | Named business rules over a type — a predicate, optional eager loading, optional ordering — as pure expression trees, applied to an `IQueryable` you still own. |
| `Loom.Paging` | `PageRequest` and `Page<T>`, so no project reinvents the paging envelope. |
| `Loom.Handlers.Abstractions` | `IHandler<TRequest, TResponse>` as pure types, so a domain project can declare handlers without referencing a container. |
| `Loom.Handlers` | Registers handlers and wraps each in an explicit, ordered decorator chain. Carries the logging decorator. |
| `Loom.Handlers.FluentValidation` | A decorator that validates a request before the handler runs, returning an `Invalid` failure. |
| `Loom.Results.AspNetCore` | `result.ToHttpResult()` — the category-to-status-code mapping, as RFC 9457 problem details. |
| `Loom.Templates` | `dotnet new` templates that scaffold a whole solution — `loom-api` and `loom-worker` — with the guidance for working in it already assembled. Ships no assembly. |
| `Loom.Persistence.EntityFrameworkCore` | The single EF Core seam: identity conversion, specification eager loading, `ToPageAsync`, domain event dispatch inside the save's transaction, and an optional transactional outbox with administration over it. |

## What it looks like

One operation is one file: its request, response, handler and entry point together.

```csharp
internal sealed record Request(Id<Order> OrderId);

internal sealed record Response(Guid OrderId, string Status, int Total);

internal sealed class Handler(OrderingDbContext database, ICurrentCustomer customer)
    : IHandler<Request, Response>
{
    public async Task<Result<Response>> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        // Eagerly loaded, because the total is computed from the lines. An aggregate reporting state
        // it has not loaded is quietly wrong rather than loudly broken.
        Order? order = await database.Orders
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        // Needs the order to decide, so it cannot be an attribute or a policy.
        if (order.CustomerId != customer.Id)
        {
            return OrderErrors.NotYours;
        }

        return new Response(order.Id.Value, order.Status.ToString(), order.Total);
    }
}
```

Errors are values with a stable code and a category:

```csharp
public static Error NotFound { get; } = Errors.NotFound("orders.not_found", "No such order.");
public static Error NotYours { get; } = Errors.Forbidden("orders.not_yours", "That order belongs to another customer.");
```

The category is what decides the status code, the log level and whether a retry makes sense — so the
endpoint is a thin adapter with no mapping of its own:

```csharp
routes.MapGet("/orders/{orderId}", async (
    Id<Order> orderId,
    IHandler<Request, Response> handler,
    CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new Request(orderId), cancellationToken)).ToHttpResult());
```

Cross-cutting behaviour is declared once, globally, and applies to every handler — declaration order
is nesting order:

```csharp
services.AddLoomHandlers(chain => chain
    .WithLogging()
    .WithValidation()
    .WithDomainEventFailures());
```

## What it deliberately does not have

Often the more useful half of an opinion:

- **No dispatcher or mediator.** Handlers are injected as `IHandler<TRequest, TResponse>` and called
  directly, so "go to definition" reaches the handler rather than a registry.
- **No repository.** `DbContext` is already a unit of work and `DbSet<T>` is already a repository;
  wrapping them produces passthrough interfaces and destroys the `IQueryable` composition that makes
  specifications work.
- **No exceptions for expected failures.** A failure a caller can anticipate is a `Result`.
- **No way to log a request's contents.** Not an option that defaults to off — it does not exist.
- **No options type on the problem details mapping.** What is forced is fixed; what is taste is
  reachable by building the object and changing it.
- **No MediatR, AutoMapper, Wolverine, FastEndpoints or Hangfire**, and no cursor paging.

## The analyzer

`Loom.Results` ships `LOOM0001`, which warns when a `Result` is computed and thrown away — the one
mistake this style makes easy. It arrives with the package; there is nothing to install or enable.

```csharp
Compute();      // warning LOOM0001: This Result<int> is discarded, so a failure would go unnoticed
_ = Compute();  // fine — discarding on purpose is visible
```

Its first run on existing code found eight unchecked discards in this repository's own tests.

## Building projects on Loom

[`docs/agents/`](https://github.com/CodeByDylan/Loom/tree/main/docs/agents) holds an opinionated stack
and structure for projects *built on* Loom — a shared core plus one delta per archetype (API, worker,
CLI), written to be read by an AI coding agent as much as by a person.

[`samples/`](https://github.com/CodeByDylan/Loom/tree/main/samples) is a working ordering API that
exercises every package: five slices, real HTTP, real Postgres, domain events inside the transaction
and an outbox after it.

## Versioning

Every package ships in lockstep from one repository-wide tag, so the versions always match. **Loom is
pre-1.0 deliberately** — while on `0.x`, breaking changes may land on a minor bump. The design is
still allowed to be wrong.

## Licence

[MIT](https://github.com/CodeByDylan/Loom/blob/main/LICENSE).
