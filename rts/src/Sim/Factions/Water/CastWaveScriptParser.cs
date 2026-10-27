// rts/src/Sim/Factions/Water/CastWaveScriptParser.cs
// Reads the cast_wave command from a command script. Part of Rts.Sim.Factions.Water.
// Registered through BuiltInMechanics.ScriptParsers so rts/src/Sim/Data/CommandScript.cs does not name the Water faction.
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Script reader for <c>{ "type": "cast_wave", "unit": id, "x": ..., "y": ... }</c>.</summary>
    public sealed class CastWaveScriptParser : IScriptCommandParser
    {
        /// <summary>The script type handled: "cast_wave".</summary>
        public string Type => CastWaveCommand.KindName;

        /// <summary>Builds a <see cref="CastWaveCommand"/>; unknown keys and bad fields throw <see cref="DataValidationException"/>.</summary>
        public Command Parse(JsonValue node, int tick, PlayerId issuer, uint sequence, string sourceName)
        {
            node.RejectUnknownKeys("type", "tick", "issuer", "sequence", "unit", "x", "y");
            return new CastWaveCommand(tick, issuer, sequence, new EntityId((uint)node.RequireInt("unit", 1, int.MaxValue)), CommandScript.ReadPoint(node));
        }
    }
}
