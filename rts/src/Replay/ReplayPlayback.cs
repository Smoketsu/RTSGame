// rts/src/Replay/ReplayPlayback.cs
// Step-by-step player of a recorded replay (spec player_interface_2.md gap G3). Part of Rts.Replay. Builds the simulation with
// ReplayVerifier.CreateSimulation, applies the refusal rules (content digest, hash interval), submits every recorded command, then
// advances one tick per Step() and compares each state hash with the next recorded checkpoint. ReplayVerifier.Verify runs this class
// to the end, so there is one comparison path. Pure computation: no clock, no random numbers, no file access.
using System;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;

namespace Rts.Replay
{
    /// <summary>Plays a replay one tick at a time and checks it against its recorded hashes.</summary>
    public sealed class ReplayPlayback
    {
        /// <summary>The replay being played. Never null.</summary>
        private readonly ReplayData _replay;

        /// <summary>Index of the next recorded checkpoint not yet compared. Range: 0 to Checkpoints.Count.</summary>
        private int _next;

        /// <summary>The simulation being stepped; read it to draw, never submit to it. Never null.</summary>
        public Simulation Sim { get; }

        /// <summary>Ticks the replay runs for. Unit: ticks. Range: 0 or more.</summary>
        public int TickCount => _replay.TickCount;

        /// <summary>True once the clock is at <see cref="TickCount"/> and the end checks ran, or a mismatch was found.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Tick of the first mismatch, or -1 while none. A final-hash mismatch is reported at <see cref="TickCount"/>. Unit: ticks.</summary>
        public int DesyncTick { get; private set; } = -1;

        /// <summary>Recorded hash at <see cref="DesyncTick"/>, or 0 when none or the replay has no checkpoint there. Range: any ulong.</summary>
        public ulong ExpectedHash { get; private set; }

        /// <summary>Re-simulated hash at <see cref="DesyncTick"/>, or 0 when none or the simulation produced none. Range: any ulong.</summary>
        public ulong ActualHash { get; private set; }

        /// <summary>State hash after the last tick, or 0 until the last tick ran (also 0 when stopped early by a checkpoint mismatch). Range: any ulong.</summary>
        public ulong FinalHash { get; private set; }

        /// <summary>True once the last tick ran with no mismatch anywhere, the final hash included.</summary>
        public bool FinalMatches => IsFinished && DesyncTick < 0;

        /// <summary>Builds the simulation from the data set, applies the refusals and submits the recorded commands. Throws <see cref="ReplayRefusedException"/> as <see cref="ReplayVerifier.Verify"/> does.</summary>
        /// <param name="replay">The replay.</param>
        /// <param name="data">The loaded data set.</param>
        public ReplayPlayback(ReplayData replay, DataSet data)
            : this(replay, ReplayVerifier.CreateSimulation(replay, data))
        {
        }

        /// <summary>Same as the public constructor for a simulation the caller built; it must be started at tick 0 (InvalidOperationException otherwise).</summary>
        /// <param name="replay">The replay.</param>
        /// <param name="sim">A started simulation at tick 0.</param>
        internal ReplayPlayback(ReplayData replay, Simulation sim)
        {
            _replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Sim = sim ?? throw new ArgumentNullException(nameof(sim));
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
            if (replay.TickCount == 0)
            {
                CheckEnd();
            }
        }

        /// <summary>
        /// Steps one tick and compares a state hash produced on it with the next recorded checkpoint. Returns true when a tick ran
        /// (the one that finds a mismatch or ends the replay included) and false, doing nothing, when already finished.
        /// </summary>
        public bool Step()
        {
            if (IsFinished)
            {
                return false;
            }
            Sim.Step();
            foreach (SimEvent simEvent in Sim.Events.Current)
            {
                if (!(simEvent is StateHashedEvent hashed))
                {
                    continue;
                }
                // Recorded checkpoint for this tick, or null when the replay has none here.
                ReplayCheckpoint? expected = _next < _replay.Checkpoints.Count ? _replay.Checkpoints[_next] : (ReplayCheckpoint?)null;
                if (expected == null || expected.Value.Tick != hashed.Tick)
                {
                    Desync(hashed.Tick, 0, hashed.Hash);
                    return true;
                }
                if (expected.Value.Hash != hashed.Hash)
                {
                    Desync(hashed.Tick, expected.Value.Hash, hashed.Hash);
                    return true;
                }
                _next++;
            }
            if (Sim.Clock.Tick >= _replay.TickCount)
            {
                CheckEnd();
            }
            return true;
        }

        /// <summary>Records the first mismatch and finishes playback.</summary>
        /// <param name="tick">Tick of the mismatch. Unit: ticks.</param>
        /// <param name="expected">Recorded hash. Range: any ulong.</param>
        /// <param name="actual">Re-simulated hash. Range: any ulong.</param>
        private void Desync(int tick, ulong expected, ulong actual)
        {
            DesyncTick = tick;
            ExpectedHash = expected;
            ActualHash = actual;
            IsFinished = true;
        }

        /// <summary>The checks after the last tick: unreached recorded checkpoints, then the recorded final hash.</summary>
        private void CheckEnd()
        {
            if (_next < _replay.Checkpoints.Count)
            {
                // A recorded checkpoint the re-simulation never produced (a hand-built replay with an off-interval tick).
                Desync(_replay.Checkpoints[_next].Tick, _replay.Checkpoints[_next].Hash, 0);
                return;
            }
            // Hash after the last step, to compare with the recorded final hash.
            ulong finalHash = Sim.ComputeStateHash();
            FinalHash = finalHash;
            if (finalHash != _replay.FinalHash)
            {
                Desync(_replay.TickCount, _replay.FinalHash, finalHash);
                return;
            }
            IsFinished = true;
        }
    }
}
