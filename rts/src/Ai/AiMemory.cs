// rts/src/Ai/AiMemory.cs
// The per-controller memory of the skirmish AI (spec section 3): stance, attack threshold, regroup timer, build attempts,
// the attack group, the last threat tick, last-seen enemy structures (with their health) and army, cleared spawns, recent hits on own units, the
// known amount left in every resource field, the depots placed, the fields with no depot site and the HQ relocation state (skirmish_ai_v2.md
// package B), the scout (route, losses, timers) and the enemy model of skirmish_ai_v3.md package B, and the command sequence counter. Part of
// Rts.Ai. This is NOT simulation state and is never hashed: it lives in the controller instance (no static fields), and it is
// a pure function of the simulation's public state, the team's vision and the AI's own seeded generator, so two runs with the
// same inputs build the same memory and submit the same commands (proven by identical harness STATE_HASH lines).
// Every collection is sorted (SortedDictionary / SortedSet) so iteration never depends on hash order.
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
    }

    /// <summary>Everything one controller remembers between thinks. Mutable; owned by one controller.</summary>
    public sealed class AiMemory
    {
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

        /// <summary>Entity ID of the scout (rule S), or None when there is none.</summary>
        public EntityId ScoutId { get; set; } = EntityId.None;

        /// <summary>True when the scout is a borrowed army flyer (released after its route), false for an own scout unit.</summary>
        public bool ScoutBorrowed { get; set; }

        /// <summary>Waypoints of the current scouting round; at most scout_route_max_waypoints. Unit: tiles.</summary>
        public List<FixVec2> ScoutRoute { get; } = new List<FixVec2>();

        /// <summary>Index into <see cref="ScoutRoute"/> of the waypoint being scouted; equal to the route length when the route is finished. Range: 0 to route length.</summary>
        public int ScoutWaypoint { get; set; }

        /// <summary>Tick the current (or last) scouting round started, or -1 before the first. Unit: ticks. Range: -1, or 0 upward.</summary>
        public int ScoutRoundStartTick { get; set; } = -1;

        /// <summary>No scout may be wanted before this tick (set when a scout dies). Unit: ticks. Range: 0 upward.</summary>
        public int NextScoutTick { get; set; }

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
    }
}
