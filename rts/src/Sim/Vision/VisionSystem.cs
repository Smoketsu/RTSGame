// rts/src/Sim/Vision/VisionSystem.cs
// Fog of war computed inside the simulation (sim.Vision): per-team visible-now and explored-ever grids, recomputed once
// per tick, plus the read-only query API used by combat, the AI and the renderer. Part of Rts.Sim.Vision
// (spec: rts/docs/specs/fog_of_war.md).
//
// Step 7 of Simulation.Step (after faction mechanics, before the state hash) calls Recompute; Simulation.Start runs it once
// after OnMatchStart so tick 0 has vision. Rules:
//   * Vision is per team, so allies share one grid. Units without an owner (neutral) give no vision.
//   * A unit stamps an integer circle (VisionStamp) of its sight radius around its own tile: sight_range_tiles on the ground
//     or water layer (and while Grounded), airborne_sight_range_tiles while its EffectiveLayer is Air.
//   * High-ground rule: a ground or water viewer never marks a tile higher than its eye height (its tile's height, plus 1
//     on a ramp). Flyers see every height. A spotter on the higher layer marks those tiles for the whole team.
//   * No line-of-sight raycasting across tiles of the same height in Phase 1.
//   * Explored is the union of every Visible grid so far and is never cleared.
// Commands are NOT filtered by vision yet: a command naming an enemy unit the issuer's team cannot see is applied
// normally. The Phase 2 anti-cheat plan (spec section 5) rejects such commands as target_not_visible and feeds a maphack
// detector; until then the only reader that gates on vision is automatic target acquisition in CombatSystem.
// Determinism: integers and Fix64 comparisons only, units visited in ascending ID order, teams in ascending TeamId order,
// the stamp cache is a SortedDictionary and is a pure function of the radius (not hashed).
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Vision
{
    /// <summary>Per-team fog of war for one match. Created by <see cref="Simulation"/> after the terrain.</summary>
    public sealed class VisionSystem
    {
        /// <summary>Terrain whose heights and ramps decide what a viewer may see.</summary>
        private readonly TerrainGrid _terrain;

        /// <summary>Players and teams, for the team of a unit's owner.</summary>
        private readonly PlayerRegistry _players;

        /// <summary>Live units, read each recompute.</summary>
        private readonly UnitRegistry _units;

        /// <summary>One entry per team of the match in ascending TeamId order.</summary>
        private readonly List<TeamVision> _teams = new List<TeamVision>();

        /// <summary>Lookup of a team's grids by TeamId.Value (0 to 254); null for teams not in the match. Lookup only, never iterated.</summary>
        private readonly TeamVision?[] _byTeam = new TeamVision?[256];

        /// <summary>Stamp offsets cached by the radius's Fix64.Raw. A pure function of the radius, so not hashed.</summary>
        private readonly SortedDictionary<long, VisionOffset[]> _stamps = new SortedDictionary<long, VisionOffset[]>();

        /// <summary>Builds empty grids for every team of the match.</summary>
        /// <param name="terrain">The match terrain.</param>
        /// <param name="players">The match players and teams.</param>
        /// <param name="units">The live units.</param>
        public VisionSystem(TerrainGrid terrain, PlayerRegistry players, UnitRegistry units)
        {
            _terrain = terrain;
            _players = players;
            _units = units;
            foreach (TeamId team in players.Teams)
            {
                // Grids of this team, created in ascending TeamId order.
                TeamVision grids = new TeamVision(team, terrain.TileCount);
                _teams.Add(grids);
                _byTeam[team.Value] = grids;
            }
        }

        /// <summary>The grids of every team in ascending TeamId order.</summary>
        public IReadOnlyList<TeamVision> Teams => _teams;

        /// <summary>The grids of one team, or null for a team that is not in the match.</summary>
        /// <param name="team">Team to look up.</param>
        public TeamVision? GridsOf(TeamId team)
        {
            return _byTeam[team.Value];
        }

        /// <summary>Team of a player (<see cref="TeamId.None"/> for an unknown player). Passes through to the player registry.</summary>
        /// <param name="player">Player to look up.</param>
        public TeamId TeamOfPlayer(PlayerId player)
        {
            return _players.TeamOf(player);
        }

        /// <summary>True when the tile was seen by the team on the last recompute. False off the map or for an unknown team.</summary>
        /// <param name="team">Team asking.</param>
        /// <param name="x">Tile column. Unit: tiles. Range: 0 to Width - 1 (anything else gives false).</param>
        /// <param name="y">Tile row. Unit: tiles. Range: 0 to Height - 1 (anything else gives false).</param>
        public bool IsTileVisible(TeamId team, int x, int y)
        {
            TeamVision? grids = _byTeam[team.Value];
            return grids != null && _terrain.Contains(x, y) && grids.IsVisible(_terrain.IndexOf(x, y));
        }

        /// <summary>True when the team has seen the tile on any tick so far. False off the map or for an unknown team.</summary>
        /// <param name="team">Team asking.</param>
        /// <param name="x">Tile column. Unit: tiles.</param>
        /// <param name="y">Tile row. Unit: tiles.</param>
        public bool IsTileExplored(TeamId team, int x, int y)
        {
            TeamVision? grids = _byTeam[team.Value];
            return grids != null && _terrain.Contains(x, y) && grids.IsExplored(_terrain.IndexOf(x, y));
        }

        /// <summary>Same as <see cref="IsTileVisible"/> (alias used by hosts that think in "can the team see").</summary>
        /// <param name="team">Team asking.</param>
        /// <param name="x">Tile column. Unit: tiles.</param>
        /// <param name="y">Tile row. Unit: tiles.</param>
        public bool CanTeamSee(TeamId team, int x, int y)
        {
            return IsTileVisible(team, x, y);
        }

        /// <summary>Same as <see cref="IsTileExplored"/>.</summary>
        /// <param name="team">Team asking.</param>
        /// <param name="x">Tile column. Unit: tiles.</param>
        /// <param name="y">Tile row. Unit: tiles.</param>
        public bool IsExplored(TeamId team, int x, int y)
        {
            return IsTileExplored(team, x, y);
        }

        /// <summary>Same as <see cref="IsUnitVisibleTo"/>.</summary>
        /// <param name="team">Team asking.</param>
        /// <param name="unit">The unit in question.</param>
        public bool CanTeamSeeUnit(TeamId team, Unit unit)
        {
            return IsUnitVisibleTo(team, unit);
        }

        /// <summary>
        /// True when the unit's owner is on <paramref name="team"/>, or when the unit's floored tile is visible to the team.
        /// A unit whose tile is off the map, or a query for an unknown team, gives false (own units excepted).
        /// </summary>
        /// <param name="team">Team asking.</param>
        /// <param name="unit">The unit in question.</param>
        public bool IsUnitVisibleTo(TeamId team, Unit unit)
        {
            if (!team.IsValid)
            {
                return false;
            }
            if (unit.Owner.IsValid && _players.TeamOf(unit.Owner) == team)
            {
                return true;
            }
            return IsTileVisible(team, unit.Position.X.FloorToInt(), unit.Position.Y.FloorToInt());
        }

        /// <summary>
        /// Clears <paramref name="into"/>, then adds in ascending ID order every unit owned by an enemy team of
        /// <paramref name="team"/> or by nobody (neutral) that <see cref="IsUnitVisibleTo"/> the team.
        /// </summary>
        /// <param name="team">Team asking.</param>
        /// <param name="into">List to fill; cleared first.</param>
        public void CollectVisibleEnemies(TeamId team, List<EntityId> into)
        {
            into.Clear();
            if (!team.IsValid || _byTeam[team.Value] == null)
            {
                return;
            }
            foreach (Unit unit in _units.All)
            {
                // True for neutral units and for units of any other team.
                bool hostile = !unit.Owner.IsValid || _players.TeamOf(unit.Owner) != team;
                if (hostile && IsUnitVisibleTo(team, unit))
                {
                    into.Add(unit.Id);
                }
            }
        }

        /// <summary>
        /// Step 7 of <see cref="Simulation.Step"/> (also run once by <see cref="Simulation.Start"/>): clears every team's
        /// visible grid, stamps every owned unit's sight circle in ascending unit ID order, then folds visible into explored.
        /// Internal: only the simulation calls it.
        /// </summary>
        internal void Recompute()
        {
            foreach (TeamVision grids in _teams)
            {
                grids.ClearVisible();
            }
            foreach (Unit unit in _units.All)
            {
                if (!unit.Owner.IsValid)
                {
                    // Neutral units give no vision.
                    continue;
                }
                // Grids of the viewer's team; null cannot happen for a registered player but is skipped defensively.
                TeamVision? grids = _byTeam[_players.TeamOf(unit.Owner).Value];
                if (grids == null)
                {
                    continue;
                }
                // The tile the unit stands on. Unit: tiles. Range: must lie inside the map or the unit gives no vision.
                int centreX = unit.Position.X.FloorToInt();
                int centreY = unit.Position.Y.FloorToInt();
                if (!_terrain.Contains(centreX, centreY))
                {
                    continue;
                }
                Stamp(grids, unit, centreX, centreY);
            }
            foreach (TeamVision grids in _teams)
            {
                grids.MergeExplored();
            }
        }

        /// <summary>
        /// Called by <see cref="Simulation.SpawnUnit"/> for a new unit: stamps its sight onto its team's visible grid at once, so a
        /// unit that appears mid-step can acquire targets on its spawn tick instead of one tick later. The step's own recompute
        /// (step 9) rebuilds the grids from scratch afterwards, so the end-of-tick state, the explored grid and the hash are exactly
        /// what a recompute alone would give. Internal: only the simulation calls it, inside a step or Start.
        /// </summary>
        /// <param name="unit">The unit just added to the registry.</param>
        internal void OnUnitSpawned(Unit unit)
        {
            if (!unit.Owner.IsValid)
            {
                return;
            }
            // Grids of the new unit's team; null cannot happen for a registered player.
            TeamVision? grids = _byTeam[_players.TeamOf(unit.Owner).Value];
            // Tile the unit stands on. Unit: tiles.
            int centreX = unit.Position.X.FloorToInt();
            int centreY = unit.Position.Y.FloorToInt();
            if (grids != null && _terrain.Contains(centreX, centreY))
            {
                Stamp(grids, unit, centreX, centreY);
            }
        }

        /// <summary>Marks the tiles one unit sees: its sight circle, filtered by the high-ground rule for ground and water viewers.</summary>
        private void Stamp(TeamVision grids, Unit unit, int centreX, int centreY)
        {
            // Layer the unit currently moves on; only Air uses the airborne radius and sees every height.
            MovementLayer layer = unit.EffectiveLayer;
            // Sight radius for this layer. Unit: tiles (fixed-point). Range: 1 to 64.
            Fix64 radius = layer == MovementLayer.Air ? unit.Definition.AirborneSightRangeTiles : unit.Definition.SightRangeTiles;
            // Highest tile height the viewer sees. Unit: height layers. Range: 0 to 8, or int.MaxValue for a flyer.
            int eye = int.MaxValue;
            if (layer != MovementLayer.Air)
            {
                // Tile the viewer stands on; a ramp lets it see one layer up.
                TerrainTile own = _terrain.At(centreX, centreY);
                eye = own.Height + (own.IsRamp ? 1 : 0);
            }
            // Offsets of the sight circle for this radius.
            VisionOffset[] offsets = StampFor(radius);
            for (int i = 0; i < offsets.Length; i++)
            {
                // Tile column and row of this offset. Unit: tiles.
                int x = centreX + offsets[i].Dx;
                int y = centreY + offsets[i].Dy;
                if (!_terrain.Contains(x, y))
                {
                    continue;
                }
                // Index of the tile in the grid.
                int index = _terrain.IndexOf(x, y);
                if (_terrain[index].Height <= eye)
                {
                    grids.MarkVisible(index);
                }
            }
        }

        /// <summary>Stamp offsets for a radius, computed once per distinct radius.</summary>
        private VisionOffset[] StampFor(Fix64 radius)
        {
            if (!_stamps.TryGetValue(radius.Raw, out VisionOffset[]? offsets))
            {
                offsets = VisionStamp.Offsets(radius);
                _stamps.Add(radius.Raw, offsets);
            }
            return offsets;
        }

        /// <summary>
        /// Feeds the team count, then for each team in ascending TeamId order: Team, word count, every visible word, every
        /// explored word. The stamp cache is not hashed. Called from <see cref="Simulation.ComputeStateHash"/> after the terrain.
        /// </summary>
        /// <param name="hasher">The state hasher.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(_teams.Count);
            foreach (TeamVision grids in _teams)
            {
                grids.WriteHash(hasher);
            }
        }
    }
}
