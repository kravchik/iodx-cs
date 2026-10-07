#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Iodx
{
    public sealed class IodxPrinterOptions
    {
        public int MaxWidth { get; set; } = 100;
        public int MaxLocalWidth { get; set; } = int.MaxValue;
        public int CompactFromLevel { get; set; }
        public string Tab { get; set; } = "  ";
    }

    public sealed class IodxPrinter
    {
        private readonly IodxPrinterOptions options;
        private int level;

        public IodxPrinter(IodxPrinterOptions? options = null)
        {
            IodxPrinterOptions source = options ?? new IodxPrinterOptions();
            this.options = new IodxPrinterOptions
            {
                MaxWidth = source.MaxWidth,
                MaxLocalWidth = source.MaxLocalWidth,
                CompactFromLevel = source.CompactFromLevel,
                Tab = source.Tab,
            };
            if (this.options.MaxWidth < 1) throw new ArgumentOutOfRangeException(nameof(options));
            if (this.options.MaxLocalWidth < 1) throw new ArgumentOutOfRangeException(nameof(options));
            if (this.options.CompactFromLevel < 0) throw new ArgumentOutOfRangeException(nameof(options));
            if (this.options.Tab == null) throw new ArgumentException("Tab cannot be null", nameof(options));
        }

        public string Render(object? value)
        {
            return string.Join("\n", PrintValue(0, value));
        }

        public string RenderAll(IEnumerable<object?> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return string.Join("\n", PrintList(ToList(values), 0, null, null, false));
        }

        public bool CanRenderWithoutQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value == "true"
                || value == "false"
                || value == "null"
                || value == "=") return false;
            try
            {
                IodxCst document = IodxParser.ParseCst(value);
                if (document.Children.Count != 1) return false;
                IodxCstKind kind = document.Children[0].Kind;
                return kind == IodxCstKind.Literal
                    || kind == IodxCstKind.Operator
                    || kind == IodxCstKind.Separator;
            }
            catch (IodxException)
            {
                return false;
            }
        }

        public string ValueToString(object? value)
        {
            if (value == null) return "null";
            if (value is bool) return (bool)value ? "true" : "false";
            string? text = value as string;
            if (text != null)
            {
                if (CanRenderWithoutQuotes(text)) return text;
                return text.IndexOf('\'') >= 0
                    ? "\"" + IodxEscaping.EscapeDoubleQuotes(text) + "\""
                    : "'" + IodxEscaping.EscapeSingleQuotes(text) + "'";
            }

            if (value is int) return ((int)value).ToString(CultureInfo.InvariantCulture);
            if (value is long) return ((long)value).ToString(CultureInfo.InvariantCulture) + "l";
            if (value is float) return FloatText((float)value, true) + "f";
            if (value is double) return DoubleText((double)value, true) + "d";
            throw Unsupported(value);
        }

        private List<string> PrintValue(int startAt, object? value)
        {
            IodxCst? cst = value as IodxCst;
            if (cst != null) return PrintCst(startAt, cst);
            IodxField? field = value as IodxField;
            if (field != null)
            {
                List<string> result = PrintValue(startAt, field.Key);
                List<string> fieldValue = PrintValue(startAt + options.Tab.Length, field.Value);
                if (result.Count == 0 || fieldValue.Count == 0)
                {
                    throw new InvalidOperationException("An IODX field produced no output");
                }

                result[result.Count - 1] += " = " + fieldValue[0];
                for (int index = 1; index < fieldValue.Count; index++) result.Add(fieldValue[index]);
                return result;
            }

            IodxComment? comment = value as IodxComment;
            if (comment != null)
            {
                return One(comment.SingleLine ? "//" + comment.Text : "/*" + comment.Text + "*/");
            }

            IodxEntity? entity = value as IodxEntity;
            if (entity != null)
            {
                return PrintList(
                    entity.Children,
                    startAt,
                    entity.Name == null ? "(" : entity.Name + "(",
                    ")",
                    true);
            }

            IodxMap? map = value as IodxMap;
            if (map != null)
            {
                if (map.Count == 0) return One("(=)");
                List<object?> fields = new List<object?>();
                foreach (IodxField item in map) fields.Add(item);
                return PrintList(fields, startAt, "(", ")", true);
            }

            IDictionary? dictionary = value as IDictionary;
            if (dictionary != null)
            {
                if (dictionary.Count == 0) return One("(=)");
                List<object?> fields = new List<object?>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    fields.Add(new IodxField(entry.Key, entry.Value));
                }

                return PrintList(fields, startAt, "(", ")", true);
            }

            if (!(value is string))
            {
                IEnumerable? sequence = value as IEnumerable;
                if (sequence != null)
                {
                    List<object?> values = new List<object?>();
                    foreach (object? item in sequence) values.Add(item);
                    return PrintList(values, startAt, "(", ")", true);
                }
            }

            return One(ValueToString(value));
        }

        private List<string> PrintCst(int startAt, IodxCst node)
        {
            if (node.Kind == IodxCstKind.ListBody) return PrintList(node.Children, startAt, null, null, false);
            if (node.Kind == IodxCstKind.NamedClass || node.Kind == IodxCstKind.UnnamedClass)
            {
                IodxCst body;
                if (!node.Fields.TryGetValue("body", out body!))
                {
                    throw new InvalidOperationException("Missing CST field 'body' on " + node.Kind);
                }

                string name = string.Empty;
                if (node.Kind == IodxCstKind.NamedClass)
                {
                    IodxCst nameNode;
                    if (!node.Fields.TryGetValue("name", out nameNode!))
                    {
                        throw new InvalidOperationException("Missing CST field 'name' on " + node.Kind);
                    }

                    name = LiteralText(nameNode.Value);
                }

                return PrintList(body.Children, startAt, name + "(", ")", true);
            }

            switch (node.Kind)
            {
                case IodxCstKind.SingleLineComment: return One("//" + Convert.ToString(node.Value));
                case IodxCstKind.MultiLineComment: return One("/*" + Convert.ToString(node.Value) + "*/");
                case IodxCstKind.IntegerLiteral: return One(IntegerCstText(node.Value));
                case IodxCstKind.FloatingPointLiteral:
                    if (node.Value is float) return One(FloatText((float)node.Value, false));
                    if (node.Value is double) return One(DoubleText((double)node.Value, false) + "d");
                    return One(Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? string.Empty);
                case IodxCstKind.DoubleQuotedString:
                    return One("\"" + IodxEscaping.EscapeDoubleQuotes(Convert.ToString(node.Value) ?? string.Empty) + "\"");
                case IodxCstKind.SingleQuotedString:
                    return One("'" + IodxEscaping.EscapeSingleQuotes(Convert.ToString(node.Value) ?? string.Empty) + "'");
                case IodxCstKind.Literal:
                case IodxCstKind.Operator:
                case IodxCstKind.Separator:
                    return One(LiteralText(node.Value));
                case IodxCstKind.LeftParenthesis: return One("(");
                case IodxCstKind.RightParenthesis: return One(")");
                case IodxCstKind.Whitespace: return One(" ");
                default: throw new InvalidOperationException("Unsupported IODX CST node kind: " + node.Kind);
            }
        }

        private List<string> PrintList(
            IEnumerable values,
            int startAt,
            string? left,
            string? right,
            bool addTabs)
        {
            if ((left == null) != (right == null))
            {
                throw new ArgumentException("left and right must either both be set or both be null");
            }

            level++;
            try
            {
                List<string> rendered = new List<string>();
                int commonLength = 0;
                bool tryCompact = true;
                foreach (object? value in values)
                {
                    if (IsSingleLineComment(value)) tryCompact = false;
                    List<string> childLines = PrintValue(startAt + options.Tab.Length, value);
                    if (childLines.Count == 0) throw new InvalidOperationException("An IODX value produced no output");
                    rendered.AddRange(childLines);
                    if (childLines.Count > 1) tryCompact = false;
                    else commonLength += childLines[0].Length;
                }

                if (tryCompact && level >= options.CompactFromLevel)
                {
                    int estimatedLength = commonLength + Math.Max(0, rendered.Count - 1);
                    if (left != null && right != null) estimatedLength += left.Length + right.Length;
                    if (estimatedLength + startAt <= options.MaxWidth
                        && estimatedLength <= options.MaxLocalWidth)
                    {
                        string body = string.Join(" ", rendered);
                        return One(left == null || right == null ? body : left + body + right);
                    }
                }

                if (left == null || right == null)
                {
                    return addTabs ? Indent(rendered) : rendered;
                }

                List<string> result = new List<string> { left };
                result.AddRange(addTabs ? Indent(rendered) : rendered);
                result.Add(right);
                return result;
            }
            finally
            {
                level--;
            }
        }

        private List<string> Indent(IEnumerable<string> lines)
        {
            List<string> result = new List<string>();
            foreach (string line in lines) result.Add(options.Tab + line);
            return result;
        }

        private static IReadOnlyList<object?> ToList(IEnumerable<object?> values)
        {
            return values as IReadOnlyList<object?> ?? new List<object?>(values);
        }

        private static List<string> One(string value)
        {
            return new List<string> { value };
        }

        private static string IntegerCstText(object? value)
        {
            if (value is long) return ((long)value).ToString(CultureInfo.InvariantCulture) + "l";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string FloatText(float value, bool compactInteger)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "IODX does not support non-finite numbers");
            }

            if (BitConverter.SingleToInt32Bits(value) == unchecked((int)0x80000000))
            {
                return compactInteger ? "-0" : "-0.0";
            }

            return FormatIntegralFloat(value.ToString("R", CultureInfo.InvariantCulture), value, compactInteger);
        }

        private static string DoubleText(double value, bool compactInteger)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "IODX does not support non-finite numbers");
            }

            if (BitConverter.DoubleToInt64Bits(value) == unchecked((long)0x8000000000000000UL))
            {
                return compactInteger ? "-0" : "-0.0";
            }

            return FormatIntegralFloat(value.ToString("R", CultureInfo.InvariantCulture), value, compactInteger);
        }

        private static string FormatIntegralFloat(string text, double value, bool compactInteger)
        {
            if (Math.Truncate(value) != value || text.IndexOf('E') >= 0 || text.IndexOf('e') >= 0) return text;
            if (compactInteger) return text.EndsWith(".0", StringComparison.Ordinal)
                ? text.Substring(0, text.Length - 2)
                : text;
            return text.EndsWith(".0", StringComparison.Ordinal) ? text : text + ".0";
        }

        private static string LiteralText(object? value)
        {
            if (value == null) return "null";
            if (value is bool) return (bool)value ? "true" : "false";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool IsSingleLineComment(object? value)
        {
            IodxComment? comment = value as IodxComment;
            if (comment != null) return comment.SingleLine;
            IodxCst? cst = value as IodxCst;
            return cst != null && cst.Kind == IodxCstKind.SingleLineComment;
        }

        private static Exception Unsupported(object value)
        {
            return new ArgumentException("Unsupported IODX value type: " + value.GetType().Name, nameof(value));
        }
    }
}
