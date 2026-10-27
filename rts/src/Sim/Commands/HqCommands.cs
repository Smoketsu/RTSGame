// rts/src/Sim/Commands/HqCommands.cs
// The two HQ commands: root and uproot. Part of Rts.Sim.Commands. Both use the usual ownership rule (the unit must belong to the
// issuer; the script issuer, PlayerId.None, may order any unit). What the states mean and how the countdowns run is documented in
// rts/src/Sim/Units/HqSystem.cs; the times are root_seconds and uproot_seconds in tuning/hq.json.
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Commands
{
    /// <summary>Orders an Uprooted HQ to root where it stands. It stops, then becomes Rooted after root_seconds.</summary>
    public sealed class RootHqCommand : Command
    {
        /// <summary>The HQ to root. Must exist, belong to the issuer (or the issuer is None), be an HQ and be Uprooted.</summary>
        public EntityId Unit { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public RootHqCommand(int tick, PlayerId issuer, uint sequence, EntityId unit) : base(tick, issuer, sequence)
        {
            Unit = unit;
        }

        /// <summary>Machine-readable kind name: "root_hq".</summary>
        public override string Kind => "root_hq";

        /// <summary>
        /// Applies the command. Rejections, in order: unknown_unit, not_owner, not_hq, already_rooted, transition_in_progress,
        /// not_near_resources (no field with resources left within hq_root_max_distance_to_field_tiles of the HQ).
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
            if (unit.Definition.Hq == null)
            {
                return CommandResult.Reject("not_hq");
            }
            if (unit.HqState == HqState.Rooted)
            {
                return CommandResult.Reject("already_rooted");
            }
            if (unit.HqState != HqState.Uprooted)
            {
                return CommandResult.Reject("transition_in_progress");
            }
            if (!HqSystem.IsNearResources(sim, unit.Position))
            {
                return CommandResult.Reject("not_near_resources");
            }
            unit.ClearMoveTarget();
            unit.ClearCombatOrders();
            HqSystem.BeginSwitch(sim, unit, HqState.Rooting);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
        }
    }

    /// <summary>Orders a Rooted HQ to uproot. It becomes Uprooted (mobile) after uproot_seconds; its weapon and drop-off role end at once.</summary>
    public sealed class UprootHqCommand : Command
    {
        /// <summary>The HQ to uproot. Must exist, belong to the issuer (or the issuer is None), be an HQ and be Rooted.</summary>
        public EntityId Unit { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public UprootHqCommand(int tick, PlayerId issuer, uint sequence, EntityId unit) : base(tick, issuer, sequence)
        {
            Unit = unit;
        }

        /// <summary>Machine-readable kind name: "uproot_hq".</summary>
        public override string Kind => "uproot_hq";

        /// <summary>Applies the command. Rejections, in order: unknown_unit, not_owner, not_hq, already_uprooted, transition_in_progress.</summary>
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
            if (unit.Definition.Hq == null)
            {
                return CommandResult.Reject("not_hq");
            }
            if (unit.HqState == HqState.Uprooted)
            {
                return CommandResult.Reject("already_uprooted");
            }
            if (unit.HqState != HqState.Rooted)
            {
                return CommandResult.Reject("transition_in_progress");
            }
            unit.ClearCombatOrders();
            HqSystem.BeginSwitch(sim, unit, HqState.Uprooting);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Unit);
        }
    }
}
