# Roadmap

Packages intended but not built. Tiers refer to the dependency table in `AGENTS.md` §4.

This is a list of intentions, not a work order. Nothing here should be started without a
decision to start it. Deliberately terse — if an entry grows into a specification, it has
outgrown this file.

| Package | Tier | Why |
| --- | --- | --- |
| `Loom.Http` | 2 | Maps a `Loom.Results` error category to `ProblemDetails` and a status code. Only after the mapping has proved itself in two consumer projects. |
| `Loom.Persistence.EntityFrameworkCore` | 3 | **The single EF Core seam.** Applies specification eager-loading (`Include`/`ThenInclude`, plus string paths) on top of `ApplyCriteriaAndOrdering`; supplies the `Id<TEntity>` value converter; adds `ToPageAsync` so paging counts and fetches without materialising everything; and **dispatches domain events** from a `SaveChanges` interceptor by draining every changed `IAggregateRoot`. Deliberately one package rather than a `.EntityFrameworkCore` sibling per feature. |
| `Loom.Results.Analyzers` | 0 | A Roslyn analyzer that makes discarding a `Result` a build error. Ships as a build-time asset inside the `Loom.Results` package, so it adds **no runtime dependency** and does not violate Tier 0. Must permit `_ = ...` as an explicit opt-out. |
| `Loom.Handlers.Logging` | 2 | A logging decorator. Deferred deliberately: what gets logged, at what level, with what structured fields, and whether the request body is included (it may hold secrets) are all decisions better made against a real project. |
| `Loom.Templates` | n/a | `dotnet new` templates that scaffold a solution and assemble its `AGENTS.md` from `docs/agents/`. Replaces `scripts/new-agents-md.sh`. |

## Built, under a different name

`Loom.Mediation` was planned here and shipped as **`Loom.Handlers`** — the rename happened once
it was settled that there is no dispatcher, which made "mediation" inaccurate. Its two
load-bearing constraints, the ones most likely to erode, now live in `AGENTS.md` §5 where they
will actually be read: **never add a dispatcher**, and **decorators are declared once, globally,
in order, and never discovered.**
