// rts/src/Replay/ReplayVerifier.cs
// Re-simulates a replay and compares every hash checkpoint and the final hash. Part of Rts.Replay. CreateSimulation builds and
// starts a simulation from a loaded DataSet and the replay's seed, map and players; Verify submits the recorded commands, steps
// the recorded tick count and reports the first tick whose hash differs. No AI and no clock: the command stream is the whole input.
// Refusal rule (ADR-009): a content digest or hash interval that differs from the replay's throws ReplayRefusedException before
// any step runs, because the result could only be a meaningless desync. Pure computation, no file access.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;

namespace Rts.Replay
{
    /// <summary>Result of re-simulating a replay.</summary>
    public sealed class ReplayVerification
    {
        /// <summary>True when every checkpoint and the final hash matched.</summary>
        public bool IsMatch { get; }

        /// <summary>Tick of the first mismatch, or -1 when everything matched. A mismatch of the final hash is reported at the tick count. Unit: ticks.</summary>
        public int DesyncTick { get; }

        /// <summary>Hash the replay recorded at <see cref="DesyncTick"/>, or 0 when matching or when the replay has no checkpoint there. Range: any ulong.</summary>
        public ulong ExpectedHash { get; }

        /// <summary>Hash the re-simulation produced at <see cref="DesyncTick"/>, or 0 when matching or when it produced none. Range: any ulong.</summary>
        public ulong ActualHash { get; }

        /// <summary>The re-simulation's state hash after the last step. Range: any ulong except 0.</summary>
        public ulong FinalHash { get; }

        /// <summary>Stores the result.</summary>
        /// <param name="desyncTick">First mismatching tick, or -1. Unit: ticks.</param>
        /// <param name="expected">Recorded hash at that tick. Range: any ulong.</param>
        /// <param name="actual">Re-simulated hash at that tick. Range: any ulong.</param>
        /// <param name="finalHash">Re-simulated hash after the last step. Range: any ulong.</param>
        public ReplayVerification(int desyncTick, ulong expected, ulong actual, ulong finalHash)
        {
            IsMatch = desyncTick < 0;
            DesyncTick = desyncTick;
            ExpectedHash = expected;
            ActualHash = actual;
            FinalHash = finalHash;
        }
    }

    /// <summary>Re-simulates replays. Stateless.</summary>
    public static class ReplayVerifier
    {
        /// <summary>
        /// Builds and starts the simulation a replay was recorded from: the replay's seed, map and players, the built-in faction mechanics
        /// and the data set's content and tuning. The Planetary Cycle list is passed only when the replay's CyclesEnabled flag is set, because a match without it hashes differently. Throws <see cref="ReplayRefusedException"/> when the replay's map is not in the data set.
        /// </summary>
        /// <param name="replay">The replay.</param>
        /// <param name="data">The loaded data set.</param>
        public static Simulation CreateSimulation(ReplayData replay, DataSet data)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (!data.Maps.TryGetValue(replay.MapId, out MapDefinition? map))
            {
                throw new ReplayRefusedException("the replay was recorded on map '" + replay.MapId + "', which is not in this data set");
            }
            // Match inputs built from the replay header and the data set.
            SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(replay.Seed), data.Content, replay.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, map, replay.CyclesEnabled ? data.Cycles.Values : null);
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>
        /// Submits the recorded commands to <paramref name="sim"/>, steps it <see cref="ReplayData.TickCount"/> times and compares each
        /// state hash with the recorded checkpoint of the same tick, then the final hash. Throws <see cref="ReplayRefusedException"/> when
        /// the simulation's content digest or hash interval differs from the replay's, and InvalidOperationException when the simulation
        /// is not freshly started at tick 0.
        /// </summary>
        /// <param name="replay">The replay.</param>
        /// <param name="sim">A started simulation at tick 0, normally from <see cref="CreateSimulation"/>.</param>
        public static ReplayVerification Verify(ReplayData replay, Simulation sim)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (!sim.IsStarted || sim.Clock.Tick != 0)
            {
                throw new InvalidOperationException("Verify needs a freshly started simulation at tick 0.");
            }
            if (sim.ContentDigest != replay.ContentDigest)
            {
                throw new ReplayRefusedException("the replay was recorded with data digest " + replay.ContentDigest.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)
                    + " but the data set in use has digest " + sim.ContentDigest.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)
                    + "; use the unit, faction, tuning and map files the replay was recorded with");
            }
            if (sim.Settings.StateHashIntervalTicks != replay.HashIntervalTicks)
            {
                throw new ReplayRefusedException("the replay hashes every " + replay.HashIntervalTicks + " ticks but this data set hashes every "
                    + sim.Settings.StateHashIntervalTicks);
            }
            foreach (Command command in replay.Commands)
            {
                sim.Submit(command);
            }
            // Index of the next recorded checkpoint not yet compared. Range: 0 to Checkpoints.Count.
            int next = 0;
            // Step index. Unit: ticks. Range: 0 to TickCount - 1.
            for (int i = 0; i < replay.TickCount; i++)
            {
                sim.Step();
                foreach (SimEvent simEvent in sim.Events.Current)
                {
                    if (!(simEvent is StateHashedEvent hashed))
                    {
                        continue;
                    }
                    // Recorded checkpoint for this tick, or null when the replay has none here.
                    ReplayCheckpoint? expected = next < replay.Checkpoints.Count ? replay.Checkpoints[next] : (ReplayCheckpoint?)null;
                    if (expected == null || expected.Value.Tick != hashed.Tick)
                    {
                        return new ReplayVerification(hashed.Tick, 0, hashed.Hash, sim.ComputeStateHash());
                    }
                    if (expected.Value.Hash != hashed.Hash)
                    {
                        return new ReplayVerification(hashed.Tick, expected.Value.Hash, hashed.Hash, sim.ComputeStateHash());
                    }
                    next++;
                }
            }
            if (next < replay.Checkpoints.Count)
            {
                // A recorded checkpoint the re-simulation never produced (a hand-built replay with an off-interval tick).
                return new ReplayVerification(replay.Checkpoints[next].Tick, replay.Checkpoints[next].Hash, 0, sim.ComputeStateHash());
            }
            // Hash after the last step, to compare with the recorded final hash.
            ulong finalHash = sim.ComputeStateHash();
            if (finalHash != replay.FinalHash)
            {
                return new ReplayVerification(replay.TickCount, replay.FinalHash, finalHash, finalHash);
            }
            return new ReplayVerification(-1, 0, 0, finalHash);
        }
    }
}
