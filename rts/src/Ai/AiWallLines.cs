// rts/src/Ai/AiWallLines.cs
// The authored wall lines of rule WL (rts/docs/specs/ai_mechanic_use.md M3.2): the parsed form of rts/data/ai/walls/<map id>.json (areas with their kind, owning start slot, resource-field indices, anchors,
// covered areas and straight lines with an optional gate tile), the validator (AiWallLines.Parse), the tile helpers and HeldAreas, which picks the areas the AI holds from its own units. Part of Rts.Ai.
// DataLoader routes the file into DataSet.AiWallLines (header and id only); Parse checks the rest against the map, the unit catalogue and tuning/economy.json wall_line_max_tiles. The seal, exit and mirror
// properties need a simulation and are checked by the tests (AiWallLineDataTests), not here. Everything is immutable after Parse. Integers and Fix64 only; lists keep file order, field and area lookups
// are sorted by index, so nothing depends on hash order.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>What an area is for: the main base is walled first and never counts against expansion_wall_max_bases.</summary>
    public enum AiWallAreaKind : byte
    {
        /// <summary>A start base.</summary>
        Main = 0,

        /// <summary>An expansion or a neutral corner; counts against expansion_wall_max_bases.</summary>
        Expansion = 1,
    }

    /// <summary>One straight line of wall tiles with an optional gate tile.</summary>
    public sealed class AiWallLine
    {
        /// <summary>The line's tiles in <see cref="WallLine.Rasterise"/> order, start first. Unit: tile coordinates (x, y). Range: 1 to wall_line_max_tiles, all on the map.</summary>
        public IReadOnlyList<(int X, int Y)> Tiles { get; }

        /// <summary>The gate tile (one of <see cref="Tiles"/>), or null when the line has none. Unit: tile coordinates (x, y).</summary>
        public (int X, int Y)? Gate { get; }

        /// <summary>Builds a line from its parsed tiles.</summary>
        /// <param name="tiles">Rasterised tiles. Range: 1 to wall_line_max_tiles entries.</param>
        /// <param name="gate">The gate tile or null; must be one of the tiles.</param>
        public AiWallLine(IReadOnlyList<(int X, int Y)> tiles, (int X, int Y)? gate)
        {
            Tiles = tiles;
            Gate = gate;
        }
    }

    /// <summary>One protected area: a main base, an expansion or a neutral corner, with the lines that seal it.</summary>
    public sealed class AiWallArea
    {
        /// <summary>Area ID, unique in its file. Range: lower-case letters, digits and underscores.</summary>
        public string Id { get; }

        /// <summary>Main or expansion.</summary>
        public AiWallAreaKind Kind { get; }

        /// <summary>Start slot that owns the area, or -1 for a neutral one. Unit: spawn index. Range: -1 to spawn count - 1.</summary>
        public int Slot { get; }

        /// <summary>Resource-field indices that identify the area (how a depot is matched to it), ascending file order. Unit: field indices. Range: 1 or more, each 0 to field count - 1.</summary>
        public IReadOnlyList<int> Fields { get; }

        /// <summary>Tiles that must lie inside the seal. Unit: tile coordinates (x, y). Range: 1 to 4, on the map.</summary>
        public IReadOnlyList<(int X, int Y)> Anchors { get; }

        /// <summary>IDs of the areas of the same file that lie inside this area's seal. Range: 0 or more IDs of the file, never this area's own.</summary>
        public IReadOnlyList<string> Covers { get; }

        /// <summary>The covered area IDs as an array, for lookups. Range: same entries as <see cref="Covers"/>.</summary>
        public string[] CoverIds { get; }

        /// <summary>The lines in file order. Range: 1 or more, at least one with a gate.</summary>
        public IReadOnlyList<AiWallLine> Lines { get; }

        /// <summary>Builds an area from validated parts.</summary>
        public AiWallArea(string id, AiWallAreaKind kind, int slot, IReadOnlyList<int> fields, IReadOnlyList<(int X, int Y)> anchors, IReadOnlyList<string> covers, IReadOnlyList<AiWallLine> lines)
        {
            Id = id;
            Kind = kind;
            Slot = slot;
            Fields = fields;
            Anchors = anchors;
            Covers = covers;
            CoverIds = new List<string>(covers).ToArray();
            Lines = lines;
        }
    }

    /// <summary>The authored areas of one map.</summary>
    public sealed class AiMapWalls
    {
        /// <summary>The map ID, equal to the file stem. Range: a loaded map ID.</summary>
        public string MapId { get; }

        /// <summary>The areas in file order. Range: 0 to 32.</summary>
        public IReadOnlyList<AiWallArea> Areas { get; }

        /// <summary>Builds the map's lines.</summary>
        public AiMapWalls(string mapId, IReadOnlyList<AiWallArea> areas)
        {
            MapId = mapId;
            Areas = areas;
        }
    }

    /// <summary>Parser, tile helpers and the held-area choice of rule WL. Static and pure.</summary>
    public static class AiWallLines
    {
        /// <summary>Most areas a file may hold. Unit: areas. Range: fixed 32 (spec M3.2.2).</summary>
        private const int MaxAreas = 32;

        /// <summary>Most anchors an area may hold. Unit: tiles. Range: fixed 4 (spec M3.2.2).</summary>
        private const int MaxAnchors = 4;

        /// <summary>Validates one wall-line file (the tree DataLoader stored in DataSet.AiWallLines) against the map, the catalogue and tuning. Throws <see cref="DataValidationException"/> naming the file and key path.</summary>
        /// <param name="root">The file's root object.</param>
        /// <param name="data">The loaded data (maps, tuning).</param>
        public static AiMapWalls Parse(JsonValue root, DataSet data)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            root.RejectUnknownKeys("id", "areas");
            // The map ID as written; must be a loaded map.
            string mapId = root.RequireNonEmptyString("id");
            if (!data.Maps.TryGetValue(mapId, out MapDefinition? map))
            {
                throw root.Require("id").Fail("names '" + mapId + "', which is not a loaded map");
            }
            // Longest allowed line. Unit: tiles. Range: 1 to 64.
            int maxLine = data.Tuning.Economy.WallLineMaxTiles;
            // Tiles of resource fields; a wall tile may not be one. Unit: tile indices. Range: 0 to tile count - 1.
            SortedSet<int> fieldTiles = new SortedSet<int>();
            foreach (ResourceField field in map.ResourceFields)
            {
                fieldTiles.Add(field.Y * map.WidthTiles + field.X);
            }
            IReadOnlyList<JsonValue> areaNodes = root.RequireArray("areas");
            if (areaNodes.Count > MaxAreas)
            {
                throw root.Require("areas").Fail("holds " + areaNodes.Count + " areas; at most " + MaxAreas);
            }
            // Parsed areas in file order.
            List<AiWallArea> areas = new List<AiWallArea>();
            // Area IDs seen so far. Range: unique.
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            // Field indices listed by some area so far. Range: unique across the file.
            SortedSet<int> usedFields = new SortedSet<int>();
            // The covers entries with their nodes, checked once every ID is known.
            List<KeyValuePair<string, JsonValue>> coverRefs = new List<KeyValuePair<string, JsonValue>>();
            // Owner area ID of each entry of coverRefs.
            List<string> coverOwners = new List<string>();
            foreach (JsonValue node in areaNodes)
            {
                node.RejectUnknownKeys("id", "kind", "slot", "fields", "anchors", "covers", "lines");
                // Area ID: lower-case letters, digits and underscores.
                string id = node.RequireNonEmptyString("id");
                foreach (char c in id)
                {
                    if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                    {
                        throw node.Require("id").Fail("must hold only lower-case letters, digits and underscores (found '" + id + "')");
                    }
                }
                if (!ids.Add(id))
                {
                    throw node.Require("id").Fail("duplicates the area id '" + id + "'");
                }
                // The kind text.
                string kindText = node.RequireString("kind");
                AiWallAreaKind kind;
                if (kindText == "main")
                {
                    kind = AiWallAreaKind.Main;
                }
                else if (kindText == "expansion")
                {
                    kind = AiWallAreaKind.Expansion;
                }
                else
                {
                    throw node.Require("kind").Fail("must be 'main' or 'expansion' (found '" + kindText + "')");
                }
                int slot = node.RequireInt("slot", -1, map.Spawns.Count - 1);
                // Field indices of the area.
                List<int> fields = new List<int>();
                IReadOnlyList<JsonValue> fieldNodes = node.RequireArray("fields");
                if (fieldNodes.Count == 0)
                {
                    throw node.Require("fields").Fail("must list at least one field index");
                }
                foreach (JsonValue fieldNode in fieldNodes)
                {
                    int index = fieldNode.AsInt(0, map.ResourceFields.Count - 1);
                    if (!usedFields.Add(index))
                    {
                        throw fieldNode.Fail("lists field " + index + ", which another entry of this file already lists");
                    }
                    fields.Add(index);
                }
                // Anchor tiles of the area.
                List<(int X, int Y)> anchors = new List<(int X, int Y)>();
                IReadOnlyList<JsonValue> anchorNodes = node.RequireArray("anchors");
                if (anchorNodes.Count < 1 || anchorNodes.Count > MaxAnchors)
                {
                    throw node.Require("anchors").Fail("must list 1 to " + MaxAnchors + " tiles (found " + anchorNodes.Count + ")");
                }
                foreach (JsonValue anchorNode in anchorNodes)
                {
                    anchors.Add(ReadTile(anchorNode, map));
                }
                // IDs of the covered areas, checked below.
                List<string> covers = new List<string>();
                foreach (JsonValue coverNode in node.RequireArray("covers"))
                {
                    covers.Add(coverNode.AsString());
                    coverRefs.Add(new KeyValuePair<string, JsonValue>(coverNode.AsString(), coverNode));
                    coverOwners.Add(id);
                }
                // Parsed lines of the area.
                List<AiWallLine> lines = new List<AiWallLine>();
                IReadOnlyList<JsonValue> lineNodes = node.RequireArray("lines");
                if (lineNodes.Count == 0)
                {
                    throw node.Require("lines").Fail("must hold at least one line");
                }
                // True once some line of the area has a gate.
                bool hasGate = false;
                foreach (JsonValue lineNode in lineNodes)
                {
                    lineNode.RejectUnknownKeys("from", "to", "gate");
                    (int X, int Y) from = ReadTile(lineNode.Require("from"), map);
                    (int X, int Y) to = ReadTile(lineNode.Require("to"), map);
                    // Tiles the line would have, before any limit. Unit: tiles. Range: 1 upward.
                    int length = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) + 1;
                    if (length > maxLine)
                    {
                        throw lineNode.Fail("is " + length + " tiles long; at most wall_line_max_tiles (" + maxLine + ")");
                    }
                    // The rasterised tiles.
                    List<(int X, int Y)> tiles = new List<(int X, int Y)>();
                    WallLine.Rasterise(from.X, from.Y, to.X, to.Y, maxLine, tiles);
                    foreach ((int X, int Y) tile in tiles)
                    {
                        if (fieldTiles.Contains(tile.Y * map.WidthTiles + tile.X))
                        {
                            throw lineNode.Fail("has the tile (" + tile.X + ", " + tile.Y + ") on a resource field");
                        }
                    }
                    // The gate tile, or null.
                    (int X, int Y)? gate = null;
                    if (lineNode.TryGetMember("gate", out JsonValue? gateNode))
                    {
                        (int X, int Y) gateTile = ReadTile(gateNode, map);
                        if (!tiles.Contains(gateTile))
                        {
                            throw gateNode.Fail("is (" + gateTile.X + ", " + gateTile.Y + "), which is not a tile of its line");
                        }
                        gate = gateTile;
                        hasGate = true;
                    }
                    lines.Add(new AiWallLine(tiles.AsReadOnly(), gate));
                }
                if (!hasGate)
                {
                    throw node.Require("lines").Fail("has no gate tile; every area needs at least one");
                }
                areas.Add(new AiWallArea(id, kind, slot, fields.AsReadOnly(), anchors.AsReadOnly(), covers.AsReadOnly(), lines.AsReadOnly()));
            }
            // Index into coverRefs. Range: 0 to count - 1.
            for (int i = 0; i < coverRefs.Count; i++)
            {
                if (!ids.Contains(coverRefs[i].Key))
                {
                    throw coverRefs[i].Value.Fail("names the area '" + coverRefs[i].Key + "', which is not in this file");
                }
                if (coverRefs[i].Key == coverOwners[i])
                {
                    throw coverRefs[i].Value.Fail("names its own area '" + coverRefs[i].Key + "'");
                }
            }
            return new AiMapWalls(mapId, areas.AsReadOnly());
        }

        /// <summary>Reads an object with x and y and checks the tile lies on the map.</summary>
        /// <param name="node">The tile object.</param>
        /// <param name="map">The map the tile must lie on.</param>
        private static (int X, int Y) ReadTile(JsonValue node, MapDefinition map)
        {
            node.RejectUnknownKeys("x", "y");
            // Tile coordinates as written. Unit: tiles. Range: -1000000 to 1000000 before the map check.
            int x = node.RequireInt("x", -1000000, 1000000);
            int y = node.RequireInt("y", -1000000, 1000000);
            if (x < 0 || y < 0 || x >= map.WidthTiles || y >= map.HeightTiles)
            {
                throw node.Fail("is the tile (" + x + ", " + y + "), which is off the map (" + map.WidthTiles + " by " + map.HeightTiles + ")");
            }
            return (x, y);
        }

        /// <summary>The tiles of a line (rasterised at parse time).</summary>
        /// <param name="line">A parsed line.</param>
        public static IReadOnlyList<(int X, int Y)> Tiles(AiWallLine line)
        {
            return line.Tiles;
        }

        /// <summary>All tiles of an area: lines in file order, tiles in line order, duplicates dropped, first kept. Unit: tile coordinates (x, y).</summary>
        /// <param name="area">A parsed area.</param>
        public static List<(int X, int Y)> AllTiles(AiWallArea area)
        {
            // Tiles in order, and the ones already listed.
            List<(int X, int Y)> all = new List<(int X, int Y)>();
            HashSet<(int X, int Y)> seen = new HashSet<(int X, int Y)>();
            foreach (AiWallLine line in area.Lines)
            {
                foreach ((int X, int Y) tile in line.Tiles)
                {
                    if (seen.Add(tile))
                    {
                        all.Add(tile);
                    }
                }
            }
            return all;
        }

        /// <summary>
        /// The areas the AI holds and wants walled, in wall order (spec M3.2.4 "Held areas", steps 1 to 7). Holders are own units and depot orders; a holder's area is the one listing the nearest field within reach. Pure.
        /// </summary>
        /// <param name="view">What the AI may know this think (own units, fields).</param>
        /// <param name="walls">The authored areas of the map.</param>
        /// <param name="ownSpawn">Own start slot (AiMemory.OwnSpawnIndex). Range: 0 to spawn count - 1.</param>
        /// <param name="reachSquared">field_max_distance_tiles squared. Unit: squared tiles. Range: 4 to 4096.</param>
        /// <param name="expansionWallMaxBases">expansion_wall_max_bases: how many held expansion areas are kept (step 5). Unit: areas. Range: 0 to 4.</param>
        public static List<AiWallArea> HeldAreas(AiWorldView view, AiMapWalls walls, int ownSpawn, Fix64 reachSquared, int expansionWallMaxBases)
        {
            // Step 1: field index to the usable area that lists it (no field is listed twice).
            SortedDictionary<int, AiWallArea> fieldArea = new SortedDictionary<int, AiWallArea>();
            foreach (AiWallArea area in walls.Areas)
            {
                if (area.Slot == ownSpawn || area.Slot == -1)
                {
                    foreach (int field in area.Fields)
                    {
                        fieldArea[field] = area;
                    }
                }
            }
            // Field centres by index. Unit: tiles.
            SortedDictionary<int, FixVec2> centres = new SortedDictionary<int, FixVec2>();
            foreach (AiFieldView field in view.Fields)
            {
                centres[field.Index] = field.Centre;
            }
            // Step 2 to 4: the held list, areas in the order of their first holder.
            List<AiWallArea> held = new List<AiWallArea>();
            // Holders (a): own HQs that are Rooted and own drop-off structures, ascending ID; tile of the position.
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if ((unit.Definition.Hq != null && unit.HqState == HqState.Rooted) || (unit.IsStructure && unit.Definition.Economy.IsDropOff))
                {
                    AddHolder(held, fieldArea, centres, view, unit.TileX, unit.TileY, reachSquared);
                }
            }
            // Holders (b): own workers with an active order to build a drop-off, ascending ID; tile of the order.
            foreach (AiUnitView unit in view.OwnUnits)
            {
                if (unit.IsWorker && unit.HasBuildOrder && view.Content.Units.TryGetValue(unit.BuildDefinitionId, out UnitDefinition? ordered) && ordered.Economy.IsDropOff)
                {
                    AddHolder(held, fieldArea, centres, view, unit.BuildTileX, unit.BuildTileY, reachSquared);
                }
            }
            // Step 5: every main area and the first expansion_wall_max_bases expansion areas of the held list.
            List<AiWallArea> kept = new List<AiWallArea>();
            // Expansion areas kept so far. Unit: areas. Range: 0 to expansion_wall_max_bases.
            int expansions = 0;
            foreach (AiWallArea area in held)
            {
                if (area.Kind == AiWallAreaKind.Main)
                {
                    kept.Add(area);
                }
                else if (expansions < expansionWallMaxBases)
                {
                    expansions++;
                    kept.Add(area);
                }
            }
            // Step 6: drop every kept area some other kept area covers. Step 7: kept main areas first, then expansion areas, each in held-list order.
            List<AiWallArea> wallOrder = new List<AiWallArea>();
            foreach (AiWallAreaKind kind in new[] { AiWallAreaKind.Main, AiWallAreaKind.Expansion })
            {
                foreach (AiWallArea area in kept)
                {
                    if (area.Kind != kind)
                    {
                        continue;
                    }
                    // True when another kept area lists this one in its covers.
                    bool covered = false;
                    foreach (AiWallArea other in kept)
                    {
                        covered |= other != area && Array.IndexOf(other.CoverIds, area.Id) >= 0;
                    }
                    if (!covered)
                    {
                        wallOrder.Add(area);
                    }
                }
            }
            return wallOrder;
        }

        /// <summary>Step 3: the area of one holder tile (nearest listed field within reach, ties to the lower field index); appended to the held list unless already there or none.</summary>
        /// <param name="held">The held list being built.</param>
        /// <param name="fieldArea">Usable areas by field index.</param>
        /// <param name="centres">Field centres by index. Unit: tiles.</param>
        /// <param name="view">The view (tile centres).</param>
        /// <param name="tileX">Holder tile column. Unit: tiles. Range: 0 to map width - 1 (or -1 for no tile, which holds nothing).</param>
        /// <param name="tileY">Holder tile row. Unit: tiles. Range: 0 to map height - 1 (or -1).</param>
        /// <param name="reachSquared">Largest squared distance from the tile centre to the field centre. Unit: squared tiles.</param>
        private static void AddHolder(List<AiWallArea> held, SortedDictionary<int, AiWallArea> fieldArea, SortedDictionary<int, FixVec2> centres, AiWorldView view, int tileX, int tileY, Fix64 reachSquared)
        {
            if (!view.Contains(tileX, tileY))
            {
                return;
            }
            // Centre of the holder's tile. Unit: tiles.
            FixVec2 at = view.CentreOf(tileX, tileY);
            // Nearest listed field so far, its squared distance. Unit: squared tiles. Range: 0 to reachSquared.
            AiWallArea? best = null;
            Fix64 bestSquared = Fix64.Zero;
            foreach (KeyValuePair<int, AiWallArea> pair in fieldArea)
            {
                if (!centres.TryGetValue(pair.Key, out FixVec2 centre))
                {
                    continue;
                }
                // Squared distance from the holder to this field. Unit: squared tiles. Range: 0 upward.
                Fix64 squared = (centre - at).LengthSquared();
                if (squared <= reachSquared && (best == null || squared < bestSquared))
                {
                    best = pair.Value;
                    bestSquared = squared;
                }
            }
            if (best != null && !held.Contains(best))
            {
                held.Add(best);
            }
        }
    }
}
