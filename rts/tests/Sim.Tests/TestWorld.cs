// rts/tests/Sim.Tests/TestWorld.cs
// Shared test fixtures: an in-code content catalog, players and a ready simulation, so tests do not
// depend on the data loader or on files. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests
{
    /// <summary>Builders for small deterministic worlds used across the test suite.</summary>
    public static class TestWorld
    {
        /// <summary>ID of the test unit type: a ground unit moving 2 tiles per second.</summary>
        public const string RunnerId = "test.runner";

        /// <summary>ID of the test faction with no mechanic.</summary>
        public const string TestFactionId = "test";

        /// <summary>ID of a faction using the Skai Momentum plug-in shell.</summary>
        public const string SkaiFactionId = "skai";

        /// <summary>Default tick rate for tests (the design default).</summary>
        public const int TicksPerSecond = 20;

        /// <summary>Default hash interval for tests: every 10 ticks, so short tests still produce several hashes.</summary>
        public const int HashInterval = 10;

        /// <summary>A catalog with one runner unit and two factions.</summary>
        public static ContentCatalog Catalog()
        {
            ContentCatalog catalog = new ContentCatalog();
            catalog.AddUnit(new UnitDefinition(RunnerId, "Runner", TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0));
            catalog.AddFaction(new FactionDefinition(TestFactionId, "Test", FactionDefinition.NoMechanic, new List<string> { RunnerId }));
            catalog.AddFaction(new FactionDefinition(SkaiFactionId, "Skai Riders", "momentum", new List<string> { RunnerId }));
            return catalog;
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

        /// <summary>A started simulation with the default catalog, two players and the given seed.</summary>
        public static Simulation Started(ulong seed = 1234)
        {
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(seed, TicksPerSecond, HashInterval),
                Catalog(),
                TwoPlayers(),
                BuiltInMechanics.CreateRegistry());
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
