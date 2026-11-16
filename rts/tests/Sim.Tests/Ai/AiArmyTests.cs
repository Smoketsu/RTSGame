// rts/tests/Sim.Tests/Ai/AiArmyTests.cs
// Tests for package C of rts/docs/specs/skirmish_ai_v2.md (the AI army and production): the production loop buys the unit the mix
// needs most (C-1 to C-4), home rally and the attack group (C-5 to C-7), the damped Defend/Massing change (C-8), finishing a low
// enemy HQ (C-9), rebuilding after a lost attack (C-10), the extra production building (C-11). Part of Rts.Sim.Tests. Each test
// builds a real greybox_1v1 simulation, spawns units with script commands and runs AiBrain.Decide on the resulting view.
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Skirmish AI v2 package C: production mix, home rally, attack group, Defend hold, kill mode, extra producer.</summary>
    [TestFixture]
    public class AiArmyTests
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

        /// <summary>The shipped aggressive profile.</summary>
        private static AiProfile Aggressive => AiProfile.FromDataSet(Data, "aggressive");

        /// <summary>A started greybox_1v1 simulation (Skai slot 0, Water slot 1, cycles off, nothing spawned).</summary>
        private static Simulation Greybox()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            return sim;
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

        /// <summary>Sets the stock of a player (test-only access to the internal setter).</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>Skai base: HQ at (7, 7), six workers (so no worker is wanted), three Roosts and a Windspire, then the army; returns the producer IDs (roosts first).</summary>
        private List<EntityId> SkaiBase(Simulation sim, int windrunners, int archers)
        {
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                Spawn(sim, "skai.fledgling", P0, "6", (8 + i / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            List<EntityId> producers = new List<EntityId>
            {
                Spawn(sim, "skai.roost", P0, "13.5", "6.5"),
                Spawn(sim, "skai.roost", P0, "13.5", "10.5"),
                Spawn(sim, "skai.roost", P0, "13.5", "14.5"),
                Spawn(sim, "skai.windspire", P0, "6.5", "14.5"),
            };
            // Army unit index. Range: 0 to count - 1.
            for (int i = 0; i < windrunners; i++)
            {
                Spawn(sim, "skai.windrunner", P0, "10", "10");
            }
            for (int i = 0; i < archers; i++)
            {
                Spawn(sim, "skai.talon_archer", P0, "10", "11");
            }
            return producers;
        }

        /// <summary>The army train intents among <paramref name="intents"/> as "producer:definition" (worker orders left out).</summary>
        private static List<string> Trains(List<AiIntent> intents)
        {
            List<string> trains = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Train && intent.DefinitionId != "skai.fledgling" && intent.DefinitionId != "water.tender")
                {
                    trains.Add(intent.Unit.Value + ":" + intent.DefinitionId);
                }
            }
            return trains;
        }

        /// <summary>Decides for P0 with the given (or a fresh) memory and generator.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory? memory = null, DeterministicRandom? rng = null)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory(), rng ?? new DeterministicRandom(1));
        }

        /// <summary>The shipped aggressive profile with text edits applied (each old text must occur once).</summary>
        private static AiProfile Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive.json"));
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive.json", Data.Content);
        }

        /// <summary>The aggressive profile with no attack-threshold jitter (the threshold is exactly 8) and the given extra edits.</summary>
        private static AiProfile NoJitter(params string[] extra)
        {
            List<string> pairs = new List<string> { "\"attack_supply_jitter\": 2", "\"attack_supply_jitter\": 0" };
            pairs.AddRange(extra);
            return Edited(pairs.ToArray());
        }

        /// <summary>The goals of the AttackMove intents among <paramref name="intents"/>, in order.</summary>
        private static List<FixVec2> AttackMoveGoals(List<AiIntent> intents)
        {
            List<FixVec2> goals = new List<FixVec2>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.AttackMove)
                {
                    goals.Add(intent.Point);
                }
            }
            return goals;
        }

        /// <summary>Queues one army unit at every listed producer (Windrunner at a Roost, Talon Archer at a Windspire) so they are busy.</summary>
        private void MakeBusy(Simulation sim, List<EntityId> producers)
        {
            SetStock(sim, P0, 5000, 0);
            foreach (EntityId producer in producers)
            {
                Assert.That(sim.Units.TryGet(producer, out Unit? record), Is.True);
                string unit = record!.Definition.Id == "skai.windspire" ? "skai.talon_archer" : "skai.windrunner";
                sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, _sequence++, producer, unit));
            }
            sim.Step();
        }

        /// <summary>The ID of P0's lowest-ID worker.</summary>
        private static EntityId FirstWorker(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Economy.CanGather)
                {
                    return unit.Id;
                }
            }
            throw new AssertionException("no worker");
        }

        /// <summary>The home rally point of the greybox Skai HQ at (7, 7): rally_distance_tiles toward the Water spawn.</summary>
        private static FixVec2 HomeRally(Simulation sim, AiProfile profile)
        {
            return FixVec2.FromInts(7, 7).MoveTowards(sim.Map.Spawns[1], profile.RallyDistanceTiles);
        }

        /// <summary>
        /// C-1 (written first, failed on the old loop): Roosts and a Windspire all idle, 6 Windrunners, 0 Archers, bank 175. The Archer
        /// has the largest deficit (7 against -3) and only the Windspire trains it; the old loop let every Roost buy a Windrunner first
        /// and left no money for the Archer.
        /// </summary>
        [Test]
        public void C1_Composition_TrainsTheMostNeededUnitFirst()
        {
            Simulation sim = Greybox();
            List<EntityId> producers = SkaiBase(sim, 6, 0);
            SetStock(sim, P0, 175, 0);
            List<string> trains = Trains(Decide(sim, Aggressive));
            Assert.That(trains, Is.EqualTo(new List<string>
            {
                producers[3].Value + ":skai.talon_archer",
                producers[0].Value + ":skai.windrunner",
            }));
        }

        /// <summary>C-2: the head of the queue is held, not skipped: with 90 nothing is trained (the Archer costs 100); with 100 the Archer is.</summary>
        [Test]
        public void C2_HeadReservation_CheaperUnitsDoNotJumpAhead()
        {
            Simulation sim = Greybox();
            List<EntityId> producers = SkaiBase(sim, 6, 0);
            SetStock(sim, P0, 90, 0);
            Assert.That(Trains(Decide(sim, Aggressive)), Is.Empty);
            SetStock(sim, P0, 100, 0);
            Assert.That(Trains(Decide(sim, Aggressive)), Is.EqualTo(new List<string> { producers[3].Value + ":skai.talon_archer" }));
        }

        /// <summary>C-3: a unit whose rare cost the stock cannot pay is skipped (the next unit is trained); with the rare in stock it is trained.</summary>
        [Test]
        public void C3_RareCandidate_IsSkippedUntilRareIsInStock()
        {
            // Roc Rider (150 common, 50 rare) has the largest deficit: Windrunner weight 3 against Roc Rider 1 with 3 Windrunners.
            AiProfile profile = Edited("\"skai.windrunner\": 3, \"skai.talon_archer\": 1", "\"skai.windrunner\": 3, \"skai.roc_rider\": 1");
            Simulation sim = Greybox();
            List<EntityId> producers = SkaiBase(sim, 3, 0);
            SetStock(sim, P0, 300, 0);
            Assert.That(Trains(Decide(sim, profile)), Is.EqualTo(new List<string>
            {
                producers[0].Value + ":skai.windrunner",
                producers[1].Value + ":skai.windrunner",
                producers[2].Value + ":skai.windrunner",
            }), "no rare in stock: Roc Rider skipped, Windrunners trained");
            SetStock(sim, P0, 300, 50);
            Assert.That(Trains(Decide(sim, profile))[0], Is.EqualTo(producers[0].Value + ":skai.roc_rider"));
        }

        /// <summary>C-4: Water with 3 Tidepools and a Shell Forge, 6 Graspers, 0 Shellbacks, bank 200: one Shellback at the Forge.</summary>
        [Test]
        public void C4_WaterMix_TrainsAShellbackAtTheForge()
        {
            Simulation sim = Greybox();
            Spawn(sim, "water.grand_shell", P1, "57", "57");
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                Spawn(sim, "water.tender", P1, "58", (50 + i / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            Spawn(sim, "water.tidepool", P1, "50.5", "57.5");
            Spawn(sim, "water.tidepool", P1, "50.5", "53.5");
            Spawn(sim, "water.tidepool", P1, "50.5", "49.5");
            EntityId forge = Spawn(sim, "water.shell_forge", P1, "57.5", "49.5");
            // Grasper index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                Spawn(sim, "water.grasper", P1, "54", "54");
            }
            SetStock(sim, P1, 200, 0);
            AiProfile profile = Aggressive;
            List<AiIntent> intents = AiBrain.Decide(AiWorldView.Build(sim, P1), profile, profile.RequireFaction("water"), new AiMemory(), new DeterministicRandom(1));
            List<string> trains = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Train && intent.DefinitionId != "water.tender")
                {
                    trains.Add(intent.Unit.Value + ":" + intent.DefinitionId);
                }
            }
            Assert.That(trains, Is.EqualTo(new List<string> { forge.Value + ":water.shellback" }));
        }

        /// <summary>C-5: with rally_at_core and the Core not held, an idle massing army unit goes to the home rally point (rally_distance_tiles from the HQ toward the enemy), not the Core centre.</summary>
        [Test]
        public void C5_HomeRally_IsNotTheCore()
        {
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            Spawn(sim, "skai.windrunner", P0, "20", "20");
            AiProfile profile = Aggressive;
            Assert.That(profile.RallyAtCore, Is.True);
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.HasCore && !view.OwnTeamHoldsCore, Is.True);
            List<FixVec2> goals = AttackMoveGoals(AiBrain.Decide(view, profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1)));
            Assert.That(goals.Count, Is.EqualTo(1));
            Assert.That((goals[0] - HomeRally(sim, profile)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)));
            Assert.That((goals[0] - view.CoreCentre).LengthSquared(), Is.GreaterThan(Fix64.FromInt(100)));
        }

        /// <summary>C-6: the attack starts with every army unit in the group; a unit trained afterwards is not in it, rallies at home and the group is unchanged.</summary>
        [Test]
        public void C6_AttackGroup_IgnoresLaterUnits()
        {
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            List<EntityId> first = new List<EntityId>();
            // Unit index. Range: 0 to 7.
            for (int i = 0; i < 8; i++)
            {
                first.Add(Spawn(sim, "skai.windrunner", P0, "10", "10"));
            }
            AiProfile profile = NoJitter("\"stage_distance_tiles\": 14", "\"stage_distance_tiles\": 0");
            AiMemory memory = new AiMemory();
            DeterministicRandom rng = new DeterministicRandom(1);
            Decide(sim, profile, memory, rng);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(memory.AttackGroup, Is.EqualTo(first));
            Assert.That(memory.AttackStartSupply, Is.EqualTo(8));
            // A ninth Windrunner, far from the home rally point.
            EntityId late = Spawn(sim, "skai.windrunner", P0, "7", "14");
            List<AiIntent> intents = Decide(sim, profile, memory, rng);
            Assert.That(memory.AttackGroup, Is.EqualTo(first));
            // Whether the late unit received a rally order.
            bool found = false;
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.AttackMove && intent.Unit == late)
                {
                    found = true;
                    Assert.That((intent.Point - HomeRally(sim, profile)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)), "the late unit rallies at home");
                }
            }
            Assert.That(found, Is.True);
        }

        /// <summary>C-7: with rally_at_core and the Core not held the attack group stages at the Core centre; once the team holds the Core it stages stage_distance_tiles short of the target.</summary>
        [Test]
        public void C7_Staging_GoesToTheCoreOnlyWhileItIsNotHeld()
        {
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            // Unit index. Range: 0 to 7.
            for (int i = 0; i < 8; i++)
            {
                Spawn(sim, "skai.windrunner", P0, "10", "10");
            }
            AiProfile profile = NoJitter();
            AiWorldView view = AiWorldView.Build(sim, P0);
            List<FixVec2> toCore = AttackMoveGoals(AiBrain.Decide(view, profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1)));
            Assert.That(toCore.Count, Is.EqualTo(8));
            foreach (FixVec2 goal in toCore)
            {
                Assert.That((goal - view.CoreCentre).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)));
            }
            view.CoreHolder = view.Team;
            List<FixVec2> held = AttackMoveGoals(AiBrain.Decide(view, profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1)));
            // Staging point 14 tiles short of the Water spawn toward the HQ.
            FixVec2 stage = sim.Map.Spawns[1].MoveTowards(FixVec2.FromInts(7, 7), profile.StageDistanceTiles);
            Assert.That(held.Count, Is.EqualTo(8));
            foreach (FixVec2 goal in held)
            {
                Assert.That((goal - stage).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)));
            }
        }

        /// <summary>C-8: after a threat disappears the stance stays Defend and orders nothing for defend_hold_seconds; then it returns to Massing with the same threshold and no new draw.</summary>
        [Test]
        public void C8_DefendHold_DampsTheFlipFlop()
        {
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            Spawn(sim, "skai.windrunner", P0, "5", "5");
            Spawn(sim, "skai.windrunner", P0, "5", "6");
            EntityId enemy = Spawn(sim, "water.grasper", P1, "17.5", "7.5");
            AiProfile profile = NoJitter();
            AiMemory memory = new AiMemory();
            DeterministicRandom rng = new DeterministicRandom(1);
            Decide(sim, profile, memory, rng);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Defend));
            // Tick of the last threat. Unit: ticks.
            int threatTick = memory.LastThreatTick;
            Assert.That(threatTick, Is.EqualTo(sim.Clock.Tick));
            // Draws and threshold after the first think.
            ulong draws = rng.DrawCount;
            int threshold = memory.AttackThreshold;
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, enemy));
            sim.Step();
            sim.Step();
            List<AiIntent> held = Decide(sim, profile, memory, rng);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Defend), "inside the 5 s hold");
            foreach (AiIntent intent in held)
            {
                Assert.That(intent.Kind, Is.Not.EqualTo(AiIntentKind.AttackMove).And.Not.EqualTo(AiIntentKind.AttackUnit).And.Not.EqualTo(AiIntentKind.Move));
            }
            while (sim.Clock.Tick < threatTick + 100)
            {
                sim.Step();
            }
            Decide(sim, profile, memory, rng);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Massing));
            Assert.That(memory.AttackThreshold, Is.EqualTo(threshold));
            Assert.That(rng.DrawCount, Is.EqualTo(draws), "leaving Defend draws nothing");
        }

        /// <summary>C-9: a known enemy HQ last seen at 30 percent health and 4 army supply (threshold 8) start an attack on that HQ; the group's losses beyond retreat_army_fraction give no retreat. With 3 supply nothing starts.</summary>
        [Test]
        public void C9_KillMode_FinishesALowHq()
        {
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            List<EntityId> units = new List<EntityId>();
            // Unit index. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                units.Add(Spawn(sim, "skai.windrunner", P0, "10", "10"));
            }
            AiProfile profile = NoJitter();
            AiMemory memory = new AiMemory();
            memory.AttackThreshold = 8;
            memory.KnownEnemyStructures.Add(new EntityId(900), new KnownStructure { Position = FixVec2.FromInts(57, 57), IsHq = true, HealthFraction = Fix64.Parse("0.3") });
            DeterministicRandom rng = new DeterministicRandom(1);
            List<FixVec2> goals = AttackMoveGoals(Decide(sim, profile, memory, rng));
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(goals.Count, Is.EqualTo(4));
            foreach (FixVec2 goal in goals)
            {
                Assert.That((goal - FixVec2.FromInts(57, 57)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)), "straight to the HQ, no staging");
            }
            // Three of the four units die and the recorded start supply is 40: without rule K the loss rule would order a retreat.
            for (int i = 0; i < 3; i++)
            {
                sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, units[i]));
            }
            sim.Step();
            memory.AttackStartSupply = 40;
            Decide(sim, profile, memory, rng);
            Assert.That(memory.Retreats, Is.EqualTo(0), "1 of 40 start supply would retreat without rule K");
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            // With only 3 supply the rule does not start and the normal threshold of 8 is not met.
            Simulation small = Greybox();
            Spawn(small, "skai.great_aerie", P0, "7", "7");
            // Unit index. Range: 0 to 2.
            for (int i = 0; i < 3; i++)
            {
                Spawn(small, "skai.windrunner", P0, "10", "10");
            }
            AiMemory quiet = new AiMemory();
            quiet.AttackThreshold = 8;
            quiet.KnownEnemyStructures.Add(new EntityId(900), new KnownStructure { Position = FixVec2.FromInts(57, 57), IsHq = true, HealthFraction = Fix64.Parse("0.3") });
            Decide(small, profile, quiet, new DeterministicRandom(1));
            Assert.That(quiet.Stance, Is.EqualTo(AiStance.Massing));
        }

        /// <summary>C-10: when every group unit is dead the stance returns to Massing with exactly one new draw, the think still trains for an idle producer, and a unit trained next goes to the home rally point.</summary>
        [Test]
        public void C10_Rebuild_AfterTheGroupIsLost()
        {
            Simulation sim = Greybox();
            List<EntityId> producers = SkaiBase(sim, 0, 0);
            SetStock(sim, P0, 100, 0);
            AiProfile profile = NoJitter();
            AiMemory memory = new AiMemory();
            memory.AttackThreshold = 8;
            memory.Stance = AiStance.Attack;
            memory.AttackStaged = true;
            memory.AttackGroup.Add(new EntityId(9999));
            DeterministicRandom rng = new DeterministicRandom(1);
            ulong before = rng.DrawCount;
            List<AiIntent> intents = Decide(sim, profile, memory, rng);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Massing));
            Assert.That(rng.DrawCount, Is.EqualTo(before + 1));
            Assert.That(memory.AttackGroup, Is.Empty);
            Assert.That(Trains(intents), Is.EqualTo(new List<string> { producers[0].Value + ":skai.windrunner" }));
            // A unit trained now goes to the home rally point on the next think.
            EntityId fresh = Spawn(sim, "skai.windrunner", P0, "7", "14");
            // Whether the fresh unit received a rally order.
            bool found = false;
            foreach (AiIntent intent in Decide(sim, profile, memory, rng))
            {
                if (intent.Kind == AiIntentKind.AttackMove && intent.Unit == fresh)
                {
                    found = true;
                    Assert.That((intent.Point - HomeRally(sim, profile)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(2)));
                }
            }
            Assert.That(found, Is.True);
        }

        /// <summary>C-11: bank 450 with every producer busy builds the producer of the top-deficit unit; not with a producer under construction, nor at max_producers.</summary>
        [Test]
        public void C11_ExtraProducer_OnlyWhenEveryProducerIsBusy()
        {
            AiProfile profile = Aggressive;
            // The build intents of one think as definition IDs.
            List<string> Builds(Simulation s)
            {
                List<string> builds = new List<string>();
                foreach (AiIntent intent in Decide(s, profile))
                {
                    if (intent.Kind == AiIntentKind.Build)
                    {
                        builds.Add(intent.DefinitionId);
                    }
                }
                return builds;
            }
            Simulation sim = Greybox();
            List<EntityId> producers = SkaiBase(sim, 0, 0);
            MakeBusy(sim, producers);
            SetStock(sim, P0, 450, 0);
            // Three Windrunners and one Archer queued give N = 4: the Windrunner (3 x 5 - 3 x 4 = 3) leads, so a Roost.
            Assert.That(Builds(sim), Is.EqualTo(new List<string> { "skai.roost" }));
            // One producer under construction: nothing.
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, FirstWorker(sim), "skai.roost", 20, 3));
            sim.Step();
            SetStock(sim, P0, 450, 0);
            Assert.That(Builds(sim), Is.Empty, "a producer is under construction");
            // Eight producers: nothing.
            Simulation full = Greybox();
            List<EntityId> eight = SkaiBase(full, 0, 0);
            // Extra Roost index. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                eight.Add(Spawn(full, "skai.roost", P0, (20.5m + 4 * i).ToString(System.Globalization.CultureInfo.InvariantCulture), "3.5"));
            }
            MakeBusy(full, eight);
            SetStock(full, P0, 450, 0);
            Assert.That(Builds(full), Is.Empty, "max_producers 8 reached");
        }
    }
}
