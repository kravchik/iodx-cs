#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
before="$(mktemp)"
after="$(mktemp)"
trap 'rm -f "$before" "$after"' EXIT

manifest() {
    find \
        "$repo_root/grammar/common" \
        "$repo_root/src/Iodx/Generated" \
        "$repo_root/tests/Iodx.Tests/Resources/Upstream" \
        -type f -print0 \
        | sort -z \
        | xargs -0 shasum
}

manifest > "$before"
"$repo_root/scripts/generate.sh" --no-sync
manifest > "$after"

if ! diff -u "$before" "$after"; then
    echo "Generated parser is stale or generation is not reproducible." >&2
    exit 1
fi

echo "Generated parser is reproducible."
