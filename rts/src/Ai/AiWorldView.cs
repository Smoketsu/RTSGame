// rts/src/Ai/AiWorldView.cs
// What one AI player may know, copied out of the simulation once per think (spec section 4, "Allowed reads"): its own units
// and economy, enemy units ONLY through its team's vision (VisionSystem.CollectVisibleEnemies and IsTileVisible), the public
// map (spawns, resource fields and how much each still holds, terrain heights, ramps and blockers), the tide level and the
// clock. Part of Rts.Ai. The brain (AiBrain) reads only this view, never the Simulation, so it cannot see through the fog.
// Everything is in ascending entity ID or tile/field index order; numbers are integers and Fix64. Building the view reads the
// simulation and changes nothing (no RNG draw, no mutation), so the state hash is unaffected. GatherAmountPerTrip (resource per load)
// and WorkerPlacementTicks (ticks a worker needs to place a building) are the two public tuning reads rules EW and ED added.
// Differences from the spec's proposed read API: economy is read from Player (Common, Rare, SupplyUsed, SupplyCap), fields from
// sim.Resources, the HQ state from Unit.HqState, orders from Unit.HasMoveTarget / IsAttackMoving / GatherState, and the tide
// level is the 0-to-1 triangle of TideClock over tide_cycle_seconds. Phase 2 adds the public Planetary Cycle and Core state
// (planetary_cycles.md section 5: the active and next cycle, the allowed next cycles, who holds and captures the Core). The Core
// capture status (capture team, contested flag, progress, holder) is PUBLIC because the shared timeline bar shows it to everyone; the
// holder's pending pick is PRIVATE and is exposed only to the holder's own team (and for script picks).
// ai_mechanic_use.md P2 adds, for own units only, the move target (rule ST asks whether a unit already walks to its trail point).
// ai_mechanic_use.md P1 adds, for own units only, the wave and grounding ready ticks and the wave strip size. The wave ready tick is read
// through FloodingMechanic (Rts.Sim.Factions.Water), a plumbing exception to "the AI never names a faction type" (AiCommandWriter in
// AiIntent.cs is the other); the brain itself names no faction.
// ai_mechanic_use.md P4 adds the own workers' wall build queue (BuildQueue) and the public map ID (MapId), which rule WL uses to find the authored wall lines.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>A unit as the AI sees it. Own units show their orders; enemy units only what a player sees on screen.</summary>
    public sealed class AiUnitView
    {
        /// <summary>Entity ID. Range: 1 upward.</summary>
        public EntityId Id { get; }

        /// <summary>Owning player.</summary>
        public PlayerId Owner { get; }

        /// <summary>Unit type (immutable shared definition).</summary>
        public UnitDefinition Definition { get; }

        /// <summary>Position. Unit: tiles. Range: inside the map.</summary>
        public FixVec2 Position { get; }

        /// <summary>Current health. Unit: health points. Range: 0 to Definition.MaxHealth.</summary>
        public Fix64 Health { get; }

        /// <summary>Movement layer the unit uses now (Grounded flyers report Ground).</summary>
        public MovementLayer Layer { get; }

        /// <summary>HQ state; Rooted for units that are not HQs.</summary>
        public HqState HqState { get; }

        /// <summary>Own units: first tick the wave can be cast again (0 when never cast, or the unit cannot cast). Enemy units: 0. Unit: ticks. Range: 0 upward.</summary>
        public int WaveReadyTick { get; }

        /// <summary>Own units: first tick the grounding ability can be cast again (Unit.GroundingReadyTick). Enemy units: 0. Unit: ticks. Range: 0 upward.</summary>
        public int GroundingReadyTick { get; }

        /// <summary>Own workers: tile indices of the queued wall-line tiles after the current build order, head first (a copy of Unit.BuildQueue). Enemy units: empty. Unit: tile indices (y * map width + x). Range: 0 to 255 entries, each 0 to map tile count - 1.</summary>
        public IReadOnlyList<int> BuildQueue { get; }

        /// <summary>True while a structure is being built.</summary>
        public bool IsUnderConstruction { get; }

        /// <summary>Own units: true while walking to a move target. Enemy units: always false (orders are hidden).</summary>
        public bool HasMoveTarget { get; }

        /// <summary>Own units: destination of the active move order (meaningful only while <see cref="HasMoveTarget"/>; zero otherwise). Enemy units: zero. Unit: tiles. Range: inside the map.</summary>
        public FixVec2 MoveTarget { get; }

        /// <summary>Own units: true while under an attack-move order. Enemy units: always false.</summary>
        public bool IsAttackMoving { get; }

        /// <summary>Own units: goal of the attack-move order (valid only while <see cref="IsAttackMoving"/>). Unit: tiles.</summary>
        public FixVec2 AttackMoveGoal { get; }

        /// <summary>Own units: the unit it is shooting or chasing, or None. Enemy units: always None.</summary>
        public EntityId AttackTarget { get; }

        /// <summary>Own units: true when <see cref="AttackTarget"/> was ordered (attack_unit). Enemy units: always false.</summary>
        public bool IsAttackTargetForced { get; }

        /// <summary>Own workers: where they are in the gather loop. Enemy units: Idle.</summary>
        public GatherState GatherState { get; }

        /// <summary>Own workers: field index of the gather order, or -1. Range: -1 to field count - 1.</summary>
        public int GatherFieldIndex { get; }

        /// <summary>Own producers: queued unit IDs, head first (empty for enemies).</summary>
        public IReadOnlyList<string> Queue { get; }

        /// <summary>Own workers: build order state (ToSite or Placing while a build order is pending). Enemy units: always None (an enemy's order is hidden).</summary>
        public BuildOrderState BuildState { get; }

        /// <summary>Own workers: definition ID of the pending build order, else "". Enemy units: always "".</summary>
        public string BuildDefinitionId { get; }

        /// <summary>Own workers: site tile column of the pending build order, else -1. Unit: tiles. Range: -1, or 0 to map width - 1.</summary>
        public int BuildTileX { get; }

        /// <summary>Own workers: site tile row of the pending build order, else -1. Unit: tiles. Range: -1, or 0 to map height - 1.</summary>
        public int BuildTileY { get; }

        /// <summary>True while the unit has a pending build order (walking to the site or placing).</summary>
        public bool HasBuildOrder => BuildState != BuildOrderState.None;

        /// <summary>True for a gatherer (can_gather).</summary>
        public bool IsWorker => Definition.Economy.CanGather;

        /// <summary>True for a structure (footprint above 0).</summary>
        public bool IsStructure => Definition.Economy.FootprintTiles > 0;

        /// <summary>True for an HQ unit.</summary>
        public bool IsHq => Definition.Hq != null;

        /// <summary>True for an army unit: mobile, not a worker, not an HQ.</summary>
        public bool IsArmy => !IsStructure && !IsHq && !IsWorker;

        /// <summary>Tile column of the position (floored). Unit: tiles.</summary>
        public int TileX => Position.X.FloorToInt();

        /// <summary>Tile row of the position (floored). Unit: tiles.</summary>
        public int TileY => Position.Y.FloorToInt();

        /// <summary>Creates a view entry; hosts and tests normally use <see cref="AiWorldView.Build"/>.</summary>
        public AiUnitView(EntityId id, PlayerId owner, UnitDefinition definition, FixVec2 position, Fix64 health, MovementLayer layer,
            HqState hqState, bool isUnderConstruction, bool hasMoveTarget, bool isAttackMoving, FixVec2 attackMoveGoal,
            EntityId attackTarget, bool isAttackTargetForced, GatherState gatherState, int gatherFieldIndex, IReadOnlyList<string> queue,
            BuildOrderState buildState = BuildOrderState.None, string buildDefinitionId = "", int buildTileX = -1, int buildTileY = -1,
            int waveReadyTick = 0, int groundingReadyTick = 0, FixVec2 moveTarget = default, IReadOnlyList<int>? buildQueue = null)
        {
            BuildQueue = buildQueue ?? Array.Empty<int>();
            MoveTarget = moveTarget;
            WaveReadyTick = waveReadyTick;
            GroundingReadyTick = groundingReadyTick;
            BuildState = buildState;
            BuildDefinitionId = buildDefinitionId;
            BuildTileX = buildTileX;
            BuildTileY = buildTileY;
            AttackTarget = attackTarget;
            IsAttackTargetForced = isAttackTargetForced;
            Id = id;
            Owner = owner;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Position = position;
            Health = health;
            Layer = layer;
            HqState = hqState;
            IsUnderConstruction = isUnderConstruction;
            HasMoveTarget = hasMoveTarget;
            IsAttackMoving = isAttackMoving;
            AttackMoveGoal = attackMoveGoal;
            GatherState = gatherState;
            GatherFieldIndex = gatherFieldIndex;
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>Copies an own unit, including its orders and production queue.</summary>
        /// <param name="unit">A unit of the AI's player.</param>
        /// <param name="waveReadyTick">The unit's wave ready tick from the flooding mechanic, 0 when none. Unit: ticks. Range: 0 upward.</param>
        internal static AiUnitView Own(Unit unit, int waveReadyTick = 0)
        {
            return new AiUnitView(unit.Id, unit.Owner, unit.Definition, unit.Position, unit.Health, unit.EffectiveLayer, unit.HqState,
                unit.IsUnderConstruction, unit.HasMoveTarget, unit.IsAttackMoving, unit.AttackMoveGoal, unit.AttackTarget,
                unit.IsAttackTargetForced, unit.GatherState,
                unit.GatherState == GatherState.Idle ? -1 : unit.GatherFieldIndex, new List<string>(unit.ProductionQueue).AsReadOnly(),
                unit.BuildState, unit.BuildDefinitionId, unit.BuildTileX, unit.BuildTileY, waveReadyTick, unit.GroundingReadyTick, unit.HasMoveTarget ? unit.MoveTarget : FixVec2.Zero, new List<int>(unit.BuildQueue).AsReadOnly());
        }

        /// <summary>Copies a visible enemy unit: only what a player sees (type, position, health, layer, HQ state, construction).</summary>
        /// <param name="unit">A unit the AI's team can see.</param>
        internal static AiUnitView Seen(Unit unit)
        {
            return new AiUnitView(unit.Id, unit.Owner, unit.Definition, unit.Position, unit.Health, unit.EffectiveLayer, unit.HqState,
                unit.IsUnderConstruction, false, false, FixVec2.Zero, EntityId.None, false, GatherState.Idle, -1, Array.Empty<string>());
        }
    }

    /// <summary>A resource field as public map knowledge: where it is, its kind, and how much it holds.</summary>
    public sealed class AiFieldView
    {
        /// <summary>Index in the map's resource_fields list. Range: 0 to field count - 1.</summary>
        public int Index { get; }

        /// <summary>Tile column. Unit: tiles.</summary>
        public int X { get; }

        /// <summary>Tile row. Unit: tiles.</summary>
        public int Y { get; }

        /// <summary>Common or rare.</summary>
        public ResourceKind Kind { get; }

        /// <summary>What is left. Unit: resource units. Range: 0 to Initial.</summary>
        public int Remaining { get; }

        /// <summary>Centre of the field's tile. Unit: tiles.</summary>
        public FixVec2 Centre { get; }

        /// <summary>What the field held at the start of the match (public map data). Unit: resource units. Range: 1 to 1000000.</summary>
        public int StartAmount { get; }

        /// <summary>Stores the field.</summary>
        public AiFieldView(int index, int x, int y, ResourceKind kind, int remaining, FixVec2 centre, int startAmount)
        {
            StartAmount = startAmount;
            Index = index;
            X = x;
            Y = y;
            Kind = kind;
            Remaining = remaining;
            Centre = centre;
        }
    }

    /// <summary>Everything one AI player may read on one think. Built by <see cref="Build"/>; read-only afterwards.</summary>
    public sealed class AiWorldView
    {
        /// <summary>The AI's player.</summary>
        public PlayerId Player { get; private set; }

        /// <summary>The AI's team (whose vision it uses).</summary>
        public TeamId Team { get; private set; }

        /// <summary>The AI player's faction ID.</summary>
        public string FactionId { get; private set; } = string.Empty;

        /// <summary>True while the player is Active (not defeated, surrendered or disconnected).</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// AI-H: the distinct faction IDs of the players on other teams, ordinal order (public: the match setup names every faction). Unit: faction IDs.
        /// Range: 0 or more entries; empty when the AI has no enemy. Settable inside Rts.Ai so tests can build other cases.
        /// </summary>
        public IReadOnlyList<string> EnemyFactionIds { get; internal set; } = Array.Empty<string>();

        /// <summary>The tick the commands will apply on (the simulation's current tick). Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; private set; }

        /// <summary>Simulation rate. Unit: ticks per second. Range: 1 upward.</summary>
        public int TicksPerSecond { get; private set; }

        /// <summary>Common resources in stock. Unit: resource units. Range: 0 upward.</summary>
        public int Common { get; private set; }

        /// <summary>Rare resources in stock. Unit: resource units. Range: 0 upward.</summary>
        public int Rare { get; private set; }

        /// <summary>Supply used, queued units included. Unit: supply. Range: 0 upward.</summary>
        public int SupplyUsed { get; private set; }

        /// <summary>
        /// Research IDs the AI player has finished, ordinal order: a copy of its own <see cref="Player.Researched"/> (own information only, nothing about
        /// enemies; research_and_unlocks.md R1.11 rule 11). Unit: research IDs. Range: 0 to the research items of the player's faction.
        /// </summary>
        public SortedSet<string> OwnResearched { get; private set; } = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Supply cap now. Unit: supply. Range: 0 to <see cref="SupplyCapLimit"/>.</summary>
        public int SupplyCap { get; private set; }

        /// <summary>Global supply cap (tuning/supply.json supply_cap). Unit: supply. Range: 1 upward.</summary>
        public int SupplyCapLimit { get; private set; }

        /// <summary>Supply any static defence costs (tuning/supply.json static_defence_supply_cost). Unit: supply. Range: 0 upward.</summary>
        public int StaticDefenceSupplyCost { get; private set; }

        /// <summary>Most static defences the simulation allows near one base (tuning/supply.json static_defences_per_base_max; AI-P P2). Unit: structures. Range: 0 to 20.</summary>
        public int StaticDefencesPerBaseMax { get; private set; }

        /// <summary>Radius of a base around its drop-off (tuning/economy.json base_radius_tiles; AI-P P1 counts producers inside it). Unit: tiles. Range: 2 to 64.</summary>
        public Fix64 BaseRadiusTiles { get; private set; }

        /// <summary>Largest production queue (tuning/economy.json max_queue_length). Unit: units. Range: 1 upward.</summary>
        public int MaxQueueLength { get; private set; }

        /// <summary>Workers that gather one common field at once (tuning/economy.json gather_slots_per_common_field). Unit: workers. Range: 1 to 16.</summary>
        public int GatherSlotsCommon { get; private set; }

        /// <summary>Workers that gather one rare field at once (tuning/economy.json gather_slots_per_rare_field). Unit: workers. Range: 1 to 16.</summary>
        public int GatherSlotsRare { get; private set; }

        /// <summary>Ticks one gathering trip holds a slot (gather_seconds_per_trip at the match rate). Unit: ticks. Range: 1 upward.</summary>
        public int GatherTicksPerTrip { get; private set; }

        /// <summary>Resource carried and deposited per gathering trip (tuning/economy.json gather_amount_per_trip; ai_economy_calc.md 2.1 "A"). Unit: resource units. Range: 1 to 100.</summary>
        public int GatherAmountPerTrip { get; private set; }

        /// <summary>Ticks a worker takes to be placed at a build site (tuning/economy.json worker placement time at the match rate; ai_economy_calc.md 2.1 "P"). Unit: ticks. Range: 0 upward.</summary>
        public int WorkerPlacementTicks { get; private set; }

        /// <summary>Distance within which a worker gathers or deposits: interact_range_tiles, never less than the arrival radius (as EconomySystem.InteractRange). Unit: tiles. Range: 0.5 to 4.</summary>
        public Fix64 InteractRangeTiles { get; private set; }

        /// <summary>Largest HQ-to-field distance for rooting (tuning/hq.json). Unit: tiles. Range: 0.5 to 64.</summary>
        public Fix64 HqRootMaxDistanceTiles { get; private set; }

        /// <summary>Length of the wave strip (tuning/flood.json wave_length_tiles). Unit: tiles. Range: 1 to 20.</summary>
        public int WaveLengthTiles { get; internal set; }

        /// <summary>Width of the wave strip (tuning/flood.json wave_width_tiles). Unit: tiles. Range: 1 to 9, odd.</summary>
        public int WaveWidthTiles { get; internal set; }

        /// <summary>ID of the map being played (Map.Id; public). Rule WL looks up the authored wall lines by it. Range: a loaded map ID.</summary>
        public string MapId { get; internal set; } = string.Empty;

        /// <summary>Tide level: 0 at low tide, 1 at high tide (triangle over tide_cycle_seconds). Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 TideLevel { get; private set; }

        /// <summary>True when the match has Planetary Cycles.</summary>
        public bool HasCycles { get; internal set; }

        /// <summary>ID of the cycle in force, or empty before the first shift.</summary>
        public string ActiveCycleId { get; internal set; } = string.Empty;

        /// <summary>ID of the locked next cycle, or empty until the lock tick.</summary>
        public string NextCycleId { get; internal set; } = string.Empty;

        /// <summary>True from the lock tick to the shift tick; picks are refused meanwhile.</summary>
        public bool IsNextCycleLocked { get; internal set; }

        /// <summary>
        /// ID of the accepted pick waiting for the lock, or empty. PRIVATE to the holder: <see cref="Build"/> fills it only when the
        /// pick was made by the AI's own team (the AI itself or a teammate) or by a script (<see cref="PendingPickTeam"/> None); an enemy
        /// holder's pending pick is never visible, so the AI cannot react to it before the lock announces it. Range: "" or a cycle ID.
        /// </summary>
        public string PendingPickId { get; internal set; } = string.Empty;

        /// <summary>
        /// Team that made the visible pending pick: the AI's own team, or None for a script pick (and for no pick). Never an enemy
        /// team (see <see cref="PendingPickId"/>). Lets the brain see that a teammate has already picked.
        /// </summary>
        public TeamId PendingPickTeam { get; internal set; } = TeamId.None;

        /// <summary>
        /// True when this AI is its team's pick captain: the lowest-ID player of its team still in the match. Only the captain emits a
        /// pick, so two AI teammates (2v2) that think on the same tick cannot overwrite each other's pick. Default true (a team of one).
        /// </summary>
        public bool IsTeamPickCaptain { get; internal set; } = true;

        /// <summary>IDs of the cycles that may start next (no repeated play style), ascending.</summary>
        public IReadOnlyList<string> AllowedNextCycleIds { get; internal set; } = Array.Empty<string>();

        /// <summary>The next shift that has not happened yet. Unit: ticks. Range: 0 when the match has no cycles.</summary>
        public int NextShiftTick { get; internal set; }

        /// <summary>True when the map has a Core.</summary>
        public bool HasCore { get; internal set; }

        /// <summary>Centre of the Core. Unit: tiles. Meaningless without a Core.</summary>
        public FixVec2 CoreCentre { get; internal set; }

        /// <summary>Capture radius of the Core. Unit: tiles. Range: 1 to 32; meaningless without a Core.</summary>
        public Fix64 CoreRadiusTiles { get; internal set; }

        /// <summary>The team holding the Core, or None.</summary>
        public TeamId CoreHolder { get; internal set; } = TeamId.None;

        /// <summary>The team currently capturing the Core, or None.</summary>
        public TeamId CoreCaptureTeam { get; internal set; } = TeamId.None;

        /// <summary>Counted ticks of the capturing team. Unit: ticks. Range: 0 to capture ticks - 1.</summary>
        public int CoreCaptureProgressTicks { get; internal set; }

        /// <summary>True while two or more teams stand in the Core.</summary>
        public bool IsCoreContested { get; internal set; }

        /// <summary>The AI team's cumulative hold time. Unit: ticks. Range: 0 to the win time.</summary>
        public int OwnHoldTicks { get; internal set; }

        /// <summary>Hold time that wins the match when the Core win is on. Unit: ticks. Range: 1 upward.</summary>
        public int CoreWinTicks { get; internal set; }

        /// <summary>True when the AI's team is the Core holder.</summary>
        public bool OwnTeamHoldsCore => Team.IsValid && CoreHolder == Team;

        /// <summary>Own units in ascending ID order.</summary>
        public IReadOnlyList<AiUnitView> OwnUnits { get; private set; } = Array.Empty<AiUnitView>();

        /// <summary>Enemy units the AI's team can see now, in ascending ID order (neutral units excluded).</summary>
        public IReadOnlyList<AiUnitView> VisibleEnemies { get; private set; } = Array.Empty<AiUnitView>();

        /// <summary>Resource fields in map order.</summary>
        public IReadOnlyList<AiFieldView> Fields { get; private set; } = Array.Empty<AiFieldView>();

        /// <summary>Map spawn points in slot order. Unit: tiles.</summary>
        public IReadOnlyList<FixVec2> Spawns { get; private set; } = Array.Empty<FixVec2>();

        /// <summary>Map width. Unit: tiles. Range: 8 to 1024.</summary>
        public int Width { get; private set; }

        /// <summary>Map height. Unit: tiles. Range: 8 to 1024.</summary>
        public int Height { get; private set; }

        /// <summary>Content catalog (unit definitions by ID), read-only.</summary>
        public ContentCatalog Content { get; }

        /// <summary>Height layer per tile, row-major (index = y * Width + x). Range: 0 to 7.</summary>
        private byte[] _heights = Array.Empty<byte>();

        /// <summary>Ramp flag per tile.</summary>
        private bool[] _ramps = Array.Empty<bool>();

        /// <summary>True where the tile has any blocker (cliff edge, footprint, ...).</summary>
        private bool[] _blocked = Array.Empty<bool>();

        /// <summary>True where an own or visible enemy non-air unit stands (a structure placed there would be rejected tile_occupied).</summary>
        private bool[] _occupied = Array.Empty<bool>();

        /// <summary>True where the AI's team has ever seen the tile.</summary>
        private bool[] _explored = Array.Empty<bool>();

        /// <summary>True where the AI's team sees the tile now.</summary>
        private bool[] _visible = Array.Empty<bool>();

        /// <summary>Only <see cref="Build"/> creates views.</summary>
        /// <param name="content">The match's content catalog.</param>
        private AiWorldView(ContentCatalog content)
        {
            Content = content;
        }

        /// <summary>
        /// Copies what <paramref name="player"/> may know out of <paramref name="sim"/>. Reads only: no command, no RNG draw, no
        /// mutation. Enemy units come only from the team's vision.
        /// </summary>
        /// <param name="sim">The simulation between steps.</param>
        /// <param name="player">The AI's player; must be in the match setup.</param>
        /// <param name="knownRemaining">
        /// The AI's remembered amount per field (AiMemory.KnownFieldRemaining), or null before the first think. A field whose tile the
        /// team does not see reports this amount in <see cref="AiFieldView.Remaining"/> (its start amount when null or too short), never
        /// the live amount. Unit: resource units. Range: each 0 upward.
        /// </param>
        public static AiWorldView Build(Simulation sim, PlayerId player, IReadOnlyList<int>? knownRemaining = null)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (!sim.Players.TryGet(player, out Player? self))
            {
                throw new ArgumentException("Player " + player + " is not in the match.", nameof(player));
            }
            // What the AI may know this think.
            AiWorldView view = new AiWorldView(sim.Content);
            view.Player = player;
            view.Team = self.Team;
            view.FactionId = self.FactionId;
            view.IsActive = self.IsActive;
            // Faction IDs of the enemy players, ordinal order, no duplicates.
            SortedSet<string> enemyFactions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Player other in sim.Players.All)
            {
                if (sim.Players.AreEnemies(player, other.Id))
                {
                    enemyFactions.Add(other.FactionId);
                }
            }
            view.EnemyFactionIds = new List<string>(enemyFactions).AsReadOnly();
            view.Tick = sim.Clock.Tick;
            view.TicksPerSecond = sim.Clock.TicksPerSecond;
            view.Common = self.Common;
            view.Rare = self.Rare;
            view.SupplyUsed = self.SupplyUsed;
            view.SupplyCap = self.SupplyCap;
            view.OwnResearched = new SortedSet<string>(self.Researched, StringComparer.Ordinal);
            view.SupplyCapLimit = sim.Tuning.Supply.SupplyCap;
            view.StaticDefenceSupplyCost = sim.Tuning.Supply.StaticDefenceSupplyCost;
            view.StaticDefencesPerBaseMax = sim.Tuning.Supply.StaticDefencesPerBaseMax;
            view.BaseRadiusTiles = sim.Tuning.Economy.BaseRadiusTiles;
            view.MaxQueueLength = sim.Tuning.Economy.MaxQueueLength;
            view.GatherSlotsCommon = sim.Tuning.Economy.GatherSlotsCommon;
            view.GatherSlotsRare = sim.Tuning.Economy.GatherSlotsRare;
            view.GatherTicksPerTrip = sim.Tuning.Economy.GatherTicksPerTrip(sim.Clock);
            view.GatherAmountPerTrip = sim.Tuning.Economy.GatherAmountPerTrip;
            view.WorkerPlacementTicks = sim.Tuning.Economy.WorkerPlacementTicks(sim.Clock);
            view.InteractRangeTiles = Fix64.Max(sim.Tuning.Economy.InteractRangeTiles, sim.Tuning.Terrain.ArrivalRadiusTiles);
            view.MapId = sim.Map.Id;
            view.WaveLengthTiles = sim.Tuning.Flood.WaveLengthTiles;
            view.WaveWidthTiles = sim.Tuning.Flood.WaveWidthTiles;
            view.HqRootMaxDistanceTiles = sim.Tuning.Hq.HqRootMaxDistanceToFieldTiles;
            // Tide cycle length. Unit: ticks. Range: 2 upward.
            int tideTicks = Math.Max(2, sim.Tuning.Match.TideCycleTicks(sim.Clock));
            view.TideLevel = Fix64.FromInt(TideClock.Triangle(sim.Clock.Tick, tideTicks)) / Fix64.FromInt(tideTicks);

            // Planetary Cycle and Core state. Public (shown on the shared timeline bar): the active cycle, the locked next cycle, the
            // Core capture status (who holds, who is capturing, progress, contested flag) and the hold totals. Private: the holder's
            // pending pick, which is exposed only to the holder's own team and for script picks (planetary_cycles.md section 5).
            CycleSystem cycles = sim.Cycles;
            view.HasCycles = cycles.IsEnabled;
            view.ActiveCycleId = cycles.ActiveCycleId;
            view.NextCycleId = cycles.NextCycleId;
            view.IsNextCycleLocked = cycles.IsNextLocked;
            if (cycles.PendingPickId.Length > 0 && (!cycles.PendingPickTeam.IsValid || cycles.PendingPickTeam == self.Team))
            {
                view.PendingPickId = cycles.PendingPickId;
                view.PendingPickTeam = cycles.PendingPickTeam;
            }
            // Pick captain: the lowest-ID Active member of the AI's team (so never a Disconnected AI itself). Active, not merely in the match, because
            // PickCycleCommand rejects a Disconnected issuer; a disconnected captain would otherwise block the team's pick.
            view.IsTeamPickCaptain = self.IsActive;
            foreach (PlayerId mate in sim.Players.PlayersOnTeam(self.Team))
            {
                if (mate == player)
                {
                    break;
                }
                // The teammate's player record.
                if (sim.Players.TryGet(mate, out Player? teammate) && teammate.IsActive)
                {
                    view.IsTeamPickCaptain = false;
                    break;
                }
            }
            view.NextShiftTick = cycles.NextShiftTick;
            // Allowed next cycle IDs in ascending order.
            List<string> allowedIds = new List<string>();
            if (cycles.IsEnabled)
            {
                foreach (CycleDefinition allowed in cycles.AllowedNextCycles())
                {
                    allowedIds.Add(allowed.Id);
                }
            }
            view.AllowedNextCycleIds = allowedIds.AsReadOnly();
            view.HasCore = sim.Map.Core != null;
            if (sim.Map.Core != null)
            {
                view.CoreCentre = sim.Map.Core.Centre;
                view.CoreRadiusTiles = sim.Map.Core.RadiusTiles;
            }
            view.CoreHolder = cycles.Holder;
            view.CoreCaptureTeam = cycles.CaptureTeam;
            view.CoreCaptureProgressTicks = cycles.CaptureProgressTicks;
            view.IsCoreContested = cycles.IsContested;
            view.OwnHoldTicks = cycles.HoldTicks(self.Team);
            view.CoreWinTicks = cycles.WinTicks;

            // The live terrain grid (read only).
            TerrainGrid terrain = sim.Terrain;
            view.Width = terrain.Width;
            view.Height = terrain.Height;
            view._heights = new byte[terrain.TileCount];
            view._ramps = new bool[terrain.TileCount];
            view._blocked = new bool[terrain.TileCount];
            view._occupied = new bool[terrain.TileCount];
            view._visible = new bool[terrain.TileCount];
            view._explored = new bool[terrain.TileCount];
            // Tile index being copied. Range: 0 to TileCount - 1.
            for (int index = 0; index < terrain.TileCount; index++)
            {
                // The tile being copied.
                TerrainTile tile = terrain[index];
                view._heights[index] = tile.Height;
                view._ramps[index] = tile.IsRamp;
                view._visible[index] = sim.Vision.IsTileVisible(self.Team, terrain.XOf(index), terrain.YOf(index));
                view._explored[index] = sim.Vision.IsTileExplored(self.Team, terrain.XOf(index), terrain.YOf(index));
                // Walls are terrain; a footprint (a structure, maybe an enemy one) is known only where the team sees the tile.
                view._blocked[index] = (tile.Blockers & TileBlockers.Wall) != 0 || ((tile.Blockers & (TileBlockers.Footprint | TileBlockers.BuiltWall | TileBlockers.Gate)) != 0 && view._visible[index]);
            }

            // foundMechanic (in the next statement): the mechanic registered under FloodingMechanic.MechanicId, set by the lookup. Range: any IFactionMechanic when the lookup succeeds, otherwise null.
            // The flooding mechanic, or null when the match has none (then no unit has a wave cooldown).
            FloodingMechanic? flooding = sim.Mechanics.TryGetValue(FloodingMechanic.MechanicId, out IFactionMechanic? foundMechanic) ? foundMechanic as FloodingMechanic : null;
            // Own units in ascending ID order.
            List<AiUnitView> own = new List<AiUnitView>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == player)
                {
                    // The unit's wave ready tick, 0 when it has not cast. Unit: ticks.
                    int waveReady = 0;
                    if (flooding != null && flooding.TryGetNextWaveTick(unit.Id, out int nextWave))
                    {
                        waveReady = nextWave;
                    }
                    own.Add(AiUnitView.Own(unit, waveReady));
                    view.MarkOccupied(terrain, unit);
                }
            }
            view.OwnUnits = own.AsReadOnly();

            // Visible enemies, through the vision API only, in ascending ID order.
            List<EntityId> visibleIds = new List<EntityId>();
            sim.Vision.CollectVisibleEnemies(self.Team, visibleIds);
            // Visible enemy units in ascending ID order.
            List<AiUnitView> enemies = new List<AiUnitView>();
            foreach (EntityId id in visibleIds)
            {
                if (sim.Units.TryGet(id, out Unit? unit) && unit.Owner.IsValid && sim.Players.AreEnemies(player, unit.Owner))
                {
                    enemies.Add(AiUnitView.Seen(unit));
                    view.MarkOccupied(terrain, unit);
                }
            }
            view.VisibleEnemies = enemies.AsReadOnly();

            // Resource fields in map order, public knowledge.
            List<AiFieldView> fields = new List<AiFieldView>();
            foreach (ResourceFieldState field in sim.Resources.All)
            {
                // Amount the AI may know: the live one only while the field's tile is visible, else what it remembers (or the start amount).
                int remaining = field.Remaining;
                if (!sim.Vision.IsTileVisible(self.Team, field.X, field.Y))
                {
                    remaining = knownRemaining != null && field.Index < knownRemaining.Count ? knownRemaining[field.Index] : field.Initial;
                }
                fields.Add(new AiFieldView(field.Index, field.X, field.Y, field.Kind, remaining, terrain.CentreOf(terrain.IndexOf(field.X, field.Y)), field.Initial));
            }
            view.Fields = fields.AsReadOnly();
            view.Spawns = new List<FixVec2>(sim.Map.Spawns).AsReadOnly();
            return view;
        }

        /// <summary>Marks the tile under a non-air unit as occupied.</summary>
        private void MarkOccupied(TerrainGrid terrain, Unit unit)
        {
            if (unit.EffectiveLayer == MovementLayer.Air)
            {
                return;
            }
            // Tile the unit stands on, or skip when off the map.
            int tile = terrain.TileOf(unit.Position);
            if (tile >= 0 && tile < _occupied.Length)
            {
                _occupied[tile] = true;
            }
        }

        /// <summary>True when (x, y) lies on the map.</summary>
        /// <param name="x">Tile column. Unit: tiles.</param>
        /// <param name="y">Tile row. Unit: tiles.</param>
        public bool Contains(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        /// <summary>Row-major tile index of (x, y); the caller checks <see cref="Contains"/> first.</summary>
        public int IndexOf(int x, int y)
        {
            return y * Width + x;
        }

        /// <summary>Centre of tile (x, y). Unit: tiles.</summary>
        public FixVec2 CentreOf(int x, int y)
        {
            return new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half);
        }

        /// <summary>Height layer of tile (x, y). Range: 0 to 7.</summary>
        public byte HeightAt(int x, int y)
        {
            return _heights[IndexOf(x, y)];
        }

        /// <summary>True when tile (x, y) is a ramp.</summary>
        public bool IsRamp(int x, int y)
        {
            return _ramps[IndexOf(x, y)];
        }

        /// <summary>True when tile (x, y) has any blocker.</summary>
        public bool IsBlocked(int x, int y)
        {
            return _blocked[IndexOf(x, y)];
        }

        /// <summary>True when an own or visible enemy non-air unit stands on tile (x, y).</summary>
        public bool IsOccupied(int x, int y)
        {
            return _occupied[IndexOf(x, y)];
        }

        /// <summary>True when the AI's team sees tile (x, y) now; false off the map.</summary>
        public bool IsVisible(int x, int y)
        {
            return Contains(x, y) && _visible[IndexOf(x, y)];
        }

        /// <summary>True when the AI's team has seen tile (x, y) at least once (the explored-ever grid); false off the map.</summary>
        public bool IsExplored(int x, int y)
        {
            return Contains(x, y) && _explored[IndexOf(x, y)];
        }

        /// <summary>Supply a unit of this type costs (static defences cost static_defence_supply_cost). Unit: supply. Range: 0 to 100.</summary>
        /// <param name="definition">The unit type.</param>
        public int SupplyOf(UnitDefinition definition)
        {
            return definition.Economy.IsStaticDefence ? StaticDefenceSupplyCost : definition.Supply;
        }

        /// <summary>Converts seconds to whole ticks at this match's rate, rounded to nearest. Unit: ticks. Range: 0 upward.</summary>
        /// <param name="seconds">Duration. Unit: seconds. Range: 0 upward.</param>
        public int SecondsToTicks(Fix64 seconds)
        {
            return (seconds * Fix64.FromInt(TicksPerSecond)).RoundToInt();
        }
    }
}
