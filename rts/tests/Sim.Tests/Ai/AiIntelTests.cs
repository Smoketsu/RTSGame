// rts/tests/Sim.Tests/Ai/AiIntelTests.cs
// Tests B-1 to B-13 of rts/docs/specs/skirmish_ai_v3.md package B (know the enemy): scouting (rule 4c: timing, unit, route, steering,
// death, borrowed flyer, army exclusion) and the enemy model (sightings, decay, kills, implied types, fog, bounds, determinism,
// validation). Part of Rts.Sim.Tests. Every test builds a real greybox_1v1 simulation (AiWorldView has no public constructor), spawns
// units with script commands and runs AiBrain.Decide or AiIntel on the resulting view.
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
    /// <summary>Skirmish AI v3 package B: scouting and the enemy model.</summary>
    [TestFixture]
    public class AiIntelTests
    {
        /// <summary>Player slot 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and removals. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The shipped stage 1 profile.</summary>
        private static AiProfile S1 => AiProfile.FromDataSet(Data, "aggressive_s1");

        /// <summary>The text of the shipped stage 1 profile, with line endings normalised.</summary>
        private static string S1Text => File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s1.json")).Replace("\r\n", "\n");

        /// <summary>The stage 1 profile with text edits applied (each old text must occur once).</summary>
        private static AiProfile S1Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = S1Text;
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s1.json", Data.Content);
        }

        /// <summary>A stage 1 profile whose compositions need a building the base does not have, so only rule S trains army units.</summary>
        private static AiProfile ScoutOnly()
        {
            return S1Edited("{ \"skai.windrunner\": 3, \"skai.talon_archer\": 1, \"skai.roc_rider\": 1 }", "{ \"skai.talon_archer\": 1 }",
                "{ \"water.grasper\": 3, \"water.shellback\": 1, \"water.ink_lobber\": 1 }", "{ \"water.shellback\": 1 }");
        }

        /// <summary>A started greybox_1v1 simulation with the given factions in slots 0 and 1 (cycles off, nothing spawned).</summary>
        private static Simulation Greybox(string faction0 = "skai", string faction1 = "skai")
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
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

        /// <summary>Removes a unit with a script command and steps once.</summary>
        private void Remove(Simulation sim, EntityId unit)
        {
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, unit));
            sim.Step();
        }

        /// <summary>Steps the simulation until its clock reads <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Sets the stock of a player (test-only access to the internal setter).</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>A Skai base for P0: HQ at (7, 7), six workers (one gathering, so there is income) and one Roost. Returns the Roost's ID.</summary>
        private EntityId SkaiBase(Simulation sim)
        {
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            // First worker, sent to gather so the player has income.
            EntityId gatherer = EntityId.None;
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                EntityId worker = Spawn(sim, "skai.fledgling", P0, "6", (8 + i / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
                gatherer = i == 0 ? worker : gatherer;
            }
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, gatherer, 0));
            sim.Step();
            EntityId roost = Spawn(sim, "skai.roost", P0, "13.5", "6.5");
            SetStock(sim, P0, 5000, 0);
            return roost;
        }

        /// <summary>Decides for a player with the given memory and generator.</summary>
        private static List<AiIntent> Decide(Simulation sim, PlayerId player, AiProfile profile, AiMemory memory, DeterministicRandom? rng = null)
        {
            // The faction's plan.
            AiFactionPlan plan = profile.RequireFaction(sim.Players.TryGet(player, out Player? self) ? self.FactionId : "skai");
            return AiBrain.Decide(AiWorldView.Build(sim, player, memory.KnownFieldRemaining), profile, plan, memory, rng ?? new DeterministicRandom(1));
        }

        /// <summary>The intents of one kind.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            List<AiIntent> found = new List<AiIntent>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == kind)
                {
                    found.Add(intent);
                }
            }
            return found;
        }

        /// <summary>The Train intents for one definition.</summary>
        private static int TrainsOf(List<AiIntent> intents, string definition)
        {
            // Matches counted. Unit: intents.
            int count = 0;
            foreach (AiIntent intent in Of(intents, AiIntentKind.Train))
            {
                count += intent.DefinitionId == definition ? 1 : 0;
            }
            return count;
        }

        /// <summary>B-1: no scout order at 39 s, a scout is trained from 40 s (Windrunner at the Roost; Reef Runner at the Tidepool); never a worker.</summary>
        [Test]
        public void B1_ScoutTimingAndUnit()
        {
            // Skai: 39 s is tick 780, 40 s is tick 800.
            Simulation skai = Greybox();
            EntityId roost = SkaiBase(skai);
            StepTo(skai, 780);
            Assert.That(TrainsOf(Decide(skai, P0, ScoutOnly(), new AiMemory()), "skai.windrunner"), Is.EqualTo(0));
            StepTo(skai, 800);
            List<AiIntent> at40 = Decide(skai, P0, ScoutOnly(), new AiMemory());
            Assert.That(TrainsOf(at40, "skai.windrunner"), Is.EqualTo(1));
            Assert.That(Of(at40, AiIntentKind.Train).Exists(i => i.DefinitionId == "skai.windrunner" && i.Unit == roost), Is.True);
            // Water: the Tidepool trains the Reef Runner.
            Simulation water = Greybox("water", "skai");
            Spawn(water, "water.grand_shell", P0, "7", "7");
            EntityId worker = Spawn(water, "water.tender", P0, "6", "8");
            water.Submit(new GatherCommand(water.Clock.Tick, P0, _sequence++, worker, 0));
            water.Step();
            EntityId tidepool = Spawn(water, "water.tidepool", P0, "13.5", "6.5");
            SetStock(water, P0, 5000, 0);
            StepTo(water, 800);
            List<AiIntent> waterAt40 = Decide(water, P0, ScoutOnly(), new AiMemory());
            Assert.That(Of(waterAt40, AiIntentKind.Train).Exists(i => i.DefinitionId == "water.reef_runner" && i.Unit == tidepool), Is.True);
        }

        /// <summary>B-2: the route from the greybox Skai HQ: the enemy spawn, then the far fields sorted from it with merging.</summary>
        [Test]
        public void B2_Route()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            AiMemory memory = new AiMemory { OwnSpawnIndex = 0 };
            List<FixVec2> route = AiIntel.BuildRoute(AiWorldView.Build(sim, P0), S1, memory, FixVec2.FromInts(7, 7));
            string[] expected = { "56,56", "42.5,53.5", "51.5,14.5", "12.5,49.5", "21.5,10.5" };
            Assert.That(route.Count, Is.EqualTo(expected.Length));
            // Waypoint index. Range: 0 to 4.
            for (int i = 0; i < expected.Length; i++)
            {
                string[] parts = expected[i].Split(',');
                Assert.That(route[i].X == Fix64.Parse(parts[0]) && route[i].Y == Fix64.Parse(parts[1]), Is.True, "waypoint " + i + " was " + route[i].X + "," + route[i].Y);
            }
        }

        /// <summary>B-3: a visible waypoint advances the route (an order to the next one); a scout that is moving and not at a new waypoint gets no order.</summary>
        [Test]
        public void B3_Steer()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            // The scout stands on waypoint 0 (the enemy spawn), which therefore is visible.
            EntityId scout = Spawn(sim, "skai.windrunner", P0, "56", "56");
            StepTo(sim, 800);
            AiMemory memory = new AiMemory();
            List<AiIntent> first = Decide(sim, P0, ScoutOnly(), memory);
            Assert.That(memory.ScoutId, Is.EqualTo(scout));
            Assert.That(memory.ScoutWaypoint, Is.GreaterThanOrEqualTo(1));
            Assert.That(Of(first, AiIntentKind.Move).Exists(i => i.Unit == scout && i.Point.X == memory.ScoutRoute[memory.ScoutWaypoint].X && i.Point.Y == memory.ScoutRoute[memory.ScoutWaypoint].Y), Is.True);
            // Now a scout that is moving towards a waypoint it cannot see: no new order. Rebuild the situation with the scout far away and moving.
            Simulation far = Greybox();
            SkaiBase(far);
            EntityId runner = Spawn(far, "skai.windrunner", P0, "20", "20");
            StepTo(far, 800);
            AiMemory moving = new AiMemory();
            Assert.That(Of(Decide(far, P0, ScoutOnly(), moving), AiIntentKind.Move).Exists(i => i.Unit == runner), Is.True, "the assignment orders a move");
            far.Submit(new MoveUnitCommand(far.Clock.Tick, P0, _sequence++, runner, FixVec2.FromInts(56, 56)));
            far.Step();
            Assert.That(Of(Decide(far, P0, ScoutOnly(), moving), AiIntentKind.Move).Exists(i => i.Unit == runner), Is.False);
        }

        /// <summary>B-4: a dead scout counts as a loss and nothing is trained for 30 s; after 3 losses none is trained, an idle Roc Rider is borrowed instead.</summary>
        [Test]
        public void B4_Death()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            StepTo(sim, 800);
            AiMemory memory = new AiMemory { ScoutId = new EntityId(99999) };
            Assert.That(TrainsOf(Decide(sim, P0, ScoutOnly(), memory), "skai.windrunner"), Is.EqualTo(0));
            Assert.That(memory.ScoutLosses, Is.EqualTo(1));
            Assert.That(memory.ScoutId, Is.EqualTo(EntityId.None));
            StepTo(sim, 800 + 599);
            Assert.That(TrainsOf(Decide(sim, P0, ScoutOnly(), memory), "skai.windrunner"), Is.EqualTo(0));
            StepTo(sim, 800 + 600);
            Assert.That(TrainsOf(Decide(sim, P0, ScoutOnly(), memory), "skai.windrunner"), Is.EqualTo(1));
            // Three losses: no more training; a Roc Rider (cost 200 at most 250) is borrowed.
            AiMemory spent = new AiMemory { ScoutId = new EntityId(99999), ScoutLosses = 2 };
            EntityId roc = Spawn(sim, "skai.roc_rider", P0, "10", "10");
            StepTo(sim, 800 + 1300);
            Decide(sim, P0, ScoutOnly(), spent);
            Assert.That(spent.ScoutLosses, Is.EqualTo(3));
            StepTo(sim, 800 + 1300 + 600);
            List<AiIntent> borrowed = Decide(sim, P0, ScoutOnly(), spent);
            Assert.That(TrainsOf(borrowed, "skai.windrunner"), Is.EqualTo(0));
            Assert.That(spent.ScoutId, Is.EqualTo(roc));
            Assert.That(spent.ScoutBorrowed, Is.True);
            Assert.That(Of(borrowed, AiIntentKind.Move).Exists(i => i.Unit == roc), Is.True);
        }

        /// <summary>B-5: a borrowed flyer that finishes its route is released and gets a Massing rally order.</summary>
        [Test]
        public void B5_BorrowedFlyerReleased()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            EntityId roc = Spawn(sim, "skai.roc_rider", P0, "20", "20");
            StepTo(sim, 800);
            AiMemory memory = new AiMemory { ScoutId = roc, ScoutBorrowed = true, ScoutRoundStartTick = sim.Clock.Tick, ScoutWaypoint = 0 };
            memory.ScoutRoute.Add(FixVec2.FromInts(20, 20));
            Decide(sim, P0, ScoutOnly(), memory);
            Assert.That(memory.ScoutId, Is.EqualTo(EntityId.None));
            Assert.That(memory.ScoutBorrowed, Is.False);
            List<AiIntent> next = Decide(sim, P0, ScoutOnly(), memory);
            Assert.That(Of(next, AiIntentKind.AttackMove).Exists(i => i.Unit == roc), Is.True);
        }

        /// <summary>B-6: in Attack the scout gets no attack-move and is not in the attack group.</summary>
        [Test]
        public void B6_ScoutExcludedFromArmy()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            EntityId scout = Spawn(sim, "skai.windrunner", P0, "10", "10");
            EntityId second = Spawn(sim, "skai.windrunner", P0, "10", "10");
            Spawn(sim, "skai.windrunner", P0, "10", "10");
            StepTo(sim, 800);
            AiProfile profile = S1Edited("\"attack_army_supply\": 8", "\"attack_army_supply\": 2", "\"attack_supply_jitter\": 2", "\"attack_supply_jitter\": 0");
            // A scout between rounds: the route is finished and the next round is not due.
            AiMemory memory = new AiMemory { ScoutId = scout, ScoutRoundStartTick = sim.Clock.Tick };
            List<AiIntent> intents = Decide(sim, P0, profile, memory);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(memory.AttackGroup.Contains(scout), Is.False);
            Assert.That(memory.AttackGroup.Contains(second), Is.True);
            Assert.That(Of(intents, AiIntentKind.AttackMove).Exists(i => i.Unit == scout), Is.False);
            Assert.That(Of(intents, AiIntentKind.AttackMove).Exists(i => i.Unit == second), Is.True);
        }

        /// <summary>Spawns three enemy Roc Riders (one at (30, 30), two at (60, 10)) each watched by an own Windrunner 7 tiles away.</summary>
        private (EntityId A, EntityId B, EntityId C, EntityId WatchA, EntityId WatchBc) RocsInSight(Simulation sim)
        {
            EntityId a = Spawn(sim, "skai.roc_rider", P1, "30", "30");
            EntityId b = Spawn(sim, "skai.roc_rider", P1, "60", "10");
            EntityId c = Spawn(sim, "skai.roc_rider", P1, "60", "10");
            EntityId watchA = Spawn(sim, "skai.windrunner", P0, "23", "30");
            EntityId watchBc = Spawn(sim, "skai.windrunner", P0, "53", "10");
            return (a, b, c, watchA, watchBc);
        }

        /// <summary>A memory that has seen the three Roc Riders of <see cref="RocsInSight"/> at the simulation's current tick.</summary>
        private static AiMemory Primed(Simulation sim, AiProfile profile)
        {
            AiMemory memory = new AiMemory();
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.VisibleEnemies.Count, Is.EqualTo(3), "all three Roc Riders are seen");
            AiIntel.UpdateModel(view, profile, memory);
            return memory;
        }

        /// <summary>B-7: a sighting counts fully for 30 s, then fades to a quarter by 150 s; a smaller sighting never lowers the remembered count.</summary>
        [Test]
        public void B7_SightingAndDecay()
        {
            Simulation sim = Greybox();
            AiProfile profile = S1;
            (EntityId a, EntityId b, EntityId c, EntityId watchA, EntityId watchBc) = RocsInSight(sim);
            AiMemory memory = Primed(sim, profile);
            Assert.That(memory.EnemyTypes["skai.roc_rider"].Count == Fix64.FromInt(3), Is.True);
            // Blind: the watchers are removed. Thirty seconds on, nothing has faded.
            Remove(sim, watchA);
            Remove(sim, watchBc);
            int start = memory.EnemyTypes["skai.roc_rider"].LastSeenTick;
            StepTo(sim, start + 600);
            Assert.That(AiIntel.ModelCounts(AiWorldView.Build(sim, P0), profile, memory)["skai.roc_rider"] == Fix64.FromInt(3), Is.True);
            // 90 s: 3 x (1 - 0.75 x 60 / 120) = 1.875.
            StepTo(sim, start + 1800);
            Assert.That(AiIntel.ModelCounts(AiWorldView.Build(sim, P0), profile, memory)["skai.roc_rider"] == Fix64.Parse("1.875"), Is.True);
            // One visible Roc Rider at 90 s: the effective count stays 1.875 and the remembered count 3.
            EntityId watcher = Spawn(sim, "skai.windrunner", P0, "23", "30");
            AiWorldView seen = AiWorldView.Build(sim, P0);
            Assert.That(seen.VisibleEnemies.Count, Is.EqualTo(1));
            AiIntel.UpdateModel(seen, profile, memory);
            Assert.That(memory.EnemyTypes["skai.roc_rider"].Count == Fix64.FromInt(3), Is.True);
            Assert.That(AiIntel.ModelCounts(seen, profile, memory)["skai.roc_rider"] > Fix64.One, Is.True);
            // 150 s and later (nobody sees them): the floor, 0.75.
            Remove(sim, watcher);
            StepTo(sim, start + 3000);
            Assert.That(AiIntel.ModelCounts(AiWorldView.Build(sim, P0), profile, memory)["skai.roc_rider"] == Fix64.Parse("0.75"), Is.True);
            StepTo(sim, start + 6000);
            Assert.That(AiIntel.ModelCounts(AiWorldView.Build(sim, P0), profile, memory)["skai.roc_rider"] == Fix64.Parse("0.75"), Is.True);
            Assert.That(a.IsValid && b.IsValid && c.IsValid, Is.True);
        }

        /// <summary>B-8: two kills of units seen last think lower the count to 1; a kill of an ID never seen changes nothing.</summary>
        [Test]
        public void B8_Kills()
        {
            Simulation sim = Greybox();
            AiProfile profile = S1;
            (EntityId a, EntityId b, EntityId c, EntityId watchA, EntityId watchBc) = RocsInSight(sim);
            AiMemory withKills = Primed(sim, profile);
            AiMemory withoutKills = Primed(sim, profile);
            // Two of the three die (and the view shows one left).
            Remove(sim, b);
            Remove(sim, c);
            AiWorldView after = AiWorldView.Build(sim, P0);
            Assert.That(after.VisibleEnemies.Count, Is.EqualTo(1));
            withKills.PendingEnemyKills.Add(b);
            withKills.PendingEnemyKills.Add(c);
            withKills.PendingEnemyKills.Add(new EntityId(99999));
            AiIntel.UpdateModel(after, profile, withKills);
            AiIntel.UpdateModel(after, profile, withoutKills);
            Assert.That(withKills.EnemyTypes["skai.roc_rider"].Count == Fix64.One, Is.True);
            Assert.That(withoutKills.EnemyTypes["skai.roc_rider"].Count == Fix64.FromInt(3), Is.True);
            Assert.That(withKills.PendingEnemyKills.Count, Is.EqualTo(0));
            // A kill of an ID never seen changes nothing.
            withoutKills.PendingEnemyKills.Add(new EntityId(88888));
            AiIntel.UpdateModel(after, profile, withoutKills);
            Assert.That(withoutKills.EnemyTypes["skai.roc_rider"].Count == Fix64.FromInt(3), Is.True);
            Assert.That(a.IsValid && watchA.IsValid && watchBc.IsValid, Is.True);
        }

        /// <summary>B-9: a known enemy Roost implies one Windrunner and one Roc Rider, so air supply 3.</summary>
        [Test]
        public void B9_Implied()
        {
            Simulation sim = Greybox();
            AiProfile profile = S1;
            AiMemory memory = new AiMemory();
            memory.KnownEnemyStructures.Add(new EntityId(500), new KnownStructure { DefinitionId = "skai.roost", Position = FixVec2.FromInts(50, 50) });
            AiWorldView view = AiWorldView.Build(sim, P0);
            SortedDictionary<string, Fix64> counts = AiIntel.ModelCounts(view, profile, memory);
            Assert.That(counts["skai.windrunner"] == Fix64.One, Is.True);
            Assert.That(counts["skai.roc_rider"] == Fix64.One, Is.True);
            Assert.That(AiIntel.AirSupply(view, profile, memory) == Fix64.FromInt(3), Is.True);
        }

        /// <summary>
        /// B-10 and B-11: with an enemy Roc Rider kept in fog for 200 thinks the model reports air supply 0 at every think and no think orders a
        /// worker as scout (B-1); the think after a watcher sees it reports 3. Over 2000 thinks the memory stays bounded (B-11).
        /// </summary>
        [Test]
        public void B10_Fog_B11_Bounds()
        {
            Simulation sim = Greybox();
            AiProfile profile = S1;
            SkaiBase(sim);
            // In the far corner no scout route waypoint comes within sight.
            EntityId roc = Spawn(sim, "skai.roc_rider", P1, "62", "2");
            SkirmishAiController controller = new SkirmishAiController(P0, profile);
            controller.OnMatchStart(sim);
            // Thinks run so far. Unit: thinks. Range: 0 to 2000.
            int thinks = 0;
            bool revealed = false;
            while (thinks < 2000)
            {
                SimulationCommandSink sink = new SimulationCommandSink(sim);
                controller.Think(sim, sink);
                if (controller.IsThinkTick(sim.Clock.Tick))
                {
                    thinks++;
                    foreach (Command command in controller.LastCommands)
                    {
                        if (command is MoveUnitCommand move)
                        {
                            Assert.That(sim.Units.TryGet(move.Unit, out Unit? mover) && mover.Definition.Economy.CanGather, Is.False, "no worker is ordered as scout");
                        }
                    }
                    AiWorldView view = AiWorldView.Build(sim, P0);
                    Assert.That(controller.Memory.PendingEnemyKills.Count, Is.EqualTo(0));
                    Assert.That(controller.Memory.EnemyTypes.Count, Is.LessThanOrEqualTo(Data.Content.Units.Count));
                    Assert.That(controller.Memory.ScoutRoute.Count, Is.LessThanOrEqualTo(8));
                    if (thinks <= 200)
                    {
                        Assert.That(view.IsVisible(62, 2), Is.False, "the Roc Rider stays in fog");
                        Assert.That(AiIntel.AirSupply(view, profile, controller.Memory) == Fix64.Zero, Is.True, "think " + thinks);
                    }
                    else if (thinks == 201)
                    {
                        // A watcher of the AI's team now sees the Roc Rider; the following think must know it.
                        Spawn(sim, "skai.windrunner", P0, "56", "2");
                        revealed = true;
                    }
                    else if (revealed && thinks == 203)
                    {
                        Assert.That(controller.Memory.EnemyTypes.ContainsKey("skai.roc_rider"), Is.True);
                        Assert.That(controller.Memory.ModelAirKnownTick, Is.GreaterThan(0));
                    }
                }
                sim.Step();
            }
            Assert.That(roc.IsValid, Is.True);
        }

        /// <summary>B-12: the same view, profile, memory and seed give the same intents and memory twice, and the model and scouting draw no random numbers.</summary>
        [Test]
        public void B12_Determinism()
        {
            Simulation sim = Greybox();
            SkaiBase(sim);
            RocsInSight(sim);
            StepTo(sim, 900);
            List<AiIntent>[] runs = new List<AiIntent>[2];
            AiMemory[] memories = { new AiMemory(), new AiMemory() };
            // Run number. Range: 0 to 1.
            for (int run = 0; run < 2; run++)
            {
                runs[run] = Decide(sim, P0, S1, memories[run], new DeterministicRandom(7));
            }
            Assert.That(runs[1].Count, Is.EqualTo(runs[0].Count));
            // Intent index. Range: 0 to count - 1.
            for (int i = 0; i < runs[0].Count; i++)
            {
                Assert.That(runs[1][i].Kind, Is.EqualTo(runs[0][i].Kind));
                Assert.That(runs[1][i].Unit, Is.EqualTo(runs[0][i].Unit));
                Assert.That(runs[1][i].DefinitionId, Is.EqualTo(runs[0][i].DefinitionId));
                Assert.That(runs[1][i].Point == runs[0][i].Point, Is.True);
            }
            Assert.That(memories[1].ScoutId, Is.EqualTo(memories[0].ScoutId));
            Assert.That(memories[1].ScoutWaypoint, Is.EqualTo(memories[0].ScoutWaypoint));
            Assert.That(memories[1].ScoutRoute.Count, Is.EqualTo(memories[0].ScoutRoute.Count));
            Assert.That(memories[1].EnemyTypes.Count, Is.EqualTo(memories[0].EnemyTypes.Count));
            // Draw counts: the stage 1 profile draws exactly as often as v2 (only the attack threshold jitter).
            DeterministicRandom v2 = new DeterministicRandom(7);
            DeterministicRandom s1 = new DeterministicRandom(7);
            Decide(sim, P0, AiProfile.FromDataSet(Data, "aggressive"), new AiMemory(), v2);
            Decide(sim, P0, S1, new AiMemory(), s1);
            Assert.That(s1.DrawCount, Is.EqualTo(v2.DrawCount));
        }

        /// <summary>B-13: a worker scout unit and a counter-mix switch without the enemy model are rejected with the file and field names.</summary>
        [Test]
        public void B13_Validation()
        {
            string worker = S1Text.Replace("\"scout_unit\": \"water.reef_runner\"", "\"scout_unit\": \"water.tender\"");
            DataValidationException workerError = Expect.Throws<DataValidationException>(() => AiProfile.Parse(worker, "ai/aggressive_s1.json", Data.Content));
            Assert.That(workerError.Message, Does.Contain("factions.water.scout_unit"));
            Assert.That(workerError.Message, Does.Contain("worker"));
            string mix = S1Text.Replace("\"use_counter_mix\": false", "\"use_counter_mix\": true").Replace("\"use_enemy_model\": true", "\"use_enemy_model\": false");
            DataValidationException mixError = Expect.Throws<DataValidationException>(() => AiProfile.Parse(mix, "ai/aggressive_s1.json", Data.Content));
            Assert.That(mixError.Message, Does.Contain("use_counter_mix"));
            Assert.That(mixError.Message, Does.Contain("use_enemy_model"));
        }
    }
}
