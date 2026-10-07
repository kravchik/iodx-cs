#nullable enable

using System;
using System.Collections.Generic;

namespace Iodx
{
    public class IodxException : Exception
    {
        public IodxException(string message, SourceRange? range = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Range = range;
        }

        public SourceRange? Range { get; }
    }

    public sealed class IodxParseException : IodxException
    {
        public IodxParseException(
            string message,
            SourceRange? range = null,
            IReadOnlyCollection<IodxTokenKind>? expectedTokens = null,
            Exception? innerException = null,
            IodxTokenKind? tokenKind = null)
            : base(message, range, innerException)
        {
            ExpectedTokens = new List<IodxTokenKind>(
                expectedTokens ?? Array.Empty<IodxTokenKind>()).AsReadOnly();
            TokenKind = tokenKind;
        }

        public IReadOnlyCollection<IodxTokenKind> ExpectedTokens { get; }
        public IodxTokenKind? TokenKind { get; }
    }

    public sealed class IodxEntityException : IodxException
    {
        public IodxEntityException(string message, SourceRange? range = null)
            : base(message, range)
        {
        }
    }
}
