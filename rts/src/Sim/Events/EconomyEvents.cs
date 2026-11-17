// rts/src/Sim/Events/EconomyEvents.cs
// The events the economy system emits: resources deposited, a field emptied, a unit finished training, a structure finished
// building, and the build order events (accepted, started, cancelled, structure placed). Part of Rts.Sim.Events. Listeners (UI, audio, AI observers) read them; they never change simulation state (rule 8).
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Events
{
    /// <summary>A worker handed its load to a drop-off and the owner's stock grew.</summary>
    public sealed class ResourceDepositedEvent : SimEvent
    {
        /// <summary>The worker.</summary>
        public EntityId Worker { get; }

        /// <summary>The drop-off that received the load.</summary>
        public EntityId DropOff { get; }

        /// <summary>The player whose stock grew.</summary>
        public PlayerId Owner { get; }

        /// <summary>Which resource was deposited.</summary>
        public ResourceKind Resource { get; }

        /// <summary>Resource units deposited. Unit: resource units. Range: 1 to gather_amount_per_trip.</summary>
        public int Amount { get; }

        /// <summary>Builds the event from its fields.</summary>
        public ResourceDepositedEvent(int tick, EntityId worker, EntityId dropOff, PlayerId owner, ResourceKind resource, int amount) : base(tick)
        {
            Worker = worker;
            DropOff = dropOff;
            Owner = owner;
            Resource = resource;
            Amount = amount;
        }

        /// <summary>Machine-readable kind name: "resource_deposited".</summary>
        public override string Kind => "resource_deposited";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Worker + " deposited " + Amount + " " + Resource + " at " + DropOff + " for " + Owner + ".";
    }

    /// <summary>A resource field ran out: its remaining amount reached 0.</summary>
    public sealed class FieldDepletedEvent : SimEvent
    {
        /// <summary>Index of the field in the map's field list. Range: 0 to field count - 1.</summary>
        public int FieldIndex { get; }

        /// <summary>Which resource the field held.</summary>
        public ResourceKind Resource { get; }

        /// <summary>Builds the event from its fields.</summary>
        public FieldDepletedEvent(int tick, int fieldIndex, ResourceKind resource) : base(tick)
        {
            FieldIndex = fieldIndex;
            Resource = resource;
        }

        /// <summary>Machine-readable kind name: "field_depleted".</summary>
        public override string Kind => "field_depleted";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => "Field " + FieldIndex + " (" + Resource + ") is depleted.";
    }

    /// <summary>A producer finished a unit and it appeared next to the producer.</summary>
    public sealed class UnitTrainedEvent : SimEvent
    {
        /// <summary>The producer.</summary>
        public EntityId Producer { get; }

        /// <summary>The new unit.</summary>
        public EntityId Unit { get; }

        /// <summary>Definition ID of the new unit, for example "skai.windrunner".</summary>
        public string DefinitionId { get; }

        /// <summary>Builds the event from its fields.</summary>
        public UnitTrainedEvent(int tick, EntityId producer, EntityId unit, string definitionId) : base(tick)
        {
            Producer = producer;
            Unit = unit;
            DefinitionId = definitionId;
        }

        /// <summary>Machine-readable kind name: "unit_trained".</summary>
        public override string Kind => "unit_trained";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Producer + " trained " + Unit + " (" + DefinitionId + ").";
    }

    /// <summary>A structure placed by a worker reached full construction and is now active (producer, drop-off, supply).</summary>
    public sealed class StructureCompletedEvent : SimEvent
    {
        /// <summary>The structure.</summary>
        public EntityId Structure { get; }

        /// <summary>Definition ID of the structure, for example "skai.roost".</summary>
        public string DefinitionId { get; }

        /// <summary>Builds the event from its fields.</summary>
        public StructureCompletedEvent(int tick, EntityId structure, string definitionId) : base(tick)
        {
            Structure = structure;
            DefinitionId = definitionId;
        }

        /// <summary>Machine-readable kind name: "structure_completed".</summary>
        public override string Kind => "structure_completed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Structure + " (" + DefinitionId + ") completed.";
    }

    /// <summary>A build order was accepted: the cost is paid and the worker is walking to the site.</summary>
    public sealed class BuildOrderAcceptedEvent : SimEvent
    {
        /// <summary>The worker.</summary>
        public EntityId Worker { get; }

        /// <summary>The worker's owner.</summary>
        public PlayerId Owner { get; }

        /// <summary>Definition ID of the structure, for example "skai.roost".</summary>
        public string DefinitionId { get; }

        /// <summary>Site tile X. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int TileX { get; }

        /// <summary>Site tile Y. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int TileY { get; }

        /// <summary>Common resource paid. Unit: resource units. Range: 0 upward.</summary>
        public int CostCommon { get; }

        /// <summary>Rare resource paid. Unit: resource units. Range: 0 upward.</summary>
        public int CostRare { get; }

        /// <summary>Builds the event from its fields.</summary>
        public BuildOrderAcceptedEvent(int tick, EntityId worker, PlayerId owner, string definitionId, int tileX, int tileY, int costCommon, int costRare) : base(tick)
        {
            Worker = worker;
            Owner = owner;
            DefinitionId = definitionId;
            TileX = tileX;
            TileY = tileY;
            CostCommon = costCommon;
            CostRare = costRare;
        }

        /// <summary>Machine-readable kind name: "build_order_accepted".</summary>
        public override string Kind => "build_order_accepted";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Worker + " ordered to build " + DefinitionId + " at (" + TileX + ", " + TileY + ") for " + Owner + ", paid " + CostCommon + "/" + CostRare + ".";
    }

    /// <summary>A worker reached its site and began the placement countdown.</summary>
    public sealed class BuildOrderStartedEvent : SimEvent
    {
        /// <summary>The worker.</summary>
        public EntityId Worker { get; }

        /// <summary>Definition ID of the structure.</summary>
        public string DefinitionId { get; }

        /// <summary>Site tile X. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int TileX { get; }

        /// <summary>Site tile Y. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int TileY { get; }

        /// <summary>Length of the countdown. Unit: ticks. Range: 1 or more.</summary>
        public int PlacementTicks { get; }

        /// <summary>Builds the event from its fields.</summary>
        public BuildOrderStartedEvent(int tick, EntityId worker, string definitionId, int tileX, int tileY, int placementTicks) : base(tick)
        {
            Worker = worker;
            DefinitionId = definitionId;
            TileX = tileX;
            TileY = tileY;
            PlacementTicks = placementTicks;
        }

        /// <summary>Machine-readable kind name: "build_order_started".</summary>
        public override string Kind => "build_order_started";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Worker + " began placing " + DefinitionId + " at (" + TileX + ", " + TileY + ") for " + PlacementTicks + " ticks.";
    }

    /// <summary>A build order ended before the structure appeared and the cost was refunded.</summary>
    public sealed class BuildOrderCancelledEvent : SimEvent
    {
        /// <summary>The worker (it may no longer exist when the reason is worker_lost).</summary>
        public EntityId Worker { get; }

        /// <summary>The worker's owner.</summary>
        public PlayerId Owner { get; }

        /// <summary>Definition ID of the structure.</summary>
        public string DefinitionId { get; }

        /// <summary>Site tile X. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int TileX { get; }

        /// <summary>Site tile Y. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int TileY { get; }

        /// <summary>Why: new_order, worker_lost, site_blocked, site_occupied or unreachable.</summary>
        public string Reason { get; }

        /// <summary>Common resource refunded. Unit: resource units. Range: 0 upward.</summary>
        public int RefundCommon { get; }

        /// <summary>Rare resource refunded. Unit: resource units. Range: 0 upward.</summary>
        public int RefundRare { get; }

        /// <summary>Builds the event from its fields.</summary>
        public BuildOrderCancelledEvent(int tick, EntityId worker, PlayerId owner, string definitionId, int tileX, int tileY, string reason, int refundCommon, int refundRare) : base(tick)
        {
            Worker = worker;
            Owner = owner;
            DefinitionId = definitionId;
            TileX = tileX;
            TileY = tileY;
            Reason = reason;
            RefundCommon = refundCommon;
            RefundRare = refundRare;
        }

        /// <summary>Machine-readable kind name: "build_order_cancelled".</summary>
        public override string Kind => "build_order_cancelled";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Worker + "'s order for " + DefinitionId + " at (" + TileX + ", " + TileY + ") was cancelled (" + Reason + "), refund " + RefundCommon + "/" + RefundRare + ".";
    }

    /// <summary>The placement countdown ended and the structure appeared, under construction.</summary>
    public sealed class StructurePlacedEvent : SimEvent
    {
        /// <summary>The worker that placed it.</summary>
        public EntityId Worker { get; }

        /// <summary>The new structure.</summary>
        public EntityId Structure { get; }

        /// <summary>Definition ID of the structure.</summary>
        public string DefinitionId { get; }

        /// <summary>Site tile X. Unit: tiles. Range: 0 to map width - 1.</summary>
        public int TileX { get; }

        /// <summary>Site tile Y. Unit: tiles. Range: 0 to map height - 1.</summary>
        public int TileY { get; }

        /// <summary>Builds the event from its fields.</summary>
        public StructurePlacedEvent(int tick, EntityId worker, EntityId structure, string definitionId, int tileX, int tileY) : base(tick)
        {
            Worker = worker;
            Structure = structure;
            DefinitionId = definitionId;
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>Machine-readable kind name: "structure_placed".</summary>
        public override string Kind => "structure_placed";

        /// <summary>One-line description for logs.</summary>
        public override string Describe() => Worker + " placed " + Structure + " (" + DefinitionId + ") at (" + TileX + ", " + TileY + ").";
    }
}
