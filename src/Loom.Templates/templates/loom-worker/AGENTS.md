# AGENTS.md

Rules for working on this project. Assembled from Loom's `docs/agents/` templates — this file
is yours now. Edit it freely.

> **FILL IN:** One sentence on what this service does, and which archetypes it contains.

## 1. Orientation

```text
src/MyApp.Domain/            pure domain; Loom packages + BCL only
src/MyApp.<Archetype>/       host; every slice lives here
src/MyApp.AppHost/           Aspire orchestration; dev-time only, ships nothing
src/MyApp.ServiceDefaults/   Aspire wiring; your code, edit it
tests/MyApp.Domain.Tests/
tests/MyApp.<Archetype>.Tests/
tests/MyApp.ArchitectureTests/
```

This is Clean Architecture reduced to its one boundary worth enforcing at compile time —
`Domain` purity — with vertical slices for everything else. The `Application`/`Infrastructure`
split is deliberately absent: it shreds a slice across two projects and cancels the point of
slicing.

A **slice** is one operation. One file, one namespace, holding its request, response, validator,
handler, and entry point together.

**The stack.** Versions live in `Directory.Packages.props`, never here and never in a `.csproj`.
Loom packages install under their full identifier, `CodeByDylan.Loom.<name>`; the namespace named below drops the `CodeByDylan.` prefix.

| Concern | Choice | Notes |
| --- | --- | --- |
| Data | EF Core + Npgsql, `Loom.Persistence.EntityFrameworkCore` | Postgres. No repositories. |
| Queries | `Loom.Specifications`, `Loom.Paging` | Named rules applied to a query the slice owns. |
| Validation | FluentValidation | Request shape only, never domain rules. |
| Dispatch | `Loom.Handlers` | No mediator. Handlers + one global decorator chain. |
| Mapping | Manual | Mapperly only for large mechanical maps. |
| Logging | `ILogger` + OpenTelemetry | No Serilog. |
| Orchestration | Aspire | Dev-time. Not in the request path. |
| Tests | TUnit, Testcontainers, Respawn | Plus NetArchTest for structure. |

Deliberately absent: MediatR and AutoMapper (both now require paid licences), Wolverine and
FastEndpoints (frameworks that would own request handling), Hangfire (LGPL, and Aspire covers
the observability its dashboard compensated for).

## 2. Hard constraints

1. **`Domain` references only Loom packages and the BCL.** No EF Core, no ASP.NET, no FluentValidation, no DI container.
2. **Never throw for expected failures.** Return a `Loom.Results` value.
3. **No slice references another slice.** Shared code within an aggregate goes in `_Shared.cs`. Anything two aggregates want belongs in `Domain`.
4. **Entities never cross the transport boundary.** Requests and responses are slice-owned types.
5. **`UNDECIDED` and `FILL IN` mean stop and ask.** Do not resolve them yourself and do not silently pick a convention.
6. **Run the §3 verify block before claiming done.** CI runs the same block; a green claim over a red build is a lie.
7. **Never edit this file to resolve a conflict between a rule and your code.** If a rule blocks you, say so.

## 3. Build and verify

```bash
dotnet format                       # fixes formatting in place
dotnet build                        # warnings are errors
dotnet test                         # TUnit
dotnet format --verify-no-changes   # confirms nothing is left unformatted
```

- Run `dotnet format` (fixing), not verify-only. Never hand-edit whitespace to satisfy the check.
- Revert formatting-only changes to files your work didn't otherwise touch.
- `.editorconfig` is the authority on code style. To change how code looks, edit it there. Do not add style rules to this file.

## 4. Architecture

