// rts/src/Replay/ReplayExceptions.cs
// The exceptions of the replay library. Part of Rts.Replay. A replay problem is never a crash: hosts and ReplayCheck catch
// ReplayException and show its message. ReplayFormatException means the bytes are not a readable replay; ReplayRefusedException
// means the replay is well formed but must not be played with this build or this data set (ADR-009, compatibility rule).
using System;

namespace Rts.Replay
{
    /// <summary>Base class of every replay problem. The message is written for a player or a developer and names what is wrong.</summary>
    public class ReplayException : Exception
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        /// <param name="message">What is wrong and, where possible, what to do. Range: non-empty text.</param>
        public ReplayException(string message) : base(message)
        {
        }
    }

    /// <summary>The bytes are not a valid replay: wrong magic, truncated, unknown command kind, bad checksum or a field out of range.</summary>
    public sealed class ReplayFormatException : ReplayException
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        /// <param name="message">What is wrong with the file. Range: non-empty text.</param>
        public ReplayFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// The replay is well formed but is refused: its format version or its data digest differs from this build's. ReplayCheck
    /// reports this as exit code 2 and never starts a re-simulation that could only desync.
    /// </summary>
    public sealed class ReplayRefusedException : ReplayException
    {
        /// <summary>Creates the exception with a user-facing message.</summary>
        /// <param name="message">Why the replay is refused. Range: non-empty text.</param>
        public ReplayRefusedException(string message) : base(message)
        {
        }
    }
}
