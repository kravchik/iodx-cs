#!/usr/bin/env bash

set -euo pipefail
shopt -s nullglob

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_root="${IODX_SOURCE_ROOT:-"$repo_root/../iodx"}"
source_grammar="$source_root/src/main/congocc/common"
source_fixtures="$source_root/src/test/resources"
target_grammar="$repo_root/grammar/common"
target_fixtures="$repo_root/tests/Iodx.Tests/Resources/Upstream"
grammar_files=(IodxTokens.inc.ccc IodxProductions.inc.ccc)

if [[ $# -gt 1 || (${1:-} != "" && ${1:-} != "--check") ]]; then
    echo "Usage: $0 [--check]" >&2
    exit 2
fi

for name in "${grammar_files[@]}"; do
    [[ -f "$source_grammar/$name" ]] || { echo "Missing upstream grammar: $name" >&2; exit 1; }
done

fixture_files=("$source_fixtures"/*.iodx)
[[ ${#fixture_files[@]} -gt 0 ]] || { echo "No upstream fixtures found" >&2; exit 1; }

if [[ ${1:-} == "--check" ]]; then
    stale=0
    for name in "${grammar_files[@]}"; do
        cmp -s "$source_grammar/$name" "$target_grammar/$name" || { echo "Out of sync: $name" >&2; stale=1; }
    done
    for source in "${fixture_files[@]}"; do
        name="$(basename "$source")"
        cmp -s "$source" "$target_fixtures/$name" || { echo "Out of sync: $name" >&2; stale=1; }
    done
    for target in "$target_fixtures"/*.iodx; do
        name="$(basename "$target")"
        [[ -f "$source_fixtures/$name" ]] || { echo "Stale fixture: $name" >&2; stale=1; }
    done
    exit "$stale"
fi

mkdir -p "$target_grammar" "$target_fixtures"
for name in "${grammar_files[@]}"; do cp "$source_grammar/$name" "$target_grammar/$name"; done
for target in "$target_fixtures"/*.iodx; do
    name="$(basename "$target")"
    [[ -f "$source_fixtures/$name" ]] || rm -f "$target"
done
for source in "${fixture_files[@]}"; do cp "$source" "$target_fixtures/"; done

echo "Synced grammar and ${#fixture_files[@]} fixtures from $source_root."
