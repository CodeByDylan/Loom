Every new .NET service started the same way. A `Result` type, rewritten slightly
differently than last time. A folder layout argued about again. A decision about whether
a not-found is an exception or a return value, made again, and made differently.

None of those are hard problems. That is exactly why re-deciding them is wasteful — the
cost is not the thinking, it is that two services in the same solution end up disagreeing
about what a failure looks like.

So Loom takes the decisions away. There is one `Error`, with a stable code and one of six
categories, and that category is what decides the status code, the log level, and whether
a retry makes sense. An endpoint becomes a thin adapter with no mapping logic of its own.
Handlers are injected and called directly, so "go to definition" lands on the handler
instead of a registry.

The harder half was deciding what to leave out. No mediator, because indirection you
cannot navigate is a cost paid on every read. No repository, because `DbContext` is
already a unit of work and wrapping it destroys the `IQueryable` composition that makes
specifications work. No option to log a request's contents — not defaulted off, but
absent, because an option is an invitation.

Ten packages rather than one, so nothing drags in a framework you did not ask for. They
version in lockstep because a matrix of compatible versions is its own maintenance
problem, and this is meant to remove those, not add one.
