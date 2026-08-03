Ten foundational **.NET packages**, versioned in lockstep, each doing one thing and
depending only on the packages below it.

Clean Architecture with vertical slice structure, made opinionated on purpose: one way
to report a failure, one way to run a handler, one way to turn a failure into a status
code. Where a decision is forced, Loom makes it — where it is taste, it hands you the
object and gets out of the way.
