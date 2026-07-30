#!/usr/bin/env bash
# Guards the documents nothing else checks.
#
# The verify block has no opinion on markdown: it is neither built nor formatted. Two files were
# silently corrupted before this existed — a table row fused onto a heading in AGENTS.md, and the same
# damage in a template fragment, where it also stopped the assembly script recognising the marker so
# every assembled file leaked an internal comment. Both survived review and CI.
#
# Checks are deliberately narrow: each one has a precise signature and no judgement, so this cannot
# start arguing about prose.

set -uo pipefail

# Every path below is repository-relative, so running from the wrong directory would check the
# wrong files — or none — and report success.
cd "$(dirname "${BASH_SOURCE[0]}")/.." || { printf 'check-docs: cannot find the repository root\n' >&2; exit 1; }

failures=0

fail() {
    printf 'check-docs: %s\n' "$1" >&2
    failures=$((failures + 1))
}

# 1. Every template fragment starts with its marker, and carries it exactly once. If the marker stops
#    being the whole of line one, the assembly script's filter misses it.
for fragment in docs/agents/*.md; do
    if ! head -1 "$fragment" | grep -qE '^<!--LOOM-TEMPLATE .*-->$'; then
        fail "$fragment: line 1 must be the LOOM-TEMPLATE marker and nothing else"
    fi

    occurrences=$(grep -c 'LOOM-TEMPLATE' "$fragment")
    if [[ "$occurrences" -ne 1 ]]; then
        fail "$fragment: the marker appears $occurrences times; expected once, on line 1"
    fi
done

# 2. An assembled file must be free of Loom's internal markers and must start with its heading. This is
#    the check that would have caught the corruption at its most damaging point: in the output a
#    consumer actually receives.
assembled=$(mktemp -d)
trap 'rm -rf "$assembled"' EXIT

if ! scripts/new-agents-md.sh --out "$assembled/AGENTS.md" --name Check api worker cli >/dev/null 2>&1; then
    fail "the assembly script failed"
else
    if grep -q 'LOOM-TEMPLATE' "$assembled/AGENTS.md"; then
        fail "the assembled template leaks a LOOM-TEMPLATE marker"
    fi

    if ! head -1 "$assembled/AGENTS.md" | grep -qxF '# AGENTS.md'; then
        fail "the assembled template does not begin with its heading"
    fi
fi

# 3. The repository's own guidance starts with its heading.
if ! head -1 AGENTS.md | grep -qxF '# AGENTS.md'; then
    fail "AGENTS.md: line 1 must be '# AGENTS.md'"
fi

# 4. Fused-line signatures, across every tracked markdown file. Concatenating a table row onto
#    something else leaves no space, which is what makes these precise rather than heuristic.
while IFS= read -r document; do
    if grep -nE '\|(#|<!--)' "$document" >&2; then
        fail "$document: a table row is fused to a heading or a comment (see above)"
    fi

    # Adjacent pipes are legal Markdown for an empty cell, but this repository writes empty cells
    # with a space — `| |`, never `||` — precisely so that adjacent pipes only ever mean two rows
    # joined into one. This check is what enforces that convention.
    if grep -nE '^\|.*\|\|' "$document" >&2; then
        fail "$document: adjacent pipes in a table row; write an empty cell as '| |', since '||' is reserved as the signature of two joined rows"
    fi
done < <(git ls-files '*.md')

if [[ "$failures" -gt 0 ]]; then
    printf 'check-docs: %s problem(s) found\n' "$failures" >&2
    exit 1
fi

printf 'check-docs: documents are intact\n'
