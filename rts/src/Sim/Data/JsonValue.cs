// rts/src/Sim/Data/JsonValue.cs
// The parsed form of a data file: an immutable tree of objects, arrays, strings, numbers, booleans and nulls.
// Part of Rts.Sim.Data. Produced by JsonReader, consumed by DataLoader and the Tuning classes. Numbers keep their
// decimal text so they reach Fix64 or int without any floating-point step (ADR-002). Every accessor that can fail
// throws a DataValidationException naming the file and the field path, so validation code stays short.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Which of the six JSON kinds a <see cref="JsonValue"/> is.</summary>
    public enum JsonKind
    {
        /// <summary>A { "key": value } object.</summary>
        Object = 0,

        /// <summary>A [ value, ... ] array.</summary>
        Array = 1,

        /// <summary>A quoted string, escapes already decoded.</summary>
        String = 2,

        /// <summary>A number kept as its decimal text.</summary>
        Number = 3,

        /// <summary>true or false.</summary>
        Bool = 4,

        /// <summary>null.</summary>
        Null = 5,
    }

    /// <summary>One node of a parsed data file. Immutable once the reader has built it.</summary>
    public sealed class JsonValue
    {
        /// <summary>Which kind of value this node is.</summary>
        public JsonKind Kind { get; }

        /// <summary>Name of the file this node came from, used as the first part of every error message. Not empty.</summary>
        public string SourceName { get; }

        /// <summary>Path of this node inside its file: "" for the root, "speed" for a member, "spawns[1].x" for nested nodes.</summary>
        public string Path { get; }

        /// <summary>Line where the node starts. Unit: lines, 1-based. Range: 1 or more.</summary>
        public int Line { get; }

        /// <summary>Column where the node starts. Unit: characters, 1-based. Range: 1 or more.</summary>
        public int Column { get; }

        /// <summary>The string value (Kind String) or the number's decimal text (Kind Number); null for other kinds.</summary>
        private readonly string? _text;

        /// <summary>The boolean value (Kind Bool); false for other kinds.</summary>
        private readonly bool _bool;

        /// <summary>Members by key in ordinal order (Kind Object); null for other kinds.</summary>
        private readonly SortedDictionary<string, JsonValue>? _members;

        /// <summary>Elements in file order (Kind Array); null for other kinds.</summary>
        private readonly List<JsonValue>? _items;

        /// <summary>Private constructor; use the Create* factories, which the reader calls.</summary>
        private JsonValue(JsonKind kind, string sourceName, string path, int line, int column,
            string? text, bool boolean, SortedDictionary<string, JsonValue>? members, List<JsonValue>? items)
        {
            Kind = kind;
            SourceName = sourceName;
            Path = path;
            Line = line;
            Column = column;
            _text = text;
            _bool = boolean;
            _members = members;
            _items = items;
        }

        /// <summary>Creates a string node.</summary>
        internal static JsonValue CreateString(string sourceName, string path, int line, int column, string value)
        {
            return new JsonValue(JsonKind.String, sourceName, path, line, column, value, false, null, null);
        }

        /// <summary>Creates a number node from its validated decimal text.</summary>
        internal static JsonValue CreateNumber(string sourceName, string path, int line, int column, string text)
        {
            return new JsonValue(JsonKind.Number, sourceName, path, line, column, text, false, null, null);
        }

        /// <summary>Creates a boolean node.</summary>
        internal static JsonValue CreateBool(string sourceName, string path, int line, int column, bool value)
        {
            return new JsonValue(JsonKind.Bool, sourceName, path, line, column, null, value, null, null);
        }

        /// <summary>Creates a null node.</summary>
        internal static JsonValue CreateNull(string sourceName, string path, int line, int column)
        {
            return new JsonValue(JsonKind.Null, sourceName, path, line, column, null, false, null, null);
        }

        /// <summary>Creates an object node. <paramref name="members"/> is owned by the node afterwards.</summary>
        internal static JsonValue CreateObject(string sourceName, string path, int line, int column, SortedDictionary<string, JsonValue> members)
        {
            return new JsonValue(JsonKind.Object, sourceName, path, line, column, null, false, members, null);
        }

        /// <summary>Creates an array node. <paramref name="items"/> is owned by the node afterwards.</summary>
        internal static JsonValue CreateArray(string sourceName, string path, int line, int column, List<JsonValue> items)
        {
            return new JsonValue(JsonKind.Array, sourceName, path, line, column, null, false, null, items);
        }

        // ---------------------------------------------------------------- errors

        /// <summary>Builds (for the caller to throw) a <see cref="DataValidationException"/> for this node's file and path.</summary>
        /// <param name="problem">Phrase that follows "field '...'" in the message.</param>
        public DataValidationException Fail(string problem)
        {
            return new DataValidationException(SourceName, Path, problem);
        }

        /// <summary>Builds the error for a child key of this object (the child need not exist).</summary>
        private DataValidationException FailChild(string key, string problem)
        {
            return new DataValidationException(SourceName, ChildPath(key), problem);
        }

        /// <summary>Path of the member called <paramref name="key"/>: the key alone at the root, otherwise "parent.key".</summary>
        private string ChildPath(string key)
        {
            return Path.Length == 0 ? key : Path + "." + key;
        }

        /// <summary>Human wording for a kind, used in "must be a ..." messages.</summary>
        private static string KindName(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.Object: return "an object";
                case JsonKind.Array: return "an array";
                case JsonKind.String: return "a string";
                case JsonKind.Number: return "a number";
                case JsonKind.Bool: return "true or false";
                default: return "null";
            }
        }

        // ---------------------------------------------------------------- scalar accessors

        /// <summary>The string value. Throws if this node is not a string.</summary>
        public string AsString()
        {
            if (Kind != JsonKind.String || _text == null)
            {
                throw Fail("must be a string (found " + KindName(Kind) + ")");
            }
            return _text;
        }

        /// <summary>The boolean value. Throws if this node is not true or false.</summary>
        public bool AsBool()
        {
            if (Kind != JsonKind.Bool)
            {
                throw Fail("must be true or false (found " + KindName(Kind) + ")");
            }
            return _bool;
        }

        /// <summary>The number's original decimal text, such as "1.5". Throws if this node is not a number.</summary>
        public string NumberText
        {
            get
            {
                if (Kind != JsonKind.Number || _text == null)
                {
                    throw Fail("must be a number (found " + KindName(Kind) + ")");
                }
                return _text;
            }
        }

        /// <summary>The number as a fixed-point value, parsed from its text with no floating point. Throws if not a number or out of Fix64 range.</summary>
        public Fix64 AsFix64()
        {
            // Decimal text of the number.
            string text = NumberText;
            try
            {
                return Fix64.Parse(text);
            }
            catch (FormatException)
            {
                throw Fail("must be a decimal number with at most 9 fraction digits within the fixed-point range (found " + text + ")");
            }
        }

        /// <summary>The number as a fixed-point value that must lie in [min, max]. Throws otherwise.</summary>
        public Fix64 AsFix64(Fix64 min, Fix64 max)
        {
            // Parsed value, checked against the range below.
            Fix64 value = AsFix64();
            if (value < min || value > max)
            {
                throw Fail("must be a number from " + min + " to " + max + " (found " + NumberText + ")");
            }
            return value;
        }

        /// <summary>The number as a 32-bit integer. Throws if not a number, if it has a fraction, or if it does not fit.</summary>
        public int AsInt()
        {
            // Decimal text of the number.
            string text = NumberText;
            // Index of the first digit (after an optional minus).
            int start = text.Length > 0 && text[0] == '-' ? 1 : 0;
            // Accumulated magnitude; long is wide enough to detect int overflow before it wraps.
            long magnitude = 0;
            for (int i = start; i < text.Length; i++)
            {
                // Current character.
                char c = text[i];
                if (c < '0' || c > '9')
                {
                    throw Fail("must be a whole number (found " + text + ")");
                }
                magnitude = magnitude * 10 + (c - '0');
                if (magnitude > 2147483648L)
                {
                    throw Fail("must be a whole number that fits in 32 bits (found " + text + ")");
                }
            }
            // Signed result.
            long value = start == 1 ? -magnitude : magnitude;
            if (value > int.MaxValue)
            {
                throw Fail("must be a whole number that fits in 32 bits (found " + text + ")");
            }
            return (int)value;
        }

        /// <summary>The number as an integer that must lie in [min, max]. Throws otherwise.</summary>
        public int AsInt(int min, int max)
        {
            // Parsed value, checked against the range below.
            int value = AsInt();
            if (value < min || value > max)
            {
                throw Fail("must be a whole number from " + min + " to " + max + " (found " + value + ")");
            }
            return value;
        }

        // ---------------------------------------------------------------- containers

        /// <summary>Members by key in ordinal key order. Throws if this node is not an object.</summary>
        public IReadOnlyDictionary<string, JsonValue> Members
        {
            get
            {
                if (Kind != JsonKind.Object || _members == null)
                {
                    throw Fail("must be an object (found " + KindName(Kind) + ")");
                }
                return _members;
            }
        }

        /// <summary>Elements in file order. Throws if this node is not an array.</summary>
        public IReadOnlyList<JsonValue> Items
        {
            get
            {
                if (Kind != JsonKind.Array || _items == null)
                {
                    throw Fail("must be an array (found " + KindName(Kind) + ")");
                }
                return _items;
            }
        }

        /// <summary>Looks up an object member without failing when it is absent. Throws if this node is not an object.</summary>
        public bool TryGetMember(string key, [MaybeNullWhen(false)] out JsonValue value)
        {
            return Members.TryGetValue(key, out value);
        }

        /// <summary>The member called <paramref name="key"/>. Throws "is missing" when absent.</summary>
        public JsonValue Require(string key)
        {
            if (!Members.TryGetValue(key, out JsonValue? value))
            {
                throw FailChild(key, "is missing");
            }
            return value;
        }

        /// <summary>Required string member.</summary>
        public string RequireString(string key)
        {
            return Require(key).AsString();
        }

        /// <summary>Required string member that must also be non-empty.</summary>
        public string RequireNonEmptyString(string key)
        {
            // The string value read from the member.
            string value = Require(key).AsString();
            if (value.Length == 0)
            {
                throw FailChild(key, "must not be empty");
            }
            return value;
        }

        /// <summary>Optional string member; returns <paramref name="fallback"/> when absent.</summary>
        public string OptionalString(string key, string fallback)
        {
            return Members.TryGetValue(key, out JsonValue? value) ? value.AsString() : fallback;
        }

        /// <summary>Required boolean member.</summary>
        public bool RequireBool(string key)
        {
            return Require(key).AsBool();
        }

        /// <summary>Required whole-number member in [min, max].</summary>
        public int RequireInt(string key, int min, int max)
        {
            return Require(key).AsInt(min, max);
        }

        /// <summary>Required decimal member in [min, max], bounds written as decimal text such as "0.05".</summary>
        public Fix64 RequireFix64(string key, string min, string max)
        {
            return Require(key).AsFix64(Fix64.Parse(min), Fix64.Parse(max));
        }

        /// <summary>Required array member.</summary>
        public IReadOnlyList<JsonValue> RequireArray(string key)
        {
            return Require(key).Items;
        }

        /// <summary>
        /// Fails with "is not a known key" for the first member (in ordinal order) that is not in <paramref name="allowed"/>.
        /// "$schema_version" and "$doc" are always allowed. Throws if this node is not an object.
        /// </summary>
        /// <param name="allowed">The member names this kind of file may contain.</param>
        public void RejectUnknownKeys(params string[] allowed)
        {
            foreach (KeyValuePair<string, JsonValue> pair in Members)
            {
                if (pair.Key == DataLoader.SchemaVersionKey || pair.Key == DataLoader.DocKey)
                {
                    continue;
                }
                // True when the key is one the caller listed.
                bool known = false;
                for (int i = 0; i < allowed.Length; i++)
                {
                    if (string.Equals(allowed[i], pair.Key, StringComparison.Ordinal))
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    throw FailChild(pair.Key, "is not a known key (check the spelling against the schema)");
                }
            }
        }
    }
}
