// rts/tests/Sim.Tests/Ai/AiRaidDefenceTests.cs
// Tests of rts/docs/specs/ai_production_defence.md packages PD-3 (rules RS and RW: RS-1 to RS-4, RW-1 to RW-5, D-1 for RW) and PD-4 (rule RA: RA-1 to RA-4, D-1 for RA), then revision 2 (spec R2.3: R2-H1-1 to R2-H5-1, R2-D-1 and an off-path check). Part of Rts.Sim.Tests.
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

        // ------------------------------------------------------------------ Revision 2 (spec R2.3)

        /// <summary>The raid memory including the revision 2 members, as text, for equality checks.</summary>
        private static string RaidKey2(AiMemory memory)
        {
            // Closed field sets, seen ticks, carried hits and the answered raid as text parts.
            List<string> parts = new List<string>();
            foreach (KeyValuePair<EntityId, SortedSet<int>> closed in memory.RaidClosedFields)
            {
                parts.Add(closed.Key.Value + ":" + string.Join("+", closed.Value));
            }
            // Text parts "drop-off ID@last seen think tick", one per RaidSeenTick entry in ascending ID order. Unit: text. Range: one part per active raid.
            List<string> seen = new List<string>();
            foreach (KeyValuePair<EntityId, int> pair in memory.RaidSeenTick)
            {
                seen.Add(pair.Key.Value + "@" + pair.Value);
            }
            return RaidKey(memory) + "|closed " + string.Join(",", parts) + "|seen " + string.Join(",", seen) + "|carried " + memory.RaidCarriedHits.Count + "|answered " + memory.RaidAnswered.Value;
        }

        /// <summary>The worker caps of the served fields (indices 0, 1, 6, 7 in that order) after the last think, read through the economy probe.</summary>
        private static IReadOnlyList<int> Caps(Simulation sim, AiProfile profile, AiMemory memory)
        {
            AiBrain.EconomyProbe? probe = AiBrain.Probe(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, SkaiPlan(profile), memory);
            Assert.That(probe, Is.Not.Null);
            Assert.That(probe!.Served, Is.EqualTo(new List<int> { 0, 1, 6, 7 }));
            return probe.ServedCaps;
        }

        /// <summary>Orders every P1 Windrunner to (60, 60), far out of sight.</summary>
        private void SendRaidersAway(Simulation sim)
        {
            foreach (AiUnitView unit in AiWorldView.Build(sim, P1, Array.Empty<int>()).OwnUnits)
            {
                if (unit.Definition.Id == "skai.windrunner")
                {
                    sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P1, _sequence++, unit.Id, new FixVec2(Fix64.FromInt(60), Fix64.FromInt(60))));
                }
            }
        }

        /// <summary>R2-H1-1's run: RS-1, then a hit at (14.5, 15.5) on a live fledgling; returns the sim, the memory and the depot, and the text of the second think's intents and raid memory.</summary>
        private string H1Closed(AiMemory memory, out Simulation sim, out EntityId depot)
        {
            sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            Decide(sim, profile, memory);
            depot = Own(sim, "skai.perch_depot")[0];
            Assert.That(memory.RaidClosedFields[depot], Is.EquivalentTo(new[] { 6, 7 }));
            sim.Step();
            // The hit: now, on a live fledgling (the report's position is what counts), 8.9 tiles from the depot and 9.9 from the HQ, more than 8 from the centres of fields 6 (8.5) and 7 and from fields 0 and 1.
            FixVec2 where = new FixVec2(Fix64.Parse("14.5"), Fix64.Parse("15.5"));
            memory.RecentDamage[Own(sim, "skai.fledgling")[0]] = new DamageReport { Tick = sim.Clock.Tick, Position = where, IsArmy = false };
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits[depot], Is.EqualTo((sim.Clock.Tick, where)));
            Assert.That(memory.RaidClosedFields[depot], Is.EquivalentTo(new[] { 6, 7 }));
            IReadOnlyList<int> caps = Caps(sim, profile, memory);
            Assert.That(caps[2], Is.EqualTo(0), "field 6 stays closed");
            Assert.That(caps[3], Is.EqualTo(0), "field 7 stays closed");
            foreach (string gather in Gathers(intents))
            {
                Assert.That(gather, Does.Not.EndWith(">6"));
                Assert.That(gather, Does.Not.EndWith(">7"));
            }
            return Key(intents) + "|" + RaidKey2(memory);
        }

        /// <summary>R2-H1-1: after RS-1, a newer hit 8.9 tiles from the depot moves the raid position, yet fields 6 and 7 stay closed (cap 0) and no Gather names them; revision 1 reopened them.</summary>
        [Test]
        public void R2H1_1_ClosedFieldStaysClosed()
        {
            H1Closed(NewMemory(), out _, out _);
        }

        /// <summary>R2-H1-2: with the raiders gone and raid_active_seconds passed, the raid's field set is gone and field 6 is open again.</summary>
        [Test]
        public void R2H1_2_SetsGoWithTheRaid()
        {
            AiMemory memory = NewMemory();
            H1Closed(memory, out Simulation sim, out EntityId depot);
            AiProfile profile = Hand();
            SteppedAfterRaid(sim, memory.RaidHits[depot].Tick, 220);
            Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(memory.RaidClosedFields, Is.Empty);
            Assert.That(Caps(sim, profile, memory)[2], Is.GreaterThan(0));
        }

        /// <summary>R2-H2-1's run: a visible raider keeps the raid 11 s after the hit; when it leaves, the raid ends 11 s after the last think that saw it. Returns the text of the memory after the first long think.</summary>
        private string H2Seen()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            List<AiIntent> first = Decide(sim, profile, memory);
            EntityId depot = Own(sim, "skai.perch_depot")[0];
            // Tick of the newest hit on the depot's raid, before the second think. Unit: ticks. Range: 0 to now.
            int hitTick = memory.RaidHits[depot].Tick;
            foreach (AiIntent intent in first)
            {
                if (intent.Kind == AiIntentKind.Gather)
                {
                    sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, intent.Unit, intent.Field));
                }
            }
            while (sim.Clock.Tick < hitTick + 220)
            {
                sim.Step();
                // Deviation from the spec text (reported): the spec asserts that no damage report follows RS-1's, but the Windrunner (range 4) keeps hitting the workers for a few ticks while they walk off field 6
                // (a hit at +17 ticks with the literal assertion). The test therefore does not feed later hits to the memory: it models the case the spec names, a raid whose last hit is RS-1's with the raider still in sight.
            }
            List<AiIntent> second = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits.ContainsKey(depot), Is.True, "still active 11 s after the hit");
            Assert.That(memory.RaidHits[depot].Tick, Is.EqualTo(hitTick));
            Assert.That(memory.RaidSeenTick[depot], Is.EqualTo(sim.Clock.Tick));
            Assert.That(Caps(sim, profile, memory)[2], Is.EqualTo(0), "field 6 stays closed");
            // Text of the second think's intents and the revision 2 raid memory, returned for the determinism check. Unit: text. Range: any length.
            string key = Key(second) + "|" + RaidKey2(memory);
            // Clock tick when the raider was last in sight (before it leaves); the raid is gone 11 s after it. Unit: ticks. Range: 0 to now.
            int seenTick = sim.Clock.Tick;
            SendRaidersAway(sim);
            while (sim.Clock.Tick < seenTick + 220)
            {
                sim.Step();
            }
            Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(memory.RaidSeenTick, Is.Empty);
            Assert.That(Caps(sim, profile, memory)[2], Is.GreaterThan(0));
            return key;
        }

        /// <summary>R2-H2-1: a visible raider keeps the raid active past raid_active_seconds (revision 1: gone), and the raid ends 11 s after the raider was last seen.</summary>
        [Test]
        public void R2H2_1_VisibleRaiderKeepsTheRaid()
        {
            H2Seen();
        }

        /// <summary>R2-H2-2: after RS-4's expiry a Windrunner that appears and has hit nothing does not revive the raid: no raid, no seen tick, no closed field, no worker re-ordered.</summary>
        [Test]
        public void R2H2_2_NoRevivalBySighting()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile profile = Hand();
            Decide(sim, profile, memory);
            int raidTick = memory.RaidHits[Own(sim, "skai.perch_depot")[0]].Tick;
            SteppedAfterRaid(sim, raidTick, 220);
            Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Spawn(sim, "skai.windrunner", P1, "21.5", "12.5");
            Assert.That(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining).VisibleEnemies.Count, Is.GreaterThan(0), "the new raider is seen");
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(memory.RaidSeenTick, Is.Empty);
            Assert.That(memory.RaidClosedFields, Is.Empty);
            Assert.That(Gathers(intents), Is.Empty);
        }

        /// <summary>R2-H3-1: a report for a unit that is not alive (9998, hit 2 ticks ago at (20.5, 9.5)) opens a raid when it is not an army unit and none when it is; the carried list is empty after the think.</summary>
        [Test]
        public void R2H3_1_HitOnWorkerThatDied()
        {
            // The raid at the depot of the run with a worker report, or none for an army report.
            foreach (bool army in new[] { false, true })
            {
                Simulation sim = RaidScene(500);
                AiMemory memory = NewMemory();
                FixVec2 where = new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5"));
                memory.RecentDamage[new EntityId(9998)] = new DamageReport { Tick = sim.Clock.Tick - 2, Position = where, IsArmy = army };
                Decide(sim, Hand(), memory);
                Assert.That(memory.RaidCarriedHits, Is.Empty);
                if (army)
                {
                    Assert.That(memory.RaidHits, Is.Empty);
                }
                else
                {
                    Assert.That(memory.RaidHits[Own(sim, "skai.perch_depot")[0]], Is.EqualTo((sim.Clock.Tick - 2, where)));
                }
            }
        }

        /// <summary>R2-H3-2: ReadEvents marks a hit on a Talon Archer as an army hit and a hit on a fledgling as not.</summary>
        [Test]
        public void R2H3_2_ReadEventsMarksArmyHits()
        {
            Simulation sim = RaidScene(500);
            AiProfile profile = Hand();
            SkirmishAiController controller = new SkirmishAiController(P0, profile);
            controller.OnMatchStart(sim);
            ListCommandSink sink = new ListCommandSink(sim.Clock.Tick);
            // A Talon Archer and a Windrunner of P1 far from the bases; the Windrunner hits the archer first.
            EntityId talon = Spawn(sim, "skai.talon_archer", P0, "40.5", "40.5");
            EntityId raider = Spawn(sim, "skai.windrunner", P1, "41.5", "40.5");
            for (int i = 0; i < 400 && !controller.Memory.RecentDamage.ContainsKey(talon); i++)
            {
                controller.Think(sim, sink);
                sim.Step();
            }
            controller.Think(sim, sink);
            Assert.That(controller.Memory.RecentDamage.ContainsKey(talon), Is.True, "the archer was hit");
            Assert.That(controller.Memory.RecentDamage[talon].IsArmy, Is.True);
            // Then a fledgling beside the raider, which is ordered to attack it.
            EntityId worker = Spawn(sim, "skai.fledgling", P0, "41.5", "41.5");
            sim.Submit(new AttackUnitCommand(sim.Clock.Tick, P1, _sequence++, raider, worker));
            for (int i = 0; i < 400 && !controller.Memory.RecentDamage.ContainsKey(worker); i++)
            {
                controller.Think(sim, sink);
                sim.Step();
            }
            controller.Think(sim, sink);
            Assert.That(controller.Memory.RecentDamage.ContainsKey(worker), Is.True, "the fledgling was hit");
            Assert.That(controller.Memory.RecentDamage[worker].IsArmy, Is.False);
        }

        /// <summary>R2-H3-3: R2-H3-1 with both raid switches off: nothing is carried, no raid opens and the report is pruned as before.</summary>
        [Test]
        public void R2H3_3_SwitchesOff()
        {
            Simulation sim = RaidScene(500);
            AiMemory memory = NewMemory();
            memory.RecentDamage[new EntityId(9998)] = new DamageReport { Tick = sim.Clock.Tick - 2, Position = new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5")), IsArmy = false };
            Decide(sim, Hand("\"use_raid_workers\": true", "\"use_raid_workers\": false", "\"use_raid_response\": true", "\"use_raid_response\": false"), memory);
            Assert.That(memory.RaidCarriedHits, Is.Empty);
            Assert.That(memory.RaidHits, Is.Empty);
            Assert.That(memory.RecentDamage.ContainsKey(new EntityId(9998)), Is.False, "pruned as today");
        }

        /// <summary>The H4 scene: P0 Skai with two Talon Archers 11.7 tiles from both raid areas (beyond the focus radius), raid A at the depot from a hit on a field-6 worker (now minus 20 ticks) and raid B at the HQ from a hit on the field-0 worker (now), with <paramref name="countA"/> and <paramref name="countB"/> P1 Windrunners at (21.5, 12.5) and (6.5, 12.5).</summary>
        private Simulation H4Scene(AiMemory memory, int countA, int countB, out List<EntityId> raidersA, out List<EntityId> raidersB, out List<EntityId> archers)
        {
            Simulation sim = RaidScene(500, 0, 1);
            archers = new List<EntityId> { Spawn(sim, "skai.talon_archer", P0, "14.5", "3.5"), Spawn(sim, "skai.talon_archer", P0, "13.5", "3.5") };
            raidersA = new List<EntityId>();
            raidersB = new List<EntityId>();
            for (int i = 0; i < countA; i++)
            {
                raidersA.Add(Spawn(sim, "skai.windrunner", P1, "21.5", "12.5"));
            }
            for (int i = 0; i < countB; i++)
            {
                raidersB.Add(Spawn(sim, "skai.windrunner", P1, "4.5", "18.5"));
            }
            List<EntityId> fledglings = Own(sim, "skai.fledgling");
            memory.RecentDamage[fledglings[0]] = new DamageReport { Tick = sim.Clock.Tick - 20, Position = new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5")) };
            memory.RecentDamage[fledglings[fledglings.Count - 1]] = new DamageReport { Tick = sim.Clock.Tick, Position = new FixVec2(Fix64.Parse("4.5"), Fix64.Parse("12.5")) };
            return sim;
        }

        /// <summary>The goal text of <paramref name="ids"/>' mean, as AttackGoals prints it.</summary>
        private static string GoalOf(Simulation sim, List<EntityId> ids)
        {
            FixVec2 mean = Mean(sim, ids);
            return mean.X.ToString() + "," + mean.Y.ToString();
        }

        /// <summary>R2-H4-1: the defence answers the newest raid (B at the HQ) and keeps answering it when a new hit makes A the newest; revision 1 swung to A.</summary>
        [Test]
        public void R2H4_1_StickyAnswer()
        {
            AiMemory memory = NewMemory();
            Simulation sim = H4Scene(memory, 1, 1, out List<EntityId> raidersA, out List<EntityId> raidersB, out List<EntityId> archers);
            AiProfile profile = RaHand();
            Assert.That(Advantage(sim, profile, archers, false, raidersA), Is.GreaterThanOrEqualTo(profile.DefendMinAdvantage), "enough against A");
            Assert.That(Advantage(sim, profile, archers, false, raidersB), Is.GreaterThanOrEqualTo(profile.DefendMinAdvantage), "enough against B");
            EntityId depot = Own(sim, "skai.perch_depot")[0];
            EntityId hq = Own(sim, "skai.great_aerie")[0];
            List<AiIntent> first = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits.Count, Is.EqualTo(2));
            Assert.That(memory.RaidAnswered, Is.EqualTo(hq), "B is the newest");
            Assert.That(AttackGoals(first, memory.DefendGroup), Is.Not.Empty, Key(first));
            Assert.That(AttackGoals(first, memory.DefendGroup), Is.All.EqualTo(GoalOf(sim, raidersB)), Key(first));
            // A new hit makes A the newest raid.
            sim.Step();
            memory.RecentDamage[Own(sim, "skai.fledgling")[0]] = new DamageReport { Tick = sim.Clock.Tick, Position = new FixVec2(Fix64.Parse("20.5"), Fix64.Parse("9.5")) };
            // Text "x,y" of raider group B's mean position in tiles (Fix64 text), taken before the second think. Unit: tiles. Range: inside the map.
            string goalB = GoalOf(sim, raidersB);
            List<AiIntent> second = Decide(sim, profile, memory);
            Assert.That(memory.RaidHits[depot].Tick, Is.GreaterThan(memory.RaidHits[hq].Tick), "A is now the newest");
            Assert.That(memory.RaidAnswered, Is.EqualTo(hq));
            Assert.That(AttackGoals(second, memory.DefendGroup), Is.All.EqualTo(goalB), "the goal does not move to A: " + Key(second));
        }

        /// <summary>R2-H4-2's run: the newest raid (B, HQ, three Windrunners) is predicted short, the older (A, depot, one Windrunner) is not; the defence answers A. Returns the intents and memory text.</summary>
        private string H4Winnable(out Simulation sim, out AiMemory memory, out List<EntityId> raidersA)
        {
            memory = NewMemory();
            sim = H4Scene(memory, 1, 3, out raidersA, out List<EntityId> raidersB, out List<EntityId> archers);
            AiProfile profile = RaHand();
            Assert.That(Advantage(sim, profile, archers, false, raidersA), Is.GreaterThanOrEqualTo(profile.DefendMinAdvantage), "enough against A");
            Assert.That(Advantage(sim, profile, archers, false, raidersB), Is.LessThan(profile.DefendMinAdvantage), "short against B (the HQ is 10.9 tiles from the raiders, beyond its reach of 6 plus 2)");
            EntityId depot = Own(sim, "skai.perch_depot")[0];
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(memory.RaidAnswered, Is.EqualTo(depot));
            Assert.That(AttackGoals(intents, memory.DefendGroup), Is.Not.Empty, Key(intents));
            Assert.That(AttackGoals(intents, memory.DefendGroup), Is.All.EqualTo(GoalOf(sim, raidersA)));
            Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.Move && archers.Contains(i.Unit)), Is.Empty, "no hold at home");
            return Key(intents) + "|" + RaidKey2(memory) + "|" + string.Join(",", memory.DefendGroup);
        }

        /// <summary>R2-H4-2: an older raid that can be won is answered although the newest cannot; revision 1 held at home.</summary>
        [Test]
        public void R2H4_2_WinnableOlderRaid()
        {
            H4Winnable(out _, out _, out _);
        }

        /// <summary>R2-H4-3: with every raid predicted short the defence holds at home with every candidate, and RaidAnswered is the newest raid.</summary>
        [Test]
        public void R2H4_3_AllShortHolds()
        {
            AiMemory memory = NewMemory();
            Simulation sim = H4Scene(memory, 3, 3, out List<EntityId> raidersA, out List<EntityId> raidersB, out List<EntityId> archers);
            AiProfile profile = RaHand();
            Assert.That(Advantage(sim, profile, archers, false, raidersA), Is.LessThan(profile.DefendMinAdvantage), "short against A");
            Assert.That(Advantage(sim, profile, archers, false, raidersB), Is.LessThan(profile.DefendMinAdvantage), "short against B");
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(memory.RaidAnswered, Is.EqualTo(Own(sim, "skai.great_aerie")[0]));
            foreach (EntityId archer in archers)
            {
                Assert.That(memory.DefendGroup, Does.Contain(archer));
                Assert.That(intents.FindAll(i => i.Unit == archer && i.Kind == AiIntentKind.Move).Count, Is.EqualTo(1), "moves home");
            }
            Assert.That(intents.FindAll(i => i.Kind == AiIntentKind.AttackMove && archers.Contains(i.Unit)), Is.Empty);
        }

        /// <summary>R2-H4-4: after R2-H4-2 the answered raid's raider leaves and the raid expires: RaidAnswered is None or names a raid that is still active.</summary>
        [Test]
        public void R2H4_4_ClearedOnExpiry()
        {
            H4Winnable(out Simulation sim, out AiMemory memory, out List<EntityId> raidersA);
            EntityId depot = Own(sim, "skai.perch_depot")[0];
            foreach (EntityId raider in raidersA)
            {
                sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P1, _sequence++, raider, new FixVec2(Fix64.FromInt(60), Fix64.FromInt(60))));
            }
            int start = sim.Clock.Tick;
            while (sim.Clock.Tick < start + 220)
            {
                sim.Step();
            }
            Decide(sim, RaHand(), memory);
            Assert.That(memory.RaidHits.ContainsKey(depot), Is.False, "the depot raid expired");
            Assert.That(memory.RaidAnswered == EntityId.None || memory.RaidHits.ContainsKey(memory.RaidAnswered), Is.True);
        }

        /// <summary>R2-H5-1: with rare_cap_yields_to_saturation on and fields 0 and 1 full, a hit that closes fields 6 and 7 makes the common-full value true in the same think; before the raid scan it was false (revision 1 kept that).</summary>
        [Test]
        public void R2H5_1_CommonFullAfterTheScan()
        {
            AiProfile profile = Hand();
            Assert.That(profile.RareCapYieldsToSaturation, Is.True, "the candidate has the key on");
            Simulation probeSim = RaidScene(500, 3);
            IReadOnlyList<int> quiet = Caps(probeSim, profile, NewMemory());
            AiMemory memory = NewMemory();
            memory.RareCapActive = true;
            Simulation sim = RaidedScene(500, memory, 3, quiet[0], quiet[1]);
            (bool CommonFull, List<int> Caps)? result = AiBrain.ProbeCommonFull(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, SkaiPlan(profile), memory);
            Assert.That(result, Is.Not.Null);
            Assert.That(memory.RaidClosedFields.Count, Is.EqualTo(1), "the hit closed fields");
            Assert.That(result!.Value.CommonFull, Is.True, "read after the scan: 6 and 7 are closed, 0 and 1 are full");
            // The map has no rare field in reach of this base (served fields are 0, 1, 6 and 7, all common), so only the common-full value is asserted.
            Assert.That(result.Value.Caps[2], Is.EqualTo(0));
        }

        /// <summary>R2-D-1: R2-H1-1, R2-H2-1 and R2-H4-2 run twice from identical inputs give identical intents and memory, the new members included.</summary>
        [Test]
        public void R2D1_Twice()
        {
            // Text result of each run of R2-H1-1 (the closed-field scenario), indexed by run (0 or 1). Unit: text. Range: two entries.
            string[] h1 = new string[2];
            // Text result of each run of R2-H2-1 (the visible-raider scenario), indexed by run (0 or 1). Unit: text. Range: two entries.
            string[] h2 = new string[2];
            // Text result of each run of R2-H4-2 (the winnable-raid scenario), indexed by run (0 or 1). Unit: text. Range: two entries.
            string[] h4 = new string[2];
            for (int run = 0; run < 2; run++)
            {
                h1[run] = H1Closed(NewMemory(), out _, out _);
                h2[run] = H2Seen();
                h4[run] = H4Winnable(out _, out _, out _);
            }
            Assert.That(h1[1], Is.EqualTo(h1[0]));
            Assert.That(h2[1], Is.EqualTo(h2[0]));
            Assert.That(h4[1], Is.EqualTo(h4[0]));
            Assert.That(h1[0], Does.Contain("Gather"));
        }

        /// <summary>The off path: with both raid switches off over a hit and a loss the revision 2 members stay empty and RaidAnswered stays None.</summary>
        [Test]
        public void R2_OffPathLeavesNewMemoryEmpty()
        {
            AiMemory memory = NewMemory();
            Simulation sim = RaidedScene(500, memory);
            AiProfile off = Hand("\"use_raid_workers\": true", "\"use_raid_workers\": false", "\"use_raid_response\": true", "\"use_raid_response\": false");
            Decide(sim, off, memory);
            Assert.That(memory.RaidClosedFields, Is.Empty);
            Assert.That(memory.RaidSeenTick, Is.Empty);
            Assert.That(memory.RaidCarriedHits, Is.Empty);
            Assert.That(memory.RaidAnswered, Is.EqualTo(EntityId.None));
        }

        // ------------------------------------------------------------------ Rule DH (ai_midgame_window.md section 3.3, tests 7 to 9)

        /// <summary>The profile of the DH tests: the RA profile with use_raid_response off and use_defence_hold <paramref name="hold"/>.</summary>
        /// <param name="hold">Value of use_defence_hold. Unit: boolean.</param>
        private static AiProfile DhHand(bool hold)
        {
            return RaHand("\"use_raid_response\": true", "\"use_raid_response\": false, \"use_defence_hold\": " + (hold ? "true" : "false"));
        }

        /// <summary>DH test 7: four Windrunners at (20.5, 9.5) in the base, no active raid (rule RA off); two Talon Archers, one 6.5 tiles from home and one 1.5, together predicted short. Without the switch both are sent at the threat; with it the far one moves home, the near one gets no order, both are in the defend group.</summary>
        [Test]
        public void DH7_ShortDefenceHolds()
        {
            // Memory for the run with the switch on, holding the hit of the raiders. Unit: none (object).
            AiMemory on = NewMemory();
            // The match: P0 Skai with its base, P1 raiders in it. Unit: none (object).
            Simulation sim = Raiders(on, "20.5", "9.5", out List<EntityId> raiders);
            raiders.Add(Spawn(sim, "skai.windrunner", P1, "20.5", "9.5"));
            raiders.Add(Spawn(sim, "skai.windrunner", P1, "20.5", "9.5"));
            // The far candidate (6.5 tiles from home). Unit: entity ID.
            EntityId far = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
            // The near candidate (1.5 tiles from home). Unit: entity ID.
            EntityId near = Spawn(sim, "skai.talon_archer", P0, "9.5", "8.5");
            // Memory for the run with the switch off, holding the same hit. Unit: none (object).
            AiMemory off = NewMemory();
            // One damage report of the first memory. Unit: none (key: entity ID, value: tick and position).
            foreach (KeyValuePair<EntityId, DamageReport> report in on.RecentDamage)
            {
                off.RecentDamage[report.Key] = new DamageReport { Tick = report.Value.Tick, Position = report.Value.Position };
            }
            // The profile with the switch on. Unit: none (object).
            AiProfile holdProfile = DhHand(true);
            Assert.That(Advantage(sim, holdProfile, new List<EntityId> { far, near }, false, raiders), Is.LessThan(holdProfile.DefendMinAdvantage));
            // Intents without the switch: both units attack the threat. Unit: none (list).
            List<AiIntent> old = Decide(sim, DhHand(false), off);
            Assert.That(old.FindAll(i => (i.Unit == far || i.Unit == near) && (i.Kind == AiIntentKind.AttackMove || i.Kind == AiIntentKind.AttackUnit)).Count, Is.EqualTo(2), Key(old));
            // Intents with the switch. Unit: none (list).
            List<AiIntent> held = Decide(sim, holdProfile, on);
            // The intents of the far unit; exactly one. Unit: none (list).
            List<AiIntent> farIntents = held.FindAll(i => i.Unit == far);
            Assert.That(farIntents.Count, Is.EqualTo(1), Key(held));
            Assert.That(farIntents[0].Kind, Is.EqualTo(AiIntentKind.Move));
            Assert.That((farIntents[0].Point - new FixVec2(Fix64.FromInt(8), Fix64.FromInt(8))).LengthSquared() <= Fix64.FromInt(4), Is.True, "next to Home");
            Assert.That(held.FindAll(i => i.Unit == near), Is.Empty, Key(held));
            Assert.That(on.DefendGroup, Does.Contain(far));
            Assert.That(on.DefendGroup, Does.Contain(near));
        }

        /// <summary>DH test 8: as test 7 with one complete Perch Tower (own static defence) beside the threat and Talon Archers added until the tower and the Talons reach defend_min_advantage while the tower alone and the Talons alone are short; then the switch holds nothing and the Talons attack the threat.</summary>
        [Test]
        public void DH8_HoldEndsWithStaticDefence()
        {
            // The AI memory, holding the hit of the raiders. Unit: none (object).
            AiMemory memory = NewMemory();
            // The match: P0 Skai with its base, P1 raiders in it. Unit: none (object).
            Simulation sim = Raiders(memory, "20.5", "9.5", out List<EntityId> raiders);
            // One complete Perch Tower (own static defence) beside the threat centre. Unit: entity ID.
            EntityId tower = Spawn(sim, "skai.perch_tower", P0, "19.5", "7.5");
            // The profile with rule DH on. Unit: none (object).
            AiProfile profile = DhHand(true);
            // Raiders are added until the tower alone is predicted short. Range: up to 12 raiders.
            while (Advantage(sim, profile, new List<EntityId> { tower }, false, raiders) >= profile.DefendMinAdvantage && raiders.Count < 12)
            {
                raiders.Add(Spawn(sim, "skai.windrunner", P1, "20.5", "9.5"));
            }
            Assert.That(Advantage(sim, profile, new List<EntityId> { tower }, false, raiders), Is.LessThan(profile.DefendMinAdvantage), "the tower alone is short");
            // The Talon Archers spawned. Unit: entity IDs. Range: 0 to 8.
            List<EntityId> talons = new List<EntityId>();
            // The tower and the Talons together. Unit: entity IDs.
            List<EntityId> side = new List<EntityId> { tower };
            while (Advantage(sim, profile, side, false, raiders) < profile.DefendMinAdvantage && talons.Count < 8)
            {
                // The Talon Archer spawned this round. Unit: entity ID.
                EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", (4 + talons.Count).ToString() + ".5");
                talons.Add(talon);
                side.Add(talon);
            }
            Assert.That(Advantage(sim, profile, side, false, raiders), Is.GreaterThanOrEqualTo(profile.DefendMinAdvantage), "talons: " + talons.Count);
            Assert.That(Advantage(sim, profile, talons, false, raiders), Is.LessThan(profile.DefendMinAdvantage), "the Talons alone are short, so the tower ends the hold");
            // The intents of the think. Unit: none (list).
            List<AiIntent> intents = Decide(sim, profile, memory);
            Assert.That(intents.FindAll(i => talons.Contains(i.Unit) && (i.Kind == AiIntentKind.AttackMove || i.Kind == AiIntentKind.AttackUnit)), Is.Not.Empty, Key(intents));
            Assert.That(intents.FindAll(i => talons.Contains(i.Unit) && i.Kind == AiIntentKind.Move), Is.Empty, Key(intents));
        }

        /// <summary>DH test 9: no visible threat and a recent hit inside the base: the intents with and without the switch are identical.</summary>
        [Test]
        public void DH9_BlindBranchUnchanged()
        {
            // The intents of each run, with and without the switch. Unit: text.
            string[] runs = new string[2];
            // Run number: 0 without the switch, 1 with it. Range: 0 to 1.
            for (int run = 0; run < 2; run++)
            {
                // The AI memory of this run. Unit: none (object).
                AiMemory memory = NewMemory();
                // The match: P0 Skai with its base, no enemy. Unit: none (object).
                Simulation sim = RaidScene(500);
                // The unit reported as hit. Unit: entity ID.
                EntityId talon = Spawn(sim, "skai.talon_archer", P0, "14.5", "8.5");
                memory.RecentDamage[talon] = new DamageReport { Tick = sim.Clock.Tick, Position = new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("8.5")) };
                runs[run] = Key(Decide(sim, DhHand(run == 1), memory));
            }
            Assert.That(runs[0], Does.Contain("AttackMove"), runs[0]);
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }
    }
}
