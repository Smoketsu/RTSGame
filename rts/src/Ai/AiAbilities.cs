// rts/src/Ai/AiAbilities.cs
// Pure target choosers for the casts of the skirmish AI (rts/docs/specs/ai_mechanic_use.md package P1): ChooseGrounding picks the Downdraft
// centre (M1) and ChooseWave picks the wave direction (M2.3), both from the AiWorldView only (own caster, visible enemies, public map size
// and the wave strip size), so they cannot see through the fog. Part of Rts.Ai; read by the rules CG and CW in AiBrain. Fix64 and integers only,
// enemies walked in ascending ID order, distances compared squared, ties to the lower ID or direction index. Changes nothing.
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Ai
{
    /// <summary>Downdraft and wave target choice. Stateless.</summary>
    public static class AiAbilities
    {
        /// <summary>
        /// Distance, along the wave direction, of the target point from the caster. The Sim's WaveDirection maps a straight offset (one axis more than
        /// twice the other) and a diagonal offset (equal axes) back to the direction step. Unit: tiles. Range: fixed at 4 (any value above 0 works).
        /// </summary>
        private const int WaveAimTiles = 4;

        /// <summary>
        /// The best Downdraft target for <paramref name="caster"/>: candidate points are the positions of visible enemy flyers within the ability range;
        /// a point scores the supply of every visible enemy flyer within the ability radius plus that flyer's own radius of it. Returns the best score
        /// (ties to the lower candidate entity ID), or 0 with <paramref name="target"/> at the caster when there is no candidate or no ability.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="caster">An own unit with a grounding ability.</param>
        /// <param name="target">The best centre. Unit: tiles. Range: inside the map; the caster's position when the score is 0.</param>
        public static int ChooseGrounding(AiWorldView view, AiUnitView caster, out FixVec2 target)
        {
            target = caster.Position;
            // The caster's ability; null when its type has none.
            GroundingAbilityDefinition? ability = caster.Definition.GroundingAbility;
            if (ability == null)
            {
                return 0;
            }
            // Squared ability range. Unit: squared tiles. Range: 0.25 to 1024.
            Fix64 rangeSquared = ability.RangeTiles * ability.RangeTiles;
            // Best score so far. Unit: supply. Range: 0 upward.
            int best = 0;
            // One candidate flyer, ascending ID.
            foreach (AiUnitView candidate in view.VisibleEnemies)
            {
                if (candidate.Layer != MovementLayer.Air || (candidate.Position - caster.Position).LengthSquared() > rangeSquared)
                {
                    continue;
                }
                // Supply caught by a pulse centred on this candidate. Unit: supply. Range: 0 upward.
                int score = 0;
                // One flyer tested against the pulse.
                foreach (AiUnitView flyer in view.VisibleEnemies)
                {
                    if (flyer.Layer != MovementLayer.Air)
                    {
                        continue;
                    }
                    // Largest centre distance that counts: pulse radius plus the flyer's own radius. Unit: tiles. Range: 0.5 upward.
                    Fix64 reach = ability.RadiusTiles + flyer.Definition.Radius;
                    if ((flyer.Position - candidate.Position).LengthSquared() <= reach * reach)
                    {
                        score += view.SupplyOf(flyer.Definition);
                    }
                }
                if (score > best)
                {
                    best = score;
                    target = candidate.Position;
                }
            }
            return best;
        }

        /// <summary>
        /// The tiles a wave covers (flooding.md section 5): s = 1..length along the direction (ux, uy), o = -(width - 1) / 2 .. (width - 1) / 2 across it,
        /// tile (cx + s ux - o uy, cy + s uy + o ux); tiles off the map are skipped.
        /// </summary>
        /// <param name="casterX">Caster tile column. Unit: tiles. Range: any.</param>
        /// <param name="casterY">Caster tile row. Unit: tiles. Range: any.</param>
        /// <param name="ux">Direction step in x. Unit: tile step. Range: -1, 0 or 1.</param>
        /// <param name="uy">Direction step in y. Unit: tile step. Range: -1, 0 or 1.</param>
        /// <param name="length">Strip length. Unit: tiles. Range: 1 to 20.</param>
        /// <param name="width">Strip width. Unit: tiles. Range: 1 to 9, odd.</param>
        /// <param name="mapWidth">Map width, to skip off-map tiles. Unit: tiles. Range: 8 to 1024.</param>
        /// <param name="mapHeight">Map height. Unit: tiles. Range: 8 to 1024.</param>
        public static SortedSet<int> WaveTiles(int casterX, int casterY, int ux, int uy, int length, int width, int mapWidth, int mapHeight)
        {
            // Covered tile indices (y * mapWidth + x), ascending.
            SortedSet<int> tiles = new SortedSet<int>();
            // Half the width, rounded down. Unit: tiles. Range: 0 to 4.
            int half = (width - 1) / 2;
            // Distance along the direction. Unit: tiles. Range: 1 to length.
            for (int s = 1; s <= length; s++)
            {
                // Offset across the direction, left negative. Unit: tiles. Range: -half to half.
                for (int o = -half; o <= half; o++)
                {
                    // Tile column of the covered tile at distance s along and offset o across. Unit: tiles. Range: any, kept only inside the map.
                    int x = casterX + s * ux - o * uy;
                    // Tile row of the same covered tile. Unit: tiles. Range: any, kept only inside the map.
                    int y = casterY + s * uy + o * ux;
                    if (x >= 0 && y >= 0 && x < mapWidth && y < mapHeight)
                    {
                        tiles.Add(y * mapWidth + x);
                    }
                }
            }
            return tiles;
        }

        /// <summary>
        /// The best wave direction for <paramref name="caster"/>: for each of the eight directions of <see cref="TerrainGrid.NeighbourDx"/> order, the
        /// supply of visible enemy army units that are not flyers, have a weapon and stand in the strip (with <paramref name="meleeOnly"/>, only those
        /// whose weapon range is at most <paramref name="meleeRange"/>). Best score; ties to the lower direction index. The target is
        /// 4 tiles from the caster along the best direction. Returns 0 when nothing is in any strip.
        /// </summary>
        /// <param name="view">What the AI may know this think.</param>
        /// <param name="caster">An own wave caster.</param>
        /// <param name="meleeOnly">True for the melee aim.</param>
        /// <param name="meleeRange">Weapon range at or below which a unit is melee. Unit: tiles. Range: 0.5 to 3.</param>
        /// <param name="target">Point that gives the best direction. Unit: tiles. Range: caster position plus 4 tiles in each axis of the direction.</param>
        public static Fix64 ChooseWave(AiWorldView view, AiUnitView caster, bool meleeOnly, Fix64 meleeRange, out FixVec2 target)
        {
            target = caster.Position;
            // Best score so far. Unit: supply. Range: 0 upward.
            Fix64 best = Fix64.Zero;
            // Caster tile. Unit: tiles.
            int cx = caster.TileX;
            int cy = caster.TileY;
            // Direction index, 0 to 7.
            for (int d = 0; d < TerrainGrid.NeighbourDx.Length; d++)
            {
                // Direction steps. Unit: tile step.
                int ux = TerrainGrid.NeighbourDx[d];
                int uy = TerrainGrid.NeighbourDy[d];
                // The strip as tile indices on this map.
                SortedSet<int> strip = WaveTiles(cx, cy, ux, uy, view.WaveLengthTiles, view.WaveWidthTiles, view.Width, view.Height);
                // Supply in this strip. Unit: supply.
                Fix64 score = Fix64.Zero;
                // One visible enemy, ascending ID.
                foreach (AiUnitView foe in view.VisibleEnemies)
                {
                    if (!foe.IsArmy || foe.Layer == MovementLayer.Air || foe.Definition.Weapon == null)
                    {
                        continue;
                    }
                    if (meleeOnly && foe.Definition.Weapon.RangeTiles > meleeRange)
                    {
                        continue;
                    }
                    if (view.Contains(foe.TileX, foe.TileY) && strip.Contains(view.IndexOf(foe.TileX, foe.TileY)))
                    {
                        score += Fix64.FromInt(view.SupplyOf(foe.Definition));
                    }
                }
                if (score > best)
                {
                    best = score;
                    target = new FixVec2(caster.Position.X + Fix64.FromInt(WaveAimTiles * ux), caster.Position.Y + Fix64.FromInt(WaveAimTiles * uy));
                }
            }
            return best;
        }
    }
}