- **One file per operation:** `Features/<Aggregate>/<Operation>.cs`.
- **One namespace per slice:** `namespace MyApp.Features.Orders.CreateOrder;`. This is what makes slice isolation mechanically enforceable (§11) rather than a review convention.
- **A slice over ~250 lines means the operation is doing too much.** Split the operation, not the file. A genuine helper gets a sibling file in the same folder, never a new folder.
- **`Features/<Aggregate>/_Shared.cs`** is the only permitted cross-slice sharing, and only within one aggregate.
- **Entry points are thin adapters.** An endpoint, a `BackgroundService`, or a CLI command validates nothing, decides nothing, and queries nothing — it adapts input and dispatches to a handler.
- **Request and response types are private to their slice.** If two slices want the same shape, they get two identical types. This looks wasteful and is the rule that keeps slices independent; a shared response DTO is how one slice's requirements start dictating another's.

## 5. Domain

- **Invariants live in `Domain` and return `Loom.Results`.** Not in validators, not in handlers, and never signalled by an exception.
- **Specifications live in `Domain` and derive from `Specification<T>`.** A specification is a *named business rule* — `OverdueOrders(customerId)` — configured entirely in its constructor. It may carry a predicate, eager-loading, and ordering; never paging.
- **Never give a specification a flag that toggles part of its query.** That is two rules sharing a name. Write two specifications.
- **Combine predicates with `Criteria.And`/`Or`/`Not`, not whole specifications.** Two specifications with conflicting ordering have no sensible combination.
- **No persistence attributes on entities.** Mapping is configured host-side via `IEntityTypeConfiguration<T>`.
- **`Domain` has no async I/O.** No `Task`-returning methods that reach outside memory.
- **Entities derive from `Entity<TSelf>`; aggregate roots from `AggregateRoot<TSelf>`.** Identity is `Id<TSelf>`, assigned at construction, never default.
- **Give every entity a private parameterless constructor for EF to materialise through.** EF writes the identity from the column, so the one minted in that constructor is discarded and the object stays attached to its row. Reconstructing through an `Id<TSelf>` constructor instead makes EF unable to choose between it and any other one-parameter constructor, and the model fails to build.
- **Entities are classes. Value objects and domain events are `sealed record`s.** Entities compare by identity, so structural equality is wrong for them; everything else in the domain is value-like and records are right.
- **Only aggregate roots get a `DbSet<>`.** Child entities are reached through their root.
- **`Id<TEntity>` ordering is not creation order.** Version 7 GUIDs are only millisecond-granular and are not monotonic within a millisecond. Never paginate on an id, and never use one to decide what happened first — sort on an explicit timestamp column.
- **Declare `WithLogging()` first in the chain, so it sits outermost.** Anything declared before it goes unrecorded — including a request refused by validation, which is the outcome most worth seeing. The level follows the failure's category: a refusal is information, an unavailable dependency is a warning, a success is debug. The request's *type name* is recorded and its contents never are, with no option to change that.
- **Declare `WithDomainEventFailures()` last in the chain, after `WithValidation()`.** A domain event handler reporting a failure abandons the save, which an object-relational mapper can only express by throwing; this decorator turns it back into the failure the handler reported, so the caller sees the right category instead of a server error. It must sit innermost, or it will also swallow exceptions from other decorators and report them as domain event failures.
- **An outbox needs a retention schedule, or it grows forever.** Nothing deletes a delivered message on its own. Call `OutboxAdministration<TContext>.PurgeDeliveredAsync` on a timer, with an interval you have chosen; it never touches a message that is still owed or one that was abandoned.
- **Recover an abandoned message through `OutboxAdministration<TContext>`, never by hand.** Retrying resets the attempt count and keeps the error, which is easy to get backwards in a hand-written statement: clear the error and the evidence is gone, leave the count and it abandons again on the first failure.
- **Ordinary domain events dispatch before the commit; deferred ones after.** An ordinary handler may change data atomically with the operation but must not reach outside the process, because a rollback cannot unsend an email. A `IDeferredDomainEvent` handler may reach outside the process and **must be idempotent**, since delivery is at least once. Which one applies is declared on the event.

