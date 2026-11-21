// rts/src/Campaign/ConditionDefinition.cs
// One node of a mission condition tree: the closed set of condition types of rts/docs/specs/campaign_scripting.md section 2, parsed and
// validated by MissionLoader and evaluated by ConditionEvaluator. Part of Rts.Campaign. Immutable. Engine-free; Fix64 and integers only.
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Campaign
{
    /// <summary>The condition types a mission may use. Range: the eight types of the spec; all_of and any_of nest.</summary>
    public enum ConditionKind
    {
        /// <summary>At least MinCount matching simulation events since the condition started counting.</summary>
        EventCount,

        /// <summary>At least MinCount live units of an owner (and definition) have their centre inside a circle now.</summary>
        UnitsInArea,

        /// <summary>A player's stock is at least both amounts now.</summary>
        ResourcesAtLeast,

        /// <summary>The owning objective (or, for a trigger, the mission) has been running for Seconds.</summary>
        SecondsActive,

        /// <summary>A camera_moved host signal with its focus inside a circle arrived since counting started (latched).</summary>
        CameraInArea,

        /// <summary>A selection_changed host signal arrived whose units include MinCount matching units (latched).</summary>
        SelectionContains,

        /// <summary>A player is in a given state now.</summary>
        PlayerStateIs,

        /// <summary>Every child is true.</summary>
        AllOf,

        /// <summary>At least one child is true.</summary>
        AnyOf,
    }

    /// <summary>A parsed condition node. Fields that do not belong to the node's <see cref="Kind"/> keep their defaults.</summary>
    public sealed class ConditionDefinition
    {
        /// <summary>Which condition this node is.</summary>
        public ConditionKind Kind { get; }

        /// <summary>Position of this node in the preorder numbering of its tree; indexes the runner's per-node counters. Unit: nodes. Range: 0 to NodeCount - 1 of the root.</summary>
        public int Index { get; }

        /// <summary>Number of nodes in this subtree including this one. Unit: nodes. Range: 1 to 73 (depth 3, at most 8 children per node).</summary>
        public int NodeCount { get; }

        /// <summary>Event kind name for EventCount, such as "resource_deposited". Range: one of the nine supported kinds; empty for other kinds.</summary>
        public string EventName { get; }

        /// <summary>Events or units needed. Unit: events or units. Range: 1 to 1000 (EventCount), 1 to 500 (UnitsInArea, SelectionContains); 0 for other kinds.</summary>
        public int MinCount { get; }

        /// <summary>True when an owner slot filter was written (the owner key of EventCount, UnitsInArea, SelectionContains).</summary>
        public bool HasOwner { get; }

        /// <summary>Owner slot filter, or the player slot of ResourcesAtLeast and PlayerStateIs. Range: a mission player slot; meaningful only when <see cref="HasOwner"/> (or for those two kinds).</summary>
        public PlayerId Owner { get; }

        /// <summary>Unit definition ID filter, or null when absent. Range: a unit definition in the data set.</summary>
        public string? Definition { get; }

        /// <summary>True when an entity ID filter was written (the unit key of EventCount).</summary>
        public bool HasUnit { get; }

        /// <summary>Entity ID filter. Range: 1 upward; meaningful only when <see cref="HasUnit"/>.</summary>
        public EntityId Unit { get; }

        /// <summary>Circle centre x. Unit: tiles. Range: 0 to the map width.</summary>
        public Fix64 X { get; }

        /// <summary>Circle centre y. Unit: tiles. Range: 0 to the map height.</summary>
        public Fix64 Y { get; }

        /// <summary>Circle radius. Unit: tiles. Range: 0.5 to 64; zero for kinds without a circle.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Common stock needed (ResourcesAtLeast). Unit: resource units. Range: 0 to 1000000.</summary>
        public int Common { get; }

        /// <summary>Rare stock needed (ResourcesAtLeast). Unit: resource units. Range: 0 to 1000000.</summary>
        public int Rare { get; }

        /// <summary>Running time needed (SecondsActive). Unit: seconds. Range: 0 to 3600.</summary>
        public Fix64 Seconds { get; }

        /// <summary>Player state to match (PlayerStateIs). Range: any PlayerState.</summary>
        public PlayerState State { get; }

        /// <summary>Children of AllOf and AnyOf, in file order. Range: 1 to 8 entries; empty for other kinds.</summary>
        public IReadOnlyList<ConditionDefinition> Children { get; }

        /// <summary>Builds the node; MissionLoader is the only caller.</summary>
        internal ConditionDefinition(ConditionKind kind, int index, int nodeCount, string eventName, int minCount, bool hasOwner, PlayerId owner, string? definition,
            bool hasUnit, EntityId unit, Fix64 x, Fix64 y, Fix64 radiusTiles, int common, int rare, Fix64 seconds, PlayerState state, IReadOnlyList<ConditionDefinition> children)
        {
            Kind = kind;
            Index = index;
            NodeCount = nodeCount;
            EventName = eventName;
            MinCount = minCount;
            HasOwner = hasOwner;
            Owner = owner;
            Definition = definition;
            HasUnit = hasUnit;
            Unit = unit;
            X = x;
            Y = y;
            RadiusTiles = radiusTiles;
            Common = common;
            Rare = rare;
            Seconds = seconds;
            State = state;
            Children = children;
        }
    }
}
