// rts/src/Sim/Data/DataLoader.cs
// Turns the text of the files under rts/data into a validated DataSet (ADR-002). Part of Rts.Sim.Data.
// The loader does no file IO: hosts pass DataSourceFile pairs. Files are processed in ordinal path order so the
// first error reported is the same on every machine. Every error is a DataValidationException that names the file
// (the first words of the message) and the field. Folder routing: tuning/, units/<faction>/, factions/, maps/, cycles/.
// Exception: the scripts/ folder is skipped. Scripted command streams (the headless harness, the Unity bootstrap and
// later campaign missions) live there and are parsed by CommandScript, not by this loader. Everything else is strict:
// unknown folders, unknown keys, missing keys, wrong types, out-of-range values, dangling references and a
// $schema_version other than 1 are all errors; nothing else is silently ignored.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Sim.Data
{
    /// <summary>Static entry point of the data loader.</summary>
    public static class DataLoader
    {
        /// <summary>Key every data file carries to say which layout version it uses.</summary>
        public const string SchemaVersionKey = "$schema_version";

        /// <summary>Key every data file carries with a sentence saying what the file is for.</summary>
        public const string DocKey = "$doc";

        /// <summary>The only schema version this build reads. Range: exactly 1. Later phases add migrations (ADR-002).</summary>
        public const int SupportedSchemaVersion = 1;

        /// <summary>
        /// Name of the data folder holding scripted command streams. Files under it are ignored by <see cref="Load"/>;
        /// <see cref="CommandScript"/> parses them. Range: fixed folder name, "scripts".
        /// </summary>
        public const string ScriptsFolder = "scripts";

        /// <summary>Largest allowed value of a health, cost or similar unit field. Unit: points or resource units. Range: fixed validation bound, not a tuning value.</summary>
        private const string MaxStatText = "100000";

        /// <summary>
        /// Parses and validates every file and builds the data set. Throws <see cref="DataValidationException"/> on the first problem.
        /// </summary>
        /// <param name="files">Every data file, in any order. Paths are data-root-relative with forward slashes.</param>
        public static DataSet Load(IEnumerable<DataSourceFile> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            // Files by path in ordinal order; also rejects duplicate paths.
            SortedDictionary<string, DataSourceFile> byPath = new SortedDictionary<string, DataSourceFile>(StringComparer.Ordinal);
            foreach (DataSourceFile file in files)
            {
                if (file == null)
                {
                    throw new ArgumentException("The file list contains a null entry.", nameof(files));
                }
                if (byPath.ContainsKey(file.Path))
                {
                    throw new DataValidationException(file.Path, string.Empty, "appears twice in the file list");
                }
                byPath.Add(file.Path, file);
            }

            // Parsed tuning roots by path.
            SortedDictionary<string, JsonValue> tuningRoots = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);
            // Unit, faction, map and cycle results by ID, plus the file each came from for cross-reference errors.
            ContentCatalog content = new ContentCatalog();
            // Map definitions by ID, in ordinal ID order.
            SortedDictionary<string, MapDefinition> maps = new SortedDictionary<string, MapDefinition>(StringComparer.Ordinal);
            // Cycle definitions by ID, in ordinal ID order.
            SortedDictionary<string, CycleDefinition> cycles = new SortedDictionary<string, CycleDefinition>(StringComparer.Ordinal);
            // Unit ID mapped to the data file that defined it, for cross-reference errors.
            SortedDictionary<string, string> unitFiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
            // Faction file roots by faction ID, kept for the unit-list reference check.
            SortedDictionary<string, JsonValue> factionRoots = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal);

            // Combat tuning, parsed ahead of the main loop because unit files are validated against its armour and damage
            // types. Null when tuning/combat.json was not supplied (reported below as a missing tuning file).
            CombatTuning? combatTuning = null;
            if (byPath.TryGetValue(CombatTuning.FilePath, out DataSourceFile? combatFile))
            {
                // Parsed combat file; syntax and header errors name the file.
                JsonValue combatRoot = JsonReader.Parse(combatFile.Text, combatFile.Path);
                CheckHeader(combatRoot);
                combatTuning = new CombatTuning(combatRoot);
            }

            foreach (KeyValuePair<string, DataSourceFile> pair in byPath)
            {
                // Path of the file being processed.
                string path = pair.Key;
                // Path split into folders and file name.
                string[] parts = path.Split('/');
                if (!path.EndsWith(".json", StringComparison.Ordinal))
                {
                    throw new DataValidationException(path, string.Empty, "is not a .json data file");
                }
                if (parts[0] == ScriptsFolder)
                {
                    // Scripted command streams live here (harness default script, Unity bootstrap, later campaign missions).
                    // CommandScript parses them with its own schema; the loader neither reads nor validates them.
                    continue;
                }
                // Parsed document; syntax errors name the file, line and column.
                JsonValue root = JsonReader.Parse(pair.Value.Text, path);
                CheckHeader(root);
                switch (parts[0])
                {
                    case "tuning":
                        tuningRoots.Add(path, root);
                        break;
                    case "units":
                        {
                            // Unit definition read from this file. Range: validated by ReadUnit.
                            UnitDefinition unit = ReadUnit(root, path, parts,
                                combatTuning ?? throw new DataValidationException(CombatTuning.FilePath, string.Empty, "is missing; every tuning file is required"));
                            AddUnit(content, unit, path);
                            unitFiles.Add(unit.Id, path);
                            break;
                        }
                    case "factions":
                        {
                            // Faction definition read from this file. Range: validated by ReadFaction.
                            FactionDefinition faction = ReadFaction(root, path, parts);
                            content.AddFaction(faction);
                            factionRoots.Add(faction.Id, root);
                            break;
                        }
                    case "maps":
                        {
                            // Map definition read from this file. Range: validated by ReadMap.
                            MapDefinition map = ReadMap(root, path, parts);
                            maps.Add(map.Id, map);
                            break;
                        }
                    case "cycles":
                        {
                            // Cycle definition read from this file. Range: validated by ReadCycle.
                            CycleDefinition cycle = ReadCycle(root, path, parts);
                            cycles.Add(cycle.Id, cycle);
                            break;
                        }
                    default:
                        throw new DataValidationException(path, string.Empty,
                            "is not under a known data folder (tuning/, units/, factions/, maps/, cycles/ or scripts/)");
                }
            }

            // Typed tuning built from every tuning file together. Range: every tuning file present.
            Tuning tuning = BuildTuning(tuningRoots);
            CheckReferences(content, unitFiles, factionRoots);
            content.SetCombatRules(tuning.Combat.CreateRules());
            return new DataSet(content, tuning, maps, cycles);
        }

        // ---------------------------------------------------------------- shared checks

        /// <summary>Checks the root is an object with a supported $schema_version and a non-empty $doc.</summary>
        private static void CheckHeader(JsonValue root)
        {
            if (root.Kind != JsonKind.Object)
            {
                throw root.Fail("the top level must be a JSON object (found " + root.Kind.ToString().ToLowerInvariant() + ")");
            }
            // Layout version declared by the file.
            int version = root.Require(SchemaVersionKey).AsInt();
            if (version != SupportedSchemaVersion)
            {
                throw root.Require(SchemaVersionKey).Fail("must be " + SupportedSchemaVersion
                    + " (this build only reads schema version " + SupportedSchemaVersion + "; found " + version + ")");
            }
            root.RequireNonEmptyString(DocKey);
        }

        /// <summary>
        /// Checks that <c>id</c> equals the value implied by the file path, and that it uses only lower-case letters,
        /// digits and underscores (plus dots for unit IDs).
        /// </summary>
        /// <param name="root">The file's root object.</param>
        /// <param name="expected">The ID the file path implies.</param>
        /// <param name="allowDot">True for unit IDs, which have the form faction.name.</param>
        private static string ReadId(JsonValue root, string expected, bool allowDot)
        {
            // The id as written in the file.
            string id = root.RequireNonEmptyString("id");
            // Index into the ID text. Unit: characters. Range: 0 to id length - 1.
            for (int i = 0; i < id.Length; i++)
            {
                // Character being checked.
                char c = id[i];
                // True when the character is allowed in an ID.
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || (allowDot && c == '.');
                if (!ok)
                {
                    throw root.Require("id").Fail("may only contain lower-case letters, digits and underscores"
                        + (allowDot ? " and dots" : string.Empty) + " (found '" + id + "')");
                }
            }
            if (!string.Equals(id, expected, StringComparison.Ordinal))
            {
                throw root.Require("id").Fail("must be '" + expected + "' to match the file path (found '" + id + "')");
            }
            return id;
        }

        /// <summary>Returns the file name without folders and without ".json".</summary>
        private static string StemOf(string[] parts)
        {
            // Last path segment, such as "windrunner.json".
            string name = parts[parts.Length - 1];
            return name.Substring(0, name.Length - ".json".Length);
        }

        /// <summary>Fails unless the path has exactly <paramref name="depth"/> segments, with a message telling the designer where the file belongs.</summary>
        private static void CheckDepth(string path, string[] parts, int depth, string shape)
        {
            if (parts.Length != depth || StemOf(parts).Length == 0)
            {
                throw new DataValidationException(path, string.Empty, "is in the wrong place; expected " + shape);
            }
        }

        // ---------------------------------------------------------------- tuning

        /// <summary>Builds the typed tuning classes, requiring every known file and rejecting unknown ones.</summary>
        private static Tuning BuildTuning(SortedDictionary<string, JsonValue> roots)
        {
            foreach (string path in roots.Keys)
            {
                if (path != MatchTuning.FilePath && path != HqTuning.FilePath && path != MomentumTuning.FilePath
                    && path != StatusTuning.FilePath && path != FloodTuning.FilePath && path != SupplyTuning.FilePath
                    && path != EarthTuning.FilePath && path != OnlineTuning.FilePath && path != HarnessTuning.FilePath
                    && path != TerrainTuning.FilePath && path != CombatTuning.FilePath)
                {
                    throw new DataValidationException(path, string.Empty, "is not a known tuning file");
                }
            }
            return new Tuning(
                new MatchTuning(RequireTuningRoot(roots, MatchTuning.FilePath)),
                new HqTuning(RequireTuningRoot(roots, HqTuning.FilePath)),
                new MomentumTuning(RequireTuningRoot(roots, MomentumTuning.FilePath)),
                new StatusTuning(RequireTuningRoot(roots, StatusTuning.FilePath)),
                new FloodTuning(RequireTuningRoot(roots, FloodTuning.FilePath)),
                new SupplyTuning(RequireTuningRoot(roots, SupplyTuning.FilePath)),
                new EarthTuning(RequireTuningRoot(roots, EarthTuning.FilePath)),
                new OnlineTuning(RequireTuningRoot(roots, OnlineTuning.FilePath)),
                new HarnessTuning(RequireTuningRoot(roots, HarnessTuning.FilePath)),
                new TerrainTuning(RequireTuningRoot(roots, TerrainTuning.FilePath)),
                new CombatTuning(RequireTuningRoot(roots, CombatTuning.FilePath)));
        }

        /// <summary>Looks up a tuning file's parsed root, failing with a file-level error when the file was not supplied.</summary>
        private static JsonValue RequireTuningRoot(SortedDictionary<string, JsonValue> roots, string path)
        {
            if (!roots.TryGetValue(path, out JsonValue? root))
            {
                throw new DataValidationException(path, string.Empty, "is missing; every tuning file is required");
            }
            return root;
        }

        // ---------------------------------------------------------------- units

        /// <summary>Reads units/&lt;faction&gt;/&lt;name&gt;.json.</summary>
        private static UnitDefinition ReadUnit(JsonValue root, string path, string[] parts, CombatTuning combat)
        {
            CheckDepth(path, parts, 3, "units/<faction>/<name>.json");
            root.RejectUnknownKeys("id", "display_name", "faction_id", "kind", "layer", "max_health",
                "speed_tiles_per_second", "supply", "radius_tiles", "cost_common", "cost_rare", "armour_type", "sight_range_tiles", "weapon");
            // ID implied by the path: the folder is the faction, the file name is the unit.
            string id = ReadId(root, parts[1] + "." + StemOf(parts), true);
            // Display name text. Range: non-empty string.
            string displayName = root.RequireNonEmptyString("display_name");
            // Owning faction ID; must equal the folder name checked below.
            string factionId = root.RequireNonEmptyString("faction_id");
            if (!string.Equals(factionId, parts[1], StringComparison.Ordinal))
            {
                throw root.Require("faction_id").Fail("must be '" + parts[1] + "' to match the folder the file is in (found '" + factionId + "')");
            }
            // Optional kind: Phase 0 validates it but UnitDefinition does not store it yet; buildings arrive in Phase 1.
            string kind = root.OptionalString("kind", "unit");
            if (kind != "unit" && kind != "building")
            {
                throw root.Require("kind").Fail("must be \"unit\" or \"building\" (found \"" + kind + "\")");
            }
            // Movement layer text and its enum value.
            string layerText = root.RequireString("layer");
            // Movement layer enum set by the switch below. Range: Ground, Water or Air.
            MovementLayer layer;
            switch (layerText)
            {
                case "ground": layer = MovementLayer.Ground; break;
                case "water": layer = MovementLayer.Water; break;
                case "air": layer = MovementLayer.Air; break;
                default:
                    throw root.Require("layer").Fail("must be \"ground\", \"water\" or \"air\" (found \"" + layerText + "\")");
            }
            // Smallest positive health and radius the loader accepts; the real ranges are in the unit schema comments.
            Fix64 maxHealth = root.RequireFix64("max_health", "1", MaxStatText);
            // Top speed. Unit: tiles per second (fixed-point). Range: 0 to 100.
            Fix64 speed = root.RequireFix64("speed_tiles_per_second", "0", "100");
            // Supply the unit uses. Unit: supply points. Range: 0 to 100.
            int supply = root.RequireInt("supply", 0, 100);
            // Collision radius. Unit: tiles (fixed-point). Range: 0.05 to 10.
            Fix64 radius = root.RequireFix64("radius_tiles", "0.05", "10");
            // Common-resource cost. Unit: resource points. Range: 0 to 100000.
            int costCommon = root.RequireInt("cost_common", 0, 100000);
            // Rare-resource cost. Unit: resource points. Range: 0 to 100000.
            int costRare = root.RequireInt("cost_rare", 0, 100000);
            // Armour type name (default from combat tuning), sight range and optional weapon; see UnitSchema.
            string armourType = UnitSchema.ReadArmourType(root, combat);
            Fix64 sightRange = UnitSchema.ReadSightRange(root);
            WeaponDefinition? weapon = UnitSchema.ReadWeapon(root, combat);
            return new UnitDefinition(id, displayName, factionId, layer, maxHealth, speed, supply, radius, costCommon, costRare, armourType, sightRange, weapon);
        }

        /// <summary>Adds a unit to the catalog, turning a duplicate ID into a file-named error.</summary>
        private static void AddUnit(ContentCatalog content, UnitDefinition unit, string path)
        {
            if (content.Units.ContainsKey(unit.Id))
            {
                throw new DataValidationException(path, "id", "duplicates the unit id '" + unit.Id + "' defined elsewhere");
            }
            content.AddUnit(unit);
        }

        // ---------------------------------------------------------------- factions

        /// <summary>Reads factions/&lt;id&gt;.json.</summary>
        private static FactionDefinition ReadFaction(JsonValue root, string path, string[] parts)
        {
            CheckDepth(path, parts, 2, "factions/<id>.json");
            root.RejectUnknownKeys("id", "display_name", "mechanic_id", "units");
            // Faction ID taken from the file name. Range: lower-case letters, digits and underscores.
            string id = ReadId(root, StemOf(parts), false);
            // Faction display name. Range: non-empty string.
            string displayName = root.RequireNonEmptyString("display_name");
            // Mechanic ID as written in the file. Range: any non-empty string; checked at match start.
            string mechanicId = root.RequireNonEmptyString("mechanic_id");
            // Unit IDs in file order; duplicates are an error.
            List<string> unitIds = new List<string>();
            // Unit IDs already listed by this faction, to reject duplicates.
            SortedSet<string> seen = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JsonValue item in root.RequireArray("units"))
            {
                // One listed unit ID.
                string unitId = item.AsString();
                if (!seen.Add(unitId))
                {
                    throw item.Fail("lists the unit '" + unitId + "' twice");
                }
                unitIds.Add(unitId);
            }
            return new FactionDefinition(id, displayName, mechanicId, unitIds);
        }

        /// <summary>Checks unit-to-faction and faction-to-unit references, naming the file and field of the dangling one.</summary>
        private static void CheckReferences(ContentCatalog content, SortedDictionary<string, string> unitFiles, SortedDictionary<string, JsonValue> factionRoots)
        {
            foreach (KeyValuePair<string, UnitDefinition> pair in content.Units)
            {
                if (!content.Factions.ContainsKey(pair.Value.FactionId))
                {
                    throw new DataValidationException(unitFiles[pair.Key], "faction_id",
                        "names the faction '" + pair.Value.FactionId + "', which has no file in factions/");
                }
            }
            foreach (KeyValuePair<string, FactionDefinition> pair in content.Factions)
            {
                // The faction file's items, to point the error at the exact array element.
                IReadOnlyList<JsonValue> items = factionRoots[pair.Key].RequireArray("units");
                // Index into the faction's unit list. Unit: list position. Range: 0 to Count - 1.
                for (int i = 0; i < items.Count; i++)
                {
                    // Unit ID listed at this position.
                    string unitId = pair.Value.UnitIds[i];
                    if (!content.Units.TryGetValue(unitId, out UnitDefinition? unit))
                    {
                        throw items[i].Fail("names the unit '" + unitId + "', which has no file in units/");
                    }
                    if (!string.Equals(unit.FactionId, pair.Key, StringComparison.Ordinal))
                    {
                        throw items[i].Fail("names the unit '" + unitId + "', which belongs to faction '" + unit.FactionId + "'");
                    }
                }
            }
        }

        // ---------------------------------------------------------------- maps

        /// <summary>Reads maps/&lt;id&gt;.json.</summary>
        private static MapDefinition ReadMap(JsonValue root, string path, string[] parts)
        {
            CheckDepth(path, parts, 2, "maps/<id>.json");
            root.RejectUnknownKeys("id", "display_name", "width_tiles", "height_tiles", "spawns", "height_rows", "ramp_rows", "resource_fields", "core");
            // Map ID taken from the file name. Range: lower-case letters, digits and underscores.
            string id = ReadId(root, StemOf(parts), false);
            // Map display name. Range: non-empty string.
            string displayName = root.RequireNonEmptyString("display_name");
            // Map width. Unit: tiles (int). Range: 8 to 1024.
            int width = root.RequireInt("width_tiles", 8, 1024);
            // Map height. Unit: tiles (int). Range: 8 to 1024.
            int height = root.RequireInt("height_tiles", 8, 1024);
            // Spawn point objects in file order. Range: MinSpawns to MaxSpawns entries (2 to 16).
            IReadOnlyList<JsonValue> spawnNodes = root.RequireArray("spawns");
            if (spawnNodes.Count < MapDefinition.MinSpawns || spawnNodes.Count > MapDefinition.MaxSpawns)
            {
                throw root.Require("spawns").Fail("must list " + MapDefinition.MinSpawns + " to " + MapDefinition.MaxSpawns + " spawn points (found " + spawnNodes.Count + ")");
            }
            // Spawn points in slot order.
            List<FixVec2> spawns = new List<FixVec2>();
            foreach (JsonValue node in spawnNodes)
            {
                node.RejectUnknownKeys("x", "y");
                // Spawn X position. Unit: tiles (fixed-point). Range: 0 to the map width.
                Fix64 x = node.RequireFix64("x", "0", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                // Spawn Y position. Unit: tiles (fixed-point). Range: 0 to the map height.
                Fix64 y = node.RequireFix64("y", "0", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                // Spawn position as a point in tiles. Range: inside the map bounds.
                FixVec2 point = new FixVec2(x, y);
                if (spawns.Contains(point))
                {
                    throw node.Fail("repeats an earlier spawn point (" + x + ", " + y + ")");
                }
                spawns.Add(point);
            }
            // Elevation layer per tile from height_rows, or all zero when the key is absent (flat map).
            byte[]? heights = ReadHeightRows(root, width, height);
            // Ramp flag per tile from ramp_rows, or none when absent.
            bool[]? ramps = ReadRampRows(root, width, height);
            // Resource fields in file order; empty when absent.
            List<ResourceField> fields = ReadResourceFields(root, width, height);
            // The Core, or null when absent.
            CoreArea? core = ReadCore(root, width, height);
            // Spawns and fields must sit on unblocked tiles: blockers are not authored in Phase 1, so only bounds apply.
            foreach (FixVec2 spawn in spawns)
            {
                if (spawn.X >= Fix64.FromInt(width) || spawn.Y >= Fix64.FromInt(height))
                {
                    throw root.Require("spawns").Fail("has a spawn on the far edge (" + spawn + "); spawns must be strictly inside the map");
                }
            }
            return new MapDefinition(id, displayName, width, height, spawns, heights, ramps, fields, core);
        }

        /// <summary>Reads height_rows: one string per row (row 0 is y = 0), one digit 0 to 7 per tile. Returns null when absent.</summary>
        private static byte[]? ReadHeightRows(JsonValue root, int width, int height)
        {
            if (!root.TryGetMember("height_rows", out JsonValue? rowsNode))
            {
                return null;
            }
            // Row strings in file order.
            IReadOnlyList<JsonValue> rows = rowsNode.Items;
            if (rows.Count != height)
            {
                throw rowsNode.Fail("must have exactly height_tiles (" + height + ") rows (found " + rows.Count + ")");
            }
            byte[] heights = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                // Digits of this row.
                string row = rows[y].AsString();
                if (row.Length != width)
                {
                    throw rows[y].Fail("must have exactly width_tiles (" + width + ") characters (found " + row.Length + ")");
                }
                for (int x = 0; x < width; x++)
                {
                    // The character for tile (x, y); must be a digit 0 to 7.
                    char c = row[x];
                    if (c < '0' || c > (char)('0' + Terrain.TerrainTile.MaxHeight))
                    {
                        throw rows[y].Fail("has '" + c + "' at column " + x + "; height digits must be 0 to " + Terrain.TerrainTile.MaxHeight);
                    }
                    heights[y * width + x] = (byte)(c - '0');
                }
            }
            return heights;
        }

        /// <summary>Reads ramp_rows: one string per row, '.' for no ramp and 'R' for a ramp tile. Returns null when absent.</summary>
        private static bool[]? ReadRampRows(JsonValue root, int width, int height)
        {
            if (!root.TryGetMember("ramp_rows", out JsonValue? rowsNode))
            {
                return null;
            }
            // Row strings in file order.
            IReadOnlyList<JsonValue> rows = rowsNode.Items;
            if (rows.Count != height)
            {
                throw rowsNode.Fail("must have exactly height_tiles (" + height + ") rows (found " + rows.Count + ")");
            }
            bool[] ramps = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                // Characters of this row.
                string row = rows[y].AsString();
                if (row.Length != width)
                {
                    throw rows[y].Fail("must have exactly width_tiles (" + width + ") characters (found " + row.Length + ")");
                }
                for (int x = 0; x < width; x++)
                {
                    // The character for tile (x, y); '.' or 'R'.
                    char c = row[x];
                    if (c != '.' && c != 'R')
                    {
                        throw rows[y].Fail("has '" + c + "' at column " + x + "; ramp rows use '.' and 'R' only");
                    }
                    ramps[y * width + x] = c == 'R';
                }
            }
            return ramps;
        }

        /// <summary>Reads resource_fields: objects with x, y (tile ints), kind ("common" or "rare") and amount. Empty when absent.</summary>
        private static List<ResourceField> ReadResourceFields(JsonValue root, int width, int height)
        {
            List<ResourceField> fields = new List<ResourceField>();
            if (!root.TryGetMember("resource_fields", out JsonValue? node))
            {
                return fields;
            }
            foreach (JsonValue item in node.Items)
            {
                item.RejectUnknownKeys("x", "y", "kind", "amount");
                // Tile X of the field. Unit: tiles. Range: 0 to width - 1.
                int x = item.RequireInt("x", 0, width - 1);
                // Tile Y of the field. Unit: tiles. Range: 0 to height - 1.
                int y = item.RequireInt("y", 0, height - 1);
                // "common" or "rare".
                string kindText = item.RequireNonEmptyString("kind");
                ResourceKind kind;
                if (kindText == "common")
                {
                    kind = ResourceKind.Common;
                }
                else if (kindText == "rare")
                {
                    kind = ResourceKind.Rare;
                }
                else
                {
                    throw item.Require("kind").Fail("must be \"common\" or \"rare\" (found " + kindText + ")");
                }
                // Starting amount. Unit: resource units. Range: 1 to 1 000 000.
                int amount = item.RequireInt("amount", 1, 1000000);
                fields.Add(new ResourceField(x, y, kind, amount));
            }
            return fields;
        }

        /// <summary>Reads core: x, y (tiles, decimal) and radius_tiles. Null when absent.</summary>
        private static CoreArea? ReadCore(JsonValue root, int width, int height)
        {
            if (!root.TryGetMember("core", out JsonValue? node))
            {
                return null;
            }
            node.RejectUnknownKeys("x", "y", "radius_tiles");
            // Core centre X. Unit: tiles. Range: 0 to width.
            Fix64 x = node.RequireFix64("x", "0", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Core centre Y. Unit: tiles. Range: 0 to height.
            Fix64 y = node.RequireFix64("y", "0", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Capture radius. Unit: tiles. Range: 1 to 32.
            Fix64 radius = node.RequireFix64("radius_tiles", "1", "32");
            return new CoreArea(new FixVec2(x, y), radius);
        }

        // ---------------------------------------------------------------- cycles

        /// <summary>Reads cycles/&lt;id&gt;.json.</summary>
        private static CycleDefinition ReadCycle(JsonValue root, string path, string[] parts)
        {
            CheckDepth(path, parts, 2, "cycles/<id>.json");
            root.RejectUnknownKeys("id", "display_name", "favours", "description");
            // Cycle ID taken from the file name. Range: lower-case letters, digits and underscores.
            string id = ReadId(root, StemOf(parts), false);
            // Cycle display name. Range: non-empty string.
            string displayName = root.RequireNonEmptyString("display_name");
            // Raw text of the favours field, validated by the switch below.
            string favoursText = root.RequireString("favours");
            // Parsed favour enum. Range: Air, Attackers or Position.
            CycleFavour favours;
            switch (favoursText)
            {
                case "air": favours = CycleFavour.Air; break;
                case "attackers": favours = CycleFavour.Attackers; break;
                case "position": favours = CycleFavour.Position; break;
                default:
                    throw root.Require("favours").Fail("must be \"air\", \"attackers\" or \"position\" (found \"" + favoursText + "\")");
            }
            // Description text from the file. Range: non-empty string.
            string description = root.RequireNonEmptyString("description");
            return new CycleDefinition(id, displayName, favours, description);
        }
    }
}
