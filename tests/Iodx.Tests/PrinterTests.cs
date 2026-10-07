#nullable enable

using System;
using System.Collections.Generic;
using Xunit;

namespace Iodx.Tests
{
    public sealed class PrinterTests
    {
        [Theory]
        [InlineData("hello", true)]
        [InlineData("+", true)]
        [InlineData("==", true)]
        [InlineData(",", true)]
        [InlineData("", false)]
        [InlineData("hello world", false)]
        [InlineData("true", false)]
        [InlineData("42", false)]
        [InlineData("=", false)]
        [InlineData("//x", false)]
        public void QuoteDetectionIsSyntaxAware(string value, bool expected)
        {
            Assert.Equal(expected, new IodxPrinter().CanRenderWithoutQuotes(value));
        }

        [Fact]
        public void FormatRendersScalarTypes()
        {
            Assert.Equal("null", IodxSyntax.Format(null));
            Assert.Equal("true", IodxSyntax.Format(true));
            Assert.Equal("42", IodxSyntax.Format(42));
            Assert.Equal("42l", IodxSyntax.Format(42L));
            Assert.Equal("3.14f", IodxSyntax.Format(3.14f));
            Assert.Equal("2.71d", IodxSyntax.Format(2.71d));
            Assert.Equal("hello", IodxSyntax.Format("hello"));
            Assert.Equal("'hello world'", IodxSyntax.Format("hello world"));
            Assert.Equal("\"can't\"", IodxSyntax.Format("can't"));
            Assert.Equal("'null'", IodxSyntax.Format("null"));
        }

        [Fact]
        public void FormatRendersStructuresAndComments()
        {
            Assert.Equal("(1 2 3)", IodxSyntax.Format(new IodxEntity(null, new object?[] { 1, 2, 3 })));
            Assert.Equal("Vec2(x y)", IodxSyntax.Format(new IodxEntity("Vec2", new object?[] { "x", "y" })));
            Assert.Equal("count = 42", IodxSyntax.Format(new IodxField("count", 42)));
            Assert.Equal("()", IodxSyntax.Format(Array.Empty<object?>()));
            Assert.Equal("(=)", IodxSyntax.Format(new IodxMap()));
            Assert.Equal("// generated", IodxSyntax.Format(new IodxComment(" generated")));
            Assert.Equal("/* generated */", IodxSyntax.Format(new IodxComment(" generated ", false)));
        }

        [Fact]
        public void SingleLineCommentsForceMultilineLayout()
        {
            IodxEntity value = new IodxEntity(
                null,
                new object?[] { 42, new IodxComment(" comment"), "hello" });
            Assert.Equal("(\n  42\n  // comment\n  hello\n)", IodxSyntax.Format(value));
        }

        [Fact]
        public void WidthAndCompactionSettingsAreApplied()
        {
            object? value = IodxSyntax.Parse("outer(inner(1))");
            Assert.Equal(
                "outer(\n  inner(\n    1\n  )\n)",
                IodxSyntax.Format(value, new IodxPrinterOptions { MaxWidth = 1 }));
            Assert.Equal(
                "outer(inner(1))",
                IodxSyntax.Format(value, new IodxPrinterOptions { CompactFromLevel = 1 }));
            Assert.Equal(
                "outer(\n  inner(1)\n)",
                IodxSyntax.Format(value, new IodxPrinterOptions { CompactFromLevel = 2 }));
        }

        [Theory]
        [InlineData("()")]
        [InlineData("foo(bar)")]
        [InlineData("(a = b e c = d)")]
        [InlineData("/*comment*/")]
        [InlineData("'hello world'")]
        [InlineData("(=)")]
        [InlineData("Person(name = 'John' age = 25)")]
        public void SyntaxModelRoundTrips(string source)
        {
            string first = IodxSyntax.Format(IodxSyntax.Parse(source));
            string second = IodxSyntax.Format(IodxSyntax.Parse(first));
            Assert.Equal(first, second);
        }

        [Fact]
        public void NumericTypesRoundTrip()
        {
            IReadOnlyList<object?> values = IodxSyntax.ParseAll("1 2L 3.5 4f 5d");
            IReadOnlyList<object?> result = IodxSyntax.ParseAll(IodxSyntax.FormatAll(values));
            Assert.IsType<int>(result[0]);
            Assert.IsType<long>(result[1]);
            Assert.IsType<float>(result[2]);
            Assert.IsType<float>(result[3]);
            Assert.IsType<double>(result[4]);
        }

        [Fact]
        public void NonFiniteAndUnsupportedValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IodxSyntax.Format(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => IodxSyntax.Format(float.NaN));
            Assert.Throws<ArgumentException>(() => IodxSyntax.Format(DateTime.UtcNow));
        }

        [Fact]
        public void PrinterSnapshotsMutableOptions()
        {
            IodxPrinterOptions options = new IodxPrinterOptions { MaxWidth = 1 };
            IodxPrinter printer = new IodxPrinter(options);
            options.MaxWidth = 100;

            Assert.Equal("(\n  a\n  b\n)", printer.Render(new IodxEntity(null, new object?[] { "a", "b" })));
        }
    }
}
