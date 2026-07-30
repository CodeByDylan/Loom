# Roadmap

Packages intended but not built. Tiers refer to the dependency table in `AGENTS.md` §4.

This is a list of intentions, not a work order. Nothing here should be started without a
decision to start it. Deliberately terse — if an entry grows into a specification, it has
outgrown this file.

| Package | Tier | Why |
| --- | --- | --- |
| `Loom.Outbox.Diagnostics` | 3 | Somewhere to see abandoned outbox messages and retry them deliberately. Today an abandoned message sits in the table as evidence, which is correct but not operable. Only worth building once something has actually been abandoned in anger. |
| `Loom.Templates` | n/a | `dotnet new` templates that scaffold a solution and assemble its `AGENTS.md` from `docs/agents/`. Replaces `scripts/new-agents-md.sh`. |

## Built

`Loom.Handlers.Logging` shipped as `chain.WithLogging()` **inside `Loom.Handlers`**, not as a package.
It would have needed `IHandlerChainBuilder` from a package in its own tier, which references may not
reach sideways — so a separate package would have had to sit at Tier 3 purely to reference Tier 2, a
workaround rather than a description. Nothing forced the split: unlike FluentValidation, logging
abstractions carry no third-party coupling worth declaring in an identifier.

The decisions this entry deferred, now made against a real project: the level follows the failure's
category, so a refusal is information and only an unavailable dependency is a warning; a success is
debug, because one line per operation is noise at volume; and the request's contents are never
recorded, with no option to change that.

`Loom.Results.Analyzers` shipped, as a warning rather than the build error planned here. Anyone
following the guidance builds with warnings as errors, so it fails their build regardless, while a
package that hard-errors on install invites being switched off wholesale rather than raised
deliberately. `.editorconfig` remains the place to make that choice.

Its first run on existing code found eight unchecked discards in the sample's own tests — in a
repository whose guidance already said not to ignore a result.

`Loom.Http` shipped as **`Loom.Results.AspNetCore`**, at Tier 3 rather than the Tier 2 planned here.
`Loom.Results` itself remains Tier 0 and knows nothing of HTTP; what forces Tier 3 is the other side
of the translation — `ProblemDetails` and ASP.NET Core's own `IResult` — which belong to a framework
that Tier 2 does not admit, since it allows only `Microsoft.Extensions.*` abstractions. Hence the
coupling is named in the identifier.

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
