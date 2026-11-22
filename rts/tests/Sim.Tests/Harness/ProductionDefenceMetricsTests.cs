// rts/tests/Sim.Tests/Harness/ProductionDefenceMetricsTests.cs
// Acceptance tests H-1 to H-4 of rts/docs/specs/ai_production_defence.md section 8 (package PD-M): the harness metrics worker_losses, worker_fed_losses, raid_seconds, raid_unanswered_edge_seconds, producers,
// capacity and the observation-only rule. H-1 to H-3 feed hand-built event streams to ProductionMetricsCollector over a real simulation of greybox_1v1 (units placed by script commands; the events are made up);
// H-4 plays a real game with and without the observer. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Tests of the production and defence metrics of the game summary.</summary>
    [TestFixture]
    public class ProductionDefenceMetricsTests
    {
        /// <summary>Player 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Loaded data, shared by the tests.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>The shipped ai_eval.json.</summary>
        private static AiEvalSettings Settings => AiEvalSettings.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_eval", "ai_eval.json")));

        /// <summary>Next sequence number of a spawn command. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A started greybox_1v1 simulation with the two factions in slots 0 and 1.</summary>
        private Simulation Scene(string faction0, string faction1)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            // Each player's HQ on its spawn point (the match starts with no units). Unit: tiles.
            Spawn(sim, faction0 == "skai" ? "skai.great_aerie" : "water.grand_shell", P0, new FixVec2(Fix64.Parse("8.5"), Fix64.Parse("8.5")));
            Spawn(sim, faction1 == "skai" ? "skai.great_aerie" : "water.grand_shell", P1, new FixVec2(Fix64.Parse("56.5"), Fix64.Parse("56.5")));
            return sim;
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

        /// <summary>The position of the owner's HQ.</summary>
        private static FixVec2 HqOf(Simulation sim, PlayerId owner)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Hq != null)
                {
                    return unit.Position;
                }
            }
            throw new AssertionException("no HQ");
        }

        /// <summary>A point <paramref name="dx"/> tiles east and <paramref name="dy"/> tiles south of <paramref name="from"/>.</summary>
        private static FixVec2 At(FixVec2 from, int dx, int dy)
        {
            return new FixVec2(from.X + Fix64.FromInt(dx), from.Y + Fix64.FromInt(dy));
        }

        /// <summary>One metrics collector begun on the scene.</summary>
        private static ProductionMetricsCollector Begin(Simulation sim)
        {
            ProductionMetricsCollector collector = new ProductionMetricsCollector(Settings);
            collector.Begin(sim);
            return collector;
        }

        /// <summary>Feeds one hand-built event at <paramref name="tick"/> as its own step.</summary>
        private static void Feed(ProductionMetricsCollector collector, Simulation sim, int tick, params SimEvent[] events)
        {
            collector.Observe(sim, tick, events);
        }

        /// <summary>H-1: a worker trained at 100 s and killed by an enemy at 130 s, one trained at 100 s and killed at 200 s, and one start worker killed at 50 s give worker_losses 3 and worker_fed_losses 1.</summary>
        [Test]
        public void H1_WorkerLossesAndFedLosses()
        {
            Simulation sim = Scene("skai", "skai");
            FixVec2 hq = HqOf(sim, P0);
            EntityId fed = Spawn(sim, "skai.fledgling", P0, At(hq, 2, 0));
            EntityId late = Spawn(sim, "skai.fledgling", P0, At(hq, 3, 0));
            EntityId start = Spawn(sim, "skai.fledgling", P0, At(hq, 4, 0));
            EntityId killer = Spawn(sim, "skai.windrunner", P1, At(hq, 30, 0));
            ProductionMetricsCollector collector = Begin(sim);
            Feed(collector, sim, 50 * 20, new UnitKilledEvent(50 * 20, start, killer, P0, P1));
            Feed(collector, sim, 100 * 20, new UnitTrainedEvent(100 * 20, EntityId.None, fed, "skai.fledgling"), new UnitTrainedEvent(100 * 20, EntityId.None, late, "skai.fledgling"));
            Feed(collector, sim, 130 * 20, new UnitKilledEvent(130 * 20, fed, killer, P0, P1));
            Feed(collector, sim, 200 * 20, new UnitKilledEvent(200 * 20, late, killer, P0, P1));
            ProductionMetrics slot0 = collector.Results()[0];
            Assert.That(slot0.WorkerLosses, Is.EqualTo(3));
            Assert.That(slot0.WorkerFedLosses, Is.EqualTo(1));
            Assert.That(collector.Results()[1].WorkerLosses, Is.EqualTo(0), "the enemy lost no worker");
        }

        /// <summary>H-2: hits on a slot-0 worker beside its HQ in seconds 120 to 124, a slot-0 armed unit arriving within 12 tiles at 123 s and slot-0 army supply above the raiders': raid_seconds 5 and raid_unanswered_edge_seconds 3 (seconds 120, 121, 122).</summary>
        [Test]
        public void H2_RaidSecondsAndUnansweredEdge()
        {
            Simulation sim = Scene("skai", "skai");
            FixVec2 hq = HqOf(sim, P0);
            EntityId worker = Spawn(sim, "skai.fledgling", P0, At(hq, 2, 0));
            // Two own army units far from the raid and one raider 8 tiles from it, outside weapon range: own supply 2 against 1.
            Spawn(sim, "skai.windrunner", P0, At(hq, 0, 40));
            Spawn(sim, "skai.windrunner", P0, At(hq, 1, 40));
            EntityId raider = Spawn(sim, "skai.windrunner", P1, At(hq, 2, 9));
            ProductionMetricsCollector collector = Begin(sim);
            for (int second = 120; second <= 124; second++)
            {
                if (second == 123)
                {
                    Spawn(sim, "skai.windrunner", P0, At(hq, 5, 0));
                }
                // The hit at the middle of the second, then the step at its last tick.
                Feed(collector, sim, second * 20 + 10, new UnitDamagedEvent(second * 20 + 10, raider, worker, Fix64.One, Fix64.FromInt(10)));
                Feed(collector, sim, second * 20 + 19);
            }
            ProductionMetrics slot0 = collector.Results()[0];
            Assert.That(slot0.RaidSeconds, Is.EqualTo(5));
            Assert.That(slot0.RaidUnansweredEdgeSeconds, Is.EqualTo(3));
            Assert.That(collector.Results()[1].RaidSeconds, Is.EqualTo(0), "the raider's side was not hit");
        }

        /// <summary>H-3: at 180 s one complete Tidepool and one complete Shell Forge, rare 0, no research: producers 2 and capacity 333 + 375 = 708; the samples after the game's end stay null.</summary>
        [Test]
        public void H3_ProducersAndCapacity()
        {
            Simulation sim = Scene("water", "water");
            FixVec2 hq = HqOf(sim, P0);
            Spawn(sim, "water.tidepool", P0, At(hq, 4, 4));
            Spawn(sim, "water.shell_forge", P0, At(hq, 8, 4));
            ProductionMetricsCollector collector = Begin(sim);
            Feed(collector, sim, 180 * 20 - 1);
            ProductionMetrics slot0 = collector.Results()[0];
            Assert.That(slot0.Producers[0], Is.EqualTo(2));
            Assert.That(slot0.Capacity[0], Is.EqualTo(708));
            Assert.That(slot0.Producers[1], Is.Null);
            Assert.That(slot0.Capacity[2], Is.Null);
        }

        /// <summary>H-3b: the 60 s income window and the bank mean: deposits of 100 common at 130 s and 170 s count at the 180 s sample, one at 100 s does not; the bank mean is the floor of the mean over seconds 60 to the window end.</summary>
        [Test]
        public void H3b_IncomeWindowAndBankMean()
        {
            Simulation sim = Scene("skai", "skai");
            ProductionMetricsCollector collector = Begin(sim);
            Feed(collector, sim, 100 * 20, new ResourceDepositedEvent(100 * 20, EntityId.None, EntityId.None, P0, ResourceKind.Common, 100));
            Feed(collector, sim, 130 * 20, new ResourceDepositedEvent(130 * 20, EntityId.None, EntityId.None, P0, ResourceKind.Common, 100));
            Feed(collector, sim, 170 * 20, new ResourceDepositedEvent(170 * 20, EntityId.None, EntityId.None, P0, ResourceKind.Common, 100), new ResourceDepositedEvent(170 * 20, EntityId.None, EntityId.None, P0, ResourceKind.Rare, 7));
            Feed(collector, sim, 180 * 20 - 1);
            Assert.That(collector.Results()[0].Income60[0], Is.EqualTo(200));
            // Bank samples are the stock now (the same at every second): the mean of equal values is that value.
            sim.Players.TryGet(P0, out Player? player);
            for (int second = 181; second <= 600; second++)
            {
                Feed(collector, sim, second * 20 - 1);
            }
            Assert.That(collector.Results()[0].BankMean, Is.EqualTo(player!.Common));
        }

        /// <summary>The median text: middle value, mean of the two middle values with .5 when not whole, a dash for none.</summary>
        [Test]
        public void MedianText_Forms()
        {
            Assert.That(BalanceReport.MedianText(new List<int> { 3, 1, 2 }), Is.EqualTo("2"));
            Assert.That(BalanceReport.MedianText(new List<int> { 1, 2, 3, 4 }), Is.EqualTo("2.5"));
            Assert.That(BalanceReport.MedianText(new List<int> { 1, 3 }), Is.EqualTo("2"));
            Assert.That(BalanceReport.MedianText(new List<int>()), Is.EqualTo("-"));
        }

        /// <summary>H-4: a real 240 s game plays the same with the metrics observer, a plain summary observer and none (state hash, result, play signature); the metrics are written, parse back byte for byte, and a summary without them has no metric keys.</summary>
        [Test]
        public void H4_ObservationOnly()
        {
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", "skai_vs_water", 0);
            AiEvalSettings settings = Settings;
            GameRecord plain = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240);
            GameRecord basic = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240, null, -1, -1, new SummaryObserver(settings.SummaryBucketSeconds, settings.SummaryBankTimesSeconds));
            GameRecord watched = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240, null, -1, -1, settings.NewObserver());
            Assert.That(watched.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(watched.Result, Is.EqualTo(plain.Result));
            Assert.That(watched.PlaySignature, Is.EqualTo(plain.PlaySignature));
            Assert.That(basic.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(basic.Summary0!.Production, Is.Null);
            Assert.That(basic.ToJsonLine(), Does.Not.Contain("worker_losses"));
            foreach (GameSummary summary in new[] { watched.Summary0!, watched.Summary1! })
            {
                ProductionMetrics m = summary.Production!;
                Assert.That(m.Producers.Count, Is.EqualTo(3));
                Assert.That(m.Producers[0], Is.GreaterThan(0), "a producer stands at 180 s");
                Assert.That(m.Producers[1], Is.Null, "the game ended at 240 s");
                Assert.That(m.BankMean, Is.Not.Null);
            }
            string line = watched.ToJsonLine();
            Assert.That(GameRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
            // The non-metric part of the line is the plain observer's: the metric keys come last in each summary.
            Assert.That(watched.Summary0!.ToJson(), Does.StartWith(basic.Summary0.ToJson().TrimEnd('}')));
        }
    }
}
