#nullable enable

using System;
using System.Collections.Generic;
using Xunit;

namespace Iodx.Tests
{
    public sealed class EntityTests
    {
        [Fact]
        public void ParseCreatesNamedUnnamedAndNestedEntities()
        {
            Assert.Empty(IodxSyntax.ParseAll(string.Empty));
            AssertEntity(IodxSyntax.Parse("()"), null, 0);
            AssertEntity(IodxSyntax.Parse("foo()"), "foo", 0);
            IodxEntity outer = AssertEntity(IodxSyntax.Parse("foo(bar(hello))"), "foo", 1);
            IodxEntity inner = AssertEntity(outer.Children[0], "bar", 1);
            Assert.Equal("hello", inner.Children[0]);
        }

        [Fact]
        public void ParseCombinesFieldsAndPreservesNullKeys()
        {
            IodxEntity entity = AssertEntity(IodxSyntax.Parse("(null=value key=null true=false)"), null, 3);
            IodxField first = Assert.IsType<IodxField>(entity.Children[0]);
            Assert.Null(first.Key);
            Assert.Equal("value", first.Value);
            IodxField second = Assert.IsType<IodxField>(entity.Children[1]);
            Assert.Equal("key", second.Key);
            Assert.Null(second.Value);
            IodxField third = Assert.IsType<IodxField>(entity.Children[2]);
            Assert.Equal(true, third.Key);
            Assert.Equal(false, third.Value);
        }

        [Fact]
        public void ExplicitEmptyMapUsesIodxMap()
        {
            Assert.Empty(Assert.IsType<IodxMap>(IodxSyntax.Parse("(=)")));
        }

        [Fact]
        public void EntitiesFieldsAndCommentsPreserveRanges()
        {
            IodxEntity person = Assert.IsType<IodxEntity>(IodxSyntax.Parse("Person(name = \"John\"\nage = 25)"));
            Assert.Equal(new SourceRange(1, 1, 2, 9, 0, 30), person.Range);
            Assert.Equal(new SourceRange(1, 8, 1, 20, 7, 20), person.ChildrenRanges[0]);
            Assert.Equal(new SourceRange(2, 1, 2, 8, 21, 29), person.ChildrenRanges[1]);

            IReadOnlyList<object?> values = IodxSyntax.ParseAll("//header\nPerson('John') 42 /*tail*/");
            IodxComment heading = Assert.IsType<IodxComment>(values[0]);
            Assert.Equal("header", heading.Text);
            Assert.True(heading.SingleLine);
            Assert.Equal(new SourceRange(1, 1, 1, 8, 0, 8), heading.Range);
            Assert.False(Assert.IsType<IodxComment>(values[3]).SingleLine);
        }

        [Theory]
        [InlineData("(= a)", "Expected key before '=' at 1:2", 1)]
        [InlineData("(a =)", "Expected value after '=' at 1:4", 3)]
        [InlineData("(a = =)", "Expected value at 1:6", 5)]
        [InlineData("(a = b = c)", "Expected key before '=' at 1:8", 7)]
        [InlineData("(a//\n = b)", "Comment instead of key at 1:3", 2)]
        [InlineData("(a = //\nb)", "Comment instead of value at 1:6", 5)]
        public void InvalidFieldsReportSemanticLocations(string source, string message, int offset)
        {
            IodxEntityException error = Assert.Throws<IodxEntityException>(() => IodxSyntax.Parse(source));
            Assert.Equal(message, error.Message);
            Assert.Equal(offset, error.Range!.BeginOffset);
        }

        [Fact]
        public void FieldHelpersQueryAndCopyWithoutMutation()
        {
            IodxEntity entity = new IodxEntity(
                "Config",
                new object?[] { new IodxField("width", 800), new IodxField("nullable", null), "visible" });
            Assert.True(entity.HasField("width"));
            Assert.False(entity.HasField("height"));
            Assert.Same(entity.Fields, entity.Fields);
            Assert.Equal(800, entity.GetField<int>("width"));
            Assert.Equal(600, entity.GetFieldOrDefault("height", 600));
            Assert.True(entity.TryGetField("nullable", out object? nullValue));
            Assert.Null(nullValue);
            Assert.False(entity.TryGetField("height", out _));
            Assert.Throws<KeyNotFoundException>(() => entity.GetField("height"));

            IodxEntity updated = entity.WithField("width", 1024).WithField("height", 600);
            Assert.Equal(1024, updated.GetField<int>("width"));
            Assert.Equal(600, updated.GetField<int>("height"));
            Assert.Equal(800, entity.GetField<int>("width"));
            Assert.Equal(900, updated.ReplaceField("height", 900).GetField<int>("height"));
            Assert.Throws<KeyNotFoundException>(() => entity.ReplaceField("height", 900));
            Assert.False(updated.WithoutField("width").HasField("width"));
        }

        [Fact]
        public void ModelConstructorsDefensivelyCopyCollections()
        {
            object?[] children = { new IodxField("width", 800) };
            SourceRange?[] ranges = { new SourceRange(1, 1, 1, 11, 0, 11) };
            IodxEntity entity = new IodxEntity("Config", children, null, ranges);
            children[0] = "changed";
            ranges[0] = null;

            Assert.IsType<IodxField>(entity.Children[0]);
            Assert.NotNull(entity.ChildrenRanges[0]);
            Assert.Throws<NotSupportedException>(
                () => ((IList<object?>)entity.Children)[0] = "changed");

            IodxField[] entries = { new IodxField("key", "value") };
            IodxMap map = new IodxMap(entries);
            entries[0] = new IodxField("changed", "changed");
            Assert.Equal("value", map["key"]);
        }

        [Fact]
        public void MapLookupDistinguishesMissingAndNullValues()
        {
            IodxMap map = new IodxMap(new[]
            {
                new IodxField(null, "null key"),
                new IodxField("nullable", null),
            });

            Assert.Equal("null key", map[null]);
            Assert.True(map.TryGetValue("nullable", out object? value));
            Assert.Null(value);
            Assert.False(map.TryGetValue("missing", out _));
            Assert.Throws<KeyNotFoundException>(() => map["missing"]);
        }

        private static IodxEntity AssertEntity(object? value, string? name, int children)
        {
            IodxEntity entity = Assert.IsType<IodxEntity>(value);
            Assert.Equal(name, entity.Name);
            Assert.Equal(children, entity.Children.Count);
            return entity;
        }
    }
}
