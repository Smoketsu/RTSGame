// rts/tests/Sim.Tests/Factions/FloodingTests.cs
// Acceptance tests of the Water flooding mechanic (rts/docs/specs/flooding.md section 7, tests 1 to 12) plus checks of the
// data keys, the cast_wave rejections, tide retreat, the Rooted-HQ source rule, determinism and the shipped smoke script.
// Part of Rts.Sim.Tests. Setup: hand-built test units (a flood source, a swimmer, walkers, a wave caster, a drop-off) on flat
// test maps, P0 = a water-faction player on team 0 and P1 = an ordinary player on team 1, 20 ticks per second. Where a test needs
// no sources to disturb the water it uses tuning with min_retreat_per_second 0. Tiny corridors are made with wall blockers.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Factions
{
    /// <summary>Flood rules, tide, emission, limits, speeds, healing and the wave.</summary>
    [TestFixture]
    public sealed class FloodingTests
    {
        /// <summary>ID of the test flood source: ground, speed 0, no footprint, output 1, capacity from the catalog builder.</summary>
        private const string SourceId = "flood.source";

        /// <summary>ID of the test swimmer: water layer, speed 3, health 100.</summary>
        private const string SwimmerId = "flood.swimmer";

        /// <summary>ID of the test walker: ground, speed 2.5, health 100.</summary>
        private const string WalkerId = "flood.walker";

        /// <summary>ID of the test wave caster: ground, speed 0, can_cast_wave.</summary>
        private const string CasterId = "flood.caster";

        /// <summary>ID of the test drop-off structure: ground, speed 0, drop-off, footprint 1.</summary>
        private const string DropOffId = "flood.dropoff";

        /// <summary>ID of the test mobile HQ that is a flood source while Rooted.</summary>
        private const string FloodHqId = "flood.hq";

        /// <summary>The water player (team 0).</summary>
        private static readonly PlayerId P0 = StatusTestWorld.P0;

        /// <summary>The ordinary opponent (team 1).</summary>
        private static readonly PlayerId P1 = StatusTestWorld.P1;

        /// <summary>Drops per depth unit, as the mechanic counts them.</summary>
        private const int Drops = FloodMath.DropsPerDepthUnit;

        // ------------------------------------------------------------------ fixtures

        /// <summary>The test catalog: the shared test units plus the flooding units and a "water" faction using the flooding mechanic.</summary>
        /// <param name="capacity">Capacity of the test flood source. Unit: depth-unit tiles. Range: above 0.</param>
        private static ContentCatalog Catalog(string capacity)
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition(SourceId, "Source", "water", MovementLayer.Ground, Fix64.FromInt(100), Fix64.Zero, 0, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, null, null, null, new UnitFloodDefinition(Fix64.One, Fix64.Parse(capacity), false)));
            catalog.AddUnit(new UnitDefinition(SwimmerId, "Swimmer", "water", MovementLayer.Water, Fix64.FromInt(100), Fix64.FromInt(3), 1, Fix64.Half, 0, 0));
            catalog.AddUnit(new UnitDefinition(WalkerId, "Walker", "water", MovementLayer.Ground, Fix64.FromInt(100), Fix64.Parse("2.5"), 1, Fix64.Half, 0, 0));
            catalog.AddUnit(new UnitDefinition(CasterId, "Caster", "water", MovementLayer.Ground, Fix64.FromInt(100), Fix64.Zero, 1, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, null, null, null, new UnitFloodDefinition(Fix64.Zero, Fix64.Zero, true)));
            catalog.AddUnit(new UnitDefinition(DropOffId, "Drop-off", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.Zero, 0, Fix64.Half, 0, 0,
                "light", Fix64.FromInt(8), null, null, new UnitEconomyDefinition(Fix64.FromInt(5), false, true, new List<string>(), false, 1, 0)));
            catalog.AddUnit(new UnitDefinition(FloodHqId, "Flood HQ", "water", MovementLayer.Water, Fix64.FromInt(200), Fix64.One, 0, Fix64.Half, 0, 0,
                "armoured", Fix64.FromInt(8), null, new HqDefinition("armoured", "light", TestWorld.WorkerId), null, null,
                new UnitFloodDefinition(Fix64.One, Fix64.FromInt(10), false)));
            catalog.AddFaction(new FactionDefinition("water", "Water", "flooding", new List<string> { SourceId, SwimmerId, WalkerId, CasterId, FloodHqId }));
            return catalog;
        }

        /// <summary>P0 = water faction on team 0, P1 = test faction on team 1.</summary>
        private static List<PlayerSetup> Players()
        {
            return new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "water", "Water"),
                new PlayerSetup(P1, new TeamId(1), TestWorld.TestFactionId, "Other"),
            };
        }

        /// <summary>A flat map of the given size with no fields.</summary>
        private static MapDefinition FlatMap(int width, int height)
        {
            return new MapDefinition("flood_flat", "Flood Flat", width, height, new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(width - 2, height - 2) });
        }

        /// <summary>The shipped tuning with one text replaced in flood.json.</summary>
        private static Tuning TuningWith(string oldText, string newText)
        {
            return DataLoader.Load(TestData.WithEdit(FloodTuning.FilePath, oldText, newText)).Tuning;
        }

        /// <summary>The shipped tuning with min_retreat_per_second 0, so water with no source stays where it is.</summary>
        private static Tuning NoRetreatTuning()
        {
            return TuningWith("\"min_retreat_per_second\": 0.5", "\"min_retreat_per_second\": 0");
        }

        /// <summary>A started match on the given map.</summary>
        private static Simulation Build(MapDefinition map, string sourceCapacity = "40", Tuning? tuning = null)
        {
            SimulationConfig config = new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), Catalog(sourceCapacity),
                Players(), BuiltInMechanics.CreateRegistry(), tuning ?? TestWorld.Tuning, map);
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>The flooding mechanic of a match.</summary>
        private static FloodingMechanic Mechanic(Simulation sim)
        {
            return (FloodingMechanic)sim.Mechanics[FloodingMechanic.MechanicId];
        }

        /// <summary>Drops on a tile (whole drops of its depth).</summary>
        private static int DropsAt(Simulation sim, int x, int y)
        {
            return FloodMath.ToDrops(sim.Terrain.At(x, y).WaterDepth);
        }

        /// <summary>All drops owned by a team on the whole map.</summary>
        private static long Held(Simulation sim, byte team)
        {
            long total = 0;
            for (int i = 0; i < sim.Terrain.TileCount; i++)
            {
                if (sim.Terrain[i].WaterTeam == new TeamId(team))
                {
                    total += FloodMath.ToDrops(sim.Terrain[i].WaterDepth);
                }
            }
            return total;
        }

        /// <summary>Submits code that sets every tile to the same water at tick 0 (script-issued, sequence <paramref name="sequence"/>).</summary>
        private static void UniformWater(Simulation sim, uint sequence, int drops, byte team)
        {
            StatusTestWorld.Run(sim, 0, sequence, s =>
            {
                for (int i = 0; i < s.Terrain.TileCount; i++)
                {
                    s.Terrain.SetWater(s, i, FloodMath.ToDepth(drops), new TeamId(team));
                }
            });
        }

        /// <summary>Submits code at tick 0 that walls every tile with y of at least 1, leaving row 0 as a one-tile-wide corridor.</summary>
        private static void Corridor(Simulation sim, uint sequence)
        {
            StatusTestWorld.Run(sim, 0, sequence, s =>
            {
                for (int i = 0; i < s.Terrain.TileCount; i++)
                {
                    if (s.Terrain.YOf(i) >= 1)
                    {
                        s.Terrain.AddBlockers(s, i, TileBlockers.Wall);
                    }
                }
            });
        }

        /// <summary>A field of flat tiles with layer drops 512 and cap 1024, all dry.</summary>
        private static FloodField Field(int width, int height)
        {
            return new FloodField(width, height, 512, 1024);
        }

        /// <summary>The drops of a field as an array copy.</summary>
        private static int[] DropsOf(FloodField field)
        {
            return (int[])field.Drops.Clone();
        }

        // ------------------------------------------------------------------ 1 to 5: redistribution rules

        /// <summary>1. Flat flow, exact drops: [1000,0,0] gives [800,200,0], then [680,280,40], then [543,325,132]; the sum stays 1000.</summary>
        [Test]
        public void Redistribute_FlatFlow_GivesExactDrops()
        {
            // Passes to run and the drops expected after them.
            (int Passes, int[] Expected)[] cases =
            {
                (1, new[] { 800, 200, 0 }),
                (2, new[] { 680, 280, 40 }),
                (4, new[] { 543, 325, 132 }),
            };
            foreach ((int passes, int[] expected) in cases)
            {
                FloodField field = Field(3, 1);
                field.Drops[0] = 1000;
                field.Team[0] = 0;
                FloodRules.Redistribute(field, passes);
                Assert.That(DropsOf(field), Is.EqualTo(expected), passes + " passes");
                Assert.That(field.Drops[0] + field.Drops[1] + field.Drops[2], Is.EqualTo(1000));
            }
        }

        /// <summary>2. No overshoot: 1000 drops in the centre of a flat 3x3, one pass: centre 200, each orthogonal neighbour 200, corners 0.</summary>
        [Test]
        public void Redistribute_NoOvershoot_CentreKeepsAFifth()
        {
            FloodField field = Field(3, 3);
            field.Drops[4] = 1000;
            field.Team[4] = 0;
            FloodRules.Redistribute(field, 1);
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 0, 200, 0, 200, 200, 200, 0, 200, 0 }));
        }

        /// <summary>3. Downhill across a cliff: [100,0] on heights [1,0] becomes [0,100]; [1000,1020] becomes [996,1024] because the 1024 cap binds and nothing is lost.</summary>
        [Test]
        public void Redistribute_AcrossCliff_FlowsDownAndRespectsCap()
        {
            FloodField field = Field(2, 1);
            field.LayerHeight[0] = 1;
            field.Drops[0] = 100;
            field.Team[0] = 0;
            FloodRules.Redistribute(field, 1);
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 0, 100 }));

            field = Field(2, 1);
            field.LayerHeight[0] = 1;
            field.Drops[0] = 1000;
            field.Drops[1] = 1020;
            field.Team[0] = 0;
            field.Team[1] = 0;
            FloodRules.Redistribute(field, 1);
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 996, 1024 }));
        }

        /// <summary>4. Walls block flow: a wall on tile 1 of [1000,0,0] keeps all the water on tile 0 after 4 passes.</summary>
        [Test]
        public void Redistribute_WallBlocksFlow()
        {
            FloodField field = Field(3, 1);
            field.Wall[1] = true;
            field.Drops[0] = 1000;
            field.Team[0] = 0;
            FloodRules.Redistribute(field, 4);
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 1000, 0, 0 }));
        }

        /// <summary>5. Teams do not mix: [1000 (T0), 0, 1000 (T1)] after one pass is [800, 200, 1000] and the middle tile belongs to team 0 (ascending index claims first).</summary>
        [Test]
        public void Redistribute_TeamsDoNotMix()
        {
            FloodField field = Field(3, 1);
            field.Drops[0] = 1000;
            field.Team[0] = 0;
            field.Drops[2] = 1000;
            field.Team[2] = 1;
            FloodRules.Redistribute(field, 1);
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 800, 200, 1000 }));
            Assert.That(field.Team, Is.EqualTo(new[] { 0, 0, 1 }));
        }

        // ------------------------------------------------------------------ 6: footprint stays dry

        /// <summary>6a. Displace on a 3x1 field: tile 1 with 300 drops moves them to tile 2 (E beats W on the tie) and ends dry.</summary>
        [Test]
        public void Displace_PrefersEastOnATie()
        {
            FloodField field = Field(3, 1);
            field.Drops[1] = 300;
            field.Team[1] = 0;
            field.Footprint[1] = true;
            int lost = FloodRules.Displace(field, 1);
            Assert.That(lost, Is.EqualTo(0));
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 0, 0, 300 }));
            Assert.That(field.Team, Is.EqualTo(new[] { FloodField.NoTeam, FloodField.NoTeam, 0 }));
        }

        /// <summary>6b. Displace removes what the target cannot hold, and all of it when no neighbour accepts.</summary>
        [Test]
        public void Displace_RemovesWhatDoesNotFit()
        {
            FloodField field = Field(3, 1);
            field.Drops[1] = 300;
            field.Team[1] = 0;
            field.Drops[2] = 900;
            field.Team[2] = 0;
            field.Wall[0] = true;
            Assert.That(FloodRules.Displace(field, 1), Is.EqualTo(176));
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 0, 0, 1024 }));

            field = Field(3, 1);
            field.Drops[1] = 300;
            field.Team[1] = 0;
            field.Wall[0] = true;
            field.Wall[2] = true;
            Assert.That(FloodRules.Displace(field, 1), Is.EqualTo(300));
            Assert.That(field.Drops[1], Is.EqualTo(0));
        }

        /// <summary>6c. In a simulation: with no sources and no retreat, water on a tile that gets a footprint moves to tile 2 of a 3-tile corridor in one update.</summary>
        [Test]
        public void Footprint_StaysDry_InAnUpdate()
        {
            Simulation sim = Build(FlatMap(8, 8), tuning: NoRetreatTuning());
            Corridor(sim, 0);
            StatusTestWorld.Run(sim, 0, 1, s =>
            {
                for (int x = 3; x < 8; x++)
                {
                    s.Terrain.AddBlockers(s, s.Terrain.IndexOf(x, 0), TileBlockers.Wall);
                }
                s.Terrain.SetWater(s, s.Terrain.IndexOf(1, 0), FloodMath.ToDepth(300), new TeamId(0));
                s.Terrain.AddBlockers(s, s.Terrain.IndexOf(1, 0), TileBlockers.Footprint);
            });
            StatusTestWorld.StepThrough(sim, 0);
            Assert.That(DropsAt(sim, 1, 0), Is.EqualTo(0));
            Assert.That(DropsAt(sim, 2, 0), Is.EqualTo(300));
            Assert.That(DropsAt(sim, 0, 0), Is.EqualTo(0));
            Assert.That(sim.Terrain.At(1, 0).WaterTeam, Is.EqualTo(TeamId.None));
            Assert.That(sim.Terrain.At(2, 0).WaterTeam, Is.EqualTo(new TeamId(0)));
        }

        /// <summary>6d. A footprint tile whose neighbours are all walls loses its water and the update reports displaced_off.</summary>
        [Test]
        public void Footprint_WithNowhereToGo_ReportsDisplacedOff()
        {
            Simulation sim = Build(FlatMap(8, 8), tuning: NoRetreatTuning());
            StatusTestWorld.Run(sim, 0, 0, s =>
            {
                int centre = s.Terrain.IndexOf(4, 4);
                for (int d = 0; d < 4; d++)
                {
                    s.Terrain.AddBlockers(s, s.Terrain.Neighbour(centre, d), TileBlockers.Wall);
                }
                s.Terrain.SetWater(s, centre, FloodMath.ToDepth(300), new TeamId(0));
                s.Terrain.AddBlockers(s, centre, TileBlockers.Footprint);
            });
            StatusTestWorld.StepThrough(sim, 0);
            Assert.That(DropsAt(sim, 4, 4), Is.EqualTo(0));
            List<FloodWaterRemovedEvent> removed = StatusTestWorld.EventsOf<FloodWaterRemovedEvent>(sim);
            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0].Reason, Is.EqualTo("displaced_off"));
            Assert.That(removed[0].Drops, Is.EqualTo(300));
            Assert.That(removed[0].Team, Is.EqualTo(new TeamId(0)));
        }

        // ------------------------------------------------------------------ 7: tide

        /// <summary>7a. Tide level: T = 4200, low 0.5: f is 0.5 at t = 0, 0.75 at 1050, 1 at 2100 and 0.75 at 3150.</summary>
        [Test]
        public void TideClock_LevelFollowsTheTriangle()
        {
            Fix64 low = Fix64.Parse("0.5");
            Assert.That(TideClock.Level(0, 4200, low), Is.EqualTo(Fix64.Parse("0.5")));
            Assert.That(TideClock.Level(1050, 4200, low), Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(TideClock.Level(2100, 4200, low), Is.EqualTo(Fix64.One));
            Assert.That(TideClock.Level(3150, 4200, low), Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(TideClock.Level(4200, 4200, low), Is.EqualTo(Fix64.Parse("0.5")), "the cycle repeats");
            Assert.That(TideClock.Triangle(0, 4200), Is.EqualTo(0));
            Assert.That(TideClock.Triangle(2100, 4200), Is.EqualTo(4200));
        }

        /// <summary>7b. TideTurnedEvent: Low at ticks 0 and 4200, High at tick 2100 (cycle index = tick / 4200), once per turning point.</summary>
        [Test]
        public void TideTurned_EventsAtTheTurningPoints()
        {
            Simulation sim = Build(FlatMap(8, 8));
            Assert.That(Mechanic(sim).TideTicks, Is.EqualTo(4200));
            List<string> seen = new List<string>();
            for (int tick = 0; tick <= 4200; tick++)
            {
                sim.Step();
                foreach (TideTurnedEvent e in StatusTestWorld.EventsOf<TideTurnedEvent>(sim))
                {
                    seen.Add(e.Tick + ":" + e.Phase + ":" + e.CycleIndex);
                }
            }
            Assert.That(seen, Is.EqualTo(new[] { "0:Low:0", "2100:High:0", "4200:Low:1" }));
        }

        // ------------------------------------------------------------------ 8: emission

        /// <summary>8a. Emission respects the volume: one source with output 1 and capacity 1 (256 drops) holds 128 drops after the tick-0 update and 129 after the tick-20 update.</summary>
        [Test]
        public void Emission_StopsAtTheTideVolume()
        {
            Simulation sim = Build(FlatMap(16, 16), "1");
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 0);
            Assert.That(Held(sim, 0), Is.EqualTo(128));
            StatusTestWorld.StepThrough(sim, 20);
            Assert.That(Held(sim, 0), Is.EqualTo(129));
        }

        /// <summary>8b. With capacity 40 on a flat 16x16 map the team holds exactly 21 x 256 = 5376 drops after the tick-400 update (output-limited, nothing lost to the flow).</summary>
        [Test]
        public void Emission_IsOutputLimitedWhenTheTideAllowsMore()
        {
            Simulation sim = Build(FlatMap(16, 16), "40");
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 400);
            Assert.That(Held(sim, 0), Is.EqualTo(21 * 256));
            Assert.That(Held(sim, 1), Is.EqualTo(0));
            for (int i = 0; i < sim.Terrain.TileCount; i++)
            {
                Assert.That(FloodMath.ToDrops(sim.Terrain[i].WaterDepth), Is.LessThanOrEqualTo(1024));
            }
        }

        /// <summary>8c. The water appears in whole drops: every tile's depth is an exact multiple of 1/256.</summary>
        [Test]
        public void Water_IsAlwaysWholeDrops()
        {
            Simulation sim = Build(FlatMap(16, 16), "40");
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 200);
            for (int i = 0; i < sim.Terrain.TileCount; i++)
            {
                Assert.That(FloodTuning.IsWholeDrops(sim.Terrain[i].WaterDepth), Is.True, "tile " + i);
            }
        }

        /// <summary>8d. A flood update writes through SetWater: it raises TerrainChangedEvent and bumps the terrain version.</summary>
        [Test]
        public void Update_WritesThroughSetWater()
        {
            Simulation sim = Build(FlatMap(16, 16), "40");
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            int before = sim.Terrain.Version;
            StatusTestWorld.StepThrough(sim, 0);
            Assert.That(sim.Terrain.Version, Is.GreaterThan(before));
            Assert.That(StatusTestWorld.EventsOf<TerrainChangedEvent>(sim).Count, Is.GreaterThan(0));
            int quiet = sim.Terrain.Version;
            StatusTestWorld.StepThrough(sim, 5);
            Assert.That(sim.Terrain.Version, Is.EqualTo(quiet), "no update, no terrain change between flood updates");
        }

        // ------------------------------------------------------------------ 9: retreat

        /// <summary>9. Retreat toward the source: 5x1 flat team 0 [200 x 5], seed tile 0, R = 256 gives [200,200,200,144,0].</summary>
        [Test]
        public void Retreat_DrainsTheFarthestTilesFirst()
        {
            FloodField field = Field(5, 1);
            for (int i = 0; i < 5; i++)
            {
                field.Drops[i] = 200;
                field.Team[i] = 0;
            }
            int removed = FloodRules.Retreat(field, 0, 256, new List<int> { 0 });
            Assert.That(removed, Is.EqualTo(256));
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 200, 200, 200, 144, 0 }));
            Assert.That(field.Team[4], Is.EqualTo(FloodField.NoTeam));
        }

        /// <summary>9b. Retreat with no seeds drains by descending index; it never touches another team's water.</summary>
        [Test]
        public void Retreat_WithoutSeeds_UsesDescendingIndex_AndSkipsOtherTeams()
        {
            FloodField field = Field(4, 1);
            for (int i = 0; i < 4; i++)
            {
                field.Drops[i] = 100;
                field.Team[i] = i == 3 ? 1 : 0;
            }
            Assert.That(FloodRules.Retreat(field, 0, 150, new List<int>()), Is.EqualTo(150));
            Assert.That(DropsOf(field), Is.EqualTo(new[] { 100, 50, 0, 100 }));
        }

        /// <summary>9c. Low tide in a match: the team holds 10240 drops at high tide (tick 2100) and is drained back to the low-tide volume 5120 by tick 4200.</summary>
        [Test]
        public void LowTide_PullsTheWaterBack()
        {
            Simulation sim = Build(FlatMap(16, 16), "40");
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 2100);
            Assert.That(Held(sim, 0), Is.EqualTo(40 * 256));
            StatusTestWorld.StepThrough(sim, 4200);
            Assert.That(Held(sim, 0), Is.EqualTo(20 * 256));
        }

        /// <summary>9d. A team with no source and the shipped min_retreat_per_second 0.5 drains 128 drops per update.</summary>
        [Test]
        public void NoSource_DrainsAtTheMinimumRetreat()
        {
            Simulation sim = Build(FlatMap(8, 8));
            UniformWater(sim, 0, 64, 0);
            StatusTestWorld.StepThrough(sim, 0);
            Assert.That(Held(sim, 0), Is.EqualTo(64 * 64 - 128));
        }

        // ------------------------------------------------------------------ 10: resource gap

        /// <summary>10a. NoFloodMask on 16x1 with an anchor at x = 12 and gap 4 is true for x = 8 to 15 only.</summary>
        [Test]
        public void NoFloodMask_MarksTheGap()
        {
            bool[] mask = FloodRules.NoFloodMask(16, 1, new List<int> { 12 }, 4);
            for (int x = 0; x < 16; x++)
            {
                Assert.That(mask[x], Is.EqualTo(x >= 8), "x = " + x);
            }
        }

        /// <summary>10b. In a corridor with an enemy drop-off at x = 12 and a source at x = 0, after 60 updates x = 7 holds water and x = 8 to 15 hold none; the drop-off's tile stays dry.</summary>
        [Test]
        public void Flood_StopsFourTilesShortOfAnEnemyDropOff()
        {
            Simulation sim = Build(FlatMap(16, 8));
            Corridor(sim, 0);
            StatusTestWorld.Spawn(sim, 0, 1, SourceId, P0, StatusTestWorld.Centre(0, 0));
            StatusTestWorld.Spawn(sim, 0, 2, DropOffId, P1, StatusTestWorld.Centre(12, 0));
            StatusTestWorld.StepThrough(sim, 60 * 20);
            Assert.That(DropsAt(sim, 7, 0), Is.GreaterThan(0));
            for (int x = 8; x < 16; x++)
            {
                Assert.That(DropsAt(sim, x, 0), Is.EqualTo(0), "x = " + x);
            }
        }

        /// <summary>10c. Water already inside the gap when an enemy drop-off appears is removed and reported as resource_gap; allies' drop-offs do not count.</summary>
        [Test]
        public void Gap_RemovesWaterAndReports_OnlyForEnemies()
        {
            Simulation sim = Build(FlatMap(16, 16), tuning: NoRetreatTuning());
            UniformWater(sim, 0, 64, 0);
            StatusTestWorld.Spawn(sim, 0, 1, DropOffId, P1, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 0);
            // Tiles within 4 of (8,8): x and y in 4 to 12, 81 tiles; the footprint tile (8,8) is reported as displaced_off, the other 80 as resource_gap.
            int dry = 0;
            for (int i = 0; i < sim.Terrain.TileCount; i++)
            {
                int x = sim.Terrain.XOf(i);
                int y = sim.Terrain.YOf(i);
                bool inside = x >= 4 && x <= 12 && y >= 4 && y <= 12;
                Assert.That(DropsAt(sim, x, y) == 0, Is.EqualTo(inside), "tile " + x + "," + y);
                dry += inside ? 1 : 0;
            }
            Assert.That(dry, Is.EqualTo(81));
            List<FloodWaterRemovedEvent> removed = StatusTestWorld.EventsOf<FloodWaterRemovedEvent>(sim);
            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(removed[0].Reason, Is.EqualTo("resource_gap"));
            Assert.That(removed[0].Drops, Is.EqualTo(80 * 64));
            Assert.That(removed[1].Reason, Is.EqualTo("displaced_off"));
            Assert.That(removed[1].Drops, Is.EqualTo(64));

            // The same drop-off owned by the water player's own team does not restrict its water.
            Simulation ally = Build(FlatMap(16, 16), tuning: NoRetreatTuning());
            UniformWater(ally, 0, 64, 0);
            StatusTestWorld.Spawn(ally, 0, 1, DropOffId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(ally, 0);
            Assert.That(DropsAt(ally, 6, 6), Is.EqualTo(64));
        }

        // ------------------------------------------------------------------ 11: speeds and healing

        /// <summary>Where a unit moving east from (2.5, 8.5) to (12.5, 8.5) is after tick 0, given uniform water of <paramref name="drops"/> owned by team <paramref name="waterTeam"/>.</summary>
        private static Fix64 FirstTickStep(string unit, PlayerId owner, int drops, byte waterTeam)
        {
            Simulation sim = Build(FlatMap(16, 16), tuning: NoRetreatTuning());
            UniformWater(sim, 0, drops, waterTeam);
            StatusTestWorld.Spawn(sim, 0, 1, unit, owner, StatusTestWorld.Centre(2, 8));
            StatusTestWorld.Move(sim, 0, 2, 1, StatusTestWorld.Centre(12, 8));
            StatusTestWorld.StepThrough(sim, 0);
            return StatusTestWorld.UnitOf(sim, 1).Position.X - (Fix64.FromInt(2) + Fix64.Half);
        }

        /// <summary>11a. Enemy ground unit (speed 2.5) on deep water moves 0.0625 tiles per tick; an own-team ground unit moves 0.125.</summary>
        [Test]
        public void GroundSpeed_EnemyWaterSlows_OwnWaterDoesNot()
        {
            Assert.That(FirstTickStep(WalkerId, P1, 512, 0), Is.EqualTo(Fix64.Parse("0.0625")));
            Assert.That(FirstTickStep(WalkerId, P0, 512, 0), Is.EqualTo(Fix64.Parse("0.125")));
        }

        /// <summary>11b. A water-layer unit of speed 3 on shallow water moves PerSecondToPerTick(3 x 1.15) per tick (about 0.1725), on either team's water.</summary>
        [Test]
        public void WaterUnitSpeed_ShallowWaterIsFaster()
        {
            Fix64 expected = new MatchClock(20).PerSecondToPerTick(Fix64.FromInt(3) * Fix64.Parse("1.15"));
            Assert.That(FirstTickStep(SwimmerId, P0, 64, 0), Is.EqualTo(expected));
            Assert.That(FirstTickStep(SwimmerId, P0, 64, 1), Is.EqualTo(expected));
        }

        /// <summary>Health of a water unit at 50 of 100 after the update ticks up to <paramref name="lastTick"/>, standing on uniform shallow water of <paramref name="waterTeam"/>; the unit belongs to P0.</summary>
        private static Fix64 HealthAfter(int lastTick, byte waterTeam, string unit = SwimmerId)
        {
            Simulation sim = Build(FlatMap(16, 16), tuning: NoRetreatTuning());
            UniformWater(sim, 0, 64, waterTeam);
            StatusTestWorld.Spawn(sim, 0, 1, unit, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.Run(sim, 0, 2, s => StatusTestWorld.UnitOf(s, 1).SetHealth(Fix64.FromInt(50)));
            StatusTestWorld.StepThrough(sim, lastTick);
            return StatusTestWorld.UnitOf(sim, 1).Health;
        }

        /// <summary>11c. A water unit at 50/100 on its own team's shallow water reads 52 after one update and 56 after three; on enemy water it stays 50; a ground unit does not heal.</summary>
        [Test]
        public void Healing_OnOwnWaterOnly()
        {
            Assert.That(HealthAfter(0, 0), Is.EqualTo(Fix64.FromInt(52)));
            Assert.That(HealthAfter(40, 0), Is.EqualTo(Fix64.FromInt(56)));
            Assert.That(HealthAfter(40, 1), Is.EqualTo(Fix64.FromInt(50)));
            Assert.That(HealthAfter(40, 0, WalkerId), Is.EqualTo(Fix64.FromInt(50)));
        }

        /// <summary>11d. Healing stops at full health and the shipped heal rate gives 2 health per update.</summary>
        [Test]
        public void Healing_StopsAtMaxHealth()
        {
            Assert.That(HealthAfter(20 * 40, 0), Is.EqualTo(Fix64.FromInt(100)));
            Assert.That(Mechanic(Build(FlatMap(8, 8))).HealPerUpdate, Is.EqualTo(Fix64.FromInt(2)));
        }

        // ------------------------------------------------------------------ 12: wave

        /// <summary>A match with a caster (unit 1, P0) at (2.5, 5.5) and an enemy walker (unit 2, P1) at (5.5, 5.5) on the 64x64 flat map.</summary>
        private static Simulation WaveMatch(Tuning? tuning = null)
        {
            Simulation sim = Build(TestWorld.FlatMap(), tuning: tuning);
            StatusTestWorld.Spawn(sim, 0, 0, CasterId, P0, StatusTestWorld.Centre(2, 5));
            StatusTestWorld.Spawn(sim, 0, 1, WalkerId, P1, StatusTestWorld.Centre(5, 5));
            return sim;
        }

        /// <summary>12a. Wave: tiles x 3 to 8, y 4 to 6 gain 256 drops each, the enemy at (5.5, 5.5) ends at (7.5, 5.5) with one UnitPushedEvent, and the state differs from a run without the wave.</summary>
        [Test]
        public void Wave_FloodsTheStripAndPushesEnemies()
        {
            Simulation sim = WaveMatch();
            sim.Submit(new CastWaveCommand(5, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            StatusTestWorld.StepThrough(sim, 5);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool inStrip = x >= 3 && x <= 8 && y >= 4 && y <= 6;
                    Assert.That(DropsAt(sim, x, y), Is.EqualTo(inStrip ? 256 : 0), "tile " + x + "," + y);
                    Assert.That(sim.Terrain.At(x, y).WaterTeam, Is.EqualTo(inStrip ? new TeamId(0) : TeamId.None));
                }
            }
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Position, Is.EqualTo(StatusTestWorld.Centre(7, 5)));
            List<UnitPushedEvent> pushed = StatusTestWorld.EventsOf<UnitPushedEvent>(sim);
            Assert.That(pushed.Count, Is.EqualTo(1));
            Assert.That(pushed[0].Unit, Is.EqualTo(new EntityId(2)));
            Assert.That(pushed[0].From, Is.EqualTo(StatusTestWorld.Centre(5, 5)));
            Assert.That(pushed[0].To, Is.EqualTo(StatusTestWorld.Centre(7, 5)));
            List<WaveCastEvent> cast = StatusTestWorld.EventsOf<WaveCastEvent>(sim);
            Assert.That(cast.Count, Is.EqualTo(1));
            Assert.That(cast[0].DirX, Is.EqualTo((sbyte)1));
            Assert.That(cast[0].DirY, Is.EqualTo((sbyte)0));
            Assert.That(cast[0].TilesFlooded, Is.EqualTo(18));
            Assert.That(StatusTestWorld.UnitOf(sim, 2).LastSelfMoveTick, Is.EqualTo(-1), "a push is not a self-move");

            Simulation without = WaveMatch();
            StatusTestWorld.StepThrough(without, 5);
            Assert.That(sim.ComputeStateHash(), Is.Not.EqualTo(without.ComputeStateHash()));
        }

        /// <summary>12b. The cooldown is 600 ticks: a second cast at tick 604 is rejected on_cooldown and one at tick 605 is accepted.</summary>
        [Test]
        public void Wave_CooldownIsSixHundredTicks()
        {
            Simulation sim = WaveMatch();
            sim.Submit(new CastWaveCommand(5, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            sim.Submit(new CastWaveCommand(604, P0, 1, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            sim.Submit(new CastWaveCommand(605, P0, 2, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            StatusTestWorld.StepThrough(sim, 5);
            Assert.That(Mechanic(sim).TryGetNextWaveTick(new EntityId(1), out int next), Is.True);
            Assert.That(next, Is.EqualTo(605));
            StatusTestWorld.StepThrough(sim, 604);
            List<CommandRejectedEvent> rejected = StatusTestWorld.EventsOf<CommandRejectedEvent>(sim);
            Assert.That(rejected.Count, Is.EqualTo(1));
            Assert.That(rejected[0].Reason, Is.EqualTo("on_cooldown"));
            StatusTestWorld.StepThrough(sim, 605);
            Assert.That(StatusTestWorld.EventsOf<CommandRejectedEvent>(sim).Count, Is.EqualTo(0));
            Assert.That(StatusTestWorld.EventsOf<WaveCastEvent>(sim).Count, Is.EqualTo(1));
        }

        /// <summary>
        /// The wave floods under an enemy HQ but does not shove it while it is Rooted (movement-locked); the same HQ left Uprooted is
        /// shoved like any unit, and an ordinary enemy walker beside it is pushed either way.
        /// </summary>
        [Test]
        public void Wave_DoesNotPushARootedHq()
        {
            // Position of the HQ after the wave, for an HQ forced Rooted or left in its spawn state (Uprooted: no resource field on the flat map).
            FixVec2 AfterWave(bool rooted)
            {
                Simulation sim = WaveMatch();
                // Enemy HQ inside the strip, on the row below the walker at (5.5, 5.5); it is unit 3.
                StatusTestWorld.Spawn(sim, 0, 2, TestWorld.HqId, P1, StatusTestWorld.Centre(5, 4));
                if (rooted)
                {
                    StatusTestWorld.Run(sim, 1, 3, s => StatusTestWorld.UnitOf(s, 3).SetHqState(HqState.Rooted, 0));
                }
                sim.Submit(new CastWaveCommand(5, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
                StatusTestWorld.StepThrough(sim, 5);
                Assert.That(StatusTestWorld.UnitOf(sim, 3).HqState, Is.EqualTo(rooted ? HqState.Rooted : HqState.Uprooted));
                Assert.That(StatusTestWorld.UnitOf(sim, 2).Position, Is.EqualTo(StatusTestWorld.Centre(7, 5)), "the walker is pushed either way");
                return StatusTestWorld.UnitOf(sim, 3).Position;
            }
            Assert.That(AfterWave(true), Is.EqualTo(StatusTestWorld.Centre(5, 4)), "a Rooted HQ stays put");
            Assert.That(AfterWave(false), Is.EqualTo(StatusTestWorld.Centre(7, 4)), "an Uprooted HQ is shoved two tiles east");
        }

        /// <summary>Rejection reason of one cast command run alone on tick 1 of a fresh wave match, or "accepted". (Tick 0 would apply the player's command before the script's spawns: commands run in (issuer, sequence) order and the script issuer sorts last.)</summary>
        private static string CastResult(PlayerId issuer, uint caster, FixVec2 target)
        {
            Simulation sim = WaveMatch();
            sim.Submit(new CastWaveCommand(1, issuer, 5, new EntityId(caster), target));
            StatusTestWorld.StepThrough(sim, 1);
            List<CommandRejectedEvent> rejected = StatusTestWorld.EventsOf<CommandRejectedEvent>(sim);
            return rejected.Count == 0 ? "accepted" : rejected[0].Reason;
        }

        /// <summary>12c. Rejections: unknown_unit, not_owner, not_wave_caster, out_of_bounds, no_direction; the script issuer may cast for anyone.</summary>
        [Test]
        public void Wave_RejectionsInOrder()
        {
            FixVec2 east = StatusTestWorld.Centre(10, 5);
            Assert.That(CastResult(P0, 99, east), Is.EqualTo("unknown_unit"));
            Assert.That(CastResult(P1, 1, east), Is.EqualTo("not_owner"));
            Assert.That(CastResult(P1, 2, east), Is.EqualTo("not_wave_caster"), "the walker cannot cast");
            Assert.That(CastResult(P0, 1, new FixVec2(Fix64.FromInt(20000), Fix64.Zero)), Is.EqualTo("out_of_bounds"));
            Assert.That(CastResult(P0, 1, new FixVec2(Fix64.FromInt(2) + Fix64.Parse("0.9"), Fix64.FromInt(5) + Fix64.Parse("0.1"))), Is.EqualTo("no_direction"));
            Assert.That(CastResult(PlayerId.None, 1, east), Is.EqualTo("accepted"));
            Assert.That(CastResult(P0, 1, east), Is.EqualTo("accepted"));
        }

        /// <summary>12d. Direction rule: a wide-angle target gives a diagonal, a steep one a vertical wave; a diagonal wave floods the rotated strip.</summary>
        [Test]
        public void Wave_DirectionFromTheTarget()
        {
            (FixVec2 Target, int DirX, int DirY)[] cases =
            {
                (StatusTestWorld.Centre(10, 5), 1, 0),
                (StatusTestWorld.Centre(2, 20), 0, 1),
                (StatusTestWorld.Centre(10, 10), 1, 1),
                (StatusTestWorld.Centre(0, 2), -1, -1),
            };
            foreach ((FixVec2 target, int dirX, int dirY) in cases)
            {
                Simulation sim = WaveMatch();
                sim.Submit(new CastWaveCommand(1, P0, 0, new EntityId(1), target));
                StatusTestWorld.StepThrough(sim, 1);
                List<WaveCastEvent> cast = StatusTestWorld.EventsOf<WaveCastEvent>(sim);
                Assert.That(cast.Count, Is.EqualTo(1), target.ToString());
                Assert.That((int)cast[0].DirX, Is.EqualTo(dirX), target.ToString());
                Assert.That((int)cast[0].DirY, Is.EqualTo(dirY), target.ToString());
            }
        }

        /// <summary>12e. A wave never fills walls or footprints, never overwrites enemy water, and stops pushing a unit at a wall.</summary>
        [Test]
        public void Wave_RespectsWallsEnemyWaterAndBlockedPushes()
        {
            Simulation sim = WaveMatch(NoRetreatTuning());
            // The terrain is prepared on tick 1, after the tick-0 flood update, and the wave follows on tick 2 (before the next update on tick 20).
            StatusTestWorld.Run(sim, 1, 5, s =>
            {
                s.Terrain.AddBlockers(s, s.Terrain.IndexOf(4, 4), TileBlockers.Wall);
                s.Terrain.AddBlockers(s, s.Terrain.IndexOf(6, 5), TileBlockers.Wall);
                s.Terrain.SetWater(s, s.Terrain.IndexOf(7, 6), FloodMath.ToDepth(100), new TeamId(1));
            });
            sim.Submit(new CastWaveCommand(2, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            StatusTestWorld.StepThrough(sim, 2);
            Assert.That(DropsAt(sim, 4, 4), Is.EqualTo(0), "wall");
            Assert.That(DropsAt(sim, 6, 5), Is.EqualTo(0), "wall");
            Assert.That(DropsAt(sim, 7, 6), Is.EqualTo(100), "enemy water stays");
            Assert.That(sim.Terrain.At(7, 6).WaterTeam, Is.EqualTo(new TeamId(1)));
            Assert.That(DropsAt(sim, 5, 5), Is.EqualTo(256));
            Assert.That(StatusTestWorld.EventsOf<WaveCastEvent>(sim)[0].TilesFlooded, Is.EqualTo(18 - 3));
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Position, Is.EqualTo(StatusTestWorld.Centre(5, 5)), "the wall at (6,5) stops the push");
            Assert.That(StatusTestWorld.EventsOf<UnitPushedEvent>(sim).Count, Is.EqualTo(0));
        }

        /// <summary>12f. Own units and flyers are not pushed.</summary>
        [Test]
        public void Wave_PushesOnlyEnemyNonAirUnits()
        {
            Simulation sim = WaveMatch();
            StatusTestWorld.Spawn(sim, 0, 2, WalkerId, P0, StatusTestWorld.Centre(4, 5));
            StatusTestWorld.Spawn(sim, 0, 3, TestWorld.FlyerId, P1, StatusTestWorld.Centre(4, 6));
            sim.Submit(new CastWaveCommand(1, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            StatusTestWorld.StepThrough(sim, 1);
            Assert.That(StatusTestWorld.UnitOf(sim, 3).Position, Is.EqualTo(StatusTestWorld.Centre(4, 5)), "own walker");
            Assert.That(StatusTestWorld.UnitOf(sim, 4).Position, Is.EqualTo(StatusTestWorld.Centre(4, 6)), "enemy flyer");
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Position, Is.EqualTo(StatusTestWorld.Centre(7, 5)));
        }

        // ------------------------------------------------------------------ state, determinism, sources

        /// <summary>The hash covers the wave cooldown: removing the caster prunes the entry and the count returns to 0.</summary>
        [Test]
        public void CooldownEntries_AreHashed_AndPruned()
        {
            Simulation sim = WaveMatch();
            sim.Submit(new CastWaveCommand(1, P0, 0, new EntityId(1), StatusTestWorld.Centre(10, 5)));
            StatusTestWorld.StepThrough(sim, 1);
            Assert.That(Mechanic(sim).CooldownEntryCount, Is.EqualTo(1));
            StatusTestWorld.Run(sim, 2, 0, s => s.RemoveUnit(new EntityId(1), "test"));
            StatusTestWorld.StepThrough(sim, 3);
            Assert.That(Mechanic(sim).CooldownEntryCount, Is.EqualTo(0));
        }

        /// <summary>Two identical runs with a source, a wave and an enemy drop-off give the same hash at every report.</summary>
        [Test]
        public void SameRun_GivesTheSameHashes()
        {
            ulong[] finals = new ulong[2];
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = Build(FlatMap(32, 32));
                StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(16, 16));
                StatusTestWorld.Spawn(sim, 0, 1, CasterId, P0, StatusTestWorld.Centre(10, 10));
                StatusTestWorld.Spawn(sim, 0, 2, DropOffId, P1, StatusTestWorld.Centre(28, 28));
                sim.Submit(new CastWaveCommand(100, P0, 0, new EntityId(2), StatusTestWorld.Centre(20, 12)));
                sim.Step(900);
                finals[run] = sim.ComputeStateHash();
            }
            Assert.That(finals[0], Is.EqualTo(finals[1]));
        }

        /// <summary>A Rooted HQ with flood output is a source; the same HQ Uprooted (no resource field in reach) is not.</summary>
        [Test]
        public void Hq_IsASourceOnlyWhileRooted()
        {
            MapDefinition withField = new MapDefinition("flood_field", "Flood Field", 16, 16,
                new List<FixVec2> { FixVec2.FromInts(1, 1), FixVec2.FromInts(14, 14) }, null, null,
                new List<ResourceField> { new ResourceField(10, 8, ResourceKind.Common, 1000) }, null);
            Simulation rooted = Build(withField);
            StatusTestWorld.Spawn(rooted, 0, 0, FloodHqId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(rooted, 20);
            Assert.That(StatusTestWorld.UnitOf(rooted, 1).HqState, Is.EqualTo(HqState.Rooted));
            Assert.That(Held(rooted, 0), Is.GreaterThan(0));

            Simulation uprooted = Build(FlatMap(16, 16));
            StatusTestWorld.Spawn(uprooted, 0, 0, FloodHqId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(uprooted, 20);
            Assert.That(StatusTestWorld.UnitOf(uprooted, 1).HqState, Is.EqualTo(HqState.Uprooted));
            Assert.That(Held(uprooted, 0), Is.EqualTo(0));
        }

        /// <summary>A source whose owner's faction does not use the flooding mechanic releases nothing (flood-capable factions are found by mechanic_id).</summary>
        [Test]
        public void Source_OfAFactionWithoutTheMechanic_ReleasesNothing()
        {
            Simulation sim = Build(FlatMap(16, 16));
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P1, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.StepThrough(sim, 100);
            Assert.That(Held(sim, 1), Is.EqualTo(0));
            Assert.That(Held(sim, 0), Is.EqualTo(0));
        }

        /// <summary>A source under construction releases nothing until it is complete.</summary>
        [Test]
        public void Source_UnderConstruction_ReleasesNothing()
        {
            Simulation sim = Build(FlatMap(16, 16));
            StatusTestWorld.Spawn(sim, 0, 0, SourceId, P0, StatusTestWorld.Centre(8, 8));
            StatusTestWorld.Run(sim, 0, 1, s => StatusTestWorld.UnitOf(s, 1).SetConstructionTicks(100));
            StatusTestWorld.StepThrough(sim, 60);
            Assert.That(Held(sim, 0), Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ data

        /// <summary>The shipped Tidepool and Grand Shell are flood sources; the Grasper is not; the validation rejects a source with no capacity.</summary>
        [Test]
        public void Data_FloodKeysAreLoaded_AndValidated()
        {
            DataSet data = TestData.LoadAll();
            UnitFloodDefinition tidepool = data.Content.Units["water.tidepool"].Flood;
            Assert.That(tidepool.IsSource, Is.True);
            Assert.That(tidepool.OutputPerSecond, Is.EqualTo(Fix64.One));
            Assert.That(tidepool.Capacity, Is.EqualTo(Fix64.FromInt(40)));
            Assert.That(data.Content.Units["water.grand_shell"].Flood.IsSource, Is.True);
            Assert.That(data.Content.Units["water.grasper"].Flood, Is.SameAs(UnitFloodDefinition.None));
            Assert.That(data.Tuning.Flood.WaveWidthTiles, Is.EqualTo(3));

            DataValidationException noCapacity = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/water/tidepool.json", "\"flood_source_capacity\": 40", "\"flood_source_capacity\": 0")));
            Assert.That(noCapacity.Message, Does.Contain("units/water/tidepool.json"));
            Assert.That(noCapacity.Message, Does.Contain("flood_source_output_per_second"));
            DataValidationException tooMuch = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit("units/water/tidepool.json", "\"flood_source_output_per_second\": 1", "\"flood_source_output_per_second\": 65")));
            Assert.That(tooMuch.Message, Does.Contain("flood_source_output_per_second"));
        }

        /// <summary>The tuning rejects an even wave width, depths that are not a multiple of 1/256, a layer depth above max_water_depth and out-of-range values; every error names the file and key.</summary>
        [Test]
        public void Tuning_RangesAreValidated()
        {
            (string Old, string New, string Key)[] cases =
            {
                ("\"wave_width_tiles\": 3", "\"wave_width_tiles\": 4", "wave_width_tiles"),
                ("\"wave_water_depth\": 1", "\"wave_water_depth\": 1.001", "wave_water_depth"),
                ("\"depth_units_per_height_layer\": 2", "\"depth_units_per_height_layer\": 2.001", "depth_units_per_height_layer"),
                ("\"depth_units_per_height_layer\": 2", "\"depth_units_per_height_layer\": 5", "depth_units_per_height_layer"),
                ("\"flow_passes_per_update\": 4", "\"flow_passes_per_update\": 17", "flow_passes_per_update"),
                ("\"flood_update_interval_seconds\": 1", "\"flood_update_interval_seconds\": 0.1", "flood_update_interval_seconds"),
                ("\"low_tide_volume_fraction\": 0.5", "\"low_tide_volume_fraction\": 1.5", "low_tide_volume_fraction"),
            };
            foreach ((string oldText, string newText, string key) in cases)
            {
                DataValidationException e = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(FloodTuning.FilePath, oldText, newText)));
                Assert.That(e.Message, Does.Contain(FloodTuning.FilePath), key);
                Assert.That(e.Message, Does.Contain(key), key);
            }
            DataValidationException depth = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                TestData.WithEdit(TerrainTuning.FilePath, "\"max_water_depth\": 4", "\"max_water_depth\": 4.001")));
            Assert.That(depth.Message, Does.Contain("max_water_depth"));
        }

        /// <summary>The cast_wave script command parses, and rejects unknown keys.</summary>
        [Test]
        public void Script_ParsesCastWave()
        {
            string json = "{ \"$schema_version\": 1, \"seed\": 1, \"ticks\": 10, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"water\", \"name\": \"A\" } ], "
                + "\"commands\": [ { \"tick\": 3, \"issuer\": 0, \"sequence\": 0, \"type\": \"cast_wave\", \"unit\": 4, \"x\": \"10.5\", \"y\": 7 } ] }";
            CommandScript script = CommandScript.Parse(json, "scripts/test.json", BuiltInMechanics.ScriptParsers);
            Assert.That(script.Commands.Count, Is.EqualTo(1));
            Assert.That(script.Commands[0], Is.InstanceOf<CastWaveCommand>());
            CastWaveCommand command = (CastWaveCommand)script.Commands[0];
            Assert.That(command.Caster, Is.EqualTo(new EntityId(4)));
            Assert.That(command.Target, Is.EqualTo(new FixVec2(Fix64.Parse("10.5"), Fix64.FromInt(7))));
            Assert.That(command.Tick, Is.EqualTo(3));
        }

        /// <summary>
        /// Without the built-in script parsers cast_wave is an unknown type and the error lists only the core kinds; with them the
        /// error for a truly unknown type lists cast_wave too, and a bad cast_wave key is rejected naming the file.
        /// </summary>
        [Test]
        public void Script_ExtraParsers_AreListedInTheUnknownTypeError()
        {
            string json = "{ \"$schema_version\": 1, \"seed\": 1, \"ticks\": 10, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"water\", \"name\": \"A\" } ], "
                + "\"commands\": [ { \"tick\": 3, \"issuer\": 0, \"sequence\": 0, \"type\": \"cast_wave\", \"unit\": 4, \"x\": \"10.5\", \"y\": 7 } ] }";
            DataValidationException without = Expect.Throws<DataValidationException>(() => CommandScript.Parse(json, "scripts/test.json"));
            Assert.That(without.Message, Does.Contain("scripts/test.json"));
            Assert.That(without.Message, Does.Not.Contain("\"cast_wave\" "), "the core list does not name the wave");
            DataValidationException unknown = Expect.Throws<DataValidationException>(() =>
                CommandScript.Parse(json.Replace("cast_wave", "no_such_command"), "scripts/test.json", BuiltInMechanics.ScriptParsers));
            Assert.That(unknown.Message, Does.Contain("\"cast_wave\""));
            Assert.That(unknown.Message, Does.Contain("\"no_such_command\""));
            DataValidationException badKey = Expect.Throws<DataValidationException>(() =>
                CommandScript.Parse(json.Replace("\"unit\"", "\"caster\""), "scripts/test.json", BuiltInMechanics.ScriptParsers));
            Assert.That(badKey.Message, Does.Contain("scripts/test.json"));
        }

        // ------------------------------------------------------------------ the shipped smoke script

        /// <summary>
        /// The shipped flood_smoke.json on the real data: Water's Rooted Grand Shell and Tidepool flood the south-east corner, the water
        /// stays out of the four-tile gap around the Skai Great Aerie and the fields it claims, the Grasper stands in water, and
        /// two runs give the same hash.
        /// </summary>
        [Test]
        public void SmokeScript_FloodsAndRespectsTheGap_Deterministically()
        {
            DataSet data = TestData.LoadAll();
            // Hash of each of the two runs, and the simulation of the last one for inspection.
            ulong[] hashes = new ulong[2];
            Simulation? last = null;
            for (int run = 0; run < 2; run++)
            {
                string path = System.IO.Path.Combine(TestData.FindDataDirectory(), "scripts", "flood_smoke.json");
                CommandScript script = CommandScript.Parse(System.IO.File.ReadAllText(path), "scripts/flood_smoke.json", BuiltInMechanics.ScriptParsers);
                SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(script.Seed),
                    data.Content, script.Players, BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId));
                Simulation sim = new Simulation(config);
                sim.Start();
                foreach (Command command in script.Commands)
                {
                    sim.Submit(command);
                }
                sim.Step(script.Ticks);
                hashes[run] = sim.ComputeStateHash();
                last = sim;
            }
            Assert.That(hashes[0], Is.EqualTo(hashes[1]));
            Simulation final = last!;
            Assert.That(Held(final, 1), Is.GreaterThan(1000), "the two sources have flooded a good area");
            Assert.That(Held(final, 0), Is.EqualTo(0));
            // Tiles within 4 (Chebyshev) of the Aerie at (50, 60) and of the two fields it claims, (53, 59) and (59, 53), stay dry.
            (int X, int Y)[] anchors = { (50, 60), (53, 59), (59, 53) };
            int wet = 0;
            for (int i = 0; i < final.Terrain.TileCount; i++)
            {
                int x = final.Terrain.XOf(i);
                int y = final.Terrain.YOf(i);
                if (final.Terrain[i].WaterDepth > Fix64.Zero)
                {
                    wet++;
                }
                foreach ((int ax, int ay) in anchors)
                {
                    if (System.Math.Max(System.Math.Abs(x - ax), System.Math.Abs(y - ay)) <= 4)
                    {
                        Assert.That(final.Terrain[i].WaterDepth, Is.EqualTo(Fix64.Zero), "gap tile " + x + "," + y);
                    }
                }
            }
            Assert.That(wet, Is.GreaterThan(20));
            Assert.That(StatusTestWorld.UnitOf(final, 5).Definition.Id, Is.EqualTo("water.grasper"));
        }
    }
}
