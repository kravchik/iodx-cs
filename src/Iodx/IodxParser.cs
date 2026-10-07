#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using GeneratedCst = Iodx.Generated.IodxCst;
using GeneratedLexer = Iodx.Generated.Lexer;
using GeneratedParseException = Iodx.Generated.ParseException;
using GeneratedParser = Iodx.Generated.Parser;
using GeneratedToken = Iodx.Generated.Token;
using GeneratedTokenType = Iodx.Generated.TokenType;

namespace Iodx
{
    public sealed class IodxToken
    {
        internal IodxToken(GeneratedToken token, string source)
        {
            Kind = IodxKindNames.Token(token.Type.ToString());
            Text = token.ToString();
            Range = CstAdapter.TokenRange(token, source);
        }

        public IodxTokenKind Kind { get; }
        public string Text { get; }
        public SourceRange Range { get; }
    }

    internal static class IodxParser
    {
        public static IodxCst ParseCst(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                GeneratedCst document = new GeneratedParser(source).ParseparseDocument();
                return CstAdapter.Convert(document, source);
            }
            catch (IodxParseException)
            {
                throw;
            }
            catch (GeneratedParseException error)
            {
                List<IodxTokenKind> expected = new List<IodxTokenKind>();
                if (error.Expected != null)
                {
                    foreach (GeneratedTokenType tokenType in error.Expected)
                    {
                        expected.Add(IodxKindNames.Token(tokenType.ToString()));
                    }
                }

                SourceRange? range = error.Token == null ? null : CstAdapter.TokenRange(error.Token, source);
                string message = string.IsNullOrEmpty(error.Message) ? "Invalid IODX syntax" : error.Message;
                throw new IodxParseException(
                    message,
                    range,
                    expected,
                    error,
                    error.Token == null ? null : IodxKindNames.Token(error.Token.Type.ToString()));
            }
        }

