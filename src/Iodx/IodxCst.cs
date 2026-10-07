#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Iodx
{
    public sealed class IodxCst
    {
        public IodxCst(
            IodxCstKind kind,
            SourceRange? range = null,
            object? value = null,
            IReadOnlyList<IodxCst>? children = null,
            IReadOnlyDictionary<string, IodxCst>? fields = null)
        {
            Kind = kind;
            Range = range;
            Value = value;
            Children = new List<IodxCst>(children ?? Array.Empty<IodxCst>()).AsReadOnly();
            Dictionary<string, IodxCst> fieldCopy = new Dictionary<string, IodxCst>(StringComparer.Ordinal);
            if (fields != null)
            {
                foreach (KeyValuePair<string, IodxCst> field in fields) fieldCopy.Add(field.Key, field.Value);
            }

            Fields = new ReadOnlyDictionary<string, IodxCst>(fieldCopy);
        }

        public IodxCstKind Kind { get; }
        public SourceRange? Range { get; }
        public object? Value { get; }
        public IReadOnlyList<IodxCst> Children { get; }
        public IReadOnlyDictionary<string, IodxCst> Fields { get; }
    }
}
