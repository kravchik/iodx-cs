#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Iodx
{
    public sealed class IodxField
    {
        public IodxField(object? key, object? value, SourceRange? range = null)
        {
            Key = key;
            Value = value;
            Range = range;
        }

        public object? Key { get; }
        public object? Value { get; }
        public SourceRange? Range { get; }
    }

    public sealed class IodxComment
    {
        public IodxComment(string text, bool singleLine = true, SourceRange? range = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            SingleLine = singleLine;
            Range = range;
        }

        public string Text { get; }
        public bool SingleLine { get; }
        public SourceRange? Range { get; }
    }

    public sealed class IodxEntity
    {
        private readonly IReadOnlyList<IodxField> fields;

        public IodxEntity(
            string? name,
            IReadOnlyList<object?>? children = null,
            SourceRange? range = null,
            IReadOnlyList<SourceRange?>? childrenRanges = null)
        {
            Name = name;
            Children = new List<object?>(children ?? Array.Empty<object?>()).AsReadOnly();
            Range = range;
            if (childrenRanges != null && childrenRanges.Count != Children.Count)
            {
                throw new ArgumentException("A range is required for every child", nameof(childrenRanges));
            }

            ChildrenRanges = new List<SourceRange?>(
                childrenRanges ?? new SourceRange?[Children.Count]).AsReadOnly();
            List<IodxField> fieldList = new List<IodxField>();
            foreach (object? child in Children)
            {
                IodxField? field = child as IodxField;
                if (field != null) fieldList.Add(field);
            }

            fields = fieldList.AsReadOnly();
        }

        public string? Name { get; }
        public IReadOnlyList<object?> Children { get; }
        public SourceRange? Range { get; }
        public IReadOnlyList<SourceRange?> ChildrenRanges { get; }
        public IReadOnlyList<IodxField> Fields => fields;

        public bool HasField(object? key)
        {
            foreach (object? child in Children)
            {
                IodxField? field = child as IodxField;
                if (field != null && Equals(field.Key, key)) return true;
            }

            return false;
        }

        public bool TryGetField(object? key, out object? value)
        {
            foreach (IodxField field in fields)
            {
                if (!Equals(field.Key, key)) continue;
                value = field.Value;
                return true;
            }

            value = null;
            return false;
        }

        public object? GetField(object? key)
        {
            object? value;
            if (!TryGetField(key, out value))
            {
                throw new KeyNotFoundException("IODX field was not found: " + Convert.ToString(key));
            }

            return value;
        }

        [return: MaybeNull]
        public T GetField<T>(object? key)
        {
            object? value = GetField(key);
            if (value is T) return (T)value;
            if (value == null && default(T) is null) return default!;
            throw new InvalidCastException(
                "IODX field '" + Convert.ToString(key) + "' is not a " + typeof(T).FullName);
        }

        public object? GetFieldOrDefault(object? key, object? defaultValue = null)
        {
            object? value;
            return TryGetField(key, out value) ? value : defaultValue;
        }

        public IodxEntity WithField(object? key, object? value)
        {
            List<object?> children = new List<object?>(Children.Count);
            List<SourceRange?> ranges = new List<SourceRange?>(ChildrenRanges);
            bool replaced = false;
            foreach (object? child in Children)
            {
                IodxField? field = child as IodxField;
                if (field != null && Equals(field.Key, key))
                {
                    children.Add(new IodxField(key, value, field.Range));
                    replaced = true;
                }
                else
                {
                    children.Add(child);
                }
            }

            if (!replaced)
            {
                children.Add(new IodxField(key, value));
                ranges.Add(null);
            }

            return new IodxEntity(Name, children, Range, ranges);
        }

        public IodxEntity ReplaceField(object? key, object? value)
        {
            if (!HasField(key))
            {
                throw new KeyNotFoundException("IODX field was not found: " + Convert.ToString(key));
            }

            return WithField(key, value);
        }

        public IodxEntity WithoutField(object? key)
        {
            List<object?> children = new List<object?>(Children.Count);
            List<SourceRange?> ranges = new List<SourceRange?>(Children.Count);
            for (int index = 0; index < Children.Count; index++)
            {
                IodxField? field = Children[index] as IodxField;
                if (field != null && Equals(field.Key, key)) continue;
                children.Add(Children[index]);
                ranges.Add(ChildrenRanges[index]);
            }

            return new IodxEntity(Name, children, Range, ranges);
        }
    }

    public sealed class IodxMap : IReadOnlyCollection<IodxField>
    {
        private readonly IReadOnlyList<IodxField> entries;

        public IodxMap(IReadOnlyList<IodxField>? fields = null)
        {
            entries = new List<IodxField>(fields ?? Array.Empty<IodxField>()).AsReadOnly();
        }

        public IReadOnlyList<IodxField> Entries => entries;
        public int Count => entries.Count;

        public object? this[object? key]
        {
            get
            {
                object? value;
                if (TryGetValue(key, out value)) return value;
                throw new KeyNotFoundException("IODX map key was not found: " + Convert.ToString(key));
            }
        }

        public bool TryGetValue(object? key, out object? value)
        {
            foreach (IodxField entry in entries)
            {
                if (!Equals(entry.Key, key)) continue;
                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }

        public IEnumerator<IodxField> GetEnumerator()
        {
            return entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
