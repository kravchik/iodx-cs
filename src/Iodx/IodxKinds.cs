namespace Iodx
{
    public enum IodxTokenKind
    {
        EndOfFile,
        LeftParenthesis,
        RightParenthesis,
        Whitespace,
        SingleLineComment,
        MultiLineComment,
        IntegerLiteral,
        InvalidLeadingZeroInteger,
        InvalidHexInteger,
        FloatingPointLiteral,
        Literal,
        Operator,
        Separator,
        DoubleQuotedString,
        SingleQuotedString,
        Dummy,
        Invalid,
    }

    public enum IodxCstKind
    {
        ListBody,
        NamedClass,
        UnnamedClass,
        LeftParenthesis,
        RightParenthesis,
        Whitespace,
        SingleLineComment,
        MultiLineComment,
        IntegerLiteral,
        FloatingPointLiteral,
        Literal,
        Operator,
        Separator,
        DoubleQuotedString,
        SingleQuotedString,
    }

    internal static class IodxKindNames
    {
        internal static IodxTokenKind Token(string name)
        {
            switch (name)
            {
                case "EOF": return IodxTokenKind.EndOfFile;
                case "LEFT_PAREN": return IodxTokenKind.LeftParenthesis;
                case "RIGHT_PAREN": return IodxTokenKind.RightParenthesis;
                case "WHITE_SPACE": return IodxTokenKind.Whitespace;
                case "COMMENT_SINGLE_LINE": return IodxTokenKind.SingleLineComment;
                case "COMMENT_MULTI_LINE": return IodxTokenKind.MultiLineComment;
                case "INTEGER_LITERAL": return IodxTokenKind.IntegerLiteral;
                case "INVALID_LEADING_ZERO_INTEGER": return IodxTokenKind.InvalidLeadingZeroInteger;
                case "INVALID_HEX_INTEGER": return IodxTokenKind.InvalidHexInteger;
                case "FLOATING_POINT_LITERAL": return IodxTokenKind.FloatingPointLiteral;
                case "ANY_LITERAL": return IodxTokenKind.Literal;
                case "ANY_OPERATOR": return IodxTokenKind.Operator;
                case "ANY_SEPARATOR": return IodxTokenKind.Separator;
                case "STRING_LITERAL_DQ": return IodxTokenKind.DoubleQuotedString;
                case "STRING_LITERAL_SQ": return IodxTokenKind.SingleQuotedString;
                case "DUMMY": return IodxTokenKind.Dummy;
                case "INVALID": return IodxTokenKind.Invalid;
                default: throw new System.ArgumentOutOfRangeException(nameof(name), name, "Unknown IODX token kind");
            }
        }

        internal static IodxCstKind Cst(string name)
        {
            switch (name)
            {
                case "LIST_BODY": return IodxCstKind.ListBody;
                case "NAMED_CLASS": return IodxCstKind.NamedClass;
                case "UNNAMED_CLASS": return IodxCstKind.UnnamedClass;
                case "LEFT_PAREN": return IodxCstKind.LeftParenthesis;
                case "RIGHT_PAREN": return IodxCstKind.RightParenthesis;
                case "WHITE_SPACE": return IodxCstKind.Whitespace;
                case "COMMENT_SINGLE_LINE": return IodxCstKind.SingleLineComment;
                case "COMMENT_MULTI_LINE": return IodxCstKind.MultiLineComment;
                case "INTEGER_LITERAL": return IodxCstKind.IntegerLiteral;
                case "FLOATING_POINT_LITERAL": return IodxCstKind.FloatingPointLiteral;
                case "ANY_LITERAL": return IodxCstKind.Literal;
                case "ANY_OPERATOR": return IodxCstKind.Operator;
                case "ANY_SEPARATOR": return IodxCstKind.Separator;
                case "STRING_LITERAL_DQ": return IodxCstKind.DoubleQuotedString;
                case "STRING_LITERAL_SQ": return IodxCstKind.SingleQuotedString;
                default: throw new System.ArgumentOutOfRangeException(nameof(name), name, "Unknown IODX CST kind");
            }
        }
    }
}
