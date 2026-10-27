// rts/src/Replay/ReplayData.cs
// The in-memory form of a replay and the constants of the binary format. Part of Rts.Replay. A replay holds everything needed to
// re-simulate a match from tick 0 without the AI, the network or the player: format version, seed, map, players, the content digest
// of the data set, the tick count, every command that was applied in (tick, issuer, sequence) order, a hash checkpoint at every
// hash interval and the final hash (ADR-009). Immutable once built.
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Teams;

namespace Rts.Replay
{
    /// <summary>Constants of the binary replay layout (ADR-009). These are format rules, not tuning values: changing any of them is a format version bump.</summary>
    public static class ReplayFormat
    {
        /// <summary>The 8 bytes every replay file starts with: ASCII "RTSREPLY". Range: fixed.</summary>
        public static readonly byte[] Magic = { 0x52, 0x54, 0x53, 0x52, 0x45, 0x50, 0x4C, 0x59 };

        /// <summary>The format version this build writes and reads. Unit: format version. Range: 1 upward; raised whenever the layout or the meaning of a field changes.</summary>
        public const uint CurrentVersion = 1;

        /// <summary>Largest string a replay may hold. Unit: UTF-8 bytes. Range: fixed at 65535; keeps a corrupt length from allocating gigabytes.</summary>
        public const int MaxStringBytes = 65535;

        /// <summary>Bytes of the trailing checksum (a 64-bit FNV-1a over every earlier byte). Unit: bytes. Range: fixed at 8.</summary>
        public const int ChecksumBytes = 8;

        /// <summary>Smallest possible encoded command: tick 4, issuer 1, sequence 4, kind length 4, payload length 4. Unit: bytes. Range: fixed at 17.</summary>
        public const int MinCommandBytes = 17;

        /// <summary>Smallest possible encoded player: id 1, team 1, faction length 4, name length 4. Unit: bytes. Range: fixed at 10.</summary>
        public const int MinPlayerBytes = 10;

        /// <summary>Encoded size of one checkpoint: tick 4, hash 8. Unit: bytes. Range: fixed at 12.</summary>
        public const int CheckpointBytes = 12;
    }

    /// <summary>The simulation state hash at one tick, as reported by a StateHashedEvent while recording.</summary>
    public readonly struct ReplayCheckpoint
    {
        /// <summary>Tick the hash was taken at (the end of that tick's step). Unit: ticks. Range: 0 upward, a multiple of the replay's hash interval.</summary>
        public readonly int Tick;

        /// <summary>The state hash. Range: any ulong except 0.</summary>
        public readonly ulong Hash;

        /// <summary>Stores the checkpoint.</summary>
        /// <param name="tick">Tick of the hash. Unit: ticks. Range: 0 upward.</param>
        /// <param name="hash">The state hash. Range: any ulong.</param>
        public ReplayCheckpoint(int tick, ulong hash)
        {
            Tick = tick;
            Hash = hash;
        }
    }

    /// <summary>A whole replay in memory. Built by <see cref="ReplayRecorder"/> or <see cref="ReplayReader"/>, written by <see cref="ReplayWriter"/>, verified by <see cref="ReplayVerifier"/>.</summary>
    public sealed class ReplayData
    {
        /// <summary>Format version the replay was written with; always <see cref="ReplayFormat.CurrentVersion"/> for data this build can hold. Unit: format version.</summary>
        public uint FormatVersion => ReplayFormat.CurrentVersion;

        /// <summary>
        /// Digest of the data set the replay was recorded with: <c>Simulation.ContentDigest</c> (unit and faction definitions, the
        /// tuning texts and the map). A replay played against a different digest is refused. Range: any ulong.
        /// </summary>
        public ulong ContentDigest { get; }

        /// <summary>Seed of the simulation's random number generator. Range: any ulong.</summary>
        public ulong Seed { get; }

        /// <summary>ID of the map that was played, for example "greybox_1v1". Range: non-empty, an ID under rts/data/maps.</summary>
        public string MapId { get; }

        /// <summary>Ticks between state hashes (the match's state_hash_interval_ticks). Unit: ticks. Range: 1 upward.</summary>
        public int HashIntervalTicks { get; }

        /// <summary>Number of steps the match ran. Unit: ticks. Range: 0 upward. The last tick stepped is TickCount - 1.</summary>
        public int TickCount { get; }

        /// <summary>The player slots in the order the match was built with.</summary>
        public IReadOnlyList<PlayerSetup> Players { get; }

        /// <summary>Every command applied (accepted or rejected), sorted by (tick, issuer, sequence). Each has Tick below <see cref="TickCount"/>.</summary>
        public IReadOnlyList<Command> Commands { get; }

        /// <summary>State hash at every multiple of <see cref="HashIntervalTicks"/> below <see cref="TickCount"/>, in ascending tick order.</summary>
        public IReadOnlyList<ReplayCheckpoint> Checkpoints { get; }

