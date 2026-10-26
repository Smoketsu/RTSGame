// rts/tests/Sim.Tests/TestWorld.cs
// Shared test fixtures: an in-code content catalog, players and a ready simulation, so tests do not
// depend on the data loader or on files. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Tests.Data;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests
{
    /// <summary>Builders for small deterministic worlds used across the test suite.</summary>
    public static class TestWorld
    {
        /// <summary>ID of the test unit type: a ground unit moving 2 tiles per second.</summary>
        public const string RunnerId = "test.runner";

        /// <summary>ID of the armed test unit: ground, 100 health, speed 2, radius 0.5, light armour; kinetic 10 damage, range 5, cooldown 0.8 s (16 ticks), hits ground and water.</summary>
        public const string ShooterId = "test.shooter";

        /// <summary>ID of an unarmed air unit: 100 health, speed 2, radius 0.5, light armour.</summary>
        public const string FlyerId = "test.flyer";

        /// <summary>ID of an unarmed ground unit with armoured armour: 100 health, speed 2, radius 0.5.</summary>
        public const string TankId = "test.tank";

        /// <summary>ID of an unarmed, fragile ground unit: 15 health (dies to two shooter hits), speed 2, radius 0.5, light armour.</summary>
        public const string GlassId = "test.glass";

        /// <summary>ID of the armed test mortar: ground, 100 health, radius 0.5; blast 20 damage, range 6, cooldown 1.0 s, hits all layers.</summary>
        public const string MortarId = "test.mortar";

        /// <summary>ID of the test mobile HQ: ground, 200 health, speed 2, radius 0.5; Rooted armour "armoured", Uprooted armour "light"; weapon kinetic 10 damage, range 5, cooldown 0.8 s, hits ground and water (fires only while Rooted); trains <see cref="WorkerId"/>.</summary>
        public const string HqId = "test.hq";

        /// <summary>ID of the test worker: ground, 40 health, speed 4, radius 0.3, light armour, supply 1, cost 50 common, build time 5 s (100 ticks), can gather.</summary>
        public const string WorkerId = "test.worker";

        /// <summary>ID of the test production building: 200 health, no movement, armoured, cost 100 common, build time 5 s (100 ticks), footprint 1, provides 8 supply, trains <see cref="RunnerId"/> and <see cref="WorkerId"/>.</summary>
        public const string BarracksId = "test.barracks";

        /// <summary>ID of the test static defence: 100 health, no movement, light armour, cost 10 common, build time 2 s (40 ticks), footprint 1, kinetic 10 damage at range 6 (ground and water); supply comes from tuning/supply.json (1).</summary>
        public const string TowerId = "test.tower";

        /// <summary>ID of the test faction with no mechanic.</summary>
        public const string TestFactionId = "test";

        /// <summary>ID of a faction using the Skai Momentum plug-in shell.</summary>
        public const string SkaiFactionId = "skai";

        /// <summary>Default tick rate for tests (the design default).</summary>
        public const int TicksPerSecond = 20;

        /// <summary>Default hash interval for tests: every 10 ticks, so short tests still produce several hashes.</summary>
        public const int HashInterval = 10;

        /// <summary>Tuning loaded once from the real rts/data files, so core tests use the shipped numbers.</summary>
        private static Tuning? _tuning;

        /// <summary>The shipped tuning (every file under rts/data/tuning), loaded on first use.</summary>
        public static Tuning Tuning
        {
            get
            {
                if (_tuning == null)
                {
                    _tuning = TestData.LoadAll().Tuning;
                }
                return _tuning;
            }
        }

        /// <summary>Side length of the flat test map. Unit: tiles.</summary>
        public const int FlatMapSize = 64;

        /// <summary>A flat 64 by 64 map with no ramps, fields or Core and two spawns, for movement tests.</summary>
        public static MapDefinition FlatMap()
        {
            return new MapDefinition("test_flat", "Test Flat", FlatMapSize, FlatMapSize,
                new List<FixVec2> { FixVec2.FromInts(4, 4), FixVec2.FromInts(60, 60) });
        }

        /// <summary>
        /// A flat 64 by 64 map with three resource fields, for economy tests: a common field with 12 units at tile (10, 10) (three
        /// trips of 5, 5 and 2), a rare field with 50 at (30, 10) and a large common field with 1500 at (50, 50). Spawns at (4, 4) and (60, 60).
        /// </summary>
        public static MapDefinition EconomyMap()
        {
            return new MapDefinition("test_economy", "Test Economy", FlatMapSize, FlatMapSize,
                new List<FixVec2> { FixVec2.FromInts(4, 4), FixVec2.FromInts(60, 60) }, null, null,
                new List<ResourceField>
                {
                    new ResourceField(10, 10, ResourceKind.Common, 12),
                    new ResourceField(30, 10, ResourceKind.Rare, 50),
                    new ResourceField(50, 50, ResourceKind.Common, 1500),
                }, null);
        }

        /// <summary>A catalog with the test units (runner, shooter, flyer, tank, glass, mortar, HQ, worker, barracks, tower) and two factions.</summary>
        public static ContentCatalog Catalog()
        {
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddUnit(new UnitDefinition(RunnerId, "Runner", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0));
            catalog.AddUnit(new UnitDefinition(ShooterId, "Shooter", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.FromInt(10), "kinetic", Fix64.FromInt(5), Fix64.Parse("0.8"), true, true, false)));
            catalog.AddUnit(new UnitDefinition(FlyerId, "Flyer", TestFactionId, MovementLayer.Air,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(TankId, "Tank", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "armoured", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(GlassId, "Glass", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(15), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(MortarId, "Mortar", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.FromInt(20), "blast", Fix64.FromInt(6), Fix64.One, true, true, true)));
            catalog.AddUnit(new UnitDefinition(HqId, "HQ", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(200), Fix64.FromInt(2), 0, Fix64.Half, 0, 0, "armoured", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.FromInt(10), "kinetic", Fix64.FromInt(5), Fix64.Parse("0.8"), true, true, false),
                new HqDefinition("armoured", "light", WorkerId),
                new UnitEconomyDefinition(Fix64.FromInt(10), false, false, new List<string>(), false, 0, 0)));
            catalog.AddUnit(new UnitDefinition(WorkerId, "Worker", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(40), Fix64.FromInt(4), 1, Fix64.Parse("0.3"), 50, 0, "light", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(5), true, false, new List<string>(), false, 0, 0)));
            catalog.AddUnit(new UnitDefinition(BarracksId, "Barracks", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 100, 0, "armoured", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string> { RunnerId, WorkerId }, false, 1, 8)));
            catalog.AddUnit(new UnitDefinition(TowerId, "Tower", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.Zero, 1, Fix64.Half, 10, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.FromInt(10), "kinetic", Fix64.FromInt(6), Fix64.Parse("0.8"), true, true, false), null,
                new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), true, 1, 0)));
            catalog.SetCombatRules(Rules());
            catalog.AddFaction(new FactionDefinition(TestFactionId, "Test", FactionDefinition.NoMechanic, new List<string> { RunnerId }, HqId));
            catalog.AddFaction(new FactionDefinition(SkaiFactionId, "Skai Riders", "momentum", new List<string> { RunnerId }));
            return catalog;
        }

        /// <summary>
        /// Test combat rules (not the shipped tuning): kinetic does 1 to light and 0.5 to armoured, blast does 0.5 to light and
        /// 2 to armoured, acquire bonus 2 tiles, default armour light.
        /// </summary>
        public static CombatRules Rules()
        {
            SortedDictionary<string, IReadOnlyDictionary<string, Fix64>> table = new SortedDictionary<string, IReadOnlyDictionary<string, Fix64>>();
            table.Add("kinetic", new SortedDictionary<string, Fix64> { { "light", Fix64.One }, { "armoured", Fix64.Half } });
            table.Add("blast", new SortedDictionary<string, Fix64> { { "light", Fix64.Half }, { "armoured", Fix64.FromInt(2) } });
            return new CombatRules(table, Fix64.FromInt(2), "light");
        }

        /// <summary>Two players on two teams: P0 test faction, P1 Skai.</summary>
        public static List<PlayerSetup> TwoPlayers()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), TestFactionId, "Alice"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), SkaiFactionId, "Bob"),
            };
        }

        /// <summary>A started simulation with the default catalog, the given players and seed.</summary>
        public static Simulation StartedWith(List<PlayerSetup> players, ulong seed = 1234)
        {
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(seed, TicksPerSecond, HashInterval),
                Catalog(),
                players,
                BuiltInMechanics.CreateRegistry(),
                Tuning,
                FlatMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>
        /// A started simulation for economy tests: the default catalog, P0 (test faction) against P1 (Skai), the economy map (or
        /// <paramref name="map"/>), the shipped tuning (or <paramref name="tuning"/>) and the given seed.
        /// </summary>
        public static Simulation StartedEconomy(ulong seed = 1234, Tuning? tuning = null, MapDefinition? map = null)
        {
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(seed, TicksPerSecond, HashInterval),
                Catalog(),
                TwoPlayers(),
                BuiltInMechanics.CreateRegistry(),
                tuning ?? Tuning,
                map ?? EconomyMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>A started simulation with the default catalog, two players, the flat map (or <paramref name="map"/>) and the given seed.</summary>
        public static Simulation Started(ulong seed = 1234, MapDefinition? map = null)
        {
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(seed, TicksPerSecond, HashInterval),
                Catalog(),
                TwoPlayers(),
                BuiltInMechanics.CreateRegistry(),
                Tuning,
                map ?? FlatMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }
    }
}

namespace Rts.Sim.Tests
{
    /// <summary>
    /// Single-overload wrappers around NUnit's exception assertions. NUnit 4.6 added System.Action overloads beside
    /// TestDelegate, which makes a lambda argument ambiguous under C# 9 (the version Unity 6 compiles). These
    /// wrappers take TestDelegate only, so the same source builds with NUnit 4.2 in CI, 4.6 locally and in Unity.
    /// </summary>
    public static class Expect
    {
#pragma warning disable 618 // TestDelegate is marked obsolete from NUnit 4.6; it is still the one unambiguous overload.
        /// <summary>Asserts that <paramref name="code"/> throws exactly <typeparamref name="T"/> and returns it.</summary>
        public static T Throws<T>(NUnit.Framework.TestDelegate code) where T : System.Exception
        {
            return NUnit.Framework.Assert.Throws<T>(code)!;
        }

        /// <summary>Asserts that <paramref name="code"/> completes without throwing.</summary>
        public static void DoesNotThrow(NUnit.Framework.TestDelegate code)
        {
            NUnit.Framework.Assert.DoesNotThrow(code);
        }
#pragma warning restore 618
    }
}
