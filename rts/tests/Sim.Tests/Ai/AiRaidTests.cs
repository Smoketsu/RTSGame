// rts/tests/Sim.Tests/Ai/AiRaidTests.cs
// Tests F-1 to F-11 of rts/docs/specs/skirmish_ai_v4.md package F (raid response and economy bugs): worker safety, hold when outmatched,
// the worker-loss exemption of the static defence value cap, depleted-field feedback and forgetting unreachable sites. Part of Rts.Sim.Tests.
// Each test builds a real greybox_1v1 simulation (P0 Skai HQ at the spawn (8, 8), a complete Perch Depot at (18.5, 7.5), the map explored),
// spawns units with script commands and runs AiBrain.Decide on the resulting view; F-7 runs the controller. Profile aggressive_s4f unless stated.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Skirmish AI v4 package F.</summary>
    [TestFixture]
    public class AiRaidTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the enemy).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The package F profile.</summary>
        private static AiProfile S4F => AiProfile.FromDataSet(Data, "aggressive_s4f");

        /// <summary>The baseline profile (every package F switch off).</summary>
        private static AiProfile S3 => AiProfile.FromDataSet(Data, "aggressive_s3");

        /// <summary>The shipped aggressive_s4f text with edits applied (each old text must occur once) and parsed.</summary>
        private static AiProfile S4FEdited(params string[] oldNewPairs)
        {
            return Edited("aggressive_s4f", oldNewPairs);
        }

        /// <summary>The shipped profile file <paramref name="id"/> with edits applied (each old text must occur once) and parsed.</summary>
        private static AiProfile Edited(string id, params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json")).Replace("\r\n", "\n");
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/" + id + ".json", Data.Content);
        }

        /// <summary>A fixed-point value from a decimal text.</summary>
        private static Fix64 F(string text)
        {
            return Fix64.Parse(text);
        }

        /// <summary>A point from two decimal texts. Unit: tiles.</summary>
        private static FixVec2 At(string x, string y)
        {
            return new FixVec2(F(x), F(y));
        }

        /// <summary>The common setup: greybox_1v1, P0 Skai HQ at the spawn, a complete Perch Depot at (18.5, 7.5), the map explored.</summary>
        private Simulation Base(out EntityId depot)
        {
            // Players of the match.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, sim.Map.Spawns[0].X.ToString(), sim.Map.Spawns[0].Y.ToString());
            depot = Spawn(sim, "skai.perch_depot", P0, "18.5", "7.5");
            ExploreKit.ExploreAll(sim);
            return sim;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, At(x, y)));
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

        /// <summary>Three P0 fledglings at (20.5, 9.5) ordered to gather field 6 and stepped until none is idle.</summary>
        private List<EntityId> Gatherers(Simulation sim)
        {
            // The workers spawned, ascending. Unit: unit IDs.
            List<EntityId> workers = new List<EntityId>();
            for (int i = 0; i < 3; i++)
            {
                workers.Add(Spawn(sim, "skai.fledgling", P0, "20.5", "9.5"));
            }
            foreach (EntityId id in workers)
            {
                sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, id, 6));
            }
            // Steps taken waiting for the orders to take effect. Unit: ticks. Range: 0 to 40.
            for (int i = 0; i < 40; i++)
            {
                sim.Step();
                bool idle = false;
                foreach (EntityId id in workers)
                {
                    sim.Units.TryGet(id, out Unit? unit);
                    idle |= unit!.GatherState == GatherState.Idle;
                }
                if (!idle)
                {
                    break;
                }
            }
            return workers;
        }

        /// <summary>Decides for P0 (an AttackThreshold of 8 is set so the first think does not draw).</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            if (memory.AttackThreshold < 0)
            {
                memory.AttackThreshold = 8;
            }
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
        }

        /// <summary>The intents of one kind.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            return intents.FindAll(i => i.Kind == kind);
        }

        /// <summary>The intents of one kind that order one of the given units.</summary>
        private static List<AiIntent> For(List<AiIntent> intents, AiIntentKind kind, List<EntityId> units)
        {
            return intents.FindAll(i => i.Kind == kind && units.Contains(i.Unit));
        }

        /// <summary>A text of the intents, for the determinism comparison.</summary>
        private static string Text(List<AiIntent> intents)
        {
            // The text being built.
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (AiIntent i in intents)
            {
                text.Append(i.Kind).Append(' ').Append(i.Unit).Append(' ').Append(i.Field).Append(' ').Append(i.DefinitionId).Append(' ')
                    .Append(i.TileX).Append(' ').Append(i.TileY).Append(' ').Append(i.Point).Append(' ').Append(i.Target).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>The predicted advantage of a group of own units against enemy units.</summary>
        private static Fix64 Advantage(AiProfile profile, List<FightEntry> own, List<FightEntry> enemy)
        {
            return FightPredictor.Predict(own, enemy, false, Data.Content.Combat, PredictorSettings.From(profile)).Advantage;
        }

        /// <summary>One predictor entry at full health.</summary>
        private static FightEntry Full(string id, int count)
        {
            // The definition of the unit type.
            UnitDefinition definition = Data.Content.Units[id];
            return new FightEntry(definition, Fix64.FromInt(count), definition.MaxHealth * count);
        }

        /// <summary>Squared distance of two points. Unit: squared tiles.</summary>
        private static Fix64 Dist2(FixVec2 a, FixVec2 b)
        {
            return (a - b).LengthSquared();
        }

        /// <summary>F-1: three workers on field 6 with a Windrunner 2 tiles away all go to field 1; none to 6 or 7 (7 is 8.0 tiles from the raider).</summary>
        [Test]
        public void F1_Evacuation()
        {
            Simulation sim = Base(out _);
            List<EntityId> workers = Gatherers(sim);
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            List<AiIntent> intents = Decide(sim, S4F, new AiMemory());
            List<AiIntent> gathers = For(intents, AiIntentKind.Gather, workers);
            Assert.That(gathers.Count, Is.EqualTo(3));
            foreach (AiIntent intent in gathers)
            {
                Assert.That(intent.Field, Is.EqualTo(1));
            }
            foreach (AiIntent intent in Of(intents, AiIntentKind.Gather))
            {
                Assert.That(intent.Field, Is.Not.EqualTo(6));
                Assert.That(intent.Field, Is.Not.EqualTo(7));
            }
        }

        /// <summary>F-2: after the raider is gone the fields stay dangerous for 10 s: at +5 s no gather to 6 or 7, at +11 s an idle fledgling goes to field 6.</summary>
        [Test]
        public void F2_DangerMemory()
        {
            Simulation sim = Base(out _);
            List<EntityId> workers = Gatherers(sim);
            EntityId raider = Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            AiMemory memory = new AiMemory();
            AiProfile profile = S4F;
            Decide(sim, profile, memory);
            // Tick of the first decision. Unit: ticks.
            int first = sim.Clock.Tick;
            Assert.That(sim.Units.Remove(raider), Is.True);
            foreach (EntityId id in workers)
            {
                Assert.That(sim.Units.Remove(id), Is.True);
            }
            Spawn(sim, "skai.fledgling", P0, "20.5", "9.5");
            while (sim.Clock.Tick < first + 100)
            {
                sim.Step();
            }
            foreach (AiIntent intent in Of(Decide(sim, profile, memory), AiIntentKind.Gather))
            {
                Assert.That(intent.Field, Is.Not.EqualTo(6).And.Not.EqualTo(7), "still dangerous at +5 s");
            }
            while (sim.Clock.Tick < first + 220)
            {
                sim.Step();
            }
            Assert.That(Of(Decide(sim, profile, memory), AiIntentKind.Gather).Exists(i => i.Field == 6), Is.True, "safe again at +11 s");
        }

        /// <summary>F-3: raiders on fields 0 and 1 as well: no safe field, and home is within 10 tiles of a raider, so the workers get no order.</summary>
        [Test]
        public void F3_NowhereSafe()
        {
            Simulation sim = Base(out _);
            List<EntityId> workers = Gatherers(sim);
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            Spawn(sim, "skai.windrunner", P1, "10.5", "6.5");
            Spawn(sim, "skai.windrunner", P1, "6.5", "10.5");
            List<AiIntent> intents = Decide(sim, S4F, new AiMemory());
            Assert.That(For(intents, AiIntentKind.Gather, workers), Is.Empty);
            Assert.That(For(intents, AiIntentKind.Move, workers), Is.Empty);
        }

        /// <summary>F-4: with the switch off (aggressive_s3) no gather intent for the three workers.</summary>
        [Test]
        public void F4_SwitchOff()
        {
            Simulation sim = Base(out _);
            List<EntityId> workers = Gatherers(sim);
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            Assert.That(For(Decide(sim, S3, new AiMemory()), AiIntentKind.Gather, workers), Is.Empty);
        }

        /// <summary>F-5: a Talon Archer against two Windrunners cannot reach 1.5, so it holds at home; with aggressive_s3 it attack-moves to the threat.</summary>
        [Test]
        public void F5_Hold()
        {
            Simulation sim = Base(out _);
            EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
            Spawn(sim, "skai.windrunner", P1, "20.5", "8.5");
            Spawn(sim, "skai.windrunner", P1, "20.5", "8.5");
            AiProfile profile = S4F;
            Assert.That(Advantage(profile, new List<FightEntry> { Full("skai.talon_archer", 1) }, new List<FightEntry> { Full("skai.windrunner", 2) }),
                Is.LessThan(profile.DefendMinAdvantage));
            AiMemory memory = new AiMemory();
            List<AiIntent> intents = Decide(sim, profile, memory);
            List<EntityId> only = new List<EntityId> { talon };
            // Home of the HQ. Unit: tiles.
            FixVec2 home = sim.Map.Spawns[0];
            List<AiIntent> moves = For(intents, AiIntentKind.Move, only);
            Assert.That(moves.Count, Is.EqualTo(1));
            Assert.That(Dist2(moves[0].Point, home) <= F("4"), Is.True, "home or the nearest standable tile");
            Assert.That(For(intents, AiIntentKind.AttackMove, only), Is.Empty);
            Assert.That(For(intents, AiIntentKind.AttackUnit, only), Is.Empty);
            Assert.That(memory.DefendGroup.Contains(talon), Is.True);

            AiMemory baseline = new AiMemory();
            List<AiIntent> old = Decide(sim, S3, baseline);
            // Without the hold the talon is sent at the threat: focus fire (AttackUnit) at 6 tiles, an attack-move otherwise.
            Assert.That(For(old, AiIntentKind.AttackUnit, only).Count + For(old, AiIntentKind.AttackMove, only).Count, Is.EqualTo(1));
            Assert.That(For(old, AiIntentKind.Move, only), Is.Empty);
        }

        /// <summary>F-6: the Windrunners inside the HQ's range plus margin: the HQ joins the fixed side; the branch follows the computed advantage.</summary>
        [Test]
        public void F6_HqJoins()
        {
            Simulation sim = Base(out _);
            EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
            Spawn(sim, "skai.windrunner", P1, "12.5", "8.5");
            Spawn(sim, "skai.windrunner", P1, "12.5", "8.5");
            AiProfile profile = S4F;
            // The own HQ and the talon as the brain's views, for the advantage the brain computes.
            List<AiUnitView> side = new List<AiUnitView>();
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0, new int[0]).OwnUnits)
            {
                if (unit.IsHq || unit.Id == talon)
                {
                    side.Add(unit);
                }
            }
            Assert.That(side.Count, Is.EqualTo(2));
            // Advantage of the HQ and the talon together.
            Fix64 together = Advantage(profile, FightPredictor.Group(side), new List<FightEntry> { Full("skai.windrunner", 2) });
            List<AiIntent> intents = Decide(sim, profile, new AiMemory());
            List<EntityId> only = new List<EntityId> { talon };
            if (together >= profile.DefendMinAdvantage)
            {
                Assert.That(For(intents, AiIntentKind.AttackMove, only).Count + For(intents, AiIntentKind.AttackUnit, only).Count, Is.EqualTo(1), "HQ + talon: " + together);
            }
            else
            {
                Assert.That(For(intents, AiIntentKind.AttackMove, only), Is.Empty, "HQ + talon: " + together);
            }
        }

        /// <summary>F-7: a gather order to a field that is empty in the sim is rejected field_depleted; with use_depleted_rejection the later thinks do not repeat it, without it they do.</summary>
        [Test]
        public void F7_DepletedField()
        {
            Assert.That(DepletedOrders(S4F, out int remembered), Is.EqualTo(1), "feedback on: one gather to field 4");
            Assert.That(remembered, Is.EqualTo(0), "the rejection zeroed the memory");
            Assert.That(DepletedOrders(S3, out int stale), Is.GreaterThan(1), "feedback off: the order is repeated");
            Assert.That(stale, Is.EqualTo(600), "feedback off: the stale amount stays");
        }

        /// <summary>Runs the F-7 scenario for 60 ticks and returns how many gather orders to field 4 the controller submitted; <paramref name="remembered"/> is the memory of field 4 at the end.</summary>
        private int DepletedOrders(AiProfile profile, out int remembered)
        {
            Simulation sim = Base(out EntityId depot);
            Assert.That(sim.Units.Remove(depot), Is.True);
            Spawn(sim, "skai.fledgling", P0, "9.5", "9.5");
            // No money, so the build order does not take the only worker as a builder.
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(0, 0);
            // Every field is empty in the sim (the visible ones are re-read by rule 0, so they must really be empty); only field 4 is fogged and remembered as full.
            for (int field = 0; field < sim.Resources.Count; field++)
            {
                sim.Resources[field].Take(sim.Resources[field].Remaining);
            }
            SkirmishAiController controller = new SkirmishAiController(P0, profile);
            controller.OnMatchStart(sim);
            // Remembered amounts: 0 everywhere but field 4, which still looks full. Unit: resource units.
            int[] known = new int[sim.Resources.Count];
            known[4] = 600;
            controller.Memory.KnownFieldRemaining = known;
            // The HQ stays rooted (its home fields are empty, which would start a relocation to field 4 and hide what this test is about).
            controller.Memory.RelocationCooldownUntilTick = int.MaxValue;
            SimulationCommandSink sink = new SimulationCommandSink(sim);
            // Gather orders to field 4 so far. Unit: orders.
            int orders = 0;
            // Ticks run. Unit: ticks. Range: 0 to 60.
            for (int i = 0; i < 60; i++)
            {
                controller.Think(sim, sink);
                foreach (Command command in controller.LastCommands)
                {
                    orders += command is GatherCommand gather && gather.FieldIndex == 4 ? 1 : 0;
                }
                sim.Step();
            }
            controller.Think(sim, sink);
            remembered = controller.Memory.KnownFieldRemaining[4];
            return orders;
        }

        /// <summary>F-8: two worker losses near the depot let rule SD build a Perch Tower past the value cap (no army); one loss does not.</summary>
        [Test]
        public void F8_WorkerLossExemption()
        {
            Assert.That(TowerBuilt(2), Is.True);
            Assert.That(TowerBuilt(1), Is.False);
        }

        /// <summary>Runs F-8 with the given number of logged losses and returns true when a Perch Tower build was decided.</summary>
        private bool TowerBuilt(int losses)
        {
            Simulation sim = Base(out EntityId depot);
            // Two builders: the roost of the build order takes one, the tower the other.
            Spawn(sim, "skai.fledgling", P0, "16.5", "9.5");
            Spawn(sim, "skai.fledgling", P0, "16.5", "10.5");
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(1000, 1000);
            while (sim.Clock.Tick < 600)
            {
                sim.Step();
            }
            AiMemory memory = new AiMemory();
            // Twenty seconds ago, in ticks.
            int ago = sim.Clock.Tick - 400;
            memory.RaidLog[depot] = new List<int> { sim.Clock.Tick - 200 };
            memory.LastRaidTick[depot] = sim.Clock.Tick - 200;
            memory.RaidPosition[depot] = At("21.5", "9.5");
            for (int i = 0; i < losses; i++)
            {
                memory.WorkerLossLog.Add((ago, At("20.5", "9.5")));
            }
            return Decide(sim, S4F, memory).Exists(i => i.Kind == AiIntentKind.Build && i.DefinitionId == "skai.perch_tower");
        }

        /// <summary>F-9: a worker of the previous think that no longer exists is logged with its last position and this tick, and forgotten.</summary>
        [Test]
        public void F9_LossDetection()
        {
            Simulation sim = Base(out _);
            AiMemory memory = new AiMemory();
            EntityId gone = new EntityId(9999);
            memory.OwnWorkerPositions[gone] = At("20.5", "9.5");
            Decide(sim, S4F, memory);
            Assert.That(memory.WorkerLossLog.Count, Is.EqualTo(1));
            Assert.That(memory.WorkerLossLog[0].Tick, Is.EqualTo(sim.Clock.Tick));
            Assert.That(memory.WorkerLossLog[0].Position, Is.EqualTo(At("20.5", "9.5")));
            Assert.That(memory.OwnWorkerPositions.ContainsKey(gone), Is.False);
        }

        /// <summary>F-10: an unreachable site is skipped for 120 s and chosen again after; with forget 0 it is never chosen.</summary>
        [Test]
        public void F10_UnreachableSiteForgets()
        {
            Simulation sim = Base(out _);
            Spawn(sim, "skai.fledgling", P0, "9.5", "9.5");
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(1000, 1000);
            // The site the roost goes to with nothing remembered.
            AiIntent? first = Decide(sim, S4F, new AiMemory()).Find(i => i.Kind == AiIntentKind.Build && i.DefinitionId == "skai.roost");
            Assert.That(first, Is.Not.Null);
            // Tile index of that site.
            int tile = first!.TileY * sim.Terrain.Width + first.TileX;
            Assert.That(RoostAt(sim, S4F, tile, sim.Clock.Tick - 119 * 20), Is.False, "119 s: still skipped");
            Assert.That(RoostAt(sim, S4F, tile, sim.Clock.Tick - 121 * 20), Is.True, "121 s: forgotten");
            AiProfile never = S4FEdited("\"unreachable_site_forget_seconds\": 120,", "\"unreachable_site_forget_seconds\": 0,");
            Assert.That(RoostAt(sim, never, tile, sim.Clock.Tick - 100000), Is.False, "forget 0: never");
        }

        /// <summary>True when the roost is built at tile <paramref name="tile"/> by a think with that tile remembered as unreachable since <paramref name="added"/>.</summary>
        private static bool RoostAt(Simulation sim, AiProfile profile, int tile, int added)
        {
            AiMemory memory = new AiMemory();
            memory.UnreachableSites[tile] = added;
            return Decide(sim, profile, memory).Exists(i => i.Kind == AiIntentKind.Build && i.DefinitionId == "skai.roost" && i.TileY * sim.Terrain.Width + i.TileX == tile);
        }

        /// <summary>F-11: the F-1 and F-5 scenarios run twice give identical intents and memory.</summary>
        [Test]
        public void F11_Deterministic()
        {
            Assert.That(Replay(), Is.EqualTo(Replay()));
        }

        /// <summary>Runs F-1 and F-5 in fresh simulations and returns the intents and the danger memory as text.</summary>
        private string Replay()
        {
            // The text being built.
            string text = string.Empty;
            Simulation one = Base(out _);
            Gatherers(one);
            Spawn(one, "skai.windrunner", P1, "21.5", "12.5");
            AiMemory memoryOne = new AiMemory();
            text += Text(Decide(one, S4F, memoryOne)) + string.Join(",", memoryOne.FieldDangerUntilTick);
            Simulation two = Base(out _);
            Spawn(two, "skai.talon_archer", P0, "14.5", "8.5");
            Spawn(two, "skai.windrunner", P1, "20.5", "8.5");
            Spawn(two, "skai.windrunner", P1, "20.5", "8.5");
            AiMemory memoryTwo = new AiMemory();
            text += Text(Decide(two, S4F, memoryTwo)) + string.Join(",", memoryTwo.DefendGroup);
            return text;
        }
    }
}
