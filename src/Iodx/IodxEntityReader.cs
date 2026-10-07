#nullable enable

using System;
using System.Collections.Generic;

namespace Iodx
{
    internal static class IodxEntityReader
    {
        internal static object? Parse(string source)
        {
            IReadOnlyList<object?> values = ParseAll(source);
            if (values.Count != 1)
            {
                throw new IodxEntityException("Expected exactly one value, got " + values.Count);
            }

            return values[0];
        }

        internal static IReadOnlyList<object?> ParseAll(string source)
        {
            return ResolveNodes(IodxParser.ParseCst(source).Children).Values;
        }

        private static object? ValueFromCst(IodxCst node)
        {
            if (node.Kind == IodxCstKind.ListBody) return ResolveNodes(node.Children).Values;
            if (node.Kind == IodxCstKind.NamedClass || node.Kind == IodxCstKind.UnnamedClass)
            {
                IodxCst body = RequiredField(node, "body");
                if (node.Kind == IodxCstKind.UnnamedClass && IsEmptyMap(body.Children)) return new IodxMap();
                string? name = node.Kind == IodxCstKind.NamedClass
                    ? EntityName(RequiredField(node, "name").Value)
                    : null;
                ResolvedNodes resolved = ResolveNodes(body.Children);
                return new IodxEntity(name, resolved.Values, node.Range, resolved.Ranges);
            }

            if (node.Kind == IodxCstKind.SingleLineComment)
            {
                return new IodxComment(Convert.ToString(node.Value) ?? string.Empty, true, node.Range);
            }

            if (node.Kind == IodxCstKind.MultiLineComment)
            {
                return new IodxComment(Convert.ToString(node.Value) ?? string.Empty, false, node.Range);
            }

            switch (node.Kind)
            {
                case IodxCstKind.IntegerLiteral:
                case IodxCstKind.FloatingPointLiteral:
                case IodxCstKind.DoubleQuotedString:
                case IodxCstKind.SingleQuotedString:
                case IodxCstKind.Literal:
                case IodxCstKind.Operator:
                case IodxCstKind.Separator:
                    return node.Value;
                default:
                    throw new IodxEntityException("Unknown IODX CST node kind: " + node.Kind, node.Range);
            }
        }

        private static ResolvedNodes ResolveNodes(IReadOnlyList<IodxCst> nodes)
        {
            List<object?> values = new List<object?>();
            List<SourceRange?> ranges = new List<SourceRange?>();
            IodxCst? leftNode = null;
            for (int index = 0; index < nodes.Count; index++)
            {
                IodxCst node = nodes[index];
                if (!IsDelimiter(node))
                {
                    leftNode = node;
                    values.Add(ResolveSingle(node));
                    ranges.Add(node.Range);
                    continue;
                }

                if (leftNode == null) throw EntityError("Expected key before '='", node);
                if (IsComment(leftNode)) throw EntityError("Comment instead of key", leftNode);
                index++;
                if (index >= nodes.Count) throw EntityError("Expected value after '='", node);
                IodxCst rightNode = nodes[index];
                if (IsComment(rightNode)) throw EntityError("Comment instead of value", rightNode);
                if (IsDelimiter(rightNode)) throw EntityError("Expected value", rightNode);

                SourceRange? fieldRange = CombineRanges(leftNode.Range, rightNode.Range);
                values[values.Count - 1] = new IodxField(
                    values[values.Count - 1],
                    ResolveSingle(rightNode),
                    fieldRange);
                ranges[ranges.Count - 1] = fieldRange;
                leftNode = null;
            }

            return new ResolvedNodes(values, ranges);
        }

        private static object? ResolveSingle(IodxCst node)
        {
            object? value = ValueFromCst(node);
            if (value is IReadOnlyList<object?>)
            {
                throw new IodxEntityException("A list body cannot be used as a single value", node.Range);
            }

            return value;
        }

        private static IodxCst RequiredField(IodxCst node, string name)
        {
            IodxCst value;
            if (!node.Fields.TryGetValue(name, out value!))
            {
                throw new IodxEntityException("Missing CST field '" + name + "' on " + node.Kind, node.Range);
            }

            return value;
        }

        private static IodxEntityException EntityError(string message, IodxCst node)
        {
            string location = node.Range == null ? string.Empty : " at " + node.Range.FormatBegin();
            return new IodxEntityException(message + location, node.Range);
        }

        private static SourceRange? CombineRanges(SourceRange? left, SourceRange? right)
        {
            if (left == null) return null;
            return right == null ? left : SourceRange.FromBounds(left, right);
        }

        private static bool IsDelimiter(IodxCst node)
        {
            return node.Kind == IodxCstKind.Operator && Equals(node.Value, "=");
        }

        private static bool IsComment(IodxCst node)
        {
            return node.Kind == IodxCstKind.SingleLineComment || node.Kind == IodxCstKind.MultiLineComment;
        }

        private static bool IsEmptyMap(IReadOnlyList<IodxCst> nodes)
        {
            return nodes.Count == 1 && IsDelimiter(nodes[0]);
        }

        private static string EntityName(object? value)
        {
            if (value == null) return "null";
            if (value is bool) return (bool)value ? "true" : "false";
            return Convert.ToString(value) ?? string.Empty;
        }

        private sealed class ResolvedNodes
        {
            internal ResolvedNodes(IReadOnlyList<object?> values, IReadOnlyList<SourceRange?> ranges)
            {
                Values = new List<object?>(values).AsReadOnly();
                Ranges = new List<SourceRange?>(ranges).AsReadOnly();
            }

            internal IReadOnlyList<object?> Values { get; }
            internal IReadOnlyList<SourceRange?> Ranges { get; }
        }
    }
}
