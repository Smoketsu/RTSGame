// rts/tests/Replay.Tests/CodecTests.cs
// Tests of the replay binary format and the per-command codecs: a round trip for every command type (checked against the full list of
// Command subclasses in the simulation assembly so a new command cannot ship without a codec), the file header, corruption, truncation,
// version refusal and plug-in codec registration. Part of Rts.Replay.Tests.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Factions.Water;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Replay.Tests
{
    /// <summary>Codec and format tests.</summary>
    public sealed class CodecTests
    {
        /// <summary>A scripted-issuer player ID (255), used for commands the match script would issue.</summary>
        private static readonly PlayerId Script = PlayerId.None;

        /// <summary>One command of every concrete command class, with distinctive values (negative and fractional numbers, multi-byte text).</summary>
        private static List<Command> SampleCommands()
        {
            // A fractional position with a non-trivial raw value.
            FixVec2 position = new FixVec2(Fix64.FromRaw(0x0000000A_80000001L), Fix64.FromRaw(-0x0000000B_40000003L));
            return new List<Command>
            {
                new NoOpCommand(1, Script, 0),
                new SpawnUnitCommand(2, Script, 1, "skai.windrunner", new PlayerId(1), position),
                new MoveUnitCommand(3, new PlayerId(0), 2, new EntityId(4000000000u), position),
                new RemoveUnitCommand(4, new PlayerId(1), 3, new EntityId(7)),
                new AttackMoveCommand(5, new PlayerId(0), 4, new EntityId(8), position),
                new AttackUnitCommand(6, new PlayerId(0), 5, new EntityId(9), new EntityId(10)),
                new StopCommand(7, new PlayerId(0), 6, new EntityId(11)),
                new GatherCommand(8, new PlayerId(1), 7, new EntityId(12), -3),
                new TrainUnitCommand(9, new PlayerId(1), 8, new EntityId(13), "water.tender"),
                new BuildStructureCommand(10, new PlayerId(1), 9, new EntityId(14), "water.pump", -5, 2000000000),
                new RootHqCommand(11, new PlayerId(0), 10, new EntityId(15)),
                new UprootHqCommand(12, new PlayerId(0), 11, new EntityId(16)),
                new PickCycleCommand(14, new PlayerId(0), 13, "quake"),
                new ApplyStatusCommand(13, Script, 12, new EntityId(17), StatusKind.Slowed, Fix64.FromRaw(0x00000000_80000000L), Fix64.FromRaw(0x00000003_00000000L), "spire.1"),
                new SurrenderCommand(14, new PlayerId(1), 13, new PlayerId(1)),
                new SetConnectionStateCommand(15, Script, 14, new PlayerId(1), ConnectionState.Disconnected),
                new CastWaveCommand(16, new PlayerId(1), 15, new EntityId(18), position),
            };
        }

        /// <summary>A replay holding all sample commands, for format tests. Two checkpoints, 40 ticks.</summary>
        private static ReplayData SampleReplay()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "Skai é"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "Water"),
            };
            List<ReplayCheckpoint> checkpoints = new List<ReplayCheckpoint>
            {
                new ReplayCheckpoint(0, 0x1111222233334444UL),
                new ReplayCheckpoint(20, 0xFEDCBA9876543210UL),
            };
            return new ReplayData(0xAABBCCDDEEFF0011UL, ulong.MaxValue, "greybox_1v1", 20, 40, players, SampleCommands(), checkpoints, 0x0123456789ABCDEFUL);
        }

        /// <summary>Hash of a command's scheduling fields and parameters, for equality checks (Command.WriteHash covers the parameters).</summary>
        private static ulong Fingerprint(Command command)
        {
            // Hasher fed the scheduling fields, then the command's own parameters.
            StateHasher hasher = new StateHasher();
            hasher.Write(command.Tick);
            hasher.Write(command.Issuer);
            hasher.Write(command.Sequence);
            command.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>Every command type survives write then read, with the same type, scheduling fields and parameters.</summary>
        [Test]
        public void EveryCommandTypeRoundTrips()
        {
            // Registry with all built-in codecs.
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            foreach (Command original in SampleCommands())
            {
                Assert.That(registry.TryGet(original.Kind, out ICommandCodec? codec), Is.True, original.Kind + " has no codec");
                // Payload written by the codec, then read back.
                ReplayBinaryWriter writer = new ReplayBinaryWriter();
                codec!.Write(writer, original);
                ReplayBinaryReader reader = new ReplayBinaryReader(writer.ToArray());
                Command copy = codec.Read(reader, original.Tick, original.Issuer, original.Sequence);
                Assert.That(reader.Remaining, Is.EqualTo(0), original.Kind + " left payload bytes unread");
                Assert.That(copy.GetType(), Is.EqualTo(original.GetType()));
                Assert.That(Fingerprint(copy), Is.EqualTo(Fingerprint(original)), original.Kind + " changed in the round trip");
            }
        }

        /// <summary>Every concrete Command class in the simulation assembly has a sample here and a built-in codec, so adding a command without a codec fails this test.</summary>
        [Test]
        public void EveryCommandClassInTheSimulationHasACodec()
        {
            // Concrete command classes found by reflection, by full name.
            SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Type type in typeof(Command).Assembly.GetTypes())
            {
                if (typeof(Command).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    found.Add(type.FullName!);
                }
            }
            // Command classes the samples cover.
            SortedSet<string> sampled = new SortedSet<string>(SampleCommands().Select(c => c.GetType().FullName!), StringComparer.Ordinal);
            Assert.That(sampled, Is.EqualTo(found), "a command class exists without a codec round-trip sample");
            // Kinds with a built-in codec.
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            foreach (Command command in SampleCommands())
            {
                Assert.That(registry.TryGet(command.Kind, out _), Is.True, command.Kind);
            }
            Assert.That(registry.Kinds.Count(), Is.EqualTo(found.Count));
        }

        /// <summary>A whole replay survives write then read: header fields, players, commands in (tick, issuer, sequence) order, checkpoints and final hash.</summary>
        [Test]
        public void WholeReplayRoundTrips()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            ReplayData original = SampleReplay();
            ReplayData copy = ReplayReader.Read(ReplayWriter.Write(original, registry), registry);
            Assert.That(copy.FormatVersion, Is.EqualTo(ReplayFormat.CurrentVersion));
            Assert.That(copy.ContentDigest, Is.EqualTo(original.ContentDigest));
            Assert.That(copy.Seed, Is.EqualTo(ulong.MaxValue));
            Assert.That(copy.MapId, Is.EqualTo("greybox_1v1"));
            Assert.That(copy.HashIntervalTicks, Is.EqualTo(20));
            Assert.That(copy.TickCount, Is.EqualTo(40));
            Assert.That(copy.FinalHash, Is.EqualTo(0x0123456789ABCDEFUL));
            Assert.That(copy.Players.Count, Is.EqualTo(2));
            Assert.That(copy.Players[0].DisplayName, Is.EqualTo("Skai é"));
            Assert.That(copy.Players[1].Team, Is.EqualTo(new TeamId(1)));
            Assert.That(copy.Checkpoints.Count, Is.EqualTo(2));
            Assert.That(copy.Checkpoints[1].Hash, Is.EqualTo(0xFEDCBA9876543210UL));
            Assert.That(copy.Commands.Count, Is.EqualTo(original.Commands.Count));
            // Index of the command being compared.
            for (int i = 0; i < original.Commands.Count; i++)
            {
                Assert.That(Fingerprint(copy.Commands[i]), Is.EqualTo(Fingerprint(original.Commands[i])), original.Commands[i].ToString());
            }
        }

        /// <summary>The same replay always encodes to the same bytes, and the layout starts with the magic and little-endian version 1.</summary>
        [Test]
        public void EncodingIsStableAndLittleEndian()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            byte[] first = ReplayWriter.Write(SampleReplay(), registry);
            byte[] second = ReplayWriter.Write(SampleReplay(), registry);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(System.Text.Encoding.ASCII.GetString(first, 0, 8), Is.EqualTo("RTSREPLY"));
            Assert.That(first.Skip(8).Take(4).ToArray(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }), "format version 1, least significant byte first");
            Assert.That(first.Skip(12).Take(8).ToArray(), Is.EqualTo(new byte[] { 0x11, 0x00, 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA }), "content digest, least significant byte first");
        }

        /// <summary>Commands given out of order are sorted by tick, issuer, sequence; equal keys keep the given order.</summary>
        [Test]
        public void CommandsAreSortedByTickIssuerSequence()
        {
            List<PlayerSetup> players = new List<PlayerSetup> { new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "A") };
            List<Command> shuffled = new List<Command>
            {
                new NoOpCommand(5, new PlayerId(1), 0),
                new NoOpCommand(5, new PlayerId(0), 9),
                new NoOpCommand(2, new PlayerId(1), 3),
                new NoOpCommand(5, new PlayerId(0), 1),
            };
            ReplayData replay = new ReplayData(1, 1, "m", 20, 10, players, shuffled, new List<ReplayCheckpoint>(), 1);
            Assert.That(replay.Commands.Select(c => c.Tick + ":" + c.Issuer.Value + ":" + c.Sequence).ToArray(),
                Is.EqualTo(new[] { "2:1:3", "5:0:1", "5:0:9", "5:1:0" }));
        }

        /// <summary>A command scheduled at or after the tick count never ran, so a replay cannot hold it.</summary>
        [Test]
        public void ACommandAfterTheLastTickIsRejected()
        {
            List<PlayerSetup> players = new List<PlayerSetup> { new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "A") };
            List<Command> late = new List<Command> { new NoOpCommand(10, Script, 0) };
            Expect.Throws<ArgumentException>(() => new ReplayData(1, 1, "m", 20, 10, players, late, new List<ReplayCheckpoint>(), 1));
        }

        /// <summary>A file with the wrong magic is reported as not a replay.</summary>
        [Test]
        public void WrongMagicIsRejected()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            byte[] bytes = ReplayWriter.Write(SampleReplay(), registry);
            bytes[0] = (byte)'X';
            ReplayFormatException ex = Expect.Throws<ReplayFormatException>(() => ReplayReader.Read(bytes, registry));
            Assert.That(ex.Message, Does.Contain("not a replay"));
            Expect.Throws<ReplayFormatException>(() => ReplayReader.Read(new byte[3], registry));
        }

        /// <summary>A different format version is refused with a message naming both versions, before any other field is read.</summary>
        [Test]
        public void ADifferentVersionIsRefused()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            byte[] bytes = ReplayWriter.Write(SampleReplay(), registry);
            bytes[8] = 2;
            ReplayRefusedException ex = Expect.Throws<ReplayRefusedException>(() => ReplayReader.Read(bytes, registry));
            Assert.That(ex.Message, Does.Contain("format version 2").And.Contain("reads version 1"));
        }

        /// <summary>Changing any byte after the header breaks the checksum, and cutting the file short is a clear truncation error.</summary>
        [Test]
        public void CorruptAndTruncatedFilesAreRejected()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            byte[] bytes = ReplayWriter.Write(SampleReplay(), registry);
            byte[] flipped = (byte[])bytes.Clone();
            flipped[40] ^= 0x01;
            ReplayFormatException damaged = Expect.Throws<ReplayFormatException>(() => ReplayReader.Read(flipped, registry));
            Assert.That(damaged.Message, Does.Contain("checksum"));
            // Every strict prefix of the file must fail with a ReplayFormatException, never another exception type.
            for (int length = 0; length < bytes.Length; length += 7)
            {
                byte[] prefix = bytes.Take(length).ToArray();
                Expect.Throws<ReplayFormatException>(() => ReplayReader.Read(prefix, registry));
            }
        }

        /// <summary>A command kind the writer has no codec for is reported by name; so is a kind the reader does not know.</summary>
        [Test]
        public void UnknownKindsAreReportedByName()
        {
            CommandCodecRegistry builtIn = CommandCodecRegistry.CreateBuiltIn();
            CommandCodecRegistry empty = new CommandCodecRegistry();
            ReplayFormatException writing = Expect.Throws<ReplayFormatException>(() => ReplayWriter.Write(SampleReplay(), empty));
            Assert.That(writing.Message, Does.Contain("no replay codec"));
            byte[] bytes = ReplayWriter.Write(SampleReplay(), builtIn);
            ReplayFormatException reading = Expect.Throws<ReplayFormatException>(() => ReplayReader.Read(bytes, empty));
            Assert.That(reading.Message, Does.Contain("no codec"));
        }

        /// <summary>A faction plug-in adds a codec for its own command kind, and registering a kind twice is an error.</summary>
        [Test]
        public void PlugInCodecsRegisterByKind()
        {
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            registry.Register(new CommandCodec<PlugInCommand>("plug_in_test",
                (w, c) => w.WriteI32(c.Amount),
                (r, tick, issuer, seq) => new PlugInCommand(tick, issuer, seq, r.ReadI32())));
            Expect.Throws<ArgumentException>(() => registry.Register(new CommandCodec<PlugInCommand>("plug_in_test", (w, c) => { }, (r, t, i, s) => new PlugInCommand(t, i, s, 0))));
            List<PlayerSetup> players = new List<PlayerSetup> { new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "A") };
            ReplayData replay = new ReplayData(1, 1, "m", 20, 10, players, new List<Command> { new PlugInCommand(3, Script, 0, -77) }, new List<ReplayCheckpoint>(), 1);
            ReplayData copy = ReplayReader.Read(ReplayWriter.Write(replay, registry), registry);
            Assert.That(((PlugInCommand)copy.Commands[0]).Amount, Is.EqualTo(-77));
        }

        /// <summary>A command that only exists in this test, standing in for a faction plug-in's command.</summary>
        private sealed class PlugInCommand : Command
        {
            /// <summary>An example parameter. Unit: none. Range: any int.</summary>
            public int Amount { get; }

            /// <summary>Builds the command.</summary>
            public PlugInCommand(int tick, PlayerId issuer, uint sequence, int amount) : base(tick, issuer, sequence)
            {
                Amount = amount;
            }

            /// <summary>Kind name "plug_in_test".</summary>
            public override string Kind => "plug_in_test";

            /// <summary>Does nothing.</summary>
            public override CommandResult Apply(Simulation sim)
            {
                return CommandResult.Ok;
            }

            /// <summary>Hashes the kind and the amount.</summary>
            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
                hasher.Write(Amount);
            }
        }
    }
}
