// rts/tests/Sim.Tests/Ai/AiPrerequisiteTests.cs
// Tests TA-1 to TA-3 of package T of rts/docs/specs/walls_and_tech.md (AI minimum): the skirmish AI never trains a unit whose requires building
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
            // Tender index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                EntityId id = Spawn(sim, "water.tender", P0, "6", (8 + i / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
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

        /// <summary>TA-2: rule T2 builds the Reef Workshop once; none with an order pending, none under construction, none with the switch off.</summary>
        [Test]
        public void TA2_RuleT2BuildsTheTech()
        {
            EntityId worker = WaterBase(out Simulation sim, false);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Build), Does.Contain("water.reef_workshop"));
            Assert.That(Of(Decide(sim, S3NoTech()), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"), "switch off");
            // A pending order: the worker has been told, the structure is not there yet.
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, worker, "water.reef_workshop", 17, 6));
            sim.Step();
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"), "pending order");
            // Under construction: step until the structure appears.
            bool placed = false;
            for (int i = 0; i < 600 && !placed; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    placed |= evt is StructurePlacedEvent;
                }
            }
            Assert.That(placed, Is.True);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Build), Does.Not.Contain("water.reef_workshop"), "under construction");
        }

        /// <summary>TA-3: with a complete Reef Workshop the think trains the Leviathan.</summary>
        [Test]
        public void TA3_TrainsOnceComplete()
        {
            WaterBase(out Simulation sim, true);
            Assert.That(Of(Decide(sim, S3), AiIntentKind.Train), Does.Contain("water.leviathan"));
        }
    }
}
