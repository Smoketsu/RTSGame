// rts/src/Sim/Vision/TeamVision.cs
// The two bit grids of one team: tiles visible this tick and tiles explored on any tick so far. Part of Rts.Sim.Vision
// (spec: rts/docs/specs/fog_of_war.md section 3). Allies share one TeamVision because vision is per team. Only
// VisionSystem changes it, and only inside Simulation.Start or Simulation.Step. Both grids are hashed.
using Rts.Sim.Core;

namespace Rts.Sim.Vision
{
    /// <summary>Visible-now and explored-ever grids of one team, one bit per tile (bit i % 64 of word i / 64, i = y * Width + x).</summary>
    public sealed class TeamVision
    {
        /// <summary>The team these grids belong to. Range: a team of the match (0 to 254).</summary>
        public TeamId Team { get; }

        /// <summary>Number of tiles covered. Unit: tiles. Range: 64 to 1 048 576.</summary>
        public int TileCount { get; }

        /// <summary>Number of 64-bit words per grid. Unit: words. Range: 1 to 16 384.</summary>
        public int WordCount => _visible.Length;

        /// <summary>Tiles seen this tick: 1 bit per tile, unused bits of the last word always 0.</summary>
        private readonly ulong[] _visible;

        /// <summary>Tiles seen on any tick so far: 1 bit per tile, never cleared, unused bits of the last word always 0.</summary>
        private readonly ulong[] _explored;

        /// <summary>Creates empty grids.</summary>
        /// <param name="team">Team the grids belong to.</param>
        /// <param name="tileCount">Number of tiles of the map. Unit: tiles. Range: 64 to 1 048 576.</param>
        public TeamVision(TeamId team, int tileCount)
        {
            Team = team;
            TileCount = tileCount;
            // Words needed to hold one bit per tile. Unit: words. Range: 1 to 16 384.
            int words = (tileCount + 63) / 64;
            _visible = new ulong[words];
            _explored = new ulong[words];
        }

        /// <summary>True when the tile index was seen this tick. False outside 0 to TileCount - 1.</summary>
        /// <param name="index">Tile index y * Width + x. Unit: tile index.</param>
        public bool IsVisible(int index)
        {
            return index >= 0 && index < TileCount && (_visible[index >> 6] & (1UL << (index & 63))) != 0;
        }

        /// <summary>True when the tile index was seen on any tick so far. False outside 0 to TileCount - 1.</summary>
        /// <param name="index">Tile index y * Width + x. Unit: tile index.</param>
        public bool IsExplored(int index)
        {
            return index >= 0 && index < TileCount && (_explored[index >> 6] & (1UL << (index & 63))) != 0;
        }

        /// <summary>Number of tiles visible now. Unit: tiles. Range: 0 to TileCount. Walks the words; for tests and tools, not the hot path.</summary>
        public int CountVisible()
        {
            return CountBits(_visible);
        }

        /// <summary>Number of tiles explored so far. Unit: tiles. Range: 0 to TileCount.</summary>
        public int CountExplored()
        {
            return CountBits(_explored);
        }

        /// <summary>Step 7.1: clears the visible grid. Internal: VisionSystem only.</summary>
        internal void ClearVisible()
        {
            for (int i = 0; i < _visible.Length; i++)
            {
                _visible[i] = 0UL;
            }
        }

        /// <summary>Marks one tile as visible. Internal: VisionSystem only; the index must be inside the map.</summary>
        /// <param name="index">Tile index y * Width + x. Unit: tile index. Range: 0 to TileCount - 1.</param>
        internal void MarkVisible(int index)
        {
            _visible[index >> 6] |= 1UL << (index & 63);
        }

        /// <summary>Step 7.3: folds the visible grid into the explored grid (explored |= visible). Internal: VisionSystem only.</summary>
        internal void MergeExplored()
        {
            for (int i = 0; i < _visible.Length; i++)
            {
                _explored[i] |= _visible[i];
            }
        }

        /// <summary>Feeds Team, the word count, every visible word ascending, then every explored word ascending into the hash.</summary>
        /// <param name="hasher">The state hasher.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Team);
            hasher.Write(_visible.Length);
            for (int i = 0; i < _visible.Length; i++)
            {
                hasher.Write(_visible[i]);
            }
            for (int i = 0; i < _explored.Length; i++)
            {
                hasher.Write(_explored[i]);
            }
        }

        /// <summary>Counts set bits in a grid.</summary>
        private static int CountBits(ulong[] words)
        {
            // Running total of set bits. Unit: tiles. Range: 0 to 1 048 576.
            int total = 0;
            for (int i = 0; i < words.Length; i++)
            {
                // Copy of the word whose lowest set bit is cleared each pass.
                ulong word = words[i];
                while (word != 0UL)
                {
                    word &= word - 1UL;
                    total++;
                }
            }
            return total;
        }
    }
}
