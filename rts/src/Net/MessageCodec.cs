// rts/src/Net/MessageCodec.cs
// Turns protocol messages into bytes and back. Part of Rts.Net (ADR-008). Wire layout of every message: protocol
// version (uint16), message kind (one byte), then the kind's fields in the order its class lists them; lists are an
// int32 count followed by the items. Commands inside bundles go through the CommandCodecRegistry. A message with a
// different version, an unknown kind, a bad enum byte, a truncated body or trailing bytes throws NetProtocolException.
// The sync log is not one message: SplitSyncLog encodes it into a body (bundles grouped by peer, runs of empty bundles
// written as one run entry) and slices the body into SyncLogPart messages that each fit under the transport's payload
// cap; JoinSyncLog is the mirror. ProtocolVersion changes whenever any layout here or in a codec changes; peers on
// different versions cannot play.
using System;
using System.Collections.Generic;
using Rts.Net.Codecs;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Net
{
    /// <summary>Encodes and decodes every <see cref="NetMessage"/>. One instance per peer, holding the command codecs.</summary>
    public sealed class MessageCodec
    {
        /// <summary>Version written at the start of every message. Range: fixed per build; bump on any wire change. 2: the sync log travels in parts with run-length encoded empty bundles.</summary>
        public const ushort ProtocolVersion = 2;

        /// <summary>Most commands one bundle may carry, a sanity bound against hostile lengths. Unit: commands. Range: fixed protocol limit.</summary>
        public const int MaxCommandsPerBundle = 1 << 14;

        /// <summary>Most bundles a sync log may carry, runs of empty bundles counted per bundle (long matches at 20 ticks per second times many peers stay far below). Unit: bundles. Range: fixed protocol limit.</summary>
        public const int MaxBundlesPerSync = 1 << 22;

        /// <summary>Most entries a list of players, peers or ranges may have. Range: fixed protocol limit.</summary>
        public const int MaxSmallList = 1 << 12;

        /// <summary>Most parts one sync log may be split into (times <see cref="SyncLogPartMaxBytes"/> is far above any log the bundle bound allows). Unit: parts. Range: fixed protocol limit.</summary>
        public const int MaxSyncLogParts = 1 << 16;

        /// <summary>Bytes of a sync log part message that are not log bytes: version, kind, index, count and the slice length. Unit: bytes. Range: fixed by the wire layout.</summary>
        public const int SyncLogPartOverheadBytes = 2 + 1 + 4 + 4 + 4;

        /// <summary>Most log bytes one part carries, so the whole part message stays within <see cref="TransportLimits.MaxPayloadBytes"/>. Unit: bytes. Range: fixed protocol limit.</summary>
        public const int SyncLogPartMaxBytes = TransportLimits.MaxPayloadBytes - SyncLogPartOverheadBytes;

        /// <summary>Entry byte in the sync log body for a run of consecutive empty bundles of one peer. Range: fixed wire value.</summary>
        private const byte EntryEmptyRun = 0;

        /// <summary>Entry byte in the sync log body for one bundle with commands. Range: fixed wire value.</summary>
        private const byte EntryBundle = 1;

        /// <summary>The command codecs used inside bundles.</summary>
        public CommandCodecRegistry Commands { get; }

        /// <summary>Builds a codec over <paramref name="commands"/>.</summary>
        /// <param name="commands">The command codec registry; not null.</param>
        public MessageCodec(CommandCodecRegistry commands)
        {
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Serialises a message to a new byte array.</summary>
        /// <param name="message">The message; not null.</param>
        public byte[] Encode(NetMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            // The buffer being filled.
            NetWriter writer = new NetWriter();
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteByte((byte)message.Kind);
            switch (message)
            {
                case CommandBundleMessage bundle:
                    WriteBundle(bundle, writer);
                    break;
                case StateHashMessage hash:
                    writer.WriteInt32(hash.Tick);
                    writer.WriteUInt64(hash.Hash);
                    break;
                case PauseRequestMessage pauseRequest:
                    writer.WritePlayerId(pauseRequest.Player);
                    break;
                case ResumeRequestMessage resumeRequest:
                    writer.WritePlayerId(resumeRequest.Player);
                    break;
                case PauseStartedMessage started:
                    WritePauseStarted(started, writer);
                    break;
                case PauseEndedMessage ended:
                    writer.WriteUInt32(ended.PauseId);
                    writer.WriteInt32(ended.ElapsedMs);
                    writer.WriteByte((byte)ended.End);
                    break;
                case PeerDroppedMessage dropped:
                    writer.WritePeerId(dropped.Peer);
                    writer.WriteInt32(dropped.LastTick);
                    break;
                case PeerRejoinedMessage rejoined:
                    writer.WritePeerId(rejoined.Peer);
                    writer.WriteInt32(rejoined.FromTick);
                    break;
                case SyncRequestMessage _:
                    break;
                case SyncLogPartMessage part:
                    writer.WriteInt32(part.Index);
                    writer.WriteInt32(part.Count);
                    writer.WriteBytes(part.Bytes);
                    break;
                case SyncDoneMessage done:
                    writer.WriteInt32(done.CheckTick);
                    writer.WriteUInt64(done.Hash);
                    break;
                case SyncRejectedMessage rejected:
                    writer.WriteString(rejected.Reason);
                    break;
                case BundleRequestMessage request:
                    writer.WritePeerId(request.Peer);
                    writer.WriteInt32(request.Tick);
                    break;
                default:
                    throw new NetProtocolException("No encoder for message kind " + message.Kind + ".");
            }
            return writer.ToArray();
        }

        /// <summary>Parses a message. Throws <see cref="NetProtocolException"/> on any malformed input.</summary>
        /// <param name="bytes">The received bytes; not null.</param>
        public NetMessage Decode(byte[] bytes)
        {
            // Reader over the received bytes.
            NetReader reader = new NetReader(bytes ?? throw new ArgumentNullException(nameof(bytes)));
            // Protocol version written by the sender. Range: must equal ProtocolVersion.
            ushort version = reader.ReadUInt16();
            if (version != ProtocolVersion)
            {
                throw new NetProtocolException("Protocol version " + version + " received; this build speaks " + ProtocolVersion + ".");
            }
            // Message kind byte. Range: a MessageKind value.
            byte kind = reader.ReadByte();
            // The parsed message.
            NetMessage message;
            switch ((MessageKind)kind)
            {
                case MessageKind.CommandBundle:
                    message = ReadBundle(reader);
                    break;
                case MessageKind.StateHash:
                    message = new StateHashMessage(reader.ReadInt32(), reader.ReadUInt64());
                    break;
                case MessageKind.PauseRequest:
                    message = new PauseRequestMessage(reader.ReadPlayerId());
                    break;
                case MessageKind.ResumeRequest:
                    message = new ResumeRequestMessage(reader.ReadPlayerId());
                    break;
                case MessageKind.PauseStarted:
                    message = ReadPauseStarted(reader);
                    break;
                case MessageKind.PauseEnded:
                    message = ReadPauseEnded(reader);
                    break;
                case MessageKind.PeerDropped:
                    message = Guard(() => new PeerDroppedMessage(reader.ReadPeerId(), reader.ReadInt32()));
                    break;
                case MessageKind.PeerRejoined:
                    message = Guard(() => new PeerRejoinedMessage(reader.ReadPeerId(), reader.ReadInt32()));
                    break;
                case MessageKind.SyncRequest:
                    message = new SyncRequestMessage();
                    break;
                case MessageKind.SyncLogPart:
                    message = ReadSyncLogPart(reader);
                    break;
                case MessageKind.SyncDone:
                    message = new SyncDoneMessage(reader.ReadInt32(), reader.ReadUInt64());
                    break;
                case MessageKind.SyncRejected:
                    message = Guard(() => new SyncRejectedMessage(reader.ReadRequiredString()));
                    break;
                case MessageKind.BundleRequest:
                    message = Guard(() => new BundleRequestMessage(reader.ReadPeerId(), reader.ReadInt32()));
                    break;
                default:
                    throw new NetProtocolException("Unknown message kind byte " + kind + ".");
            }
            reader.RequireEnd();
            return message;
        }

        /// <summary>Runs a message constructor, turning its argument checks into protocol errors.</summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="build">The constructor call.</param>
        private static T Guard<T>(Func<T> build) where T : class
        {
            try
            {
                return build();
            }
            catch (ArgumentException ex)
            {
                throw new NetProtocolException("Message rejected its fields: " + ex.Message, ex);
            }
        }

        /// <summary>Writes tick, peer, command count, then each command through the registry.</summary>
        private void WriteBundle(CommandBundleMessage bundle, NetWriter writer)
        {
            writer.WriteInt32(bundle.Tick);
            writer.WritePeerId(bundle.Peer);
            WriteCommands(bundle.Commands, writer);
        }

        /// <summary>Writes a command count then each command through the registry.</summary>
        private void WriteCommands(IReadOnlyList<Command> commands, NetWriter writer)
        {
            writer.WriteInt32(commands.Count);
            foreach (Command command in commands)
            {
                Commands.Encode(command, writer);
            }
        }

        /// <summary>Reads a bundle written by <see cref="WriteBundle"/>; a command whose tick differs from the bundle's is a protocol error.</summary>
        private CommandBundleMessage ReadBundle(NetReader reader)
        {
            // Fields in wire order.
            int tick = reader.ReadInt32();
            PeerId peer = reader.ReadPeerId();
            List<Command> commands = ReadCommands(reader);
            return Guard(() => new CommandBundleMessage(tick, peer, commands));
        }

        /// <summary>Reads a command count (bounded by <see cref="MaxCommandsPerBundle"/>) then that many commands.</summary>
        private List<Command> ReadCommands(NetReader reader)
        {
            // The count as written. Range: 0 to MaxCommandsPerBundle.
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxCommandsPerBundle)
            {
                throw new NetProtocolException("Bad command count " + count + " in a bundle.");
            }
            // The decoded commands in wire order.
            List<Command> commands = new List<Command>(count);
            // Index of the command being read. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                commands.Add(Commands.Decode(reader));
            }
            return commands;
        }

        /// <summary>Writes pause id, player, peer, reason byte and limit.</summary>
        private static void WritePauseStarted(PauseStartedMessage started, NetWriter writer)
        {
            writer.WriteUInt32(started.PauseId);
            writer.WritePlayerId(started.Player);
            writer.WritePeerId(started.Peer);
            writer.WriteByte((byte)started.Reason);
            writer.WriteInt32(started.LimitMs);
        }

        /// <summary>Reads a pause start; an unknown reason byte is a protocol error.</summary>
        private static PauseStartedMessage ReadPauseStarted(NetReader reader)
        {
            // Fields in wire order.
            uint id = reader.ReadUInt32();
            PlayerId player = reader.ReadPlayerId();
            PeerId peer = reader.ReadPeerId();
            byte reason = reader.ReadByte();
            int limit = reader.ReadInt32();
            if (reason > (byte)PauseReason.Resync)
            {
                throw new NetProtocolException("Unknown pause reason byte " + reason + ".");
            }
            return Guard(() => new PauseStartedMessage(id, player, peer, (PauseReason)reason, limit));
        }

        /// <summary>Reads a pause end; an unknown end byte is a protocol error.</summary>
        private static PauseEndedMessage ReadPauseEnded(NetReader reader)
        {
            // Fields in wire order.
            uint id = reader.ReadUInt32();
            int elapsed = reader.ReadInt32();
            byte end = reader.ReadByte();
            if (end > (byte)PauseEnd.Expired)
            {
                throw new NetProtocolException("Unknown pause end byte " + end + ".");
            }
            return Guard(() => new PauseEndedMessage(id, elapsed, (PauseEnd)end));
        }

        /// <summary>Reads a sync log part; a count or slice beyond the limits is a protocol error.</summary>
        private static SyncLogPartMessage ReadSyncLogPart(NetReader reader)
        {
            // Fields in wire order.
            int index = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count < 1 || count > MaxSyncLogParts)
            {
                throw new NetProtocolException("Bad sync log part count " + count + ".");
            }
            // The slice; its length prefix is checked against the part cap.
            byte[] bytes = reader.ReadBytes();
            if (bytes.Length > SyncLogPartMaxBytes)
            {
                throw new NetProtocolException("Sync log part of " + bytes.Length + " bytes exceeds the part cap of " + SyncLogPartMaxBytes + ".");
            }
            return Guard(() => new SyncLogPartMessage(index, count, bytes));
        }

        // ---------------------------------------------------------------- sync log

        /// <summary>
        /// Encodes a sync log as the run of part messages to send in index order. Every encoded part stays within
        /// <see cref="TransportLimits.MaxPayloadBytes"/>; an empty log is one part. The body is the log's scalars, then the
        /// bundles grouped by peer ascending and tick ascending with consecutive empty bundles folded into one run entry, then
        /// the ledger, ranges, dropped peers, the optional active pause and the next pause id.
        /// </summary>
        /// <param name="log">The log; not null.</param>
        public List<SyncLogPartMessage> SplitSyncLog(SyncLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }
            // The whole encoded body.
            NetWriter writer = new NetWriter(1 << 12);
            WriteSyncLogBody(log, writer);
            byte[] body = writer.ToArray();
            // Parts needed. Range: 1 upward.
            int count = Math.Max(1, (body.Length + SyncLogPartMaxBytes - 1) / SyncLogPartMaxBytes);
            if (count > MaxSyncLogParts)
            {
                throw new NetProtocolException("Sync log of " + body.Length + " bytes needs " + count + " parts, above the limit of " + MaxSyncLogParts + ".");
            }
            // The parts in index order.
            List<SyncLogPartMessage> parts = new List<SyncLogPartMessage>(count);
            // Index of the part being cut. Range: 0 to count - 1.
            for (int index = 0; index < count; index++)
            {
                // First byte and length of this slice. Unit: bytes.
                int start = index * SyncLogPartMaxBytes;
                int length = Math.Min(SyncLogPartMaxBytes, body.Length - start);
                // A body shorter than one byte (never, the header alone is longer) would still need a non-empty slice.
                byte[] slice = new byte[Math.Max(1, length)];
                Array.Copy(body, start, slice, 0, length);
                parts.Add(new SyncLogPartMessage(index, count, slice));
            }
            return parts;
        }

        /// <summary>
        /// Decodes the log from a complete run of parts (indices 0 to Count - 1 in order, all with the same Count). A gap,
        /// a repeated index, a mismatched count or a malformed body is a protocol error.
        /// </summary>
        /// <param name="parts">The received parts in index order; not null, not empty.</param>
        public SyncLog JoinSyncLog(IReadOnlyList<SyncLogPartMessage> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }
            if (parts.Count == 0)
            {
                throw new NetProtocolException("A sync log needs at least one part.");
            }
            // The count every part must agree on.
            int count = parts[0].Count;
            if (parts.Count != count)
            {
                throw new NetProtocolException("Sync log run has " + parts.Count + " parts but each announces " + count + ".");
            }
            // Total body length. Unit: bytes.
            int total = 0;
            // Index of the part being checked. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                if (parts[i].Index != i || parts[i].Count != count)
                {
                    throw new NetProtocolException("Sync log part " + parts[i].Index + " of " + parts[i].Count + " arrived where part " + i + " of " + count + " was expected.");
                }
                total += parts[i].Bytes.Length;
            }
            // The joined body.
            byte[] body = new byte[total];
            // Write position in the body. Unit: bytes.
            int at = 0;
            foreach (SyncLogPartMessage part in parts)
            {
                Array.Copy(part.Bytes, 0, body, at, part.Bytes.Length);
                at += part.Bytes.Length;
            }
            // Reader over the body; it must be consumed exactly.
            NetReader reader = new NetReader(body);
            SyncLog log = ReadSyncLogBody(reader);
            reader.RequireEnd();
            return log;
        }

        /// <summary>Writes the sync log body: scalars, bundles grouped by peer with empty runs folded, ledger, ranges, dropped peers, the optional active pause, next pause id.</summary>
        private void WriteSyncLogBody(SyncLog log, NetWriter writer)
        {
            writer.WriteInt32(log.RefereeTick);
            writer.WriteInt32(log.CheckTick);
            writer.WriteUInt64(log.CheckHash);
            // Bundles by peer (ascending), each list sorted by tick.
            SortedDictionary<PeerId, List<CommandBundleMessage>> byPeer = new SortedDictionary<PeerId, List<CommandBundleMessage>>();
            foreach (CommandBundleMessage bundle in log.Bundles)
            {
                if (!byPeer.TryGetValue(bundle.Peer, out List<CommandBundleMessage>? list))
                {
                    list = new List<CommandBundleMessage>();
                    byPeer.Add(bundle.Peer, list);
                }
                list.Add(bundle);
            }
            writer.WriteInt32(byPeer.Count);
            foreach (KeyValuePair<PeerId, List<CommandBundleMessage>> group in byPeer)
            {
                // Ticks are unique per peer (one bundle per (tick, peer)), so this order is total.
                group.Value.Sort(CompareByTick);
                writer.WritePeerId(group.Key);
                // Entries of this peer: one per non-empty bundle, one per run of consecutive empty bundles.
                List<CommandBundleMessage> bundles = group.Value;
                // Entry count position is patched after the entries are counted, so write them to a side buffer first.
                NetWriter entries = new NetWriter(1 << 10);
                // Entries written. Range: 0 upward.
                int entryCount = 0;
                // Index of the bundle being written. Range: 0 to bundles.Count.
                int i = 0;
                while (i < bundles.Count)
                {
                    // The bundle at i.
                    CommandBundleMessage bundle = bundles[i];
                    if (bundle.Commands.Count > 0)
                    {
                        entries.WriteByte(EntryBundle);
                        entries.WriteInt32(bundle.Tick);
                        WriteCommands(bundle.Commands, entries);
                        entryCount++;
                        i++;
                        continue;
                    }
                    // Length of the run of empty bundles on consecutive ticks starting at i. Range: 1 upward.
                    int run = 1;
                    while (i + run < bundles.Count && bundles[i + run].Commands.Count == 0 && bundles[i + run].Tick == bundle.Tick + run)
                    {
                        run++;
                    }
                    entries.WriteByte(EntryEmptyRun);
                    entries.WriteInt32(bundle.Tick);
                    entries.WriteInt32(run);
                    entryCount++;
                    i += run;
                }
                writer.WriteInt32(entryCount);
                writer.WriteBytes(entries.ToArray());
            }
            writer.WriteInt32(log.Ledger.Count);
            foreach (PauseAccountEntry entry in log.Ledger)
            {
                writer.WritePlayerId(entry.Player);
                writer.WriteInt32(entry.DisconnectPausesUsed);
                writer.WriteInt32(entry.PauseMsUsed);
            }
            writer.WriteInt32(log.Ranges.Count);
            foreach (PeerRangesEntry entry in log.Ranges)
            {
                writer.WritePeerId(entry.Peer);
                writer.WriteInt32(entry.Ranges.Count);
                foreach (TickRange range in entry.Ranges)
                {
                    writer.WriteInt32(range.From);
                    writer.WriteInt32(range.To);
                }
            }
            writer.WriteInt32(log.Dropped.Count);
            foreach (PeerId peer in log.Dropped)
            {
                writer.WritePeerId(peer);
            }
            writer.WriteBool(log.ActivePause != null);
            if (log.ActivePause != null)
            {
                WritePauseStarted(log.ActivePause, writer);
            }
            writer.WriteUInt32(log.NextPauseId);
        }

        /// <summary>Orders bundles of one peer by tick ascending.</summary>
        private static int CompareByTick(CommandBundleMessage a, CommandBundleMessage b)
        {
            return a.Tick.CompareTo(b.Tick);
        }

        /// <summary>Reads a sync log body written by <see cref="WriteSyncLogBody"/>. The bundles come back grouped by peer and ascending by tick.</summary>
        private SyncLog ReadSyncLogBody(NetReader reader)
        {
            // Scalars in wire order.
            int refereeTick = reader.ReadInt32();
            int checkTick = reader.ReadInt32();
            ulong checkHash = reader.ReadUInt64();
            // Peer group count. Range: 0 to MaxSmallList.
            int groupCount = ReadSmallCount(reader, "bundle peer");
            // The bundles, grouped by peer then tick.
            List<CommandBundleMessage> bundles = new List<CommandBundleMessage>();
            // Index of the group being read. Range: 0 to groupCount - 1.
            for (int g = 0; g < groupCount; g++)
            {
                // The group's peer and its entries, read from the group's byte block.
                PeerId peer = reader.ReadPeerId();
                int entryCount = reader.ReadInt32();
                if (entryCount < 0 || entryCount > MaxBundlesPerSync)
                {
                    throw new NetProtocolException("Bad sync log entry count " + entryCount + " for " + peer + ".");
                }
                NetReader entries = new NetReader(reader.ReadBytes());
                // Tick the next entry must start after, so ticks ascend and never repeat. Range: -1 upward.
                int lastTick = -1;
                // Index of the entry being read. Range: 0 to entryCount - 1.
                for (int e = 0; e < entryCount; e++)
                {
                    // Entry kind byte and its first tick.
                    byte kind = entries.ReadByte();
                    int tick = entries.ReadInt32();
                    if (tick <= lastTick)
                    {
                        throw new NetProtocolException("Sync log bundles of " + peer + " are not ascending at tick " + tick + ".");
                    }
                    if (kind == EntryEmptyRun)
                    {
                        // Run length. Range: 1 to the bundle bound, and the run must not overflow the tick range.
                        int run = entries.ReadInt32();
                        if (run < 1 || run > MaxBundlesPerSync || run - 1 > int.MaxValue - tick || bundles.Count + run > MaxBundlesPerSync)
                        {
                            throw new NetProtocolException("Bad empty bundle run of " + run + " at tick " + tick + " for " + peer + ".");
                        }
                        // Tick of the empty bundle being added. Range: tick to tick + run - 1.
                        for (int t = tick; t < tick + run; t++)
                        {
                            bundles.Add(Guard(() => new CommandBundleMessage(t, peer, Array.Empty<Command>())));
                        }
                        lastTick = tick + run - 1;
                    }
                    else if (kind == EntryBundle)
                    {
                        if (bundles.Count + 1 > MaxBundlesPerSync)
                        {
                            throw new NetProtocolException("Sync log holds more than " + MaxBundlesPerSync + " bundles.");
                        }
                        List<Command> commands = ReadCommands(entries);
                        bundles.Add(Guard(() => new CommandBundleMessage(tick, peer, commands)));
                        lastTick = tick;
                    }
                    else
                    {
                        throw new NetProtocolException("Unknown sync log entry byte " + kind + ".");
                    }
                }
                entries.RequireEnd();
            }
            // Ledger entry count. Range: 0 to MaxSmallList.
            int ledgerCount = ReadSmallCount(reader, "ledger");
            // The ledger entries in wire order.
            List<PauseAccountEntry> ledger = new List<PauseAccountEntry>(ledgerCount);
            for (int i = 0; i < ledgerCount; i++)
            {
                // Entry fields in wire order.
                PlayerId player = reader.ReadPlayerId();
                int pauses = reader.ReadInt32();
                int ms = reader.ReadInt32();
                ledger.Add(new PauseAccountEntry(player, pauses, ms));
            }
            // Range entry count. Range: 0 to MaxSmallList.
            int rangeEntries = ReadSmallCount(reader, "ranges");
            // The range entries in wire order.
            List<PeerRangesEntry> ranges = new List<PeerRangesEntry>(rangeEntries);
            for (int i = 0; i < rangeEntries; i++)
            {
                // The peer and its range count.
                PeerId peer = reader.ReadPeerId();
                int rangeCount = ReadSmallCount(reader, "peer ranges");
                // The peer's ranges in wire order.
                List<TickRange> peerRanges = new List<TickRange>(rangeCount);
                for (int r = 0; r < rangeCount; r++)
                {
                    // From and To in wire order.
                    int from = reader.ReadInt32();
                    int to = reader.ReadInt32();
                    peerRanges.Add(new TickRange(from, to));
                }
                ranges.Add(new PeerRangesEntry(peer, peerRanges));
            }
            // Dropped peer count. Range: 0 to MaxSmallList.
            int droppedCount = ReadSmallCount(reader, "dropped");
            // The dropped peers in wire order.
            List<PeerId> dropped = new List<PeerId>(droppedCount);
            for (int i = 0; i < droppedCount; i++)
            {
                dropped.Add(reader.ReadPeerId());
            }
            // The active pause when the flag is set.
            PauseStartedMessage? pause = reader.ReadBool() ? ReadPauseStarted(reader) : null;
            // The referee's next pause number.
            uint nextPauseId = reader.ReadUInt32();
            return Guard(() => new SyncLog(refereeTick, checkTick, checkHash, bundles, ledger, ranges, dropped, pause, nextPauseId));
        }

        /// <summary>Reads a list count bounded by <see cref="MaxSmallList"/>.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="what">Name of the list for the error message.</param>
        private static int ReadSmallCount(NetReader reader, string what)
        {
            // The count as written. Range: 0 to MaxSmallList.
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxSmallList)
            {
                throw new NetProtocolException("Bad " + what + " count " + count + ".");
            }
            return count;
        }
    }
}
