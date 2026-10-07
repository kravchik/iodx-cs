#nullable enable

namespace Iodx
{
    public sealed class SourceRange
    {
        public SourceRange(
            int beginLine,
            int beginColumn,
            int endLine,
            int endColumn,
            int beginOffset,
            int endOffset)
        {
            BeginLine = beginLine;
            BeginColumn = beginColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            BeginOffset = beginOffset;
            EndOffset = endOffset;
        }

        public int BeginLine { get; }
        public int BeginColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
        public int BeginOffset { get; }
        public int EndOffset { get; }

        public string FormatBegin()
        {
            return BeginLine + ":" + BeginColumn;
        }

        public static SourceRange FromBounds(SourceRange start, SourceRange end)
        {
            return new SourceRange(
                start.BeginLine,
                start.BeginColumn,
                end.EndLine,
                end.EndColumn,
                start.BeginOffset,
                end.EndOffset);
        }

        public override bool Equals(object? obj)
        {
            SourceRange? other = obj as SourceRange;
            return other != null
                && BeginLine == other.BeginLine
                && BeginColumn == other.BeginColumn
                && EndLine == other.EndLine
                && EndColumn == other.EndColumn
                && BeginOffset == other.BeginOffset
                && EndOffset == other.EndOffset;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = BeginLine;
                result = (result * 397) ^ BeginColumn;
                result = (result * 397) ^ EndLine;
                result = (result * 397) ^ EndColumn;
                result = (result * 397) ^ BeginOffset;
                return (result * 397) ^ EndOffset;
            }
        }
    }
}