Register identity conversion once per assembly, from `ConfigureConventions` — not `OnModelCreating`, where discovery has already skipped identities that are not keys:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
    configurationBuilder.UseLoomIdentities(typeof(Order).Assembly);
```

## 6. Persistence

- **One `AppDbContext`**, in the host. `IEntityTypeConfiguration<T>` classes co-located per aggregate.
- **Inject `DbContext` into handlers directly. No repositories.** `DbContext` is already a unit of work and `DbSet<T>` is already a repository; wrapping them produces passthrough interfaces and destroys the `IQueryable` composition that makes `Loom.Specifications` work.
- **Reads project in the query:** `AsNoTracking()` then `.Select(...)` straight into the slice's response type. Never materialise an entity in order to map it — that is the most common performance defect in EF codebases.
- **Migrations are generated with `dotnet ef`, reviewed by a human, and applied deliberately.** Never `EnsureCreated()`, never auto-migrate on startup in a deployed environment.
- Dapper is permitted for a specific query that demands it, as a documented exception. It is not a second default.
- **Apply specifications to the query the slice owns.** `db.Orders.ApplySpecification(new OverdueOrders(id))` — never hand a specification to something that queries on your behalf. Reintroducing a repository is the one way this design fails. The name differs from the specification package's own `Apply` deliberately; that one cannot honour eager loading and refuses rather than silently dropping it.
- **Paging is the caller's decision, applied after the specification:** `.ApplySpecification(spec).ToPageAsync(request, ct)`. Return `Page<T>` so every endpoint reports paging identically.
- **`PageRequest` validates itself, including a maximum size.** Never accept a raw page size from a query string without it — `?size=1000000` returns the table.

## 7. Validation and errors

- **FluentValidation for request shape** — format, ranges, required fields, cross-field consistency. One validator per slice, in the slice file.
- **Validation runs through `Loom.Handlers.FluentValidation`'s decorator**, not an endpoint filter. A decorator behaves identically in an API, a worker, and a CLI; a filter only exists in the first. It is enabled once, for every handler: `services.AddLoomHandlers(chain => chain.WithValidation())`.
- **Domain rules are never FluentValidation.** If a rule needs domain knowledge, it belongs in §5.
- **Every error carries one of six categories:** `NotFound`, `Conflict`, `Invalid`, `Unauthorized`, `Forbidden`, `Unavailable`. These are semantic, not transport-specific, which is what lets one category become a status code in an API, a retry-or-dead-letter decision in a worker, and an exit code in a CLI.
- **The set is closed.** A seventh category means the taxonomy has become a status-code enum. Carry the specifics as metadata on `Error<TMetadata>` instead.
- **A failed result carries exactly one error.** Several validation failures are one `ValidationError` whose metadata is a field→messages map, which maps straight onto the ProblemDetails `errors` extension.
- **Never serialize a `Result`.** It is a control-flow type; response types cross the wire. Serializers reflect over public members, and reading `Value` on a failure throws from inside the serializer.
- **Never ignore a returned `Result`.** Write `_ = ...` when the outcome really is of no interest — at which point you have said so, which is the whole point. Do not silence the rule to avoid the sentence.
- **`LOOM0001` covers a `Result` discarded directly, and only that.** A statement whose value is a `Result` — including an awaited one — fails the build, since warnings are errors here. Two cases it does not see: a `Task<Result>` that is never awaited, and a `Result` discarded as the body of a void-returning lambda. Those still need reading for, so do not treat a clean build as proof that no outcome was dropped.
- **Category-to-transport mapping comes from a Loom package, never inline in a slice.** In an API that is `Loom.Results.AspNetCore`: `result.ToHttpResult()`. If a project wants different titles or extra members, it builds the problem details with `ToProblemDetails()` and changes it — the package has no options type on purpose.

## 8. Configuration and authorization

- **Typed options only.** One `<Concern>Options` class per concern with a `const string SectionName`.
- **Validate at startup:** `.ValidateDataAnnotations().ValidateOnStart()`. A misconfigured app must fail to boot, not fail on the first request that touches the bad setting.
- **`IConfiguration` is read only in the composition root** — `Program.cs`, and the startup extensions it calls on the builder, such as `ServiceDefaults`. **Never inject it into a type resolved from the container**; bind a typed options class and inject that. The distinction is what the rule protects: reading a value while composing the application is composition, whereas a service reaching for configuration at run time hides a dependency the constructor does not declare.
- **Secrets:** user-secrets locally, environment variables when deployed. Never in `appsettings*.json`, including `Development`.
- **Authorization policies are named constants** in a `Policies` static class. No inline role or claim strings at call sites.
- **Authorization that depends on domain state belongs in the handler**, returning a `Forbidden` error. "Can this user cancel *this* order" needs the order, so it cannot be an attribute.

> **UNDECIDED:** Which identity provider issues tokens. Driven by the deployment environment,
> so the template does not choose. ASP.NET Core Identity is out of scope — self-hosting
> accounts, resets, and MFA is a project-defining decision, not a default.
>
> The routes are already closed: endpoints map into a group carrying `RequireAuthorization()`, and
> **no authentication scheme is registered until you add one**. Any endpoint that does not
> `AllowAnonymous()` will fault rather than refuse until that is done. Register the scheme first,
> then remove the opt-outs from the example slices.

## 9. Observability

- **Always an injected `ILogger<T>`.** Never a static logger, never `LoggerFactory.Create` at a call site.
- **`[LoggerMessage]` source-generated log methods, not interpolated strings.** Interpolation allocates and boxes even when the level is disabled, and produces unstructured output.
- OpenTelemetry is configured once, in `ServiceDefaults`. That file is your code — edit it rather than working around it.

## 10. Testing

- **`Domain` gets unit tests.** Pure, fast, no infrastructure.
- **Every slice gets at least one integration test through its real entry point.** This is the load-bearing rule; correctness lives here, because there are no repository seams to unit-test against.
- **`WebApplicationFactory` + Testcontainers Postgres**, one container and database per test assembly, **Respawn between tests**. Not transaction-rollback isolation — handlers own their transactions, so rollback-based isolation will lie to you.
- **`Aspire.Hosting.Testing` for a handful of smoke tests only.** Booting the AppHost per test destroys the feedback loop.
- **Never mock `DbContext`.** Mocking your own code is a design smell; mocking a genuine external dependency is fine.
- **Outbound HTTP is stubbed at `HttpMessageHandler`**, or WireMock.Net when you need protocol fidelity.

## 11. Enforcement

`tests/MyApp.ArchitectureTests` asserts, from the assembly's metadata:

1. `Domain` references nothing but Loom packages and the BCL.
2. No slice namespace depends on another slice namespace.
3. Domain entities appear in no request or response type's public surface.
4. `IConfiguration` is a constructor parameter of no type — it is read in the composition root or not at all.

`tests/MyApp.<Archetype>.Tests` asserts the one rule that needs a built container:

5. Every `IHandler<,>` implementation, and every validator, resolves from the application's own service graph. Metadata cannot answer this — a registration exists only once the container is built — so it lives beside the tests that have one. Discover both by reflection; naming a handler proves only that handler is registered.

A structural rule that is not in this list is a rule that will erode. If you add a structural
rule to this file, add its test.

## 12. Adding a slice

1. Create `Features/<Aggregate>/<Operation>.cs` with `namespace MyApp.Features.<Aggregate>.<Operation>;`.
2. Write the request, the response, the validator, and the handler in that file.
3. Register the handler and its decorator chain.
4. Wire the entry point (see the archetype section below).
5. Write at least one integration test through the real entry point.
6. Run the §3 verify block.

## 13. How to add a rule

- One rule per bullet, imperative, self-contained.
- Add a **rationale only if the rule is surprising** — if the obvious instinct is the opposite. Obvious rules need no defending.
- Add a `// Do this` / `// Not this` snippet if a rule is easy to satisfy in letter and violate in spirit.
- Unsettled rules get a `> **UNDECIDED:**` callout, not a guess.
- **Budget: ~400 lines assembled.** Over budget means something moves out — into `.editorconfig`, an analyzer, or an architecture test. Growing past it is not an option; agents stop reading.
## B. Worker archetype

