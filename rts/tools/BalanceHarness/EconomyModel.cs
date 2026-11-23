// rts/tools/BalanceHarness/EconomyModel.cs
// Package E-OPT (rts/docs/specs/ai_economy_calc.md 3.2): a small integer economy model of one player on one map, stepped once per second, and an exhaustive grid search for the best worker and depot schedule in it.
// The model has two uses: Income plays one schedule (a worker cap N and depot triggers) and Replay plays the AI's own schedule (its workers per bucket and its drop-off completion seconds), so the econ_gap check can
// compare OPT (best schedule), MOD (the AI's schedule in the model) and REAL (the AI's deposits). Integers and Fix64 only; fields, bases and schedules are visited in ascending index order, so a run is exact and
// repeatable. Tool-side measurement only: no controller reads it. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One resource field of the model's map (public map data; the model has no fog and uses start amounts).</summary>
    public readonly struct ModelField
    {
        /// <summary>Centre of the field's tile. Unit: tiles.</summary>
        public FixVec2 Centre { get; }

        /// <summary>True for a rare field (2 gather slots today), false for common (3). Unit: boolean.</summary>
        public bool Rare { get; }

        /// <summary>Resource the field starts with. Unit: resource units. Range: 1 upward.</summary>
        public int Amount { get; }

        /// <summary>Bundles the values.</summary>
        public ModelField(FixVec2 centre, bool rare, int amount)
        {
            Centre = centre;
            Rare = rare;
            Amount = amount;
        }
    }

    /// <summary>Every number the model reads, as plain values (the check fills them from the data set; a test fills them by hand).</summary>
    public sealed class ModelInputs
    {
        /// <summary>Simulation ticks per second. Unit: ticks per second. Range: 1 upward (20 today).</summary>
        public int TicksPerSecond { get; init; } = 20;

        /// <summary>Length of one income bucket. Unit: seconds. Range: 1 upward (30 today).</summary>
        public int BucketSeconds { get; init; } = 30;

        /// <summary>The map's resource fields in index order.</summary>
        public IReadOnlyList<ModelField> Fields { get; init; } = Array.Empty<ModelField>();

        /// <summary>The slot's spawn point, where the HQ stands. Unit: tiles.</summary>
        public FixVec2 Spawn { get; init; }

        /// <summary>Worker speed. Unit: tiles per second. Range: 0 upward (0 counts as no walk time).</summary>
        public Fix64 WorkerSpeed { get; init; }

        /// <summary>Worker cost, common plus rare (rare valued 1 to 1). Unit: resource units. Range: 1 upward.</summary>
        public int WorkerCost { get; init; } = 50;

        /// <summary>Worker build time. Unit: ticks. Range: 1 upward.</summary>
        public int WorkerBuildTicks { get; init; } = 240;

        /// <summary>True when the faction plan builds depots; false gives no depot in any schedule. Unit: boolean.</summary>
        public bool HasDepot { get; init; } = true;

        /// <summary>Depot cost, common plus rare. Unit: resource units. Range: 0 upward.</summary>
        public int DepotCost { get; init; } = 100;

        /// <summary>Depot build time. Unit: ticks. Range: 1 upward.</summary>
        public int DepotBuildTicks { get; init; } = 400;

        /// <summary>Depot footprint radius. Unit: tiles. Range: 0 to 4.</summary>
        public Fix64 DepotRadius { get; init; }

        /// <summary>HQ footprint radius. Unit: tiles. Range: 0 to 4.</summary>
        public Fix64 HqRadius { get; init; }

        /// <summary>Resource per load. Unit: resource units. Range: 1 to 100.</summary>
        public int AmountPerTrip { get; init; } = 5;

        /// <summary>Ticks a gather slot is held per load. Unit: ticks. Range: 1 upward.</summary>
        public int HoldTicks { get; init; } = 40;

        /// <summary>Gather slots of a common field. Unit: workers. Range: 1 to 16.</summary>
        public int SlotsCommon { get; init; } = 3;

        /// <summary>Gather slots of a rare field. Unit: workers. Range: 1 to 16.</summary>
        public int SlotsRare { get; init; } = 2;

        /// <summary>Interact range of a worker. Unit: tiles. Range: above 0.</summary>
        public Fix64 Interact { get; init; } = Fix64.One;

        /// <summary>Ticks a worker needs to place a building at its site. Unit: ticks. Range: 1 upward.</summary>
        public int PlacementTicks { get; init; } = 40;

        /// <summary>Common resource in the bank at the start. Unit: resource units. Range: 0 upward.</summary>
        public int StartingBank { get; init; } = 200;

        /// <summary>Workers at the start. Unit: workers. Range: 0 upward.</summary>
        public int StartingWorkers { get; init; } = 4;

        /// <summary>The profile's field_max_distance_tiles (R): how far a base serves. Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 Reach { get; init; } = Fix64.FromInt(10);

        /// <summary>The profile's depot_ring_min_tiles: how far a depot stands from its target field. Unit: tiles. Range: 1 to 10.</summary>
        public int RingMin { get; init; } = 3;

        /// <summary>What the game spent in each bucket outside workers and drop-offs (common plus rare), paid at the bucket's first second; a bucket past the end of the list pays nothing. Unit: resource units per bucket. Range: any.</summary>
        public IReadOnlyList<long> OtherSpending { get; init; } = Array.Empty<long>();
    }

    /// <summary>The best schedule found by <see cref="EconomyModel.Search"/>.</summary>
    public sealed class ModelBest
    {
        /// <summary>Income of the schedule up to the end second. Unit: resource units. Range: 0 upward.</summary>
        public long Income { get; }

        /// <summary>The worker cap N of the schedule. Unit: workers. Range: 1 to 200.</summary>
        public int WorkerCap { get; }

        /// <summary>The depot triggers w_1 to w_M, non-decreasing; empty for no depot. Unit: workers. Range: 1 to 200 each.</summary>
        public int[] Triggers { get; }

        /// <summary>Bundles the values.</summary>
        public ModelBest(long income, int workerCap, int[] triggers)
        {
            Income = income;
            WorkerCap = workerCap;
            Triggers = triggers;
        }
    }

    /// <summary>The economy model of one player (ai_economy_calc.md 3.2).</summary>
    public sealed class EconomyModel
    {
        /// <summary>The numbers the model reads.</summary>
        private readonly ModelInputs _in;

        /// <summary>Number of fields. Unit: fields. Range: 0 upward.</summary>
        private readonly int _count;

        /// <summary>Gather slots of each field by index. Unit: workers. Range: 1 to 16.</summary>
        private readonly int[] _slots;

        /// <summary>Trip cycle (hold plus round-trip walk) of each field when the HQ serves it, or -1 when it is beyond reach. Unit: ticks. Range: -1, or hold upward.</summary>
        private readonly int[] _hqCycle;

        /// <summary>Field indices by squared distance from the spawn, ascending, ties to the lower index (the order depots pick their target in).</summary>
        private readonly int[] _order;

        /// <summary>Trip cycle of field g when a depot stands at target t (index [t, g]), or -1 when g is beyond reach of t. Unit: ticks. Range: -1, or hold upward.</summary>
        private readonly int[,] _depotCycle;

        /// <summary>Whole seconds from ordering a depot at target t to its completion: walk from the HQ, placement and build, rounded up. Unit: seconds. Range: 1 upward.</summary>
        private readonly int[] _leadSeconds;

        /// <summary>Whole seconds a worker takes to train, rounded up. Unit: seconds. Range: 1 upward.</summary>
        private readonly int _workerSeconds;

        /// <summary>Builds the fixed structure of the model (distances, trip cycles, lead times) once for one player on one map.</summary>
        /// <param name="inputs">The numbers the model reads.</param>
        public EconomyModel(ModelInputs inputs)
        {
            _in = inputs;
            _count = inputs.Fields.Count;
            _slots = new int[_count];
            _hqCycle = new int[_count];
            _depotCycle = new int[_count, _count];
            _leadSeconds = new int[_count];
            // Squared reach. Unit: squared tiles.
            Fix64 reachSquared = inputs.Reach * inputs.Reach;
            // Squared distance from the spawn of each field, for the target order. Unit: squared tiles.
            Fix64[] fromSpawn = new Fix64[_count];
            for (int f = 0; f < _count; f++)
            {
                _slots[f] = inputs.Fields[f].Rare ? inputs.SlotsRare : inputs.SlotsCommon;
                fromSpawn[f] = (inputs.Fields[f].Centre - inputs.Spawn).LengthSquared();
                _hqCycle[f] = fromSpawn[f] <= reachSquared ? inputs.HoldTicks + WalkTicks((inputs.Fields[f].Centre - inputs.Spawn).Length(), inputs.HqRadius, inputs.Interact, inputs.WorkerSpeed, inputs.TicksPerSecond) : -1;
            }
            // Field indices in model order. Range: a permutation of 0 to field count - 1.
            List<int> order = new List<int>();
            for (int f = 0; f < _count; f++)
            {
                order.Add(f);
            }
            order.Sort((a, b) => fromSpawn[a] != fromSpawn[b] ? fromSpawn[a].CompareTo(fromSpawn[b]) : a.CompareTo(b));
            _order = order.ToArray();
            for (int t = 0; t < _count; t++)
            {
                for (int g = 0; g < _count; g++)
                {
                    // Distance between target t and field g. Unit: tiles.
                    Fix64 between = (inputs.Fields[g].Centre - inputs.Fields[t].Centre).Length();
                    // Walk distance from field g to the depot: ring minimum at the target, else the distance to the target plus the ring minimum. Unit: tiles.
                    Fix64 toDepot = g == t ? Fix64.FromInt(inputs.RingMin) : between + Fix64.FromInt(inputs.RingMin);
                    _depotCycle[t, g] = (inputs.Fields[g].Centre - inputs.Fields[t].Centre).LengthSquared() <= reachSquared
                        ? inputs.HoldTicks + WalkTicks(toDepot, inputs.DepotRadius, inputs.Interact, inputs.WorkerSpeed, inputs.TicksPerSecond) : -1;
                }
                // The site stands ring_min tiles from the target on the HQ's side; the builder walks from the spawn. Unit: tiles.
                Fix64 siteDistance = Fix64.Max(Fix64.Zero, (inputs.Fields[t].Centre - inputs.Spawn).Length() - Fix64.FromInt(inputs.RingMin));
                // One-way walk of the builder, rounded up. Unit: ticks. Range: 0 upward.
                int walkOneWay = inputs.WorkerSpeed > Fix64.Zero ? (siteDistance * inputs.TicksPerSecond / inputs.WorkerSpeed).CeilingToInt() : 0;
                _leadSeconds[t] = Math.Max(1, (walkOneWay + inputs.PlacementTicks + inputs.DepotBuildTicks + inputs.TicksPerSecond - 1) / inputs.TicksPerSecond);
            }
            _workerSeconds = Math.Max(1, (inputs.WorkerBuildTicks + inputs.TicksPerSecond - 1) / inputs.TicksPerSecond);
        }

        /// <summary>
        /// The round-trip walk between a field and a drop-off, the same formula as the AI's WalkTicks (AiBrain.cs, internal to Rts.Ai, so copied here; a test pins the two equal): one way = max(0, distance - 2 x interact - radius)
        /// tiles, ticks = ceil(2 x one way x ticksPerSecond / speed), 0 when the speed is 0.
        /// </summary>
        /// <param name="distance">Field centre to drop-off position. Unit: tiles. Range: 0 upward.</param>
        /// <param name="radius">Drop-off footprint radius. Unit: tiles. Range: 0 upward.</param>
        /// <param name="interact">Interact range. Unit: tiles. Range: above 0.</param>
        /// <param name="speed">Worker speed. Unit: tiles per second. Range: 0 upward.</param>
        /// <param name="ticksPerSecond">Simulation rate. Unit: ticks per second. Range: 1 upward.</param>
        public static int WalkTicks(Fix64 distance, Fix64 radius, Fix64 interact, Fix64 speed, int ticksPerSecond)
        {
            // One-way walk beyond the interact ranges. Unit: tiles. Range: 0 upward.
            Fix64 walk = Fix64.Max(Fix64.Zero, distance - interact * 2 - radius);
            return speed > Fix64.Zero ? (walk * (2 * ticksPerSecond) / speed).CeilingToInt() : 0;
        }

        /// <summary>Income of one schedule of the search: the HQ trains workers up to <paramref name="workerCap"/> and depot m is ordered once the workers reach triggers[m]. Unit: resource units. Range: 0 upward.</summary>
        /// <param name="atSeconds">Seconds stepped. Unit: seconds. Range: 1 upward.</param>
        /// <param name="workerCap">N: workers, training included, the HQ trains up to. Unit: workers. Range: 1 to 200.</param>
        /// <param name="triggers">Depot triggers, non-decreasing; empty for no depot. Unit: workers. Range: 1 to 200 each.</param>
        /// <param name="workersByBucket">Optional output: workers after the last second of each completed bucket (the AI's workers_by_bucket, for the replay test). Unit: workers.</param>
        /// <param name="depotTicks">Optional output: completion tick of each depot that finished (the AI's drop_off_ticks). Unit: ticks.</param>
        public long Income(int atSeconds, int workerCap, int[] triggers, List<int>? workersByBucket = null, List<int>? depotTicks = null)
        {
            return Simulate(atSeconds, workerCap, triggers, null, null, workersByBucket, depotTicks);
        }

        /// <summary>
        /// Income of the AI's own schedule (MOD): the gathering workers of bucket b are the AI's count after bucket b - 1 (the starting count in bucket 0), assigned by the same fill; depot m completes at the second of
        /// the AI's m-th drop-off; nothing is bought, so the bank is never tested. Unit: resource units. Range: 0 upward.
        /// </summary>
        /// <param name="atSeconds">Seconds stepped. Unit: seconds. Range: 1 upward.</param>
        /// <param name="workersByBucket">The AI's workers after each completed bucket. Unit: workers. Range: 0 upward.</param>
        /// <param name="dropOffTicks">The AI's drop-off completion ticks, ascending. Unit: ticks. Range: 0 upward.</param>
        public long Replay(int atSeconds, IReadOnlyList<int> workersByBucket, IReadOnlyList<int> dropOffTicks)
        {
            // Completion second of each drop-off (rounded down). Unit: seconds. Range: 0 upward.
            int[] seconds = new int[_in.HasDepot ? dropOffTicks.Count : 0];
            for (int i = 0; i < seconds.Length; i++)
            {
                seconds[i] = dropOffTicks[i] / _in.TicksPerSecond;
            }
            return Simulate(atSeconds, 0, Array.Empty<int>(), workersByBucket, seconds, null, null);
        }

        /// <summary>
        /// Steps the model once per second for <paramref name="atSeconds"/> seconds. Order inside a second: the bucket's other spending is paid (first second of a bucket), a worker or depot that finishes joins, the HQ
        /// trains a worker (search mode, first), a depot is ordered (search mode, second), then every served field yields its loads. Returns the income deposited. A null <paramref name="replayWorkers"/> is search mode.
        /// </summary>
        private long Simulate(int atSeconds, int workerCap, int[] triggers, IReadOnlyList<int>? replayWorkers, int[]? replayDepotSeconds, List<int>? workersByBucket, List<int>? depotTicks)
        {
            // Ticks per second and the slot hold, as locals for speed. Unit: ticks per second, ticks.
            int tps = _in.TicksPerSecond;
            int hold = _in.HoldTicks;
            // Bank, which may go negative; nothing is bought while it is below the cost. Unit: resource units.
            long bank = _in.StartingBank;
            // Income deposited so far. Unit: resource units. Range: 0 upward.
            long income = 0;
            // Amount left in each field, the fractional load carried over in each field (ticks of slot time) and the workers assigned to it. Unit: resource units, ticks, workers.
            int[] remaining = new int[_count];
            long[] carry = new long[_count];
            int[] assigned = new int[_count];
            // Trip cycle of each field's base, -1 while no base serves it; and the workers it can hold, ceil(slots x cycle / hold). Unit: ticks, workers.
            int[] cycle = new int[_count];
            int[] room = new int[_count];
            // Served fields in (cycle, index) order, the fill order; and how many there are. Unit: field indices, fields.
            int[] served = new int[_count];
            int servedCount = 0;
            for (int f = 0; f < _count; f++)
            {
                remaining[f] = _in.Fields[f].Amount;
                cycle[f] = -1;
            }

            // Adds field f to the served list with trip cycle c, keeping (cycle, index) order.
            void Serve(int f, int c)
            {
                cycle[f] = c;
                room[f] = (int)(((long)_slots[f] * c + hold - 1) / hold);
                // Position the field takes in the served list. Range: 0 to servedCount.
                int at = servedCount;
                while (at > 0 && (cycle[served[at - 1]] > c || (cycle[served[at - 1]] == c && served[at - 1] > f)))
                {
                    served[at] = served[at - 1];
                    at--;
                }
                served[at] = f;
                servedCount++;
            }

            // The first field in model order that is unserved and has amount left, or -1.
            int FirstTarget()
            {
                foreach (int f in _order)
                {
                    if (cycle[f] < 0 && remaining[f] > 0)
                    {
                        return f;
                    }
                }
                return -1;
            }

            // Serves every unserved field within reach of target t, in ascending index.
            void ServeAround(int t)
            {
                for (int g = 0; g < _count; g++)
                {
                    if (cycle[g] < 0 && _depotCycle[t, g] >= 0)
                    {
                        Serve(g, _depotCycle[t, g]);
                    }
                }
            }

            for (int f = 0; f < _count; f++)
            {
                if (_hqCycle[f] >= 0)
                {
                    Serve(f, _hqCycle[f]);
                }
            }
            // Workers that have finished training (search mode). Unit: workers. Range: 0 upward.
            int workers = _in.StartingWorkers;
            // Second a worker in training finishes, or -1 when the HQ is idle. Unit: seconds.
            int trainEnd = -1;
            // Depots ordered so far (search mode) or completed (replay). Unit: depots. Range: 0 to 4 (search) or the AI's count.
            int depots = 0;
            // Second the pending depot completes (-1 when none) and its target field. Unit: seconds, field index.
            int pendingEnd = -1;
            int pendingTarget = -1;
            for (int sec = 0; sec < atSeconds; sec++)
            {
                if (sec % _in.BucketSeconds == 0 && sec / _in.BucketSeconds < _in.OtherSpending.Count)
                {
                    bank -= _in.OtherSpending[sec / _in.BucketSeconds];
                }
                if (trainEnd == sec)
                {
                    workers++;
                    trainEnd = -1;
                }
                if (replayDepotSeconds == null)
                {
                    if (pendingEnd == sec)
                    {
                        ServeAround(pendingTarget);
                        depotTicks?.Add(sec * tps);
                        pendingEnd = -1;
                    }
                }
                else
                {
                    while (depots < replayDepotSeconds.Length && replayDepotSeconds[depots] <= sec)
                    {
                        // Target of the AI's next depot, picked when it completes by the model's rule.
                        int target = FirstTarget();
                        if (target >= 0)
                        {
                            ServeAround(target);
                        }
                        depots++;
                    }
                    // Bucket of this second, and the AI's worker count of the bucket before it. Unit: bucket index, workers.
                    int bucket = sec / _in.BucketSeconds;
                    workers = bucket == 0 || replayWorkers!.Count == 0 ? _in.StartingWorkers : replayWorkers[Math.Min(bucket, replayWorkers.Count) - 1];
                }
                if (replayWorkers == null)
                {
                    // Workers the served fields can hold. Unit: workers. Range: 0 upward.
                    int space = 0;
                    for (int i = 0; i < servedCount; i++)
                    {
                        space += remaining[served[i]] > 0 ? room[served[i]] : 0;
                    }
                    if (trainEnd < 0 && bank >= _in.WorkerCost && workers < workerCap && workers < space)
                    {
                        bank -= _in.WorkerCost;
                        trainEnd = sec + _workerSeconds;
                    }
                    if (depots < triggers.Length && pendingEnd < 0 && workers + (trainEnd >= 0 ? 1 : 0) >= triggers[depots] && bank >= _in.DepotCost)
                    {
                        // The field the depot goes to, or -1 when every field is served or empty.
                        int target = FirstTarget();
                        if (target >= 0)
                        {
                            bank -= _in.DepotCost;
                            pendingTarget = target;
                            pendingEnd = sec + _leadSeconds[target];
                            depots++;
                        }
                    }
                }
                // Workers still to place while filling the served fields in order. Unit: workers. Range: 0 upward.
                int left = workers;
                for (int i = 0; i < servedCount; i++)
                {
                    // Field being filled. Range: 0 to field count - 1.
                    int f = served[i];
                    assigned[f] = remaining[f] > 0 ? Math.Min(left, room[f]) : 0;
                    left -= assigned[f];
                    // Slot time the workers use this second, bounded by their own cycles and by the slots. Unit: ticks. Range: 0 upward.
                    carry[f] += (long)tps * Math.Min((long)assigned[f] * hold, (long)_slots[f] * cycle[f]);
                    // Whole loads delivered this second; the rest stays in carry. Unit: loads. Range: 0 upward.
                    long loads = carry[f] / ((long)hold * cycle[f]);
                    carry[f] -= loads * hold * cycle[f];
                    // Resource deposited by the field this second. Unit: resource units. Range: 0 to remaining.
                    int deposit = (int)Math.Min(remaining[f], _in.AmountPerTrip * loads);
                    remaining[f] -= deposit;
                    bank += deposit;
                    income += deposit;
                }
                if (workersByBucket != null && (sec + 1) % _in.BucketSeconds == 0)
                {
                    workersByBucket.Add(workers);
                }
            }
            return income;
        }

        /// <summary>
        /// The best schedule on the grid: every N from <paramref name="capMin"/> to <paramref name="capMax"/> by <paramref name="capStep"/>, every non-decreasing trigger tuple of 0 to <paramref name="maxDepots"/> depots
        /// from <paramref name="triggerMin"/> to <paramref name="triggerMax"/> by <paramref name="triggerStep"/>. Largest income wins; a tie goes to the smaller N, then fewer depots, then the lexicographically smaller tuple.
        /// </summary>
        /// <param name="atSeconds">Seconds stepped. Unit: seconds. Range: 1 upward.</param>
        /// <param name="capMin">Smallest worker cap. Unit: workers. Range: 1 to 200.</param>
        /// <param name="capMax">Largest worker cap. Unit: workers. Range: capMin to 200.</param>
        /// <param name="capStep">Step of the cap. Unit: workers. Range: 1 to 200.</param>
        /// <param name="triggerMin">Smallest depot trigger. Unit: workers. Range: 1 to 200.</param>
        /// <param name="triggerMax">Largest depot trigger. Unit: workers. Range: triggerMin to 200.</param>
        /// <param name="triggerStep">Step of the trigger. Unit: workers. Range: 1 to 200.</param>
        /// <param name="maxDepots">Most depots in a schedule (0 when the plan builds none). Unit: depots. Range: 0 to 4.</param>
        public ModelBest Search(int atSeconds, int capMin, int capMax, int capStep, int triggerMin, int triggerMax, int triggerStep, int maxDepots)
        {
            // Best schedule so far; income -1 so the first schedule always replaces it.
            ModelBest best = new ModelBest(-1, capMin, Array.Empty<int>());
            // Most depots tried: none when the plan builds no depot.
            int most = _in.HasDepot ? maxDepots : 0;
            for (int cap = capMin; cap <= capMax; cap += capStep)
            {
                for (int count = 0; count <= most; count++)
                {
                    // The tuple being filled, position by position.
                    int[] tuple = new int[count];
                    Enumerate(tuple, 0, triggerMin, triggerMax, triggerStep, atSeconds, cap, ref best);
                }
            }
            return best;
        }

        /// <summary>Fills tuple[position..] with every non-decreasing value sequence in ascending lexicographic order and keeps the first schedule with the largest income in <paramref name="best"/>.</summary>
        private void Enumerate(int[] tuple, int position, int low, int triggerMax, int step, int atSeconds, int cap, ref ModelBest best)
        {
            if (position == tuple.Length)
            {
                // Income of this schedule. Unit: resource units. Range: 0 upward.
                long income = Simulate(atSeconds, cap, tuple, null, null, null, null);
                if (income > best.Income)
                {
                    best = new ModelBest(income, cap, (int[])tuple.Clone());
                }
                return;
            }
            for (int value = low; value <= triggerMax; value += step)
            {
                tuple[position] = value;
                Enumerate(tuple, position + 1, value, triggerMax, step, atSeconds, cap, ref best);
            }
        }
    }
}
