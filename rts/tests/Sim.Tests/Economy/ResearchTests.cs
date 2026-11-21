// rts/tests/Sim.Tests/Economy/ResearchTests.cs
// Acceptance tests R-1 to R-16 of package R1 of rts/docs/specs/research_and_unlocks.md (research and unlocks): research items queued at a tech
// building through train_unit, finishing into a per-player set that `requires` can name, rejections, queue lengths, lost buildings, hashes,
// queries, the script path, the loader and the shipped data. Part of Rts.Sim.Tests. Economy setup: TestWorld's economy map, shipped tuning,
// 20 ticks per second, P0 (test faction) starts with 200 common; rare is granted with the internal Player.AddResource before the first step.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Research: simulation rules, queries, loader and shipped data.</summary>
    [TestFixture]
    public sealed class ResearchTests
    {
        /// <summary>Research item A: 25 common, 50 rare, 2 s (40 ticks).</summary>
        private const string PlansA = "test.plans_a";

        /// <summary>Research item B: 25 common, 0 rare, 1 s (20 ticks).</summary>
        private const string PlansB = "test.plans_b";

        /// <summary>Research building: footprint 1, researches plans A and B.</summary>
        private const string LabId = "test.lab";

        /// <summary>Unit gated by plans A: speed 4, supply 1, cost 50, build 2 s.</summary>
        private const string LockedId = "test.locked";

        /// <summary>Producer of the locked unit and the runner, provides 8 supply.</summary>
        private const string LabBarracksId = "test.lab_barracks";

        /// <summary>Building that trains the runner and researches plans B.</summary>
        private const string MixedId = "test.mixed";

        /// <summary>Research building with 5 health, so one shot kills it (combat variant of R-6).</summary>
        private const string FrailLabId = "test.frail_lab";

        /// <summary>Unit that requires the lab and plans A (R-11).</summary>
        private const string BothId = "test.both";

        /// <summary>A research definition of the test faction.</summary>
        private static ResearchDefinition Research(string id, int common, int rare, string seconds)
        {
            return new ResearchDefinition(id, id, TestWorld.TestFactionId, common, rare, Fix64.Parse(seconds));
        }

        /// <summary>A building of the test faction (footprint 1, radius 0.5) with the given economy lists.</summary>
        private static UnitDefinition Building(string id, int health, int cost, List<string> trains, int supply, List<string>? researches)
        {
            return new UnitDefinition(id, id, TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(health), Fix64.Zero, 0, Fix64.Half, cost, 0,
                "armoured", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(5), false, false, trains, false, 1, supply, false, null, false, researches));
        }

        /// <summary>The catalog of the tests: TestWorld's plus the research items and buildings (never added to TestWorld.Catalog()).</summary>
        private static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddResearch(Research(PlansA, 25, 50, "2"));
            catalog.AddResearch(Research(PlansB, 25, 0, "1"));
            List<string> both = new List<string> { PlansA, PlansB };
            catalog.AddUnit(Building(LabId, 200, 50, new List<string>(), 0, both));
            catalog.AddUnit(Building(FrailLabId, 5, 50, new List<string>(), 0, new List<string> { PlansA }));
            catalog.AddUnit(new UnitDefinition(LockedId, "Locked", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 0, 0, false, new List<string> { PlansA })));
            catalog.AddUnit(new UnitDefinition(BothId, "Both", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 50, 0,
                "light", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 0, 0, false, new List<string> { LabId, PlansA })));
            catalog.AddUnit(Building(LabBarracksId, 200, 100, new List<string> { LockedId, TestWorld.RunnerId }, 8, null));
            catalog.AddUnit(Building(MixedId, 200, 100, new List<string> { TestWorld.RunnerId }, 8, new List<string> { PlansB }));
            return catalog;
        }

        /// <summary>A started economy simulation with this fixture's catalog; P0 holds 100 rare more than the start.</summary>
        private static Simulation Sim(int rare = 100, Tuning? tuning = null)
        {
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), Catalog(),
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), tuning ?? TestWorld.Tuning, TestWorld.EconomyMap()));
            sim.Start();
            PlayerOf(sim, P0).AddResource(ResourceKind.Rare, rare);
            return sim;
        }

        /// <summary>A simulation with a complete lab (16, 16) and barracks (12, 16) of P0.</summary>
        private static Simulation WithLab(out EntityId lab, out EntityId barracks, int rare = 100)
        {
            Simulation sim = Sim(rare);
            lab = Spawn(sim, LabId, P0, 16, 16);
            barracks = Spawn(sim, LabBarracksId, P0, 12, 16);
            return sim;
        }

        /// <summary>A train command for the current tick, issued by P0.</summary>
        private static TrainUnitCommand Train(Simulation sim, EntityId producer, string definition)
        {
            return new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), producer, definition);
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/> (the next tick to run) and returns every event on the way.</summary>
        private static List<SimEvent> StepTo(Simulation sim, int tick)
        {
            return StepCollect(sim, tick - sim.Clock.Tick);
        }

        // ------------------------------------------------------------------ R-1 to R-4

        /// <summary>R-1: queue and complete: paid at once, done on T + 39, nothing spawned.</summary>
        [Test]
        public void R1_QueueAndComplete()
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            int units = sim.Units.All.Count();
            int t = sim.Clock.Tick;
            int supply = PlayerOf(sim, P0).SupplyUsed;
            AssertAccepted(sim, Train(sim, lab, PlansA));
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.EqualTo((175, 50)));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply));
            Assert.That(EconomyRules.IsResearchQueued(sim, P0, PlansA), Is.True);
            Assert.That(EconomyRules.QueueEntryTicks(sim, PlansA), Is.EqualTo(40));
            // Ticks T + 1 to T + 38: not done.
            for (int tick = t + 1; tick <= t + 38; tick++)
            {
                sim.Step();
                Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.False, "tick " + tick);
            }
            sim.Step();
            Assert.That(sim.Clock.Tick, Is.EqualTo(t + 40));
            List<SimEvent> events = new List<SimEvent>(sim.Events.Current);
            ResearchCompletedEvent done = Of<ResearchCompletedEvent>(events).Single();
            Assert.That((done.Tick, done.Producer, done.Owner, done.ResearchId), Is.EqualTo((t + 39, lab, P0, PlansA)));
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.True);
            Assert.That(Get(sim, lab).ProductionQueue.Count, Is.EqualTo(0));
            Assert.That(EconomyRules.IsResearchQueued(sim, P0, PlansA), Is.False);
            Assert.That(sim.Units.All.Count(), Is.EqualTo(units));
        }

        /// <summary>R-2: the gate opens for orders on T + 40, not before.</summary>
        [Test]
        public void R2_UnlockAtOrderTime()
        {
            Simulation sim = WithLab(out EntityId lab, out EntityId barracks);
            UnitDefinition locked = sim.Content.Units[LockedId];
            int t = sim.Clock.Tick;
            AssertAccepted(sim, Train(sim, lab, PlansA));
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, locked), Is.EqualTo(PlansA));
            AssertRejected(sim, Train(sim, barracks, LockedId), "missing_prerequisite");
            StepTo(sim, t + 39);
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, locked), Is.EqualTo(PlansA));
            // Command on T + 39: step 2 runs before 7b, so still rejected.
            AssertRejected(sim, Train(sim, barracks, LockedId), "missing_prerequisite");
            Assert.That(sim.Clock.Tick, Is.EqualTo(t + 40));
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, locked), Is.Null);
            AssertAccepted(sim, Train(sim, barracks, LockedId));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(175 - 50));
        }

        /// <summary>R-3: every rejection reason of the research path.</summary>
        [Test]
        public void R3_Rejections()
        {
            Simulation a = WithLab(out _, out EntityId barracks);
            AssertRejected(a, Train(a, barracks, PlansA), "cannot_train");

            Simulation b = WithLab(out EntityId labB, out _);
            AssertRejected(b, Train(b, labB, "test.nothing"), "unknown_definition");

            Simulation c = WithLab(out EntityId labC, out _);
            c.Submit(Train(c, labC, PlansA));
            c.Submit(Train(c, labC, PlansA));
            c.Step();
            Assert.That(Reasons(new List<SimEvent>(c.Events.Current)), Is.EqualTo(new List<string> { "already_queued" }));
            Assert.That(PlayerOf(c, P0).Rare, Is.EqualTo(50));
            Assert.That(Get(c, labC).ProductionQueue.Count, Is.EqualTo(1));

            Simulation d = WithLab(out EntityId labD, out _);
            AssertAccepted(d, Train(d, labD, PlansA));
            StepCollect(d, 45);
            AssertRejected(d, Train(d, labD, PlansA), "already_researched");

            Simulation e = Sim();
            EntityId worker = Spawn(e, TestWorld.WorkerId, P0, 14, 14);
            EntityId building = PlaceStructure(e, worker, LabId, 20, 14);
            Assert.That(Get(e, building).IsUnderConstruction, Is.True);
            AssertRejected(e, Train(e, building, PlansA), "under_construction");

            Simulation f = WithLab(out EntityId labF, out _);
            AssertRejected(f, new TrainUnitCommand(f.Clock.Tick, P1, Seq(), labF, PlansA), "not_owner");

            Simulation g = WithLab(out EntityId labG, out _, 0);
            AssertRejected(g, Train(g, labG, PlansA), "insufficient_resources");
            AssertAccepted(g, Train(g, labG, PlansB));

            Simulation h = Sim();
            EntityId mixed = Spawn(h, MixedId, P0, 16, 16);
            PlayerOf(h, P0).AddResource(ResourceKind.Common, 1000);
            int full = h.Tuning.Economy.MaxQueueLength;
            for (int i = 0; i < full; i++)
            {
                AssertAccepted(h, Train(h, mixed, TestWorld.RunnerId));
            }
            AssertRejected(h, Train(h, mixed, PlansB), "queue_full");
        }

        /// <summary>R-4: rejection order: already_researched before insufficient_resources, already_queued before queue_full.</summary>
        [Test]
        public void R4_RejectionOrder()
        {
            Simulation sim = WithLab(out EntityId lab, out _, 50);
            AssertAccepted(sim, Train(sim, lab, PlansA));
            StepCollect(sim, 45);
            Assert.That(PlayerOf(sim, P0).Rare, Is.EqualTo(0));
            AssertRejected(sim, Train(sim, lab, PlansA), "already_researched");

            Tuning one = TuningWithEdit("tuning/economy.json", "\"max_queue_length\": 5", "\"max_queue_length\": 1");
            Simulation full = Sim(100, one);
            EntityId labFull = Spawn(full, LabId, P0, 16, 16);
            AssertAccepted(full, Train(full, labFull, PlansA));
            AssertRejected(full, Train(full, labFull, PlansA), "already_queued");
            AssertRejected(full, Train(full, labFull, PlansB), "queue_full");
        }

        // ------------------------------------------------------------------ R-5 to R-9

        /// <summary>R-5: serial queue: each entry has its own length, also behind a research entry.</summary>
        [Test]
        public void R5_SerialQueueLengths()
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            int t = sim.Clock.Tick;
            sim.Submit(Train(sim, lab, PlansB));
            sim.Submit(Train(sim, lab, PlansA));
            List<SimEvent> events = StepTo(sim, t + 61);
            Assert.That(Of<ResearchCompletedEvent>(events).Select(e => (e.ResearchId, e.Tick)).ToList(),
                Is.EqualTo(new List<(string, int)> { (PlansB, t + 19), (PlansA, t + 59) }));

            Simulation mix = Sim();
            EntityId mixed = Spawn(mix, MixedId, P0, 16, 16);
            int t2 = mix.Clock.Tick;
            mix.Submit(Train(mix, mixed, PlansB));
            mix.Submit(Train(mix, mixed, TestWorld.RunnerId));
            int runnerTicks = EconomyRules.BuildTicks(mix, mix.Content.Units[TestWorld.RunnerId]);
            List<SimEvent> mixEvents = StepTo(mix, t2 + runnerTicks + 25);
            Assert.That(Of<ResearchCompletedEvent>(mixEvents).Single().Tick, Is.EqualTo(t2 + 19));
            Assert.That(Of<UnitTrainedEvent>(mixEvents).Single().Tick, Is.EqualTo(t2 + 19 + runnerTicks));
        }

        /// <summary>R-6: a lost building loses the item in progress without refund; finished research is kept; the item can be bought again.</summary>
        [Test]
        public void R6_BuildingLostMidResearch()
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            int t = sim.Clock.Tick;
            AssertAccepted(sim, Train(sim, lab, PlansA));
            StepTo(sim, t + 10);
            List<SimEvent> events = Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), lab));
            events.AddRange(StepTo(sim, t + 101));
            Assert.That(Of<ResearchCompletedEvent>(events), Is.Empty);
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.False);
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.EqualTo((175, 50)));
            Assert.That(EconomyRules.IsResearchQueued(sim, P0, PlansA), Is.False);
            EntityId second = Spawn(sim, LabId, P0, 18, 16);
            AssertAccepted(sim, Train(sim, second, PlansA));
            Assert.That(PlayerOf(sim, P0).Rare, Is.EqualTo(0));
        }

        /// <summary>R-6 combat variant: the lab is shot dead mid-research; same result.</summary>
        [Test]
        public void R6_BuildingKilledMidResearch()
        {
            Simulation sim = Sim();
            EntityId lab = Spawn(sim, FrailLabId, P0, 16, 16);
            AssertAccepted(sim, Train(sim, lab, PlansA));
            EntityId shooter = Spawn(sim, TestWorld.ShooterId, P1, 16, 19);
            // The shooter acquires the lab by itself (5 health, one shot).
            List<SimEvent> events = StepCollect(sim, 100);
            Assert.That(sim.Units.TryGet(lab, out Unit? _), Is.False);
            Assert.That(Of<ResearchCompletedEvent>(events), Is.Empty);
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.False);
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.EqualTo((175, 50)));
            Assert.That(EconomyRules.IsResearchQueued(sim, P0, PlansA), Is.False);
        }

        /// <summary>R-7: finished research outlives its building.</summary>
        [Test]
        public void R7_FinishedResearchKept()
        {
            Simulation sim = WithLab(out EntityId lab, out EntityId barracks);
            AssertAccepted(sim, Train(sim, lab, PlansA));
            StepCollect(sim, 45);
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), lab));
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.True);
            AssertAccepted(sim, Train(sim, barracks, LockedId));
        }

        /// <summary>R-8: research counts only for the player whose building finished it.</summary>
        [Test]
        public void R8_OwnOnly()
        {
            Simulation sim = WithLab(out _, out EntityId barracks);
            PlayerOf(sim, P1).AddResource(ResourceKind.Rare, 100);
            EntityId labP1 = Spawn(sim, LabId, P1, 40, 40);
            AssertAccepted(sim, new TrainUnitCommand(sim.Clock.Tick, P1, Seq(), labP1, PlansA));
            StepCollect(sim, 45);
            Assert.That(PlayerOf(sim, P1).HasResearched(PlansA), Is.True);
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.False);
            AssertRejected(sim, Train(sim, barracks, LockedId), "missing_prerequisite");
        }

        /// <summary>R-9: two completions on one tick come in ascending producer ID order.</summary>
        [Test]
        public void R9_TwoCompletionsOneTick()
        {
            Simulation sim = Sim();
            EntityId l1 = Spawn(sim, LabId, P0, 16, 16);
            EntityId l2 = Spawn(sim, LabId, P0, 18, 16);
            Assert.That(l1.Value, Is.LessThan(l2.Value));
            int t = sim.Clock.Tick;
            sim.Submit(Train(sim, l2, PlansA));
            List<SimEvent> events = StepTo(sim, t + 20);
            sim.Submit(Train(sim, l1, PlansB));
            events.AddRange(StepTo(sim, t + 45));
            List<ResearchCompletedEvent> done = Of<ResearchCompletedEvent>(events);
            Assert.That(done.Select(e => (e.Tick, e.Producer, e.ResearchId)).ToList(),
                Is.EqualTo(new List<(int, EntityId, string)> { (t + 39, l1, PlansB), (t + 39, l2, PlansA) }));
        }

        // ------------------------------------------------------------------ R-10 to R-13

        /// <summary>The hash of every hash tick of an R-1 run that researches <paramref name="id"/>.</summary>
        private static List<ulong> HashesOfRun(string id)
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            AssertAccepted(sim, Train(sim, lab, id));
            List<ulong> hashes = new List<ulong>();
            foreach (SimEvent e in StepCollect(sim, 120))
            {
                if (e is StateHashedEvent hashed)
                {
                    hashes.Add(hashed.Hash);
                }
            }
            return hashes;
        }

        /// <summary>R-10: equal runs hash equal; a different item changes the hash; the player hash is order-independent.</summary>
        [Test]
        public void R10_Hash()
        {
            List<ulong> one = HashesOfRun(PlansA);
            Assert.That(one, Is.Not.Empty);
            Assert.That(HashesOfRun(PlansA), Is.EqualTo(one));
            List<ulong> other = HashesOfRun(PlansB);
            Assert.That(other.Count, Is.EqualTo(one.Count));
            Assert.That(other[0], Is.Not.EqualTo(one[0]), "differs at the first hash tick at or after T");

            Simulation ab = Sim();
            PlayerOf(ab, P0).AddResearch(PlansB);
            PlayerOf(ab, P0).AddResearch(PlansA);
            Simulation ba = Sim();
            PlayerOf(ba, P0).AddResearch(PlansA);
            PlayerOf(ba, P0).AddResearch(PlansB);
            Assert.That(ab.ComputeStateHash(), Is.EqualTo(ba.ComputeStateHash()));
            Assert.That(PlayerOf(ab, P0).Researched, Is.EqualTo(new[] { PlansA, PlansB }));
        }

        /// <summary>R-11: the queries.</summary>
        [Test]
        public void R11_Queries()
        {
            Simulation sim = Sim();
            Assert.That(EconomyRules.QueueEntryTicks(sim, PlansA), Is.EqualTo(40));
            Assert.That(EconomyRules.QueueEntryTicks(sim, TestWorld.RunnerId), Is.EqualTo(EconomyRules.BuildTicks(sim, sim.Content.Units[TestWorld.RunnerId])));
            Assert.That(EconomyRules.QueueEntryTicks(sim, "test.unknown"), Is.EqualTo(0));
            UnitDefinition both = sim.Content.Units[BothId];
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, both), Is.EqualTo(LabId));
            EntityId lab = Spawn(sim, LabId, P0, 16, 16);
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, both), Is.EqualTo(PlansA));
            AssertAccepted(sim, Train(sim, lab, PlansA));
            StepCollect(sim, 45);
            Assert.That(EconomyRules.FirstMissingRequirement(sim, P0, both), Is.Null);
        }

        /// <summary>R-12: a direct spawn ignores requires, research included.</summary>
        [Test]
        public void R12_DirectSpawn()
        {
            Simulation sim = Sim();
            EntityId locked = Spawn(sim, LockedId, P0, 20, 20);
            Assert.That(sim.Units.TryGet(locked, out Unit? unit) && unit != null, Is.True);
        }

        /// <summary>R-13: a command script naming the research gives the result of R-1; shipped data takes 900 ticks.</summary>
        [Test]
        public void R13_ScriptPath()
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            int t = sim.Clock.Tick;
            CommandScript script = CommandScript.Parse("{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 100,"
                + " \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"test\" } ],"
                + " \"commands\": [ { \"tick\": " + t + ", \"issuer\": 0, \"sequence\": 0, \"type\": \"train_unit\", \"producer\": " + lab.Value
                + ", \"definition\": \"test.plans_a\" } ] }", "test/research.json");
            sim.Submit(script.Commands[0]);
            List<SimEvent> events = StepTo(sim, t + 45);
            Assert.That(Of<ResearchCompletedEvent>(events).Single().Tick, Is.EqualTo(t + 39));

            DataSet data = TestData.LoadAll();
            CommandScript shipped = CommandScript.Parse("{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 1000,"
                + " \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" }, { \"id\": 1, \"team\": 1, \"faction\": \"water\" } ],"
                + " \"commands\": [ { \"tick\": 0, \"issuer\": 255, \"sequence\": 0, \"type\": \"spawn_unit\", \"definition\": \"skai.storm_spire\", \"owner\": 0, \"x\": \"10\", \"y\": \"10\" },"
                + " { \"tick\": 1, \"issuer\": 0, \"sequence\": 0, \"type\": \"train_unit\", \"producer\": 1, \"definition\": \"skai.thunderhead_plans\" } ] }",
                "test/shipped_research.json", BuiltInMechanics.ScriptParsers);
            Simulation real = new Simulation(new SimulationConfig(data.CreateMatchSettings(shipped.Seed), data.Content, shipped.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(null), data.Cycles.Values));
            real.Start();
            PlayerOf(real, P0).AddResource(ResourceKind.Rare, 175);
            foreach (Command command in shipped.Commands)
            {
                real.Submit(command);
            }
            List<SimEvent> all = StepCollect(real, 1000);
            Assert.That(Of<ResearchCompletedEvent>(all).Single().Tick, Is.EqualTo(1 + 899));
            Assert.That(PlayerOf(real, P0).HasResearched("skai.thunderhead_plans"), Is.True);
        }

        // ------------------------------------------------------------------ R-14 to R-16

        /// <summary>The shipped Thunderhead Plans text.</summary>
        private const string PlansPath = "research/skai/thunderhead_plans.json";

        /// <summary>The shipped files plus one extra file.</summary>
        private static List<DataSourceFile> WithFile(string path, string text)
        {
            List<DataSourceFile> files = TestData.ReadAll();
            files.Add(new DataSourceFile(path, text));
            return files;
        }

        /// <summary>The shipped text of a data file.</summary>
        private static string TextOf(string path)
        {
            return TestData.ReadAll().Single(f => f.Path == path).Text;
        }

        /// <summary>Asserts loading <paramref name="files"/> fails with a message naming the file, the field and the text.</summary>
        private static void AssertLoadFails(List<DataSourceFile> files, string file, string field, string text)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.Contain(file), text);
            Assert.That(ex.Message, Does.Contain(field), text);
            Assert.That(ex.Message, Does.Contain(text), text);
        }

        /// <summary>The Storm Spire file with its researches list replaced.</summary>
        private static List<DataSourceFile> Spire(string list)
        {
            return TestData.WithEdit("units/skai/storm_spire.json", "\"researches\": [\"skai.stormcaller_plans\", \"skai.thunderhead_plans\"]", "\"researches\": " + list);
        }

        /// <summary>R-14: every bad research file, researches list and requires entry is rejected with file, field and message.</summary>
        [Test]
        public void R14_Loader()
        {
            string plans = TextOf(PlansPath);
            AssertLoadFails(WithFile("research/misplaced.json", plans), "research/misplaced.json", string.Empty, "research/<faction>/<name>.json");
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"cost_rare\": 175,", "\"cost_rare\": 175, \"bogus\": 1,"), "thunderhead_plans.json", "bogus", "bogus");
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"id\": \"skai.thunderhead_plans\"", "\"id\": \"skai.other\""), "thunderhead_plans.json", "id", "skai.thunderhead_plans");
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"faction_id\": \"skai\"", "\"faction_id\": \"water\""), "thunderhead_plans.json", "faction_id", "match the folder");
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"cost_rare\": 175", "\"cost_rare\": 100001"), "thunderhead_plans.json", "cost_rare", "100000");
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"display_name\": \"Thunderhead Plans\"", "\"display_name\": \"" + new string('x', 41) + "\""), "thunderhead_plans.json", "display_name", "1 to 40 characters");
            Assert.That(DataLoader.Load(TestData.WithEdit(PlansPath, "\"display_name\": \"Thunderhead Plans\"", "\"display_name\": \"" + new string('x', 40) + "\"")).Content.Researches["skai.thunderhead_plans"].DisplayName.Length, Is.EqualTo(40));
            ArgumentOutOfRangeException rare = Expect.Throws<ArgumentOutOfRangeException>(() => new ResearchDefinition("a.b", "n", "a", 0, 100001, Fix64.One));
            Assert.That(rare.ParamName, Is.EqualTo("costRare"));
            AssertLoadFails(TestData.WithEdit(PlansPath, "\"research_seconds\": 45", "\"research_seconds\": 0"), "thunderhead_plans.json", "research_seconds", "0.05");
            AssertLoadFails(WithFile("research/skai/windrunner.json", plans.Replace("skai.thunderhead_plans", "skai.windrunner")), "research/skai/windrunner.json", "id",
                "research and unit IDs share one namespace");
            AssertLoadFails(Spire("[\"skai.nope\"]"), "storm_spire.json", "researches", "no file in research/");
            AssertLoadFails(Spire("[\"water.leviathan_plans\"]"), "storm_spire.json", "researches", "research must be of the same faction");
            AssertLoadFails(Spire("[\"skai.thunderhead_plans\", \"skai.thunderhead_plans\"]"), "storm_spire.json", "researches", "twice");
            AssertLoadFails(Spire("[\"a1\", \"a2\", \"a3\", \"a4\", \"a5\", \"a6\", \"a7\", \"a8\", \"a9\"]"), "storm_spire.json", "researches", "more than 8");
            AssertLoadFails(TestData.WithEdit("units/skai/windrunner.json", "\"id\": \"skai.windrunner\",", "\"id\": \"skai.windrunner\", \"researches\": [\"skai.thunderhead_plans\"],"),
                "windrunner.json", "researches", "only allowed on a building");
            AssertLoadFails(TestData.WithEdit("units/skai/great_aerie.json", "\"id\": \"skai.great_aerie\",", "\"id\": \"skai.great_aerie\", \"researches\": [\"skai.thunderhead_plans\"],"),
                "great_aerie.json", "researches", "only allowed on a building");
            AssertLoadFails(Spire("[\"skai.thunderhead_plans\"]"), "stormcaller.json", "requires[0]", "which no building of faction 'skai' lists in researches");
            AssertLoadFails(TestData.WithEdit("units/skai/thunderhead.json", "\"skai.thunderhead_plans\"]", "\"water.leviathan_plans\"]"), "thunderhead.json", "requires[1]", "same faction");
            // A unit that mixes a building and a research item loads, and the cycle check passes over research IDs.
            DataSet data = TestData.LoadAll();
            Assert.That(data.Content.Units["skai.thunderhead"].Economy.Requires, Is.EqualTo(new[] { "skai.storm_spire", "skai.thunderhead_plans" }));
        }

        /// <summary>R-15: shipped research data, the four gated units and the equal trees.</summary>
        [Test]
        public void R15_ShippedData()
        {
            DataSet data = TestData.LoadAll();
            (string Id, string Faction, int Common, int Rare, string Seconds)[] expected =
            {
                ("skai.stormcaller_plans", "skai", 25, 125, "30"),
                ("skai.thunderhead_plans", "skai", 25, 175, "45"),
                ("water.ink_lobber_plans", "water", 25, 125, "30"),
                ("water.leviathan_plans", "water", 25, 175, "45"),
            };
            Assert.That(data.Content.Researches.Count, Is.EqualTo(4));
            foreach ((string id, string faction, int common, int rare, string seconds) in expected)
            {
                ResearchDefinition r = data.Content.Researches[id];
                Assert.That((r.FactionId, r.CostCommon, r.CostRare, r.ResearchSeconds), Is.EqualTo((faction, common, rare, Fix64.Parse(seconds))), id);
            }
            Assert.That(data.Content.Units["skai.storm_spire"].Economy.Researches, Is.EqualTo(new[] { "skai.stormcaller_plans", "skai.thunderhead_plans" }));
            Assert.That(data.Content.Units["water.reef_workshop"].Economy.Researches, Is.EqualTo(new[] { "water.ink_lobber_plans", "water.leviathan_plans" }));
            Assert.That(data.Content.Units["skai.stormcaller"].Economy.Requires, Is.EqualTo(new[] { "skai.stormcaller_plans" }));
            Assert.That(data.Content.Units["water.ink_lobber"].Economy.Requires, Is.EqualTo(new[] { "water.ink_lobber_plans" }));
            Assert.That(data.Content.Units["skai.thunderhead"].Economy.Requires, Is.EqualTo(new[] { "skai.storm_spire", "skai.thunderhead_plans" }));
            Assert.That(data.Content.Units["water.leviathan"].Economy.Requires, Is.EqualTo(new[] { "water.reef_workshop", "water.leviathan_plans" }));
            foreach (UnitDefinition unit in data.Content.Units.Values)
            {
                bool gated = unit.Id == "skai.stormcaller" || unit.Id == "skai.thunderhead" || unit.Id == "water.ink_lobber" || unit.Id == "water.leviathan";
                Assert.That(unit.Economy.Requires.Any(r => data.Content.Researches.ContainsKey(r)), Is.EqualTo(gated), unit.Id);
            }
            // Equal trees: count, common, rare and seconds per faction.
            List<(int, int, int, Fix64)> trees = new List<(int, int, int, Fix64)>();
            foreach (string faction in new[] { "skai", "water" })
            {
                List<ResearchDefinition> items = data.Content.Researches.Values.Where(r => r.FactionId == faction).ToList();
                trees.Add((items.Count, items.Sum(r => r.CostCommon), items.Sum(r => r.CostRare), items.Aggregate(Fix64.Zero, (sum, r) => sum + r.ResearchSeconds)));
            }
            Assert.That(trees[0], Is.EqualTo(trees[1]));
            Assert.That(trees[0], Is.EqualTo((2, 50, 300, Fix64.FromInt(75))));
        }

        /// <summary>R-16: the content digest covers research cost and the researches list, not the display name.</summary>
        [Test]
        public void R16_DigestAndCoverage()
        {
            ulong baseline = DataLoader.Load(TestData.ReadAll()).Content.ComputeDigest();
            Assert.That(DataLoader.Load(TestData.WithEdit(PlansPath, "\"cost_rare\": 175", "\"cost_rare\": 176")).Content.ComputeDigest(), Is.Not.EqualTo(baseline));
            Assert.That(DataLoader.Load(Spire("[\"skai.thunderhead_plans\", \"skai.stormcaller_plans\"]")).Content.ComputeDigest(), Is.Not.EqualTo(baseline));
            Assert.That(DataLoader.Load(TestData.WithEdit(PlansPath, "\"display_name\": \"Thunderhead Plans\"", "\"display_name\": \"Renamed\"")).Content.ComputeDigest(), Is.EqualTo(baseline));
        }
    }
}
