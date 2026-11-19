// rts/tests/Sim.Tests/Ai/AiPlacementMeasure.cs
// Measurement helper for package AI-P (field_saturation.md section 9): replays harness games (the same setup and loop as GameRunner.Play, with
// the final state hash checked against GameRunner so the replay is the same game) and prints every structure the AI orders that trains units or
// is a static defence, with the tick, the player, the tile and the base (the Rooted HQ or a complete depot) it stands nearest to. Explicit: it is
// never run with the suite; run it with
//   dotnet test rts/tests/Sim.Tests -c Release --filter "FullyQualifiedName~AiPlacementMeasure" --logger "console;verbosity=detailed"
// and the environment variables P_PROFILE (profile ID, default aggressive_s5) and P_GAMES (games per matchup, default 4). Part of Rts.Sim.Tests.
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
    /// <summary>Prints where and when producers and static defences were ordered in harness games.</summary>
    [TestFixture]
    [Explicit("measurement, not a test")]
    public class AiPlacementMeasure
    {
        /// <summary>Plays the games of the environment settings and prints one line per structure and one summary line per game.</summary>
        [Test]
        public void PrintPlacements()
        {
            DataSet data = TestData.LoadAll();
            // Profile played in both seats. Range: a profile ID of the data set.
            string profileId = Environment.GetEnvironmentVariable("P_PROFILE") ?? "aggressive_s5";
            // Games per matchup. Unit: games. Range: 1 to 72.
            int games = int.Parse(Environment.GetEnvironmentVariable("P_GAMES") ?? "4");
            AiProfile profile = AiProfile.FromDataSet(data, profileId);
            foreach (string matchup in new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" })
            {
                for (int game = 0; game < games; game++)
                {
                    PlayOne(data, profile, profileId, matchup, game);
                }
            }
        }

        /// <summary>Plays one game as <see cref="GameRunner.Play"/> does and prints its orders.</summary>
        private static void PlayOne(DataSet data, AiProfile profile, string profileId, string matchup, int game)
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
            // Counts per player: producers at the HQ ring, producers at a depot, defences at the HQ, defences at a depot. Unit: structures.
            int[,] counts = new int[2, 4];
            // Tick of each player's first static defence order, or -1. Unit: ticks.
            int[] firstDefence = { -1, -1 };
            // Army supply and army value (common plus rare cost) of each player's finished mobile non-worker units at 300 s and 480 s, -1 when the game ended earlier. Unit: supply, resource.
            int[,] armySupply = { { -1, -1 }, { -1, -1 } };
            int[,] armyValue = { { -1, -1 }, { -1, -1 } };
            // Total cost (common plus rare) of every static defence order of each player, retries included. Unit: resource.
            int[] defenceCost = { 0, 0 };
            // The match ends at the harness cap. Unit: ticks.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(2400));
            for (int i = 0; i < maxTicks; i++)
            {
                if (i == 300 * 20 || i == 480 * 20)
                {
                    // Column: 0 at 300 s, 1 at 480 s.
                    int column = i == 300 * 20 ? 0 : 1;
                    for (int p = 0; p < 2; p++)
                    {
                        armySupply[p, column] = 0;
                        armyValue[p, column] = 0;
                    }
                    foreach (Unit unit in sim.Units.All)
                    {
                        UnitDefinition d = unit.Definition;
                        if (unit.IsUnderConstruction || d.Economy.FootprintTiles > 0 || d.Hq != null || d.Economy.CanGather)
                        {
                            continue;
                        }
                        armySupply[unit.Owner.Value, column] += d.Supply;
                        armyValue[unit.Owner.Value, column] += d.CostCommon + d.CostRare;
                    }
                }
                AiHost.ThinkAll(sim, controllers);
                foreach (IPlayerController controller in controllers)
                {
                    foreach (Command command in ((SkirmishAiController)controller).LastCommands)
                    {
                        if (!(command is BuildStructureCommand build))
                        {
                            continue;
                        }
                        UnitDefinition definition = data.Content.Units[build.DefinitionId];
                        bool defence = definition.Economy.IsStaticDefence;
                        bool producer = definition.Economy.Trains.Count > 0 && !definition.Economy.IsDropOff;
                        if (!defence && !producer)
                        {
                            continue;
                        }
                        // Position of the site. Unit: tiles.
                        FixVec2 site = new FixVec2(Fix64.FromInt(build.TileX) + Fix64.Half, Fix64.FromInt(build.TileY) + Fix64.Half);
                        // Nearest base of the issuer (Rooted HQ or complete depot) and its label.
                        string nearest = "none";
                        Fix64 best = Fix64.Zero;
                        foreach (Unit unit in sim.Units.All)
                        {
                            bool isBase = unit.Owner == build.Issuer && !unit.IsUnderConstruction
                                && ((unit.Definition.Hq != null && unit.HqState == HqState.Rooted) || unit.Definition.Economy.IsDropOff);
                            if (!isBase)
                            {
                                continue;
                            }
                            Fix64 squared = (unit.Position - site).LengthSquared();
                            if (nearest == "none" || squared < best)
                            {
                                best = squared;
                                nearest = unit.Definition.Hq != null ? "HQ" : "depot@" + unit.Position;
                            }
                        }
                        int player = build.Issuer.Value;
                        bool atDepot = nearest.StartsWith("depot", StringComparison.Ordinal);
                        counts[player, (defence ? 2 : 0) + (atDepot ? 1 : 0)]++;
                        if (defence)
                        {
                            defenceCost[build.Issuer.Value] += definition.CostCommon + definition.CostRare;
                        }
                        if (defence && firstDefence[player] < 0)
                        {
                            firstDefence[player] = sim.Clock.Tick;
                        }
                        TestContext.Out.WriteLine("ORDER " + matchup + " game " + game + " tick " + sim.Clock.Tick + " P" + player + " " + build.DefinitionId + " tile (" + build.TileX + "," + build.TileY + ") nearest " + nearest);
                    }
                }
                sim.Step();
                if (MatchOutcome.Evaluate(sim.Players).IsDecided)
                {
                    break;
                }
            }
            TestContext.Out.WriteLine("GAME " + profileId + " " + matchup + " game " + game + " end_tick " + sim.Clock.Tick + " state_hash " + sim.ComputeStateHash().ToString("x16")
                + " producers_hq_P0/P1 " + counts[0, 0] + "/" + counts[1, 0] + " producers_depot " + counts[0, 1] + "/" + counts[1, 1]
                + " defences_hq " + counts[0, 2] + "/" + counts[1, 2] + " defences_depot " + counts[0, 3] + "/" + counts[1, 3]
                + " first_defence_tick " + firstDefence[0] + "/" + firstDefence[1]
                + " defence_cost " + defenceCost[0] + "/" + defenceCost[1]
                + " army_supply_300 " + armySupply[0, 0] + "/" + armySupply[1, 0] + " army_supply_480 " + armySupply[0, 1] + "/" + armySupply[1, 1]
                + " army_value_300 " + armyValue[0, 0] + "/" + armyValue[1, 0] + " army_value_480 " + armyValue[0, 1] + "/" + armyValue[1, 1]);
        }
    }
}
