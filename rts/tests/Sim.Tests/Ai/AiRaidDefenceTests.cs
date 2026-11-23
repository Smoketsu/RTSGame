// rts/tests/Sim.Tests/Ai/AiRaidDefenceTests.cs
// Tests of rts/docs/specs/ai_production_defence.md packages PD-3 (rules RS and RW: RS-1 to RS-4, RW-1 to RW-5, D-1 for RW) and PD-4 (rule RA: RA-1 to RA-4, D-1 for RA). Part of Rts.Sim.Tests.
// Raid common setup (spec section 8): a real greybox_1v1 simulation, P0 Skai with its Great Aerie at the map spawn (8, 8), a complete skai.perch_depot of P0 at (18.5, 7.5), the map explored for P0, three fledglings gathering
// field 6 at (21, 10); the other fields near the bases are 0 at (4, 10), 1 at (10, 4) and 7 at (21, 4) (checked against rts/data/maps/greybox_1v1.json). P1 is Water; its enemy units are spawned by script. Hits are fed to the memory
// the way SkirmishAiController.ReadEvents does it (an own unit's damage report from a UnitDamagedEvent with a valid attacker). Profiles are aggressive_s5_pd text with the counter-mix, tech, static defence, scouting and walls off.
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
    /// <summary>Packages PD-3 (raid workers) and PD-4 (raid response) of ai_production_defence.md.</summary>
    [TestFixture]
    public class AiRaidDefenceTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the raider).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and orders. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A hand-made profile: aggressive_s5_pd with the counter-mix, tech, static defence, scouting and walls off, then the old/new text pairs applied (each old text must occur).</summary>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile Hand(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_pd.json")).Replace("\r\n", "\n");
            // Switches written off so only the rule under test acts.
            string[] off = { "use_counter_mix", "use_rare_priority", "use_predictor_mix", "use_tech_on_demand", "use_static_defence", "use_scouting", "use_walls" };
            foreach (string key in off)
            {
                text = text.Replace("\"" + key + "\": true", "\"" + key + "\": false");
            }
            // Index of the old text of the pair being applied. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text, Does.Contain(oldNewPairs[i]));
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_pd.json", Data.Content);
        }

        /// <summary>The Skai plan of the raid tests: an empty build order and a Windrunner and Talon Archer composition.</summary>
        private static AiFactionPlan SkaiPlan(AiProfile profile)
        {
            return profile.RequireFaction("skai").WithOrders(new List<AiBuildStep>(), new SortedDictionary<string, int>(StringComparer.Ordinal) { ["skai.windrunner"] = 30, ["skai.talon_archer"] = 20 });
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

        /// <summary>Spawns a P0 unit and orders it to gather field <paramref name="field"/> (the spawn steps the clock, so the order is stamped after it).</summary>
        private void SpawnGather(Simulation sim, string definition, PlayerId owner, string x, string y, int field)
        {
            // The new unit.
            EntityId id = Spawn(sim, definition, owner, x, y);
            sim.Submit(new GatherCommand(sim.Clock.Tick, owner, _sequence++, id, field));
        }

        /// <summary>
        /// The raid common setup: P0's Great Aerie at (8, 8), the depot at (18.5, 7.5) and <paramref name="roosts"/> Roosts (supply, so training is not supply-blocked), three fledglings gathering field 6 (settled for 100 ticks),
        /// <paramref name="onField0"/> and <paramref name="onField1"/> more fledglings gathering fields 0 and 1, and <paramref name="common"/> common.
        /// </summary>
        private Simulation RaidScene(int common, int roosts = 0, int onField0 = 0, int onField1 = 0)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            ExploreKit.ExploreAll(sim);
            Spawn(sim, "skai.great_aerie", P0, "8", "8");
            Spawn(sim, "skai.perch_depot", P0, "18.5", "7.5");
            for (int i = 0; i < roosts; i++)
            {
                Spawn(sim, "skai.roost", P0, (14 + 3 * i).ToString() + ".5", "17.5");
            }
            for (int i = 0; i < 3; i++)
            {
                SpawnGather(sim, "skai.fledgling", P0, (19 + i).ToString() + ".5", "11.5", 6);
            }
            for (int i = 0; i < onField0; i++)
            {
                SpawnGather(sim, "skai.fledgling", P0, (4 + i % 6).ToString() + ".5", "12.5", 0);
            }
            for (int i = 0; i < onField1; i++)
            {
                SpawnGather(sim, "skai.fledgling", P0, (8 + i % 6).ToString() + ".5", "5.5", 1);
            }
            for (int i = 0; i < 100; i++)
            {
                sim.Step();
            }
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(common, 0);
            return sim;
        }

        /// <summary>Records the hits of the events of the last step the way SkirmishAiController.ReadEvents does; returns true when an own unit was hit.</summary>
        private static bool Feed(Simulation sim, AiMemory memory)
        {
            // True once an own unit was hit by a valid attacker.
            bool hit = false;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitDamagedEvent damaged && damaged.Attacker.IsValid && sim.Units.TryGet(damaged.Target, out Unit? target) && target.Owner == P0)
                {
                    memory.RecentDamage[target.Id] = new DamageReport { Tick = damaged.Tick, Position = target.Position };
                    hit = true;
                }
            }
            return hit;
        }

        /// <summary>Steps (feeding hits) until an own unit has been hit; fails after 600 ticks.</summary>
        private static void StepUntilHit(Simulation sim, AiMemory memory)
        {
            for (int i = 0; i < 600; i++)
            {
                sim.Step();
                if (Feed(sim, memory))
                {
                    return;
                }
            }
            throw new AssertionException("no own unit was hit");
        }

        /// <summary>RS-1's start: the setup with a P1 Windrunner at (21.5, 12.5) stepped until it has hit one own unit; the memory holds the hit.</summary>
        private Simulation RaidedScene(int common, AiMemory memory, int roosts = 0, int onField0 = 0, int onField1 = 0)
        {
            Simulation sim = RaidScene(common, roosts, onField0, onField1);
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            StepUntilHit(sim, memory);
            return sim;
        }

        /// <summary>A memory that does not draw an attack threshold on its first think.</summary>
        private static AiMemory NewMemory()
        {
            return new AiMemory { AttackThreshold = 8 };
        }

        /// <summary>Decides for P0 on a fresh view.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, SkaiPlan(profile), memory, new DeterministicRandom(1));
        }

        /// <summary>The own units of the given definition, ascending ID.</summary>
        private static List<EntityId> Own(Simulation sim, string definition)
        {
            List<EntityId> found = new List<EntityId>();
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0, Array.Empty<int>()).OwnUnits)
            {
                if (unit.Definition.Id == definition)
                {
                    found.Add(unit.Id);
                }
            }
            return found;
        }

        /// <summary>The Gather intents as "worker>field" text.</summary>
        private static List<string> Gathers(List<AiIntent> intents)
        {
            List<string> found = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Gather)
                {
                    found.Add(intent.Unit.Value + ">" + intent.Field);
                }
            }
            return found;
        }

        /// <summary>The intents as text (kind, unit, definition, field, point), for equality checks.</summary>
        private static string Key(List<AiIntent> intents)
        {
            return string.Join(";", intents.ConvertAll(i => i.Kind + ":" + i.Unit.Value + ":" + i.DefinitionId + ":" + i.Field + ":" + i.Point.X.ToString() + "," + i.Point.Y.ToString()));
        }

        /// <summary>The raid memory as text, for equality checks.</summary>
        private static string RaidKey(AiMemory memory)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<EntityId, (int Tick, FixVec2 Position)> raid in memory.RaidHits)
            {
                parts.Add(raid.Key.Value + "@" + raid.Value.Tick + "(" + raid.Value.Position.X.ToString() + "," + raid.Value.Position.Y.ToString() + ")");
            }
            return string.Join(",", parts) + "|scan " + memory.RaidScanTick + "|workers " + memory.OwnWorkerPositions.Count;
        }

        // ------------------------------------------------------------------ Rule RS

        /// <summary>RS-1: a hit on a worker opens a raid at the depot with that hit's tick and the worker's position.</summary>
        [Test]
        public void RS1_HitOpensRaid()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            Assert.That(memory.RecentDamage.Count, Is.EqualTo(1));
            DamageReport hit = default!;
            foreach (DamageReport report in memory.RecentDamage.Values)
            {
                hit = report;
            }
            Decide(sim, Hand(), memory);
            List<EntityId> depot = Own(sim, "skai.perch_depot");
            Assert.That(memory.RaidHits.Count, Is.EqualTo(1));
            Assert.That(memory.RaidHits.ContainsKey(depot[0]), Is.True);
            Assert.That(memory.RaidHits[depot[0]], Is.EqualTo((hit.Tick, hit.Position)));
        }

        /// <summary>RS-2: an enemy Windrunner that is seen beside the workers but has not hit anything opens no raid, closes no field and re-orders no worker (package F's failure mode).</summary>
        [Test]
        public void RS2_SightingAloneDoesNot()
        {
            Simulation sim = RaidScene(500);
            // The raider is placed and Decide runs before any hit is read.
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            AiMemory memory = NewMemory();
            Assert.That(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining).VisibleEnemies.Count, Is.EqualTo(1), "the raider is seen");
            List<AiIntent> intents = Decide(sim, Hand(), memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(Gathers(intents), Is.Empty, "no worker is re-ordered");
            Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.Move), Is.Empty);
        }

        /// <summary>RS-3: a worker that was in the memory and is gone is a loss at its last position, at this tick.</summary>
        [Test]
        public void RS3_LossCounts()
        {
            Simulation sim = RaidScene(500);
            AiMemory memory = NewMemory();
            memory.OwnWorkerPositions[new EntityId(9999)] = new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5"));
            Decide(sim, Hand(), memory);
            List<EntityId> depot = Own(sim, "skai.perch_depot");
            Assert.That(memory.RaidHits.Count, Is.EqualTo(1));
            Assert.That(memory.RaidHits[depot[0]], Is.EqualTo((sim.Clock.Tick, new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5")))));
            Assert.That(memory.OwnWorkerPositions.ContainsKey(new EntityId(9999)), Is.False, "the memory is rebuilt from the view");
        }

        /// <summary>Sends the raider away so it hits nothing more, then steps to <paramref name="ticks"/> after the raid's tick.</summary>
        private void SteppedAfterRaid(Simulation sim, int raidTick, int ticks)
        {
            // The raider of P1, found through P1's own view.
            foreach (AiUnitView unit in AiWorldView.Build(sim, P1, Array.Empty<int>()).OwnUnits)
            {
                if (unit.Definition.Id == "skai.windrunner")
                {
                    sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P1, _sequence++, unit.Id, new FixVec2(Fix64.Parse("60"), Fix64.Parse("60"))));
                }
            }
            while (sim.Clock.Tick < raidTick + ticks)
            {
                sim.Step();
            }
        }

        /// <summary>RS-4: after RS-1 and the raider leaving, the raid is active 9 s after the hit and gone 11 s after it; no worker was lost meanwhile.</summary>
        [Test]
        public void RS4_Expiry()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            Decide(sim, profile, memory);
            List<EntityId> depot = Own(sim, "skai.perch_depot");
            int raidTick = memory.RaidHits[depot[0]].Tick;
            SteppedAfterRaid(sim, raidTick, 180);
            Assert.That(Own(sim, "skai.fledgling").Count, Is.EqualTo(3), "no worker died while the raid was being waited out");
            Decide(sim, profile, memory);
            Assert.That(memory.RaidHits.ContainsKey(depot[0]), Is.True, "active at +9 s");
            while (sim.Clock.Tick < raidTick + 220)
            {
                sim.Step();
            }
            Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty, "gone at +11 s");
        }

        // ------------------------------------------------------------------ Rule RW

        /// <summary>RW-1: after RS-1 the three workers on field 6 are ordered to field 1 (6 and 7 are within 8 tiles of the hit); no intent names field 6 or 7.</summary>
        [Test]
        public void RW1_Evacuation()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            List<AiIntent> intents = Decide(sim, Hand(), memory);
            List<string> expected = new List<string>();
            foreach (EntityId worker in Own(sim, "skai.fledgling"))
            {
                expected.Add(worker.Value + ">1");
            }
            Assert.That(Gathers(intents), Is.EqualTo(expected));
            // The switch off: the workers stay where they are.
            AiMemory quiet = NewMemory();
            Simulation off = RaidedScene(500, quiet);
            Assert.That(Gathers(Decide(off, Hand("\"use_raid_workers\": true", "\"use_raid_workers\": false"), quiet)), Is.Empty);
        }

        /// <summary>RW-2: with fields 0 and 1 filled to their caps and the raid on field 6 and 7, no worker is trained into the raid; with the switch off one is.</summary>
        [Test]
        public void RW2_NoReplacementIntoTheRaid()
        {
            // Caps of fields 0 and 1 (workers), read from a quiet probe.
            AiProfile profile = Hand();
            Simulation probeSim = RaidScene(500, 3);
            AiBrain.EconomyProbe? probe = AiBrain.Probe(AiWorldView.Build(probeSim, P0, Array.Empty<int>()), profile, SkaiPlan(profile), NewMemory());
            Assert.That(probe, Is.Not.Null);
            Assert.That(probe!.Served, Is.EqualTo(new List<int> { 0, 1, 6, 7 }));
            int cap0 = probe.ServedCaps[0];
            int cap1 = probe.ServedCaps[1];
            foreach (bool enabled in new[] { true, false })
            {
                AiMemory memory = NewMemory();
                Simulation sim = RaidedScene(500, memory, 3, cap0, cap1);
                AiProfile used = enabled ? profile : Hand("\"use_raid_workers\": true", "\"use_raid_workers\": false");
                List<AiIntent> intents = Decide(sim, used, memory);
                Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.Train && i.DefinitionId == "skai.fledgling").Count, Is.EqualTo(enabled ? 0 : 1), enabled ? "on" : "off");
            }
        }

        /// <summary>RW-3: with raids at the HQ's fields and at Home the workers get no order; with Home beyond every raid and no open field they move to Home.</summary>
        [Test]
        public void RW3_NothingOpen()
        {
            // Raids on the depot (the real one) and the HQ (placed at (7, 7): within 8 tiles of fields 0 and 1 and of Home).
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            Decide(sim, profile, memory);
            EntityId hq = Own(sim, "skai.great_aerie")[0];
            memory.RaidHits[hq] = (sim.Clock.Tick, new FixVec2(Fix64.FromInt(7), Fix64.FromInt(7)));
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(Gathers(intents), Is.Empty);
            Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.Move), Is.Empty);
            // Radius 14 and raids at (24, 4) and (4, 24): every field is within 14 tiles of one, Home (8, 8) is beyond 14 of both.
            AiMemory far = NewMemory();
            Simulation farSim = RaidedScene(500, far);
            AiProfile wide = Hand("\"raid_field_radius_tiles\": 8", "\"raid_field_radius_tiles\": 14");
            Decide(farSim, wide, far);
            far.RaidHits[Own(farSim, "skai.perch_depot")[0]] = (farSim.Clock.Tick, new FixVec2(Fix64.FromInt(24), Fix64.FromInt(4)));
            far.RaidHits[Own(farSim, "skai.great_aerie")[0]] = (farSim.Clock.Tick, new FixVec2(Fix64.FromInt(4), Fix64.FromInt(24)));
            List<AiIntent> moves = Decide(farSim, wide, far);
            Assert.That(Gathers(moves), Is.Empty);
            List<AiIntent> home = moves.FindAll(i => i.Kind == AiIntentKind.Move);
            Assert.That(home.Count, Is.EqualTo(3));
            foreach (AiIntent move in home)
            {
                Assert.That((move.Point - new FixVec2(Fix64.FromInt(8), Fix64.FromInt(8))).LengthSquared() <= Fix64.FromInt(4), Is.True, "next to Home");
            }
        }

        /// <summary>RW-4: after the raid expires an idle fledgling at the depot is sent to field 6 again.</summary>
        [Test]
        public void RW4_Reopening()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            Decide(sim, profile, memory);
            int raidTick = memory.RaidHits[Own(sim, "skai.perch_depot")[0]].Tick;
            SteppedAfterRaid(sim, raidTick, 220);
            EntityId idle = Spawn(sim, "skai.fledgling", P0, "18.5", "9.5");
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(Gathers(intents), Does.Contain(idle.Value + ">6"));
        }

        /// <summary>RW-5: fifteen workers on closed fields and a cap of 12: 12 orders this think, the other 3 the next.</summary>
        [Test]
        public void RW5_CommandCap()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidScene(500, 3);
            // Twelve more workers on the closed fields 6 and 7 (fifteen in all).
            for (int i = 0; i < 12; i++)
            {
                SpawnGather(sim, "skai.fledgling", P0, (17 + i % 6).ToString() + ".5", (13 + i / 6).ToString() + ".5", i < 6 ? 6 : 7);
            }
            for (int i = 0; i < 40; i++)
            {
                sim.Step();
            }
            Spawn(sim, "skai.windrunner", P1, "21.5", "14.5");
            StepUntilHit(sim, memory);
            AiProfile profile = Hand("\"max_commands_per_think\": 40", "\"max_commands_per_think\": 100");
            List<AiIntent> first = Decide(sim, profile, memory);
            Assert.That(Gathers(first).Count, Is.EqualTo(12));
            foreach (AiIntent intent in first)
            {
                if (intent.Kind == AiIntentKind.Gather)
                {
                    sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, intent.Unit, intent.Field));
                }
            }
            sim.Step();
            List<AiIntent> second = Decide(sim, profile, memory);
            Assert.That(Gathers(second).Count, Is.EqualTo(3));
        }

        /// <summary>D-1 (RW): RW-1 run twice from identical inputs gives identical intents and raid memory.</summary>
        [Test]
        public void D1_RaidWorkersTwice()
        {
            // Intents and raid memory of each run.
            string[] runs = new string[2];
            for (int run = 0; run < 2; run++)
            {
                AiMemory memory = NewMemory();
                Simulation sim = RaidedScene(500, memory);
                runs[run] = Key(Decide(sim, Hand(), memory)) + "|" + RaidKey(memory);
            }
            Assert.That(runs[0], Does.Contain("Gather"));
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }

        // ------------------------------------------------------------------ Rule RA

        /// <summary>The profile of the RA tests: aggressive_s5_pd (use_raid_response on) with unit control off, so the units under test are not taken out of the defence by the fall-back and keep-distance rules; then the old/new pairs.</summary>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile RaHand(params string[] oldNewPairs)
        {
            // The pairs with unit control switched off first.
            List<string> pairs = new List<string> { "\"use_unit_control\": true", "\"use_unit_control\": false" };
            pairs.AddRange(oldNewPairs);
            return Hand(pairs.ToArray());
        }

        /// <summary>The raid scene of RA-1 to RA-3: two P1 Windrunners at <paramref name="x"/>, <paramref name="y"/> stepped until one has hit an own unit; returns the sim and the raiders' IDs.</summary>
        private Simulation Raiders(AiMemory memory, string x, string y, out List<EntityId> raiders)
        {
            Simulation sim = RaidScene(500);
            raiders = new List<EntityId> { Spawn(sim, "skai.windrunner", P1, x, y), Spawn(sim, "skai.windrunner", P1, x, y) };
            StepUntilHit(sim, memory);
            return sim;
        }

        /// <summary>The mean position of some units. Unit: tiles.</summary>
        private static FixVec2 Mean(Simulation sim, List<EntityId> ids)
        {
            Fix64 sumX = Fix64.Zero;
            Fix64 sumY = Fix64.Zero;
            foreach (EntityId id in ids)
            {
                Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
                sumX += unit!.Position.X;
                sumY += unit.Position.Y;
            }
            return new FixVec2(sumX / ids.Count, sumY / ids.Count);
        }

        /// <summary>The predictor advantage of the given own units (and the own HQ when asked) against the given enemy units, with the profile's settings. Unit: ratio.</summary>
        private static Fix64 Advantage(Simulation sim, AiProfile profile, List<EntityId> own, bool withHq, List<EntityId> enemy)
        {
            AiWorldView view = AiWorldView.Build(sim, P0, Array.Empty<int>());
            List<AiUnitView> mine = new List<AiUnitView>();
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if (own.Contains(unit.Id) || (withHq && unit.IsHq))
                {
                    mine.Add(unit);
                }
            }
            List<AiUnitView> foes = new List<AiUnitView>();
            foreach (AiUnitView unit in view.VisibleEnemies)
            {
                if (enemy.Contains(unit.Id))
                {
                    foes.Add(unit);
                }
            }
            return FightPredictor.Predict(FightPredictor.Group(mine), FightPredictor.Group(foes), false, Data.Content.Combat, PredictorSettings.From(profile)).Advantage;
        }

        /// <summary>The AttackMove goals in the intents of units in <paramref name="group"/> (the defend group; the others rally at home), as "x,y" text.</summary>
        private static List<string> AttackGoals(List<AiIntent> intents, ICollection<EntityId> group)
        {
            List<string> goals = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.AttackMove && group.Contains(intent.Unit))
                {
                    goals.Add(intent.Point.X.ToString() + "," + intent.Point.Y.ToString());
                }
            }
            return goals;
        }

        /// <summary>RA-1: the defenders go to the mean of the two raiders that hit (the newest raid), not the mean of all three visible threats in the base; with the switch off they go to the mean of all three.</summary>
        [Test]
        public void RA1_NewestRaid()
        {
            AiMemory memory = NewMemory();
            Simulation sim = Raiders(memory, "20.5", "9.5", out List<EntityId> raiders);
            // A third raider that has hit nothing: tile (6.5, 12.5), 4.7 tiles from the HQ and 14 from the raid.
            EntityId third = Spawn(sim, "skai.windrunner", P1, "6.5", "12.5");
            // Six own Windrunners farther than the focus radius (8 tiles) from every raider, enough to answer the two raiders.
            for (int i = 0; i < 6; i++)
            {
                Spawn(sim, "skai.windrunner", P0, (14 + i).ToString() + ".5", "20.5");
            }
            AiMemory off = NewMemory();
            foreach (KeyValuePair<EntityId, DamageReport> report in memory.RecentDamage)
            {
                off.RecentDamage[report.Key] = new DamageReport { Tick = report.Value.Tick, Position = report.Value.Position };
            }
            List<AiIntent> on = Decide(sim, RaHand(), memory);
            Assert.That(memory.RaidHits.Count, Is.EqualTo(1));
            FixVec2 expected = Mean(sim, raiders);
            Assert.That(AttackGoals(on, memory.DefendGroup), Is.Not.Empty, Key(on));
            Assert.That(AttackGoals(on, memory.DefendGroup), Is.All.EqualTo(expected.X.ToString() + "," + expected.Y.ToString()), Key(on));
            List<EntityId> all = new List<EntityId>(raiders) { third };
            FixVec2 everyone = Mean(sim, all);
            List<AiIntent> old = Decide(sim, RaHand("\"use_raid_response\": true", "\"use_raid_response\": false"), off);
            Assert.That(AttackGoals(old, off.DefendGroup), Is.Not.Empty);
            Assert.That(AttackGoals(old, off.DefendGroup), Is.All.EqualTo(everyone.X.ToString() + "," + everyone.Y.ToString()));
        }

        /// <summary>RA-2: one Talon Archer against two Windrunners is predicted short: it moves home and gets no attack order, and joins the defend group; with the switch off it is sent at the raiders.</summary>
        [Test]
        public void RA2_Hold()
        {
            AiMemory memory = NewMemory();
            Simulation sim = Raiders(memory, "20.5", "9.5", out List<EntityId> raiders);
            EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
            AiProfile profile = RaHand();
            Assert.That(Advantage(sim, profile, new List<EntityId> { talon }, false, raiders), Is.LessThan(profile.DefendMinAdvantage));
            AiMemory quiet = NewMemory();
            foreach (KeyValuePair<EntityId, DamageReport> report in memory.RecentDamage)
            {
                quiet.RecentDamage[report.Key] = new DamageReport { Tick = report.Value.Tick, Position = report.Value.Position };
            }
            List<AiIntent> intents = Decide(sim, profile, memory);
            List<AiIntent> mine = intents.FindAll(i => i.Unit == talon);
            Assert.That(mine.Count, Is.EqualTo(1), Key(mine));
            Assert.That(mine[0].Kind, Is.EqualTo(AiIntentKind.Move));
            Assert.That((mine[0].Point - new FixVec2(Fix64.FromInt(8), Fix64.FromInt(8))).LengthSquared() <= Fix64.FromInt(4), Is.True, "next to Home");
            Assert.That(memory.DefendGroup, Does.Contain(talon));
            List<AiIntent> old = Decide(sim, RaHand("\"use_raid_response\": true", "\"use_raid_response\": false"), quiet).FindAll(i => i.Unit == talon);
            Assert.That(old.Count, Is.EqualTo(1), Key(old));
            Assert.That(old[0].Kind == AiIntentKind.AttackMove || old[0].Kind == AiIntentKind.AttackUnit, Is.True, "switch off: sent at the raiders");
        }

        /// <summary>RA-3: with the raiders at (12.5, 8.5), inside the HQ's weapon range plus 2, the HQ joins the defending side; the branch taken (attack-move when HQ plus Talon Archer reach defend_min_advantage, else hold) matches the predictor's advantage of that pair.</summary>
        [Test]
        public void RA3_HqJoins()
        {
            AiMemory memory = NewMemory();
            Simulation sim = Raiders(memory, "12.5", "8.5", out List<EntityId> raiders);
            EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
            AiProfile profile = RaHand();
            Fix64 alone = Advantage(sim, profile, new List<EntityId> { talon }, false, raiders);
            Fix64 withHq = Advantage(sim, profile, new List<EntityId> { talon }, true, raiders);
            List<AiIntent> mine = Decide(sim, profile, memory).FindAll(i => i.Unit == talon);
            Assert.That(mine.Count, Is.EqualTo(1), Key(mine) + " alone " + alone + " with HQ " + withHq);
            if (withHq >= profile.DefendMinAdvantage)
            {
                Assert.That(mine[0].Kind == AiIntentKind.AttackMove || mine[0].Kind == AiIntentKind.AttackUnit, Is.True, "HQ plus talon is enough: " + withHq);
            }
            else
            {
                Assert.That(mine[0].Kind, Is.EqualTo(AiIntentKind.Move), "HQ plus talon is short: " + withHq);
            }
            // The rule's effect: without the HQ on the fixed side the same view would be judged on the Talon alone.
            Assert.That(withHq, Is.GreaterThan(alone), "the HQ's weapon adds to the defending side");
        }

        /// <summary>RA-4: use_raid_response true with use_measured_defence false fails to load and names the key.</summary>
        [Test]
        public void RA4_Validation()
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => Hand("\"use_measured_defence\": true", "\"use_measured_defence\": false"));
            Assert.That(error.Message, Does.Contain("use_raid_response"));
        }

        /// <summary>D-1 (RA): RA-2 run twice from identical inputs gives identical intents and memory.</summary>
        [Test]
        public void D1_RaidResponseTwice()
        {
            // Intents, raid memory and defend group of each run.
            string[] runs = new string[2];
            for (int run = 0; run < 2; run++)
            {
                AiMemory memory = NewMemory();
                Simulation sim = Raiders(memory, "20.5", "9.5", out List<EntityId> raiders);
                Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
                runs[run] = Key(Decide(sim, RaHand(), memory)) + "|" + RaidKey(memory) + "|" + string.Join(",", memory.DefendGroup);
            }
            Assert.That(runs[0], Does.Contain("Move"));
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }
    }
}
