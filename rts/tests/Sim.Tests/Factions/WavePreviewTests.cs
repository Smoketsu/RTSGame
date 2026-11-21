// rts/tests/Sim.Tests/Factions/WavePreviewTests.cs
// Acceptance tests IS-6 to IS-9 of package I2 of rts/docs/specs/interface_support.md: FloodingMechanic.PreviewWave (Unity's wave strip preview) shares its
// direction, strip and flood rule with cast_wave, so it predicts the cast, shows hidden tiles only as covered, and changes nothing. This is the second
// part of the FloodingTests class (see FloodingTests.cs) so it can use that fixture: P0 water faction on team 0, P1 test faction on team 1, flat 64 by 64 map,
// caster "flood.caster" (sight 8 tiles). Wave length 6 and width 3 unless a test says otherwise. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions.Water;
using Rts.Sim.Tests.Statuses;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Factions
{
    /// <summary>Wave preview tests (part of FloodingTests).</summary>
    public sealed partial class FloodingTests
    {
        /// <summary>A match with the caster (unit 1, P0) at the centre of tile (30, 30), spawned and stepped through tick 1; the next tick to run is 2.</summary>
        private static Simulation PreviewMatch(Tuning? tuning = null)
        {
            Simulation sim = Build(TestWorld.FlatMap(), tuning: tuning);
            StatusTestWorld.Spawn(sim, 0, 0, CasterId, P0, StatusTestWorld.Centre(30, 30));
            StatusTestWorld.StepThrough(sim, 1);
            return sim;
        }

        /// <summary>Water depth of every tile, in tile index order. Unit: tiles (fixed-point).</summary>
        private static Fix64[] Depths(Simulation sim)
        {
            // Depth per tile index.
            Fix64[] depths = new Fix64[sim.Terrain.TileCount];
            for (int i = 0; i < depths.Length; i++)
            {
                depths[i] = sim.Terrain[i].WaterDepth;
            }
            return depths;
        }

        /// <summary>IS-6: with every tile visible, the preview's floods are the tiles whose water rose in the cast that follows, and the area has the strip's size; four directions plus a diagonal.</summary>
        [Test]
        public void Is06_PreviewAgreesWithTheCast()
        {
            // Targets east, west, south, north and one diagonal from tile (30, 30).
            FixVec2[] targets = { StatusTestWorld.Centre(40, 30), StatusTestWorld.Centre(20, 30), StatusTestWorld.Centre(30, 40), StatusTestWorld.Centre(30, 20), StatusTestWorld.Centre(40, 40) };
            foreach (FixVec2 target in targets)
            {
                Simulation sim = PreviewMatch();
                // A P0 spotter 4 tiles from the caster towards the target, so the whole strip is in sight (the caster alone sees 8 tiles, short of a diagonal strip's far corners).
                StatusTestWorld.Spawn(sim, sim.Clock.Tick, 1, WalkerId, P0, StatusTestWorld.Centre(30 + 4 * (target.X - StatusTestWorld.Centre(30, 30).X).Sign(), 30 + 4 * (target.Y - StatusTestWorld.Centre(30, 30).Y).Sign()));
                sim.Step();
                FloodingMechanic mechanic = Mechanic(sim);
                List<int> area = new List<int>();
                List<int> floods = new List<int>();
                Assert.That(mechanic.PreviewWave(sim, StatusTestWorld.UnitOf(sim, 1), target, area, floods), Is.Null);
                // Strip size away from the map edges: wave_length_tiles * (2 * ((wave_width_tiles - 1) / 2) + 1).
                Assert.That(area.Count, Is.EqualTo(sim.Tuning.Flood.WaveLengthTiles * (2 * ((sim.Tuning.Flood.WaveWidthTiles - 1) / 2) + 1)), target.ToString());
                Assert.That(area, Is.Ordered);
                TeamId team = sim.Players.TeamOf(P0);
                foreach (int tile in area)
                {
                    Assert.That(sim.Vision.IsTileVisible(team, sim.Terrain.XOf(tile), sim.Terrain.YOf(tile)), Is.True, "every tile visible in this setup");
                }
                Fix64[] before = Depths(sim);
                sim.Submit(new CastWaveCommand(sim.Clock.Tick, P0, 0, new EntityId(1), target));
                sim.Step();
                Fix64[] after = Depths(sim);
                List<int> rose = new List<int>();
                for (int i = 0; i < after.Length; i++)
                {
                    if (after[i] > before[i])
                    {
                        rose.Add(i);
                    }
                }
                Assert.That(floods, Is.EqualTo(rose), target.ToString());
                Assert.That(StatusTestWorld.EventsOf<WaveCastEvent>(sim).Single().TilesFlooded, Is.EqualTo(floods.Count));
            }
        }

        /// <summary>IS-7: the reasons, with the lists cleared on every return; on cooldown the preview still answers.</summary>
        [Test]
        public void Is07_Reasons()
        {
            Simulation sim = PreviewMatch();
            StatusTestWorld.Spawn(sim, sim.Clock.Tick, 1, WalkerId, P1, StatusTestWorld.Centre(34, 30));
            sim.Step();
            FloodingMechanic mechanic = Mechanic(sim);
            Unit caster = StatusTestWorld.UnitOf(sim, 1);
            List<int> area = new List<int> { 1, 2, 3 };
            List<int> floods = new List<int> { 4, 5 };
            Assert.That(mechanic.PreviewWave(sim, caster, caster.Position + FixVec2.Zero, area, floods), Is.EqualTo("no_direction"));
            Assert.That(area, Is.Empty);
            Assert.That(floods, Is.Empty);
            area.Add(1);
            floods.Add(2);
            Assert.That(mechanic.PreviewWave(sim, caster, new FixVec2(Fix64.FromInt(20000), Fix64.Zero), area, floods), Is.EqualTo("out_of_bounds"));
            Assert.That(area, Is.Empty);
            Assert.That(floods, Is.Empty);
            area.Add(1);
            floods.Add(2);
            Assert.That(mechanic.PreviewWave(sim, StatusTestWorld.UnitOf(sim, 2), StatusTestWorld.Centre(40, 30), area, floods), Is.EqualTo("not_wave_caster"));
            Assert.That(area, Is.Empty);
            Assert.That(floods, Is.Empty);
            // Right after a cast the wave is on cooldown, the preview still shows the strip.
            sim.Submit(new CastWaveCommand(sim.Clock.Tick, P0, 0, new EntityId(1), StatusTestWorld.Centre(40, 30)));
            sim.Step();
            Assert.That(mechanic.TryGetNextWaveTick(new EntityId(1), out int next), Is.True);
            Assert.That(next, Is.GreaterThan(sim.Clock.Tick));
            Assert.That(mechanic.PreviewWave(sim, caster, StatusTestWorld.Centre(40, 30), area, floods), Is.Null);
            Assert.That(area, Is.Not.Empty);
        }

        /// <summary>IS-8: a tile the team cannot see is in the area and never in the floods; a drop-off hidden in fog does not shorten the preview's floods although it shortens the cast's.</summary>
        [Test]
        public void Is08_Fog()
        {
            // Wave length 12, so the far end of the strip is out of the caster's sight of 8 tiles.
            Tuning longWave = TuningWith("\"wave_length_tiles\": 6", "\"wave_length_tiles\": 12");
            Simulation sim = Build(TestWorld.FlatMap(), tuning: longWave);
            StatusTestWorld.Spawn(sim, 0, 0, CasterId, P0, StatusTestWorld.Centre(2, 5));
            // Enemy drop-off at (13, 10), out of sight; its no-flood square (4 tiles) covers visible area tile (9, 6) but no tile of the strip's near end.
            StatusTestWorld.Spawn(sim, 0, 1, DropOffId, P1, StatusTestWorld.Centre(13, 10));
            StatusTestWorld.StepThrough(sim, 1);
            TeamId team = sim.Players.TeamOf(P0);
            Assert.That(sim.Vision.IsTileVisible(team, 13, 10), Is.False);
            Assert.That(sim.Vision.IsTileVisible(team, 9, 6), Is.True);
            List<int> area = new List<int>();
            List<int> floods = new List<int>();
            Assert.That(Mechanic(sim).PreviewWave(sim, StatusTestWorld.UnitOf(sim, 1), StatusTestWorld.Centre(20, 5), area, floods), Is.Null);
            // Hidden area tiles: in the area, not in the floods.
            int hidden = 0;
            foreach (int tile in area)
            {
                if (!sim.Vision.IsTileVisible(team, sim.Terrain.XOf(tile), sim.Terrain.YOf(tile)))
                {
                    hidden++;
                    Assert.That(floods, Does.Not.Contain(tile));
                }
            }
            Assert.That(hidden, Is.GreaterThan(0));
            // The hidden drop-off's mask square covers (9, 6), which is visible: the preview floods it, the cast does not.
            int masked = sim.Terrain.IndexOf(9, 6);
            Assert.That(area, Does.Contain(masked));
            Assert.That(floods, Does.Contain(masked));
            Fix64 before = sim.Terrain[masked].WaterDepth;
            sim.Submit(new CastWaveCommand(sim.Clock.Tick, P0, 0, new EntityId(1), StatusTestWorld.Centre(20, 5)));
            sim.Step();
            Assert.That(sim.Terrain[masked].WaterDepth, Is.EqualTo(before), "the cast honours the real drop-off");
            Assert.That(sim.Terrain[sim.Terrain.IndexOf(8, 5)].WaterDepth, Is.GreaterThan(Fix64.Zero), "a tile outside the mask is flooded");
        }

        /// <summary>IS-9: the preview does not move the state hash or the event log, even with hidden water, and works with no drop-offs and with some.</summary>
        [Test]
        public void Is09_Pure()
        {
            Simulation sim = PreviewMatch();
            StatusTestWorld.Spawn(sim, sim.Clock.Tick, 1, DropOffId, P1, StatusTestWorld.Centre(36, 30));
            sim.Step();
            // State before the calls.
            ulong before = sim.ComputeStateHash();
            int events = sim.Events.Current.Count;
            Unit caster = StatusTestWorld.UnitOf(sim, 1);
            List<int> area = new List<int>();
            List<int> floods = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                Mechanic(sim).PreviewWave(sim, caster, StatusTestWorld.Centre(40, 30 + i % 10), area, floods);
            }
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(before));
            Assert.That(sim.Events.Current.Count, Is.EqualTo(events));
        }
    }
}
