// rts/src/Sim/Commands/EconomyCommands.cs
// The economy commands: gather, train a unit, order a worker to build a structure (walk, place, then the structure builds alone), and build_wall_line (a dragged line of walls). Part of Rts.Sim.Commands. All three use the usual ownership
// rule (the unit must belong to the issuer; the script issuer, PlayerId.None, may order any unit) and spend only the owning
// player's resources. What the orders do each tick is documented in rts/src/Sim/Economy/EconomySystem.cs.
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
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
        /// An accepted order cancels the worker's pending build order (refund, reason new_order).
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
            if (unit.CarriedAmount > 0 && EconomyRules.NearestDropOff(sim, unit.Owner, unit.Position) == null)
            {
                return CommandResult.Reject("no_drop_off");
            }
            EconomySystem.CancelBuild(sim, unit, "new_order", false);
            EconomySystem.StartGatherLoop(sim, unit, FieldIndex);
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
        /// cannot_train (the definition is not in the producer's trains list, or the HQ's worker), hq_not_rooted, missing_prerequisite (the owner lacks a complete building the definition requires), queue_full,
        /// insufficient_resources, supply_cap (supply used plus this unit's supply, queued units included, would exceed the cap).
        /// A definition ID that names a research item takes the research path instead (research_and_unlocks.md R1.4): unknown_unit, not_owner,
        /// unknown_definition, not_owner, under_construction, cannot_train (not in the producer's researches), already_researched,
        /// already_queued, queue_full, insufficient_resources; hq_not_rooted, missing_prerequisite and supply_cap do not apply.
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
                return sim.Content.Researches.TryGetValue(DefinitionId, out ResearchDefinition? research)
                    ? ApplyResearch(sim, producer, research)
                    : CommandResult.Reject("unknown_definition");
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
            if (EconomyRules.FirstMissingRequirement(sim, producer.Owner, definition) != null)
            {
                return CommandResult.Reject("missing_prerequisite");
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

        /// <summary>
        /// The research path of <see cref="Apply"/> (research_and_unlocks.md R1.4); unknown_unit, not_owner and unknown_definition are already checked.
        /// Rejections, in order: not_owner (no player record), under_construction, cannot_train (not in the producer's researches), already_researched,
        /// already_queued (at any of the owner's producers), queue_full, insufficient_resources. No supply is used and no requires apply.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="producer">The building; exists and belongs to the issuer.</param>
        /// <param name="research">The research item named by <see cref="DefinitionId"/>.</param>
        private CommandResult ApplyResearch(Simulation sim, Unit producer, ResearchDefinition research)
        {
            if (!sim.Players.TryGet(producer.Owner, out Player? owner))
            {
                return CommandResult.Reject("not_owner");
            }
            if (producer.IsUnderConstruction)
            {
                return CommandResult.Reject("under_construction");
            }
            // True when the producer's data lists this research item.
            bool listed = false;
            foreach (string id in producer.Definition.Economy.Researches)
            {
                listed |= string.Equals(id, DefinitionId, System.StringComparison.Ordinal);
            }
            if (!listed)
            {
                return CommandResult.Reject("cannot_train");
            }
            if (owner.HasResearched(DefinitionId))
            {
                return CommandResult.Reject("already_researched");
            }
            if (EconomyRules.IsResearchQueued(sim, producer.Owner, DefinitionId))
            {
                return CommandResult.Reject("already_queued");
            }
            if (producer.ProductionQueue.Count >= sim.Tuning.Economy.MaxQueueLength)
            {
                return CommandResult.Reject("queue_full");
            }
            if (!owner.CanAfford(research.CostCommon, research.CostRare))
            {
                return CommandResult.Reject("insufficient_resources");
            }
            owner.Spend(research.CostCommon, research.CostRare);
            producer.EnqueueProduction(DefinitionId, EconomyRules.QueueEntryTicks(sim, DefinitionId));
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
    /// Orders a worker to build a structure on a tile. The cost is paid and the supply reserved at once; the worker walks to the site,
    /// spends worker_placement_seconds there, then the structure appears under construction (construction_start_health_fraction of
    /// its health) and finishes by its own build_seconds without the worker, who goes back to the gather field it left. The order is
    /// refunded if it is cancelled before the structure appears. See specs/worker_construction.md and EconomySystem.
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
        /// Applies the command. Rejections, in order: unknown_unit (the worker), not_owner, cannot_build (not a worker), then the site checks of
        /// <see cref="EconomyRules.BuildSiteProblem"/> (shared with the placement query), in order: unknown_definition, not_a_structure (no footprint), wrong_faction, missing_prerequisite (the owner lacks a complete building the definition requires), out_of_bounds, site_unexplored (the worker owner's team
        /// has never seen the tile; checked before tile_blocked so a rejection reveals nothing), tile_blocked, tile_occupied (a ground
        /// or water unit stands on the tile), site_pending (an own unit already has a build order for the tile), no_base (a static
        /// defence needs an own drop-off within base_radius_tiles of the site), static_defence_cap (static_defences_per_base_max of them,
        /// pending orders included, already stand within base_radius_tiles of that drop-off), insufficient_resources, supply_cap
        /// (pending orders count as supply used). A rejection changes nothing. On acceptance a previous order of the worker is
        /// cancelled (new_order, refunded), the cost is paid and the worker walks to the site.
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
            EconomySystem.RecomputeSupply(sim);
            // The first reason the site cannot be used, or null; the same code the placement query runs.
            string? problem = EconomyRules.BuildSiteProblem(sim, owner, DefinitionId, TileX, TileY, false);
            if (problem != null)
            {
                return CommandResult.Reject(problem);
            }
            // The structure's definition (BuildSiteProblem found it).
            UnitDefinition definition = sim.Content.Units[DefinitionId];
            // Site centre. Unit: tiles (fixed-point).
            FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(TileX, TileY));
            // Gather field to resume afterwards. Unit: field index. Range: -1 or 0 to field count - 1.
            int resume = worker.GatherState != GatherState.Idle ? worker.GatherFieldIndex : worker.BuildResumeFieldIndex;
            // A replaced order is refunded first (its event comes before the accepted event).
            EconomySystem.CancelBuild(sim, worker, "new_order", false);
            owner.Spend(definition.CostCommon, definition.CostRare);
            worker.ClearCombatOrders();
            worker.ClearGather();
            worker.SetBuildOrder(DefinitionId, TileX, TileY, resume);
            worker.SetMoveTarget(centre);
            EconomySystem.RecomputeSupply(sim);
            sim.Events.Add(new BuildOrderAcceptedEvent(sim.Clock.Tick, worker.Id, worker.Owner, DefinitionId, TileX, TileY, definition.CostCommon, definition.CostRare));
            return CommandResult.Ok;
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

    /// <summary>
    /// Cancels one entry of a producer's production queue (a unit or a research item) and refunds it in full (specs/interface_support.md I4). Script name
    /// <c>cancel_production</c>. Rejections, in order: unknown_unit, not_owner (the issuer is a player other than the owner; the issuer None may command any unit),
    /// not_owner (the owner has no player record), no_entry (the index is outside the queue, an empty queue included), entry_mismatch (the entry at the index is not
    /// <see cref="DefinitionId"/>). A rejection changes nothing. Allowed while the producer is paused (an Uprooted HQ) and when the head waits for a spawn tile.
    /// On acceptance: the cost is refunded to the producer's current owner, the entry is removed (removing the head loses its progress and the next entry starts
    /// at its full length), supply is recomputed and ProductionCancelledEvent is emitted.
    /// </summary>
    public sealed class CancelProductionCommand : Command
    {
        /// <summary>The producing building. Must exist and belong to the issuer (or the issuer is None).</summary>
        public EntityId Producer { get; }

        /// <summary>Queue position to cancel. Unit: entries. Range: 0 (the head) to the queue length - 1, else rejected no_entry.</summary>
        public int Index { get; }

        /// <summary>The unit or research ID the player saw at that position; guards against the queue having shifted under input delay. Non-empty.</summary>
        public string DefinitionId { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public CancelProductionCommand(int tick, PlayerId issuer, uint sequence, EntityId producer, int index, string definitionId) : base(tick, issuer, sequence)
        {
            Producer = producer;
            Index = index;
            DefinitionId = definitionId ?? string.Empty;
        }

        /// <summary>Machine-readable kind name: "cancel_production".</summary>
        public override string Kind => "cancel_production";

        /// <summary>Applies the command; see the class summary for the rejections and the effects.</summary>
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
            if (!sim.Players.TryGet(producer.Owner, out Player? owner))
            {
                return CommandResult.Reject("not_owner");
            }
            if (Index < 0 || Index >= producer.ProductionQueue.Count)
            {
                return CommandResult.Reject("no_entry");
            }
            if (!string.Equals(producer.ProductionQueue[Index], DefinitionId, System.StringComparison.Ordinal))
            {
                return CommandResult.Reject("entry_mismatch");
            }
            // Refund amounts: the entry's full cost from the unit or the research definition. Unit: resource units. Range: 0 to 100000.
            int refundCommon = 0;
            int refundRare = 0;
            if (sim.Content.Units.TryGetValue(DefinitionId, out UnitDefinition? unit))
            {
                refundCommon = unit.CostCommon;
                refundRare = unit.CostRare;
            }
            else if (sim.Content.Researches.TryGetValue(DefinitionId, out ResearchDefinition? research))
            {
                refundCommon = research.CostCommon;
                refundRare = research.CostRare;
            }
            owner.AddResource(ResourceKind.Common, refundCommon);
            owner.AddResource(ResourceKind.Rare, refundRare);
            if (Index == 0)
            {
                // The head's progress is lost; the next entry, if any, starts at its full length.
                producer.FinishProductionHead(producer.ProductionQueue.Count > 1 ? EconomyRules.QueueEntryTicks(sim, producer.ProductionQueue[1]) : 0);
            }
            else
            {
                producer.RemoveQueuedProduction(Index);
            }
            EconomySystem.RecomputeSupply(sim);
            sim.Events.Add(new ProductionCancelledEvent(sim.Clock.Tick, producer.Id, producer.Owner, DefinitionId, Index, refundCommon, refundRare));
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Producer);
            hasher.Write(Index);
            hasher.Write(DefinitionId);
        }
    }

    /// <summary>
    /// Orders a worker to build a dragged line of walls (or gates) one tile after another (walls_and_tech.md D.2). The whole line is paid when
    /// the order is given; the first tile becomes the worker's order and the others wait on <see cref="Unit.BuildQueue"/>.
    /// </summary>
    public sealed class BuildWallLineCommand : Command
    {
        /// <summary>The worker. Must exist, belong to the issuer (or the issuer is None) and have can_gather.</summary>
        public EntityId Worker { get; }

        /// <summary>Definition ID of the wall or gate, for example "skai.wind_baffle". Non-empty; must have is_wall.</summary>
        public string DefinitionId { get; }

        /// <summary>The tiles in build order. Unit: tile coordinates (x, y). Range: 1 to wall_line_max_tiles entries (0 to 255 on the wire); each coordinate checked against the map when the command applies.</summary>
        public IReadOnlyList<(int X, int Y)> Tiles { get; }

        /// <summary>Builds the command from its scheduling fields and parameters; the tile list is copied.</summary>
        public BuildWallLineCommand(int tick, PlayerId issuer, uint sequence, EntityId worker, string definitionId, IEnumerable<(int X, int Y)> tiles)
            : base(tick, issuer, sequence)
        {
            Worker = worker;
            DefinitionId = definitionId ?? string.Empty;
            Tiles = new List<(int X, int Y)>(tiles ?? throw new System.ArgumentNullException(nameof(tiles)));
        }

        /// <summary>Machine-readable kind name: "build_wall_line".</summary>
        public override string Kind => "build_wall_line";

        /// <summary>
        /// Applies the command. Rejections, in order: unknown_unit, not_owner, cannot_build, unknown_definition, not_a_wall (the definition lacks
        /// is_wall), wrong_faction, missing_prerequisite, no_tiles, too_many_tiles (more than wall_line_max_tiles); then, when no tile is kept, the
        /// reason of the first tile that was filtered (<see cref="EconomyRules.WallLineTileProblem"/> or duplicate_tile) or, when tiles survived
        /// the filter but none can be paid for, insufficient_resources or supply_cap. A rejection changes nothing. On acceptance the previous
        /// order and queue of the worker are cancelled (refunded), the cost of every kept tile is paid, supply is reserved and
        /// BuildOrderAcceptedEvent (cost = the whole amount) is followed by one BuildLineTileSkippedEvent per left-out tile in list order.
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
            if (!definition.Economy.IsWall)
            {
                return CommandResult.Reject("not_a_wall");
            }
            if (!string.Equals(definition.FactionId, owner.FactionId, System.StringComparison.Ordinal))
            {
                return CommandResult.Reject("wrong_faction");
            }
            if (EconomyRules.FirstMissingRequirement(sim, worker.Owner, definition) != null)
            {
                return CommandResult.Reject("missing_prerequisite");
            }
            if (Tiles.Count == 0)
            {
                return CommandResult.Reject("no_tiles");
            }
            if (Tiles.Count > sim.Tuning.Economy.WallLineMaxTiles)
            {
                return CommandResult.Reject("too_many_tiles");
            }
            // Reason each listed tile is left out, or null while it is kept; index = list position.
            string?[] reasons = new string?[Tiles.Count];
            // Reason of the first tile that failed the static filter, for the all-tiles-failed rejection. Null until one fails.
            string? firstReason = null;
            // Tiles that passed the static filter, as list positions in list order.
            List<int> passed = new List<int>();
            // List position being filtered. Unit: index. Range: 0 to Tiles.Count - 1.
            for (int i = 0; i < Tiles.Count; i++)
            {
                // Why this tile is out: the static check, else a repeat of an earlier list entry.
                string? problem = EconomyRules.WallLineTileProblem(sim, worker, Tiles[i].X, Tiles[i].Y);
                for (int earlier = 0; problem == null && earlier < i; earlier++)
                {
                    if (Tiles[earlier].X == Tiles[i].X && Tiles[earlier].Y == Tiles[i].Y)
                    {
                        problem = "duplicate_tile";
                    }
                }
                reasons[i] = problem;
                if (problem == null)
                {
                    passed.Add(i);
                }
                else if (firstReason == null)
                {
                    firstReason = problem;
                }
            }
            if (passed.Count == 0)
            {
                return CommandResult.Reject(firstReason ?? "no_tiles");
            }
            // Pay for the line up front, in list order: keep each tile while the running total is affordable and its supply fits; the
            // first tile that fails and every later one are dropped (walls_and_tech.md D.2 rule 3).
            EconomySystem.RecomputeSupply(sim);
            // Supply each tile of this structure reserves. Unit: supply. Range: 0 to 100.
            int supplyEach = EconomyRules.SupplyOf(sim, definition);
            // Number of tiles kept so far. Unit: tiles (count). Range: 0 to passed.Count.
            int kept = 0;
            // Reason the walk stopped, or null when every passed tile is kept.
            string? stopReason = null;
            foreach (int index in passed)
            {
                if (stopReason == null)
                {
                    if (!owner.CanAfford(definition.CostCommon * (kept + 1), definition.CostRare * (kept + 1)))
                    {
                        stopReason = "insufficient_resources";
                    }
                    else if (owner.SupplyUsed + supplyEach * (kept + 1) > owner.SupplyCap)
                    {
                        stopReason = "supply_cap";
                    }
                }
                if (stopReason == null)
                {
                    kept++;
                }
                else
                {
                    reasons[index] = stopReason;
                }
            }
            if (kept == 0)
            {
                return CommandResult.Reject(stopReason ?? "insufficient_resources");
            }
            // Gather field to resume afterwards. Unit: field index. Range: -1 or 0 to field count - 1.
            int resume = worker.GatherState != GatherState.Idle ? worker.GatherFieldIndex : worker.BuildResumeFieldIndex;
            // A replaced order and its queue are refunded first (their events come before the accepted event).
            EconomySystem.CancelBuild(sim, worker, "new_order", false);
            // Tile indices kept, in list order. Unit: tile index. Range: 0 to tile count - 1.
            List<int> keptTiles = new List<int>();
            foreach (int index in passed)
            {
                if (reasons[index] == null)
                {
                    keptTiles.Add(sim.Terrain.IndexOf(Tiles[index].X, Tiles[index].Y));
                }
            }
            owner.Spend(definition.CostCommon * kept, definition.CostRare * kept);
            worker.ClearCombatOrders();
            worker.ClearGather();
            worker.SetBuildOrder(DefinitionId, sim.Terrain.XOf(keptTiles[0]), sim.Terrain.YOf(keptTiles[0]), resume);
            worker.SetBuildQueue(keptTiles.GetRange(1, keptTiles.Count - 1));
            worker.SetMoveTarget(sim.Terrain.CentreOf(keptTiles[0]));
            EconomySystem.RecomputeSupply(sim);
            sim.Events.Add(new BuildOrderAcceptedEvent(sim.Clock.Tick, worker.Id, worker.Owner, DefinitionId, sim.Terrain.XOf(keptTiles[0]), sim.Terrain.YOf(keptTiles[0]),
                definition.CostCommon * kept, definition.CostRare * kept));
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (reasons[i] != null)
                {
                    sim.Events.Add(new BuildLineTileSkippedEvent(sim.Clock.Tick, worker.Id, worker.Owner, DefinitionId, Tiles[i].X, Tiles[i].Y, reasons[i]!, 0, 0));
                }
            }
            return CommandResult.Ok;
        }

        /// <summary>Feeds the kind, worker, definition, tile count and every tile into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Worker);
            hasher.Write(DefinitionId);
            hasher.Write(Tiles.Count);
            foreach ((int x, int y) in Tiles)
            {
                hasher.Write(x);
                hasher.Write(y);
            }
        }
    }
}
