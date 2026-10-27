// rts/src/Sim/Factions/Water/FloodRules.cs
// The pure integer rules of flooding over a FloodField: where a team's water is accepted, the downhill redistribution
// passes, tide retreat toward the sources, displacement off a tile that became a footprint (or was raised), the no-flood
// mask around enemy anchors and the release tile of a source. Part of Rts.Sim.Factions.Water (spec:
// rts/docs/specs/flooding.md sections 4 and 6). Everything is integers (drops, tiles, ticks): no floating point, no
// random numbers, tiles always visited in ascending index order and neighbours in the order N, E, S, W, so the same field
// gives the same result on every machine. The earth system (Phase 4) will call Displace when it raises ground under water.
using System;
using System.Collections.Generic;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Static flood rules over a <see cref="FloodField"/>.</summary>
    public static class FloodRules
    {
        /// <summary>
        /// Divisor of the level difference in one redistribution step: 4 neighbours plus the tile itself, so a tile never gives
        /// away more than it can level with all four neighbours (no overshoot). Range: fixed 5.
        /// </summary>
        public const int FlowDivisor = 5;

        /// <summary>
        /// True when team <paramref name="team"/>'s water may be put on tile <paramref name="tile"/>: on the map, no wall, no
        /// footprint, outside the team's no-flood mask, and the tile holds no other team's water (water pending from this pass
        /// counts as the owner).
        /// </summary>
        /// <param name="field">The working field.</param>
        /// <param name="tile">Candidate tile index, or -1 for off the map (always false).</param>
        /// <param name="team">The water's team. Range: 0 to 254.</param>
        /// <param name="pending">Owners claimed so far in the current redistribution pass per tile, or null outside a pass.</param>
        public static bool Accepts(FloodField field, int tile, int team, int[]? pending)
        {
            if (tile < 0 || field.Wall[tile] || field.Footprint[tile] || field.IsNoFlood(team, tile))
            {
                return false;
            }
            // Team that holds or has claimed the tile, or NoTeam.
            int owner = field.Team[tile];
            if (owner == FloodField.NoTeam && pending != null)
            {
                owner = pending[tile];
            }
            return owner == FloodField.NoTeam || owner == team;
        }

        /// <summary>Free space on a tile. Unit: drops. Range: 0 to MaxDrops.</summary>
        public static int Room(FloodField field, int tile)
        {
            return field.MaxDrops - field.Drops[tile];
        }

        /// <summary>
        /// Runs <paramref name="passes"/> Jacobi passes of downhill flow: every pass reads one snapshot of the levels and gives
        /// each water tile (ascending index) a share of one fifth of the level difference to each lower accepting neighbour
        /// (N, E, S, W), capped by the water it still holds and by the neighbour's room. Water crosses cliffs downward freely
        /// and climbs only when levels allow it. Total drops are conserved. A tile owned by nobody, or a wall, never gives.
        /// </summary>
        /// <param name="field">The working field; its Drops and Team arrays change.</param>
        /// <param name="passes">Number of passes. Unit: passes. Range: 1 to 16 (0 does nothing).</param>
        public static void Redistribute(FloodField field, int passes)
        {
            // Per-pass snapshot of every tile's level. Unit: drops.
            int[] level = new int[field.TileCount];
            // Net change of every tile in the current pass. Unit: drops.
            int[] delta = new int[field.TileCount];
            // Owner claimed on a dry tile in the current pass, NoTeam when unclaimed.
            int[] pending = new int[field.TileCount];
            // Pass counter. Unit: passes. Range: 0 to passes - 1.
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < field.TileCount; i++)
                {
                    level[i] = field.Level(i);
                    delta[i] = 0;
                    pending[i] = FloodField.NoTeam;
                }
                for (int i = 0; i < field.TileCount; i++)
                {
                    if (field.Drops[i] == 0 || field.Wall[i] || field.Team[i] == FloodField.NoTeam)
                    {
                        continue;
                    }
                    // Owner of this tile's water and the part of it not yet given away this pass. Unit: drops.
                    int team = field.Team[i];
                    int remaining = field.Drops[i];
                    // Direction counter in the order N, E, S, W.
                    for (int direction = 0; direction < 4; direction++)
                    {
                        // Neighbour index in that direction, -1 off the map.
                        int j = field.Neighbour(i, direction);
                        if (!Accepts(field, j, team, pending))
                        {
                            continue;
                        }
                        // Level difference to the neighbour. Unit: drops. Positive means downhill.
                        int difference = level[i] - level[j];
                        if (difference <= 0)
                        {
                            continue;
                        }
                        // Drops to move: one fifth of the difference, at most what is left and what the neighbour can still take.
                        int amount = Math.Min(difference / FlowDivisor, Math.Min(remaining, field.MaxDrops - field.Drops[j] - delta[j]));
                        if (amount <= 0)
                        {
                            continue;
                        }
                        remaining -= amount;
                        delta[i] -= amount;
                        delta[j] += amount;
                        if (field.Team[j] == FloodField.NoTeam && pending[j] == FloodField.NoTeam)
                        {
                            pending[j] = team;
                        }
                    }
                }
                for (int i = 0; i < field.TileCount; i++)
                {
                    field.Drops[i] += delta[i];
                    if (pending[i] != FloodField.NoTeam && field.Team[i] == FloodField.NoTeam)
                    {
                        field.Team[i] = pending[i];
                    }
                    if (field.Drops[i] == 0)
                    {
                        field.Team[i] = FloodField.NoTeam;
                    }
                }
            }
        }

        /// <summary>
        /// Takes <paramref name="amount"/> drops of team <paramref name="team"/>'s water away, farthest from the sources first:
        /// a breadth-first search (N, E, S, W) from the seed tiles over that team's water tiles gives each a distance;
        /// tiles are drained in order of distance descending then index descending. Tiles the search never reaches count as
        /// infinitely far. A drained tile becomes dry and unowned.
        /// </summary>
        /// <param name="field">The working field.</param>
        /// <param name="team">The team whose water retreats. Range: 0 to 254.</param>
        /// <param name="amount">Drops to remove. Unit: drops. Range: 0 upward.</param>
        /// <param name="seeds">Release tiles of the team's sources (any order, duplicates allowed); empty when the team has none.</param>
        /// <returns>The drops actually removed. Unit: drops. Range: 0 to amount.</returns>
        public static int Retreat(FloodField field, int team, int amount, IReadOnlyList<int> seeds)
        {
            if (amount <= 0)
            {
                return 0;
            }
            // Breadth-first distance from the nearest seed per tile; int.MaxValue marks unreached tiles.
            int[] distance = new int[field.TileCount];
            for (int i = 0; i < field.TileCount; i++)
            {
                distance[i] = int.MaxValue;
            }
            // Seed tiles, ascending and without duplicates, so the search order never depends on the caller's list order.
            List<int> ordered = new List<int>(seeds);
            ordered.Sort();
            // First-in first-out queue of tiles to expand, as an array with a read and a write position.
            int[] queue = new int[field.TileCount];
            int head = 0;
            int tail = 0;
            foreach (int seed in ordered)
            {
                if (seed >= 0 && seed < field.TileCount && distance[seed] == int.MaxValue)
                {
                    distance[seed] = 0;
                    queue[tail++] = seed;
                }
            }
            while (head < tail)
            {
                // Tile being expanded.
                int current = queue[head++];
                for (int direction = 0; direction < 4; direction++)
                {
                    // Neighbour in that direction; only the retreating team's water tiles are walked through.
                    int next = field.Neighbour(current, direction);
                    if (next >= 0 && distance[next] == int.MaxValue && field.Team[next] == team)
                    {
                        distance[next] = distance[current] + 1;
                        queue[tail++] = next;
                    }
                }
            }
            // The team's water tiles in drain order: farthest first, then the higher index first.
            List<int> tiles = new List<int>();
            for (int i = 0; i < field.TileCount; i++)
            {
                if (field.Team[i] == team && field.Drops[i] > 0)
                {
                    tiles.Add(i);
                }
            }
            tiles.Sort((a, b) =>
            {
                if (distance[a] != distance[b])
                {
                    return distance[a] > distance[b] ? -1 : 1;
                }
                return b.CompareTo(a);
            });
            // Drops still to remove. Unit: drops.
            int left = amount;
            foreach (int tile in tiles)
            {
                if (left == 0)
                {
                    break;
                }
                // Drops taken from this tile.
                int take = Math.Min(field.Drops[tile], left);
                field.Drops[tile] -= take;
                left -= take;
                if (field.Drops[tile] == 0)
                {
                    field.Team[tile] = FloodField.NoTeam;
                }
            }
            return amount - left;
        }

        /// <summary>
        /// Moves the water off a tile that may no longer hold it (a building footprint, raised ground): to the accepting
        /// neighbour with the lowest level (ties to the earlier of N, E, S, W), as much as fits there; whatever does not fit,
        /// or all of it when no neighbour accepts, is removed. The tile ends dry and unowned.
        /// </summary>
        /// <param name="field">The working field.</param>
        /// <param name="tile">The tile index to clear.</param>
        /// <returns>The drops removed from the map (not moved). Unit: drops. Range: 0 to the tile's water.</returns>
        public static int Displace(FloodField field, int tile)
        {
            // The water on the tile and its owner.
            int drops = field.Drops[tile];
            int team = field.Team[tile];
            if (drops == 0)
            {
                field.Team[tile] = FloodField.NoTeam;
                return 0;
            }
            field.Drops[tile] = 0;
            field.Team[tile] = FloodField.NoTeam;
            if (team == FloodField.NoTeam)
            {
                return drops;
            }
            // Best target so far and its level; -1 while none qualifies.
            int best = -1;
            int bestLevel = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                // Neighbour in that direction, -1 off the map.
                int j = field.Neighbour(tile, direction);
                if (!Accepts(field, j, team, null))
                {
                    continue;
                }
                // Level of the candidate; only a strictly lower one replaces the earlier choice.
                int level = field.Level(j);
                if (best < 0 || level < bestLevel)
                {
                    best = j;
                    bestLevel = level;
                }
            }
            if (best < 0)
            {
                return drops;
            }
            // Drops that fit on the target; the rest is removed.
            int moved = Math.Min(drops, Room(field, best));
            if (moved > 0)
            {
                field.Drops[best] += moved;
                if (field.Team[best] == FloodField.NoTeam)
                {
                    field.Team[best] = team;
                }
            }
            return drops - moved;
        }

        /// <summary>
        /// Marks every tile within <paramref name="gapTiles"/> Chebyshev tiles of any anchor tile: the tiles an enemy's water may
        /// not occupy. With a gap of 4 and an anchor at x = 12 the marked columns are 8 to 16 (four dry tiles between the
        /// water's edge and the anchor).
        /// </summary>
        /// <param name="width">Map width in tiles.</param>
        /// <param name="height">Map height in tiles.</param>
        /// <param name="anchors">Anchor tile indices (enemy drop-off footprints and enemy resource fields).</param>
        /// <param name="gapTiles">Whole tiles of dry gap. Unit: tiles. Range: 0 to 12.</param>
        public static bool[] NoFloodMask(int width, int height, IReadOnlyList<int> anchors, int gapTiles)
        {
            // Result mask, false everywhere at first.
            bool[] mask = new bool[width * height];
            foreach (int anchor in anchors)
            {
                // Anchor coordinates and the clamped square around them. Unit: tiles.
                int ax = anchor % width;
                int ay = anchor / width;
                int x0 = Math.Max(0, ax - gapTiles);
                int x1 = Math.Min(width - 1, ax + gapTiles);
                int y0 = Math.Max(0, ay - gapTiles);
                int y1 = Math.Min(height - 1, ay + gapTiles);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        mask[y * width + x] = true;
                    }
                }
            }
            return mask;
        }

        /// <summary>
        /// The tile a source at <paramref name="tile"/> releases onto: searching rings of Chebyshev radius 0 up to
        /// <paramref name="radius"/> around it (each ring in ascending tile index), the first tile that accepts the team's
        /// water. Returns -1 when none does.
        /// </summary>
        /// <param name="field">The working field.</param>
        /// <param name="tile">The source's own tile.</param>
        /// <param name="team">The source owner's team. Range: 0 to 254.</param>
        /// <param name="radius">Largest ring. Unit: tiles. Range: 0 to 6.</param>
        public static int Origin(FloodField field, int tile, int team, int radius)
        {
            // Source coordinates. Unit: tiles.
            int sx = tile % field.Width;
            int sy = tile / field.Width;
            for (int ring = 0; ring <= radius; ring++)
            {
                // Rows then columns, ascending, which is ascending tile index.
                for (int y = sy - ring; y <= sy + ring; y++)
                {
                    if (y < 0 || y >= field.Height)
                    {
                        continue;
                    }
                    for (int x = sx - ring; x <= sx + ring; x++)
                    {
                        if (x < 0 || x >= field.Width || Math.Max(Math.Abs(x - sx), Math.Abs(y - sy)) != ring)
                        {
                            continue;
                        }
                        // Candidate tile index.
                        int candidate = y * field.Width + x;
                        if (Accepts(field, candidate, team, null))
                        {
                            return candidate;
                        }
                    }
                }
            }
            return -1;
        }
    }
}
