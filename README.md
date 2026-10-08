# IODX for C#

C# implementation of [IODX](https://iodx.org/), a compact, human-readable
syntax for structured data, configs, fixtures, serialization, and data exchange.

This implementation is under development. The language-independent grammar lives
in the main [IODX repository](https://github.com/kravchik/iodx).

## Usage

Parse text into the syntax model with `IodxSyntax.Parse`, or use `ParseCst` when
the concrete syntax tree and exact source ranges are needed:

```csharp
using Iodx;

IodxEntity scroll = (IodxEntity)IodxSyntax.Parse(
    "SpellScroll(title = \"Whisper\" charges = 3)")!;

string title = scroll.GetField<string>("title")!;
int charges = scroll.GetField<int>("charges");
string text = IodxSyntax.Format(scroll);
```

Use `TryGetField` to distinguish a missing field from a present field whose value
is `null`. `WithField`, `ReplaceField`, and `WithoutField` return modified copies;
syntax model instances and their exposed collections are immutable.

`ParseAll` and `FormatAll` handle multiple top-level values. Literal values
use native C# types: IODX integers are `int` or `long`, floating-point values are
`float` or `double`, and booleans, strings, and null are `bool`, `string`, and
`null`. Entities, fields, comments, and maps use `IodxEntity`, `IodxField`,
`IodxComment`, and `IodxMap`.

Tokens and CST nodes expose stable `IodxTokenKind` and `IodxCstKind` enums.
`SourceRange` uses UTF-16 offsets with an exclusive `EndOffset`; line and column
coordinates identify the first and last covered code points.

## Development

Build and test the solution with:

```shell
dotnet test Iodx.sln
```

Generated C# is committed, so normal builds, tests, package installation, and
runtime usage do not require Java, Maven, CongoCC, or the Java repository.

## Parser generation

The common grammar and `.iodx` fixtures are copied into this repository and
committed. Synchronization is intentionally a separate workspace maintenance
step, so parser generation only reads the local grammar. Regenerate and verify
the parser with:

```shell
scripts/generate.sh
scripts/check-generated.sh
```

Parser generation requires Java and Maven; CongoCC is pinned in
`codegen/pom.xml`. Handwritten code is under `src/Iodx`, while generated code is
under `src/Iodx/Generated` and must not be edited manually.

Run the complete local verification, including NuGet packing, with:

```shell
scripts/check.sh
scripts/package-smoke.sh
```

## License

[MIT](LICENSE)
