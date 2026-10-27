// rts/tests/Sim.Tests/Commands/CommandCodecTests.cs
// Tests of the shared command codec (Rts.Sim.Commands.Codec, ADR-008 and ADR-009): every concrete Command subclass found by reflection
// over the simulation assembly has a codec and round-trips field for field (a new command without a codec fails here), the
// byte layout of a few commands is pinned, the string rules (strict UTF-8, uint16 byte length, at most 65,535 bytes, no lone
// surrogates, invalid UTF-8 rejected on read), and the registry rules (duplicates, ordinal iteration, errors that name the kind or
// type). Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Sim.Tests
{
    /// <summary>The shared command codec: coverage, round trips, string rules and registry rules.</summary>
    [TestFixture]
    public sealed class CommandCodecTests
    {
        /// <summary>Player slot 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>A position with fractional components, so Fix64 raw bits are exercised. Unit: tiles.</summary>
        private static readonly FixVec2 Spot = new FixVec2(Fix64.Parse("12.75"), Fix64.Parse("-3.5"));

        /// <summary>The registry under test: core codecs plus the built-in faction codecs.</summary>
        private static CommandCodecRegistry Registry => BuiltInMechanics.CreateCodecRegistry();

        /// <summary>One sample of every concrete command class in the simulation assembly, with distinctive field values.</summary>
        private static List<Command> Samples()
        {
            return new List<Command>
            {
                new NoOpCommand(7, P0, 1),
                new SpawnUnitCommand(8, PlayerId.None, 2, "skai.windrunner", P1, Spot),
                new MoveUnitCommand(9, P0, 3, new EntityId(42), Spot),
                new RemoveUnitCommand(10, P1, 4, new EntityId(43)),
                new AttackMoveCommand(11, P0, 5, new EntityId(44), Spot),
                new AttackUnitCommand(12, P0, 6, new EntityId(45), new EntityId(46)),
                new StopCommand(13, P1, 7, new EntityId(47)),
                new RootHqCommand(14, P0, 8, new EntityId(48)),
                new UprootHqCommand(15, P1, 9, new EntityId(49)),
                new PickCycleCommand(15, P0, 9, "quake"),
                new GatherCommand(16, P0, 10, new EntityId(50), -3),
                new TrainUnitCommand(17, P1, 11, new EntityId(51), "water.tender"),
                new BuildStructureCommand(18, P0, 12, new EntityId(52), "skai.roost", 21, -34),
                new ApplyStatusCommand(19, PlayerId.None, 13, new EntityId(53), StatusKind.Slowed, Fix64.Parse("0.6"), Fix64.Parse("2.5"), "test.slow"),
                new SurrenderCommand(20, P1, 14, P1),
                new SetConnectionStateCommand(21, PlayerId.None, 15, P0, ConnectionState.Disconnected),
                new CastWaveCommand(22, P1, 16, new EntityId(54), Spot),
            };
        }

        /// <summary>Hash of a command's scheduling fields and parameters, so two commands can be compared field by field.</summary>
        private static ulong Fingerprint(Command command)
        {
            // Hasher fed the scheduling fields then the command's own parameters.
            StateHasher hasher = new StateHasher();
            hasher.Write(command.Tick);
            hasher.Write(command.Issuer);
            hasher.Write(command.Sequence);
            command.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>Lower-case hex of a byte array, for readable layout assertions.</summary>
        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>Every concrete Command subclass in the simulation assembly has a registered codec and a sample here; a new command without either fails this test.</summary>
        [Test]
        public void EveryCommandSubclass_HasACodecAndASample()
        {
            // Concrete command classes found by reflection over the simulation assembly, by full name.
            SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            foreach (Type type in typeof(Command).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(Command).IsAssignableFrom(type))
                {
                    continue;
                }
                found.Add(type.FullName ?? type.Name);
                Assert.That(registry.TryGetByType(type, out ICommandCodec? codec), Is.True, "no codec registered for " + type.FullName);
                Assert.That(codec!.CommandType, Is.EqualTo(type));
            }
            // Command classes the samples cover, by full name.
            SortedSet<string> sampled = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Command sample in Samples())
            {
                sampled.Add(sample.GetType().FullName ?? sample.GetType().Name);
            }
            Assert.That(sampled, Is.EquivalentTo(found), "every Command subclass needs a sample in Samples()");
            Assert.That(registry.Count, Is.EqualTo(found.Count), "one codec per command class, none for a class that does not exist");
        }

        /// <summary>Every command round-trips through its payload with the same class, kind, scheduling fields and parameters.</summary>
        [Test]
        public void EveryCommand_RoundTripsThroughItsPayload()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            foreach (Command sample in Samples())
            {
                // Payload bytes, then the command rebuilt from the framing fields the caller kept.
                byte[] payload = registry.EncodePayload(sample);
                Command decoded = registry.DecodePayload(sample.Kind, sample.Tick, sample.Issuer, sample.Sequence, payload);
                Assert.That(decoded.GetType(), Is.EqualTo(sample.GetType()), sample.Kind);
                Assert.That(decoded.Kind, Is.EqualTo(sample.Kind));
                Assert.That(Fingerprint(decoded), Is.EqualTo(Fingerprint(sample)), sample.Kind + " fields survive");
                Assert.That(Hex(registry.EncodePayload(decoded)), Is.EqualTo(Hex(payload)), sample.Kind + " re-encodes to the same bytes");
            }
        }

        /// <summary>The core registry names no faction: the Water cast_wave codec is added only by the built-in registry.</summary>
        [Test]
        public void CoreCodecs_NameNoFaction_AndBuiltInAddsCastWave()
        {
            // A registry holding only the core codecs.
            CommandCodecRegistry core = new CommandCodecRegistry();
            CoreCommandCodecs.RegisterAll(core);
            Assert.That(core.Contains(CastWaveCommand.KindName), Is.False);
            Assert.That(core.Count, Is.EqualTo(16));
            CommandCodecException ex = Expect.Throws<CommandCodecException>(() => core.EncodePayload(new CastWaveCommand(1, P1, 1, new EntityId(1), Spot)));
            Assert.That(ex.Message, Does.Contain("CastWaveCommand"), "the error names the command type");
            // The built-in registry is core plus the faction codecs.
            CommandCodecRegistry builtIn = Registry;
            Assert.That(builtIn.Contains(CastWaveCommand.KindName), Is.True);
            Assert.That(builtIn.Count, Is.EqualTo(17));
        }

        /// <summary>The byte layout keeps the field order the network codecs had: pinned for move_unit, spawn_unit and apply_status.</summary>
        [Test]
        public void ByteLayout_IsPinned()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            // move_unit: unit u32 (42), target x raw i64 (1 tile = 2^32), target y raw i64 (2 tiles = 2^33).
            Command move = new MoveUnitCommand(1, P0, 1, new EntityId(42), new FixVec2(Fix64.FromInt(1), Fix64.FromInt(2)));
            Assert.That(Hex(registry.EncodePayload(move)), Is.EqualTo("2a000000" + "0000000001000000" + "0000000002000000"));
            // spawn_unit: definition (u16 byte length 2, "ab"), owner u8 (1), position (0, 0).
            Command spawn = new SpawnUnitCommand(1, P0, 1, "ab", P1, new FixVec2(Fix64.FromInt(0), Fix64.FromInt(0)));
            Assert.That(Hex(registry.EncodePayload(spawn)), Is.EqualTo("0200" + "6162" + "01" + "0000000000000000" + "0000000000000000"));
            // apply_status: unit u32 (5), status u8 (1 = slowed), multiplier raw (1), duration raw (2), source (length 1, "s").
            Command status = new ApplyStatusCommand(1, P0, 1, new EntityId(5), StatusKind.Slowed, Fix64.FromInt(1), Fix64.FromInt(2), "s");
            Assert.That(Hex(registry.EncodePayload(status)), Is.EqualTo("05000000" + "01" + "0000000001000000" + "0000000002000000" + "0100" + "73"));
        }

        /// <summary>A string at the limit of 65,535 UTF-8 bytes round-trips; one byte more is refused when writing; multi-byte characters are counted in bytes.</summary>
        [Test]
        public void Strings_MaximumLengthAndOverLength()
        {
            // The longest legal string, 65,535 one-byte characters.
            string atLimit = new string('a', CommandWriter.MaxStringBytes);
            CommandWriter writer = new CommandWriter();
            writer.WriteString(atLimit);
            Assert.That(writer.Length, Is.EqualTo(2 + 65535));
            Assert.That(new CommandReader(writer.ToArray()).ReadString(), Is.EqualTo(atLimit));
            // One byte over the limit.
            CommandCodecException over = Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString(new string('a', 65536)));
            Assert.That(over.Message, Does.Contain("65535"));
            // Three-byte characters: 21,845 fit exactly (65,535 bytes), 21,846 do not although they are far fewer than 65,535 characters.
            string euros = new string('€', 21845);
            CommandWriter euroWriter = new CommandWriter();
            euroWriter.WriteString(euros);
            Assert.That(new CommandReader(euroWriter.ToArray()).ReadString(), Is.EqualTo(euros));
            Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString(new string('€', 21846)));
            // A failed write leaves the buffer untouched.
            CommandWriter untouched = new CommandWriter();
            untouched.WriteByte(9);
            Expect.Throws<CommandCodecException>(() => untouched.WriteString(new string('a', 70000)));
            Assert.That(untouched.Length, Is.EqualTo(1));
        }

        /// <summary>Lone surrogates (high or low, alone or misordered) and null are refused when writing.</summary>
        [Test]
        public void Strings_LoneSurrogatesAndNullAreRejected()
        {
            Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString("a\uD800"));
            Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString("\uDC00b"));
            Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString("\uDC00\uD800"));
            Expect.Throws<CommandCodecException>(() => new CommandWriter().WriteString(null!));
            // A correctly paired surrogate (U+1D11E, four UTF-8 bytes) is fine.
            CommandWriter pair = new CommandWriter();
            pair.WriteString("𝄞");
            Assert.That(Hex(pair.ToArray()), Is.EqualTo("0400" + "f09d849e"));
        }

        /// <summary>Non-ASCII text, the empty string and an astral character survive a round trip, and the bytes are UTF-8.</summary>
        [Test]
        public void Strings_NonAsciiRoundTrips()
        {
            // Samples: accents, a snowman, an astral clef, empty.
            string[] samples = { "Ünïcode ☃", "𝄞 clef", string.Empty, "plain" };
            foreach (string sample in samples)
            {
                CommandWriter writer = new CommandWriter();
                writer.WriteString(sample);
                writer.WriteByte(0x5A);
                // Reader over the bytes; the string must not swallow the byte after it.
                CommandReader reader = new CommandReader(writer.ToArray());
                Assert.That(reader.ReadString(), Is.EqualTo(sample));
                Assert.That(reader.ReadByte(), Is.EqualTo(0x5A));
                reader.RequireEnd();
            }
            // "Ü" is two bytes in UTF-8 (c3 9c), not one UTF-16 unit.
            CommandWriter accent = new CommandWriter();
            accent.WriteString("Ü");
            Assert.That(Hex(accent.ToArray()), Is.EqualTo("0200" + "c39c"));
            // A command carrying non-ASCII text round-trips through the registry.
            Command train = new TrainUnitCommand(3, P0, 1, new EntityId(9), "wäter.☃");
            Command copy = Registry.DecodePayload(train.Kind, 3, P0, 1, Registry.EncodePayload(train));
            Assert.That(((TrainUnitCommand)copy).DefinitionId, Is.EqualTo("wäter.☃"));
        }

        /// <summary>The reader rejects invalid UTF-8 (a stray byte, an overlong form, an encoded surrogate) and lengths beyond the payload.</summary>
        [Test]
        public void Strings_InvalidBytesAreRejectedOnRead()
        {
            // Each case is a uint16 length then the bytes: 0xFF, overlong "/" (c0 af), UTF-8 encoded surrogate (ed a0 80), truncated 3-byte sequence (e2 82).
            byte[][] bad =
            {
                new byte[] { 1, 0, 0xFF },
                new byte[] { 2, 0, 0xC0, 0xAF },
                new byte[] { 3, 0, 0xED, 0xA0, 0x80 },
                new byte[] { 2, 0, 0xE2, 0x82 },
            };
            foreach (byte[] bytes in bad)
            {
                CommandCodecException ex = Expect.Throws<CommandCodecException>(() => new CommandReader(bytes).ReadString());
                Assert.That(ex.Message, Does.Contain("UTF-8"));
            }
            // Length 10 with only 3 bytes behind it: truncated, not a giant allocation or an over-read.
            CommandCodecException truncated = Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[] { 10, 0, 0x61, 0x62, 0x63 }).ReadString());
            Assert.That(truncated.Message, Does.Contain("truncated"));
            // Length 65535 with nothing behind it.
            Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[] { 0xFF, 0xFF }).ReadString());
        }

        /// <summary>Truncated primitives and trailing bytes are codec errors, never an index error.</summary>
        [Test]
        public void Reader_TruncationAndTrailingBytes()
        {
            Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[0]).ReadByte());
            Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[3]).ReadUInt32());
            Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[7]).ReadFix64());
            Expect.Throws<CommandCodecException>(() => new CommandReader(new byte[15]).ReadFixVec2());
            // One byte left over after a one-byte read.
            CommandReader reader = new CommandReader(new byte[] { 1, 2 });
            reader.ReadByte();
            Assert.That(reader.Remaining, Is.EqualTo(1));
            Expect.Throws<CommandCodecException>(() => reader.RequireEnd());
            // A whole command payload with an extra byte is refused, naming the kind.
            byte[] payload = Registry.EncodePayload(new StopCommand(1, P0, 1, new EntityId(3)));
            byte[] longer = new byte[payload.Length + 1];
            Array.Copy(payload, longer, payload.Length);
            CommandCodecException trailing = Expect.Throws<CommandCodecException>(() => Registry.DecodePayload("stop", 1, P0, 1, longer));
            Assert.That(trailing.Message, Does.Contain("stop"));
            Assert.That(trailing.Message, Does.Contain("trailing"));
        }

        /// <summary>Primitives write and read little-endian values including negative and extreme ones.</summary>
        [Test]
        public void Primitives_RoundTrip()
        {
            // The buffer under test.
            CommandWriter writer = new CommandWriter();
            writer.WriteByte(200);
            writer.WriteUInt16(65535);
            writer.WriteInt32(-123456);
            writer.WriteUInt32(4000000000u);
            writer.WriteInt64(long.MinValue);
            writer.WriteUInt64(ulong.MaxValue);
            writer.WriteFix64(Fix64.Parse("-0.000000001"));
            writer.WriteFixVec2(Spot);
            writer.WriteEntityId(new EntityId(7));
            writer.WritePlayerId(PlayerId.None);
            // Explicit little-endian check of the first values: 200, then ffff, then -123456 = 0xfffe1dc0.
            Assert.That(Hex(writer.ToArray()).Substring(0, 2 + 4 + 8), Is.EqualTo("c8" + "ffff" + "c01dfeff"));
            // The reader over the written bytes.
            CommandReader reader = new CommandReader(writer.ToArray());
            Assert.That(reader.ReadByte(), Is.EqualTo(200));
            Assert.That(reader.ReadUInt16(), Is.EqualTo(65535));
            Assert.That(reader.ReadInt32(), Is.EqualTo(-123456));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(4000000000u));
            Assert.That(reader.ReadInt64(), Is.EqualTo(long.MinValue));
            Assert.That(reader.ReadUInt64(), Is.EqualTo(ulong.MaxValue));
            Assert.That(reader.ReadFix64(), Is.EqualTo(Fix64.Parse("-0.000000001")));
            Assert.That(reader.ReadFixVec2(), Is.EqualTo(Spot));
            Assert.That(reader.ReadEntityId(), Is.EqualTo(new EntityId(7)));
            Assert.That(reader.ReadPlayerId(), Is.EqualTo(PlayerId.None));
            reader.RequireEnd();
        }

        /// <summary>An unknown kind, a bad enum byte and a field the command constructor rejects are codec errors that name the kind.</summary>
        [Test]
        public void Decode_BadKindEnumAndFields()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            CommandCodecException unknown = Expect.Throws<CommandCodecException>(() => registry.DecodePayload("teleport", 1, P0, 1, new byte[0]));
            Assert.That(unknown.Message, Does.Contain("teleport"));
            // apply_status with status byte 9.
            byte[] status = registry.EncodePayload(new ApplyStatusCommand(1, P0, 1, new EntityId(5), StatusKind.Slowed, Fix64.FromInt(1), Fix64.FromInt(2), "s"));
            status[4] = 9;
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.DecodePayload("apply_status", 1, P0, 1, status)).Message, Does.Contain("apply_status"));
            // set_connection_state with state byte 7.
            byte[] connection = registry.EncodePayload(new SetConnectionStateCommand(1, P0, 1, P1, ConnectionState.Connected));
            connection[1] = 7;
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.DecodePayload("set_connection_state", 1, P0, 1, connection)).Message, Does.Contain("set_connection_state"));
            // pick_cycle with an empty cycle id: the constructor's ArgumentException becomes a codec error.
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.DecodePayload("pick_cycle", 1, P0, 1, new byte[] { 0, 0 })).Message, Does.Contain("pick_cycle"));
            // A negative tick from the framing is rejected by the command constructor, also as a codec error.
            Expect.Throws<CommandCodecException>(() => registry.DecodePayload("noop", -1, P0, 1, new byte[0]));
        }

        /// <summary>Registering a second codec for a kind or for a command class throws; bad codecs are refused; errors name the kind or type.</summary>
        [Test]
        public void Registry_DuplicatesAndBadCodecs()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            InvalidOperationException sameKind = Expect.Throws<InvalidOperationException>(() => registry.Register(new SameKindCodec()));
            Assert.That(sameKind.Message, Does.Contain("noop"));
            InvalidOperationException sameType = Expect.Throws<InvalidOperationException>(() => registry.Register(new SameTypeCodec()));
            Assert.That(sameType.Message, Does.Contain("NoOpCommand"));
            Expect.Throws<InvalidOperationException>(() => registry.Register(new CastWaveCodec()));
            Expect.Throws<InvalidOperationException>(() => CoreCommandCodecs.RegisterAll(registry));
            Expect.Throws<ArgumentNullException>(() => registry.Register(null!));
            Expect.Throws<ArgumentException>(() => registry.Register(new AbstractTypeCodec()));
            // A failed registration leaves the registry as it was.
            Assert.That(registry.Count, Is.EqualTo(17));
            Assert.That(registry.Contains("noop_again"), Is.False);
            // Lookup failures name the kind and the type.
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.GetByKind("warp")).Message, Does.Contain("warp"));
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.GetByType(typeof(SameTypeCodec))).Message, Does.Contain("SameTypeCodec"));
            Assert.That(registry.GetByKind("move_unit").CommandType, Is.EqualTo(typeof(MoveUnitCommand)));
            Assert.That(registry.GetByType(typeof(MoveUnitCommand)).Kind, Is.EqualTo("move_unit"));
            // Handing a codec the wrong command class is a codec error naming the codec.
            Assert.That(Expect.Throws<CommandCodecException>(() => registry.GetByKind("move_unit").Encode(new NoOpCommand(1, P0, 1), new CommandWriter())).Message, Does.Contain("move_unit"));
        }

        /// <summary>Kinds and codecs iterate in ascending ordinal kind order whatever the registration order.</summary>
        [Test]
        public void Registry_IteratesInOrdinalKindOrder()
        {
            // Kinds in iteration order, and the same list sorted ordinally.
            List<string> kinds = new List<string>(Registry.Kinds);
            List<string> sorted = new List<string>(kinds);
            sorted.Sort(StringComparer.Ordinal);
            Assert.That(kinds, Is.EqualTo(sorted));
            // The codecs enumerate in the same order.
            List<string> fromCodecs = new List<string>();
            foreach (ICommandCodec codec in Registry.Codecs)
            {
                fromCodecs.Add(codec.Kind);
            }
            Assert.That(fromCodecs, Is.EqualTo(sorted));
            // A registry filled in the reverse order gives the same iteration.
            CommandCodecRegistry reversed = new CommandCodecRegistry();
            // Codecs of the normal registry, to re-add backwards.
            List<ICommandCodec> codecs = new List<ICommandCodec>(Registry.Codecs);
            for (int i = codecs.Count - 1; i >= 0; i--)
            {
                reversed.Register(codecs[i]);
            }
            Assert.That(new List<string>(reversed.Kinds), Is.EqualTo(sorted));
        }

        /// <summary>Validate accepts a good command and refuses one with no codec or a bad string, without keeping any state.</summary>
        [Test]
        public void Validate_AcceptsGoodAndRefusesBad()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            registry.Validate(new PickCycleCommand(1, P0, 1, new string('a', 65535)));
            Expect.Throws<CommandCodecException>(() => registry.Validate(new PickCycleCommand(1, P0, 1, new string('a', 65536))));
            Expect.Throws<CommandCodecException>(() => registry.Validate(new TrainUnitCommand(1, P0, 1, new EntityId(1), "bad\uD800")));
            Expect.Throws<CommandCodecException>(() => registry.Validate(new UnregisteredCommand(1)));
            Expect.Throws<ArgumentNullException>(() => registry.Validate(null!));
        }

        /// <summary>A command class that only exists in this test and has no codec.</summary>
        private sealed class UnregisteredCommand : Command
        {
            /// <summary>Builds the command.</summary>
            /// <param name="tick">Scheduled tick. Unit: ticks. Range: 0 upward.</param>
            public UnregisteredCommand(int tick) : base(tick, PlayerId.None, 0)
            {
            }

            /// <summary>"unregistered".</summary>
            public override string Kind => "unregistered";

            /// <summary>Does nothing.</summary>
            public override CommandResult Apply(Simulation sim)
            {
                return CommandResult.Ok;
            }

            /// <summary>Hashes the kind.</summary>
            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
            }
        }

        /// <summary>A codec that claims the kind "noop" for a different class, to test the duplicate-kind error.</summary>
        private sealed class SameKindCodec : CommandCodec<UnregisteredCommand>
        {
            /// <summary>"noop", already taken by the core codec.</summary>
            public override string Kind => "noop";

            /// <summary>Writes nothing.</summary>
            protected override void Write(UnregisteredCommand command, CommandWriter writer)
            {
            }

            /// <summary>Builds the command.</summary>
            protected override UnregisteredCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
            {
                return new UnregisteredCommand(tick);
            }
        }

        /// <summary>A codec for the class NoOpCommand under a new kind, to test the duplicate-type error.</summary>
        private sealed class SameTypeCodec : CommandCodec<NoOpCommand>
        {
            /// <summary>"noop_again", not yet taken.</summary>
            public override string Kind => "noop_again";

            /// <summary>Writes nothing.</summary>
            protected override void Write(NoOpCommand command, CommandWriter writer)
            {
            }

            /// <summary>Builds the command.</summary>
            protected override NoOpCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
            {
                return new NoOpCommand(tick, issuer, sequence);
            }
        }

        /// <summary>A codec that names the abstract Command class, which the registry refuses.</summary>
        private sealed class AbstractTypeCodec : ICommandCodec
        {
            /// <summary>"abstract_test".</summary>
            public string Kind => "abstract_test";

            /// <summary>The abstract base class, not a concrete command.</summary>
            public Type CommandType => typeof(Command);

            /// <summary>Writes nothing.</summary>
            public void Encode(Command command, CommandWriter writer)
            {
            }

            /// <summary>Never reached.</summary>
            public Command Decode(CommandReader reader, int tick, PlayerId issuer, uint sequence)
            {
                throw new InvalidOperationException("unused");
            }
        }
    }
}
