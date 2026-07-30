# Roadmap

Packages intended but not built. Tiers refer to the dependency table in `AGENTS.md` §4.

This is a list of intentions, not a work order. Nothing here should be started without a
decision to start it. Deliberately terse — if an entry grows into a specification, it has
outgrown this file.

| Package | Tier | Why |
| --- | --- | --- |
| `Loom.Outbox.Diagnostics` | 3 | Somewhere to see abandoned outbox messages and retry them deliberately. Today an abandoned message sits in the table as evidence, which is correct but not operable. Only worth building once something has actually been abandoned in anger. |
| `Loom.Results.Analyzers` | 0 | A Roslyn analyzer that makes discarding a `Result` a build error. Ships as a build-time asset inside the `Loom.Results` package, so it adds **no runtime dependency** and does not violate Tier 0. Must permit `_ = ...` as an explicit opt-out. |
| `Loom.Handlers.Logging` | 2 | A logging decorator. Deferred deliberately: what gets logged, at what level, with what structured fields, and whether the request body is included (it may hold secrets) are all decisions better made against a real project. |
| `Loom.Templates` | n/a | `dotnet new` templates that scaffold a solution and assemble its `AGENTS.md` from `docs/agents/`. Replaces `scripts/new-agents-md.sh`. |

## Built

`Loom.Http` shipped as **`Loom.Results.AspNetCore`**, at Tier 3 rather than the Tier 2 planned here:
problem details and results are ASP.NET Core types, and Tier 2 admits only `Microsoft.Extensions.*`
abstractions, so the coupling had to be named in the identifier.

It was also built before the "two consumer projects" condition originally written here was met,
deliberately. That condition assumed a stability obligation Loom has not taken on — while on `0.x`,
breaking changes need no ceremony, so extracting early costs little if a second project disagrees.
What the single project did change is the shape: only the parts that are *forced* are fixed, namely
the category-to-status mapping and the validation payload. Everything that is taste — titles, the
member carrying the code, extra members — is reachable by building the problem details and changing
it, rather than by configuring the package.

`Loom.Persistence.EntityFrameworkCore` shipped, and closes the three gaps its absence left:
specification eager loading, asynchronous paging, and domain event dispatch. Deferred events are
delivered through an optional outbox, opted into per event rather than globally, because immediate
and deferred handlers carry conflicting contracts.

## Built, under a different name

`Loom.Mediation` was planned here and shipped as **`Loom.Handlers`** — the rename happened once
it was settled that there is no dispatcher, which made "mediation" inaccurate. Its two
load-bearing constraints, the ones most likely to erode, now live in `AGENTS.md` §5 where they
will actually be read: **never add a dispatcher**, and **decorators are declared once, globally,
in order, and never discovered.**
