// rts/src/Sim/Commands/Codec/ICommandCodec.cs
// The per-kind command codec contract and a typed base class for implementing it. Part of Rts.Sim.Commands.Codec (ADR-008,
// ADR-009). A codec writes only a command's own parameters, in the order its constructor takes them, with CommandWriter's
// explicit primitives; the caller's framing (a lockstep bundle, a replay file) supplies and stores the kind, tick, issuer and
// sequence around the payload and hands them back to Decode. Mirrors IScriptCommandParser: the core codec set names no faction
// and a faction plug-in's command ships its own codec, registered from BuiltInMechanics.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Encodes and decodes the parameters of one command class. Stateless and deterministic.</summary>
    public interface ICommandCodec
    {
        /// <summary>The <see cref="Command.Kind"/> this codec handles, such as "move_unit". Range: non-empty snake_case, unique in a registry.</summary>
        string Kind { get; }

        /// <summary>The exact command class this codec handles. Range: a non-abstract subclass of <see cref="Command"/>, unique in a registry.</summary>
        Type CommandType { get; }

        /// <summary>Writes the command's parameters (not its kind, tick, issuer or sequence) in a fixed order.</summary>
        /// <param name="command">A command of <see cref="CommandType"/>; any other class throws <see cref="CommandCodecException"/>.</param>
        /// <param name="writer">The payload being built.</param>
        void Encode(Command command, CommandWriter writer);

        /// <summary>Reads the parameters written by <see cref="Encode"/> and builds the command with the scheduling fields the framing supplied.</summary>
        /// <param name="reader">Positioned at the first parameter.</param>
        /// <param name="tick">Tick the command applies at. Unit: ticks. Range: 0 upward.</param>
        /// <param name="issuer">Issuing player slot or None.</param>
        /// <param name="sequence">Per-issuer sequence number. Range: any uint.</param>
        Command Decode(CommandReader reader, int tick, PlayerId issuer, uint sequence);
    }

    /// <summary>Base class for a codec of one command class <typeparamref name="T"/>: does the type check and cast so subclasses only write and read fields.</summary>
    /// <typeparam name="T">The command class handled.</typeparam>
    public abstract class CommandCodec<T> : ICommandCodec where T : Command
    {
        /// <summary>The command kind handled. Range: non-empty snake_case.</summary>
        public abstract string Kind { get; }

        /// <summary>The command class handled: <typeparamref name="T"/>.</summary>
        public Type CommandType => typeof(T);

        /// <summary>Writes the parameters; throws <see cref="CommandCodecException"/> when the command is not a <typeparamref name="T"/>.</summary>
        public void Encode(Command command, CommandWriter writer)
        {
            if (!(command is T typed))
            {
                throw new CommandCodecException("Codec '" + Kind + "' was given a " + command.GetType().Name + " (kind '" + command.Kind + "').");
            }
            Write(typed, writer);
        }

        /// <summary>Reads the parameters and builds the command.</summary>
        public Command Decode(CommandReader reader, int tick, PlayerId issuer, uint sequence)
        {
            return Read(reader, tick, issuer, sequence);
        }

        /// <summary>Writes the parameters of <paramref name="command"/>.</summary>
        /// <param name="command">The typed command.</param>
        /// <param name="writer">The payload being built.</param>
        protected abstract void Write(T command, CommandWriter writer);

        /// <summary>Reads the parameters in the order <see cref="Write"/> wrote them and builds the command.</summary>
        /// <param name="reader">Positioned at the first parameter.</param>
        /// <param name="tick">Scheduled tick. Unit: ticks. Range: 0 upward.</param>
        /// <param name="issuer">Issuing player slot or None.</param>
        /// <param name="sequence">Per-issuer sequence number. Range: any uint.</param>
        protected abstract T Read(CommandReader reader, int tick, PlayerId issuer, uint sequence);
    }
}
