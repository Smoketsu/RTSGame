// rts/src/Ai/AiWorldView.cs
// What one AI player may know, copied out of the simulation once per think (spec section 4, "Allowed reads"): its own units
// and economy, enemy units ONLY through its team's vision (VisionSystem.CollectVisibleEnemies and IsTileVisible), the public
// map (spawns, resource fields and how much each still holds, terrain heights, ramps and blockers), the tide level and the
// clock. Part of Rts.Ai. The brain (AiBrain) reads only this view, never the Simulation, so it cannot see through the fog.
// Everything is in ascending entity ID or tile/field index order; numbers are integers and Fix64. Building the view reads the
// simulation and changes nothing (no RNG draw, no mutation), so the state hash is unaffected.
// Differences from the spec's proposed read API: economy is read from Player (Common, Rare, SupplyUsed, SupplyCap), fields from
// sim.Resources, the HQ state from Unit.HqState, orders from Unit.HasMoveTarget / IsAttackMoving / GatherState, and the tide
// level is the 0-to-1 triangle of TideClock over tide_cycle_seconds.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
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

        /// <summary>True while a structure is being built.</summary>
        public bool IsUnderConstruction { get; }

        /// <summary>Own units: true while walking to a move target. Enemy units: always false (orders are hidden).</summary>
        public bool HasMoveTarget { get; }

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
            EntityId attackTarget, bool isAttackTargetForced, GatherState gatherState, int gatherFieldIndex, IReadOnlyList<string> queue)
        {
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
        internal static AiUnitView Own(Unit unit)
        {
            return new AiUnitView(unit.Id, unit.Owner, unit.Definition, unit.Position, unit.Health, unit.EffectiveLayer, unit.HqState,
                unit.IsUnderConstruction, unit.HasMoveTarget, unit.IsAttackMoving, unit.AttackMoveGoal, unit.AttackTarget,
                unit.IsAttackTargetForced, unit.GatherState,
                unit.GatherState == GatherState.Idle ? -1 : unit.GatherFieldIndex, new List<string>(unit.ProductionQueue).AsReadOnly());
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

        /// <summary>Stores the field.</summary>
        public AiFieldView(int index, int x, int y, ResourceKind kind, int remaining, FixVec2 centre)
        {
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

        /// <summary>Supply cap now. Unit: supply. Range: 0 to <see cref="SupplyCapLimit"/>.</summary>
        public int SupplyCap { get; private set; }

        /// <summary>Global supply cap (tuning/supply.json supply_cap). Unit: supply. Range: 1 upward.</summary>
        public int SupplyCapLimit { get; private set; }

        /// <summary>Supply any static defence costs (tuning/supply.json static_defence_supply_cost). Unit: supply. Range: 0 upward.</summary>
        public int StaticDefenceSupplyCost { get; private set; }

        /// <summary>Largest production queue (tuning/economy.json max_queue_length). Unit: units. Range: 1 upward.</summary>
        public int MaxQueueLength { get; private set; }

        /// <summary>Largest HQ-to-field distance for rooting (tuning/hq.json). Unit: tiles. Range: 0.5 to 64.</summary>
        public Fix64 HqRootMaxDistanceTiles { get; private set; }

        /// <summary>Tide level: 0 at low tide, 1 at high tide (triangle over tide_cycle_seconds). Unit: fraction. Range: 0 to 1.</summary>
        public Fix64 TideLevel { get; private set; }

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
        public static AiWorldView Build(Simulation sim, PlayerId player)
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
            view.Tick = sim.Clock.Tick;
            view.TicksPerSecond = sim.Clock.TicksPerSecond;
            view.Common = self.Common;
            view.Rare = self.Rare;
            view.SupplyUsed = self.SupplyUsed;
            view.SupplyCap = self.SupplyCap;
            view.SupplyCapLimit = sim.Tuning.Supply.SupplyCap;
            view.StaticDefenceSupplyCost = sim.Tuning.Supply.StaticDefenceSupplyCost;
            view.MaxQueueLength = sim.Tuning.Economy.MaxQueueLength;
            view.HqRootMaxDistanceTiles = sim.Tuning.Hq.HqRootMaxDistanceToFieldTiles;
            // Tide cycle length. Unit: ticks. Range: 2 upward.
            int tideTicks = Math.Max(2, sim.Tuning.Match.TideCycleTicks(sim.Clock));
            view.TideLevel = Fix64.FromInt(TideClock.Triangle(sim.Clock.Tick, tideTicks)) / Fix64.FromInt(tideTicks);

            // The live terrain grid (read only).
            TerrainGrid terrain = sim.Terrain;
            view.Width = terrain.Width;
            view.Height = terrain.Height;
            view._heights = new byte[terrain.TileCount];
            view._ramps = new bool[terrain.TileCount];
            view._blocked = new bool[terrain.TileCount];
            view._occupied = new bool[terrain.TileCount];
            view._visible = new bool[terrain.TileCount];
            // Tile index being copied. Range: 0 to TileCount - 1.
            for (int index = 0; index < terrain.TileCount; index++)
            {
                // The tile being copied.
                TerrainTile tile = terrain[index];
                view._heights[index] = tile.Height;
                view._ramps[index] = tile.IsRamp;
                view._blocked[index] = tile.IsBlocked;
                view._visible[index] = sim.Vision.IsTileVisible(self.Team, terrain.XOf(index), terrain.YOf(index));
            }

            // Own units in ascending ID order.
            List<AiUnitView> own = new List<AiUnitView>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == player)
                {
                    own.Add(AiUnitView.Own(unit));
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
                fields.Add(new AiFieldView(field.Index, field.X, field.Y, field.Kind, field.Remaining, terrain.CentreOf(terrain.IndexOf(field.X, field.Y))));
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
