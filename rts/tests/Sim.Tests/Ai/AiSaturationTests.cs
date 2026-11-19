// rts/tests/Sim.Tests/Ai/AiSaturationTests.cs
// Tests for package AI-E of rts/docs/specs/field_saturation.md (the AI economy follows the gather-slot rule): the cap formula (E-1), the
// worker target (E-2), the rare cap yielding to saturation (E-3), depots at saturation (E-4), the threat gate (E-5), a greybox mirror
// run of aggressive_s5 (E-6) and the old profiles staying bit-identical (E-7: abl_s5_no_e against aggressive_s4r here, and the A-11, C2-11
// and S3 pins in SkirmishAiTests and AiVarietyTests, which are not re-pinned). Part of Rts.Sim.Tests. Real simulations on shipped maps
// (greybox_1v1: field 0 (4, 10) and 1 (10, 4) are P0's main fields, 4 (8, 52) is a rare field), AiBrain.Decide / Probe on the views.
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
    /// <summary>Package AI-E: worker caps, rare cap, depots and the threat gate follow the gather slots.</summary>
    [TestFixture]
    public class AiSaturationTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ticks per second of the shipped match settings. Unit: ticks per second. Range: 20 (asserted in <see cref="Game"/>).</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Converts seconds to ticks. Unit: ticks.</summary>
        private static int Ticks(int seconds)
        {
            return seconds * TicksPerSecond;
        }

        /// <summary>A started Skai-versus-Water match on <paramref name="mapId"/>: HQs rooted at the spawns, <paramref name="workers"/> workers each, one producer each, every site explored.</summary>
        private Simulation Game(string mapId, int workers)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(mapId)));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(TicksPerSecond));
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to workers - 1.
            for (int i = 0; i < workers; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "water.tender", P1, s1 - offset);
            }
            Spawn(sim, "skai.roost", P0, s0 + new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            Spawn(sim, "water.tidepool", P1, s1 - new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            ExploreKit.ExploreAll(sim);
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

        /// <summary>Spawns a unit at a tile centre.</summary>
        private EntityId SpawnAtTile(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            return Spawn(sim, definition, owner, new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half));
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Worker IDs of a player, ascending.</summary>
        private static List<EntityId> Workers(Simulation sim, PlayerId owner)
        {
            List<EntityId> ids = new List<EntityId>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Economy.CanGather)
                {
                    ids.Add(unit.Id);
                }
            }
            return ids;
        }

        /// <summary>Sets a player's stock.</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>Orders a worker to gather a field and steps once.</summary>
        private void Gather(Simulation sim, PlayerId owner, EntityId worker, int field)
        {
            sim.Submit(new GatherCommand(sim.Clock.Tick, owner, _sequence++, worker, field));
            sim.Step();
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

        /// <summary>The aggressive profile with the saturation worker switch on.</summary>
        private static AiProfile SaturationWorkers()
        {
            return Edited("\"use_saturation_workers\": false,", "\"use_saturation_workers\": true,");
        }

        /// <summary>Probe of P0's economy on the current view.</summary>
        private static AiBrain.EconomyProbe Probe(Simulation sim, AiProfile profile, AiMemory? memory = null)
        {
            return AiBrain.Probe(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory())!;
        }

        /// <summary>The Skai build intents for the depot, in order, from one decision of P0.</summary>
        private static int DepotOrders(Simulation sim, AiProfile profile)
        {
            // Intents of one think.
            List<AiIntent> intents = AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1));
            // Depot orders counted. Unit: orders.
            int count = 0;
            foreach (AiIntent intent in intents)
            {
                count += intent.Kind == AiIntentKind.Build && intent.DefinitionId == "skai.perch_depot" ? 1 : 0;
            }
            return count;
        }

        /// <summary>E-1: the worked examples of E1.1 (5 and 12) and the extra-workers key.</summary>
        [Test]
        public void E1_CapFormula()
        {
            // Main field: 5.4 tiles from a Great Aerie (radius 1.2), interact 1.5, worker speed 2.5, 40 ticks per trip.
            Assert.That(AiBrain.SaturationCap(3, Fix64.Parse("5.4"), Fix64.Parse("1.2"), Fix64.Parse("1.5"), Fix64.Parse("2.5"), 20, 40, 0), Is.EqualTo(5));
            // Far field: 9 tiles from a depot of radius 0.5, speed 2.0.
            Assert.That(AiBrain.SaturationCap(3, Fix64.FromInt(9), Fix64.Half, Fix64.Parse("1.5"), Fix64.FromInt(2), 20, 40, 0), Is.EqualTo(12));
            Assert.That(AiBrain.SaturationCap(3, Fix64.Parse("5.4"), Fix64.Parse("1.2"), Fix64.Parse("1.5"), Fix64.Parse("2.5"), 20, 40, 1), Is.EqualTo(6));
            Assert.That(AiBrain.SaturationCap(3, Fix64.FromInt(9), Fix64.Half, Fix64.Parse("1.5"), Fix64.FromInt(2), 20, 40, 1), Is.EqualTo(13));
            Assert.That(AiBrain.SaturationCap(3, Fix64.FromInt(2), Fix64.Half, Fix64.Parse("1.5"), Fix64.FromInt(2), 20, 40, 0), Is.EqualTo(3), "no walk: the slots only");
        }

        /// <summary>E-2: the target is the sum of the served caps, clipped by max_workers; with nothing served the floor is the first target-order field's cap.</summary>
        [Test]
        public void E2_WantedWorkers()
        {
            AiProfile profile = SaturationWorkers();
            Simulation sim = Game("greybox_1v1", 2);
            AiBrain.EconomyProbe probe = Probe(sim, profile);
            // Sum of the served caps. Unit: workers.
            int sum = 0;
            foreach (int cap in probe.ServedCaps)
            {
                sum += cap;
            }
            Assert.That(probe.Served.Count, Is.GreaterThan(0));
            Assert.That(sum, Is.GreaterThan(probe.Served.Count * 3 - 1), "every cap is at least the slots");
            Assert.That(probe.WantedWorkers, Is.EqualTo(sum));
            AiProfile clipped = Edited("\"use_saturation_workers\": false,", "\"use_saturation_workers\": true,", "\"max_workers\": 24,", "\"max_workers\": 4,");
            Assert.That(Probe(sim, clipped).WantedWorkers, Is.EqualTo(4));

            // Both main fields empty: nothing served, the floor is the cap of the nearest live field (capped distance: no drop-off is near it).
            sim.Resources[0].Take(1000000);
            sim.Resources[1].Take(1000000);
            sim.Step();
            // Probe on the new view, with one think of memory so the empty fields are known.
            AiMemory memory = new AiMemory();
            Probe(sim, profile, memory);
            AiBrain.EconomyProbe starved = Probe(sim, profile, memory);
            Assert.That(starved.Served, Is.Empty);
            Assert.That(starved.WantedWorkers, Is.GreaterThanOrEqualTo(3), "the floor is a field's cap, at least its slots");
        }

        /// <summary>
        /// E-3: rare bank above the soft cap. Every served common field at its cap: the served rare field keeps a cap above 0; one common field
        /// below its cap: 0; with the switch off both are 0.
        /// </summary>
        [Test]
        public void E3_RareCapYieldsToSaturation()
        {
            AiProfile on = Edited("\"use_saturation_workers\": false,", "\"use_saturation_workers\": true,", "\"rare_bank_soft_cap\": 0,", "\"rare_bank_soft_cap\": 300,",
                "\"rare_cap_yields_to_saturation\": false,", "\"rare_cap_yields_to_saturation\": true,");
            AiProfile off = Edited("\"use_saturation_workers\": false,", "\"use_saturation_workers\": true,", "\"rare_bank_soft_cap\": 0,", "\"rare_bank_soft_cap\": 300,");
            // Rare cap seen for a match where common field 0 holds fullZero and field 1 holds fullOne workers, with the profile given.
            int RareCap(AiProfile profile, int onFieldZero, int onFieldOne)
            {
                Simulation sim = Game("greybox_1v1", 0);
                SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
                // Worker index. Range: 0 to the larger count.
                for (int i = 0; i < onFieldZero; i++)
                {
                    Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 0);
                }
                for (int i = 0; i < onFieldOne; i++)
                {
                    Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 1);
                }
                Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 4);
                SetStock(sim, P0, 0, 5000);
                AiBrain.EconomyProbe probe = Probe(sim, profile);
                Assert.That(probe.Served, Does.Contain(4));
                return probe.ServedCaps[new List<int>(probe.Served).IndexOf(4)];
            }
            // Caps of the two main fields (equal: both 5.4 tiles from the HQ, same formula), read from a throwaway game.
            Simulation probeGame = Game("greybox_1v1", 0);
            AiBrain.EconomyProbe caps = Probe(probeGame, on);
            int cap0 = caps.ServedCaps[new List<int>(caps.Served).IndexOf(0)];
            int cap1 = caps.ServedCaps[new List<int>(caps.Served).IndexOf(1)];
            Assert.That(RareCap(on, cap0, cap1), Is.GreaterThan(0), "both common fields full: the rare field stays open");
            Assert.That(RareCap(on, cap0, cap1 - 1), Is.EqualTo(0), "one common field below its cap: closed");
            Assert.That(RareCap(off, cap0, cap1), Is.EqualTo(0), "switch off: closed (today's rule)");
            Assert.That(RareCap(off, cap0, cap1 - 1), Is.EqualTo(0), "switch off: closed");
        }

        /// <summary>E-4: a depot at 80 percent saturation before the clock gate, none at 70 percent; none at max_live_depots.</summary>
        [Test]
        public void E4_SaturationExpansion()
        {
            AiProfile on = Edited("\"use_saturation_expansion\": false,", "\"use_saturation_expansion\": true,", "\"expand_saturation_fraction\": 1,", "\"expand_saturation_fraction\": 0.8,",
                "\"max_workers\": 24,", "\"max_workers\": 48,");
            // The target the fresh prototype base has (HQ fields only). Unit: workers.
            int wanted = Probe(Game("prototype_1v1", 2), on).WantedWorkers;
            Assert.That(wanted, Is.GreaterThan(4));
            // Workers at 0.8 x wanted, rounded up, and one fewer (below 0.8, so 0.7 for wanted 10).
            int high = (8 * wanted + 9) / 10;
            // Depot orders given at 100 s with the stated worker count and profile.
            int Orders(int workers, AiProfile profile)
            {
                Simulation sim = Game("prototype_1v1", workers);
                StepTo(sim, Ticks(100));
                Gather(sim, P0, Workers(sim, P0)[1], 0);
                SetStock(sim, P0, 300, 0);
                return DepotOrders(sim, profile);
            }
            Assert.That(Orders(high, on), Is.EqualTo(1), "at 0.8 of the target a depot is ordered before the clock gate");
            Assert.That(Orders(high - 1, on), Is.EqualTo(0), "below 0.8 of the target: none");
            Assert.That(Orders(high, Edited("\"max_workers\": 24,", "\"max_workers\": 48,")), Is.EqualTo(0), "switch off: none before the clock gate");
            AiProfile capped = Edited("\"use_saturation_expansion\": false,", "\"use_saturation_expansion\": true,", "\"expand_saturation_fraction\": 1,", "\"expand_saturation_fraction\": 0.8,",
                "\"max_workers\": 24,", "\"max_workers\": 48,", "\"max_live_depots\": 2,", "\"max_live_depots\": 0,");
            Assert.That(Orders(high, capped), Is.EqualTo(0), "no depot at max_live_depots");
        }

        /// <summary>E-5: the threat gate. A seen threat of the grasper's supply blocks a depot when expand_block_threat_supply is at or below it, not when above; 0 keeps any-threat blocks.</summary>
        [Test]
        public void E5_ThreatGate()
        {
            // Supply of the visible threat (one Water grasper). Unit: supply. Range: 1 upward.
            int supply = Data.Content.Units["water.grasper"].Supply;
            // Depot orders at 240 s (the clock gate is open) with one grasper inside the base and the key at the given value.
            int Orders(int block)
            {
                Simulation sim = Game("greybox_1v1", 2);
                StepTo(sim, Ticks(240));
                SetStock(sim, P0, 500, 0);
                SpawnAtTile(sim, "water.grasper", P1, 4, 18);
                return DepotOrders(sim, Edited("\"expand_block_threat_supply\": 0,", "\"expand_block_threat_supply\": " + block + ","));
            }
            Assert.That(Orders(0), Is.EqualTo(0), "0: any threat blocks");
            Assert.That(Orders(supply + 1), Is.EqualTo(1), "threat below the gate: a depot is ordered");
            Assert.That(Orders(supply), Is.EqualTo(0), "threat at the gate: blocked");
        }

        /// <summary>
        /// E-6: two aggressive_s5 Skai AIs on prototype_1v1 for 600 s. Workers assigned to a served field exceed its cap by more than one for
        /// more than one think in a row never; the first depot is placed no later than the clock gate (expand_at_seconds plus the jitter).
        /// </summary>
        [Test]
        public void E6_MirrorRun_CapsHold_AndFirstDepotIsOnTime()
        {
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s5");
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai A"),
                new PlayerSetup(P1, new TeamId(1), "skai", "Skai B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, sim.Map.Spawns[0]);
            Spawn(sim, "skai.great_aerie", P1, sim.Map.Spawns[1]);
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, sim.Map.Spawns[0] + offset);
                Spawn(sim, "skai.fledgling", P1, sim.Map.Spawns[1] - offset);
            }
            List<IPlayerController> controllers = new List<IPlayerController>
            {
                new SkirmishAiController(P0, profile),
                new SkirmishAiController(P1, profile),
            };
            AiHost.StartAll(sim, controllers);
            // Ticks between two thinks (think_interval_seconds); the caps are checked once per think. Unit: ticks. Range: 1 upward.
            int thinkTicks = (profile.ThinkIntervalSeconds * TicksPerSecond).RoundToInt();
            // Memory of the probe (field knowledge only).
            AiMemory probeMemory = new AiMemory();
            // Consecutive thinks each field index has been more than one over its cap. Unit: thinks.
            SortedDictionary<int, int> over = new SortedDictionary<int, int>();
            // Tick of the first depot order of player 0, or -1.
            int firstDepot = -1;
            while (sim.Clock.Tick < Ticks(600))
            {
                AiHost.ThinkAll(sim, controllers);
                foreach (Command command in ((SkirmishAiController)controllers[0]).LastCommands)
                {
                    if (firstDepot < 0 && command is BuildStructureCommand build && build.DefinitionId == "skai.perch_depot")
                    {
                        firstDepot = sim.Clock.Tick;
                    }
                }
                AiWorldView view = AiWorldView.Build(sim, P0);
                AiBrain.EconomyProbe? probe = AiBrain.Probe(view, profile, profile.RequireFaction("skai"), probeMemory);
                if (probe != null && sim.Clock.Tick % thinkTicks == 0)
                {
                    // Workers per field index right now. Unit: workers.
                    int[] assigned = new int[sim.Resources.Count];
                    foreach (AiUnitView unit in view.OwnUnits)
                    {
                        if (unit.IsWorker && unit.GatherFieldIndex >= 0)
                        {
                            assigned[unit.GatherFieldIndex]++;
                        }
                    }
                    // Index into the served list. Range: 0 to served count - 1.
                    for (int k = 0; k < probe.Served.Count; k++)
                    {
                        // Field index of this served field. A closed rare field (cap 0, the C2.3 soft cap) is drained at rebalance_workers_per_think, so it is not checked.
                        int field = probe.Served[k];
                        over[field] = probe.ServedCaps[k] > 0 && assigned[field] > probe.ServedCaps[k] + 1 ? (over.TryGetValue(field, out int run) ? run : 0) + 1 : 0;
                        Assert.That(over[field], Is.LessThanOrEqualTo(1), "field " + field + " over cap + 1 for two thinks at tick " + sim.Clock.Tick);
                    }
                }
                sim.Step();
            }
            // Latest clock gate: expand_at_seconds of the profile's plans plus the jitter, as ticks.
            int gate = Ticks(profile.ExpandJitterSeconds) + Ticks(300);
            Assert.That(firstDepot, Is.GreaterThanOrEqualTo(0), "a depot is placed");
            Assert.That(firstDepot, Is.LessThanOrEqualTo(gate + Ticks(30)), "by the clock gate (300 s is above every opening's expand_at_seconds plus a build trip)");
        }

        /// <summary>E-7 (part): abl_s5_no_e plays bit-identically to aggressive_s4r over 3 minutes of a Skai-versus-Water match on prototype_1v1 (same state hash).</summary>
        [Test]
        public void E7_AblationEqualsS4r()
        {
            // State hash after three minutes with the given profile in both seats. Range: any.
            ulong Run(string id)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Simulation sim = Game("prototype_1v1", 6);
                List<IPlayerController> controllers = new List<IPlayerController>
                {
                    new SkirmishAiController(P0, profile),
                    new SkirmishAiController(P1, profile),
                };
                AiHost.StartAll(sim, controllers);
                while (sim.Clock.Tick < Ticks(180))
                {
                    AiHost.ThinkAll(sim, controllers);
                    sim.Step();
                }
                return sim.ComputeStateHash();
            }
            Assert.That(Run("abl_s5_no_e"), Is.EqualTo(Run("aggressive_s4r")));
        }
    }
}
