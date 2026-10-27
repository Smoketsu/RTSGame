// rts/tests/Net.Tests/CodecTests.cs
// Round-trip tests for the command codecs (one sample of every Command subclass in the simulation assembly, found by
// reflection so a new command without a codec fails here) and for every protocol message kind, plus the malformed
// input the decoder must reject. Part of Rts.Net.Tests.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rts.Net.Codecs;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Factions.Water;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;

namespace Rts.Net.Tests
{
    /// <summary>Command and message encoding.</summary>
    [TestFixture]
    public class CodecTests
    {
        /// <summary>Player slot 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>A position with fractional components, so Fix64 raw bits are exercised. Unit: tiles.</summary>
        private static readonly FixVec2 Spot = new FixVec2(Fix64.Parse("12.75"), Fix64.Parse("-3.5"));

        /// <summary>The registry under test, with every built-in codec.</summary>
        private static CommandCodecRegistry Registry => BuiltInCodecs.CreateRegistry();

        /// <summary>One sample of every command kind the game can issue, with distinctive field values.</summary>
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
                new GatherCommand(16, P0, 10, new EntityId(50), 3),
                new TrainUnitCommand(17, P1, 11, new EntityId(51), "water.tender"),
                new BuildStructureCommand(18, P0, 12, new EntityId(52), "skai.roost", 21, 34),
                new ApplyStatusCommand(19, PlayerId.None, 13, new EntityId(53), StatusKind.Slowed, Fix64.Parse("0.6"), Fix64.Parse("2.5"), "test.slow"),
                new SurrenderCommand(20, P1, 14, P1),
                new SetConnectionStateCommand(21, PlayerId.None, 15, P0, ConnectionState.Disconnected),
                new CastWaveCommand(22, P1, 16, new EntityId(54), Spot),
            };
        }

        /// <summary>Hash of a command's scheduling fields and parameters, so two commands can be compared field by field.</summary>
        private static ulong Fingerprint(Command command)
        {
            // Hasher fed the scheduling fields then the command's own WriteHash.
            StateHasher hasher = new StateHasher();
            hasher.Write(command.Tick);
            hasher.Write(command.Issuer);
            hasher.Write(command.Sequence);
            command.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>Every command kind round-trips through its codec with identical fields.</summary>
        [Test]
        public void EveryCommandKind_RoundTrips()
        {
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            foreach (Command sample in Samples())
            {
                // Encoded bytes of the sample.
                NetWriter writer = new NetWriter();
                registry.Encode(sample, writer);
                // The decoded copy.
                Command decoded = registry.Decode(new NetReader(writer.ToArray()));
                Assert.That(decoded.GetType(), Is.EqualTo(sample.GetType()), sample.Kind);
                Assert.That(decoded.Kind, Is.EqualTo(sample.Kind));
                Assert.That(Fingerprint(decoded), Is.EqualTo(Fingerprint(sample)), sample.Kind + " fields survive the round trip");
            }
        }

        /// <summary>The samples cover every concrete Command subclass in the simulation assembly, and the registry has a codec for each.</summary>
        [Test]
        public void EveryCommandSubclass_HasASampleAndACodec()
        {
            // Concrete command types in the simulation assembly, by full name.
            SortedSet<string> commandTypes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Type type in typeof(Command).Assembly.GetTypes())
            {
                if (!type.IsAbstract && typeof(Command).IsAssignableFrom(type))
                {
                    commandTypes.Add(type.FullName ?? type.Name);
                }
            }
            // Types the samples cover, by full name.
            SortedSet<string> sampled = new SortedSet<string>(StringComparer.Ordinal);
            // The registry under test.
            CommandCodecRegistry registry = Registry;
            foreach (Command sample in Samples())
            {
                sampled.Add(sample.GetType().FullName ?? sample.GetType().Name);
                Assert.That(registry.Contains(sample.Kind), Is.True, "codec for " + sample.Kind);
            }
            Assert.That(sampled, Is.EquivalentTo(commandTypes), "every Command subclass has a sample (add one here and a codec)");
            Assert.That(registry.Count, Is.EqualTo(commandTypes.Count), "one codec per command type");
        }

        /// <summary>Every message kind round-trips through the message codec.</summary>
        [Test]
        public void EveryMessageKind_RoundTrips()
        {
            // The codec under test.
            MessageCodec codec = new MessageCodec(Registry);
            // A bundle holding every sample command at one tick.
            List<Command> atTick = new List<Command>();
            foreach (Command sample in Samples())
            {
                atTick.Add(sample);
            }
            // Bundle at tick 30 (every sample re-stamped to that tick through the codec's decode path is not needed: bundles require matching ticks, so build the bundle from re-ticked samples).
            CommandBundleMessage bundle = new CommandBundleMessage(9, new PeerId(1), new List<Command> { new MoveUnitCommand(9, P0, 3, new EntityId(42), Spot) });
            // The pause announcement reused inside the sync log.
            PauseStartedMessage pause = new PauseStartedMessage(5, P1, new PeerId(1), PauseReason.Disconnect, 300000);
            // A small sync log: one non-empty bundle, two empty bundles on consecutive ticks and one apart, so both entry kinds are written.
            SyncLog syncLog = new SyncLog(120, 100, 0xfeedUL,
                new List<CommandBundleMessage> { bundle, new CommandBundleMessage(10, new PeerId(1), Array.Empty<Command>()), new CommandBundleMessage(11, new PeerId(1), Array.Empty<Command>()), new CommandBundleMessage(13, new PeerId(1), Array.Empty<Command>()) },
                new List<PauseAccountEntry> { new PauseAccountEntry(P0, 0, 5000), new PauseAccountEntry(P1, 1, 0) },
                new List<PeerRangesEntry> { new PeerRangesEntry(new PeerId(0), new List<TickRange> { new TickRange(0, int.MaxValue) }), new PeerRangesEntry(new PeerId(1), new List<TickRange> { new TickRange(0, 99) }) },
                new List<PeerId> { new PeerId(1) }, pause, 6);
            // The log's parts; a small log is one part.
            List<SyncLogPartMessage> parts = codec.SplitSyncLog(syncLog);
            Assert.That(parts.Count, Is.EqualTo(1));
            // One instance of every message kind.
            List<NetMessage> messages = new List<NetMessage>
            {
                bundle,
                new CommandBundleMessage(0, new PeerId(0), Array.Empty<Command>()),
                new StateHashMessage(40, 0x1234567890abcdefUL),
                new PauseRequestMessage(P1),
                new ResumeRequestMessage(P0),
                pause,
                new PauseEndedMessage(5, 12345, PauseEnd.Expired),
                new PeerDroppedMessage(new PeerId(1), 99),
                new PeerRejoinedMessage(new PeerId(1), 130),
                new SyncRequestMessage(),
                parts[0],
                new SyncDoneMessage(100, 0xfeedUL),
                new SyncRejectedMessage("hash_mismatch"),
                new BundleRequestMessage(new PeerId(1), 77),
            };
            // Kinds seen, to check the list covers the enum.
            SortedSet<byte> kinds = new SortedSet<byte>();
            foreach (NetMessage message in messages)
            {
                kinds.Add((byte)message.Kind);
                // The decoded copy.
                NetMessage decoded = codec.Decode(codec.Encode(message));
                Assert.That(decoded.Kind, Is.EqualTo(message.Kind));
                Assert.That(codec.Encode(decoded), Is.EqualTo(codec.Encode(message)), message.Kind + " re-encodes to the same bytes");
            }
            Assert.That(kinds.Count, Is.EqualTo(Enum.GetValues(typeof(MessageKind)).Length), "every MessageKind has a sample");
            // The sync log joined back from its decoded part, checked field by field; bundles come back grouped by peer then tick.
            SyncLog log = codec.JoinSyncLog(new List<SyncLogPartMessage> { (SyncLogPartMessage)codec.Decode(codec.Encode(parts[0])) });
            Assert.That(log.RefereeTick, Is.EqualTo(120));
            Assert.That(log.CheckTick, Is.EqualTo(100));
            Assert.That(log.Bundles.Count, Is.EqualTo(4));
            Assert.That(log.Bundles[0].Tick, Is.EqualTo(9));
            Assert.That(log.Bundles[0].Commands.Count, Is.EqualTo(1));
            Assert.That(log.Bundles[1].Tick, Is.EqualTo(10));
            Assert.That(log.Bundles[2].Tick, Is.EqualTo(11));
            Assert.That(log.Bundles[3].Tick, Is.EqualTo(13));
            Assert.That(log.Bundles[3].Commands, Is.Empty);
            Assert.That(log.Ledger[1].DisconnectPausesUsed, Is.EqualTo(1));
            Assert.That(log.Ranges[1].Ranges[0].To, Is.EqualTo(99));
            Assert.That(log.Dropped, Is.EqualTo(new[] { new PeerId(1) }));
            Assert.That(log.ActivePause!.LimitMs, Is.EqualTo(300000));
            Assert.That(log.NextPauseId, Is.EqualTo(6));
            Assert.That(codec.SplitSyncLog(log)[0].Bytes, Is.EqualTo(parts[0].Bytes), "the joined log splits to the same bytes");
        }

        /// <summary>
        /// Review finding 5: a long match's sync log must fit the transport. A 48,000-tick (40 minutes at 20 ticks per second), 3-peer
        /// log with a command in every fourth bundle is split into several parts that each stay within TransportLimits.MaxPayloadBytes
        /// and joins back to the same bundles; the same length of empty bundles for 8 peers folds into one small part.
        /// </summary>
        [Test]
        public void LongMatchSyncLog_SplitsUnderThePayloadCap_AndRoundTrips()
        {
            // The codec under test.
            MessageCodec codec = new MessageCodec(Registry);
            // Ticks of the long match. Unit: ticks.
            const int ticks = 48000;
            // Every (tick, peer) bundle of three peers; every fourth bundle carries one move.
            List<CommandBundleMessage> bundles = new List<CommandBundleMessage>(ticks * 3);
            // Expected fingerprint of the bundle at (tick, peer), to compare after the round trip. Keyed (peer << 32) | tick.
            SortedDictionary<ulong, ulong> expected = new SortedDictionary<ulong, ulong>();
            for (int tick = 0; tick < ticks; tick++)
            {
                for (byte peer = 0; peer < 3; peer++)
                {
                    // The commands of this bundle: a move in every fourth bundle, by the peer's own player.
                    List<Command> commands = new List<Command>();
                    if ((tick + peer) % 4 == 0)
                    {
                        commands.Add(new MoveUnitCommand(tick, new PlayerId(peer), (uint)tick, new EntityId((uint)(tick + 1)), Spot));
                    }
                    bundles.Add(new CommandBundleMessage(tick, new PeerId(peer), commands));
                    expected[((ulong)peer << 32) | (uint)tick] = commands.Count == 0 ? 0 : Fingerprint(commands[0]);
                }
            }
            // The log and its parts.
            SyncLog log = new SyncLog(ticks, ticks - 20, 0xabcUL, bundles, new List<PauseAccountEntry>(), new List<PeerRangesEntry>(), new List<PeerId>(), null, 1);
            List<SyncLogPartMessage> parts = codec.SplitSyncLog(log);
            Assert.That(parts.Count, Is.GreaterThan(1), "a log with 36,000 commands needs more than one part");
            foreach (SyncLogPartMessage part in parts)
            {
                Assert.That(codec.Encode(part).Length, Is.LessThanOrEqualTo(TransportLimits.MaxPayloadBytes), "part " + part.Index + " fits the transport");
                Assert.That(part.Count, Is.EqualTo(parts.Count));
            }
            // The parts as received (decoded) and the log joined from them.
            List<SyncLogPartMessage> received = new List<SyncLogPartMessage>();
            foreach (SyncLogPartMessage part in parts)
            {
                received.Add((SyncLogPartMessage)codec.Decode(codec.Encode(part)));
            }
            SyncLog joined = codec.JoinSyncLog(received);
            Assert.That(joined.Bundles.Count, Is.EqualTo(bundles.Count));
            Assert.That(joined.RefereeTick, Is.EqualTo(ticks));
            foreach (CommandBundleMessage bundle in joined.Bundles)
            {
                // Key and fingerprint of the received bundle.
                ulong key = ((ulong)bundle.Peer.Value << 32) | (uint)bundle.Tick;
                Assert.That(expected.TryGetValue(key, out ulong fingerprint), Is.True, "bundle " + bundle.Peer + "@" + bundle.Tick + " was sent");
                Assert.That(bundle.Commands.Count == 0 ? 0 : Fingerprint(bundle.Commands[0]), Is.EqualTo(fingerprint), "bundle " + bundle.Peer + "@" + bundle.Tick + " survives");
                expected.Remove(key);
            }
            Assert.That(expected, Is.Empty, "every bundle came back once");
            // The same length of empty bundles for eight peers: one run entry per peer, so one small part.
            List<CommandBundleMessage> empties = new List<CommandBundleMessage>(ticks * 8);
            for (byte peer = 0; peer < 8; peer++)
            {
                for (int tick = 0; tick < ticks; tick++)
                {
                    empties.Add(new CommandBundleMessage(tick, new PeerId(peer), Array.Empty<Command>()));
                }
            }
            List<SyncLogPartMessage> emptyParts = codec.SplitSyncLog(new SyncLog(ticks, -1, 0, empties, new List<PauseAccountEntry>(), new List<PeerRangesEntry>(), new List<PeerId>(), null, 1));
            Assert.That(emptyParts.Count, Is.EqualTo(1));
            Assert.That(emptyParts[0].Bytes.Length, Is.LessThan(1024), "384,000 empty bundles fold into a few run entries");
            Assert.That(codec.JoinSyncLog(emptyParts).Bundles.Count, Is.EqualTo(ticks * 8));
            // Parts out of order or with a mismatched count are protocol errors.
            Assert.That(() => codec.JoinSyncLog(new List<SyncLogPartMessage> { received[1], received[0] }), Throws.TypeOf<NetProtocolException>(), "out of order");
            Assert.That(() => codec.JoinSyncLog(new List<SyncLogPartMessage> { received[0] }), Throws.TypeOf<NetProtocolException>(), "incomplete");
        }

        /// <summary>Wrong version, unknown kind, truncation, trailing bytes, a bad enum byte and an unknown command kind are protocol errors.</summary>
        [Test]
        public void MalformedInput_IsRejected()
        {
            // The codec under test.
            MessageCodec codec = new MessageCodec(Registry);
            // A valid message to corrupt.
            byte[] good = codec.Encode(new PauseEndedMessage(1, 10, PauseEnd.Resumed));
            Assert.That(() => codec.Decode(Corrupt(good, 0, 9)), Throws.TypeOf<NetProtocolException>(), "version");
            Assert.That(() => codec.Decode(Corrupt(good, 2, 200)), Throws.TypeOf<NetProtocolException>(), "kind");
            Assert.That(() => codec.Decode(Corrupt(good, good.Length - 1, 7)), Throws.TypeOf<NetProtocolException>(), "pause end byte");
            // Truncated copy missing the last byte.
            byte[] truncated = new byte[good.Length - 1];
            Array.Copy(good, truncated, truncated.Length);
            Assert.That(() => codec.Decode(truncated), Throws.TypeOf<NetProtocolException>(), "truncated");
            // Copy with one trailing byte.
            byte[] trailing = new byte[good.Length + 1];
            Array.Copy(good, trailing, good.Length);
            Assert.That(() => codec.Decode(trailing), Throws.TypeOf<NetProtocolException>(), "trailing");
            // A command of a kind the registry does not know.
            NetWriter writer = new NetWriter();
            writer.WriteString("teleport");
            Assert.That(() => Registry.Decode(new NetReader(writer.ToArray())), Throws.TypeOf<NetProtocolException>(), "unknown command kind");
            // A registry without the Water codec cannot encode cast_wave.
            CommandCodecRegistry core = new CommandCodecRegistry();
            CoreCommandCodecs.RegisterAll(core);
            Assert.That(() => core.Encode(new CastWaveCommand(1, P1, 1, new EntityId(1), Spot), new NetWriter()), Throws.TypeOf<NetProtocolException>());
            Assert.That(() => core.Register(new NoOpCodec()), Throws.InvalidOperationException, "duplicate kind");
        }

        /// <summary>A copy of <paramref name="bytes"/> with one byte replaced.</summary>
        private static byte[] Corrupt(byte[] bytes, int index, byte value)
        {
            // The corrupted copy.
            byte[] copy = (byte[])bytes.Clone();
            copy[index] = value;
            return copy;
        }

        /// <summary>The writer and reader agree on every primitive, including negative and extreme values and null strings.</summary>
        [Test]
        public void Primitives_RoundTrip()
        {
            // The buffer under test.
            NetWriter writer = new NetWriter();
            writer.WriteByte(200);
            writer.WriteBool(true);
            writer.WriteUInt16(65535);
            writer.WriteInt32(-123456);
            writer.WriteUInt32(4000000000u);
            writer.WriteInt64(long.MinValue);
            writer.WriteUInt64(ulong.MaxValue);
            writer.WriteFix64(Fix64.Parse("-0.000000001"));
            writer.WriteFixVec2(Spot);
            writer.WriteEntityId(new EntityId(7));
            writer.WritePlayerId(PlayerId.None);
            writer.WritePeerId(new PeerId(3));
            writer.WriteString(null);
            writer.WriteString(string.Empty);
            writer.WriteString("Ünïcode ☃");
            writer.WriteBytes(new byte[] { 1, 2, 3 });
            // The reader over the written bytes.
            NetReader reader = new NetReader(writer.ToArray());
            Assert.That(reader.ReadByte(), Is.EqualTo(200));
            Assert.That(reader.ReadBool(), Is.True);
            Assert.That(reader.ReadUInt16(), Is.EqualTo(65535));
            Assert.That(reader.ReadInt32(), Is.EqualTo(-123456));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(4000000000u));
            Assert.That(reader.ReadInt64(), Is.EqualTo(long.MinValue));
            Assert.That(reader.ReadUInt64(), Is.EqualTo(ulong.MaxValue));
            Assert.That(reader.ReadFix64(), Is.EqualTo(Fix64.Parse("-0.000000001")));
            Assert.That(reader.ReadFixVec2(), Is.EqualTo(Spot));
            Assert.That(reader.ReadEntityId(), Is.EqualTo(new EntityId(7)));
            Assert.That(reader.ReadPlayerId(), Is.EqualTo(PlayerId.None));
            Assert.That(reader.ReadPeerId(), Is.EqualTo(new PeerId(3)));
            Assert.That(reader.ReadString(), Is.Null);
            Assert.That(reader.ReadString(), Is.EqualTo(string.Empty));
            Assert.That(reader.ReadString(), Is.EqualTo("Ünïcode ☃"));
            Assert.That(reader.ReadBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(reader.Remaining, Is.EqualTo(0));
            Assert.That(() => reader.ReadByte(), Throws.TypeOf<NetProtocolException>());
        }
    }
}
