// rts/tools/BalanceHarness/GameRunner.cs
// Plays one batch game: two skirmish AIs of one profile, the standard match start, on one map, until the match is decided or the tick
// limit is reached, and returns a GameRecord (spec: rts/docs/specs/balance_harness.md section 4). A worker process calls it for every game of its
// chunk; tests call it in-process. Reads only public simulation APIs; nothing it measures feeds back into the simulation. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Runs single batch games.</summary>
    public static class GameRunner
    {
        /// <summary>Splits "a_vs_b" into its two faction IDs; throws <see cref="HarnessException"/> when the text has no single "_vs_" or an empty side.</summary>
        /// <param name="matchupId">Matchup ID such as "skai_vs_water".</param>
        public static string[] SplitMatchup(string matchupId)
        {
            // Position of the separator. Range: -1 when missing.
            int at = matchupId.IndexOf("_vs_", StringComparison.Ordinal);
            if (at <= 0 || at + 4 >= matchupId.Length || matchupId.IndexOf("_vs_", at + 1, StringComparison.Ordinal) >= 0)
            {
                throw new HarnessException("matchup '" + matchupId + "' must look like <faction>_vs_<faction>");
            }
            return new[] { matchupId.Substring(0, at), matchupId.Substring(at + 4) };
        }

        /// <summary>Faction in slot <paramref name="slot"/> of game <paramref name="game"/> of a matchup: even games put the first-named faction in slot 0, odd games swap.</summary>
        /// <param name="matchupId">Matchup ID.</param>
        /// <param name="game">Game index. Unit: games. Range: 0 upward.</param>
        /// <param name="slot">Player slot. Range: 0 or 1.</param>
        public static string FactionInSlot(string matchupId, int game, int slot)
        {
            // The two factions, first-named first.
            string[] pair = SplitMatchup(matchupId);
            return pair[(game + slot) % 2];
        }

        /// <summary>Plays one game and returns its record.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="mapId">Map ID; the map must have two spawns.</param>
        /// <param name="matchupId">Matchup ID "a_vs_b".</param>
        /// <param name="game">Game index in the cell. Unit: games. Range: 0 upward.</param>
        /// <param name="seed">Simulation seed (see <see cref="SeedDerivation"/>). Range: any ulong.</param>
        /// <param name="aiProfile">AI profile ID both slots play.</param>
        /// <param name="maxGameSeconds">Tick limit in seconds. Unit: seconds. Range: 1 to 7200.</param>
        public static GameRecord Play(DataSet data, string mapId, string matchupId, int game, ulong seed, string aiProfile, int maxGameSeconds)
        {
            MapDefinition map = data.RequireMap(mapId);
            // Players of the game: slot 0 team 0, slot 1 team 1, named after their factions.
            List<PlayerSetup> players = new List<PlayerSetup>();
            for (int slot = 0; slot < 2; slot++)
            {
                string faction = FactionInSlot(matchupId, game, slot);
                players.Add(new PlayerSetup(new PlayerId((byte)slot), new TeamId((byte)slot), faction, faction + " AI"));
            }
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(seed), data.Content, players, BuiltInMechanics.CreateRegistry(),
                data.Tuning, map, data.Cycles.Values));
            sim.Start();
            foreach (Rts.Sim.Commands.Command command in StandardMatchStart.Commands(data, map, players))
            {
                sim.Submit(command);
            }
            // The AI controllers, ascending slot order; each gets its own copy of the profile.
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (PlayerSetup player in players)
            {
                controllers.Add(new SkirmishAiController(player.Id, AiProfile.FromDataSet(data, aiProfile)));
            }
            AiHost.StartAll(sim, controllers);

            // Tick limit. Unit: ticks. Range: 1 upward.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(maxGameSeconds));
            // HQ units being watched by entity ID, with their faction and the first damage and death ticks (-1 until seen).
            SortedDictionary<EntityId, HqWatch> watches = new SortedDictionary<EntityId, HqWatch>();
            // Last tick stepped. Unit: ticks. Range: -1 before the first step.
            int lastTick = -1;
            // Outcome after the last step.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            for (int i = 0; i < maxTicks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                lastTick = sim.Clock.Tick;
                sim.Step();
                if (i == 0)
                {
                    // shortcut: only the HQs of the standard start are watched; an HQ trained later would be missed, upgrade when HQs can be built.
                    foreach (Unit unit in sim.Units.All)
                    {
                        if (unit.Definition.Hq != null)
                        {
                            watches.Add(unit.Id, new HqWatch(unit.Definition.FactionId));
                        }
                    }
                }
                foreach (SimEvent simEvent in sim.Events.Current)
                {
                    if (simEvent is UnitDamagedEvent damaged)
                    {
                        if (watches.TryGetValue(damaged.Target, out HqWatch? hit) && hit.FirstDamageTick < 0 && damaged.Damage > Fix64.Zero)
                        {
                            hit.FirstDamageTick = damaged.Tick;
                        }
                    }
                    else if (simEvent is UnitRemovedEvent removed && watches.TryGetValue(removed.Unit, out HqWatch? dead) && dead.DeathTick < 0)
                    {
                        dead.DeathTick = removed.Tick;
                    }
                }
                outcome = MatchOutcome.Evaluate(sim.Players);
                if (outcome.IsDecided)
                {
                    break;
                }
            }

            // Result text of the game.
            string result = !outcome.IsDecided ? GameRecord.Undecided : outcome.IsDraw ? GameRecord.Draw : outcome.Winner.Value == 0 ? GameRecord.Slot0 : GameRecord.Slot1;
            // Kills in ascending entity order.
            List<HqKill> kills = new List<HqKill>();
            foreach (HqWatch watch in watches.Values)
            {
                if (watch.DeathTick >= 0)
                {
                    kills.Add(new HqKill(watch.Faction, watch.FirstDamageTick >= 0 ? watch.FirstDamageTick : watch.DeathTick, watch.DeathTick));
                }
            }
            return new GameRecord(mapId, matchupId, game, seed, FactionInSlot(matchupId, game, 0), FactionInSlot(matchupId, game, 1), result,
                Math.Max(lastTick, 0), sim.ComputeStateHash(), sim.ContentDigest, kills);
        }

        /// <summary>Damage and death ticks of one HQ unit while a game runs.</summary>
        private sealed class HqWatch
        {
            /// <summary>Faction of the HQ.</summary>
            public string Faction { get; }

            /// <summary>Tick of the first damage above 0, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstDamageTick { get; set; } = -1;

            /// <summary>Tick the HQ was removed, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int DeathTick { get; set; } = -1;

            /// <summary>Creates a watch for an HQ of <paramref name="faction"/>.</summary>
            public HqWatch(string faction)
            {
                Faction = faction;
            }
        }
    }
}
