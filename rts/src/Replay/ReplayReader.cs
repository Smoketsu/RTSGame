// rts/src/Replay/ReplayReader.cs
// Parses the bytes of a replay file back into a ReplayData (layout: ReplayWriter.cs, ADR-009). Part of Rts.Replay. In memory only.
// Checks in this order so the message names the real problem: file long enough for the fixed header, magic, format version
// (a different version is REFUSED before anything else is read, because later fields may mean something else), checksum,
// then every field. Any bad field is a ReplayFormatException; nothing in here crashes on corrupt input.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Replay
{
    /// <summary>Parses replays. Stateless.</summary>
    public static class ReplayReader
    {
        /// <summary>
        /// Decodes a replay. Throws <see cref="ReplayRefusedException"/> for another format version and <see cref="ReplayFormatException"/>
        /// for anything else wrong with the bytes (not a replay, truncated, checksum mismatch, unknown command kind, field out of range).
        /// </summary>
        /// <param name="bytes">The whole file. Range: not null.</param>
        /// <param name="codecs">The shared command codecs; use <c>BuiltInMechanics.CreateCodecRegistry()</c> plus any plug-in codecs.</param>
        public static ReplayData Read(byte[] bytes, CommandCodecRegistry codecs)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (codecs == null)
            {
                throw new ArgumentNullException(nameof(codecs));
            }
            // Smallest file that still has magic and version. Unit: bytes.
            int prefixBytes = ReplayFormat.Magic.Length + 4;
            if (bytes.Length < prefixBytes)
            {
                throw new ReplayFormatException("this is not a replay file (only " + bytes.Length + " bytes long)");
            }
            // Index of the magic byte being compared. Unit: bytes. Range: 0 to 7.
            for (int i = 0; i < ReplayFormat.Magic.Length; i++)
            {
                if (bytes[i] != ReplayFormat.Magic[i])
                {
                    throw new ReplayFormatException("this is not a replay file (the RTSREPLY marker is missing)");
                }
            }
            // Version field, the 4 bytes after the magic. Unit: format version. Range: any uint.
            uint version = new ReplayBinaryReader(bytes, ReplayFormat.Magic.Length, prefixBytes).ReadU32();
            if (version != ReplayFormat.CurrentVersion)
            {
                throw new ReplayRefusedException("this replay uses format version " + version.ToString(CultureInfo.InvariantCulture)
                    + " but this build reads version " + ReplayFormat.CurrentVersion.ToString(CultureInfo.InvariantCulture)
                    + "; play it with the build that recorded it");
            }
            if (bytes.Length < prefixBytes + ReplayFormat.ChecksumBytes)
            {
                throw new ReplayFormatException("the replay is truncated (no room for the checksum)");
            }
            // Number of bytes the checksum covers. Unit: bytes.
            int bodyLength = bytes.Length - ReplayFormat.ChecksumBytes;
            // Checksum stored in the last 8 bytes. Range: any ulong.
            ulong storedChecksum = new ReplayBinaryReader(bytes, bodyLength, bytes.Length).ReadU64();
            if (storedChecksum != ReplayWriter.Checksum(bytes, bodyLength))
            {
                throw new ReplayFormatException("the replay is damaged (its checksum does not match its contents)");
            }
            // Reader limited to the body between the version field and the checksum, so no field can run into the checksum.
            ReplayBinaryReader fields = new ReplayBinaryReader(bytes, prefixBytes, bodyLength);

            ulong contentDigest = fields.ReadU64();
            ulong seed = fields.ReadU64();
            uint hashInterval = fields.ReadU32();
            uint tickCount = fields.ReadU32();
            if (hashInterval < 1 || hashInterval > int.MaxValue || tickCount > int.MaxValue)
            {
                throw new ReplayFormatException("the replay header holds an impossible hash interval (" + hashInterval + ") or tick count (" + tickCount + ")");
            }
            // Header flags. Range: only ReplayFormat.KnownFlags bits may be set.
            byte flags = fields.ReadU8();
            if ((flags & ~ReplayFormat.KnownFlags) != 0)
            {
                throw new ReplayFormatException("the replay header holds the unknown flag bits 0x" + flags.ToString("x2", CultureInfo.InvariantCulture));
            }
            bool cyclesEnabled = (flags & ReplayFormat.FlagCyclesEnabled) != 0;
            string mapId = fields.ReadString();

            // Player count and the players, in file order. Range: 1 upward.
            uint playerCount = fields.ReadU32();
            fields.RequireCount(playerCount, ReplayFormat.MinPlayerBytes, "players");
            List<PlayerSetup> players = new List<PlayerSetup>();
            // Index of the player being read. Unit: players. Range: 0 to playerCount - 1.
            for (uint i = 0; i < playerCount; i++)
            {
                PlayerId id = fields.ReadPlayer();
                TeamId team = new TeamId(fields.ReadU8());
                string faction = fields.ReadString();
                string name = fields.ReadString();
                players.Add(new PlayerSetup(id, team, faction, name));
            }

            // Command count and the commands, in file order.
            uint commandCount = fields.ReadU32();
            fields.RequireCount(commandCount, ReplayFormat.MinCommandBytes, "commands");
            List<Command> commands = new List<Command>();
            // Index of the command being read. Unit: commands. Range: 0 to commandCount - 1.
            for (uint i = 0; i < commandCount; i++)
            {
                commands.Add(ReadCommand(fields, codecs));
            }

            // Checkpoint count and the checkpoints, in file order.
            uint checkpointCount = fields.ReadU32();
            fields.RequireCount(checkpointCount, ReplayFormat.CheckpointBytes, "checkpoints");
            List<ReplayCheckpoint> checkpoints = new List<ReplayCheckpoint>();
            // Index of the checkpoint being read. Unit: checkpoints. Range: 0 to checkpointCount - 1.
            for (uint i = 0; i < checkpointCount; i++)
            {
                // Tick and hash of this checkpoint, read in file order.
                int tick = fields.ReadI32();
                checkpoints.Add(new ReplayCheckpoint(tick, fields.ReadU64()));
            }
            ulong finalHash = fields.ReadU64();
            if (fields.Remaining != 0)
            {
                throw new ReplayFormatException("the replay has " + fields.Remaining + " unexpected bytes after its final hash");
            }
            try
            {
                return new ReplayData(contentDigest, seed, mapId, (int)hashInterval, (int)tickCount, players, commands, checkpoints, finalHash, cyclesEnabled);
            }
            catch (ArgumentException ex)
            {
                throw new ReplayFormatException("the replay holds inconsistent data: " + ex.Message);
            }
        }

        /// <summary>Reads one command record: tick, issuer, sequence, kind, payload length and payload.</summary>
        /// <param name="reader">Reader positioned at the record.</param>
        /// <param name="codecs">The shared command codecs.</param>
        private static Command ReadCommand(ReplayBinaryReader reader, CommandCodecRegistry codecs)
        {
            // Scheduling fields of the record. Tick: ticks, 0 upward. Issuer: slot or 255. Sequence: 0 upward.
            int tick = reader.ReadI32();
            PlayerId issuer = reader.ReadPlayer();
            uint sequence = reader.ReadU32();
            string kind = reader.ReadString();
            // Exactly this command's payload bytes.
            byte[] payload = reader.ReadBlock(reader.ReadU32());
            if (tick < 0)
            {
                throw new ReplayFormatException("a '" + kind + "' command has the negative tick " + tick);
            }
            if (!codecs.Contains(kind))
            {
                throw new ReplayFormatException("the replay holds a command of kind '" + kind + "' that this build has no codec for");
            }
            try
            {
                return codecs.DecodePayload(kind, tick, issuer, sequence, payload);
            }
            catch (CommandCodecException ex)
            {
                throw new ReplayFormatException("a '" + kind + "' command is invalid: " + ex.Message);
            }
        }
    }
}
