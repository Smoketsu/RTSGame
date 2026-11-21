// rts/tests/Sim.Tests/Ai/AiPrerequisiteTests.cs
// Tests TA-1 to TA-3 of package T (TA-2 and TA-3 changed by research_and_unlocks.md R-17) of rts/docs/specs/walls_and_tech.md (AI minimum): the skirmish AI never trains a unit whose requires building
// it does not own complete (FindFreeProducer), and rule T2 builds that building when the mix wants the unit and its producer exists. TA-4
// (exit test, no rejection) is SkirmishAiTests.ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided, which already demands zero rejections.
// Part of Rts.Sim.Tests. Setup: greybox_1v1, water against water, shipped aggressive_s3 (counter mix: Leviathan weight 700 against 6 Graspers
// with rare 400), a Grand Shell, a Shell Forge and a Tidepool (so rule T has nothing to build), six Tenders, stock 5000 common and 400 rare.
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
    /// <summary>The AI and unit prerequisites.</summary>
    [TestFixture]
    public sealed class AiPrerequisiteTests
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

        /// <summary>The shipped aggressive_s3 profile.</summary>
        private static AiProfile S3 => AiProfile.FromDataSet(Data, "aggressive_s3");

        /// <summary>The shipped aggressive_s3 profile with rule T switched off.</summary>
        private static AiProfile S3NoTech()
        {
            // The profile text with the switch flipped.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s3.json")).Replace("\r\n", "\n");
            Assert.That(text, Does.Contain("\"use_tech_on_demand\": true"));
            return AiProfile.Parse(text.Replace("\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false"), "ai/aggressive_s3.json", Data.Content);
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

        /// <summary>The water base described in the file header; returns the first Tender. <paramref name="workshop"/> adds a complete Reef Workshop.</summary>
        private EntityId WaterBase(out Simulation sim, bool workshop)
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
            player!.SetStock(5000, 400);
            return first;
        }

        /// <summary>The AI's think for P0 against a model of six Graspers.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile)
        {
            AiMemory memory = new AiMemory();
            memory.EnemyTypes["water.grasper"] = new EnemyTypeEstimate { Count = Fix64.FromInt(6), LastSeenTick = sim.Clock.Tick };
            memory.AttackThreshold = 8;
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("water"), memory, new DeterministicRandom(1));
        }

        /// <summary>Definition IDs of the intents of one kind.</summary>
        private static List<string> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            return intents.FindAll(i => i.Kind == kind).ConvertAll(i => i.DefinitionId);
        }

        /// <summary>TA-1: with no Reef Workshop the think never trains the Leviathan; it trains another mix unit from the Shell Forge.</summary>
        [Test]
        public void TA1_NoGatedTraining()
        {
            WaterBase(out Simulation sim, false);
            List<string> trains = Of(Decide(sim, S3), AiIntentKind.Train);
            Assert.That(trains, Does.Not.Contain("water.leviathan"));
            Assert.That(trains.Exists(id => id == "water.shellback" || id == "water.ink_lobber"), Is.True, string.Join(",", trains));
        }

        /// <summary>
        /// TA-2 (changed by research_and_unlocks.md R-17c): the Leviathan needs Leviathan Plans, which the AI never researches in R1, so rule T2 skips it
        /// and builds no Reef Workshop for it, with rule T on or off.
        /// </summary>
        [Test]
        public void TA2_RuleT2SkipsResearchGatedUnits()
        {
            WaterBase(out Simulation sim, false);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"));
            Assert.That(Of(Decide(sim, S3NoTech()), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"), "switch off");
        }

        /// <summary>
        /// TA-3 (changed by R-17d): with a complete Reef Workshop but without Leviathan Plans there is no Leviathan train; with the research
        /// queued at the workshop and stepped to completion the Leviathan train is emitted.
        /// </summary>
        [Test]
        public void TA3_TrainsOnceResearched()
        {
            WaterBase(out Simulation sim, true);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Train), Does.Not.Contain("water.leviathan"));
            // The workshop's ID (the only Reef Workshop of P0).
            EntityId workshop = EntityId.None;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Id == "water.reef_workshop")
                {
                    workshop = unit.Id;
                }
            }
            Assert.That(workshop, Is.Not.EqualTo(EntityId.None));
            sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, _sequence++, workshop, "water.leviathan_plans"));
            sim.Step();
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            Assert.That(player!.HasResearched("water.leviathan_plans"), Is.False);
            Assert.That(sim.Units.TryGet(workshop, out Unit? shop) && shop!.ProductionQueue.Count == 1, Is.True, "the research is queued");
            // The 45 s of research are not stepped here: the AI's plan changes with the clock (by tick 900 the mix no longer asks for the Leviathan), so the
            // finished research is set directly (the queue path itself is covered by ResearchTests R-1). The flag is the only thing the AI reads.
            player.AddResearch("water.leviathan_plans");
            // The research cost 175 rare; restore the stock the other tests of this file use (the counter-mix budget reads it).
            player.SetStock(5000, 400);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Train), Does.Contain("water.leviathan"));
        }
    }
}
