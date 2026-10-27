// rts/src/Sim/Factions/Water/CastWaveCodec.cs
// Codec for the Water faction's cast_wave command (CastWaveCommand.cs). Part of Rts.Sim.Factions.Water (ADR-008, ADR-009). It
// lives with the faction, registered only by BuiltInMechanics.CreateCodecRegistry, so the core codec set (CoreCommandCodecs) and
// the Net and Replay assemblies name no faction: the same split as CastWaveScriptParser and BuiltInMechanics.ScriptParsers.
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Codec for <see cref="CastWaveCommand"/>: caster, target.</summary>
    public sealed class CastWaveCodec : CommandCodec<CastWaveCommand>
    {
        /// <summary>"cast_wave".</summary>
        public override string Kind => CastWaveCommand.KindName;

        /// <summary>Writes caster and target.</summary>
        protected override void Write(CastWaveCommand command, CommandWriter writer)
        {
            writer.WriteEntityId(command.Caster);
            writer.WriteFixVec2(command.Target);
        }

        /// <summary>Reads caster and target.</summary>
        protected override CastWaveCommand Read(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId caster = reader.ReadEntityId();
            FixVec2 target = reader.ReadFixVec2();
            return new CastWaveCommand(tick, issuer, sequence, caster, target);
        }
    }
}
