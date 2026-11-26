// rts/tests/Sim.Tests/Ai/AiProductionTests.cs
// Tests of rts/docs/specs/ai_production_defence.md packages PD-1 (rules RR and PC: RR-1 to RR-3, PC-1 to PC-8, D-1 for PC, OFF-1, OFF-2) and PD-2 (rule SA: SA-1 to SA-4, D-1 for SA). Part of Rts.Sim.Tests.
// Each case builds a real greybox_1v1 simulation (P0 HQ at the map spawn (8, 8), structures spawned complete by script, three workers gathering, the whole map explored) and runs AiBrain.Decide on the resulting view
// with a hand-made profile (aggressive_s5_pd text with the counter-mix, tech, static defence and scouting off, the plan's build order empty and its composition as the test writes it). OFF-1 is also held by the existing pin tests, unchanged.
using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>Packages PD-1 (production capacity) and PD-2 (supply ahead) of ai_production_defence.md.</summary>
    [TestFixture]
    public class AiProductionTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the enemy).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and orders. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The tuning the next <see cref="Scene"/> uses; the shipped one unless a test replaced it (SA-4 lowers the supply cap limit). Null means the shipped tuning.</summary>
        private Tuning? _tuning;

        /// <summary>The text of a profile file with line endings normalised.</summary>
        /// <param name="id">Profile ID (file name without extension).</param>
        private static string ProfileText(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json")).Replace("\r\n", "\n");
        }

        /// <summary>A hand-made profile: the candidate's text with the counter-mix, tech on demand, static defence and scouting off, then the given old/new pairs (each old text must occur) applied.</summary>
        /// <param name="id">Profile file to start from.</param>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile Hand(string id, params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = ProfileText(id);
            // Switches written off so only the rule under test acts.
            string[] off = { "use_counter_mix", "use_rare_priority", "use_predictor_mix", "use_tech_on_demand", "use_static_defence", "use_scouting", "use_walls" };
            foreach (string key in off)
            {
                text = text.Replace("\"" + key + "\": true", "\"" + key + "\": false");
            }
            // Index of the old text of the pair being applied. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text, Does.Contain(oldNewPairs[i]));
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/" + id + ".json", Data.Content);
        }

        /// <summary>The faction's plan with an empty build order and the given composition.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="faction">Faction ID.</param>
        /// <param name="composition">Unit ID to weight.</param>
        private static AiFactionPlan Plan(AiProfile profile, string faction, params (string Unit, int Weight)[] composition)
        {
            // The composition as a sorted dictionary.
            SortedDictionary<string, int> weights = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach ((string unit, int weight) in composition)
            {
                weights[unit] = weight;
            }
            return profile.RequireFaction(faction).WithOrders(new List<AiBuildStep>(), weights);
        }

        /// <summary>One structure of a scene: definition, position and the unit queued in it (null for an idle one).</summary>
        private sealed class Site
        {
            /// <summary>Definition ID. Range: a structure of the faction.</summary>
            public string Definition { get; }

            /// <summary>Tile-centre X. Unit: tiles. Range: on the map.</summary>
            public string X { get; }

            /// <summary>Tile-centre Y. Unit: tiles. Range: on the map.</summary>
            public string Y { get; }

            /// <summary>Unit queued at the structure, or null. Range: a unit it trains.</summary>
            public string? Queued { get; }

            /// <summary>Stores the fields.</summary>
            public Site(string definition, string x, string y, string? queued)
            {
                Definition = definition;
                X = x;
                Y = y;
                Queued = queued;
            }
        }

        /// <summary>Shorthand for a <see cref="Site"/>.</summary>
        private static Site At(string definition, string x, string y, string? queued = null)
        {
            return new Site(definition, x, y, queued);
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
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

        /// <summary>
        /// A started greybox_1v1 match: P0 of <paramref name="faction"/> with its HQ at (8, 8), three workers gathering field 1, the given structures complete (units queued where named), P1 of the other faction, the map explored,
        /// stepped to <paramref name="tick"/> and the stock set to <paramref name="common"/> and 0 rare.
        /// </summary>
        private Simulation Scene(string faction, int tick, int common, params Site[] sites)
        {
            // The enemy's faction (only its units are used).
            string enemy = faction == "skai" ? "water" : "skai";
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction, "A"),
                new PlayerSetup(P1, new TeamId(1), enemy, "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), _tuning ?? Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            ExploreKit.ExploreAll(sim);
            Spawn(sim, faction == "skai" ? "skai.great_aerie" : "water.grand_shell", P0, "8", "8");
            // Worker definition of the faction.
            string worker = faction == "skai" ? "skai.fledgling" : "water.tender";
            for (int i = 0; i < 3; i++)
            {
                EntityId id = Spawn(sim, worker, P0, (10 + i).ToString(), "12");
                sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, id, 1));
            }
            foreach (Site site in sites)
            {
                Spawn(sim, site.Definition, P0, site.X, site.Y);
            }
            // Units to queue after the stepping.
            List<(EntityId Producer, string Unit)> queue = new List<(EntityId, string)>();
            foreach (AiUnitView view in AiWorldView.Build(sim, P0, Array.Empty<int>()).OwnUnits)
            {
                foreach (Site site in sites)
                {
                    if (site.Queued != null && string.Equals(view.Definition.Id, site.Definition, StringComparison.Ordinal) && view.Position.X == Fix64.Parse(site.X) && view.Position.Y == Fix64.Parse(site.Y))
                    {
                        queue.Add((view.Id, site.Queued));
                    }
                }
            }
            while (sim.Clock.Tick < tick - queue.Count)
            {
                sim.Step();
            }
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(5000, 0);
            foreach ((EntityId producer, string unit) in queue)
            {
                sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, _sequence++, producer, unit));
                sim.Step();
            }
            player.SetStock(common, 0);
            return sim;
        }

        /// <summary>A memory with a deposit log of <paramref name="common"/> common deposited 400 ticks ago, and an attack threshold set so the first think does not draw.</summary>
        private static AiMemory Memory(Simulation sim, int common)
        {
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            memory.DepositLog.Add((sim.Clock.Tick - 400, common));
            return memory;
        }

        /// <summary>Decides for P0 on a fresh view.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiFactionPlan plan, AiMemory memory)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, plan, memory, new DeterministicRandom(1));
        }

        /// <summary>The intents as text (kind, unit, definition, tile, point), for equality checks.</summary>
        private static string Key(List<AiIntent> intents)
        {
            return string.Join(";", intents.ConvertAll(i => i.Kind + ":" + i.Unit.Value + ":" + i.DefinitionId + ":" + i.TileX + "," + i.TileY + ":" + i.Point.X.ToString() + "," + i.Point.Y.ToString()));
        }

        /// <summary>The Build intents of one definition.</summary>
        private static int Builds(List<AiIntent> intents, string definition)
        {
            return intents.FindAll(i => i.Kind == AiIntentKind.Build && i.DefinitionId == definition).Count;
        }

        /// <summary>The Train intents of one definition.</summary>
        private static int Trains(List<AiIntent> intents, string definition)
        {
            return intents.FindAll(i => i.Kind == AiIntentKind.Train && i.DefinitionId == definition).Count;
        }

        /// <summary>The PC-1 water scene: a busy Tidepool and a busy Shell Forge, 400 common, tick 2400.</summary>
        private Simulation WaterScene(int tick = 2400, int common = 400, params Site[] extra)
        {
            List<Site> sites = new List<Site> { At("water.tidepool", "13.5", "10.5", "water.grasper"), At("water.shell_forge", "16.5", "10.5", "water.shellback") };
            sites.AddRange(extra);
            return Scene("water", tick, common, sites.ToArray());
        }

        /// <summary>The PC-1 composition.</summary>
        private static (string, int)[] WaterMix => new[] { ("water.grasper", 30), ("water.shellback", 10), ("water.ink_lobber", 10), ("water.spoutling", 10) };

        // ------------------------------------------------------------------ Rule RR

        /// <summary>RR-1: a Tidecaller that waits for rare does not stop production: the Grasper is trained and no Tidecaller; with the switch off nothing is trained.</summary>
        [Test]
        public void RR1_SupportDoesNotStopProduction()
        {
            // A profile with the support unit wanted at 12 army supply and a composition without a rare-free Pummeler gap.
            AiProfile on = Hand("aggressive_s5_pd");
            AiProfile off = Hand("aggressive_s5_pd", "\"use_production_capacity\": true", "\"use_production_capacity\": false");
            foreach (bool enabled in new[] { true, false })
            {
                Simulation sim = Scene("water", 600, 500, At("water.tidepool", "13.5", "10.5"));
                for (int i = 0; i < 6; i++)
                {
                    Spawn(sim, "water.grasper", P0, (15 + i).ToString(), "15");
                }
                Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
                player!.SetStock(500, 0);
                AiProfile profile = enabled ? on : off;
                AiFactionPlan plan = Plan(profile, "water", ("water.grasper", 30), ("water.spoutling", 10));
                List<AiIntent> intents = Decide(sim, profile, plan, new AiMemory { AttackThreshold = 8 });
                Assert.That(Trains(intents, "water.tidecaller"), Is.EqualTo(0));
                Assert.That(Trains(intents, "water.grasper"), Is.EqualTo(enabled ? 1 : 0), enabled ? "on" : "off");
                if (!enabled)
                {
                    Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.Train && i.DefinitionId != "water.tender"), Is.Empty, "no unit at all with the switch off");
                }
            }
        }

        /// <summary>A scene with a Reef Workshop (150 common, 50 rare) due in the build order, rare 0 and the given common; returns the reservations after rule 3.</summary>
        private (int Common, int Rare) WorkshopReserves(AiProfile profile, int common)
        {
            Simulation sim = Scene("water", 600, common, At("water.tidepool", "13.5", "10.5"));
            AiFactionPlan plan = profile.RequireFaction("water").WithOrders(new List<AiBuildStep> { new AiBuildStep("water.reef_workshop", 1, 0) }, new SortedDictionary<string, int>(StringComparer.Ordinal) { ["water.grasper"] = 30 });
            (int CommonBefore, int RareBefore, int Common, int Rare, List<AiIntent> Intents)? probe = AiBrain.ProbeReserves(AiWorldView.Build(sim, P0, Array.Empty<int>()), profile, plan, new AiMemory { AttackThreshold = 8 }, true);
            Assert.That(probe, Is.Not.Null);
            return (probe!.Value.Common, probe.Value.Rare);
        }

        /// <summary>RR-2: a structure that waits only for rare holds no common (reserve 0 common, 50 rare); with the switch off it holds 150 common.</summary>
        [Test]
        public void RR2_RareShortStructureHoldsNoCommon()
        {
            Assert.That(WorkshopReserves(Hand("aggressive_s5_pd"), 300), Is.EqualTo((0, 50)));
            Assert.That(WorkshopReserves(Hand("aggressive_s5_pd", "\"use_production_capacity\": true", "\"use_production_capacity\": false"), 300), Is.EqualTo((150, 50)));
        }

        /// <summary>RR-3: with the common part short too, both are reserved.</summary>
        [Test]
        public void RR3_CommonShortReservesBoth()
        {
            Assert.That(WorkshopReserves(Hand("aggressive_s5_pd"), 100), Is.EqualTo((150, 50)));
        }

        // ------------------------------------------------------------------ Rule PC

        /// <summary>PC-1: capacity 708 below income 950: one Tidepool is built; also with a raider near the depot but outside the main.</summary>
        [Test]
        public void PC1_BuildsProducerWhenCapacityShort()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = WaterScene();
            List<AiIntent> first = Decide(sim, profile, Plan(profile, "water", WaterMix), Memory(sim, 1200));
            Assert.That(Builds(first, "water.tidepool"), Is.EqualTo(1), Key(first) + " tick " + sim.Clock.Tick);
            // A raid elsewhere: the depot is 11 tiles from the HQ's east, the raider 15 tiles from the HQ but within the defend radius of the depot.
            Simulation raided = WaterScene(2400, 400, At("water.pearl_depot", "18.5", "7.5"));
            Spawn(raided, "skai.windrunner", P1, "22.5", "9.5");
            Assert.That(Builds(Decide(raided, profile, Plan(profile, "water", WaterMix), Memory(raided, 1200)), "water.tidepool"), Is.EqualTo(1));
        }

        /// <summary>PC-2: two more complete busy Tidepools lift the capacity to 1374, above the income: no producer is built.</summary>
        [Test]
        public void PC2_NoBuildWhenCapacityMeetsIncome()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = WaterScene(2400, 400, At("water.tidepool", "13.5", "13.5", "water.grasper"), At("water.tidepool", "16.5", "13.5", "water.grasper"));
            List<AiIntent> intents = Decide(sim, profile, Plan(profile, "water", WaterMix), Memory(sim, 1200));
            Assert.That(Builds(intents, "water.tidepool") + Builds(intents, "water.shell_forge"), Is.EqualTo(0));
        }

        /// <summary>PC-3: a visible enemy army unit inside the defend radius of the HQ: no producer is built.</summary>
        [Test]
        public void PC3_MainUnderAttack()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = WaterScene();
            Spawn(sim, "skai.windrunner", P1, "15.5", "8.5");
            List<AiIntent> intents = Decide(sim, profile, Plan(profile, "water", WaterMix), Memory(sim, 1200));
            Assert.That(Builds(intents, "water.tidepool") + Builds(intents, "water.shell_forge"), Is.EqualTo(0));
        }

        /// <summary>PC-4: an idle Tidepool that can train nothing payable does not block: the Shell Forge is built; rule X on the same view builds nothing.</summary>
        [Test]
        public void PC4_IdleProducerWithNothingPayableDoesNotBlock()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            AiProfile ruleX = Hand("aggressive_s5_pd", "\"use_production_capacity\": true", "\"use_production_capacity\": false");
            (string, int)[] mix = { ("water.shellback", 30), ("water.spoutling", 30) };
            Simulation sim = Scene("water", 2400, 400, At("water.tidepool", "13.5", "10.5"), At("water.shell_forge", "16.5", "10.5", "water.shellback"));
            Assert.That(Builds(Decide(sim, profile, Plan(profile, "water", mix), Memory(sim, 1200)), "water.shell_forge"), Is.EqualTo(1));
            Simulation again = Scene("water", 2400, 400, At("water.tidepool", "13.5", "10.5"), At("water.shell_forge", "16.5", "10.5", "water.shellback"));
            List<AiIntent> intents = Decide(again, ruleX, Plan(ruleX, "water", mix), Memory(again, 1200));
            Assert.That(Builds(intents, "water.shell_forge") + Builds(intents, "water.tidepool"), Is.EqualTo(0));
        }

        /// <summary>PC-5: with 100 common nothing is built and rule PC reserves nothing.</summary>
        [Test]
        public void PC5_NoReservation()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = WaterScene(2400, 100);
            (int CommonBefore, int RareBefore, int Common, int Rare, List<AiIntent> Intents)? probe = AiBrain.ProbeReserves(AiWorldView.Build(sim, P0, Array.Empty<int>()), profile, Plan(profile, "water", WaterMix), Memory(sim, 1200), false);
            Assert.That(probe, Is.Not.Null);
            Assert.That(Builds(probe!.Value.Intents, "water.tidepool"), Is.EqualTo(0));
            Assert.That(probe.Value.Common, Is.EqualTo(probe.Value.CommonBefore));
            Assert.That(probe.Value.Rare, Is.EqualTo(probe.Value.RareBefore));
        }

        /// <summary>PC-6: at tick 1000 (50 s) the 60 s window is not full: no producer is built.</summary>
        [Test]
        public void PC6_WindowNotFull()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = WaterScene(1000);
            Assert.That(Builds(Decide(sim, profile, Plan(profile, "water", WaterMix), Memory(sim, 1200)), "water.tidepool"), Is.EqualTo(0));
        }

        /// <summary>PC-7: the same rule for Skai: a busy Roost (250) and a busy Windspire (300) are 550 against 950: a Roost is built.</summary>
        [Test]
        public void PC7_FactionNeutral()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            Simulation sim = Scene("skai", 2400, 400, At("skai.roost", "13.5", "10.5", "skai.windrunner"), At("skai.windspire", "16.5", "10.5", "skai.talon_archer"));
            Assert.That(Builds(Decide(sim, profile, Plan(profile, "skai", ("skai.windrunner", 30), ("skai.talon_archer", 20)), Memory(sim, 1200)), "skai.roost"), Is.EqualTo(1));
        }

        /// <summary>
        /// PC-8: with three workers gathering for 75 s the log holds the common the events show deposited for P0 inside the window (in tick order, nothing older after a think); with the switch off the log stays empty.
        /// </summary>
        [Test]
        public void PC8_DepositLog()
        {
            foreach (bool enabled in new[] { true, false })
            {
                AiProfile profile = Hand("aggressive_s5_pd", "\"use_production_capacity\": true", enabled ? "\"use_production_capacity\": true" : "\"use_production_capacity\": false");
                Simulation sim = Scene("water", 0, 0, At("water.tidepool", "13.5", "10.5"));
                SkirmishAiController controller = new SkirmishAiController(P0, profile);
                controller.OnMatchStart(sim);
                ListCommandSink sink = new ListCommandSink(sim.Clock.Tick);
                // Common deposited per tick for P0, from the events. Unit: resource units. Range: 0 upward.
                List<(int Tick, int Amount)> seen = new List<(int, int)>();
                // Ticks stepped: at least 1500 and then on to the next think tick.
                int steps = 0;
                while (steps < 1500 || !controller.IsThinkTick(sim.Clock.Tick))
                {
                    sink.ScheduleTick = sim.Clock.Tick + 1;
                    controller.Think(sim, sink);
                    sim.Step();
                    steps++;
                    foreach (SimEvent evt in sim.Events.Current)
                    {
                        if (evt is ResourceDepositedEvent deposit && deposit.Owner == P0 && deposit.Resource == ResourceKind.Common)
                        {
                            seen.Add((deposit.Tick, deposit.Amount));
                        }
                    }
                }
                controller.Think(sim, sink);
                // Tick of the last think and the window start (exclusive). Unit: ticks.
                int now = sim.Clock.Tick;
                int oldest = now - sim.Clock.SecondsToTicks(Fix64.FromInt(profile.IncomeWindowSeconds));
                if (!enabled)
                {
                    Assert.That(controller.Memory.DepositLog, Is.Empty);
                    continue;
                }
                // Expected common inside the window, and the log's sum.
                int expected = 0;
                foreach ((int tick, int amount) in seen)
                {
                    if (tick > oldest)
                    {
                        expected += amount;
                    }
                }
                int logged = 0;
                int previous = int.MinValue;
                foreach ((int tick, int common) in controller.Memory.DepositLog)
                {
                    Assert.That(tick, Is.GreaterThan(oldest));
                    Assert.That(tick, Is.GreaterThanOrEqualTo(previous));
                    previous = tick;
                    logged += common;
                }
                Assert.That(seen.Count, Is.GreaterThan(0), "the workers deposited");
                Assert.That(expected, Is.GreaterThan(0));
                Assert.That(logged, Is.EqualTo(expected));
            }
        }

        // ------------------------------------------------------------------ Rule SA

        /// <summary>
        /// The Skai scene of SA-1: four complete Roosts and one Windspire (cap 50), a spare fourth worker, <paramref name="army"/> Windrunners, and <paramref name="constructing"/> Roosts ordered to workers (pending, each 8 supply); common set last.
        /// </summary>
        private Simulation SupplyScene(int constructing, int army, int common)
        {
            Simulation sim = Scene("skai", 600, 5000, At("skai.roost", "14.5", "17.5"), At("skai.roost", "16.5", "17.5"), At("skai.roost", "18.5", "17.5"), At("skai.roost", "20.5", "17.5"), At("skai.windspire", "22.5", "17.5"));
            // A fourth worker that stays free to build, so a fourth supply structure has a builder.
            Spawn(sim, "skai.fledgling", P0, "12", "12");
            for (int i = 0; i < army; i++)
            {
                Spawn(sim, "skai.windrunner", P0, (15 + i % 9).ToString(), (14 + i / 9).ToString());
            }
            // The own workers, ascending ID.
            List<EntityId> workers = new List<EntityId>();
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0, Array.Empty<int>()).OwnUnits)
            {
                if (unit.IsWorker)
                {
                    workers.Add(unit.Id);
                }
            }
            for (int i = 0; i < constructing; i++)
            {
                sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, workers[i], "skai.roost", 14 + 3 * i, 21));
                sim.Step();
            }
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(common, 0);
            return sim;
        }

        /// <summary>The supply headroom SupplyCap + P - SupplyUsed of a scene with <paramref name="constructing"/> Roosts pending (8 supply each). Unit: supply.</summary>
        private static int Headroom(Simulation sim, int constructing)
        {
            AiWorldView view = AiWorldView.Build(sim, P0, Array.Empty<int>());
            Assert.That(view.SupplyCap, Is.EqualTo(50), "base 10 + four Roosts and a Windspire at 8");
            return view.SupplyCap + 8 * constructing - view.SupplyUsed;
        }

        /// <summary>A Skai profile whose supply_buffer is <paramref name="buffer"/> (0 to 40, so the arithmetic of SA-1 sits on the edge) and whose pending cap is <paramref name="maxPending"/>.</summary>
        private static AiProfile SupplyProfile(int buffer, int maxPending = 3, bool on = true)
        {
            return Hand("aggressive_s5_pd", "\"supply_buffer\": 3", "\"supply_buffer\": " + buffer, "\"supply_ahead_max_pending\": 3", "\"supply_ahead_max_pending\": " + maxPending,
                "\"use_supply_ahead\": true", on ? "\"use_supply_ahead\": true" : "\"use_supply_ahead\": false");
        }

        /// <summary>The Skai plan of the SA tests.</summary>
        private static AiFactionPlan SkaiPlan(AiProfile profile)
        {
            return Plan(profile, "skai", ("skai.windrunner", 30), ("skai.talon_archer", 20));
        }

        /// <summary>
        /// SA-1: with one Roost pending the projected need is N = 4 x 2 + 3 + 3 = 14 supply; a second Roost is ordered exactly when the headroom is below N + supply_buffer (tested at the edge: buffer H - 13 orders, H - 14 does not); the switch off orders none.
        /// </summary>
        [Test]
        public void SA1_SecondSupplyStructure()
        {
            Simulation sim = SupplyScene(1, 5, 400);
            // Headroom: cap 50 + 8 pending - used 8.
            int headroom = Headroom(sim, 1);
            AiProfile at = SupplyProfile(headroom - 13);
            Assert.That(Builds(Decide(sim, at, SkaiPlan(at), Memory(sim, 0)), "skai.roost"), Is.EqualTo(1), "headroom " + headroom + " < 14 + " + (headroom - 13));
            AiProfile below = SupplyProfile(headroom - 14);
            Assert.That(Builds(Decide(SupplyScene(1, 5, 400), below, SkaiPlan(below), Memory(sim, 0)), "skai.roost"), Is.EqualTo(0), "headroom is not below N + buffer");
            AiProfile off = SupplyProfile(headroom - 13, 3, false);
            Assert.That(Builds(Decide(SupplyScene(1, 5, 400), off, SkaiPlan(off), Memory(sim, 0)), "skai.roost"), Is.EqualTo(0), "switch off: one is already under construction");
        }

        /// <summary>SA-2: with three Roosts pending and supply_ahead_max_pending 3 none is ordered; with 4 one is (so the cap is the only thing that stops it).</summary>
        [Test]
        public void SA2_PendingCap()
        {
            Simulation sim = SupplyScene(3, 23, 400);
            // Headroom: cap 50 + 24 pending - used 26.
            int headroom = Headroom(sim, 3);
            AiProfile capped = SupplyProfile(headroom - 13, 3);
            Assert.That(Builds(Decide(sim, capped, SkaiPlan(capped), Memory(sim, 0)), "skai.roost"), Is.EqualTo(0));
            AiProfile open = SupplyProfile(headroom - 13, 4);
            Assert.That(Builds(Decide(SupplyScene(3, 23, 400), open, SkaiPlan(open), Memory(sim, 0)), "skai.roost"), Is.EqualTo(1));
        }

        /// <summary>SA-3: with 100 common (a Roost costs 150) none is ordered and nothing is reserved.</summary>
        [Test]
        public void SA3_NoReservation()
        {
            Simulation sim = SupplyScene(1, 5, 100);
            AiProfile profile = SupplyProfile(Headroom(sim, 1) - 13);
            (int CommonBefore, int RareBefore, int Common, int Rare, List<AiIntent> Intents)? probe = AiBrain.ProbeReserves(AiWorldView.Build(sim, P0, Array.Empty<int>()), profile, SkaiPlan(profile), Memory(sim, 0), true);
            Assert.That(probe, Is.Not.Null);
            Assert.That(Builds(probe!.Value.Intents, "skai.roost"), Is.EqualTo(0));
            Assert.That((probe.Value.Common, probe.Value.Rare), Is.EqualTo((0, 0)));
        }

        /// <summary>SA-4: with the supply cap limit at the cap plus the pending supply (58) none is ordered; one above it (59) the same scene orders one.</summary>
        [Test]
        public void SA4_SupplyLimit()
        {
            Tuning shipped = Data.Tuning;
            foreach ((int limit, int expected) in new[] { (58, 0), (59, 1) })
            {
                SupplyTuning supply = new SupplyTuning(JsonReader.Parse("{ \"supply_cap\": " + limit + ", \"static_defence_supply_cost\": " + shipped.Supply.StaticDefenceSupplyCost
                    + ", \"static_defences_per_base_max\": " + shipped.Supply.StaticDefencesPerBaseMax + " }", "supply.json"));
                _tuning = new Tuning(shipped.Match, shipped.Hq, shipped.Momentum, shipped.Status, shipped.Flood, supply, shipped.Earth, shipped.Online, shipped.Harness, shipped.Terrain, shipped.Combat, shipped.Economy, shipped.HarnessBatch);
                Simulation sim = SupplyScene(1, 5, 400);
                _tuning = null;
                AiProfile profile = SupplyProfile(Headroom(sim, 1) - 13);
                Assert.That(Builds(Decide(sim, profile, SkaiPlan(profile), Memory(sim, 0)), "skai.roost"), Is.EqualTo(expected), "limit " + limit);
            }
        }

        /// <summary>D-1 (SA): SA-1 run twice from identical inputs gives identical intents.</summary>
        [Test]
        public void D1_SupplyAheadTwice()
        {
            // Intents of each run.
            string[] runs = new string[2];
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = SupplyScene(1, 5, 400);
                AiProfile profile = SupplyProfile(Headroom(sim, 1) - 13);
                runs[run] = Key(Decide(sim, profile, SkaiPlan(profile), Memory(sim, 0)));
            }
            Assert.That(runs[0], Does.Contain("Build"));
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }

        // ------------------------------------------------------------------ Determinism and the off path

        /// <summary>D-1 (PC): PC-1 run twice from identical inputs gives identical intents and memory.</summary>
        [Test]
        public void D1_ProductionCapacityTwice()
        {
            AiProfile profile = Hand("aggressive_s5_pd");
            // Intents and log text of each run.
            string[] runs = new string[2];
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = WaterScene();
                AiMemory memory = Memory(sim, 1200);
                runs[run] = Key(Decide(sim, profile, Plan(profile, "water", WaterMix), memory)) + "|" + string.Join(",", memory.DepositLog) + "|" + memory.LastBuildAttemptTick.Count;
            }
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }

        /// <summary>OFF-1: every profile file other than aggressive_s5_pd has the switch false and the old values; the candidate has it true. The pinned hashes are held by the existing tests, which this change does not re-pin.</summary>
        [Test]
        public void Off1_ProfilesCarryTheOldValues()
        {
            foreach (string path in Directory.GetFiles(Path.Combine(TestData.FindDataDirectory(), "ai"), "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseProductionCapacity, Is.EqualTo(id == "aggressive_s5_pd"), id);
                Assert.That(profile.IncomeWindowSeconds, Is.EqualTo(60), id);
                Assert.That(profile.ProductionCapacityTarget, Is.EqualTo(Fix64.One), id);
                Assert.That(profile.UseSupplyAhead, Is.EqualTo(id == "aggressive_s5_pd"), id);
                Assert.That(profile.SupplyAheadMaxPending, Is.EqualTo(id == "aggressive_s5_pd" ? 3 : 1), id);
                Assert.That(profile.UseRaidWorkers, Is.EqualTo(id == "aggressive_s5_pd"), id);
                Assert.That(profile.RaidActiveSeconds, Is.EqualTo(Fix64.FromInt(10)), id);
                Assert.That(profile.RaidFieldRadiusTiles, Is.EqualTo(Fix64.FromInt(8)), id);
                Assert.That(profile.RaidEvacuateMaxCommandsPerThink, Is.EqualTo(12), id);
                Assert.That(profile.UseRaidResponse, Is.EqualTo(id == "aggressive_s5_pd"), id);
                Assert.That(profile.RaidHoldRadiusTiles, Is.EqualTo(Fix64.FromInt(4)), id);
                Assert.That(profile.RaidHqCoverMarginTiles, Is.EqualTo(Fix64.FromInt(2)), id);
            }
            DataValidationException range = Expect.Throws<DataValidationException>(() => Hand("aggressive_s5_pd", "\"income_window_seconds\": 60", "\"income_window_seconds\": 5"));
            Assert.That(range.Message, Does.Contain("income_window_seconds"));
        }

        /// <summary>Plays 4000 ticks of a Skai versus Water game with the given profiles and returns the commands, final state hash, draw counts and opening of each controller as one text.</summary>
        private static string Trace(AiProfile skai, AiProfile water)
        {
            MapDefinition map = Data.RequireMap("prototype_1v1");
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai AI"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water AI"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(SeedDerivation.GameSeed(20261122UL, "prototype_1v1", "skai_vs_water", 0)), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, map, Data.Cycles.Values));
            sim.Start();
            foreach (Command command in StandardMatchStart.Commands(Data, map, players))
            {
                sim.Submit(command);
            }
            List<IPlayerController> controllers = new List<IPlayerController> { new SkirmishAiController(P0, skai), new SkirmishAiController(P1, water) };
            AiHost.StartAll(sim, controllers);
            // The commands of every think, as text.
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            for (int i = 0; i < 4000; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                foreach (IPlayerController controller in controllers)
                {
                    foreach (Command command in ((SkirmishAiController)controller).LastCommands)
                    {
                        log.Append(sim.Clock.Tick).Append(':').Append(command.GetType().Name).Append(':').Append(command.Issuer.Value).Append(':').Append(command.Sequence).Append(';');
                    }
                }
                sim.Step();
            }
            foreach (IPlayerController controller in controllers)
            {
                SkirmishAiController ai = (SkirmishAiController)controller;
                log.Append("|rng ").Append(ai.Rng!.State).Append('/').Append(ai.Rng.DrawCount).Append(' ').Append(ai.OpeningId);
            }
            return log.Append("|hash ").Append(sim.ComputeStateHash().ToString("x16")).ToString();
        }

        /// <summary>OFF-2: aggressive_s5_m and a copy with the switch explicitly false and the other new keys at other values play the same traced game with the same draws.</summary>
        [Test]
        public void Off2_OldBehaviourAndDraws()
        {
            // Parsed from the file text like the twin (25 Nov 2026): FromDataSet also attaches the authored wall lines, which the text parse cannot, and with Reef Runners in the mix the drawn wall style builds them, so the two would differ for that reason alone.
            AiProfile baseline = AiProfile.Parse(ProfileText("aggressive_s5_m"), "ai/aggressive_s5_m.json", Data.Content);
            AiProfile twin = AiProfile.Parse(ProfileText("aggressive_s5_m").Replace("\"income_window_seconds\": 60", "\"income_window_seconds\": 120").Replace("\"production_capacity_target\": 1,", "\"production_capacity_target\": 2,").Replace("\"supply_ahead_max_pending\": 1", "\"supply_ahead_max_pending\": 6")
                .Replace("\"raid_active_seconds\": 10", "\"raid_active_seconds\": 30").Replace("\"raid_field_radius_tiles\": 8", "\"raid_field_radius_tiles\": 20").Replace("\"raid_evacuate_max_commands_per_think\": 12", "\"raid_evacuate_max_commands_per_think\": 1")
                .Replace("\"raid_hold_radius_tiles\": 4", "\"raid_hold_radius_tiles\": 15").Replace("\"raid_hq_cover_margin_tiles\": 2", "\"raid_hq_cover_margin_tiles\": 9"),
                "ai/aggressive_s5_m.json", Data.Content);
            Assert.That(twin.UseProductionCapacity, Is.False);
            Assert.That(twin.IncomeWindowSeconds, Is.EqualTo(120));
            Assert.That(twin.UseSupplyAhead, Is.False);
            Assert.That(twin.SupplyAheadMaxPending, Is.EqualTo(6));
            Assert.That(twin.UseRaidWorkers, Is.False);
            Assert.That(twin.RaidActiveSeconds, Is.EqualTo(Fix64.FromInt(30)));
            Assert.That(twin.RaidEvacuateMaxCommandsPerThink, Is.EqualTo(1));
            Assert.That(twin.UseRaidResponse, Is.False);
            Assert.That(twin.RaidHoldRadiusTiles, Is.EqualTo(Fix64.FromInt(15)));
            Assert.That(twin.RaidHqCoverMarginTiles, Is.EqualTo(Fix64.FromInt(9)));
            Assert.That(Trace(twin, twin), Is.EqualTo(Trace(baseline, baseline)));
        }
    }
}
