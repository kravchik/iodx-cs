#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$repo_root/scripts/generate.sh"
dotnet restore "$repo_root/Iodx.sln"
dotnet build "$repo_root/Iodx.sln" --configuration Release --no-restore
dotnet test "$repo_root/Iodx.sln" --configuration Release --no-build --no-restore
dotnet pack "$repo_root/src/Iodx/Iodx.csproj" \
    --configuration Release \
    --no-build \
    --no-restore \
    --output "$repo_root/artifacts/package"
