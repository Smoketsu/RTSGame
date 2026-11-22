// rts/src/Ai/AiBrainWalls.cs
// Rule WL of the skirmish AI (rts/docs/specs/ai_mechanic_use.md M3.2.4, only with use_walls): walls and gates on the authored lines of the areas the AI holds (main base first, then held expansions up to
// expansion_wall_max_bases), from spare money, within a per-match spending cap, never under fire. Runs in Decide after rule 3c (static defence) and before rule 4 (expand). Part of Rts.Ai (a partial of AiBrain's Turn).
// The held areas come from AiWallLines.HeldAreas; the wall style (none, funnel, sealed) was drawn per match by AiVariety. Reads only the view, the profile, memory and the authored lines (public map data).
// Integers and Fix64 only; areas in held order, lines in file order, tiles in rasterised order, workers by the nearest-then-lowest-ID rule of PickBuilder, so nothing depends on hash order.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Units;

namespace Rts.Ai
{
    public static partial class AiBrain
    {
        private sealed partial class Turn
        {
            /// <summary>
            /// Rule WL (ai_mechanic_use.md M3.2.4). Does nothing unless use_walls is on, the map has authored lines, the drawn style is not none, wall_earliest_seconds have passed, the enemy model is mostly ground and the match
            /// spending cap is not reached. For each held area in wall order that no visible enemy army unit threatens, and each of its lines in file order: one build_wall_line for the missing wall tiles the free money pays, then
            /// (style sealed) a gate on the line's gate tile. Stops for the think when a tile cannot be paid (nothing is reserved), no worker is free, the command cap is reached or wall_max_lines_per_think lines were emitted.
            /// </summary>
            public void Walls()
            {
                // The authored wall lines of the current map, set by TryGetValue. Range: non-null when the branch continues.
                if (!_profile.UseWalls || !_profile.WallLines.TryGetValue(_view.MapId, out AiMapWalls? walls))
                {
                    return;
                }
                // The wall style drawn for this match; none without a variety stream.
                WallStyle style = _memory.Variety?.WallStyle ?? WallStyle.None;
                if (style == WallStyle.None || _view.Tick < _view.SecondsToTicks(Fix64.FromInt(_profile.WallEarliestSeconds)) || _memory.WallSpentCommon >= _profile.WallMaxSpendCommon)
                {
                    return;
                }
                if (_profile.UseEnemyModel && !EnemyMostlyGround())
                {
                    return;
                }
                // The faction's wall and gate definitions: the ordinally lowest ID of each kind (a unit of the plan's faction with is_wall and not is_gate, and with is_gate).
                UnitDefinition? wall = null;
                UnitDefinition? gate = null;
                foreach (UnitDefinition candidate in _view.Content.Units.Values)
                {
                    if (!string.Equals(candidate.FactionId, _plan.FactionId, StringComparison.Ordinal) || !candidate.Economy.IsWall)
                    {
                        continue;
                    }
                    if (candidate.Economy.IsGate)
                    {
                        gate = gate == null || string.CompareOrdinal(candidate.Id, gate.Id) < 0 ? candidate : gate;
                    }
                    else
                    {
                        wall = wall == null || string.CompareOrdinal(candidate.Id, wall.Id) < 0 ? candidate : wall;
                    }
                }
                if (wall == null || gate == null || !RequirementsMet(wall) || !RequirementsMet(gate))
                {
                    return;
                }
                // Areas to wall this think, in wall order.
                List<AiWallArea> areas = AiWallLines.HeldAreas(_view, walls, _memory.OwnSpawnIndex, ReachSquared, _profile.ExpansionWallMaxBases);
                if (areas.Count == 0)
                {
                    return;
                }
                // Tiles that already hold, or are about to hold, an own wall: wall units (complete or under construction), own workers' active build tiles and queued tiles. Unit: tile indices. Range: 0 to tile count - 1.
                SortedSet<int> taken = new SortedSet<int>();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.Definition.Economy.IsWall)
                    {
                        taken.Add(_view.IndexOf(unit.TileX, unit.TileY));
                    }
                    if (unit.IsWorker)
                    {
                        if (unit.HasBuildOrder && _view.Contains(unit.BuildTileX, unit.BuildTileY))
                        {
                            taken.Add(_view.IndexOf(unit.BuildTileX, unit.BuildTileY));
                        }
                        foreach (int queued in unit.BuildQueue)
                        {
                            taken.Add(queued);
                        }
                    }
                }
                // Wall lines emitted this think (a gate is not a line). Unit: lines. Range: 0 to wall_max_lines_per_think.
                int emitted = 0;
                foreach (AiWallArea area in areas)
                {
                    if (AreaThreatened(area))
                    {
                        continue;
                    }
                    foreach (AiWallLine line in area.Lines)
                    {
                        if (emitted >= _profile.WallMaxLinesPerThink)
                        {
                            return;
                        }
                        // Wall tiles of this line still to build for the style, in line order.
                        List<(int X, int Y)> missing = new List<(int X, int Y)>();
                        foreach ((int X, int Y) tile in line.Tiles)
                        {
                            if (WallWanted(line, tile, style) && TileMissing(tile, taken))
                            {
                                missing.Add(tile);
                            }
                        }
                        if (missing.Count > 0)
                        {
                            // 1 when the line was handled (ordered, or the builder sent to explore it), -1 when the rule must stop. Unit: result code.
                            int outcome = OrderLine(wall, missing);
                            if (outcome < 0)
                            {
                                return;
                            }
                            emitted += outcome;
                            if (outcome > 0 && !_view.IsExplored(missing[0].X, missing[0].Y))
                            {
                                // The builder was only sent to look at the line; its gate waits for the next think too.
                                continue;
                            }
                        }
                        if (style == WallStyle.Sealed && line.Gate != null && TileMissing(line.Gate.Value, taken) && !OrderGate(gate, line.Gate.Value))
                        {
                            return;
                        }
                    }
                }
            }

