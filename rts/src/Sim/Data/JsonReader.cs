// rts/src/Sim/Data/JsonReader.cs
// Hand-written parser for the data-file dialect of JSON (ADR-002): standard JSON plus // line comments and
// /* block */ comments, and nothing else. Part of Rts.Sim.Data. No trailing commas, no unquoted keys, no exponent
// numbers, no duplicate keys. Numbers are kept as decimal text. Syntax errors carry the source name, line and column.
// The reader never touches the file system: the host passes the text in.
using System;
using System.Collections.Generic;
using System.Text;

namespace Rts.Sim.Data
{
    /// <summary>A JSON syntax error. Message: "file: line L, column C: what went wrong". The field is always empty.</summary>
    public sealed class JsonParseException : DataValidationException
    {
        /// <summary>Line of the error. Unit: lines, 1-based.</summary>
        public int Line { get; }

        /// <summary>Column of the error. Unit: characters, 1-based.</summary>
        public int Column { get; }

        /// <summary>Builds the error.</summary>
        /// <param name="sourceName">File name or source name.</param>
        /// <param name="line">1-based line.</param>
        /// <param name="column">1-based column.</param>
        /// <param name="problem">What is wrong, as a sentence fragment.</param>
        public JsonParseException(string sourceName, int line, int column, string problem)
            : base(sourceName, string.Empty, "line " + line + ", column " + column + ": " + problem)
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>Parser for data-file JSON. Use <see cref="Parse"/>; instances are internal to one parse.</summary>
    public sealed class JsonReader
    {
        /// <summary>Deepest allowed nesting of objects and arrays. Unit: levels. Guards against stack overflow on hostile text.</summary>
        private const int MaxDepth = 64;

        /// <summary>The text being parsed.</summary>
        private readonly string _text;

        /// <summary>Source name for error messages.</summary>
        private readonly string _source;

        /// <summary>Index of the next unread character. Unit: characters. Range: 0 to text length.</summary>
        private int _pos;

        /// <summary>Line of the next unread character. Unit: lines, 1-based.</summary>
        private int _line = 1;

        /// <summary>Column of the next unread character. Unit: characters, 1-based.</summary>
        private int _column = 1;

        /// <summary>Creates a reader positioned at the start of the text.</summary>
        private JsonReader(string text, string sourceName)
        {
            _text = text;
            _source = sourceName;
        }

        /// <summary>Parses one JSON document with comments. Throws <see cref="JsonParseException"/> on any syntax error.</summary>
        /// <param name="text">The whole file as text. A leading byte-order mark is ignored.</param>
        /// <param name="sourceName">Name used in error messages, normally the data-root-relative file path.</param>
        public static JsonValue Parse(string text, string sourceName)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (string.IsNullOrEmpty(sourceName))
            {
                throw new ArgumentException("A source name is needed for error messages.", nameof(sourceName));
            }
            JsonReader reader = new JsonReader(text, sourceName);
            if (text.Length > 0 && text[0] == '﻿')
            {
                reader._pos = 1;
            }
            reader.SkipTrivia();
            if (reader.AtEnd)
            {
                throw reader.Error("the file is empty; expected a JSON object");
            }
            // The document's single root value.
            JsonValue root = reader.ParseValue(string.Empty, 0);
            reader.SkipTrivia();
            if (!reader.AtEnd)
            {
                throw reader.Error("unexpected '" + reader.Peek() + "' after the end of the document");
            }
            return root;
        }

        // ---------------------------------------------------------------- cursor helpers

        /// <summary>True when every character has been read.</summary>
        private bool AtEnd => _pos >= _text.Length;

        /// <summary>The next unread character; only valid when not at the end.</summary>
        private char Peek()
        {
            return _text[_pos];
        }

        /// <summary>Reads one character and advances the line and column counters.</summary>
        private char Next()
        {
            // The character being consumed.
            char c = _text[_pos++];
            if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            return c;
        }

        /// <summary>Builds a syntax error at the current position.</summary>
        private JsonParseException Error(string problem)
        {
            return new JsonParseException(_source, _line, _column, problem);
        }

        /// <summary>Builds a syntax error at a remembered earlier position.</summary>
        private JsonParseException ErrorAt(int line, int column, string problem)
        {
            return new JsonParseException(_source, line, column, problem);
        }

