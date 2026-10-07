#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -gt 1 || (${1:-} != "" && ${1:-} != "--no-sync") ]]; then
    echo "Usage: $0 [--no-sync]" >&2
    exit 2
fi

if [[ ${1:-} != "--no-sync" ]]; then
    "$repo_root/scripts/sync-from-java.sh"
fi
mvn -q -f "$repo_root/codegen/pom.xml" generate-sources
"$repo_root/scripts/normalize-generated.sh"
