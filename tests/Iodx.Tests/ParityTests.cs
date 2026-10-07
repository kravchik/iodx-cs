#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Iodx.Tests
{
    public sealed class ParityTests
    {
        private static readonly string Resources = Path.Combine(AppContext.BaseDirectory, "Resources");

        [Fact]
        public void TokenStreamsAndCstMatchJavaOracle()
        {
            using JsonDocument corpus = ReadJson(Path.Combine(Resources, "Parity", "corpus.json"));
            using JsonDocument oracle = ReadJson(Path.Combine(Resources, "Parity", "java-oracle.json"));
            JsonElement cases = oracle.RootElement.GetProperty("cases");

            foreach (JsonElement entry in corpus.RootElement.EnumerateArray())
            {
                string id = entry.GetProperty("id").GetString()!;
                string source = SourceFor(entry);
                JsonElement expected = cases.GetProperty(id);

                AssertJsonEqual(expected.GetProperty("tokens"), ToJson(NormalizeTokens(source)), id + ": tokens");
                AssertJsonEqual(expected.GetProperty("parse"), ToJson(NormalizeParse(source)), id + ": parse");
            }
        }

        private static object NormalizeTokens(string source)
        {
            return IodxSyntax.Tokenize(source).Select(token => new Dictionary<string, object?>
            {
                ["type"] = TokenKindName(token.Kind),
                ["text"] = token.Text,
                ["beginOffset"] = token.Range.BeginOffset,
                ["endOffset"] = token.Range.EndOffset,
                ["beginLine"] = token.Range.BeginLine,
                ["beginColumn"] = token.Range.BeginColumn,
                ["endLine"] = token.Range.EndLine,
                ["endColumn"] = token.Range.EndColumn,
            }).ToList();
        }

        private static object NormalizeParse(string source)
        {
            try
            {
                return new Dictionary<string, object?>
                {
                    ["status"] = "ok",
                    ["cst"] = NormalizeCst(IodxSyntax.ParseCst(source)),
                };
            }
            catch (IodxParseException error)
            {
                return new Dictionary<string, object?>
                {
                    ["status"] = "error",
                    ["token"] = NormalizeError(error),
                };
            }
        }

        private static object NormalizeCst(IodxCst node)
        {
            SortedDictionary<string, int> fields = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IodxCst> field in node.Fields)
            {
                fields[field.Key] = IndexOfReference(node.Children, field.Value);
            }

            return new Dictionary<string, object?>
            {
                ["type"] = CstKindName(node.Kind),
                ["caret"] = node.Range == null ? null : NormalizeRange(node.Range),
                ["value"] = NormalizeValue(node.Value),
                ["children"] = node.Children.Select(NormalizeCst).ToList(),
                ["fields"] = fields,
            };
        }

        private static object NormalizeValue(object? value)
        {
            if (value == null) return new Dictionary<string, object?> { ["kind"] = "null" };
            if (value is int) return KindValue("int32", value);
            if (value is long) return KindValue("int64", ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (value is float) return KindValue("float32", value);
            if (value is double) return KindValue("float64", value);
            if (value is bool) return KindValue("boolean", value);
            if (value is string) return KindValue("string", value);
            throw new InvalidOperationException("Unsupported CST value type: " + value.GetType());
        }

        private static object? NormalizeError(IodxParseException error)
        {
            if (error.Range == null) return null;
            Dictionary<string, object?> token = NormalizeRange(error.Range);
            token["type"] = error.TokenKind == null ? null : TokenKindName(error.TokenKind.Value);
            return token;
        }

        private static Dictionary<string, object?> NormalizeRange(SourceRange range)
        {
            return new Dictionary<string, object?>
            {
                ["beginLine"] = range.BeginLine,
                ["beginColumn"] = range.BeginColumn,
                ["endLine"] = range.EndLine,
                ["endColumn"] = range.EndColumn,
                ["beginOffset"] = range.BeginOffset,
                ["endOffset"] = range.EndOffset,
            };
        }

        private static string TokenKindName(IodxTokenKind kind)
        {
            switch (kind)
            {
                case IodxTokenKind.EndOfFile: return "EOF";
                case IodxTokenKind.LeftParenthesis: return "LEFT_PAREN";
                case IodxTokenKind.RightParenthesis: return "RIGHT_PAREN";
                case IodxTokenKind.Whitespace: return "WHITE_SPACE";
                case IodxTokenKind.SingleLineComment: return "COMMENT_SINGLE_LINE";
                case IodxTokenKind.MultiLineComment: return "COMMENT_MULTI_LINE";
                case IodxTokenKind.IntegerLiteral: return "INTEGER_LITERAL";
                case IodxTokenKind.InvalidLeadingZeroInteger: return "INVALID_LEADING_ZERO_INTEGER";
                case IodxTokenKind.InvalidHexInteger: return "INVALID_HEX_INTEGER";
                case IodxTokenKind.FloatingPointLiteral: return "FLOATING_POINT_LITERAL";
                case IodxTokenKind.Literal: return "ANY_LITERAL";
                case IodxTokenKind.Operator: return "ANY_OPERATOR";
                case IodxTokenKind.Separator: return "ANY_SEPARATOR";
                case IodxTokenKind.DoubleQuotedString: return "STRING_LITERAL_DQ";
                case IodxTokenKind.SingleQuotedString: return "STRING_LITERAL_SQ";
                case IodxTokenKind.Dummy: return "DUMMY";
                case IodxTokenKind.Invalid: return "INVALID";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static string CstKindName(IodxCstKind kind)
        {
            switch (kind)
            {
                case IodxCstKind.ListBody: return "LIST_BODY";
                case IodxCstKind.NamedClass: return "NAMED_CLASS";
                case IodxCstKind.UnnamedClass: return "UNNAMED_CLASS";
                case IodxCstKind.LeftParenthesis: return "LEFT_PAREN";
                case IodxCstKind.RightParenthesis: return "RIGHT_PAREN";
                case IodxCstKind.Whitespace: return "WHITE_SPACE";
                case IodxCstKind.SingleLineComment: return "COMMENT_SINGLE_LINE";
                case IodxCstKind.MultiLineComment: return "COMMENT_MULTI_LINE";
                case IodxCstKind.IntegerLiteral: return "INTEGER_LITERAL";
                case IodxCstKind.FloatingPointLiteral: return "FLOATING_POINT_LITERAL";
                case IodxCstKind.Literal: return "ANY_LITERAL";
                case IodxCstKind.Operator: return "ANY_OPERATOR";
                case IodxCstKind.Separator: return "ANY_SEPARATOR";
                case IodxCstKind.DoubleQuotedString: return "STRING_LITERAL_DQ";
                case IodxCstKind.SingleQuotedString: return "STRING_LITERAL_SQ";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static Dictionary<string, object?> KindValue(string kind, object value)
        {
            return new Dictionary<string, object?> { ["kind"] = kind, ["value"] = value };
        }

        private static int IndexOfReference(IReadOnlyList<IodxCst> children, IodxCst value)
        {
            for (int index = 0; index < children.Count; index++)
            {
                if (ReferenceEquals(children[index], value)) return index;
            }

            throw new InvalidOperationException("A named CST field does not reference one of its node's children");
        }

        private static string SourceFor(JsonElement entry)
        {
            JsonElement source;
            if (entry.TryGetProperty("source", out source)) return source.GetString()!;
            string fixture = entry.GetProperty("fixture").GetString()!;
            return File.ReadAllText(Path.Combine(Resources, "Upstream", fixture));
        }

        private static JsonDocument ReadJson(string path)
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }

        private static JsonElement ToJson(object value)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
        }

        private static void AssertJsonEqual(JsonElement expected, JsonElement actual, string path)
        {
            Assert.True(expected.ValueKind == actual.ValueKind, path + ": expected " + expected.ValueKind + ", got " + actual.ValueKind);
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    JsonProperty[] expectedProperties = expected.EnumerateObject().ToArray();
                    JsonProperty[] actualProperties = actual.EnumerateObject().ToArray();
                    Assert.True(expectedProperties.Length == actualProperties.Length, path + ": object property count differs");
                    foreach (JsonProperty property in expectedProperties)
                    {
                        JsonElement actualProperty;
                        Assert.True(actual.TryGetProperty(property.Name, out actualProperty), path + ": missing property " + property.Name);
                        AssertJsonEqual(property.Value, actualProperty, path + "." + property.Name);
                    }
                    break;
                case JsonValueKind.Array:
                    JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                    JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                    Assert.True(expectedItems.Length == actualItems.Length, path + ": array length differs");
                    for (int index = 0; index < expectedItems.Length; index++)
                    {
                        AssertJsonEqual(expectedItems[index], actualItems[index], path + "[" + index + "]");
                    }
                    break;
                case JsonValueKind.Number:
                    Assert.True(expected.GetDouble().Equals(actual.GetDouble()), path + ": expected " + expected + ", got " + actual);
                    break;
                case JsonValueKind.String:
                    Assert.Equal(expected.GetString(), actual.GetString());
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    throw new InvalidOperationException("Unsupported JSON kind at " + path + ": " + expected.ValueKind);
            }
        }
    }
}
