# AGENTS.md

Rules for working **on Loom itself**. Loom is a family of foundational .NET packages,
consumed by other projects. Rules for those consuming projects live in
`docs/AGENTS.consumer.template.md` and **do not apply here**.

## 1. Orientation

Packages that exist today:

| Package | Purpose | Does not contain |
| --- | --- | --- |
| `Loom.Results` | Result and error abstractions — the vocabulary every other package's signatures are written in. Carries the analyzer that reports a discarded result. | Anything with a dependency. Anything domain-specific. |
| `Loom.Results.Analyzers` | `LOOM0001`: a result computed and thrown away. Packed into `Loom.Results`, never referenced at run time. | Anything the compiler already reports. A code fix — inserting `_ =` should take a moment's thought, not a keystroke. |
| `Loom.Entities` | `Id<TEntity>`, `Entity<TSelf>`, `AggregateRoot<TSelf>`, `IDomainEvent`. Identity, identity equality, and domain event collection. | Persistence. Querying. Event *dispatch*. Audit fields. Concurrency tokens. A `ValueObject` base — `sealed record` already does that. |
| `Loom.Specifications` | Named business rules over a type: a predicate, optional eager-loading, optional ordering, as pure expression trees. Applied to an `IQueryable` the caller still owns. | Paging. Any specific ORM. A repository to consume them. |
| `Loom.Paging` | `PageRequest` and `Page<T>` — offset paging vocabulary, so no project reinvents the envelope. | Cursor paging. Async counting (needs an ORM). |
| `Loom.Handlers.Abstractions` | `IHandler<TRequest, TResponse>` and `IHandler<TRequest>`, as pure types, so a consumer's domain project can declare handlers. | Anything touching a container. |
| `Loom.Handlers` | Registers handlers and wraps each in an explicit, ordered decorator chain. Carries the logging decorator, whose level follows the failure's category. | A dispatcher. See §5. Any way to log a request's contents. |
| `Loom.Handlers.FluentValidation` | A decorator that validates requests before a handler runs, returning an `Invalid` failure. | Any validation rule of its own. |
| `Loom.Persistence.EntityFrameworkCore` | The single EF Core seam: identity conversion, specification eager loading, `ToPageAsync`, domain event dispatch, an optional outbox with administration over it, and a decorator turning an abandoned save back into the failure that caused it. | A database provider — the consumer picks one. Cursor paging. Any endpoint, command or dashboard over the outbox. |

Every package listed has code. There are no placeholder projects left.

Layout: `src/Loom.<Name>/` and `tests/Loom.<Name>.Tests/`. The `.slnx` groups these into
per-area virtual folders — virtual structure and disk structure are allowed to differ.

## 2. Hard constraints

1. **Check the dependency tier table (§4) before adding any `PackageReference` or `ProjectReference`.** If the reference is not permitted, stop and ask. Never promote a package a tier to make code compile.
2. **`Loom.Results` depends on nothing.** BCL only. No exceptions.
3. **Never throw for expected failures.** Failure is a return value.
4. **`UNDECIDED` means stop and ask.** Do not resolve the question yourself, and do not silently pick a convention.
5. **Never edit this file to resolve a conflict between a rule and your code.** If a rule blocks you, say so.
6. **Run the §3 verify block before claiming done.** CI runs the same block; a green claim over a red build is a lie.
7. **Never create a git tag, never `dotnet nuget push`.** Releasing is a human action.

## 3. Build and verify

Run all five, in order, before reporting work complete:

```bash
scripts/check-docs.sh               # markdown is neither built nor formatted
dotnet format                       # fixes formatting in place
dotnet build                        # warnings are errors
dotnet test                         # TUnit, via Microsoft.Testing.Platform
dotnet format --verify-no-changes   # confirms nothing is left unformatted
```

- Run `dotnet format` (fixing), not verify-only. Do not hand-edit whitespace to satisfy the check.
- If `dotnet format` touches files unrelated to your change, revert those files. Formatting-only churn does not belong in a feature diff.
- `.editorconfig` is the authority on code style, not this file. To change how code looks, edit `.editorconfig`. Do not add style rules here.
- **Never edit a markdown table with a pattern substitution.** Two documents were silently corrupted that way — a table row fused onto a heading, and the same onto a template marker, which then leaked into every assembled consumer file. `scripts/check-docs.sh` now catches both, but the habit is the actual defect: edit tables by replacing exact text.
- `.github/workflows/ci.yml` runs the same checks on push and PR, with one deliberate difference: it has no fixing `dotnet format` step, only the verify. CI cannot commit fixes, so formatting is fixed locally and merely confirmed there.