            /// <summary>True when the enemy model is empty or its non-air share of supply is at least wall_min_enemy_ground_share (walls do not stop flyers).</summary>
            private bool EnemyMostlyGround()
            {
                // Supply of the whole model, and of its air part. Unit: supply (Fix64). Range: 0 upward.
                Fix64 total = Fix64.Zero;
                foreach (KeyValuePair<string, Fix64> pair in _model)
                {
                    if (_view.Content.Units.TryGetValue(pair.Key, out UnitDefinition? definition))
                    {
                        total += pair.Value * Fix64.FromInt(_view.SupplyOf(definition));
                    }
                }
                if (total <= Fix64.Zero)
                {
                    return true;
                }
                // Supply of the model's air types. Unit: supply (Fix64). Range: 0 to total.
                Fix64 air = AiIntel.LayerSupply(_view, _model, MovementLayer.Air);
                return (total - air) >= _profile.WallMinEnemyGroundShare * total;
            }

            /// <summary>Step 1: true when a visible enemy army unit stands within wall_threat_tiles of an anchor or a line tile of the area.</summary>
            /// <param name="area">The area about to be walled.</param>
            private bool AreaThreatened(AiWallArea area)
            {
                // Squared threat distance. Unit: squared tiles. Range: 0 to 900.
                Fix64 threatSquared = _profile.WallThreatTiles * _profile.WallThreatTiles;
                foreach (AiUnitView enemy in _view.VisibleEnemies)
                {
                    if (!enemy.IsArmy)
                    {
                        continue;
                    }
                    foreach ((int X, int Y) anchor in area.Anchors)
                    {
                        if ((enemy.Position - _view.CentreOf(anchor.X, anchor.Y)).LengthSquared() <= threatSquared)
                        {
                            return true;
                        }
                    }
                    foreach (AiWallLine line in area.Lines)
                    {
                        foreach ((int X, int Y) tile in line.Tiles)
                        {
                            if ((enemy.Position - _view.CentreOf(tile.X, tile.Y)).LengthSquared() <= threatSquared)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            /// <summary>True when the style wants a wall (not the gate, not the open funnel gap) on this tile of the line.</summary>
            /// <param name="line">The line.</param>
            /// <param name="tile">A tile of the line. Unit: tile coordinates.</param>
            /// <param name="style">Funnel or sealed.</param>
            private bool WallWanted(AiWallLine line, (int X, int Y) tile, WallStyle style)
            {
                if (line.Gate == null)
                {
                    return true;
                }
                if (style == WallStyle.Sealed)
                {
                    return tile != line.Gate.Value;
                }
                // Chebyshev distance from the gate tile. Unit: tiles. Range: 0 to wall_line_max_tiles.
                int distance = Math.Max(Math.Abs(tile.X - line.Gate.Value.X), Math.Abs(tile.Y - line.Gate.Value.Y));
                return distance > _profile.WallFunnelGapTiles;
            }

            /// <summary>True when a wanted tile still needs an order: no own wall or pending order on it, not reserved this think, not refused for good and not blocked.</summary>
            /// <param name="tile">The tile. Unit: tile coordinates.</param>
            /// <param name="taken">Tile indices holding or about to hold an own wall.</param>
            private bool TileMissing((int X, int Y) tile, SortedSet<int> taken)
            {
                // Tile index. Unit: tile index. Range: 0 to tile count - 1.
                int index = _view.IndexOf(tile.X, tile.Y);
                return !taken.Contains(index) && !_reservedSites.Contains(index) && !_memory.WallTilesRefused.Contains(index) && !_view.IsBlocked(tile.X, tile.Y);
            }

            /// <summary>The defence ceiling (B.1.2) for an amount: false when spending <paramref name="cost"/> more would take the defence queue's share of all spending above budget_defence_max_spend_share.</summary>
            /// <param name="cost">Common plus rare. Unit: resource units. Range: 0 upward.</param>
            private bool DefenceAllowsCost(int cost)
            {
                // Cumulative spending of the defence queue and of all queues, common plus rare. Unit: resource units. Range: 0 upward.
                long defence = _memory.BudgetSpent[AiBudget.Cell(AiBudget.Defence, AiBudget.Common)] + _memory.BudgetSpent[AiBudget.Cell(AiBudget.Defence, AiBudget.Rare)];
                long total = AiBudget.Sum(_memory.BudgetSpent, AiBudget.Common) + AiBudget.Sum(_memory.BudgetSpent, AiBudget.Rare);
                // The ceiling as parts per 10 000. Unit: parts per 10 000. Range: 0 to 10000.
                int sharePt = (_profile.BudgetDefenceMaxSpendShare * Fix64.FromInt(10000)).RoundToInt();
                return AiBudget.DefenceAllowed(defence, total, cost, sharePt);
            }

            /// <summary>True when one more wall item of <paramref name="definition"/> is payable now and within the spending cap, after <paramref name="already"/> items of the same order.</summary>
            /// <param name="definition">Wall or gate.</param>
            /// <param name="already">Items already counted in this order. Unit: items. Range: 0 upward.</param>
            private bool CanBuyOneMore(UnitDefinition definition, int already)
            {
                // Cost of already + 1 items. Unit: resource units. Range: 0 upward.
                int common = (already + 1) * definition.CostCommon;
                int rare = (already + 1) * definition.CostRare;
                if (_memory.WallSpentCommon + common > _profile.WallMaxSpendCommon)
                {
                    return false;
                }
                if (_bq)
                {
                    return BqCanPay(AiBudget.Defence, common, rare) && DefenceAllowsCost(common + rare);
                }
                return (common == 0 || _common - _reserveCommon >= common) && (rare == 0 || _rare - _reserveRare >= rare);
            }

            /// <summary>
            /// Orders the walls of one line: the longest prefix of <paramref name="missing"/> the free money and the cap pay, built by the nearest free worker; sends that worker to look first when the first tile was never explored.
            /// Returns 1 when the line was handled (ordered, or the worker sent to explore it) and -1 when rule WL must stop for this think (nothing payable, no free worker, command cap).
            /// </summary>
            /// <param name="wall">The wall definition.</param>
            /// <param name="missing">The missing wall tiles in line order. Range: 1 to wall_line_max_tiles entries.</param>
            private int OrderLine(UnitDefinition wall, List<(int X, int Y)> missing)
            {
                if (_bq && BqBlocked(AiBudget.Defence))
                {
                    return -1;
                }
                // Tiles the money pays, from the start. Unit: tiles. Range: 0 to missing.Count.
                int n = 0;
                while (n < missing.Count && CanBuyOneMore(wall, n))
                {
                    n++;
                }
                if (n == 0)
                {
                    return -1;
                }
                // The nearest free worker to the first tile, not picked this think; none stops the rule.
                AiUnitView? builder = PickBuilder(_view.CentreOf(missing[0].X, missing[0].Y));
                if (builder == null)
                {
                    return -1;
                }
                if (!_view.IsExplored(missing[0].X, missing[0].Y))
                {
                    // The simulation refuses tiles the team never saw (site_unexplored): walk there first; the line waits.
                    if (!Emit(AiIntent.Move(builder.Id, _view.CentreOf(missing[0].X, missing[0].Y))))
                    {
                        return -1;
                    }
                    _pickedBuilders.Add(builder.Id);
                    return 1;
                }
                // The tiles ordered, in line order.
                List<(int X, int Y)> tiles = missing.GetRange(0, n);
                if (!Emit(AiIntent.BuildWallLine(builder.Id, wall.Id, tiles)))
                {
                    return -1;
                }
                // Whole cost of the order. Unit: resource units. Range: 0 upward.
                int common = n * wall.CostCommon;
                int rare = n * wall.CostRare;
                if (_bq)
                {
                    BqPay(AiBudget.Defence, common, rare);
                }
                _common -= common;
                _rare -= rare;
                _memory.WallSpentCommon += common;
                _pickedBuilders.Add(builder.Id);
                foreach ((int X, int Y) tile in tiles)
                {
                    _reservedSites.Add(_view.IndexOf(tile.X, tile.Y));
                }
                return 1;
            }

            /// <summary>
            /// Orders the gate of a line through <see cref="TryBuild"/> when the free money and the cap pay it now. Returns false when rule WL must stop for this think (the gate is not payable); true otherwise, whether or not
            /// TryBuild emitted (it may wait for a worker, a retry timer or exploration).
            /// </summary>
            /// <param name="gate">The gate definition.</param>
            /// <param name="tile">The gate tile. Unit: tile coordinates.</param>
            private bool OrderGate(UnitDefinition gate, (int X, int Y) tile)
            {
                if (_bq && BqBlocked(AiBudget.Defence))
                {
                    return false;
                }
                if (!CanBuyOneMore(gate, 0))
                {
                    return false;
                }
                if (TryBuild(gate, tile, false, AiBudget.Defence))
                {
                    _memory.WallSpentCommon += gate.CostCommon;
                }
                return true;
            }
        }
    }
}
