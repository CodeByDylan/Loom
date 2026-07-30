# The Loom Project

## What is Loom?
##### Loom is a group of foundational packages for multiple project types. 
Heavily based on Clean Architecture with vertical slice structure enforcement, but very opinionated in my own vision.

## Installing

Nine packages, versioned in lockstep. **Identifiers carry a `CodeByDylan.` prefix; namespaces do not:**

```bash
dotnet add package CodeByDylan.Loom.Results
```

```csharp
using Loom.Results;
```

That split is deliberate. Only the identifier needs the prefix, because the identifier is what
nuget.org reserves — and `Loom.` cannot be reserved, being a common word with packages already
published under it by other authors. The namespace stays short because it is the part you type.

Each of these installs as `CodeByDylan.<name>`:

`Loom.Results` · `Loom.Entities` · `Loom.Specifications` · `Loom.Paging` · `Loom.Handlers` ·
`Loom.Handlers.Abstractions` · `Loom.Handlers.FluentValidation` · `Loom.Results.AspNetCore` ·
`Loom.Persistence.EntityFrameworkCore`