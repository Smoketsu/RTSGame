// rts/tests/Sim.Tests/Ai/AiResearchTests.cs
// Tests RA-1 to RA-6 and RA-9 of package R2 (rts/docs/specs/research_and_unlocks.md R2.4): the skirmish AI's research rules RQ-1 and RQ-2 behind use_research, and the fog rule. RA-7 (exit test with
// research) is in SkirmishAiTests, RA-8 (harness accounting) in Harness/ResearchAccountingTests. Part of Rts.Sim.Tests. Setup as in AiPrerequisiteTests: greybox_1v1, water against water, a Grand Shell,
// a Shell Forge, a Tidepool, six Tenders, stock 5000 common and 400 rare, a model of six Graspers (the counter mix gives the Leviathan weight 700 in aggressive_s3).
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>The AI and research items.</summary>
    [TestFixture]
    public sealed class AiResearchTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A shipped profile with use_research set as given (the file text is edited, the shipped file is untouched).</summary>
        /// <param name="id">Profile ID (file name without extension).</param>
        /// <param name="research">Value of use_research to write.</param>
        private static AiProfile Profile(string id, bool research)
        {
            // The profile text with the switch set.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json")).Replace("\r\n", "\n");
            Assert.That(text, Does.Contain("\"use_research\": false"));
            return AiProfile.Parse(text.Replace("\"use_research\": false", research ? "\"use_research\": true" : "\"use_research\": false"), "ai/" + id + ".json", Data.Content);
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

        /// <summary>The water base of the file header, P0 holding 5000 common and <paramref name="rare"/> rare; returns the first Tender.</summary>
        private EntityId WaterBase(out Simulation sim, bool workshop, int rare = 400)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "water", "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            ExploreKit.ExploreAll(sim);
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            EntityId first = EntityId.None;
            // Tender y positions. Unit: tiles. Range: on the map.
            string[] tenderY = { "8", "8.5", "9", "9.5", "10", "10.5" };
            // Tender index. Range: 0 to 5.
            for (int i = 0; i < tenderY.Length; i++)
            {
                EntityId id = Spawn(sim, "water.tender", P0, "6", tenderY[i]);
                first = i == 0 ? id : first;
            }
            Spawn(sim, "water.shell_forge", P0, "13.5", "6.5");
            Spawn(sim, "water.tidepool", P0, "13.5", "10.5");
            if (workshop)
            {
                Spawn(sim, "water.reef_workshop", P0, "16.5", "6.5");
            }
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(5000, rare);
            // One Tender gathers from field 0, so the player has income and a budget-queue profile buys through its queues.
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, first, 0));
            sim.Step();
            return first;
        }

        /// <summary>The AI's think for P0 against a model of six Graspers, with the given memory.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            memory.EnemyTypes["water.grasper"] = new EnemyTypeEstimate { Count = Fix64.FromInt(6), LastSeenTick = sim.Clock.Tick };
            memory.AttackThreshold = 8;
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("water"), memory, new DeterministicRandom(1));
        }

        /// <summary>The think with a fresh memory.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile)
        {
            return Decide(sim, profile, new AiMemory());
        }

        /// <summary>The intents of one kind as "producer:definition" (Train) or "definition" (Build), in order.</summary>
        private static List<string> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            return intents.FindAll(i => i.Kind == kind).ConvertAll(i => kind == AiIntentKind.Train ? i.Unit.Value + ":" + i.DefinitionId : i.DefinitionId);
        }

        /// <summary>The ID of P0's first unit of a definition.</summary>
        private static EntityId Find(Simulation sim, string definition)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Id == definition)
                {
                    return unit.Id;
                }
            }
            throw new AssertionException("no " + definition);
        }

        /// <summary>RA-1: the shipped profiles carry use_research false but the candidate; with it false the think equals the shipped profile's think.</summary>
        [Test]
        public void RA1_SwitchOffIsR1()
        {
            foreach (string id in new[] { "aggressive", "aggressive_s3", "aggressive_s5", "aggressive_s5_b", "defensive" })
            {
                Assert.That(AiProfile.FromDataSet(Data, id).UseResearch, Is.False, id);
            }
            Assert.That(AiProfile.FromDataSet(Data, "aggressive_s5_r").UseResearch, Is.True);
            WaterBase(out Simulation sim, true);
            // Intents of the shipped profile and of the copy with the switch written false, as text.
            List<string> shipped = Of(Decide(sim, AiProfile.FromDataSet(Data, "aggressive_s5")), AiIntentKind.Train);
            List<string> copy = Of(Decide(sim, Profile("aggressive_s5", false)), AiIntentKind.Train);
            Assert.That(copy, Is.EqualTo(shipped));
            Assert.That(shipped.Exists(s => s.EndsWith("_plans")), Is.False);
        }

        /// <summary>RA-2: Leviathan weight 700, an idle complete Reef Workshop and money: Leviathan Plans is queued; with it queued there is no second research train.</summary>
        [Test]
        public void RA2_ResearchForTheMix()
        {
            WaterBase(out Simulation sim, true);
            // The workshop's ID.
            EntityId workshop = Find(sim, "water.reef_workshop");
            AiProfile profile = Profile("aggressive_s3", true);
            Assert.That(Of(Decide(sim, profile), AiIntentKind.Train), Does.Contain(workshop.Value + ":water.leviathan_plans"));
            sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, _sequence++, workshop, "water.leviathan_plans"));
            sim.Step();
            Assert.That(Of(Decide(sim, profile), AiIntentKind.Train).FindAll(s => s.EndsWith("_plans")), Is.Empty);
        }

        /// <summary>RA-3: the Ink Lobber needs Brinecaster Plans and no workshop exists: the think builds one; with one ordered but not placed, none.</summary>
        [Test]
        public void RA3_BuildsTheResearcher()
        {
            EntityId tender = WaterBase(out Simulation sim, false);
            AiProfile profile = Profile("aggressive_s3", true);
            Assert.That(Of(Decide(sim, profile), AiIntentKind.Build), Does.Contain("water.reef_workshop"));
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, tender, "water.reef_workshop", 16, 6));
            sim.Step();
            Assert.That(Of(Decide(sim, profile), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"));
        }

        /// <summary>RA-4: with Leviathan Plans in the view the Leviathan is trained at the Shell Forge.</summary>
        [Test]
        public void RA4_TrainsOnceResearched()
        {
            WaterBase(out Simulation sim, true);
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.AddResearch("water.leviathan_plans");
            Assert.That(Of(Decide(sim, Profile("aggressive_s3", true)), AiIntentKind.Train), Does.Contain(Find(sim, "water.shell_forge").Value + ":water.leviathan"));
        }

        /// <summary>RA-5: budget queues, surplus on, the mix needs nothing: the ordinally first unresearched item is bought; with the surplus off (thresholds above the stock) none.</summary>
        [Test]
        public void RA5_Surplus()
        {
            WaterBase(out Simulation sim, true);
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            // A profile on budget queues with the rare surplus thresholds as written, and one with them above any stock.
            AiProfile on = Profile("aggressive_s5_b", true);
            AiMemory funded = new AiMemory();
            funded.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Common)] = 500;
            funded.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)] = 500;
            Assert.That(Of(Decide(sim, on, funded), AiIntentKind.Train).FindAll(s => s.EndsWith("_plans")), Is.Not.Empty, "an item is bought");
            // Both mix items done: nothing is left to buy.
            player!.AddResearch("water.ink_lobber_plans");
            player.AddResearch("water.leviathan_plans");
            AiMemory fundedAgain = new AiMemory();
            fundedAgain.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Common)] = 500;
            fundedAgain.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)] = 500;
            Assert.That(Of(Decide(sim, on, fundedAgain), AiIntentKind.Train).FindAll(s => s.EndsWith("_plans")), Is.Empty);
        }

        /// <summary>RA-6: a research purchase on budget queues is charged to the tech queue's cumulative spending; money short records the tech want and buys nothing.</summary>
        [Test]
        public void RA6_BudgetAccounting()
        {
            WaterBase(out Simulation sim, true);
            AiProfile profile = Profile("aggressive_s5_b", true);
            AiMemory memory = new AiMemory();
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Common)] = 500;
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)] = 500;
            // The think with full stock.
            List<string> trains = Of(Decide(sim, profile, memory), AiIntentKind.Train).FindAll(s => s.EndsWith("_plans"));
            Assert.That(trains, Is.Not.Empty);
            // The research bought, as the definition the cost is read from.
            ResearchDefinition bought = Data.Content.Researches[trains[0].Substring(trains[0].IndexOf(':') + 1)];
            Assert.That(memory.BudgetSpent[AiBudget.Cell(AiBudget.Tech, AiBudget.Common)], Is.EqualTo(bought.CostCommon));
            Assert.That(memory.BudgetSpent[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)], Is.EqualTo(bought.CostRare));
            // The same view with no rare at all: the item cannot be paid.
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(5000, 0);
            AiMemory poor = new AiMemory();
            Assert.That(Of(Decide(sim, profile, poor), AiIntentKind.Train).FindAll(s => s.EndsWith("_plans")), Is.Empty);
            Assert.That(poor.BudgetSpent[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)], Is.EqualTo(0));
            Assert.That(poor.BudgetWants[AiBudget.Cell(AiBudget.Tech, AiBudget.Rare)], Is.GreaterThan(0), "the tech want is recorded");
        }

        /// <summary>RA-9: P0's view holds none of the enemy's research and no queue of an enemy structure.</summary>
        [Test]
        public void RA9_Fog()
        {
            WaterBase(out Simulation sim, true);
            Assert.That(sim.Players.TryGet(P0, out Player? self), Is.True);
            Assert.That(sim.Players.TryGet(P1, out Player? enemy), Is.True);
            enemy!.AddResearch("water.ink_lobber_plans");
            Spawn(sim, "water.shell_forge", P1, "30.5", "30.5");
            AiWorldView view = AiWorldView.Build(sim, P0, new AiMemory().KnownFieldRemaining);
            Assert.That(view.OwnResearched, Is.EqualTo(self!.Researched));
            Assert.That(view.OwnResearched, Does.Not.Contain("water.ink_lobber_plans"));
            foreach (AiUnitView unit in view.VisibleEnemies)
            {
                Assert.That(unit.Queue, Is.Empty);
            }
        }
    }
}
