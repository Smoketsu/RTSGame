// rts/src/Net/Codecs/CastWaveCodec.cs
// Codec for the Water faction's cast_wave command (rts/src/Sim/Factions/Water/CastWaveCommand.cs). Part of
// Rts.Net.Codecs (ADR-008). It lives in its own file, registered only by BuiltInCodecs, so the core codec set
// (CoreCommandCodecs) names no faction, the same split as CastWaveScriptParser and BuiltInMechanics.ScriptParsers.
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Factions.Water;

namespace Rts.Net.Codecs
{
    /// <summary>Codec for <see cref="CastWaveCommand"/>: caster, target.</summary>
    public sealed class CastWaveCodec : ICommandCodec
    {
        /// <summary>"cast_wave".</summary>
        public string Kind => CastWaveCommand.KindName;

        /// <summary>Writes caster and target.</summary>
        public void Encode(Command command, NetWriter writer)
        {
            // The typed command.
            CastWaveCommand cast = CoreCommandCodecs.Expect<CastWaveCommand>(command, Kind);
            writer.WriteEntityId(cast.Caster);
            writer.WriteFixVec2(cast.Target);
        }

        /// <summary>Reads caster and target.</summary>
        public Command Decode(NetReader reader, int tick, PlayerId issuer, uint sequence)
        {
            // Fields in wire order.
            EntityId caster = reader.ReadEntityId();
            FixVec2 target = reader.ReadFixVec2();
            return new CastWaveCommand(tick, issuer, sequence, caster, target);
        }
    }
}
