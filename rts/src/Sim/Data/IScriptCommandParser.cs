// rts/src/Sim/Data/IScriptCommandParser.cs
// Extension point for command script readers (rts/src/Sim/Data/CommandScript.cs). Part of Rts.Sim.Data.
// The core reader knows only the faction-neutral command kinds. A faction mechanic that adds its own command (for
// example the Water wave) ships a parser for its script "type" and hosts pass the parsers in; this keeps the core from
// naming any faction (architecture rule 4). BuiltInMechanics.ScriptParsers lists the parsers of the shipped mechanics.
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Sim.Data
{
    /// <summary>Reads one extra command kind from a script command object.</summary>
    public interface IScriptCommandParser
    {
        /// <summary>The script "type" text this parser handles, such as "cast_wave". Range: non-empty, lower-case letters and underscores, unique among the parsers passed to one script parse.</summary>
        string Type { get; }

        /// <summary>
        /// Builds the command from its JSON object. Must call <see cref="JsonValue.RejectUnknownKeys"/> for its own key set and
        /// throw <see cref="DataValidationException"/> (through <see cref="JsonValue.Fail"/>) on any bad field.
        /// </summary>
        /// <param name="node">The command object, already known to contain tick, issuer, sequence and a type equal to <see cref="Type"/>.</param>
        /// <param name="tick">The tick the command applies at. Unit: ticks. Range: 0 to CommandScript.MaxTicks.</param>
        /// <param name="issuer">The issuing player slot. Range: valid slot or PlayerId.None.</param>
        /// <param name="sequence">The per-issuer sequence number. Unit: integer sequence. Range: 0 to int.MaxValue.</param>
        /// <param name="sourceName">The script path, for messages about the file as a whole. Range: any non-empty text.</param>
        Command Parse(JsonValue node, int tick, PlayerId issuer, uint sequence, string sourceName);
    }
}
