// rts/src/Ai/SkirmishAiController.cs
// The skirmish AI player controller (spec sections 3, 4 and 6): plays one player slot from the host, outside the simulation.
// Part of Rts.Ai. Host loop: build the Simulation, Start it, create one controller per AI slot, call OnMatchStart, then before
// every sim.Step() call Think(sim, sink) for each controller in ascending player order (AiHost.ThinkAll does this). Think reads
// the previous tick's events (hits on own units, for the Defend rule) on every call, and on its think ticks
// ((tick + player) % ThinkTicks == 0, staggered per player) builds an AiWorldView, runs AiBrain and submits the intents through
// AiCommandWriter. It never calls Step, never mutates the simulation, never draws from sim.Rng (locked between steps anyway),
// and keeps no static state, so two AIs in one process share nothing. Its generator is a DeterministicRandom (PCG32, the same
// class the simulation uses) seeded matchSeed ^ (0x9E3779B97F4A7C15 * (player + 1)).
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>One AI player. Create with a profile; see the file header for the host loop.</summary>
    public sealed class SkirmishAiController : IPlayerController
    {
        /// <summary>Golden-ratio multiplier that spreads player slots across the seed space (spec section 3). Range: fixed constant.</summary>
        public const ulong SeedMultiplier = 0x9E3779B97F4A7C15UL;

        /// <summary>The player slot this controller plays.</summary>
        public PlayerId Player { get; }

        /// <summary>The profile it plays by.</summary>
        public AiProfile Profile { get; }

        /// <summary>The controller's memory (not simulation state). Exposed for tests and debugging overlays.</summary>
        public AiMemory Memory { get; } = new AiMemory();

        /// <summary>The controller's own generator; created by <see cref="OnMatchStart"/>. Null before.</summary>
        public DeterministicRandom? Rng { get; private set; }

        /// <summary>The profile's plan for the player's faction; set by <see cref="OnMatchStart"/>. Null before.</summary>
        public AiFactionPlan? Plan { get; private set; }

        /// <summary>Ticks between decisions: max(1, think_interval_seconds in ticks). Unit: ticks. Range: 1 upward; 0 before OnMatchStart.</summary>
        public int ThinkTicks { get; private set; }

        /// <summary>Commands submitted by the most recent think (empty when the last call was not a think tick). For tests and logs.</summary>
        public IReadOnlyList<Command> LastCommands { get; private set; } = Array.Empty<Command>();

        /// <summary>Creates a controller for <paramref name="player"/> playing <paramref name="profile"/>.</summary>
        /// <param name="player">A valid player slot.</param>
        /// <param name="profile">A validated profile.</param>
        public SkirmishAiController(PlayerId player, AiProfile profile)
        {
            if (!player.IsValid)
            {
                throw new ArgumentException("An AI needs a real player slot.", nameof(player));
            }
            Player = player;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>The AI generator seed for a player: matchSeed ^ (0x9E3779B97F4A7C15 * (player + 1)), wrapping. Range: any ulong.</summary>
        /// <param name="matchSeed">The match seed (MatchSettings.Seed).</param>
        /// <param name="player">The AI's player slot.</param>
        public static ulong SeedFor(ulong matchSeed, PlayerId player)
        {
            return matchSeed ^ unchecked(SeedMultiplier * (ulong)(player.Value + 1));
        }

        /// <summary>
        /// Seeds the generator, resolves the faction plan (throws DataValidationException when the profile has none) and the think
        /// interval. Called by the host after <see cref="Simulation.Start"/>; <see cref="Think"/> calls it itself if the host did not.
        /// </summary>
        /// <param name="sim">The started simulation.</param>
        public void OnMatchStart(Simulation sim)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (!sim.Players.TryGet(Player, out Player? self))
            {
                throw new InvalidOperationException("Player " + Player + " is not in the match.");
            }
            Rng = new DeterministicRandom(SeedFor(sim.Settings.Seed, Player));
            Plan = Profile.RequireFaction(self.FactionId);
            ThinkTicks = Math.Max(1, sim.Clock.SecondsToTicks(Profile.ThinkIntervalSeconds));
        }

        /// <summary>True when the controller decides on <paramref name="tick"/>: (tick + player) % ThinkTicks == 0.</summary>
        /// <param name="tick">The tick about to be stepped. Unit: ticks.</param>
        public bool IsThinkTick(int tick)
        {
            return ThinkTicks > 0 && (tick + Player.Value) % ThinkTicks == 0;
        }

        /// <summary>
        /// Reads the last stepped tick's hits on own units, then on think ticks decides and submits commands to <paramref name="sink"/>.
        /// Call before every <see cref="Simulation.Step"/>.
        /// </summary>
        /// <param name="sim">The simulation between steps.</param>
        /// <param name="sink">Where the commands go.</param>
        public void Think(Simulation sim, ICommandSink sink)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }
            if (sim.IsStepping)
            {
                throw new InvalidOperationException("The AI must not think inside Simulation.Step.");
            }
            if (Rng == null || Plan == null)
            {
                OnMatchStart(sim);
            }
            ReadEvents(sim);
            LastCommands = Array.Empty<Command>();
            if (!IsThinkTick(sim.Clock.Tick))
            {
                return;
            }
            // What the AI may know this think.
            AiWorldView view = AiWorldView.Build(sim, Player);
            Memory.ThinkCount++;
            // Intents decided this think, in rule order.
            List<AiIntent> intents = AiBrain.Decide(view, Profile, Plan!, Memory, Rng!);
            LastCommands = AiCommandWriter.Write(intents, Player, sink, Memory);
        }

        /// <summary>
        /// Records hits on own units from the last stepped tick's events (UnitDamagedEvent whose target is an own live unit): the own
        /// unit's ID, tick and position. The attacker is not read, so a hidden attacker stays hidden.
        /// </summary>
        private void ReadEvents(Simulation sim)
        {
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                if (simEvent.Tick <= Memory.LastEventTickRead || !(simEvent is UnitDamagedEvent hit))
                {
                    continue;
                }
                if (!sim.Units.TryGet(hit.Target, out Unit? target) || target.Owner != Player)
                {
                    continue;
                }
                if (!Memory.RecentDamage.TryGetValue(target.Id, out DamageReport? report))
                {
                    report = new DamageReport();
                    Memory.RecentDamage.Add(target.Id, report);
                }
                report.Tick = hit.Tick;
                report.Position = target.Position;
            }
            // Ticks covered by the events just read (the log holds the last stepped tick only).
            Memory.LastEventTickRead = Math.Max(Memory.LastEventTickRead, sim.Clock.Tick - 1);
        }
    }
}
