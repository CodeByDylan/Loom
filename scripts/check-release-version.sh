#!/usr/bin/env bash
# Confirms the produced packages carry the version the tag asked for.
#
# MinVer is configured rather than magical. A wrong tag prefix, a shallow clone, or a missing tag all
# yield a development version like 0.0.0-alpha.0.17 while everything else looks fine — and pushing
# that to a public feed burns the version number permanently, because a published version cannot be
# replaced. So the packages are checked against the tag before any of them leave the machine.
#
# Usage: check-release-version.sh <tag> <directory>

set -uo pipefail

tag=${1:-}
directory=${2:-}

if [[ -z "$tag" || -z "$directory" ]]; then
    printf 'usage: check-release-version.sh <tag> <directory>\n' >&2
    exit 1
fi

if [[ ! -d "$directory" ]]; then
    printf 'check-release: no such directory: %s\n' "$directory" >&2
    exit 1
fi

# The tag prefix is 'v', matching MinVerTagPrefix in Directory.Build.props.
expected=${tag#v}

if [[ "$expected" == "$tag" ]]; then
    printf "check-release: tag '%s' does not begin with 'v', which is what MinVer is configured to strip\n" "$tag" >&2
    exit 1
fi

shopt -s nullglob
# Symbol packages carry the same version and are pushed alongside their package, so a mismatched one
# would be published just as permanently. They are checked too, not assumed to follow.
packages=("$directory"/*.nupkg "$directory"/*.snupkg)

if [[ ${#packages[@]} -eq 0 ]]; then
    printf 'check-release: no packages found in %s\n' "$directory" >&2
    exit 1
fi

failures=0

for package in "${packages[@]}"; do
    name=$(basename "${package%.*}")

    # Loom.Results.AspNetCore.0.1.0 -> 0.1.0. The identifier itself contains dots, so the version is
    # taken as the tail beginning at the first digit-led segment.
    version=$(printf '%s\n' "$name" | sed -E 's/^([A-Za-z][A-Za-z.]*[A-Za-z])\.([0-9].*)$/\2/')

    if [[ "$version" == "$name" ]]; then
        printf 'check-release: cannot read a version from %s\n' "$name" >&2
        failures=$((failures + 1))
        continue
    fi

    if [[ "$version" != "$expected" ]]; then
        printf "check-release: %s is version '%s' but the tag asks for '%s'\n" "$name" "$version" "$expected" >&2
        failures=$((failures + 1))
    fi
done

if [[ "$failures" -gt 0 ]]; then
    printf 'check-release: %s package(s) do not match the tag; nothing was published\n' "$failures" >&2
    exit 1
fi

printf 'check-release: all %s package(s), symbols included, are version %s\n' "${#packages[@]}" "$expected"
