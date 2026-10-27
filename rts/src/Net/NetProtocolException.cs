// rts/src/Net/NetProtocolException.cs
// The one exception the protocol throws for malformed or unexpected network input. Part of Rts.Net (ADR-008).
// Thrown by the binary reader on truncated or out-of-range data, by the codecs on unknown kinds or bad fields, and
// by LockstepPeer on a message that breaks the protocol (wrong version, unknown peer). Hosts treat it as "this
// session is broken": log it and leave the match; it is never swallowed inside the protocol.
using System;

namespace Rts.Net
{
    /// <summary>Malformed or protocol-breaking network input. The message names what was wrong.</summary>
    public sealed class NetProtocolException : Exception
    {
        /// <summary>Builds the exception with a message naming the problem.</summary>
        /// <param name="message">What was wrong, for logs.</param>
        public NetProtocolException(string message) : base(message)
        {
        }

        /// <summary>Builds the exception wrapping the error that caused it.</summary>
        /// <param name="message">What was wrong, for logs.</param>
        /// <param name="inner">The underlying error.</param>
        public NetProtocolException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
