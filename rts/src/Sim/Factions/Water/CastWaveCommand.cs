// rts/src/Sim/Factions/Water/CastWaveCommand.cs
// The wave command of the Water faction: cast_wave. Part of Rts.Sim.Factions.Water (spec: rts/docs/specs/flooding.md section 5).
// It lives with the mechanic it drives so the core (Commands, Data) never names a faction; its script reader is
// CastWaveScriptParser, registered in BuiltInMechanics.ScriptParsers.
// Like every command it only validates and then asks the flooding mechanic (rts/src/Sim/Factions/Water) to do the work,
// so players, the AI, replays and campaign scripts all cast the same way.
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Sim.Factions.Water
{
    /// <summary>
    /// A unit casts the first-pass wave towards a point: water spreads over a strip in front of it and enemy non-air units
    /// in the strip are shoved back. Script name <c>cast_wave</c>. Rejections, in this order: unknown_unit, not_owner (the
    /// issuer is a player other than the owner; the script issuer None may command any unit), not_wave_caster (the unit's
    /// definition has can_cast_wave false, or its faction does not use the flooding mechanic), out_of_bounds (the target is
    /// beyond <see cref="Simulation.WorldBoundTiles"/>), on_cooldown (the caster cast less than wave_cooldown_seconds ago) and
    /// no_direction (the target lies in the caster's own tile).
    /// </summary>
    public sealed class CastWaveCommand : Command
    {
        /// <summary>Script and log name of the command kind.</summary>
        public const string KindName = "cast_wave";

        /// <summary>The casting unit. Must exist, belong to the issuer and be able to cast.</summary>
        public EntityId Caster { get; }

        /// <summary>Point the wave heads towards. Unit: tiles. Range: each component within ±<see cref="Simulation.WorldBoundTiles"/>, else rejected; must lie outside the caster's tile.</summary>
        public FixVec2 Target { get; }

        /// <summary>Builds the command from its scheduling fields and parameters.</summary>
        public CastWaveCommand(int tick, PlayerId issuer, uint sequence, EntityId caster, FixVec2 target)
            : base(tick, issuer, sequence)
        {
            Caster = caster;
            Target = target;
        }

        /// <summary>Machine-readable kind name: "cast_wave".</summary>
        public override string Kind => KindName;

        /// <summary>Applies the command; see the class summary for the rejections.</summary>
        public override CommandResult Apply(Simulation sim)
        {
            if (!sim.Units.TryGet(Caster, out Unit? caster))
            {
                return CommandResult.Reject("unknown_unit");
            }
            if (Issuer.IsValid && caster.Owner != Issuer)
            {
                return CommandResult.Reject("not_owner");
            }
            if (!sim.Mechanics.TryGetValue(FloodingMechanic.MechanicId, out IFactionMechanic? found) || !(found is FloodingMechanic mechanic) || !mechanic.CanCast(sim, caster))
            {
                return CommandResult.Reject("not_wave_caster");
            }
            if (!Simulation.IsWithinWorldBounds(Target))
            {
                return CommandResult.Reject("out_of_bounds");
            }
            if (mechanic.TryGetNextWaveTick(caster.Id, out int nextTick) && sim.Clock.Tick < nextTick)
            {
                return CommandResult.Reject("on_cooldown");
            }
            // Caster and target tiles; the wave needs a direction between them.
            int casterTile = sim.Terrain.TileOf(caster.Position);
            if (casterTile == sim.Terrain.TileOf(Target))
            {
                return CommandResult.Reject("no_direction");
            }
            // Offset from the caster to the target. Unit: tiles.
            Fix64 dx = Target.X - caster.Position.X;
            Fix64 dy = Target.Y - caster.Position.Y;
            // Direction steps: the dominant axis when one offset is more than twice the other, else the diagonal.
            int ux;
            int uy;
            if (dx.Abs() > dy.Abs() * 2)
            {
                ux = dx.Sign();
                uy = 0;
            }
            else if (dy.Abs() > dx.Abs() * 2)
            {
                ux = 0;
                uy = dy.Sign();
            }
            else
            {
                ux = dx.Sign();
                uy = dy.Sign();
            }
            if (ux == 0 && uy == 0)
            {
                return CommandResult.Reject("no_direction");
            }
            mechanic.CastWave(sim, caster, ux, uy);
            return CommandResult.Ok;
        }

        /// <summary>Feeds the command's parameters into a hash in a fixed order.</summary>
        public override void WriteHash(StateHasher hasher)
        {
            hasher.Write(Kind);
            hasher.Write(Caster);
            hasher.Write(Target);
        }
    }
}
