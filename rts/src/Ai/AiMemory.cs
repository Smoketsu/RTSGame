// rts/src/Ai/AiMemory.cs
// The per-controller memory of the skirmish AI (spec section 3): stance, attack threshold, regroup timer, build attempts,
// last-seen enemy structures and army, cleared spawns, recent hits on own units, and the command sequence counter. Part of
// Rts.Ai. This is NOT simulation state and is never hashed: it lives in the controller instance (no static fields), and it is
// a pure function of the simulation's public state, the team's vision and the AI's own seeded generator, so two runs with the
// same inputs build the same memory and submit the same commands (proven by identical harness STATE_HASH lines).
// Every collection is sorted (SortedDictionary / SortedSet) so iteration never depends on hash order.
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>What the army is doing.</summary>
    public enum AiStance : byte
    {
        /// <summary>Building up at the rally point until the attack threshold is reached.</summary>
        Massing = 0,

        /// <summary>Attack-moving toward the chosen enemy target.</summary>
        Attack = 1,

        /// <summary>Attack-moving to an enemy threat inside the own base.</summary>
        Defend = 2,

        /// <summary>Moving home after a lost engagement (transient: the same think switches to Massing with a regroup timer).</summary>
        Retreat = 3,
    }

    /// <summary>An enemy structure (or HQ) the AI has seen and not yet seen gone.</summary>
    public sealed class KnownStructure
    {
        /// <summary>Where it was last seen. Unit: tiles.</summary>
        public FixVec2 Position { get; set; }

        /// <summary>True for an enemy HQ (the target that wins the match).</summary>
        public bool IsHq { get; set; }

        /// <summary>Tick it was last seen. Unit: ticks. Range: 0 upward.</summary>
        public int LastSeenTick { get; set; }
    }

    /// <summary>A hit on an own unit, read from UnitDamagedEvent (own units only; the attacker is not used).</summary>
    public sealed class DamageReport
    {
        /// <summary>Tick of the latest hit. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; set; }

        /// <summary>Position of the own unit when it was hit. Unit: tiles.</summary>
        public FixVec2 Position { get; set; }
    }

    /// <summary>Everything one controller remembers between thinks. Mutable; owned by one controller.</summary>
    public sealed class AiMemory
    {
        /// <summary>Current army stance.</summary>
        public AiStance Stance { get; set; } = AiStance.Massing;

        /// <summary>Army supply needed for the next attack (attack_army_supply plus jitter), or -1 before the first draw. Unit: supply. Range: -1, or 1 to 240.</summary>
        public int AttackThreshold { get; set; } = -1;

        /// <summary>Army supply when the current attack started. Unit: supply. Range: 0 upward.</summary>
        public int AttackStartSupply { get; set; }

        /// <summary>No attack may start before this tick (set by a retreat). Unit: ticks. Range: 0 upward.</summary>
        public int RegroupUntilTick { get; set; }

        /// <summary>True when <see cref="AttackTarget"/> holds the point the army is attacking.</summary>
        public bool HasAttackTarget { get; set; }

        /// <summary>Point the current attack heads for. Unit: tiles.</summary>
        public FixVec2 AttackTarget { get; set; }

        /// <summary>Tick the current attack started. Unit: ticks. Range: 0 upward.</summary>
        public int AttackStartTick { get; set; }

        /// <summary>False while the attacking army is still gathering at the staging point short of the target; true once it pushes.</summary>
        public bool AttackStaged { get; set; }

        /// <summary>Tick of the last attempt to place each structure, by definition ID (ordinal). Unit: ticks.</summary>
        public SortedDictionary<string, int> LastBuildAttemptTick { get; } = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Enemy structures and HQs seen and not yet seen gone, by entity ID (ascending).</summary>
        public SortedDictionary<EntityId, KnownStructure> KnownEnemyStructures { get; } = new SortedDictionary<EntityId, KnownStructure>();

        /// <summary>True once an enemy army unit has been seen.</summary>
        public bool HasLastSeenEnemy { get; set; }

        /// <summary>Position of the enemy army unit nearest the own HQ when enemies were last seen. Unit: tiles.</summary>
        public FixVec2 LastSeenEnemyPosition { get; set; }

        /// <summary>Tick enemies were last seen. Unit: ticks. Range: 0 upward.</summary>
        public int LastSeenEnemyTick { get; set; }

        /// <summary>Index of the own start spawn (nearest map spawn to the HQ at the first think), or -1 before. Range: -1 to spawn count - 1.</summary>
        public int OwnSpawnIndex { get; set; } = -1;

        /// <summary>Enemy spawn indices the AI has seen empty (no known enemy structure near them).</summary>
        public SortedSet<int> ClearedSpawns { get; } = new SortedSet<int>();

        /// <summary>Recent hits on own units, by the own unit's ID (ascending).</summary>
        public SortedDictionary<EntityId, DamageReport> RecentDamage { get; } = new SortedDictionary<EntityId, DamageReport>();

        /// <summary>Highest tick whose events were read, or -1. Unit: ticks.</summary>
        public int LastEventTickRead { get; set; } = -1;

        /// <summary>Next command sequence number; unique for the controller's player across the match. Range: 0 to uint.MaxValue.</summary>
        public uint NextSequence { get; set; }

        /// <summary>Thinks run so far (for logs and tests). Unit: count. Range: 0 upward.</summary>
        public int ThinkCount { get; set; }

        /// <summary>Attacks started so far. Unit: count. Range: 0 upward.</summary>
        public int AttacksStarted { get; set; }

        /// <summary>Retreats ordered so far. Unit: count. Range: 0 upward.</summary>
        public int Retreats { get; set; }
    }
}
