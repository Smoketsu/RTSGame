// rts/tests/Sim.Tests/Data/MapLayoutTests.cs
// Tests for package A of rts/docs/specs/skirmish_ai_v2.md (data and maps for expansion): A-1 point symmetry of every 2-spawn map,
// A-2 every field reachable on the ground from both spawns, A-3 field amounts and counts, A-4 Core clearance, A-5 a depot site
// beside every expansion field, A-6 the two depot definitions, A-7 a depot works as a drop-off in the economy, A-8 the greybox
// symmetry fix. Part of Rts.Sim.Tests. Every shipped map is checked (including prototype_1v1); the greybox-only counts (A-3) and spot
// checks (A-8) name their map.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Package A: map layout, field data and the depot definitions.</summary>
    [TestFixture]
    public sealed class MapLayoutTests
    {
        /// <summary>The shipped data, loaded once. Range: the data folder.</summary>
        private static readonly DataSet Data = TestData.LoadAll();

        /// <summary>
        /// Smallest distance from any field tile centre to the Core centre. Unit: tiles. Range: 0 to 64. Source: map authoring rule of
        /// skirmish_ai_v2.md A.3 (new fields are 23 tiles or more from the Core; the whole map keeps 12 tiles).
        /// </summary>
        private const int MinCoreClearanceTiles = 12;

        /// <summary>Innermost Chebyshev ring searched for a depot site. Unit: tiles. Range: 1 to 10. Source: depot_ring_min_tiles in rts/data/ai/aggressive.json.</summary>
        private const int DepotRingMin = 3;

        /// <summary>Outermost Chebyshev ring searched for a depot site. Unit: tiles. Range: depot ring min to 20. Source: depot_ring_max_tiles in rts/data/ai/aggressive.json.</summary>
        private const int DepotRingMax = 6;

        /// <summary>A field this close to a spawn is a main field, not an expansion. Unit: tiles. Range: 1 to 64. Source: skirmish_ai_v2.md A.3 (naturals are 12.7 tiles or more from the HQ).</summary>
        private const int MainFieldRadiusTiles = 10;

        /// <summary>Every shipped map ID in ordinal order.</summary>
        public static IEnumerable<string> MapIds()
        {
            foreach (string id in Data.Maps.Keys)
            {
                yield return id;
            }
        }

        /// <summary>Every shipped map with exactly two spawns, in ordinal order.</summary>
        public static IEnumerable<string> TwoSpawnMapIds()
        {
            foreach (KeyValuePair<string, MapDefinition> pair in Data.Maps)
            {
                if (pair.Value.Spawns.Count == 2)
                {
                    yield return pair.Key;
                }
            }
        }

        /// <summary>The three grey-box maps A.3 extends, in ordinal order.</summary>
        public static IEnumerable<string> GreyboxMapIds()
        {
            yield return "greybox_1v1";
            yield return "long_lowland";
            yield return "twin_chokes";
        }

        /// <summary>A-1: height, ramp, spawns, fields and Core are point-symmetric under (x, y) to (W - 1 - x, H - 1 - y).</summary>
        [TestCaseSource(nameof(TwoSpawnMapIds))]
        public void A1_TwoSpawnMapsAreSymmetricUnderTheHalfTurn(string mapId)
        {
            MapDefinition map = Data.RequireMap(mapId);
            // Map width and height. Unit: tiles. Range: 8 to 1024.
            int w = map.WidthTiles;
            int h = map.HeightTiles;
            // Tile column. Range: 0 to w - 1.
            for (int x = 0; x < w; x++)
            {
                // Tile row. Range: 0 to h - 1.
                for (int y = 0; y < h; y++)
                {
                    // Index of the tile and of its half-turn image. Range: 0 to tile count - 1.
                    int a = y * w + x;
                    int b = (h - 1 - y) * w + (w - 1 - x);
                    Assert.That(map.HeightAt(a), Is.EqualTo(map.HeightAt(b)), mapId + " height (" + x + ", " + y + ")");
                    Assert.That(map.IsRampAt(a), Is.EqualTo(map.IsRampAt(b)), mapId + " ramp (" + x + ", " + y + ")");
                }
            }
            Assert.That(map.Spawns[1].X, Is.EqualTo(Fix64.FromInt(w) - map.Spawns[0].X), mapId + " spawn x");
            Assert.That(map.Spawns[1].Y, Is.EqualTo(Fix64.FromInt(h) - map.Spawns[0].Y), mapId + " spawn y");
            // Field multiset as "x,y,kind,amount" strings in sorted order, and the same after the half turn.
            List<string> fields = new List<string>();
            List<string> turned = new List<string>();
            foreach (ResourceField field in map.ResourceFields)
            {
                fields.Add(field.X + "," + field.Y + "," + field.Kind + "," + field.Amount);
                turned.Add((w - 1 - field.X) + "," + (h - 1 - field.Y) + "," + field.Kind + "," + field.Amount);
            }
            fields.Sort(StringComparer.Ordinal);
            turned.Sort(StringComparer.Ordinal);
            Assert.That(turned, Is.EqualTo(fields), mapId + " fields");
            Assert.That(map.Core, Is.Not.Null, mapId + " has a Core");
            Assert.That(map.Core!.Centre.X, Is.EqualTo(Fix64.FromInt(w) - map.Core.Centre.X), mapId + " Core x");
            Assert.That(map.Core.Centre.Y, Is.EqualTo(Fix64.FromInt(h) - map.Core.Centre.Y), mapId + " Core y");
        }

        /// <summary>A-2: every field tile is reachable on the ground from both spawn tiles.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void A2_EveryFieldIsReachableOnTheGroundFromEverySpawn(string mapId)
        {
            MapDefinition map = Data.RequireMap(mapId);
            Simulation sim = TestWorld.Started(3, map);
            TerrainGrid terrain = sim.Terrain;
            foreach (FixVec2 spawn in map.Spawns)
            {
                // Tiles reached by the search from this spawn's tile.
                bool[] seen = Flood(terrain, terrain.IndexOf(spawn.X.FloorToInt(), spawn.Y.FloorToInt()));
                foreach (ResourceField field in map.ResourceFields)
                {
                    Assert.That(seen[terrain.IndexOf(field.X, field.Y)], Is.True, mapId + " field (" + field.X + ", " + field.Y + ") from spawn " + spawn.X + ", " + spawn.Y);
                }
            }
        }

        /// <summary>Breadth-first search over ground steps (fixed direction order); returns the reached flags per tile.</summary>
        private static bool[] Flood(TerrainGrid terrain, int start)
        {
            // Reached flag per tile.
            bool[] seen = new bool[terrain.TileCount];
            // Queue of tile indices still to expand.
            Queue<int> queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                // Tile being expanded.
                int from = queue.Dequeue();
                // Direction number. Range: 0 to 7.
                for (int d = 0; d < TerrainGrid.DirectionCount; d++)
                {
                    // Neighbour tile index, or -1 off the map.
                    int to = terrain.Neighbour(from, d);
                    if (to >= 0 && !seen[to] && terrain.IsStepPassable(from, to, MovementLayer.Ground))
                    {
                        seen[to] = true;
                        queue.Enqueue(to);
                    }
                }
            }
            return seen;
        }

        /// <summary>A-3: common fields hold 3000, rare ones 600, and the three grey-box maps hold the stated counts.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void A3_FieldAmounts(string mapId)
        {
            foreach (ResourceField field in Data.RequireMap(mapId).ResourceFields)
            {
                Assert.That(field.Amount, Is.EqualTo(field.Kind == ResourceKind.Common ? 3000 : 600), mapId + " field (" + field.X + ", " + field.Y + ")");
            }
        }

        /// <summary>A-3: field counts of the three grey-box maps (total, common).</summary>
        [TestCase("greybox_1v1", 12, 10)]
        [TestCase("long_lowland", 14, 10)]
        [TestCase("twin_chokes", 12, 10)]
        public void A3_FieldCounts(string mapId, int total, int common)
        {
            MapDefinition map = Data.RequireMap(mapId);
            // Common fields counted. Range: 0 to total.
            int commons = 0;
            foreach (ResourceField field in map.ResourceFields)
            {
                commons += field.Kind == ResourceKind.Common ? 1 : 0;
            }
            Assert.That(map.ResourceFields.Count, Is.EqualTo(total));
            Assert.That(commons, Is.EqualTo(common));
        }

        /// <summary>A-4: every field is at least 12 tiles from the Core centre.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void A4_FieldsKeepClearOfTheCore(string mapId)
        {
            MapDefinition map = Data.RequireMap(mapId);
            // Squared minimum distance. Unit: squared tiles.
            Fix64 minSquared = Fix64.FromInt(MinCoreClearanceTiles * MinCoreClearanceTiles);
            foreach (ResourceField field in map.ResourceFields)
            {
                // Field tile centre. Unit: tiles.
                FixVec2 centre = new FixVec2(Fix64.FromInt(field.X) + Fix64.Half, Fix64.FromInt(field.Y) + Fix64.Half);
                Assert.That((centre - map.Core!.Centre).LengthSquared() >= minSquared, Is.True, mapId + " field (" + field.X + ", " + field.Y + ")");
            }
        }

        /// <summary>A-5: beside every common field that is more than 10 tiles from every spawn a flat depot site exists on rings 3 to 6.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void A5_EveryExpansionFieldHasADepotSite(string mapId)
        {
            MapDefinition map = Data.RequireMap(mapId);
            Simulation sim = TestWorld.Started(3, map);
            // Expansion fields tested. Range: 0 upward.
            int tested = 0;
            foreach (ResourceField field in map.ResourceFields)
            {
                if (field.Kind != ResourceKind.Common || NearSpawn(map, field))
                {
                    continue;
                }
                tested++;
                Assert.That(HasDepotSite(sim.Terrain, map, field), Is.True, mapId + " field (" + field.X + ", " + field.Y + ")");
            }
            Assert.That(tested, Is.GreaterThanOrEqualTo(2), mapId + " has expansion fields");
        }

        /// <summary>True when the field is within 10 tiles of any spawn (a main field).</summary>
        private static bool NearSpawn(MapDefinition map, ResourceField field)
        {
            // Field tile centre. Unit: tiles.
            FixVec2 centre = new FixVec2(Fix64.FromInt(field.X) + Fix64.Half, Fix64.FromInt(field.Y) + Fix64.Half);
            foreach (FixVec2 spawn in map.Spawns)
            {
                if ((centre - spawn).LengthSquared() <= Fix64.FromInt(MainFieldRadiusTiles * MainFieldRadiusTiles))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when some tile on Chebyshev rings 3 to 6 has a flat unblocked 3x3 block on the field's layer, no ramp, and is more than 2 tiles from every field.</summary>
        private static bool HasDepotSite(TerrainGrid terrain, MapDefinition map, ResourceField field)
        {
            // Height layer of the field tile. Range: 0 to 7.
            byte layer = terrain[terrain.IndexOf(field.X, field.Y)].Height;
            // Column offset. Unit: tiles. Range: -6 to 6.
            for (int dx = -DepotRingMax; dx <= DepotRingMax; dx++)
            {
                // Row offset. Unit: tiles. Range: -6 to 6.
                for (int dy = -DepotRingMax; dy <= DepotRingMax; dy++)
                {
                    // Chebyshev ring of this offset. Unit: tiles.
                    int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (ring < DepotRingMin || ring > DepotRingMax)
                    {
                        continue;
                    }
                    if (FlatBlock(terrain, field.X + dx, field.Y + dy, layer) && ClearOfFields(map, field.X + dx, field.Y + dy))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True when the 3x3 block around (x, y) is on the map, unblocked, not a ramp and on <paramref name="layer"/>.</summary>
        private static bool FlatBlock(TerrainGrid terrain, int x, int y, byte layer)
        {
            // Block row and column offsets. Unit: tiles. Range: -1 to 1.
            for (int ny = -1; ny <= 1; ny++)
            {
                for (int nx = -1; nx <= 1; nx++)
                {
                    if (!terrain.Contains(x + nx, y + ny))
                    {
                        return false;
                    }
                    // The tile being tested.
                    TerrainTile tile = terrain[terrain.IndexOf(x + nx, y + ny)];
                    if (tile.IsBlocked || tile.IsRamp || tile.Height != layer)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>True when (x, y) is more than 2 tiles (Chebyshev) from every field.</summary>
        private static bool ClearOfFields(MapDefinition map, int x, int y)
        {
            foreach (ResourceField other in map.ResourceFields)
            {
                if (Math.Max(Math.Abs(other.X - x), Math.Abs(other.Y - y)) <= 2)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A-6: both depot definitions load as complete drop-off buildings.</summary>
        [TestCase("skai.perch_depot", "skai", 350)]
        [TestCase("water.pearl_depot", "water", 400)]
        public void A6_DepotDefinitions(string id, string faction, int health)
        {
            UnitDefinition depot = Data.Content.Units[id];
            Assert.That(depot.Economy.IsDropOff, Is.True);
            Assert.That(depot.Economy.FootprintTiles, Is.EqualTo(1));
            Assert.That(depot.FactionId, Is.EqualTo(faction));
            Assert.That(depot.Economy.Trains, Is.Empty);
            Assert.That(depot.Economy.ProvidesSupply, Is.EqualTo(0));
            Assert.That(depot.CostCommon, Is.EqualTo(100));
            Assert.That(depot.CostRare, Is.EqualTo(0));
            Assert.That(depot.MaxHealth, Is.EqualTo(Fix64.FromInt(health)));
        }

        /// <summary>
        /// A-7: a worker gathering field 6 at (21, 10) next to a complete Perch Depot at (21, 7) deposits there although the Rooted HQ
        /// at (7, 7) is 14 tiles away; while the depot is still under construction the load goes to the HQ. (The spec's 20 tiles
        /// cannot be built on this map: a script-spawned HQ only starts Rooted beside a field.)
        /// </summary>
        [Test]
        public void A7_DepotIsADropOffOnceComplete()
        {
            List<ResourceDepositedEvent> complete = RunDepotGame(false, out EntityId depotComplete, out EntityId hqComplete);
            Assert.That(complete, Is.Not.Empty);
            foreach (ResourceDepositedEvent evt in complete)
            {
                Assert.That(evt.DropOff, Is.EqualTo(depotComplete));
            }
            Assert.That(depotComplete, Is.Not.EqualTo(hqComplete));
            List<ResourceDepositedEvent> building = RunDepotGame(true, out EntityId depotBuilding, out EntityId hqBuilding);
            Assert.That(building, Is.Not.Empty);
            Assert.That(building[0].DropOff, Is.EqualTo(hqBuilding), "the first deposit goes to the HQ while the depot is not complete");
            Assert.That(depotBuilding, Is.Not.EqualTo(hqBuilding));
        }

        /// <summary>
        /// Plays up to 1200 ticks on greybox_1v1: Rooted HQ at (7, 7), a Perch Depot at tile (21, 7) (complete from a script, or placed by
        /// a second worker when <paramref name="underConstruction"/>, in which case the run stops at the first deposit) and a worker
        /// gathering field 6 at (21, 10). Returns the deposit events (three at most).
        /// </summary>
        private static List<ResourceDepositedEvent> RunDepotGame(bool underConstruction, out EntityId depot, out EntityId hq)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "Skai"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            PlayerId owner = new PlayerId(0);
            // Next script sequence number. Range: 0 upward.
            uint sequence = 0;
            hq = Spawn(sim, "skai.great_aerie", owner, "7", "7", ref sequence);
            Assert.That(sim.Units.TryGet(hq, out Unit? hqUnit) && hqUnit.HqState == HqState.Rooted, Is.True, "the HQ starts Rooted beside a field");
            EntityId worker = Spawn(sim, "skai.fledgling", owner, "20", "9", ref sequence);
            // A second worker places the depot, so the gatherer is never the builder.
            EntityId builder = Spawn(sim, "skai.fledgling", owner, "22", "9", ref sequence);
            if (underConstruction)
            {
                sim.Players.TryGet(owner, out Player? record);
                record!.SetStock(500, 0);
                sim.Submit(new BuildStructureCommand(sim.Clock.Tick, owner, sequence++, builder, "skai.perch_depot", 21, 7));
                sim.Step();
                depot = EntityId.None;
                foreach (Unit unit in sim.Units.All)
                {
                    if (unit.Definition.Id == "skai.perch_depot")
                    {
                        depot = unit.Id;
                    }
                }
                Assert.That(depot, Is.Not.EqualTo(EntityId.None), "the depot was placed");
            }
            else
            {
                depot = Spawn(sim, "skai.perch_depot", owner, "21.5", "7.5", ref sequence);
            }
            sim.Submit(new GatherCommand(sim.Clock.Tick, owner, sequence++, worker, 6));
            // Deposits seen while stepping.
            List<ResourceDepositedEvent> deposits = new List<ResourceDepositedEvent>();
            // Tick counter. Unit: ticks. Range: 0 to 1200.
            for (int i = 0; i < 1200 && deposits.Count < 3; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is ResourceDepositedEvent deposited)
                    {
                        deposits.Add(deposited);
                    }
                    Assert.That(evt is CommandRejectedEvent, Is.False, evt.Describe());
                }
                if (underConstruction && deposits.Count > 0)
                {
                    // The first deposit must happen before the depot is complete (build time 20 s = 400 ticks).
                    Assert.That(sim.Units.TryGet(depot, out Unit? building) && building.IsUnderConstruction, Is.True, "first deposit came while the depot was still being built");
                    break;
                }
            }
            return deposits;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private static EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y, ref uint sequence)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
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

        /// <summary>A-8: the spot checks of the greybox symmetry fix (height, ramp) at the rim.</summary>
        [TestCase(31, 43, 0, true)]
        [TestCase(32, 43, 1, true)]
        [TestCase(43, 31, 0, true)]
        [TestCase(43, 32, 1, true)]
        [TestCase(44, 31, 1, false)]
        [TestCase(44, 32, 1, false)]
        [TestCase(32, 32, 0, false)]
        public void A8_GreyboxSymmetryFixSpotChecks(int x, int y, int height, bool ramp)
        {
            MapDefinition map = Data.RequireMap("greybox_1v1");
            // Tile index. Range: 0 to 4095.
            int index = y * map.WidthTiles + x;
            Assert.That(map.HeightAt(index), Is.EqualTo(height));
            Assert.That(map.IsRampAt(index), Is.EqualTo(ramp));
        }
    }
}
