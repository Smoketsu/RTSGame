// rts/src/Ai/AiBrainPlacement.cs
// Package AI-P of rts/docs/specs/field_saturation.md as the third part of the brain's per-think working state (AiBrain.Turn is partial):
//   P1 (use_forward_producers) the extra producer (rule X) and the tech producer (rule T) are built on the ring around the own base with the
//      fewest producers within base_radius_tiles of it (a base is the Rooted HQ, or a complete own depot with a live field in reach; ties to the
//      HQ, then the lower ID), falling back to the HQ ring when that base has no good site. Build-order steps, supply buildings and depots keep
//      their sites. Forward or proxy production near the enemy is not part of this package.
//   P2 (use_threat_defence) rule SD step SD-4: the exposed base is the base nearest the last seen enemy army (else nearest a known enemy
//      structure or uncleared enemy spawn); when the enemy model holds at least threat_defence_min_enemy_supply and the fight predictor gives the
//      enemy (side A) at least threat_defence_min_enemy_advantage against the own army units, static defences and Rooted HQ within
//      defend_radius_tiles of that base, one static defence is built there (cap, one at a time and value share as rule SD). The per-base cap
//      is min(static_defence_max_per_base, the Sim's static_defences_per_base_max). Hysteresis: once SD-4 fired it stays armed while its two
//      numbers stay at threat_defence_release_fraction of their thresholds, and the exposed base changes only to a base that much closer.
// Part of Rts.Ai. Fix64 and integers only, ascending-ID iteration with ties to the lower ID, no random draw (the exposed-base memory is not hashed).
// With both switches off none of this runs and the AI plays as before.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    public static partial class AiBrain
    {
        /// <summary>
        /// The per-base static defence cap (AI-P P2.1): min(profile, Sim) with <paramref name="threatDefence"/> on, the profile value alone otherwise
        /// (so every old profile keeps its cap whatever the Sim allows).
        /// </summary>
        /// <param name="profileMax">static_defence_max_per_base. Unit: structures. Range: 0 to 6.</param>
        /// <param name="simMax">The Sim's static_defences_per_base_max. Unit: structures. Range: 0 to 20.</param>
        /// <param name="threatDefence">use_threat_defence.</param>
        /// <returns>The cap. Unit: structures. Range: 0 to 6.</returns>
        internal static int EffectiveDefenceCap(int profileMax, int simMax, bool threatDefence)
        {
            return threatDefence ? Math.Min(profileMax, simMax) : profileMax;
        }

        /// <summary>
        /// The SD-4 trigger with hysteresis: the enemy model must hold supply of at least threat_defence_min_enemy_supply and the predicted enemy
        /// advantage must be at least threat_defence_min_enemy_advantage; both thresholds are multiplied by threat_defence_release_fraction while
        /// the rule is already <paramref name="armed"/>. An empty enemy model (supply 0) never triggers.
        /// </summary>
        /// <param name="profile">The profile (thresholds and release fraction).</param>
        /// <param name="armed">True when SD-4 fired earlier and has not been released.</param>
        /// <param name="enemySupply">Supply of the enemy model's army. Unit: supply. Range: 0 upward.</param>
        /// <param name="advantage">Predicted enemy advantage (predictor ratio, enemy as side A). Unit: ratio. Range: 0 to the predictor cap.</param>
        /// <returns>True when SD-4 may build.</returns>
        internal static bool ThreatDefenceHolds(AiProfile profile, bool armed, Fix64 enemySupply, Fix64 advantage)
        {
            // Share of the thresholds required now; 1 while not armed. Unit: fraction. Range: 0 to 1.
            Fix64 share = armed ? profile.ThreatDefenceReleaseFraction : Fix64.One;
            return enemySupply > Fix64.Zero && enemySupply >= Fix64.FromInt(profile.ThreatDefenceMinEnemySupply) * share
                && advantage >= profile.ThreatDefenceMinEnemyAdvantage * share;
        }

        /// <summary>What a think's placement rules would choose (test hook, see <see cref="ProbePlacement"/>).</summary>
        internal sealed class PlacementProbe
        {
            /// <summary>Site rule X or rule T would use now (the producer search, else the HQ ring), or null when no tile qualifies. Unit: tile column and row.</summary>
            public (int X, int Y)? ProducerSite { get; }

            /// <summary>Site the HQ-ring search finds (today's rule), or null. Unit: tile column and row.</summary>
            public (int X, int Y)? HqRingSite { get; }

            /// <summary>ID of the exposed base (SD-4), or <see cref="EntityId.None"/> when nothing is known about the enemy. Range: an own drop-off ID or None.</summary>
            public EntityId ExposedBase { get; }

            /// <summary>Supply of the enemy model's army. Unit: supply. Range: 0 upward.</summary>
            public Fix64 EnemySupply { get; }

            /// <summary>Predicted enemy advantage at the exposed base. Unit: ratio. Range: 0 to the predictor cap.</summary>
            public Fix64 Advantage { get; }

            /// <summary>Bundles the values.</summary>
            public PlacementProbe((int X, int Y)? producerSite, (int X, int Y)? hqRingSite, EntityId exposedBase, Fix64 enemySupply, Fix64 advantage)
            {
                ProducerSite = producerSite;
                HqRingSite = hqRingSite;
                ExposedBase = exposedBase;
                EnemySupply = enemySupply;
                Advantage = advantage;
            }
        }

        /// <summary>
        /// Test hook: the producer site, the HQ-ring site, the exposed base and the SD-4 numbers one think would see, without deciding anything and without
        /// changing the exposed-base memory. Updates the intel like <see cref="Decide"/>. Returns null when the player has no HQ.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The AI profile.</param>
        /// <param name="plan">The profile's plan for the player's faction.</param>
        /// <param name="memory">The controller's memory; the intel part is updated.</param>
        internal static PlacementProbe? ProbePlacement(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory)
        {
            // The own HQ (lowest ID), or null when the player has none.
            AiUnitView? hq = FindHq(view);
            UpdateIntel(view, profile, memory, hq);
            if (hq == null)
            {
                return null;
            }
            if (memory.OwnSpawnIndex < 0)
            {
                memory.OwnSpawnIndex = NearestSpawn(view, hq.Position);
            }
            // This think's working state; nothing is decided.
            Turn turn = new Turn(view, profile, plan, memory, new DeterministicRandom(1), hq, new List<AiIntent>());
            return turn.PlacementProbeNow();
        }

        private sealed partial class Turn
        {
            /// <summary>True once a static defence build intent was emitted this think (rule SD steps 2 and 3); SD-4 then waits for the next think.</summary>
            private bool _defenceOrdered;

            /// <summary>The per-base static defence cap in force (AI-P P2.1). Unit: structures. Range: 0 to 6.</summary>
            private int DefenceCap => EffectiveDefenceCap(_profile.StaticDefenceMaxPerBase, _view.StaticDefencesPerBaseMax, _profile.UseThreatDefence);

            /// <summary>
            /// The own bases (AI-P P1): the Rooted complete HQs, then the complete own depots with a live field in reach, each group in ascending ID,
            /// so a strict "fewer" or "nearer" comparison over the list resolves ties to the HQ and then the lower ID.
            /// </summary>
            private List<AiUnitView> Bases()
            {
                // The Rooted HQs found, and the depots found.
                List<AiUnitView> hqs = new List<AiUnitView>();
                List<AiUnitView> depots = new List<AiUnitView>();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsUnderConstruction)
                    {
                        continue;
                    }
                    if (unit.IsHq && unit.HqState == HqState.Rooted)
                    {
                        hqs.Add(unit);
                    }
                    else if (!unit.IsHq && unit.IsStructure && unit.Definition.Economy.IsDropOff && AnyLiveFieldWithin(unit.Position))
                    {
                        depots.Add(unit);
                    }
                }
                hqs.AddRange(depots);
                return hqs;
            }

            /// <summary>Own structures that train a composition unit (complete, under construction or pending order) within base_radius_tiles of <paramref name="baseUnit"/>. Unit: structures. Range: 0 upward.</summary>
            private int ProducersAt(AiUnitView baseUnit)
            {
                // Squared base radius. Unit: squared tiles. Range: 4 to 4096.
                Fix64 radiusSquared = _view.BaseRadiusTiles * _view.BaseRadiusTiles;
                // Producers found. Unit: structures.
                int count = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && TrainsCompositionUnit(unit.Definition) && (unit.Position - baseUnit.Position).LengthSquared() <= radiusSquared)
                    {
                        count++;
                    }
                }
                return count;
            }

            /// <summary>
            /// AI-P P1: a site on the ring around the base with the fewest producers (ties to the HQ, then the lower depot ID). False when there is no
            /// base or that base has no good site; the caller then uses the HQ ring.
            /// </summary>
            /// <param name="siteX">Receives the site tile column (0 when none). Unit: tiles.</param>
            /// <param name="siteY">Receives the site tile row (0 when none). Unit: tiles.</param>
            private bool TryFindProducerSite(out int siteX, out int siteY)
            {
                // Base with the fewest producers so far, and its count. Unit: structures.
                AiUnitView? best = null;
                int bestCount = 0;
                foreach (AiUnitView candidate in Bases())
                {
                    // Producers at this candidate.
                    int count = ProducersAt(candidate);
                    if (best == null || count < bestCount)
                    {
                        best = candidate;
                        bestCount = count;
                    }
                }
                siteX = 0;
                siteY = 0;
                return best != null && TryFindSiteAround(best.TileX, best.TileY, out siteX, out siteY);
            }

            /// <summary>
            /// The points the enemy is believed to be at (AI-P P2.2): the last seen enemy army position while it is within enemy_army_memory_seconds,
            /// else every known enemy structure (ascending ID) and every uncleared enemy spawn (ascending index). Empty when nothing is known.
            /// </summary>
            private List<FixVec2> EnemyPoints()
            {
                // The points found.
                List<FixVec2> points = new List<FixVec2>();
                // Oldest tick at which a seen enemy army still counts. Unit: ticks.
                int armyFreshAfter = _view.Tick - _view.SecondsToTicks(_profile.EnemyArmyMemorySeconds);
                if (_memory.HasLastSeenEnemy && _memory.LastSeenEnemyTick >= armyFreshAfter)
                {
                    points.Add(_memory.LastSeenEnemyPosition);
                    return points;
                }
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    points.Add(known.Position);
                }
                // Spawn index. Range: 0 to spawn count - 1.
                for (int i = 0; i < _view.Spawns.Count; i++)
                {
                    if (i != _memory.OwnSpawnIndex && !_memory.ClearedSpawns.Contains(i))
                    {
                        points.Add(_view.Spawns[i]);
                    }
                }
                return points;
            }

            /// <summary>Squared distance from <paramref name="from"/> to the nearest of <paramref name="points"/>; <paramref name="nearest"/> receives that point (the first on a tie). Unit: squared tiles. Range: 0 upward.</summary>
            private static Fix64 NearestPoint(FixVec2 from, List<FixVec2> points, out FixVec2 nearest)
            {
                nearest = points[0];
                // Squared distance to the nearest point so far. Unit: squared tiles.
                Fix64 bestSquared = (points[0] - from).LengthSquared();
                // Point index. Range: 1 to points.Count - 1.
                for (int i = 1; i < points.Count; i++)
                {
                    // Squared distance to this point. Unit: squared tiles.
                    Fix64 squared = (points[i] - from).LengthSquared();
                    if (squared < bestSquared)
                    {
                        bestSquared = squared;
                        nearest = points[i];
                    }
                }
                return bestSquared;
            }

            /// <summary>
            /// The exposed base (AI-P P2.2): the base nearest an enemy point (ties to the HQ, then the lower ID). With hysteresis the previous exposed
            /// base is kept unless the nearest base is closer to the enemy by the factor threat_defence_release_fraction (distance below that share
            /// of the previous base's). Null when there is no base or nothing is known about the enemy; <paramref name="toward"/> then is zero.
            /// </summary>
            /// <param name="toward">Receives the enemy point nearest the exposed base. Unit: tiles.</param>
            private AiUnitView? FindExposedBase(out FixVec2 toward)
            {
                toward = FixVec2.Zero;
                // Bases and enemy points.
                List<AiUnitView> bases = Bases();
                List<FixVec2> points = EnemyPoints();
                if (bases.Count == 0 || points.Count == 0)
                {
                    return null;
                }
                // Nearest base so far, its squared distance to the enemy and the enemy point. Unit: squared tiles.
                AiUnitView? best = null;
                Fix64 bestSquared = Fix64.Zero;
                // The previous exposed base when it still is a base, and its squared distance.
                AiUnitView? previous = null;
                Fix64 previousSquared = Fix64.Zero;
                foreach (AiUnitView candidate in bases)
                {
                    // Squared distance of this candidate to the nearest enemy point.
                    Fix64 squared = NearestPoint(candidate.Position, points, out _);
                    if (best == null || squared < bestSquared)
                    {
                        best = candidate;
                        bestSquared = squared;
                    }
                    if (candidate.Id == _memory.ExposedBase)
                    {
                        previous = candidate;
                        previousSquared = squared;
                    }
                }
                // The base chosen: the previous one unless the best is closer by the release fraction (squared distances compare with the squared fraction).
                AiUnitView chosen = best!;
                if (previous != null && best!.Id != previous.Id
                    && !(bestSquared < previousSquared * _profile.ThreatDefenceReleaseFraction * _profile.ThreatDefenceReleaseFraction))
                {
                    chosen = previous;
                }
                NearestPoint(chosen.Position, points, out toward);
                return chosen;
            }

            /// <summary>Supply of the enemy model's army: the sum of count x supply over the model's types. Unit: supply. Range: 0 upward.</summary>
            private Fix64 ModelSupply()
            {
                // Supply total so far.
                Fix64 total = Fix64.Zero;
                foreach (KeyValuePair<string, Fix64> pair in _model)
                {
                    if (_view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                    {
                        total += pair.Value * Fix64.FromInt(_view.SupplyOf(definition));
                    }
                }
                return total;
            }

            /// <summary>
            /// The predicted enemy advantage at <paramref name="baseUnit"/>: the predictor ratio with the enemy model as side A against the own complete
            /// army units and static defences with a weapon within defend_radius_tiles of the base, plus the HQ when the base is the Rooted HQ.
            /// Unit: ratio. Range: 0 to the predictor cap.
            /// </summary>
            private Fix64 EnemyAdvantageAt(AiUnitView baseUnit)
            {
                // Squared defend radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                // Own units that fight here.
                List<AiUnitView> own = new List<AiUnitView>();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsUnderConstruction || unit.Definition.Weapon == null)
                    {
                        continue;
                    }
                    // True for an army unit or a static defence, and for the base's own HQ.
                    bool fights = unit.IsArmy || (unit.IsStructure && unit.Definition.Economy.IsStaticDefence) || (unit.Id == baseUnit.Id && unit.IsHq);
                    if (fights && (unit.Position - baseUnit.Position).LengthSquared() <= radiusSquared)
                    {
                        own.Add(unit);
                    }
                }
                return PredictFight(_enemies, FightPredictor.Group(own), false).Advantage;
            }

            /// <summary>
            /// Rule SD step SD-4 (AI-P P2): when the enemy model and the predictor favour the enemy at the exposed base, builds one static defence there
            /// if the base is below the cap, no defence is under construction (the caller checked) and the value share allows it. Updates the hysteresis memory.
            /// </summary>
            private void ThreatDefence()
            {
                // The exposed base and the enemy point nearest it.
                AiUnitView? exposed = FindExposedBase(out FixVec2 toward);
                // Enemy model supply. Unit: supply.
                Fix64 supply = ModelSupply();
                if (exposed == null || supply <= Fix64.Zero)
                {
                    _memory.ThreatDefenceArmed = false;
                    _memory.ExposedBase = EntityId.None;
                    return;
                }
                _memory.ExposedBase = exposed.Id;
                // True when the trigger holds now (with the release band while armed).
                bool holds = ThreatDefenceHolds(_profile, _memory.ThreatDefenceArmed, supply, EnemyAdvantageAt(exposed));
                _memory.ThreatDefenceArmed = holds;
                if (holds && StaticDefencesNear(exposed.Position) < DefenceCap && WithinValueCap(_profile.ThreatDefenceMaxValueShare))
                {
                    BuildDefence(exposed.Position, toward, new List<MovementLayer>());
                }
            }

            /// <summary>Test hook state, see <see cref="ProbePlacement"/>.</summary>
            public PlacementProbe PlacementProbeNow()
            {
                // The producer site: the search, else the HQ ring.
                (int X, int Y)? producer = null;
                if ((_profile.UseForwardProducers && TryFindProducerSite(out int px, out int py)) || TryFindSite(out px, out py))
                {
                    producer = (px, py);
                }
                // The HQ-ring site.
                (int X, int Y)? ring = null;
                if (TryFindSite(out int rx, out int ry))
                {
                    ring = (rx, ry);
                }
                // Exposed base and the SD-4 numbers.
                AiUnitView? exposed = FindExposedBase(out FixVec2 ignored);
                return new PlacementProbe(producer, ring, exposed?.Id ?? EntityId.None, ModelSupply(), exposed == null ? Fix64.Zero : EnemyAdvantageAt(exposed));
            }
        }
    }
}