Applies to `src/MyApp.Worker/`.

### Entry points

- **`BackgroundService` + `PeriodicTimer`.** No scheduling framework by default — most workers are "every N minutes" or "drain this," and the BCL does both with no dependencies.
- **Escalate to Quartz.NET only for a stated need:** cron expressions, clustering, or persistent job state. Adding it speculatively buys a database table and a configuration surface you do not want.
- **Hangfire is out.** `Hangfire.Core` is LGPL v3, which is a licence to adopt deliberately rather than inherit, and its Pro tier is paid. Its dashboard was compensating for missing observability, which Aspire already provides.
- **`ExecuteAsync` contains no business logic.** It ticks, runs one pass, and refuses to let a failure end the loop. Resolving a scope and dispatching to a handler happens a level down in `RunOnceAsync` — that is the position an endpoint occupies in the API archetype, and keeping the two apart is what makes either testable without the other.

```csharp
internal sealed partial class ReconcileOrdersWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ReconcileOrdersWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await RunGuardedAsync(ct);
        }
    }

    // internal, not private: the testing rules below call this directly, so the test assembly needs
    // an InternalsVisibleTo.
    internal async Task RunGuardedAsync(CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(ct);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Swallowed, never silent. Only cancellation that shutdown asked for ends the loop.
            Failed(logger, exception);
        }
    }

    // ExecuteAsync schedules and keeps the loop alive; one pass lives here.
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IHandler<Request, Response>>();
        var result = await handler.HandleAsync(new Request(), ct);
        // map result category to retry / dead-letter / log — never throw to signal it
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A pass threw and was swallowed to keep the worker alive.")]
    private static partial void Failed(ILogger logger, Exception exception);
}
```

