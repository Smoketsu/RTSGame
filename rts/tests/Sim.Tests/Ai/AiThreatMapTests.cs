// rts/tests/Sim.Tests/Ai/AiThreatMapTests.cs
// Tests TM-1 to TM-15 of rts/docs/specs/ai_threat_map.md (package S-MAP, the threat map): the grid, the spread, health, ageing, seen-gone, structures, own sources,
// army-only sightings, fog, sums, determinism, the off path, the passive map, the trace column and grid dump, and profile parsing. Part of Rts.Sim.Tests.
// Real simulations on prototype_1v1 (96 by 96 tiles, cells of 4 tiles, 24 by 24 cells; P0 HQ rooted at (12.5, 12.5), cell (3, 3)), both players Skai so a P1 Windrunner
// is a unit of its own faction; AiBrain.ProbeThreat on P0's view. Values: Windrunner 75 (60 health), Talon Archer 100, Perch Tower 100, Coral Turret 100, Pearl Depot 100 (unarmed), HQ 0;
// 20 ticks per second, so M = 1200 ticks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package S-MAP: the threat map.</summary>
    [TestFixture]
    public class AiThreatMapTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Skai too, so its Windrunners are its own faction's units).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The candidate profile (map on, consumers absent).</summary>
        private static AiProfile Candidate => AiProfile.FromDataSet(Data, "aggressive_s5_s");

        /// <summary>A started Skai-versus-Skai match on prototype_1v1: HQs at the spawns, two workers each, every tile explored for both teams.</summary>
        private Simulation Game()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "skai", "Skai"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(20));
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "skai.great_aerie", P1, s1);
            // Worker index. Range: 0 to 1.
            for (int i = 0; i < 2; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "skai.fledgling", P1, s1 - offset);
            }
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
            throw new AssertionException("spawn of " + definition + " failed at " + position);
        }

        /// <summary>Spawns a unit at whole-tile coordinates (the tile corner, so cell arithmetic in the tests is exact).</summary>
        private EntityId SpawnAt(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            return Spawn(sim, definition, owner, FixVec2.FromInts(x, y));
        }

        /// <summary>Sets a unit's health (internal setter, visible to the tests).</summary>
        private static void SetHealth(Simulation sim, EntityId id, int health)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
            unit!.SetHealth(Fix64.FromInt(health));
        }

        /// <summary>Restores every unit of P1 to full health (a step may have hurt a Windrunner beside the P0 HQ) so the strengths are the data values.</summary>
        private static void HealAll(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                unit.SetHealth(unit.Definition.MaxHealth);
            }
        }

        /// <summary>The probe of P0 on the current view with the candidate profile and the given memory (a fresh one by default).</summary>
        private static ThreatProbe Probe(Simulation sim, AiMemory? memory = null)
        {
            AiProfile profile = Candidate;
            return AiBrain.ProbeThreat(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory())!;
        }

        /// <summary>Index of cell (column, row) on the 24-column grid. Unit: cell index.</summary>
        private static int Cell(int column, int row)
        {
            return row * 24 + column;
        }

        /// <summary>Total of a layer. Unit: resource units.</summary>
        private static int Sum(int[] layer)
        {
            int total = 0;
            foreach (int value in layer)
            {
                total += value;
            }
            return total;
        }

        /// <summary>TM-1: a 96 by 96 map with c 4 has 24 by 24 cells; (95.9, 0.0) is cell (23, 0); a 10-wide map has 3 columns; (-1, 5) clamps to column 0.</summary>
        [Test]
        public void TM1_Grid()
        {
            Assert.That(AiThreatMap.CellCount(96, 4), Is.EqualTo(24));
            Assert.That(AiThreatMap.CellCount(10, 4), Is.EqualTo(3));
            Assert.That(AiThreatMap.CellOf(new FixVec2(Fix64.Parse("95.9"), Fix64.Zero), 4, 24, 24), Is.EqualTo(Cell(23, 0)));
            Assert.That(AiThreatMap.CellOf(new FixVec2(Fix64.FromInt(-1), Fix64.FromInt(5)), 4, 3, 3), Is.EqualTo(1 * 3 + 0));
            Assert.That(AiThreatMap.CellOf(new FixVec2(Fix64.FromInt(200), Fix64.FromInt(200)), 4, 3, 3), Is.EqualTo(2 * 3 + 2));
            Assert.That(AiThreatMap.CellOf(new FixVec2(Fix64.Parse("3.99"), Fix64.Parse("4")), 4, 24, 24), Is.EqualTo(Cell(0, 1)));
            // The real grid on the shipped map.
            Simulation sim = Game();
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Columns, Is.EqualTo(24));
            Assert.That(probe.Rows, Is.EqualTo(24));
            Assert.That(probe.Enemy.Length, Is.EqualTo(576));
            Assert.That(probe.Own.Length, Is.EqualTo(576));
        }

        /// <summary>TM-2: one visible P1 Windrunner at the centre of cell (3, 3): 75 there, 50 on ring 1, 25 on ring 2, 0 on ring 3; K 0 gives 75 in its cell only; a unit in cell (0, 0) writes nothing outside the grid.</summary>
        [Test]
        public void TM2_Spread()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            sim.Step();
            HealAll(sim);
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Sightings.Count, Is.EqualTo(1));
            Assert.That(probe.Enemy[Cell(3, 3)], Is.EqualTo(75));
            Assert.That(probe.Enemy[Cell(4, 3)], Is.EqualTo(50));
            Assert.That(probe.Enemy[Cell(2, 2)], Is.EqualTo(50));
            Assert.That(probe.Enemy[Cell(5, 3)], Is.EqualTo(25));
            Assert.That(probe.Enemy[Cell(1, 5)], Is.EqualTo(25));
            Assert.That(probe.Enemy[Cell(6, 3)], Is.EqualTo(0));
            Assert.That(probe.Enemy[Cell(3, 0)], Is.EqualTo(0));
            // 1 cell of 75, 8 of 50, 16 of 25.
            Assert.That(Sum(probe.Enemy), Is.EqualTo(75 + 8 * 50 + 16 * 25));
            // K 0: its own cell only.
            AiProfile flat = AiProfile.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_s.json")).Replace("\"threat_spread_cells\": 2,", "\"threat_spread_cells\": 0,"),
                "ai/aggressive_s5_s.json", Data.Content);
            ThreatProbe flatProbe = AiBrain.ProbeThreat(AiWorldView.Build(sim, P0), flat, flat.RequireFaction("skai"), new AiMemory())!;
            Assert.That(flatProbe.Enemy[Cell(3, 3)], Is.EqualTo(75));
            Assert.That(Sum(flatProbe.Enemy), Is.EqualTo(75));
            // A unit in cell (0, 0): only the cells on the grid get a value (4 cells within two rings of the corner: 1 + 3 + 5).
            Simulation corner = Game();
            AiMemory cornerMemory = new AiMemory();
            cornerMemory.ThreatSightings[new EntityId(900)] = (FixVec2.FromInts(1, 1), 75, corner.Clock.Tick);
            ThreatProbe cornerProbe = Probe(corner, cornerMemory);
            Assert.That(cornerProbe.Enemy[Cell(0, 0)], Is.EqualTo(75));
            Assert.That(cornerProbe.Enemy[Cell(1, 1)], Is.EqualTo(50));
            Assert.That(cornerProbe.Enemy[Cell(2, 2)], Is.EqualTo(25));
            Assert.That(Sum(cornerProbe.Enemy), Is.EqualTo(75 + 3 * 50 + 5 * 25));
        }

        /// <summary>TM-3: the same Windrunner at half health gives 37 in its cell and floor(37 x 2 / 3) = 24 on ring 1.</summary>
        [Test]
        public void TM3_Health()
        {
            Simulation sim = Game();
            EntityId enemy = SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            sim.Step();
            HealAll(sim);
            SetHealth(sim, enemy, 30);
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Enemy[Cell(3, 3)], Is.EqualTo(37));
            Assert.That(probe.Enemy[Cell(4, 3)], Is.EqualTo(24));
            Assert.That(probe.Enemy[Cell(5, 3)], Is.EqualTo(12));
        }

        /// <summary>TM-4: ageing (a sighting in memory, its tile not visible to P0): seen 600 ticks ago gives 37, 1200 ticks ago removes it and the layer is 0.</summary>
        [Test]
        public void TM4_Ageing()
        {
            Simulation sim = Game();
            int now = sim.Clock.Tick;
            // Sighting position, far from every P0 unit (cell (20, 20)). Unit: tiles.
            FixVec2 far = FixVec2.FromInts(80, 80);
            AiMemory half = new AiMemory();
            half.ThreatSightings[new EntityId(900)] = (far, 75, now - 600);
            ThreatProbe halfProbe = Probe(sim, half);
            Assert.That(halfProbe.Enemy[Cell(20, 20)], Is.EqualTo(37));
            Assert.That(halfProbe.Sightings.Count, Is.EqualTo(1));
            AiMemory old = new AiMemory();
            old.ThreatSightings[new EntityId(900)] = (far, 75, now - 1200);
            ThreatProbe oldProbe = Probe(sim, old);
            Assert.That(oldProbe.Sightings.Count, Is.EqualTo(0));
            Assert.That(Sum(oldProbe.Enemy), Is.EqualTo(0));
        }

        /// <summary>TM-5: a sighting whose stored tile is visible to P0 now, with no enemy there, is removed on the next update.</summary>
        [Test]
        public void TM5_SeenGone()
        {
            Simulation sim = Game();
            // A tile beside the P0 HQ (12.5, 12.5), which the HQ sees. Unit: tiles.
            FixVec2 seen = new FixVec2(Fix64.Parse("15.5"), Fix64.Parse("12.5"));
            Assert.That(AiWorldView.Build(sim, P0).IsVisible(15, 12), Is.True);
            AiMemory memory = new AiMemory();
            memory.ThreatSightings[new EntityId(900)] = (seen, 75, sim.Clock.Tick - 10);
            ThreatProbe probe = Probe(sim, memory);
            Assert.That(probe.Sightings.Count, Is.EqualTo(0));
            Assert.That(Sum(probe.Enemy), Is.EqualTo(0));
        }

        /// <summary>TM-6: a known Coral Turret (health share 1, last seen 300 s ago) adds 100 in its cell; a known Pearl Depot and a known HQ add 0.</summary>
        [Test]
        public void TM6_Structures()
        {
            Simulation sim = Game();
            int seen = sim.Clock.Tick - 6000;
            AiMemory memory = new AiMemory();
            memory.KnownEnemyStructures.Add(new EntityId(500), new KnownStructure { DefinitionId = "water.coral_turret", Position = FixVec2.FromInts(80, 80), LastSeenTick = seen });
            memory.KnownEnemyStructures.Add(new EntityId(501), new KnownStructure { DefinitionId = "water.pearl_depot", Position = FixVec2.FromInts(60, 80), LastSeenTick = seen });
            memory.KnownEnemyStructures.Add(new EntityId(502), new KnownStructure { DefinitionId = "water.grand_shell", Position = FixVec2.FromInts(80, 60), IsHq = true, LastSeenTick = seen });
            ThreatProbe probe = Probe(sim, memory);
            Assert.That(probe.Enemy[Cell(20, 20)], Is.EqualTo(100));
            Assert.That(probe.Enemy[Cell(21, 20)], Is.EqualTo(66));
            Assert.That(probe.Enemy[Cell(15, 20)], Is.EqualTo(0));
            Assert.That(probe.Enemy[Cell(20, 15)], Is.EqualTo(0));
            Assert.That(Sum(probe.Enemy), Is.EqualTo(100 + 8 * 66 + 16 * 33));
        }

        /// <summary>TM-7: a complete own Talon Archer adds 100 to Own; a worker, the HQ and a Perch Tower under construction add 0; a complete Perch Tower adds 100.</summary>
        [Test]
        public void TM7_Own()
        {
            Simulation sim = Game();
            // Baseline: the HQ and two workers add nothing.
            Assert.That(Sum(Probe(sim).Own), Is.EqualTo(0));
            // Positions (tile corners) in empty cells of the P0 base area; Talon Archer in cell (2, 3), towers in cells (3, 5) and (5, 5).
            SpawnAt(sim, "skai.talon_archer", P0, 8, 15);
            EntityId building = SpawnAt(sim, "skai.perch_tower", P0, 12, 20);
            SpawnAt(sim, "skai.perch_tower", P0, 20, 20);
            sim.Step();
            HealAll(sim);
            Assert.That(sim.Units.TryGet(building, out Unit? tower), Is.True);
            tower!.SetConstructionTicks(100);
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Own[Cell(2, 3)], Is.EqualTo(100));
            Assert.That(probe.Own[Cell(3, 5)], Is.EqualTo(66), "ring 2 of the Talon Archer (33) plus ring 2 of the complete tower (33); nothing from the tower under construction");
            Assert.That(probe.Own[Cell(5, 5)], Is.EqualTo(100));
            // 1 + 8 + 16 weighted cells of the Talon Archer and of the complete tower, nothing from the others.
            Assert.That(Sum(probe.Own), Is.EqualTo(2 * (100 + 8 * 66 + 16 * 33)));
        }

        /// <summary>TM-8: only armed army units become sightings: a visible P1 worker adds nothing and makes no sighting.</summary>
        [Test]
        public void TM8_OnlyArmedArmy()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.fledgling", P1, 14, 14);
            sim.Step();
            Assert.That(AiWorldView.Build(sim, P0).VisibleEnemies.Count, Is.GreaterThan(0));
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Sightings.Count, Is.EqualTo(0));
            Assert.That(Sum(probe.Enemy), Is.EqualTo(0));
        }

        /// <summary>TM-9: a P1 Windrunner present in the simulation but not in VisibleEnemies adds nothing anywhere.</summary>
        [Test]
        public void TM9_Fog()
        {
            Simulation sim = Game();
            // Cell (12, 12), far from every P0 unit.
            SpawnAt(sim, "skai.windrunner", P1, 50, 50);
            sim.Step();
            HealAll(sim);
            AiWorldView view = AiWorldView.Build(sim, P0);
            foreach (AiUnitView enemy in view.VisibleEnemies)
            {
                Assert.That(enemy.Definition.Id, Is.Not.EqualTo("skai.windrunner"));
            }
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Sightings.Count, Is.EqualTo(0));
            Assert.That(Sum(probe.Enemy), Is.EqualTo(0));
        }

        /// <summary>TM-10: two Windrunners in one cell give 150 there.</summary>
        [Test]
        public void TM10_Sum()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            SpawnAt(sim, "skai.windrunner", P1, 15, 15);
            sim.Step();
            HealAll(sim);
            ThreatProbe probe = Probe(sim);
            Assert.That(probe.Enemy[Cell(3, 3)], Is.EqualTo(150));
            Assert.That(probe.Enemy[Cell(4, 4)], Is.EqualTo(100));
        }

        /// <summary>TM-11: two runs of the same think give identical layers and sightings.</summary>
        [Test]
        public void TM11_Determinism()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            SpawnAt(sim, "skai.windrunner", P1, 15, 13);
            SpawnAt(sim, "skai.talon_archer", P0, 8, 15);
            sim.Step();
            HealAll(sim);
            ThreatProbe first = Probe(sim);
            ThreatProbe second = Probe(sim);
            Assert.That(second.Enemy, Is.EqualTo(first.Enemy));
            Assert.That(second.Own, Is.EqualTo(first.Own));
            Assert.That(second.Sightings.Count, Is.EqualTo(first.Sightings.Count));
            foreach (KeyValuePair<EntityId, (FixVec2 Position, int Strength, int Tick)> pair in first.Sightings)
            {
                Assert.That(second.Sightings[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        /// <summary>
        /// TM-12: with use_threat_map false, ThreatSightings stays empty and ThreatTick stays -1 over thinks with a visible armed enemy in view; the pins of the spec (section 0) are held by their own
        /// existing tests, which are not re-pinned (HarnessUnitTests smoke and economy_smoke, FightSimTests, SkirmishAiTests V2ReferenceHash and BlindTraceHash).
        /// </summary>
        [Test]
        public void TM12_OffPath()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            sim.Step();
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s5");
            AiMemory memory = new AiMemory();
            DeterministicRandom rng = new DeterministicRandom(7);
            for (int think = 0; think < 20; think++)
            {
                AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory, rng);
                sim.Step();
            }
            Assert.That(memory.ThreatSightings.Count, Is.EqualTo(0));
            Assert.That(memory.ThreatTick, Is.EqualTo(-1));
            Assert.That(memory.ThreatEnemy.Length, Is.EqualTo(0));
            // And the switch on does build it on the same thinks.
            AiProfile on = Candidate;
            AiMemory built = new AiMemory();
            AiBrain.Decide(AiWorldView.Build(sim, P0), on, on.RequireFaction("skai"), built, new DeterministicRandom(7));
            Assert.That(built.ThreatTick, Is.EqualTo(sim.Clock.Tick));
        }

        /// <summary>
        /// TM-13: aggressive_s5 and aggressive_s5_s (map on, both consumers absent) give identical intents on every think and identical generator draw counts and states over 100 thinks beside a visible enemy
        /// group and an own army; and two full games on prototype_1v1 (skai_vs_water, seed 20261026, 600 s) give the same record line (end tick, state hash, result).
        /// </summary>
        [Test]
        public void TM13_PassiveMap()
        {
            Simulation sim = Game();
            SpawnAt(sim, "skai.windrunner", P1, 14, 14);
            SpawnAt(sim, "skai.windrunner", P1, 15, 13);
            SpawnAt(sim, "skai.talon_archer", P0, 8, 15);
            SpawnAt(sim, "skai.windrunner", P0, 9, 15);
            AiProfile plain = AiProfile.FromDataSet(Data, "aggressive_s5");
            // The candidate with its consumer switch off again (package S-TS switched rule TS on in the file), so the map alone is compared.
            AiProfile mapped = AiProfile.Parse(ProfileText("aggressive_s5_s").Replace("\"use_threat_safety\": true,", "\"use_threat_safety\": false,"), "ai/aggressive_s5_s.json", Data.Content);
            AiMemory plainMemory = new AiMemory();
            AiMemory mappedMemory = new AiMemory();
            DeterministicRandom plainRng = new DeterministicRandom(99);
            DeterministicRandom mappedRng = new DeterministicRandom(99);
            // Total intents compared, to show the loop decided something.
            int compared = 0;
            for (int think = 0; think < 100; think++)
            {
                List<AiIntent> a = AiBrain.Decide(AiWorldView.Build(sim, P0), plain, plain.RequireFaction("skai"), plainMemory, plainRng);
                List<AiIntent> b = AiBrain.Decide(AiWorldView.Build(sim, P0), mapped, mapped.RequireFaction("skai"), mappedMemory, mappedRng);
                Assert.That(b.Count, Is.EqualTo(a.Count), "think " + think);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                    Assert.That(b[i].Unit, Is.EqualTo(a[i].Unit));
                    Assert.That(b[i].Field, Is.EqualTo(a[i].Field));
                    Assert.That(b[i].DefinitionId, Is.EqualTo(a[i].DefinitionId));
                    Assert.That(b[i].TileX, Is.EqualTo(a[i].TileX));
                    Assert.That(b[i].TileY, Is.EqualTo(a[i].TileY));
                    Assert.That(b[i].Point, Is.EqualTo(a[i].Point));
                    Assert.That(b[i].Target, Is.EqualTo(a[i].Target));
                    compared++;
                }
                Assert.That(mappedRng.DrawCount, Is.EqualTo(plainRng.DrawCount), "think " + think);
                Assert.That(mappedRng.State, Is.EqualTo(plainRng.State), "think " + think);
                sim.Step();
            }
            Assert.That(compared, Is.GreaterThan(0));
            Assert.That(mappedMemory.ThreatTick, Is.GreaterThan(0));
            Assert.That(plainMemory.ThreatTick, Is.EqualTo(-1));
            // Full games: the record line carries the end tick, result and final state hash; the candidate plays under the baseline's name so the names match.
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord baseline = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, plain, plain, "aggressive_s5", "aggressive_s5", 600);
            GameRecord candidate = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, mapped, mapped, "aggressive_s5", "aggressive_s5", 600);
            Assert.That(candidate.ToJsonLine(), Is.EqualTo(baseline.ToJsonLine()));
        }

        /// <summary>
        /// TM-14: an aggressive_s5 trace has no thr= or threat lines; an aggressive_s5_s trace has thr= on every player sample line; --threat-grid (GameTrace.ThreatGrid) prints 24 rows per layer per player on
        /// prototype_1v1, 24 values each. (That the aggressive_s5 trace is byte-identical to the trace before S-MAP is the builder's cmp, in the report.)
        /// </summary>
        [Test]
        public void TM14_TraceColumnAndGrid()
        {
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            StringWriter plain = new StringWriter();
            GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120, new GameTrace(plain, 30) { ThreatGrid = true });
            Assert.That(plain.ToString(), Does.Not.Contain("thr="));
            Assert.That(plain.ToString(), Does.Not.Contain("threat "));
            StringWriter candidate = new StringWriter();
            GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_s", "aggressive_s5_s", 120, new GameTrace(candidate, 30));
            int samples = 0;
            foreach (string line in candidate.ToString().Split('\n'))
            {
                if (Regex.IsMatch(line, @"^t=\S+ .* bank="))
                {
                    samples++;
                    Assert.That(line, Does.Match(@" thr=s\d+/e\d+/o\d+/m\d+@-?\d+,-?\d+( \(final\))?$"), line);
                }
            }
            Assert.That(samples, Is.GreaterThanOrEqualTo(8));
            Assert.That(candidate.ToString(), Does.Not.Contain("threat "));
            // The grid dump.
            StringWriter grid = new StringWriter();
            GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_s", "aggressive_s5_s", 60, new GameTrace(grid, 30) { ThreatGrid = true });
            string[] lines = grid.ToString().Split('\n');
            int headers = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"^threat (enemy|own) P[01]$"))
                {
                    headers++;
                    for (int row = 1; row <= 24; row++)
                    {
                        Assert.That(lines[i + row], Does.Match(@"^\d+( \d+){23}$"), lines[i + row]);
                    }
                    Assert.That(lines[i + 25], Does.Not.Match(@"^\d+( \d+){23}$"));
                }
            }
            // Two samples (30 s and 60 s) x two players x two layers.
            Assert.That(headers, Is.GreaterThanOrEqualTo(8));
        }

        /// <summary>
        /// TM-15: every shipped profile has the three keys; only aggressive_s5_s has use_threat_map true; threat_cell_tiles 0 and 17 and threat_spread_cells 9 fail naming the key; a missing key fails;
        /// aggressive_s5_s.json differs from aggressive_s5.json only in line 1, $doc, id and the S keys (line diff after removing comment lines).
        /// </summary>
        [Test]
        public void TM15_Parsing()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                string text = ProfileText(id);
                Assert.That(text, Does.Contain("\"use_threat_map\":"), id);
                Assert.That(text, Does.Contain("\"threat_cell_tiles\": 4,"), id);
                Assert.That(text, Does.Contain("\"threat_spread_cells\": 2,"), id);
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseThreatMap, Is.EqualTo(id == "aggressive_s5_s"), id);
                Assert.That(profile.ThreatCellTiles, Is.EqualTo(4), id);
                Assert.That(profile.ThreatSpreadCells, Is.EqualTo(2), id);
            }
            string candidate = ProfileText("aggressive_s5_s");
            string file = "ai/aggressive_s5_s.json";
            (string Old, string New, string Key)[] bad =
            {
                ("\"threat_cell_tiles\": 4,", "\"threat_cell_tiles\": 0,", "threat_cell_tiles"),
                ("\"threat_cell_tiles\": 4,", "\"threat_cell_tiles\": 17,", "threat_cell_tiles"),
                ("\"threat_spread_cells\": 2,", "\"threat_spread_cells\": 9,", "threat_spread_cells"),
                ("\"threat_spread_cells\": 2,", "\"threat_spread_cells\": -1,", "threat_spread_cells"),
                ("\"use_threat_map\": true,", "\"use_threat_map\": 1,", "use_threat_map"),
            };
            foreach ((string oldText, string newText, string key) in bad)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => AiProfile.Parse(candidate.Replace(oldText, newText), file, Data.Content));
                Assert.That(error.Message, Does.Contain(file).And.Contain(key), newText);
            }
            // The limits are accepted.
            Assert.That(AiProfile.Parse(candidate.Replace("\"threat_cell_tiles\": 4,", "\"threat_cell_tiles\": 16,"), file, Data.Content).ThreatCellTiles, Is.EqualTo(16));
            Assert.That(AiProfile.Parse(candidate.Replace("\"threat_cell_tiles\": 4,", "\"threat_cell_tiles\": 1,"), file, Data.Content).ThreatCellTiles, Is.EqualTo(1));
            Assert.That(AiProfile.Parse(candidate.Replace("\"threat_spread_cells\": 2,", "\"threat_spread_cells\": 8,"), file, Data.Content).ThreatSpreadCells, Is.EqualTo(8));
            foreach (string key in new[] { "use_threat_map", "threat_cell_tiles", "threat_spread_cells" })
            {
                // The candidate's text without the line that holds the key.
                string without = Regex.Replace(candidate, "\\n  \"" + key + "\":[^\\n]*", string.Empty);
                Assert.That(without, Does.Not.Contain("\"" + key + "\":"));
                DataValidationException missing = Expect.Throws<DataValidationException>(() => AiProfile.Parse(without, file, Data.Content));
                Assert.That(missing.Message, Does.Contain(file).And.Contain(key));
            }
            // The candidate against its baseline.
            List<string> baseline = NonCommentLines(ProfileText("aggressive_s5"));
            List<string> changed = new List<string>();
            List<string> candidateLines = NonCommentLines(candidate);
            Assert.That(candidateLines.Count, Is.EqualTo(baseline.Count));
            for (int i = 0; i < baseline.Count; i++)
            {
                if (baseline[i] != candidateLines[i])
                {
                    changed.Add(candidateLines[i].Trim());
                }
            }
            Assert.That(changed.Count, Is.EqualTo(4), string.Join("\n", changed));
            Assert.That(changed[0], Does.StartWith("\"$doc\":"));
            Assert.That(changed[1], Is.EqualTo("\"id\": \"aggressive_s5_s\","));
            Assert.That(changed[2], Is.EqualTo("\"use_threat_map\": true,"));
            Assert.That(changed[3], Is.EqualTo("\"use_threat_safety\": true,"), "package S-TS switches rule TS on in the candidate");
            Assert.That(candidate.Split('\n')[0], Does.StartWith("// rts/data/ai/aggressive_s5_s.json"));
        }

        /// <summary>The text of a shipped profile file.</summary>
        private static string ProfileText(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json"));
        }

        /// <summary>The non-comment lines of a file's text, in order.</summary>
        private static List<string> NonCommentLines(string text)
        {
            List<string> lines = new List<string>();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    lines.Add(line);
                }
            }
            return lines;
        }
    }
}
