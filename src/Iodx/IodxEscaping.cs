#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Iodx
{
    public sealed class IodxEscapeException : IodxException
    {
        public IodxEscapeException(string message, int offset, int length)
            : base(message)
        {
            Offset = offset;
            Length = Math.Max(1, length);
        }

        public int Offset { get; }
        public int Length { get; }
    }

    public static class IodxEscaping
    {
        private static readonly IReadOnlyDictionary<char, char> Unescapes =
            new Dictionary<char, char>
            {
                ['t'] = '\t',
                ['b'] = '\b',
                ['r'] = '\r',
                ['f'] = '\f',
                ['\\'] = '\\',
                ['n'] = '\n',
                ['s'] = ' ',
                ['\"'] = '\"',
                ['\''] = '\'',
            };

        public static string UnescapeQuoted(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length < 2
                || (value[0] != '\"' && value[0] != '\'')
                || value[value.Length - 1] != value[0])
            {
                throw new IodxEscapeException("Expected a quoted string", 0, value.Length);
            }

            return Unescape(value.Substring(1, value.Length - 2));
        }

        public static string Unescape(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            StringBuilder result = new StringBuilder(value.Length);
            for (int offset = 0; offset < value.Length; offset++)
            {
                char current = value[offset];
                if (current == '\r') continue;
                if (current == '\\')
                {
                    int escapeOffset = offset++;
                    if (offset >= value.Length)
                    {
                        throw new IodxEscapeException("Uncompleted escape sequence", escapeOffset, 1);
                    }

                    char symbol = value[offset];
                    if (symbol == 'u')
                    {
                        char unicode = ParseUnicodeEscape(value, escapeOffset);
                        offset = escapeOffset + 5;
                        if (char.IsHighSurrogate(unicode))
                        {
                            int lowOffset = offset + 1;
                            if (lowOffset + 1 >= value.Length
                                || value[lowOffset] != '\\'
                                || value[lowOffset + 1] != 'u')
                            {
                                throw new IodxEscapeException(
                                    "High surrogate must be followed by a low surrogate escape",
                                    escapeOffset,
                                    6);
                            }

                            char low = ParseUnicodeEscape(value, lowOffset);
                            if (!char.IsLowSurrogate(low))
                            {
                                throw new IodxEscapeException("Expected a low surrogate escape", lowOffset, 6);
                            }

                            result.Append(unicode).Append(low);
                            offset = lowOffset + 5;
                            continue;
                        }

                        if (char.IsLowSurrogate(unicode))
                        {
                            throw new IodxEscapeException("Unexpected low surrogate", escapeOffset, 6);
                        }

                        result.Append(unicode);
                        continue;
                    }

                    char decoded;
                    if (!Unescapes.TryGetValue(symbol, out decoded))
                    {
                        throw new IodxEscapeException("Unknown escape symbol: " + symbol, escapeOffset, 2);
                    }

                    result.Append(decoded);
                    continue;
                }

                if (char.IsHighSurrogate(current))
                {
                    if (offset + 1 >= value.Length || !char.IsLowSurrogate(value[offset + 1]))
                    {
                        throw new IodxEscapeException("Lone high surrogate", offset, 1);
                    }

                    result.Append(current).Append(value[++offset]);
                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    throw new IodxEscapeException("Lone low surrogate", offset, 1);
                }

                result.Append(current);
            }

            return result.ToString();
        }

        public static string EscapeDoubleQuotes(string value)
        {
            return Escape(value, '\"');
        }

        public static string EscapeSingleQuotes(string value)
        {
            return Escape(value, '\'');
        }

        private static string Escape(string value, char quote)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            StringBuilder result = new StringBuilder(value.Length);
            for (int offset = 0; offset < value.Length; offset++)
            {
                char current = value[offset];
                if (char.IsHighSurrogate(current))
                {
                    if (offset + 1 >= value.Length || !char.IsLowSurrogate(value[offset + 1]))
                    {
                        throw new ArgumentException("Lone high surrogate at offset " + offset, nameof(value));
                    }

                    result.Append(current).Append(value[++offset]);
                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    throw new ArgumentException("Lone low surrogate at offset " + offset, nameof(value));
                }

                string? escaped = null;
                switch (current)
                {
                    case '\t': escaped = "t"; break;
                    case '\b': escaped = "b"; break;
                    case '\r': escaped = "r"; break;
                    case '\f': escaped = "f"; break;
                    case '\\': escaped = "\\"; break;
                    default:
                        if (current == quote) escaped = current.ToString();
                        break;
                }

                if (escaped != null) result.Append('\\').Append(escaped);
                else if (current != '\n' && IsIsoControl(current))
                {
                    result.Append("\\u").Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                }
                else result.Append(current);
            }

            return result.ToString();
        }

        private static char ParseUnicodeEscape(string value, int offset)
        {
            int available = value.Length - offset;
            if (available < 6)
            {
                throw new IodxEscapeException("Incomplete Unicode escape", offset, available);
            }

            int result = 0;
            for (int index = offset + 2; index < offset + 6; index++)
            {
                int digit = HexToInt(value[index]);
                if (digit < 0)
                {
                    throw new IodxEscapeException("Invalid hexadecimal digit in Unicode escape", offset, 6);
                }

                result = (result << 4) | digit;
            }

            return (char)result;
        }

        private static int HexToInt(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
        }

        private static bool IsIsoControl(char value)
        {
            return value <= 0x1f || (value >= 0x7f && value <= 0x9f);
        }
    }
}