- **Resolve a new DI scope per iteration.** A `DbContext` captured across iterations accumulates tracked entities and will eventually behave incorrectly. This is the most common worker defect.
- **Pass the `CancellationToken` everywhere and honour it.** A worker that ignores shutdown gets killed mid-transaction.
- **Inject `TimeProvider`, never `DateTime.Now`.** It is also what makes the timer testable.

### Error handling

- **A failed iteration must not kill the worker.** An unhandled exception out of `ExecuteAsync` stops the service silently in some hosting models. Catch, log, and continue.
- **The `Loom.Results` category decides the response:** `Unavailable` retries with backoff; `Invalid` and `Conflict` dead-letter, because retrying will not change the outcome; `NotFound` is usually a no-op worth logging once.
- **Retries are bounded and logged.** Unbounded retry against a permanent failure is an outage with extra steps.

### Testing

- **Handlers are tested directly**, against Testcontainers Postgres, with Respawn between tests. There is no transport to go through, so the handler *is* the entry point.
- **Separate the pass from the schedule, and test the pass.** `RunOnceAsync` holds everything decided by a result — the scope, the dispatch, retry against dead-letter — so it can be exercised against a stub handler with no clock and no timer. Make the retry backoff a setting so a test can set it to zero.
- **Test one guarded iteration, not the timer.** Extract the `try`/`catch` into an `internal` method — with `<InternalsVisibleTo Include="MyApp.Worker.Tests" />` on the host project — so a test can call it directly and assert which exceptions survive it. That a failing pass does not end the loop is your logic; that `PeriodicTimer` fires is the BCL's. Driving a real `BackgroundService` from a fake clock needs the scheduler to hand off between advancing the clock and the loop resuming — a test that does it is flaky unless it sleeps, and then it is a slow test of someone else's code.
