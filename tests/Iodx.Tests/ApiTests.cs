#nullable enable

using Xunit;

namespace Iodx.Tests
{
    public sealed class ApiTests
    {
        [Fact]
        public void GeneratedTypesAreNotPublicApi()
        {
            Assert.DoesNotContain(
                typeof(IodxSyntax).Assembly.GetExportedTypes(),
                type => type.Namespace == "Iodx.Generated");
        }

        [Fact]
        public void CstDefensivelyCopiesChildrenAndFields()
        {
            IodxCst child = new IodxCst(IodxCstKind.Literal, value: "value");
            IodxCst[] children = { child };
            System.Collections.Generic.Dictionary<string, IodxCst> fields =
                new System.Collections.Generic.Dictionary<string, IodxCst> { ["value"] = child };
            IodxCst cst = new IodxCst(IodxCstKind.ListBody, children: children, fields: fields);

            children[0] = new IodxCst(IodxCstKind.Literal, value: "changed");
            fields.Clear();

            Assert.Same(child, Assert.Single(cst.Children));
            Assert.Same(child, cst.Fields["value"]);
            Assert.Throws<System.NotSupportedException>(
                () => ((System.Collections.Generic.IList<IodxCst>)cst.Children)[0] = child);
            Assert.Throws<System.NotSupportedException>(
                () => ((System.Collections.Generic.IDictionary<string, IodxCst>)cst.Fields).Clear());
        }

        [Fact]
        public void SourceRangesUseEndExclusiveOffsets()
        {
            IodxCst value = Assert.Single(IodxSyntax.ParseCst("hello").Children);
            Assert.Equal(new SourceRange(1, 1, 1, 5, 0, 5), value.Range);
        }
    }
}
