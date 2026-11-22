// rts/tests/Sim.Tests/Ai/ExploreKit.cs
// Test helper for the AI tests after the worker construction rule (specs/worker_construction.md): a build order is refused as site_unexplored
// on a tile the team never saw, and the AI walks a worker there first. Tests that are about something else (where a depot goes, when a producer
// is built) mark the whole map as explored for every team so the AI builds at once; the tests of the scouting trip leave the map alone.
// Marking uses the internal vision mutators (Rts.Sim.Tests sees them) and leaves the visible grid as the last step computed it, so fog tests are
// not affected. Part of Rts.Sim.Tests. Test code only; no change to Rts.Sim.
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Vision;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Marks maps explored for the AI tests.</summary>
    internal static class ExploreKit
    {
        /// <summary>
        /// Marks every tile of the map explored for every team. The visible grid is cleared and refilled to what it was, so only the explored
        /// grid changes (and with it the state hash, which is fine in a test that does not compare against a pinned value).
        /// </summary>
        /// <param name="sim">A started simulation between steps.</param>
        public static void ExploreAll(Simulation sim)
        {
            // Vision grids of one team each. Range: one per team.
            foreach (TeamVision grids in sim.Vision.Teams)
            {
                // Visible flag per tile index (index 0 to TileCount - 1) before marking, restored afterwards.
                bool[] wasVisible = new bool[grids.TileCount];
                // Tile index. Unit: tile index. Range: 0 to TileCount - 1.
                for (int index = 0; index < grids.TileCount; index++)
                {
                    wasVisible[index] = grids.IsVisible(index);
                    grids.MarkVisible(index);
                }
                grids.MergeExplored();
                grids.ClearVisible();
                for (int index = 0; index < grids.TileCount; index++)
                {
                    if (wasVisible[index])
                    {
                        grids.MarkVisible(index);
                    }
                }
            }
        }
        /// <summary>
        /// As <see cref="ExploreAll"/>, but the listed tiles stay as they were (a test of a line nobody has seen). Call it before any step that could reveal them.
        /// </summary>
        /// <param name="sim">A started simulation between steps.</param>
        /// <param name="except">Tiles (x, y) left unexplored. Unit: tile coordinates. Range: on the map.</param>
        public static void ExploreAllExcept(Simulation sim, IEnumerable<(int X, int Y)> except)
        {
            // Tile indices left alone. Unit: tile index.
            SortedSet<int> skipped = new SortedSet<int>();
            foreach ((int X, int Y) tile in except)
            {
                skipped.Add(sim.Terrain.IndexOf(tile.X, tile.Y));
            }
            // Vision grids of one team each. Range: one per team.
            foreach (TeamVision grids in sim.Vision.Teams)
            {
                // Visible flag per tile index before marking, restored afterwards.
                bool[] wasVisible = new bool[grids.TileCount];
                // Tile index. Unit: tile index. Range: 0 to TileCount - 1.
                for (int index = 0; index < grids.TileCount; index++)
                {
                    wasVisible[index] = grids.IsVisible(index);
                    if (!skipped.Contains(index))
                    {
                        grids.MarkVisible(index);
                    }
                }
                grids.MergeExplored();
                grids.ClearVisible();
                for (int index = 0; index < grids.TileCount; index++)
                {
                    if (wasVisible[index])
                    {
                        grids.MarkVisible(index);
                    }
                }
            }
        }
    }
}
