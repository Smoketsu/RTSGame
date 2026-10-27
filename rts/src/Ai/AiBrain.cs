// rts/src/Ai/AiBrain.cs
// The skirmish AI's decision rules (spec section 4): a pure function of the world view, the profile, the controller's memory
// and its own seeded generator, returning an ordered list of intents. Part of Rts.Ai. It never sees the Simulation, so it can
// only know what AiWorldView exposes (own units, public map data and the team's vision).
//
// Rules, in this fixed order each think (each appends intents; nothing more is appended once max_commands_per_think is reached):
//   0. Intel: remember visible enemy structures and HQs, forget those whose last tile is visible and empty, remember where enemy
//      army units were last seen, mark enemy spawns seen empty, drop old damage reports. (Runs even without an own HQ.)
//   1. HQ: an Uprooted HQ that is not moving and stands next to a field with resources is rooted. Otherwise the HQ is left alone
//      (Phase 1: the HQ stays Rooted; no relocation).
//   1b. Cycle (planetary_cycles.md section 5; the spec numbers it rule 9): when the AI's team holds the Core, the next cycle is not
//      locked yet and the pending pick is not already the AI's choice, pick the first cycle of the faction's cycle_preference that is
//      allowed next (the first allowed cycle when none of the preferences is). With rally_at_core the Massing rally point is the Core
//      centre while the Core exists and the AI's team does not hold it.
//   2. Workers gather: each idle worker (no gather loop, no move or attack-move order), ascending ID, goes to the nearest field
//      with resources within field_max_distance_tiles of the HQ that has fewer than workers_per_field workers (else the nearest
//      such field at all). Only while the HQ is Rooted (it is the drop-off).
//   3. Structures: (a) supply: when cap - used <= supply_buffer, the cap is below the global cap and no supply-giving structure is
//      under construction, build supply_building; (b) build order: the first step with fewer structures (built or under
//      construction) than its count, once supply used >= at_supply_used (later steps wait). A due structure that cannot be paid
//      yet reserves its cost, so worker training and production below leave that much in the bank. A placed structure is not
//      re-tried for build_retry_seconds. Site: the first tile on Chebyshev rings build_ring_min_tiles..build_ring_max_tiles around
//      the HQ tile (ring by ring, ascending tile index) whose 3x3 block is on the map, unblocked, not a ramp and on the HQ's
//      height layer, whose own tile is not occupied by a unit, and that is more than field_clearance_tiles from every field.
//      The builder is the lowest-ID gathering worker (else the lowest-ID worker).
//   4. Train workers at the Rooted HQ while workers (queued included) < min(max_workers, workers_per_field x fields in range),
//      one at a time, if affordable and supply is free. Workers ignore structure reservations: they are the income, and a
//      reservation that starved them could deadlock an AI whose workers were killed.
//   5. Production: each complete idle producer, ascending ID, trains the composition unit it can make with the largest deficit
//      d_u = w_u * (N + 1) - c_u * W (integers; ties to the ordinally lowest ID) if affordable after reservations and supply is free.
//   6. Army (army = own mobile units that are not workers or HQs): Defend (attack-move to a threat inside the base: a visible
//      enemy army unit within defend_radius_tiles of the HQ or an own structure, or a recent hit on an own unit there; an attacking
//      army is recalled only when the visible threat supply reaches defend_recall_supply), Massing (gather at the rally point; attack
//      once army supply >= the drawn threshold, the regroup timer has run out and the tide window allows), Attack (attack-move to the
//      nearest known enemy HQ, else known structure, else unexplored enemy spawn, else last seen enemy, first gathering at a staging
//      point stage_distance_tiles short of it until stage_gather_fraction of the army supply stands within stage_radius_tiles of it
//      or stage_timeout_seconds pass; reinforce; retreat on losses
//      below retreat_army_fraction or when the army's power falls below retreat_power_ratio x the power of the visible armed enemies
//      within engagement_radius_tiles of any army unit (power = supply x health / max health); siege: when an enemy
//      HQ is visible and no visible enemy army unit stands within engagement_radius_tiles of it, army units within that radius
//      of the HQ that can hit its layer are ordered to attack it (attack_unit, the only order that names an enemy unit, and only
//      one the team sees now), and the order is replaced by an attack-move as soon as defenders show up; focus fire: in Defend and
//      Attack, a unit with a visible enemy army unit it can hit within focus_radius_tiles attacks the nearest one (attack_unit),
//      keeping its current target while that stays valid, so fights are won army-first instead of on buildings), Retreat (plain move
//      to the HQ, regroup timer, back to Massing with a new threshold). All in: with all_in_when_mined_out, once no field within
//      field_max_distance_tiles has resources left, the attack threshold drops to 1 supply, the tide window is ignored and the
//      army never retreats (no reinforcements can come, so waiting only stalls the match).
// Determinism: integers and Fix64 only; own units, enemies and fields walked in ascending ID or index order; squared distances;
// ties to the lower ID or index; the only random draw is the attack threshold jitter from the controller's own generator.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>The decision rules. Stateless: all state is in <see cref="AiMemory"/>.</summary>
    public static class AiBrain
    {
        /// <summary>Squared distance under which two goal points count as the same (2 tiles). Unit: squared tiles. Fixed tolerance, not tuning.</summary>
        private static readonly Fix64 SameGoalSquared = Fix64.FromInt(4);

        /// <summary>Squared distance under which a massing unit counts as at the rally point (3 tiles). Unit: squared tiles. Fixed tolerance, not tuning.</summary>
        private static readonly Fix64 AtRallySquared = Fix64.FromInt(9);

        /// <summary>
        /// Runs every rule once and returns the intents in order. Changes only <paramref name="memory"/> and draws only from
        /// <paramref name="rng"/>.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The AI profile.</param>
        /// <param name="plan">The profile's plan for the player's faction.</param>
        /// <param name="memory">The controller's memory; updated.</param>
        /// <param name="rng">The controller's own generator (never the simulation's).</param>
        public static List<AiIntent> Decide(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory, DeterministicRandom rng)
        {
            // Intents decided this think, in rule order.
            List<AiIntent> intents = new List<AiIntent>();
            // The own HQ (lowest ID), or null when the player has none.
            AiUnitView? hq = FindHq(view);
            UpdateIntel(view, profile, memory, hq);
            if (!view.IsActive || hq == null)
            {
                return intents;
            }
            if (memory.OwnSpawnIndex < 0)
            {
                memory.OwnSpawnIndex = NearestSpawn(view, hq.Position);
            }
            if (memory.AttackThreshold < 0)
            {
                EnterMassing(memory, profile, rng);
            }
            // This think's working state (ledger, reservations, intents).
            Turn turn = new Turn(view, profile, plan, memory, rng, hq, intents);
            turn.RootHq();
            turn.Cycle();
            turn.Gather();
            turn.Structures();
            turn.TrainWorkers();
            turn.Produce();
            turn.Army();
            return intents;
        }

        /// <summary>
        /// The composition unit with the largest deficit d_u = w_u * (N + 1) - c_u * W among those <paramref name="canTrain"/> accepts
        /// and whose weight is above 0, ties to the ordinally lowest ID; null when none qualifies. N is the sum of the counts and W the
        /// sum of all weights (spec section 4 rule 5).
        /// </summary>
        /// <param name="weights">Weight per unit ID (ordinal order). Unit: weight. Range: 0 to 100 each.</param>
        /// <param name="counts">Own count per unit ID, queued units included; missing IDs count 0. Unit: units.</param>
        /// <param name="canTrain">True for unit IDs the producer can make.</param>
        public static string? PickByDeficit(IReadOnlyDictionary<string, int> weights, IReadOnlyDictionary<string, int> counts, Func<string, bool> canTrain)
        {
            // Sum of all weights and of all counts. Unit: weight and units. Range: 0 upward.
            long totalWeight = 0;
            // Sum of the own counts of all composition units (N). Unit: units. Range: 0 upward.
            long totalCount = 0;
            foreach (KeyValuePair<string, int> pair in weights)
            {
                totalWeight += pair.Value;
                counts.TryGetValue(pair.Key, out int count);
                totalCount += count;
            }
            // Best unit so far and its deficit. Unit: weight x units.
            string? best = null;
            // Deficit of the best unit so far. Unit: weight x units.
            long bestDeficit = 0;
            foreach (KeyValuePair<string, int> pair in weights)
            {
                if (pair.Value <= 0 || !canTrain(pair.Key))
                {
                    continue;
                }
                counts.TryGetValue(pair.Key, out int count);
                // This unit's deficit against its share of the mix.
                long deficit = (long)pair.Value * (totalCount + 1) - (long)count * totalWeight;
                if (best == null || deficit > bestDeficit)
                {
                    best = pair.Key;
                    bestDeficit = deficit;
                }
            }
            return best;
        }

        /// <summary>The army supply needed for the next attack: attack_army_supply plus a draw of 0 to attack_supply_jitter.</summary>
        /// <param name="memory">Gets the new threshold and stance Massing.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="rng">The controller's generator.</param>
        internal static void EnterMassing(AiMemory memory, AiProfile profile, DeterministicRandom rng)
        {
            memory.Stance = AiStance.Massing;
            memory.HasAttackTarget = false;
            memory.AttackThreshold = profile.AttackArmySupply + rng.NextInt(profile.AttackSupplyJitter + 1);
        }

        /// <summary>The lowest-ID own HQ, or null.</summary>
        private static AiUnitView? FindHq(AiWorldView view)
        {
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if (unit.IsHq)
                {
                    return unit;
                }
            }
            return null;
        }

        /// <summary>Index of the spawn nearest <paramref name="point"/>, ties to the lowest index; -1 when the map has none.</summary>
        private static int NearestSpawn(AiWorldView view, FixVec2 point)
        {
            // Best index and squared distance so far. Unit: squared tiles.
            int best = -1;
            // Squared distance of the best candidate so far. Unit: squared tiles. Range: 0 upward.
            Fix64 bestSquared = Fix64.Zero;
            // Spawn index. Range: 0 to spawn count - 1.
            for (int i = 0; i < view.Spawns.Count; i++)
            {
                // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                Fix64 squared = (view.Spawns[i] - point).LengthSquared();
                if (best < 0 || squared < bestSquared)
                {
                    best = i;
                    bestSquared = squared;
                }
            }
            return best;
        }

        /// <summary>Rule 0: updates the memory from what the team sees now.</summary>
        private static void UpdateIntel(AiWorldView view, AiProfile profile, AiMemory memory, AiUnitView? hq)
        {
            // IDs of enemies visible now, for the "seen gone" test.
            SortedSet<EntityId> visible = new SortedSet<EntityId>();
            // Nearest visible enemy army unit to the HQ, and its squared distance. Unit: squared tiles.
            AiUnitView? nearestArmy = null;
            // Squared distance of the nearest candidate so far. Unit: squared tiles. Range: 0 upward.
            Fix64 nearestSquared = Fix64.Zero;
            foreach (AiUnitView enemy in view.VisibleEnemies)
            {
                visible.Add(enemy.Id);
                if (enemy.IsStructure || enemy.IsHq)
                {
                    if (!memory.KnownEnemyStructures.TryGetValue(enemy.Id, out KnownStructure? known))
                    {
                        known = new KnownStructure();
                        memory.KnownEnemyStructures.Add(enemy.Id, known);
                    }
                    known.Position = enemy.Position;
                    known.IsHq = enemy.IsHq;
                    known.LastSeenTick = view.Tick;
                }
                else if (enemy.IsArmy)
                {
                    // Squared distance from the HQ (or 0 without one, so the lowest ID wins).
                    Fix64 squared = hq == null ? Fix64.Zero : (enemy.Position - hq.Position).LengthSquared();
                    if (nearestArmy == null || squared < nearestSquared)
                    {
                        nearestArmy = enemy;
                        nearestSquared = squared;
                    }
                }
            }
            if (nearestArmy != null)
            {
                memory.HasLastSeenEnemy = true;
                memory.LastSeenEnemyPosition = nearestArmy.Position;
                memory.LastSeenEnemyTick = view.Tick;
            }
            // Known structures whose last tile is visible now but which are not seen: gone.
            List<EntityId> gone = new List<EntityId>();
            foreach (KeyValuePair<EntityId, KnownStructure> pair in memory.KnownEnemyStructures)
            {
                if (!visible.Contains(pair.Key) && view.IsVisible(pair.Value.Position.X.FloorToInt(), pair.Value.Position.Y.FloorToInt()))
                {
                    gone.Add(pair.Key);
                }
            }
            foreach (EntityId id in gone)
            {
                memory.KnownEnemyStructures.Remove(id);
            }
            // A visible enemy spawn with no known enemy structure near it is cleared.
            Fix64 clearSquared = profile.EngagementRadiusTiles * profile.EngagementRadiusTiles;
            // Spawn index. Range: 0 to spawn count - 1.
            for (int i = 0; i < view.Spawns.Count; i++)
            {
                if (i == memory.OwnSpawnIndex || memory.ClearedSpawns.Contains(i))
                {
                    continue;
                }
                // The spawn point being checked. Unit: tiles.
                FixVec2 spawn = view.Spawns[i];
                if (!view.IsVisible(spawn.X.FloorToInt(), spawn.Y.FloorToInt()))
                {
                    continue;
                }
                // True when a known enemy structure stands near the spawn.
                bool occupied = false;
                foreach (KnownStructure known in memory.KnownEnemyStructures.Values)
                {
                    occupied |= (known.Position - spawn).LengthSquared() <= clearSquared;
                }
                if (!occupied)
                {
                    memory.ClearedSpawns.Add(i);
                }
            }
            // Damage reports older than damage_memory_seconds, or about units that are gone, are dropped.
            int window = view.SecondsToTicks(profile.DamageMemorySeconds);
            // Own unit IDs alive now.
            SortedSet<EntityId> alive = new SortedSet<EntityId>();
            foreach (AiUnitView unit in view.OwnUnits)
            {
                alive.Add(unit.Id);
            }
            // Damage reports to drop (old, or about units that are gone).
            List<EntityId> stale = new List<EntityId>();
            foreach (KeyValuePair<EntityId, DamageReport> pair in memory.RecentDamage)
            {
                if (!alive.Contains(pair.Key) || pair.Value.Tick < view.Tick - window)
                {
                    stale.Add(pair.Key);
                }
            }
            foreach (EntityId id in stale)
            {
                memory.RecentDamage.Remove(id);
            }
        }

        /// <summary>One think's working state: the resource ledger, reservations and the intent list.</summary>
        private sealed class Turn
        {
            /// <summary>The view being decided on.</summary>
            private readonly AiWorldView _view;

            /// <summary>The profile.</summary>
            private readonly AiProfile _profile;

            /// <summary>The faction plan.</summary>
            private readonly AiFactionPlan _plan;

            /// <summary>The controller's memory.</summary>
            private readonly AiMemory _memory;

            /// <summary>The controller's generator.</summary>
            private readonly DeterministicRandom _rng;

            /// <summary>The own HQ.</summary>
            private readonly AiUnitView _hq;

            /// <summary>Intents so far.</summary>
            private readonly List<AiIntent> _intents;

            /// <summary>Common left after this think's spending so far. Unit: resource units. Range: 0 upward.</summary>
            private int _common;

            /// <summary>Rare left after this think's spending so far. Unit: resource units. Range: 0 upward.</summary>
            private int _rare;

            /// <summary>Supply used including this think's orders. Unit: supply. Range: 0 upward.</summary>
            private int _supplyUsed;

            /// <summary>Common held back for a due structure. Unit: resource units. Range: 0 upward.</summary>
            private int _reserveCommon;

            /// <summary>Rare held back for a due structure. Unit: resource units. Range: 0 upward.</summary>
            private int _reserveRare;

            /// <summary>Building sites chosen this think (tile indices), so two structures never share one.</summary>
            private readonly SortedSet<int> _reservedSites = new SortedSet<int>();

            /// <summary>Fields with resources within field_max_distance_tiles of the HQ, in index order.</summary>
            private readonly List<AiFieldView> _fieldsInRange = new List<AiFieldView>();

            /// <summary>True when all_in_when_mined_out is set and no field in range has resources left: attack with everything, never retreat.</summary>
            private bool AllIn => _profile.AllInWhenMinedOut && _fieldsInRange.Count == 0;

            /// <summary>Captures the think's inputs and starts the ledger from the view's stock.</summary>
            public Turn(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory, DeterministicRandom rng, AiUnitView hq, List<AiIntent> intents)
            {
                _view = view;
                _profile = profile;
                _plan = plan;
                _memory = memory;
                _rng = rng;
                _hq = hq;
                _intents = intents;
                _common = view.Common;
                _rare = view.Rare;
                _supplyUsed = view.SupplyUsed;
                // Largest worked distance, squared. Unit: squared tiles.
                Fix64 reachSquared = profile.FieldMaxDistanceTiles * profile.FieldMaxDistanceTiles;
                foreach (AiFieldView field in view.Fields)
                {
                    if (field.Remaining > 0 && (field.Centre - hq.Position).LengthSquared() <= reachSquared)
                    {
                        _fieldsInRange.Add(field);
                    }
                }
            }

            /// <summary>Appends an intent unless the per-think cap is reached; returns false when it was dropped.</summary>
            private bool Emit(AiIntent intent)
            {
                if (_intents.Count >= _profile.MaxCommandsPerThink)
                {
                    return false;
                }
                _intents.Add(intent);
                return true;
            }

            /// <summary>True when the stock minus reservations pays for <paramref name="definition"/>.</summary>
            private bool CanPay(UnitDefinition definition)
            {
                return _common - _reserveCommon >= definition.CostCommon && _rare - _reserveRare >= definition.CostRare;
            }

            /// <summary>True when <paramref name="definition"/>'s supply fits under the cap.</summary>
            private bool HasSupply(UnitDefinition definition)
            {
                return _supplyUsed + _view.SupplyOf(definition) <= _view.SupplyCap;
            }

            /// <summary>Books the cost and supply of an ordered unit or structure.</summary>
            private void Pay(UnitDefinition definition)
            {
                _common -= definition.CostCommon;
                _rare -= definition.CostRare;
                _supplyUsed += _view.SupplyOf(definition);
            }

            /// <summary>Rule 1b: the Core holder picks the next Planetary Cycle (see the class header).</summary>
            public void Cycle()
            {
                if (!_view.HasCycles || !_view.OwnTeamHoldsCore || _view.IsNextCycleLocked || _view.AllowedNextCycleIds.Count == 0)
                {
                    return;
                }
                // The cycle the AI wants: the first preference that is allowed next, else the first allowed cycle.
                string choice = _view.AllowedNextCycleIds[0];
                foreach (string preferred in _plan.CyclePreference)
                {
                    if (Contains(_view.AllowedNextCycleIds, preferred))
                    {
                        choice = preferred;
                        break;
                    }
                }
                if (!string.Equals(_view.PendingPickId, choice, StringComparison.Ordinal))
                {
                    Emit(AiIntent.PickCycle(choice));
                }
            }

            /// <summary>Rule 1: roots an Uprooted, idle HQ that stands next to a field with resources.</summary>
            public void RootHq()
            {
                if (_hq.HqState != HqState.Uprooted || _hq.HasMoveTarget)
                {
                    return;
                }
                // Root reach squared. Unit: squared tiles.
                Fix64 reachSquared = _view.HqRootMaxDistanceTiles * _view.HqRootMaxDistanceTiles;
                foreach (AiFieldView field in _view.Fields)
                {
                    if (field.Remaining > 0 && (field.Centre - _hq.Position).LengthSquared() <= reachSquared)
                    {
                        Emit(AiIntent.RootHq(_hq.Id));
                        return;
                    }
                }
            }

            /// <summary>Rule 2: sends idle workers to fields.</summary>
            public void Gather()
            {
                if (_hq.HqState != HqState.Rooted || _fieldsInRange.Count == 0)
                {
                    return;
                }
                // Workers already on each field, by field index.
                int[] assigned = new int[_view.Fields.Count];
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsWorker && unit.GatherFieldIndex >= 0 && unit.GatherFieldIndex < assigned.Length)
                    {
                        assigned[unit.GatherFieldIndex]++;
                    }
                }
                foreach (AiUnitView worker in _view.OwnUnits)
                {
                    if (!worker.IsWorker || worker.GatherState != GatherState.Idle || worker.HasMoveTarget || worker.IsAttackMoving)
                    {
                        continue;
                    }
                    // Nearest field under the per-field cap, else nearest field in range at all.
                    AiFieldView? field = NearestField(worker.Position, assigned, true) ?? NearestField(worker.Position, assigned, false);
                    if (field == null || !Emit(AiIntent.Gather(worker.Id, field.Index)))
                    {
                        continue;
                    }
                    assigned[field.Index]++;
                }
            }

            /// <summary>The in-range field nearest <paramref name="from"/>, ties to the lowest index; with <paramref name="underCap"/> only fields below workers_per_field.</summary>
            private AiFieldView? NearestField(FixVec2 from, int[] assigned, bool underCap)
            {
                // Best candidate so far, or null.
                AiFieldView? best = null;
                // Squared distance of the best field. Unit: squared tiles.
                Fix64 bestSquared = Fix64.Zero;
                foreach (AiFieldView field in _fieldsInRange)
                {
                    if (underCap && assigned[field.Index] >= _profile.WorkersPerField)
                    {
                        continue;
                    }
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (field.Centre - from).LengthSquared();
                    if (best == null || squared < bestSquared)
                    {
                        best = field;
                        bestSquared = squared;
                    }
                }
                return best;
            }

            /// <summary>Rule 3: supply structure, then the first unmet build order step.</summary>
            public void Structures()
            {
                // True while a supply-giving structure of ours is still being built.
                bool supplyPending = false;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    supplyPending |= unit.IsUnderConstruction && unit.Definition.Economy.ProvidesSupply > 0;
                }
                if (!supplyPending && _view.SupplyCap - _view.SupplyUsed <= _profile.SupplyBuffer && _view.SupplyCap < _view.SupplyCapLimit)
                {
                    TryBuild(_view.Content.Units[_plan.SupplyBuildingId]);
                }
                foreach (AiBuildStep step in _plan.BuildOrder)
                {
                    if (CountOwn(step.StructureId) >= step.Count)
                    {
                        continue;
                    }
                    if (_view.SupplyUsed >= step.AtSupplyUsed)
                    {
                        TryBuild(_view.Content.Units[step.StructureId]);
                    }
                    // Strict order: later steps wait for this one.
                    break;
                }
            }

            /// <summary>Own units (structures included, under construction included) of a definition. Unit: count.</summary>
            private int CountOwn(string definitionId)
            {
                // Matches found. Unit: count.
                int count = 0;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (string.Equals(unit.Definition.Id, definitionId, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
                return count;
            }

            /// <summary>Places <paramref name="definition"/> if the retry time passed, it is affordable, a builder and a site exist; reserves its cost when only money is short.</summary>
            private void TryBuild(UnitDefinition definition)
            {
                // Ticks between attempts at one structure. Unit: ticks.
                int retryTicks = _view.SecondsToTicks(_profile.BuildRetrySeconds);
                if (_memory.LastBuildAttemptTick.TryGetValue(definition.Id, out int last) && _view.Tick < last + retryTicks)
                {
                    return;
                }
                if (!HasSupply(definition))
                {
                    return;
                }
                if (_common - _reserveCommon < definition.CostCommon || _rare - _reserveRare < definition.CostRare)
                {
                    _reserveCommon += definition.CostCommon;
                    _reserveRare += definition.CostRare;
                    return;
                }
                // Worker that places the structure, or null when the player has none.
                AiUnitView? builder = PickBuilder();
                if (builder == null)
                {
                    return;
                }
                _memory.LastBuildAttemptTick[definition.Id] = _view.Tick;
                if (!TryFindSite(out int x, out int y))
                {
                    return;
                }
                if (Emit(AiIntent.Build(builder.Id, definition.Id, x, y)))
                {
                    Pay(definition);
                    _reservedSites.Add(_view.IndexOf(x, y));
                }
            }

            /// <summary>The lowest-ID gathering worker, else the lowest-ID worker, else null.</summary>
            private AiUnitView? PickBuilder()
            {
                // Lowest-ID worker of any state, the fallback builder.
                AiUnitView? any = null;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (!unit.IsWorker)
                    {
                        continue;
                    }
                    if (unit.GatherState != GatherState.Idle)
                    {
                        return unit;
                    }
                    any ??= unit;
                }
                return any;
            }

            /// <summary>The first acceptable building site around the HQ (see rule 3 in the file header).</summary>
            private bool TryFindSite(out int siteX, out int siteY)
            {
                // HQ tile and its height layer.
                int hx = _hq.TileX;
                // HQ tile row. Unit: tiles.
                int hy = _hq.TileY;
                // Height layer of the HQ's tile; sites must match it. Range: 0 to 7.
                byte baseHeight = _view.HeightAt(hx, hy);
                // Ring distance from the HQ tile. Unit: tiles. Range: build_ring_min_tiles to build_ring_max_tiles.
                for (int ring = _profile.BuildRingMinTiles; ring <= _profile.BuildRingMaxTiles; ring++)
                {
                    // Row offset. Unit: tiles. Range: -ring to ring.
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        // Column offset. Unit: tiles. Range: -ring to ring.
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                            {
                                continue;
                            }
                            if (IsGoodSite(hx + dx, hy + dy, baseHeight))
                            {
                                siteX = hx + dx;
                                siteY = hy + dy;
                                return true;
                            }
                        }
                    }
                }
                siteX = 0;
                siteY = 0;
                return false;
            }

            /// <summary>True when (x, y) passes every site test of rule 3.</summary>
            private bool IsGoodSite(int x, int y, byte baseHeight)
            {
                // Neighbour row offset. Unit: tiles. Range: -1 to 1.
                for (int ny = -1; ny <= 1; ny++)
                {
                    // Neighbour column offset. Unit: tiles. Range: -1 to 1.
                    for (int nx = -1; nx <= 1; nx++)
                    {
                        if (!_view.Contains(x + nx, y + ny) || _view.IsBlocked(x + nx, y + ny) || _view.IsRamp(x + nx, y + ny)
                            || _view.HeightAt(x + nx, y + ny) != baseHeight)
                        {
                            return false;
                        }
                    }
                }
                if (_view.IsOccupied(x, y) || _reservedSites.Contains(_view.IndexOf(x, y)))
                {
                    return false;
                }
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Math.Max(Math.Abs(field.X - x), Math.Abs(field.Y - y)) <= _profile.FieldClearanceTiles)
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>Rule 4: trains one worker at a time at the Rooted HQ up to the target.</summary>
            public void TrainWorkers()
            {
                if (_hq.HqState != HqState.Rooted || _hq.IsUnderConstruction || _hq.Queue.Count > 0 || _hq.Definition.Hq == null
                    || !string.Equals(_hq.Definition.Hq.WorkerUnitId, _plan.WorkerUnitId, StringComparison.Ordinal))
                {
                    return;
                }
                // Workers wanted: workers_per_field per field in range, at most max_workers. Unit: workers.
                int target = Math.Min(_profile.MaxWorkers, _profile.WorkersPerField * _fieldsInRange.Count);
                // Workers alive plus workers queued anywhere. Unit: workers.
                int count = 0;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsWorker)
                    {
                        count++;
                    }
                    foreach (string queued in unit.Queue)
                    {
                        if (string.Equals(queued, _plan.WorkerUnitId, StringComparison.Ordinal))
                        {
                            count++;
                        }
                    }
                }
                if (count >= target)
                {
                    return;
                }
                // The worker unit type.
                UnitDefinition worker = _view.Content.Units[_plan.WorkerUnitId];
                if (_common >= worker.CostCommon && _rare >= worker.CostRare && HasSupply(worker) && Emit(AiIntent.Train(_hq.Id, worker.Id)))
                {
                    Pay(worker);
                }
            }

            /// <summary>Rule 5: each idle producer trains the composition unit with the largest deficit.</summary>
            public void Produce()
            {
                // Own count per composition unit, queued included, in ordinal order. Unit: units.
                SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (string id in _plan.Composition.Keys)
                {
                    counts[id] = 0;
                }
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (counts.ContainsKey(unit.Definition.Id))
                    {
                        counts[unit.Definition.Id]++;
                    }
                    foreach (string queued in unit.Queue)
                    {
                        if (counts.ContainsKey(queued))
                        {
                            counts[queued]++;
                        }
                    }
                }
                foreach (AiUnitView producer in _view.OwnUnits)
                {
                    if (!producer.IsStructure || producer.IsUnderConstruction || producer.Queue.Count > 0 || producer.Definition.Economy.Trains.Count == 0)
                    {
                        continue;
                    }
                    // Trains list of this producer, for the filter.
                    IReadOnlyList<string> trains = producer.Definition.Economy.Trains;
                    // Composition unit this producer should train, or null.
                    string? choice = PickByDeficit(_plan.Composition, counts, id => Contains(trains, id));
                    if (choice == null)
                    {
                        continue;
                    }
                    // Definition of the chosen unit.
                    UnitDefinition definition = _view.Content.Units[choice];
                    if (CanPay(definition) && HasSupply(definition) && Emit(AiIntent.Train(producer.Id, choice)))
                    {
                        Pay(definition);
                        counts[choice]++;
                    }
                }
            }

            /// <summary>True when <paramref name="list"/> holds <paramref name="id"/> (ordinal).</summary>
            private static bool Contains(IReadOnlyList<string> list, string id)
            {
                foreach (string item in list)
                {
                    if (string.Equals(item, id, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>Rule 6: the army stance machine.</summary>
            public void Army()
            {
                // Army units in ascending ID order, and their total supply. Unit: supply.
                List<AiUnitView> army = new List<AiUnitView>();
                // Total supply of the army. Unit: supply. Range: 0 upward.
                int armySupply = 0;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsArmy)
                    {
                        army.Add(unit);
                        armySupply += _view.SupplyOf(unit.Definition);
                    }
                }
                if (DefendIfThreatened(army))
                {
                    return;
                }
                if (_memory.Stance == AiStance.Defend || _memory.Stance == AiStance.Retreat)
                {
                    EnterMassing(_memory, _profile, _rng);
                }
                if (_memory.Stance == AiStance.Massing)
                {
                    Mass(army, armySupply);
                    return;
                }
                Attack(army, armySupply);
            }

            /// <summary>Defend: returns true (and orders the army) when a threat inside the base calls for it.</summary>
            private bool DefendIfThreatened(List<AiUnitView> army)
            {
                // Squared defend radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                // Sum of threat positions and the threat count, for the centre; and the threat's supply. Unit: tiles, units, supply.
                Fix64 sumX = Fix64.Zero;
                // Sum of the Y positions, for the mean. Unit: tiles.
                Fix64 sumY = Fix64.Zero;
                // Visible enemy army units inside the base. Unit: units. Range: 0 upward.
                int threats = 0;
                // Supply of those threats. Unit: supply. Range: 0 upward.
                int threatSupply = 0;
                foreach (AiUnitView enemy in _view.VisibleEnemies)
                {
                    if (enemy.IsArmy && IsInsideBase(enemy.Position, radiusSquared))
                    {
                        sumX += enemy.Position.X;
                        sumY += enemy.Position.Y;
                        threats++;
                        threatSupply += _view.SupplyOf(enemy.Definition);
                    }
                }
                // Most recent hit inside the base, ties to the lowest unit ID.
                DamageReport? latestHit = null;
                foreach (DamageReport report in _memory.RecentDamage.Values)
                {
                    if (IsInsideBase(report.Position, radiusSquared) && (latestHit == null || report.Tick > latestHit.Tick))
                    {
                        latestHit = report;
                    }
                }
                if (threats == 0 && latestHit == null)
                {
                    return false;
                }
                if (_memory.Stance == AiStance.Attack && threatSupply < _profile.DefendRecallSupply)
                {
                    return false;
                }
                _memory.Stance = AiStance.Defend;
                _memory.HasAttackTarget = false;
                // Where the army goes: the mean threat position, else the latest hit.
                FixVec2 centre = threats > 0 ? new FixVec2(sumX / threats, sumY / threats) : latestHit!.Position;
                // Defence goal, moved off a blocked tile. Unit: tiles.
                FixVec2 goal = Standable(centre);
                foreach (AiUnitView unit in army)
                {
                    if (Focus(unit))
                    {
                        continue;
                    }
                    if (unit.IsAttackTargetForced || !unit.IsAttackMoving || (unit.AttackMoveGoal - goal).LengthSquared() > SameGoalSquared)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, goal));
                    }
                }
                return true;
            }

            /// <summary>True when <paramref name="point"/> is within the squared radius of the HQ or of any own structure.</summary>
            private bool IsInsideBase(FixVec2 point, Fix64 radiusSquared)
            {
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if ((unit.IsHq || unit.IsStructure) && (unit.Position - point).LengthSquared() <= radiusSquared)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>Massing: start the attack when ready, otherwise gather idle army units at the rally point.</summary>
            private void Mass(List<AiUnitView> army, int armySupply)
            {
                // True when the tide window allows an attack, or the army is big enough to ignore it.
                bool tideOk = AllIn || (_view.TideLevel >= _plan.AttackTideMin && _view.TideLevel <= _plan.AttackTideMax) || armySupply >= _plan.AttackTideOverrideSupply;
                // Supply needed to attack now: the drawn threshold, or 1 when all in. Unit: supply.
                int threshold = AllIn ? 1 : _memory.AttackThreshold;
                if (army.Count > 0 && _view.Tick >= _memory.RegroupUntilTick && armySupply >= threshold && tideOk
                    && TryChooseTarget(out FixVec2 target))
                {
                    _memory.Stance = AiStance.Attack;
                    _memory.AttackStartSupply = armySupply;
                    _memory.AttackStartTick = _view.Tick;
                    _memory.AttackStaged = _profile.StageDistanceTiles <= Fix64.Zero;
                    _memory.AttacksStarted++;
                    _memory.HasAttackTarget = false;
                    Attack(army, armySupply);
                    return;
                }
                // Where the massing army waits. Unit: tiles.
                FixVec2 rally = RallyPoint();
                foreach (AiUnitView unit in army)
                {
                    if (!unit.HasMoveTarget && !unit.IsAttackMoving && (unit.Position - rally).LengthSquared() > AtRallySquared)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, rally));
                    }
                }
            }

            /// <summary>Attack: retreat on losses, otherwise keep every army unit attack-moving to the current target.</summary>
            private void Attack(List<AiUnitView> army, int armySupply)
            {
                if (army.Count == 0)
                {
                    EnterMassing(_memory, _profile, _rng);
                    return;
                }
                if (!AllIn && ShouldRetreat(army, armySupply))
                {
                    _memory.Retreats++;
                    _memory.Stance = AiStance.Retreat;
                    // Retreat destination: the HQ position, moved off a blocked tile. Unit: tiles.
                    FixVec2 home = Standable(_hq.Position);
                    foreach (AiUnitView unit in army)
                    {
                        Emit(AiIntent.Move(unit.Id, home));
                    }
                    _memory.RegroupUntilTick = _view.Tick + _view.SecondsToTicks(_profile.RegroupSeconds);
                    EnterMassing(_memory, _profile, _rng);
                    return;
                }
                if (!TryChooseTarget(out FixVec2 target))
                {
                    // Nothing known left to attack: go home and mass again.
                    EnterMassing(_memory, _profile, _rng);
                    return;
                }
                // True when the attack switched to a new target this think.
                bool changed = !_memory.HasAttackTarget || (target - _memory.AttackTarget).LengthSquared() > SameGoalSquared;
                _memory.HasAttackTarget = true;
                _memory.AttackTarget = target;
                if (!_memory.AttackStaged)
                {
                    if (!Stage(army, armySupply, target))
                    {
                        return;
                    }
                    changed = true;
                }
                // A visible, undefended enemy HQ to focus, or null.
                AiUnitView? siege = FindSiegeTarget();
                // Squared engagement radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.EngagementRadiusTiles * _profile.EngagementRadiusTiles;
                foreach (AiUnitView unit in army)
                {
                    if (Focus(unit))
                    {
                        continue;
                    }
                    // The unit's weapon, or null for an unarmed unit.
                    WeaponDefinition? weapon = unit.Definition.Weapon;
                    if (siege != null && weapon != null && weapon.CanTarget(siege.Layer) && (unit.Position - siege.Position).LengthSquared() <= radiusSquared)
                    {
                        if (unit.AttackTarget != siege.Id || !unit.IsAttackTargetForced)
                        {
                            Emit(AiIntent.AttackUnit(unit.Id, siege.Id));
                        }
                        continue;
                    }
                    // True when the unit is not already heading for this target (a forced target from an ended siege counts as off target).
                    bool offTarget = unit.IsAttackTargetForced || (unit.IsAttackMoving
                        ? (unit.AttackMoveGoal - target).LengthSquared() > SameGoalSquared
                        : (unit.Position - target).LengthSquared() > SameGoalSquared);
                    if (changed || offTarget)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, target));
                    }
                }
            }

            /// <summary>
            /// Staging: gathers the army at the point stage_distance_tiles short of <paramref name="target"/> (toward the own HQ).
            /// Returns true (and marks the attack staged) once stage_gather_fraction of the army supply stands within
            /// stage_radius_tiles of that point or stage_timeout_seconds have passed since the attack started; otherwise orders
            /// stragglers there and returns false.
            /// </summary>
            private bool Stage(List<AiUnitView> army, int armySupply, FixVec2 target)
            {
                // Staging point short of the target, moved off a blocked tile. Unit: tiles.
                FixVec2 stage = Standable(target.MoveTowards(_hq.Position, _profile.StageDistanceTiles));
                // Squared gathering radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.StageRadiusTiles * _profile.StageRadiusTiles;
                // Army supply standing at the staging point. Unit: supply.
                int gathered = 0;
                foreach (AiUnitView unit in army)
                {
                    if ((unit.Position - stage).LengthSquared() <= radiusSquared)
                    {
                        gathered += _view.SupplyOf(unit.Definition);
                    }
                }
                // True once the wait has run out. Unit: ticks compared.
                bool timedOut = _view.Tick >= _memory.AttackStartTick + _view.SecondsToTicks(_profile.StageTimeoutSeconds);
                if (timedOut || Fix64.FromInt(gathered) >= _profile.StageGatherFraction * Fix64.FromInt(armySupply))
                {
                    _memory.AttackStaged = true;
                    return true;
                }
                foreach (AiUnitView unit in army)
                {
                    if (Focus(unit))
                    {
                        continue;
                    }
                    // True when the unit is already walking to (or standing at) the staging point.
                    bool onTheWay = unit.IsAttackMoving
                        ? (unit.AttackMoveGoal - stage).LengthSquared() <= SameGoalSquared
                        : (unit.Position - stage).LengthSquared() <= radiusSquared;
                    if (!onTheWay || unit.IsAttackTargetForced)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, stage));
                    }
                }
                return false;
            }

            /// <summary>
            /// Focus fire: when a visible enemy army unit this unit's weapon can hit stands within focus_radius_tiles of it, keeps (or
            /// gives) the unit an attack_unit order on one: its current forced target while that is still such a unit, otherwise the
            /// nearest (ties to the lowest ID). Returns true when the unit is now focused (an order was emitted or was already in place),
            /// false when no enemy army unit is near (the caller then gives the stance's normal order). Only enemies in
            /// <see cref="AiWorldView.VisibleEnemies"/> are ever named.
            /// </summary>
            private bool Focus(AiUnitView unit)
            {
                // The unit's weapon, or null for an unarmed unit.
                WeaponDefinition? weapon = unit.Definition.Weapon;
                if (weapon == null || _profile.FocusRadiusTiles <= Fix64.Zero)
                {
                    return false;
                }
                // Squared focus radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.FocusRadiusTiles * _profile.FocusRadiusTiles;
                // Nearest valid candidate so far, or null.
                AiUnitView? nearest = null;
                // Squared distance of the nearest candidate. Unit: squared tiles.
                Fix64 nearestSquared = Fix64.Zero;
                foreach (AiUnitView foe in _view.VisibleEnemies)
                {
                    if (!foe.IsArmy || !weapon.CanTarget(foe.Layer))
                    {
                        continue;
                    }
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (foe.Position - unit.Position).LengthSquared();
                    if (squared > radiusSquared)
                    {
                        continue;
                    }
                    if (unit.IsAttackTargetForced && unit.AttackTarget == foe.Id)
                    {
                        // Already focused on a valid target: keep it.
                        return true;
                    }
                    if (nearest == null || squared < nearestSquared)
                    {
                        nearest = foe;
                        nearestSquared = squared;
                    }
                }
                if (nearest == null)
                {
                    return false;
                }
                Emit(AiIntent.AttackUnit(unit.Id, nearest.Id));
                return true;
            }

            /// <summary>
            /// The visible enemy HQ nearest the own HQ (ties to the lowest ID) that has no visible enemy army unit within
            /// engagement_radius_tiles of it, or null. Only units in <see cref="AiWorldView.VisibleEnemies"/> are considered.
            /// </summary>
            private AiUnitView? FindSiegeTarget()
            {
                // Squared engagement radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.EngagementRadiusTiles * _profile.EngagementRadiusTiles;
                // Best candidate so far, or null.
                AiUnitView? best = null;
                // Squared distance of the best HQ from the own HQ. Unit: squared tiles.
                Fix64 bestSquared = Fix64.Zero;
                foreach (AiUnitView candidate in _view.VisibleEnemies)
                {
                    if (!candidate.IsHq)
                    {
                        continue;
                    }
                    // True when an enemy army unit guards this HQ.
                    bool guarded = false;
                    foreach (AiUnitView other in _view.VisibleEnemies)
                    {
                        guarded |= other.IsArmy && (other.Position - candidate.Position).LengthSquared() <= radiusSquared;
                    }
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (candidate.Position - _hq.Position).LengthSquared();
                    if (!guarded && (best == null || squared < bestSquared))
                    {
                        best = candidate;
                        bestSquared = squared;
                    }
                }
                return best;
            }

            /// <summary>
            /// True when the attack has lost too much (army supply below retreat_army_fraction of its start) or is outgunned: the whole
            /// army's power is below retreat_power_ratio x the power of the visible armed enemies within engagement_radius_tiles of any
            /// army unit. The whole army counts on the own side because stragglers are walking to the same fight.
            /// </summary>
            private bool ShouldRetreat(List<AiUnitView> army, int armySupply)
            {
                if (Fix64.FromInt(armySupply) < _profile.RetreatArmyFraction * Fix64.FromInt(_memory.AttackStartSupply))
                {
                    return true;
                }
                // Squared engagement radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.EngagementRadiusTiles * _profile.EngagementRadiusTiles;
                // Own army power and engaged visible enemy power. Unit: supply-weighted health fraction.
                Fix64 own = Fix64.Zero;
                // Power of the visible armed enemies engaged with the army. Unit: supply-weighted health fraction.
                Fix64 enemy = Fix64.Zero;
                foreach (AiUnitView unit in army)
                {
                    own += PowerOf(unit);
                }
                foreach (AiUnitView foe in _view.VisibleEnemies)
                {
                    if (foe.Definition.Weapon == null)
                    {
                        continue;
                    }
                    foreach (AiUnitView unit in army)
                    {
                        if ((unit.Position - foe.Position).LengthSquared() <= radiusSquared)
                        {
                            enemy += PowerOf(foe);
                            break;
                        }
                    }
                }
                return enemy > Fix64.Zero && own < _profile.RetreatPowerRatio * enemy;
            }

            /// <summary>Combat power of one unit: supply x health / max health. Unit: supply. Range: 0 to the unit's supply.</summary>
            private Fix64 PowerOf(AiUnitView unit)
            {
                return Fix64.FromInt(_view.SupplyOf(unit.Definition)) * unit.Health / unit.Definition.MaxHealth;
            }

            /// <summary>
            /// The attack target: the known enemy HQ nearest the own HQ, else the nearest known enemy structure, else the nearest
            /// enemy spawn not yet seen empty, else the last seen enemy army position. False when none exists.
            /// </summary>
            private bool TryChooseTarget(out FixVec2 target)
            {
                // Pass 0 looks at HQs only, pass 1 at every known structure.
                for (int pass = 0; pass < 2; pass++)
                {
                    // Best candidate so far, or null.
                    KnownStructure? best = null;
                    // Squared distance of the best candidate. Unit: squared tiles.
                    Fix64 bestSquared = Fix64.Zero;
                    foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                    {
                        if (pass == 0 && !known.IsHq)
                        {
                            continue;
                        }
                        // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                        Fix64 squared = (known.Position - _hq.Position).LengthSquared();
                        if (best == null || squared < bestSquared)
                        {
                            best = known;
                            bestSquared = squared;
                        }
                    }
                    if (best != null)
                    {
                        target = Standable(best.Position);
                        return true;
                    }
                }
                if (TryEnemySpawn(out FixVec2 spawn))
                {
                    target = Standable(spawn);
                    return true;
                }
                if (_memory.HasLastSeenEnemy)
                {
                    target = Standable(_memory.LastSeenEnemyPosition);
                    return true;
                }
                target = _hq.Position;
                return false;
            }

            /// <summary>The enemy spawn nearest the own HQ that is neither the own spawn nor seen empty, ties to the lowest index.</summary>
            private bool TryEnemySpawn(out FixVec2 spawn)
            {
                // Best spawn index and squared distance. Unit: squared tiles.
                int best = -1;
                // Squared distance of the best candidate so far. Unit: squared tiles. Range: 0 upward.
                Fix64 bestSquared = Fix64.Zero;
                // Spawn index. Range: 0 to spawn count - 1.
                for (int i = 0; i < _view.Spawns.Count; i++)
                {
                    if (i == _memory.OwnSpawnIndex || _memory.ClearedSpawns.Contains(i))
                    {
                        continue;
                    }
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (_view.Spawns[i] - _hq.Position).LengthSquared();
                    if (best < 0 || squared < bestSquared)
                    {
                        best = i;
                        bestSquared = squared;
                    }
                }
                spawn = best >= 0 ? _view.Spawns[best] : _hq.Position;
                return best >= 0;
            }

            /// <summary>Where the massing army waits: the Core centre when rally_at_core applies, else rally_distance_tiles from the HQ toward the attack target (or the HQ itself).</summary>
            private FixVec2 RallyPoint()
            {
                if (_profile.RallyAtCore && _view.HasCore && !_view.OwnTeamHoldsCore)
                {
                    // Take and hold the Core: the massing army waits at its centre.
                    return Standable(_view.CoreCentre);
                }
                if (!TryChooseTarget(out FixVec2 toward))
                {
                    return Standable(_hq.Position);
                }
                return Standable(_hq.Position.MoveTowards(toward, _profile.RallyDistanceTiles));
            }

            /// <summary>
            /// <paramref name="point"/> when its tile is on the map and unblocked, else the centre of the first unblocked neighbour
            /// tile in TerrainGrid direction order (N, E, S, W, NE, SE, SW, NW), else the point (same rule as EconomyRules.StandablePointNear).
            /// </summary>
            private FixVec2 Standable(FixVec2 point)
            {
                // Tile of the point.
                int x = point.X.FloorToInt();
                // Tile row of the point. Unit: tiles.
                int y = point.Y.FloorToInt();
                if (!_view.Contains(x, y) || !_view.IsBlocked(x, y))
                {
                    return point;
                }
                // Direction number. Range: 0 to 7.
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Neighbour tile column. Unit: tiles.
                    int nx = x + TerrainGrid.NeighbourDx[direction];
                    // Neighbour tile row. Unit: tiles.
                    int ny = y + TerrainGrid.NeighbourDy[direction];
                    if (_view.Contains(nx, ny) && !_view.IsBlocked(nx, ny))
                    {
                        return _view.CentreOf(nx, ny);
                    }
                }
                return point;
            }
        }
    }
}