## 4. Dependency policy

Every package sits in a tier. **References point strictly lower — never sideways.** Two packages
in the same tier may not reference each other, which forces a genuine layering decision instead
of letting a tier become a web of mutual references.

| Tier | Packages | May reference |
| --- | --- | --- |
| 0 | `Loom.Results` | **Nothing.** BCL only. |
| 1 | `Loom.Entities`, `Loom.Specifications`, `Loom.Paging`, `Loom.Handlers.Abstractions` | Tier 0 + BCL. |
| 2 | `Loom.Handlers` | Tiers 0–1 + `Microsoft.Extensions.*` **Abstractions** packages only. |
| 3 | `Loom.Handlers.FluentValidation`, `Loom.Persistence.EntityFrameworkCore`, `Loom.Results.AspNetCore` | Tiers 0–2 + one third-party dependency, named in the package ID. |

- Tier 0 is absolute. `Loom.Results` appears in every consumer's method signatures, so any dependency it takes is in every consumer's transitive graph forever.
- At Tier 2, reference abstractions packages only — `Microsoft.Extensions.Logging.Abstractions`, never `Microsoft.Extensions.Logging`. The non-abstractions package is the one that shows up in application code; it does not belong in a library.
- Third-party coupling is declared in the package ID: `Loom.Persistence.EntityFrameworkCore`, never a `Loom.Persistence` that quietly pulls in EF Core. A consumer should be able to read their NuGet list and know their coupling.
- **A `FrameworkReference` counts as the named dependency.** The tier rules talk about package references throughout, so a framework reference can look free. It is not: referencing `Microsoft.AspNetCore.App` restricts a package to ASP.NET Core hosts as firmly as any dependency, so it puts the package at Tier 3 and must be declared in the package ID the same way.
- "One third-party dependency" means one *product*, not one NuGet identifier. `Loom.Persistence.EntityFrameworkCore` references both `Microsoft.EntityFrameworkCore` and its `.Relational` companion, because mapping a table it defines is impossible without the latter. A second, unrelated product would not be permitted.
- A Tier 3 package stays **provider-neutral** where the product allows it. `Loom.Persistence.EntityFrameworkCore` does not reference Npgsql; a consumer chooses its own provider. Naming a provider would make the package a second opinion about the database.
- The `dotnet-ef` tool in `dotnet-tools.json` serves `Loom.Persistence.EntityFrameworkCore`. EF Core must not appear in Tiers 0–2.
- All versions live in `Directory.Packages.props`. Never put a `Version` attribute on a `PackageReference`.

## 5. API design