        /// <summary>Skips whitespace, // comments and /* */ comments.</summary>
        private void SkipTrivia()
        {
            while (!AtEnd)
            {
                // Next character to classify.
                char c = Peek();
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    Next();
                }
                else if (c == '/')
                {
                    // Position of the slash, for the unterminated-comment message.
                    int startLine = _line;
                    int startColumn = _column;
                    Next();
                    if (AtEnd)
                    {
                        throw ErrorAt(startLine, startColumn, "a lone '/' is not valid; comments start with // or /*");
                    }
                    // Character after the slash.
                    char second = Peek();
                    if (second == '/')
                    {
                        while (!AtEnd && Peek() != '\n')
                        {
                            Next();
                        }
                    }
                    else if (second == '*')
                    {
                        Next();
                        // True once the closing */ has been consumed.
                        bool closed = false;
                        while (!AtEnd)
                        {
                            if (Next() == '*' && !AtEnd && Peek() == '/')
                            {
                                Next();
                                closed = true;
                                break;
                            }
                        }
                        if (!closed)
                        {
                            throw ErrorAt(startLine, startColumn, "this /* comment is never closed with */");
                        }
                    }
                    else
                    {
                        throw ErrorAt(startLine, startColumn, "a lone '/' is not valid; comments start with // or /*");
                    }
                }
                else
                {
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- grammar

        /// <summary>Parses any value at the cursor.</summary>
        /// <param name="path">Path of the value inside the file, for error messages on the tree.</param>
        /// <param name="depth">Current nesting depth. Range: 0 to MaxDepth.</param>
        private JsonValue ParseValue(string path, int depth)
        {
            if (AtEnd)
            {
                throw Error("unexpected end of file; expected a value");
            }
            // Start of the value, recorded on the node.
            int line = _line;
            int column = _column;
            // First character decides the kind.
            char c = Peek();
            switch (c)
            {
                case '{':
                    return ParseObject(path, depth, line, column);
                case '[':
                    return ParseArray(path, depth, line, column);
                case '"':
                    return JsonValue.CreateString(_source, path, line, column, ParseString());
                case 't':
                    ExpectWord("true");
                    return JsonValue.CreateBool(_source, path, line, column, true);
                case 'f':
                    ExpectWord("false");
                    return JsonValue.CreateBool(_source, path, line, column, false);
                case 'n':
                    ExpectWord("null");
                    return JsonValue.CreateNull(_source, path, line, column);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return JsonValue.CreateNumber(_source, path, line, column, ParseNumber());
                    }
                    throw Error("unexpected '" + c + "'; expected a value");
            }
        }

        /// <summary>Consumes a literal word such as "true", failing if the text does not match.</summary>
        private void ExpectWord(string word)
        {
            // Position of the word's first letter.
            int startLine = _line;
            int startColumn = _column;
            for (int i = 0; i < word.Length; i++)
            {
                if (AtEnd || Peek() != word[i])
                {
                    throw ErrorAt(startLine, startColumn, "expected '" + word + "'");
                }
                Next();
            }
            // A word must not run into more letters or digits ("trueish").
            if (!AtEnd && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            {
                throw ErrorAt(startLine, startColumn, "expected '" + word + "'");
            }
        }

        /// <summary>Parses an object. The cursor is on the opening brace.</summary>
        private JsonValue ParseObject(string path, int depth, int line, int column)
        {
            if (depth >= MaxDepth)
            {
                throw Error("nesting is deeper than " + MaxDepth + " levels");
            }
            Next();
            // Members collected so far, ordinal key order.
            SortedDictionary<string, JsonValue> members = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);
            SkipTrivia();
            if (!AtEnd && Peek() == '}')
            {
                Next();
                return JsonValue.CreateObject(_source, path, line, column, members);
            }
            while (true)
            {
                SkipTrivia();
                if (AtEnd)
                {
                    throw Error("unexpected end of file inside an object; expected a key or '}'");
                }
                if (Peek() != '"')
                {
                    throw Error("expected a quoted key but found '" + Peek() + "' (trailing commas and unquoted keys are not allowed)");
                }
                // Position of the key, for the duplicate-key message.
                int keyLine = _line;
                int keyColumn = _column;
                string key = ParseString();
                if (members.ContainsKey(key))
                {
                    throw ErrorAt(keyLine, keyColumn, "duplicate key '" + key + "'");
                }
                SkipTrivia();
                if (AtEnd || Peek() != ':')
                {
                    throw Error("expected ':' after key '" + key + "'");
                }
                Next();
                SkipTrivia();
                // Path of the member value.
                string childPath = path.Length == 0 ? key : path + "." + key;
                members.Add(key, ParseValue(childPath, depth + 1));
                SkipTrivia();
                if (AtEnd)
                {
                    throw Error("unexpected end of file inside an object; expected ',' or '}'");
                }
                // Separator or terminator.
                char next = Next();
                if (next == '}')
                {
                    return JsonValue.CreateObject(_source, path, line, column, members);
                }
                if (next != ',')
                {
                    throw ErrorAt(_line, _column - 1, "expected ',' or '}' but found '" + next + "'");
                }
            }
        }

        /// <summary>Parses an array. The cursor is on the opening bracket.</summary>
        private JsonValue ParseArray(string path, int depth, int line, int column)
        {
            if (depth >= MaxDepth)
            {
                throw Error("nesting is deeper than " + MaxDepth + " levels");
            }
            Next();
            // Elements collected so far, file order.
            List<JsonValue> items = new List<JsonValue>();
            SkipTrivia();
            if (!AtEnd && Peek() == ']')
            {
                Next();
                return JsonValue.CreateArray(_source, path, line, column, items);
            }
            while (true)
            {
                SkipTrivia();
                items.Add(ParseValue(path + "[" + items.Count + "]", depth + 1));
                SkipTrivia();
                if (AtEnd)
                {
                    throw Error("unexpected end of file inside an array; expected ',' or ']'");
                }
                // Separator or terminator.
                char next = Next();
                if (next == ']')
                {
                    return JsonValue.CreateArray(_source, path, line, column, items);
                }
                if (next != ',')
                {
                    throw ErrorAt(_line, _column - 1, "expected ',' or ']' but found '" + next + "'");
                }
            }
        }

        /// <summary>Parses a quoted string and decodes its escapes. The cursor is on the opening quote.</summary>
        private string ParseString()
        {
            // Position of the opening quote, for the unterminated-string message.
            int startLine = _line;
            int startColumn = _column;
            Next();
            // Decoded characters.
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                {
                    throw ErrorAt(startLine, startColumn, "this string is never closed with a quote");
                }
                // Position of the character about to be read, for escape errors.
                int charLine = _line;
                int charColumn = _column;
                char c = Next();
                if (c == '"')
                {
                    return builder.ToString();
                }
                if (c == '\n' || c == '\r')
                {
                    throw ErrorAt(startLine, startColumn, "this string is not closed before the end of the line");
                }
                if (c < ' ')
                {
                    throw ErrorAt(charLine, charColumn, "control character U+" + ((int)c).ToString("X4") + " must be escaped inside a string");
                }
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }
                if (AtEnd)
                {
                    throw ErrorAt(startLine, startColumn, "this string is never closed with a quote");
                }
                // The escape letter after the backslash.
                char escape = Next();
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        // UTF-16 code unit from four hex digits; surrogate pairs arrive as two \u escapes.
                        int code = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (AtEnd)
                            {
                                throw ErrorAt(charLine, charColumn, "\\u needs four hex digits");
                            }
                            // Hex digit value, or -1 if the character is not one.
                            int digit = HexValue(Peek());
                            if (digit < 0)
                            {
                                throw ErrorAt(charLine, charColumn, "\\u needs four hex digits");
                            }
                            Next();
                            code = code * 16 + digit;
                        }
                        builder.Append((char)code);
                        break;
                    default:
                        throw ErrorAt(charLine, charColumn, "unknown escape '\\" + escape + "'");
                }
            }
        }

        /// <summary>Value 0 to 15 of a hex digit, or -1 for any other character.</summary>
        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            return -1;
        }

        /// <summary>
        /// Parses a number: optional minus, digits without a leading zero, optional '.' and digits. Exponents are
        /// rejected. Returns the exact source text.
        /// </summary>
        private string ParseNumber()
        {
            // Position of the first character, for error messages.
            int startLine = _line;
            int startColumn = _column;
            // Index where the number text starts.
            int start = _pos;
            if (Peek() == '-')
            {
                Next();
            }
            if (AtEnd || Peek() < '0' || Peek() > '9')
            {
                throw ErrorAt(startLine, startColumn, "a number needs a digit after the minus sign");
            }
            // First digit of the whole part.
            char first = Next();
            if (first == '0' && !AtEnd && Peek() >= '0' && Peek() <= '9')
            {
                throw ErrorAt(startLine, startColumn, "numbers must not have leading zeros");
            }
            while (!AtEnd && Peek() >= '0' && Peek() <= '9')
            {
                Next();
            }
            if (!AtEnd && Peek() == '.')
            {
                Next();
                if (AtEnd || Peek() < '0' || Peek() > '9')
                {
                    throw ErrorAt(startLine, startColumn, "a number needs a digit after the decimal point");
                }
                while (!AtEnd && Peek() >= '0' && Peek() <= '9')
                {
                    Next();
                }
            }
            if (!AtEnd && (Peek() == 'e' || Peek() == 'E'))
            {
                throw Error("exponent notation is not allowed in data files; write the number out in full");
            }
            return _text.Substring(start, _pos - start);
        }
    }
}
