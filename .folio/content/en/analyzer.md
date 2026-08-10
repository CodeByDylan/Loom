# An analyzer for the one mistake this style makes easy

## situation

Returning failures as values instead of exceptions means a discarded `Result` is silently
a swallowed failure. The compiler has nothing to say about it.

## task

Catch discarded results at build time, without asking anyone to install or enable
anything.

## action

Shipped `LOOM0001` inside `CodeByDylan.Loom.Results`, so it arrives with the package.
Discarding on purpose stays legal by writing `_ =`, which makes the intent visible in
review.

## result

Its first run against existing code found eight unchecked discards in this repository's
own test suite.
