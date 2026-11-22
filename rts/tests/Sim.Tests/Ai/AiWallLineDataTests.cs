// rts/tests/Sim.Tests/Ai/AiWallLineDataTests.cs
// Data tests of the authored AI wall lines (rts/data/ai/walls/<map id>.json) of rts/docs/specs/ai_mechanic_use.md. P4-0: WD-4 (the loader route and the digest). P4: WD-1 (parse and validation), WD-2 (the seal, exit,
// covers and not-vacuous properties in the simulation's own path code, for every area of every file) and WD-3 (mirror symmetry). Every file in DataSet.AiWallLines is covered, so a new map file needs no new test. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Pathing;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Tests of the authored wall-line data and of the loader route that carries it.</summary>
    [TestFixture]
    public class AiWallLineDataTests
    {
        /// <summary>Path of the authored file of prototype_1v1, data-root-relative. Range: fixed.</summary>
        private const string ProtoWalls = "ai/walls/prototype_1v1.json";

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Plays the first ticks of smoke.json on the given data and returns the content digest and the state hash.</summary>
        /// <param name="data">Loaded data set.</param>
        /// <param name="ticks">Ticks to step. Unit: ticks. Range: 1 to 1000.</param>
        private static (ulong Digest, ulong Hash) RunSmoke(DataSet data, int ticks)
        {
            // The smoke script from the real data folder.
            CommandScript script = CommandScript.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts", "smoke.json")), "scripts/smoke.json", BuiltInMechanics.ScriptParsers);
            // The simulation under test.
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(script.Seed), data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId)));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            // Tick counter. Unit: ticks. Range: 0 to ticks.
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
            }
            return (sim.ContentDigest, sim.ComputeStateHash());
        }

        /// <summary>WD-4: the wall file is routed to AiWallLines, not AiProfiles; misplaced and mismatched files are rejected; the digest and state hash ignore it.</summary>
        [Test]
        public void Wd4_LoaderRouteAndDigest()
        {
            // Everything as shipped.
            DataSet data = TestData.LoadAll();
            Assert.That(data.AiWallLines.Keys, Does.Contain("prototype_1v1"));
            Assert.That(data.AiProfiles.Keys, Does.Not.Contain("prototype_1v1"));
            Assert.That(data.AiProfiles.Keys, Does.Contain("aggressive_s5_m"));

            // The shipped files plus one file one level too deep under ai/, which stays rejected.
            List<DataSourceFile> deep = TestData.ReadAll();
            deep.Add(new DataSourceFile("ai/other/x.json", "{ \"$schema_version\": 1, \"$doc\": \"x\", \"id\": \"x\" }"));
            DataValidationException wrongPlace = Expect.Throws<DataValidationException>(() => DataLoader.Load(deep));
            Assert.That(wrongPlace.Message, Does.Contain("ai/other/x.json").And.Contain("wrong place"));

            // A wall file whose id differs from its stem is rejected.
            DataValidationException badId = Expect.Throws<DataValidationException>(() =>
                DataLoader.Load(TestData.WithEdit(ProtoWalls, "\"id\": \"prototype_1v1\"", "\"id\": \"prototype_x\"")));
            Assert.That(badId.Message, Does.Contain(ProtoWalls));

            // The same data without the wall files: same digest, same state hash after 200 ticks.
            DataSet without = DataLoader.Load(TestData.Without(ProtoWalls));
            Assert.That(without.AiWallLines, Is.Empty);
            (ulong Digest, ulong Hash) withWalls = RunSmoke(data, 200);
            (ulong Digest, ulong Hash) withoutWalls = RunSmoke(without, 200);
            Assert.That(withoutWalls.Digest, Is.EqualTo(withWalls.Digest));
            Assert.That(withoutWalls.Hash, Is.EqualTo(withWalls.Hash));
        }

        /// <summary>Parses wall-line file text as the loader would hand it over, with the file name of <see cref="ProtoWalls"/>.</summary>
        /// <param name="text">The file text.</param>
        private static AiMapWalls ParseText(string text)
        {
            return AiWallLines.Parse(JsonReader.Parse(text, ProtoWalls), Data);
        }

        /// <summary>The shipped prototype_1v1 file text with the first occurrence of <paramref name="oldText"/> replaced.</summary>
        /// <param name="oldText">Text to replace; must occur.</param>
        /// <param name="newText">Replacement.</param>
        private static string Edited(string oldText, string newText)
        {
            // The shipped text, line endings normalised.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "walls", "prototype_1v1.json")).Replace("\r\n", "\n");
            // Index of the text to replace. Range: 0 upward.
            int at = text.IndexOf(oldText, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "test setup: '" + oldText + "' not in the file");
            return text.Substring(0, at) + newText + text.Substring(at + oldText.Length);
        }

        /// <summary>Asserts that the edited file is rejected and the message names the file and the given key text.</summary>
        /// <param name="oldText">Text to replace.</param>
        /// <param name="newText">Replacement.</param>
        /// <param name="mention">Text the message must contain besides the file name.</param>
        private static void AssertRejected(string oldText, string newText, string mention)
        {
            DataValidationException error = Expect.Throws<DataValidationException>(() => ParseText(Edited(oldText, newText)));
            Assert.That(error.Message, Does.Contain(ProtoWalls), "file name");
            Assert.That(error.Message, Does.Contain(mention), error.Message);
        }

        /// <summary>WD-1: every file parses; prototype_1v1 matches the spec table; profiles carry the lines; bad hand-written files are rejected with file and key path.</summary>
        [Test]
        public void Wd1_ParseAndValidation()
        {
            foreach (KeyValuePair<string, JsonValue> pair in Data.AiWallLines)
            {
                AiMapWalls parsed = AiWallLines.Parse(pair.Value, Data);
                Assert.That(parsed.MapId, Is.EqualTo(pair.Key));
                Assert.That(Data.Maps.ContainsKey(pair.Key), Is.True);
            }
            // The table of M3.2.3: area ID, kind, slot, fields, tiles.
            object[][] table =
            {
                new object[] { "main_0", AiWallAreaKind.Main, 0, "0,1", 8 },
                new object[] { "natural_0", AiWallAreaKind.Expansion, 0, "2,3,16", 18 },
                new object[] { "basin_0", AiWallAreaKind.Expansion, 0, "7,18", 19 },
                new object[] { "main_1", AiWallAreaKind.Main, 1, "8,9", 8 },
                new object[] { "natural_1", AiWallAreaKind.Expansion, 1, "10,11,19", 18 },
                new object[] { "basin_1", AiWallAreaKind.Expansion, 1, "15,21", 19 },
                new object[] { "flank_w", AiWallAreaKind.Expansion, -1, "4,17", 21 },
                new object[] { "flank_e", AiWallAreaKind.Expansion, -1, "12,20", 21 },
                new object[] { "corner_nw", AiWallAreaKind.Expansion, -1, "5,6", 16 },
                new object[] { "corner_se", AiWallAreaKind.Expansion, -1, "13,14", 16 },
            };
            AiMapWalls proto = AiWallLines.Parse(Data.AiWallLines["prototype_1v1"], Data);
            Assert.That(proto.Areas.Count, Is.EqualTo(10));
            // Index into the table. Range: 0 to 9.
            for (int i = 0; i < table.Length; i++)
            {
                AiWallArea area = proto.Areas[i];
                Assert.That(area.Id, Is.EqualTo((string)table[i][0]));
                Assert.That(area.Kind, Is.EqualTo((AiWallAreaKind)table[i][1]));
                Assert.That(area.Slot, Is.EqualTo((int)table[i][2]));
                Assert.That(string.Join(",", area.Fields), Is.EqualTo((string)table[i][3]));
                Assert.That(AiWallLines.AllTiles(area).Count, Is.EqualTo((int)table[i][4]), area.Id);
                // Gate tiles of the area. Unit: tiles. Range: exactly 1 for the shipped file.
                int gates = 0;
                foreach (AiWallLine line in area.Lines)
                {
                    gates += line.Gate != null ? 1 : 0;
                }
                Assert.That(gates, Is.EqualTo(1), area.Id);
            }
            Assert.That(proto.Areas[0].Lines[1].Gate, Is.EqualTo((22, 8)));
            Assert.That(proto.Areas[3].Lines[1].Gate, Is.EqualTo((73, 87)));
            // The candidate and its forced-opening copy carry the lines.
            AiProfile candidate = AiProfile.FromDataSet(Data, "aggressive_s5_m");
            Assert.That(candidate.WallLines.ContainsKey("prototype_1v1"), Is.True);
            Assert.That(candidate.WithForcedOpenings(new Dictionary<string, string>()).WallLines.ContainsKey("prototype_1v1"), Is.True);

            AssertRejected("\"areas\": [", "\"zzz\": 1, \"areas\": [", "zzz");
            AssertRejected("\"kind\": \"main\",", "\"kind\": \"main\", \"zzz\": 1,", "zzz");
            AssertRejected("{ \"from\": { \"x\": 4, \"y\": 21 }, \"to\": { \"x\": 7, \"y\": 21 } }", "{ \"from\": { \"x\": 4, \"y\": 21 }, \"to\": { \"x\": 7, \"y\": 21 }, \"zzz\": 1 }", "zzz");
            AssertRejected("\"id\": \"prototype_1v1\"", "\"id\": \"prototype_x\"", "not a loaded map");
            AssertRejected("\"id\": \"natural_0\"", "\"id\": \"main_0\"", "duplicates");
            AssertRejected("\"kind\": \"main\"", "\"kind\": \"base\"", "kind");
            AssertRejected("\"slot\": 0,", "\"slot\": 2,", "slot");
            AssertRejected("\"fields\": [0, 1]", "\"fields\": [0, 22]", "fields");
            AssertRejected("\"fields\": [2, 3, 16]", "\"fields\": [2, 3, 0]", "another entry");
            AssertRejected("\"covers\": [\"main_0\"]", "\"covers\": [\"nope\"]", "not in this file");
            AssertRejected("\"covers\": [\"main_0\"]", "\"covers\": [\"natural_0\"]", "its own area");
            AssertRejected("\"to\": { \"x\": 7, \"y\": 21 }", "\"to\": { \"x\": 20, \"y\": 21 }", "wall_line_max_tiles");
            AssertRejected("\"from\": { \"x\": 4, \"y\": 21 }", "\"from\": { \"x\": 96, \"y\": 21 }", "off the map");
            AssertRejected("{ \"from\": { \"x\": 4, \"y\": 21 }, \"to\": { \"x\": 7, \"y\": 21 } }", "{ \"from\": { \"x\": 30, \"y\": 13 }, \"to\": { \"x\": 30, \"y\": 13 } }", "resource field");
            AssertRejected("\"gate\": { \"x\": 22, \"y\": 8 }", "\"gate\": { \"x\": 22, \"y\": 12 }", "not a tile of its line");
            AssertRejected(", \"gate\": { \"x\": 22, \"y\": 8 }", "", "no gate");
        }

        /// <summary>The wall and gate definitions of a faction: the ordinally lowest ID with is_wall and not is_gate, and with is_gate.</summary>
        /// <param name="data">Loaded data.</param>
        /// <param name="faction">Faction ID.</param>
        private static (UnitDefinition Wall, UnitDefinition Gate) WallDefinitions(DataSet data, string faction)
        {
            UnitDefinition? wall = null;
            UnitDefinition? gate = null;
            foreach (UnitDefinition unit in data.Content.Units.Values)
            {
                if (unit.FactionId != faction || !unit.Economy.IsWall)
                {
                    continue;
                }
                if (unit.Economy.IsGate)
                {
                    gate = gate == null || string.CompareOrdinal(unit.Id, gate.Id) < 0 ? unit : gate;
                }
                else
                {
                    wall = wall == null || string.CompareOrdinal(unit.Id, wall.Id) < 0 ? unit : wall;
                }
            }
            Assert.That(wall, Is.Not.Null, faction);
            Assert.That(gate, Is.Not.Null, faction);
            return (wall!, gate!);
        }

        /// <summary>A started simulation on a map with one player per spawn (factions alternate water and skai, one team each); optionally with the walls and gates of an area spawned by script for its slot player.</summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The area to wall, or null for a wall-free simulation.</param>
        private static Simulation SealSim(MapDefinition map, AiWallArea? area)
        {
            // One player per spawn.
            List<PlayerSetup> players = new List<PlayerSetup>();
            for (int i = 0; i < map.Spawns.Count; i++)
            {
                players.Add(new PlayerSetup(new PlayerId((byte)i), new TeamId((byte)i), i % 2 == 0 ? "water" : "skai", "P" + i));
            }
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, map));
            sim.Start();
            if (area == null)
            {
                return sim;
            }
            // The slot player owns the walls; a neutral area uses player 0.
            int owner = Math.Max(0, area.Slot);
            (UnitDefinition wall, UnitDefinition gate) = WallDefinitions(Data, players[owner].FactionId);
            // Script sequence number. Unit: counter. Range: 0 upward.
            uint sequence = 0;
            foreach (AiWallLine line in area.Lines)
            {
                foreach ((int X, int Y) tile in line.Tiles)
                {
                    sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, sequence++, line.Gate == tile ? gate.Id : wall.Id, players[owner].Id,
                        new FixVec2(Fix64.FromInt(tile.X) + Fix64.Half, Fix64.FromInt(tile.Y) + Fix64.Half)));
                }
            }
            sim.Step();
            return sim;
        }

        /// <summary>The target tiles G of an area (M3.2.2): anchors, the neighbours of each listed field tile that have the field tile's height, and the slot's spawn tile; as tile indices, ascending.</summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The area.</param>
        private static SortedSet<int> Targets(MapDefinition map, AiWallArea area)
        {
            // Tile indices of the targets.
            SortedSet<int> targets = new SortedSet<int>();
            foreach ((int X, int Y) anchor in area.Anchors)
            {
                targets.Add(anchor.Y * map.WidthTiles + anchor.X);
            }
            foreach (int fieldIndex in area.Fields)
            {
                ResourceField field = map.ResourceFields[fieldIndex];
                // Height layer of the field tile. Range: 0 to 7.
                byte height = map.HeightAt(field.Y * map.WidthTiles + field.X);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        // Neighbour tile. Unit: tiles.
                        int nx = field.X + dx;
                        int ny = field.Y + dy;
                        if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < map.WidthTiles && ny < map.HeightTiles && map.HeightAt(ny * map.WidthTiles + nx) == height)
                        {
                            targets.Add(ny * map.WidthTiles + nx);
                        }
                    }
                }
            }
            if (area.Slot >= 0)
            {
                targets.Add(SpawnTile(map, area.Slot));
            }
            return targets;
        }

        /// <summary>Tile index of a spawn.</summary>
        /// <param name="map">The map.</param>
        /// <param name="spawn">Spawn index. Range: 0 to spawn count - 1.</param>
        private static int SpawnTile(MapDefinition map, int spawn)
        {
            return map.Spawns[spawn].Y.FloorToInt() * map.WidthTiles + map.Spawns[spawn].X.FloorToInt();
        }

        /// <summary>Enemy start tiles E of an area: every spawn except its slot (every spawn for a neutral area).</summary>
        /// <param name="map">The map.</param>
        /// <param name="area">The area.</param>
        private static List<int> EnemyStarts(MapDefinition map, AiWallArea area)
        {
            List<int> starts = new List<int>();
            for (int i = 0; i < map.Spawns.Count; i++)
            {
                if (i != area.Slot)
                {
                    starts.Add(SpawnTile(map, i));
                }
            }
            return starts;
        }

        /// <summary>WD-2: for every area of every file, in the simulation's own path code with the walls and gates standing: sealed against every enemy start, own team exits through the gate, every covered area is sealed too, and on the bare map the targets are reachable (not vacuous).</summary>
        [Test]
        public void Wd2_SealInTheSimulationsOwnPathCode()
        {
            foreach (KeyValuePair<string, JsonValue> file in Data.AiWallLines)
            {
                AiMapWalls walls = AiWallLines.Parse(file.Value, Data);
                MapDefinition map = Data.Maps[file.Key];
                // The wall-free simulation of the map, for the not-vacuous check.
                Simulation bare = SealSim(map, null);
                foreach (AiWallArea area in walls.Areas)
                {
                    SortedSet<int> targets = Targets(map, area);
                    List<int> enemies = EnemyStarts(map, area);
                    Simulation sim = SealSim(map, area);
                    // Counts for the report: tiles, walls, gates and the sealed and funnel costs from the unit definitions.
                    List<(int X, int Y)> tiles = AiWallLines.AllTiles(area);
                    int gates = 0;
                    foreach (AiWallLine line in area.Lines)
                    {
                        gates += line.Gate != null ? 1 : 0;
                    }
                    (UnitDefinition wall, UnitDefinition gate) = WallDefinitions(Data, "water");
                    int sealedCost = (tiles.Count - gates) * wall.CostCommon + gates * gate.CostCommon;
                    int funnelCost = (tiles.Count - gates) * wall.CostCommon;
                    TestContext.WriteLine(file.Key + "/" + area.Id + ": tiles " + tiles.Count + " walls " + (tiles.Count - gates) + " gates " + gates + " sealed " + sealedCost + " funnel " + funnelCost);

                    // Seal: no target is reachable from any enemy start, every gate counting as a wall.
                    foreach (int target in targets)
                    {
                        FlowField field = sim.Pathing.Get(MovementLayer.Ground, target, TeamId.None);
                        foreach (int enemy in enemies)
                        {
                            Assert.That(field.IsReachable(enemy), Is.False, file.Key + "/" + area.Id + ": target tile " + target + " is reachable from enemy start tile " + enemy);
                        }
                    }
                    // Exit: for the owner's team every target reaches some enemy start (own units leave through the gate).
                    TeamId owner = new TeamId((byte)Math.Max(0, area.Slot));
                    foreach (int target in targets)
                    {
                        // True when some enemy start can be reached from the target for the owner's team.
                        bool leaves = false;
                        foreach (int enemy in enemies)
                        {
                            leaves |= sim.Pathing.Get(MovementLayer.Ground, enemy, owner).IsReachable(target);
                        }
                        Assert.That(leaves, Is.True, file.Key + "/" + area.Id + ": target tile " + target + " cannot leave through the gate");
                    }
                    // Covers: every target of every covered area is sealed too.
                    foreach (string coveredId in area.Covers)
                    {
                        foreach (AiWallArea covered in walls.Areas)
                        {
                            if (covered.Id != coveredId)
                            {
                                continue;
                            }
                            foreach (int target in Targets(map, covered))
                            {
                                FlowField field = sim.Pathing.Get(MovementLayer.Ground, target, TeamId.None);
                                foreach (int enemy in enemies)
                                {
                                    Assert.That(field.IsReachable(enemy), Is.False, file.Key + "/" + area.Id + " covers " + coveredId + ": target tile " + target + " is reachable from enemy start tile " + enemy);
                                }
                            }
                        }
                    }
                    // Not vacuous: on the bare map every target is reachable from every enemy start.
                    foreach (int target in targets)
                    {
                        FlowField field = bare.Pathing.Get(MovementLayer.Ground, target, TeamId.None);
                        foreach (int enemy in enemies)
                        {
                            Assert.That(field.IsReachable(enemy), Is.True, file.Key + "/" + area.Id + ": target tile " + target + " is unreachable from enemy start tile " + enemy + " even without walls");
                        }
                    }
                    if (file.Key == "prototype_1v1")
                    {
                        Assert.That(gates, Is.EqualTo(1));
                    }
                }
            }
        }

        /// <summary>WD-3: in every file whose map is point-symmetric, every area has exactly one mirrored partner with the same kind, mirrored slot, anchors, line tiles, gates and covers.</summary>
        [Test]
        public void Wd3_Mirror()
        {
            foreach (KeyValuePair<string, JsonValue> file in Data.AiWallLines)
            {
                AiMapWalls walls = AiWallLines.Parse(file.Value, Data);
                MapDefinition map = Data.Maps[file.Key];
                // Map width and height. Unit: tiles.
                int w = map.WidthTiles;
                int h = map.HeightTiles;
                // True while the heights, ramps, spawns and fields are all point-symmetric.
                bool symmetric = true;
                // Tile index being compared with its mirror. Range: 0 to w x h - 1.
                for (int i = 0; i < w * h && symmetric; i++)
                {
                    int mirror = (h - 1 - i / w) * w + (w - 1 - i % w);
                    symmetric &= map.HeightAt(i) == map.HeightAt(mirror) && map.IsRampAt(i) == map.IsRampAt(mirror);
                }
                // Spawn mirror: spawn index to the index of the spawn at the mirrored position.
                int[] spawnMirror = new int[map.Spawns.Count];
                for (int i = 0; i < spawnMirror.Length; i++)
                {
                    spawnMirror[i] = -1;
                    for (int j = 0; j < spawnMirror.Length; j++)
                    {
                        if (map.Spawns[j].X == Fix64.FromInt(w) - map.Spawns[i].X && map.Spawns[j].Y == Fix64.FromInt(h) - map.Spawns[i].Y)
                        {
                            spawnMirror[i] = j;
                        }
                    }
                    symmetric &= spawnMirror[i] >= 0;
                }
                // Field mirror: field index to the field at the mirrored tile.
                int[] fieldMirror = new int[map.ResourceFields.Count];
                for (int i = 0; i < fieldMirror.Length; i++)
                {
                    fieldMirror[i] = -1;
                    for (int j = 0; j < fieldMirror.Length; j++)
                    {
                        if (map.ResourceFields[j].X == w - 1 - map.ResourceFields[i].X && map.ResourceFields[j].Y == h - 1 - map.ResourceFields[i].Y)
                        {
                            fieldMirror[i] = j;
                        }
                    }
                    symmetric &= fieldMirror[i] >= 0;
                }
                if (file.Key == "prototype_1v1")
                {
                    Assert.That(symmetric, Is.True, "prototype_1v1 is point-symmetric");
                }
                if (!symmetric)
                {
                    continue;
                }
                // Partner area by area ID.
                Dictionary<string, AiWallArea> partner = new Dictionary<string, AiWallArea>();
                foreach (AiWallArea a in walls.Areas)
                {
                    // Areas whose field set is the mirrored field set of a, and that mirrored set.
                    List<AiWallArea> matches = new List<AiWallArea>();
                    SortedSet<int> mirroredFields = new SortedSet<int>();
                    foreach (int f in a.Fields)
                    {
                        mirroredFields.Add(fieldMirror[f]);
                    }
                    foreach (AiWallArea b in walls.Areas)
                    {
                        if (mirroredFields.SetEquals(b.Fields))
                        {
                            matches.Add(b);
                        }
                    }
                    Assert.That(matches.Count, Is.EqualTo(1), file.Key + "/" + a.Id + " needs exactly one partner");
                    partner[a.Id] = matches[0];
                }
                foreach (AiWallArea a in walls.Areas)
                {
                    AiWallArea b = partner[a.Id];
                    Assert.That(b.Kind, Is.EqualTo(a.Kind), a.Id);
                    Assert.That(b.Slot, Is.EqualTo(a.Slot < 0 ? -1 : spawnMirror[a.Slot]), a.Id);
                    Assert.That(Mirrored(a.Anchors, w, h), Is.EqualTo(Sorted(b.Anchors)), a.Id + " anchors");
                    Assert.That(Mirrored(AiWallLines.AllTiles(a), w, h), Is.EqualTo(Sorted(AiWallLines.AllTiles(b))), a.Id + " line tiles");
                    // Gate tiles of a and b.
                    List<(int X, int Y)> gatesA = new List<(int X, int Y)>();
                    List<(int X, int Y)> gatesB = new List<(int X, int Y)>();
                    foreach (AiWallLine line in a.Lines)
                    {
                        if (line.Gate != null)
                        {
                            gatesA.Add(line.Gate.Value);
                        }
                    }
                    foreach (AiWallLine line in b.Lines)
                    {
                        if (line.Gate != null)
                        {
                            gatesB.Add(line.Gate.Value);
                        }
                    }
                    Assert.That(Mirrored(gatesA, w, h), Is.EqualTo(Sorted(gatesB)), a.Id + " gates");
                    // IDs of the partners of a's covered areas, and b's covers, ascending.
                    List<string> mappedCovers = new List<string>();
                    foreach (string covered in a.Covers)
                    {
                        mappedCovers.Add(partner[covered].Id);
                    }
                    mappedCovers.Sort(StringComparer.Ordinal);
                    List<string> coversOfB = new List<string>(b.Covers);
                    coversOfB.Sort(StringComparer.Ordinal);
                    Assert.That(mappedCovers, Is.EqualTo(coversOfB), a.Id + " covers");
                }
                if (file.Key == "prototype_1v1")
                {
                    // The expected pairs.
                    string[][] pairs = { new[] { "main_0", "main_1" }, new[] { "natural_0", "natural_1" }, new[] { "basin_0", "basin_1" }, new[] { "flank_w", "flank_e" }, new[] { "corner_nw", "corner_se" } };
                    foreach (string[] pair in pairs)
                    {
                        Assert.That(partner[pair[0]].Id, Is.EqualTo(pair[1]));
                        Assert.That(partner[pair[1]].Id, Is.EqualTo(pair[0]));
                    }
                }
            }
        }

        /// <summary>The tiles mirrored through the map centre, sorted.</summary>
        /// <param name="tiles">Tiles to mirror.</param>
        /// <param name="w">Map width. Unit: tiles.</param>
        /// <param name="h">Map height. Unit: tiles.</param>
        private static List<(int X, int Y)> Mirrored(IReadOnlyList<(int X, int Y)> tiles, int w, int h)
        {
            // Mirrored tiles.
            List<(int X, int Y)> mirrored = new List<(int X, int Y)>();
            foreach ((int X, int Y) tile in tiles)
            {
                mirrored.Add((w - 1 - tile.X, h - 1 - tile.Y));
            }
            return Sorted(mirrored);
        }

        /// <summary>A sorted copy of a tile list (by x, then y), duplicates kept.</summary>
        /// <param name="tiles">Tiles to copy.</param>
        private static List<(int X, int Y)> Sorted(IReadOnlyList<(int X, int Y)> tiles)
        {
            List<(int X, int Y)> copy = new List<(int X, int Y)>(tiles);
            copy.Sort();
            return copy;
        }
    }
}
