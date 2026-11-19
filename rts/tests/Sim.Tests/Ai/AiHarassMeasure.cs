// rts/tests/Sim.Tests/Ai/AiHarassMeasure.cs
// Measurement helper for package AI-H (field_saturation.md section 9): replays harness games (the same setup and loop as GameRunner.Play) and
// prints every harass raid the AI launches (tick, player, the unit types sent) and how many of its members died while the raid was out, plus
// one summary line per game with the final state hash (equal to the harness game's). Explicit: never run with the suite; run it with
//   dotnet test rts/tests/Sim.Tests -c Release --no-build --filter "FullyQualifiedName~AiHarassMeasure" --logger "console;verbosity=detailed"
// and the environment variables H_PROFILE (profile ID played in both seats, default aggressive_s5) and H_GAMES (games per matchup, default 4).
// Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.Text;
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
    /// <summary>Prints the harass raids of harness games.</summary>
    [TestFixture]
    [Explicit("measurement, not a test")]
    public class AiHarassMeasure
    {
        /// <summary>Plays the games of the environment settings and prints one line per raid and one summary line per game.</summary>
        [Test]
        public void PrintRaids()
        {
            DataSet data = TestData.LoadAll();
            // Profile played in both seats. Range: a profile ID of the data set.
            string profileId = Environment.GetEnvironmentVariable("H_PROFILE") ?? "aggressive_s5";
            // Games per matchup. Unit: games. Range: 1 to 72.
            int games = int.Parse(Environment.GetEnvironmentVariable("H_GAMES") ?? "4");
            foreach (string matchup in new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" })
            {
                for (int game = 0; game < games; game++)
                {
                    PlayOne(data, profileId, matchup, game);
                }
            }
        }

        /// <summary>Plays one game as <see cref="GameRunner.Play"/> does and prints its raids.</summary>
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
            // Raids launched, raiders sent and raiders dead per player. Unit: raids, units.
            int[] raids = { 0, 0 };
            int[] sent = { 0, 0 };
            int[] died = { 0, 0 };
            // True while a raid of the player is out (its HarassGroup is not empty). Unit: boolean.
            bool[] active = { false, false };
            // The living tracked members of each player's raid out now (empty when none). Unit: unit IDs.
            List<EntityId>[] tracked = { new List<EntityId>(), new List<EntityId>() };
            // The match ends at the harness cap. Unit: ticks.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(2400));
            for (int i = 0; i < maxTicks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                for (int p = 0; p < 2; p++)
                {
                    // The raid memory of this player.
                    SortedSet<EntityId> group = ((SkirmishAiController)controllers[p]).Memory.HarassGroup;
                    if (!active[p] && group.Count > 0)
                    {
                        active[p] = true;
                        // A raid has just been launched: list its unit types.
                        StringBuilder types = new StringBuilder();
                        foreach (EntityId id in group)
                        {
                            tracked[p].Add(id);
                            types.Append(types.Length == 0 ? string.Empty : ",").Append(sim.Units.TryGet(id, out Unit? member) ? member!.Definition.Id : "?");
                        }
                        raids[p]++;
                        sent[p] += group.Count;
                        TestContext.Out.WriteLine("RAID " + profileId + " " + matchup + " game " + game + " tick " + sim.Clock.Tick + " P" + p + " " + players[p].FactionId + " units " + group.Count + " types " + types);
                    }
                }
                sim.Step();
                for (int p = 0; p < 2; p++)
                {
                    SortedSet<EntityId> group = ((SkirmishAiController)controllers[p]).Memory.HarassGroup;
                    for (int k = tracked[p].Count - 1; k >= 0; k--)
                    {
                        if (!sim.Units.TryGet(tracked[p][k], out Unit? _))
                        {
                            died[p]++;
                            tracked[p].RemoveAt(k);
                        }
                    }
                    if (group.Count == 0)
                    {
                        // The raid has ended.
                        active[p] = false;
                        tracked[p].Clear();
                    }
                }
                if (MatchOutcome.Evaluate(sim.Players).IsDecided)
                {
                    break;
                }
            }
            TestContext.Out.WriteLine("GAME " + profileId + " " + matchup + " game " + game + " end_tick " + sim.Clock.Tick + " state_hash " + sim.ComputeStateHash().ToString("x16")
                + " raids_P0/P1 " + raids[0] + "/" + raids[1] + " raiders_sent " + sent[0] + "/" + sent[1] + " raiders_died " + died[0] + "/" + died[1]);
        }
    }
}
