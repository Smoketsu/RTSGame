// rts/tests/Sim.Tests/Cycles/CycleTestRig.cs
// Shared fixtures for the Planetary Cycle and Core capture tests (spec: rts/docs/specs/planetary_cycles.md section 7): a flat
// 32 by 32 map with a Core at (16, 16) radius 4, the shipped tuning and cycle files, small test units (scout, sky scout, striker,
// dummy, tower, ...), and a rig that wraps a started simulation with script-command helpers and an event log that remembers the
// tick of every event. Part of Rts.Sim.Tests. Test code only (may read files).
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Cycles
{
    /// <summary>Builders for cycle tests: units, maps, tuning edits and the <see cref="CycleRig"/>.</summary>
    internal static class CycleWorld
    {
        /// <summary>Ground unit, 100 health, speed 2, radius 0.5, no weapon. The unit "present" in the Core in most tests.</summary>
        public const string ScoutId = "test.scout";

        /// <summary>Flying unit, 100 health, speed 4, radius 0.5, no weapon.</summary>
        public const string SkyScoutId = "test.sky_scout";

        /// <summary>Ground unit with a weapon of range 5 (damage 1, cooldown one tick, ground and water targets), radius 0.5, sight 8. Mobile, so the range cycle effect applies.</summary>
        public const string ArcherId = "test.archer";

        /// <summary>Like <see cref="ArcherId"/> but 10 damage per hit, for the Core surge multiplier.</summary>
        public const string StrikerId = "test.striker";

        /// <summary>Immobile, unarmed target: 100000 health, radius 0.5, light armour. Speed 0, so it never counts for the Core.</summary>
        public const string DummyId = "test.dummy";

        /// <summary>Ground unit with a range-2 weapon (a ranged weapon, not melee).</summary>
        public const string Range2Id = "test.range2";

        /// <summary>Ground unit with a range-1 weapon (melee).</summary>
        public const string MeleeId = "test.melee";

        /// <summary>Flying unit with a range-5 weapon, to prove the range effect leaves flyers alone.</summary>
        public const string SkyShooterId = "test.sky_shooter";

        /// <summary>Static defence: 300 health, speed 0, footprint 1, no weapon.</summary>
        public const string WallId = "test.wall";

        /// <summary>Water-layer unit: 100 health, speed 2, radius 0.5, no weapon.</summary>
        public const string BoatId = "test.boat";

        /// <summary>Player 0.</summary>
        public static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1.</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Player 2.</summary>
        public static readonly PlayerId P2 = new PlayerId(2);

        /// <summary>Team 0.</summary>
        public static readonly TeamId T0 = new TeamId(0);

        /// <summary>Team 1.</summary>
        public static readonly TeamId T1 = new TeamId(1);

        /// <summary>Team 2.</summary>
        public static readonly TeamId T2 = new TeamId(2);

        /// <summary>The test catalog plus the cycle test units, all of the plain "test" faction (no mechanic).</summary>
        public static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            WeaponDefinition range5 = new WeaponDefinition(Fix64.One, "kinetic", Fix64.FromInt(5), Fix64.Parse("0.05"), true, true, false);
            catalog.AddUnit(new UnitDefinition(ScoutId, "Scout", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(SkyScoutId, "Sky scout", TestWorld.TestFactionId, MovementLayer.Air,
                Fix64.FromInt(100), Fix64.FromInt(4), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(BoatId, "Boat", TestWorld.TestFactionId, MovementLayer.Water,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(ArcherId, "Archer", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), range5));
            catalog.AddUnit(new UnitDefinition(StrikerId, "Striker", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.FromInt(10), "kinetic", Fix64.FromInt(5), Fix64.Parse("0.05"), true, true, false)));
            catalog.AddUnit(new UnitDefinition(DummyId, "Dummy", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100000), Fix64.Zero, 0, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), null));
            catalog.AddUnit(new UnitDefinition(Range2Id, "Range 2", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.One, "kinetic", Fix64.FromInt(2), Fix64.One, true, true, false)));
            catalog.AddUnit(new UnitDefinition(MeleeId, "Melee", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.One, "kinetic", Fix64.One, Fix64.One, true, true, false)));
            catalog.AddUnit(new UnitDefinition(SkyShooterId, "Sky shooter", TestWorld.TestFactionId, MovementLayer.Air,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 0, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.One, "kinetic", Fix64.FromInt(5), Fix64.One, true, true, false), null, null, Fix64.FromInt(8)));
            catalog.AddUnit(new UnitDefinition(WallId, "Wall", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(300), Fix64.Zero, 0, Fix64.Half, 0, 0, "light", Fix64.FromInt(8), null, null,
                new UnitEconomyDefinition(Fix64.FromInt(2), false, false, new List<string>(), true, 1, 0)));
            return catalog;
        }

        /// <summary>A flat 32 by 32 map, spawns at (4, 4) and (28, 28), a Core at (16, 16) radius 4 and the given quake paths.</summary>
        /// <param name="withCore">False for a map without a Core.</param>
        /// <param name="paths">Quake paths, or null for none.</param>
        /// <param name="heights">Elevation layer per tile (1024 entries), or null for a flat map.</param>
        /// <param name="ramps">Ramp flag per tile (1024 entries), or null for none.</param>
        /// <param name="fields">Resource fields, or null for none.</param>
        public static MapDefinition Map(bool withCore = true, List<QuakePath>? paths = null, byte[]? heights = null, bool[]? ramps = null,
            List<ResourceField>? fields = null)
        {
            return new MapDefinition("cycle_test", "Cycle test", 32, 32,
                new List<FixVec2> { FixVec2.FromInts(4, 4), FixVec2.FromInts(28, 28) }, heights, ramps, fields,
                withCore ? new CoreArea(FixVec2.FromInts(16, 16), Fix64.FromInt(4)) : null, paths);
        }

        /// <summary>Tile index on the 32 by 32 test map.</summary>
        public static int Tile(int x, int y)
        {
            return y * 32 + x;
        }

        /// <summary>The three shipped cycle definitions (windstorm, quake, core_surge), loaded from rts/data.</summary>
        public static List<CycleDefinition> ShippedCycles()
        {
            return new List<CycleDefinition>(TestData.LoadAll().Cycles.Values);
        }

        /// <summary>Players 0 and 1 on teams 0 and 1, plus player 2 on team 2 when <paramref name="count"/> is 3. All of the plain test faction.</summary>
        public static List<PlayerSetup> Players(int count = 2)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, T0, TestWorld.TestFactionId, "Alice"),
                new PlayerSetup(P1, T1, TestWorld.TestFactionId, "Bob"),
            };
            if (count >= 3)
            {
                players.Add(new PlayerSetup(P2, T2, TestWorld.TestFactionId, "Cleo"));
            }
            return players;
        }

        /// <summary>The shipped tuning with each (file, old text, new text) edit applied once, in order. Each old text must occur in its file.</summary>
        public static Tuning EditedTuning(params string[] fileOldNew)
        {
            return DataLoader.Load(EditedFiles(fileOldNew)).Tuning;
        }

        /// <summary>The shipped data files with each (file, old text, new text) triple applied once, in order.</summary>
        public static List<DataSourceFile> EditedFiles(params string[] fileOldNew)
        {
            // The real files, edited in place below.
            List<DataSourceFile> files = TestData.ReadAll();
            // Index into the triples. Range: multiples of 3.
            for (int i = 0; i < fileOldNew.Length; i += 3)
            {
                // Position of the file in the list.
                int at = files.FindIndex(f => f.Path == fileOldNew[i]);
                if (at < 0)
                {
                    throw new InvalidOperationException("Test setup: no data file " + fileOldNew[i] + ".");
                }
                // Text of the file with CRLF turned into LF, so the edit text matches on every checkout.
                string text = files[at].Text.Replace("\r\n", "\n");
                int found = text.IndexOf(fileOldNew[i + 1], StringComparison.Ordinal);
                if (found < 0)
                {
                    throw new InvalidOperationException("Test setup: '" + fileOldNew[i + 1] + "' not found in " + fileOldNew[i] + ".");
                }
                files[at] = new DataSourceFile(fileOldNew[i], text.Substring(0, found) + fileOldNew[i + 2] + text.Substring(found + fileOldNew[i + 1].Length));
            }
            return files;
        }

        /// <summary>Tuning edits that make cycles shift every 60 s with no warning (a shift on tick 1200, the next on 2400).</summary>
        public static string[] ShortCycleEdits()
        {
            return new[]
            {
                "tuning/match.json", "\"planetary_cycle_interval_seconds\": 240", "\"planetary_cycle_interval_seconds\": 60",
                "tuning/match.json", "\"planetary_cycle_warning_seconds\": 60", "\"planetary_cycle_warning_seconds\": 0",
            };
        }
    }

    /// <summary>A started simulation with script-command helpers and an event log that keeps the tick of every event.</summary>
    internal sealed class CycleRig
    {
        /// <summary>The simulation under test.</summary>
        public Simulation Sim { get; }

        /// <summary>Every event of every tick stepped so far, in order.</summary>
        public List<SimEvent> Events { get; } = new List<SimEvent>();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _scriptSequence;

        /// <summary>Next sequence number per player slot. Unit: counter. Range: 0 upward.</summary>
        private readonly SortedDictionary<PlayerId, uint> _playerSequence = new SortedDictionary<PlayerId, uint>();

        /// <summary>Units spawned through <see cref="Spawn"/> so far; the next spawn gets ID spawned + 1. Unit: count.</summary>
        private uint _spawned;

        /// <summary>Starts a simulation.</summary>
        /// <param name="players">The players; default two on two teams.</param>
        /// <param name="map">The map; default the 32 by 32 map with a Core.</param>
        /// <param name="tuning">Tuning; default the shipped tuning.</param>
        /// <param name="cycles">Cycles in play; null means none (use <see cref="CycleWorld.ShippedCycles"/> for the three shipped ones).</param>
        /// <param name="seed">Match seed.</param>
        /// <param name="hashInterval">Ticks between state hashes. Range: 1 to 600.</param>
        public CycleRig(List<PlayerSetup>? players = null, MapDefinition? map = null, Tuning? tuning = null, List<CycleDefinition>? cycles = null,
            ulong seed = 1234, int hashInterval = 600)
        {
            SimulationConfig config = new SimulationConfig(new MatchSettings(seed, TestWorld.TicksPerSecond, hashInterval), CycleWorld.Catalog(),
                players ?? CycleWorld.Players(), BuiltInMechanics.CreateRegistry(), tuning ?? TestWorld.Tuning, map ?? CycleWorld.Map(), cycles);
            Sim = new Simulation(config);
            Sim.Start();
        }

        /// <summary>Queues a script spawn and returns the ID the unit will get. Spawns must be queued in chronological order for the predicted IDs to hold.</summary>
        public EntityId Spawn(int tick, string definition, PlayerId owner, string x, string y)
        {
            Sim.Submit(new SpawnUnitCommand(tick, PlayerId.None, _scriptSequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
            _spawned++;
            return new EntityId(_spawned);
        }

        /// <summary>Queues a script removal of a unit.</summary>
        public void Remove(int tick, EntityId unit)
        {
            Sim.Submit(new RemoveUnitCommand(tick, PlayerId.None, _scriptSequence++, unit));
        }

        /// <summary>Queues a script move order.</summary>
        public void Move(int tick, EntityId unit, string x, string y)
        {
            Sim.Submit(new MoveUnitCommand(tick, PlayerId.None, _scriptSequence++, unit, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
        }

        /// <summary>Queues a pick_cycle from a player.</summary>
        public void Pick(int tick, PlayerId player, string cycleId)
        {
            Sim.Submit(new PickCycleCommand(tick, player, NextSequence(player), cycleId));
        }

        /// <summary>Queues a pick_cycle from the script issuer.</summary>
        public void ScriptPick(int tick, string cycleId)
        {
            Sim.Submit(new PickCycleCommand(tick, PlayerId.None, _scriptSequence++, cycleId));
        }

        /// <summary>Queues a surrender by a player.</summary>
        public void Surrender(int tick, PlayerId player)
        {
            Sim.Submit(new SurrenderCommand(tick, player, NextSequence(player), player));
        }

        /// <summary>Next command sequence number of a player.</summary>
        private uint NextSequence(PlayerId player)
        {
            _playerSequence.TryGetValue(player, out uint next);
            _playerSequence[player] = next + 1;
            return next;
        }

        /// <summary>Steps until <paramref name="lastTick"/> has been simulated (the next tick to run is lastTick + 1), logging the events.</summary>
        public void RunThrough(int lastTick)
        {
            while (Sim.Clock.Tick <= lastTick)
            {
                Sim.Step();
                Events.AddRange(Sim.Events.Current);
            }
        }

        /// <summary>Events of type T, in order.</summary>
        public List<T> Of<T>() where T : SimEvent
        {
            List<T> found = new List<T>();
            foreach (SimEvent evt in Events)
            {
                if (evt is T match)
                {
                    found.Add(match);
                }
            }
            return found;
        }

        /// <summary>Ticks of the events of type T accepted by <paramref name="filter"/>, in order.</summary>
        public List<int> TicksOf<T>(Func<T, bool>? filter = null) where T : SimEvent
        {
            List<int> ticks = new List<int>();
            foreach (T evt in Of<T>())
            {
                if (filter == null || filter(evt))
                {
                    ticks.Add(evt.Tick);
                }
            }
            return ticks;
        }

        /// <summary>The first event of type T on <paramref name="tick"/> accepted by the filter, or null.</summary>
        public T? At<T>(int tick, Func<T, bool>? filter = null) where T : SimEvent
        {
            foreach (T evt in Of<T>())
            {
                if (evt.Tick == tick && (filter == null || filter(evt)))
                {
                    return evt;
                }
            }
            return null;
        }

        /// <summary>Rejection reasons logged at <paramref name="tick"/>, in order.</summary>
        public List<string> RejectionsAt(int tick)
        {
            List<string> reasons = new List<string>();
            foreach (CommandRejectedEvent evt in Of<CommandRejectedEvent>())
            {
                if (evt.Tick == tick)
                {
                    reasons.Add(evt.Reason);
                }
            }
            return reasons;
        }

        /// <summary>The live unit with this ID.</summary>
        public Unit UnitOf(EntityId id)
        {
            return Sim.Units.TryGet(id, out Unit? unit) ? unit : throw new InvalidOperationException("No unit " + id);
        }

        /// <summary>Index of the first event in <see cref="Events"/> of type T on <paramref name="tick"/>, or -1. For checking the order of two events on one tick.</summary>
        public int IndexOf<T>(int tick, Func<T, bool>? filter = null) where T : SimEvent
        {
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i] is T match && match.Tick == tick && (filter == null || filter(match)))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
