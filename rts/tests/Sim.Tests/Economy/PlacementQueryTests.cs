// rts/tests/Sim.Tests/Economy/PlacementQueryTests.cs
// Acceptance tests IS-1 to IS-4 of package I1 of rts/docs/specs/interface_support.md: EconomyRules.PlacementProblem (Unity's ghost query) and
// BuildStructureCommand share one check, so the query names the reason the command would give, hides what the team cannot see, and changes nothing.
// IS-5 (unchanged behaviour) is the existing WorkerConstructionTests, WallTests and prerequisite tests plus every pin, run unchanged. Part of Rts.Sim.Tests.
// Economy setup: TestWorld.StartedEconomy (20 ticks/s, P0 200 common, economy map 64 by 64), P0's worker at (14, 14).
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>The placement query and its agreement with build_structure.</summary>
    [TestFixture]
    public class PlacementQueryTests
    {
        /// <summary>ID of the tech building a gated structure requires.</summary>
        private const string TechId = "test.is_tech";

        /// <summary>ID of the gated structure: footprint 1, cost 10, requires <see cref="TechId"/>.</summary>
        private const string GatedTowerId = "test.is_gated_tower";

        /// <summary>The default test catalog plus a tech building and a structure that requires it.</summary>
        private static Simulation Sim()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition(TechId, "Tech", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(200), Fix64.Zero, 0, Fix64.Half, 50, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string>(), false, 1, 0)));
            catalog.AddUnit(new UnitDefinition(GatedTowerId, "Gated tower", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.Zero, 0, Fix64.Half, 10, 0,
                "armoured", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), false, 1, 0, false, new List<string> { TechId })));
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog, TestWorld.TwoPlayers(),
                BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap()));
            sim.Start();
            return sim;
        }

        /// <summary>The query's answer, then the command's rejection reason (null when accepted) on the next tick; both must equal <paramref name="expected"/>.</summary>
        private static void Agree(Simulation sim, EntityId worker, string definition, int x, int y, string? expected)
        {
            Assert.That(EconomyRules.PlacementProblem(sim, P0, definition, x, y), Is.EqualTo(expected), "query " + definition + " " + x + "," + y);
            List<string> reasons = Reasons(Do(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, definition, x, y)));
            Assert.That(reasons.Count == 0 ? null : reasons[0], Is.EqualTo(expected), "command " + definition + " " + x + "," + y);
        }

        /// <summary>A started sim with the P0 worker at (14, 14).</summary>
        private static Simulation WithWorker(out EntityId worker)
        {
            Simulation sim = Sim();
            worker = Spawn(sim, TestWorld.WorkerId, P0, 14, 14);
            return sim;
        }

        /// <summary>Orders a barracks on (x, 14) by a new worker at (x - 4, 15) and returns nothing; the order costs 100 common.</summary>
        private static void PendingBarracks(Simulation sim, int x)
        {
            EntityId other = Spawn(sim, TestWorld.WorkerId, P0, x - 4, 15);
            AssertAccepted(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), other, TestWorld.BarracksId, x, 14));
        }

        /// <summary>IS-1: for every reason, the query equals the command's rejection (null equals accepted).</summary>
        [Test]
        public void Is01_QueryAgreesWithTheCommand()
        {
            // Rejections that need nothing but the worker.
            Simulation sim = WithWorker(out EntityId worker);
            Agree(sim, worker, "test.nope", 20, 14, "unknown_definition");
            Agree(sim, worker, TestWorld.RunnerId, 20, 14, "not_a_structure");
            Agree(sim, worker, GatedTowerId, 20, 14, "missing_prerequisite");
            Agree(sim, worker, TestWorld.BarracksId, 64, 14, "out_of_bounds");
            Agree(sim, worker, TestWorld.BarracksId, 40, 40, "site_unexplored");
            Agree(sim, worker, TestWorld.TowerId, 20, 14, "no_base");
            // Accepted last: it spends.
            Agree(sim, worker, TestWorld.BarracksId, 20, 14, null);

            // A footprint on a visible tile.
            sim = WithWorker(out worker);
            Spawn(sim, TestWorld.BarracksId, P0, 16, 16);
            Agree(sim, worker, TestWorld.BarracksId, 16, 16, "tile_blocked");

            // An own unit on the tile.
            sim = WithWorker(out worker);
            Spawn(sim, TestWorld.RunnerId, P0, 18, 14);
            Agree(sim, worker, TestWorld.BarracksId, 18, 14, "tile_occupied");

            // Another own worker's order on the tile.
            sim = WithWorker(out worker);
            PendingBarracks(sim, 20);
            Agree(sim, worker, TestWorld.BarracksId, 20, 14, "site_pending");

            // The base already holds the most static defences (6 in the WC-12 layout, the 7th is the one that is rejected).
            sim = WithWorker(out worker);
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            for (int x = 16; x <= 21; x++)
            {
                Spawn(sim, TestWorld.TowerId, P0, x, 10);
            }
            Agree(sim, worker, TestWorld.TowerId, 22, 10, "static_defence_cap");

            // Nothing left to pay with: two pending barracks spend all 200.
            sim = WithWorker(out worker);
            PendingBarracks(sim, 20);
            PendingBarracks(sim, 18);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(0));
            Agree(sim, worker, TestWorld.BarracksId, 16, 12, "insufficient_resources");

            // Supply: HQ plus ten workers fill the cap of 10 (the test HQ costs none), so a tower does not fit.
            sim = WithWorker(out worker);
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            for (int i = 0; i < 9; i++)
            {
                Spawn(sim, TestWorld.WorkerId, P0, 10 + i, 17);
            }
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(PlayerOf(sim, P0).SupplyCap));
            Agree(sim, worker, TestWorld.TowerId, 16, 10, "supply_cap");

            // A gated structure is accepted once its tech stands.
            sim = WithWorker(out worker);
            Spawn(sim, TechId, P0, 16, 16);
            Agree(sim, worker, GatedTowerId, 20, 14, null);

            // A player with no record cannot build.
            Assert.That(EconomyRules.PlacementProblem(sim, new PlayerId(9), TestWorld.BarracksId, 20, 14), Is.EqualTo("cannot_build"));
        }

        /// <summary>IS-2: the order of the checks holds when several apply at once.</summary>
        [Test]
        public void Is02_Order()
        {
            // An own unit and an own pending order on one tile: tile_occupied comes first.
            Simulation sim = WithWorker(out EntityId worker);
            PendingBarracks(sim, 20);
            Spawn(sim, TestWorld.RunnerId, P0, 20, 14);
            Agree(sim, worker, TestWorld.BarracksId, 20, 14, "tile_occupied");

            // An unexplored tile with a map wall: site_unexplored comes first.
            sim = WithWorker(out worker);
            StatusTestWorld.Run(sim, sim.Clock.Tick, 7101, s => s.Terrain.AddBlockers(s, s.Terrain.IndexOf(40, 40), TileBlockers.Wall));
            sim.Step();
            Agree(sim, worker, TestWorld.BarracksId, 40, 40, "site_unexplored");

            // An unaffordable tower with no base: no_base comes first.
            sim = WithWorker(out worker);
            PendingBarracks(sim, 20);
            PendingBarracks(sim, 18);
            Agree(sim, worker, TestWorld.TowerId, 16, 12, "no_base");
        }

        /// <summary>IS-3: on an explored tile nobody of the team sees now, the query reveals no hidden unit or structure and the command still does; on a visible tile they agree.</summary>
        [Test]
        public void Is03_Fog()
        {
            Simulation sim = WithWorker(out EntityId worker);
            EntityId spotter = Spawn(sim, TestWorld.RunnerId, P0, 40, 14);
            TeamId team = sim.Players.TeamOf(P0);
            Do(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), spotter, At(60, 60)));
            // Ticks stepped while waiting for the tiles to go dark. Unit: ticks. Range: 0 to 400.
            for (int i = 0; i < 400 && (sim.Vision.IsTileVisible(team, 40, 14) || sim.Vision.IsTileVisible(team, 41, 15)); i++)
            {
                sim.Step();
            }
            Assert.That(sim.Vision.IsTileVisible(team, 40, 14), Is.False);
            Assert.That(sim.Vision.IsTileExplored(team, 41, 15), Is.True);
            Spawn(sim, TestWorld.RunnerId, P1, 40, 14);
            Spawn(sim, TestWorld.TowerId, P1, 41, 15);
            StatusTestWorld.Run(sim, sim.Clock.Tick, 7102, s => s.Terrain.AddBlockers(s, s.Terrain.IndexOf(42, 14), TileBlockers.Wall));
            sim.Step();
            Assert.That(sim.Vision.IsTileVisible(team, 40, 14), Is.False);
            Assert.That(sim.Vision.IsTileVisible(team, 41, 15), Is.False);
            Assert.That(sim.Vision.IsTileVisible(team, 42, 14), Is.False);
            // Hidden: the query says nothing about the unit and the structure; the map wall is public.
            Assert.That(EconomyRules.PlacementProblem(sim, P0, TestWorld.BarracksId, 40, 14), Is.Null);
            Assert.That(EconomyRules.PlacementProblem(sim, P0, TestWorld.BarracksId, 41, 15), Is.Null);
            Assert.That(EconomyRules.PlacementProblem(sim, P0, TestWorld.BarracksId, 42, 14), Is.EqualTo("tile_blocked"));
            // The command still reveals them.
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 40, 14), "tile_occupied");
            AssertRejected(sim, new BuildStructureCommand(sim.Clock.Tick, P0, Seq(), worker, TestWorld.BarracksId, 41, 15), "tile_blocked");
            // Made visible, the query equals the command.
            Spawn(sim, TestWorld.RunnerId, P0, 41, 14);
            Assert.That(sim.Vision.IsTileVisible(team, 40, 14), Is.True);
            Assert.That(sim.Vision.IsTileVisible(team, 41, 15), Is.True);
            Agree(sim, worker, TestWorld.BarracksId, 41, 15, "tile_blocked");
            Agree(sim, worker, TestWorld.BarracksId, 40, 14, "tile_occupied");
        }

        /// <summary>IS-4: the query does not throw between steps, leaves the state hash and the event log unchanged.</summary>
        [Test]
        public void Is04_Pure()
        {
            Simulation sim = WithWorker(out EntityId worker);
            Assert.That(worker.IsValid, Is.True);
            // State before the calls.
            ulong before = sim.ComputeStateHash();
            int events = sim.Events.Current.Count;
            for (int i = 0; i < 100; i++)
            {
                EconomyRules.PlacementProblem(sim, P0, i % 2 == 0 ? TestWorld.BarracksId : TestWorld.TowerId, 10 + i % 40, 14);
            }
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(before));
            Assert.That(sim.Events.Current.Count, Is.EqualTo(events));
        }
    }
}
