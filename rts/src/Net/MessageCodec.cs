// rts/src/Net/MessageCodec.cs
// Turns protocol messages into bytes and back. Part of Rts.Net (ADR-008). Wire layout of every message: protocol
// version (uint16), message kind (one byte), then the kind's fields in the order its class lists them; lists are an
// int32 count followed by the items. Commands inside bundles go through the CommandCodecRegistry. A message with a
// different version, an unknown kind, a bad enum byte, a truncated body or trailing bytes throws NetProtocolException.
// ProtocolVersion changes whenever any layout here or in a codec changes; peers on different versions cannot play.
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
        /// <summary>Version written at the start of every message. Range: fixed per build; bump on any wire change.</summary>
        public const ushort ProtocolVersion = 1;

        /// <summary>Most commands one bundle may carry, a sanity bound against hostile lengths. Unit: commands. Range: fixed protocol limit.</summary>
        public const int MaxCommandsPerBundle = 1 << 14;

        /// <summary>Most bundles a sync log may carry (long matches at 20 ticks per second times many peers stay far below). Unit: bundles. Range: fixed protocol limit.</summary>
        public const int MaxBundlesPerSync = 1 << 22;

        /// <summary>Most entries a list of players, peers or ranges may have. Range: fixed protocol limit.</summary>
        public const int MaxSmallList = 1 << 12;

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
                case SyncLogMessage log:
                    WriteSyncLog(log, writer);
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
                case MessageKind.SyncLog:
                    message = ReadSyncLog(reader);
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
        private static T Guard<T>(Func<T> build) where T : NetMessage
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
            writer.WriteInt32(bundle.Commands.Count);
            foreach (Command command in bundle.Commands)
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
            return Guard(() => new CommandBundleMessage(tick, peer, commands));
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

        /// <summary>Writes the sync log: scalars, bundles, ledger, ranges, dropped peers, the optional active pause, next pause id.</summary>
        private void WriteSyncLog(SyncLogMessage log, NetWriter writer)
        {
            writer.WriteInt32(log.RefereeTick);
            writer.WriteInt32(log.CheckTick);
            writer.WriteUInt64(log.CheckHash);
            writer.WriteInt32(log.Bundles.Count);
            foreach (CommandBundleMessage bundle in log.Bundles)
            {
                WriteBundle(bundle, writer);
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

        /// <summary>Reads a sync log written by <see cref="WriteSyncLog"/>.</summary>
        private SyncLogMessage ReadSyncLog(NetReader reader)
        {
            // Scalars in wire order.
            int refereeTick = reader.ReadInt32();
            int checkTick = reader.ReadInt32();
            ulong checkHash = reader.ReadUInt64();
            // Bundle count. Range: 0 to MaxBundlesPerSync.
            int bundleCount = reader.ReadInt32();
            if (bundleCount < 0 || bundleCount > MaxBundlesPerSync)
            {
                throw new NetProtocolException("Bad bundle count " + bundleCount + " in a sync log.");
            }
            // The bundles in wire order.
            List<CommandBundleMessage> bundles = new List<CommandBundleMessage>(bundleCount);
            // Index of the bundle being read. Range: 0 to bundleCount - 1.
            for (int i = 0; i < bundleCount; i++)
            {
                bundles.Add(ReadBundle(reader));
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
            return Guard(() => new SyncLogMessage(refereeTick, checkTick, checkHash, bundles, ledger, ranges, dropped, pause, nextPauseId));
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
