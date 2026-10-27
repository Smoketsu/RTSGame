// rts/src/Sim/Commands/EconomyCommands.cs
// The economy commands: gather, train a unit, build a structure. Part of Rts.Sim.Commands. All three use the usual ownership
// rule (the unit must belong to the issuer; the script issuer, PlayerId.None, may order any unit) and spend only the owning
// player's resources. What the orders do each tick is documented in rts/src/Sim/Economy/EconomySystem.cs.
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Commands
{
    /// <summary>Orders a worker to gather from a resource field and carry the loads to the nearest drop-off until the field is empty.</summary>
    public sealed class GatherCommand : Command
    {
        /// <summary>The worker. Must exist, belong to the issuer (or the issuer is None) and have can_gather.</summary>
        public EntityId Unit { get; }

        /// <summary>Index of the field in the map's resource field list. Range: 0 to field count - 1, else rejected.</summary>
        public int FieldIndex { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public GatherCommand(int tick, PlayerId issuer, uint sequence, EntityId unit, int fieldIndex) : base(tick, issuer, sequence)
        {
            Unit = unit;
            FieldIndex = fieldIndex;
        }

        /// <summary>Machine-readable kind name: "gather".</summary>
        public override string Kind => "gather";

        /// <summary>
        /// Applies the command. Rejections, in order: unknown_unit, not_owner, cannot_gather, unknown_field, field_depleted, no_drop_off
        /// (only when the worker still carries a load and the owner has no drop-off). A worker that carries a load deposits it first.
        /// </summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Unit, out Unit? unit))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && unit.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!unit.Definition.Economy.CanGather)
            {
                return CommandResult.Reject("cannot_gather");
            }
            if (FieldIndex < 0 || FieldIndex >= sim.Resources.Count)
            {
                return CommandResult.Reject("unknown_field");
            }
            // The chosen field.
            ResourceFieldState field = sim.Resources[FieldIndex];
            if (field.IsDepleted)
            {
                return CommandResult.Reject("field_depleted");
            }
            unit.ClearCombatOrders();
            if (unit.CarriedAmount > 0)
            {
                // Nearest drop-off for the load the worker still carries.
                Unit? dropOff = EconomyRules.NearestDropOff(sim, unit.Owner, unit.Position);
                if (dropOff == null)
                {
                    return CommandResult.Reject("no_drop_off");
                }
                unit.SetGather(GatherState.ToDropOff, FieldIndex, 0);
                unit.SetMoveTarget(EconomyRules.StandablePointNear(sim, dropOff.Position, unit.EffectiveLayer));
                return CommandResult.Ok;
            }
            unit.SetGather(GatherState.ToField, FieldIndex, 0);
            unit.SetMoveTarget(EconomyRules.StandablePointNear(sim, sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y)), unit.EffectiveLayer));
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
            hasher.Write(FieldIndex);
        }
    }

    /// <summary>Queues a unit at a producer. The cost is paid and the supply reserved at once; the unit appears after its build_seconds.</summary>
    public sealed class TrainUnitCommand : Command
    {
        /// <summary>The producer. Must exist, belong to the issuer (or the issuer is None) and be able to train the unit.</summary>
        public EntityId Producer { get; }

        /// <summary>Definition ID of the unit to train, for example "skai.windrunner". Non-empty.</summary>
        public string DefinitionId { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public TrainUnitCommand(int tick, PlayerId issuer, uint sequence, EntityId producer, string definitionId) : base(tick, issuer, sequence)
        {
            Producer = producer;
            DefinitionId = definitionId ?? string.Empty;
        }

        /// <summary>Machine-readable kind name: "train_unit".</summary>
        public override string Kind => "train_unit";

        /// <summary>
        /// Applies the command. Rejections, in order: unknown_unit (the producer), not_owner, unknown_definition, under_construction,
        /// cannot_train (the definition is not in the producer's trains list, or the HQ's worker), hq_not_rooted, queue_full,
        /// insufficient_resources, supply_cap (supply used plus this unit's supply, queued units included, would exceed the cap).
        /// </summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Producer, out Unit? producer))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && producer.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!sim.Content.Units.TryGetValue(DefinitionId, out UnitDefinition? definition))
            {
                return CommandResult.Reject("unknown_definition");
            }
            if (!sim.Players.TryGet(producer.Owner, out Player? owner))
            {
                return CommandResult.Reject("not_owner");
            }
            if (producer.IsUnderConstruction)
            {
                return CommandResult.Reject("under_construction");
            }
            // True when the producer's data lists the unit, or the producer is an HQ and the unit is its worker.
            bool listed = false;
            foreach (string id in producer.Definition.Economy.Trains)
            {
                listed |= string.Equals(id, DefinitionId, System.StringComparison.Ordinal);
            }
            listed |= producer.Definition.Hq != null && string.Equals(producer.Definition.Hq.WorkerUnitId, DefinitionId, System.StringComparison.Ordinal);
            if (!listed)
            {
                return CommandResult.Reject("cannot_train");
            }
            if (producer.Definition.Hq != null && producer.HqState != HqState.Rooted)
            {
                return CommandResult.Reject("hq_not_rooted");
            }
            if (producer.ProductionQueue.Count >= sim.Tuning.Economy.MaxQueueLength)
            {
                return CommandResult.Reject("queue_full");
            }
            if (!owner.CanAfford(definition.CostCommon, definition.CostRare))
            {
                return CommandResult.Reject("insufficient_resources");
            }
            EconomySystem.RecomputeSupply(sim);
            if (owner.SupplyUsed + EconomyRules.SupplyOf(sim, definition) > owner.SupplyCap)
            {
                return CommandResult.Reject("supply_cap");
            }
            owner.Spend(definition.CostCommon, definition.CostRare);
            producer.EnqueueProduction(DefinitionId, EconomyRules.BuildTicks(sim, definition));
            EconomySystem.RecomputeSupply(sim);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Producer);
            hasher.Write(DefinitionId);
        }
    }

    /// <summary>
    /// Places a structure on a tile. The structure appears at once, under construction, at construction_start_health_fraction of its
    /// health, and finishes after its build_seconds. Phase 1 placeholder: the worker is not required to walk to the site or to stay.
    /// </summary>
    public sealed class BuildStructureCommand : Command
    {
        /// <summary>The worker that places the structure. Must exist, belong to the issuer (or the issuer is None) and have can_gather.</summary>
        public EntityId Worker { get; }

        /// <summary>Definition ID of the structure, for example "skai.roost". Non-empty; must have a footprint.</summary>
        public string DefinitionId { get; }

        /// <summary>Tile X of the site. Unit: tiles. Range: 0 to map width - 1, else rejected out_of_bounds.</summary>
        public int TileX { get; }

        /// <summary>Tile Y of the site. Unit: tiles. Range: 0 to map height - 1, else rejected out_of_bounds.</summary>
        public int TileY { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public BuildStructureCommand(int tick, PlayerId issuer, uint sequence, EntityId worker, string definitionId, int tileX, int tileY)
            : base(tick, issuer, sequence)
        {
            Worker = worker;
            DefinitionId = definitionId ?? string.Empty;
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>Machine-readable kind name: "build_structure".</summary>
        public override string Kind => "build_structure";

        /// <summary>
        /// Applies the command. Rejections, in order: unknown_unit (the worker), not_owner, cannot_build (not a worker),
        /// unknown_definition, not_a_structure (no footprint), wrong_faction, out_of_bounds, tile_blocked, tile_occupied (a ground or
        /// water unit stands on the tile), no_base (a static defence needs an own drop-off within base_radius_tiles of the site),
        /// static_defence_cap (static_defences_per_base_max of them already stand within base_radius_tiles of that drop-off),
        /// insufficient_resources, supply_cap.
        /// </summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Worker, out Unit? worker))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && worker.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!sim.Players.TryGet(worker.Owner, out Player? owner) || !worker.Definition.Economy.CanGather)
            {
                return CommandResult.Reject("cannot_build");
            }
            if (!sim.Content.Units.TryGetValue(DefinitionId, out UnitDefinition? definition))
            {
                return CommandResult.Reject("unknown_definition");
            }
            if (definition.Economy.FootprintTiles <= 0)
            {
                return CommandResult.Reject("not_a_structure");
            }
            if (!string.Equals(definition.FactionId, owner.FactionId, System.StringComparison.Ordinal))
            {
                return CommandResult.Reject("wrong_faction");
            }
            if (!sim.Terrain.Contains(TileX, TileY))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            // Site tile and its centre.
            int tile = sim.Terrain.IndexOf(TileX, TileY);
            FixVec2 centre = sim.Terrain.CentreOf(tile);
            if (sim.Terrain[tile].IsBlocked)
            {
                return CommandResult.Reject("tile_blocked");
            }
            foreach (Unit other in sim.Units.All)
            {
                if (other.EffectiveLayer != MovementLayer.Air && sim.Terrain.TileOf(other.Position) == tile)
                {
                    return CommandResult.Reject("tile_occupied");
                }
            }
            if (definition.Economy.IsStaticDefence)
            {
                CommandResult? defenceProblem = CheckStaticDefenceCap(sim, worker.Owner, centre);
                if (defenceProblem.HasValue)
                {
                    return defenceProblem.Value;
                }
            }
            if (!owner.CanAfford(definition.CostCommon, definition.CostRare))
            {
                return CommandResult.Reject("insufficient_resources");
            }
            EconomySystem.RecomputeSupply(sim);
            if (owner.SupplyUsed + EconomyRules.SupplyOf(sim, definition) > owner.SupplyCap)
            {
                return CommandResult.Reject("supply_cap");
            }
            owner.Spend(definition.CostCommon, definition.CostRare);
            // The new structure; the footprint blocker is added by the spawn path.
            Unit structure = sim.SpawnUnit(definition, worker.Owner, centre);
            structure.SetHealth(definition.MaxHealth * sim.Tuning.Economy.ConstructionStartHealthFraction);
            structure.SetConstructionTicks(EconomyRules.BuildTicks(sim, definition));
            EconomySystem.RecomputeSupply(sim);
            return CommandResult.Ok;
        }

        /// <summary>
        /// Checks the per-base static defence limit for a site. Returns null when the site is fine, otherwise the rejection:
        /// no_base when the owner has no drop-off within base_radius_tiles of the site, static_defence_cap when that drop-off's base
        /// already holds static_defences_per_base_max of the owner's static defences (under construction ones count).
        /// </summary>
        private static CommandResult? CheckStaticDefenceCap(Simulation sim, PlayerId owner, FixVec2 site)
        {
            // Squared base radius. Unit: squared tiles.
            Fix64 radius = sim.Tuning.Economy.BaseRadiusTiles;
            Fix64 radiusSquared = radius * radius;
            Unit? dropOff = EconomyRules.NearestDropOff(sim, owner, site);
            if (dropOff == null || (dropOff.Position - site).LengthSquared() > radiusSquared)
            {
                return CommandResult.Reject("no_base");
            }
            // Static defences the owner already has in that base. Unit: count.
            int count = 0;
            foreach (Unit other in sim.Units.All)
            {
                if (other.Owner == owner && other.Definition.Economy.IsStaticDefence && (other.Position - dropOff.Position).LengthSquared() <= radiusSquared)
                {
                    count++;
                }
            }
            if (count >= sim.Tuning.Supply.StaticDefencesPerBaseMax)
            {
                return CommandResult.Reject("static_defence_cap");
            }
            return null;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Worker);
            hasher.Write(DefinitionId);
            hasher.Write(TileX);
            hasher.Write(TileY);
        }
    }
}
