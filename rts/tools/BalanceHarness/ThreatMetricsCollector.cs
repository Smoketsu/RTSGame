// rts/tools/BalanceHarness/ThreatMetricsCollector.cs
// Collects the two package S-M summary fields of rts/docs/specs/ai_threat_map.md section 7.1 while a game runs: drop_off_life (when each own non-HQ drop-off structure first existed and when an enemy killed it) and
// harass (squads started, workers killed, value killed and value lost by the units of the controller's harass squad). SummaryObserver calls Begin once, AfterThink before each step and Observe after each step, and reads
// Results at the end. It only reads the simulation, its events and the controllers' memory (HarassGroup); it never writes any of them, so the state hash and play signature are the same with and without it.
// Tool-side only. Part of Rts.Tools.BalanceHarness.
using System.Collections.Generic;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The harass counters of one slot of one game (ai_threat_map.md 7.1).</summary>
    public sealed class HarassMetrics
    {
        /// <summary>Thinks at which the slot's harass set went from empty to not empty, up to the window. Unit: squads. Range: 0 upward.</summary>
        public int Squads { get; }

        /// <summary>Enemy workers (can gather, no footprint) killed by a unit of the slot's harass set, up to the window. Unit: workers. Range: 0 upward.</summary>
        public int WorkerKills { get; }

        /// <summary>Sum of cost_common + cost_rare of every enemy unit killed by a unit of the slot's harass set, up to the window. Unit: resource units. Range: 0 upward.</summary>
        public int KillsValue { get; }

        /// <summary>Sum of cost_common + cost_rare of every unit of the slot's harass set that died, up to the window. Unit: resource units. Range: 0 upward.</summary>
        public int LossesValue { get; }

        /// <summary>Bundles the values.</summary>
        public HarassMetrics(int squads, int workerKills, int killsValue, int lossesValue)
        {
            Squads = squads;
            WorkerKills = workerKills;
            KillsValue = killsValue;
            LossesValue = lossesValue;
        }

        /// <summary>The metrics of a record written before S-M: all 0.</summary>
        public static HarassMetrics Empty => new HarassMetrics(0, 0, 0, 0);
    }

    /// <summary>Gathers drop-off lives and harass counters per player slot.</summary>
    public sealed class ThreatMetricsCollector
    {
        /// <summary>Owner and definition of a unit seen at some point (kept after death so later events can still be attributed).</summary>
        private sealed class Seen
        {
            /// <summary>Owning player.</summary>
            public PlayerId Owner { get; }

            /// <summary>The unit's definition.</summary>
            public UnitDefinition Definition { get; }

            /// <summary>Bundles the values.</summary>
            public Seen(PlayerId owner, UnitDefinition definition)
            {
                Owner = owner;
                Definition = definition;
            }
        }

        /// <summary>Everything accumulated for one slot.</summary>
        private sealed class SlotState
        {
            /// <summary>Each own non-HQ drop-off as [placed tick, death tick or -1], in placement order. Unit: ticks. Range: 0 upward, -1 for alive.</summary>
            public List<int[]> Life { get; } = new List<int[]>();

            /// <summary>Index into <see cref="Life"/> of each drop-off by unit ID, so a kill finds its entry. Unit: list index.</summary>
            public SortedDictionary<EntityId, int> LifeIndex { get; } = new SortedDictionary<EntityId, int>();

            /// <summary>The harass set as copied at the last think. Unit: unit IDs.</summary>
            public SortedSet<EntityId> Set { get; } = new SortedSet<EntityId>();

            /// <summary>Squads started so far. Unit: squads. Range: 0 upward.</summary>
            public int Squads;

            /// <summary>Workers killed by the set. Unit: workers. Range: 0 upward.</summary>
            public int WorkerKills;

            /// <summary>Value killed by the set. Unit: resource units. Range: 0 upward.</summary>
            public int KillsValue;

            /// <summary>Value of the set's units that died. Unit: resource units. Range: 0 upward.</summary>
            public int LossesValue;
        }

        /// <summary>Last tick counted (inclusive); events and thinks after it are not counted. Unit: ticks. Range: 0 upward; int.MaxValue for no limit.</summary>
        private readonly int _windowTicks;

        /// <summary>The slots by player number, ascending.</summary>
        private readonly SortedDictionary<byte, SlotState> _slots = new SortedDictionary<byte, SlotState>();

        /// <summary>Every unit seen so far, by entity ID.</summary>
        private readonly SortedDictionary<EntityId, Seen> _seen = new SortedDictionary<EntityId, Seen>();

        /// <summary>Creates a collector.</summary>
        /// <param name="windowTicks">Last tick counted (inclusive), the summary window. Unit: ticks. Range: 1 upward; int.MaxValue for no limit.</param>
        public ThreatMetricsCollector(int windowTicks)
        {
            _windowTicks = windowTicks;
        }

        /// <summary>Called once after the controllers started and before the first step: makes a slot per player and notes the units that exist (placed at tick 0).</summary>
        /// <param name="sim">The started simulation.</param>
        public void Begin(Simulation sim)
        {
            foreach (Player player in sim.Players.All)
            {
                _slots.Add(player.Id.Value, new SlotState());
            }
            Learn(sim, 0);
        }

        /// <summary>
        /// Called after the controllers thought and before the step: copies each controller's harass group into its slot's set, and counts a squad when a set went from empty to not empty.
        /// Other controllers leave the set empty.
        /// </summary>
        /// <param name="tick">Tick about to be stepped. Unit: ticks. Range: 0 upward.</param>
        /// <param name="controllers">The game's controllers.</param>
        public void AfterThink(int tick, IReadOnlyList<IPlayerController> controllers)
        {
            foreach (IPlayerController controller in controllers)
            {
                if (controller is SkirmishAiController ai && _slots.TryGetValue(ai.Player.Value, out SlotState? slot))
                {
                    // True when the set was empty before this copy.
                    bool wasEmpty = slot.Set.Count == 0;
                    slot.Set.Clear();
                    foreach (EntityId id in ai.Memory.HarassGroup)
                    {
                        slot.Set.Add(id);
                    }
                    if (wasEmpty && slot.Set.Count > 0 && tick <= _windowTicks)
                    {
                        slot.Squads++;
                    }
                }
            }
        }

        /// <summary>Called after every step with the state after it and the events it reported: notes new drop-offs, then reads the kills.</summary>
        /// <param name="sim">The simulation after the step.</param>
        /// <param name="steppedTick">Tick of the step just taken. Unit: ticks. Range: 0 upward.</param>
        /// <param name="events">The events of that step.</param>
        public void Observe(Simulation sim, int steppedTick, IReadOnlyList<SimEvent> events)
        {
            Learn(sim, steppedTick);
            foreach (SimEvent simEvent in events)
            {
                if (!(simEvent is UnitKilledEvent killed) || !_seen.TryGetValue(killed.Victim, out Seen? victim))
                {
                    continue;
                }
                // The victim's cost in the weighted unit (rare weight 1, as rare_weight in behaviour_checks.json). Unit: resource units.
                int cost = victim.Definition.CostCommon + victim.Definition.CostRare;
                if (_slots.TryGetValue(victim.Owner.Value, out SlotState? loser))
                {
                    if (loser.LifeIndex.TryGetValue(killed.Victim, out int index) && sim.Players.AreEnemies(victim.Owner, killed.KillerOwner))
                    {
                        loser.Life[index][1] = killed.Tick;
                    }
                    if (killed.Tick <= _windowTicks && loser.Set.Contains(killed.Victim))
                    {
                        loser.LossesValue += cost;
                    }
                }
                if (killed.Tick <= _windowTicks && _slots.TryGetValue(killed.KillerOwner.Value, out SlotState? winner) && winner.Set.Contains(killed.Killer))
                {
                    winner.KillsValue += cost;
                    if (victim.Definition.Economy.CanGather && victim.Definition.Economy.FootprintTiles == 0)
                    {
                        winner.WorkerKills++;
                    }
                }
            }
        }

        /// <summary>The drop-off lives of every slot, ascending by player number: one [placed tick, death tick or -1] per own non-HQ drop-off structure, in placement order.</summary>
        public IReadOnlyList<IReadOnlyList<int[]>> Lives()
        {
            List<IReadOnlyList<int[]>> lives = new List<IReadOnlyList<int[]>>();
            foreach (SlotState slot in _slots.Values)
            {
                lives.Add(slot.Life);
            }
            return lives;
        }

        /// <summary>The harass counters of every slot, ascending by player number.</summary>
        public HarassMetrics[] Harass()
        {
            List<HarassMetrics> list = new List<HarassMetrics>();
            foreach (SlotState slot in _slots.Values)
            {
                list.Add(new HarassMetrics(slot.Squads, slot.WorkerKills, slot.KillsValue, slot.LossesValue));
            }
            return list.ToArray();
        }

        /// <summary>Remembers every unit now in the simulation; a drop-off structure seen for the first time starts its life at <paramref name="tick"/>.</summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="tick">Tick the units were first seen at. Unit: ticks. Range: 0 upward.</param>
        private void Learn(Simulation sim, int tick)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (_seen.ContainsKey(unit.Id))
                {
                    continue;
                }
                _seen.Add(unit.Id, new Seen(unit.Owner, unit.Definition));
                if (unit.Definition.Economy.IsDropOff && unit.Definition.Hq == null && unit.Definition.Economy.FootprintTiles > 0 && _slots.TryGetValue(unit.Owner.Value, out SlotState? slot))
                {
                    slot.LifeIndex[unit.Id] = slot.Life.Count;
                    slot.Life.Add(new[] { tick, -1 });
                }
            }
        }
    }
}
