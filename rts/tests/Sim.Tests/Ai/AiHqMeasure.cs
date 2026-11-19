// rts/tests/Sim.Tests/Ai/AiHqMeasure.cs
// Measurement helper for package AI-HQ (field_saturation.md section 9): replays harness games (the same setup and loop as GameRunner.Play) and
// prints every HQ move (UPROOT with the tick, ROOT when it stands again), an HQ that dies (HQ_LOST, with whether it was not Rooted and how long
// after its last uproot), and one GAME line per game with the final state hash (equal to the harness game's). Explicit: never run with the suite;
//   dotnet test rts/tests/Sim.Tests -c Release --no-build --filter "FullyQualifiedName~AiHqMeasure" --logger "console;verbosity=detailed"
// with the environment variables HQ_PROFILE (profile ID played in both seats, default aggressive_s5), HQ_GAMES (games per matchup, default 4) and
// HQ_ONLY (optional "matchup:game" list separated by commas; then only those games are played). Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Prints the HQ moves of harness games.</summary>
    [TestFixture]
    [Explicit("measurement, not a test")]
    public class AiHqMeasure
    {
        /// <summary>Plays the games of the environment settings and prints the HQ lines and one summary line per game.</summary>
        [Test]
        public void PrintMoves()
        {
            DataSet data = TestData.LoadAll();
            // Profile played in both seats. Range: a profile ID of the data set.
            string profileId = Environment.GetEnvironmentVariable("HQ_PROFILE") ?? "aggressive_s5";
            // Games per matchup. Unit: games. Range: 1 to 72.
            int games = int.Parse(Environment.GetEnvironmentVariable("HQ_GAMES") ?? "4");
            // Optional "matchup:game" list; empty plays every game below the games count.
            string only = Environment.GetEnvironmentVariable("HQ_ONLY") ?? string.Empty;
            foreach (string matchup in new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" })
            {
                for (int game = 0; game < games; game++)
                {
                    if (only.Length > 0 && Array.IndexOf(only.Split(','), matchup + ":" + game) < 0)
                    {
                        continue;
                    }
                    PlayOne(data, profileId, matchup, game);
                }
            }
        }

        /// <summary>The HQ of a player, or null when it has none. Range: a unit with an HQ definition owned by the player.</summary>
        private static Unit? HqOf(Simulation sim, int player)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner.Value == player && unit.Definition.Hq != null)
                {
                    return unit;
                }
            }
            return null;
        }

        /// <summary>Plays one game as <see cref="GameRunner.Play"/> does and prints its HQ moves.</summary>
        private static void PlayOne(DataSet data, string profileId, string matchup, int game)
        {
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", matchup, game);
            MapDefinition map = data.RequireMap("prototype_1v1");
            List<PlayerSetup> players = new List<PlayerSetup>();
            for (int slot = 0; slot < 2; slot++)
            {
                string faction = GameRunner.FactionInSlot(matchup, game, slot);
                players.Add(new PlayerSetup(new PlayerId((byte)slot), new TeamId((byte)slot), faction, faction + " AI"));
            }
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(seed), data.Content, players, BuiltInMechanics.CreateRegistry(),
                data.Tuning, map, data.Cycles.Values));
            sim.Start();
            foreach (Command command in StandardMatchStart.Commands(data, map, players))
            {
                sim.Submit(command);
            }
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (PlayerSetup player in players)
            {
                controllers.Add(new SkirmishAiController(player.Id, AiProfile.FromDataSet(data, profileId)));
            }
            AiHost.StartAll(sim, controllers);
            // Uproots and HQ deaths per player. Unit: count.
            int[] uproots = { 0, 0 };
            int[] lost = { 0, 0 };
            // Last seen HQ state of each player (null before the first look). Range: any HqState.
            HqState?[] last = { null, null };
            // Tick of each player's last uproot start, or -1. Unit: ticks.
            int[] lastUproot = { -1, -1 };
            // The match ends at the harness cap. Unit: ticks.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(2400));
            for (int i = 0; i < maxTicks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                sim.Step();
                for (int p = 0; p < 2; p++)
                {
                    // This player's HQ now, or null when it is gone.
                    Unit? hq = HqOf(sim, p);
                    string head = profileId + " " + matchup + " game " + game + " tick " + sim.Clock.Tick + " P" + p + " " + players[p].FactionId;
                    if (hq == null)
                    {
                        if (last[p].HasValue)
                        {
                            lost[p]++;
                            // Seconds since the last uproot began, or -1 when none. Unit: seconds.
                            int since = lastUproot[p] < 0 ? -1 : (sim.Clock.Tick - lastUproot[p]) / 20;
                            TestContext.Out.WriteLine("HQ_LOST " + head + " state " + last[p] + " seconds_since_uproot " + since);
                            last[p] = null;
                        }
                        continue;
                    }
                    if (last[p].HasValue && last[p] == HqState.Rooted && hq.HqState != HqState.Rooted)
                    {
                        uproots[p]++;
                        lastUproot[p] = sim.Clock.Tick;
                        TestContext.Out.WriteLine("UPROOT " + head);
                    }
                    else if (last[p].HasValue && last[p] != HqState.Rooted && hq.HqState == HqState.Rooted)
                    {
                        TestContext.Out.WriteLine("ROOT " + head);
                    }
                    last[p] = hq.HqState;
                }
                if (MatchOutcome.Evaluate(sim.Players).IsDecided)
                {
                    break;
                }
            }
            TestContext.Out.WriteLine("GAME " + profileId + " " + matchup + " game " + game + " end_tick " + sim.Clock.Tick + " state_hash " + sim.ComputeStateHash().ToString("x16")
                + " uproots_P0/P1 " + uproots[0] + "/" + uproots[1] + " hq_lost_P0/P1 " + lost[0] + "/" + lost[1]);
        }
    }
}
