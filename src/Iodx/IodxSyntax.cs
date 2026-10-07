#nullable enable

using System;
using System.Collections.Generic;

namespace Iodx
{
    public static class IodxSyntax
    {
        public static IodxCst ParseCst(string source)
        {
            return IodxParser.ParseCst(source);
        }

        public static IReadOnlyList<IodxToken> Tokenize(string source)
        {
            return IodxParser.Tokenize(source);
        }

        public static object? Parse(string source)
        {
            return IodxEntityReader.Parse(source);
        }

        public static IReadOnlyList<object?> ParseAll(string source)
        {
            return IodxEntityReader.ParseAll(source);
        }

        public static string Format(object? value, IodxPrinterOptions? options = null)
        {
            return new IodxPrinter(options).Render(value);
        }

        public static string FormatAll(
            IEnumerable<object?> values,
            IodxPrinterOptions? options = null)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return new IodxPrinter(options).RenderAll(values);
        }
    }
}
