#!/usr/bin/env bash
# Assembles an AGENTS.md for a consumer project from the shared core plus one or more
# archetype deltas. A consumer project must end up with a single self-contained AGENTS.md —
# agents do not reliably follow links to rules stored elsewhere.
#
# Usage:
#   scripts/new-agents-md.sh --out ../MyApp/AGENTS.md --name MyApp api
#   scripts/new-agents-md.sh --out ../MyApp/AGENTS.md --name Billing api worker
#
# Archetypes: api, worker, cli (any combination; one solution may contain several).

set -euo pipefail

TEMPLATE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../docs/agents" && pwd)"
OUT=""
NAME=""
FORCE=0
ARCHETYPES=()

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

usage() {
    sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --out)   OUT="${2:-}"; shift 2 ;;
        --name)  NAME="${2:-}"; shift 2 ;;
        --force) FORCE=1; shift ;;
        -h|--help) usage 0 ;;
        api|worker|cli) ARCHETYPES+=("$1"); shift ;;
        *) die "unknown argument '$1' (try --help)" ;;
    esac
done

[[ -n "$OUT" ]] || die "--out is required"
[[ ${#ARCHETYPES[@]} -gt 0 ]] || die "name at least one archetype: api, worker, cli"

# The same shapes the template engine accepts. Refused here rather than passed to the substitution,
# where a stray character would corrupt the output silently instead of failing loudly.
if [[ -n "$NAME" && ! "$NAME" =~ ^[A-Za-z0-9._-]+$ ]]; then
    die "--name may contain only letters, digits, dots, underscores and dashes"
fi

if [[ -e "$OUT" && $FORCE -ne 1 ]]; then
    die "$OUT already exists; pass --force to overwrite"
fi

SOURCES=("$TEMPLATE_DIR/00-core.md")
for archetype in "${ARCHETYPES[@]}"; do
    fragment="$TEMPLATE_DIR/10-$archetype.md"
    [[ -f "$fragment" ]] || die "missing template fragment: $fragment"
    SOURCES+=("$fragment")
done

mkdir -p "$(dirname "$OUT")"

# Strip the LOOM-TEMPLATE marker comments — they orient a reader inside the Loom repo and
# are meaningless once the file has been assembled into a project.
cat "${SOURCES[@]}" | grep -v '^<!--LOOM-TEMPLATE' > "$OUT"

# Bash's own substitution rather than sed: the replacement is taken literally, so no character in a
# name can be misread as syntax, and there is no GNU/BSD -i split to trip over.
if [[ -n "$NAME" ]]; then
    content="$(cat "$OUT")"
    printf '%s\n' "${content//MyApp/$NAME}" > "$OUT"
fi

printf 'wrote %s (%s lines) from: %s\n' \
    "$OUT" "$(wc -l < "$OUT" | tr -d ' ')" "core ${ARCHETYPES[*]}"

LINES=$(wc -l < "$OUT" | tr -d ' ')
if [[ "$LINES" -gt 400 ]]; then
    printf 'warning: %s lines exceeds the 400-line budget; move rules into .editorconfig, an analyzer, or an architecture test\n' \
        "$LINES" >&2
fi
