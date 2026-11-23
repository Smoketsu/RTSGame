// rts/src/Ai/AiBrainRaids.cs
// Raid sites and raid-aware workers of the skirmish AI (rts/docs/specs/ai_production_defence.md packages PD-3: rule RS, with use_raid_workers or use_raid_response, and rule RW, only with use_raid_workers; PD-4: the raid helpers of rule RA). Part of Rts.Ai (a partial of AiBrain's Turn).
// Rule RS (Raids) turns hits on own workers and structures, and worker losses, near an own drop-off into raid sites held in AiMemory.RaidHits; a raid is recognised from hits and losses only, never from a sighting.
// Rule RW closes the fields near an active raid: no worker is sent to one or trained for one (CapOf is 0), workers on one are ordered to an open field or home (EvacuateClosedFields), at most raid_evacuate_max_commands_per_think per think.
// Revision 2 (R2.1): fields stay closed for the whole raid (RaidClosedFields), visible raiders keep an existing raid alive (RaidSeenTick), hits on units that died between thinks count (RaidCarriedHits), the defence answers one raid at a time (RaidOrder, RaidAnswered), and the common-full test is redone after the scan.
// Reads only the view, the profile and memory (the AI's own damage reports and own worker positions). No random draw. Integers and Fix64 only; own units, drop-offs and fields walked in ascending ID or index order.
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
            /// Rule RS (spec section 5.1): records raid sites from this think's hits on own workers and structures and worker losses, drops expired ones, and remembers the own workers for the next think.
            /// Runs only with use_raid_workers or use_raid_response. Changes only AiMemory.RaidHits, RaidClosedFields, RaidSeenTick, RaidCarriedHits, RaidAnswered, OwnWorkerPositions and RaidScanTick, and the turn's common-full flag.
            /// </summary>
            public void Raids()
            {
                if (!_profile.UseRaidWorkers && !_profile.UseRaidResponse)
                {
                    return;
                }
                // The tick of this think. Unit: ticks. Range: 0 upward.
                int now = _view.Tick;
                // Own workers in the view, by ID (ascending).
                SortedSet<EntityId> workerIds = new SortedSet<EntityId>();
                // Own army units in the view, by ID (ascending).
                SortedSet<EntityId> armyIds = new SortedSet<EntityId>();
                // Every own unit in the view, by ID (ascending); a raided drop-off that is not in it is gone.
                SortedSet<EntityId> ownIds = new SortedSet<EntityId>();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    ownIds.Add(unit.Id);
                    if (unit.IsWorker)
                    {
                        workerIds.Add(unit.Id);
                    }
                    else if (unit.IsArmy)
                    {
                        armyIds.Add(unit.Id);
                    }
                }
                // Hits (tick and position of the own unit), then losses (this tick and the last known position): the spec's order, each group by ascending unit ID.
                // Raid events of this think: Tick is the tick of the hit or loss (ticks, 0 to now), Position the own unit's position (tiles, inside the map).
                List<(int Tick, FixVec2 Position)> events = new List<(int Tick, FixVec2 Position)>();
                foreach (KeyValuePair<EntityId, DamageReport> pair in _memory.RecentDamage)
                {
                    if (pair.Value.Tick > _memory.RaidScanTick && !armyIds.Contains(pair.Key))
                    {
                        events.Add((pair.Value.Tick, pair.Value.Position));
                    }
                }
                // Revision 2 (R2.1.3): hits on non-army units that died between thinks, in the intel prune's order, then emptied.
                // shortcut: a unit killed by its first hit leaves no report (ReadEvents drops hits on removed units), so a worker new since the last think and killed by one hit stays uncounted; fix by reading the kill event's position if one is ever added.
                events.AddRange(_memory.RaidCarriedHits);
                _memory.RaidCarriedHits.Clear();
                foreach (KeyValuePair<EntityId, FixVec2> pair in _memory.OwnWorkerPositions)
                {
                    if (!workerIds.Contains(pair.Key))
                    {
                        events.Add((now, pair.Value));
                    }
                }
                _memory.OwnWorkerPositions.Clear();
                foreach (AiUnitView unit in _view.OwnUnits)
                {
                    if (unit.IsWorker)
                    {
                        _memory.OwnWorkerPositions[unit.Id] = unit.Position;
                    }
                }
                // Own drop-offs, ascending ID.
                List<AiUnitView> dropOffs = DropOffs();
                // Squared defend radius a raid event must be within of a drop-off. Unit: squared tiles. Range: 1 to 900.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                // Deconstructed per event: tick of the hit or loss (ticks, 0 to now) and its position (tiles, inside the map).
                foreach ((int tick, FixVec2 position) in events)
                {
                    // Nearest drop-off within the radius (ties to the lower ID, which comes first), or null.
                    AiUnitView? nearest = null;
                    // Squared distance of the nearest drop-off. Unit: squared tiles. Range: 0 to the squared radius.
                    Fix64 nearestSquared = Fix64.Zero;
                    foreach (AiUnitView dropOff in dropOffs)
                    {
                        // Squared distance from this drop-off to the event. Unit: squared tiles. Range: 0 upward.
                        Fix64 squared = (dropOff.Position - position).LengthSquared();
                        if (squared <= radiusSquared && (nearest == null || squared < nearestSquared))
                        {
                            nearest = dropOff;
                            nearestSquared = squared;
                        }
                    }
                    // Out variable: the drop-off's stored newest raid hit (Tick in ticks, 0 to now; Position in tiles), set only when one exists.
                    if (nearest != null && (!_memory.RaidHits.TryGetValue(nearest.Id, out (int Tick, FixVec2 Position) stored) || tick >= stored.Tick))
                    {
                        _memory.RaidHits[nearest.Id] = (tick, position);
                    }
                }
                // Squared raid_field_radius_tiles: a field or sighting this near a raid position belongs to the raid (revision 2). Unit: squared tiles. Range: 1 to 900.
                Fix64 fieldRadiusSquared = _profile.RaidFieldRadiusTiles * _profile.RaidFieldRadiusTiles;
                // Rule RS (R2.1.1): every field within the radius of an active raid's newest position stays closed for the raid's life.
                foreach (KeyValuePair<EntityId, (int Tick, FixVec2 Position)> raid in _memory.RaidHits)
                {
                    // Field indices this raid has closed so far, as a sorted set, created empty when the raid has none yet. Unit: field index. Range: 0 to the field count minus 1.
                    if (!_memory.RaidClosedFields.TryGetValue(raid.Key, out SortedSet<int>? closed))
                    {
                        closed = new SortedSet<int>();
                        _memory.RaidClosedFields.Add(raid.Key, closed);
                    }
                    foreach (AiFieldView field in _view.Fields)
                    {
                        if ((field.Centre - raid.Value.Position).LengthSquared() <= fieldRadiusSquared)
                        {
                            closed.Add(field.Index);
                        }
                    }
                }
                // Rule RS (R2.1.2): a visible enemy army unit in an active raid's area keeps that raid alive; it never creates or moves a raid.
                foreach (KeyValuePair<EntityId, (int Tick, FixVec2 Position)> raid in _memory.RaidHits)
                {
                    // True once a visible raider is found in this raid's area. Unit: boolean.
                    bool seen = false;
                    foreach (AiUnitView enemy in _view.VisibleEnemies)
                    {
                        if (!enemy.IsArmy)
                        {
                            continue;
                        }
                        seen = (enemy.Position - raid.Value.Position).LengthSquared() <= fieldRadiusSquared;
                        foreach (int index in _memory.RaidClosedFields[raid.Key])
                        {
                            seen |= (enemy.Position - _view.Fields[index].Centre).LengthSquared() <= fieldRadiusSquared;
                        }
                        if (seen)
                        {
                            break;
                        }
                    }
                    if (seen)
                    {
                        _memory.RaidSeenTick[raid.Key] = now;
                    }
                }
                // Oldest tick an active raid may have; a raid with a lower tick has expired. Unit: ticks. Range: any int.
                int oldest = now - _view.SecondsToTicks(_profile.RaidActiveSeconds);
                // Entries to remove: the drop-off is gone, or the raid expired.
                // Drop-off IDs whose raid entry is removed this think. Unit: entity IDs. Range: 0 to the number of RaidHits entries.
                List<EntityId> expired = new List<EntityId>();
                foreach (KeyValuePair<EntityId, (int Tick, FixVec2 Position)> pair in _memory.RaidHits)
                {
                    if (!ownIds.Contains(pair.Key) || (pair.Value.Tick < oldest && (!_memory.RaidSeenTick.TryGetValue(pair.Key, out int seenTick) || seenTick < oldest)))
                    {
                        expired.Add(pair.Key);
                    }
                }
                foreach (EntityId id in expired)
                {
                    _memory.RaidHits.Remove(id);
                    _memory.RaidSeenTick.Remove(id);
                    _memory.RaidClosedFields.Remove(id);
                    if (_memory.RaidAnswered == id)
                    {
                        _memory.RaidAnswered = EntityId.None;
                    }
                }
                _memory.RaidScanTick = now;
                if (_profile.UseRaidWorkers && _profile.RareCapYieldsToSaturation)
                {
                    // Revision 2 (R2.1.5): the caps now read this think's raid state.
                    UpdateCommonFull();
                }
            }

            /// <summary>Rule RA step 1: the visible enemy army units within defend_radius_tiles of <paramref name="point"/>, ascending ID.</summary>
            /// <param name="point">The raid position. Unit: tiles.</param>
            private List<AiUnitView> ThreatsNear(FixVec2 point)
            {
                // Units found.
                List<AiUnitView> near = new List<AiUnitView>();
                // Squared defend radius. Unit: squared tiles. Range: 1 to 900.
                Fix64 radiusSquared = _profile.DefendRadiusTiles * _profile.DefendRadiusTiles;
                foreach (AiUnitView enemy in _view.VisibleEnemies)
                {
                    if (enemy.IsArmy && (enemy.Position - point).LengthSquared() <= radiusSquared)
                    {
                        near.Add(enemy);
                    }
                }
                return near;
            }

            /// <summary>
            /// Rule RA step 2: true when the own HQ is Rooted, complete and armed and <paramref name="centre"/> is within its weapon range plus raid_hq_cover_margin_tiles (squared), so it joins the fixed defending side.
            /// </summary>
            /// <param name="centre">The threat centre. Unit: tiles.</param>
            private bool HqCovers(FixVec2 centre)
            {
                if (_hq.HqState != HqState.Rooted || _hq.IsUnderConstruction || _hq.Definition.Weapon == null)
                {
                    return false;
                }
                // Reach of the HQ's weapon plus the margin. Unit: tiles. Range: 0 to about 40.
                Fix64 reach = _hq.Definition.Weapon.RangeTiles + _profile.RaidHqCoverMarginTiles;
                return (centre - _hq.Position).LengthSquared() <= reach * reach;
            }

            /// <summary>Rule RW: true when the switch is on and some active raid's position is within raid_field_radius_tiles of <paramref name="point"/> (squared Fix64). Unit: boolean.</summary>
            /// <param name="point">A point on the map. Unit: tiles.</param>
            private bool NearRaid(FixVec2 point)
            {
                if (!_profile.UseRaidWorkers || _memory.RaidHits.Count == 0)
                {
                    return false;
                }
                // Squared closing radius. Unit: squared tiles. Range: 1 to 900.
                Fix64 radiusSquared = _profile.RaidFieldRadiusTiles * _profile.RaidFieldRadiusTiles;
                foreach (KeyValuePair<EntityId, (int Tick, FixVec2 Position)> raid in _memory.RaidHits)
                {
                    if ((raid.Value.Position - point).LengthSquared() <= radiusSquared)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>Rule RW: true when the field is closed, that is an active raid holds it in AiMemory.RaidClosedFields (a raid keeps every field it closed until it ends). Unit: boolean.</summary>
            /// <param name="field">Any field.</param>
            private bool IsClosed(AiFieldView field)
            {
                if (!_profile.UseRaidWorkers)
                {
                    return false;
                }
                foreach (SortedSet<int> closed in _memory.RaidClosedFields.Values)
                {
                    if (closed.Contains(field.Index))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Rule RA step 1 (revision 2, R2.1.4): the active raids in answer order: the raid <see cref="AiMemory.RaidAnswered"/> names first if still active, then the others by hit tick descending, ties to the lower drop-off ID.
            /// </summary>
            private List<KeyValuePair<EntityId, (int Tick, FixVec2 Position)>> RaidOrder()
            {
                // The raids, ascending drop-off ID.
                List<KeyValuePair<EntityId, (int Tick, FixVec2 Position)>> order = new List<KeyValuePair<EntityId, (int Tick, FixVec2 Position)>>(_memory.RaidHits);
                // Total order (deterministic although List.Sort is not stable): the answered raid first, then newest hit tick first, then lower ID.
                order.Sort((a, b) =>
                {
                    // Whether each raid is the one answered last think. Unit: boolean.
                    bool aa = a.Key == _memory.RaidAnswered;
                    bool ab = b.Key == _memory.RaidAnswered;
                    if (aa != ab)
                    {
                        return aa ? -1 : 1;
                    }
                    return a.Value.Tick != b.Value.Tick ? b.Value.Tick.CompareTo(a.Value.Tick) : a.Key.CompareTo(b.Key);
                });
                return order;
            }

            /// <summary>
            /// Rule RW step 3: orders each own worker (ascending ID, no build order) that gathers on a closed field to the nearest open field below its cap, else the nearest open field, else (when the home point is not near a raid) home;
            /// at most raid_evacuate_max_commands_per_think orders; updates <paramref name="assigned"/>.
            /// </summary>
            /// <param name="assigned">Workers on each field by field index; updated for the orders given.</param>
            private void EvacuateClosedFields(int[] assigned)
            {
                if (!_profile.UseRaidWorkers || _memory.RaidHits.Count == 0)
                {
                    return;
                }
                // Orders still allowed this think. Unit: commands. Range: 0 to raid_evacuate_max_commands_per_think.
                int budget = _profile.RaidEvacuateMaxCommandsPerThink;
                foreach (AiUnitView worker in _view.OwnUnits)
                {
                    if (budget <= 0)
                    {
                        return;
                    }
                    if (!worker.IsWorker || worker.HasBuildOrder || worker.GatherFieldIndex < 0 || worker.GatherFieldIndex >= assigned.Length || !IsClosed(_view.Fields[worker.GatherFieldIndex]))
                    {
                        continue;
                    }
                    // Open field to go to, or null when none is open.
                    AiFieldView? target = NearestField(worker.Position, assigned, true) ?? NearestField(worker.Position, assigned, false);
                    if (target != null)
                    {
                        if (Emit(AiIntent.Gather(worker.Id, target.Index)))
                        {
                            assigned[worker.GatherFieldIndex]--;
                            assigned[target.Index]++;
                            budget--;
                        }
                    }
                    else if (!NearRaid(Home()) && Emit(AiIntent.Move(worker.Id, Standable(Home()))))
                    {
                        assigned[worker.GatherFieldIndex]--;
                        budget--;
                    }
                }
            }
        }
    }
}
