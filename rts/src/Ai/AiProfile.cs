// rts/src/Ai/AiProfile.cs
// A validated skirmish AI profile (rts/data/ai/<id>.json): the decision timing, economy targets, build placement, attack,
// retreat and defence numbers, and one plan per faction (worker, supply building, build order, army composition, tide window).
// Part of Rts.Ai. DataLoader routes the ai/ folder and keeps each file's parsed JsonValue in DataSet.AiProfiles (checking only
// the header and that "id" matches the file name); AiProfile.Parse validates everything else here with the same JsonValue
// Require* helpers, so every error is a DataValidationException naming the file and the field, for example
// "ai/standard.json: field 'attack_army_supply' is missing". Decimals are Fix64 (parsed exactly), never floating point.
// Differences from rts/docs/specs/skirmish_ai.md section 2 (written before the economy, HQ and vision code existed):
//   * the expansion keys now exist (skirmish_ai_v2.md package B): expand_at_seconds, expand_interval_seconds,
//     expand_when_served_remaining_fraction, max_live_depots, expand_min_field_remaining, expand_safe_radius_tiles,
//     enemy_army_memory_seconds, depot_ring_min_tiles, depot_ring_max_tiles, rebalance_workers_per_think and the per-faction optional
//     drop_off_building; the HQ relocation keys relocate_when_home_remaining_fraction, relocate_stand_off_tiles,
//     relocate_timeout_seconds and relocate_cooldown_seconds; and rare_workers_per_field. max_bases and base_site_taken_radius_tiles
//     of skirmish_ai.md stay unused (the depot rules replace them);
//   * added keys for numbers the spec hard-coded or left implicit: field_max_distance_tiles, build_ring_min_tiles,
//     build_ring_max_tiles, field_clearance_tiles, rally_distance_tiles and damage_memory_seconds; the attack staging keys
//     stage_distance_tiles, stage_radius_tiles, stage_gather_fraction and stage_timeout_seconds (the army gathers short of the
//     target so fast and slow units arrive together instead of trickling into the defence); focus_radius_tiles (focus fire on
//     visible enemy army units); and all_in_when_mined_out
//     (Phase 1 maps hold only the main fields, so a mined-out AI attacks with what it has instead of stalling the match);
//   * Phase 2 (planetary_cycles.md section 5): the root key rally_at_core (the massing army gathers at the Core while the AI does not
//     hold it) and, per faction, cycle_preference (every cycle ID, most wanted first; the AI picks the first allowed one when its team
//     holds the Core);
//   * skirmish_ai_v2.md package C (army): defend_hold_seconds, kill_hq_health_fraction, kill_seen_within_seconds, kill_min_army_supply, extra_producer_bank_common
//     and max_producers; rally_at_core now moves the attack group's staging point to the Core (the massing army rallies at home).
//     field_max_distance_tiles now means the distance from an own drop-off (not the HQ) and all_in_when_mined_out means "no field on the map
//     has known resources" (package B).
//   * skirmish_ai_v3.md package B (know the enemy): the switches use_scouting and use_enemy_model (and use_counter_mix, parsed now so its
//     dependency on use_enemy_model is validated; package C reads it), the scouting keys scout_first_seconds, scout_interval_seconds,
//     scout_retry_seconds, scout_max_losses, scout_route_max_waypoints, scout_merge_tiles and scout_air_max_cost, the enemy model keys
//     enemy_model_hold_seconds, enemy_model_decay_seconds, enemy_model_floor_fraction and enemy_model_implied_count, and the per-faction
//     optional scout_unit.
//   * skirmish_ai_v3.md package C (build the right army): the switches use_tech_on_demand, use_static_defence and use_rare_priority (each
//     needs use_enemy_model; the first and the last also need use_counter_mix), the predictor_* keys (fight predictor), the counter_* keys
//     and mix_resolution (counter-mix), tech_min_weight_share, raided_window_seconds, raided_gap_seconds, static_defence_after_raids,
//     static_defence_max_per_base, static_defence_ring_min_tiles, static_defence_ring_max_tiles, unanswered_layer_min_supply and
//     rare_need_weight_share.
//   * skirmish_ai_v3.md package C2 (counter-mix revision): use_predictor_mix (needs use_counter_mix), mix_eval_budget_common,
//     mix_rare_allowance, rare_bank_soft_cap, raid_min_supply, static_defence_max_value_share, use_early_expansion and
//     early_expansion_seconds (every profile file carries them; the "off" values reproduce package C).
//   * skirmish_ai_v3.md package D (choose fights): the switches use_predictor_attack (needs use_enemy_model), use_target_choice,
//     use_measured_defence and use_unit_control, and the keys attack_min_advantage, attack_min_supply, attack_force_supply,
//     undefended_army_share, target_defence_radius_tiles, retreat_max_advantage, harass_* (min army supply, min speed, squad supply,
//     cooldown, linger, max commands), defend_min_advantage, defend_blind_supply, defend_max_candidates, fallback_* (health fraction,
//     danger tiles, max commands), kite_* (trigger, step, min range gap, min speed ratio, max commands), focus_switch_ratio and
//     focus_max_commands_per_think (every profile file carries them; the switches are false except in aggressive_s3); and the package D
//     review keys use_focus_fire (fastest-kill focus split from use_unit_control), fallback_release_seconds, attack_retreat_grace_seconds,
//     harass_max_seconds and rare_bank_return_cap (hysteresis of the rare-worker cap).
//   * skirmish_ai_v4_rl.md package R (seeded variety): composition_jitter_fraction, expand_jitter_seconds and attack_advantage_jitter
//     (every profile file carries them; 0 = off, which plays exactly as before; see AiVariety).
//   * field_saturation.md package AI-E (the economy follows gather slots): use_saturation_workers, saturation_extra_workers_per_field,
//     rare_cap_yields_to_saturation, use_saturation_expansion, expand_saturation_fraction and expand_block_threat_supply (every profile
//     file carries them; the old values false, 0, false, false, 1, 0 reproduce the earlier rules exactly).
//   * field_saturation.md package AI-P (placement and defence): use_forward_producers, use_threat_defence, threat_defence_min_enemy_supply,
//     threat_defence_min_enemy_advantage, threat_defence_max_value_share and threat_defence_release_fraction (every profile file carries
//     them; the old values false, false, 0, 0, 0, 1 reproduce the earlier rules exactly; use_threat_defence needs use_static_defence).
//   * field_saturation.md package AI-H (faction-fair harass speed): harass_speed_ratio (every profile file carries it; the old value 0 keeps the
//     fixed harass_min_speed_tiles_per_second and the flyer exemption).
//   * skirmish_ai_v4_rl.md package L (opening library): each faction plan carries "openings" (0 to 16 entries, [] = none), each with id, weight,
//     build_order, composition, handover_seconds, expand_at_seconds, early_expansion_seconds, attack_army_supply and attack_min_supply; one is
//     drawn at match start (AiVariety). Validated here: unique IDs, a weight sum above 0, and that every unit with a requires list has its
//     required structures in the opening's build order.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>One build order step: keep at least <see cref="Count"/> of a structure once supply used reaches a threshold.</summary>
    public sealed class AiBuildStep
    {
        /// <summary>Definition ID of the structure, for example "skai.roost". A structure (footprint above 0) of the plan's faction.</summary>
        public string StructureId { get; }

        /// <summary>How many of the structure the AI keeps (built or under construction). Unit: structures. Range: 1 to 20.</summary>
        public int Count { get; }

        /// <summary>Supply used at or above which the step starts. Unit: supply. Range: 0 to 200.</summary>
        public int AtSupplyUsed { get; }

        /// <summary>Stores the step.</summary>
        /// <param name="structureId">Structure definition ID.</param>
        /// <param name="count">Structures to keep. Range: 1 to 20.</param>
        /// <param name="atSupplyUsed">Supply threshold. Unit: supply. Range: 0 to 200.</param>
        public AiBuildStep(string structureId, int count, int atSupplyUsed)
        {
            StructureId = structureId;
            Count = count;
            AtSupplyUsed = atSupplyUsed;
        }
    }

    /// <summary>One opening of a faction plan (skirmish_ai_v4_rl.md L.2): a build order for the whole game and an army mix and timing values until the handover.</summary>
    public sealed class AiOpening
    {
        /// <summary>Name in reports and records. Range: lower-case letters, digits and underscores, 1 to 48 characters; unique within the plan.</summary>
        public string Id { get; }

        /// <summary>Chance of being drawn = weight / sum of the plan's weights; 0 keeps the opening in the file without drawing it. Unit: weight. Range: 0 to 100.</summary>
        public int Weight { get; }

        /// <summary>Replaces the plan's build order for the whole game. Range: 1 to 20 steps.</summary>
        public IReadOnlyList<AiBuildStep> BuildOrder { get; }

        /// <summary>The base army mix until the handover. Unit: weight by unit ID, ordinal order. Range: 0 to 100 each, at least one above 0.</summary>
        public IReadOnlyDictionary<string, int> Composition { get; }

        /// <summary>Clock time at which this opening's composition and timing values give way to the plan's. Unit: seconds. Range: 0 to 3600.</summary>
        public int HandoverSeconds { get; }

        /// <summary>Replaces expand_at_seconds until the handover. Unit: seconds. Range: 0 to 1800.</summary>
        public Fix64 ExpandAtSeconds { get; }

        /// <summary>Replaces early_expansion_seconds until the handover. Unit: seconds. Range: 0 to 1800.</summary>
        public Fix64 EarlyExpansionSeconds { get; }

        /// <summary>Replaces attack_army_supply until the handover (the fallback attack threshold). Unit: supply. Range: 1 to 200.</summary>
        public int AttackArmySupply { get; }

        /// <summary>Replaces attack_min_supply until the handover. Unit: supply. Range: 1 to 200.</summary>
        public int AttackMinSupply { get; }

        /// <summary>Stores the opening; <see cref="AiProfile.Parse(JsonValue, ContentCatalog)"/> validates it first.</summary>
        public AiOpening(string id, int weight, IReadOnlyList<AiBuildStep> buildOrder, IReadOnlyDictionary<string, int> composition, int handoverSeconds,
            Fix64 expandAtSeconds, Fix64 earlyExpansionSeconds, int attackArmySupply, int attackMinSupply)
        {
            Id = id;
            Weight = weight;
            BuildOrder = buildOrder;
            Composition = composition;
            HandoverSeconds = handoverSeconds;
            ExpandAtSeconds = expandAtSeconds;
            EarlyExpansionSeconds = earlyExpansionSeconds;
            AttackArmySupply = attackArmySupply;
            AttackMinSupply = attackMinSupply;
        }
    }

    /// <summary>The per-faction part of a profile.</summary>
    public sealed class AiFactionPlan
    {
        /// <summary>Faction this plan is for, for example "skai".</summary>
        public string FactionId { get; }

        /// <summary>Worker unit the HQ trains; must have can_gather. Range: a unit ID of the faction.</summary>
        public string WorkerUnitId { get; }

        /// <summary>Structure built when free supply runs low; must provide supply. Range: a structure ID of the faction.</summary>
        public string SupplyBuildingId { get; }

        /// <summary>Build order steps in priority order; only the first unmet step is considered (strict order).</summary>
        public IReadOnlyList<AiBuildStep> BuildOrder { get; }

        /// <summary>Army mix as integer weights by unit ID, in ordinal ID order. Unit: weight. Range: 0 to 100 each, at least one above 0.</summary>
        public IReadOnlyDictionary<string, int> Composition { get; }

        /// <summary>Lowest tide level at which the AI starts an attack. Unit: fraction (0 low tide, 1 high tide). Range: 0 to 1.</summary>
        public Fix64 AttackTideMin { get; }

        /// <summary>Highest tide level at which the AI starts an attack. Unit: fraction. Range: AttackTideMin to 1.</summary>
        public Fix64 AttackTideMax { get; }

        /// <summary>Army supply at or above which the tide window is ignored. Unit: supply. Range: 1 to 400.</summary>
        public int AttackTideOverrideSupply { get; }

        /// <summary>
        /// Planetary Cycle IDs, most wanted first, that the AI picks from when its team holds the Core. Range: distinct cycle IDs; every
        /// cycle of the data set when the profile was parsed with the cycle list; may be empty for a plan built by hand (the AI then
        /// picks the first allowed cycle).
        /// </summary>
        public IReadOnlyList<string> CyclePreference { get; }

        /// <summary>
        /// Drop-off structure the AI builds at a new base (skirmish_ai_v2.md B.1), for example "skai.perch_depot". Range: empty (the plan
        /// builds no depots; the AI still relocates its HQ), or a structure of the faction with footprint 1 and is_drop_off.
        /// </summary>
        public string DropOffBuildingId { get; }

        /// <summary>
        /// Unit the AI trains as its scout (skirmish_ai_v3.md B.1), for example "skai.windrunner". Range: empty (no scout is trained; flyers
        /// may still be borrowed), or a mobile unit of the faction that is not a worker or HQ and is in some structure's trains list.
        /// </summary>
        public string ScoutUnitId { get; }

        /// <summary>The openings one of which is drawn at match start (skirmish_ai_v4_rl.md package L), in file order. Range: 0 to 16 entries; empty = no openings.</summary>
        public IReadOnlyList<AiOpening> Openings { get; }

        /// <summary>Stores the plan; <see cref="AiProfile.Parse(JsonValue, ContentCatalog)"/> validates it first.</summary>
        public AiFactionPlan(string factionId, string workerUnitId, string supplyBuildingId, IReadOnlyList<AiBuildStep> buildOrder,
            IReadOnlyDictionary<string, int> composition, Fix64 attackTideMin, Fix64 attackTideMax, int attackTideOverrideSupply,
            IReadOnlyList<string>? cyclePreference = null, string? dropOffBuildingId = null, string? scoutUnitId = null,
            IReadOnlyList<AiOpening>? openings = null)
        {
            Openings = openings ?? Array.Empty<AiOpening>();
            ScoutUnitId = scoutUnitId ?? string.Empty;
            DropOffBuildingId = dropOffBuildingId ?? string.Empty;
            CyclePreference = cyclePreference ?? Array.Empty<string>();
            FactionId = factionId;
            WorkerUnitId = workerUnitId;
            SupplyBuildingId = supplyBuildingId;
            BuildOrder = buildOrder;
            Composition = composition;
            AttackTideMin = attackTideMin;
            AttackTideMax = attackTideMax;
            AttackTideOverrideSupply = attackTideOverrideSupply;
        }

        /// <summary>A copy of this plan with another build order and army composition and every other field the same (used by <see cref="AiVariety"/>).</summary>
        /// <param name="buildOrder">The build order of the copy.</param>
        /// <param name="composition">The composition of the copy. Unit: weight. Range: 0 to 100 each.</param>
        public AiFactionPlan WithOrders(IReadOnlyList<AiBuildStep> buildOrder, IReadOnlyDictionary<string, int> composition)
        {
            return new AiFactionPlan(FactionId, WorkerUnitId, SupplyBuildingId, buildOrder, composition, AttackTideMin, AttackTideMax,
                AttackTideOverrideSupply, CyclePreference, DropOffBuildingId, ScoutUnitId, Openings);
        }
    }

    /// <summary>A validated AI profile. Immutable; one instance may be shared by several controllers.</summary>
    public sealed class AiProfile
    {
        /// <summary>Profile ID, equal to the file name, for example "aggressive".</summary>
        public string Id { get; }

        /// <summary>Data-root-relative file the profile came from, for error messages, for example "ai/aggressive.json".</summary>
        public string SourceName { get; }

        /// <summary>Time between decisions. Unit: seconds. Range: 0.1 to 5.</summary>
        public Fix64 ThinkIntervalSeconds { get; }

        /// <summary>Most commands one decision may submit; later intents are dropped. Unit: commands. Range: 1 to 200.</summary>
        public int MaxCommandsPerThink { get; }

        /// <summary>Workers sent to one served common resource field before the next field is used (skirmish_ai_v2.md B.1: common fields only). Unit: workers. Range: 1 to 8.</summary>
        public int WorkersPerField { get; }

        /// <summary>Worker count the AI stops training at. Unit: workers. Range: 1 to 100.</summary>
        public int MaxWorkers { get; }

        /// <summary>A field is served (worked from a drop-off) when its centre is this close to an own drop-off: the Rooted HQ, a complete depot or a depot under construction. Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 FieldMaxDistanceTiles { get; }

        /// <summary>Build supply when free supply is at or below this. Unit: supply. Range: 0 to 40.</summary>
        public int SupplyBuffer { get; }

        /// <summary>Wait before re-trying a structure whose last attempt did not appear. Unit: seconds. Range: 1 to 60.</summary>
        public Fix64 BuildRetrySeconds { get; }

        /// <summary>Innermost Chebyshev ring around the HQ tile searched for a building site. Unit: tiles. Range: 1 to 20.</summary>
        public int BuildRingMinTiles { get; }

        /// <summary>Outermost Chebyshev ring around the HQ tile searched for a building site. Unit: tiles. Range: build_ring_min_tiles to 30.</summary>
        public int BuildRingMaxTiles { get; }

        /// <summary>No structure is placed within this Chebyshev distance of a resource field tile. Unit: tiles. Range: 0 to 10.</summary>
        public int FieldClearanceTiles { get; }

        /// <summary>Massing army waits this far from the HQ toward the enemy spawn. Unit: tiles. Range: 0 to 20.</summary>
        public Fix64 RallyDistanceTiles { get; }

        /// <summary>Army supply at which the AI attacks, before jitter. Unit: supply. Range: 1 to 200.</summary>
        public int AttackArmySupply { get; }

        /// <summary>Extra supply drawn 0 to this from the AI's own generator for each attack wave. Unit: supply. Range: 0 to 40.</summary>
        public int AttackSupplyJitter { get; }

        /// <summary>Retreat when army supply falls below this share of its supply at the start of the attack. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 RetreatArmyFraction { get; }

        /// <summary>Retreat when local own power is below this multiple of local visible enemy power. Unit: ratio. Range: 0 to 2.</summary>
        public Fix64 RetreatPowerRatio { get; }

        /// <summary>The attacking army first gathers this far short of the target (toward the own HQ); 0 attacks straight away. Unit: tiles. Range: 0 to 40.</summary>
        public Fix64 StageDistanceTiles { get; }

        /// <summary>Army units within this distance of the staging point count as gathered. Unit: tiles. Range: 1 to 20.</summary>
        public Fix64 StageRadiusTiles { get; }

        /// <summary>The army pushes from the staging point once this share of its supply has gathered there. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 StageGatherFraction { get; }

        /// <summary>The army pushes anyway this long after the attack started. Unit: seconds. Range: 0 to 120.</summary>
        public Fix64 StageTimeoutSeconds { get; }

        /// <summary>Focus fire reach: an army unit attacks the nearest visible enemy army unit within this distance; 0 turns focus fire off. Unit: tiles. Range: 0 to 20.</summary>
        public Fix64 FocusRadiusTiles { get; }

        /// <summary>Radius used for the retreat power test and the siege test. Unit: tiles. Range: 4 to 40.</summary>
        public Fix64 EngagementRadiusTiles { get; }

        /// <summary>Visible enemies this close to an own structure or HQ are a threat. Unit: tiles. Range: 4 to 40.</summary>
        public Fix64 DefendRadiusTiles { get; }

        /// <summary>Threat supply that recalls an attacking army home. Unit: supply. Range: 1 to 100.</summary>
        public int DefendRecallSupply { get; }

        /// <summary>How long a hit on an own unit inside the base counts as a threat. Unit: seconds. Range: 0 to 30.</summary>
        public Fix64 DamageMemorySeconds { get; }

        /// <summary>Time after a retreat before the next attack may start. Unit: seconds. Range: 0 to 120.</summary>
        public Fix64 RegroupSeconds { get; }

        /// <summary>
        /// When true and no field on the whole map has known resources left (skirmish_ai_v2.md B.5 rule A; no reinforcements will
        /// come), the AI goes all in: it attacks with any army regardless of the threshold and the tide window and never retreats.
        /// Unit: boolean. Range: true or false.
        /// </summary>
        public bool AllInWhenMinedOut { get; }

        /// <summary>
        /// When true and the AI's team does not hold the Core, the attack group stages at the Core centre instead of short of the
        /// target (skirmish_ai_v2.md C.2 rule G; it no longer moves the massing army, which rallies at home). Unit: boolean. Range: true or false.
        /// </summary>
        public bool RallyAtCore { get; }

        /// <summary>Stay in Defend this long after the last threat before massing again. Unit: seconds. Range: 0 to 60.</summary>
        public Fix64 DefendHoldSeconds { get; }

        /// <summary>A known enemy HQ last seen at or below this share of its health is finished off (rule K). Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 KillHqHealthFraction { get; }

        /// <summary>A remembered enemy HQ is a kill target only if it was last seen this recently (its health may have changed since). Unit: seconds. Range: 1 to 600.</summary>
        public Fix64 KillSeenWithinSeconds { get; }

        /// <summary>Army supply the kill rule needs. Unit: supply. Range: 1 to 200.</summary>
        public int KillMinArmySupply { get; }

        /// <summary>Bank (after this think's spending and reservations) at which rule X adds a production building. Unit: resource units. Range: 0 to 100000.</summary>
        public int ExtraProducerBankCommon { get; }

        /// <summary>Most own structures that train a composition unit (complete or under construction). Unit: structures. Range: 1 to 30.</summary>
        public int MaxProducers { get; }

        /// <summary>Workers sent to each served rare field (skirmish_ai_v2.md B.1). Unit: workers. Range: 0 to 8.</summary>
        public int RareWorkersPerField { get; }

        /// <summary>Earliest time for the first depot by the clock. Unit: seconds. Range: 0 to 1800.</summary>
        public Fix64 ExpandAtSeconds { get; }

        /// <summary>Seconds added to <see cref="ExpandAtSeconds"/> per depot already placed. Unit: seconds. Range: 0 to 900.</summary>
        public Fix64 ExpandIntervalSeconds { get; }

        /// <summary>Expand early when the served fields hold less than this share of their start amount. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 ExpandWhenServedRemainingFraction { get; }

        /// <summary>Most live depots (rule E); 0 turns depots off. Unit: depots. Range: 0 to 6.</summary>
        public int MaxLiveDepots { get; }

        /// <summary>A field below this is not an expansion or relocation target unless no field qualifies. Unit: resource units. Range: 1 to 1000000.</summary>
        public int ExpandMinFieldRemaining { get; }

        /// <summary>A target this close to a known enemy structure, an uncleared enemy spawn or a recently seen enemy army is unsafe. Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 ExpandSafeRadiusTiles { get; }

        /// <summary>How long the last seen enemy army position counts for safety. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 EnemyArmyMemorySeconds { get; }

        /// <summary>Innermost Chebyshev ring around the target field searched for a depot site. Unit: tiles. Range: 1 to 10.</summary>
        public int DepotRingMinTiles { get; }

        /// <summary>Outermost Chebyshev ring around the target field searched for a depot site. Unit: tiles. Range: depot_ring_min_tiles to 20.</summary>
        public int DepotRingMaxTiles { get; }

        /// <summary>Most workers moved off an over-full field per think (rule G rebalance). Unit: workers. Range: 0 to 20.</summary>
        public int RebalanceWorkersPerThink { get; }

        /// <summary>The HQ may relocate once its home fields hold this share or less of their start amount. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 RelocateWhenHomeRemainingFraction { get; }

        /// <summary>The relocating HQ stops this far from the target field centre toward its old position (capped at half the root distance). Unit: tiles. Range: 0.5 to 32.</summary>
        public Fix64 RelocateStandOffTiles { get; }

        /// <summary>A relocation not rooted after this long re-targets from where the HQ stands. Unit: seconds. Range: 10 to 600.</summary>
        public Fix64 RelocateTimeoutSeconds { get; }

        /// <summary>No new relocation this soon after rooting. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 RelocateCooldownSeconds { get; }

        /// <summary>Rule S (scouting) is on. Unit: boolean. Range: true or false.</summary>
        public bool UseScouting { get; }

        /// <summary>The enemy model is updated and read. Unit: boolean. Range: true or false.</summary>
        public bool UseEnemyModel { get; }

        /// <summary>Counter-mix switch (skirmish_ai_v3.md C.4): the plan composition is replaced by counter-mix weights. Unit: boolean. Range: true or false; true needs <see cref="UseEnemyModel"/>.</summary>
        public bool UseCounterMix { get; }

        /// <summary>First scout is wanted from this time. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 ScoutFirstSeconds { get; }

        /// <summary>A new scouting round starts this long after the last one started. Unit: seconds. Range: 10 to 900.</summary>
        public Fix64 ScoutIntervalSeconds { get; }

        /// <summary>Wait after a scout dies. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 ScoutRetrySeconds { get; }

        /// <summary>After this many dead scouts none is trained. Unit: units. Range: 0 to 20.</summary>
        public int ScoutMaxLosses { get; }

        /// <summary>Route length cap. Unit: waypoints. Range: 1 to 32.</summary>
        public int ScoutRouteMaxWaypoints { get; }

        /// <summary>A field this close to an earlier waypoint adds no waypoint. Unit: tiles. Range: 0 to 64.</summary>
        public Fix64 ScoutMergeTiles { get; }

        /// <summary>An own air army unit costing at most this (common + rare) may be borrowed as scout; 0 never borrows. Unit: resource units. Range: 0 to 100000.</summary>
        public int ScoutAirMaxCost { get; }

        /// <summary>A sighting counts fully for this long. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 EnemyModelHoldSeconds { get; }

        /// <summary>After the hold time a sighting fades linearly to the floor over this long. Unit: seconds. Range: 1 to 3600.</summary>
        public Fix64 EnemyModelDecaySeconds { get; }

        /// <summary>Old sightings never count for less than this share. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 EnemyModelFloorFraction { get; }

        /// <summary>Count implied for each unit type a known enemy structure trains. Unit: units. Range: 0 to 20.</summary>
        public int EnemyModelImpliedCount { get; }

        /// <summary>Rule T (tech on demand) is on. Unit: boolean. Range: true or false; true needs <see cref="UseCounterMix"/>.</summary>
        public bool UseTechOnDemand { get; }

        /// <summary>Rule SD (static defence) is on. Unit: boolean. Range: true or false; true needs <see cref="UseEnemyModel"/>.</summary>
        public bool UseStaticDefence { get; }

        /// <summary>Rule RS (rare priority) is on. Unit: boolean. Range: true or false; true needs <see cref="UseCounterMix"/>.</summary>
        public bool UseRarePriority { get; }

        /// <summary>Longest time a side needs in the fight predictor (also "never"). Unit: seconds. Range: 10 to 36000.</summary>
        public Fix64 PredictorMaxSeconds { get; }

        /// <summary>Shortest time a side needs in the fight predictor. Unit: seconds. Range: 0.01 to 10.</summary>
        public Fix64 PredictorMinSeconds { get; }

        /// <summary>Share of the range head start subtracted from a predicted time. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 PredictorRangeWeight { get; }

        /// <summary>Longest range head start. Unit: seconds. Range: 0 to 60.</summary>
        public Fix64 PredictorRangeHeadstartMaxSeconds { get; }

        /// <summary>Share of shield domes counted as extra health against ranged damage. Unit: fraction. Range: 0 to 2.</summary>
        public Fix64 PredictorDomeWeight { get; }

        /// <summary>Cap of the predicted advantage ratio. Unit: ratio. Range: 1 to 10000.</summary>
        public Fix64 PredictorMaxAdvantage { get; }

        /// <summary>Weight of the counter share against the base composition at full confidence. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 CounterWeight { get; }

        /// <summary>Known enemy army supply at which the counter-mix has full confidence. Unit: supply. Range: 1 to 200.</summary>
        public Fix64 CounterFullConfidenceSupply { get; }

        /// <summary>A unit is kept in the counter share when its score is at least this share of the best score. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 CounterKeepFraction { get; }

        /// <summary>Weight of a unit's rare cost in its counter-score cost. Unit: ratio. Range: 0 to 20.</summary>
        public Fix64 CounterRareCostWeight { get; }

        /// <summary>Floor of the damage an enemy is assumed to deal to a unit in the counter-score. Unit: health points per second. Range: 0.01 to 1000.</summary>
        public Fix64 CounterMinIncomingDps { get; }

        /// <summary>The mix weights are integers out of this many. Unit: weight units. Range: 10 to 100000.</summary>
        public int MixResolution { get; }

        /// <summary>Rule T builds a producer when the best missing unit's mix weight is at least this share of the resolution. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 TechMinWeightShare { get; }

        /// <summary>Raid starts within this long count towards static defence. Unit: seconds. Range: 10 to 1800.</summary>
        public Fix64 RaidedWindowSeconds { get; }

        /// <summary>Quiet time at a drop-off after which new activity there is a new raid. Unit: seconds. Range: 0 to 600.</summary>
        public Fix64 RaidedGapSeconds { get; }

        /// <summary>Raids in the window that call for a static defence. Unit: raids. Range: 1 to 20.</summary>
        public int StaticDefenceAfterRaids { get; }

        /// <summary>Most own static defences near one base for rule SD. Unit: structures. Range: 0 to 6 (not above supply.json static_defences_per_base_max, checked by a test).</summary>
        public int StaticDefenceMaxPerBase { get; }

        /// <summary>Innermost Chebyshev ring around a drop-off searched for a static defence site. Unit: tiles. Range: 1 to 10.</summary>
        public int StaticDefenceRingMinTiles { get; }

        /// <summary>Outermost Chebyshev ring searched for a static defence site. Unit: tiles. Range: ring min to 12.</summary>
        public int StaticDefenceRingMaxTiles { get; }

        /// <summary>Model supply of a layer that nothing answers before rule SD builds a defence at the HQ. Unit: supply. Range: 1 to 200.</summary>
        public int UnansweredLayerMinSupply { get; }

        /// <summary>Mix weight share of rare-cost units at which rule RS puts rare sites first in rule E's target order (rule RS only reorders rule E's targets since C2.5). Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 RareNeedWeightShare { get; }

        /// <summary>C2.2: the counter-mix is scored by the fight predictor. Unit: boolean. Range: true or false; true needs <see cref="UseCounterMix"/>.</summary>
        public bool UsePredictorMix { get; }

        /// <summary>C2.2: common purchase each candidate unit is valued by. Unit: resource units (common). Range: 50 to 100000.</summary>
        public int MixEvalBudgetCommon { get; }

        /// <summary>C2.2: rare a candidate may plan beyond the rare bank. Unit: resource units (rare). Range: 0 to 100000.</summary>
        public int MixRareAllowance { get; }

        /// <summary>C2.3: rare fields take no workers while the rare bank is at or above this; 0 is off. Unit: resource units (rare). Range: 0 to 100000.</summary>
        public int RareBankSoftCap { get; }

        /// <summary>C2.4: raid strength that counts as a raid; 1 is package C. Unit: supply. Range: 1 to 200.</summary>
        public int RaidMinSupply { get; }

        /// <summary>C2.4: own static defences may be worth at most this share of the army value; 0 is off. Unit: fraction. Range: 0 to 10.</summary>
        public Fix64 StaticDefenceMaxValueShare { get; }

        /// <summary>C2.5: while no rare field is served, rule E's clock gate is <see cref="EarlyExpansionSeconds"/>. Unit: boolean. Range: true or false.</summary>
        public bool UseEarlyExpansion { get; }

        /// <summary>C2.5: clock gate of rule E while no rare field is served and <see cref="UseEarlyExpansion"/> is on. Unit: seconds. Range: 0 to 1800.</summary>
        public Fix64 EarlyExpansionSeconds { get; }

        /// <summary>D.1, D.2: the attack and retreat tests use the fight predictor; the drawn threshold stays the fallback when nothing is known. Unit: boolean. Range: true or false; true needs <see cref="UseEnemyModel"/>.</summary>
        public bool UsePredictorAttack { get; }

        /// <summary>D.3, D.4: undefended enemy depots and producers are targets and fast units harass workers. Unit: boolean. Range: true or false.</summary>
        public bool UseTargetChoice { get; }

        /// <summary>D.5: only the defenders the predictor needs answer a threat inside the base. Unit: boolean. Range: true or false.</summary>
        public bool UseMeasuredDefence { get; }

        /// <summary>D.6: fall back, keep distance and fastest-kill focus fire. Unit: boolean. Range: true or false.</summary>
        public bool UseUnitControl { get; }

        /// <summary>D.1: predicted advantage needed to attack. Unit: ratio. Range: 0 to 100.</summary>
        public Fix64 AttackMinAdvantage { get; }

        /// <summary>D.1: smallest army supply that may attack on a predicted win. Unit: supply. Range: 1 to 200.</summary>
        public int AttackMinSupply { get; }

        /// <summary>D.1: army supply that attacks whatever the prediction says. Unit: supply. Range: 1 to 400.</summary>
        public int AttackForceSupply { get; }

        /// <summary>D.1: share of the enemy army model assumed to defend an undefended target. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 UndefendedArmyShare { get; }

        /// <summary>D.1, D.3, D.4: radius around a target in which known static defences defend it and an enemy army makes it defended. Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 TargetDefenceRadiusTiles { get; }

        /// <summary>D.2: the attack group retreats below this predicted advantage. Unit: ratio. Range: 0 to 10.</summary>
        public Fix64 RetreatMaxAdvantage { get; }

        /// <summary>D.4: smallest army supply at which a harass squad may start. Unit: supply. Range: 1 to 200.</summary>
        public int HarassMinArmySupply { get; }

        /// <summary>D.4: ground units at least this fast may harass (flyers always may). Unit: tiles per second. Range: 0 to 20.</summary>
        public Fix64 HarassMinSpeedTilesPerSecond { get; }

        /// <summary>D.4: supply of a harass squad. Unit: supply. Range: 1 to 50.</summary>
        public int HarassSquadSupply { get; }

        /// <summary>D.4: wait after a squad ends before the next may start. Unit: seconds. Range: 0 to 900.</summary>
        public Fix64 HarassCooldownSeconds { get; }

        /// <summary>D.4: time at the target without seeing a worker before the squad goes home. Unit: seconds. Range: 0 to 120.</summary>
        public Fix64 HarassLingerSeconds { get; }

        /// <summary>D.4: most commands of the harass rule per think. Unit: commands. Range: 0 to 40.</summary>
        public int HarassMaxCommandsPerThink { get; }

        /// <summary>D.5: predicted advantage the defenders need against a threat. Unit: ratio. Range: 0 to 100.</summary>
        public Fix64 DefendMinAdvantage { get; }

        /// <summary>D.5: supply sent to a hit report with no visible threat. Unit: supply. Range: 1 to 200.</summary>
        public int DefendBlindSupply { get; }

        /// <summary>D.5: most units considered as defenders. Unit: units. Range: 1 to 200.</summary>
        public int DefendMaxCandidates { get; }

        /// <summary>D.6.1: health share at or below which a unit near an armed enemy falls back. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 FallbackHealthFraction { get; }

        /// <summary>D.6.1: distance of an armed enemy that triggers a fall back. Unit: tiles. Range: 1 to 30.</summary>
        public Fix64 FallbackDangerTiles { get; }

        /// <summary>D.6.1: most fall-back orders per think. Unit: commands. Range: 0 to 200.</summary>
        public int FallbackMaxCommandsPerThink { get; }

        /// <summary>D.6.2: distance of a melee enemy that makes a ranged unit step back. Unit: tiles. Range: 0.5 to 20.</summary>
        public Fix64 KiteTriggerTiles { get; }

        /// <summary>D.6.2: length of the step back. Unit: tiles. Range: 0.5 to 20.</summary>
        public Fix64 KiteStepTiles { get; }

        /// <summary>D.6.2: range the unit must have over the enemy's to kite. Unit: tiles. Range: 0 to 20.</summary>
        public Fix64 KiteMinRangeGapTiles { get; }

        /// <summary>D.6.2: speed the unit must have, as a multiple of the enemy's, to kite. Unit: ratio. Range: 0 to 5.</summary>
        public Fix64 KiteSpeedRatioMin { get; }

        /// <summary>D.6.2: most keep-distance orders per think. Unit: commands. Range: 0 to 200.</summary>
        public int KiteMaxCommandsPerThink { get; }

        /// <summary>D.6.3: a unit keeps its target while its time to kill is at most this multiple of the best. Unit: ratio. Range: 1 to 10.</summary>
        public Fix64 FocusSwitchRatio { get; }

        /// <summary>D.6.3: most focus-fire orders per think. Unit: commands. Range: 0 to 200.</summary>
        public int FocusMaxCommandsPerThink { get; }

        /// <summary>D.6.3: units focus the enemy that dies fastest instead of the nearest one. Unit: boolean. Range: true or false.</summary>
        public bool UseFocusFire { get; }

        /// <summary>D.6.1: a fallen-back unit rejoins the army this long after it fell back. Unit: seconds. Range: 1 to 600.</summary>
        public Fix64 FallbackReleaseSeconds { get; }

        /// <summary>D.2: no predicted retreat this long after an attack starts unless the group has lost supply. Unit: seconds. Range: 0 to 60.</summary>
        public Fix64 AttackRetreatGraceSeconds { get; }

        /// <summary>D.4: a harass squad is recalled this long after it started. Unit: seconds. Range: 1 to 900.</summary>
        public Fix64 HarassMaxSeconds { get; }

        /// <summary>C2.3: once capped, rare fields stay closed until the rare bank falls below this; 0 means the cap itself. Unit: resource units (rare). Range: 0 to <see cref="RareBankSoftCap"/> when that is above 0.</summary>
        public int RareBankReturnCap { get; }

        /// <summary>R.4 (skirmish_ai_v4_rl.md): each base composition weight moves by up to this fraction at match start (one draw per unit from the variety stream); 0 = off. Unit: fraction. Range: 0 to 0.5.</summary>
        public Fix64 CompositionJitterFraction { get; }

        /// <summary>R.4: the first depot clocks (expand_at_seconds, early_expansion_seconds) move by a whole number of seconds drawn from minus to plus this at match start; 0 = off. Unit: seconds. Range: 0 to 120.</summary>
        public int ExpandJitterSeconds { get; }

        /// <summary>R.4: attack_min_advantage moves by an offset drawn from minus to plus this, in steps of 0.01, each time the army starts massing; 0 = off. Unit: ratio. Range: 0 to 0.5.</summary>
        public Fix64 AttackAdvantageJitter { get; }

        /// <summary>AI-E E1: worker caps per field come from the gather slots and the walk to the drop-off instead of workers_per_field / rare_workers_per_field (max_workers stays a ceiling). Unit: boolean.</summary>
        public bool UseSaturationWorkers { get; }

        /// <summary>AI-E E1.1: workers added to every saturation cap. Unit: workers. Range: 0 to 4.</summary>
        public int SaturationExtraWorkersPerField { get; }

        /// <summary>AI-E E1.4: the rare soft cap closes rare fields only while a served common field is below its cap. Unit: boolean.</summary>
        public bool RareCapYieldsToSaturation { get; }

        /// <summary>AI-E E2.1: rule E also fires when the workers reach expand_saturation_fraction of the workers wanted. Unit: boolean.</summary>
        public bool UseSaturationExpansion { get; }

        /// <summary>AI-E E2.1: share of the uncapped worker target at which a full base expands. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 ExpandSaturationFraction { get; }

        /// <summary>AI-E E2.2: a visible threat blocks a depot only from this supply; 0 means any threat blocks. Unit: supply. Range: 0 to 200.</summary>
        public int ExpandBlockThreatSupply { get; }

        /// <summary>AI-P P1: extra and tech producers go to the own base with the fewest producers instead of the HQ ring. Unit: boolean.</summary>
        public bool UseForwardProducers { get; }

        /// <summary>AI-P P2: rule SD step SD-4 (static defence at the base the enemy model threatens) and the min(profile, Sim) per-base cap. Unit: boolean.</summary>
        public bool UseThreatDefence { get; }

        /// <summary>AI-P P2: enemy model army supply SD-4 needs. Unit: supply. Range: 0 to 200.</summary>
        public int ThreatDefenceMinEnemySupply { get; }

        /// <summary>AI-P P2: predicted enemy advantage (predictor ratio, enemy as side A) SD-4 needs. Unit: ratio. Range: 0 to 100.</summary>
        public Fix64 ThreatDefenceMinEnemyAdvantage { get; }

        /// <summary>AI-P P2: own static defences including the new one may be worth at most this share of the own army value; 0 means no cap. Unit: fraction. Range: 0 to 2.</summary>
        public Fix64 ThreatDefenceMaxValueShare { get; }

        /// <summary>AI-P P2 hysteresis: SD-4 stays armed while supply and advantage stay at this fraction of their thresholds, and the exposed base changes only to one this fraction closer to the enemy. 1 means no band. Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 ThreatDefenceReleaseFraction { get; }

        /// <summary>AI-H: a unit may harass when its speed is at least this multiple of the chaser speed (<see cref="AiIntel.ChaserSpeed"/>); 0 keeps the fixed-speed rule and the flyer exemption. Unit: ratio. Range: 0 to 4.</summary>
        public Fix64 HarassSpeedRatio { get; }

        /// <summary>Per-faction plans by faction ID, in ordinal order.</summary>
        public IReadOnlyDictionary<string, AiFactionPlan> Factions { get; }

        /// <summary>Builds the profile from its validated parts; see <see cref="Parse(JsonValue, ContentCatalog)"/>.</summary>
        private AiProfile(JsonValue root, string id, IReadOnlyDictionary<string, AiFactionPlan> factions)
        {
            Id = id;
            SourceName = root.SourceName;
            Factions = factions;
            ThinkIntervalSeconds = root.RequireFix64("think_interval_seconds", "0.1", "5");
            MaxCommandsPerThink = root.RequireInt("max_commands_per_think", 1, 200);
            WorkersPerField = root.RequireInt("workers_per_field", 1, 8);
            MaxWorkers = root.RequireInt("max_workers", 1, 100);
            FieldMaxDistanceTiles = root.RequireFix64("field_max_distance_tiles", "2", "64");
            SupplyBuffer = root.RequireInt("supply_buffer", 0, 40);
            BuildRetrySeconds = root.RequireFix64("build_retry_seconds", "1", "60");
            BuildRingMinTiles = root.RequireInt("build_ring_min_tiles", 1, 20);
            BuildRingMaxTiles = root.RequireInt("build_ring_max_tiles", BuildRingMinTiles, 30);
            FieldClearanceTiles = root.RequireInt("field_clearance_tiles", 0, 10);
            RallyDistanceTiles = root.RequireFix64("rally_distance_tiles", "0", "20");
            AttackArmySupply = root.RequireInt("attack_army_supply", 1, 200);
            AttackSupplyJitter = root.RequireInt("attack_supply_jitter", 0, 40);
            RetreatArmyFraction = root.RequireFix64("retreat_army_fraction", "0", "1");
            RetreatPowerRatio = root.RequireFix64("retreat_power_ratio", "0", "2");
            EngagementRadiusTiles = root.RequireFix64("engagement_radius_tiles", "4", "40");
            StageDistanceTiles = root.RequireFix64("stage_distance_tiles", "0", "40");
            FocusRadiusTiles = root.RequireFix64("focus_radius_tiles", "0", "20");
            StageRadiusTiles = root.RequireFix64("stage_radius_tiles", "1", "20");
            StageGatherFraction = root.RequireFix64("stage_gather_fraction", "0", "1");
            StageTimeoutSeconds = root.RequireFix64("stage_timeout_seconds", "0", "120");
            DefendRadiusTiles = root.RequireFix64("defend_radius_tiles", "4", "40");
            DefendRecallSupply = root.RequireInt("defend_recall_supply", 1, 100);
            DamageMemorySeconds = root.RequireFix64("damage_memory_seconds", "0", "30");
            RegroupSeconds = root.RequireFix64("regroup_seconds", "0", "120");
            AllInWhenMinedOut = root.RequireBool("all_in_when_mined_out");
            RallyAtCore = root.RequireBool("rally_at_core");
            DefendHoldSeconds = root.RequireFix64("defend_hold_seconds", "0", "60");
            KillHqHealthFraction = root.RequireFix64("kill_hq_health_fraction", "0", "1");
            KillSeenWithinSeconds = root.RequireFix64("kill_seen_within_seconds", "1", "600");
            KillMinArmySupply = root.RequireInt("kill_min_army_supply", 1, 200);
            ExtraProducerBankCommon = root.RequireInt("extra_producer_bank_common", 0, 100000);
            MaxProducers = root.RequireInt("max_producers", 1, 30);
            RareWorkersPerField = root.RequireInt("rare_workers_per_field", 0, 8);
            ExpandAtSeconds = root.RequireFix64("expand_at_seconds", "0", "1800");
            ExpandIntervalSeconds = root.RequireFix64("expand_interval_seconds", "0", "900");
            ExpandWhenServedRemainingFraction = root.RequireFix64("expand_when_served_remaining_fraction", "0", "1");
            MaxLiveDepots = root.RequireInt("max_live_depots", 0, 6);
            ExpandMinFieldRemaining = root.RequireInt("expand_min_field_remaining", 1, 1000000);
            ExpandSafeRadiusTiles = root.RequireFix64("expand_safe_radius_tiles", "2", "64");
            EnemyArmyMemorySeconds = root.RequireFix64("enemy_army_memory_seconds", "0", "600");
            DepotRingMinTiles = root.RequireInt("depot_ring_min_tiles", 1, 10);
            DepotRingMaxTiles = root.RequireInt("depot_ring_max_tiles", DepotRingMinTiles, 20);
            RebalanceWorkersPerThink = root.RequireInt("rebalance_workers_per_think", 0, 20);
            RelocateWhenHomeRemainingFraction = root.RequireFix64("relocate_when_home_remaining_fraction", "0", "1");
            RelocateStandOffTiles = root.RequireFix64("relocate_stand_off_tiles", "0.5", "32");
            RelocateTimeoutSeconds = root.RequireFix64("relocate_timeout_seconds", "10", "600");
            RelocateCooldownSeconds = root.RequireFix64("relocate_cooldown_seconds", "0", "600");
            UseScouting = root.RequireBool("use_scouting");
            UseEnemyModel = root.RequireBool("use_enemy_model");
            UseCounterMix = root.RequireBool("use_counter_mix");
            if (UseCounterMix && !UseEnemyModel)
            {
                throw root.Require("use_counter_mix").Fail("requires use_enemy_model to be true as well");
            }
            ScoutFirstSeconds = root.RequireFix64("scout_first_seconds", "0", "600");
            ScoutIntervalSeconds = root.RequireFix64("scout_interval_seconds", "10", "900");
            ScoutRetrySeconds = root.RequireFix64("scout_retry_seconds", "0", "600");
            ScoutMaxLosses = root.RequireInt("scout_max_losses", 0, 20);
            ScoutRouteMaxWaypoints = root.RequireInt("scout_route_max_waypoints", 1, 32);
            ScoutMergeTiles = root.RequireFix64("scout_merge_tiles", "0", "64");
            ScoutAirMaxCost = root.RequireInt("scout_air_max_cost", 0, 100000);
            EnemyModelHoldSeconds = root.RequireFix64("enemy_model_hold_seconds", "0", "600");
            EnemyModelDecaySeconds = root.RequireFix64("enemy_model_decay_seconds", "1", "3600");
            EnemyModelFloorFraction = root.RequireFix64("enemy_model_floor_fraction", "0", "1");
            EnemyModelImpliedCount = root.RequireInt("enemy_model_implied_count", 0, 20);
            UseTechOnDemand = root.RequireBool("use_tech_on_demand");
            UseStaticDefence = root.RequireBool("use_static_defence");
            UseRarePriority = root.RequireBool("use_rare_priority");
            if (UseTechOnDemand && !UseCounterMix)
            {
                throw root.Require("use_tech_on_demand").Fail("requires use_counter_mix to be true as well");
            }
            if (UseRarePriority && !UseCounterMix)
            {
                throw root.Require("use_rare_priority").Fail("requires use_counter_mix to be true as well");
            }
            if (UseStaticDefence && !UseEnemyModel)
            {
                throw root.Require("use_static_defence").Fail("requires use_enemy_model to be true as well");
            }
            PredictorMaxSeconds = root.RequireFix64("predictor_max_seconds", "10", "36000");
            PredictorMinSeconds = root.RequireFix64("predictor_min_seconds", "0.01", "10");
            PredictorRangeWeight = root.RequireFix64("predictor_range_weight", "0", "1");
            PredictorRangeHeadstartMaxSeconds = root.RequireFix64("predictor_range_headstart_max_seconds", "0", "60");
            PredictorDomeWeight = root.RequireFix64("predictor_dome_weight", "0", "2");
            PredictorMaxAdvantage = root.RequireFix64("predictor_max_advantage", "1", "10000");
            CounterWeight = root.RequireFix64("counter_weight", "0", "1");
            CounterFullConfidenceSupply = root.RequireFix64("counter_full_confidence_supply", "1", "200");
            CounterKeepFraction = root.RequireFix64("counter_keep_fraction", "0", "1");
            CounterRareCostWeight = root.RequireFix64("counter_rare_cost_weight", "0", "20");
            CounterMinIncomingDps = root.RequireFix64("counter_min_incoming_dps", "0.01", "1000");
            MixResolution = root.RequireInt("mix_resolution", 10, 100000);
            TechMinWeightShare = root.RequireFix64("tech_min_weight_share", "0", "1");
            RaidedWindowSeconds = root.RequireFix64("raided_window_seconds", "10", "1800");
            RaidedGapSeconds = root.RequireFix64("raided_gap_seconds", "0", "600");
            StaticDefenceAfterRaids = root.RequireInt("static_defence_after_raids", 1, 20);
            StaticDefenceMaxPerBase = root.RequireInt("static_defence_max_per_base", 0, 6);
            StaticDefenceRingMinTiles = root.RequireInt("static_defence_ring_min_tiles", 1, 10);
            StaticDefenceRingMaxTiles = root.RequireInt("static_defence_ring_max_tiles", StaticDefenceRingMinTiles, 12);
            UnansweredLayerMinSupply = root.RequireInt("unanswered_layer_min_supply", 1, 200);
            RareNeedWeightShare = root.RequireFix64("rare_need_weight_share", "0", "1");
            UsePredictorMix = root.RequireBool("use_predictor_mix");
            if (UsePredictorMix && !UseCounterMix)
            {
                throw root.Require("use_predictor_mix").Fail("requires use_counter_mix to be true as well");
            }
            MixEvalBudgetCommon = root.RequireInt("mix_eval_budget_common", 50, 100000);
            MixRareAllowance = root.RequireInt("mix_rare_allowance", 0, 100000);
            RareBankSoftCap = root.RequireInt("rare_bank_soft_cap", 0, 100000);
            RaidMinSupply = root.RequireInt("raid_min_supply", 1, 200);
            StaticDefenceMaxValueShare = root.RequireFix64("static_defence_max_value_share", "0", "10");
            UseEarlyExpansion = root.RequireBool("use_early_expansion");
            EarlyExpansionSeconds = root.RequireFix64("early_expansion_seconds", "0", "1800");
            UsePredictorAttack = root.RequireBool("use_predictor_attack");
            if (UsePredictorAttack && !UseEnemyModel)
            {
                throw root.Require("use_predictor_attack").Fail("requires use_enemy_model to be true as well");
            }
            UseTargetChoice = root.RequireBool("use_target_choice");
            UseMeasuredDefence = root.RequireBool("use_measured_defence");
            UseUnitControl = root.RequireBool("use_unit_control");
            AttackMinAdvantage = root.RequireFix64("attack_min_advantage", "0", "100");
            AttackMinSupply = root.RequireInt("attack_min_supply", 1, 200);
            AttackForceSupply = root.RequireInt("attack_force_supply", 1, 400);
            UndefendedArmyShare = root.RequireFix64("undefended_army_share", "0", "1");
            TargetDefenceRadiusTiles = root.RequireFix64("target_defence_radius_tiles", "2", "64");
            RetreatMaxAdvantage = root.RequireFix64("retreat_max_advantage", "0", "10");
            HarassMinArmySupply = root.RequireInt("harass_min_army_supply", 1, 200);
            HarassMinSpeedTilesPerSecond = root.RequireFix64("harass_min_speed_tiles_per_second", "0", "20");
            HarassSquadSupply = root.RequireInt("harass_squad_supply", 1, 50);
            HarassCooldownSeconds = root.RequireFix64("harass_cooldown_seconds", "0", "900");
            HarassLingerSeconds = root.RequireFix64("harass_linger_seconds", "0", "120");
            HarassMaxCommandsPerThink = root.RequireInt("harass_max_commands_per_think", 0, 40);
            DefendMinAdvantage = root.RequireFix64("defend_min_advantage", "0", "100");
            DefendBlindSupply = root.RequireInt("defend_blind_supply", 1, 200);
            DefendMaxCandidates = root.RequireInt("defend_max_candidates", 1, 200);
            FallbackHealthFraction = root.RequireFix64("fallback_health_fraction", "0", "1");
            FallbackDangerTiles = root.RequireFix64("fallback_danger_tiles", "1", "30");
            FallbackMaxCommandsPerThink = root.RequireInt("fallback_max_commands_per_think", 0, 200);
            KiteTriggerTiles = root.RequireFix64("kite_trigger_tiles", "0.5", "20");
            KiteStepTiles = root.RequireFix64("kite_step_tiles", "0.5", "20");
            KiteMinRangeGapTiles = root.RequireFix64("kite_min_range_gap_tiles", "0", "20");
            KiteSpeedRatioMin = root.RequireFix64("kite_speed_ratio_min", "0", "5");
            KiteMaxCommandsPerThink = root.RequireInt("kite_max_commands_per_think", 0, 200);
            FocusSwitchRatio = root.RequireFix64("focus_switch_ratio", "1", "10");
            FocusMaxCommandsPerThink = root.RequireInt("focus_max_commands_per_think", 0, 200);
            UseFocusFire = root.RequireBool("use_focus_fire");
            FallbackReleaseSeconds = root.RequireFix64("fallback_release_seconds", "1", "600");
            AttackRetreatGraceSeconds = root.RequireFix64("attack_retreat_grace_seconds", "0", "60");
            HarassMaxSeconds = root.RequireFix64("harass_max_seconds", "1", "900");
            RareBankReturnCap = root.RequireInt("rare_bank_return_cap", 0, 100000);
            if (RareBankSoftCap > 0 && RareBankReturnCap > RareBankSoftCap)
            {
                throw root.Require("rare_bank_return_cap").Fail("must not exceed rare_bank_soft_cap");
            }
            CompositionJitterFraction = root.RequireFix64("composition_jitter_fraction", "0", "0.5");
            ExpandJitterSeconds = root.RequireInt("expand_jitter_seconds", 0, 120);
            AttackAdvantageJitter = root.RequireFix64("attack_advantage_jitter", "0", "0.5");
            UseSaturationWorkers = root.RequireBool("use_saturation_workers");
            SaturationExtraWorkersPerField = root.RequireInt("saturation_extra_workers_per_field", 0, 4);
            RareCapYieldsToSaturation = root.RequireBool("rare_cap_yields_to_saturation");
            UseSaturationExpansion = root.RequireBool("use_saturation_expansion");
            ExpandSaturationFraction = root.RequireFix64("expand_saturation_fraction", "0", "1");
            ExpandBlockThreatSupply = root.RequireInt("expand_block_threat_supply", 0, 200);
            UseForwardProducers = root.RequireBool("use_forward_producers");
            UseThreatDefence = root.RequireBool("use_threat_defence");
            ThreatDefenceMinEnemySupply = root.RequireInt("threat_defence_min_enemy_supply", 0, 200);
            ThreatDefenceMinEnemyAdvantage = root.RequireFix64("threat_defence_min_enemy_advantage", "0", "100");
            ThreatDefenceMaxValueShare = root.RequireFix64("threat_defence_max_value_share", "0", "2");
            ThreatDefenceReleaseFraction = root.RequireFix64("threat_defence_release_fraction", "0", "1");
            HarassSpeedRatio = root.RequireFix64("harass_speed_ratio", "0", "4");
            if (UseThreatDefence && !UseStaticDefence)
            {
                throw root.Require("use_threat_defence").Fail("requires use_static_defence to be true as well");
            }
        }

        /// <summary>Parses and validates profile text. Throws <see cref="DataValidationException"/> naming the file and the field.</summary>
        /// <param name="text">The profile file's text (JSON with comments).</param>
        /// <param name="sourceName">Data-root-relative path, for example "ai/aggressive.json"; the id must match its file name.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        public static AiProfile Parse(string text, string sourceName, ContentCatalog content)
        {
            return Parse(JsonReader.Parse(text, sourceName), content, null);
        }

        /// <summary>Parses and validates profile text, also checking cycle_preference against the cycle IDs. Throws <see cref="DataValidationException"/> naming the file and the field.</summary>
        /// <param name="text">The profile file's text (JSON with comments).</param>
        /// <param name="sourceName">Data-root-relative path, for example "ai/aggressive.json"; the id must match its file name.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        /// <param name="cycleIds">IDs of the Planetary Cycles in the data set; every cycle_preference must list exactly these. Null skips that check.</param>
        public static AiProfile Parse(string text, string sourceName, ContentCatalog content, IReadOnlyCollection<string>? cycleIds)
        {
            return Parse(JsonReader.Parse(text, sourceName), content, cycleIds);
        }

        /// <summary>
        /// Validates a parsed profile tree (as stored in <see cref="DataSet.AiProfiles"/>). Throws <see cref="DataValidationException"/>
        /// naming the file and the field on any problem: unknown or missing keys, values out of range, and IDs that are unknown, belong
        /// to another faction or have the wrong kind.
        /// </summary>
        /// <param name="root">The profile file's root object.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        public static AiProfile Parse(JsonValue root, ContentCatalog content)
        {
            return Parse(root, content, null);
        }

        /// <summary>
        /// Validates a parsed profile tree like <see cref="Parse(JsonValue, ContentCatalog)"/> and also checks each faction's
        /// cycle_preference: only known cycle IDs, none twice, and (when <paramref name="cycleIds"/> is given) every cycle listed.
        /// </summary>
        /// <param name="root">The profile file's root object.</param>
        /// <param name="content">Units and factions the IDs are checked against.</param>
        /// <param name="cycleIds">IDs of the Planetary Cycles in the data set, or null to skip the existence and completeness checks.</param>
        public static AiProfile Parse(JsonValue root, ContentCatalog content, IReadOnlyCollection<string>? cycleIds)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            if (root.Kind != JsonKind.Object)
            {
                throw root.Fail("the top level must be a JSON object");
            }
            if (root.Require(DataLoader.SchemaVersionKey).AsInt() != DataLoader.SupportedSchemaVersion)
            {
                throw root.Require(DataLoader.SchemaVersionKey).Fail("must be " + DataLoader.SupportedSchemaVersion);
            }
            root.RequireNonEmptyString(DataLoader.DocKey);
            root.RejectUnknownKeys("id", "think_interval_seconds", "max_commands_per_think", "workers_per_field", "max_workers",
                "field_max_distance_tiles", "supply_buffer", "build_retry_seconds", "build_ring_min_tiles", "build_ring_max_tiles",
                "field_clearance_tiles", "rally_distance_tiles", "attack_army_supply", "attack_supply_jitter", "retreat_army_fraction",
                "retreat_power_ratio", "engagement_radius_tiles", "stage_distance_tiles", "stage_radius_tiles", "stage_gather_fraction",
                "stage_timeout_seconds", "focus_radius_tiles", "defend_radius_tiles", "defend_recall_supply", "damage_memory_seconds",
                "regroup_seconds", "all_in_when_mined_out", "rally_at_core", "defend_hold_seconds", "kill_hq_health_fraction",
                "kill_seen_within_seconds", "kill_min_army_supply", "extra_producer_bank_common", "max_producers", "rare_workers_per_field",
                "expand_at_seconds", "expand_interval_seconds", "expand_when_served_remaining_fraction", "max_live_depots",
                "expand_min_field_remaining", "expand_safe_radius_tiles", "enemy_army_memory_seconds", "depot_ring_min_tiles",
                "depot_ring_max_tiles", "rebalance_workers_per_think", "relocate_when_home_remaining_fraction", "relocate_stand_off_tiles",
                "relocate_timeout_seconds", "relocate_cooldown_seconds", "use_scouting", "use_enemy_model", "use_counter_mix",
                "scout_first_seconds", "scout_interval_seconds", "scout_retry_seconds", "scout_max_losses", "scout_route_max_waypoints",
                "scout_merge_tiles", "scout_air_max_cost", "enemy_model_hold_seconds", "enemy_model_decay_seconds",
                "enemy_model_floor_fraction", "enemy_model_implied_count", "use_tech_on_demand", "use_static_defence", "use_rare_priority",
                "predictor_max_seconds", "predictor_min_seconds", "predictor_range_weight", "predictor_range_headstart_max_seconds",
                "predictor_dome_weight", "predictor_max_advantage", "counter_weight", "counter_full_confidence_supply", "counter_keep_fraction",
                "counter_rare_cost_weight", "counter_min_incoming_dps", "mix_resolution", "tech_min_weight_share", "raided_window_seconds",
                "raided_gap_seconds", "static_defence_after_raids", "static_defence_max_per_base", "static_defence_ring_min_tiles",
                "static_defence_ring_max_tiles", "unanswered_layer_min_supply", "rare_need_weight_share", "use_predictor_mix", "mix_eval_budget_common",
                "mix_rare_allowance", "rare_bank_soft_cap", "raid_min_supply", "static_defence_max_value_share", "use_early_expansion",
                "early_expansion_seconds", "use_predictor_attack", "use_target_choice", "use_measured_defence", "use_unit_control",
                "attack_min_advantage", "attack_min_supply", "attack_force_supply", "undefended_army_share",
                "target_defence_radius_tiles", "retreat_max_advantage", "harass_min_army_supply",
                "harass_min_speed_tiles_per_second", "harass_squad_supply", "harass_cooldown_seconds",
                "harass_linger_seconds", "harass_max_commands_per_think", "defend_min_advantage", "defend_blind_supply",
                "defend_max_candidates", "fallback_health_fraction", "fallback_danger_tiles",
                "fallback_max_commands_per_think", "kite_trigger_tiles", "kite_step_tiles", "kite_min_range_gap_tiles",
                "kite_speed_ratio_min", "kite_max_commands_per_think", "focus_switch_ratio", "focus_max_commands_per_think", "use_focus_fire", "fallback_release_seconds",
                "attack_retreat_grace_seconds", "harass_max_seconds", "rare_bank_return_cap", "composition_jitter_fraction", "expand_jitter_seconds", "attack_advantage_jitter",
                "use_saturation_workers", "saturation_extra_workers_per_field", "rare_cap_yields_to_saturation", "use_saturation_expansion",
                "expand_saturation_fraction", "expand_block_threat_supply", "use_forward_producers", "use_threat_defence", "threat_defence_min_enemy_supply",
                "threat_defence_min_enemy_advantage", "threat_defence_max_value_share", "threat_defence_release_fraction", "harass_speed_ratio", "factions");
            // Profile ID as written; must equal the file name without folder and extension.
            string id = root.RequireNonEmptyString("id");
            // File name stem the id must match, from the source path.
            string expected = StemOf(root.SourceName);
            if (expected.Length > 0 && !string.Equals(id, expected, StringComparison.Ordinal))
            {
                throw root.Require("id").Fail("must be '" + expected + "' to match the file path (found '" + id + "')");
            }
            // The factions object: one plan per faction ID.
            JsonValue factionsNode = root.Require("factions");
            // Plans by faction ID in ordinal order.
            SortedDictionary<string, AiFactionPlan> plans = new SortedDictionary<string, AiFactionPlan>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in factionsNode.Members)
            {
                plans.Add(pair.Key, ParseFaction(pair.Key, pair.Value, content, cycleIds));
            }
            if (plans.Count == 0)
            {
                throw factionsNode.Fail("must hold a plan for at least one faction");
            }
            return new AiProfile(root, id, plans);
        }

        /// <summary>
        /// The profile <paramref name="id"/> from a loaded data set, validated against its content. Throws
        /// <see cref="DataValidationException"/> when the file is missing or invalid.
        /// </summary>
        /// <param name="data">The loaded data folder.</param>
        /// <param name="id">Profile ID, the file name under rts/data/ai without ".json".</param>
        public static AiProfile FromDataSet(DataSet data, string id)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (!data.AiProfiles.TryGetValue(id, out JsonValue? root))
            {
                throw new DataValidationException(DataLoader.AiFolder + "/" + id + ".json", string.Empty, "is not a loaded AI profile");
            }
            return Parse(root, data.Content, new List<string>(data.Cycles.Keys));
        }

        /// <summary>The plan for <paramref name="factionId"/>; throws <see cref="DataValidationException"/> naming this file when it has none.</summary>
        /// <param name="factionId">A faction ID, for example "water".</param>
        public AiFactionPlan RequireFaction(string factionId)
        {
            if (!Factions.TryGetValue(factionId, out AiFactionPlan? plan))
            {
                throw new DataValidationException(SourceName, "factions." + factionId, "is missing; this profile has no plan for that faction");
            }
            return plan;
        }

        /// <summary>Validates one faction plan object.</summary>
        /// <param name="factionId">The key the plan is stored under.</param>
        /// <param name="node">The plan object.</param>
        /// <param name="content">Units and factions to check IDs against.</param>
        /// <param name="cycleIds">IDs of the Planetary Cycles in the data set, or null to skip the existence and completeness checks.</param>
        private static AiFactionPlan ParseFaction(string factionId, JsonValue node, ContentCatalog content, IReadOnlyCollection<string>? cycleIds)
        {
            if (!content.Factions.TryGetValue(factionId, out FactionDefinition? faction))
            {
                throw node.Fail("names an unknown faction (no factions/" + factionId + ".json)");
            }
            if (node.Kind != JsonKind.Object)
            {
                throw node.Fail("must be an object");
            }
            node.RejectUnknownKeys("worker_unit", "supply_building", "build_order", "composition", "attack_tide_min", "attack_tide_max",
                "attack_tide_override_supply", "cycle_preference", "drop_off_building", "scout_unit", "openings");
            // Worker unit ID, checked to be a gatherer of this faction.
            string worker = node.RequireNonEmptyString("worker_unit");
            // Definition of the worker unit.
            UnitDefinition workerDefinition = RequireUnit(node.Require("worker_unit"), worker, faction.Id, content);
            if (!workerDefinition.Economy.CanGather)
            {
                throw node.Require("worker_unit").Fail("must be a unit with can_gather (found '" + worker + "')");
            }
            // Supply structure ID, checked to be a structure that provides supply.
            string supply = node.RequireNonEmptyString("supply_building");
            // Definition of the supply structure.
            UnitDefinition supplyDefinition = RequireUnit(node.Require("supply_building"), supply, faction.Id, content);
            if (supplyDefinition.Economy.FootprintTiles <= 0 || supplyDefinition.Economy.ProvidesSupply <= 0)
            {
                throw node.Require("supply_building").Fail("must be a structure with provides_supply above 0 (found '" + supply + "')");
            }
            // Build order steps in file order.
            List<AiBuildStep> steps = ParseSteps(node.Require("build_order"), node.RequireArray("build_order"), faction, content);
            // The composition object: unit ID to weight.
            JsonValue compositionNode = node.Require("composition");
            // Weights by unit ID in ordinal order.
            SortedDictionary<string, int> composition = ParseComposition(compositionNode, faction, content);
            // Tide window bounds. Unit: fraction. Range: 0 to 1, min at most max.
            Fix64 tideMin = node.RequireFix64("attack_tide_min", "0", "1");
            // Upper bound of the tide window. Unit: fraction. Range: 0 to 1.
            Fix64 tideMax = node.RequireFix64("attack_tide_max", "0", "1");
            if (tideMax < tideMin)
            {
                throw node.Require("attack_tide_max").Fail("must be at least attack_tide_min");
            }
            // Cycle IDs, most wanted first. Range: distinct, known cycle IDs; every cycle of the data set when cycleIds is given.
            List<string> preference = new List<string>();
            // Cycle IDs already listed, to reject repeats.
            SortedSet<string> listed = new SortedSet<string>(StringComparer.Ordinal);
            JsonValue preferenceNode = node.Require("cycle_preference");
            foreach (JsonValue item in preferenceNode.Items)
            {
                // The cycle ID at this position.
                string cycleId = item.AsString();
                if (cycleIds != null && !ContainsId(cycleIds, cycleId))
                {
                    throw item.Fail("names an unknown cycle '" + cycleId + "' (no cycles/" + cycleId + ".json)");
                }
                if (!listed.Add(cycleId))
                {
                    throw item.Fail("lists the cycle '" + cycleId + "' twice");
                }
                preference.Add(cycleId);
            }
            if (cycleIds != null)
            {
                foreach (string cycleId in cycleIds)
                {
                    if (!listed.Contains(cycleId))
                    {
                        throw preferenceNode.Fail("must list every cycle; '" + cycleId + "' is missing");
                    }
                }
            }
            // Optional drop-off structure ID ("" when the key is absent: no depots).
            string dropOff = string.Empty;
            if (node.TryGetMember("drop_off_building", out JsonValue? dropOffNode))
            {
                dropOff = dropOffNode.AsString();
                // Definition of the depot structure.
                UnitDefinition dropOffDefinition = RequireUnit(dropOffNode, dropOff, faction.Id, content);
                if (dropOffDefinition.Economy.FootprintTiles != 1 || !dropOffDefinition.Economy.IsDropOff)
                {
                    throw dropOffNode.Fail("must be a structure with footprint 1 and is_drop_off true (found '" + dropOff + "')");
                }
            }
            // Optional scout unit ID ("" when the key is absent: no scout is trained).
            string scoutId = string.Empty;
            if (node.TryGetMember("scout_unit", out JsonValue? scoutNode))
            {
                scoutId = scoutNode.AsString();
                // Definition of the scout unit.
                UnitDefinition scoutDefinition = RequireUnit(scoutNode, scoutId, faction.Id, content);
                if (scoutDefinition.Economy.CanGather)
                {
                    throw scoutNode.Fail("must not be a worker (found '" + scoutId + "')");
                }
                if (scoutDefinition.Economy.FootprintTiles > 0 || scoutDefinition.Hq != null)
                {
                    throw scoutNode.Fail("must be a mobile unit, not a structure or an HQ (found '" + scoutId + "')");
                }
                if (!IsTrainedByFaction(scoutId, faction.Id, content))
                {
                    throw scoutNode.Fail("is not in the trains list of any " + faction.Id + " structure");
                }
            }
            return new AiFactionPlan(faction.Id, worker, supply, steps.AsReadOnly(), composition, tideMin, tideMax,
                node.RequireInt("attack_tide_override_supply", 1, 400), preference.AsReadOnly(), dropOff, scoutId, ParseOpenings(node, faction, content));
        }

        /// <summary>Validates a build order array (the plan's or an opening's): each step names a structure of the faction.</summary>
        /// <param name="owner">The array node the steps came from. Unused: the method body never reads it; kept so the signature stays as it is. Range: any JSON array node.</param>
        /// <param name="items">The array's elements.</param>
        /// <param name="faction">The faction the structures must belong to.</param>
        /// <param name="content">Units to check IDs against.</param>
        private static List<AiBuildStep> ParseSteps(JsonValue owner, IReadOnlyList<JsonValue> items, FactionDefinition faction, ContentCatalog content)
        {
            // Build order steps in file order.
            List<AiBuildStep> steps = new List<AiBuildStep>();
            // One build order step object, in file order. Range: any JSON object with structure, count and at_supply_used.
            foreach (JsonValue stepNode in items)
            {
                stepNode.RejectUnknownKeys("structure", "count", "at_supply_used");
                // Structure ID of this step.
                string structure = stepNode.RequireNonEmptyString("structure");
                // Definition of the step's structure.
                UnitDefinition structureDefinition = RequireUnit(stepNode.Require("structure"), structure, faction.Id, content);
                if (structureDefinition.Economy.FootprintTiles <= 0)
                {
                    throw stepNode.Require("structure").Fail("must be a structure (footprint above 0; found '" + structure + "')");
                }
                steps.Add(new AiBuildStep(structure, stepNode.RequireInt("count", 1, 20), stepNode.RequireInt("at_supply_used", 0, 200)));
            }
            return steps;
        }

        /// <summary>Validates a composition object (the plan's or an opening's): army units of the faction, weights 0 to 100, at least one above 0.</summary>
        /// <param name="compositionNode">The composition object.</param>
        /// <param name="faction">The faction the units must belong to.</param>
        /// <param name="content">Units to check IDs against.</param>
        private static SortedDictionary<string, int> ParseComposition(JsonValue compositionNode, FactionDefinition faction, ContentCatalog content)
        {
            // Weights by unit ID in ordinal order.
            SortedDictionary<string, int> composition = new SortedDictionary<string, int>(StringComparer.Ordinal);
            // Sum of all weights; must end above 0. Unit: weight. Range: 0 to 100 per unit.
            int totalWeight = 0;
            // One unit ID and its JSON weight, in file order. Range: the ID is a unit of the faction; the weight is checked as 0 to 100.
            foreach (KeyValuePair<string, JsonValue> pair in compositionNode.Members)
            {
                // Definition of the composition unit.
                UnitDefinition unit = RequireUnit(pair.Value, pair.Key, faction.Id, content);
                if (unit.Economy.FootprintTiles > 0 || unit.Hq != null || unit.Economy.CanGather)
                {
                    throw pair.Value.Fail("must be an army unit (mobile, not the HQ, not a worker; found '" + pair.Key + "')");
                }
                if (!IsTrainedByFaction(pair.Key, faction.Id, content))
                {
                    throw pair.Value.Fail("is not in the trains list of any " + faction.Id + " structure");
                }
                // Weight of this unit in the mix. Unit: weight. Range: 0 to 100.
                int weight = pair.Value.AsInt(0, 100);
                composition.Add(pair.Key, weight);
                totalWeight += weight;
            }
            if (totalWeight <= 0)
            {
                throw compositionNode.Fail("must give at least one unit a weight above 0");
            }
            return composition;
        }

        /// <summary>Validates the "openings" array of a faction plan (skirmish_ai_v4_rl.md L.2).</summary>
        /// <param name="node">The faction plan object.</param>
        /// <param name="faction">The plan's faction.</param>
        /// <param name="content">Units to check IDs against.</param>
        private static List<AiOpening> ParseOpenings(JsonValue node, FactionDefinition faction, ContentCatalog content)
        {
            // The openings in file order.
            List<AiOpening> openings = new List<AiOpening>();
            // IDs already used in this plan, to reject repeats.
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            // Sum of the weights. Unit: weight. Range: 0 to 1600.
            int totalWeight = 0;
            // JSON node of the "openings" key, for the count and weight errors. Range: an array (checked by RequireArray).
            JsonValue openingsNode = node.Require("openings");
            // Elements of the openings array, in file order. Unit: openings. Range: 0 to 16 (more is rejected below).
            IReadOnlyList<JsonValue> items = node.RequireArray("openings");
            if (items.Count > 16)
            {
                throw openingsNode.Fail("holds " + items.Count + " openings; at most 16 are allowed");
            }
            // One opening object, in file order. Range: an object with the nine opening keys.
            foreach (JsonValue item in items)
            {
                item.RejectUnknownKeys("id", "weight", "build_order", "composition", "handover_seconds", "expand_at_seconds", "early_expansion_seconds",
                    "attack_army_supply", "attack_min_supply");
                // The opening's ID.
                string id = item.RequireNonEmptyString("id");
                if (id.Length > 48 || !IsOpeningId(id))
                {
                    throw item.Require("id").Fail("must be 1 to 48 lower-case letters, digits or underscores (found '" + id + "')");
                }
                if (!ids.Add(id))
                {
                    throw item.Require("id").Fail("is used twice in this plan ('" + id + "')");
                }
                // Weight of this opening. Unit: weight. Range: 0 to 100.
                int weight = item.RequireInt("weight", 0, 100);
                totalWeight += weight;
                // JSON node of this opening's build_order, for error messages. Range: an array (checked by RequireArray).
                JsonValue orderNode = item.Require("build_order");
                // Elements of this opening's build_order. Unit: steps. Range: 1 to 20 (checked below).
                IReadOnlyList<JsonValue> orderItems = item.RequireArray("build_order");
                if (orderItems.Count < 1 || orderItems.Count > 20)
                {
                    throw orderNode.Fail("must hold 1 to 20 steps (found " + orderItems.Count + ")");
                }
                // Build order steps of this opening, parsed and checked against the faction. Unit: steps. Range: 1 to 20.
                List<AiBuildStep> steps = ParseSteps(orderNode, orderItems, faction, content);
                // JSON node of this opening's composition object. Range: a JSON object.
                JsonValue compositionNode = item.Require("composition");
                // Army weights of this opening by unit ID, in ordinal order. Unit: weight. Range: 0 to 100 each, sum above 0.
                SortedDictionary<string, int> composition = ParseComposition(compositionNode, faction, content);
                // Structures the build order builds, up to and including each step (for the requires check).
                SortedSet<string> built = new SortedSet<string>(StringComparer.Ordinal);
                // Index of the step being checked. Range: 0 to steps - 1.
                for (int i = 0; i < steps.Count; i++)
                {
                    // A structure ID the step's structure requires (Economy.Requires). Range: a structure ID of the faction.
                    foreach (string required in content.Units[steps[i].StructureId].Economy.Requires)
                    {
                        if (!built.Contains(required))
                        {
                            throw orderNode.Fail("step " + i + " builds '" + steps[i].StructureId + "', which requires '" + required + "' in an earlier step");
                        }
                    }
                    built.Add(steps[i].StructureId);
                }
                // One composition entry (unit ID and weight), in ordinal order. Range: weight 0 to 100.
                foreach (KeyValuePair<string, int> pair in composition)
                {
                    if (pair.Value <= 0)
                    {
                        continue;
                    }
                    // A structure ID the unit requires (Economy.Requires). Range: a structure ID of the faction.
                    foreach (string required in content.Units[pair.Key].Economy.Requires)
                    {
                        if (!built.Contains(required))
                        {
                            throw compositionNode.Require(pair.Key).Fail("requires '" + required + "', which this opening's build_order never builds");
                        }
                    }
                }
                openings.Add(new AiOpening(id, weight, steps.AsReadOnly(), composition, item.RequireInt("handover_seconds", 0, 3600),
                    item.RequireFix64("expand_at_seconds", "0", "1800"), item.RequireFix64("early_expansion_seconds", "0", "1800"),
                    item.RequireInt("attack_army_supply", 1, 200), item.RequireInt("attack_min_supply", 1, 200)));
            }
            if (openings.Count > 0 && totalWeight <= 0)
            {
                throw openingsNode.Fail("must give at least one opening a weight above 0");
            }
            return openings;
        }

        /// <summary>True when <paramref name="id"/> holds only lower-case ASCII letters, digits and underscores.</summary>
        private static bool IsOpeningId(string id)
        {
            foreach (char c in id)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when <paramref name="ids"/> holds <paramref name="id"/> (ordinal comparison).</summary>
        private static bool ContainsId(IReadOnlyCollection<string> ids, string id)
        {
            foreach (string known in ids)
            {
                if (string.Equals(known, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The unit definition <paramref name="id"/>; fails on <paramref name="node"/> when it is unknown or of another faction.</summary>
        private static UnitDefinition RequireUnit(JsonValue node, string id, string factionId, ContentCatalog content)
        {
            if (!content.Units.TryGetValue(id, out UnitDefinition? definition))
            {
                throw node.Fail("names an unknown unit '" + id + "'");
            }
            if (!string.Equals(definition.FactionId, factionId, StringComparison.Ordinal))
            {
                throw node.Fail("names '" + id + "', which belongs to faction '" + definition.FactionId + "', not '" + factionId + "'");
            }
            return definition;
        }

        /// <summary>True when some structure of the faction lists <paramref name="unitId"/> in its trains list.</summary>
        private static bool IsTrainedByFaction(string unitId, string factionId, ContentCatalog content)
        {
            foreach (UnitDefinition producer in content.Units.Values)
            {
                if (!string.Equals(producer.FactionId, factionId, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (string trained in producer.Economy.Trains)
                {
                    if (string.Equals(trained, unitId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>File name of <paramref name="path"/> without folders and without ".json"; empty when the path has no .json name.</summary>
        private static string StemOf(string path)
        {
            // Index of the last folder separator, or -1.
            int slash = path.LastIndexOf('/');
            // File name part of the path.
            string name = slash >= 0 ? path.Substring(slash + 1) : path;
            return name.EndsWith(".json", StringComparison.Ordinal) ? name.Substring(0, name.Length - ".json".Length) : string.Empty;
        }
    }
}
