// rts/src/Sim/Core/Ids.cs
// Strongly typed identifiers for entities, players and teams. Part of Rts.Sim.Core.
// Rules in docs/decisions/ADR-003-entity-ids.md: entity IDs count up from 1 and are never reused in a
// match; player and team IDs are lobby slot indices fixed before tick 0. All three sort by their number,
// which is the only iteration order the simulation uses.
using System;

namespace Rts.Sim.Core
{
    /// <summary>Identifies one simulated entity (unit, building, projectile). 0 means "no entity".</summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        /// <summary>The number. 0 = none; 1 and up are allocated in order by <see cref="EntityIdAllocator"/>.</summary>
        public readonly uint Value;

        /// <summary>The "no entity" value.</summary>
        public static readonly EntityId None = new EntityId(0);

        /// <summary>Wraps a number. Callers outside the allocator use this only for commands, events and tests.</summary>
        public EntityId(uint value)
        {
            Value = value;
        }

        /// <summary>True for any allocated ID, false for <see cref="None"/>.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EntityId other && Value == other.Value;
        public override int GetHashCode() => (int)Value;
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
        public override string ToString() => "E" + Value;
    }

    /// <summary>Identifies one player slot. 0 to 254 are players; <see cref="None"/> (255) is the neutral world.</summary>
    public readonly struct PlayerId : IEquatable<PlayerId>, IComparable<PlayerId>
    {
        /// <summary>Slot index, 0 to 254, or 255 for none.</summary>
        public readonly byte Value;

        /// <summary>Largest valid player slot index (254).</summary>
        public const byte MaxValue = 254;

        /// <summary>The "no player" value, used for neutral entities.</summary>
        public static readonly PlayerId None = new PlayerId(255);

        /// <summary>Wraps a slot index. Validation of the range happens in the match setup, not here.</summary>
        public PlayerId(byte value)
        {
            Value = value;
        }

        /// <summary>True for a real player slot, false for <see cref="None"/>.</summary>
        public bool IsValid => Value != None.Value;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is PlayerId other && Value == other.Value;
        public override int GetHashCode() => Value;
        public int CompareTo(PlayerId other) => Value.CompareTo(other.Value);
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? "P" + Value : "P-none";
    }

    /// <summary>Identifies one team. 0 to 254 are teams; <see cref="None"/> (255) means unaligned.</summary>
    public readonly struct TeamId : IEquatable<TeamId>, IComparable<TeamId>
    {
        /// <summary>Team index, 0 to 254, or 255 for none.</summary>
        public readonly byte Value;

        /// <summary>Largest valid team index (254).</summary>
        public const byte MaxValue = 254;

        /// <summary>The "no team" value.</summary>
        public static readonly TeamId None = new TeamId(255);

        /// <summary>Wraps a team index.</summary>
        public TeamId(byte value)
        {
            Value = value;
        }

        /// <summary>True for a real team, false for <see cref="None"/>.</summary>
        public bool IsValid => Value != None.Value;

        public bool Equals(TeamId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is TeamId other && Value == other.Value;
        public override int GetHashCode() => Value;
        public int CompareTo(TeamId other) => Value.CompareTo(other.Value);
        public static bool operator ==(TeamId a, TeamId b) => a.Value == b.Value;
        public static bool operator !=(TeamId a, TeamId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? "T" + Value : "T-none";
    }

    /// <summary>Hands out entity IDs in order, never reusing one. Owned by <see cref="Simulation"/>; its counter is hashed.</summary>
    public sealed class EntityIdAllocator
    {
        /// <summary>The value the next call to <see cref="Allocate"/> returns. Starts at 1; only ever increases.</summary>
        public uint Next { get; private set; } = 1;

        /// <summary>Returns the next ID and advances. Throws once all 2^32 - 1 IDs are used, which no match reaches.</summary>
        public EntityId Allocate()
        {
            if (Next == uint.MaxValue)
            {
                throw new InvalidOperationException("EntityIdAllocator exhausted.");
            }
            EntityId id = new EntityId(Next);
            Next++;
            return id;
        }

        /// <summary>Restores the counter, for replays and tests.</summary>
        /// <param name="next">Value for <see cref="Next"/>; must be at least 1.</param>
        public void Restore(uint next)
        {
            if (next == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(next), "Entity IDs start at 1.");
            }
            Next = next;
        }

        /// <summary>Feeds the counter into a state hash.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Next);
        }
    }
}
