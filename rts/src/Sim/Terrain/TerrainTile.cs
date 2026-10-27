// rts/src/Sim/Terrain/TerrainTile.cs
// One tile of the terrain grid: elevation layer, ramp flag, water depth, surface state and blockers.
// Part of Rts.Sim.Terrain (ADR-006). A plain struct stored in TerrainGrid's array; mutated only through
// the grid's internal mutators so every change raises an event and bumps the terrain version.
using System;
using Rts.Sim.Core;

namespace Rts.Sim.Terrain
{
    /// <summary>Surface state of a tile apart from water, which is <see cref="TerrainTile.WaterDepth"/>. Values are stable: they appear in data and hashes.</summary>
    public enum SurfaceState : byte
    {
        /// <summary>Plain ground.</summary>
        Dry = 0,

        /// <summary>Raised by the earth faction (Phase 4).</summary>
        Raised = 1,

        /// <summary>Scorched by the fire faction (Phase 6).</summary>
        Scorched = 2,
    }

    /// <summary>Things that make a tile impassable for ground and water movement. Air ignores all of them.</summary>
    [Flags]
    public enum TileBlockers : byte
    {
        /// <summary>Nothing blocks the tile.</summary>
        None = 0,

        /// <summary>A wall stands on the tile; also blocks flood flow.</summary>
        Wall = 1,

        /// <summary>A building footprint covers the tile; stays dry under floods.</summary>
        Footprint = 2,
    }

    /// <summary>Immutable snapshot of one tile. Range notes are per field.</summary>
    public readonly struct TerrainTile
    {
        /// <summary>Elevation layer. Unit: layer index. Range: 0 to <see cref="MaxHeight"/>.</summary>
        public readonly byte Height;

        /// <summary>True when ground movement may cross between this tile and a neighbour one layer up or down.</summary>
        public readonly bool IsRamp;

        /// <summary>Water on the tile. Unit: depth units (1.0 = one tile of water height). Range: 0 (dry) to tuning max_water_depth.</summary>
        public readonly Fix64 WaterDepth;

        /// <summary>Team whose flooding owns the water, or <see cref="TeamId.None"/> when dry. Water of different teams never mixes; allies share it.</summary>
        public readonly TeamId WaterTeam;

        /// <summary>Dry, raised or scorched.</summary>
        public readonly SurfaceState Surface;

        /// <summary>Walls and footprints on the tile.</summary>
        public readonly TileBlockers Blockers;

        /// <summary>Highest elevation layer a map may use (eight layers, 0 to 7).</summary>
        public const byte MaxHeight = 7;

        /// <summary>Builds a tile; callers validate ranges (the loader and the grid mutators do).</summary>
        public TerrainTile(byte height, bool isRamp, Fix64 waterDepth, TeamId waterTeam, SurfaceState surface, TileBlockers blockers)
        {
            Height = height;
            IsRamp = isRamp;
            WaterDepth = waterDepth;
            WaterTeam = waterDepth > Fix64.Zero ? waterTeam : TeamId.None;
            Surface = surface;
            Blockers = blockers;
        }

        /// <summary>True when any water stands on the tile.</summary>
        public bool IsFlooded => WaterDepth > Fix64.Zero;

        /// <summary>True when a wall or footprint blocks ground and water movement.</summary>
        public bool IsBlocked => Blockers != TileBlockers.None;

        /// <summary>Feeds every field into the state hash in a fixed order.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Height);
            hasher.Write(IsRamp);
            hasher.Write(WaterDepth);
            hasher.Write(WaterTeam);
            hasher.Write((byte)Surface);
            hasher.Write((byte)Blockers);
        }
    }
}