- **Seal every public class** unless inheritance is a designed feature. Unsealing later is non-breaking; sealing later is not.
- **Make helpers, extensions, and implementation details `internal`.** The abstractions a package exists to expose are public; nothing else is by default.
- **Prefer immutability.** `init`-only or constructor-set properties, no setters. Mutable state in a foundational type needs an argument.
- **Every public member has an XML doc comment.** This is enforced: `GenerateDocumentationFile` is on and warnings are errors. If a `<summary>` is hard to write, the API is not ready.
- **Do not redeclare `TargetFramework`, `Nullable`, `ImplicitUsings`, or `LangVersion` in a `.csproj`.** `Directory.Build.props` owns them — including the one exception, analyzer projects, which the compiler only loads when built against `netstandard2.0`. That is decided from the project name there, not by a flag in the project: the file is imported before a project's own properties, so a flag set there is read as unset.
- **An analyzer ships inside the package whose contract it enforces**, as a build-time asset, so it adds no runtime dependency and a consumer gets it without installing anything else. Analyzers do not travel through a `ProjectReference`, so anything in this repository that should be checked by one has to reference it explicitly.
- **One concept per file, named after it.** `Result` and `Result<T>` share `Result.cs` because they are one concept that converts between itself. Two unrelated types do not share a file.
- **Throwing on a programming error is correct, and is not what §2.3 forbids.** Hard constraint 3 is about *expected* failures — a domain rule that did not hold. Misusing an API is a bug: reading `Value` off a failed result, or observing a `default`-constructed one, throws deliberately. Do not "fix" those guards by returning a failure; a silent wrong answer is worse than a stack trace.
- **`Loom.Results.Error` is unsealed, as a documented exception to seal-by-default.** Consumers derive from it to declare domain errors, and deriving is what makes them convert implicitly to a `Result`. C# forbids user-defined conversions from an interface (CS0552), so an `IError` interface could not have offered that.
- **Never serialize a `Result`.** It is a control-flow type; response types cross the wire. Serializers reflect over public members, and reading `Value` on a failure throws from inside the serializer.
- **Avoid boxing on the success path of generic code.** `ArgumentNullException.ThrowIfNull` takes `object?`, so calling it on an unconstrained `T` boxes every value. Guard with `!typeof(T).IsValueType && value is null`, which the JIT erases for value types.
- **`Loom.Handlers` never gains a dispatcher.** No `ISender`, no `IMediator`, no resolving a handler by request type. Consumers inject `IHandler<TRequest, TResponse>` directly, so the caller and the handler are visible to each other and the decorator chain is knowable from `Program.cs`. A dispatcher would let a caller invoke a handler it cannot see — the coupling-hiding that a slice architecture exists to prevent. This is the constraint most likely to be eroded by someone adding it "for completeness."
- **Never log a request's contents, and offer no option to.** A request holds whatever a caller sent — passwords, tokens, personal data — so the logging decorator records its *type name* only. An option to include the request is one that gets enabled in production to debug something; a project that genuinely wants payloads writes its own decorator and owns that decision.
- **A decorator that only needs `Microsoft.Extensions.*` abstractions belongs in `Loom.Handlers`, not a package of its own.** `Loom.Handlers.FluentValidation` is separate because its coupling is third-party and has to be visible before installing. Logging abstractions carry no such coupling, so a separate package would have needed Tier 3 purely to reference Tier 2 sideways — a workaround, not a description.
- **Decorators are declared once, globally, in order, and never discovered.** No assembly scanning for decorators, no per-handler overrides. If reading the single `AddLoomHandlers` call does not tell you exactly what wraps a handler, the design has been broken.
- **Entities are classes; value objects and domain events are records.** An entity is equal to another by *identity*, so structural equality is actively wrong for it — two `Order`s with the same `Id` are the same order however far their fields have diverged. `record` is the reflexive default for a new type now, which is exactly why this needs saying. `Entity<TSelf>` and `AggregateRoot<TSelf>` are the second and third documented exceptions to seal-by-default.
- **A specification is a named rule, never a parameter bag, and never consumed by a repository.** `OverdueOrders(customerId)` is the shape. A specification that grows a boolean toggling part of its query is two specifications sharing a name — split it. Specifications are applied to an `IQueryable` the caller owns; the moment something else consumes them, the repository we rejected has returned. `Specification<T>` is the fourth documented exception to seal-by-default.
- **Composition is over criteria, not whole specifications.** Two specifications each carrying their own ordering have no defined combination. Combine predicates with `Criteria.And`/`Or`/`Not`, which rebind parameters properly — never wrap in `Expression.Invoke`, which many query providers cannot translate.
- **A request handler and a domain event handler are different abstractions, deliberately.** `IHandler` is request/response: exactly one per request type, invoked by a caller that wants the result, wrapped in the decorator chain. `IDomainEventHandler` is fan-out: any number per event, invoked by infrastructure, no chain, nobody awaiting a value. Keeping them separate is what preserves the no-notifications rule above; collapsing them is what would break it.
- **Store an instant that must be ordered as UTC ticks, not as a `DateTimeOffset`.** Providers disagree on whether an offset-bearing value can be ordered at all — SQLite refuses — so a provider-neutral package that orders by time cannot store the native type.
- **Do not present `Id<TEntity>` ordering as creation order.** Version 7 GUIDs embed a millisecond timestamp, and .NET does not make them monotonic within one — measured at ~50% inversion for values created back to back. Ordering is for index locality and stable sorting only. Never paginate on it, and never use it to decide which of two things happened first.

## 6. Testing

