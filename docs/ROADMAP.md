# Roadmap

Packages intended but not built. Tiers refer to the dependency table in `AGENTS.md` §4.

This is a list of intentions, not a work order. Nothing here should be started without a
decision to start it. Deliberately terse — if an entry grows into a specification, it has
outgrown this file.

| Package | Tier | Why |
| --- | --- | --- |
| `loom-cli` template | n/a | The last archetype. Needs `System.CommandLine` and a category-to-exit-code mapping that no package provides yet. |

## Built

`loom-worker` followed, so two of the three archetypes exist. Its schedule is tested by advancing a
fake clock rather than sleeping, and building it exposed two defects in that test harness worth
recording: asserting one dispatch hid the fact that the loop only ever ran once, and `PeriodicTimer`
coalesces ticks, so advancing a fake clock in a tight burst collapses every tick into a single
iteration. Both tests now require a second dispatch, which is what actually proves the loop survived
the first.

The worker has no equivalent of `ToHttpResult()`. Its category-to-disposition mapping — retry, dead
letter, log once — lives in the loop, which is where `10-worker.md` shows it. If a second worker project
ever wants the same mapping, that is the moment to consider a package for it, not before.

`Loom.Templates` shipped with **one archetype**, `loom-api`, not three. The API is the only archetype
with a reference implementation: `samples/Ordering` builds, runs and has already had four defects shaken
out of it, so the template was derived from something proven rather than invented to match a document.
There is no reference worker and no reference CLI, and inventing their structure inside a package whose
entire purpose is that people copy it unexamined is how a guess becomes everyone's convention.

So `scripts/new-agents-md.sh` survives. This entry originally said the templates replace it; that is not
true while two archetypes still need it, and it stays until they exist.

The `AGENTS.md` is assembled at pack time rather than at scaffold time, and the assembled copy is
committed so the package content is exactly what the repository shows. `check-docs.sh` regenerates it and
fails if it has drifted, because a template that ships stale guidance is worse than one that ships none.

Building it turned up three defects that no Loom test could have caught, because they only exist in
generated output: a `using` for a namespace that does not exist (`Loom.Results.AspNetCore` is a package
identifier, not a namespace), an Aspire resource name derived from the project name and therefore
invalid for any name containing a dot, and the class the Aspire SDK generates for a project reference,
which turns dots into underscores and so cannot be produced by the template engine's name substitution
alone. CI now scaffolds and builds a solution called `Acme.Billing` on every push — the dotted name is
the case that breaks and the one people actually use.

`Loom.Outbox.Diagnostics` shipped as `OutboxAdministration<TContext>` **inside
`Loom.Persistence.EntityFrameworkCore`**, for the same reason as the logging decorator: it needs the
outbox types and the mapper from a package in its own tier, and references may not reach sideways.

Its condition — "only worth building once something has actually been abandoned in anger" — has not
been met, so only the part that does not depend on operating experience was built. Finding what was
abandoned, retrying it and clearing what was delivered all follow from the table's own columns. How an
operator *reaches* those does not, so there is no endpoint, command, dashboard or metric, and this
entry survives for whichever of those turns out to be wanted.

Building it turned up something more pressing than retrying. **Nothing ever deleted a delivered
message**, so the table grew without bound in every deployment — guaranteed, unlike abandonment, which
needs a bug. Purging delivered messages is therefore part of the same work, and refuses to touch an
abandoned one at any age, since deleting the record of a failure is how a failure stays unexplained.

| Still wanted | |
| --- | --- |
| A surface | Whichever of an endpoint, a command, a dashboard or a metric an operator actually reaches for. |
| A retention schedule | Purging exists; nothing calls it on a timer. Deciding the interval needs a deployment. |

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
