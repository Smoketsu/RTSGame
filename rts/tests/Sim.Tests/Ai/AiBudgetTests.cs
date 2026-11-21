// rts/tests/Sim.Tests/Ai/AiBudgetTests.cs
// Acceptance tests BQ-1 to BQ-13 of rts/docs/specs/ai_budget_queues.md (P-006 package B, the budget queues): off is identical (BQ-1), the profile parser (BQ-2), the proportional split and its cap (BQ-3, BQ-4), release and
// bounds over 1000 random inputs (BQ-5), head order (BQ-6), queues that do not block each other (BQ-7), no income (BQ-8), the defence ceiling (BQ-9), the worker floor (BQ-10), the rare surplus (BQ-11), the factors (BQ-12)
// and two real games (BQ-13). The allocation tests call AiBudget on arrays; the rule tests build a real greybox_1v1 simulation, spawn units with script commands and run AiBrain.Decide on the view. Part of Rts.Sim.Tests.
// BQ-1 and BQ-13 play full games through the harness code (about three minutes together).
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
using Rts.Sim.Tests.Harness;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package B: budget queues.</summary>
    [TestFixture]
    public class AiBudgetTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Deletes the temporary folders of the harness helpers.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        // ------------------------------------------------------------------ world helpers

        /// <summary>A started greybox_1v1 match (Skai slot 0, Water slot 1, cycles off, seed 1234) with nothing spawned.</summary>
        private static Simulation Greybox()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
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

        /// <summary>
        /// A base for <paramref name="owner"/> beside its spawn: the HQ, <paramref name="workers"/> workers of which <paramref name="gathering"/> are sent to field <paramref name="field"/> (so the player has income),
        /// and the given extra structures and units on free tiles. Every site counts as explored.
        /// </summary>
        private Simulation Base(PlayerId owner, int workers, int gathering, int field, string[] extras)
        {
            Simulation sim = Greybox();
            // The spawn point of the player. Unit: tiles.
            FixVec2 spawn = sim.Map.Spawns[owner.Value];
            // Sign that places the base towards the map centre. Range: -1 or 1.
            int sign = owner == P0 ? 1 : -1;
            Spawn(sim, owner == P0 ? "skai.great_aerie" : "water.grand_shell", owner, spawn);
            // Worker number. Range: 0 to workers - 1.
            for (int i = 0; i < workers; i++)
            {
                EntityId worker = Spawn(sim, owner == P0 ? "skai.fledgling" : "water.tender", owner, spawn + new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(sign * (1 + i / 3))));
                if (i < gathering)
                {
                    sim.Submit(new GatherCommand(sim.Clock.Tick, owner, _sequence++, worker, field));
                    sim.Step();
                }
            }
            // Extra number. Range: 0 to extras.Length - 1.
            for (int i = 0; i < extras.Length; i++)
            {
                Spawn(sim, extras[i], owner, spawn + new FixVec2(Fix64.FromInt(sign * (5 + 3 * i)), Fix64.FromInt(-sign * 2)));
            }
            ExploreKit.ExploreAll(sim);
            return sim;
        }

        /// <summary>Sets a player's stock (test-only access to the internal setter).</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>Decides for a player with a profile and memory.</summary>
        private static List<AiIntent> Decide(Simulation sim, PlayerId player, string profileId, AiMemory memory)
        {
            AiProfile profile = AiProfile.FromDataSet(Data, profileId);
            return AiBrain.Decide(AiWorldView.Build(sim, player), profile, profile.RequireFaction(player == P0 ? "skai" : "water"), memory, new DeterministicRandom(1));
        }

        /// <summary>The train intents of a list as "definition" strings, in order.</summary>
        private static List<string> Trains(List<AiIntent> intents)
        {
            List<string> trains = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Train)
                {
                    trains.Add(intent.DefinitionId);
                }
            }
            return trains;
        }

        /// <summary>One intent as text, so two lists can be compared.</summary>
        private static string Text(AiIntent intent)
        {
            return intent.Kind + "|" + intent.Unit + "|" + intent.Field + "|" + intent.DefinitionId + "|" + intent.TileX + "|" + intent.TileY + "|" + intent.Point + "|" + intent.Target;
        }

        /// <summary>The profile file text with edits applied (each old text must occur once), parsed under the candidate's file name.</summary>
        private static AiProfile Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_b.json"));
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_b.json", Data.Content);
        }

        // ------------------------------------------------------------------ BQ-1

        /// <summary>BQ-1: every shipped profile loads and carries the switch off except the candidate; abl_s5b_no_b and aggressive_s5 give equal state hash and play signature in one fixed-seed 600 s game per matchup on prototype_1v1.</summary>
        [Test]
        public void Bq1_OffIsIdentical()
        {
            // Profile IDs found in the ai folder.
            int found = 0;
            foreach (string path in Directory.GetFiles(Path.Combine(TestData.FindDataDirectory(), "ai"), "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseBudgetQueues, Is.EqualTo(id == "aggressive_s5_b"), id);
                found++;
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(23));
            AiProfile s5 = AiProfile.FromDataSet(Data, "aggressive_s5");
            AiProfile ablation = AiProfile.FromDataSet(Data, "abl_s5b_no_b");
            Assert.That(ablation.UseThreatDefence, Is.False);
            Assert.That(ablation.UseForwardProducers, Is.False);
            Assert.That(ablation.BudgetWorkerFloor, Is.EqualTo(s5.BudgetWorkerFloor));
            foreach (string matchup in new[] { "skai_vs_skai", "skai_vs_water", "water_vs_water" })
            {
                ulong seed = SeedDerivation.GameSeed(20261121UL, "prototype_1v1", matchup, 0);
                GameRecord a = GameRunner.Play(Data, "prototype_1v1", matchup, 0, seed, "aggressive_s5", "aggressive_s5", 600);
                GameRecord b = GameRunner.Play(Data, "prototype_1v1", matchup, 0, seed, "abl_s5b_no_b", "abl_s5b_no_b", 600);
                Assert.That(b.StateHash, Is.EqualTo(a.StateHash), matchup + " state hash");
                Assert.That(b.PlaySignature, Is.EqualTo(a.PlaySignature), matchup + " play signature");
                Assert.That(b.EndTick, Is.EqualTo(a.EndTick), matchup + " end tick");
            }
        }

        // ------------------------------------------------------------------ BQ-2

        /// <summary>BQ-2: a missing key, an unknown queue ID, a missing queue ID, weight 1001, all-zero common weights and budget_rare_surplus_off above on each fail naming the file and the field.</summary>
        [Test]
        public void Bq2_Parser()
        {
            // File name every message must start with.
            const string file = "ai/aggressive_s5_b.json";
            DataValidationException missing = Expect.Throws<DataValidationException>(() => Edited("\"budget_worker_floor\": 4,", ""));
            Assert.That(missing.Message, Does.StartWith(file).And.Contain("budget_worker_floor"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => Edited("\"defence\": 6 }", "\"defense\": 6 }"));
            Assert.That(unknown.Message, Does.StartWith(file).And.Contain("defense"));
            DataValidationException noQueue = Expect.Throws<DataValidationException>(() => Edited("\"tech\": 4, \"defence\": 6 }", "\"defence\": 6 }"));
            Assert.That(noQueue.Message, Does.StartWith(file).And.Contain("tech"));
            DataValidationException heavy = Expect.Throws<DataValidationException>(() => Edited("\"army\": 60, \"tech\": 4,", "\"army\": 1001, \"tech\": 4,"));
            Assert.That(heavy.Message, Does.StartWith(file).And.Contain("army"));
            DataValidationException zero = Expect.Throws<DataValidationException>(() => Edited("{ \"economy\": 120, \"expansion\": 120, \"production\": 10, \"army\": 60, \"tech\": 4, \"defence\": 6 }",
                "{ \"economy\": 0, \"expansion\": 0, \"production\": 0, \"army\": 0, \"tech\": 0, \"defence\": 0 }"));
            Assert.That(zero.Message, Does.StartWith(file).And.Contain("budget_common_weights"));
            DataValidationException band = Expect.Throws<DataValidationException>(() => Edited("\"budget_rare_surplus_off\": 80", "\"budget_rare_surplus_off\": 101"));
            Assert.That(band.Message, Does.StartWith(file).And.Contain("budget_rare_surplus_off"));
        }

        // ------------------------------------------------------------------ BQ-3 to BQ-5, BQ-9, BQ-12

        /// <summary>BQ-3: free common 300, army needs 300 (weight 60) and defence 300 (weight 6): 272 and 27 after pass 1, then the leftover 1 to the army: 273 and 27.</summary>
        [Test]
        public void Bq3_ProportionalSplit()
        {
            // Needs, weights and grants per queue (army is queue 3, defence queue 5).
            int[] need = { 0, 0, 0, 300, 0, 300 };
            int[] weight = { 0, 0, 0, 60, 0, 6 };
            int[] grant = new int[6];
            // Money left after the split. Unit: resource units.
            long left = AiBudget.Distribute(300, need, weight, grant);
            Assert.That(grant[3], Is.EqualTo(273));
            Assert.That(grant[5], Is.EqualTo(27));
            Assert.That(left, Is.EqualTo(0));
        }

        /// <summary>BQ-4: free 1000, army needs 150 (weight 60), defence 100 (weight 6): army 150, defence 90 in pass 1 and 10 in pass 2; 750 stays free.</summary>
        [Test]
        public void Bq4_CapAndFreeMoney()
        {
            int[] need = { 0, 0, 0, 150, 0, 100 };
            int[] weight = { 0, 0, 0, 60, 0, 6 };
            int[] grant = new int[6];
            long left = AiBudget.Distribute(1000, need, weight, grant);
            Assert.That(grant[3], Is.EqualTo(150));
            Assert.That(grant[5], Is.EqualTo(100));
            Assert.That(left, Is.EqualTo(750));
            // A weight-0 queue is served by the leftover pass only.
            int[] zeroNeed = { 0, 0, 0, 0, 40, 0 };
            int[] zeroGrant = new int[6];
            Assert.That(AiBudget.Distribute(100, zeroNeed, new int[6], zeroGrant), Is.EqualTo(60));
            Assert.That(zeroGrant[4], Is.EqualTo(40));
        }

        /// <summary>BQ-5: after Close a queue without a want holds 0 and one with a want holds at most its want; over 1000 seeded random inputs, after Open the accounts sum to at most the bank per resource and no account exceeds its want.</summary>
        [Test]
        public void Bq5_ReleaseAndBounds()
        {
            DeterministicRandom rng = new DeterministicRandom(20261121UL);
            for (int round = 0; round < 1000; round++)
            {
                // Wants, accounts (at most the want), weights and pause flags of this round.
                int[] wants = new int[AiBudget.Cells];
                int[] accounts = new int[AiBudget.Cells];
                int[] weights = new int[AiBudget.Cells];
                bool[] paused = new bool[AiBudget.QueueCount];
                for (int c = 0; c < AiBudget.Cells; c++)
                {
                    wants[c] = rng.NextInt(4) == 0 ? 0 : rng.NextInt(400);
                    accounts[c] = wants[c] == 0 ? 0 : rng.NextInt(wants[c] + 1);
                    weights[c] = rng.NextInt(5) == 0 ? 0 : rng.NextInt(1001);
                }
                for (int q = 0; q < AiBudget.QueueCount; q++)
                {
                    paused[q] = rng.NextInt(6) == 0;
                }
                // The bank may be below the accounts (an unexpected loss).
                int[] bank = { rng.NextInt(2000), rng.NextInt(600) };
                AiBudget.Open(accounts, wants, bank, paused, weights);
                for (int r = 0; r < 2; r++)
                {
                    Assert.That(AiBudget.Sum(accounts, r), Is.LessThanOrEqualTo(bank[r]), "round " + round);
                }
                for (int c = 0; c < AiBudget.Cells; c++)
                {
                    Assert.That(accounts[c], Is.LessThanOrEqualTo(wants[c]), "round " + round + " cell " + c);
                }
                // This think's wants and which queues have one.
                int[] thisWants = new int[AiBudget.Cells];
                bool[] hasWant = new bool[AiBudget.QueueCount];
                for (int q = 0; q < AiBudget.QueueCount; q++)
                {
                    hasWant[q] = !paused[q] && rng.NextInt(2) == 0;
                    thisWants[AiBudget.Cell(q, 0)] = rng.NextInt(300);
                    thisWants[AiBudget.Cell(q, 1)] = rng.NextInt(100);
                }
                int[] stored = new int[AiBudget.Cells];
                AiBudget.Close(accounts, stored, thisWants, hasWant);
                for (int q = 0; q < AiBudget.QueueCount; q++)
                {
                    for (int r = 0; r < 2; r++)
                    {
                        int cell = AiBudget.Cell(q, r);
                        Assert.That(accounts[cell], hasWant[q] ? Is.LessThanOrEqualTo(thisWants[cell]) : Is.EqualTo(0), "round " + round + " cell " + cell);
                        Assert.That(stored[cell], Is.EqualTo(hasWant[q] ? thisWants[cell] : 0));
                    }
                }
            }
        }

        /// <summary>BQ-9: ceiling 0.12: spent 1000 of which defence 100, a 100 tower is refused (200 x 10000 above 1200 x 1100); spent 2000 of which defence 100 allows it; no ceiling (0) allows anything.</summary>
        [Test]
        public void Bq9_DefenceCeiling()
        {
            Assert.That(AiBudget.DefenceAllowed(100, 1000, 100, 1200), Is.False);
            Assert.That(AiBudget.DefenceAllowed(100, 2000, 100, 1200), Is.True);
            Assert.That(AiBudget.DefenceAllowed(100000, 1000, 100, 0), Is.True);
            // The data value 0.12 is 1200 parts per 10 000 in the profile.
            Assert.That((AiProfile.FromDataSet(Data, "aggressive_s5_b").BudgetDefenceMaxSpendShare * Fix64.FromInt(10000)).RoundToInt(), Is.EqualTo(1200));
        }

        /// <summary>BQ-12: the production weights are times 12 before the opening's handover and times 1 after; the defence weights times 2 only while a threat is inside the base.</summary>
        [Test]
        public void Bq12_Factors()
        {
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s5_b");
            int[] opening = AiBudget.EffectiveWeights(profile.BudgetCommonWeights, profile.BudgetRareWeights, profile.BudgetOpeningProductionFactor, profile.BudgetThreatDefenceFactor, true, false);
            int[] later = AiBudget.EffectiveWeights(profile.BudgetCommonWeights, profile.BudgetRareWeights, profile.BudgetOpeningProductionFactor, profile.BudgetThreatDefenceFactor, false, false);
            int[] threat = AiBudget.EffectiveWeights(profile.BudgetCommonWeights, profile.BudgetRareWeights, profile.BudgetOpeningProductionFactor, profile.BudgetThreatDefenceFactor, false, true);
            Assert.That(opening[AiBudget.Cell(AiBudget.Production, 0)], Is.EqualTo(120));
            Assert.That(opening[AiBudget.Cell(AiBudget.Production, 1)], Is.EqualTo(360));
            Assert.That(later[AiBudget.Cell(AiBudget.Production, 0)], Is.EqualTo(10));
            Assert.That(later[AiBudget.Cell(AiBudget.Defence, 0)], Is.EqualTo(6));
            Assert.That(threat[AiBudget.Cell(AiBudget.Defence, 0)], Is.EqualTo(12));
            Assert.That(opening[AiBudget.Cell(AiBudget.Defence, 0)], Is.EqualTo(6));
            Assert.That(threat[AiBudget.Cell(AiBudget.Army, 0)], Is.EqualTo(60));
        }

        // ------------------------------------------------------------------ BQ-6 to BQ-8, BQ-10, BQ-11 (the rules on a real view)

        /// <summary>
        /// BQ-6: the army's head is an ink lobber (150 common, 40 rare) that the army account cannot pay, and a grasper (100) is affordable from the account: no grasper is ordered that think and the lobber is the want.
        /// Three graspers stand, so the lobber has the largest deficit (ties to the lowest ID).
        /// </summary>
        [Test]
        public void Bq6_HeadOrder()
        {
            Simulation sim = Base(P1, 6, 1, 0, new[] { "water.tidepool", "water.shell_forge" });
            for (int i = 0; i < 3; i++)
            {
                Spawn(sim, "water.grasper", P1, sim.Map.Spawns[1] + new FixVec2(Fix64.FromInt(-6 - i), Fix64.FromInt(-6)));
            }
            SetStock(sim, P1, 100, 40);
            AiMemory memory = new AiMemory();
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)] = 100;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 0)] = 150;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 1)] = 40;
            List<string> trains = Trains(Decide(sim, P1, "aggressive_s5_b", memory));
            Assert.That(trains, Does.Not.Contain("water.grasper"), "the head waits, the cheaper unit does not jump ahead");
            Assert.That(trains, Does.Not.Contain("water.ink_lobber"));
            Assert.That(memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 0)], Is.EqualTo(150));
            Assert.That(memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 1)], Is.EqualTo(40));
            Assert.That(memory.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)], Is.EqualTo(100), "the account keeps what it holds for the head");
        }

        /// <summary>BQ-7: a defence want of 100 that nothing pays does not stop a worker paid from the economy account; with the switch off the same view reserves under rule R and the worker still trains.</summary>
        [Test]
        public void Bq7_QueuesDoNotBlockEachOther()
        {
            // The two profiles on the same kind of view: bank 100, a worker (50) wanted.
            foreach (string profile in new[] { "aggressive_s5_b", "abl_s5b_no_b" })
            {
                Simulation sim = Base(P0, 3, 1, 0, new[] { "skai.roost" });
                SetStock(sim, P0, 100, 0);
                AiMemory memory = new AiMemory();
                memory.BudgetWants[AiBudget.Cell(AiBudget.Economy, 0)] = 50;
                memory.BudgetWants[AiBudget.Cell(AiBudget.Defence, 0)] = 100;
                List<string> trains = Trains(Decide(sim, P0, profile, memory));
                Assert.That(trains, Does.Contain("skai.fledgling"), profile);
            }
        }

        /// <summary>BQ-8: every worker idle: all accounts and wants are 0 after the think and the intents equal those of the switch-off profile for that think.</summary>
        [Test]
        public void Bq8_NoIncome()
        {
            Simulation sim = Base(P0, 3, 0, 0, new[] { "skai.roost" });
            SetStock(sim, P0, 400, 0);
            AiMemory memory = new AiMemory();
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Defence, 0)] = 20;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Defence, 0)] = 100;
            List<AiIntent> on = Decide(sim, P0, "aggressive_s5_b", memory);
            List<AiIntent> off = Decide(sim, P0, "abl_s5b_no_b", new AiMemory());
            Assert.That(Array.TrueForAll(memory.BudgetAccounts, v => v == 0), Is.True);
            Assert.That(Array.TrueForAll(memory.BudgetWants, v => v == 0), Is.True);
            Assert.That(on.Count, Is.GreaterThan(0), "the bank is spent without income");
            Assert.That(on.ConvertAll(Text), Is.EqualTo(off.ConvertAll(Text)));
        }

        /// <summary>BQ-10: three earning workers with the floor at 4: army and defence buy nothing and hold 0; a worker is bought from the economy queue.</summary>
        [Test]
        public void Bq10_WorkerFloor()
        {
            Simulation sim = Base(P0, 3, 3, 0, new[] { "skai.roost" });
            SetStock(sim, P0, 1000, 0);
            AiMemory memory = new AiMemory();
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory.BudgetWants[AiBudget.Cell(AiBudget.Economy, 0)] = 50;
            List<string> trains = Trains(Decide(sim, P0, "aggressive_s5_b", memory));
            Assert.That(trains, Is.EqualTo(new List<string> { "skai.fledgling" }), "only the worker is bought");
            Assert.That(memory.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)], Is.EqualTo(0));
            Assert.That(memory.BudgetAccounts[AiBudget.Cell(AiBudget.Defence, 0)], Is.EqualTo(0));
            // The same view with the floor at 0 (never) buys the army unit as well.
            Simulation sim2 = Base(P0, 3, 3, 0, new[] { "skai.roost" });
            SetStock(sim2, P0, 1000, 0);
            AiMemory memory2 = new AiMemory();
            memory2.BudgetAccounts[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory2.BudgetWants[AiBudget.Cell(AiBudget.Army, 0)] = 75;
            memory2.BudgetWants[AiBudget.Cell(AiBudget.Economy, 0)] = 50;
            AiProfile noFloor = Edited("\"budget_worker_floor\": 4", "\"budget_worker_floor\": 0");
            List<string> free = Trains(AiBrain.Decide(AiWorldView.Build(sim2, P0), noFloor, noFloor.RequireFaction("skai"), memory2, new DeterministicRandom(1)));
            Assert.That(free, Does.Contain("skai.windrunner"));
        }

        /// <summary>BQ-11: free rare 100 sets the surplus flag, 90 keeps it, 79 clears it (a think with income); while set, the rare-cost unit gets the larger deficit where the plain weights pick a grasper.</summary>
        [Test]
        public void Bq11_RareSurplus()
        {
            // One think with a rare stock; returns the flag afterwards.
            AiMemory memory = new AiMemory();
            Func<int, bool> think = rare =>
            {
                Simulation sim = Base(P1, 4, 1, 0, new[] { "water.tidepool" });
                SetStock(sim, P1, 300, rare);
                Decide(sim, P1, "aggressive_s5_b", memory);
                return memory.RareSurplusActive;
            };
            Assert.That(think(100), Is.True);
            Assert.That(think(90), Is.True, "hysteresis keeps it between off and on");
            Assert.That(think(79), Is.False);
            Assert.That(think(90), Is.False, "below on, it stays clear");
            // The effect on rule P's pick with constructed counts: a grasper (cost rare 0) against an ink lobber (cost rare 40).
            AiFactionPlan plan = AiProfile.FromDataSet(Data, "aggressive_s5_b").RequireFaction("water");
            SortedDictionary<string, int> weights = new SortedDictionary<string, int>(StringComparer.Ordinal) { ["water.grasper"] = 30, ["water.ink_lobber"] = 10 };
            Dictionary<string, int> counts = new Dictionary<string, int> { ["water.grasper"] = 1, ["water.ink_lobber"] = 0 };
            Assert.That(plan.Composition["water.ink_lobber"], Is.GreaterThan(0));
            Assert.That(AiBrain.PickByDeficit(weights, counts, id => true), Is.EqualTo("water.grasper"), "plain: 30 x 2 - 1 x 40 = 20 against 10 x 2 = 20, ties go to the lower ID");
            SortedDictionary<string, int> boosted = AiBudget.BoostRare(weights, id => Data.Content.Units[id].CostRare > 0, 3);
            Assert.That(boosted["water.ink_lobber"], Is.EqualTo(30));
            Assert.That(boosted["water.grasper"], Is.EqualTo(30));
            Assert.That(AiBrain.PickByDeficit(boosted, counts, id => true), Is.EqualTo("water.ink_lobber"), "boosted: the lobber has the larger deficit");
        }

        // ------------------------------------------------------------------ BQ-13

        /// <summary>
        /// BQ-13: aggressive_s5_b in both slots, 600 s, prototype_1v1, both mirrors, fixed seed: no exception; after every think the accounts sum to at most the stock; each side trains at least one rare-cost unit;
        /// two runs give the same state hash.
        /// </summary>
        [Test]
        public void Bq13_RealGame()
        {
            foreach (string matchup in new[] { "skai_vs_skai", "water_vs_water" })
            {
                // Final state hashes of the two runs.
                string first = PlayChecked(matchup, true);
                string second = PlayChecked(matchup, false);
                Assert.That(second, Is.EqualTo(first), matchup + " state hash");
            }
        }

        /// <summary>Plays one 600 s mirror game of aggressive_s5_b and returns the final state hash as text; with <paramref name="check"/> it asserts the per-think and rare-unit conditions.</summary>
        private static string PlayChecked(string matchup, bool check)
        {
            MapDefinition map = Data.RequireMap("prototype_1v1");
            // The faction both slots play.
            string faction = matchup.StartsWith("skai", StringComparison.Ordinal) ? "skai" : "water";
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction, faction + " AI"),
                new PlayerSetup(P1, new TeamId(1), faction, faction + " AI"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(SeedDerivation.GameSeed(20261121UL, "prototype_1v1", matchup, 0)), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, map, Data.Cycles.Values));
            sim.Start();
            foreach (Command command in StandardMatchStart.Commands(Data, map, players))
            {
                sim.Submit(command);
            }
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s5_b");
            List<IPlayerController> controllers = new List<IPlayerController> { new SkirmishAiController(P0, profile), new SkirmishAiController(P1, profile) };
            AiHost.StartAll(sim, controllers);
            // Rare-cost units trained per slot.
            int[] rareUnits = new int[2];
            // Tick limit of 600 s. Unit: ticks.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(600));
            for (int i = 0; i < maxTicks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                if (check)
                {
                    foreach (IPlayerController controller in controllers)
                    {
                        SkirmishAiController ai = (SkirmishAiController)controller;
                        Assert.That(sim.Players.TryGet(ai.Player, out Player? player), Is.True);
                        Assert.That(AiBudget.Sum(ai.Memory.BudgetAccounts, 0), Is.LessThanOrEqualTo(player!.Common), "common accounts at tick " + sim.Clock.Tick);
                        Assert.That(AiBudget.Sum(ai.Memory.BudgetAccounts, 1), Is.LessThanOrEqualTo(player.Rare), "rare accounts at tick " + sim.Clock.Tick);
                    }
                }
                sim.Step();
                if (check)
                {
                    foreach (SimEvent simEvent in sim.Events.Current)
                    {
                        if (simEvent is UnitTrainedEvent trained && sim.Units.TryGet(trained.Unit, out Unit? unit) && unit.Definition.CostRare > 0)
                        {
                            rareUnits[unit.Owner.Value]++;
                        }
                    }
                }
                if (MatchOutcome.Evaluate(sim.Players).IsDecided)
                {
                    break;
                }
            }
            if (check)
            {
                Assert.That(rareUnits[0], Is.GreaterThan(0), matchup + " slot 0 trained a rare-cost unit");
                Assert.That(rareUnits[1], Is.GreaterThan(0), matchup + " slot 1 trained a rare-cost unit");
            }
            return sim.ComputeStateHash().ToString("x16");
        }
    }
}
