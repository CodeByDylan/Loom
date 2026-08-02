#!/usr/bin/env bash
# Confirms the produced packages carry the version the tag asked for — in their names, and inside the
# templates package, where the version a scaffolded solution restores is written into the payload.
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

# The templates package carries a version no file name reveals. Pack rewrites <LoomVersion> in each
# archetype's Directory.Packages.props to the version being released, and that pin is what a
# scaffolded solution restores — so the package can be named correctly and still be unusable. It went
# wrong exactly that way: 0.2.1, 0.3.0 and 0.3.1 all shipped pinned to 1.0.0, a version that has never
# existed, because MinVer had not yet run when the rewrite read $(PackageVersion). Every `dotnet new
# loom-api` from those packages fails to restore, and nothing here noticed, because the names were right.
templates=("$directory"/CodeByDylan.Loom.Templates.*.nupkg)

if [[ ${#templates[@]} -ne 1 ]]; then
    printf 'check-release: expected one templates package in %s, found %s\n' "$directory" "${#templates[@]}" >&2
    exit 1
fi

templates_package="${templates[0]}"

# Required rather than worked around. Reading the pin is the whole point of this section, and a check
# that quietly skips when a tool is missing is worse than one that was never written.
if ! command -v unzip >/dev/null 2>&1; then
    printf 'check-release: unzip is needed to read the pinned version out of %s\n' "$(basename "$templates_package")" >&2
    exit 1
fi

pinned=$(unzip -Z1 "$templates_package" 'content/templates/*/Directory.Packages.props' 2>/dev/null)

if [[ -z "$pinned" ]]; then
    printf 'check-release: %s carries no template Directory.Packages.props to pin\n' "$(basename "$templates_package")" >&2
    exit 1
fi

archetypes=0

while IFS= read -r member; do
    archetypes=$((archetypes + 1))

    # One element, read as a whole rather than by line, so a value split across lines cannot pass.
    version=$(unzip -p "$templates_package" "$member" | tr -d '\n' | grep -oE '<LoomVersion>[^<]*</LoomVersion>')
    version=${version#<LoomVersion>}
    version=${version%</LoomVersion>}

    if [[ -z "$version" ]]; then
        printf 'check-release: %s names no <LoomVersion>\n' "$member" >&2
        failures=$((failures + 1))
        continue
    fi

    if [[ "$version" != "$expected" ]]; then
        printf "check-release: %s pins Loom '%s' but the tag asks for '%s'\n" "$member" "$version" "$expected" >&2
        failures=$((failures + 1))
    fi
done <<< "$pinned"

if [[ "$failures" -gt 0 ]]; then
    printf 'check-release: %s check(s) failed against the tag; nothing was published\n' "$failures" >&2
    exit 1
fi

printf 'check-release: all %s package(s), symbols included, are version %s\n' "${#packages[@]}" "$expected"
printf 'check-release: the templates package pins all %s archetype(s) at %s\n' "$archetypes" "$expected"
