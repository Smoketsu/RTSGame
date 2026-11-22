// rts/tools/BalanceHarness/ProductionMetricsCollector.cs
// Collects the production and defence metrics (spec: rts/docs/specs/ai_production_defence.md section 7) while a game runs: SummaryObserver calls Begin once and Observe after every step, and reads Results at the end.
// Observe reads the events of a step and the state after it; it never submits a command or steps the simulation, so the state hash and play signature are the same with and without it. It sees every unit (the
// harness is omniscient), which is correct for measurement and never reaches a controller. Second k of a game is ticks [k T, (k + 1) T) for T ticks per second; a raid second is judged at its last tick.
// Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Gathers a <see cref="ProductionMetrics"/> per player slot.</summary>
    public sealed class ProductionMetricsCollector
    {
        /// <summary>Owner, definition and last known position of a unit seen at some point (kept after death so later events can be attributed).</summary>
        private sealed class Seen
        {
            /// <summary>Owning player.</summary>
            public PlayerId Owner { get; }

            /// <summary>The unit's definition.</summary>
            public UnitDefinition Definition { get; }

            /// <summary>Last position the unit was seen at. Unit: tiles.</summary>
            public FixVec2 Position { get; set; }

            /// <summary>Bundles the values.</summary>
            public Seen(PlayerId owner, UnitDefinition definition, FixVec2 position)
            {
                Owner = owner;
                Definition = definition;
                Position = position;
            }
        }

        /// <summary>Everything accumulated for one slot.</summary>
        private sealed class SlotState
        {
            /// <summary>Workers killed by an enemy in the window. Unit: workers. Range: 0 upward.</summary>
            public int WorkerLosses;

            /// <summary>Of those, killed soon after their training. Unit: workers. Range: 0 upward.</summary>
            public int WorkerFedLosses;

            /// <summary>Raid seconds judged so far. Unit: seconds. Range: 0 upward.</summary>
            public int RaidSeconds;

            /// <summary>Unanswered raid seconds judged so far. Unit: seconds. Range: 0 upward.</summary>
            public int Unanswered;

            /// <summary>Position of the newest hit of each second that had one and is not judged yet, by second index. Unit: tiles.</summary>
            public SortedDictionary<int, FixVec2> Hits { get; } = new SortedDictionary<int, FixVec2>();

            /// <summary>Training tick of each own worker trained in the game, by unit. Unit: ticks. Range: 0 upward.</summary>
            public SortedDictionary<EntityId, int> TrainedAt { get; } = new SortedDictionary<EntityId, int>();

            /// <summary>Common deposits as (tick, amount) in event order. Unit: ticks, common.</summary>
            public List<KeyValuePair<int, int>> Deposits { get; } = new List<KeyValuePair<int, int>>();

            /// <summary>Sum of the bank samples. Unit: common. Range: 0 upward.</summary>
            public long BankSum;

            /// <summary>Number of bank samples. Unit: samples. Range: 0 upward.</summary>
            public int BankCount;

            /// <summary>Producers at each sample time, null until reached. Unit: structures.</summary>
            public int?[] Producers;

            /// <summary>Capacity at each sample time, null until reached. Unit: common per minute.</summary>
            public int?[] Capacity;

            /// <summary>Income of the last 60 s at each sample time, null until reached. Unit: common.</summary>
            public int?[] Income60;

            /// <summary>Army supply at each sample time, null until reached. Unit: supply.</summary>
            public int?[] ArmySupply;

            /// <summary>Allocates the sample arrays.</summary>
            /// <param name="samples">Number of sample times. Range: 1 to 8.</param>
            public SlotState(int samples)
            {
                Producers = new int?[samples];
                Capacity = new int?[samples];
                Income60 = new int?[samples];
                ArmySupply = new int?[samples];
            }
        }

        /// <summary>The settings of ai_eval.json.</summary>
        private readonly AiEvalSettings _settings;

        /// <summary>The slots by player number, ascending.</summary>
        private readonly SortedDictionary<byte, SlotState> _slots = new SortedDictionary<byte, SlotState>();

        /// <summary>Every unit seen so far.</summary>
        private readonly SortedDictionary<EntityId, Seen> _seen = new SortedDictionary<EntityId, Seen>();

        /// <summary>Ticks per second, set by <see cref="Begin"/>. Unit: ticks. Range: 1 upward; 1 before Begin.</summary>
        private int _tps = 1;

        /// <summary>Tick of each sample time. Unit: ticks. Range: 1 upward.</summary>
        private int[] _sampleTicks = Array.Empty<int>();

        /// <summary>Raid radius squared. Unit: tiles squared. Range: 1 to 900.</summary>
        private Fix64 _radiusSquared = Fix64.One;

        /// <summary>Creates a collector.</summary>
        /// <param name="settings">The parsed ai_eval.json.</param>
        public ProductionMetricsCollector(AiEvalSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Called once after the simulation started and before the first step.</summary>
        /// <param name="sim">The started simulation.</param>
        public void Begin(Simulation sim)
        {
            _tps = sim.Clock.SecondsToTicks(Fix64.One);
            _sampleTicks = new int[_settings.SummarySampleSeconds.Count];
            for (int i = 0; i < _sampleTicks.Length; i++)
            {
                _sampleTicks[i] = _settings.SummarySampleSeconds[i] * _tps;
            }
            _radiusSquared = Fix64.FromInt(_settings.RaidRadiusTiles) * Fix64.FromInt(_settings.RaidRadiusTiles);
            foreach (Player player in sim.Players.All)
            {
                _slots.Add(player.Id.Value, new SlotState(_sampleTicks.Length));
            }
            Refresh(sim);
        }

        /// <summary>Called after every step with the state after it and the events it reported.</summary>
        /// <param name="sim">The simulation after the step.</param>
        /// <param name="steppedTick">Tick of the step just taken. Unit: ticks. Range: 0 upward.</param>
        /// <param name="events">The events of that step.</param>
        public void Observe(Simulation sim, int steppedTick, IReadOnlyList<SimEvent> events)
        {
            Refresh(sim);
            // Tick at which the window ends (events at or after it are not counted). Unit: ticks.
            int windowEnd = _settings.SummaryWindowSeconds * _tps;
            foreach (SimEvent simEvent in events)
            {
                if (simEvent is ResourceDepositedEvent deposit)
                {
                    if (deposit.Resource == ResourceKind.Common && _slots.TryGetValue(deposit.Owner.Value, out SlotState? depositor))
                    {
                        depositor.Deposits.Add(new KeyValuePair<int, int>(deposit.Tick, deposit.Amount));
                    }
                }
                else if (simEvent is UnitTrainedEvent trained)
                {
                    if (_seen.TryGetValue(trained.Unit, out Seen? made) && _slots.TryGetValue(made.Owner.Value, out SlotState? producer) && IsWorker(made.Definition))
                    {
                        producer.TrainedAt[trained.Unit] = trained.Tick;
                    }
                }
                else if (simEvent is UnitKilledEvent killed && killed.Tick < windowEnd)
                {
                    if (_seen.TryGetValue(killed.Victim, out Seen? victim) && IsWorker(victim.Definition) && _slots.TryGetValue(victim.Owner.Value, out SlotState? loser) && IsEnemy(sim, victim.Owner, killed.KillerOwner))
                    {
                        loser.WorkerLosses++;
                        if (loser.TrainedAt.TryGetValue(killed.Victim, out int trainedTick) && killed.Tick - trainedTick <= _settings.FedWindowSeconds * _tps)
                        {
                            loser.WorkerFedLosses++;
                        }
                    }
                }
                else if (simEvent is UnitDamagedEvent damaged && damaged.Tick < windowEnd)
                {
                    ReadHit(sim, damaged);
                }
            }
            // Tick count after the step: the step's tick plus one. Unit: ticks.
            int after = steppedTick + 1;
            if (after % _tps == 0)
            {
                EndOfSecond(sim, after / _tps - 1);
            }
            for (int i = 0; i < _sampleTicks.Length; i++)
            {
                if (after == _sampleTicks[i])
                {
                    Sample(sim, i, steppedTick);
                }
            }
        }

        /// <summary>The metrics of every slot, ascending by player number.</summary>
        public ProductionMetrics[] Results()
        {
            List<ProductionMetrics> list = new List<ProductionMetrics>();
            foreach (SlotState slot in _slots.Values)
            {
                list.Add(new ProductionMetrics(slot.WorkerLosses, slot.WorkerFedLosses, slot.RaidSeconds, slot.Unanswered, slot.Producers, slot.Capacity, slot.Income60, slot.ArmySupply,
                    slot.BankCount > 0 ? (int?)(int)(slot.BankSum / slot.BankCount) : null));
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------ predicates

        /// <summary>True for a worker: can gather and has no footprint.</summary>
        private static bool IsWorker(UnitDefinition definition)
        {
            return definition.Economy.CanGather && definition.Economy.FootprintTiles == 0;
        }

        /// <summary>True for an army unit: mobile, not a worker, not an HQ, not a structure.</summary>
        private static bool IsArmy(UnitDefinition definition)
        {
            return definition.Hq == null && !IsWorker(definition) && definition.Economy.FootprintTiles == 0 && !definition.Economy.IsStaticDefence && definition.SpeedTilesPerSecond > Fix64.Zero;
        }

        /// <summary>True when both players are real and on different teams.</summary>
        private static bool IsEnemy(Simulation sim, PlayerId a, PlayerId b)
        {
            return a.IsValid && b.IsValid && sim.Players.TryGet(a, out Player? first) && sim.Players.TryGet(b, out Player? second) && first.Team != second.Team;
        }

        /// <summary>Remembers every unit now in the simulation and where it stands.</summary>
        private void Refresh(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (_seen.TryGetValue(unit.Id, out Seen? known))
                {
                    known.Position = unit.Position;
                }
                else
                {
                    _seen.Add(unit.Id, new Seen(unit.Owner, unit.Definition, unit.Position));
                }
            }
        }

        // ------------------------------------------------------------------ raids

        /// <summary>Notes a hit on an own worker, structure or HQ near an own drop-off, from an enemy, as the newest hit of its second.</summary>
        private void ReadHit(Simulation sim, UnitDamagedEvent damaged)
        {
            if (!_seen.TryGetValue(damaged.Target, out Seen? target) || !_seen.TryGetValue(damaged.Attacker, out Seen? attacker) || !_slots.TryGetValue(target.Owner.Value, out SlotState? slot)
                || IsArmy(target.Definition) || !IsEnemy(sim, target.Owner, attacker.Owner))
            {
                return;
            }
            // Where the target stood (its last known position when it died this step). Unit: tiles.
            FixVec2 at = target.Position;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == target.Owner && EconomyRules.IsDropOff(unit) && (unit.Position - at).LengthSquared() <= _radiusSquared)
                {
                    slot.Hits[damaged.Tick / _tps] = at;
                    return;
                }
            }
        }

        /// <summary>Judges the raid second <paramref name="second"/> that just ended and takes the bank sample of the whole second after it.</summary>
        private void EndOfSecond(Simulation sim, int second)
        {
            foreach (KeyValuePair<byte, SlotState> pair in _slots)
            {
                SlotState slot = pair.Value;
                if (slot.Hits.TryGetValue(second, out FixVec2 hit))
                {
                    slot.Hits.Remove(second);
                    slot.RaidSeconds++;
                    if (Unanswered(sim, new PlayerId(pair.Key), hit))
                    {
                        slot.Unanswered++;
                    }
                }
                // Whole seconds elapsed, the bank sample's time. Unit: seconds.
                int elapsed = second + 1;
                if (elapsed >= _settings.BankMeanFromSeconds && elapsed <= _settings.SummaryWindowSeconds && sim.Players.TryGet(new PlayerId(pair.Key), out Player? player))
                {
                    slot.BankSum += player.Common;
                    slot.BankCount++;
                }
            }
        }

        /// <summary>True when no own armed army unit stands or is headed within the radius of <paramref name="hit"/> and own army supply is at least that of the enemy army units within it.</summary>
        private bool Unanswered(Simulation sim, PlayerId owner, FixVec2 hit)
        {
            // Own army supply and the enemy army supply within the radius of the hit. Unit: supply.
            int own = 0;
            int enemy = 0;
            foreach (Unit unit in sim.Units.All)
            {
                if (!IsArmy(unit.Definition))
                {
                    continue;
                }
                if (unit.Owner == owner)
                {
                    own += unit.Definition.Supply;
                    if (unit.Definition.Weapon != null && (Near(unit.Position, hit) || (unit.HasMoveTarget && Near(unit.MoveTarget, hit)) || (unit.IsAttackMoving && Near(unit.AttackMoveGoal, hit))))
                    {
                        return false;
                    }
                }
                else if (IsEnemy(sim, owner, unit.Owner) && Near(unit.Position, hit))
                {
                    enemy += unit.Definition.Supply;
                }
            }
            return own >= enemy;
        }

        /// <summary>True when two points are within the raid radius.</summary>
        private bool Near(FixVec2 a, FixVec2 b)
        {
            return (a - b).LengthSquared() <= _radiusSquared;
        }

        // ------------------------------------------------------------------ samples

        /// <summary>Records producers, capacity, income and army supply of every slot at sample <paramref name="index"/>.</summary>
        private void Sample(Simulation sim, int index, int steppedTick)
        {
            // Start of the 60 s income window. Unit: ticks.
            int from = steppedTick + 1 - 60 * _tps;
            foreach (KeyValuePair<byte, SlotState> pair in _slots)
            {
                PlayerId owner = new PlayerId(pair.Key);
                SlotState slot = pair.Value;
                if (!sim.Players.TryGet(owner, out Player? player))
                {
                    continue;
                }
                // Producers, their capacity and the army supply. Unit: structures, common per minute, supply.
                int producers = 0;
                int capacity = 0;
                int army = 0;
                foreach (Unit unit in sim.Units.All)
                {
                    if (unit.Owner != owner)
                    {
                        continue;
                    }
                    if (IsArmy(unit.Definition))
                    {
                        army += unit.Definition.Supply;
                    }
                    if (unit.IsUnderConstruction || unit.Health <= Fix64.Zero)
                    {
                        continue;
                    }
                    // True when the structure trains a non-worker; its best rate over the units it can train and pay for now. Unit: common per minute.
                    bool trains = false;
                    int best = 0;
                    foreach (string id in unit.Definition.Economy.Trains)
                    {
                        if (!sim.Content.Units.TryGetValue(id, out UnitDefinition? trainable) || trainable.Economy.CanGather)
                        {
                            continue;
                        }
                        trains = true;
                        if (EconomyRules.FirstMissingRequirement(sim, owner, trainable) == null && (trainable.CostRare == 0 || trainable.CostRare <= player.Rare))
                        {
                            best = Math.Max(best, (int)((long)trainable.CostCommon * 60 * _tps / EconomyRules.BuildTicks(sim, trainable)));
                        }
                    }
                    if (trains)
                    {
                        producers++;
                        capacity += best;
                    }
                }
                // Common deposited in the 60 s before the sample. Unit: common.
                int income = 0;
                foreach (KeyValuePair<int, int> deposit in slot.Deposits)
                {
                    if (deposit.Key >= from && deposit.Key <= steppedTick)
                    {
                        income += deposit.Value;
                    }
                }
                slot.Producers[index] = producers;
                slot.Capacity[index] = capacity;
                slot.Income60[index] = income;
                slot.ArmySupply[index] = army;
            }
        }
    }
}
