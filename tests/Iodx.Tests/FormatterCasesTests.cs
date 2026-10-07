#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Iodx.Tests
{
    public sealed class FormatterCasesTests
    {
        [Fact]
        public void UpstreamCasesAndWhitespaceVariantsUseCanonicalFormatting()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Upstream", "formatting.cases.sql.style.iodx");
            IReadOnlyList<object?> cases = IodxSyntax.ParseAll(File.ReadAllText(path));
            IodxPrinterOptions options = new IodxPrinterOptions();

            foreach (object? item in cases)
            {
                IodxField? setting = item as IodxField;
                if (setting != null)
                {
                    ApplySetting(options, setting);
                    continue;
                }

                string? canonical = item as string;
                if (canonical == null) continue;
                Assert.Equal(canonical, Format(canonical, options));
                Assert.Equal(canonical, Format(ReplaceWhitespace(canonical, " "), options));
                Assert.Equal(canonical, Format(ReplaceWhitespace(canonical, "\n    "), options));
            }
        }

        private static void ApplySetting(IodxPrinterOptions options, IodxField field)
        {
            int value = Assert.IsType<int>(field.Value);
            switch (Assert.IsType<string>(field.Key))
            {
                case "maxWidth": options.MaxWidth = value; break;
                case "maxLocalWidth": options.MaxLocalWidth = value; break;
                case "compactFromLevel": options.CompactFromLevel = value; break;
                default: throw new InvalidOperationException("Unknown formatter setting: " + field.Key);
            }
        }

        private static string Format(string source, IodxPrinterOptions options)
        {
            return "\n" + IodxSyntax.Format(IodxSyntax.Parse(source), options) + "\n";
        }

        private static string ReplaceWhitespace(string source, string replacement)
        {
            StringBuilder result = new StringBuilder(source.Length);
            ScanState state = ScanState.Normal;
            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                if (state == ScanState.SingleQuoted || state == ScanState.DoubleQuoted)
                {
                    result.Append(current);
                    if (current == '\\' && index + 1 < source.Length) result.Append(source[++index]);
                    else if (state == ScanState.SingleQuoted && current == '\'') state = ScanState.Normal;
                    else if (state == ScanState.DoubleQuoted && current == '"') state = ScanState.Normal;
                    continue;
                }

                if (state == ScanState.LineComment)
                {
                    result.Append(current);
                    if (current == '\n' || current == '\r') state = ScanState.Normal;
                    continue;
                }

                if (state == ScanState.BlockComment)
                {
                    result.Append(current);
                    if (current == '*' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        result.Append(source[++index]);
                        state = ScanState.Normal;
                    }
                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    state = current == '\'' ? ScanState.SingleQuoted : ScanState.DoubleQuoted;
                    result.Append(current);
                }
                else if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
                {
                    state = ScanState.LineComment;
                    result.Append(current).Append(source[++index]);
                }
                else if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
                {
                    state = ScanState.BlockComment;
                    result.Append(current).Append(source[++index]);
                }
                else if (char.IsWhiteSpace(current))
                {
                    while (index + 1 < source.Length && char.IsWhiteSpace(source[index + 1])) index++;
                    result.Append(replacement);
                }
                else
                {
                    result.Append(current);
                }
            }

            return result.ToString();
        }

        private enum ScanState
        {
            Normal,
            SingleQuoted,
            DoubleQuoted,
            LineComment,
            BlockComment,
        }
    }
}
