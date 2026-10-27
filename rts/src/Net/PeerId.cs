// rts/src/Net/PeerId.cs
// Identifies one network peer (one running game client) in a lockstep session. Part of Rts.Net (ADR-008).
// A peer is not a player: the lobby roster (LockstepRoster) says which player slots each peer controls, so a
// peer may run several players (a local 2v2 on one machine) and a player may be re-attached to a new peer
// instance when its owner reconnects. Peers sort by their number, which is the only order the protocol uses.
using System;

namespace Rts.Net
{
    /// <summary>Identifies one peer of a session. 0 to 254 are peers; <see cref="None"/> (255) means no peer.</summary>
    public readonly struct PeerId : IEquatable<PeerId>, IComparable<PeerId>
    {
        /// <summary>Peer index, 0 to 254, or 255 for none. Assigned by the lobby before tick 0.</summary>
        public readonly byte Value;

        /// <summary>Largest valid peer index (254).</summary>
        public const byte MaxValue = 254;

        /// <summary>The "no peer" value.</summary>
        public static readonly PeerId None = new PeerId(255);

        /// <summary>Wraps a peer index. Validation happens in the roster, not here.</summary>
        /// <param name="value">Peer index. Range: 0 to 255.</param>
        public PeerId(byte value)
        {
            Value = value;
        }

        /// <summary>True for a real peer, false for <see cref="None"/>.</summary>
        public bool IsValid => Value != None.Value;

        /// <summary>True when both values are equal.</summary>
        public bool Equals(PeerId other) => Value == other.Value;
        /// <summary>True when <paramref name="obj"/> is the same type with the same value.</summary>
        public override bool Equals(object? obj) => obj is PeerId other && Value == other.Value;
        /// <summary>Hash for dictionaries outside hot paths; the protocol itself only uses sorted collections.</summary>
        public override int GetHashCode() => Value;
        /// <summary>Orders by the numeric value.</summary>
        public int CompareTo(PeerId other) => Value.CompareTo(other.Value);
        /// <summary>True when the underlying values are equal.</summary>
        public static bool operator ==(PeerId a, PeerId b) => a.Value == b.Value;
        /// <summary>True when the underlying values differ.</summary>
        public static bool operator !=(PeerId a, PeerId b) => a.Value != b.Value;
        /// <summary>Short text for logs and tests, such as "N2".</summary>
        public override string ToString() => IsValid ? "N" + Value : "N-none";
    }
}