- **Every package has a test project**, created in the same change as the package. Not later.
- **TUnit, with TUnit's built-in assertions.** No xUnit, no NUnit, no Shouldly, no FluentAssertions.
- **No mocking library.** If a Loom package needs a mock to be tested, the design is wrong — say so rather than reaching for NSubstitute.
- Test method names read as sentences: `HasValue_Is_False_When_Value_Is_Null`. `.editorconfig` disables the PascalCase naming rule under `tests/` for exactly this reason.
- TUnit assertions are awaited: `await Assert.That(x).IsFalse();`.
- **No mutable static state in tests.** TUnit runs tests in parallel by default, so a `static` counter or flag is shared across them and produces failures that look like product bugs. Record through an injected object scoped to the test instead.
- **A test fixture is `public sealed`.** Public so TUnit discovers it, sealed because nothing derives from it — the same rule as §5, applied here too so there is no second convention to remember. Test methods carry no documentation comment: their names are the sentence, and test projects generate no documentation file for one to appear in.

## 7. Adding a new package

1. Create `src/Loom.<Name>/Loom.<Name>.csproj`. It should contain a `<Description>` and little else.
2. Create `tests/Loom.<Name>.Tests/Loom.<Name>.Tests.csproj` with `<OutputType>Exe</OutputType>` and a `PackageReference` to `TUnit`.
3. Register both in `Loom.slnx` under `/<Area>/src/` and `/<Area>/tests/`.
4. Add the package to the §4 tier table **before** adding any reference to it or from it.
5. Add any new dependency's version to `Directory.Packages.props`.
6. Run the §3 verify block.

## 8. Versioning and release

- Versions come from git tags via MinVer. Tags look like `v0.3.0`; `MinVerTagPrefix` is `v`.
- **All packages version in lockstep** from one repo-wide tag. There are no per-package tag streams.
- Packages publish to **nuget.org** under the **MIT** licence (`LICENSE`, and `PackageLicenseExpression` in `Directory.Build.props`).
- **Loom is pre-1.0, deliberately.** While on `0.x`, breaking changes are permitted on a minor bump and need no ceremony. Do not preserve an awkward API out of compatibility caution — there are no external consumers to protect. Fix the design.
- Tagging and pushing are human actions (§2.7). `release.yml` is what makes that mechanical rather than customary: it runs only for a tag someone pushed, and its publish step waits on the `nuget` environment, whose required reviewers are configured in repository settings. An agent prepares a release; a person causes one.
- **A release verifies before it packs, and checks the version before it pushes.** `scripts/check-release-version.sh` compares every produced package against the tag. MinVer is configured, not magical: a wrong prefix or a shallow clone yields `0.0.0-alpha.0.N` while everything else looks healthy, and a published version can never be replaced — so the wrong number would be burned for good.
- **`v0.1.0` releases `0.1.0`.** The prefix in the tag is stripped, matching `MinVerTagPrefix`. A tag without it is refused rather than guessed at.
- Publishing needs a `NUGET_API_KEY` secret on the repository. `workflow_dispatch` runs the whole thing without publishing, which is how to check a release before making one.

## 9. Consuming Loom

`docs/agents/` holds the opinionated stack and structure for projects *built on* Loom:
`00-core.md` plus one delta per archetype (`10-api.md`, `10-worker.md`, `10-cli.md`).
`scripts/new-agents-md.sh` assembles them into a single `AGENTS.md` for a new project.

**Nothing in `docs/agents/` governs the code here.** Those files describe applications —
EF Core, ASP.NET, FluentValidation, Aspire. Applying any of it to a Loom package would
violate §4. Loom's constraints are the opposite of an application's.

Planned-but-unbuilt packages are listed in `docs/ROADMAP.md`. It is a list of intentions,
not a work order — do not start building from it.

---

## How to add a rule

- One rule per bullet, imperative, self-contained.
- Add a **rationale only if the rule is surprising** — i.e. if the obvious instinct is the opposite. Obvious rules do not need defending.
- Add a `// Do this` / `// Not this` snippet if the rule is easy to satisfy in letter and violate in spirit.
- If a rule should exist but is not settled, write a `> **UNDECIDED:**` callout in the relevant section rather than guessing. The set of these markers is the design agenda.
- **Budget: ~200 lines.** Over budget means something must move out — down into `.editorconfig`, an analyzer, or an architecture test, or sideways into a package-level `src/Loom.<Name>/AGENTS.md` for rules that apply to one package only. Growing past the budget is not an option; agents stop reading.
