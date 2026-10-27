// rts/src/Sim/Commands/Codec/CommandCodecException.cs
// The one exception the shared command codec throws for bad data: a string that breaks the encoding rules, truncated or
// trailing bytes, an unknown command kind or type, an out-of-range enum byte, or a field value a command constructor rejects.
// Part of Rts.Sim.Commands.Codec (ADR-008, ADR-009). Rts.Net and Rts.Replay catch it and turn it into their own error type
// (NetProtocolException, ReplayFormatException), so the framing layers never see a second exception type from command bytes.
using System;

namespace Rts.Sim.Commands.Codec
{
    /// <summary>Raised when a command cannot be encoded or its bytes cannot be decoded. The message names the kind or type involved.</summary>
    public sealed class CommandCodecException : Exception
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="message">What went wrong, naming the command kind or type where known. Range: non-empty.</param>
        public CommandCodecException(string message) : base(message)
        {
        }

        /// <summary>Creates the exception with the error that caused it.</summary>
        /// <param name="message">What went wrong. Range: non-empty.</param>
        /// <param name="inner">The underlying error, for example a command constructor's ArgumentException.</param>
        public CommandCodecException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
