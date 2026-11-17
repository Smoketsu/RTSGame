// rts/tools/BalanceHarness/ResponseSeries.cs
// What the response observer records during one scenario run (spec: rts/docs/specs/skirmish_ai_v3.md A.4): plain numbers and lists, no simulation objects, so the
// check methods in ResponseChecks are pure functions of a series and tests can build synthetic series. Filled by ResponseRunner's observer, which only reads the
// simulation. Harness-only; positions are doubles (tiles) because nothing here feeds back into a simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A point in time with an amount: a death or a trained unit.</summary>
    public readonly struct TimedAmount
    {
        /// <summary>Tick the thing happened. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>Unit definition ID involved, or empty when the amount alone matters.</summary>
        public string Definition { get; }

        /// <summary>Supply involved (army supply of the unit). Unit: supply. Range: 0 upward.</summary>
        public int Supply { get; }

        /// <summary>Bundles the values.</summary>
        public TimedAmount(int tick, string definition, int supply)
        {
            Tick = tick;
            Definition = definition;
            Supply = supply;
        }
    }

    /// <summary>A point on the map in tiles.</summary>
    public readonly struct TilePoint
    {
        /// <summary>Column coordinate. Unit: tiles. Range: 0 to the map width.</summary>
        public double X { get; }

        /// <summary>Row coordinate. Unit: tiles. Range: 0 to the map height.</summary>
        public double Y { get; }

        /// <summary>Bundles the values.</summary>
        public TilePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Squared distance to <paramref name="other"/>. Unit: tiles squared. Range: 0 upward.</summary>
        public double DistanceSquared(TilePoint other)
        {
            return (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
        }
    }

    /// <summary>The state of the AI under test, sampled once per game second.</summary>
    public sealed class ResponseSample
    {
        /// <summary>Tick of the sample (a multiple of the tick rate, or the last tick of the run). Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>Complete own non-worker, non-HQ units, static defences included, whose weapon can target air. Unit: units. Range: 0 upward.</summary>
        public int AaUnits { get; }

        /// <summary>Own static defences in any state (under construction too). Unit: positions in tiles.</summary>
        public IReadOnlyList<TilePoint> StaticDefences { get; }

        /// <summary>Own complete drop-offs (depots and a Rooted HQ). Unit: positions in tiles.</summary>
        public IReadOnlyList<TilePoint> DropOffs { get; }

        /// <summary>Bundles the values.</summary>
        public ResponseSample(int tick, int aaUnits, IReadOnlyList<TilePoint> staticDefences, IReadOnlyList<TilePoint> dropOffs)
        {
            Tick = tick;
            AaUnits = aaUnits;
            StaticDefences = staticDefences;
            DropOffs = dropOffs;
        }
    }

    /// <summary>Everything the checks read about one run.</summary>
    public sealed class ResponseSeries
    {
        /// <summary>Simulation tick rate. Unit: ticks per second. Range: 1 to 1000 (20 in the shipped data).</summary>
        public int TicksPerSecond { get; set; } = 20;

        /// <summary>Last tick stepped. Unit: ticks. Range: 0 upward.</summary>
        public int EndTick { get; set; }

        /// <summary>True when the match ended with the AI's team as the only team standing.</summary>
        public bool AiWon { get; set; }

        /// <summary>Tick the AI's HQ was removed, or -1 when it stood at the end of the run. Unit: ticks. Range: -1 upward.</summary>
        public int HqDeathTick { get; set; } = -1;

        /// <summary>First tick an enemy air unit was visible to the AI's team, or -1. Unit: ticks. Range: -1 upward.</summary>
        public int FirstEnemyAirVisibleTick { get; set; } = -1;

        /// <summary>
        /// First tick the AI's enemy model reported air supply above 0 (AiMemory.ModelAirKnownTick, spec B.3), or -1. The runner copies it from the AI under test; it stays -1
        /// for a profile with use_enemy_model off, and the air_answer check part 1 then fails with "model_air_known_s=none".
        /// </summary>
        public int ModelAirKnownTick { get; set; } = -1;

        /// <summary>First tick any enemy army unit (mobile, not a worker, not an HQ) was visible to the AI's team, or -1. Unit: ticks. Range: -1 upward.</summary>
        public int FirstEnemyArmyVisibleTick { get; set; } = -1;

        /// <summary>Supply of the distinct enemy army units ever visible to the AI's team, by armour type. Unit: supply. Range: 0 upward per key.</summary>
        public SortedDictionary<string, int> SeenArmourSupply { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Definition IDs of the enemy army units ever visible to the AI's team.</summary>
        public SortedSet<string> SeenEnemyDefinitions { get; } = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Army units the AI's producers finished, in order (Definition and Supply set).</summary>
        public List<TimedAmount> Trained { get; } = new List<TimedAmount>();

        /// <summary>Enemy army units that died, with their supply.</summary>
        public List<TimedAmount> EnemyArmyKilled { get; } = new List<TimedAmount>();

        /// <summary>The AI's own army units that died, with their supply.</summary>
        public List<TimedAmount> OwnArmyLost { get; } = new List<TimedAmount>();

        /// <summary>Ticks at which an own worker died. Unit: ticks.</summary>
        public List<int> WorkerLossTicks { get; } = new List<int>();

        /// <summary>First tick an enemy structure (a unit with a footprint) was destroyed, or -1. Unit: ticks. Range: -1 upward.</summary>
        public int FirstEnemyStructureKillTick { get; set; } = -1;

        /// <summary>Where the opponent's timed spawns appear (the position of its first spawn after tick 0), or null when the script has none.</summary>
        public TilePoint? WaveSpawnPoint { get; set; }

        /// <summary>Per-second samples of the AI under test, ascending tick; at least one after a run.</summary>
        public List<ResponseSample> Samples { get; } = new List<ResponseSample>();

        /// <summary>The last sample at or before <paramref name="seconds"/>, or the first sample when none is that early, or null when there are no samples. Unit: game seconds.</summary>
        /// <param name="seconds">Game seconds. Range: 0 upward.</param>
        public ResponseSample? SampleAt(int seconds)
        {
            // Tick wanted. Unit: ticks.
            int tick = seconds * TicksPerSecond;
            ResponseSample? best = null;
            foreach (ResponseSample sample in Samples)
            {
                if (sample.Tick <= tick || best == null)
                {
                    best = sample;
                }
            }
            return best;
        }

        /// <summary>True when the AI's HQ stood at game second <paramref name="seconds"/> (it was never removed, or removed later).</summary>
        /// <param name="seconds">Game seconds. Range: 0 upward.</param>
        public bool HqAliveAt(int seconds)
        {
            return HqDeathTick < 0 || HqDeathTick > seconds * TicksPerSecond;
        }
    }
}
