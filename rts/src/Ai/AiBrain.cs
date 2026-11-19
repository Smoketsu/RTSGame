// rts/src/Ai/AiBrain.cs
// The skirmish AI's decision rules (spec section 4): a pure function of the world view, the profile, the controller's memory
// and its own seeded generator, returning an ordered list of intents. Part of Rts.Ai. It never sees the Simulation, so it can
// only know what AiWorldView exposes (own units, public map data and the team's vision).
//
// Rules, in this fixed order each think (each appends intents; nothing more is appended once max_commands_per_think is reached):
//   0. Intel: remember visible enemy structures and HQs (with their health share), forget those whose last tile is visible and empty,
//      remember where enemy army units were last seen, mark enemy spawns seen empty, drop old damage reports, and update the known
//      amount left in every field (only fields whose tile the team sees; the rest keep the last known amount). Runs even without an
//      own HQ. Every economy rule reads "known remaining", never the live amount.
//   1. HQ (rule H of skirmish_ai_v2.md B.5): an Uprooted HQ that is not moving and stands within the root reach of a field with known
//      resources is rooted. A Rooted HQ whose home fields (every field within field_max_distance_tiles of it) hold relocate_when_home_
//      remaining_fraction or less of their start amount relocates (UprootHq, then MoveUnit to the stand-off point beside the chosen
//      field) when no cheaper option exists (live depots at max_live_depots, no depot building in the plan, or no income and not enough
//      common for a depot), no threat is seen and the cooldown has passed. A relocation re-targets when its field runs out, its
//      destination turns unsafe or relocate_timeout_seconds pass; rooting ends it (cooldown, statistics). An Uprooted HQ with no
//      relocation (for example after a script) starts one.
//   1a. HQ cluster relocation (field_saturation.md package AI-HQ rule HQ-C, only with use_cluster_relocation): the trigger above becomes: home
//      remaining <= relocate_when_home_remaining_fraction and the best candidate cluster (most live gather slots, then nearest, then lower field
//      index; common anchor field) has live slots >= relocate_gain_ratio x home's (live = known remaining >= expand_min_field_remaining within
//      field_max_distance_tiles of the stand-off point or the HQ). A field served by an own complete depot may be the target and then the
//      "cheaper option" test is skipped; never while the army stance is Defend; a home left this way is never a target again (latch).
//   1b. Cycle (planetary_cycles.md section 5; the spec numbers it rule 9): when the AI's team holds the Core, the next cycle is not
//      locked yet, no pick of the own team or a script is pending and the AI is its team's pick captain (lowest-ID player still in the
//      match, so two AI teammates never overwrite each other), pick the first cycle of the faction's cycle_preference that is
//      allowed next (the first allowed cycle when none of the preferences is).
//   2. Workers gather (rule G): whenever the player has a drop-off (a complete depot or the Rooted HQ). A field is SERVED when it has
//      known resources and lies within field_max_distance_tiles of a planned drop-off (a drop-off, or a depot under construction).
//      Each idle worker (no gather loop, no move or attack-move order), ascending ID, goes to the nearest served field below its cap
//      (workers_per_field for common, rare_workers_per_field for rare; else the nearest served field). With no served field (starved)
//      it goes to the first field in target order instead (long haul). A player with no drop-off whose HQ is relocating sends idle
//      workers after the HQ (MoveUnit). Then at most rebalance_workers_per_think of the highest-ID workers on an over-full served field
//      move to the nearest served field below its cap.
//   3. Structures: (a) supply: when cap - used <= supply_buffer, the cap is below the global cap and no supply-giving structure is
//      under construction, build supply_building; (b) build order: the first step with fewer structures (built or under
//      construction) than its count, once supply used >= at_supply_used (later steps wait). A due structure that cannot be paid
//      yet reserves its cost (rule R: only while the player has income, that is some worker is not idle; without income the bank is
//      spent on whatever it can buy), so worker training and production below leave that much in the bank. A placed structure is not
//      re-tried for build_retry_seconds. Site: the first tile on Chebyshev rings build_ring_min_tiles..build_ring_max_tiles around
//      the HQ tile (ring by ring, ascending tile index) whose 3x3 block is on the map, unblocked, not a ramp and on the HQ's
//      height layer, whose own tile is not occupied by a unit, and that is more than field_clearance_tiles from every field.
//      The builder is the own worker without a build order nearest the site (ties to the lower ID; a worker already picked in this think is
//      skipped). Own pending build orders (a worker walking to a site or placing) count as structures under construction at their site
//      tile, for the supply, build order, tech, static defence and depot rules; their tiles are never offered again. A site the team has
//      never seen (site_unexplored) is first scouted: the builder is sent to the site centre and the build waits for the next attempt.
//   3b. Tech on demand (rule T, skirmish_ai_v3.md C.5, only when use_tech_on_demand): the counter-mix unit with the largest weight that no
//      own structure trains gets its producer built (rule X's choice of structure) when its weight is at least tech_min_weight_share x
//      mix_resolution and no producer of a mix unit is under construction.
//   3c. Static defence (rule SD, C.6, only when use_static_defence): raids on an own drop-off are logged (enemy army within defend_radius_tiles
//      or a recent hit there; a new raid after raided_gap_seconds of quiet). A drop-off raided static_defence_after_raids times within
//      raided_window_seconds gets one static defence (ring sites, nearest the raiders); else a layer of the enemy model with
//      unanswered_layer_min_supply that no own unit, defence or affordable pool unit can hit gets one at the HQ. At most
//      static_defence_max_per_base per base, one under construction at a time.
//   4. Expand (rule E): when the plan has a drop_off_building, no threat is seen, live depots < max_live_depots and no depot is under
//      construction, and either the clock reached expand_at_seconds + depots placed x expand_interval_seconds or the served fields hold
//      less than expand_when_served_remaining_fraction of their start amount, build a depot at the best site beside the first field in
//      target order that is not served (safe fields first, nearest the HQ first; unsafe ones only while starved).
//   4a. Saturation economy (field_saturation.md package AI-E, only with the keys on; the old values reproduce rules G, E and W above):
//      use_saturation_workers: a field's cap is its gather slots + ceil(slots x round-trip walk ticks / gather ticks per trip)
//      + saturation_extra_workers_per_field (walk = distance to the nearest planned drop-off - 2 x interact range - its radius), and
//      the worker target floor is the first target-order field's cap; max_workers is only a ceiling. rare_cap_yields_to_saturation:
//      the rare soft cap closes rare fields only while a served common field has fewer workers than its cap. use_saturation_expansion:
//      rule E also fires when the workers (queued included) reach expand_saturation_fraction x the uncapped target. A seen threat blocks
//      a depot only from expand_block_threat_supply (0 = any threat blocks).
//   4a'. Placement and defence (field_saturation.md package AI-P, AiBrainPlacement.cs, only with the keys on). use_forward_producers: rule X
//      and rule T build the producer on the ring around the own base (Rooted HQ, or a complete depot with a live field in reach) with the
//      fewest producers within base_radius_tiles (ties to the HQ, then the lower ID); no good site there: the HQ ring. use_threat_defence:
//      rule SD step SD-4 after the raid and unanswered-layer steps builds one defence at the base nearest the last seen enemy army (else
//      nearest a known enemy structure or uncleared spawn) when the enemy model holds threat_defence_min_enemy_supply and the predictor gives
//      the enemy threat_defence_min_enemy_advantage against the own units, defences and Rooted HQ within defend_radius_tiles of it, within
//      threat_defence_max_value_share of the army value; the per-base cap is min(static_defence_max_per_base, the Sim's cap). Both toggles
//      have a release band (threat_defence_release_fraction). The old values reproduce the earlier rules exactly.
//   4a''. Harass speed (field_saturation.md package AI-H, AiIntel.MayHarass, only with harass_speed_ratio above 0 and use_target_choice): a
//      unit may join the harass squad when its speed is at least harass_speed_ratio x the chaser speed (count-weighted mean speed of the enemy
//      model's armed types that can hit its layer; the plain mean over the enemy factions' armed mobile definitions when the model has none;
//      it qualifies when nothing can hit its layer). It replaces the flyer exemption and harass_min_speed_tiles_per_second; 0 keeps them.
//   4b. Train workers at the Rooted HQ while workers (queued included) < min(max_workers, workers_per_field x served common fields
//      + rare_workers_per_field x served rare fields; at least workers_per_field while any field has resources), one at a time, if
//      affordable and supply is free. Workers ignore structure reservations: they are the income, and a reservation that starved them
//      could deadlock an AI whose workers were killed.
//   4c. Scout (skirmish_ai_v3.md B.2, only when use_scouting): from scout_first_seconds, every scout_interval_seconds one unit scouts a
//      route of waypoints (every enemy spawn, then the fields far from the own bases; public map data only). The scout is a cheap
//      own flyer in the Massing army (borrowed, released after its route), else the lowest-ID unit of the plan's scout_unit, which is
//      trained at a free producer when none exists (at most scout_max_losses times; the cost is reserved under rule R when it cannot
//      be paid). It moves (a plain move) to the next waypoint once the current waypoint's tile is visible, is never an army unit
//      (no stance orders, not in the attack group) and, when it is an own unit, goes home after the route and waits for the next round.
//   5. Production (skirmish_ai_v2.md C.2 rule P; replaces the per-producer loop): repeat: the candidates are the composition units
//      (weight above 0) that some complete producer with an empty queue, not used this think, lists in its trains and whose rare cost
//      the rare stock minus reservations can pay; pick the one with the largest deficit d_u = w_u * (N + 1) - c_u * W (integers; ties
//      to the ordinally lowest ID) and give it to the lowest-ID such producer. If affordable after reservations and supply is free:
//      train it, count it and repeat; otherwise reserve its cost and stop, so cheaper units never jump ahead of the most needed one.
//      (The old loop let every producer buy the best unit it could make: a Roost can only make Windrunners, so they were bought
//      even at a negative deficit and the Archer's Windspire never saw the money; test C1.)
//   5b. Extra producer (rule X): no threat, bank minus reservations >= extra_producer_bank_common, every producer busy or used this
//      think, none under construction and fewer than max_producers: build the structure that trains the composition unit with the
//      largest deficit (first in the build order, else the lowest ID of the faction), at the HQ ring sites.
//   6. Army (army = own mobile units that are not workers or HQs): Defend (attack-move to a threat inside the base: a visible
//      enemy army unit within defend_radius_tiles of the HQ or an own structure, or a recent hit on an own unit there; an attacking
//      army is recalled only when the visible threat supply reaches defend_recall_supply; after the last threat the stance stays
//      Defend, ordering nothing, for defend_hold_seconds and then returns to Massing WITHOUT a new threshold draw: rule D), Massing
//      (units wait at the HOME rally point, rule M: rally_distance_tiles from home toward the target; attack once army supply >= the
//      drawn threshold, the regroup timer has run out and the tide window allows, or at once for rule K below), Attack (rule G: the
//      units that were army units when the attack began are the attack group, and only they are ordered; later units rally at home.
//      The group attack-moves to the nearest known enemy HQ, else known structure, else unexplored enemy spawn, else last seen enemy,
//      first gathering at a staging point (the Core centre when rally_at_core, the map has a Core and the team does not hold it, else
//      stage_distance_tiles short of the target) until stage_gather_fraction of the group supply stands within stage_radius_tiles of it
//      or stage_timeout_seconds pass; retreat on losses
//      below retreat_army_fraction of the group's start supply or when the group's power falls below retreat_power_ratio x the power of
//      the visible armed enemies within engagement_radius_tiles of any group unit (power = supply x health / max health); siege: when an
//      enemy HQ is visible and no visible enemy army unit stands within engagement_radius_tiles of it, group units within that radius
//      of the HQ that can hit its layer are ordered to attack it (attack_unit, the only order that names an enemy unit, and only
//      one the team sees now), and the order is replaced by an attack-move as soon as defenders show up; focus fire: in Defend and
//      Attack, a unit with a visible enemy army unit it can hit within focus_radius_tiles attacks the nearest one (attack_unit),
//      keeping its current target while that stays valid, so fights are won army-first instead of on buildings), Retreat (plain move
//      to the HQ, regroup timer, back to Massing with a new threshold). The threshold is drawn on the first think and whenever an
//      attack ends (group empty, retreat, no target left), never on leaving Defend. Kill (rule K): a known enemy HQ last seen at or
//      below kill_hq_health_fraction of its health (and was last seen within kill_seen_within_seconds) is attacked by any army of kill_min_army_supply, ignoring the threshold, the tide
//      window and the regroup timer, without staging and without retreating. All in: with all_in_when_mined_out, once no
//      field on the map has known resources (rule A), the attack threshold drops to 1 supply, the tide window is ignored, the army never
//      retreats and every army unit joins the group each think (no reinforcements can come, so waiting only stalls the match).
// Package C (skirmish_ai_v3.md C.4 and C.7) replaces, while its switch is on: the plan's composition by the counter-mix weights (use_counter_mix:
// AiCounter.Mix from the enemy model and unit data; read by rules P, X and T) and rule E's clock gate and target order (use_rare_priority:
// when rare-cost units weigh at least rare_need_weight_share of the mix and no rare field is served, rare sites come first; since package C2 it no
// longer lifts the clock gate).
// Package C2 (skirmish_ai_v3.md C2, each part behind a profile key whose off value is package C): use_predictor_mix scores the counter-mix
// by the fight predictor (AiCounter.PredictorMix) with confidence from sighted units only; rule G (rare_bank_soft_cap > 0): rare fields have
// cap 0 while the rare bank is at or above the cap, so their workers move to the nearest common field and 4b counts them as 0; rule SD:
// raid_min_supply (a raid needs strength = supply of visible enemy army units near the drop-off + 1 for a recent hit) and
// static_defence_max_value_share (step 2 builds only while own defences plus the new one are worth at most that share of the army; step 3
// is never capped); rule E: use_early_expansion makes the clock gate early_expansion_seconds while no rare field is served.
// Package D (skirmish_ai_v3.md D, code in AiBrainFight.cs; each part behind its own switch, all off in every profile but aggressive_s3):
// use_predictor_attack replaces Massing's supply threshold by the fight predictor against the enemy model and the known static defences at the
// chosen target (the drawn threshold stays the fallback when nothing is known) and replaces the retreat power test by the predicted advantage of
// the attack group against the visible armed enemies near it; use_target_choice puts undefended enemy depots and producers before the v2 target
// order and adds rule HR (a small squad of fast units harasses the known enemy drop-off farthest from the enemy army); use_measured_defence
// replaces "every army unit defends" by the nearest units the predictor needs (own static defences count), recalling the attack group only when
// it is needed; use_unit_control adds fall back and keep distance, and use_focus_fire adds fastest-kill focus fire (replacing the nearest-enemy Focus). The scout, the
// harass squad, the defend group and fallen-back units get no stance orders. Spatial tests use AiProximity, so no loop runs over every unit pair: each spatial test looks at its own cells (bounded by the army size).
// Rule S now also treats a Defend that this think's threat would start as Defend (the scout is not taken on the first think of a Defend), and the
// unreachable-waypoint skip waits until the move order has taken effect (AiMemory.ScoutOrderEffectiveTick).
// Rule 0 also (skirmish_ai_v3.md B.3, only when use_enemy_model) updates the enemy model: the enemy army unit types seen with a count
// that holds for enemy_model_hold_seconds and then fades to enemy_model_floor_fraction, reduced by seen kills, plus the types a known
// enemy structure trains (AiIntel). Package C reads it (the counter-mix, rule SD) as does the harness response check; with every v3 switch
// off the AI plays exactly as v2 (test A-11). Army membership: the scout (rule 4c) is not an army unit (and not counted by rule P)
// while it is away on its route; an own scout resting between rounds is an army unit again and is re-taken when the next round is due.
// Deleted or replaced by skirmish_ai_v2.md package C: the Massing rally at the Core (rally_at_core now moves the staging point),
// attack reinforcement by idle army units, the per-producer production loop, and the threshold redraw on every Massing entry.
// Deleted or replaced by package B: the Rooted-HQ-only gather gate and the HQ-only "fields in range" list (now served fields),
// the worker target min(max_workers, workers_per_field x fields in range), the all-in test on fields near the HQ, and "Phase 1: the
// HQ stays Rooted; no relocation". Home (rule M) is the relocation destination while the HQ relocates, else the HQ position.
// Determinism: integers and Fix64 only; own units, enemies and fields walked in ascending ID or index order; squared distances;
// ties to the lower ID or index; the random draws are the attack threshold jitter from the controller's own generator and, only when a profile
// key of package R is above 0, the variety stream of AiVariety (a second generator in AiMemory.Variety: match-start composition band and
// expansion offset, then an attack-advantage offset after each threshold draw).
// Package L (opening library): a faction plan may list openings; AiVariety draws one at match start and PlanAt(tick) gives the brain the
// opening's build order (whole game) and composition (until its handover_seconds); ExpandAtSeconds, EarlyExpansionSeconds, AttackArmySupply and
// AttackMinSupply read the opening's value until the handover and the profile's after it.
// Package R (skirmish_ai_v4_rl.md, keys composition_jitter_fraction, expand_jitter_seconds, attack_advantage_jitter, all 0 in old profiles):
// Decide first swaps in memory.Variety.PlanAt(tick) (the jittered plan); rule E reads ExpandAtSeconds / EarlyExpansionSeconds (clock plus
// the drawn offset, clamped to 0 to 1800) and the predicted attack reads AttackMinAdvantage (plus the drawn offset, clamped to 0 to 100).
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>The decision rules. Stateless: all state is in <see cref="AiMemory"/>.</summary>
    public static partial class AiBrain
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
            // Package R and L: the plan of the drawn opening or the jittered plan, when the controller made a variety stream.
            plan = memory.Variety?.PlanAt(view.Tick) ?? plan;
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
                EnterMassing(memory, profile, rng, view.Tick);
            }
            // This think's working state (ledger, reservations, intents).
            Turn turn = new Turn(view, profile, plan, memory, rng, hq, intents);
            turn.Hq();
            turn.Cycle();
            turn.Gather();
            turn.Structures();
            turn.Tech();
            turn.StaticDefence();
            turn.Expand();
            turn.TrainWorkers();
            turn.Scout();
            turn.Produce();
            turn.ExtraProducer();
            turn.Army();
            return intents;
        }

        /// <summary>
        /// Test hook: what one think would see of the economy (served fields, workers wanted, live depots) without deciding anything.
        /// Updates the intel and field memory like <see cref="Decide"/>. Returns null when the player has no HQ.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="profile">The AI profile.</param>
        /// <param name="plan">The profile's plan for the player's faction.</param>
        /// <param name="memory">The controller's memory; updated.</param>
        internal static EconomyProbe? Probe(AiWorldView view, AiProfile profile, AiFactionPlan plan, AiMemory memory)
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
            return new EconomyProbe(turn.ServedIndices(), turn.WantedWorkers(), turn.LiveDepots, turn.StaticDefenceUnderConstructionNow, turn.StaticDefencesAtHq, turn.ServedCaps());
        }

        /// <summary>
        /// AI-E E1.1 formula (test hook): walk = max(0, distance - 2 x interact - radius) one way; W = ceil(2 x walk x ticksPerSecond / speed) ticks;
        /// cap = slots + ceil(slots x W / gatherTicks) + extra. Unit: workers. Range: slots to slots + a few dozen.
        /// </summary>
        /// <param name="slots">Gather slots of the field. Unit: workers. Range: 1 to 16.</param>
        /// <param name="distance">Field centre to drop-off position. Unit: tiles. Range: 0 upward.</param>
        /// <param name="radius">Drop-off footprint radius. Unit: tiles. Range: 0 upward.</param>
        /// <param name="interact">Interact range. Unit: tiles. Range: above 0.</param>
        /// <param name="speed">Worker speed. Unit: tiles per second. Range: 0 upward (0 counts as no walk time).</param>
        /// <param name="ticksPerSecond">Simulation rate. Unit: ticks per second. Range: 1 upward.</param>
        /// <param name="gatherTicks">Ticks one slot is held per load. Unit: ticks. Range: 1 upward.</param>
        /// <param name="extra">saturation_extra_workers_per_field. Unit: workers. Range: 0 to 4.</param>
        internal static int SaturationCap(int slots, Fix64 distance, Fix64 radius, Fix64 interact, Fix64 speed, int ticksPerSecond, int gatherTicks, int extra)
        {
            // One-way walk beyond the interact ranges. Unit: tiles. Range: 0 upward.
            Fix64 walk = Fix64.Max(Fix64.Zero, distance - interact * 2 - radius);
            // Round-trip walk time. Unit: ticks. Range: 0 upward.
            int walkTicks = speed > Fix64.Zero ? (walk * (2 * ticksPerSecond) / speed).CeilingToInt() : 0;
            // Ticks one slot is held per load, at least 1. Unit: ticks. Range: 1 upward.
            int hold = Math.Max(1, gatherTicks);
            return slots + (slots * walkTicks + hold - 1) / hold + extra;
        }

        /// <summary>The economy facts one think derives from a view (test hook, see <see cref="Probe"/>).</summary>
        internal sealed class EconomyProbe
        {
            /// <summary>Indices of the served fields, ascending. Range: 0 to field count - 1.</summary>
            public IReadOnlyList<int> Served { get; }

            /// <summary>Workers wanted by rule W. Unit: workers. Range: 0 to max_workers.</summary>
            public int WantedWorkers { get; }

            /// <summary>Live depots. Unit: depots. Range: 0 upward.</summary>
            public int LiveDepots { get; }

            /// <summary>True when an own static defence is under construction or ordered (a pending build order counts).</summary>
            public bool StaticDefenceUnderConstruction { get; }

            /// <summary>Own static defences, pending build orders included, within field_max_distance_tiles of the HQ. Unit: structures. Range: 0 upward.</summary>
            public int StaticDefencesAtHq { get; }

            /// <summary>Worker cap of each served field, parallel to <see cref="Served"/> (AI-E). Unit: workers. Range: 0 upward.</summary>
            public IReadOnlyList<int> ServedCaps { get; }

            /// <summary>Stores the probe result.</summary>
            public EconomyProbe(IReadOnlyList<int> served, int wantedWorkers, int liveDepots, bool staticDefenceUnderConstruction, int staticDefencesAtHq, IReadOnlyList<int> servedCaps)
            {
                ServedCaps = servedCaps;
                StaticDefenceUnderConstruction = staticDefenceUnderConstruction;
                StaticDefencesAtHq = staticDefencesAtHq;
                Served = served;
                WantedWorkers = wantedWorkers;
                LiveDepots = liveDepots;
            }
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

        /// <summary>
        /// The army supply needed for the next attack: attack_army_supply plus a draw of 0 to attack_supply_jitter. Called on the first
        /// think and when an attack ends (rule D); it also empties the attack group.
        /// </summary>
        /// <param name="memory">Gets the new threshold and stance Massing.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="rng">The controller's generator.</param>
        /// <param name="tick">The tick being decided (selects the opening's or the profile's attack_army_supply). Unit: ticks. Range: 0 upward.</param>
        internal static void EnterMassing(AiMemory memory, AiProfile profile, DeterministicRandom rng, int tick)
        {
            memory.Stance = AiStance.Massing;
            memory.HasAttackTarget = false;
            memory.AttackGroup.Clear();
            memory.AttackThreshold = AttackArmySupply(profile, memory, tick) + rng.NextInt(profile.AttackSupplyJitter + 1);
            // Package R draw 5, after the old draw so the old stream is untouched: a new offset of the predicted-advantage bar for this attack.
            memory.Variety?.DrawAttackAdvantageOffset(profile);
        }

        /// <summary>
        /// Rule E's clock of the first depot (expand_at_seconds) with the variety offset, clamped to 0 to 1800; the profile value when the
        /// memory has no variety stream.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory.</param>
        /// <param name="tick">The tick being decided (the opening's value applies before its handover). Unit: ticks. Range: 0 upward.</param>
        internal static Fix64 ExpandAtSeconds(AiProfile profile, AiMemory memory, int tick)
        {
            return OffsetSeconds(memory.Variety != null && memory.Variety.InOpening(tick) ? memory.Variety.Opening!.ExpandAtSeconds : profile.ExpandAtSeconds, memory);
        }

        /// <summary>Rule E's early-expansion clock (early_expansion_seconds) with the variety offset, clamped to 0 to 1800; the profile value without variety.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory.</param>
        /// <param name="tick">The tick being decided (the opening's value applies before its handover). Unit: ticks. Range: 0 upward.</param>
        internal static Fix64 EarlyExpansionSeconds(AiProfile profile, AiMemory memory, int tick)
        {
            return OffsetSeconds(memory.Variety != null && memory.Variety.InOpening(tick) ? memory.Variety.Opening!.EarlyExpansionSeconds : profile.EarlyExpansionSeconds, memory);
        }

        /// <summary>The effective attack_army_supply (the fallback attack threshold before jitter): the opening's value before its handover, else the profile's.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory.</param>
        /// <param name="tick">The tick being decided. Unit: ticks. Range: 0 upward.</param>
        internal static int AttackArmySupply(AiProfile profile, AiMemory memory, int tick)
        {
            return memory.Variety != null && memory.Variety.InOpening(tick) ? memory.Variety.Opening!.AttackArmySupply : profile.AttackArmySupply;
        }

        /// <summary>The effective attack_min_supply: the opening's value before its handover, else the profile's.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory.</param>
        /// <param name="tick">The tick being decided. Unit: ticks. Range: 0 upward.</param>
        internal static int AttackMinSupply(AiProfile profile, AiMemory memory, int tick)
        {
            return memory.Variety != null && memory.Variety.InOpening(tick) ? memory.Variety.Opening!.AttackMinSupply : profile.AttackMinSupply;
        }

        /// <summary>The effective attack_min_advantage: the profile value plus the variety offset, clamped to 0 to 100; the profile value without variety.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="memory">The controller's memory.</param>
        internal static Fix64 AttackMinAdvantage(AiProfile profile, AiMemory memory)
        {
            return memory.Variety == null ? profile.AttackMinAdvantage
                : Fix64.Clamp(profile.AttackMinAdvantage + memory.Variety.AttackAdvantageOffset, Fix64.Zero, Fix64.FromInt(100));
        }

        /// <summary>A clock value in seconds plus the variety's expansion offset, clamped to 0 to 1800; unchanged without variety.</summary>
        /// <param name="seconds">The clock value to offset: expand_at_seconds or early_expansion_seconds of the opening or the plan. Unit: seconds. Range: 0 to 1800.</param>
        /// <param name="memory">The controller's memory; its variety, when set, supplies the offset in whole seconds. Range: any AiMemory.</param>
        private static Fix64 OffsetSeconds(Fix64 seconds, AiMemory memory)
        {
            return memory.Variety == null ? seconds
                : Fix64.Clamp(seconds + Fix64.FromInt(memory.Variety.ExpandOffsetSeconds), Fix64.Zero, Fix64.FromInt(1800));
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
            // Known field amounts: start amounts on the first think, then the live amount of every field whose tile the team sees.
            if (memory.KnownFieldRemaining.Length != view.Fields.Count)
            {
                // Fresh known amounts, one per field. Unit: resource units.
                int[] known = new int[view.Fields.Count];
                // Field position in the list. Range: 0 to field count - 1.
                for (int i = 0; i < known.Length; i++)
                {
                    known[i] = view.Fields[i].StartAmount;
                }
                memory.KnownFieldRemaining = known;
            }
            foreach (AiFieldView field in view.Fields)
            {
                if (view.IsVisible(field.X, field.Y))
                {
                    memory.KnownFieldRemaining[field.Index] = field.Remaining;
                }
            }
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
                    known.DefinitionId = enemy.Definition.Id;
                    known.LastSeenTick = view.Tick;
                    // Health share when seen, 0 to 1 (a structure under construction can show above its build health; capped).
                    Fix64 fraction = enemy.Health / enemy.Definition.MaxHealth;
                    known.HealthFraction = fraction > Fix64.One ? Fix64.One : fraction;
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
            if (profile.UseEnemyModel)
            {
                AiIntel.UpdateModel(view, profile, memory);
            }
        }

        /// <summary>One think's working state: the resource ledger, reservations and the intent list.</summary>
        private sealed partial class Turn
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

            /// <summary>Workers given a build order or a scouting move earlier in this think; <see cref="PickBuilder"/> skips them. Ascending ID.</summary>
            private readonly SortedSet<EntityId> _pickedBuilders = new SortedSet<EntityId>();

            /// <summary>Own count per composition unit, queued included, in ordinal ID order; filled by <see cref="Produce"/>. Unit: units. Range: 0 upward.</summary>
            private readonly SortedDictionary<string, int> _counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Producers that were given a train order this think (rule P); they count as busy. Ascending ID.</summary>
            private readonly SortedSet<EntityId> _usedProducers = new SortedSet<EntityId>();

            /// <summary>Visible enemy army units inside the base this think (the threats). Unit: units. Range: 0 upward.</summary>
            private int _threatCount;

            /// <summary>Supply of those threats. Unit: supply. Range: 0 upward.</summary>
            private int _threatSupply;

            /// <summary>Sum of the threats' X positions, for the mean. Unit: tiles. Range: 0 to threat count x map width.</summary>
            private Fix64 _threatSumX = Fix64.Zero;

            /// <summary>Sum of the threats' Y positions, for the mean. Unit: tiles. Range: 0 to threat count x map height.</summary>
            private Fix64 _threatSumY = Fix64.Zero;

            /// <summary>The most recent hit on an own unit inside the base (ties to the lowest unit ID), or null.</summary>
            private DamageReport? _latestHit;

            /// <summary>The known low enemy HQ that rule K attacks this think, or null (set by <see cref="Army"/>).</summary>
            private KnownStructure? _kill;

            /// <summary>Served fields (known resources, within field_max_distance_tiles of a planned drop-off), in index order (B.3).</summary>
            private readonly List<AiFieldView> _served = new List<AiFieldView>();

            /// <summary>
            /// Own units plus one synthetic under-construction structure per own pending build order (a worker walking to a site or placing),
            /// at the centre of the site tile (worker_construction.md 9.3 item 2). The rules that count or locate own structures read this list;
            /// the real units come first in ascending ID, then the pending orders in ascending worker ID. Pending entries are never a usable
            /// drop-off, a free producer or a unit that can be ordered. Unit: units. Range: 0 to own unit count x 2.
            /// </summary>
            private readonly List<AiUnitView> _planned = new List<AiUnitView>();

            /// <summary>Positions of the planned drop-offs: complete drop-offs and the Rooted HQ, then depots under construction. Unit: tiles.</summary>
            private readonly List<FixVec2> _plannedDropOffs = new List<FixVec2>();

            /// <summary>Footprint radius of each entry of <see cref="_plannedDropOffs"/>, same order (AI-E cap formula). Unit: tiles. Range: 0 to 4.</summary>
            private readonly List<Fix64> _plannedDropOffRadii = new List<Fix64>();

            /// <summary>Own workers gathering or heading to each field at the start of this think (the same count rule G uses), by field index. Unit: workers. Range: 0 upward.</summary>
            private int[] _assignedAtStart = Array.Empty<int>();

            /// <summary>AI-E E1.4: true when rare_cap_yields_to_saturation is on and every served common field holds at least its cap of workers, so rare fields stay open. Unit: boolean.</summary>
            private bool _commonFull;

            /// <summary>Saturation cap per field index, filled on first use within a think (AI-E E1.1). Unit: workers. Range: 1 upward.</summary>
            private readonly SortedDictionary<int, int> _saturationCaps = new SortedDictionary<int, int>();

            /// <summary>True when a complete drop-off exists (a complete depot or the Rooted HQ).</summary>
            private bool _hasDropOff;

            /// <summary>Live depots: own drop-off structures under construction, or complete with a field of known resources in reach. Unit: depots. Range: 0 upward.</summary>
            private int _liveDepots;

            /// <summary>True while an own depot is under construction.</summary>
            private bool _depotUnderConstruction;

            /// <summary>True when some field on the map has known resources.</summary>
            private bool _anyLive;

            /// <summary>True when at least one own worker is not idle (rule R's income test).</summary>
            private bool _income;

            /// <summary>Sum of the known remaining amounts of the home fields (every field within reach of the HQ position). Unit: resource units. Range: 0 upward.</summary>
            private int _homeRemaining;

            /// <summary>Sum of the start amounts of the home fields. Unit: resource units. Range: 0 upward.</summary>
            private int _homeStart;

            /// <summary>True when no field is served (skirmish_ai_v2.md B.3 "starved").</summary>
            private bool Starved => _served.Count == 0;

            /// <summary>True when all_in_when_mined_out is set and no field on the map has known resources (rule A): attack with everything, never retreat.</summary>
            private bool AllIn => _profile.AllInWhenMinedOut && !_anyLive;

            /// <summary>True when a threat is seen inside the base this think (the army rules' definition, used by E and H).</summary>
            private bool UnderThreat => _threatCount > 0 || _latestHit != null;

            /// <summary>Known remaining amount of a field. Unit: resource units. Range: 0 upward.</summary>
            private int Known(AiFieldView field)
            {
                return _memory.KnownFieldRemaining[field.Index];
            }

            /// <summary>Squared reach of a drop-off, field_max_distance_tiles squared. Unit: squared tiles.</summary>
            private Fix64 ReachSquared => _profile.FieldMaxDistanceTiles * _profile.FieldMaxDistanceTiles;

            /// <summary>
            /// Workers a field may hold: workers_per_field for common, rare_workers_per_field for rare, and 0 for rare while rare_bank_soft_cap
            /// is on (above 0) and the rare bank is at or above it (C2.3). Unit: workers.
            /// </summary>
            private int CapOf(AiFieldView field)
            {
                if (field.Kind == ResourceKind.Common)
                {
                    return _profile.UseSaturationWorkers ? SaturationCapOf(field) : _profile.WorkersPerField;
                }
                if (RareCapped)
                {
                    return 0;
                }
                return _profile.UseSaturationWorkers ? SaturationCapOf(field) : _profile.RareWorkersPerField;
            }

            /// <summary>
            /// AI-E E1.1: the saturation cap of a field: its gather slots plus the workers that cover the walk to the nearest planned drop-off
            /// (nearest by squared distance; a tie goes to the earlier entry of the planned drop-off list, which is ascending ID for real structures
            /// and then pending orders by worker ID, so the lower ID; with none, field_max_distance_tiles away), plus saturation_extra_workers_per_field.
            /// Unit: workers. Range: 1 upward.
            /// </summary>
            private int SaturationCapOf(AiFieldView field)
            {
                // Cap already computed this think for this field (out value). Unit: workers. Range: 1 upward.
                if (_saturationCaps.TryGetValue(field.Index, out int known))
                {
                    return known;
                }
                // Distance from the field to its nearest planned drop-off, capped at field_max_distance_tiles. Unit: tiles. Range: 0 to field_max_distance_tiles.
                Fix64 distance = _profile.FieldMaxDistanceTiles;
                // Footprint radius of that drop-off. Unit: tiles. Range: 0 to 4.
                Fix64 radius = Fix64.Zero;
                // Squared distance of the best drop-off so far. Unit: squared tiles. Range: 0 upward.
                Fix64 bestSquared = Fix64.Zero;
                // True once a drop-off has been seen. Unit: boolean. Range: true or false.
                bool any = false;
                // Index into the planned drop-offs. Range: 0 to count - 1.
                for (int i = 0; i < _plannedDropOffs.Count; i++)
                {
                    // Squared distance of this drop-off. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (_plannedDropOffs[i] - field.Centre).LengthSquared();
                    if (!any || squared < bestSquared)
                    {
                        any = true;
                        bestSquared = squared;
                        distance = Fix64.Min((_plannedDropOffs[i] - field.Centre).Length(), _profile.FieldMaxDistanceTiles);
                        radius = _plannedDropOffRadii[i];
                    }
                }
                // Gather slots of this field's kind. Unit: workers. Range: 1 to 16.
                int slots = field.Kind == ResourceKind.Rare ? _view.GatherSlotsRare : _view.GatherSlotsCommon;
                // Worker speed of the plan's worker. Unit: tiles per second. Range: 0 upward.
                Fix64 speed = _view.Content.Units[_plan.WorkerUnitId].SpeedTilesPerSecond;
                // Cap computed from the formula. Unit: workers. Range: 1 upward.
                int cap = AiBrain.SaturationCap(slots, distance, radius, _view.InteractRangeTiles, speed, _view.TicksPerSecond, _view.GatherTicksPerTrip, _profile.SaturationExtraWorkersPerField);
                _saturationCaps[field.Index] = cap;
                return cap;
            }

            /// <summary>True while rare_bank_soft_cap is on (above 0) and the rare bank is at or above it (C2.3): rare fields take no workers. Unit: boolean.</summary>
            private bool RareCapped => _memory.RareCapActive && !_commonFull;

            /// <summary>
            /// Updates the rare-worker cap state (C2.3 with its review hysteresis): on when the rare bank reaches rare_bank_soft_cap, off again when
            /// it falls below rare_bank_return_cap (0 means the cap itself). Always off while rare_bank_soft_cap is 0. Called once per think by the constructor.
            /// </summary>
            private void UpdateRareCap()
            {
                if (_profile.RareBankSoftCap <= 0)
                {
                    _memory.RareCapActive = false;
                    return;
                }
                // Bank level below which the cap lifts. Unit: resource units (rare). Range: 1 to rare_bank_soft_cap.
                int returnAt = _profile.RareBankReturnCap > 0 ? _profile.RareBankReturnCap : _profile.RareBankSoftCap;
                if (_view.Rare >= _profile.RareBankSoftCap)
                {
                    _memory.RareCapActive = true;
                }
                else if (_view.Rare < returnAt)
                {
                    _memory.RareCapActive = false;
                }
            }

            /// <summary>The enemy model M(t) per army type (B.3) this think; empty when use_enemy_model is off. Unit: units. Range: above 0 each.</summary>
            private readonly SortedDictionary<string, Fix64> _model;

            /// <summary>The enemy model as fight entries (C.1), ordinal order; empty when nothing is known.</summary>
            private readonly List<FightEntry> _enemies;

            /// <summary>The army composition weights in force this think: the counter-mix (C.4) when use_counter_mix, else the plan's composition. Unit: weight units. Range: 0 upward each.</summary>
            private readonly IReadOnlyDictionary<string, int> _composition;

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
                UpdateRareCap();
                // Largest worked distance, squared. Unit: squared tiles.
                Fix64 reachSquared = ReachSquared;
                // DepotsPlaced counts the depots among the own units and the pending depot orders (rule E's schedule), not the orders given this think.
                memory.DepotsPlaced = 0;
                _planned.AddRange(view.OwnUnits);
                foreach (AiUnitView worker in view.OwnUnits)
                {
                    // Definition of the pending order's structure, when this worker has one.
                    if (worker.HasBuildOrder && view.Content.Units.TryGetValue(worker.BuildDefinitionId, out UnitDefinition? ordered) && view.Contains(worker.BuildTileX, worker.BuildTileY))
                    {
                        _planned.Add(new AiUnitView(worker.Id, worker.Owner, ordered, view.CentreOf(worker.BuildTileX, worker.BuildTileY), ordered.MaxHealth,
                            ordered.Layer, HqState.Rooted, true, false, false, FixVec2.Zero, EntityId.None, false, GatherState.Idle, -1, Array.Empty<string>()));
                        // The site is taken: no other structure is planned on it (so the simulation never answers site_pending).
                        _reservedSites.Add(view.IndexOf(worker.BuildTileX, worker.BuildTileY));
                    }
                }
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsWorker && unit.GatherState != GatherState.Idle)
                    {
                        _income = true;
                    }
                    if (unit.IsHq && unit.HqState == HqState.Rooted && !unit.IsUnderConstruction)
                    {
                        _hasDropOff = true;
                        _plannedDropOffs.Add(unit.Position);
                        _plannedDropOffRadii.Add(unit.Definition.Radius);
                    }
                    else if (unit.IsStructure && unit.Definition.Economy.IsDropOff)
                    {
                        _plannedDropOffs.Add(unit.Position);
                        _plannedDropOffRadii.Add(unit.Definition.Radius);
                        _memory.DepotsPlaced++;
                        if (unit.IsUnderConstruction)
                        {
                            _depotUnderConstruction = true;
                            _liveDepots++;
                        }
                        else
                        {
                            _hasDropOff = true;
                            if (AnyLiveFieldWithin(unit.Position))
                            {
                                _liveDepots++;
                            }
                        }
                    }
                }
                foreach (AiFieldView field in view.Fields)
                {
                    if (Known(field) > 0)
                    {
                        _anyLive = true;
                        if (IsNearAny(_plannedDropOffs, field.Centre))
                        {
                            _served.Add(field);
                        }
                    }
                    if ((field.Centre - hq.Position).LengthSquared() <= reachSquared)
                    {
                        _homeRemaining += Known(field);
                        _homeStart += field.StartAmount;
                    }
                }
                // AI-E E1.4: workers per field now, and whether every served common field is full (only with the rare switch on).
                _assignedAtStart = new int[view.Fields.Count];
                foreach (AiUnitView unit in view.OwnUnits)
                {
                    if (unit.IsWorker && unit.GatherFieldIndex >= 0 && unit.GatherFieldIndex < _assignedAtStart.Length)
                    {
                        _assignedAtStart[unit.GatherFieldIndex]++;
                    }
                }
                if (profile.RareCapYieldsToSaturation)
                {
                    _commonFull = true;
                    foreach (AiFieldView field in _served)
                    {
                        if (field.Kind == ResourceKind.Common && _assignedAtStart[field.Index] < CapOf(field))
                        {
                            _commonFull = false;
                        }
                    }
                }
                ScanThreat();
                _model = profile.UseEnemyModel ? AiIntel.ModelCounts(view, profile, memory) : new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
                _enemies = AiCounter.Entries(view, _model);
                _composition = profile.UseCounterMix ? AiCounter.Mix(view, profile, plan, memory) : plan.Composition;
            }

            /// <summary>True when some field with known resources lies within field_max_distance_tiles of <paramref name="point"/>.</summary>
            private bool AnyLiveFieldWithin(FixVec2 point)
            {
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Known(field) > 0 && (field.Centre - point).LengthSquared() <= ReachSquared)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>True when <paramref name="point"/> is within field_max_distance_tiles of any of <paramref name="places"/>.</summary>
            private bool IsNearAny(List<FixVec2> places, FixVec2 point)
            {
                foreach (FixVec2 place in places)
                {
                    if ((place - point).LengthSquared() <= ReachSquared)
                    {
                        return true;
                    }
                }
                return false;
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

            /// <summary>True when the stock minus reservations pays for <paramref name="definition"/>; a resource the definition does not cost is not checked (a reservation in it never blocks the purchase).</summary>
            private bool CanPay(UnitDefinition definition)
            {
                return (definition.CostCommon == 0 || _common - _reserveCommon >= definition.CostCommon) && (definition.CostRare == 0 || _rare - _reserveRare >= definition.CostRare);
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
                // Only the team's pick captain picks, and not again while a pick (its own, a teammate's or a script's) is pending: the
                // view shows a pending pick only when it belongs to the own team or a script, so two teammates with different
                // preferences cannot overwrite each other's pick every think.
                if (!_view.IsTeamPickCaptain || _view.PendingPickId.Length > 0)
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
                Emit(AiIntent.PickCycle(choice));
            }

            /// <summary>
            /// Rule 1 (rule H of skirmish_ai_v2.md B.5): roots an Uprooted, idle HQ that stands within root reach of a field with known
            /// resources; ends a finished relocation; starts, steers and re-targets relocations (see the class header).
            /// </summary>
            public void Hq()
            {
                if (_hq.HqState == HqState.Rooted)
                {
                    if (_memory.RelocationActive)
                    {
                        // The relocation is over: the HQ stands on its new field.
                        _memory.RelocationActive = false;
                        _memory.RelocationCooldownUntilTick = _view.Tick + _view.SecondsToTicks(_profile.RelocateCooldownSeconds);
                        _memory.Relocations++;
                    }
                    // The failed-field memory lasts only until the HQ stands on a field again.
                    _memory.FailedRelocationFields.Clear();
                    TriggerRelocation();
                    return;
                }
                if (_hq.HqState != HqState.Uprooted)
                {
                    // Rooting or uprooting: nothing to order until the switch is done.
                    return;
                }
                if (!_hq.HasMoveTarget && CanRootHere())
                {
                    Emit(AiIntent.RootHq(_hq.Id));
                    return;
                }
                if (!_memory.RelocationActive)
                {
                    if (!_hq.HasMoveTarget)
                    {
                        // An Uprooted HQ nobody sent anywhere (for example after a script): relocate it.
                        // Best field to head for, measured from where it stands (the nearest live field when no candidate is left), or null.
                        AiFieldView? choice = ChooseRelocationTarget(true);
                        if (choice != null)
                        {
                            BeginRelocation(choice);
                            Emit(AiIntent.Move(_hq.Id, _memory.RelocationDestination));
                        }
                    }
                    return;
                }
                // True when the target changed this think, so the HQ must be sent to the new destination even while moving.
                bool retargeted = Retarget();
                if (retargeted || !_hq.HasMoveTarget)
                {
                    Emit(AiIntent.Move(_hq.Id, _memory.RelocationDestination));
                }
            }

            /// <summary>
            /// Rule H step 1: true when the Uprooted HQ may root where it stands. With no relocation running, a field with known resources
            /// within the root reach is enough. While relocating, only the relocation's own target field counts (the nearly empty home
            /// fields it is leaving must not pull it back down), or any live field in reach once no candidate field is left (step 4).
            /// </summary>
            private bool CanRootHere()
            {
                if (!_memory.RelocationActive)
                {
                    return LiveFieldInRootReach();
                }
                // Target field of the running relocation.
                AiFieldView target = _view.Fields[_memory.RelocationField];
                // Root reach squared. Unit: squared tiles.
                Fix64 reachSquared = _view.HqRootMaxDistanceTiles * _view.HqRootMaxDistanceTiles;
                if (Known(target) > 0 && (target.Centre - _hq.Position).LengthSquared() <= reachSquared)
                {
                    return true;
                }
                return RelocationCandidates().Count == 0 && LiveFieldInRootReach();
            }

            /// <summary>
            /// The field an Uprooted HQ heads for: the first relocation candidate, else (so the HQ always ends up rooted while any field
            /// has resources, also when it stands 6 to 10 tiles from one) the nearest field with known resources, preferring fields that
            /// have not just timed out (<paramref name="avoidFailed"/>). Null when no field has known resources.
            /// </summary>
            private AiFieldView? ChooseRelocationTarget(bool avoidFailed)
            {
                List<AiFieldView> candidates = RelocationCandidates();
                if (candidates.Count > 0)
                {
                    return candidates[0];
                }
                // Nearest field with known resources, or null. Unit: field. Range: any field.
                AiFieldView? nearest = null;
                // Nearest such field that is not a failed relocation target, or null. Unit: field. Range: any field.
                AiFieldView? nearestFresh = null;
                // Squared distance of the nearest field from the HQ. Unit: squared tiles. Range: 0 upward.
                Fix64 nearestSquared = Fix64.Zero;
                // Squared distance of the nearest non-failed field from the HQ. Unit: squared tiles. Range: 0 upward.
                Fix64 freshSquared = Fix64.Zero;
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Known(field) <= 0)
                    {
                        continue;
                    }
                    // Squared distance of this field. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (field.Centre - _hq.Position).LengthSquared();
                    if (nearest == null || squared < nearestSquared)
                    {
                        nearest = field;
                        nearestSquared = squared;
                    }
                    if (!_memory.FailedRelocationFields.Contains(field.Index) && (nearestFresh == null || squared < freshSquared))
                    {
                        nearestFresh = field;
                        freshSquared = squared;
                    }
                }
                return avoidFailed && nearestFresh != null ? nearestFresh : nearest;
            }

            /// <summary>True when a field with known resources lies within the HQ root reach (tuning/hq.json) of the HQ.</summary>
            private bool LiveFieldInRootReach()
            {
                // Root reach squared. Unit: squared tiles.
                Fix64 reachSquared = _view.HqRootMaxDistanceTiles * _view.HqRootMaxDistanceTiles;
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Known(field) > 0 && (field.Centre - _hq.Position).LengthSquared() <= reachSquared)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>Rule H step 2: a Rooted HQ whose home fields are nearly empty and that has no cheaper option uproots and heads for a new field.</summary>
            private void TriggerRelocation()
            {
                if (_memory.RelocationActive || UnderThreat || _view.Tick < _memory.RelocationCooldownUntilTick)
                {
                    return;
                }
                // Share of the home fields' start amount still known to be there, as the test home <= fraction x start (no home fields: 0 <= anything).
                if (Fix64.FromInt(_homeRemaining) > _profile.RelocateWhenHomeRemainingFraction * Fix64.FromInt(_homeStart))
                {
                    return;
                }
                if (_profile.UseClusterRelocation)
                {
                    TriggerClusterRelocation();
                    return;
                }
                if (CheaperOptionExists())
                {
                    return;
                }
                // Candidate fields measured from the HQ position, excluding the home fields.
                List<AiFieldView> candidates = RelocationCandidates();
                if (candidates.Count == 0)
                {
                    return;
                }
                BeginRelocation(candidates[0]);
                Emit(AiIntent.UprootHq(_hq.Id));
            }

            /// <summary>True when a depot is a cheaper way to reach new fields than moving the HQ (the plan builds one, the live-depot cap allows it, and it is affordable or income exists).</summary>
            private bool CheaperOptionExists()
            {
                // Depot the plan builds, or null when it builds none.
                UnitDefinition? depot = _plan.DropOffBuildingId.Length > 0 ? _view.Content.Units[_plan.DropOffBuildingId] : null;
                return depot != null && _liveDepots < _profile.MaxLiveDepots && (_income || _common >= depot.CostCommon);
            }

            /// <summary>
            /// AI-HQ rule HQ-C (use_cluster_relocation): called with home at or below relocate_when_home_remaining_fraction, no threat and the cooldown
            /// over. Picks the best candidate cluster (most live gather slots, then nearest to the HQ, then lower field index; the anchor field is common,
            /// moving to a rare field is deferred), and uproots when its live slots are at least relocate_gain_ratio times home's (any candidate with
            /// slots when home has none). The cheaper-option test is skipped when an own complete depot already serves the anchor field. Never while
            /// the army stance is Defend. The fields of the home it leaves go into the one-way latch <see cref="AiMemory.ClusterLeftFields"/>.
            /// </summary>
            private void TriggerClusterRelocation()
            {
                if (_memory.Stance == AiStance.Defend)
                {
                    return;
                }
                // Live gather slots at home. Unit: workers. Range: 0 upward.
                int homeSlots = LiveSlotsAround(_hq.Position);
                // Best candidate anchor so far, or null. Unit: field. Range: any common field.
                AiFieldView? best = null;
                // Live slots of the best candidate. Unit: workers. Range: 1 upward once best is set.
                int bestSlots = 0;
                // Squared distance of the best candidate from the HQ. Unit: squared tiles. Range: 0 upward.
                Fix64 bestSquared = Fix64.Zero;
                foreach (AiFieldView field in RelocationCandidates(true))
                {
                    if (field.Kind != ResourceKind.Common)
                    {
                        continue;
                    }
                    // Live slots around where the HQ would stand. Unit: workers. Range: 0 upward.
                    int slots = LiveSlotsAround(DestinationFor(field));
                    // Squared distance of this candidate from the HQ. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (field.Centre - _hq.Position).LengthSquared();
                    if (best == null || slots > bestSlots || (slots == bestSlots && (squared < bestSquared || (squared == bestSquared && field.Index < best.Index))))
                    {
                        best = field;
                        bestSlots = slots;
                        bestSquared = squared;
                    }
                }
                if (best == null || bestSlots <= 0 || Fix64.FromInt(bestSlots) < _profile.RelocateGainRatio * Fix64.FromInt(homeSlots))
                {
                    return;
                }
                if (!IsNearAny(OwnDepotPositions(true), best.Centre) && CheaperOptionExists())
                {
                    return;
                }
                foreach (AiFieldView field in _view.Fields)
                {
                    if ((field.Centre - _hq.Position).LengthSquared() <= ReachSquared)
                    {
                        _memory.ClusterLeftFields.Add(field.Index);
                    }
                }
                BeginRelocation(best);
                Emit(AiIntent.UprootHq(_hq.Id));
            }

            /// <summary>
            /// AI-HQ: live gather slots around <paramref name="point"/>: the sum of the gather slots (common or rare, from the Sim tuning) of every
            /// field within field_max_distance_tiles of it whose known remaining is at least expand_min_field_remaining. Unit: workers. Range: 0 upward.
            /// </summary>
            private int LiveSlotsAround(FixVec2 point)
            {
                // Slots counted so far. Unit: workers. Range: 0 upward.
                int slots = 0;
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Known(field) >= _profile.ExpandMinFieldRemaining && (field.Centre - point).LengthSquared() <= ReachSquared)
                    {
                        slots += field.Kind == ResourceKind.Rare ? _view.GatherSlotsRare : _view.GatherSlotsCommon;
                    }
                }
                return slots;
            }

            /// <summary>Positions of the own depots (drop-off structures, not the HQ); with <paramref name="completeOnly"/> those under construction are left out.</summary>
            private List<FixVec2> OwnDepotPositions(bool completeOnly)
            {
                // Positions found. Unit: tiles.
                List<FixVec2> depots = new List<FixVec2>();
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.Definition.Economy.IsDropOff && !(completeOnly && unit.IsUnderConstruction))
                    {
                        depots.Add(unit.Position);
                    }
                }
                return depots;
            }

            /// <summary>
            /// Fields a relocation may head for, in target order from the HQ position: not within field_max_distance_tiles of an own depot
            /// (a drop-off structure, complete or not; with <paramref name="allowCompleteDepots"/> only one under construction) and not a field of a home
            /// left by a cluster relocation (AI-HQ latch) and not a home field (within field_max_distance_tiles of the HQ position, so a
            /// relocating HQ never picks the base it is leaving, nor, after a re-target, the fields around where it stands).
            /// </summary>
            private List<AiFieldView> RelocationCandidates(bool allowCompleteDepots = false)
            {
                // Positions of the own depots that rule out a field: all, or (AI-HQ) only those still under construction.
                List<FixVec2> depots = new List<FixVec2>();
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.Definition.Economy.IsDropOff && !(allowCompleteDepots && !unit.IsUnderConstruction))
                    {
                        depots.Add(unit.Position);
                    }
                }
                // Candidates left after the filters, in target order.
                List<AiFieldView> result = new List<AiFieldView>();
                foreach (AiFieldView field in TargetOrder(_hq.Position))
                {
                    if (IsNearAny(depots, field.Centre) || (field.Centre - _hq.Position).LengthSquared() <= ReachSquared
                        || _memory.FailedRelocationFields.Contains(field.Index) || _memory.ClusterLeftFields.Contains(field.Index) || SurroundingsNearlyEmpty(field))
                    {
                        continue;
                    }
                    result.Add(field);
                }
                return result;
            }

            /// <summary>
            /// True when the fields around <paramref name="field"/> (every field within field_max_distance_tiles of its centre, itself
            /// included) hold relocate_when_home_remaining_fraction or less of their start amount: moving there would only start the
            /// next relocation, so the HQ would bounce between nearly empty bases.
            /// </summary>
            private bool SurroundingsNearlyEmpty(AiFieldView field)
            {
                // Known amount left in the fields around the field. Unit: resource units. Range: 0 upward.
                int known = 0;
                // Start amount of those fields. Unit: resource units. Range: 1 upward.
                int start = 0;
                foreach (AiFieldView other in _view.Fields)
                {
                    if ((other.Centre - field.Centre).LengthSquared() <= ReachSquared)
                    {
                        known += Known(other);
                        start += other.StartAmount;
                    }
                }
                return Fix64.FromInt(known) <= _profile.RelocateWhenHomeRemainingFraction * Fix64.FromInt(start);
            }

            /// <summary>Where the HQ stops for <paramref name="field"/>: stand_off tiles (at most half the root distance) from its centre toward the HQ, on a standable tile.</summary>
            private FixVec2 DestinationFor(AiFieldView field)
            {
                // Distance kept from the field centre. Unit: tiles. Range: 0.5 to half the root distance.
                Fix64 standOff = Fix64.Min(_profile.RelocateStandOffTiles, _view.HqRootMaxDistanceTiles / 2);
                return Standable(field.Centre.MoveTowards(_hq.Position, standOff));
            }

            /// <summary>Records a relocation to <paramref name="field"/> in the memory.</summary>
            private void BeginRelocation(AiFieldView field)
            {
                _memory.RelocationActive = true;
                _memory.RelocationField = field.Index;
                _memory.RelocationDestination = DestinationFor(field);
                _memory.RelocationStartTick = _view.Tick;
            }

            /// <summary>
            /// Rule H step 4: while relocating, chooses again when the target field is known empty, its destination is unsafe and a safe
            /// candidate exists, or the timeout has passed. Returns true when the destination changed.
            /// </summary>
            private bool Retarget()
            {
                // Target field of the running relocation.
                AiFieldView target = _view.Fields[_memory.RelocationField];
                // True when the target has no known resources left.
                bool depleted = Known(target) <= 0;
                // True when the relocation has taken too long.
                bool timedOut = _view.Tick >= _memory.RelocationStartTick + _view.SecondsToTicks(_profile.RelocateTimeoutSeconds);
                // True when the destination is now close to the enemy.
                bool unsafeDestination = IsUnsafe(_memory.RelocationDestination);
                if (!depleted && !timedOut && !unsafeDestination)
                {
                    return false;
                }
                // Candidate fields measured from where the HQ stands.
                List<AiFieldView> candidates = RelocationCandidates();
                if (!depleted && candidates.Count == 0)
                {
                    // Nothing better: keep moving to the old destination (a timeout is handled below).
                    if (!timedOut)
                    {
                        return false;
                    }
                }
                if (!depleted && !timedOut && IsUnsafe(candidates[0].Centre))
                {
                    // Only unsafe alternatives: stay on the old target.
                    return false;
                }
                if (timedOut && !depleted)
                {
                    // The destination could not be reached in time: do not pick this field again until the HQ has rooted.
                    _memory.FailedRelocationFields.Add(_memory.RelocationField);
                    candidates = RelocationCandidates();
                }
                // Next field: the first candidate, else the nearest live field (the HQ must end up rooted while any field has resources).
                AiFieldView? next = candidates.Count > 0 ? candidates[0] : ChooseRelocationTarget(true);
                if (next == null)
                {
                    return false;
                }
                BeginRelocation(next);
                return true;
            }

            /// <summary>Rule 2 (rule G of skirmish_ai_v2.md B.5): sends idle workers to served fields, long hauls when starved, follows a relocating HQ, rebalances.</summary>
            public void Gather()
            {
                if (!_hasDropOff && !_memory.RelocationActive)
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
                // Target order from the HQ position, computed once for the long haul (empty unless starved with a drop-off).
                List<AiFieldView> haul = new List<AiFieldView>();
                if (_hasDropOff && Starved)
                {
                    // Unsafe fields stay out of the long haul (workers walk there alone; a depot may still be built beside one, rule E).
                    foreach (AiFieldView field in TargetOrder(_hq.Position))
                    {
                        if (!IsUnsafe(field.Centre))
                        {
                            haul.Add(field);
                        }
                    }
                }
                foreach (AiUnitView worker in _view.OwnUnits)
                {
                    if (!worker.IsWorker || worker.GatherState != GatherState.Idle || worker.HasMoveTarget || worker.IsAttackMoving || worker.HasBuildOrder)
                    {
                        continue;
                    }
                    if (!_hasDropOff)
                    {
                        // Rule G3: follow the relocating HQ (workers already at the destination stay).
                        if ((worker.Position - _memory.RelocationDestination).LengthSquared() > AtRallySquared)
                        {
                            Emit(AiIntent.Move(worker.Id, _memory.RelocationDestination));
                        }
                        continue;
                    }
                    // Nearest served field under its cap, else nearest served field at all.
                    AiFieldView? field = NearestField(worker.Position, assigned, true) ?? NearestField(worker.Position, assigned, false);
                    if (field == null && haul.Count > 0)
                    {
                        // Rule G2: long haul to the first field in target order under its cap, else the first.
                        field = haul[0];
                        foreach (AiFieldView candidate in haul)
                        {
                            if (assigned[candidate.Index] < CapOf(candidate))
                            {
                                field = candidate;
                                break;
                            }
                        }
                    }
                    if (field == null || !Emit(AiIntent.Gather(worker.Id, field.Index)))
                    {
                        continue;
                    }
                    assigned[field.Index]++;
                }
                if (_hasDropOff)
                {
                    Rebalance(assigned);
                }
            }

            /// <summary>
            /// Rule G4: for each served field in index order holding more workers than its cap, the highest-ID excess workers (at most
            /// rebalance_workers_per_think in total) are sent to the nearest served field below its cap.
            /// </summary>
            private void Rebalance(int[] assigned)
            {
                // Moves still allowed this think. Unit: workers. Range: 0 to rebalance_workers_per_think.
                int budget = _profile.RebalanceWorkersPerThink;
                foreach (AiFieldView field in _served)
                {
                    // Workers above the cap on this field. Unit: workers.
                    int excess = assigned[field.Index] - CapOf(field);
                    // Own units in descending ID order: the highest-ID workers on this field move first.
                    for (int i = _view.OwnUnits.Count - 1; i >= 0 && excess > 0 && budget > 0; i--)
                    {
                        AiUnitView worker = _view.OwnUnits[i];
                        if (!worker.IsWorker || worker.GatherFieldIndex != field.Index || worker.HasBuildOrder)
                        {
                            continue;
                        }
                        // Nearest served field below its cap, other than the full one (it is at or over its cap, so it never qualifies).
                        AiFieldView? target = NearestField(worker.Position, assigned, true);
                        if (target == null && field.Kind == ResourceKind.Rare && RareCapped && !_profile.RareCapYieldsToSaturation)
                        {
                            // C2 review item 2: the rare bank is at the soft cap, so rare workers leave for the nearest served common field even when it is full.
                            // (Not with rare_cap_yields_to_saturation: a full common field would run over its cap, the rare cap would reopen and the
                            // worker would be sent back; AI-E review finding 1. Those workers stay and only new workers are kept off the rare field.)
                            target = NearestCommonField(worker.Position);
                        }
                        if (target == null && field.Kind == ResourceKind.Rare && RareCapped && _profile.RareCapYieldsToSaturation)
                        {
                            // Yielding rare cap: nowhere to put the rest of this closed rare field's workers; go on with the next field.
                            break;
                        }
                        if (target == null)
                        {
                            return;
                        }
                        if (Emit(AiIntent.Gather(worker.Id, target.Index)))
                        {
                            assigned[field.Index]--;
                            assigned[target.Index]++;
                            excess--;
                            budget--;
                        }
                    }
                }
            }

            /// <summary>The served common field nearest <paramref name="from"/>, ties to the lowest index, whatever its worker count; null when none is served.</summary>
            private AiFieldView? NearestCommonField(FixVec2 from)
            {
                // Best candidate so far, or null.
                AiFieldView? best = null;
                // Squared distance of the best field. Unit: squared tiles. Range: 0 upward.
                Fix64 bestSquared = Fix64.Zero;
                foreach (AiFieldView field in _served)
                {
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (field.Centre - from).LengthSquared();
                    if (field.Kind == ResourceKind.Common && (best == null || squared < bestSquared))
                    {
                        best = field;
                        bestSquared = squared;
                    }
                }
                return best;
            }

            /// <summary>The served field nearest <paramref name="from"/>, ties to the lowest index; with <paramref name="underCap"/> only fields below their cap.</summary>
            private AiFieldView? NearestField(FixVec2 from, int[] assigned, bool underCap)
            {
                // Best candidate so far, or null.
                AiFieldView? best = null;
                // Squared distance of the best field. Unit: squared tiles.
                Fix64 bestSquared = Fix64.Zero;
                // True when some served field may hold workers at all; the fallback skips cap-0 fields then (C2.3). Unit: boolean.
                bool anyOpen = false;
                foreach (AiFieldView field in _served)
                {
                    anyOpen |= CapOf(field) > 0;
                }
                foreach (AiFieldView field in _served)
                {
                    if ((underCap && assigned[field.Index] >= CapOf(field)) || (!underCap && anyOpen && CapOf(field) == 0))
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
                foreach (AiUnitView unit in _planned)
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

            /// <summary>Own units (structures included, under construction included, pending build orders included) of a definition. Unit: count.</summary>
            private int CountOwn(string definitionId)
            {
                // Matches found. Unit: count.
                int count = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (string.Equals(unit.Definition.Id, definitionId, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
                return count;
            }

            /// <summary>
            /// Places <paramref name="definition"/> if the retry time passed, it is affordable, a builder and a site exist; reserves its cost
            /// when only money is short and the player has income (rule R). With <paramref name="site"/> given the structure goes on that
            /// tile (rule E's depot site), otherwise on the first good tile around the HQ (for a producer with use_forward_producers: around the
            /// base with the fewest producers, AI-P P1). Returns true when a build intent was emitted.
            /// </summary>
            /// <param name="definition">The structure to build.</param>
            /// <param name="site">Fixed site tile, or null to search.</param>
            /// <param name="producer">True for the producers of rule X and rule T (AI-P P1 may search another base's ring).</param>
            private bool TryBuild(UnitDefinition definition, (int X, int Y)? site = null, bool producer = false)
            {
                if (!RequirementsMet(definition))
                {
                    return false;
                }
                // Ticks between attempts at one structure. Unit: ticks.
                int retryTicks = _view.SecondsToTicks(_profile.BuildRetrySeconds);
                if (_memory.LastBuildAttemptTick.TryGetValue(definition.Id, out int last) && _view.Tick < last + retryTicks)
                {
                    return false;
                }
                if (!HasSupply(definition))
                {
                    return false;
                }
                if ((definition.CostCommon != 0 && _common - _reserveCommon < definition.CostCommon) || (definition.CostRare != 0 && _rare - _reserveRare < definition.CostRare))
                {
                    // Rule R: hold the cost only while workers are earning; without income the bank is spent on whatever it can buy.
                    if (_income)
                    {
                        _reserveCommon += definition.CostCommon;
                        _reserveRare += definition.CostRare;
                    }
                    return false;
                }
                // A worker without a build order must exist; the one that goes is chosen once the site is known.
                if (PickBuilder(_hq.Position) == null)
                {
                    return false;
                }
                _memory.LastBuildAttemptTick[definition.Id] = _view.Tick;
                // Tile column of the new structure. Unit: tiles. Range: 0 to map width - 1.
                int x = 0;
                // Tile row of the new structure. Unit: tiles. Range: 0 to map height - 1.
                int y = 0;
                if (site != null)
                {
                    x = site.Value.X;
                    y = site.Value.Y;
                }
                else if (!(producer && _profile.UseForwardProducers && TryFindProducerSite(out x, out y)) && !TryFindSite(out x, out y))
                {
                    return false;
                }
                // Worker that places the structure: the nearest free one to the site, never null here.
                AiUnitView builder = PickBuilder(_view.CentreOf(x, y))!;
                if (!_view.IsExplored(x, y))
                {
                    // The simulation refuses sites the team never saw (site_unexplored): walk there first; the next attempt builds.
                    if (Emit(AiIntent.Move(builder.Id, _view.CentreOf(x, y))))
                    {
                        _pickedBuilders.Add(builder.Id);
                    }
                    return false;
                }
                if (Emit(AiIntent.Build(builder.Id, definition.Id, x, y)))
                {
                    Pay(definition);
                    _pickedBuilders.Add(builder.Id);
                    _reservedSites.Add(_view.IndexOf(x, y));
                    return true;
                }
                return false;
            }

            /// <summary>
            /// The own worker without a build order nearest <paramref name="site"/> (squared distance, ties to the lowest ID), whatever it is
            /// doing (gathering, idle or carrying); null when there is none.
            /// </summary>
            /// <param name="site">Centre of the building site. Unit: tiles.</param>
            private AiUnitView? PickBuilder(FixVec2 site)
            {
                // Nearest free worker so far, and its squared distance. Unit: squared tiles. Range: 0 upward.
                AiUnitView? best = null;
                Fix64 bestSquared = Fix64.Zero;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (!unit.IsWorker || unit.HasBuildOrder || _pickedBuilders.Contains(unit.Id))
                    {
                        continue;
                    }
                    // Squared distance of this worker to the site. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (unit.Position - site).LengthSquared();
                    if (best == null || squared < bestSquared)
                    {
                        best = unit;
                        bestSquared = squared;
                    }
                }
                return best;
            }

            /// <summary>The first acceptable building site around the HQ (see rule 3 in the file header).</summary>
            private bool TryFindSite(out int siteX, out int siteY)
            {
                return TryFindSiteAround(_hq.TileX, _hq.TileY, out siteX, out siteY);
            }

            /// <summary>
            /// The first acceptable building site on the rings around the tile (<paramref name="hx"/>, <paramref name="hy"/>): rule 3's search with
            /// that tile as the centre and its height layer as the base height (AI-P P1 passes a depot's tile, <see cref="TryFindSite"/> the HQ's).
            /// </summary>
            /// <param name="hx">Centre tile column. Unit: tiles. Range: 0 to map width - 1.</param>
            /// <param name="hy">Centre tile row. Unit: tiles. Range: 0 to map height - 1.</param>
            /// <param name="siteX">Receives the site tile column (0 when none). Unit: tiles.</param>
            /// <param name="siteY">Receives the site tile row (0 when none). Unit: tiles.</param>
            private bool TryFindSiteAround(int hx, int hy, out int siteX, out int siteY)
            {
                // Height layer of the HQ's tile; sites must match it. Range: 0 to 7.
                byte baseHeight = _view.HeightAt(hx, hy);
                // Scan direction per axis: +1 when the map centre lies at or beyond the HQ tile in that axis, else -1 (a point-symmetric map
                // then gives the two players exactly mirrored scans, so neither starts behind its base and the other in front).
                int sx = _view.Width / 2 >= hx ? 1 : -1;
                int sy = _view.Height / 2 >= hy ? 1 : -1;
                // Ring distance from the HQ tile. Unit: tiles. Range: build_ring_min_tiles to build_ring_max_tiles.
                for (int ring = _profile.BuildRingMinTiles; ring <= _profile.BuildRingMaxTiles; ring++)
                {
                    // Scan step along the row axis. Unit: tiles. Range: -ring to ring.
                    for (int stepY = -ring; stepY <= ring; stepY++)
                    {
                        // Row offset, flipped toward the map centre so both players scan from behind their base. Unit: tiles. Range: -ring to ring.
                        int dy = stepY * sy;
                        // Scan step along the column axis. Unit: tiles. Range: -ring to ring.
                        for (int stepX = -ring; stepX <= ring; stepX++)
                        {
                            // Column offset, flipped the same way. Unit: tiles. Range: -ring to ring.
                            int dx = stepX * sx;
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
                        // A tile the simulation refused as blocked and that the team cannot see now (an enemy structure in the fog).
                        if (!_view.IsVisible(x + nx, y + ny) && _memory.RejectedSites.Contains(_view.IndexOf(x + nx, y + ny)))
                        {
                            return false;
                        }
                    }
                }
                if (_view.IsOccupied(x, y) || _reservedSites.Contains(_view.IndexOf(x, y)) || _memory.UnreachableSites.Contains(_view.IndexOf(x, y)))
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

            /// <summary>True when <paramref name="point"/> is unsafe (skirmish_ai_v2.md B.3): near a known enemy structure, an uncleared enemy spawn or a recently seen enemy army.</summary>
            private bool IsUnsafe(FixVec2 point)
            {
                // Squared safety radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.ExpandSafeRadiusTiles * _profile.ExpandSafeRadiusTiles;
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    if ((known.Position - point).LengthSquared() <= radiusSquared)
                    {
                        return true;
                    }
                }
                // Spawn index. Range: 0 to spawn count - 1.
                for (int i = 0; i < _view.Spawns.Count; i++)
                {
                    if (i != _memory.OwnSpawnIndex && !_memory.ClearedSpawns.Contains(i) && (_view.Spawns[i] - point).LengthSquared() <= radiusSquared)
                    {
                        return true;
                    }
                }
                // Oldest tick at which a seen enemy army still counts. Unit: ticks.
                int armyFreshAfter = _view.Tick - _view.SecondsToTicks(_profile.EnemyArmyMemorySeconds);
                return _memory.HasLastSeenEnemy && _memory.LastSeenEnemyTick >= armyFreshAfter
                    && (_memory.LastSeenEnemyPosition - point).LengthSquared() <= radiusSquared;
            }

            /// <summary>
            /// Target order (skirmish_ai_v2.md B.3) from <paramref name="from"/>: fields with known resources (at least expand_min_field_remaining
            /// when any qualify), safe ones first, each group by squared distance from <paramref name="from"/> then field index; unsafe
            /// ones are listed only while starved.
            /// </summary>
            private List<AiFieldView> TargetOrder(FixVec2 from, bool rarePriority = false)
            {
                // Fields that may be targets.
                List<AiFieldView> pool = new List<AiFieldView>();
                foreach (AiFieldView field in _view.Fields)
                {
                    if (Known(field) >= _profile.ExpandMinFieldRemaining)
                    {
                        pool.Add(field);
                    }
                }
                if (pool.Count == 0)
                {
                    foreach (AiFieldView field in _view.Fields)
                    {
                        if (Known(field) > 0)
                        {
                            pool.Add(field);
                        }
                    }
                }
                // Pool fields that are safe. Unit: fields. Range: 0 to field count.
                List<AiFieldView> safe = new List<AiFieldView>();
                // Pool fields that are unsafe. Unit: fields. Range: 0 to field count.
                List<AiFieldView> unsafeFields = new List<AiFieldView>();
                foreach (AiFieldView field in pool)
                {
                    (IsUnsafe(field.Centre) ? unsafeFields : safe).Add(field);
                }
                // Rule RS: rare sites first inside each safety group (a stable key: rare flag, then distance, then index).
                Comparison<AiFieldView> order = (a, b) =>
                {
                    int byRare = rarePriority ? IsRareSite(b).CompareTo(IsRareSite(a)) : 0;
                    return byRare != 0 ? byRare : CompareByDistance(a, b, from);
                };
                safe.Sort(order);
                unsafeFields.Sort(order);
                if (Starved)
                {
                    safe.AddRange(unsafeFields);
                }
                return safe;
            }

            /// <summary>True for a rare field, or a field within field_max_distance_tiles of a field that holds known rare resources (rule RS).</summary>
            private bool IsRareSite(AiFieldView field)
            {
                foreach (AiFieldView other in _view.Fields)
                {
                    if (other.Kind == ResourceKind.Rare && Known(other) > 0 && (other.Index == field.Index || (other.Centre - field.Centre).LengthSquared() <= ReachSquared))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>True when some served field holds the rare resource. Unit: boolean.</summary>
            private bool RareServed()
            {
                foreach (AiFieldView field in _served)
                {
                    if (field.Kind == ResourceKind.Rare)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Rule RS (skirmish_ai_v3.md C.7, C2.5: it only reorders rule E's targets now): true when use_rare_priority is on, the mix weights of units with a rare cost sum to at least
            /// rare_need_weight_share x mix_resolution, and no served field holds the rare resource.
            /// </summary>
            private bool RarePriority()
            {
                if (!_profile.UseRarePriority || RareServed())
                {
                    return false;
                }
                // Mix weight of the units that cost rare. Unit: weight units. Range: 0 upward.
                int rareWeight = 0;
                foreach (KeyValuePair<string, int> pair in _composition)
                {
                    if (_view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? unit) && unit.CostRare > 0)
                    {
                        rareWeight += pair.Value;
                    }
                }
                return Fix64.FromInt(rareWeight) >= _profile.RareNeedWeightShare * Fix64.FromInt(_profile.MixResolution);
            }

            /// <summary>Orders two fields by squared distance from <paramref name="from"/>, then by index (a total order, so the sort is deterministic).</summary>
            private static int CompareByDistance(AiFieldView a, AiFieldView b, FixVec2 from)
            {
                // Squared distance of the first field from the origin point. Unit: squared tiles. Range: 0 upward.
                Fix64 da = (a.Centre - from).LengthSquared();
                // Squared distance of the second field from the origin point. Unit: squared tiles. Range: 0 upward.
                Fix64 db = (b.Centre - from).LengthSquared();
                int byDistance = da.CompareTo(db);
                return byDistance != 0 ? byDistance : a.Index.CompareTo(b.Index);
            }

            /// <summary>True when some own structure (complete or under construction) lists <paramref name="unitId"/> in its trains.</summary>
            private bool AnyOwnTrains(string unitId)
            {
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsStructure && Contains(unit.Definition.Economy.Trains, unitId))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Rule T (skirmish_ai_v3.md C.5, only when use_tech_on_demand): the mix unit with the largest weight that no own structure trains
            /// (ties to the ordinally lower ID) gets its producer built when its weight is at least tech_min_weight_share x mix_resolution and no
            /// own structure that trains a mix unit is under construction.
            /// </summary>
            public void Tech()
            {
                if (!_profile.UseTechOnDemand)
                {
                    return;
                }
                if (!TechProducer())
                {
                    TechPrerequisite();
                }
            }

            /// <summary>
            /// Rule T2 (walls_and_tech.md T.4), run when rule T built nothing: among composition units with weight at least tech_min_weight_share x
            /// mix_resolution (descending weight, then ordinal ID) the first whose producer exists but whose requirements are not met gets its first
            /// unmet requirement built, unless an own unit (complete, under construction or a pending order) already has that definition.
            /// </summary>
            private void TechPrerequisite()
            {
                // Composition units in the order T2 considers them.
                List<KeyValuePair<string, int>> ranked = new List<KeyValuePair<string, int>>(_composition);
                ranked.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
                foreach (KeyValuePair<string, int> pair in ranked)
                {
                    if (pair.Value <= 0 || Fix64.FromInt(pair.Value) < _profile.TechMinWeightShare * Fix64.FromInt(_profile.MixResolution))
                    {
                        continue;
                    }
                    // Definition of this composition unit and its first missing building.
                    UnitDefinition unit = _view.Content.Units[pair.Key];
                    string? missing = FirstUnmetRequirement(unit);
                    if (missing == null || !AnyOwnTrains(pair.Key))
                    {
                        continue;
                    }
                    foreach (AiUnitView planned in _planned)
                    {
                        if (string.Equals(planned.Definition.Id, missing, StringComparison.Ordinal))
                        {
                            return;
                        }
                    }
                    TryBuild(_view.Content.Units[missing]);
                    return;
                }
            }

            /// <summary>Rule T proper: builds the producer of the heaviest mix unit nothing trains yet. Returns true when a build intent was emitted.</summary>
            private bool TechProducer()
            {
                // The best unit nothing trains yet, and its weight. Unit: weight units. Range: 0 upward.
                string? top = null;
                int topWeight = 0;
                foreach (KeyValuePair<string, int> pair in _composition)
                {
                    if (pair.Value > topWeight && !AnyOwnTrains(pair.Key))
                    {
                        top = pair.Key;
                        topWeight = pair.Value;
                    }
                }
                if (top == null || Fix64.FromInt(topWeight) < _profile.TechMinWeightShare * Fix64.FromInt(_profile.MixResolution))
                {
                    return false;
                }
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.IsUnderConstruction && TrainsCompositionUnit(unit.Definition))
                    {
                        return false;
                    }
                }
                // The structure that trains the unit, or null when the faction has none.
                UnitDefinition? structure = ProducerStructureFor(top);
                return structure != null && TryBuild(structure, null, true);
            }

            /// <summary>Own drop-offs for rule SD: the Rooted HQ and every complete drop-off structure, ascending ID.</summary>
            private List<AiUnitView> DropOffs()
            {
                // The drop-offs found.
                List<AiUnitView> found = new List<AiUnitView>();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (!unit.IsUnderConstruction && ((unit.IsHq && unit.HqState == HqState.Rooted) || (unit.IsStructure && unit.Definition.Economy.IsDropOff)))
                    {
                        found.Add(unit);
                    }
                }
                return found;
            }

            /// <summary>
            /// Rule SD step 1 (C.6): records enemy activity at every own drop-off: a visible enemy army unit within defend_radius_tiles, or a
            /// recent hit on an own unit there. Activity with none in the previous raided_gap_seconds starts a raid (its tick is appended to the log).
            /// Forgets the logs of units that no longer exist (an uprooted HQ keeps its log).
            /// </summary>
            /// <param name="dropOffs">The own drop-offs, ascending ID.</param>
            private void UpdateRaids(List<AiUnitView> dropOffs)
            {
                // Squared defend radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                // Quiet time that separates two raids. Unit: ticks.
                int gapTicks = _view.SecondsToTicks(_profile.RaidedGapSeconds);
                // Entries kept per log. Unit: raids. Range: 2 to 21.
                int keep = _profile.StaticDefenceAfterRaids + 1;
                // Logged drop-offs that are gone.
                List<EntityId> gone = new List<EntityId>();
                foreach (EntityId id in _memory.RaidLog.Keys)
                {
                    // An HQ that is uprooted (relocating) is not a drop-off for now but keeps its raid history; only units that no longer exist are forgotten.
                    if (FindOwn(id) == null)
                    {
                        gone.Add(id);
                    }
                }
                foreach (EntityId id in gone)
                {
                    _memory.RaidLog.Remove(id);
                    _memory.LastRaidTick.Remove(id);
                    _memory.RaidPosition.Remove(id);
                }
                foreach (AiUnitView dropOff in dropOffs)
                {
                    // Sum of the raiders' X positions. Unit: tiles. Range: 0 to raiders x map width.
                    Fix64 sumX = Fix64.Zero;
                    // Sum of the raiders' Y positions. Unit: tiles. Range: 0 to raiders x map height.
                    Fix64 sumY = Fix64.Zero;
                    // Visible enemy army units within defend_radius_tiles of the drop-off. Unit: units. Range: 0 upward.
                    int raiders = 0;
                    // Raid strength of C2.4: their supply (plus 1 for a hit, added below). Unit: supply. Range: 0 upward.
                    int strength = 0;
                    foreach (AiUnitView enemy in _view.VisibleEnemies)
                    {
                        if (enemy.IsArmy && (enemy.Position - dropOff.Position).LengthSquared() <= radiusSquared)
                        {
                            sumX += enemy.Position.X;
                            sumY += enemy.Position.Y;
                            raiders++;
                            strength += _view.SupplyOf(enemy.Definition);
                        }
                    }
                    // Where the activity is, and whether there is any.
                    FixVec2 at = raiders > 0 ? new FixVec2(sumX / raiders, sumY / raiders) : dropOff.Position;
                    // Tick of the newest hit near the drop-off. Unit: ticks.
                    int newest = -1;
                    foreach (DamageReport report in _memory.RecentDamage.Values)
                    {
                        if ((report.Position - dropOff.Position).LengthSquared() <= radiusSquared && report.Tick > newest)
                        {
                            newest = report.Tick;
                            if (raiders == 0)
                            {
                                at = report.Position;
                            }
                        }
                    }
                    // A recent hit within the radius adds 1 to the strength (C2.4 step 1); raid_min_supply 1 is package C.
                    strength += newest >= 0 ? 1 : 0;
                    if (strength < _profile.RaidMinSupply)
                    {
                        continue;
                    }
                    if (!_memory.LastRaidTick.TryGetValue(dropOff.Id, out int last) || _view.Tick - last > gapTicks)
                    {
                        if (!_memory.RaidLog.TryGetValue(dropOff.Id, out List<int>? log))
                        {
                            log = new List<int>();
                            _memory.RaidLog.Add(dropOff.Id, log);
                        }
                        log.Add(_view.Tick);
                        if (log.Count > keep)
                        {
                            log.RemoveRange(0, log.Count - keep);
                        }
                    }
                    _memory.LastRaidTick[dropOff.Id] = _view.Tick;
                    _memory.RaidPosition[dropOff.Id] = at;
                }
            }

            /// <summary>Own static defences (complete or under construction) within field_max_distance_tiles of <paramref name="point"/>. Unit: structures.</summary>
            private int StaticDefencesNear(FixVec2 point)
            {
                // Matches found. Unit: structures.
                int count = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.Definition.Economy.IsStaticDefence && (unit.Position - point).LengthSquared() <= ReachSquared)
                    {
                        count++;
                    }
                }
                return count;
            }

            /// <summary>True when an own static defence is under construction anywhere.</summary>
            private bool StaticDefenceUnderConstruction()
            {
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.IsUnderConstruction && unit.Definition.Economy.IsStaticDefence)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>True when a complete own army unit or static defence has a weapon that can hit <paramref name="layer"/>.</summary>
            private bool OwnAnswers(MovementLayer layer)
            {
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (!unit.IsUnderConstruction && (unit.IsArmy || (unit.IsStructure && unit.Definition.Economy.IsStaticDefence))
                        && unit.Definition.Weapon != null && unit.Definition.Weapon.CanTarget(layer))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>True when some unit of the faction's pool can hit <paramref name="layer"/> and its rare cost is within the rare stock.</summary>
            private bool PoolAnswers(MovementLayer layer)
            {
                foreach (UnitDefinition unit in AiCounter.Pool(_view, _plan.FactionId))
                {
                    if (unit.Weapon != null && unit.Weapon.CanTarget(layer) && unit.CostRare <= _rare)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>The nearest enemy spawn to the own HQ (lower index on a tie), or the HQ position when the map has none.</summary>
            private FixVec2 EnemySpawn()
            {
                // Best spawn so far and its squared distance. Unit: tiles; squared tiles.
                FixVec2 best = _hq.Position;
                Fix64 bestSquared = Fix64.Zero;
                bool found = false;
                // Spawn index. Range: 0 to spawn count - 1.
                for (int i = 0; i < _view.Spawns.Count; i++)
                {
                    if (i == _memory.OwnSpawnIndex)
                    {
                        continue;
                    }
                    Fix64 squared = (_view.Spawns[i] - _hq.Position).LengthSquared();
                    if (!found || squared < bestSquared)
                    {
                        best = _view.Spawns[i];
                        bestSquared = squared;
                        found = true;
                    }
                }
                return best;
            }

            /// <summary>
            /// The faction static defence with the highest counter score against the enemy model (ties to the ordinally lower ID) among those
            /// whose weapon can hit every layer in <paramref name="layers"/>; null when none can.
            /// </summary>
            /// <param name="layers">The layers that triggered the build (empty: any defence).</param>
            private UnitDefinition? PickDefence(List<MovementLayer> layers)
            {
                // Best defence so far, and its score.
                UnitDefinition? best = null;
                Fix64 bestScore = Fix64.Zero;
                foreach (UnitDefinition defence in AiCounter.StaticDefences(_view, _plan.FactionId))
                {
                    // True when the weapon can hit every triggering layer.
                    bool fits = true;
                    foreach (MovementLayer layer in layers)
                    {
                        fits &= defence.Weapon!.CanTarget(layer);
                    }
                    if (!fits)
                    {
                        continue;
                    }
                    Fix64 score = _enemies.Count == 0 ? Fix64.Zero : AiCounter.Score(defence, _enemies, _profile, _view);
                    if (best == null || score > bestScore)
                    {
                        best = defence;
                        bestScore = score;
                    }
                }
                return best;
            }

            /// <summary>
            /// The static defence site (C.6 step 4): on Chebyshev rings static_defence_ring_min_tiles to static_defence_ring_max_tiles around
            /// <paramref name="origin"/> (scanned like rule 3's ring scan), the tile that passes <see cref="IsGoodSite"/> on the origin tile's
            /// height layer with the lowest squared distance to <paramref name="toward"/>; a later tile must score strictly lower.
            /// </summary>
            private bool TryFindDefenceSite(FixVec2 origin, FixVec2 toward, out int siteX, out int siteY)
            {
                siteX = 0;
                siteY = 0;
                // Origin tile and its height layer.
                int ox = origin.X.FloorToInt();
                // Origin tile row. Unit: tiles.
                int oy = origin.Y.FloorToInt();
                if (!_view.Contains(ox, oy))
                {
                    return false;
                }
                // Height layer of the origin tile; a site must be on the same layer. Unit: height level. Range: 0 to 255.
                byte baseHeight = _view.HeightAt(ox, oy);
                // Scan directions, as in rule 3.
                int sx = _view.Width / 2 >= ox ? 1 : -1;
                // Scan direction along the rows: +1 when the map centre lies at or below the origin tile, else -1. Unit: sign. Range: -1 or 1.
                int sy = _view.Height / 2 >= oy ? 1 : -1;
                // True once a site is found, and the squared distance of the best so far. Unit: squared tiles.
                bool found = false;
                Fix64 bestSquared = Fix64.Zero;
                // Ring distance from the origin tile. Unit: tiles. Range: static_defence_ring_min_tiles to static_defence_ring_max_tiles.
                for (int ring = _profile.StaticDefenceRingMinTiles; ring <= _profile.StaticDefenceRingMaxTiles; ring++)
                {
                    // Scan step along the row axis. Unit: tiles. Range: -ring to ring.
                    for (int stepY = -ring; stepY <= ring; stepY++)
                    {
                        // Scan step along the column axis. Unit: tiles. Range: -ring to ring.
                        for (int stepX = -ring; stepX <= ring; stepX++)
                        {
                            // Offsets flipped toward the map centre.
                            int dx = stepX * sx;
                            int dy = stepY * sy;
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !IsGoodSite(ox + dx, oy + dy, baseHeight))
                            {
                                continue;
                            }
                            Fix64 squared = (_view.CentreOf(ox + dx, oy + dy) - toward).LengthSquared();
                            if (!found || squared < bestSquared)
                            {
                                found = true;
                                bestSquared = squared;
                                siteX = ox + dx;
                                siteY = oy + dy;
                            }
                        }
                    }
                }
                return found;
            }

            /// <summary>
            /// C2.4 step 2: true when static_defence_max_value_share is 0 (off), or the value of own static defences (complete or under
            /// construction, anywhere) plus the one step 4 would build is at most that share of the value of the own complete army units
            /// (the scout included); also true when no defence fits (nothing would be built anyway).
            /// </summary>
            /// <param name="share">The cap as a share of the army value (static_defence_max_value_share, or threat_defence_max_value_share for SD-4); 0 or less means no cap. Unit: fraction. Range: 0 to 10.</param>
            private bool WithinValueCap(Fix64 share)
            {
                if (share <= Fix64.Zero)
                {
                    return true;
                }
                // The defence a raid build would pick.
                UnitDefinition? next = PickDefence(new List<MovementLayer>());
                if (next == null)
                {
                    return true;
                }
                // D (own defences) and A (own army) in resource units. Range: 0 upward.
                Fix64 defences = Fix64.Zero;
                Fix64 army = Fix64.Zero;
                foreach (AiUnitView unit in _planned)
                {
                    if (unit.IsStructure && unit.Definition.Economy.IsStaticDefence)
                    {
                        defences += AiCounter.Value(unit.Definition, _profile);
                    }
                    else if (unit.IsArmy && !unit.IsUnderConstruction)
                    {
                        army += AiCounter.Value(unit.Definition, _profile);
                    }
                }
                return defences + AiCounter.Value(next, _profile) <= share * army;
            }

            /// <summary>Picks a defence for <paramref name="layers"/> and builds it at the best site around <paramref name="origin"/>.</summary>
            private void BuildDefence(FixVec2 origin, FixVec2 toward, List<MovementLayer> layers)
            {
                // The defence to build, or null when none fits.
                UnitDefinition? defence = PickDefence(layers);
                if (defence != null && TryFindDefenceSite(origin, toward, out int x, out int y))
                {
                    _defenceOrdered |= TryBuild(defence, (x, y));
                }
            }

            /// <summary>
            /// Rule SD (skirmish_ai_v3.md C.6, only when use_static_defence): logs raids (step 1); builds one static defence beside a drop-off
            /// raided static_defence_after_raids times within raided_window_seconds (step 2), else one at the HQ against a layer of the model
            /// that nothing answers (step 3); at most static_defence_max_per_base per base, one under construction at a time, one order per think.
            /// </summary>
            public void StaticDefence()
            {
                if (!_profile.UseStaticDefence)
                {
                    return;
                }
                // Own drop-offs, ascending ID.
                List<AiUnitView> dropOffs = DropOffs();
                UpdateRaids(dropOffs);
                if (DefenceCap == 0 || StaticDefenceUnderConstruction())
                {
                    return;
                }
                // Oldest raid start that still counts. Unit: ticks.
                int windowStart = _view.Tick - _view.SecondsToTicks(_profile.RaidedWindowSeconds);
                foreach (AiUnitView dropOff in dropOffs)
                {
                    if (!_memory.RaidLog.TryGetValue(dropOff.Id, out List<int>? log))
                    {
                        continue;
                    }
                    // Raids in the window. Unit: raids.
                    int raids = 0;
                    foreach (int start in log)
                    {
                        raids += start >= windowStart ? 1 : 0;
                    }
                    if (raids >= _profile.StaticDefenceAfterRaids && StaticDefencesNear(dropOff.Position) < DefenceCap)
                    {
                        if (!WithinValueCap(_profile.StaticDefenceMaxValueShare))
                        {
                            // C2.4 step 2: the defences would outweigh the army; no build this think (step 3 is still tried).
                            break;
                        }
                        BuildDefence(dropOff.Position, _memory.RaidPosition[dropOff.Id], new List<MovementLayer>());
                        return;
                    }
                }
                // Step 3: layers of the model that nothing answers.
                List<MovementLayer> unanswered = new List<MovementLayer>();
                foreach (MovementLayer layer in new[] { MovementLayer.Ground, MovementLayer.Water, MovementLayer.Air })
                {
                    if (AiIntel.LayerSupply(_view, _model, layer) >= Fix64.FromInt(_profile.UnansweredLayerMinSupply) && !OwnAnswers(layer) && !PoolAnswers(layer))
                    {
                        unanswered.Add(layer);
                    }
                }
                if (unanswered.Count > 0 && StaticDefencesNear(_hq.Position) < DefenceCap)
                {
                    BuildDefence(_hq.Position, EnemySpawn(), unanswered);
                }
                // SD-4 (AI-P P2): only when no raid or unanswered-layer defence was ordered this think.
                if (_profile.UseThreatDefence && !_defenceOrdered)
                {
                    ThreatDefence();
                }
            }

            /// <summary>Rule E (skirmish_ai_v2.md B.5): builds a depot beside the first unserved field in target order when one is due.</summary>
            public void Expand()
            {
                // True while a seen threat blocks depots: any threat (expand_block_threat_supply 0) or one of at least that supply (AI-E E2.2). Unit: boolean. Range: true or false.
                bool blocked = _profile.ExpandBlockThreatSupply == 0 ? UnderThreat : _threatSupply >= _profile.ExpandBlockThreatSupply;
                if (_plan.DropOffBuildingId.Length == 0 || blocked || _liveDepots >= _profile.MaxLiveDepots || _depotUnderConstruction)
                {
                    return;
                }
                // Clock time from which the next depot is due. Unit: seconds.
                Fix64 dueSeconds = ExpandAtSeconds(_profile, _memory, _view.Tick) + _profile.ExpandIntervalSeconds * _memory.DepotsPlaced;
                // Known amount left in the served fields. Unit: resource units. Range: 0 upward.
                int servedKnown = 0;
                // Start amount of the served fields. Unit: resource units. Range: 0 upward.
                int servedStart = 0;
                foreach (AiFieldView field in _served)
                {
                    servedKnown += Known(field);
                    servedStart += field.StartAmount;
                }
                // True when the served fields are running low (starved counts as empty).
                bool low = Starved || Fix64.FromInt(servedKnown) < _profile.ExpandWhenServedRemainingFraction * Fix64.FromInt(servedStart);
                // True while the early-expansion gate (C2.5) is in force: no rare field is served. Unit: boolean.
                bool earlyGate = _profile.UseEarlyExpansion && !RareServed();
                // C2.5: while no rare field is served, the first rare depot is due at early_expansion_seconds (the other gates stay).
                if (earlyGate)
                {
                    dueSeconds = Fix64.Min(dueSeconds, EarlyExpansionSeconds(_profile, _memory, _view.Tick));
                }
                // True once the early gate has opened (its time has come), so the depot it allows goes to a rare site even when the mix's rare
                // share is below rare_need_weight_share (C2 review item 1). Unit: boolean.
                bool earlyLift = earlyGate && _view.Tick >= _view.SecondsToTicks(EarlyExpansionSeconds(_profile, _memory, _view.Tick));
                // Rule RS: a needed rare resource puts rare sites first in the target order.
                bool rarePriority = RarePriority() || earlyLift;
                // AI-E E2.1: workers (queued included) at expand_saturation_fraction of the uncapped target. Unit: boolean. Range: true or false.
                bool saturated = false;
                if (_profile.UseSaturationExpansion)
                {
                    // Workers wanted before the max_workers ceiling. Unit: workers. Range: 0 upward.
                    int uncapped = WantedWorkersUncapped();
                    saturated = uncapped > 0 && Fix64.FromInt(WorkersIncludingQueued()) >= _profile.ExpandSaturationFraction * Fix64.FromInt(uncapped);
                }
                if (_view.Tick < _view.SecondsToTicks(dueSeconds) && !low && !saturated)
                {
                    return;
                }
                // The first field in target order that is neither served nor known to have no depot site.
                AiFieldView? target = null;
                foreach (AiFieldView field in TargetOrder(_hq.Position, rarePriority))
                {
                    if (!_served.Contains(field) && !_memory.UnbuildableFields.Contains(field.Index))
                    {
                        target = field;
                        break;
                    }
                }
                if (target == null)
                {
                    return;
                }
                if (!TryFindDepotSite(target, out int x, out int y))
                {
                    _memory.UnbuildableFields.Add(target.Index);
                    return;
                }
                TryBuild(_view.Content.Units[_plan.DropOffBuildingId], (x, y));
            }

            /// <summary>
            /// The depot site beside <paramref name="target"/>: the tile on Chebyshev rings depot_ring_min_tiles to depot_ring_max_tiles that
            /// passes <see cref="IsGoodSite"/> on the target's height layer and has the lowest sum of squared distances to the live fields
            /// within field_max_distance_tiles of the target (a later tile must score strictly lower). False when none passes.
            /// </summary>
            private bool TryFindDepotSite(AiFieldView target, out int siteX, out int siteY)
            {
                // Height layer of the target field's tile; the depot must stand on it. Range: 0 to 7.
                byte baseHeight = _view.HeightAt(target.X, target.Y);
                // True once a depot site has been found. Unit: boolean. Range: true or false.
                bool found = false;
                // Score of the best site so far (sum of squared distances to the live fields near the target). Unit: squared tiles. Range: 0 upward.
                Fix64 bestScore = Fix64.Zero;
                siteX = 0;
                siteY = 0;
                // Scan direction per axis: +1 when the target field lies at or beyond the HQ tile in that axis, else -1 (mirrored for the
                // other player on a point-symmetric map).
                int sx = target.X >= _hq.TileX ? 1 : -1;
                int sy = target.Y >= _hq.TileY ? 1 : -1;
                // Ring distance from the target tile. Unit: tiles. Range: depot_ring_min_tiles to depot_ring_max_tiles.
                for (int ring = _profile.DepotRingMinTiles; ring <= _profile.DepotRingMaxTiles; ring++)
                {
                    // Scan step along the row axis. Unit: tiles. Range: -ring to ring.
                    for (int stepY = -ring; stepY <= ring; stepY++)
                    {
                        // Row offset, flipped toward the HQ side per the scan direction. Unit: tiles. Range: -ring to ring.
                        int dy = stepY * sy;
                        // Scan step along the column axis. Unit: tiles. Range: -ring to ring.
                        for (int stepX = -ring; stepX <= ring; stepX++)
                        {
                            // Column offset, flipped the same way. Unit: tiles. Range: -ring to ring.
                            int dx = stepX * sx;
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !IsGoodSite(target.X + dx, target.Y + dy, baseHeight))
                            {
                                continue;
                            }
                            // Centre of the candidate tile. Unit: tiles.
                            FixVec2 tileCentre = _view.CentreOf(target.X + dx, target.Y + dy);
                            // Sum of squared distances to the live fields near the target. Unit: squared tiles.
                            Fix64 score = Fix64.Zero;
                            foreach (AiFieldView field in _view.Fields)
                            {
                                if (Known(field) > 0 && (field.Centre - target.Centre).LengthSquared() <= ReachSquared)
                                {
                                    score += (field.Centre - tileCentre).LengthSquared();
                                }
                            }
                            if (!found || score < bestScore)
                            {
                                found = true;
                                bestScore = score;
                                siteX = target.X + dx;
                                siteY = target.Y + dy;
                            }
                        }
                    }
                }
                return found;
            }

            /// <summary>
            /// Workers wanted (rule W): workers_per_field per served common field plus rare_workers_per_field per served rare field, at
            /// least workers_per_field while any field has resources, at most max_workers. Unit: workers. Range: 0 to max_workers.
            /// </summary>
            public int WantedWorkers()
            {
                return Math.Min(_profile.MaxWorkers, WantedWorkersUncapped());
            }

            /// <summary>
            /// <see cref="WantedWorkers"/> before the max_workers ceiling: the sum of the served fields' caps, at least the cap of the first
            /// field in target order (workers_per_field without use_saturation_workers) while any field has resources. Unit: workers.
            /// </summary>
            private int WantedWorkersUncapped()
            {
                // Sum of the caps of the served fields. Unit: workers. Range: 0 upward.
                int target = 0;
                foreach (AiFieldView field in _served)
                {
                    target += CapOf(field);
                }
                if (_anyLive)
                {
                    // Floor: workers_per_field, or (AI-E E1.3) the cap of the first field in target order. Unit: workers. Range: 1 to 8, or a saturation cap (1 upward).
                    int floor = _profile.WorkersPerField;
                    if (_profile.UseSaturationWorkers)
                    {
                        // Fields in target order from the HQ. Unit: fields. Range: 0 to field count.
                        List<AiFieldView> order = TargetOrder(_hq.Position);
                        floor = order.Count > 0 ? CapOf(order[0]) : floor;
                    }
                    target = Math.Max(target, floor);
                }
                return target;
            }

            /// <summary>Own workers alive plus workers queued anywhere. Unit: workers. Range: 0 upward.</summary>
            private int WorkersIncludingQueued()
            {
                // Workers counted so far. Unit: workers. Range: 0 upward.
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
                return count;
            }

            /// <summary>Indices of the served fields, ascending (test hook through <see cref="Probe"/>).</summary>
            public List<int> ServedIndices()
            {
                // Indices copied from the served list. Unit: field index.
                List<int> indices = new List<int>();
                foreach (AiFieldView field in _served)
                {
                    indices.Add(field.Index);
                }
                return indices;
            }

            /// <summary>Worker cap of each served field in index order (test hook through <see cref="Probe"/>). Unit: workers.</summary>
            public List<int> ServedCaps()
            {
                // Caps copied from the served list. Unit: workers.
                List<int> caps = new List<int>();
                foreach (AiFieldView field in _served)
                {
                    caps.Add(CapOf(field));
                }
                return caps;
            }

            /// <summary>True when an own static defence is under construction or ordered (test hook through <see cref="Probe"/>).</summary>
            public bool StaticDefenceUnderConstructionNow => StaticDefenceUnderConstruction();

            /// <summary>Own static defences, pending orders included, near the HQ (test hook through <see cref="Probe"/>). Unit: structures.</summary>
            public int StaticDefencesAtHq => StaticDefencesNear(_hq.Position);

            /// <summary>Live depots of this think (test hook through <see cref="Probe"/>). Unit: depots.</summary>
            public int LiveDepots => _liveDepots;

            /// <summary>Rule 4: trains one worker at a time at the Rooted HQ up to the target.</summary>
            public void TrainWorkers()
            {
                if (_hq.HqState != HqState.Rooted || _hq.IsUnderConstruction || _hq.Queue.Count > 0 || _hq.Definition.Hq == null
                    || !string.Equals(_hq.Definition.Hq.WorkerUnitId, _plan.WorkerUnitId, StringComparison.Ordinal))
                {
                    return;
                }
                // Workers wanted. Unit: workers.
                int target = WantedWorkers();
                // Workers alive plus workers queued anywhere. Unit: workers.
                int count = WorkersIncludingQueued();
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

            /// <summary>The own unit with this ID, or null.</summary>
            private AiUnitView? FindOwn(EntityId id)
            {
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.Id == id)
                    {
                        return unit;
                    }
                }
                return null;
            }

            /// <summary>
            /// True when the army defends this think: the stance is already Defend, or this think's threat would start (or keep) Defend (the same
            /// test as <see cref="DefendIfThreatened"/>, which runs later in Army). Rule S reads it so no scout is taken on the first think of a Defend.
            /// </summary>
            private bool WillDefend => _memory.Stance == AiStance.Defend
                || (!_profile.UseMeasuredDefence && (_threatCount > 0 || _latestHit != null) && !(_memory.Stance == AiStance.Attack && _threatSupply < _profile.DefendRecallSupply));

            /// <summary>Rule 4c (skirmish_ai_v3.md B.2): keeps one scout on its route, assigns or trains one when it is due (see the class header).</summary>
            public void Scout()
            {
                if (!_profile.UseScouting)
                {
                    return;
                }
                // The scout's view entry, or null when there is none (or it died).
                AiUnitView? scout = _memory.ScoutId == EntityId.None ? null : FindOwn(_memory.ScoutId);
                if (_memory.ScoutId != EntityId.None && scout == null)
                {
                    // Step 1: the scout died.
                    _memory.ScoutLosses++;
                    _memory.ScoutId = EntityId.None;
                    _memory.ScoutBorrowed = false;
                    _memory.NextScoutTick = _view.Tick + _view.SecondsToTicks(_profile.ScoutRetrySeconds);
                }
                // True when the interval since the last round started has passed (or no round has started).
                bool roundDue = _memory.ScoutRoundStartTick < 0 || _view.Tick >= _memory.ScoutRoundStartTick + _view.SecondsToTicks(_profile.ScoutIntervalSeconds);
                // True when the route was (re)built or the waypoint changed this think, so the scout needs an order now.
                bool changed = false;
                if (scout != null)
                {
                    if (!_memory.ScoutBorrowed && _memory.ScoutWaypoint >= _memory.ScoutRoute.Count && roundDue
                        && !WillDefend && !_memory.AttackGroup.Contains(scout.Id))
                    {
                        // An own scout resting with the army (not defending, not in the attack group) is taken again for the next round.
                        StartRound();
                        changed = true;
                    }
                }
                else if (_view.Tick >= _view.SecondsToTicks(_profile.ScoutFirstSeconds) && _view.Tick >= _memory.NextScoutTick && roundDue)
                {
                    // Steps 2 to 4: a scout is wanted.
                    scout = PickScout(out bool borrowed);
                    if (scout == null)
                    {
                        TrainScout();
                        return;
                    }
                    _memory.ScoutId = scout.Id;
                    _memory.ScoutBorrowed = borrowed;
                    StartRound();
                    changed = true;
                }
                if (scout == null)
                {
                    return;
                }
                // Step 6: advance past waypoints the team can see.
                while (_memory.ScoutWaypoint < _memory.ScoutRoute.Count
                    && _view.IsVisible(_memory.ScoutRoute[_memory.ScoutWaypoint].X.FloorToInt(), _memory.ScoutRoute[_memory.ScoutWaypoint].Y.FloorToInt()))
                {
                    _memory.ScoutWaypoint++;
                    changed = true;
                }
                if (!changed && _memory.ScoutWaypoint < _memory.ScoutRoute.Count && !scout.HasMoveTarget && _memory.ScoutHasOrder
                    && _view.Tick > _memory.ScoutOrderEffectiveTick && scout.Position == _memory.ScoutOrderPosition)
                {
                    // The last order moved nothing (the waypoint cannot be reached): skip it, then go on to the next one.
                    _memory.ScoutWaypoint++;
                    changed = true;
                    while (_memory.ScoutWaypoint < _memory.ScoutRoute.Count
                        && _view.IsVisible(_memory.ScoutRoute[_memory.ScoutWaypoint].X.FloorToInt(), _memory.ScoutRoute[_memory.ScoutWaypoint].Y.FloorToInt()))
                    {
                        _memory.ScoutWaypoint++;
                    }
                }
                if (_memory.ScoutWaypoint < _memory.ScoutRoute.Count)
                {
                    if (changed || !scout.HasMoveTarget)
                    {
                        if (Emit(AiIntent.Move(scout.Id, Standable(_memory.ScoutRoute[_memory.ScoutWaypoint]))))
                        {
                            _memory.ScoutHasOrder = true;
                            _memory.ScoutOrderPosition = scout.Position;
                            _memory.ScoutOrderEffectiveTick = _view.Tick + _memory.CommandDelayTicks;
                        }
                    }
                }
                else if (_memory.ScoutBorrowed)
                {
                    // The borrowed flyer is released and rejoins the army.
                    _memory.ScoutId = EntityId.None;
                    _memory.ScoutBorrowed = false;
                }
                else if (changed)
                {
                    Emit(AiIntent.Move(scout.Id, Standable(Home())));
                }
            }

            /// <summary>Starts a scouting round: builds the route, points at waypoint 0 and stamps the round start.</summary>
            private void StartRound()
            {
                _memory.ScoutRoute.Clear();
                _memory.ScoutRoute.AddRange(AiIntel.BuildRoute(_view, _profile, _memory, _hq.Position));
                _memory.ScoutWaypoint = 0;
                _memory.ScoutHasOrder = false;
                _memory.ScoutRoundStartTick = _view.Tick;
            }

            /// <summary>
            /// Step 3: the unit to send scouting, or null: (a) while the army masses, an own air army unit costing at most scout_air_max_cost
            /// (lowest supply, then lowest ID) that is not in the attack group; (b) unless the army defends, the lowest-ID own unit of the plan's
            /// scout_unit outside the group.
            /// </summary>
            /// <param name="borrowed">True when branch (a) picked the unit (a borrowed flyer), false for branch (b).</param>
            private AiUnitView? PickScout(out bool borrowed)
            {
                // Best borrowed flyer so far, or null.
                AiUnitView? flyer = null;
                // The lowest-ID own scout unit, or null.
                AiUnitView? own = null;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (!unit.IsArmy || _memory.AttackGroup.Contains(unit.Id) || InPackageDRole(unit.Id))
                    {
                        continue;
                    }
                    if (own == null && !WillDefend && _plan.ScoutUnitId.Length > 0 && string.Equals(unit.Definition.Id, _plan.ScoutUnitId, StringComparison.Ordinal))
                    {
                        own = unit;
                    }
                    if (_memory.Stance == AiStance.Massing && !WillDefend && unit.Layer == MovementLayer.Air
                        && unit.Definition.CostCommon + unit.Definition.CostRare <= _profile.ScoutAirMaxCost
                        && (flyer == null || _view.SupplyOf(unit.Definition) < _view.SupplyOf(flyer.Definition)))
                    {
                        flyer = unit;
                    }
                }
                borrowed = flyer != null;
                return flyer ?? own;
            }

            /// <summary>True while the scout is away on its route: a scout is set and it is borrowed or its route is not finished. Such a unit is not an army unit.</summary>
            private bool IsActiveScout(EntityId id)
            {
                return id == _memory.ScoutId && id != EntityId.None && (_memory.ScoutBorrowed || _memory.ScoutWaypoint < _memory.ScoutRoute.Count);
            }

            /// <summary>Step 4: trains the plan's scout unit at the lowest-ID free producer; reserves its cost (rule R) when it cannot be paid.</summary>
            private void TrainScout()
            {
                if (_plan.ScoutUnitId.Length == 0 || _memory.ScoutLosses >= _profile.ScoutMaxLosses)
                {
                    return;
                }
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (string.Equals(unit.Definition.Id, _plan.ScoutUnitId, StringComparison.Ordinal) || Contains(unit.Queue, _plan.ScoutUnitId))
                    {
                        // One scout unit alive or queued is enough (an alive one outside the group is picked by PickScout).
                        return;
                    }
                }
                // The lowest-ID free producer that trains the scout unit, or null.
                AiUnitView? producer = FindFreeProducer(_plan.ScoutUnitId);
                if (producer == null)
                {
                    return;
                }
                // The scout unit type.
                UnitDefinition definition = _view.Content.Units[_plan.ScoutUnitId];
                if (!CanPay(definition))
                {
                    if (_income)
                    {
                        _reserveCommon += definition.CostCommon;
                        _reserveRare += definition.CostRare;
                    }
                    return;
                }
                if (HasSupply(definition) && Emit(AiIntent.Train(producer.Id, definition.Id)))
                {
                    Pay(definition);
                    _usedProducers.Add(producer.Id);
                }
            }

            /// <summary>Rule 5 (rule P of skirmish_ai_v2.md): buys the composition unit the mix needs most, one producer at a time.</summary>
            public void Produce()
            {
                // Own count per composition unit, queued included, in ordinal order. Unit: units.
                foreach (string id in _composition.Keys)
                {
                    _counts[id] = 0;
                }
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (_counts.ContainsKey(unit.Definition.Id) && !IsActiveScout(unit.Id))
                    {
                        _counts[unit.Definition.Id]++;
                    }
                    foreach (string queued in unit.Queue)
                    {
                        if (_counts.ContainsKey(queued))
                        {
                            _counts[queued]++;
                        }
                    }
                }
                // Upper bound on units bought this think (one per producer), a guard against a loop that never ends. Unit: units.
                int guard = _view.OwnUnits.Count + _composition.Count;
                // Units the bank cannot pay while the player has no income: they hold no reservation, so cheaper units may be bought instead (rule R).
                SortedSet<string> unaffordable = new SortedSet<string>(StringComparer.Ordinal);
                // Purchase number. Range: 0 to guard.
                for (int bought = 0; bought < guard; bought++)
                {
                    // Composition units some free producer can make and whose rare cost the stock can pay.
                    SortedSet<string> candidates = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, int> pair in _composition)
                    {
                        // Definition of this composition unit.
                        UnitDefinition candidate = _view.Content.Units[pair.Key];
                        if (pair.Value > 0 && !unaffordable.Contains(pair.Key) && (candidate.CostRare == 0 || candidate.CostRare <= _rare - _reserveRare) && FindFreeProducer(pair.Key) != null)
                        {
                            candidates.Add(pair.Key);
                        }
                    }
                    // The most needed candidate, or null when none.
                    string? choice = PickByDeficit(_composition, _counts, id => candidates.Contains(id));
                    if (choice == null)
                    {
                        return;
                    }
                    // The lowest-ID free producer of the choice (never null: the choice came from the candidates).
                    AiUnitView producer = FindFreeProducer(choice)!;
                    // Definition of the chosen unit.
                    UnitDefinition definition = _view.Content.Units[choice];
                    if (!CanPay(definition) || !HasSupply(definition))
                    {
                        // The head of the queue waits: hold its cost so cheaper units do not jump ahead (rule R: only while the player has income).
                        if (!_income)
                        {
                            unaffordable.Add(choice);
                            continue;
                        }
                        _reserveCommon += definition.CostCommon;
                        _reserveRare += definition.CostRare;
                        return;
                    }
                    if (!Emit(AiIntent.Train(producer.Id, choice)))
                    {
                        return;
                    }
                    Pay(definition);
                    _counts[choice]++;
                    _usedProducers.Add(producer.Id);
                }
            }

            /// <summary>True for a complete own structure that can train now: it has a trains list and an empty queue, and was not used this think.</summary>
            private bool IsFreeProducer(AiUnitView unit)
            {
                return unit.IsStructure && !unit.IsUnderConstruction && unit.Queue.Count == 0 && unit.Definition.Economy.Trains.Count > 0
                    && !_usedProducers.Contains(unit.Id);
            }

            /// <summary>
            /// True when every <c>requires</c> building of <paramref name="definition"/> is an own, complete unit (walls_and_tech.md T.4). Own units
            /// only; pending build orders do not count.
            /// </summary>
            private bool RequirementsMet(UnitDefinition definition)
            {
                return FirstUnmetRequirement(definition) == null;
            }

            /// <summary>The first <c>requires</c> entry of <paramref name="definition"/>, in file order, that no own complete unit satisfies, or null.</summary>
            private string? FirstUnmetRequirement(UnitDefinition definition)
            {
                foreach (string required in definition.Economy.Requires)
                {
                    // True once an own complete unit of the required definition is found.
                    bool met = false;
                    foreach (AiUnitView unit in _view.OwnUnits)
                    {
                        met |= !unit.IsUnderConstruction && string.Equals(unit.Definition.Id, required, StringComparison.Ordinal);
                    }
                    if (!met)
                    {
                        return required;
                    }
                }
                return null;
            }

            /// <summary>The lowest-ID free producer that lists <paramref name="unitId"/> in its trains, or null.</summary>
            private AiUnitView? FindFreeProducer(string unitId)
            {
                if (!RequirementsMet(_view.Content.Units[unitId]))
                {
                    return null;
                }
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (IsFreeProducer(unit) && Contains(unit.Definition.Economy.Trains, unitId))
                    {
                        return unit;
                    }
                }
                return null;
            }

            /// <summary>True when the structure trains at least one composition unit (weight above 0).</summary>
            private bool TrainsCompositionUnit(UnitDefinition structure)
            {
                foreach (KeyValuePair<string, int> pair in _composition)
                {
                    if (pair.Value > 0 && Contains(structure.Economy.Trains, pair.Key))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>Rule X of skirmish_ai_v2.md: when money piles up and every producer is busy, builds the producer of the most needed unit.</summary>
            public void ExtraProducer()
            {
                if (_threatCount > 0 || _latestHit != null || _common - _reserveCommon < _profile.ExtraProducerBankCommon)
                {
                    return;
                }
                // Own structures that train a composition unit, complete or under construction. Unit: structures. Range: 0 upward.
                int producers = 0;
                foreach (AiUnitView unit in _planned)
                {
                    if (!unit.IsStructure || !TrainsCompositionUnit(unit.Definition))
                    {
                        continue;
                    }
                    producers++;
                    if (unit.IsUnderConstruction || IsFreeProducer(unit))
                    {
                        return;
                    }
                }
                if (producers >= _profile.MaxProducers)
                {
                    return;
                }
                // The composition unit with the largest deficit over all composition units.
                string? needed = PickByDeficit(_composition, _counts, id => true);
                if (needed == null)
                {
                    return;
                }
                // The structure to build.
                UnitDefinition? structure = ProducerStructureFor(needed);
                if (structure != null)
                {
                    TryBuild(structure, null, true);
                }
            }

            /// <summary>The structure to build to be able to train <paramref name="unitId"/>: the first build order step that trains it, else the lowest ID structure of the faction that does, else null.</summary>
            private UnitDefinition? ProducerStructureFor(string unitId)
            {
                foreach (AiBuildStep step in _plan.BuildOrder)
                {
                    // Definition of this step's structure.
                    UnitDefinition stepDefinition = _view.Content.Units[step.StructureId];
                    if (Contains(stepDefinition.Economy.Trains, unitId))
                    {
                        return stepDefinition;
                    }
                }
                foreach (UnitDefinition candidate in _view.Content.Units.Values)
                {
                    if (string.Equals(candidate.FactionId, _plan.FactionId, StringComparison.Ordinal) && candidate.Economy.FootprintTiles > 0
                        && Contains(candidate.Economy.Trains, unitId))
                    {
                        return candidate;
                    }
                }
                return null;
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

            /// <summary>Rule 6: the army stance machine (rules D, G, K and M of skirmish_ai_v2.md), with the package D rules HR, D.5 and D.6 before the stance orders.</summary>
            public void Army()
            {
                // Army units in ascending ID order, and their total supply. Unit: supply.
                List<AiUnitView> armyAll = new List<AiUnitView>();
                // Total supply of the army. Unit: supply. Range: 0 upward.
                int armySupplyAll = 0;
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsArmy && !IsActiveScout(unit.Id))
                    {
                        armyAll.Add(unit);
                        armySupplyAll += _view.SupplyOf(unit.Definition);
                    }
                }
                _armyAll = armyAll;
                _kill = FindKillTarget();
                PrepareGroups(armyAll);
                Harass(armyAll, armySupplyAll);
                UnitControl(armyAll);
                // The army the stance orders go to: everyone except the harass, defend and fallen-back units (all of them when package D is off).
                // Total supply of the stance army (declared by the call below). Unit: supply. Range: 0 upward.
                List<AiUnitView> army = StanceArmy(armyAll, out int armySupply);
                if (_profile.UseMeasuredDefence)
                {
                    if (MeasuredDefence(armyAll))
                    {
                        return;
                    }
                    army = StanceArmy(armyAll, out armySupply);
                }
                else if (DefendIfThreatened(army))
                {
                    return;
                }
                if (_memory.Stance == AiStance.Defend)
                {
                    if (_view.Tick < _memory.LastThreatTick + _view.SecondsToTicks(_profile.DefendHoldSeconds))
                    {
                        // Rule D: hold Defend and order nothing until the hold time has passed.
                        return;
                    }
                    // Back to massing without a new threshold draw (rule D).
                    _memory.Stance = AiStance.Massing;
                    _memory.HasAttackTarget = false;
                    _memory.AttackGroup.Clear();
                }
                else if (_memory.Stance == AiStance.Retreat)
                {
                    EnterMassing(_memory, _profile, _rng, _view.Tick);
                }
                if (_memory.Stance == AiStance.Massing)
                {
                    Mass(army, armySupply);
                    return;
                }
                Attack(army);
            }

            /// <summary>Reads this think's threats (see <see cref="DefendIfThreatened"/>) into the threat fields; called once by the constructor.</summary>
            private void ScanThreat()
            {
                // Squared defend radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                foreach (AiUnitView enemy in _view.VisibleEnemies)
                {
                    if (enemy.IsArmy && IsInsideBase(enemy.Position, radiusSquared))
                    {
                        _threatSumX += enemy.Position.X;
                        _threatSumY += enemy.Position.Y;
                        _threatCount++;
                        _threatSupply += _view.SupplyOf(enemy.Definition);
                        _threats.Add(enemy);
                    }
                }
                foreach (DamageReport report in _memory.RecentDamage.Values)
                {
                    if (IsInsideBase(report.Position, radiusSquared) && (_latestHit == null || report.Tick > _latestHit.Tick))
                    {
                        _latestHit = report;
                    }
                }
            }

            /// <summary>
            /// Defend: returns true (and orders the army) when a threat inside the base calls for it, and records the tick as the last
            /// threat tick (rule D).
            /// </summary>
            private bool DefendIfThreatened(List<AiUnitView> army)
            {
                if (_threatCount == 0 && _latestHit == null)
                {
                    return false;
                }
                if (_memory.Stance == AiStance.Attack && _threatSupply < _profile.DefendRecallSupply)
                {
                    return false;
                }
                _memory.Stance = AiStance.Defend;
                _memory.HasAttackTarget = false;
                _memory.LastThreatTick = _view.Tick;
                // Where the army goes: the mean threat position, else the latest hit.
                FixVec2 centre = _threatCount > 0 ? new FixVec2(_threatSumX / _threatCount, _threatSumY / _threatCount) : _latestHit!.Position;
                // Defence goal, moved off a blocked tile. Unit: tiles.
                FixVec2 goal = Standable(centre);
                foreach (AiUnitView unit in army)
                {
                    if (Controlled(unit) || Focus(unit))
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

            /// <summary>
            /// Rule K: the known enemy HQ last seen at or below kill_hq_health_fraction of its health (lowest fraction, ties nearer the
            /// own HQ, then lower ID), or null.
            /// </summary>
            private KnownStructure? FindKillTarget()
            {
                // Best candidate so far, or null.
                KnownStructure? best = null;
                // Squared distance of the best candidate from home. Unit: squared tiles.
                Fix64 bestSquared = Fix64.Zero;
                // Oldest last-seen tick that still counts (review fix: stale health is not trusted). Unit: ticks. Range: below 0 upward.
                int freshAfter = _view.Tick - _view.SecondsToTicks(_profile.KillSeenWithinSeconds);
                foreach (KnownStructure known in _memory.KnownEnemyStructures.Values)
                {
                    if (!known.IsHq || known.HealthFraction > _profile.KillHqHealthFraction || known.LastSeenTick < freshAfter)
                    {
                        continue;
                    }
                    // Squared distance of this candidate. Unit: squared tiles. Range: 0 upward.
                    Fix64 squared = (known.Position - Home()).LengthSquared();
                    if (best == null || known.HealthFraction < best.HealthFraction || (known.HealthFraction == best.HealthFraction && squared < bestSquared))
                    {
                        best = known;
                        bestSquared = squared;
                    }
                }
                return best;
            }

            /// <summary>
            /// The home anchor of the army: where it rallies and retreats to (rule M): the relocation destination while the HQ relocates,
            /// else the HQ position. Unit: tiles.
            /// </summary>
            private FixVec2 Home()
            {
                return _memory.RelocationActive ? _memory.RelocationDestination : _hq.Position;
            }

            /// <summary>Massing: start the attack when ready (or at once for rule K), otherwise gather idle army units at the home rally point.</summary>
            private void Mass(List<AiUnitView> army, int armySupply)
            {
                // True when the tide window allows an attack, or the army is big enough to ignore it.
                bool tideOk = AllIn || (_view.TideLevel >= _plan.AttackTideMin && _view.TideLevel <= _plan.AttackTideMax) || armySupply >= _plan.AttackTideOverrideSupply;
                // Supply needed to attack now: the drawn threshold, or 1 when all in. Unit: supply.
                int threshold = AllIn ? 1 : _memory.AttackThreshold;
                // True when rule K starts the attack: a low enemy HQ is known and the army is big enough (threshold, tide and regroup ignored).
                bool kill = _kill != null && army.Count > 0 && armySupply >= _profile.KillMinArmySupply;
                // True when the army is strong enough by the v2 rule (the drawn threshold) or, with use_predictor_attack and something known about the enemy, by the predictor (D.1).
                bool strong = armySupply >= threshold;
                if (_profile.UsePredictorAttack && !AllIn && army.Count > 0 && PredictedAttack(army, armySupply, out bool predictedWin))
                {
                    strong = predictedWin;
                }
                // True when the normal attack conditions hold.
                bool ready = army.Count > 0 && _view.Tick >= _memory.RegroupUntilTick && strong && tideOk;
                if ((kill || ready) && TryChooseTarget(out _))
                {
                    // Rule G: the group is every army unit now.
                    _memory.AttackGroup.Clear();
                    foreach (AiUnitView unit in army)
                    {
                        _memory.AttackGroup.Add(unit.Id);
                    }
                    _memory.Stance = AiStance.Attack;
                    _memory.AttackStartSupply = armySupply;
                    _memory.AttackStartTick = _view.Tick;
                    _memory.AttackStaged = kill || _profile.StageDistanceTiles <= Fix64.Zero;
                    _memory.AttacksStarted++;
                    _memory.HasAttackTarget = false;
                    Attack(army);
                    return;
                }
                RallyIdle(army);
            }

            /// <summary>Rule M: idle army units more than 3 tiles from the home rally point attack-move there.</summary>
            private void RallyIdle(List<AiUnitView> units)
            {
                // Where the waiting units gather. Unit: tiles.
                FixVec2 rally = RallyPoint();
                foreach (AiUnitView unit in units)
                {
                    if (!Controlled(unit) && !unit.HasMoveTarget && !unit.IsAttackMoving && (unit.Position - rally).LengthSquared() > AtRallySquared)
                    {
                        Emit(AiIntent.AttackMove(unit.Id, rally));
                    }
                }
            }

            /// <summary>
            /// Attack (rule G): only the attack group is ordered; retreat on the group's losses, otherwise the group attack-moves to the
            /// current target and army units outside the group rally at home.
            /// </summary>
            private void Attack(List<AiUnitView> army)
            {
                // True when every army unit joins the group each think (all in, or rule K).
                bool joinAll = AllIn || _kill != null;
                // Review fix: drop group IDs that are no longer army units so the set cannot grow during a long attack.
                // IDs of the army units, so the prune below costs one lookup per group member. Unit: unit IDs.
                SortedSet<EntityId> armyIds = new SortedSet<EntityId>();
                foreach (AiUnitView unit in army)
                {
                    armyIds.Add(unit.Id);
                }
                _memory.AttackGroup.RemoveWhere(id => !armyIds.Contains(id));
                if (_kill != null)
                {
                    // Rule K skips staging, also when it begins during an attack that is still staging.
                    _memory.AttackStaged = true;
                }
                // Living members of the attack group, and their total supply. Unit: supply.
                List<AiUnitView> group = new List<AiUnitView>();
                // Supply of the living group. Unit: supply. Range: 0 upward.
                int groupSupply = 0;
                // Army units outside the group (trained after the attack began).
                List<AiUnitView> rest = new List<AiUnitView>();
                foreach (AiUnitView unit in army)
                {
                    if (joinAll)
                    {
                        _memory.AttackGroup.Add(unit.Id);
                    }
                    if (_memory.AttackGroup.Contains(unit.Id))
                    {
                        group.Add(unit);
                        groupSupply += _view.SupplyOf(unit.Definition);
                    }
                    else
                    {
                        rest.Add(unit);
                    }
                }
                if (group.Count == 0)
                {
                    EnterMassing(_memory, _profile, _rng, _view.Tick);
                    return;
                }
                if (!AllIn && _kill == null && ShouldRetreat(group, groupSupply))
                {
                    _memory.Retreats++;
                    _memory.Stance = AiStance.Retreat;
                    // Retreat destination: home, moved off a blocked tile. Unit: tiles.
                    FixVec2 home = Standable(Home());
                    foreach (AiUnitView unit in group)
                    {
                        Emit(AiIntent.Move(unit.Id, home));
                    }
                    _memory.RegroupUntilTick = _view.Tick + _view.SecondsToTicks(_profile.RegroupSeconds);
                    EnterMassing(_memory, _profile, _rng, _view.Tick);
                    return;
                }
                if (!TryChooseTarget(out FixVec2 target))
                {
                    // Nothing known left to attack: go home and mass again.
                    EnterMassing(_memory, _profile, _rng, _view.Tick);
                    return;
                }
                RallyIdle(rest);
                // True when the attack switched to a new target this think.
                bool changed = !_memory.HasAttackTarget || (target - _memory.AttackTarget).LengthSquared() > SameGoalSquared;
                _memory.HasAttackTarget = true;
                _memory.AttackTarget = target;
                if (!_memory.AttackStaged)
                {
                    if (!Stage(group, groupSupply, target))
                    {
                        return;
                    }
                    changed = true;
                }
                // A visible, undefended enemy HQ to focus, or null.
                AiUnitView? siege = FindSiegeTarget();
                // Squared engagement radius. Unit: squared tiles.
                Fix64 radiusSquared = _profile.EngagementRadiusTiles * _profile.EngagementRadiusTiles;
                foreach (AiUnitView unit in group)
                {
                    if (Controlled(unit) || Focus(unit))
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
            /// Staging: gathers the group at the staging point (the Core centre when rally_at_core, the map has a Core and the own team
            /// does not hold it; otherwise stage_distance_tiles short of <paramref name="target"/> toward home). Returns true (and marks
            /// the attack staged) once stage_gather_fraction of the group supply stands within stage_radius_tiles of that point or
            /// stage_timeout_seconds have passed since the attack started; otherwise orders stragglers there and returns false.
            /// </summary>
            private bool Stage(List<AiUnitView> army, int armySupply, FixVec2 target)
            {
                // Staging point, moved off a blocked tile. Unit: tiles.
                FixVec2 stage = _profile.RallyAtCore && _view.HasCore && !_view.OwnTeamHoldsCore
                    ? Standable(_view.CoreCentre)
                    : Standable(target.MoveTowards(Home(), _profile.StageDistanceTiles));
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
                    if (Controlled(unit) || Focus(unit))
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
                if (_profile.UseFocusFire)
                {
                    return FocusFastestKill(unit);
                }
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
                if (_profile.UsePredictorAttack)
                {
                    // D.2 review fix: an attack that has just started (and has lost nothing) is not recalled by the first visible enemies.
                    bool fresh = _view.Tick < _memory.AttackStartTick + _view.SecondsToTicks(_profile.AttackRetreatGraceSeconds) && armySupply >= _memory.AttackStartSupply;
                    return !fresh && PredictedRetreat(army);
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
                    if (foe.Definition.Weapon == null || foe.IsWorker)
                    {
                        // Unarmed units and workers (armed since P-003, but they flee or gather rather than fight) are not army power.
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
            /// The attack target: the kill target of rule K when there is one, else the known enemy HQ nearest the own HQ, else the nearest known enemy structure, else the nearest
            /// enemy spawn not yet seen empty, else the last seen enemy army position. False when none exists.
            /// </summary>
            private bool TryChooseTarget(out FixVec2 target)
            {
                return TryChooseTarget(out target, out _);
            }

            /// <summary>
            /// <see cref="TryChooseTarget(out FixVec2)"/> that also says whether the target is an undefended structure chosen by D.3
            /// (<paramref name="undefended"/>), which the attack decision D.1 weighs with undefended_army_share.
            /// </summary>
            private bool TryChooseTarget(out FixVec2 target, out bool undefended)
            {
                undefended = false;
                if (_kill != null)
                {
                    // Rule K: a known low enemy HQ comes before everything else.
                    target = Standable(_kill.Position);
                    return true;
                }
                if (_profile.UseTargetChoice && TryUndefendedTarget(out target))
                {
                    undefended = true;
                    return true;
                }
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

            /// <summary>
            /// Rule M: where the massing army waits: rally_distance_tiles from home toward the attack target, or home itself when there
            /// is no target. Never the Core (rally_at_core moves only the attack group's staging point).
            /// </summary>
            private FixVec2 RallyPoint()
            {
                if (!TryChooseTarget(out FixVec2 toward))
                {
                    return Standable(Home());
                }
                return Standable(Home().MoveTowards(toward, _profile.RallyDistanceTiles));
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