        /// <summary>The state hash after the last step (the simulation clock at <see cref="TickCount"/>). Range: any ulong except 0.</summary>
        public ulong FinalHash { get; }

        /// <summary>
        /// Builds a replay. Commands are copied and put in (tick, issuer, sequence) order, keeping the given order for equal keys.
        /// Throws <see cref="ArgumentException"/> when a field is out of range.
        /// </summary>
        /// <param name="contentDigest">Content digest of the data set. Range: any ulong.</param>
        /// <param name="seed">Match seed. Range: any ulong.</param>
        /// <param name="mapId">Map ID. Range: non-empty.</param>
        /// <param name="hashIntervalTicks">Ticks between hashes. Unit: ticks. Range: 1 upward.</param>
        /// <param name="tickCount">Steps run. Unit: ticks. Range: 0 upward.</param>
        /// <param name="players">Player setup in slot order. Range: at least one player.</param>
        /// <param name="commands">Applied commands in any order; each must have Tick below <paramref name="tickCount"/>.</param>
        /// <param name="checkpoints">Hash checkpoints in ascending tick order, each below <paramref name="tickCount"/>.</param>
        /// <param name="finalHash">Hash after the last step. Range: any ulong.</param>
        public ReplayData(ulong contentDigest, ulong seed, string mapId, int hashIntervalTicks, int tickCount, IReadOnlyList<PlayerSetup> players,
            IReadOnlyList<Command> commands, IReadOnlyList<ReplayCheckpoint> checkpoints, ulong finalHash)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                throw new ArgumentException("A replay needs a map ID.", nameof(mapId));
            }
            if (hashIntervalTicks < 1)
            {
                throw new ArgumentException("The hash interval must be at least 1 tick.", nameof(hashIntervalTicks));
            }
            if (tickCount < 0)
            {
                throw new ArgumentException("The tick count cannot be negative.", nameof(tickCount));
            }
            if (players == null || players.Count == 0)
            {
                throw new ArgumentException("A replay needs at least one player.", nameof(players));
            }
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }
            if (checkpoints == null)
            {
                throw new ArgumentNullException(nameof(checkpoints));
            }
            // Commands with their arrival index, so equal (tick, issuer, sequence) keys keep the given order after sorting.
            List<KeyValuePair<int, Command>> indexed = new List<KeyValuePair<int, Command>>();
            foreach (Command command in commands)
            {
                if (command == null)
                {
                    throw new ArgumentException("A replay cannot hold a null command.", nameof(commands));
                }
                if (command.Tick >= tickCount)
                {
                    throw new ArgumentException("Command " + command + " is scheduled at or after the last tick (" + tickCount + ") and never ran.", nameof(commands));
                }
                indexed.Add(new KeyValuePair<int, Command>(indexed.Count, command));
            }
            indexed.Sort(CompareIndexed);
            // Sorted commands, ready to expose.
            List<Command> sorted = new List<Command>(indexed.Count);
            foreach (KeyValuePair<int, Command> pair in indexed)
            {
                sorted.Add(pair.Value);
            }
            // Previous checkpoint tick, to check ascending order. Unit: ticks. Range: -1 before the first.
            int previousTick = -1;
            foreach (ReplayCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint.Tick <= previousTick || checkpoint.Tick >= tickCount)
                {
                    throw new ArgumentException("Checkpoint ticks must ascend and stay below the tick count (found tick " + checkpoint.Tick + ").", nameof(checkpoints));
                }
                previousTick = checkpoint.Tick;
            }
            ContentDigest = contentDigest;
            Seed = seed;
            MapId = mapId;
            HashIntervalTicks = hashIntervalTicks;
            TickCount = tickCount;
            Players = new List<PlayerSetup>(players);
            Commands = sorted;
            Checkpoints = new List<ReplayCheckpoint>(checkpoints);
            FinalHash = finalHash;
        }

        /// <summary>Orders (arrival index, command) pairs by tick, issuer slot, sequence, then arrival index. The same key the simulation applies commands in.</summary>
        private static int CompareIndexed(KeyValuePair<int, Command> a, KeyValuePair<int, Command> b)
        {
            // Sign of the tick comparison. Range: -1, 0 or 1.
            int byTick = a.Value.Tick.CompareTo(b.Value.Tick);
            if (byTick != 0)
            {
                return byTick;
            }
            // Sign of the issuer-slot comparison. Range: -1, 0 or 1.
            int byIssuer = a.Value.Issuer.CompareTo(b.Value.Issuer);
            if (byIssuer != 0)
            {
                return byIssuer;
            }
            // Sign of the sequence comparison. Range: -1, 0 or 1.
            int bySequence = a.Value.Sequence.CompareTo(b.Value.Sequence);
            return bySequence != 0 ? bySequence : a.Key.CompareTo(b.Key);
        }
    }
}
