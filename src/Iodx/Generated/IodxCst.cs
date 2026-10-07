#nullable enable

using System;
using System.Collections.Generic;

namespace Iodx.Generated
{
    internal static class CstSupport
    {
        internal static ListAdapter<IodxCst> NewList()
        {
            return new ListAdapter<IodxCst>();
        }

        internal static MapAdapter<string, IodxCst> NewMap()
        {
            return new MapAdapter<string, IodxCst>();
        }

        internal static IodxCst ListBody(
            Token begin,
            Token end,
            ListAdapter<IodxCst> children)
        {
            bool dummyBegin = begin.Type == TokenType.DUMMY;
            bool dummyEnd = end.Type == TokenType.DUMMY || end.Type == TokenType.EOF;
            int beginLine = dummyBegin ? 1 : begin.BeginLine;
            int beginColumn = dummyBegin ? 1 : begin.BeginColumn;
            int beginOffset = dummyBegin ? 0 : begin.BeginOffset;
            return new IodxCst(
                "LIST_BODY",
                beginLine,
                beginColumn,
                dummyEnd ? beginLine : end.EndLine,
                dummyEnd ? beginColumn : end.EndColumn,
                beginOffset,
                end.EndOffset,
                null,
                children,
                null);
        }
    }

    internal sealed class IodxCst
    {
        public IodxCst(
            string type,
            int beginLine,
            int beginColumn,
            int endLine,
            int endColumn,
            int beginOffset,
            int endOffset,
            object? value,
            IList<IodxCst>? children = null,
            IDictionary<string, IodxCst>? fields = null)
        {
            Type = type;
            BeginLine = beginLine;
            BeginColumn = beginColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            BeginOffset = beginOffset;
            EndOffset = endOffset;
            Value = value;
            Children = children ?? new List<IodxCst>();
            Fields = fields ?? new Dictionary<string, IodxCst>(StringComparer.Ordinal);
        }

        public string Type { get; }
        public int BeginLine { get; }
        public int BeginColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
        public int BeginOffset { get; }
        public int EndOffset { get; }
        public object? Value { get; }
        public IList<IodxCst> Children { get; }
        public IDictionary<string, IodxCst> Fields { get; }
    }
}
