// rts/src/Sim/Factions/Skai/MomentumMechanic.cs
// Skai Riders signature mechanic: Momentum (spec: rts/docs/specs/momentum.md). Each eligible unit gains one stack per
// second spent moving (up to a cap), each stack adds a speed and damage bonus, stacks start to drain after a still
// delay, and all stacks are lost when the unit is Grounded or Slowed. Part of Rts.Sim.Factions.Skai.
// "Moving" is Unit.LastSelfMoveTick == the current tick, written by MovementSystem when a unit walks under its own
// order; the mechanic never compares positions, so wave pushes and landing relocation do not count.
// Eligible: a unit with a speed above 0 whose owner's faction has this mechanic's ID as its mechanic_id (resolved from
// the content catalog at match start; no faction is named here). The mechanic changes speed and damage only through
// the ISpeedModifier and IDamageModifier hooks it registers. Every number comes from rts/data/tuning/momentum.json.
// Determinism: integer counters, entries in a SortedDictionary visited in ascending unit ID, no random numbers.
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Factions.Skai
{
    /// <summary>Momentum plug-in, speed modifier and damage modifier in one object.</summary>
    public sealed class MomentumMechanic : IFactionMechanic, ISpeedModifier, IDamageModifier
    {
        /// <summary>Mechanic ID as named in data/factions/skai.json.</summary>
        public const string MechanicId = "momentum";

        /// <summary>Per-unit Momentum state, by unit ID in ascending order. Holds one entry per live eligible unit.</summary>
        private readonly SortedDictionary<EntityId, MomentumState> _states = new SortedDictionary<EntityId, MomentumState>();

        /// <summary>Faction IDs whose mechanic_id is Momentum, filled at match start. Membership test only.</summary>
        private readonly SortedSet<string> _eligibleFactions = new SortedSet<string>(System.StringComparer.Ordinal);

        /// <summary>Tick constants and bonuses of this match, set at match start.</summary>
        private MomentumRates _rates;

        /// <summary>Mechanic ID as named in faction data.</summary>
        public string Id => MechanicId;

        /// <summary>The constants derived from tuning at match start (default values before the match starts). Read-only; for tests, UI and the AI.</summary>
        public MomentumRates Rates => _rates;

        /// <summary>Number of units that currently hold a Momentum entry. Range: 0 upward.</summary>
        public int EntryCount => _states.Count;

        /// <summary>
        /// Reads the tuning into tick counts, finds which factions use this mechanic and registers the speed and damage
        /// modifiers. Called once before tick 0.
        /// </summary>
        /// <param name="sim">The simulation being started.</param>
        public void OnMatchStart(Simulation sim)
        {
            _rates = new MomentumRates(sim.Tuning.Momentum, sim.Clock);
            _eligibleFactions.Clear();
            foreach (KeyValuePair<string, FactionDefinition> pair in sim.Content.Factions)
            {
                if (string.Equals(pair.Value.MechanicId, MechanicId, System.StringComparison.Ordinal))
                {
                    _eligibleFactions.Add(pair.Key);
                }
            }
            sim.RegisterSpeedModifier(this);
            sim.RegisterDamageModifier(this);
        }

        /// <summary>
        /// Runs after statuses, movement and combat of the same tick: syncs the entries with the live eligible units, then
        /// per unit in ascending ID order applies the loss rule, the gain rule (moving) or the wait and drain rule (still).
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        public void OnTick(Simulation sim)
        {
            // Current tick. Unit: ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            // Step 1a: drop entries whose unit is gone, in ascending key order.
            List<EntityId> gone = new List<EntityId>();
            foreach (EntityId id in _states.Keys)
            {
                if (!sim.Units.Contains(id))
                {
                    gone.Add(id);
                }
            }
            foreach (EntityId id in gone)
            {
                _states.Remove(id);
            }
            // Step 1b: add an empty entry for every live eligible unit that has none, in ascending ID order.
            foreach (Unit unit in sim.Units.All)
            {
                if (!_states.ContainsKey(unit.Id) && IsEligible(sim, unit))
                {
                    _states.Add(unit.Id, new MomentumState());
                }
            }
            // Step 2: per entry in ascending ID order. The loop changes the state objects, never the dictionary's keys.
            foreach (KeyValuePair<EntityId, MomentumState> pair in _states)
            {
                if (!sim.Units.TryGet(pair.Key, out Unit? unit))
                {
                    continue;
                }
                StepUnit(sim, tick, unit, pair.Value);
            }
        }

        /// <summary>Applies the per-tick rules to one unit's state.</summary>
        private void StepUnit(Simulation sim, int tick, Unit unit, MomentumState state)
        {
            // True when Grounded is active and wipes Momentum; it wins the event reason over "slowed".
            bool grounded = _rates.LostOnGrounded && unit.Statuses.IsGrounded;
            // True when a Slowed status is active and wipes Momentum.
            bool slowed = _rates.LostOnSlowed && unit.Statuses.IsSlowed;
            if (grounded || slowed)
            {
                if (state.Stacks > 0)
                {
                    sim.Events.Add(new MomentumLostEvent(tick, unit.Id, state.Stacks, grounded ? "grounded" : "slowed"));
                }
                state.Stacks = 0;
                state.GainTicks = 0;
                state.StillTicks = 0;
                state.DrainTicks = 0;
                return;
            }
            if (unit.LastSelfMoveTick == tick)
            {
                state.StillTicks = 0;
                state.DrainTicks = 0;
                if (state.Stacks < _rates.MaxStacks)
                {
                    state.GainTicks += 1;
                    if (state.GainTicks >= _rates.GainTicksPerStack)
                    {
                        state.Stacks += 1;
                        state.GainTicks = 0;
                        sim.Events.Add(new MomentumChangedEvent(tick, unit.Id, state.Stacks - 1, state.Stacks));
                    }
                }
                else
                {
                    state.GainTicks = 0;
                }
                return;
            }
            if (state.StillTicks < _rates.DrainDelayTicks)
            {
                // Waiting out the drain delay; the progress toward the next stack (GainTicks) is kept.
                state.StillTicks += 1;
                return;
            }
            state.GainTicks = 0;
            if (state.Stacks == 0)
            {
                state.DrainTicks = 0;
                return;
            }
            state.DrainTicks += 1;
            if (state.DrainTicks >= _rates.DrainTicksPerStack)
            {
                state.Stacks -= 1;
                state.DrainTicks = 0;
                sim.Events.Add(new MomentumChangedEvent(tick, unit.Id, state.Stacks + 1, state.Stacks));
            }
        }

        /// <summary>True when the unit can hold Momentum: it can move and its owner's faction uses this mechanic.</summary>
        private bool IsEligible(Simulation sim, Unit unit)
        {
            return unit.Definition.SpeedTilesPerSecond > Fix64.Zero
                && sim.Players.TryGet(unit.Owner, out Player? owner)
                && _eligibleFactions.Contains(owner.FactionId);
        }

        /// <summary>Momentum state of a unit, or false when it has no entry (ineligible units have none). For tests, UI and the AI.</summary>
        /// <param name="id">The unit's ID.</param>
        /// <param name="state">The state when found.</param>
        public bool TryGetState(EntityId id, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out MomentumState state)
        {
            return _states.TryGetValue(id, out state);
        }

        /// <summary>Speed multiplier from stacks: one plus the per-stack bonus times the stacks, or one without an entry. Unit: multiplier of base speed. Range: 1 up to 1 plus 0.2 × 10 stacks (3) at the loader's extreme limits; 1.2 at the defaults.</summary>
        /// <param name="sim">The running simulation.</param>
        /// <param name="unit">The moving unit.</param>
        public Fix64 SpeedMultiplier(Simulation sim, Unit unit)
        {
            if (!_states.TryGetValue(unit.Id, out MomentumState? state))
            {
                return Fix64.One;
            }
            return Fix64.One + _rates.SpeedBonusPerStack * state.Stacks;
        }

        /// <summary>Damage multiplier from stacks, read when a hit resolves: one plus the per-stack bonus times the stacks, or one without an entry. Unit: fraction (1 = unchanged). Range: 1 and up.</summary>
        /// <param name="sim">The running simulation.</param>
        /// <param name="attacker">The unit that is hitting.</param>
        public Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker)
        {
            if (!_states.TryGetValue(attacker.Id, out MomentumState? state))
            {
                return Fix64.One;
            }
            return Fix64.One + _rates.DamageBonusPerStack * state.Stacks;
        }

        /// <summary>Feeds the mechanic's state into the state hash: the ID, the entry count, then per entry in ascending unit ID the ID and the four counters. Tick constants are derived from tuning and not hashed.</summary>
        /// <param name="hasher">The hasher to write to.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write(_states.Count);
            foreach (KeyValuePair<EntityId, MomentumState> pair in _states)
            {
                hasher.Write(pair.Key);
                hasher.Write(pair.Value.Stacks);
                hasher.Write(pair.Value.GainTicks);
                hasher.Write(pair.Value.StillTicks);
                hasher.Write(pair.Value.DrainTicks);
            }
        }
    }
}
