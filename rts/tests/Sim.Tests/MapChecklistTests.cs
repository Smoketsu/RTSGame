// rts/tests/Sim.Tests/MapChecklistTests.cs
// The map checklist (docs/PHASE2_PLAN.md step 6; ADR-006). Loads every map under rts/data/maps with the real data loader and checks
// the layout rules from the design's Maps section, one test per rule per map, so a failing map names the rule it breaks.
// Part of Rts.Sim.Tests. The thresholds below are constants in this fixture (not a data file) because the data loader rejects any
// tuning file it does not know, and changing the simulation was out of scope for this step. Each constant is documented where declared.
//
// Rules (all distances are in tiles, measured between tile centres or spawn points; a tile (x, y) has its centre at (x + 0.5, y + 0.5)):
//
//  1. Layers. The map uses at least MinLayerCount distinct elevation layers, and the tiles on the lowest layer used are at most
//     MaxLowestLayerPercent percent of all tiles (ADR-006).
//
//  2. Two routes. A "route" is a sequence of 8-way steps over the ground layer where every step satisfies TerrainGrid.IsStepPassable
//     (the cliff and ramp rule, no corner cutting). For every pair of spawns, route 1 is the shortest route by step count (breadth-first,
//     neighbours in the fixed N, E, S, W, NE, SE, SW, NW order). A tile is "exempt" when it lies within BaseExemptRadiusTiles of either
//     spawn of the pair: the start areas, where both routes must start and end. Every non-exempt tile within RouteSeparationTiles
//     (Chebyshev distance) of a route-1 tile is then forbidden, and route 2 is the shortest route that avoids all forbidden tiles. The
//     pair passes when route 2 exists and has at most MaxSecondRouteLengthPercent percent of route 1's steps (a detour twice as long is
//     not a usable second route). The check is greedy (it does not search for the best disjoint pair), so a map whose only disjoint
//     pair needs a longer first route can fail; authors fix that by widening the second corridor.
//
//  3. Skai cliff spots. A "cliff spot" is a tile that is higher than an edge-adjacent (N, E, S or W) tile with neither of the two being a
//     ramp, so ground units cannot step between them, and that lies within BaseZoneRadiusTiles of a spawn (a flyer perched there looks
//     down on the approach to that base). A spot is covered when some ground-reachable tile (reachable from the first spawn by route
//     rules) that is strictly lower than the spot lies within ReachTiles of it. ReachTiles comes from the unit data: the shortest
//     reach of any mobile, non-air-layer, non-melee unit whose weapon can hit air (weapon range + the shooter's radius), plus the
//     largest radius of a Skai air-layer unit (the perched flyer). Design decision (flagged in the report): the design only says "every
//     structure still stays in range of ground ranged units from below", so the simplest defensible reading is used: from below means
//     standing on a lower layer, and the shortest anti-air reach is the conservative range.
//
//  4. Rare fields. At least MinRareFields rare fields exist. Each is on the flanks: farther than FlankBandHalfWidthTiles from every
//     straight segment joining two spawns. Each is outside the Core area: farther than the Core radius plus RareCoreMarginTiles from the
//     Core centre. Each is on a tile reachable on the ground from the first spawn.
//
//  5. Core. The map has a Core, its centre lies inside the map on a tile reachable on the ground from the first spawn, and every spawn
//     is reachable from the first spawn.
//
// Each rule is also exercised against small synthetic maps built to break it, so the checker cannot pass vacuously.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests
{
    /// <summary>The map checklist: layout rules every shipped map must satisfy.</summary>
    [TestFixture]
    public sealed class MapChecklistTests
    {
        /// <summary>Fewest distinct elevation layers a map must use. Unit: layers. Range: 1 to 8. Source: ADR-006 and Maps / Layout rules (at least three elevation layers).</summary>
        private const int MinLayerCount = 3;

        /// <summary>Most tiles, as a percentage of all tiles, the lowest used layer may cover. Unit: percent. Range: 1 to 100. Source: ADR-006 (lowest layer at most 35 percent); Maps / Layout rules.</summary>
        private const int MaxLowestLayerPercent = 35;

        /// <summary>Radius around each spawn inside which route tiles are shared freely (the start area). Unit: tiles. Range: 1 to 16. Source: PLACEHOLDER (not in the design doc).</summary>
        private const double BaseExemptRadiusTiles = 5.0;

        /// <summary>Chebyshev distance around route-1 tiles that route 2 must stay clear of, outside the start areas. Unit: tiles. Range: 0 to 8. Source: PLACEHOLDER (not in the design doc); 2 means a one-tile-wide offset of the same corridor does not count.</summary>
        private const int RouteSeparationTiles = 2;

        /// <summary>Longest allowed route 2 as a percentage of route 1's length in steps. Unit: percent. Range: 100 to 1000. Source: PLACEHOLDER (not in the design doc).</summary>
        private const int MaxSecondRouteLengthPercent = 200;

        /// <summary>Distance from a spawn within which a cliff counts as overlooking that base's approach. Unit: tiles. Range: 4 to 64. Source: PLACEHOLDER (not in the design doc).</summary>
        private const double BaseZoneRadiusTiles = 24.0;

        /// <summary>Half the width of the band around the straight line between spawns where rare fields may not stand. Unit: tiles. Range: 1 to 32. Source: Economy (rare resource on the flanks); band width is a PLACEHOLDER.</summary>
        private const double FlankBandHalfWidthTiles = 10.0;

        /// <summary>Extra distance beyond the Core radius that rare fields must keep from the Core centre. Unit: tiles. Range: 0 to 16. Source: Economy (never in the Core's area); margin is a PLACEHOLDER.</summary>
        private const double RareCoreMarginTiles = 4.0;

        /// <summary>Fewest rare fields a map must have. Unit: fields. Range: 1 to 16. Source: Economy (rare on the flanks, mirrored); PLACEHOLDER count.</summary>
        private const int MinRareFields = 2;

        /// <summary>Fixed-point scale: Fix64 raw units per 1.0. Unit: raw units. Range: exactly 2^32.</summary>
        private const double FixScale = 4294967296.0;

        /// <summary>The real data set, loaded once. Range: the shipped data folder.</summary>
        private static readonly DataSet Data = TestData.LoadAll();

        /// <summary>Every shipped map ID in ordinal order, the source for the per-map test cases.</summary>
        public static IEnumerable<string> MapIds()
        {
            foreach (string id in Data.Maps.Keys)
            {
                yield return id;
            }
        }

        // ------------------------------------------------------------------ shipped maps

        /// <summary>At least three maps are shipped (the plan's map set).</summary>
        [Test]
        public void ThreeOrMoreMapsAreShipped()
        {
            Assert.That(Data.Maps.Count, Is.GreaterThanOrEqualTo(3), "the plan ships three 1v1 maps");
        }

        /// <summary>Rule 1 on one shipped map: at least three layers, lowest layer at most 35 percent of tiles.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void LowestLayerIsAtMostThirtyFivePercentAndThreeLayersExist(string mapId)
        {
            AssertNoViolations(CheckLayers(Data.RequireMap(mapId)));
        }

        /// <summary>Rule 2 on one shipped map: two separate ground routes between every pair of spawns.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void EverySpawnPairHasTwoSeparateGroundRoutes(string mapId)
        {
            AssertNoViolations(CheckRoutes(Data.RequireMap(mapId)));
        }

        /// <summary>Rule 3 on one shipped map: every cliff spot above a base is within ground fire reach from lower ground.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void EverySkaiCliffSpotIsInRangeOfGroundFire(string mapId)
        {
            AssertNoViolations(CheckCliffSpots(Data.RequireMap(mapId)));
        }

        /// <summary>Rule 4 on one shipped map: rare fields off the spawn line, outside the Core, reachable.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void RareFieldsAreOnTheFlanksAndOutsideTheCore(string mapId)
        {
            AssertNoViolations(CheckRareFields(Data.RequireMap(mapId)));
        }

        /// <summary>Rule 5 on one shipped map: a Core exists and is reachable.</summary>
        [TestCaseSource(nameof(MapIds))]
        public void MapHasAReachableCore(string mapId)
        {
            AssertNoViolations(CheckCore(Data.RequireMap(mapId)));
        }

        // ------------------------------------------------------------------ the checker catches bad maps

        /// <summary>Rule 1 fails on a one-layer map and on a map whose lowest layer is 75 percent of tiles.</summary>
        [Test]
        public void Checker_RejectsAFlatMapAndAMapWithATooLowLowestLayer()
        {
            // 16 by 16 map with only layer 0.
            MapDefinition flat = Synthetic("flat", 16, 16, (x, y) => 0, null, FixVec2.FromInts(2, 2), FixVec2.FromInts(13, 13));
            Assert.That(CheckLayers(flat), Is.Not.Empty, "one layer is fewer than three");
            // Three layers, but the lowest (0) covers the left 12 of 16 columns: 75 percent.
            MapDefinition lowHeavy = Synthetic("low_heavy", 16, 16, (x, y) => x < 12 ? 0 : (x < 14 ? 1 : 2), null, FixVec2.FromInts(2, 2), FixVec2.FromInts(13, 13));
            Assert.That(CheckLayers(lowHeavy), Is.Not.Empty, "the lowest layer covers 75 percent");
        }

        /// <summary>Rule 2 fails on a wall with one gap or two neighbouring gaps, and passes with two separate gaps.</summary>
        [Test]
        public void Checker_RejectsASingleRouteMapAndAcceptsTwoRoutes()
        {
            // A 32 by 16 map with a layer-2 wall across columns 14 to 17 and one ramp tile at each end of a single gap in row 8.
            MapDefinition single = Synthetic("one_route", 32, 16, (x, y) => (x >= 14 && x <= 17) ? 2 : 1,
                (x, y) => y == 8 && (x == 14 || x == 17), FixVec2.FromInts(3, 8), FixVec2.FromInts(28, 8));
            Assert.That(CheckRoutes(single), Is.Not.Empty, "the wall has one gap");
            // The same wall with a second gap 7 rows away (rows 8 and 15).
            MapDefinition two = Synthetic("two_routes", 32, 16, (x, y) => (x >= 14 && x <= 17) ? 2 : 1,
                (x, y) => (y == 8 || y == 15) && (x == 14 || x == 17), FixVec2.FromInts(3, 8), FixVec2.FromInts(28, 8));
            Assert.That(CheckRoutes(two), Is.Empty, CheckRoutes(two).Count > 0 ? CheckRoutes(two)[0] : string.Empty);
            // Two gaps only one row apart: the second is inside the separation distance of the first.
            MapDefinition adjacent = Synthetic("adjacent_routes", 32, 16, (x, y) => (x >= 14 && x <= 17) ? 2 : 1,
                (x, y) => (y == 8 || y == 9) && (x == 14 || x == 17), FixVec2.FromInts(3, 8), FixVec2.FromInts(28, 8));
            Assert.That(CheckRoutes(adjacent), Is.Not.Empty, "a neighbouring gap is the same corridor");
        }

        /// <summary>Rule 3 fails on a pit nothing can reach and passes once a ramp makes it reachable.</summary>
        [Test]
        public void Checker_RejectsACliffSpotWithNoReachableLowerGround()
        {
            // 24 by 24 map: layer 1 everywhere except a layer-0 pit at columns 10 to 13, rows 10 to 13 with no ramp, and layer 2 in the far corner
            // so three layers exist. The pit is unreachable on the ground, so the layer-1 tiles around it have no lower reachable tile near them.
            MapDefinition pit = Synthetic("pit", 24, 24,
                (x, y) => (x >= 10 && x <= 13 && y >= 10 && y <= 13) ? 0 : (x >= 20 && y >= 20 ? 2 : 1), null,
                FixVec2.FromInts(4, 4), FixVec2.FromInts(19, 4));
            Assert.That(CheckCliffSpots(pit), Is.Not.Empty, "the pit rim cannot be shot from below");
            // The same pit with a ramp makes it reachable, so the rim is covered.
            MapDefinition ramped = Synthetic("pit_ramp", 24, 24,
                (x, y) => (x >= 10 && x <= 13 && y >= 10 && y <= 13) ? 0 : (x >= 20 && y >= 20 ? 2 : 1),
                (x, y) => x == 9 && y == 11, FixVec2.FromInts(4, 4), FixVec2.FromInts(19, 4));
            Assert.That(CheckCliffSpots(ramped), Is.Empty, CheckCliffSpots(ramped).Count > 0 ? CheckCliffSpots(ramped)[0] : string.Empty);
        }

        /// <summary>Rule 4 fails for a rare field on the spawn line, inside the Core area, or too few rare fields.</summary>
        [Test]
        public void Checker_RejectsRareFieldsOnTheCentreLineOrInTheCore()
        {
            // Fields to place on an otherwise flat 64 by 64 map with spawns at (8, 8) and (56, 56) and the Core at (32, 32) radius 4.
            FixVec2[] spawns = { FixVec2.FromInts(8, 8), FixVec2.FromInts(56, 56) };
            CoreArea core = new CoreArea(FixVec2.FromInts(32, 32), Fix64.FromInt(4));
            // Two rare fields on the flanks: passes.
            Assert.That(CheckRareFields(FieldsMap(spawns, core, new ResourceField(8, 52, ResourceKind.Rare, 600), new ResourceField(55, 11, ResourceKind.Rare, 600))), Is.Empty);
            // One rare field on the straight line between the spawns.
            Assert.That(CheckRareFields(FieldsMap(spawns, core, new ResourceField(20, 20, ResourceKind.Rare, 600), new ResourceField(55, 11, ResourceKind.Rare, 600))), Is.Not.Empty);
            // One rare field inside the Core area, off the spawn line.
            Assert.That(CheckRareFields(FieldsMap(spawns, core, new ResourceField(8, 52, ResourceKind.Rare, 600), new ResourceField(30, 36, ResourceKind.Rare, 600))), Is.Not.Empty);
            // Only one rare field.
            Assert.That(CheckRareFields(FieldsMap(spawns, core, new ResourceField(8, 52, ResourceKind.Rare, 600))), Is.Not.Empty);
        }

        /// <summary>Rule 5 fails on a map with no Core.</summary>
        [Test]
        public void Checker_RejectsAMapWithoutACore()
        {
            // 16 by 16 flat map with no Core.
            MapDefinition noCore = Synthetic("no_core", 16, 16, (x, y) => 0, null, FixVec2.FromInts(2, 2), FixVec2.FromInts(13, 13));
            Assert.That(CheckCore(noCore), Is.Not.Empty);
        }

        // ------------------------------------------------------------------ checks (each returns the violations found; empty means pass)

        /// <summary>Rule 1: layer count and the lowest-layer share. Returns violation texts.</summary>
        private static List<string> CheckLayers(MapDefinition map)
        {
            // Violations found. Range: empty when the rule holds.
            List<string> problems = new List<string>();
            // Tiles on each elevation layer. Unit: tiles. Range: index 0 to 7, each 0 to the tile count.
            int[] perLayer = new int[8];
            // Total tile count of the map. Unit: tiles. Range: 64 to 1 048 576.
            int total = map.WidthTiles * map.HeightTiles;
            // Tile index being counted. Unit: tile index. Range: 0 to total - 1.
            for (int index = 0; index < total; index++)
            {
                perLayer[map.HeightAt(index)]++;
            }
            // Number of layers with at least one tile. Unit: layers. Range: 1 to 8.
            int used = 0;
            // Lowest layer with at least one tile, or -1 before one is found. Unit: layer. Range: -1 to 7.
            int lowest = -1;
            // Layer being inspected. Unit: layer. Range: 0 to 7.
            for (int layer = 0; layer < perLayer.Length; layer++)
            {
                if (perLayer[layer] > 0)
                {
                    used++;
                    if (lowest < 0)
                    {
                        lowest = layer;
                    }
                }
            }
            if (used < MinLayerCount)
            {
                problems.Add(map.Id + ": uses " + used + " elevation layers; at least " + MinLayerCount + " are required");
            }
            if (lowest >= 0 && (long)perLayer[lowest] * 100 > (long)MaxLowestLayerPercent * total)
            {
                problems.Add(map.Id + ": the lowest layer (" + lowest + ") covers " + perLayer[lowest] + " of " + total + " tiles, over the " + MaxLowestLayerPercent + " percent cap");
            }
            return problems;
        }

        /// <summary>Rule 2: two separate ground routes between every pair of spawns. Returns violation texts.</summary>
        private static List<string> CheckRoutes(MapDefinition map)
        {
            // Violations found.
            List<string> problems = new List<string>();
            // Terrain of the map with no blockers.
            TerrainGrid grid = GridOf(map);
            // First spawn of the pair. Range: 0 to spawn count - 2.
            for (int a = 0; a < map.Spawns.Count; a++)
            {
                // Second spawn of the pair. Range: a + 1 to spawn count - 1.
                for (int b = a + 1; b < map.Spawns.Count; b++)
                {
                    // Start tile of the pair. Unit: tile index.
                    int from = grid.TileOf(map.Spawns[a]);
                    // End tile of the pair. Unit: tile index.
                    int to = grid.TileOf(map.Spawns[b]);
                    // Route 1: the plain shortest route, or null when the spawns are cut off from each other.
                    List<int>? first = ShortestRoute(grid, from, to, null);
                    if (first == null)
                    {
                        problems.Add(map.Id + ": spawns " + a + " and " + b + " have no ground route at all");
                        continue;
                    }
                    // Tiles route 2 may not use. Range: one flag per tile.
                    bool[] forbidden = new bool[grid.TileCount];
                    // Route-1 tile being widened into the forbidden corridor.
                    foreach (int tile in first)
                    {
                        // Column and row of the tile. Unit: tiles.
                        int tx = grid.XOf(tile);
                        int ty = grid.YOf(tile);
                        // Row offset of the neighbour being marked. Unit: tiles. Range: -RouteSeparationTiles to +RouteSeparationTiles.
                        for (int dy = -RouteSeparationTiles; dy <= RouteSeparationTiles; dy++)
                        {
                            // Column offset of the neighbour being marked. Unit: tiles. Range: same.
                            for (int dx = -RouteSeparationTiles; dx <= RouteSeparationTiles; dx++)
                            {
                                if (grid.Contains(tx + dx, ty + dy))
                                {
                                    forbidden[grid.IndexOf(tx + dx, ty + dy)] = true;
                                }
                            }
                        }
                    }
                    // Start areas are never forbidden: both routes start and end there.
                    // Tile index being un-forbidden if it lies in a start area. Unit: tile index.
                    for (int index = 0; index < forbidden.Length; index++)
                    {
                        if (forbidden[index] && (DistanceToPoint(grid, index, map.Spawns[a]) <= BaseExemptRadiusTiles
                            || DistanceToPoint(grid, index, map.Spawns[b]) <= BaseExemptRadiusTiles))
                        {
                            forbidden[index] = false;
                        }
                    }
                    // Route 2, or null when every other way is cut.
                    List<int>? second = ShortestRoute(grid, from, to, forbidden);
                    if (second == null)
                    {
                        problems.Add(map.Id + ": spawns " + a + " and " + b + " have only one ground route (no second route clear of the first by "
                            + RouteSeparationTiles + " tiles outside the start areas)");
                    }
                    else if ((long)(second.Count - 1) * 100 > (long)(first.Count - 1) * MaxSecondRouteLengthPercent)
                    {
                        problems.Add(map.Id + ": spawns " + a + " and " + b + ": the second route is " + (second.Count - 1) + " steps against "
                            + (first.Count - 1) + " for the first, over the " + MaxSecondRouteLengthPercent + " percent limit");
                    }
                }
            }
            return problems;
        }

        /// <summary>Rule 3: every cliff spot above a base approach is within reach of ground fire from lower ground. Returns violation texts.</summary>
        private static List<string> CheckCliffSpots(MapDefinition map)
        {
            // Violations found.
            List<string> problems = new List<string>();
            // Terrain of the map with no blockers.
            TerrainGrid grid = GridOf(map);
            // Ground tiles reachable from the first spawn. Range: one flag per tile.
            bool[] reachable = ReachableFrom(grid, grid.TileOf(map.Spawns[0]));
            // Distance a ground shooter and a perched flyer can be apart centre to centre while the shooter hits. Unit: tiles. Range: greater than 0.
            double reach = GroundFireReach();
            // Whole-tile search window around a spot. Unit: tiles. Range: 1 to 16.
            int window = (int)Math.Ceiling(reach);
            // Count of spots found, for the failure message. Unit: spots. Range: 0 upward.
            int spotCount = 0;
            // Tile index being tested as a cliff spot. Unit: tile index.
            for (int index = 0; index < grid.TileCount; index++)
            {
                if (!IsCliffSpot(grid, index) || !NearAnySpawn(grid, map, index))
                {
                    continue;
                }
                spotCount++;
                // Column and row of the spot. Unit: tiles.
                int sx = grid.XOf(index);
                int sy = grid.YOf(index);
                // True once a lower reachable tile in reach is found.
                bool covered = false;
                // Row offset of the candidate shooter tile. Unit: tiles. Range: -window to +window.
                for (int dy = -window; dy <= window && !covered; dy++)
                {
                    // Column offset of the candidate shooter tile. Unit: tiles. Range: -window to +window.
                    for (int dx = -window; dx <= window && !covered; dx++)
                    {
                        if (!grid.Contains(sx + dx, sy + dy))
                        {
                            continue;
                        }
                        // Candidate shooter tile index. Unit: tile index.
                        int candidate = grid.IndexOf(sx + dx, sy + dy);
                        covered = reachable[candidate] && grid[candidate].Height < grid[index].Height
                            && Math.Sqrt((double)dx * dx + (double)dy * dy) <= reach;
                    }
                }
                if (!covered)
                {
                    problems.Add(map.Id + ": cliff spot (" + sx + ", " + sy + ") has no reachable lower ground within " + reach.ToString("0.##", CultureInfo.InvariantCulture) + " tiles");
                }
            }
            return problems;
        }

        /// <summary>Rule 4: rare fields on the flanks, outside the Core and reachable. Returns violation texts.</summary>
        private static List<string> CheckRareFields(MapDefinition map)
        {
            // Violations found.
            List<string> problems = new List<string>();
            // Terrain of the map with no blockers.
            TerrainGrid grid = GridOf(map);
            // Ground tiles reachable from the first spawn.
            bool[] reachable = ReachableFrom(grid, grid.TileOf(map.Spawns[0]));
            // Rare fields seen. Unit: fields. Range: 0 upward.
            int rareCount = 0;
            foreach (ResourceField field in map.ResourceFields)
            {
                if (field.Kind != ResourceKind.Rare)
                {
                    continue;
                }
                rareCount++;
                // Field centre in tiles. Unit: tiles.
                double fx = field.X + 0.5;
                double fy = field.Y + 0.5;
                // Pair being measured. Range: spawn indices.
                for (int a = 0; a < map.Spawns.Count; a++)
                {
                    for (int b = a + 1; b < map.Spawns.Count; b++)
                    {
                        // Distance from the field to the segment joining the two spawns. Unit: tiles.
                        double toLine = DistanceToSegment(fx, fy, ToDouble(map.Spawns[a].X), ToDouble(map.Spawns[a].Y), ToDouble(map.Spawns[b].X), ToDouble(map.Spawns[b].Y));
                        if (toLine <= FlankBandHalfWidthTiles)
                        {
                            problems.Add(map.Id + ": rare field (" + field.X + ", " + field.Y + ") is " + toLine.ToString("0.#", CultureInfo.InvariantCulture)
                                + " tiles from the line between spawns " + a + " and " + b + "; flank fields need more than " + FlankBandHalfWidthTiles.ToString("0.#", CultureInfo.InvariantCulture));
                        }
                    }
                }
                if (map.Core != null)
                {
                    // Distance from the field to the Core centre. Unit: tiles.
                    double toCore = Math.Sqrt(Math.Pow(fx - ToDouble(map.Core.Centre.X), 2) + Math.Pow(fy - ToDouble(map.Core.Centre.Y), 2));
                    if (toCore <= ToDouble(map.Core.RadiusTiles) + RareCoreMarginTiles)
                    {
                        problems.Add(map.Id + ": rare field (" + field.X + ", " + field.Y + ") is " + toCore.ToString("0.#", CultureInfo.InvariantCulture) + " tiles from the Core centre, inside the Core area plus margin");
                    }
                }
                if (!reachable[grid.IndexOf(field.X, field.Y)])
                {
                    problems.Add(map.Id + ": rare field (" + field.X + ", " + field.Y + ") cannot be reached on the ground from spawn 0");
                }
            }
            if (rareCount < MinRareFields)
            {
                problems.Add(map.Id + ": has " + rareCount + " rare fields; at least " + MinRareFields + " are required");
            }
            return problems;
        }

        /// <summary>Rule 5: a Core exists, lies inside the map and is reachable; all spawns are connected. Returns violation texts.</summary>
        private static List<string> CheckCore(MapDefinition map)
        {
            // Violations found.
            List<string> problems = new List<string>();
            // Terrain of the map with no blockers.
            TerrainGrid grid = GridOf(map);
            // Ground tiles reachable from the first spawn.
            bool[] reachable = ReachableFrom(grid, grid.TileOf(map.Spawns[0]));
            // Spawn being checked for connection. Range: 1 to spawn count - 1.
            for (int i = 1; i < map.Spawns.Count; i++)
            {
                if (!reachable[grid.TileOf(map.Spawns[i])])
                {
                    problems.Add(map.Id + ": spawn " + i + " cannot be reached on the ground from spawn 0");
                }
            }
            if (map.Core == null)
            {
                problems.Add(map.Id + ": has no Core");
                return problems;
            }
            // Column and row of the Core centre. Unit: tiles. Range: 0 to width (height).
            int cx = map.Core.Centre.X.FloorToInt();
            int cy = map.Core.Centre.Y.FloorToInt();
            if (!grid.Contains(cx, cy))
            {
                problems.Add(map.Id + ": the Core centre is outside the map");
            }
            else if (!reachable[grid.IndexOf(cx, cy)])
            {
                problems.Add(map.Id + ": the Core centre cannot be reached on the ground from spawn 0");
            }
            return problems;
        }

        // ------------------------------------------------------------------ geometry and search helpers

        /// <summary>Builds the terrain grid of a map with the real tuning. No simulation is needed to query passability.</summary>
        private static TerrainGrid GridOf(MapDefinition map)
        {
            return new TerrainGrid(map, Data.Tuning.Terrain, Data.Tuning.Flood);
        }

        /// <summary>A Fix64 value as a double, for test-side geometry only. Unit: as the input. Range: any.</summary>
        private static double ToDouble(Fix64 value)
        {
            return value.Raw / FixScale;
        }

        /// <summary>Distance from a tile's centre to a point. Unit: tiles. Range: 0 upward.</summary>
        private static double DistanceToPoint(TerrainGrid grid, int index, FixVec2 point)
        {
            // Offsets from the point to the tile centre. Unit: tiles.
            double dx = grid.XOf(index) + 0.5 - ToDouble(point.X);
            double dy = grid.YOf(index) + 0.5 - ToDouble(point.Y);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance from point (px, py) to the segment (ax, ay)-(bx, by). Unit: tiles. Range: 0 upward.</summary>
        private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            // Segment direction and its squared length. Unit: tiles and tiles squared.
            double sx = bx - ax;
            double sy = by - ay;
            double lengthSquared = sx * sx + sy * sy;
            // Position of the closest point along the segment as a fraction. Unit: fraction. Range: 0 to 1.
            double t = lengthSquared <= 0 ? 0 : Math.Max(0, Math.Min(1, ((px - ax) * sx + (py - ay) * sy) / lengthSquared));
            // Closest point on the segment. Unit: tiles.
            double qx = ax + t * sx;
            double qy = ay + t * sy;
            return Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
        }

        /// <summary>True when the tile is higher than an edge-adjacent tile and neither of the pair is a ramp (a cliff top a flyer could perch on).</summary>
        private static bool IsCliffSpot(TerrainGrid grid, int index)
        {
            // Orthogonal directions only: N, E, S, W are directions 0 to 3.
            for (int direction = 0; direction < 4; direction++)
            {
                // Neighbour tile index, or -1 off the map.
                int neighbour = grid.Neighbour(index, direction);
                if (neighbour >= 0 && grid[index].Height > grid[neighbour].Height && !grid[index].IsRamp && !grid[neighbour].IsRamp)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the tile centre is within BaseZoneRadiusTiles of any spawn.</summary>
        private static bool NearAnySpawn(TerrainGrid grid, MapDefinition map, int index)
        {
            foreach (FixVec2 spawn in map.Spawns)
            {
                if (DistanceToPoint(grid, index, spawn) <= BaseZoneRadiusTiles)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Centre-to-centre distance within which a ground anti-air shooter hits a perched Skai flyer: the shortest (weapon range + shooter
        /// radius) over mobile, non-air-layer, non-melee units that can target air, plus the largest radius of a Skai air-layer unit.
        /// Unit: tiles. Range: greater than 0.
        /// </summary>
        private static double GroundFireReach()
        {
            // Shortest shooter reach found so far; starts above any real value. Unit: tiles.
            double shortest = double.MaxValue;
            // Largest flyer radius found. Unit: tiles.
            double flyerRadius = 0;
            foreach (KeyValuePair<string, Rts.Sim.Units.UnitDefinition> pair in Data.Content.Units)
            {
                Rts.Sim.Units.UnitDefinition unit = pair.Value;
                if (unit.Layer == MovementLayer.Air)
                {
                    if (unit.FactionId == "skai")
                    {
                        flyerRadius = Math.Max(flyerRadius, ToDouble(unit.Radius));
                    }
                    continue;
                }
                if (unit.Weapon != null && unit.Weapon.CanTargetAir && !unit.Weapon.IsMelee && unit.SpeedTilesPerSecond > Fix64.Zero)
                {
                    shortest = Math.Min(shortest, ToDouble(unit.Weapon.RangeTiles) + ToDouble(unit.Radius));
                }
            }
            Assert.That(shortest, Is.LessThan(double.MaxValue), "no mobile anti-air ground unit in the data");
            return shortest + flyerRadius;
        }

        /// <summary>Ground tiles reachable from a start tile by breadth-first search over TerrainGrid.IsStepPassable.</summary>
        private static bool[] ReachableFrom(TerrainGrid grid, int start)
        {
            // Visited flag per tile.
            bool[] seen = new bool[grid.TileCount];
            // Queue of tile indices still to expand, as a list with a read position.
            List<int> queue = new List<int> { start };
            seen[start] = true;
            // Read position in the queue. Unit: queue index.
            for (int head = 0; head < queue.Count; head++)
            {
                // Tile being expanded.
                int current = queue[head];
                // Direction being tried. Unit: direction index. Range: 0 to 7.
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Neighbour tile index, or -1 off the map.
                    int next = grid.Neighbour(current, direction);
                    if (next >= 0 && !seen[next] && grid.IsStepPassable(current, next, MovementLayer.Ground))
                    {
                        seen[next] = true;
                        queue.Add(next);
                    }
                }
            }
            return seen;
        }

        /// <summary>
        /// Shortest ground route by step count from <paramref name="from"/> to <paramref name="to"/>, as tile indices including both ends,
        /// never entering a tile flagged in <paramref name="forbidden"/> (may be null). Null when no route exists.
        /// </summary>
        private static List<int>? ShortestRoute(TerrainGrid grid, int from, int to, bool[]? forbidden)
        {
            // Tile each tile was reached from, -1 when unvisited. Range: -1 or a tile index.
            int[] parent = new int[grid.TileCount];
            Array.Fill(parent, -1);
            parent[from] = from;
            // Breadth-first queue with a read position.
            List<int> queue = new List<int> { from };
            // Read position in the queue. Unit: queue index.
            for (int head = 0; head < queue.Count && parent[to] < 0; head++)
            {
                // Tile being expanded.
                int current = queue[head];
                // Direction being tried. Unit: direction index. Range: 0 to 7.
                for (int direction = 0; direction < TerrainGrid.DirectionCount; direction++)
                {
                    // Neighbour tile index, or -1 off the map.
                    int next = grid.Neighbour(current, direction);
                    if (next < 0 || parent[next] >= 0 || (forbidden != null && forbidden[next]) || !grid.IsStepPassable(current, next, MovementLayer.Ground))
                    {
                        continue;
                    }
                    parent[next] = current;
                    queue.Add(next);
                }
            }
            if (parent[to] < 0)
            {
                return null;
            }
            // Route built end to start, then reversed.
            List<int> route = new List<int>();
            // Tile being walked back toward the start.
            int step = to;
            while (step != from)
            {
                route.Add(step);
                step = parent[step];
            }
            route.Add(from);
            route.Reverse();
            return route;
        }

        // ------------------------------------------------------------------ synthetic maps and reporting

        /// <summary>
        /// Builds a small test map from a height function and an optional ramp function over (x, y), with two spawns and no fields or Core.
        /// </summary>
        private static MapDefinition Synthetic(string id, int width, int height, Func<int, int, int> heightOf, Func<int, int, bool>? rampOf, FixVec2 spawn0, FixVec2 spawn1)
        {
            // Authored heights in index order. Unit: layer. Range: 0 to 7.
            byte[] heights = new byte[width * height];
            // Authored ramp flags in index order.
            bool[] ramps = new bool[width * height];
            // Row being filled. Unit: tiles.
            for (int y = 0; y < height; y++)
            {
                // Column being filled. Unit: tiles.
                for (int x = 0; x < width; x++)
                {
                    heights[y * width + x] = (byte)heightOf(x, y);
                    ramps[y * width + x] = rampOf != null && rampOf(x, y);
                }
            }
            return new MapDefinition(id, id, width, height, new List<FixVec2> { spawn0, spawn1 }, heights, ramps, null, null);
        }

        /// <summary>A flat 64 by 64 map with the given spawns, Core and fields, for the rare-field checks.</summary>
        private static MapDefinition FieldsMap(FixVec2[] spawns, CoreArea core, params ResourceField[] fields)
        {
            return new MapDefinition("fields_test", "fields_test", 64, 64, new List<FixVec2>(spawns), null, null, new List<ResourceField>(fields), core);
        }

        /// <summary>Fails the test listing every violation, or passes when there are none.</summary>
        private static void AssertNoViolations(List<string> problems)
        {
            // All violations joined one per line for the failure message.
            StringBuilder text = new StringBuilder();
            foreach (string problem in problems)
            {
                text.AppendLine(problem);
            }
            Assert.That(problems, Is.Empty, text.ToString());
        }
    }
}
