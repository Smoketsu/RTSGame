// rts/tests/Sim.Tests/Data/JsonReaderTests.cs
// Tests for the hand-written JSON-with-comments reader: comments, escapes, nesting, exact number handling and
// line/column reporting. Part of Rts.Sim.Tests.
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Tests.Data
{
    /// <summary>JsonReader behaviour.</summary>
    [TestFixture]
    public class JsonReaderTests
    {
        /// <summary>Source name used by every test; errors must start with it.</summary>
        private const string Source = "test/file.json";

        /// <summary>Line comments, block comments and comment-like text inside strings.</summary>
        [Test]
        public void Comments_AreIgnored_ButNotInsideStrings()
        {
            // Document with every comment style, ending in a line comment without a newline.
            string text = "// header\n{\n  /* block\n     comment */ \"a\": 1, // trailing\n  \"url\": \"http://x // y /* z */\"\n}\n// end";
            JsonValue root = JsonReader.Parse(text, Source);
            Assert.That(root.Require("a").AsInt(), Is.EqualTo(1));
            Assert.That(root.Require("url").AsString(), Is.EqualTo("http://x // y /* z */"));
        }

        /// <summary>Every JSON escape decodes.</summary>
        [Test]
        public void StringEscapes_AreDecoded()
        {
            JsonValue root = JsonReader.Parse("{\"s\": \"q\\\" b\\\\ s\\/ n\\n t\\t r\\r f\\f b\\b u\\u0041\\u00e9\"}", Source);
            Assert.That(root.RequireString("s"), Is.EqualTo("q\" b\\ s/ n\n t\t r\r f\f b\b uAé"));
        }

        /// <summary>Objects and arrays nest, and nodes know their path.</summary>
        [Test]
        public void NestedStructures_ParseWithPaths()
        {
            JsonValue root = JsonReader.Parse("{\"spawns\": [ {\"x\": 1, \"y\": 2}, {\"x\": 3, \"y\": 4} ], \"empty\": {}, \"none\": [], \"flag\": true, \"gone\": null, \"off\": false}", Source);
            Assert.That(root.RequireArray("spawns").Count, Is.EqualTo(2));
            JsonValue second = root.RequireArray("spawns")[1];
            Assert.That(second.Path, Is.EqualTo("spawns[1]"));
            Assert.That(second.Require("y").Path, Is.EqualTo("spawns[1].y"));
            Assert.That(second.RequireInt("y", 0, 10), Is.EqualTo(4));
            Assert.That(root.Require("empty").Members.Count, Is.EqualTo(0));
            Assert.That(root.RequireArray("none").Count, Is.EqualTo(0));
            Assert.That(root.RequireBool("flag"), Is.True);
            Assert.That(root.RequireBool("off"), Is.False);
            Assert.That(root.Require("gone").Kind, Is.EqualTo(JsonKind.Null));
        }

        /// <summary>1.5 becomes exactly 1.5 in Q32.32 and the text is kept.</summary>
        [Test]
        public void Number_OnePointFive_ParsesToExactFix64()
        {
            JsonValue root = JsonReader.Parse("{\"n\": 1.5, \"t\": 0.040, \"neg\": -2.25}", Source);
            Assert.That(root.Require("n").AsFix64(), Is.EqualTo(Fix64.FromFraction(3, 2)));
            Assert.That(root.Require("n").AsFix64().Raw, Is.EqualTo(3L << 31));
            Assert.That(root.Require("t").NumberText, Is.EqualTo("0.040"));
            Assert.That(root.Require("t").AsFix64(), Is.EqualTo(Fix64.Parse("0.04")));
            Assert.That(root.Require("neg").AsFix64(), Is.EqualTo(Fix64.FromFraction(-9, 4)));
        }

        /// <summary>Integer accessor accepts whole numbers only.</summary>
        [Test]
        public void AsInt_RejectsFractionsAndOverflow()
        {
            JsonValue root = JsonReader.Parse("{\"a\": -7, \"b\": 1.5, \"c\": 3000000000, \"d\": 2147483647, \"e\": -2147483648}", Source);
            Assert.That(root.Require("a").AsInt(), Is.EqualTo(-7));
            Assert.That(root.Require("d").AsInt(), Is.EqualTo(int.MaxValue));
            Assert.That(root.Require("e").AsInt(), Is.EqualTo(int.MinValue));
            DataValidationException fraction = Expect.Throws<DataValidationException>(() => root.Require("b").AsInt());
            Assert.That(fraction.Message, Does.StartWith(Source + ": field 'b'"));
            Expect.Throws<DataValidationException>(() => root.Require("c").AsInt());
        }

        /// <summary>Exponent notation is not part of the dialect.</summary>
        [TestCase("{\"n\": 1e5}")]
        [TestCase("{\"n\": 1.5E3}")]
        [TestCase("{\"n\": 2e-2}")]
        public void Exponent_IsRejected(string text)
        {
            JsonParseException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse(text, Source));
            Assert.That(ex.Message, Does.Contain("exponent"));
            Assert.That(ex.Message, Does.StartWith(Source + ": "));
        }

        /// <summary>A syntax error reports the exact line and column.</summary>
        [Test]
        public void SyntaxError_ReportsLineAndColumn()
        {
            // The question mark is on line 3, column 8.
            JsonParseException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse("{\n  \"a\": 1,\n  \"b\": ?\n}", Source));
            Assert.That(ex.Line, Is.EqualTo(3));
            Assert.That(ex.Column, Is.EqualTo(8));
            Assert.That(ex.Message, Is.EqualTo(Source + ": line 3, column 8: unexpected '?'; expected a value"));
        }

        /// <summary>The parse exception is a DataValidationException, so callers need only one catch.</summary>
        [Test]
        public void ParseException_IsADataValidationException()
        {
            DataValidationException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse("{", Source));
            Assert.That(ex.File, Is.EqualTo(Source));
        }

        /// <summary>Malformed documents all fail with a message that names the source.</summary>
        [TestCase("")]
        [TestCase("   // only a comment")]
        [TestCase("{\"a\": 1,}")]
        [TestCase("{a: 1}")]
        [TestCase("{\"a\": 1 \"b\": 2}")]
        [TestCase("{\"a\" 1}")]
        [TestCase("[1, 2,]")]
        [TestCase("[1 2]")]
        [TestCase("{\"a\": 1} extra")]
        [TestCase("{\"a\": \"unterminated}")]
        [TestCase("{\"a\": \"line\nbreak\"}")]
        [TestCase("{\"a\": \"bad \\q escape\"}")]
        [TestCase("{\"a\": \"bad \\u12 escape\"}")]
        [TestCase("{\"a\": 01}")]
        [TestCase("{\"a\": 1.}")]
        [TestCase("{\"a\": -}")]
        [TestCase("{\"a\": +1}")]
        [TestCase("{\"a\": .5}")]
        [TestCase("{\"a\": 1, \"a\": 2}")]
        [TestCase("{\"a\": 1 /* never closed")]
        [TestCase("{\"a\": 1 / 2}")]
        [TestCase("{\"a\": tru}")]
        [TestCase("{\"a\": nul}")]
        [TestCase("{\"a\": trueish}")]
        public void Malformed_Throws(string text)
        {
            JsonParseException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse(text, Source));
            Assert.That(ex.Message, Does.StartWith(Source + ": line "));
            Assert.That(ex.Line, Is.GreaterThanOrEqualTo(1));
            Assert.That(ex.Column, Is.GreaterThanOrEqualTo(1));
        }

        /// <summary>The duplicate key error points at the second occurrence.</summary>
        [Test]
        public void DuplicateKey_PointsAtSecondOccurrence()
        {
            JsonParseException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse("{\n  \"a\": 1,\n  \"a\": 2\n}", Source));
            Assert.That(ex.Line, Is.EqualTo(3));
            Assert.That(ex.Column, Is.EqualTo(3));
            Assert.That(ex.Message, Does.Contain("duplicate key 'a'"));
        }

        /// <summary>A byte-order mark at the start of a file is ignored.</summary>
        [Test]
        public void ByteOrderMark_IsIgnored()
        {
            JsonValue root = JsonReader.Parse("﻿{\"a\": 1}", Source);
            Assert.That(root.RequireInt("a", 0, 5), Is.EqualTo(1));
        }

        /// <summary>Hostile nesting is refused instead of overflowing the stack.</summary>
        [Test]
        public void DeepNesting_IsRefused()
        {
            string text = new string('[', 200) + new string(']', 200);
            JsonParseException ex = Expect.Throws<JsonParseException>(() => JsonReader.Parse(text, Source));
            Assert.That(ex.Message, Does.Contain("nesting"));
        }

        /// <summary>Accessor errors name the file and the field path.</summary>
        [Test]
        public void AccessorErrors_NameFileAndField()
        {
            JsonValue root = JsonReader.Parse("{\"outer\": {\"inner\": \"text\"}}", Source);
            DataValidationException wrongType = Expect.Throws<DataValidationException>(() => root.Require("outer").Require("inner").AsInt());
            Assert.That(wrongType.Message, Does.StartWith(Source + ": field 'outer.inner' must be a number"));
            DataValidationException missing = Expect.Throws<DataValidationException>(() => root.Require("outer").Require("nope"));
            Assert.That(missing.Message, Is.EqualTo(Source + ": field 'outer.nope' is missing"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => root.RejectUnknownKeys("other"));
            Assert.That(unknown.Message, Does.StartWith(Source + ": field 'outer' is not a known key"));
        }
    }
}
