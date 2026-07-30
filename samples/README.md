# Samples

`Ordering` is an HTTP API built on Loom, in its own solution so that an application's dependencies
stay out of the library solution. It exists for two reasons: to prove the packages work together,
and to be copied.

```bash
dotnet build samples/Loom.Samples.slnx
dotnet test  samples/Loom.Samples.slnx    # needs Docker; the integration tests start Postgres
```

## What each part demonstrates

| | |
| --- | --- |
| `Ordering.Domain` | Entities, aggregate roots, strongly-typed identities, invariants returning results, and specifications. References only Loom and the base class library — the one boundary enforced at compile time. |
| `Ordering.Api` | Five slices, one file and one namespace each. Endpoints are thin adapters; the database context is injected straight into handlers; specifications are applied to the query the slice owns. |
| `Ordering.AppHost` | Development-time orchestration. Starts Postgres and the service and wires the connection string between them, so nothing needs one in a file. Ships nothing. |
| `Ordering.ServiceDefaults` | Telemetry, health, resilience and service discovery. Part of the solution as editable code rather than a dependency, which is why it does not fall foul of the objection that ruled out frameworks owning the request path. |
| `Ordering.Domain.Tests` | Pure, fast, no infrastructure. |
| `Ordering.Api.Tests` | Every slice through real HTTP against real Postgres, reset with Respawn between tests. Also the route table snapshot, and the handler and validator registration checks. |
| `Ordering.ArchitectureTests` | The structural rules, asserted rather than reviewed. |

## The five slices

| Slice | What it covers |
| --- | --- |
| `POST /orders` | The validation decorator, domain construction, `Invalid` |
| `GET /orders/{orderId}` | `NotFound`, and a domain-state authorization check reporting `Forbidden` |
| `GET /orders` | A specification with eager loading and ordering, asynchronous paging, `Page<T>` |
| `POST /orders/{orderId}/cancel` | An invariant reporting `Conflict`; an ordinary domain event writing atomically |
| `POST /orders/{orderId}/ship` | A deferred domain event delivered through the outbox |

## Where it deviates from the guidance, and why

**Authorization uses a symmetric key issued by the sample.** The guidance leaves the identity
provider undecided, and a sample cannot contain an undecided. What is being demonstrated is the
shape: authenticated by default, named policy constants, and authorization that depends on domain
state living in the handler. A real deployment validates tokens from an external issuer and holds no
signing key at all.

The key lives in `appsettings.Development.json`, which means it is public — so `appsettings.json`
carries none, and startup refuses the development key in any other environment rather than accepting
tokens anyone could have signed. Running the sample outside Development means supplying one:

```bash
dotnet user-secrets --project samples/src/Ordering.Api \
    set Authentication:SigningKey "<at least 32 characters>"
```

The connection string is configured the same way, and in development the AppHost supplies it.

**There is no generated API document.** The current package brings a transitive dependency with a
known high-severity advisory, and the fixed major version is incompatible with it. It contributes
nothing to exercising Loom, so it was dropped rather than suppressed.

**Packages are referenced by project, not by version.** That means a change to Loom breaks this build
immediately, which is the point — but it also means packaging mistakes, such as a type left internal
that should be public, will not be caught here.

## Three things worth knowing, found by building this

**A validator that is not registered is silently ignored.** The decorator treats a missing validator
as nothing to validate, which is right for a slice with no rules. The assembly scan does not include
internal types unless asked, and slice validators are internal — so at first every validator in this
application was missing and no test of a valid request noticed. `ValidatorRegistrationTests` now turns
that into a failing build.

**An aggregate can raise an event about state it has not loaded.** Cancelling raises an event carrying
the order total, which is computed from its lines. Without eager loading, the total was zero and the
event was quietly wrong. A handler that raises an event must load what the event reports.

**The route table snapshot earns its keep immediately.** Adding the health endpoints from the service defaults broke it, which is the point: a route appeared and the build failed until the expected set was updated deliberately. Those endpoints answer any verb and are development-only, so the test host pins its environment to keep the set deterministic.

**Strongly-typed identities defeat a naive contract check.** An architecture test looking for domain
entities in request and response types flags `Id<Order>` unless it stops unwrapping at the identity.
An identity naming its entity is correct; the entity itself crossing the boundary is not.
