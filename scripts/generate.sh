#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -ne 0 ]]; then
    echo "Usage: $0" >&2
    exit 2
fi

mvn -q -f "$repo_root/codegen/pom.xml" generate-sources
"$repo_root/scripts/normalize-generated.sh"
