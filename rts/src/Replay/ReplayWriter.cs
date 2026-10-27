// rts/src/Replay/ReplayWriter.cs
// Turns a ReplayData into the bytes of a replay file (ADR-009). Part of Rts.Replay. In memory only: the host writes the returned
// bytes to disk. The layout, in order, all little-endian:
//   magic (8 bytes "RTSREPLY"), format version u32, content digest u64, seed u64, hash interval u32, tick count u32,
//   map ID string, player count u32 then per player (id u8, team u8, faction string, name string),
//   command count u32 then per command (tick i32, issuer u8, sequence u32, kind string, payload length u32, payload bytes),
//   checkpoint count u32 then per checkpoint (tick i32, hash u64), final hash u64, checksum u64 (FNV-1a over every earlier byte).
using System;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Replay
{
    /// <summary>Serialises replays. Stateless.</summary>
    public static class ReplayWriter
    {
        /// <summary>
        /// Encodes <paramref name="replay"/>. Throws <see cref="ReplayFormatException"/> when a command has no codec in <paramref name="codecs"/>
        /// (a faction plug-in forgot to register one).
        /// </summary>
        /// <param name="replay">The replay to encode.</param>
        /// <param name="codecs">Codecs by command kind; use <see cref="CommandCodecRegistry.CreateBuiltIn"/> plus any plug-in codecs.</param>
        public static byte[] Write(ReplayData replay, CommandCodecRegistry codecs)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }
            if (codecs == null)
            {
                throw new ArgumentNullException(nameof(codecs));
            }
            // Buffer receiving the file bytes.
            ReplayBinaryWriter writer = new ReplayBinaryWriter();
            writer.WriteRaw(ReplayFormat.Magic);
            writer.WriteU32(replay.FormatVersion);
            writer.WriteU64(replay.ContentDigest);
            writer.WriteU64(replay.Seed);
            writer.WriteU32((uint)replay.HashIntervalTicks);
            writer.WriteU32((uint)replay.TickCount);
            writer.WriteString(replay.MapId);

            writer.WriteU32((uint)replay.Players.Count);
            foreach (PlayerSetup player in replay.Players)
            {
                writer.WritePlayer(player.Id);
                writer.WriteU8(player.Team.Value);
                writer.WriteString(player.FactionId);
                writer.WriteString(player.DisplayName);
            }

            writer.WriteU32((uint)replay.Commands.Count);
            foreach (Command command in replay.Commands)
            {
                if (!codecs.TryGet(command.Kind, out ICommandCodec? codec) || codec == null)
                {
                    throw new ReplayFormatException("no replay codec is registered for command kind '" + command.Kind + "'");
                }
                // Payload buffer, so its length can be written before it.
                ReplayBinaryWriter payload = new ReplayBinaryWriter();
                codec.Write(payload, command);
                writer.WriteI32(command.Tick);
                writer.WritePlayer(command.Issuer);
                writer.WriteU32(command.Sequence);
                writer.WriteString(command.Kind);
                writer.WriteU32((uint)payload.Length);
                writer.WriteRaw(payload.ToArray());
            }

            writer.WriteU32((uint)replay.Checkpoints.Count);
            foreach (ReplayCheckpoint checkpoint in replay.Checkpoints)
            {
                writer.WriteI32(checkpoint.Tick);
                writer.WriteU64(checkpoint.Hash);
            }
            writer.WriteU64(replay.FinalHash);
            // Everything written so far, which the checksum covers.
            byte[] body = writer.ToArray();
            writer.WriteU64(Checksum(body, body.Length));
            return writer.ToArray();
        }

        /// <summary>FNV-1a 64-bit checksum of the first <paramref name="length"/> bytes, using the simulation's StateHasher so there is one hash function in the codebase (ADR-005).</summary>
        /// <param name="bytes">The bytes. Range: not null.</param>
        /// <param name="length">How many leading bytes to cover. Unit: bytes. Range: 0 to bytes.Length.</param>
        internal static ulong Checksum(byte[] bytes, int length)
        {
            // Hasher fed one byte at a time.
            StateHasher hasher = new StateHasher();
            // Index of the byte being fed. Unit: bytes. Range: 0 to length - 1.
            for (int i = 0; i < length; i++)
            {
                hasher.Write(bytes[i]);
            }
            return hasher.Hash;
        }
    }
}