        public static IReadOnlyList<IodxToken> Tokenize(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            GeneratedLexer lexer = new GeneratedLexer(source);
            List<IodxToken> result = new List<IodxToken>();
            GeneratedToken? previous = null;
            do
            {
                previous = lexer.GetNextToken(previous);
                result.Add(new IodxToken(previous, source));
            }
            while (previous.Type != GeneratedTokenType.EOF);

            return result.AsReadOnly();
        }
    }

    internal static class CstAdapter
    {
        internal static IodxCst Convert(GeneratedCst source, string sourceText)
        {
            List<IodxCst> children = new List<IodxCst>(source.Children.Count);
            Dictionary<GeneratedCst, IodxCst> convertedBySource =
                new Dictionary<GeneratedCst, IodxCst>();
            foreach (GeneratedCst child in source.Children)
            {
                IodxCst converted = Convert(child, sourceText);
                children.Add(converted);
                convertedBySource[child] = converted;
            }

            Dictionary<string, IodxCst> fields = new Dictionary<string, IodxCst>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, GeneratedCst> field in source.Fields)
            {
                IodxCst converted;
                if (!convertedBySource.TryGetValue(field.Value, out converted!))
                {
                    converted = Convert(field.Value, sourceText);
                }

                fields[field.Key] = converted;
            }

            SourceRange range = RangeFromOffsets(sourceText, source.BeginOffset, source.EndOffset);
            object? value = ConvertValue(source, sourceText, range);
            return new IodxCst(IodxKindNames.Cst(source.Type), range, value, children, fields);
        }

        internal static SourceRange TokenRange(GeneratedToken token, string source)
        {
            return RangeFromOffsets(source, token.BeginOffset, token.EndOffset);
        }

        private static object? ConvertValue(GeneratedCst source, string sourceText, SourceRange range)
        {
            string text = source.Value as string ?? string.Empty;
            switch (source.Type)
            {
                case "COMMENT_SINGLE_LINE":
                    return text.Length >= 2 ? text.Substring(2) : string.Empty;
                case "COMMENT_MULTI_LINE":
                    return text.Length >= 4 ? text.Substring(2, text.Length - 4) : string.Empty;
                case "INTEGER_LITERAL":
                    return ParseInteger(text, range);
                case "FLOATING_POINT_LITERAL":
                    return ParseFloatingPoint(text, range);
                case "STRING_LITERAL_DQ":
                case "STRING_LITERAL_SQ":
                    return ParseString(text, sourceText, range);
                case "ANY_LITERAL":
                    if (text == "true") return true;
                    if (text == "false") return false;
                    if (text == "null") return null;
                    return text;
                default:
                    return source.Value;
            }
        }

        private static object ParseInteger(string literal, SourceRange range)
        {
            bool isLong = literal.EndsWith("l", StringComparison.OrdinalIgnoreCase);
            string number = isLong ? literal.Substring(0, literal.Length - 1) : literal;
            bool negative = number.StartsWith("-", StringComparison.Ordinal);
            string unsigned = negative ? number.Substring(1) : number;
            bool hexadecimal = unsigned.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string digits = hexadecimal ? unsigned.Substring(2) : unsigned;

            if (hexadecimal)
            {
                ulong magnitude;
                if (digits.Length == 0
                    || !ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out magnitude))
                {
                    throw IntegerError(literal, range);
                }

                if (isLong)
                {
                    ulong maxMagnitude = negative ? 0x8000000000000000UL : 0x7fffffffffffffffUL;
                    if (magnitude > maxMagnitude) throw IntegerError(literal, range);
                    if (negative && magnitude == 0x8000000000000000UL) return long.MinValue;
                    return negative ? -(long)magnitude : (long)magnitude;
                }

                ulong intMaxMagnitude = negative ? 0x80000000UL : 0x7fffffffUL;
                if (magnitude > intMaxMagnitude) throw IntegerError(literal, range);
                if (negative && magnitude == 0x80000000UL) return int.MinValue;
                return negative ? -(int)magnitude : (int)magnitude;
            }

            if (isLong)
            {
                long value;
                if (!long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                {
                    throw IntegerError(literal, range);
                }

                return value;
            }

            int intValue;
            if (!int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out intValue))
            {
                throw IntegerError(literal, range);
            }

            return intValue;
        }

        private static object ParseFloatingPoint(string literal, SourceRange range)
        {
            char suffix = char.ToLowerInvariant(literal[literal.Length - 1]);
            bool hasSuffix = suffix == 'f' || suffix == 'd';
            string number = hasSuffix ? literal.Substring(0, literal.Length - 1) : literal;
            if (suffix == 'd')
            {
                double value;
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw new IodxParseException(
                        "Invalid floating-point literal: " + literal,
                        range,
                        null,
                        null,
                        IodxTokenKind.FloatingPointLiteral);
                }

                return value;
            }

            float floatValue;
            if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
            {
                throw new IodxParseException(
                    "Invalid floating-point literal: " + literal,
                    range,
                    null,
                    null,
                    IodxTokenKind.FloatingPointLiteral);
            }

            return floatValue;
        }

        private static string ParseString(string literal, string source, SourceRange tokenRange)
        {
            try
            {
                return IodxEscaping.UnescapeQuoted(literal);
            }
            catch (IodxEscapeException error)
            {
                int begin = tokenRange.BeginOffset + 1 + error.Offset;
                int contentEnd = tokenRange.EndOffset - 1;
                int end = Math.Min(contentEnd, begin + error.Length);
                SourceRange range = RangeFromOffsets(source, begin, Math.Max(begin + 1, end));
                throw new IodxParseException(
                    error.Message + " at offset " + begin,
                    range,
                    null,
                    error,
                    IodxTokenKind.Invalid);
            }
        }

        private static IodxParseException IntegerError(string literal, SourceRange range)
        {
            return new IodxParseException(
                "Integer literal out of range: " + literal,
                range,
                null,
                null,
                IodxTokenKind.IntegerLiteral);
        }

        private static SourceRange RangeFromOffsets(string source, int begin, int end)
        {
            int beginLine;
            int beginColumn;
            LocationAt(source, begin, out beginLine, out beginColumn);
            int endLine;
            int endColumn;
            int endLocation = end > begin
                ? end - 1
                : begin == source.Length && begin > 0 ? begin - 1 : begin;
            LocationAt(source, endLocation, out endLine, out endColumn);
            return new SourceRange(beginLine, beginColumn, endLine, endColumn, begin, end);
        }

        private static void LocationAt(string source, int offset, out int line, out int column)
        {
            if (offset > 0
                && offset < source.Length
                && char.IsLowSurrogate(source[offset])
                && char.IsHighSurrogate(source[offset - 1])) offset--;

            line = 1;
            int limit = Math.Min(Math.Max(offset, 0), source.Length);
            for (int index = 0; index < limit; index++)
            {
                if (source[index] == '\n') line++;
            }

            if (offset >= source.Length)
            {
                column = 1;
                return;
            }

            int lineStart = offset <= 0 ? 0 : source.LastIndexOf('\n', offset - 1) + 1;
            column = 1;
            for (int index = lineStart; index < offset; index++)
            {
                if (char.IsHighSurrogate(source[index])
                    && index + 1 < offset
                    && char.IsLowSurrogate(source[index + 1])) index++;
                column++;
            }
        }
    }
}
