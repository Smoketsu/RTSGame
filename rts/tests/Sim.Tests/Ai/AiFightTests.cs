// rts/tests/Sim.Tests/Ai/AiFightTests.cs
// Tests D-1 to D-12 of rts/docs/specs/skirmish_ai_v3.md package D (the fight choices: attack and retreat by prediction, target choice,
// rule HR harassment, measured defence, unit control), plus the profile switches and the spatial index. Part of Rts.Sim.Tests. Each test
// builds a real greybox_1v1 simulation, spawns units with script commands (health set through the internal setter), writes what the AI
// has "seen" into the AiMemory where a sighting would be needed, and runs AiBrain.Decide on the resulting view.
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
    /// <summary>Skirmish AI v3 package D.</summary>
    [TestFixture]
    public class AiFightTests
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

        /// <summary>The shipped package D profile.</summary>
        private static AiProfile S3 => AiProfile.FromDataSet(Data, "aggressive_s3");

        /// <summary>The package D profile with the spec's attack_min_supply 6 (the shipped file retuned it to 14 for the response gate); the D.1 test numbers assume 6.</summary>
        private static AiProfile S3Spec => S3Edited("\"attack_min_supply\": 14,", "\"attack_min_supply\": 6,");

        /// <summary>Whether the shipped aggressive_s3 has use_focus_fire on (decided by the package D review check; see the report).</summary>
        private const bool S3FocusFire = false;

        /// <summary>The package D profile with use_focus_fire on (the shipped file has it off after the package D review check), for the D.6.3 tests.</summary>
        private static AiProfile S3Focus => S3Edited("\"use_focus_fire\": false,", "\"use_focus_fire\": true,");

        /// <summary>The package C2 profile (every package D switch off).</summary>
        private static AiProfile S2b => AiProfile.FromDataSet(Data, "aggressive_s2b");

        /// <summary>The shipped aggressive_s3 text with edits applied (each old text must occur once) and parsed.</summary>
        private static AiProfile S3Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s3.json")).Replace("\r\n", "\n");
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s3.json", Data.Content);
        }

        /// <summary>A started greybox_1v1 simulation with the given factions in slots 0 and 1.</summary>
        private static Simulation Greybox(string faction0 = "skai", string faction1 = "water", string map = "greybox_1v1")
        {
            // Value of this case.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
            };
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(map)));
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

        /// <summary>Sets a unit's health to <paramref name="fraction"/> of its maximum (test-only access to the internal setter).</summary>
        private static void Hurt(Simulation sim, EntityId id, string fraction)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
            unit!.SetHealth(unit.Definition.MaxHealth * Fix64.Parse(fraction));
        }

        /// <summary>Spawns <paramref name="count"/> units in a row at the given tile row, starting at column <paramref name="x"/>.</summary>
        private List<EntityId> Row(Simulation sim, string definition, PlayerId owner, int count, int x, int y)
        {
            // The units spawned.
            List<EntityId> ids = new List<EntityId>();
            // Unit number. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                ids.Add(Spawn(sim, definition, owner, (x + i % 8).ToString(System.Globalization.CultureInfo.InvariantCulture), (y + i / 8).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            return ids;
        }

        /// <summary>A Skai HQ for P0 at (7, 7).</summary>
        private void SkaiHq(Simulation sim)
        {
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
        }

        /// <summary>A Water HQ for P0 at (7, 7).</summary>
        private void WaterHq(Simulation sim)
        {
            Spawn(sim, "water.grand_shell", P0, "7", "7");
        }

        /// <summary>Writes <paramref name="count"/> units of an enemy type into the model as just seen.</summary>
        private static void Believe(AiMemory memory, string type, int count, int tick)
        {
            memory.EnemyTypes[type] = new EnemyTypeEstimate { Count = Fix64.FromInt(count), LastSeenTick = tick };
        }

        /// <summary>Decides for P0 with the given memory (an AttackThreshold of 8 is set so the first think does not draw) and generator.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory, DeterministicRandom? rng = null)
        {
            if (memory.AttackThreshold < 0)
            {
                memory.AttackThreshold = 8;
            }
            // The faction's plan.
            AiFactionPlan plan = profile.RequireFaction(sim.Players.TryGet(P0, out Player? self) ? self.FactionId : "skai");
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, plan, memory, rng ?? new DeterministicRandom(1));
        }

        /// <summary>The intents of one kind.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            return intents.FindAll(i => i.Kind == kind);
        }

        /// <summary>The predictor settings of a profile.</summary>
        private static PredictorSettings Settings(AiProfile profile)
        {
            return PredictorSettings.From(profile);
        }

        /// <summary>The predicted advantage of a group of own units against enemy units, from entries as the brain builds them.</summary>
        private static Fix64 Advantage(AiProfile profile, List<FightEntry> own, List<FightEntry> enemy, bool uphill = false)
        {
            return FightPredictor.Predict(own, enemy, uphill, Data.Content.Combat, Settings(profile)).Advantage;
        }

        /// <summary>One predictor entry at full health.</summary>
        private static FightEntry Full(string id, int count)
        {
            // The definition of the unit type being built.
            UnitDefinition definition = Data.Content.Units[id];
            return new FightEntry(definition, Fix64.FromInt(count), definition.MaxHealth * count);
        }

        /// <summary>Asserts a Fix64 is within 0.001 of a decimal text.</summary>
        private static void Near(Fix64 actual, string expected)
        {
            Assert.That((actual - Fix64.Parse(expected)).Abs() <= Fix64.Parse("0.001"), Is.True, actual + " vs " + expected);
        }

        /// <summary>Puts a known enemy structure in the memory.</summary>
        private static void Know(AiMemory memory, int id, string definition, string x, string y, int tick)
        {
            memory.KnownEnemyStructures[new EntityId((uint)id)] = new KnownStructure
            {
                DefinitionId = definition,
                Position = new FixVec2(Fix64.Parse(x), Fix64.Parse(y)),
                IsHq = definition == "water.grand_shell" || definition == "skai.great_aerie",
                LastSeenTick = tick,
            };
        }

        // ------------------------------------------------------------------ D.1 to D.3

        /// <summary>D-1: 4 Windrunners against a model of 2 Pummelers do not attack (advantage 1.1392 and too few supply); 6 do (advantage above 1.3).</summary>
        [Test]
        public void D1_AttackByPrediction()
        {
            Near(Advantage(S3, new List<FightEntry> { Full("skai.windrunner", 4) }, new List<FightEntry> { Full("water.grasper", 2) }), "1.1392");
            Assert.That(Advantage(S3, new List<FightEntry> { Full("skai.windrunner", 6) }, new List<FightEntry> { Full("water.grasper", 2) }), Is.GreaterThan(Fix64.Parse("1.3")));
            foreach ((int count, AiStance expected) in new[] { (4, AiStance.Massing), (6, AiStance.Attack) })
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox();
                SkaiHq(sim);
                Row(sim, "skai.windrunner", P0, count, 10, 10);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory();
                Believe(memory, "water.grasper", 2, sim.Clock.Tick);
                Decide(sim, S3Spec, memory);
                Assert.That(memory.Stance, Is.EqualTo(expected), count + " Windrunners");
            }
            // The shipped (retuned) profile wants 14 supply before it attacks on a predicted win: 6 Windrunners stay home.
            Simulation shipped = Greybox();
            SkaiHq(shipped);
            Row(shipped, "skai.windrunner", P0, 6, 10, 10);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory shippedMemory = new AiMemory();
            Believe(shippedMemory, "water.grasper", 2, shipped.Clock.Tick);
            Decide(shipped, S3, shippedMemory);
            Assert.That(shippedMemory.Stance, Is.EqualTo(AiStance.Massing));
            // The v2 profile on the same 6 units: the drawn threshold of 8 is not reached, so it does not attack.
            Simulation v2 = Greybox();
            SkaiHq(v2);
            Row(v2, "skai.windrunner", P0, 6, 10, 10);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory v2Memory = new AiMemory();
            Believe(v2Memory, "water.grasper", 2, v2.Clock.Tick);
            Decide(v2, S2b, v2Memory);
            Assert.That(v2Memory.Stance, Is.EqualTo(AiStance.Massing));
        }

        /// <summary>D-1b: the uphill factor reaches the decision: against a known undefended depot on a higher height layer than the rally point the army attacks exactly when the uphill prediction says so (on the first shipped map that has such a tile).</summary>
        [Test]
        public void D1b_UphillTargetUsesTheUphillPrediction()
        {
            // Maps searched for a tile above the rally layer.
            bool tested = false;
            foreach (string map in new[] { "greybox_1v1", "prototype_1v1", "twin_chokes", "long_lowland" })
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox("skai", "water", map);
                // The own HQ at the first spawn and the rally point toward the second, as the brain computes it.
                FixVec2 hq = sim.Map.Spawns[0];
                Spawn(sim, "skai.great_aerie", P0, hq.X.ToString(), hq.Y.ToString());
                // What the AI sees in this case (own units, visible enemies, map).
                AiWorldView probe = AiWorldView.Build(sim, P0);
                FixVec2 rally = hq.MoveTowards(sim.Map.Spawns[1], S3.RallyDistanceTiles);
                byte rallyHeight = probe.HeightAt(rally.X.FloorToInt(), rally.Y.FloorToInt());
                // The first unblocked tile on a higher layer, at least 30 tiles from the HQ, or -1.
                int upX = -1;
                int upY = -1;
                for (int y = 0; y < probe.Height && upX < 0; y++)
                {
                    for (int x = 0; x < probe.Width && upX < 0; x++)
                    {
                        if (!probe.IsBlocked(x, y) && probe.HeightAt(x, y) > rallyHeight && (probe.CentreOf(x, y) - hq).LengthSquared() > Fix64.FromInt(900))
                        {
                            upX = x;
                            upY = y;
                        }
                    }
                }
                if (upX < 0)
                {
                    continue;
                }
                tested = true;
                // Six Windrunners at the HQ, a model of 2 Pummelers and a known enemy depot on the higher tile.
                Row(sim, "skai.windrunner", P0, 6, hq.X.FloorToInt() + 2, hq.Y.FloorToInt() + 2);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory();
                Believe(memory, "water.grasper", 2, sim.Clock.Tick);
                Know(memory, 900, "water.pearl_depot", upX + ".5", upY + ".5", sim.Clock.Tick);
                Decide(sim, S3Spec, memory);
                // The model entries at an undefended target are halved (undefended_army_share 0.5).
                List<FightEntry> half = new List<FightEntry> { new FightEntry(Data.Content.Units["water.grasper"], Fix64.One, Data.Content.Units["water.grasper"].MaxHealth) };
                Fix64 uphill = Advantage(S3, new List<FightEntry> { Full("skai.windrunner", 6) }, half, true);
                Assert.That(memory.Stance == AiStance.Attack, Is.EqualTo(uphill >= S3.AttackMinAdvantage), map + " uphill advantage " + uphill);
                break;
            }
            Assert.That(tested, Is.True, "some shipped 1v1 map has a tile above the rally layer");
        }

        /// <summary>D-2: with nothing known (no model, no static defence) the v2 drawn threshold decides and the random generator is used exactly as in v2.</summary>
        [Test]
        public void D2_NothingKnownFallsBackToTheThreshold()
        {
            foreach ((int count, AiStance expected) in new[] { (7, AiStance.Massing), (8, AiStance.Attack) })
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox();
                SkaiHq(sim);
                Row(sim, "skai.windrunner", P0, count, 10, 10);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory();
                DeterministicRandom rng = new DeterministicRandom(1);
                Decide(sim, S3, memory, rng);
                Assert.That(memory.Stance, Is.EqualTo(expected), count + " Windrunners");
                // The same think by the v2 stage 2 profile draws the same number of random numbers.
                AiMemory other = new AiMemory();
                DeterministicRandom otherRng = new DeterministicRandom(1);
                Decide(sim, S2b, other, otherRng);
                Assert.That(otherRng.DrawCount, Is.EqualTo(rng.DrawCount));
                Assert.That(other.Stance, Is.EqualTo(memory.Stance));
            }
        }

        /// <summary>D-3: 6 Roc Riders against an army of Pummelers (which cannot hit air; advantage 0): attack_force_supply (60) attacks anyway, 58 does not.</summary>
        [Test]
        public void D3_ForceSupplyAttacksAnyway()
        {
            // Supply of one Pummeler. Unit: supply.
            int supply = Data.Content.Units["water.grasper"].Supply;
            foreach ((int total, AiStance expected) in new[] { (60, AiStance.Attack), (58, AiStance.Massing) })
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox("water", "skai");
                WaterHq(sim);
                // Pummelers needed for the supply (rounded up).
                int count = (total + supply - 1) / supply;
                Row(sim, "water.grasper", P0, count, 10, 10);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory { AttackThreshold = 1000 };
                Believe(memory, "skai.roc_rider", 6, sim.Clock.Tick);
                Decide(sim, S3, memory);
                Assert.That(memory.Stance, Is.EqualTo(expected), total + " supply");
            }
        }

        /// <summary>D-4: a group of 3 Pummelers that sees 2 Roc Riders within 10 tiles retreats (advantage 0.0292 below 0.7).</summary>
        [Test]
        public void D4_RetreatByPrediction()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox("water", "skai");
            WaterHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> group = Row(sim, "water.grasper", P0, 3, 30, 30);
            Row(sim, "skai.roc_rider", P1, 2, 34, 30);
            Near(Advantage(S3, new List<FightEntry> { Full("water.grasper", 3) }, new List<FightEntry> { Full("skai.roc_rider", 2) }), "0.0292");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 6, AttackThreshold = 8, AttackStartTick = sim.Clock.Tick - 200 };
            foreach (EntityId id in group)
            {
                memory.AttackGroup.Add(id);
            }
            Decide(sim, S3, memory);
            Assert.That(memory.Retreats, Is.EqualTo(1));
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Massing), "a retreat ends in Massing with a regroup timer");
            Assert.That(memory.RegroupUntilTick, Is.GreaterThan(sim.Clock.Tick));
            // The v2 profile's power test (0.4 x enemy power) does not retreat 3 Pummelers from 2 Roc Riders of the same supply.
            AiMemory old = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 6, AttackThreshold = 8 };
            foreach (EntityId id in group)
            {
                old.AttackGroup.Add(id);
            }
            Decide(sim, S2b, old);
            Assert.That(old.Retreats, Is.EqualTo(0));
        }

        /// <summary>D-5: the target is an undefended enemy depot far from the last seen army; a turret near the depot makes it defended and the target is the HQ.</summary>
        [Test]
        public void D5_Targets()
        {
            foreach (bool turret in new[] { false, true })
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox();
                SkaiHq(sim);
                Row(sim, "skai.windrunner", P0, 8, 10, 10);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory();
                Know(memory, 900, "water.grand_shell", "57.5", "57.5", sim.Clock.Tick);
                Know(memory, 901, "water.pearl_depot", "27.5", "30.5", sim.Clock.Tick);
                if (turret)
                {
                    Know(memory, 902, "water.coral_turret", "32.5", "30.5", sim.Clock.Tick);
                }
                memory.HasLastSeenEnemy = true;
                memory.LastSeenEnemyPosition = new FixVec2(Fix64.Parse("57.5"), Fix64.Parse("30.5"));
                memory.LastSeenEnemyTick = sim.Clock.Tick - 200;
                Decide(sim, S3, memory);
                Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
                // The target the brain chose, compared with the depot and the HQ. Unit: squared tiles.
                Fix64 toDepot = (memory.AttackTarget - new FixVec2(Fix64.Parse("27.5"), Fix64.Parse("30.5"))).LengthSquared();
                Fix64 toHq = (memory.AttackTarget - new FixVec2(Fix64.Parse("57.5"), Fix64.Parse("57.5"))).LengthSquared();
                if (turret)
                {
                    Assert.That(toHq, Is.LessThanOrEqualTo(Fix64.FromInt(4)), "defended depot: the HQ");
                }
                else
                {
                    Assert.That(toDepot, Is.LessThanOrEqualTo(Fix64.FromInt(4)), "undefended depot");
                }
            }
        }

        // ------------------------------------------------------------------ D.4

        /// <summary>D-6: rule HR picks the 4 Windrunners of a 12-supply army, sends them to the known depot farthest from the enemy army, attacks a visible worker in reach, and after 10 s at the target without a worker sends them home and starts the cooldown.</summary>
        [Test]
        public void D6_Harass()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // The squad is the 4 Windrunners; the 4 Talon Archers (8 supply) stay home and are not harassers (speed 2.5).
            List<EntityId> runners = Row(sim, "skai.windrunner", P0, 4, 10, 10);
            Row(sim, "skai.talon_archer", P0, 4, 10, 14);
            Assert.That(Data.Content.Units["skai.windrunner"].Supply * 4 + Data.Content.Units["skai.talon_archer"].Supply * 4, Is.GreaterThanOrEqualTo(S3.HarassMinArmySupply));
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.grand_shell", "57.5", "57.5", sim.Clock.Tick);
            Know(memory, 901, "water.pearl_depot", "12.5", "50.5", sim.Clock.Tick);
            memory.HasLastSeenEnemy = true;
            memory.LastSeenEnemyPosition = new FixVec2(Fix64.Parse("55.5"), Fix64.Parse("55.5"));
            memory.LastSeenEnemyTick = sim.Clock.Tick;
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> first = Decide(sim, S3, memory);
            Assert.That(new List<EntityId>(memory.HarassGroup), Is.EqualTo(runners));
            // Attack-move orders of the squad: all four go to the far depot.
            int toDepot = 0;
            foreach (AiIntent intent in Of(first, AiIntentKind.AttackMove))
            {
                if (runners.Contains(intent.Unit))
                {
                    Assert.That((intent.Point - new FixVec2(Fix64.Parse("12.5"), Fix64.Parse("50.5"))).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(4)));
                    toDepot++;
                }
            }
            Assert.That(toDepot, Is.EqualTo(4));
            Assert.That(memory.HarassArrivedTick, Is.EqualTo(-1), "not arrived at the first think");
        }

        /// <summary>D-6b: a squad standing at its target attacks the nearest visible worker, and goes home after harass_linger_seconds without one (cooldown set).</summary>
        [Test]
        public void D6b_HarassWorkersAndLinger()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // The squad at the enemy depot, so it has "arrived" at the first think of the run phase.
            List<EntityId> runners = Row(sim, "skai.windrunner", P0, 4, 50, 14);
            Row(sim, "skai.talon_archer", P0, 4, 10, 14);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.grand_shell", "57.5", "57.5", sim.Clock.Tick);
            // The depot really stands there (the squad sees the tile, so a depot that is not there would be forgotten).
            Spawn(sim, "water.pearl_depot", P1, "52.5", "14.5");
            memory.HasLastSeenEnemy = true;
            memory.LastSeenEnemyPosition = new FixVec2(Fix64.Parse("55.5"), Fix64.Parse("55.5"));
            memory.LastSeenEnemyTick = sim.Clock.Tick;
            // A worker in reach of the squad (2 tiles from the first Windrunner), spawned before the first think.
            EntityId worker = Spawn(sim, "water.tender", P1, "52", "16");
            Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(4), "the squad starts");
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> run = Decide(sim, S3, memory);
            Assert.That(memory.HarassArrivedTick, Is.GreaterThanOrEqualTo(0), "arrived");
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> attacks = Of(run, AiIntentKind.AttackUnit);
            Assert.That(attacks.Count, Is.EqualTo(4));
            foreach (AiIntent attack in attacks)
            {
                Assert.That(attack.Target, Is.EqualTo(worker));
            }
            // Remove the worker and wait 10 s: the squad is sent home and the cooldown starts.
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, worker));
            sim.Step();
            // Counter of this case. Unit: count (ticks for tick values). Range: 0 upward.
            int arrived = memory.HarassArrivedTick;
            while (sim.Clock.Tick < arrived + 199)
            {
                sim.Step();
            }
            Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(4), "inside the linger time");
            sim.Step();
            sim.Step();
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> home = Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(0));
            Assert.That(memory.NextHarassTick, Is.EqualTo(sim.Clock.Tick + 60 * 20));
            // Counter of this case. Unit: count (ticks for tick values). Range: 0 upward.
            int moves = 0;
            foreach (AiIntent intent in Of(home, AiIntentKind.Move))
            {
                if (runners.Contains(intent.Unit))
                {
                    moves++;
                }
            }
            Assert.That(moves, Is.EqualTo(4));
        }

        // ------------------------------------------------------------------ D.5

        /// <summary>D-7: 2 enemy Windrunners in the base: only the nearest Pummelers the predictor needs defend, the stance stays Attack and the attack group is not recalled; 8 enemy Roc Riders (Pummelers cannot hit air) recall the attack group.</summary>
        [Test]
        public void D7_MeasuredDefence()
        {
            // Own side: 8 Pummelers at home, 4 more far away in the attack group.
            Simulation sim = Greybox("water", "skai");
            WaterHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> home = Row(sim, "water.grasper", P0, 8, 12, 10);
            List<EntityId> away = Row(sim, "water.grasper", P0, 4, 40, 40);
            Row(sim, "skai.windrunner", P1, 2, 14, 12);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 24, AttackThreshold = 8, HasAttackTarget = true };
            foreach (EntityId id in away)
            {
                memory.AttackGroup.Add(id);
            }
            // The expected defender count: the smallest number of Pummelers whose advantage reaches defend_min_advantage (all are one type, so the count is what matters).
            int expected = 0;
            List<FightEntry> threat = new List<FightEntry> { Full("skai.windrunner", 2) };
            while (expected <= home.Count && Advantage(S3, new List<FightEntry> { Full("water.grasper", expected) }, threat) < S3.DefendMinAdvantage)
            {
                expected++;
            }
            Assert.That(expected, Is.InRange(1, home.Count - 1), "the scenario needs some but not all of the units at home");
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> intents = Decide(sim, S3, memory);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack), "the attack goes on");
            Assert.That(memory.DefendGroup.Count, Is.EqualTo(expected));
            Assert.That(memory.DefendGroup.Count, Is.LessThan(home.Count), "not the whole army");
            foreach (EntityId id in memory.DefendGroup)
            {
                Assert.That(home.Contains(id), Is.True, "defenders come from the units at home");
            }
            foreach (AiIntent intent in intents)
            {
                Assert.That(away.Contains(intent.Unit) && intent.Kind == AiIntentKind.AttackMove && (intent.Point - new FixVec2(Fix64.FromInt(14), Fix64.FromInt(12))).LengthSquared() < Fix64.FromInt(16), Is.False, "the attack group is not sent home");
            }
            // Eight Roc Riders: Pummelers cannot hit air, so every unit is used and the attack group is recalled.
            Simulation air = Greybox("water", "skai");
            WaterHq(air);
            Row(air, "water.grasper", P0, 8, 12, 10);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> awayAir = Row(air, "water.grasper", P0, 4, 40, 40);
            Row(air, "skai.roc_rider", P1, 8, 14, 12);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory recalled = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 24, AttackThreshold = 8, HasAttackTarget = true };
            foreach (EntityId id in awayAir)
            {
                recalled.AttackGroup.Add(id);
            }
            Decide(air, S3, recalled);
            Assert.That(recalled.Stance, Is.EqualTo(AiStance.Defend));
            foreach (EntityId id in awayAir)
            {
                Assert.That(recalled.DefendGroup.Contains(id), Is.True, "the recalled attack group defends");
            }
        }

        // ------------------------------------------------------------------ D.6

        /// <summary>D-8: a unit at 25 percent health with an armed enemy 5 tiles away moves home and leaves the attack group; within 3 tiles of home it is an army unit again.</summary>
        [Test]
        public void D8_FallBack()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId hurt = Spawn(sim, "skai.windrunner", P0, "30", "30");
            Hurt(sim, hurt, "0.25");
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId healthy = Spawn(sim, "skai.windrunner", P0, "30", "31");
            // More healthy units so the group is not outgunned by the one Pummeler (no predicted retreat).
            List<EntityId> others = Row(sim, "skai.windrunner", P0, 6, 28, 33);
            Spawn(sim, "water.grasper", P1, "35", "30");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 8, AttackThreshold = 8 };
            memory.AttackGroup.Add(hurt);
            memory.AttackGroup.Add(healthy);
            foreach (EntityId id in others)
            {
                memory.AttackGroup.Add(id);
            }
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> intents = Decide(sim, S3, memory);
            AiIntent? order = Of(intents, AiIntentKind.Move).Find(i => i.Unit == hurt);
            Assert.That(order, Is.Not.Null);
            Assert.That((order!.Point - FixVec2.FromInts(7, 7)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(4)));
            Assert.That(memory.AttackGroup.Contains(hurt), Is.False);
            Assert.That(memory.AttackGroup.Contains(healthy), Is.True);
            Assert.That(memory.FallenBack.Contains(hurt), Is.True);
            Assert.That(Of(intents, AiIntentKind.AttackMove).Exists(i => i.Unit == hurt), Is.False, "no stance order for the fallen-back unit");
            // A fallen-back unit within 3 tiles of home is an army unit again.
            Simulation near = Greybox();
            SkaiHq(near);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId back = Spawn(near, "skai.windrunner", P0, "8", "8");
            AiMemory returned = new AiMemory { AttackThreshold = 8 };
            returned.FallenBack.Add(back);
            Decide(near, S3, returned);
            Assert.That(returned.FallenBack.Count, Is.EqualTo(0));
            // Under rule K (a known low enemy HQ) nobody falls back.
            Simulation kill = Greybox();
            SkaiHq(kill);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId hurtKill = Spawn(kill, "skai.windrunner", P0, "30", "30");
            Hurt(kill, hurtKill, "0.25");
            Row(kill, "skai.windrunner", P0, 3, 31, 30);
            Spawn(kill, "water.grasper", P1, "35", "30");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory killMemory = new AiMemory { AttackThreshold = 8 };
            Know(killMemory, 900, "water.grand_shell", "57.5", "57.5", kill.Clock.Tick);
            killMemory.KnownEnemyStructures[new EntityId(900)].HealthFraction = Fix64.Parse("0.2");
            Decide(kill, S3, killMemory);
            Assert.That(killMemory.FallenBack.Count, Is.EqualTo(0));
        }

        /// <summary>D-9: a Talon Archer (range 6, speed 2.5) with a Pummeler 2 tiles away moves 3 tiles directly away from it; a Shellback (range 2) at 2 tiles is also kited (range gap 4, speed ratio 2.5 / 1.8).</summary>
        [Test]
        public void D9_KeepDistance()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId archer = Spawn(sim, "skai.talon_archer", P0, "30", "30");
            Spawn(sim, "water.grasper", P1, "32", "30");
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> intents = Decide(sim, S3, new AiMemory());
            AiIntent? order = Of(intents, AiIntentKind.Move).Find(i => i.Unit == archer);
            Assert.That(order, Is.Not.Null);
            // Exactly 3 tiles straight away from the Pummeler (along -x).
            Assert.That((order!.Point - new FixVec2(Fix64.FromInt(27), Fix64.FromInt(30))).LengthSquared(), Is.LessThanOrEqualTo(Fix64.Parse("0.01")));
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation shell = Greybox();
            SkaiHq(shell);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId archer2 = Spawn(shell, "skai.talon_archer", P0, "30", "30");
            Spawn(shell, "water.shellback", P1, "30", "32");
            // The order the AI gave that unit, or null when it gave none.
            AiIntent? shellOrder = Of(Decide(shell, S3, new AiMemory()), AiIntentKind.Move).Find(i => i.Unit == archer2);
            Assert.That(shellOrder, Is.Not.Null, "a Shellback (melee-range weapon 2) is kited too");
            Assert.That((shellOrder!.Point - new FixVec2(Fix64.FromInt(30), Fix64.FromInt(27))).LengthSquared(), Is.LessThanOrEqualTo(Fix64.Parse("0.01")));
            // Too fast an enemy (speed ratio below 0.8) or too long a weapon (no range gap): no keep-distance order.
            Simulation gap = Greybox();
            SkaiHq(gap);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId windrunner = Spawn(gap, "skai.windrunner", P0, "30", "30");
            Spawn(gap, "water.grasper", P1, "32", "30");
            // A Windrunner (range 4, speed 3.5) against a Pummeler (range 1, speed 3): the gap 3 and the speed ratio hold, so it kites too; with a gap requirement of 4 it does not.
            Assert.That(Of(Decide(gap, S3, new AiMemory()), AiIntentKind.Move).Exists(i => i.Unit == windrunner), Is.True);
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation wide = Greybox();
            SkaiHq(wide);
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId stays = Spawn(wide, "skai.windrunner", P0, "30", "30");
            Spawn(wide, "water.grasper", P1, "32", "30");
            // The profile under test (a shipped file or an edited copy of it).
            AiProfile strict = S3Edited("\"kite_min_range_gap_tiles\": 2,", "\"kite_min_range_gap_tiles\": 4,");
            Assert.That(Of(Decide(wide, strict, new AiMemory()), AiIntentKind.Move).Exists(i => i.Unit == stays), Is.False);
        }

        /// <summary>D-10: a Windrunner at 20 health and a Pummeler at 110, both in reach of 3 Talon Archers: all three focus the Windrunner; once it is gone they switch to the Pummeler.</summary>
        [Test]
        public void D10_FocusFastestKill()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> archers = Row(sim, "skai.talon_archer", P0, 3, 30, 30);
            EntityId runner = Spawn(sim, "skai.windrunner", P1, "36", "30");
            Hurt(sim, runner, (20.0 / 60.0).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId pummeler = Spawn(sim, "water.grasper", P1, "36", "31");
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 6, AttackThreshold = 8 };
            foreach (EntityId id in archers)
            {
                memory.AttackGroup.Add(id);
            }
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> first = Decide(sim, S3Focus, memory);
            List<AiIntent> attacks = Of(first, AiIntentKind.AttackUnit);
            Assert.That(attacks.Count, Is.EqualTo(3));
            foreach (AiIntent attack in attacks)
            {
                Assert.That(attack.Target, Is.EqualTo(runner));
            }
            // The orders take effect; then the Windrunner is gone and every Archer switches to the Pummeler.
            foreach (EntityId id in archers)
            {
                sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P0, _sequence++, id, runner));
            }
            sim.Step();
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, runner));
            sim.Step();
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> second = Of(Decide(sim, S3Focus, memory), AiIntentKind.AttackUnit);
            Assert.That(second.Count, Is.EqualTo(3));
            foreach (AiIntent attack in second)
            {
                Assert.That(attack.Target, Is.EqualTo(pummeler));
            }
        }

        /// <summary>D-11: 30 Talon Archers that all qualify for focus fire with focus_max_commands_per_think 12 give exactly 12 attack orders; the total never exceeds max_commands_per_think.</summary>
        [Test]
        public void D11_Budgets()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> archers = Row(sim, "skai.talon_archer", P0, 30, 30, 30);
            Spawn(sim, "water.grasper", P1, "36", "32");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 60, AttackThreshold = 8 };
            foreach (EntityId id in archers)
            {
                memory.AttackGroup.Add(id);
            }
            // Intents decided by the think, in rule order. Unit: intents. Range: 0 to max_commands_per_think.
            List<AiIntent> intents = Decide(sim, S3Focus, memory);
            Assert.That(Of(intents, AiIntentKind.AttackUnit).Count, Is.EqualTo(S3.FocusMaxCommandsPerThink));
            Assert.That(intents.Count, Is.LessThanOrEqualTo(S3.MaxCommandsPerThink));
        }

        /// <summary>D-12: the same view, profile, memory copy and seed twice give identical intents, memory and random draws.</summary>
        [Test]
        public void D12_Deterministic()
        {
            // Builds the situation of D-10 and thinks once; returns the intent lines, the stance and the draws.
            (List<string> Lines, int Stance, ulong Draws, int Harass) Run()
            {
                // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
                Simulation sim = Greybox();
                SkaiHq(sim);
                // IDs of the units spawned for this case, ascending. Unit: unit IDs.
                List<EntityId> archers = Row(sim, "skai.talon_archer", P0, 6, 30, 30);
                EntityId runner = Spawn(sim, "skai.windrunner", P1, "36", "30");
                Hurt(sim, runner, "0.3");
                Spawn(sim, "water.grasper", P1, "36", "31");
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 12, AttackThreshold = 8 };
                foreach (EntityId id in archers)
                {
                    memory.AttackGroup.Add(id);
                }
                // The think's own generator (seeded; its draw count is compared).
                DeterministicRandom rng = new DeterministicRandom(7);
                List<AiIntent> intents = Decide(sim, S3Focus, memory, rng);
                return (intents.ConvertAll(i => i.Kind + " " + i.Unit + " " + i.Target + " " + i.Point), (int)memory.Stance, rng.DrawCount, memory.HarassGroup.Count);
            }
            (List<string> Lines, int Stance, ulong Draws, int Harass) a = Run();
            (List<string> Lines, int Stance, ulong Draws, int Harass) b = Run();
            Assert.That(b.Lines, Is.EqualTo(a.Lines));
            Assert.That(b.Stance, Is.EqualTo(a.Stance));
            Assert.That(b.Draws, Is.EqualTo(a.Draws));
        }


        // ------------------------------------------------------------------ package D review fixes

        /// <summary>Review item 4: an attack that started less than attack_retreat_grace_seconds ago is not recalled by the retreat test, unless the group has already lost supply.</summary>
        [Test]
        public void D4b_RetreatGraceAfterTheAttackStarts()
        {
            // The scenario of D-4 with the attack starting now (fresh) or having lost supply already.
            foreach ((int startSupply, bool retreats) in new[] { (6, false), (12, true) })
            {
                // The match of this case.
                Simulation sim = Greybox("water", "skai");
                WaterHq(sim);
                // IDs of the units spawned for this case, ascending. Unit: unit IDs.
                List<EntityId> group = Row(sim, "water.grasper", P0, 3, 30, 30);
                Row(sim, "skai.roc_rider", P1, 2, 34, 30);
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = startSupply, AttackThreshold = 8, AttackStartTick = sim.Clock.Tick };
                foreach (EntityId id in group)
                {
                    memory.AttackGroup.Add(id);
                }
                Decide(sim, S3, memory);
                Assert.That(memory.Retreats, Is.EqualTo(retreats ? 1 : 0), "start supply " + startSupply);
            }
        }

        /// <summary>Review item 1: stacked and almost stacked units give no keep-distance order and no exception; at the map edge the step stays inside the map.</summary>
        [Test]
        public void D9b_KiteGuardsAgainstZeroDistanceAndTheMapEdge()
        {
            foreach (string enemyX in new[] { "30", "30.00001", "30.000001" })
            {
                // The match of this case.
                Simulation sim = Greybox();
                SkaiHq(sim);
                // ID of the archer.
                EntityId archer = Spawn(sim, "skai.talon_archer", P0, "30", "30");
                Spawn(sim, "water.grasper", P1, enemyX, "30");
                // Intents decided by the think.
                List<AiIntent> intents = null!;
                Assert.DoesNotThrow(() => intents = Decide(sim, S3, new AiMemory()));
                Assert.That(Of(intents, AiIntentKind.Move).Exists(i => i.Unit == archer && (i.Point - new FixVec2(Fix64.FromInt(30), Fix64.FromInt(30))).LengthSquared() > Fix64.FromInt(100)), Is.False);
            }
            // At the left map edge the archer (0.6, 30) with a Pummeler 1.4 tiles east steps to x = 0.5, not off the map.
            Simulation edge = Greybox();
            SkaiHq(edge);
            // ID of the archer at the edge.
            EntityId atEdge = Spawn(edge, "skai.talon_archer", P0, "0.6", "30");
            Spawn(edge, "water.grasper", P1, "2", "30");
            // The keep-distance order, or null.
            AiIntent? order = Of(Decide(edge, S3, new AiMemory()), AiIntentKind.Move).Find(i => i.Unit == atEdge);
            Assert.That(order, Is.Not.Null);
            Assert.That(order!.Point.X >= Fix64.Half, Is.True, "inside the map");
        }

        /// <summary>Review item 2: a fallen-back unit that idles away from home is sent again, is released after fallback_release_seconds, and follows a relocating HQ.</summary>
        [Test]
        public void D8b_FallenBackUnitsAreNotStranded()
        {
            // The match of this case.
            Simulation sim = Greybox();
            SkaiHq(sim);
            // ID of the hurt unit.
            EntityId hurt = Spawn(sim, "skai.windrunner", P0, "30", "30");
            Hurt(sim, hurt, "0.25");
            // ID of the enemy that makes the unit fall back; it is removed before the release so the unit does not fall back again.
            EntityId threat = Spawn(sim, "water.grasper", P1, "35", "30");
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            Decide(sim, S3, memory);
            Assert.That(memory.FallenBack.Contains(hurt), Is.True);
            // The unit has not moved (the orders were not applied), so it is idle away from home: the move is given again.
            // Intents of the second think.
            List<AiIntent> again = Decide(sim, S3, memory);
            Assert.That(Of(again, AiIntentKind.Move).Exists(i => i.Unit == hurt), Is.True, "move re-issued");
            Assert.That(Of(again, AiIntentKind.AttackMove).Exists(i => i.Unit == hurt), Is.False);
            // After the release time it is an army unit again.
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, threat));
            for (int i = 0; i < (int)(S3.FallbackReleaseSeconds.FloorToInt() * 20) + 20; i++)
            {
                sim.Step();
            }
            Decide(sim, S3, memory);
            Assert.That(memory.FallenBack.Count, Is.EqualTo(0));
            Assert.That(memory.FallenBackSince.Count, Is.EqualTo(0));
            // A relocating HQ: the unit is sent to the new home point, not the old HQ.
            Simulation moving = Greybox();
            SkaiHq(moving);
            // ID of the HQ.
            EntityId hq = EntityId.None;
            foreach (AiUnitView unit in AiWorldView.Build(moving, P0).OwnUnits)
            {
                hq = unit.IsHq ? unit.Id : hq;
            }
            moving.Submit(new UprootHqCommand(moving.Clock.Tick, P0, _sequence++, hq));
            // Steps spent waiting for the uproot. Range: 0 to 600.
            for (int i = 0; i < 600; i++)
            {
                moving.Step();
            }
            // ID of the unit spawned for this case. Unit: unit ID.
            EntityId stranded = Spawn(moving, "skai.windrunner", P0, "30", "30");
            AiMemory relocating = new AiMemory { AttackThreshold = 8, RelocationActive = true, RelocationField = 11, RelocationStartTick = moving.Clock.Tick };
            relocating.RelocationDestination = FixVec2.FromInts(45, 10);
            relocating.FallenBack.Add(stranded);
            relocating.FallenBackSince[stranded] = moving.Clock.Tick;
            // The move given to the stranded unit.
            AiIntent? follow = Of(Decide(moving, S3, relocating), AiIntentKind.Move).Find(i => i.Unit == stranded);
            Assert.That(follow, Is.Not.Null);
            Assert.That((follow!.Point - relocating.RelocationDestination).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(9)), "toward the relocation destination");
        }

        /// <summary>Review item 3: a one-unit threat during an attack is answered by the units at home; with nobody at home the small supply asked of the attack group does not recall it.</summary>
        [Test]
        public void D7b_SmallThreatDoesNotEndTheAttack()
        {
            foreach (bool unitsAtHome in new[] { true, false })
            {
                // The match of this case.
                Simulation sim = Greybox("water", "water");
                WaterHq(sim);
                if (unitsAtHome)
                {
                    Row(sim, "water.grasper", P0, 6, 12, 10);
                }
                // IDs of the attack group, far away.
                List<EntityId> away = Row(sim, "water.grasper", P0, 4, 40, 40);
                Spawn(sim, "water.reef_runner", P1, "14", "12");
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 8, AttackThreshold = 8, HasAttackTarget = true, AttackStartTick = sim.Clock.Tick - 400 };
                foreach (EntityId id in away)
                {
                    memory.AttackGroup.Add(id);
                }
                Decide(sim, S3, memory);
                Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack), "at home: " + unitsAtHome);
                foreach (EntityId id in away)
                {
                    Assert.That(memory.DefendGroup.Contains(id), Is.False);
                }
                Assert.That(memory.DefendGroup.Count > 0, Is.EqualTo(unitsAtHome));
            }
        }

        /// <summary>Review item 5: a harass squad is recalled after harass_max_seconds, and a member dying is handled (the squad shrinks; with every member dead it ends with the cooldown).</summary>
        [Test]
        public void D6c_HarassExpiresAndSurvivesDeaths()
        {
            // The match of this case.
            Simulation sim = Greybox();
            SkaiHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> runners = Row(sim, "skai.windrunner", P0, 4, 10, 10);
            Row(sim, "skai.talon_archer", P0, 4, 10, 14);
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.grand_shell", "57.5", "57.5", sim.Clock.Tick);
            Know(memory, 901, "water.pearl_depot", "12.5", "50.5", sim.Clock.Tick);
            memory.HasLastSeenEnemy = true;
            memory.LastSeenEnemyPosition = new FixVec2(Fix64.Parse("55.5"), Fix64.Parse("55.5"));
            memory.LastSeenEnemyTick = sim.Clock.Tick;
            Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(4));
            // One member dies: the squad goes on with three.
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, runners[0]));
            sim.Step();
            Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(3));
            // The squad never reaches the target; after 90 s it is recalled and the cooldown starts.
            // Ticks until the squad's time is up. Unit: ticks.
            int expire = memory.HarassStartTick + 90 * 20;
            while (sim.Clock.Tick < expire)
            {
                sim.Step();
            }
            // Intents of the think at the expiry.
            List<AiIntent> home = Decide(sim, S3, memory);
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(0));
            Assert.That(memory.NextHarassTick, Is.EqualTo(sim.Clock.Tick + 60 * 20));
            Assert.That(Of(home, AiIntentKind.Move).Count, Is.EqualTo(3));
            // Every member of a new squad dead: the group is cleared with the cooldown.
            AiMemory lost = new AiMemory();
            lost.HarassGroup.Add(runners[1]);
            lost.HarassGroup.Add(new EntityId(9999));
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation other = Greybox();
            SkaiHq(other);
            Decide(other, S3, lost);
            Assert.That(lost.HarassGroup.Count, Is.EqualTo(0));
            Assert.That(lost.NextHarassTick, Is.EqualTo(other.Clock.Tick + 60 * 20));
        }

        /// <summary>Review item 9: when nothing of ours can damage the visible enemy (Pummelers against a Roc Rider) fastest-kill focus fire gives no order and does not fail.</summary>
        [Test]
        public void D10b_FocusWithNothingThatCanDamage()
        {
            // The match of this case.
            Simulation sim = Greybox("water", "skai");
            WaterHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> group = Row(sim, "water.grasper", P0, 3, 30, 30);
            Spawn(sim, "skai.roc_rider", P1, "34", "30");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 6, AttackThreshold = 8, AttackStartTick = sim.Clock.Tick };
            foreach (EntityId id in group)
            {
                memory.AttackGroup.Add(id);
            }
            // Intents decided by the think.
            List<AiIntent> intents = null!;
            Assert.DoesNotThrow(() => intents = Decide(sim, S3Focus, memory));
            Assert.That(Of(intents, AiIntentKind.AttackUnit).Count, Is.EqualTo(0));
        }

        /// <summary>Review item 9 (fog): an enemy structure and an enemy unit nobody sees are ignored by target choice and focus fire.</summary>
        [Test]
        public void DFog_UnseenEnemiesAreIgnored()
        {
            // The match of this case.
            Simulation sim = Greybox();
            SkaiHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> archers = Row(sim, "skai.talon_archer", P0, 8, 10, 10);
            // A depot and a Pummeler far outside every sight range: the AI has never seen them.
            Spawn(sim, "water.pearl_depot", P1, "50.5", "20.5");
            Spawn(sim, "water.grasper", P1, "55", "55");
            // The AI memory of this case (stance, groups, known enemies; one per case).
            AiMemory memory = new AiMemory();
            // Intents decided by the think.
            List<AiIntent> intents = Decide(sim, S3, memory);
            Assert.That(memory.KnownEnemyStructures.Count, Is.EqualTo(0));
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That((memory.AttackTarget - new FixVec2(Fix64.Parse("50.5"), Fix64.Parse("20.5"))).LengthSquared(), Is.GreaterThan(Fix64.FromInt(100)), "not aimed at the unseen depot");
            Assert.That(Of(intents, AiIntentKind.AttackUnit).Count, Is.EqualTo(0));
            Assert.That(archers.Count, Is.EqualTo(8));
        }

        /// <summary>Review item 9: shuffling the order in which identical units are spawned (so their IDs differ) gives the same orders by position.</summary>
        [Test]
        public void D12b_ShuffledSpawnOrderGivesTheSameOrders()
        {
            // Describes the intents of one run as sorted text of kind, own position, target position and point (IDs left out).
            List<string> Run(int[] order)
            {
                // The match of this case.
                Simulation sim = Greybox();
                SkaiHq(sim);
                // The six archers' column offsets, spawned in the given order.
                foreach (int i in order)
                {
                    Spawn(sim, "skai.talon_archer", P0, (30 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), "30");
                }
                Spawn(sim, "water.grasper", P1, "36", "30");
                Spawn(sim, "water.shellback", P1, "36", "31");
                // The AI memory of this case (stance, groups, known enemies; one per case).
                AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 12, AttackThreshold = 8, AttackStartTick = sim.Clock.Tick - 400 };
                // The view used to look units up by ID.
                AiWorldView view = AiWorldView.Build(sim, P0);
                foreach (AiUnitView unit in view.OwnUnits)
                {
                    if (unit.Definition.Id == "skai.talon_archer")
                    {
                        memory.AttackGroup.Add(unit.Id);
                    }
                }
                // Lines describing each intent.
                List<string> lines = new List<string>();
                foreach (AiIntent intent in Decide(sim, S3Focus, memory))
                {
                    // Position of the intent's unit and of its target. Unit: tiles.
                    string own = string.Empty;
                    string target = string.Empty;
                    foreach (AiUnitView unit in view.OwnUnits)
                    {
                        own = unit.Id == intent.Unit ? unit.Position.ToString() : own;
                    }
                    foreach (AiUnitView unit in view.VisibleEnemies)
                    {
                        target = unit.Id == intent.Target ? unit.Position.ToString() : target;
                    }
                    lines.Add(intent.Kind + " " + intent.DefinitionId + " " + own + " " + target + " " + intent.Point);
                }
                lines.Sort(StringComparer.Ordinal);
                return lines;
            }
            Assert.That(Run(new[] { 5, 4, 3, 2, 1, 0 }), Is.EqualTo(Run(new[] { 0, 1, 2, 3, 4, 5 })));
            Assert.That(Run(new[] { 3, 0, 5, 1, 4, 2 }), Is.EqualTo(Run(new[] { 0, 1, 2, 3, 4, 5 })));
        }

        // ------------------------------------------------------------------ profiles and helpers

        /// <summary>The package D switches: aggressive_s3 has all four on, every other shipped profile has them off; use_predictor_attack needs use_enemy_model (the error names the file and both fields).</summary>
        [Test]
        public void DProfiles_SwitchesAndValidation()
        {
            Assert.That(S3.UsePredictorAttack && S3.UseTargetChoice && S3.UseMeasuredDefence && S3.UseUnitControl, Is.True);
            Assert.That(S3.UseFocusFire, Is.EqualTo(S3FocusFire), "use_focus_fire as decided by the 72-game check");
            foreach (string id in Data.AiProfiles.Keys)
            {
                if (id == "aggressive_s3" || id == "aggressive_s4r" || id == "aggressive_s5" || id == "aggressive_s5_fp2" || id == "abl_s5_no_e" || id == "abl_s5_no_p" || id == "abl_s5_no_h" || id == "aggressive_s5_b" || id == "aggressive_s5_r" || id == "aggressive_s5_m" || id == "aggressive_s5_pd" || id == "aggressive_s5_e" || id == "aggressive_s5_s" || id == "aggressive_s5_rr" || id == "aggressive_s5_kite" || id == "abl_s5b_no_b" || id == "abl_s5_p1_only" || id == "abl_s5_p2_only")
                {
                    continue;
                }
                // The profile under test (a shipped file or an edited copy of it).
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UsePredictorAttack || profile.UseTargetChoice || profile.UseMeasuredDefence || profile.UseUnitControl || profile.UseFocusFire, Is.False, id);
            }
            DataValidationException error = Expect.Throws<DataValidationException>(() => S3Edited("\"use_enemy_model\": true", "\"use_enemy_model\": false", "\"use_counter_mix\": true", "\"use_counter_mix\": false",
                "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false", "\"use_static_defence\": true", "\"use_static_defence\": false", "\"use_rare_priority\": true", "\"use_rare_priority\": false",
                "\"use_predictor_mix\": true", "\"use_predictor_mix\": false"));
            Assert.That(error.Message, Does.Contain("use_predictor_attack").And.Contain("use_enemy_model").And.Contain("aggressive_s3.json"));
            DataValidationException missing = Expect.Throws<DataValidationException>(() => S3Edited("\"attack_force_supply\": 60,", string.Empty));
            Assert.That(missing.Message, Does.Contain("attack_force_supply"));
            Assert.That(S3.AttackMinAdvantage == Fix64.Parse("1.3"), Is.True);
            Assert.That(S3.FocusMaxCommandsPerThink, Is.EqualTo(12));
        }

        /// <summary>The grid index returns exactly what a brute-force scan returns, in ascending list order.</summary>
        [Test]
        public void DProximity_MatchesABruteForceScan()
        {
            // The match of this case: a started greybox_1v1 simulation (one per case; tick 0 upward).
            Simulation sim = Greybox();
            SkaiHq(sim);
            // IDs of the units spawned for this case, ascending. Unit: unit IDs.
            List<EntityId> ids = new List<EntityId>();
            // Spawn index. Range: 0 to 39.
            for (int i = 0; i < 40; i++)
            {
                ids.Add(Spawn(sim, "skai.windrunner", P0, (8 + (i * 7) % 23).ToString(System.Globalization.CultureInfo.InvariantCulture), (8 + (i * 5) % 19).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            // Unit views collected for this check, ascending ID. Unit: units.
            List<AiUnitView> units = new List<AiUnitView>();
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0).OwnUnits)
            {
                if (unit.IsArmy)
                {
                    units.Add(unit);
                }
            }
            // Radius of the spatial query. Unit: tiles. Range: 6.
            Fix64 radius = Fix64.FromInt(6);
            AiProximity index = new AiProximity(units, radius);
            foreach (AiUnitView centre in units)
            {
                // Unit views collected for this check, ascending ID. Unit: units.
                List<AiUnitView> fast = new List<AiUnitView>();
                index.Near(centre.Position, radius, fast);
                // Unit views collected for this check, ascending ID. Unit: units.
                List<AiUnitView> slow = units.FindAll(u => (u.Position - centre.Position).LengthSquared() <= radius * radius);
                Assert.That(fast.ConvertAll(u => u.Id), Is.EqualTo(slow.ConvertAll(u => u.Id)));
            }
            Assert.That(ids.Count, Is.EqualTo(40));
        }
    }
}
