// rts/src/Ai/AiBrainFight.cs
// The fight choices of skirmish_ai_v3.md Package D, as the second half of the brain's per-think working state (AiBrain.Turn is partial):
//   D.1 attack by prediction (use_predictor_attack): the Massing army attacks when the fight predictor says it wins by attack_min_advantage
//       against the enemy model at the chosen target (scaled by undefended_army_share for an undefended target) plus the known static
//       defences near it, or at attack_force_supply; with nothing known the v2 drawn threshold decides.
//   D.2 retreat by prediction (use_predictor_attack): the attack group retreats when the visible armed enemies near it beat it below
//       retreat_max_advantage.
//   D.3 target choice (use_target_choice): undefended enemy depots, then undefended producers, come before the v2 target order.
//   D.4 rule HR (use_target_choice): a small squad of fast units harasses the known enemy drop-off farthest from the enemy army.
//   D.5 measured defence (use_measured_defence): only the nearest units the predictor needs defend a threat inside the base.
//   D.6 unit control (use_unit_control): hurt units fall back and ranged units step back from armed enemy army units with a shorter range;
//       with use_focus_fire, units focus the enemy that dies fastest.
// Part of Rts.Ai. Everything reads only the AiWorldView (visible enemies, own units, public map data), the profile and the AiMemory; Fix64 and
// integers only, ascending-ID iteration, ties to the lower ID, no random draw, no faction or unit named. Spatial queries go through AiProximity so no
// loop runs over every pair of own and enemy units (a clumped army still visits every pair in the worst case, so the cost is bounded by the army
// size squared, not removed); each rule has a command budget from the profile. With every package D switch off none of this
// runs and the AI plays as before (the memory sets stay empty).
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    public static partial class AiBrain
    {
        private sealed partial class Turn
        {
            /// <summary>The visible enemy army units inside the base this think (the threats of <see cref="ScanThreat"/>), ascending ID. Unit: units. Range: 0 to the visible enemy army count.</summary>
            private readonly List<AiUnitView> _threats = new List<AiUnitView>();

            /// <summary>Every own army unit that is not an active scout, ascending ID; set by <see cref="Army"/> before the package D rules run.</summary>
            private List<AiUnitView> _armyAll = new List<AiUnitView>();

            /// <summary>Units given an order by the fall-back or keep-distance rule this think; they get no stance order (D.6). Ascending ID.</summary>
            private readonly SortedSet<EntityId> _controlled = new SortedSet<EntityId>();

            /// <summary>Focus-fire orders given this think (D.6.3). Unit: commands. Range: 0 to focus_max_commands_per_think.</summary>
            private int _focusCommands;

            /// <summary>Time to kill of each visible enemy army unit computed this think, or -1 when no own unit can shoot it (D.6.3). Unit: seconds. Range: -1, or 0 upward.</summary>
            private readonly SortedDictionary<EntityId, Fix64> _timeToKill = new SortedDictionary<EntityId, Fix64>();

            /// <summary>Own army index for focus fire, built on first use. Unit: grid cells of focus_radius_tiles (tiles, 1 to 30) over the own army units (0 to max army size).</summary>
            private AiProximity? _ownIndex;

            /// <summary>Visible enemy army units index for focus fire, built on first use. Unit: grid cells of focus_radius_tiles (tiles, 1 to 30) over the visible enemy army units (0 to their count).</summary>
            private AiProximity? _foeIndex;

            /// <summary>Visible armed non-worker enemies (structures included) index for the fall-back test, built on first use. Unit: grid cells of fallback_danger_tiles (tiles, 1 to 30) over the armed enemies (0 to their count).</summary>
            private AiProximity? _armedFoeIndex;

            /// <summary>Visible armed enemy army units index for the keep-distance test, built on first use. Unit: grid cells of kite_trigger_tiles (tiles, 0.5 to 20) over the armed enemy army units (0 to their count).</summary>
            private AiProximity? _kiteFoeIndex;

            /// <summary>The predictor settings of the profile, built on first use.</summary>
            private PredictorSettings? _predictorSettings;

            /// <summary>Known enemy static defences with a weapon: position and predictor entry (count 1, believed health), ascending ID; built on first use.</summary>
            private List<(FixVec2 Position, FightEntry Entry)>? _knownDefences;

            /// <summary>True when <paramref name="unit"/> was ordered by the fall-back or keep-distance rule this think.</summary>
            private bool Controlled(AiUnitView unit)
            {
                return _controlled.Count > 0 && _controlled.Contains(unit.Id);
            }

            /// <summary>True when the unit is in the fallen-back, harass or defend group, so it gets no stance order (D.4, D.5, D.6.1).</summary>
            private bool InPackageDRole(EntityId id)
            {
                return (_memory.FallenBack.Count > 0 && _memory.FallenBack.Contains(id))
                    || (_memory.HarassGroup.Count > 0 && _memory.HarassGroup.Contains(id))
                    || (_memory.DefendGroup.Count > 0 && _memory.DefendGroup.Contains(id));
            }

            /// <summary>The army units that receive stance orders: <paramref name="armyAll"/> without the fallen-back, harass and defend groups (ascending ID).</summary>
            /// <param name="armyAll">Every own army unit that is not an active scout.</param>
            /// <param name="supply">Receives their total supply. Unit: supply. Range: 0 upward.</param>
            private List<AiUnitView> StanceArmy(List<AiUnitView> armyAll, out int supply)
            {
                // The units left after the exclusions.
                List<AiUnitView> army = new List<AiUnitView>();
                supply = 0;
                foreach (AiUnitView unit in armyAll)
                {
                    if (!InPackageDRole(unit.Id))
                    {
                        army.Add(unit);
                        supply += _view.SupplyOf(unit.Definition);
                    }
                }
                return army;
            }

            /// <summary>Predictor call with the profile's settings and the damage table.</summary>
            private FightPrediction PredictFight(List<FightEntry> a, List<FightEntry> b, bool aUphill)
            {
                _predictorSettings ??= PredictorSettings.From(_profile);
                return FightPredictor.Predict(a, b, aUphill, _view.Content.Combat, _predictorSettings);
            }

            /// <summary>True when the tile of <paramref name="to"/> lies on a higher height layer than the tile of <paramref name="from"/> (the predictor's "uphill").</summary>
            private bool IsUphill(FixVec2 to, FixVec2 from)
            {
                // Tile columns and rows of the two points. Unit: tiles.
                int toX = to.X.FloorToInt();
                int toY = to.Y.FloorToInt();
                int fromX = from.X.FloorToInt();
                int fromY = from.Y.FloorToInt();
                return _view.Contains(toX, toY) && _view.Contains(fromX, fromY) && _view.HeightAt(toX, toY) > _view.HeightAt(fromX, fromY);
            }

            /// <summary>The mean position of a non-empty unit list. Unit: tiles.</summary>
            private static FixVec2 MeanPosition(List<AiUnitView> units)
            {
                // Sums of the X and Y positions. Unit: tiles.
                Fix64 sumX = Fix64.Zero;
                Fix64 sumY = Fix64.Zero;
                foreach (AiUnitView unit in units)
                {
                    sumX += unit.Position.X;
                    sumY += unit.Position.Y;
                }
                return new FixVec2(sumX / units.Count, sumY / units.Count);
            }

            // ------------------------------------------------------------------ D.1 and D.2

            /// <summary>The known enemy static defences with a weapon, built once per think.</summary>
            private List<(FixVec2 Position, FightEntry Entry)> KnownDefences()
            {
                if (_knownDefences != null)
                {
                    return _knownDefences;
                }
                _knownDefences = new List<(FixVec2, FightEntry)>();
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    if (_view.Content.Units.TryGetValue(known.DefinitionId, out UnitDefinition? definition) && definition.Economy.IsStaticDefence && definition.Weapon != null)
                    {
                        _knownDefences.Add((known.Position, AiFight.StructureEntry(definition, known.HealthFraction)));
                    }
                }
                return _knownDefences;
            }

            /// <summary>The known enemy static defences within target_defence_radius_tiles of <paramref name="point"/> as predictor entries (ordinal order).</summary>
            private List<FightEntry> DefenceEntriesNear(FixVec2 point)
            {
                // Squared radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.TargetDefenceRadiusTiles * _profile.TargetDefenceRadiusTiles;
                // The defences found.
                List<FightEntry> near = new List<FightEntry>();
                foreach ((FixVec2 position, FightEntry entry) in KnownDefences())
                {
                    if ((position - point).LengthSquared() <= radiusSquared)
                    {
                        near.Add(entry);
                    }
                }
                return AiFight.Merge(near, new List<FightEntry>());
            }

            /// <summary>
            /// D.1: when something is known about the enemy at the chosen target, says whether the army should attack it. Returns false (the v2
            /// threshold then decides) when no target exists or the enemy side is empty (no model entry with a count and no static defence near
            /// the target). Win: AWins with Advantage at least attack_min_advantage and army supply at least attack_min_supply, or army supply at
            /// least attack_force_supply.
            /// </summary>
            /// <param name="army">The units that would attack (no scout, harass, defend or fallen-back units).</param>
            /// <param name="armySupply">Their supply. Unit: supply.</param>
            /// <param name="win">True when the army should attack. Meaningful only when the method returns true.</param>
            private bool PredictedAttack(List<AiUnitView> army, int armySupply, out bool win)
            {
                win = false;
                if (!TryChooseTarget(out FixVec2 target, out bool undefended))
                {
                    return false;
                }
                // The enemy model at its full or undefended strength, plus the defences near the target.
                List<FightEntry> enemy = AiFight.Merge(AiFight.Scaled(_enemies, undefended ? _profile.UndefendedArmyShare : Fix64.One), DefenceEntriesNear(target));
                // True when at least one entry has a count (the information test).
                bool informed = false;
                foreach (FightEntry entry in enemy)
                {
                    informed |= entry.Count > Fix64.Zero;
                }
                if (!informed)
                {
                    return false;
                }
                // The prediction for the whole army against that enemy side.
                FightPrediction prediction = PredictFight(FightPredictor.Group(army), enemy, IsUphill(target, RallyPoint()));
                win = (prediction.AWins && prediction.Advantage >= _profile.AttackMinAdvantage && armySupply >= _profile.AttackMinSupply)
                    || armySupply >= _profile.AttackForceSupply;
                return true;
            }

            /// <summary>The visible armed non-worker enemies within engagement_radius_tiles of any unit indexed by <paramref name="own"/> (static defences included), ascending ID.</summary>
            private List<AiUnitView> EngagedEnemies(AiProximity own)
            {
                // The engaged enemies.
                List<AiUnitView> engaged = new List<AiUnitView>();
                foreach (AiUnitView foe in _view.VisibleEnemies)
                {
                    if (foe.Definition.Weapon != null && !foe.IsWorker && own.AnyNear(foe.Position, _profile.EngagementRadiusTiles))
                    {
                        engaged.Add(foe);
                    }
                }
                return engaged;
            }

            /// <summary>
            /// D.2 (also the end test of rule HR): true when the visible armed enemies near <paramref name="group"/> are not empty and the
            /// group's predicted advantage against them is below retreat_max_advantage. The group shoots uphill when the enemies' mean position is
            /// on a higher height layer than its own.
            /// </summary>
            /// <param name="group">The units that fight (the attack group or the harass squad), ascending ID.</param>
            private bool PredictedRetreat(List<AiUnitView> group)
            {
                if (group.Count == 0)
                {
                    return false;
                }
                // Visible armed enemies near the group.
                List<AiUnitView> engaged = EngagedEnemies(new AiProximity(group, _profile.EngagementRadiusTiles));
                if (engaged.Count == 0)
                {
                    return false;
                }
                // The group's prediction against those enemies.
                FightPrediction prediction = PredictFight(FightPredictor.Group(group), FightPredictor.Group(engaged), IsUphill(MeanPosition(engaged), MeanPosition(group)));
                return prediction.Advantage < _profile.RetreatMaxAdvantage;
            }

            // ------------------------------------------------------------------ D.3

            /// <summary>
            /// The enemy army position of D.3: the last seen enemy army unit while seen within enemy_army_memory_seconds, else the nearest
            /// known enemy HQ to home, else the nearest uncleared enemy spawn. False (position = home) when none exists.
            /// </summary>
            private bool EnemyArmyPosition(out FixVec2 position)
            {
                if (_memory.HasLastSeenEnemy && _view.Tick - _memory.LastSeenEnemyTick <= _view.SecondsToTicks(_profile.EnemyArmyMemorySeconds))
                {
                    position = _memory.LastSeenEnemyPosition;
                    return true;
                }
                // Nearest known enemy HQ to home, or null, and its squared distance. Unit: squared tiles.
                KnownStructure? nearest = null;
                Fix64 nearestSquared = Fix64.Zero;
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    // Squared distance of this candidate from home. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (known.Position - Home()).LengthSquared();
                    if (known.IsHq && (nearest == null || squared < nearestSquared))
                    {
                        nearest = known;
                        nearestSquared = squared;
                    }
                }
                if (nearest != null)
                {
                    position = nearest.Position;
                    return true;
                }
                if (TryEnemySpawn(out FixVec2 spawn))
                {
                    position = spawn;
                    return true;
                }
                position = Home();
                return false;
            }

            /// <summary>
            /// D.3: the nearest-to-home undefended known enemy drop-off that is not an HQ, else the nearest undefended known enemy producer
            /// (ties to the lower ID). A structure is undefended when no known enemy static defence lies within target_defence_radius_tiles
            /// of it and the enemy army position is farther than that. False when there is none.
            /// </summary>
            private bool TryUndefendedTarget(out FixVec2 target)
            {
                // Squared defence radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.TargetDefenceRadiusTiles * _profile.TargetDefenceRadiusTiles;
                // Where the enemy army is believed to be, and whether it is known.
                bool hasArmy = EnemyArmyPosition(out FixVec2 armyPosition);
                // Pass 0 looks at drop-offs, pass 1 at producers.
                for (int pass = 0; pass < 2; pass++)
                {
                    // Best candidate so far, or null, and its squared distance from home. Unit: squared tiles.
                    KnownStructure? best = null;
                    Fix64 bestSquared = Fix64.Zero;
                    foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                    {
                        if (!_view.Content.Units.TryGetValue(known.DefinitionId, out UnitDefinition? definition))
                        {
                            continue;
                        }
                        if (pass == 0 ? (known.IsHq || !definition.Economy.IsDropOff) : definition.Economy.Trains.Count == 0)
                        {
                            continue;
                        }
                        if ((hasArmy && (armyPosition - known.Position).LengthSquared() <= radiusSquared) || DefenceEntriesNear(known.Position).Count > 0)
                        {
                            continue;
                        }
                        // Squared distance of this candidate from home. Unit: squared tiles. Range: 0 upward.
                        Fix64 squared = (known.Position - Home()).LengthSquared();
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
                target = _hq.Position;
                return false;
            }

            // ------------------------------------------------------------------ groups

            /// <summary>
            /// Housekeeping before the package D rules: fallen-back units that died or became scouts are forgotten, those within 3 tiles of
            /// home leave the fallen-back group; defend-group members that no longer exist are dropped.
            /// </summary>
            /// <param name="armyAll">Every own army unit that is not an active scout.</param>
            private void PrepareGroups(List<AiUnitView> armyAll)
            {
                if (_memory.FallenBack.Count == 0 && _memory.DefendGroup.Count == 0)
                {
                    return;
                }
                // The army units by ID.
                SortedDictionary<EntityId, AiUnitView> byId = new SortedDictionary<EntityId, AiUnitView>();
                foreach (AiUnitView unit in armyAll)
                {
                    byId.Add(unit.Id, unit);
                }
                // Where fallen-back units are headed (the relocation destination while the HQ relocates). Unit: tiles.
                FixVec2 home = Standable(Home());
                // Ticks after which a fallen-back unit is released. Unit: ticks.
                int release = _view.SecondsToTicks(_profile.FallbackReleaseSeconds);
                // Fallen-back units that leave the group this think (dead, home, or released after the timeout). Unit: unit IDs.
                List<EntityId> leaving = new List<EntityId>();
                foreach (EntityId id in _memory.FallenBack)
                {
                    if (!byId.TryGetValue(id, out AiUnitView? unit) || (unit.Position - home).LengthSquared() <= AtRallySquared
                        || (_memory.FallenBackSince.TryGetValue(id, out int since) && _view.Tick >= since + release))
                    {
                        leaving.Add(id);
                    }
                    else if (!unit.HasMoveTarget && !unit.IsAttackMoving)
                    {
                        // Idle and away from home (its move ended short, or the home point moved): send it again.
                        Emit(AiIntent.Move(unit.Id, home));
                    }
                }
                foreach (EntityId id in leaving)
                {
                    _memory.FallenBack.Remove(id);
                    _memory.FallenBackSince.Remove(id);
                }
                _memory.DefendGroup.RemoveWhere(id => !byId.ContainsKey(id));
            }

            // ------------------------------------------------------------------ D.4 rule HR

            /// <summary>
            /// Rule HR (D.4): runs the harass squad, ends it, or starts a new one. Only with use_target_choice. Runs before the stance orders; its
            /// members are excluded from them.
            /// </summary>
            /// <param name="armyAll">Every own army unit that is not an active scout.</param>
            /// <param name="armySupplyAll">Their total supply. Unit: supply.</param>
            private void Harass(List<AiUnitView> armyAll, int armySupplyAll)
            {
                if (!_profile.UseTargetChoice)
                {
                    return;
                }
                // Cooldown after a squad ends. Unit: ticks.
                int cooldown = _view.SecondsToTicks(_profile.HarassCooldownSeconds);
                // The living squad members, ascending ID.
                List<AiUnitView> squad = new List<AiUnitView>();
                foreach (AiUnitView unit in armyAll)
                {
                    if (_memory.HarassGroup.Contains(unit.Id))
                    {
                        squad.Add(unit);
                    }
                }
                if (_memory.HarassGroup.Count > 0 && squad.Count == 0)
                {
                    // Every member is dead: the squad ends.
                    _memory.HarassGroup.Clear();
                    _memory.NextHarassTick = _view.Tick + cooldown;
                    return;
                }
                if (squad.Count > 0)
                {
                    _memory.HarassGroup.Clear();
                    foreach (AiUnitView unit in squad)
                    {
                        _memory.HarassGroup.Add(unit.Id);
                    }
                    RunSquad(squad, cooldown);
                    return;
                }
                if (_view.Tick >= _memory.NextHarassTick && (_memory.Stance == AiStance.Massing || _memory.Stance == AiStance.Attack) && armySupplyAll >= _profile.HarassMinArmySupply)
                {
                    StartSquad(armyAll);
                }
            }

            /// <summary>Rule HR start: picks fast armed army units until harass_squad_supply, the farthest known enemy drop-off from the enemy army, and sends the squad if it wins there.</summary>
            private void StartSquad(List<AiUnitView> armyAll)
            {
                // The squad being picked, and its supply. Unit: supply.
                List<AiUnitView> squad = new List<AiUnitView>();
                int supply = 0;
                foreach (AiUnitView unit in armyAll)
                {
                    if (supply >= _profile.HarassSquadSupply)
                    {
                        break;
                    }
                    if (unit.Id == _memory.ScoutId || unit.Definition.Weapon == null || _memory.AttackGroup.Contains(unit.Id) || InPackageDRole(unit.Id))
                    {
                        continue;
                    }
                    if (unit.Layer == MovementLayer.Air || unit.Definition.SpeedTilesPerSecond >= _profile.HarassMinSpeedTilesPerSecond)
                    {
                        squad.Add(unit);
                        supply += _view.SupplyOf(unit.Definition);
                    }
                }
                if (supply < _profile.HarassSquadSupply || !EnemyArmyPosition(out FixVec2 armyPosition))
                {
                    return;
                }
                // The known drop-off (HQ included) farthest from the enemy army, ties to the lower ID, and its squared distance. Unit: squared tiles.
                KnownStructure? target = null;
                Fix64 farthest = Fix64.Zero;
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    if (!known.IsHq && !(_view.Content.Units.TryGetValue(known.DefinitionId, out UnitDefinition? definition) && definition.Economy.IsDropOff))
                    {
                        continue;
                    }
                    // Squared distance of this candidate from the enemy army. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (known.Position - armyPosition).LengthSquared();
                    if (target == null || squared > farthest)
                    {
                        target = known;
                        farthest = squared;
                    }
                }
                if (target == null)
                {
                    return;
                }
                // Static defences near the target; the squad must win against them.
                List<FightEntry> defences = DefenceEntriesNear(target.Position);
                if (defences.Count > 0 && !PredictFight(FightPredictor.Group(squad), defences, IsUphill(target.Position, Home())).AWins)
                {
                    return;
                }
                _memory.HarassTarget = target.Position;
                _memory.HarassStartTick = _view.Tick;
                _memory.HarassArrivedTick = -1;
                _memory.HarassLastWorkerTick = _view.Tick;
                // Where the squad goes. Unit: tiles.
                FixVec2 goal = Standable(target.Position);
                // Commands still allowed this think. Unit: commands.
                int budget = _profile.HarassMaxCommandsPerThink;
                foreach (AiUnitView unit in squad)
                {
                    _memory.HarassGroup.Add(unit.Id);
                    if (budget > 0 && Emit(AiIntent.AttackMove(unit.Id, goal)))
                    {
                        budget--;
                    }
                }
            }

            /// <summary>Rule HR run and end: ends the squad when it would lose or has lingered without seeing a worker; else sends members at the weakest visible enemy worker or back to the target.</summary>
            /// <param name="squad">The living members, ascending ID.</param>
            /// <param name="cooldown">Cooldown after the squad ends. Unit: ticks.</param>
            private void RunSquad(List<AiUnitView> squad, int cooldown)
            {
                // Commands still allowed this think. Unit: commands.
                int budget = _profile.HarassMaxCommandsPerThink;
                // Where the squad is headed. Unit: tiles.
                FixVec2 goal = Standable(_memory.HarassTarget);
                // Squared gathering radius, defence radius and focus radius. Unit: squared tiles.
                Fix64 stageSquared = _profile.StageRadiusTiles * _profile.StageRadiusTiles;
                Fix64 defenceSquared = _profile.TargetDefenceRadiusTiles * _profile.TargetDefenceRadiusTiles;
                Fix64 focusSquared = _profile.FocusRadiusTiles * _profile.FocusRadiusTiles;
                if (_memory.HarassArrivedTick < 0)
                {
                    foreach (AiUnitView unit in squad)
                    {
                        if ((unit.Position - goal).LengthSquared() <= stageSquared)
                        {
                            _memory.HarassArrivedTick = _view.Tick;
                            _memory.HarassLastWorkerTick = _view.Tick;
                            break;
                        }
                    }
                }
                // Visible enemy workers, ascending ID.
                List<AiUnitView> workers = new List<AiUnitView>();
                foreach (AiUnitView foe in _view.VisibleEnemies)
                {
                    if (foe.IsWorker)
                    {
                        workers.Add(foe);
                        if (_memory.HarassArrivedTick >= 0 && (foe.Position - _memory.HarassTarget).LengthSquared() <= defenceSquared)
                        {
                            _memory.HarassLastWorkerTick = _view.Tick;
                        }
                    }
                }
                // True when the squad has stood at the target for harass_linger_seconds without seeing a worker there.
                bool lingered = _memory.HarassArrivedTick >= 0 && _view.Tick - _memory.HarassLastWorkerTick >= _view.SecondsToTicks(_profile.HarassLingerSeconds);
                // True when the squad has been out for harass_max_seconds (an unreachable target must not hold it forever).
                bool expired = _view.Tick >= _memory.HarassStartTick + _view.SecondsToTicks(_profile.HarassMaxSeconds);
                if (PredictedRetreat(squad) || lingered || expired)
                {
                    // Home is where the squad is called to. Unit: tiles.
                    FixVec2 home = Standable(Home());
                    foreach (AiUnitView unit in squad)
                    {
                        if (budget > 0 && Emit(AiIntent.Move(unit.Id, home)))
                        {
                            budget--;
                        }
                    }
                    _memory.HarassGroup.Clear();
                    _memory.NextHarassTick = _view.Tick + cooldown;
                    return;
                }
                foreach (AiUnitView unit in squad)
                {
                    if (budget <= 0)
                    {
                        break;
                    }
                    // The unit's weapon (squad members are armed).
                    WeaponDefinition weapon = unit.Definition.Weapon!;
                    // The weakest visible worker in reach (ties nearer, then lower ID), the worker the unit already attacks if that is still in reach, and their distances.
                    AiUnitView? pick = null;
                    Fix64 pickSquared = Fix64.Zero;
                    bool keep = false;
                    foreach (AiUnitView worker in workers)
                    {
                        // Squared distance to this worker. Unit: squared tiles. Range: 0 upward.
                        Fix64 squared = (worker.Position - unit.Position).LengthSquared();
                        if (squared > focusSquared || !weapon.CanTarget(worker.Layer))
                        {
                            continue;
                        }
                        if (unit.IsAttackTargetForced && unit.AttackTarget == worker.Id)
                        {
                            keep = true;
                        }
                        if (pick == null || worker.Health < pick.Health || (worker.Health == pick.Health && squared < pickSquared))
                        {
                            pick = worker;
                            pickSquared = squared;
                        }
                    }
                    if (keep)
                    {
                        continue;
                    }
                    if (pick != null)
                    {
                        if (Emit(AiIntent.AttackUnit(unit.Id, pick.Id)))
                        {
                            budget--;
                        }
                    }
                    else if (unit.IsAttackTargetForced || (!unit.IsAttackMoving && !unit.HasMoveTarget && (unit.Position - goal).LengthSquared() > AtRallySquared))
                    {
                        if (Emit(AiIntent.AttackMove(unit.Id, goal)))
                        {
                            budget--;
                        }
                    }
                }
            }

            // ------------------------------------------------------------------ D.5 measured defence

            /// <summary>
            /// D.5: answers a threat inside the base with as few units as the predictor needs (own complete static defences near the threat count
            /// as fixed defenders). Chosen units become the defend group and get the v2 Defend orders; if the chosen set holds an attack-group
            /// member and the supply asked of attack-group members reaches defend_recall_supply, the stance becomes Defend (a recall: every army unit defends, as in v2). Without a threat the group holds until
            /// defend_hold_seconds after the last one. Returns true when the stance is Defend and the army has been ordered (Army then returns).
            /// </summary>
            /// <param name="armyAll">Every own army unit that is not an active scout.</param>
            private bool MeasuredDefence(List<AiUnitView> armyAll)
            {
                if (_threatCount == 0 && _latestHit == null)
                {
                    if (_memory.DefendGroup.Count > 0 && _view.Tick >= _memory.LastThreatTick + _view.SecondsToTicks(_profile.DefendHoldSeconds))
                    {
                        _memory.DefendGroup.Clear();
                    }
                    return false;
                }
                _memory.LastThreatTick = _view.Tick;
                // Where the defenders go: the mean threat position, else the latest hit.
                FixVec2 centre = _threatCount > 0 ? new FixVec2(_threatSumX / _threatCount, _threatSumY / _threatCount) : _latestHit!.Position;
                // Candidates: not fallen back, not harassing, not ordered by unit control this think; nearest to the threat first.
                List<AiUnitView> candidates = new List<AiUnitView>();
                foreach (AiUnitView unit in armyAll)
                {
                    if (!_memory.FallenBack.Contains(unit.Id) && !_memory.HarassGroup.Contains(unit.Id) && !Controlled(unit))
                    {
                        candidates.Add(unit);
                    }
                }
                // True while an attack runs, so units of its group are the last choice (D.5, review item 3). Unit: boolean.
                bool attacking = _memory.Stance == AiStance.Attack;
                candidates.Sort((a, b) =>
                {
                    // Whether each unit belongs to the attack group (those sort last). Unit: boolean.
                    bool ga = attacking && _memory.AttackGroup.Contains(a.Id);
                    bool gb = attacking && _memory.AttackGroup.Contains(b.Id);
                    if (ga != gb)
                    {
                        return ga ? 1 : -1;
                    }
                    // Squared distances of the two units from the threat. Unit: squared tiles. Range: 0 upward.
                    Fix64 da = (a.Position - centre).LengthSquared();
                    Fix64 db = (b.Position - centre).LengthSquared();
                    return da < db ? -1 : (da > db ? 1 : a.Id.CompareTo(b.Id));
                });
                // Every army unit that may defend when the stance is Defend (the whole list, nearest first).
                List<AiUnitView> everyone = new List<AiUnitView>(candidates);
                if (candidates.Count > _profile.DefendMaxCandidates)
                {
                    candidates.RemoveRange(_profile.DefendMaxCandidates, candidates.Count - _profile.DefendMaxCandidates);
                }
                // The units chosen to defend.
                List<AiUnitView> chosen = new List<AiUnitView>();
                if (_threatCount > 0)
                {
                    // Own complete static defences near the threat, the fixed side.
                    List<AiUnitView> fixedUnits = new List<AiUnitView>();
                    // Squared engagement radius. Unit: squared tiles.
                    Fix64 engagementSquared = _profile.EngagementRadiusTiles * _profile.EngagementRadiusTiles;
                    foreach (AiUnitView own in _view.OwnUnits)
                    {
                        if (own.IsStructure && !own.IsUnderConstruction && own.Definition.Economy.IsStaticDefence && own.Definition.Weapon != null
                            && (own.Position - centre).LengthSquared() <= engagementSquared)
                        {
                            fixedUnits.Add(own);
                        }
                    }
                    // The threat as predictor entries (real health). Unit: units and health points.
                    List<FightEntry> threat = FightPredictor.Group(_threats);
                    // The fixed defences as predictor entries (real health). Unit: units and health points.
                    List<FightEntry> fixedEntries = FightPredictor.Group(fixedUnits);
                    // True once the chosen units and the fixed defences reach defend_min_advantage.
                    bool enough = PredictFight(fixedEntries, threat, false).Advantage >= _profile.DefendMinAdvantage;
                    // Candidate number. Range: 0 to the candidate count.
                    for (int i = 0; i < candidates.Count && !enough; i++)
                    {
                        chosen.Add(candidates[i]);
                        enough = PredictFight(AiFight.Merge(fixedEntries, FightPredictor.Group(chosen)), threat, false).Advantage >= _profile.DefendMinAdvantage;
                    }
                }
                else
                {
                    // Supply of the nearest units chosen so far. Unit: supply.
                    int supply = 0;
                    for (int i = 0; i < candidates.Count && supply < _profile.DefendBlindSupply; i++)
                    {
                        chosen.Add(candidates[i]);
                        supply += _view.SupplyOf(candidates[i].Definition);
                    }
                }
                if (attacking)
                {
                    // Supply of attack-group units the predictor asked for. Unit: supply. Range: 0 upward.
                    int groupSupply = 0;
                    foreach (AiUnitView unit in chosen)
                    {
                        groupSupply += _memory.AttackGroup.Contains(unit.Id) ? _view.SupplyOf(unit.Definition) : 0;
                    }
                    if (groupSupply >= _profile.DefendRecallSupply)
                    {
                        // The defence needs enough of the attack group: recall it (the stance becomes Defend as in v2).
                        _memory.Stance = AiStance.Defend;
                        _memory.HasAttackTarget = false;
                    }
                    else
                    {
                        // Too little is asked of the group to end the attack: the others defend and the group's units keep attacking.
                        chosen.RemoveAll(unit => _memory.AttackGroup.Contains(unit.Id));
                    }
                }
                // The units that get Defend orders: everyone in Defend, else the chosen.
                List<AiUnitView> defenders = _memory.Stance == AiStance.Defend ? everyone : chosen;
                _memory.DefendGroup.Clear();
                // Defence goal, moved off a blocked tile. Unit: tiles.
                FixVec2 goal = Standable(centre);
                foreach (AiUnitView unit in defenders)
                {
                    _memory.DefendGroup.Add(unit.Id);
                    if (Controlled(unit) || Focus(unit))
                    {
                        continue;
                    }
                    if (unit.IsAttackTargetForced || !unit.IsAttackMoving || (unit.AttackMoveGoal - goal).LengthSquared() > SameGoalSquared)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, goal));
                    }
                }
                return _memory.Stance == AiStance.Defend;
            }

            // ------------------------------------------------------------------ D.6 unit control

            /// <summary>Visible armed non-worker enemies (structures included), the index for the fall-back test.</summary>
            private AiProximity ArmedFoeIndex()
            {
                if (_armedFoeIndex == null)
                {
                    // The enemies indexed.
                    List<AiUnitView> armed = new List<AiUnitView>();
                    foreach (AiUnitView foe in _view.VisibleEnemies)
                    {
                        if (foe.Definition.Weapon != null && !foe.IsWorker)
                        {
                            armed.Add(foe);
                        }
                    }
                    _armedFoeIndex = new AiProximity(armed, _profile.FallbackDangerTiles);
                }
                return _armedFoeIndex;
            }

            /// <summary>Visible armed enemy army units, the index for the keep-distance test (the range gap of the rule keeps out enemies that out-range the unit).</summary>
            private AiProximity KiteFoeIndex()
            {
                if (_kiteFoeIndex == null)
                {
                    // The enemies indexed.
                    List<AiUnitView> armed = new List<AiUnitView>();
                    foreach (AiUnitView foe in _view.VisibleEnemies)
                    {
                        if (foe.IsArmy && foe.Definition.Weapon != null)
                        {
                            armed.Add(foe);
                        }
                    }
                    _kiteFoeIndex = new AiProximity(armed, _profile.KiteTriggerTiles);
                }
                return _kiteFoeIndex;
            }

            /// <summary>D.6.1 test: true when a visible armed enemy that can target the unit's layer stands within fallback_danger_tiles of it.</summary>
            private bool DangerNear(AiUnitView unit)
            {
                // Enemies in range.
                List<AiUnitView> near = new List<AiUnitView>();
                ArmedFoeIndex().Near(unit.Position, _profile.FallbackDangerTiles, near);
                foreach (AiUnitView foe in near)
                {
                    if (foe.Definition.Weapon!.CanTarget(unit.Layer))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// D.6.2: when the ranged <paramref name="unit"/> has a visible armed enemy army unit that can hit it within kite_trigger_tiles (the spec says
            /// melee, but its test D-9 also kites from a range-2 Shellback, so the range gap alone separates short-range enemies), and
            /// its range exceeds the enemy's by kite_min_range_gap_tiles and its speed is at least kite_speed_ratio_min times the enemy's, orders
            /// a move kite_step_tiles directly away from the nearest such enemy. Returns true when the order was given.
            /// </summary>
            private bool TryKite(AiUnitView unit)
            {
                // The unit's weapon, or null.
                WeaponDefinition? weapon = unit.Definition.Weapon;
                if (weapon == null || weapon.IsMelee)
                {
                    return false;
                }
                // Armed enemy army units in trigger range.
                List<AiUnitView> near = new List<AiUnitView>();
                KiteFoeIndex().Near(unit.Position, _profile.KiteTriggerTiles, near);
                // The nearest enemy that can hit the unit (ties to the lower ID), and its squared distance. Unit: squared tiles.
                AiUnitView? nearest = null;
                Fix64 nearestSquared = Fix64.Zero;
                foreach (AiUnitView foe in near)
                {
                    // Squared distance to this enemy. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (foe.Position - unit.Position).LengthSquared();
                    if (foe.Definition.Weapon!.CanTarget(unit.Layer) && (nearest == null || squared < nearestSquared))
                    {
                        nearest = foe;
                        nearestSquared = squared;
                    }
                }
                if (nearest == null || nearest.Position == unit.Position)
                {
                    return false;
                }
                if (weapon.RangeTiles < nearest.Definition.Weapon!.RangeTiles + _profile.KiteMinRangeGapTiles
                    || unit.Definition.SpeedTilesPerSecond < _profile.KiteSpeedRatioMin * nearest.Definition.SpeedTilesPerSecond)
                {
                    return false;
                }
                // The vector from the enemy to the unit and its length (above 0: the positions differ). Unit: tiles.
                FixVec2 offset = unit.Position - nearest.Position;
                Fix64 distance = offset.Length();
                if (distance <= Fix64.Zero)
                {
                    // Stacked or almost stacked units (the length rounds to 0): there is no direction to step away in.
                    return false;
                }
                // The point exactly kite_step_tiles from the unit directly away from the enemy, kept 0.5 tiles inside the map edge (D.6.2; the spec's
                // MoveTowards(2u - e, step) would stop at the mirror point when the enemy is closer than the step). Unit: tiles.
                FixVec2 away = unit.Position + offset * (_profile.KiteStepTiles / distance);
                Fix64 awayX = Fix64.Clamp(away.X, Fix64.Half, Fix64.FromInt(_view.Width) - Fix64.Half);
                Fix64 awayY = Fix64.Clamp(away.Y, Fix64.Half, Fix64.FromInt(_view.Height) - Fix64.Half);
                return Emit(AiIntent.Move(unit.Id, Standable(new FixVec2(awayX, awayY))));
            }

            /// <summary>
            /// D.6.1 and D.6.2: hurt units near an armed enemy fall back home (never under all-in or rule K), and ranged units step back from
            /// nearby armed enemy army units with a range gap (the spec says melee). Units ordered here are in <see cref="_controlled"/> and get no stance order this think.
            /// </summary>
            /// <param name="armyAll">Every own army unit that is not an active scout.</param>
            private void UnitControl(List<AiUnitView> armyAll)
            {
                if (!_profile.UseUnitControl)
                {
                    return;
                }
                // Orders still allowed this think. Unit: commands.
                int fallbackBudget = _profile.FallbackMaxCommandsPerThink;
                int kiteBudget = _profile.KiteMaxCommandsPerThink;
                // True when units may fall back (not under all-in or rule K).
                bool mayFallBack = !AllIn && _kill == null;
                foreach (AiUnitView unit in armyAll)
                {
                    if (_memory.FallenBack.Contains(unit.Id) || _memory.HarassGroup.Contains(unit.Id))
                    {
                        continue;
                    }
                    if (mayFallBack && fallbackBudget > 0 && unit.Health <= _profile.FallbackHealthFraction * unit.Definition.MaxHealth && DangerNear(unit))
                    {
                        if (Emit(AiIntent.Move(unit.Id, Standable(Home()))))
                        {
                            fallbackBudget--;
                            _memory.AttackGroup.Remove(unit.Id);
                            _memory.DefendGroup.Remove(unit.Id);
                            _memory.FallenBack.Add(unit.Id);
                            _memory.FallenBackSince[unit.Id] = _view.Tick;
                            _controlled.Add(unit.Id);
                        }
                        continue;
                    }
                    if (kiteBudget > 0 && TryKite(unit))
                    {
                        kiteBudget--;
                        _controlled.Add(unit.Id);
                    }
                }
            }

            /// <summary>
            /// The time the own army needs to kill <paramref name="foe"/>: its health divided by the damage per second of every own army unit
            /// within focus_radius_tiles of it (D.6.3). Cached per think; -1 when no own unit can shoot it. Unit: seconds. Range: -1, or 0 upward.
            /// </summary>
            private Fix64 TimeToKill(AiUnitView foe)
            {
                if (_timeToKill.TryGetValue(foe.Id, out Fix64 cached))
                {
                    return cached;
                }
                _ownIndex ??= new AiProximity(_armyAll, _profile.FocusRadiusTiles);
                // Own units in reach of the foe.
                List<AiUnitView> shooters = new List<AiUnitView>();
                _ownIndex.Near(foe.Position, _profile.FocusRadiusTiles, shooters);
                // Their summed damage per second against the foe. Unit: health points per second. Range: 0 upward.
                Fix64 dps = Fix64.Zero;
                foreach (AiUnitView shooter in shooters)
                {
                    dps += FightPredictor.Dps(shooter.Definition, foe.Definition, false, _view.Content.Combat);
                }
                // The time to kill, or -1 with no shooter. Unit: seconds.
                Fix64 time = dps <= Fix64.Zero ? -Fix64.One : foe.Health / dps;
                _timeToKill.Add(foe.Id, time);
                return time;
            }

            /// <summary>
            /// D.6.3 (replaces <see cref="Focus"/> when use_focus_fire is on): the unit attacks, among the visible enemy army units it can
            /// hit within focus_radius_tiles, the one that dies fastest (ties nearer, then lower ID). It keeps its current forced target while
            /// that target's time to kill is at most focus_switch_ratio times the best. At most focus_max_commands_per_think new orders per
            /// think; a unit beyond the budget gets its stance order. Returns true when the unit is focused (ordered or already on target).
            /// </summary>
            private bool FocusFastestKill(AiUnitView unit)
            {
                // The unit's weapon, or null for an unarmed unit.
                WeaponDefinition? weapon = unit.Definition.Weapon;
                if (weapon == null || _profile.FocusRadiusTiles <= Fix64.Zero)
                {
                    return false;
                }
                // True when the unit already holds a forced target.
                bool holds = unit.IsAttackTargetForced;
                if (!holds && _focusCommands >= _profile.FocusMaxCommandsPerThink)
                {
                    return false;
                }
                if (_foeIndex == null)
                {
                    // Visible enemy army units, ascending ID.
                    List<AiUnitView> foes = new List<AiUnitView>();
                    foreach (AiUnitView foe in _view.VisibleEnemies)
                    {
                        if (foe.IsArmy)
                        {
                            foes.Add(foe);
                        }
                    }
                    _foeIndex = new AiProximity(foes, _profile.FocusRadiusTiles);
                }
                // Enemies within reach of this unit.
                List<AiUnitView> near = new List<AiUnitView>();
                _foeIndex.Near(unit.Position, _profile.FocusRadiusTiles, near);
                // The best target so far, its time to kill and squared distance; the unit's current target and its time to kill.
                AiUnitView? best = null;
                Fix64 bestTime = Fix64.Zero;
                Fix64 bestSquared = Fix64.Zero;
                bool hasCurrent = false;
                Fix64 currentTime = Fix64.Zero;
                foreach (AiUnitView foe in near)
                {
                    if (!weapon.CanTarget(foe.Layer))
                    {
                        continue;
                    }
                    // This enemy's time to kill, and squared distance from the unit. Unit: seconds; squared tiles.
                    Fix64 time = TimeToKill(foe);
                    Fix64 squared = (foe.Position - unit.Position).LengthSquared();
                    if (time < Fix64.Zero)
                    {
                        continue;
                    }
                    if (holds && unit.AttackTarget == foe.Id)
                    {
                        hasCurrent = true;
                        currentTime = time;
                    }
                    if (best == null || time < bestTime || (time == bestTime && squared < bestSquared))
                    {
                        best = foe;
                        bestTime = time;
                        bestSquared = squared;
                    }
                }
                if (best == null)
                {
                    return false;
                }
                if (hasCurrent && currentTime <= _profile.FocusSwitchRatio * bestTime)
                {
                    return true;
                }
                if (_focusCommands >= _profile.FocusMaxCommandsPerThink)
                {
                    return false;
                }
                if (Emit(AiIntent.AttackUnit(unit.Id, best.Id)))
                {
                    _focusCommands++;
                }
                return true;
            }
        }
    }
}
