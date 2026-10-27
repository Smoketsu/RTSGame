// rts/src/Sim/Commands/Command.cs
// Base class for every command: the only way anything changes simulation state.
// Part of Rts.Sim.Commands. Rule 2 of the architecture: player input, AI, network peers, replays and
// campaign scripts all build these objects and submit them for a set tick. Ordering within a tick is
// (issuer, sequence), see docs/decisions/ADR-003-entity-ids.md.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Commands
{
    /// <summary>Outcome of applying one command. Accepted commands changed state; rejected ones did nothing.</summary>
    public readonly struct CommandResult
    {
        /// <summary>True when the command was applied.</summary>
        public readonly bool Accepted;

        /// <summary>Short snake_case reason when rejected, for example "unknown_unit". Null when accepted.</summary>
        public readonly string? Reason;

        /// <summary>Stores the outcome; use <see cref="Ok"/> or <see cref="Reject"/>.</summary>
        private CommandResult(bool accepted, string? reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        /// <summary>The accepted result.</summary>
        public static readonly CommandResult Ok = new CommandResult(true, null);

        /// <summary>Builds a rejection with a reason.</summary>
        /// <param name="reason">Non-empty snake_case reason.</param>
        public static CommandResult Reject(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("A rejection needs a reason.", nameof(reason));
            }
            return new CommandResult(false, reason);
        }
    }

    /// <summary>
    /// An instruction scheduled for one tick. Immutable once built. Subclasses hold the parameters and implement
    /// <see cref="Apply"/>, which runs inside <see cref="Simulation.Step"/> and may use the simulation's mutation methods.
    /// </summary>
    public abstract class Command
    {
        /// <summary>Tick on which the command is applied. Range: 0 upward; must be at or after the current tick when submitted.</summary>
        public int Tick { get; }

        /// <summary>Player who issued it, or <see cref="PlayerId.None"/> for the match script or the world.</summary>
        public PlayerId Issuer { get; }

        /// <summary>Per-issuer counter giving commands a total order within a tick. Range: 0 upward; unique per (tick, issuer).</summary>
        public uint Sequence { get; }

        /// <summary>Short machine-readable name such as "move", used in logs, replays and tie-breaking.</summary>
        public abstract string Kind { get; }

        /// <summary>Stores the scheduling fields.</summary>
        /// <param name="tick">Scheduled tick, 0 or more.</param>
        /// <param name="issuer">Issuing player or None.</param>
        /// <param name="sequence">Issuer's command counter.</param>
        protected Command(int tick, PlayerId issuer, uint sequence)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Commands cannot be scheduled before tick 0.");
            }
            Tick = tick;
            Issuer = issuer;
            Sequence = sequence;
        }

        /// <summary>
        /// Applies the command to <paramref name="sim"/>. Called only by the simulation during its step, in order.
        /// Must be deterministic: no randomness except <see cref="Simulation.Rng"/>, no external state.
        /// </summary>
        public abstract CommandResult Apply(Simulation sim);

        /// <summary>Feeds the command's parameters into a hash; used by replay checks (Phase 2) and tests.</summary>
        public abstract void WriteHash(StateHasher hasher);

        /// <summary>Text such as "move#3 by P0 @t40" for logs.</summary>
        public override string ToString()
        {
            return Kind + "#" + Sequence + " by " + Issuer + " @t" + Tick;
        }
    }
}
