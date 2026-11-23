// rts/tests/Sim.Tests/Ai/AiOpeningTests.cs
// Tests for package L of the skirmish AI (skirmish_ai_v4_rl.md L.6, L-1 to L-9): the openings schema and its validation errors, the weighted
// draw, the opening's build order, the handover of the composition and of the timing values, the controller's OpeningId and determinism.
// Each test edits the shipped aggressive_s3 text (its faction plans carry "openings": []) and runs AiBrain.Decide on a real greybox_1v1
// simulation. Part of Rts.Sim.Tests. Test code only (may use System.IO).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package L: the opening library.</summary>
    [TestFixture]
    public class AiOpeningTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Simulation ticks per second in the shipped tuning. Unit: ticks per second. Range: 20.</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Script sequence number for the next spawn. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>One build order step as JSON text.</summary>
        private static string Step(string structure, int count, int atSupply)
        {
            return "{ \"structure\": \"" + structure + "\", \"count\": " + count + ", \"at_supply_used\": " + atSupply + " }";
        }

        /// <summary>One opening as JSON text.</summary>
        private static string Opening(string id, int weight, string buildOrder, string composition, int handover = 300, int expandAt = 240, int early = 0, int attackArmy = 8, int attackMin = 14)
        {
            return "{ \"id\": \"" + id + "\", \"weight\": " + weight + ", \"build_order\": [" + buildOrder + "], \"composition\": {" + composition + "}, \"handover_seconds\": " + handover
                + ", \"expand_at_seconds\": " + expandAt + ", \"early_expansion_seconds\": " + early + ", \"attack_army_supply\": " + attackArmy + ", \"attack_min_supply\": " + attackMin + " }";
        }

        /// <summary>The shipped aggressive_s3 text with the skai plan's openings array replaced by <paramref name="openings"/> (JSON elements) and the other edits applied, parsed.</summary>
        private static AiProfile Edited(string openings, params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s3.json")).Replace("\r\n", "\n");
            int at = text.IndexOf("\"openings\": [],", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0));
            text = text.Substring(0, at) + "\"openings\": [" + openings + "]," + text.Substring(at + "\"openings\": [],".Length);
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s3.json", Data.Content);
        }

        /// <summary>A started greybox_1v1 simulation with both HQs on the spawns, two workers each and every tile explored; 500 common and 100 rare for Skai.</summary>
        private Simulation Game()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to 1.
            for (int i = 0; i < 2; i++)
            {
                Spawn(sim, "skai.fledgling", P0, s0 + new FixVec2(Fix64.FromInt(-2 + i), Fix64.FromInt(1)));
                Spawn(sim, "water.tender", P1, s1 - new FixVec2(Fix64.FromInt(-2 + i), Fix64.FromInt(1)));
            }
            ExploreKit.ExploreAll(sim);
            Fund(sim);
            return sim;
        }

        /// <summary>Sets Skai's stock to 500 common and 100 rare.</summary>
        private static void Fund(Simulation sim)
        {
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(500, 100);
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, FixVec2 position)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, position));
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    return spawned.Unit;
                }
            }
            throw new AssertionException("spawn of " + definition + " failed");
        }

        /// <summary>Steps the simulation until its clock reads <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Decides for P0 at the simulation's current tick with a variety stream made from <paramref name="profile"/> and <paramref name="seed"/>.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
        }

        /// <summary>A memory with the variety stream the controller would make for <paramref name="seed"/>.</summary>
        private static AiMemory MemoryFor(AiProfile profile, ulong seed)
        {
            return new AiMemory { Variety = AiVariety.Create(profile, profile.RequireFaction("skai"), AiVariety.SeedFor(seed, P0), TicksPerSecond) };
        }

        /// <summary>The intents of one kind and definition.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind, string definition)
        {
            return intents.FindAll(i => i.Kind == kind && i.DefinitionId == definition);
        }

        /// <summary>The intents as text, in order (for the determinism check).</summary>
        private static string Text(List<AiIntent> intents)
        {
            // The text being built.
            StringBuilder text = new StringBuilder();
            foreach (AiIntent intent in intents)
            {
                text.Append(intent.Kind).Append(' ').Append(intent.Unit).Append(' ').Append(intent.DefinitionId).Append(' ').Append(intent.TileX).Append(',').Append(intent.TileY).Append(';');
            }
            return text.ToString();
        }

        /// <summary>An opening with the plan's own mix and build order, for the tests that only need an opening to exist.</summary>
        private static string Standard(string id, int weight)
        {
            return Opening(id, weight, Step("skai.roost", 1, 0), "\"skai.windrunner\": 3, \"skai.talon_archer\": 2");
        }

        /// <summary>L-1: every shipped profile parses; aggressive_s4r has the four openings per faction of L.5 and every other profile has none.</summary>
        [Test]
        public void L1_ShippedProfilesParse()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                foreach (KeyValuePair<string, AiFactionPlan> pair in profile.Factions)
                {
                    if (id != "aggressive_s4r" && id != "aggressive_s5" && id != "aggressive_s5_fp2" && id != "abl_s5_no_e" && id != "abl_s5_no_p" && id != "abl_s5_no_h" && id != "aggressive_s5_b" && id != "aggressive_s5_r" && id != "aggressive_s5_m" && id != "aggressive_s5_pd" && id != "aggressive_s5_e" && id != "abl_s5b_no_b" && id != "abl_s5_p1_only" && id != "abl_s5_p2_only")
                    {
                        Assert.That(pair.Value.Openings.Count, Is.EqualTo(0), id + "/" + pair.Key);
                    }
                    else
                    {
                        // Opening IDs and weights of L.5 (rush rows renamed by L-R) in file order.
                        string expected = pair.Key == "skai"
                            ? "skai_standard=30,skai_windrunner_pressure=20,skai_fast_expand=25,skai_air=25"
                            : "water_standard=30,water_grasper_pressure=20,water_fast_expand=25,water_leviathan_tech=25";
                        Assert.That(string.Join(",", System.Linq.Enumerable.Select(pair.Value.Openings, o => o.Id + "=" + o.Weight)), Is.EqualTo(expected), pair.Key);
                    }
                }
            }
            AiProfile edited = Edited(Standard("one", 3) + "," + Standard("two", 1));
            Assert.That(edited.RequireFaction("skai").Openings.Count, Is.EqualTo(2));
            Assert.That(edited.RequireFaction("skai").Openings[1].Id, Is.EqualTo("two"));
            Assert.That(edited.RequireFaction("skai").Openings[0].Weight, Is.EqualTo(3));
            Assert.That(edited.RequireFaction("water").Openings.Count, Is.EqualTo(0));
        }

        /// <summary>The shipped aggressive_s4r text with its eight opening weights replaced, in file order (skai 4, then water 4), and the profile id changed.</summary>
        /// <param name="skai">Skai weights in file order (standard, pressure, fast expand, air). Unit: weight. Range: 0 to 100.</param>
        /// <param name="water">Water weights in file order (standard, pressure, fast expand, tech). Unit: weight. Range: 0 to 100.</param>
        /// <param name="id">The profile id to write (the harness needs it equal to the key). Unit: text ID.</param>
        private static string S4rWithWeights(int[] skai, int[] water, string id)
        {
            // Shipped file text.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s4r.json")).Replace("\r\n", "\n");
            // The eight weights in file order.
            int[] all = new int[8];
            Array.Copy(skai, 0, all, 0, 4);
            Array.Copy(water, 0, all, 4, 4);
            // Index of the next weight to write. Unit: counter. Range: 0 to 8.
            int next = 0;
            text = System.Text.RegularExpressions.Regex.Replace(text, "\"weight\": \\d+,", m => "\"weight\": " + all[next++] + ",");
            Assert.That(next, Is.EqualTo(8));
            Assert.That(text, Does.Contain("\"id\": \"aggressive_s4r\","));
            return text.Replace("\"id\": \"aggressive_s4r\",", "\"id\": \"" + id + "\",");
        }

        /// <summary>L-R-1: each pressure opening of the shipped aggressive_s4r has exactly the L-R.2 values (literals).</summary>
        [Test]
        public void LR1_PressureOpeningValues()
        {
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s4r");
            // Faction, opening id, roost-like structure, tech structure, main unit, second unit. Unit: text IDs.
            string[][] rows =
            {
                new[] { "skai", "skai_windrunner_pressure", "skai.roost", "skai.windspire", "skai.windrunner", "skai.talon_archer" },
                new[] { "water", "water_grasper_pressure", "water.tidepool", "water.shell_forge", "water.grasper", "water.reef_runner" },
            };
            foreach (string[] row in rows)
            {
                AiOpening o = System.Linq.Enumerable.Single(profile.RequireFaction(row[0]).Openings, x => x.Id == row[1]);
                Assert.That(o.Weight, Is.EqualTo(20), row[1]);
                Assert.That(o.BuildOrder.Count, Is.EqualTo(4), row[1]);
                // Expected steps: (structure, count, at supply used).
                (string, int, int)[] steps = { (row[2], 1, 0), (row[2], 2, 5), (row[2], 3, 8), (row[3], 1, 16) };
                for (int i = 0; i < 4; i++)
                {
                    Assert.That((o.BuildOrder[i].StructureId, o.BuildOrder[i].Count, o.BuildOrder[i].AtSupplyUsed), Is.EqualTo(steps[i]), row[1] + " step " + i);
                }
                Assert.That(o.Composition.Count, Is.EqualTo(2), row[1]);
                Assert.That(o.Composition[row[4]], Is.EqualTo(60), row[1]);
                Assert.That(o.Composition[row[5]], Is.EqualTo(10), row[1]);
                Assert.That(o.HandoverSeconds, Is.EqualTo(300), row[1]);
                Assert.That(o.ExpandAtSeconds, Is.EqualTo(Fix64.FromInt(300)), row[1]);
                Assert.That(o.EarlyExpansionSeconds, Is.EqualTo(Fix64.Zero), row[1]);
                Assert.That(o.AttackArmySupply, Is.EqualTo(6), row[1]);
                Assert.That(o.AttackMinSupply, Is.EqualTo(8), row[1]);
            }
        }

        /// <summary>L-R-2: with only the pressure opening drawable, seeds 1 to 50 give that opening and effective clocks of 0 to 20 (early expansion) and 280 to 320 (expand at).</summary>
        [Test]
        public void LR2_EffectiveClocks()
        {
            // Faction, expected opening id. Unit: text.
            string[][] rows = { new[] { "skai", "skai_windrunner_pressure" }, new[] { "water", "water_grasper_pressure" } };
            // Weights that leave only the pressure opening drawable. Unit: weight.
            int[] only = { 0, 100, 0, 0 };
            AiProfile profile = AiProfile.Parse(S4rWithWeights(only, only, "aggressive_s4r"), "ai/aggressive_s4r.json", Data.Content);
            foreach (string[] row in rows)
            {
                // Seed. Range: 1 to 50.
                for (ulong seed = 1; seed <= 50; seed++)
                {
                    AiMemory memory = new AiMemory { Variety = AiVariety.Create(profile, profile.RequireFaction(row[0]), seed, TicksPerSecond) };
                    Assert.That(memory.Variety.OpeningId, Is.EqualTo(row[1]), row[0] + " seed " + seed);
                    // Effective clocks at tick 0. Unit: seconds.
                    Fix64 early = AiBrain.EarlyExpansionSeconds(profile, memory, 0);
                    Assert.That(early >= Fix64.Zero && early <= Fix64.FromInt(20), Is.True, row[0] + " early " + early + " seed " + seed);
                    Fix64 expand = AiBrain.ExpandAtSeconds(profile, memory, 0);
                    Assert.That(expand >= Fix64.FromInt(280) && expand <= Fix64.FromInt(320), Is.True, row[0] + " expand " + expand + " seed " + seed);
                }
            }
        }

        /// <summary>L-R-3: a forced pressure opening against a forced standard one, both factions: first depot before 90 s, 12 or more workers at 180 s, first attack before 180 s and before the standard side's.</summary>
        [Test]
        public void LR3_PressureExpandsAtOnceAndAttacksFirst()
        {
            // Profile set with lr_pressure (weights 0,100,0,0) and lr_standard (100,0,0,0) added.
            SortedDictionary<string, JsonValue> profiles = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonValue> pair in Data.AiProfiles)
            {
                profiles[pair.Key] = pair.Value;
            }
            profiles["lr_pressure"] = JsonReader.Parse(S4rWithWeights(new[] { 0, 100, 0, 0 }, new[] { 0, 100, 0, 0 }, "lr_pressure"), "ai/lr_pressure.json");
            profiles["lr_standard"] = JsonReader.Parse(S4rWithWeights(new[] { 100, 0, 0, 0 }, new[] { 100, 0, 0, 0 }, "lr_standard"), "ai/lr_standard.json");
            DataSet data = new DataSet(Data.Content, Data.Tuning, Data.Maps, Data.Cycles, profiles);
            // Matchup and the depot id the pressure side builds. Unit: text.
            string[][] rows = { new[] { "skai_vs_skai", "perch_depot" }, new[] { "water_vs_water", "pearl_depot" } };
            foreach (string[] row in rows)
            {
                StringWriter writer = new StringWriter();
                GameRunner.Play(data, "prototype_1v1", row[0], 0, SeedDerivation.GameSeed(20261117, "prototype_1v1", row[0], 0), "lr_pressure", "lr_standard", 300, new GameTrace(writer, 30));
                // Trace text of the game.
                string trace = writer.ToString().Replace("\r\n", "\n");
                // Slot 0's "structure sites" line (the first in the text).
                string sites = System.Linq.Enumerable.First(trace.Split('\n'), l => l.StartsWith("  structure sites:", StringComparison.Ordinal));
                System.Text.RegularExpressions.Match depot = System.Text.RegularExpressions.Regex.Match(sites, "tick (\\d+) [a-z]+\\." + row[1]);
                Assert.That(depot.Success, Is.True, row[0] + " no depot placed by slot 0");
                Assert.That(int.Parse(depot.Groups[1].Value), Is.LessThan(1800), row[0] + " first depot tick");
                System.Text.RegularExpressions.Match workers = System.Text.RegularExpressions.Regex.Match(trace, "t=180s P0 [^\\n]*? workers=(\\d+)");
                Assert.That(workers.Success, Is.True, row[0] + " no t=180s P0 line");
                Assert.That(int.Parse(workers.Groups[1].Value), Is.GreaterThanOrEqualTo(12), row[0] + " workers at 180 s");
                // First Massing -> Attack second of slot 0 and slot 1; int.MaxValue when none. Unit: seconds.
                int[] first = { int.MaxValue, int.MaxValue };
                // Slot whose "totals" block is being read. Range: -1 (none yet), 0 or 1.
                int slot = -1;
                foreach (string line in trace.Split('\n'))
                {
                    if (line.EndsWith(" totals", StringComparison.Ordinal) && line.StartsWith("P", StringComparison.Ordinal))
                    {
                        slot = line[1] - '0';
                    }
                    else if (slot >= 0 && first[slot] == int.MaxValue && line.Contains("Massing -> Attack"))
                    {
                        first[slot] = int.Parse(System.Text.RegularExpressions.Regex.Match(line, "\\((\\d+)s\\)").Groups[1].Value);
                    }
                }
                Assert.That(first[0], Is.LessThan(180), row[0] + " first attack of the pressure side");
                Assert.That(first[0] < first[1] || first[1] == int.MaxValue, Is.True, row[0] + " pressure attacks first: " + first[0] + " against " + first[1]);
            }
        }

        /// <summary>L-2: each validation error names the file and the field path.</summary>
        [Test]
        public void L2_ValidationErrors()
        {
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => Edited(Standard("one", 1).Replace("\"weight\": 1,", "\"weight\": 1, \"colour\": 3,")));
            Assert.That(unknown.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("colour"));
            DataValidationException duplicate = Expect.Throws<DataValidationException>(() => Edited(Standard("one", 1) + "," + Standard("one", 2)));
            Assert.That(duplicate.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("id"));
            DataValidationException zero = Expect.Throws<DataValidationException>(() => Edited(Standard("one", 0) + "," + Standard("two", 0)));
            Assert.That(zero.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("weight"));
            DataValidationException gated = Expect.Throws<DataValidationException>(() => Edited(Opening("air", 1, Step("skai.roost", 1, 0), "\"skai.thunderhead\": 1")));
            Assert.That(gated.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("requires").And.Contain("skai.storm_spire"));
            DataValidationException notStructure = Expect.Throws<DataValidationException>(() => Edited(Opening("bad", 1, Step("skai.windrunner", 1, 0), "\"skai.windrunner\": 1")));
            Assert.That(notStructure.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("structure"));
            DataValidationException late = Expect.Throws<DataValidationException>(() => Edited(Opening("late", 1, Step("skai.roost", 1, 0), "\"skai.windrunner\": 1", handover: 3601)));
            Assert.That(late.Message, Does.Contain("aggressive_s3.json").And.Contain("openings").And.Contain("handover_seconds"));
            DataValidationException badId = Expect.Throws<DataValidationException>(() => Edited(Standard("Not-Valid", 1)));
            Assert.That(badId.Message, Does.Contain("openings").And.Contain("id"));
            // A thunderhead opening with the Storm Spire in its build order is accepted.
            Assert.That(Edited(Opening("air", 1, Step("skai.roost", 1, 0) + "," + Step("skai.storm_spire", 1, 12), "\"skai.thunderhead\": 1")).RequireFaction("skai").Openings.Count, Is.EqualTo(1));
        }

        /// <summary>Recorded draw counts per opening for 30/20/25/25 over match seeds 1 to 400. Unit: draws. Range: 0 to 400, sum 400.</summary>
        private static readonly int[] WeightedCounts = { 129, 69, 105, 97 };

        /// <summary>L-3: weights pick the opening: 0, 0, 100, 0 always gives the third; 30/20/25/25 over 400 seeds draws every opening near its share (counts pinned).</summary>
        [Test]
        public void L3_Weights()
        {
            string[] ids = { "o1", "o2", "o3", "o4" };
            AiProfile forced = Edited(Standard("o1", 0) + "," + Standard("o2", 0) + "," + Standard("o3", 100) + "," + Standard("o4", 0));
            // Match seed. Range: 1 to 50.
            for (ulong seed = 1; seed <= 50; seed++)
            {
                Assert.That(AiVariety.Create(forced, forced.RequireFaction("skai"), AiVariety.SeedFor(seed, P0), TicksPerSecond).OpeningId, Is.EqualTo("o3"), "seed " + seed);
            }
            AiProfile spread = Edited(Standard("o1", 30) + "," + Standard("o2", 20) + "," + Standard("o3", 25) + "," + Standard("o4", 25));
            // Draws per opening, in file order. Unit: draws.
            int[] counts = new int[4];
            // Match seed. Range: 1 to 400.
            for (ulong seed = 1; seed <= 400; seed++)
            {
                counts[Array.IndexOf(ids, AiVariety.Create(spread, spread.RequireFaction("skai"), AiVariety.SeedFor(seed, P0), TicksPerSecond).OpeningId)]++;
            }
            // Weight shares in file order. Unit: fraction.
            double[] shares = { 0.30, 0.20, 0.25, 0.25 };
            // Opening number. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                Assert.That(counts[i], Is.GreaterThan(0), ids[i]);
                Assert.That(Math.Abs(counts[i] / 400.0 - shares[i]), Is.LessThanOrEqualTo(0.10), ids[i]);
            }
            Assert.That(counts, Is.EqualTo(WeightedCounts));
        }

        /// <summary>The first-think intents of L-4 for a profile, as text.</summary>
        private string FirstThink(AiProfile profile, out List<AiIntent> intents)
        {
            Simulation sim = Game();
            AiMemory memory = MemoryFor(profile, 1);
            intents = Decide(sim, profile, memory);
            return Text(intents);
        }

        /// <summary>L-4: an opening whose build order is one Windspire makes the first think build a Windspire; aggressive_s3 builds a Roost.</summary>
        [Test]
        public void L4_OpeningBuildOrder()
        {
            AiProfile opening = Edited(Opening("windspire_first", 100, Step("skai.windspire", 1, 0), "\"skai.windrunner\": 1"));
            string first = FirstThink(opening, out List<AiIntent> intents);
            Assert.That(Of(intents, AiIntentKind.Build, "skai.windspire").Count, Is.EqualTo(1), first);
            string plain = FirstThink(AiProfile.FromDataSet(Data, "aggressive_s3"), out List<AiIntent> plainIntents);
            Assert.That(Of(plainIntents, AiIntentKind.Build, "skai.roost").Count, Is.EqualTo(1), plain);
            Assert.That(Of(plainIntents, AiIntentKind.Build, "skai.windspire").Count, Is.EqualTo(0), plain);
        }

        /// <summary>The L-5 profile: the opening's mix is Roc Riders only, the plan's mix Windrunners only, every counter switch off, no jitter, handover at 60 s.</summary>
        private static AiProfile HandoverProfile()
        {
            return Edited(Opening("roc", 100, Step("skai.roost", 1, 0), "\"skai.roc_rider\": 1, \"skai.windrunner\": 0", handover: 60),
                "\"use_counter_mix\": true", "\"use_counter_mix\": false", "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false",
                "\"use_rare_priority\": true", "\"use_rare_priority\": false", "\"use_predictor_mix\": true", "\"use_predictor_mix\": false", "\"use_scouting\": true", "\"use_scouting\": false",
                "\"composition\": { \"skai.windrunner\": 3, \"skai.talon_archer\": 2, \"skai.roc_rider\": 1, \"skai.stormcaller\": 1 }", "\"composition\": { \"skai.windrunner\": 1 }");
        }

        /// <summary>The Train intents at the ticks just before and at the handover of L-5, as text.</summary>
        private string HandoverRun()
        {
            AiProfile profile = HandoverProfile();
            Simulation sim = Game();
            EntityId roost = Spawn(sim, "skai.roost", P0, sim.Map.Spawns[0] + new FixVec2(Fix64.FromInt(4), Fix64.FromInt(4)));
            AiMemory memory = MemoryFor(profile, 1);
            // Handover tick. Unit: ticks.
            int handover = 60 * TicksPerSecond;
            StepTo(sim, handover - 1);
            Fund(sim);
            List<AiIntent> before = Decide(sim, profile, memory);
            Assert.That(Of(before, AiIntentKind.Train, "skai.roc_rider").Count, Is.EqualTo(1), Text(before));
            Assert.That(before.Exists(i => i.Kind == AiIntentKind.Train && i.Unit == roost && i.DefinitionId == "skai.roc_rider"), Is.True, Text(before));
            Assert.That(Of(before, AiIntentKind.Train, "skai.windrunner").Count, Is.EqualTo(0), Text(before));
            StepTo(sim, handover);
            Fund(sim);
            List<AiIntent> after = Decide(sim, profile, memory);
            Assert.That(after.Exists(i => i.Kind == AiIntentKind.Train && i.Unit == roost && i.DefinitionId == "skai.windrunner"), Is.True, Text(after));
            Assert.That(Of(after, AiIntentKind.Train, "skai.roc_rider").Count, Is.EqualTo(0), Text(after));
            return Text(before) + "|" + Text(after) + "|" + memory.AttackThreshold;
        }

        /// <summary>L-5: the opening's composition applies up to the handover, the plan's after it.</summary>
        [Test]
        public void L5_HandoverOfTheComposition()
        {
            Assert.That(HandoverRun(), Is.Not.Empty);
        }

        /// <summary>Whether a depot Build intent comes at <paramref name="seconds"/> for the L-6 profile.</summary>
        private bool DepotAt(AiProfile profile, int seconds)
        {
            Simulation sim = Game();
            AiMemory memory = MemoryFor(profile, 1);
            StepTo(sim, seconds * TicksPerSecond);
            Fund(sim);
            return Of(Decide(sim, profile, memory), AiIntentKind.Build, "skai.perch_depot").Count > 0;
        }

        /// <summary>L-6: the opening's early_expansion_seconds (240) holds the first depot back until 240 s while the opening lasts; after the handover the root value (0) applies.</summary>
        [Test]
        public void L6_TimingValues()
        {
            AiProfile held = Edited(Opening("slow", 100, Step("skai.roost", 1, 0), "\"skai.windrunner\": 1", handover: 300, expandAt: 300, early: 240));
            Assert.That(DepotAt(held, 239), Is.False, "239 s");
            Assert.That(DepotAt(held, 240), Is.True, "240 s");
            AiProfile handed = Edited(Opening("slow", 100, Step("skai.roost", 1, 0), "\"skai.windrunner\": 1", handover: 100, expandAt: 300, early: 240));
            Assert.That(DepotAt(handed, 100), Is.True, "100 s, after the handover the root value 0 applies");
        }

        /// <summary>L-7: the opening's attack_army_supply sets the first threshold (6 to 8 with jitter 2); after the handover the root value 8 does (8 to 10).</summary>
        [Test]
        public void L7_AttackValues()
        {
            AiProfile profile = Edited(Opening("small", 100, Step("skai.roost", 1, 0), "\"skai.windrunner\": 1", handover: 300, attackArmy: 6));
            AiMemory memory = MemoryFor(profile, 1);
            DeterministicRandom rng = new DeterministicRandom(3);
            AiBrain.EnterMassing(memory, profile, rng, 0);
            Assert.That(memory.AttackThreshold, Is.InRange(6, 8));
            AiBrain.EnterMassing(memory, profile, rng, 301 * TicksPerSecond);
            Assert.That(memory.AttackThreshold, Is.InRange(8, 10));
        }

        /// <summary>L-8: OpeningId is "" for aggressive_s3 and the drawn ID (equal to Memory.Variety.OpeningId) for a profile with openings.</summary>
        [Test]
        public void L8_OpeningId()
        {
            Simulation plain = Game();
            SkirmishAiController s3 = new SkirmishAiController(P0, AiProfile.FromDataSet(Data, "aggressive_s3"));
            s3.OnMatchStart(plain);
            Assert.That(s3.OpeningId, Is.EqualTo(string.Empty));
            Simulation sim = Game();
            SkirmishAiController withOpenings = new SkirmishAiController(P0, Edited(Standard("one", 50) + "," + Standard("two", 50)));
            withOpenings.OnMatchStart(sim);
            Assert.That(withOpenings.OpeningId, Is.Not.Empty);
            Assert.That(withOpenings.OpeningId, Is.EqualTo(withOpenings.Memory.Variety!.OpeningId));
        }

        /// <summary>
        /// A due build step that costs rare (the Storm Spire of skai_air, 150 common
        /// and 50 rare) with less rare in the bank reserves 50 rare (rule R, while the AI has income), and rule P then admits only units whose rare cost fits in
        /// "rare - reserved rare" (AiBrain.Produce: CostRare &lt;= _rare - _reserveRare), which was negative, so rare-free units were refused (the bug). Fixed: a reservation in one resource never blocks a purchase that costs none of it,
        /// so the Perch Depot (rare source) and other rare-free purchases go through while the Spire waits for rare, and the Spire is still not built until 50 rare are banked.
        /// </summary>
        [Test]
        public void L10_RareReservationDoesNotBlockRareFreePurchases()
        {
            AiProfile profile = Edited(Opening("air", 100, Step("skai.roost", 1, 0) + "," + Step("skai.roost", 2, 6) + "," + Step("skai.storm_spire", 1, 12) + "," + Step("skai.windspire", 1, 14),
                "\"skai.roc_rider\": 3, \"skai.thunderhead\": 1, \"skai.windrunner\": 1", handover: 420));
            Simulation sim = Game();
            FixVec2 home = sim.Map.Spawns[0];
            Spawn(sim, "skai.roost", P0, home + new FixVec2(Fix64.FromInt(4), Fix64.FromInt(4)));
            Spawn(sim, "skai.roost", P0, home + new FixVec2(Fix64.FromInt(4), Fix64.FromInt(-4)));
            // Windrunners spawned until 12 supply is in use, so the Storm Spire step is the first unmet step.
            while (AiWorldView.Build(sim, P0).SupplyUsed < 12)
            {
                Spawn(sim, "skai.windrunner", P0, home + new FixVec2(Fix64.FromInt(-4), Fix64.FromInt(-4)));
            }
            // Both workers gather, so the AI has income (rule R reserves only then).
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Economy.CanGather)
                {
                    sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, unit.Id, 0));
                }
            }
            sim.Step();
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(400, 0);
            AiMemory memory = MemoryFor(profile, 1);
            List<AiIntent> poor = Decide(sim, profile, memory);
            Assert.That(Of(poor, AiIntentKind.Build, "skai.perch_depot").Count, Is.EqualTo(1), "the rare-free Perch Depot (the rare source) is bought: " + Text(poor));
            Assert.That(Of(poor, AiIntentKind.Build, "skai.storm_spire").Count, Is.EqualTo(0), Text(poor));
            record.SetStock(400, 60);
            List<AiIntent> rich = Decide(sim, profile, MemoryFor(profile, 1));
            Assert.That(Of(rich, AiIntentKind.Build, "skai.storm_spire").Count, Is.EqualTo(1), Text(rich));
        }

        /// <summary>L-9: the L-4 and L-5 runs twice give identical intents and memory.</summary>
        [Test]
        public void L9_Determinism()
        {
            AiProfile opening = Edited(Opening("windspire_first", 100, Step("skai.windspire", 1, 0), "\"skai.windrunner\": 1"));
            Assert.That(FirstThink(opening, out _), Is.EqualTo(FirstThink(opening, out _)));
            Assert.That(new AiOpeningTests().HandoverRun(), Is.EqualTo(new AiOpeningTests().HandoverRun()));
        }
    }
}
