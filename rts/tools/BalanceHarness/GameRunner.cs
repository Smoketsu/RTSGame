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

        /// <summary>
        /// AI profile slot <paramref name="slot"/> plays in ladder game <paramref name="game"/>: profile A when (game / 2) % 2 == slot, else B, so games
        /// g to g+3 (g a multiple of 4) cover every seat and, with <see cref="FactionInSlot"/>, every faction combination.
        /// </summary>
        /// <param name="game">Game index. Unit: games. Range: 0 upward.</param>
        /// <param name="slot">Player slot. Range: 0 or 1.</param>
        /// <param name="profileA">Baseline profile ID.</param>
        /// <param name="profileB">Candidate profile ID.</param>
        public static string LadderProfileInSlot(int game, int slot, string profileA, string profileB)
        {
            return (game / 2) % 2 == slot ? profileA : profileB;
        }

        /// <summary>
        /// AI profile slot <paramref name="slot"/> plays in game <paramref name="game"/> of a paired ladder (balance_measurement.md M.1.2): the candidate (B) when slot equals the
        /// candidate's slot g % 2, else the baseline (A).
        /// </summary>
        /// <param name="game">Game index. Unit: games. Range: 0 upward.</param>
        /// <param name="slot">Player slot. Range: 0 or 1.</param>
        /// <param name="profileA">Baseline profile ID.</param>
        /// <param name="profileB">Candidate profile ID.</param>
        public static string PairedProfileInSlot(int game, int slot, string profileA, string profileB)
        {
            return slot == game % 2 ? profileB : profileA;
        }

        /// <summary>
        /// Plays game <paramref name="game"/> of a ladder cell with the ladder's seat rule (unpaired: <see cref="LadderProfileInSlot"/>, seed of the game index, factions swap on odd games) or the paired
        /// layout of M.1.2 (profile <see cref="PairedProfileInSlot"/>, seed and factions of pair g / 2, so both games of a pair share them). The one place ladder games are played: workers, the
        /// determinism spot check and --trace all call it, so the seat rule cannot drift between them.
        /// </summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="mapId">Map ID.</param>
        /// <param name="matchupId">Matchup ID.</param>
        /// <param name="game">Game index in the cell. Unit: games. Range: 0 upward.</param>
        /// <param name="baseSeed">The ladder's base seed. Range: any ulong.</param>
        /// <param name="profileA">Baseline profile ID.</param>
        /// <param name="profileB">Candidate profile ID.</param>
        /// <param name="maxGameSeconds">Tick limit in seconds. Unit: seconds. Range: 1 to 7200.</param>
        /// <param name="paired">True for the paired layout.</param>
        /// <param name="seedXor">Value xor-ed into the seed (test fault only). Range: any ulong; 0 normally.</param>
        /// <param name="trace">Optional read-only observer.</param>
        public static GameRecord PlayLadder(DataSet data, string mapId, string matchupId, int game, ulong baseSeed, string profileA, string profileB, int maxGameSeconds, bool paired,
            ulong seedXor = 0, GameTrace? trace = null)
        {
            // Index the seed derives from: the pair when paired, else the game.
            int seedIndex = paired ? game / 2 : game;
            ulong seed = SeedDerivation.GameSeed(baseSeed, mapId, matchupId, seedIndex) ^ seedXor;
            if (!paired)
            {
                return Play(data, mapId, matchupId, game, seed, LadderProfileInSlot(game, 0, profileA, profileB), LadderProfileInSlot(game, 1, profileA, profileB), maxGameSeconds, trace);
            }
            return Play(data, mapId, matchupId, game, seed, PairedProfileInSlot(game, 0, profileA, profileB), PairedProfileInSlot(game, 1, profileA, profileB), maxGameSeconds, trace, game / 2, game % 2);
        }

        /// <summary>
        /// Plays game <paramref name="game"/> of a paired ladder cell of the opponent pool (balance_measurement.md M.3.3): the paired layout of <see cref="PlayLadder"/> with the baseline given as a
        /// profile object (a pool member's, possibly with forced openings); the record carries the member ID and names the baseline side by it.
        /// </summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="mapId">Map ID.</param>
        /// <param name="matchupId">Matchup ID.</param>
        /// <param name="game">Game index in the cell. Unit: games. Range: 0 upward.</param>
        /// <param name="baseSeed">The run's base seed. Range: any ulong.</param>
        /// <param name="baseline">The member's profile (the baseline).</param>
        /// <param name="memberId">The member's ID.</param>
        /// <param name="candidate">The candidate's profile.</param>
        /// <param name="candidateId">The candidate's profile ID.</param>
        /// <param name="maxGameSeconds">Tick limit in seconds. Unit: seconds. Range: 1 to 7200.</param>
        /// <param name="seedXor">Value xor-ed into the seed (test fault only). Range: any ulong; 0 normally.</param>
        public static GameRecord PlayPool(DataSet data, string mapId, string matchupId, int game, ulong baseSeed, AiProfile baseline, string memberId, AiProfile candidate, string candidateId,
            int maxGameSeconds, ulong seedXor = 0)
        {
            // The seed of the pair, as PlayLadder derives it for a paired game.
            ulong seed = SeedDerivation.GameSeed(baseSeed, mapId, matchupId, game / 2) ^ seedXor;
            // True when the candidate plays slot 0 (even games).
            bool candidateFirst = game % 2 == 0;
            return Play(data, mapId, matchupId, game, seed, candidateFirst ? candidate : baseline, candidateFirst ? baseline : candidate, candidateFirst ? candidateId : memberId,
                candidateFirst ? memberId : candidateId, maxGameSeconds, null, game / 2, game % 2, memberId);
        }

        /// <summary>Plays one game by profile IDs (each loaded from the data set) and returns its record; see the profile-object overload.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="mapId">Map ID; the map must have two spawns.</param>
        /// <param name="matchupId">Matchup ID "a_vs_b".</param>
        /// <param name="game">Game index in the cell. Unit: games. Range: 0 upward.</param>
        /// <param name="seed">Simulation seed (see <see cref="SeedDerivation"/>). Range: any ulong.</param>
        /// <param name="profileSlot0">AI profile ID slot 0 plays.</param>
        /// <param name="profileSlot1">AI profile ID slot 1 plays.</param>
        /// <param name="maxGameSeconds">Tick limit in seconds. Unit: seconds. Range: 1 to 7200.</param>
        /// <param name="trace">Optional read-only observer (--trace mode); null in batch games. It never changes the game.</param>
        /// <param name="pair">Pair index of a paired ladder game, or -1 (unpaired). Unit: pairs. Range: -1, or 0 upward.</param>
        /// <param name="candidateSlot">Slot of the candidate in a paired game, written to the record. Range: -1 (unpaired), 0 or 1.</param>
        public static GameRecord Play(DataSet data, string mapId, string matchupId, int game, ulong seed, string profileSlot0, string profileSlot1, int maxGameSeconds, GameTrace? trace = null,
            int pair = -1, int candidateSlot = -1)
        {
            return Play(data, mapId, matchupId, game, seed, AiProfile.FromDataSet(data, profileSlot0), AiProfile.FromDataSet(data, profileSlot1), profileSlot0, profileSlot1, maxGameSeconds,
                trace, pair, candidateSlot);
        }

        /// <summary>Plays one game and returns its record. The profile objects may be shared and immutable; the names go to the record.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="mapId">Map ID; the map must have two spawns.</param>
        /// <param name="matchupId">Matchup ID "a_vs_b".</param>
        /// <param name="game">Game index in the cell. Unit: games. Range: 0 upward.</param>
        /// <param name="seed">Simulation seed (see <see cref="SeedDerivation"/>). Range: any ulong.</param>
        /// <param name="profile0">Profile slot 0 plays.</param>
        /// <param name="profile1">Profile slot 1 plays.</param>
        /// <param name="name0">Name written to the record for slot 0's profile (a profile ID or a pool member ID).</param>
        /// <param name="name1">Name written to the record for slot 1's profile.</param>
        /// <param name="maxGameSeconds">Tick limit in seconds. Unit: seconds. Range: 1 to 7200.</param>
        /// <param name="trace">Optional read-only observer (--trace mode); null in batch games. It never changes the game.</param>
        /// <param name="pair">Pair index of a paired ladder game: the factions follow the pair (<see cref="FactionInSlot"/> of the pair, so both games of a pair match) and the record carries it. Unit: pairs. Range: -1 (unpaired, factions follow the game), or 0 upward.</param>
        /// <param name="candidateSlot">Slot of the candidate in a paired game, written to the record. Range: -1 (unpaired), 0 or 1.</param>
        /// <param name="member">Pool member ID written to the record, or empty. Range: empty, or a pool member ID.</param>
        public static GameRecord Play(DataSet data, string mapId, string matchupId, int game, ulong seed, AiProfile profile0, AiProfile profile1, string name0, string name1, int maxGameSeconds,
            GameTrace? trace = null, int pair = -1, int candidateSlot = -1, string member = "")
        {
            string profileSlot0 = name0;
            string profileSlot1 = name1;
            // Index the factions follow: the pair of a paired game, else the game itself.
            int factionIndex = pair >= 0 ? pair : game;
            MapDefinition map = data.RequireMap(mapId);
            // Players of the game: slot 0 team 0, slot 1 team 1, named after their factions.
            List<PlayerSetup> players = new List<PlayerSetup>();
            for (int slot = 0; slot < 2; slot++)
            {
                string faction = FactionInSlot(matchupId, factionIndex, slot);
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
                controllers.Add(new SkirmishAiController(player.Id, player.Id.Value == 0 ? profile0 : profile1));
            }
            AiHost.StartAll(sim, controllers);
            // IDs of the openings the two AIs drew (skirmish_ai_v4_rl.md H.1), "" for a profile without openings.
            string opening0 = ((SkirmishAiController)controllers[0]).OpeningId;
            string opening1 = ((SkirmishAiController)controllers[1]).OpeningId;
            trace?.Begin(sim, mapId + " " + matchupId + " game " + game + " seed " + seed + " profile " + profileSlot0 + " (P0) / " + profileSlot1 + " (P1) cap " + maxGameSeconds + " s; P0 "
                + players[0].FactionId + ", P1 " + players[1].FactionId);

            // Tick limit. Unit: ticks. Range: 1 upward.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(maxGameSeconds));
            // HQ units being watched by entity ID, with their faction and the first damage and death ticks (-1 until seen).
            SortedDictionary<EntityId, HqWatch> watches = new SortedDictionary<EntityId, HqWatch>();
            // Play signature of the game (skirmish_ai_v4.md S.1): FNV-1a over every AI command, fresh per game, never read by the simulation.
            StateHasher signature = new StateHasher();
            // Last tick stepped. Unit: ticks. Range: -1 before the first step.
            int lastTick = -1;
            // Outcome after the last step.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            for (int i = 0; i < maxTicks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                foreach (IPlayerController controller in controllers)
                {
                    if (controller is SkirmishAiController ai)
                    {
                        foreach (Rts.Sim.Commands.Command command in ai.LastCommands)
                        {
                            signature.Write(command.Tick);
                            signature.Write(command.Issuer);
                            signature.Write(command.Sequence);
                            command.WriteHash(signature);
                        }
                    }
                }
                trace?.AfterThink(sim, controllers);
                lastTick = sim.Clock.Tick;
                sim.Step();
                trace?.AfterStep(sim);
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

            trace?.Finish(sim, outcome, Math.Max(lastTick, 0), maxGameSeconds);
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
            signature.Write(Math.Max(lastTick, 0));
            signature.Write(result);
            return new GameRecord(mapId, matchupId, game, seed, FactionInSlot(matchupId, factionIndex, 0), FactionInSlot(matchupId, factionIndex, 1), profileSlot0, profileSlot1, result,
                Math.Max(lastTick, 0), sim.ComputeStateHash(), signature.Hash, sim.ContentDigest, kills, opening0, opening1, pair, candidateSlot, member);
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
