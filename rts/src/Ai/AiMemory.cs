// rts/src/Ai/AiMemory.cs
// The per-controller memory of the skirmish AI (spec section 3): stance, attack threshold, regroup timer, build attempts,
// the attack group, the last threat tick, last-seen enemy structures (with their health) and army, cleared spawns, recent hits on own units, the
// known amount left in every resource field, the depots placed, the fields with no depot site and the HQ relocation state (skirmish_ai_v2.md
// package B), the scout (route, losses, timers), the enemy model of skirmish_ai_v3.md package B, the raid log of package C (rule SD), the harass, defend and fallen-back groups of package D, the variety stream of skirmish_ai_v4_rl.md package R, and the command sequence counter. Part of
// Rts.Ai. This is NOT simulation state and is never hashed: it lives in the controller instance (no static fields), and it is
// a pure function of the simulation's public state, the team's vision and the AI's own seeded generator, so two runs with the
// same inputs build the same memory and submit the same commands (proven by identical harness STATE_HASH lines).
// Every collection is sorted (SortedDictionary / SortedSet) so iteration never depends on hash order.
// ai_mechanic_use.md P4 adds WallTilesRefused and WallSpentCommon (rule WL, only with use_walls).
// ai_production_defence.md PD-1 adds DepositLog (rule PC's measured income, only with use_production_capacity); PD-3 adds RaidHits, OwnWorkerPositions and RaidScanTick (rules RS and RW, only with use_raid_workers); revision 2 adds RaidClosedFields, RaidSeenTick, RaidCarriedHits, RaidAnswered and DamageReport.IsArmy (all written only with a raid switch on, except IsArmy; none hashed).
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>What the army is doing.</summary>
    public enum AiStance : byte
    {
        /// <summary>Building up at the rally point until the attack threshold is reached.</summary>
        Massing = 0,

        /// <summary>Attack-moving toward the chosen enemy target.</summary>
        Attack = 1,

        /// <summary>Attack-moving to an enemy threat inside the own base.</summary>
        Defend = 2,

        /// <summary>Moving home after a lost engagement (transient: the same think switches to Massing with a regroup timer).</summary>
        Retreat = 3,
    }

    /// <summary>An enemy structure (or HQ) the AI has seen and not yet seen gone.</summary>
    public sealed class KnownStructure
    {
        /// <summary>Where it was last seen. Unit: tiles.</summary>
        public FixVec2 Position { get; set; }

        /// <summary>True for an enemy HQ (the target that wins the match).</summary>
        public bool IsHq { get; set; }

        /// <summary>Tick it was last seen. Unit: ticks. Range: 0 upward.</summary>
        public int LastSeenTick { get; set; }

        /// <summary>Definition ID of the structure (for example "skai.roost"), read by the enemy model to imply what it trains. Range: a unit ID of the data set.</summary>
        public string DefinitionId { get; set; } = string.Empty;

        /// <summary>Health divided by maximum health when last seen. Unit: fraction. Range: 0 to 1. Set by the intel rule every time the structure is seen; used by the kill rule (skirmish_ai_v2.md C.2 rule K).</summary>
        public Fix64 HealthFraction { get; set; } = Fix64.One;
    }

    /// <summary>What the AI believes about one enemy army unit type (skirmish_ai_v3.md B.3).</summary>
    public sealed class EnemyTypeEstimate
    {
        /// <summary>Units of the type believed alive when last seen, reduced by kills seen. Unit: units. Range: 0 to the most ever seen at once.</summary>
        public Fix64 Count { get; set; }

        /// <summary>Tick the count was last confirmed by a sighting. Unit: ticks. Range: 0 upward.</summary>
        public int LastSeenTick { get; set; }
    }

    /// <summary>A hit on an own unit, read from UnitDamagedEvent (own units only; the attacker is not used).</summary>
    public sealed class DamageReport
    {
        /// <summary>Tick of the latest hit. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; set; }

        /// <summary>Position of the own unit when it was hit. Unit: tiles.</summary>
        public FixVec2 Position { get; set; }

        /// <summary>Revision 2 (rule RS): true when the hit unit is an army unit (footprint 0, no Hq, cannot gather), set by the controller at each hit; read only by rule RS. Unit: boolean.</summary>
        public bool IsArmy { get; set; }
    }

    /// <summary>Everything one controller remembers between thinks. Mutable; owned by one controller.</summary>
    public sealed class AiMemory
    {
        /// <summary>Package B: the budget account of each queue and resource (index queue x 2 + resource, queue order of <see cref="AiBudget"/>), as closed by the last think. Unit: resource units. Range: 0 upward each; never above the matching want.</summary>
        public int[] BudgetAccounts { get; } = new int[AiBudget.Cells];

        /// <summary>Package B: the want of each queue and resource (the cost of the head item that failed on money in the last think; 0 for none), same layout. Unit: resource units. Range: 0 upward each.</summary>
        public int[] BudgetWants { get; } = new int[AiBudget.Cells];

        /// <summary>Package B: cumulative spending of each queue and resource since match start, same layout (read by the defence ceiling and the tests). Unit: resource units. Range: 0 upward each.</summary>
        public int[] BudgetSpent { get; } = new int[AiBudget.Cells];

        /// <summary>Package B: true while the free rare bank is idle (at or above budget_rare_surplus_on and not yet below budget_rare_surplus_off), so rare-cost units weigh more in the composition. Unit: boolean.</summary>
        public bool RareSurplusActive { get; set; }

        /// <summary>AI-P P2 hysteresis: true while rule SD step SD-4 is armed (it fired and its thresholds, relaxed by threat_defence_release_fraction, still hold).</summary>
        public bool ThreatDefenceArmed { get; set; }

        /// <summary>AI-P P2 hysteresis: ID of the base chosen as the exposed base at the last SD-4 evaluation, or <see cref="EntityId.None"/>. Range: an own drop-off ID or None.</summary>
        public EntityId ExposedBase { get; set; } = EntityId.None;

        /// <summary>Current army stance.</summary>
        public AiStance Stance { get; set; } = AiStance.Massing;

        /// <summary>Army supply needed for the next attack (attack_army_supply plus jitter), or -1 before the first draw. Unit: supply. Range: -1, or 1 to 240.</summary>
        public int AttackThreshold { get; set; } = -1;

        /// <summary>Army supply when the current attack started. Unit: supply. Range: 0 upward.</summary>
        public int AttackStartSupply { get; set; }

        /// <summary>No attack may start before this tick (set by a retreat). Unit: ticks. Range: 0 upward.</summary>
        public int RegroupUntilTick { get; set; }

        /// <summary>True when <see cref="AttackTarget"/> holds the point the army is attacking.</summary>
        public bool HasAttackTarget { get; set; }

        /// <summary>Point the current attack heads for. Unit: tiles.</summary>
        public FixVec2 AttackTarget { get; set; }

        /// <summary>Tick the current attack started. Unit: ticks. Range: 0 upward.</summary>
        public int AttackStartTick { get; set; }

        /// <summary>False while the attacking army is still gathering at the staging point short of the target; true once it pushes.</summary>
        public bool AttackStaged { get; set; }

        /// <summary>IDs of the army units sent on the current attack (skirmish_ai_v2.md C.2 rule G). Only these are ordered in Attack; empty outside an attack. Ascending ID.</summary>
        public SortedSet<EntityId> AttackGroup { get; } = new SortedSet<EntityId>();

        /// <summary>Tick of the latest think that saw a threat inside the base, or -1 before the first (skirmish_ai_v2.md C.2 rule D). Unit: ticks. Range: -1, or 0 upward.</summary>
        public int LastThreatTick { get; set; } = -1;

        /// <summary>Tick of the last attempt to place each structure, by definition ID (ordinal). Unit: ticks.</summary>
        public SortedDictionary<string, int> LastBuildAttemptTick { get; } = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Enemy structures and HQs seen and not yet seen gone, by entity ID (ascending).</summary>
        public SortedDictionary<EntityId, KnownStructure> KnownEnemyStructures { get; } = new SortedDictionary<EntityId, KnownStructure>();

        /// <summary>True once an enemy army unit has been seen.</summary>
        public bool HasLastSeenEnemy { get; set; }

        /// <summary>Position of the enemy army unit nearest the own HQ when enemies were last seen. Unit: tiles.</summary>
        public FixVec2 LastSeenEnemyPosition { get; set; }

        /// <summary>Tick enemies were last seen. Unit: ticks. Range: 0 upward.</summary>
        public int LastSeenEnemyTick { get; set; }

        /// <summary>Index of the own start spawn (nearest map spawn to the HQ at the first think), or -1 before. Range: -1 to spawn count - 1.</summary>
        public int OwnSpawnIndex { get; set; } = -1;

        /// <summary>Enemy spawn indices the AI has seen empty (no known enemy structure near them).</summary>
        public SortedSet<int> ClearedSpawns { get; } = new SortedSet<int>();

        /// <summary>Recent hits on own units, by the own unit's ID (ascending).</summary>
        public SortedDictionary<EntityId, DamageReport> RecentDamage { get; } = new SortedDictionary<EntityId, DamageReport>();

        /// <summary>
        /// Rule PC (ai_production_defence.md section 2, only with use_production_capacity): the own common deposits (tick, amount), in tick order, written by the controller from ResourceDepositedEvent and pruned at each think
        /// to ticks above now minus income_window_seconds. Unit: Tick in ticks (0 upward), Common in resource units (1 upward). Empty at the start and while the switch is off. Not hashed.
        /// </summary>
        public List<(int Tick, int Common)> DepositLog { get; } = new List<(int Tick, int Common)>();

        /// <summary>
        /// Rule RS (ai_production_defence.md section 2, only with use_raid_workers): the newest hit or worker loss near each own drop-off (key), as its tick and position; entries expire raid_active_seconds after their tick.
        /// Unit: Tick in ticks (0 upward), Position in tiles. Empty at the start and while the switch is off. Not hashed.
        /// </summary>
        public SortedDictionary<EntityId, (int Tick, FixVec2 Position)> RaidHits { get; } = new SortedDictionary<EntityId, (int Tick, FixVec2 Position)>();

        /// <summary>Rule RS (only with use_raid_workers): the own workers of the last think and where they stood, by ID (ascending); an ID that disappears is a worker loss at that position. Unit: tiles. Empty while the switch is off. Not hashed.</summary>
        public SortedDictionary<EntityId, FixVec2> OwnWorkerPositions { get; } = new SortedDictionary<EntityId, FixVec2>();

        /// <summary>Rule RS (only with use_raid_workers): tick of the last raid scan, or -1 before the first; only damage reports newer than this are new hits. Unit: ticks. Range: -1, or 0 upward.</summary>
        public int RaidScanTick { get; set; } = -1;

        /// <summary>Revision 2 rule RS (ai_production_defence.md R2.1.1, only with a raid switch): the field indices each active raid has closed, by the raid's drop-off ID; sets only grow while the raid is active. Unit: field indices, 0 to field count - 1. Empty while the switches are off. Not hashed.</summary>
        public SortedDictionary<EntityId, SortedSet<int>> RaidClosedFields { get; } = new SortedDictionary<EntityId, SortedSet<int>>();

        /// <summary>Revision 2 rule RS (R2.1.2, only with a raid switch): the last think tick at which an enemy army unit was seen in an active raid's area, by the raid's drop-off ID. Unit: ticks, 0 to now. Empty while the switches are off. Not hashed.</summary>
        public SortedDictionary<EntityId, int> RaidSeenTick { get; } = new SortedDictionary<EntityId, int>();

        /// <summary>Revision 2 rule RS (R2.1.3, only with a raid switch): hits on non-army own units that died between thinks, carried from the intel prune to the next raid scan, in ascending unit ID order, then emptied. Unit: Tick in ticks (0 upward), Position in tiles. Not hashed.</summary>
        public List<(int Tick, FixVec2 Position)> RaidCarriedHits { get; } = new List<(int Tick, FixVec2 Position)>();

        /// <summary>Revision 2 rule RA (R2.1.4, only with use_raid_response): the drop-off ID of the raid the defence answered at the last think, or <see cref="EntityId.None"/>. Not hashed.</summary>
        public EntityId RaidAnswered { get; set; } = EntityId.None;

        /// <summary>Highest tick whose events were read, or -1. Unit: ticks.</summary>
        public int LastEventTickRead { get; set; } = -1;

        /// <summary>Next command sequence number; unique for the controller's player across the match. Range: 0 to uint.MaxValue.</summary>
        public uint NextSequence { get; set; }

        /// <summary>Thinks run so far (for logs and tests). Unit: count. Range: 0 upward.</summary>
        public int ThinkCount { get; set; }

        /// <summary>Attacks started so far. Unit: count. Range: 0 upward.</summary>
        public int AttacksStarted { get; set; }

        /// <summary>Retreats ordered so far. Unit: count. Range: 0 upward.</summary>
        public int Retreats { get; set; }

        /// <summary>
        /// Last known amount left in each field, by field index (skirmish_ai_v2.md B.2). Set to every field's start amount on the first
        /// think; afterwards updated from the live amount only while the field's tile is visible to the team. Every economy rule reads
        /// amounts from here. Unit: resource units. Range: each 0 to the field's start amount; empty before the first think.
        /// </summary>
        public int[] KnownFieldRemaining { get; set; } = System.Array.Empty<int>();

        /// <summary>Own depots (drop-off structures, complete or under construction) counted at the latest think; sets when the next one is due (rule E). Unit: count. Range: 0 upward.</summary>
        public int DepotsPlaced { get; set; }

        /// <summary>Field indices around which no depot site was found; skipped as depot targets (rule E). Ascending. Range: each 0 to field count - 1; empty at the start.</summary>
        public SortedSet<int> UnbuildableFields { get; } = new SortedSet<int>();

        /// <summary>
        /// Tile indices (y * width + x) where the simulation rejected one of the AI's own build_structure commands as tile_blocked, which
        /// happens when an enemy structure stands there in the fog. Site search skips them while the tile is not visible (a visible tile
        /// is judged by what is seen, so a destroyed structure frees its tile). Ascending. Range: each 0 to tile count - 1; empty at the start.
        /// </summary>
        public SortedSet<int> RejectedSites { get; } = new SortedSet<int>();

        /// <summary>
        /// Tile indices (y * width + x) where an own build order was cancelled as unreachable (the worker could not walk to the site, for
        /// example a walled-off tile). Unlike <see cref="RejectedSites"/> they are skipped whether or not the tile is visible, for the
        /// rest of the match. Ascending. Range: each 0 to tile count - 1; empty at the start.
        /// </summary>
        public SortedSet<int> UnreachableSites { get; } = new SortedSet<int>();

        /// <summary>
        /// Rule WB (ai_mechanic_use.md M3.1, only with use_wall_breach): the last MoveUnreachableEvent of each own unit, by unit ID (ascending): the tick it was reported (ticks, 0 upward) and the goal it could not reach
        /// (tiles). Overwritten by a newer event; entries of dead units and older than wall_breach_memory_seconds are dropped by the intel step. Not hashed.
        /// </summary>
        public SortedDictionary<EntityId, (int Tick, FixVec2 Goal)> UnreachableGoals { get; } = new SortedDictionary<EntityId, (int Tick, FixVec2 Goal)>();

        /// <summary>
        /// Rule WL (ai_mechanic_use.md M3.2, only with use_walls): tile indices of wall-line tiles of this AI the simulation refused for good (out_of_bounds, tile_blocked or on_resource_field, from
        /// BuildLineTileSkippedEvent, or a gate build_structure rejected tile_blocked or on_resource_field); never ordered again. Ascending. Unit: tile indices (y * map width + x). Range: each 0 to map tile count - 1; empty at the start. Not hashed.
        /// </summary>
        public SortedSet<int> WallTilesRefused { get; } = new SortedSet<int>();

        /// <summary>Rule WL: common committed to wall lines and gates this match, counted at order time and never reduced by refunds. Unit: common resource units. Range: 0 to wall_max_spend_common plus one order's cost. Not hashed.</summary>
        public int WallSpentCommon { get; set; }

        /// <summary>
        /// Fields a relocation timed out on; skipped when choosing a relocation target until the HQ roots again, when the set is
        /// cleared (rule H step 4). Ascending. Range: each 0 to field count - 1; empty at the start.
        /// </summary>
        public SortedSet<int> FailedRelocationFields { get; } = new SortedSet<int>();

        /// <summary>True while the HQ is moving to a new field (rule H). Unit: boolean. Range: true or false; false at the start.</summary>
        public bool RelocationActive { get; set; }

        /// <summary>Field index the relocating HQ heads for. Range: 0 to field count - 1; meaningful only while <see cref="RelocationActive"/>.</summary>
        public int RelocationField { get; set; }

        /// <summary>Where the relocating HQ goes (beside the target field). Unit: tiles. Meaningful only while <see cref="RelocationActive"/>.</summary>
        public FixVec2 RelocationDestination { get; set; }

        /// <summary>Tick the current relocation (or its last re-target) started. Unit: ticks. Range: 0 upward.</summary>
        public int RelocationStartTick { get; set; }

        /// <summary>No new relocation may start before this tick (set when a relocation ends). Unit: ticks. Range: 0 upward.</summary>
        public int RelocationCooldownUntilTick { get; set; }

        /// <summary>Relocations completed so far (statistic). Unit: count. Range: 0 upward.</summary>
        public int Relocations { get; set; }

        /// <summary>Entity ID of the scout (rule S), or None when there is none. Range: None, or the ID of an own unit alive at the last think.</summary>
        public EntityId ScoutId { get; set; } = EntityId.None;

        /// <summary>True when the scout is a borrowed army flyer (released after its route), false for an own scout unit.</summary>
        public bool ScoutBorrowed { get; set; }

        /// <summary>Waypoints of the current scouting round; at most scout_route_max_waypoints. Unit: tiles. Range: 0 to 32 entries, each inside the map; empty before the first round.</summary>
        public List<FixVec2> ScoutRoute { get; } = new List<FixVec2>();

        /// <summary>Index into <see cref="ScoutRoute"/> of the waypoint being scouted; equal to the route length when the route is finished. Range: 0 to route length.</summary>
        public int ScoutWaypoint { get; set; }

        /// <summary>Tick the current (or last) scouting round started, or -1 before the first. Unit: ticks. Range: -1, or 0 upward.</summary>
        public int ScoutRoundStartTick { get; set; } = -1;

        /// <summary>No scout may be wanted before this tick (set when a scout dies). Unit: ticks. Range: 0 upward.</summary>
        public int NextScoutTick { get; set; }

        /// <summary>True when the scout has been given a move order this round (so <see cref="ScoutOrderPosition"/> and <see cref="ScoutOrderEffectiveTick"/> are valid). Unit: boolean. Range: true or false.</summary>
        public bool ScoutHasOrder { get; set; }

        /// <summary>Where the scout stood when the latest move order was given; a scout with no move target still standing here has an unreachable waypoint. Unit: tiles. Range: inside the map.</summary>
        public FixVec2 ScoutOrderPosition { get; set; }

        /// <summary>Tick at whose step the latest scout move order is applied (the think tick plus the command delay); the unreachable-waypoint skip waits until a later think. Unit: ticks. Range: 0 upward; meaningful only while <see cref="ScoutHasOrder"/>.</summary>
        public int ScoutOrderEffectiveTick { get; set; }

        /// <summary>Ticks between a think and the step that applies its commands, set by the controller before each think (the sink's schedule tick minus the current tick). Unit: ticks. Range: 0 to 20.</summary>
        public int CommandDelayTicks { get; set; }

        /// <summary>Positions of the units in <see cref="LastVisibleEnemyTypes"/> at the last think; a kill counts only while its victim's last tile is visible. Unit: tiles. Range: inside the map; same keys as <see cref="LastVisibleEnemyTypes"/>.</summary>
        public SortedDictionary<EntityId, FixVec2> LastVisibleEnemyPositions { get; } = new SortedDictionary<EntityId, FixVec2>();

        /// <summary>Scouts lost so far. Unit: count. Range: 0 upward.</summary>
        public int ScoutLosses { get; set; }

        /// <summary>Enemy army unit types seen, by definition ID (ordinal). Bounded by the number of unit definitions.</summary>
        public SortedDictionary<string, EnemyTypeEstimate> EnemyTypes { get; } = new SortedDictionary<string, EnemyTypeEstimate>(System.StringComparer.Ordinal);

        /// <summary>The visible enemy army units of the previous think and their definition IDs; replaced every think. Bounded by the visible count.</summary>
        public SortedDictionary<EntityId, string> LastVisibleEnemyTypes { get; } = new SortedDictionary<EntityId, string>();

        /// <summary>
        /// Enemy units reported killed since the last think (read from UnitKilledEvent by the controller), oldest first; cleared by each
        /// think. Bounded: at most max_commands_per_think x 8 entries are kept (the oldest are dropped).
        /// </summary>
        public List<EntityId> PendingEnemyKills { get; } = new List<EntityId>();

        /// <summary>First tick at which the enemy model reported air supply above 0, or -1 (read by the harness response check). Unit: ticks. Range: -1, or 0 upward.</summary>
        public int ModelAirKnownTick { get; set; } = -1;

        /// <summary>
        /// Rule SD (skirmish_ai_v3.md C.6): start ticks of the raids at each own drop-off, by the drop-off's ID (ascending), oldest first. Each list
        /// holds at most static_defence_after_raids + 1 entries; entries of drop-offs that no longer exist are removed every think. Unit: ticks. Range: 0 upward.
        /// </summary>
        public SortedDictionary<EntityId, List<int>> RaidLog { get; } = new SortedDictionary<EntityId, List<int>>();

        /// <summary>Rule SD: tick of the latest enemy activity at each own drop-off, by drop-off ID (ascending); same keys as <see cref="RaidLog"/>. Unit: ticks. Range: 0 upward.</summary>
        public SortedDictionary<EntityId, int> LastRaidTick { get; } = new SortedDictionary<EntityId, int>();

        /// <summary>Rule SD: where the enemy was at the latest activity at each own drop-off (mean of the visible raiders, else the hit unit's position), by drop-off ID; same keys as <see cref="RaidLog"/>. Unit: tiles. Range: inside the map.</summary>
        public SortedDictionary<EntityId, FixVec2> RaidPosition { get; } = new SortedDictionary<EntityId, FixVec2>();

        /// <summary>Rule HR (skirmish_ai_v3.md D.4): IDs of the units on a harass mission, ascending; empty when no squad is out. Excluded from the army's stance orders. Unit: unit IDs. Range: at most harass_squad_supply plus one unit's supply.</summary>
        public SortedSet<EntityId> HarassGroup { get; } = new SortedSet<EntityId>();

        /// <summary>Rule HR: the enemy drop-off the squad attacks. Unit: tiles. Range: inside the map; meaningful only while <see cref="HarassGroup"/> is not empty.</summary>
        public FixVec2 HarassTarget { get; set; }

        /// <summary>Rule HR: tick of the first think a squad member stood within stage_radius_tiles of the target, or -1 before. Unit: ticks. Range: -1, or 0 upward.</summary>
        public int HarassArrivedTick { get; set; } = -1;

        /// <summary>Rule HR: tick of the latest think that saw an enemy worker near the target (set at arrival too), for the linger test. Unit: ticks. Range: 0 upward; meaningful once <see cref="HarassArrivedTick"/> is set.</summary>
        public int HarassLastWorkerTick { get; set; }

        /// <summary>Rule HR: no squad may start before this tick. Unit: ticks. Range: 0 upward.</summary>
        public int NextHarassTick { get; set; }

        /// <summary>Measured defence (D.5): IDs of the units chosen to defend, ascending; held without orders until defend_hold_seconds after the last threat. Excluded from the army's stance orders. Unit: unit IDs. Range: own army units; empty at the start.</summary>
        public SortedSet<EntityId> DefendGroup { get; } = new SortedSet<EntityId>();

        /// <summary>Unit control (D.6.1): IDs of the units that fell back and have not yet reached home, ascending. Excluded from the army's stance orders. Unit: unit IDs. Range: own army units; empty at the start.</summary>
        public SortedSet<EntityId> FallenBack { get; } = new SortedSet<EntityId>();

        /// <summary>Unit control (D.6.1): the tick each fallen-back unit fell back, by unit ID (ascending); same keys as <see cref="FallenBack"/>. Unit: ticks. Range: 0 upward.</summary>
        public SortedDictionary<EntityId, int> FallenBackSince { get; } = new SortedDictionary<EntityId, int>();

        /// <summary>Rule HR: tick the current harass squad started. Unit: ticks. Range: 0 upward; meaningful only while <see cref="HarassGroup"/> is not empty.</summary>
        public int HarassStartTick { get; set; }

        /// <summary>Rule G (C2.3 hysteresis): true while the rare-worker cap is in force (set when the rare bank reaches rare_bank_soft_cap, cleared when it falls below rare_bank_return_cap). Unit: boolean. Range: true or false; false at the start.</summary>
        public bool RareCapActive { get; set; }

        /// <summary>
        /// The variety stream and its drawn values (skirmish_ai_v4_rl.md R.1); null when the profile asks for no variety (and for an
        /// AiMemory built by hand), in which case every rule reads the profile and plan unchanged. Set by SkirmishAiController.OnMatchStart.
        /// </summary>
        public AiVariety? Variety { get; set; }

        /// <summary>
        /// The game data of the lane-simulation fight predictor (fight_predictor_v2.md 4), built once by SkirmishAiController.OnMatchStart; null when the profile's
        /// predictor_model is formula (and for an AiMemory built by hand), in which case no caller reads it.
        /// </summary>
        public FightSimRules? FightSimRules { get; set; }
    }
}
