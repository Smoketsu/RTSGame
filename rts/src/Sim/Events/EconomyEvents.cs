// rts/src/Sim/Events/EconomyEvents.cs
// The events the economy system emits: resources deposited, a field emptied, a unit finished training, a structure finished
// building. Part of Rts.Sim.Events. Listeners (UI, audio, AI observers) read them; they never change simulation state (rule 8).
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
}
