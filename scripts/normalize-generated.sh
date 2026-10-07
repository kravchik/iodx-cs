#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
generated="$repo_root/src/Iodx/Generated/CongoCC/cs-Generatedparser"

[[ -f "$generated/Lexer.cs" ]] || { echo "Generated Lexer.cs is missing" >&2; exit 1; }

# The generated standalone project and CLI harness are not part of the library.
rm -rf "$generated/test"
rm -f "$generated/Iodx.Generated.csproj"

# CongoCC emits its runtime as public types; keep it out of the package API.
perl -pi -e 's/^    public ((?:sealed |abstract |static )?(?:class|interface|enum|struct))/    internal $1/' \
    "$generated"/*.cs \
    "$repo_root/src/Iodx/Generated/IodxCst.cs"

# The C# backend conflates source text with a possible filename and returns an
# uninitialized field when line endings are preserved. IODX always passes text.
perl -pi -e 's/var input = InputText\(inputSource\);/var input = inputSource;/' "$generated/Lexer.cs"
perl -pi -e 's/return _content;/return content;/' "$generated/Lexer.cs"

# A static dummy token leaks cached tokens and TokenSource between parser instances.
perl -pi -e 's/LastConsumedToken = Lexer\.DummyStartToken;/LastConsumedToken = Token.NewToken(TokenType.DUMMY, tokenSource, 0, 0);/' "$generated/Parser.cs"

# Generated source comments must not depend on the local checkout location.
escaped_root=$(printf '%s' "$repo_root/grammar/" | sed 's/[.[\*^$()+?{|]/\\&/g')
perl -pi -e "s|$escaped_root|grammar/|g" "$generated/Parser.cs"
