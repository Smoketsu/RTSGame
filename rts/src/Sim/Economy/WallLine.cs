// rts/src/Sim/Economy/WallLine.cs
// Integer line rasteriser for dragged wall lines (walls_and_tech.md D.3). Part of Rts.Sim.Economy. Pure and static: no simulation state, no
// map bounds, no floating point. Unity calls it to draw the ghost tiles with the same list the simulation will accept in build_wall_line.
using System;
using System.Collections.Generic;

namespace Rts.Sim.Economy
{
    /// <summary>Bresenham line of tiles, 8-connected, in drag order.</summary>
    public static class WallLine
    {
        /// <summary>
        /// Clears <paramref name="output"/> and fills it with the tiles of the line from (x0, y0) toward (x1, y1), start first (all-octant
        /// Bresenham, integers only). A drag longer than <paramref name="maxTiles"/> keeps the tiles nearest the start. Consecutive tiles
        /// share an edge or a corner, so the line has no walkable gap for ground, water or flood flow. Map bounds are ignored (the command checks them).
        /// </summary>
        /// <param name="x0">Start tile X. Unit: tiles. Range: any int in -2^20 to 2^20.</param>
        /// <param name="y0">Start tile Y. Unit: tiles. Range: any int in -2^20 to 2^20.</param>
        /// <param name="x1">End tile X. Unit: tiles. Range: any int in -2^20 to 2^20.</param>
        /// <param name="y1">End tile Y. Unit: tiles. Range: any int in -2^20 to 2^20.</param>
        /// <param name="maxTiles">Most tiles to produce. Unit: tiles (count). Range: 1 to 64 (wall_line_max_tiles); 0 or less produces none.</param>
        /// <param name="output">List to fill; cleared first. Not null.</param>
        /// <returns>The number of tiles written, min(max(|x1 - x0|, |y1 - y0|) + 1, maxTiles).</returns>
        public static int Rasterise(int x0, int y0, int x1, int y1, int maxTiles, List<(int X, int Y)> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (maxTiles <= 0)
            {
                return 0;
            }
            // Absolute X distance and X step direction. Unit: tiles; step -1 or 1.
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            // Negative absolute Y distance and Y step direction. Unit: tiles; step -1 or 1.
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            // Bresenham error term. Unit: tiles. Range: -2^21 to 2^21.
            int err = dx + dy;
            // Current tile.
            int x = x0;
            int y = y0;
            while (true)
            {
                output.Add((x, y));
                if ((x == x1 && y == y1) || output.Count >= maxTiles)
                {
                    return output.Count;
                }
                // Doubled error term deciding which axes step this iteration.
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
    }
}
