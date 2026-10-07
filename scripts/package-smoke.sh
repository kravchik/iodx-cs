#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
packages="$repo_root/artifacts/package-smoke"
consumer="$(mktemp -d)"
trap 'rm -rf "$consumer"' EXIT

rm -rf "$packages"
dotnet pack "$repo_root/src/Iodx/Iodx.csproj" \
    --configuration Release \
    --output "$packages"

package="$packages/Iodx.0.1.0-alpha.1.nupkg"
if unzip -Z1 "$package" | grep -Eq '(^|/)(codegen|grammar|tests?|Generated)/|\.(ccc|java)$'; then
    echo "Development-only files leaked into the runtime package." >&2
    exit 1
fi

dotnet new console --output "$consumer" --no-restore >/dev/null
export NUGET_PACKAGES="$consumer/.nuget/packages"
dotnet add "$consumer" package Iodx \
    --version 0.1.0-alpha.1 \
    --source "$packages" >/dev/null
printf '%s\n' \
    'using System;' \
    'using Iodx;' \
    '' \
    'IodxEntity value = (IodxEntity)IodxSyntax.Parse("SpellScroll(charges = 3)")!;' \
    'if (value.GetField<int>("charges") != 3) throw new Exception("Parse smoke failed");' \
    'if (IodxSyntax.Format(value) != "SpellScroll(charges = 3)") throw new Exception("Format smoke failed");' \
    'Console.WriteLine("NuGet package smoke test passed.");' \
    > "$consumer/Program.cs"
dotnet run --project "$consumer" --no-restore
