// rts/tests/Sim.Tests/Terrain/TerrainAndPathingTests.cs
// Tests for the terrain grid (ADR-006) and flow-field pathfinding and movement (ADR-007): passability rules,
// speed multipliers, routing around cliffs through ramps, unreachable goals, cache invalidation, layer
// switching for Grounded flyers, terrain hashing and determinism on the shipped greybox map.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Pathing;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Terrain
{
    [TestFixture]
    public sealed class TerrainAndPathingTests
    {
        /// <summary>Side length of the small test maps. Unit: tiles.</summary>
        private const int Size = 16;

        /// <summary>
        /// A 16 × 16 map whose column x = 8 is layer 1 (a cliff wall) except for a ramp tile at (8, 2), with everything
        /// else layer 0. Ground units from the west must cross at the ramp; air ignores it.
        /// </summary>
        private static MapDefinition CliffMap()
        {
            byte[] heights = new byte[Size * Size];
            bool[] ramps = new bool[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                heights[y * Size + 8] = 1;
            }
            ramps[2 * Size + 8] = true;
            return new MapDefinition("test_cliff", "Test Cliff", Size, Size,
                new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(14, 14) }, heights, ramps, null, null);
        }

        /// <summary>A started simulation on <paramref name="map"/> with one runner for player 0 at <paramref name="at"/>.</summary>
        private static Simulation WithRunner(MapDefinition map, FixVec2 at, out Unit unit)
        {
            Simulation sim = TestWorld.Started(42, map);
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), at));
            sim.Step();
            unit = sim.Units.TryGet(new EntityId(1), out Unit? found) ? found! : throw new InvalidOperationException();
            return sim;
        }

        [Test]
        public void Passability_FollowsHeightRampAndBlockerRules()
        {
            Simulation sim = TestWorld.Started(1, CliffMap());
            TerrainGrid grid = sim.Terrain;
            int west = grid.IndexOf(7, 5);
            int wall = grid.IndexOf(8, 5);
            int ramp = grid.IndexOf(8, 2);
            int westOfRamp = grid.IndexOf(7, 2);
            Assert.That(grid.IsStepPassable(west, wall, MovementLayer.Ground), Is.False, "cliff edge blocks ground");
            Assert.That(grid.IsStepPassable(west, wall, MovementLayer.Air), Is.True, "air ignores cliffs");
            Assert.That(grid.IsStepPassable(westOfRamp, ramp, MovementLayer.Ground), Is.True, "ramp joins layers");
            Assert.That(grid.IsStepPassable(ramp, grid.IndexOf(9, 2), MovementLayer.Water), Is.True, "ramp works downhill for water units too");
            // Diagonal around the cliff corner must not cut it.
            Assert.That(grid.IsStepPassable(grid.IndexOf(7, 1), grid.IndexOf(8, 2), MovementLayer.Ground), Is.False, "no corner cutting onto the ramp from a cliff-adjacent diagonal");
            Assert.That(grid.At(8, 2).IsRamp, Is.True);
            Assert.That(grid.At(8, 5).Height, Is.EqualTo(1));
        }

        [Test]
        public void SpeedMultipliers_ComeFromTuningByLayerAndDepth()
        {
            Simulation sim = TestWorld.Started(1, TestWorld.FlatMap());
            TerrainGrid grid = sim.Terrain;
            TerrainTuning terrain = sim.Tuning.Terrain;
            FloodTuning flood = sim.Tuning.Flood;
            int tile = grid.IndexOf(3, 3);
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Ground), Is.EqualTo(terrain.GroundDrySpeedMultiplier));
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Water), Is.EqualTo(terrain.WaterUnitDrySpeedMultiplier));
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Air), Is.EqualTo(Fix64.One));
            // Flood the tile inside a step through a test command.
            sim.Submit(new SetDepthCommand(0, PlayerId.None, 0, tile, terrain.DeepWaterDepthThreshold));
            sim.Step();
            Assert.That(grid.DepthClass(tile), Is.EqualTo(2));
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Ground), Is.EqualTo(flood.DeepWaterEnemySpeedMultiplier));
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Water), Is.EqualTo(terrain.WaterUnitDeepSpeedMultiplier));
            sim.Submit(new SetDepthCommand(1, PlayerId.None, 1, tile, terrain.DeepWaterDepthThreshold / Fix64.FromInt(2)));
            sim.Step();
            Assert.That(grid.DepthClass(tile), Is.EqualTo(1));
            Assert.That(grid.SpeedMultiplier(tile, MovementLayer.Ground), Is.EqualTo(flood.ShallowWaterEnemySpeedMultiplier));
        }

        [Test]
        public void TerrainChange_OnlyInsideStep_RaisesEventAndVersion()
        {
            Simulation sim = TestWorld.Started(1, TestWorld.FlatMap());
            int before = sim.Terrain.Version;
            Expect.Throws<InvalidOperationException>(() => { sim.Terrain.SetWaterDepth(sim, 0, Fix64.One); });
            ulong hashBefore = sim.ComputeStateHash();
            sim.Submit(new SetDepthCommand(0, PlayerId.None, 0, 5, Fix64.One));
            sim.Step();
            Assert.That(sim.Terrain.Version, Is.EqualTo(before + 1));
            bool seen = false;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is TerrainChangedEvent changed && changed.TileIndex == 5)
                {
                    seen = true;
                }
            }
            Assert.That(seen, Is.True, "terrain_changed event for the tile");
            Assert.That(sim.ComputeStateHash(), Is.Not.EqualTo(hashBefore), "terrain is hashed");
        }

        [Test]
        public void GroundUnit_RoutesThroughRampAroundCliff()
        {
            Simulation sim = WithRunner(CliffMap(), new FixVec2(Fix64.FromInt(3) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half), out Unit unit);
            FixVec2 goal = new FixVec2(Fix64.FromInt(12) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half);
            sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 0, unit.Id, goal));
            // Lowest y the unit reaches: it must dip to the ramp row (y = 2) to cross the cliff.
            Fix64 lowestY = unit.Position.Y;
            bool arrived = false;
            for (int i = 0; i < 1200 && !arrived; i++)
            {
                sim.Step();
                lowestY = Fix64.Min(lowestY, unit.Position.Y);
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is UnitArrivedEvent)
                    {
                        arrived = true;
                    }
                    Assert.That(evt, Is.Not.InstanceOf<MoveUnreachableEvent>());
                }
                if (unit.HasMoveTarget)
                {
                    Assert.That(sim.Terrain.IsStandable(sim.Terrain.TileOf(unit.Position), MovementLayer.Ground), Is.True);
                }
            }
            Assert.That(arrived, Is.True, "runner reached the far side");
            Assert.That(lowestY < Fix64.FromInt(3), Is.True, "path went through the ramp row, lowest y " + lowestY);
            Assert.That((unit.Position - goal).Length() <= sim.Tuning.Terrain.ArrivalRadiusTiles, Is.True);
        }

        [Test]
        public void AirUnit_FliesStraightOverCliff()
        {
            MapDefinition map = CliffMap();
            Simulation sim = TestWorld.Started(42, map);
            // An air unit definition is added to the content before Start in a fresh catalog.
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition("test.bird", "Flyer", TestWorld.TestFactionId, MovementLayer.Air,
                Fix64.FromInt(50), Fix64.FromInt(4), 1, Fix64.Half, 50, 0));
            SimulationConfig config = new SimulationConfig(new MatchSettings(7, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                catalog, TestWorld.TwoPlayers(), Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, map);
            sim = new Simulation(config);
            sim.Start();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, "test.bird", new PlayerId(0), new FixVec2(Fix64.FromInt(3) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half)));
            sim.Step();
            Unit flyer = sim.Units.TryGet(new EntityId(1), out Unit? f) ? f! : throw new InvalidOperationException();
            sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 0, flyer.Id, new FixVec2(Fix64.FromInt(12) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half)));
            // 9 tiles at 4 tiles per second: 45 ticks; the y coordinate never changes.
            sim.Step(50);
            Assert.That(flyer.HasMoveTarget, Is.False);
            Assert.That(flyer.Position.Y, Is.EqualTo(Fix64.FromInt(10) + Fix64.Half));
        }

        [Test]
        public void GroundedFlyer_UsesGroundLayer()
        {
            Simulation sim = TestWorld.Started(42, CliffMap());
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition("test.bird", "Flyer", TestWorld.TestFactionId, MovementLayer.Air,
                Fix64.FromInt(50), Fix64.FromInt(4), 1, Fix64.Half, 50, 0));
            SimulationConfig config = new SimulationConfig(new MatchSettings(7, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                catalog, TestWorld.TwoPlayers(), Rts.Sim.Factions.BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, CliffMap());
            sim = new Simulation(config);
            sim.Start();
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, "test.bird", new PlayerId(0), new FixVec2(Fix64.FromInt(3) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half)));
            sim.Step();
            Unit flyer = sim.Units.TryGet(new EntityId(1), out Unit? f) ? f! : throw new InvalidOperationException();
            sim.Submit(new GroundCommand(1, PlayerId.None, 0, flyer.Id));
            sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 0, flyer.Id, new FixVec2(Fix64.FromInt(12) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half)));
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            sim.Step();
            Assert.That(flyer.EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            // While grounded it must use the ramp, so it leaves the start row within the first few seconds.
            sim.Step(60);
            Assert.That(flyer.Position.Y, Is.Not.EqualTo(Fix64.FromInt(10) + Fix64.Half));
        }

        [Test]
        public void UnreachableGoal_CancelsMoveWithEvent()
        {
            // Wall off the goal tile with blockers on all eight neighbours and the tile itself.
            Simulation sim = WithRunner(TestWorld.FlatMap(), FixVec2.FromInts(2, 2), out Unit unit);
            List<int> walled = new List<int>();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    walled.Add(sim.Terrain.IndexOf(20 + dx, 20 + dy));
                }
            }
            sim.Submit(new BlockCommand(1, PlayerId.None, 0, walled));
            sim.Step();
            sim.Submit(new MoveUnitCommand(2, new PlayerId(0), 0, unit.Id, new FixVec2(Fix64.FromInt(20) + Fix64.Half, Fix64.FromInt(20) + Fix64.Half)));
            sim.Step();
            bool unreachable = false;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is MoveUnreachableEvent)
                {
                    unreachable = true;
                }
            }
            Assert.That(unreachable, Is.True);
            Assert.That(unit.HasMoveTarget, Is.False);
            Assert.That(unit.Position, Is.EqualTo(FixVec2.FromInts(2, 2)));
        }

        [Test]
        public void FlowFieldCache_ReusesUntilTerrainChanges_AndEvictsOldest()
        {
            Simulation sim = TestWorld.Started(1, TestWorld.FlatMap());
            FlowField a = sim.Pathing.Get(MovementLayer.Ground, 100);
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, 100), Is.SameAs(a), "same version reuses the field");
            Assert.That(sim.Pathing.BuildCount, Is.EqualTo(1L));
            sim.Submit(new SetDepthCommand(0, PlayerId.None, 0, 7, Fix64.One));
            sim.Step();
            Assert.That(sim.Pathing.Get(MovementLayer.Ground, 100), Is.Not.SameAs(a), "a terrain change rebuilds");
            // Fill past the capacity; the count never exceeds the tuning limit.
            int capacity = sim.Tuning.Terrain.MaxCachedFlowFields;
            for (int goal = 0; goal < capacity + 5; goal++)
            {
                sim.Pathing.Get(MovementLayer.Water, goal);
            }
            Assert.That(sim.Pathing.Count, Is.LessThanOrEqualTo(capacity));
        }

        [Test]
        public void FlowField_IntegrationAndDirectionsAreDeterministic()
        {
            Simulation first = TestWorld.Started(1, CliffMap());
            Simulation second = TestWorld.Started(1, CliffMap());
            int goal = first.Terrain.IndexOf(12, 10);
            FlowField fa = first.Pathing.Get(MovementLayer.Ground, goal);
            FlowField fb = second.Pathing.Get(MovementLayer.Ground, goal);
            for (int i = 0; i < first.Terrain.TileCount; i++)
            {
                Assert.That(fa.IntegrationAt(i), Is.EqualTo(fb.IntegrationAt(i)));
                Assert.That(fa.DirectionAt(i), Is.EqualTo(fb.DirectionAt(i)));
            }
            Assert.That(fa.IntegrationAt(goal), Is.EqualTo(0));
            Assert.That(fa.DirectionAt(goal), Is.EqualTo(FlowField.NoDirection));
            // West of the cliff the goal is reachable only through the ramp: cost is larger than the straight distance.
            int west = first.Terrain.IndexOf(3, 10);
            Assert.That(fa.IsReachable(west), Is.True);
            Assert.That(fa.IntegrationAt(west), Is.GreaterThan(9 * FlowField.CostScale));
        }

        [Test]
        public void GreyboxMap_LoadsLayersRampsFieldsAndCore_AndRunsDeterministically()
        {
            DataSet data = TestData.LoadAll();
            MapDefinition map = data.RequireMap("greybox_1v1");
            Assert.That(map.HeightAt(map.WidthTiles * 8 + 8), Is.EqualTo(2), "spawn plateau is layer 2");
            Assert.That(map.HeightAt(map.WidthTiles * 32 + 32), Is.EqualTo(0), "basin is layer 0");
            Assert.That(map.IsRampAt(map.WidthTiles * 7 + 14), Is.True, "plateau exit ramp");
            Assert.That(map.ResourceFields.Count, Is.EqualTo(6));
            Assert.That(map.Core, Is.Not.Null);
            // Lowest layer under the 35% cap from flood.json.
            int lowest = 0;
            for (int i = 0; i < map.WidthTiles * map.HeightTiles; i++)
            {
                if (map.HeightAt(i) == 0)
                {
                    lowest++;
                }
            }
            Fix64 fraction = Fix64.FromInt(lowest) / Fix64.FromInt(map.WidthTiles * map.HeightTiles);
            Assert.That(fraction <= data.Tuning.Flood.LowestLayerMaxMapFraction, Is.True);
            ulong[] hashes = new ulong[2];
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = TestWorld.Started(3, map);
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, 0, TestWorld.RunnerId, new PlayerId(0), FixVec2.FromInts(8, 8)));
                sim.Step();
                sim.Submit(new MoveUnitCommand(1, new PlayerId(0), 0, new EntityId(1), FixVec2.FromInts(32, 32)));
                sim.Step(600);
                hashes[run] = sim.ComputeStateHash();
                Assert.That(sim.Units.TryGet(new EntityId(1), out Unit? u) && !u!.HasMoveTarget, Is.True, "runner reached the Core basin");
            }
            Assert.That(hashes[0], Is.EqualTo(hashes[1]));
        }

        /// <summary>Test-only command that sets a tile's water depth inside a step.</summary>
        private sealed class SetDepthCommand : Command
        {
            /// <summary>Tile to flood.</summary>
            private readonly int _tile;

            /// <summary>New depth in depth units.</summary>
            private readonly Fix64 _depth;

            public SetDepthCommand(int tick, PlayerId issuer, uint sequence, int tile, Fix64 depth) : base(tick, issuer, sequence)
            {
                _tile = tile;
                _depth = depth;
            }

            public override string Kind => "test_set_depth";

            public override CommandResult Apply(Simulation sim)
            {
                sim.Terrain.SetWaterDepth(sim, _tile, _depth);
                return CommandResult.Ok;
            }

            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
                hasher.Write(_tile);
                hasher.Write(_depth);
            }
        }

        /// <summary>Test-only command that walls a list of tiles.</summary>
        private sealed class BlockCommand : Command
        {
            /// <summary>Tiles to wall.</summary>
            private readonly List<int> _tiles;

            public BlockCommand(int tick, PlayerId issuer, uint sequence, List<int> tiles) : base(tick, issuer, sequence)
            {
                _tiles = tiles;
            }

            public override string Kind => "test_block";

            public override CommandResult Apply(Simulation sim)
            {
                foreach (int tile in _tiles)
                {
                    sim.Terrain.AddBlockers(sim, tile, TileBlockers.Wall);
                }
                return CommandResult.Ok;
            }

            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
                hasher.Write(_tiles.Count);
            }
        }

        /// <summary>Test-only command that forces a unit onto the ground layer, standing in for the Grounded status.</summary>
        private sealed class GroundCommand : Command
        {
            /// <summary>Unit to ground.</summary>
            private readonly EntityId _unit;

            public GroundCommand(int tick, PlayerId issuer, uint sequence, EntityId unit) : base(tick, issuer, sequence)
            {
                _unit = unit;
            }

            public override string Kind => "test_ground";

            public override CommandResult Apply(Simulation sim)
            {
                if (!sim.Units.TryGet(_unit, out Unit? unit))
                {
                    return CommandResult.Reject("unknown_unit");
                }
                unit.SetLayerOverride(MovementLayer.Ground);
                return CommandResult.Ok;
            }

            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
                hasher.Write(_unit);
            }
        }
    }
}
