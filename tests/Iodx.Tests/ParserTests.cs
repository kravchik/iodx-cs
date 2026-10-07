#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Iodx.Tests
{
    public sealed class ParserTests
    {
        [Fact]
        public void TokenizeUsesLongestMatchesAndPreservesRanges()
        {
            IReadOnlyList<IodxToken> tokens = IodxSyntax.Tokenize("-0xFFL 1.5e-2d // note\n/* block */ == name");

            Assert.Collection(
                tokens,
                token => Token(token, IodxTokenKind.IntegerLiteral, "-0xFFL", 0, 6),
                token => Token(token, IodxTokenKind.Whitespace, " ", 6, 7),
                token => Token(token, IodxTokenKind.FloatingPointLiteral, "1.5e-2d", 7, 14),
                token => Token(token, IodxTokenKind.Whitespace, " ", 14, 15),
                token => Token(token, IodxTokenKind.SingleLineComment, "// note", 15, 22),
                token => Token(token, IodxTokenKind.Whitespace, "\n", 22, 23),
                token => Token(token, IodxTokenKind.MultiLineComment, "/* block */", 23, 34),
                token => Token(token, IodxTokenKind.Whitespace, " ", 34, 35),
                token => Token(token, IodxTokenKind.Operator, "==", 35, 37),
                token => Token(token, IodxTokenKind.Whitespace, " ", 37, 38),
                token => Token(token, IodxTokenKind.Literal, "name", 38, 42),
                token => Token(token, IodxTokenKind.EndOfFile, string.Empty, 42, 42));
        }

        [Fact]
        public void ParseCstConvertsLiteralTypes()
        {
            IodxCst document = IodxSyntax.ParseCst(
                "Spell(active = true count = 42 id = 9223372036854775807L chance = 1.5 speed = 2d text = \"open\")");
            IodxCst entity = Assert.Single(document.Children);
            Assert.Equal(IodxCstKind.NamedClass, entity.Kind);
            Assert.Equal("Spell", entity.Fields["name"].Value);

            IReadOnlyList<IodxCst> values = entity.Fields["body"].Children;
            Assert.IsType<bool>(values[2].Value);
            Assert.IsType<int>(values[5].Value);
            Assert.IsType<long>(values[8].Value);
            Assert.IsType<float>(values[11].Value);
            Assert.IsType<double>(values[14].Value);
            Assert.Equal("open", values[17].Value);
        }

        [Fact]
        public void ParseCstDecodesCommentsStringsAndUnicode()
        {
            IodxCst document = IodxSyntax.ParseCst(
                "// heading\n/*details*/ 'old\\sscroll' \"Say \\\"open\\\"\" 'emoji: \\uD83D\\uDE00'");

            Assert.Equal(" heading", document.Children[0].Value);
            Assert.Equal("details", document.Children[1].Value);
            Assert.Equal("old scroll", document.Children[2].Value);
            Assert.Equal("Say \"open\"", document.Children[3].Value);
            Assert.Equal("emoji: 😀", document.Children[4].Value);
        }

        [Fact]
        public void ParseTreatsExistingFilenameAsSourceText()
        {
            const string source = "existing.iodx";
            File.WriteAllText(source, "different");
            try
            {
                IodxCst document = IodxSyntax.ParseCst(source);
                Assert.Equal(source, Assert.Single(document.Children).Value);
            }
            finally
            {
                File.Delete(source);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\n\t")]
        public void EmptyDocumentsProduceAnEmptyList(string source)
        {
            IodxCst document = IodxSyntax.ParseCst(source);
            Assert.Empty(document.Children);
            Assert.Equal(0, document.Range!.BeginOffset);
            Assert.Equal(source.Length, document.Range.EndOffset);
        }

        [Fact]
        public void CrLfAndSurrogateOffsetsReferToOriginalSource()
        {
            string source = "'😀'\r\nnext";
            IReadOnlyList<IodxToken> tokens = IodxSyntax.Tokenize(source);
            Assert.Equal(4, tokens[0].Range.EndOffset);
            Assert.Equal(6, tokens[1].Range.EndOffset);
            Assert.Equal(6, tokens[2].Range.BeginOffset);
            Assert.Equal(2, tokens[2].Range.BeginLine);
            Assert.Equal(1, tokens[2].Range.BeginColumn);
        }

        [Theory]
        [InlineData("012", 0)]
        [InlineData("0x", 0)]
        [InlineData("hello)", 5)]
        [InlineData("(", 1)]
        public void RejectedSyntaxHasStableErrorOffsets(string source, int offset)
        {
            IodxParseException error = Assert.Throws<IodxParseException>(() => IodxSyntax.ParseCst(source));
            Assert.NotNull(error.Range);
            Assert.Equal(offset, error.Range!.BeginOffset);
        }

        [Theory]
        [InlineData("'bad\\u12'", 4, 8, "Incomplete Unicode escape")]
        [InlineData("'bad\\u12G4'", 4, 10, "Invalid hexadecimal digit")]
        [InlineData("'bad\\uD83D'", 4, 10, "High surrogate")]
        [InlineData("'bad\\uDE00'", 4, 10, "Unexpected low surrogate")]
        [InlineData("'bad\\q'", 4, 6, "Unknown escape symbol")]
        [InlineData("2147483648", 0, 10, "Integer literal out of range")]
        public void LiteralErrorsHaveExactRanges(string source, int begin, int end, string message)
        {
            IodxParseException error = Assert.Throws<IodxParseException>(() => IodxSyntax.ParseCst(source));
            Assert.Contains(message, error.Message);
            Assert.Equal(begin, error.Range!.BeginOffset);
            Assert.Equal(end, error.Range.EndOffset);
        }

        [Theory]
        [InlineData("-2147483648", typeof(int))]
        [InlineData("-0x80000000", typeof(int))]
        [InlineData("-9223372036854775808L", typeof(long))]
        [InlineData("-0x8000000000000000L", typeof(long))]
        public void IntegerMinimumValuesAreAccepted(string source, Type expectedType)
        {
            object? value = Assert.Single(IodxSyntax.ParseCst(source).Children).Value;
            Assert.IsType(expectedType, value);
        }

        private static void Token(IodxToken token, IodxTokenKind kind, string text, int begin, int end)
        {
            Assert.Equal(kind, token.Kind);
            Assert.Equal(text, token.Text);
            Assert.Equal(begin, token.Range.BeginOffset);
            Assert.Equal(end, token.Range.EndOffset);
        }
    }
}
