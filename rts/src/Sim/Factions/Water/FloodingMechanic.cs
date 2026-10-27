// rts/src/Sim/Factions/Water/FloodingMechanic.cs
// Water faction signature mechanic: Flooding with tides and waves (spec: rts/docs/specs/flooding.md). Flood sources (units
// whose data gives flood_source_output_per_second) release water in drops onto the terrain grid; the water flows downhill
// in fixed passes in ascending tile index order; one shared tide on the match clock fills each team's sources up to a
// volume that rises from the low-tide fraction to the full capacity and back; floods stop short of enemy resource fields
// and drop-offs, keep building footprints dry and cannot cross walls; water-layer units heal on their own team's water;
// and a caster can send the first-pass wave (push plus extra water). Part of Rts.Sim.Factions.Water.
// The mechanic resolves flood-capable factions by mechanic_id "flooding" from the content catalog and names no faction.
// It changes terrain only through TerrainGrid.SetWater (so TerrainChangedEvent and the terrain Version fire) and
// reports TideTurnedEvent, WaveCastEvent, UnitPushedEvent and FloodWaterRemovedEvent. Determinism: all water maths is integer
// drops (FloodMath, FloodRules); sources, teams, tiles and units are visited in ascending order; no random numbers; the
// only state kept across ticks is the per-caster wave cooldown, which is written to the state hash. Tide level, no-flood
// masks and breadth-first distances are derived from hashed state each update and never stored.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Factions.Water
{
    /// <summary>Flooding plug-in: tide clock, flood sources, water flow, flood limits, healing and the wave.</summary>
    public sealed class FloodingMechanic : IFactionMechanic
    {
        /// <summary>Mechanic ID as named in data/factions/water.json.</summary>
        public const string MechanicId = "flooding";

        /// <summary>Reason text of the event for water removed inside the dry gap around an enemy resource field or drop-off.</summary>
        public const string ReasonResourceGap = "resource_gap";

        /// <summary>Reason text of the event for water a footprint pushed off a tile that no neighbour could take.</summary>
        public const string ReasonDisplacedOff = "displaced_off";

        /// <summary>First tick a caster may cast the wave again, per caster, ascending by unit ID. Hashed. Entries of removed casters are pruned at the start of every tick.</summary>
        private readonly SortedDictionary<EntityId, int> _nextWaveTick = new SortedDictionary<EntityId, int>();

        /// <summary>IDs of the factions whose mechanic_id is "flooding", filled at match start. Membership test only (never iterated for effect).</summary>
        private readonly SortedSet<string> _floodFactions = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Working copy of the terrain, reused by every update and wave so a second of play allocates little. Not state: refilled from the terrain before each use.</summary>
        private FloodField? _field;

        /// <summary>Length of one full tide cycle. Unit: ticks. Range: 2 upward (tide_cycle_seconds times the tick rate, 4200 at the defaults).</summary>
        public int TideTicks { get; private set; }

        /// <summary>Ticks between flood updates. Unit: ticks. Range: 1 upward (20 at the defaults).</summary>
        public int UpdateTicks { get; private set; }

        /// <summary>Drops that count as one height layer. Unit: drops. Range: 128 upward (512 at the defaults).</summary>
        public int LayerDrops { get; private set; }

        /// <summary>Most drops one tile holds. Unit: drops. Range: 128 upward (1024 at the defaults).</summary>
        public int MaxDrops { get; private set; }

        /// <summary>Drops a wave adds to each covered tile. Unit: drops. Range: 0 to 2048 (256 at the defaults).</summary>
        public int WaveDrops { get; private set; }

        /// <summary>Ticks between two waves of one caster. Unit: ticks. Range: 1 upward (600 at the defaults).</summary>
        public int WaveCooldownTicks { get; private set; }

        /// <summary>Health a water-layer unit regains per flood update on its own team's water. Unit: health points. Range: 0 to 250 (2 at the defaults).</summary>
        public Fix64 HealPerUpdate { get; private set; }

        /// <summary>Drops the tide may remove per update at the least (min_retreat_per_second as drops per update). Unit: drops. Range: 0 upward.</summary>
        private int _minRetreatDrops;

        /// <summary>Mechanic ID as named in faction data.</summary>
        public string Id => MechanicId;

        /// <summary>Number of casters with a wave cooldown entry. Range: 0 upward. For tests and the UI.</summary>
        public int CooldownEntryCount => _nextWaveTick.Count;

        /// <summary>
        /// Converts the tuning to ticks and drops (pure functions of tuning, not hashed) and finds which factions use this
        /// mechanic. Called once before tick 0.
        /// </summary>
        /// <param name="sim">The simulation being started.</param>
        public void OnMatchStart(Simulation sim)
        {
            // Flood tuning: ranges and rates read by this method. Unit: see FloodTuning.
            FloodTuning flood = sim.Tuning.Flood;
            // The match clock, for second to tick conversions.
            MatchClock clock = sim.Clock;
            TideTicks = sim.Tuning.Match.TideCycleTicks(clock);
            UpdateTicks = flood.FloodUpdateIntervalTicks(clock);
            LayerDrops = FloodMath.RoundToDrops(flood.DepthUnitsPerHeightLayer);
            MaxDrops = FloodMath.RoundToDrops(sim.Tuning.Terrain.MaxWaterDepth);
            WaveDrops = FloodMath.RoundToDrops(flood.WaveWaterDepth);
            WaveCooldownTicks = flood.WaveCooldownTicks(clock);
            HealPerUpdate = flood.WaterUnitHealPerSecond * Fix64.FromInt(UpdateTicks) / Fix64.FromInt(clock.TicksPerSecond);
            _minRetreatDrops = DropsPerUpdate(sim, flood.MinRetreatPerSecond);
            _floodFactions.Clear();
            // Faction entry in ascending ID order; its key is the faction ID.
            foreach (KeyValuePair<string, FactionDefinition> pair in sim.Content.Factions)
            {
                if (string.Equals(pair.Value.MechanicId, MechanicId, StringComparison.Ordinal))
                {
                    _floodFactions.Add(pair.Key);
                }
            }
            _nextWaveTick.Clear();
            _field = new FloodField(sim.Terrain.Width, sim.Terrain.Height, LayerDrops, MaxDrops);
        }

        /// <summary>
        /// Runs every tick after movement and combat: prunes cooldowns of removed casters, announces the tide turning, and
        /// on every <see cref="UpdateTicks"/>th tick runs one flood update (limits, tide, flow, write-back, healing).
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        public void OnTick(Simulation sim)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("FloodingMechanic.OnTick can only run inside Simulation.Step.");
            }
            // Current tick. Unit: ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            PruneCooldowns(sim);
            if (TideClock.IsLowTurn(tick, TideTicks))
            {
                sim.Events.Add(new TideTurnedEvent(tick, TidePhase.Low, tick / TideTicks));
            }
            if (TideClock.IsHighTurn(tick, TideTicks))
            {
                sim.Events.Add(new TideTurnedEvent(tick, TidePhase.High, tick / TideTicks));
            }
            if (tick % UpdateTicks == 0)
            {
                Update(sim, tick);
            }
        }

        /// <summary>
        /// Feeds the mechanic's state into the state hash: the ID, the number of wave cooldowns, then per caster in ascending
        /// ID order the caster ID and the next tick it may cast. Everything else is derived and not hashed.
        /// </summary>
        /// <param name="hasher">The hasher being fed.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
            hasher.Write(_nextWaveTick.Count);
            // Cooldown entry in ascending caster ID: key caster, value next tick the caster may cast. Unit: ticks.
            foreach (KeyValuePair<EntityId, int> pair in _nextWaveTick)
            {
                hasher.Write(pair.Key);
                hasher.Write(pair.Value);
            }
        }

        /// <summary>Tide level at a tick as a fraction of high-tide volume (see <see cref="TideClock.Level"/>). Unit: fraction. Range: low_tide_volume_fraction to 1.</summary>
        /// <param name="sim">The simulation (for the tuning).</param>
        /// <param name="tick">Match tick. Unit: ticks. Range: 0 upward.</param>
        public Fix64 TideLevel(Simulation sim, int tick)
        {
            return TideClock.Level(tick, TideTicks, sim.Tuning.Flood.LowTideVolumeFraction);
        }

        /// <summary>True when the faction's mechanic_id is "flooding".</summary>
        /// <param name="factionId">A faction ID.</param>
        public bool IsFloodFaction(string factionId)
        {
            return _floodFactions.Contains(factionId);
        }

        /// <summary>The first tick the caster may cast again, or false when it has not cast yet. Unit: ticks. Range: 0 upward.</summary>
        /// <param name="caster">The casting unit.</param>
        /// <param name="nextTick">The tick when found.</param>
        public bool TryGetNextWaveTick(EntityId caster, out int nextTick)
        {
            return _nextWaveTick.TryGetValue(caster, out nextTick);
        }

        /// <summary>
        /// Turns a per-second rate of depth-unit tiles into drops per update: (rate * 256 * UpdateTicks / TicksPerSecond), rounded
        /// down, left to right.
        /// </summary>
        /// <param name="sim">The simulation (for the clock).</param>
        /// <param name="perSecond">Rate. Unit: depth-unit tiles per second. Range: 0 to 64.</param>
        private int DropsPerUpdate(Simulation sim, Fix64 perSecond)
        {
            return (perSecond * Fix64.FromInt(FloodMath.DropsPerDepthUnit) * Fix64.FromInt(UpdateTicks) / Fix64.FromInt(sim.Clock.TicksPerSecond)).FloorToInt();
        }

        /// <summary>Removes the cooldown entries of casters that no longer exist, in ascending key order.</summary>
        private void PruneCooldowns(Simulation sim)
        {
            if (_nextWaveTick.Count == 0)
            {
                return;
            }
            // Entries whose caster is gone.
            List<EntityId> gone = new List<EntityId>();
            // Caster ID with a cooldown entry, ascending.
            foreach (EntityId id in _nextWaveTick.Keys)
            {
                if (!sim.Units.Contains(id))
                {
                    gone.Add(id);
                }
            }
            // Caster ID whose entry is removed, ascending.
            foreach (EntityId id in gone)
            {
                _nextWaveTick.Remove(id);
            }
        }

        /// <summary>One flood source for the current update.</summary>
        private struct SourceInfo
        {
            /// <summary>The source unit's tile. Unit: tile index.</summary>
            public int Tile;

            /// <summary>Drops the source releases per update at most. Unit: drops. Range: 0 upward.</summary>
            public int OutputDrops;

            /// <summary>The source's share of the team's high-tide volume. Unit: drops. Range: 1 upward.</summary>
            public int CapacityDrops;
        }

        /// <summary>The team of a player, or None for an unknown player.</summary>
        private static TeamId TeamOfOwner(Simulation sim, PlayerId owner)
        {
            return sim.Players.TryGet(owner, out Player? player) ? player.Team : TeamId.None;
        }

        /// <summary>True when the unit currently releases water: a complete unit of a flooding faction with output above 0; an HQ only while Rooted.</summary>
        private bool IsActiveSource(Simulation sim, Unit unit)
        {
            if (!unit.Definition.Flood.IsSource || !unit.Owner.IsValid || unit.IsUnderConstruction)
            {
                return false;
            }
            if (unit.Definition.Hq != null && unit.HqState != HqState.Rooted)
            {
                return false;
            }
            return sim.Players.TryGet(unit.Owner, out Player? player) && _floodFactions.Contains(player.FactionId);
        }

        /// <summary>Copies the terrain into the reusable working field and clears its no-flood masks.</summary>
        private FloodField ReadField(Simulation sim)
        {
            // The live terrain grid this method reads and (through SetWater) changes.
            TerrainGrid terrain = sim.Terrain;
            // The reusable working field this method fills.
            FloodField field = _field ?? throw new InvalidOperationException("The flooding mechanic has not started.");
            // Tile index being copied. Unit: tile index. Range: 0 to TileCount - 1.
            for (int i = 0; i < field.TileCount; i++)
            {
                // The tile being copied. Unit: one tile.
                TerrainTile tile = terrain[i];
                field.LayerHeight[i] = tile.Height;
                field.Wall[i] = (tile.Blockers & TileBlockers.Wall) != 0;
                field.Footprint[i] = (tile.Blockers & TileBlockers.Footprint) != 0;
                field.Drops[i] = FloodMath.ToDrops(tile.WaterDepth);
                // Owner of the water. Water another system left unowned stays unowned (never given away) until a team's flow claims the tile.
                field.Team[i] = field.Drops[i] > 0 && tile.WaterTeam.IsValid ? tile.WaterTeam.Value : FloodField.NoTeam;
            }
            // Team slot whose no-flood mask is cleared. Unit: team number. Range: 0 to TeamSlots - 1.
            for (int team = 0; team < FloodField.TeamSlots; team++)
            {
                field.NoFlood[team] = null;
            }
            return field;
        }

        /// <summary>
        /// Fills the no-flood masks of every team that could flood: for team T the anchors are every drop-off of a player on
        /// another team plus every resource field within resource_claim_radius_tiles (Chebyshev) of such a drop-off, and the
        /// mask is every tile within enemy_resource_gap_tiles of an anchor. Teams without an enemy anchor get no mask.
        /// </summary>
        private void BuildMasks(Simulation sim, FloodField field)
        {
            // Flood tuning: ranges and rates read by this method. Unit: see FloodTuning.
            FloodTuning flood = sim.Tuning.Flood;
            // The live terrain grid, read for unit tiles.
            TerrainGrid terrain = sim.Terrain;
            // Drop-off anchor tiles per owner team, each list in ascending unit ID order.
            SortedDictionary<int, List<int>> anchorsByTeam = new SortedDictionary<int, List<int>>();
            // Unit checked for being a drop-off, ascending ID.
            foreach (Unit unit in sim.Units.All)
            {
                if (!EconomyRules.IsDropOff(unit))
                {
                    continue;
                }
                // Team of the drop-off's owner and the tile it stands on.
                TeamId team = TeamOfOwner(sim, unit.Owner);
                if (!team.IsValid)
                {
                    continue;
                }
                if (!anchorsByTeam.TryGetValue(team.Value, out List<int>? anchors))
                {
                    anchors = new List<int>();
                    anchorsByTeam.Add(team.Value, anchors);
                }
                // The footprint is the unit's own tile (single-tile structures and mobile HQs in Phase 1).
                int tile = terrain.TileOf(unit.Position);
                anchors.Add(tile);
                // Resource fields within the claim radius of this drop-off count as enemy resource fields to everyone else.
                IReadOnlyList<ResourceField> fields = sim.Map.ResourceFields;
                // Index of the resource field being tested. Unit: list position. Range: 0 to field count - 1.
                for (int f = 0; f < fields.Count; f++)
                {
                    if (Math.Max(Math.Abs(fields[f].X - terrain.XOf(tile)), Math.Abs(fields[f].Y - terrain.YOf(tile))) <= flood.ResourceClaimRadiusTiles)
                    {
                        anchors.Add(terrain.IndexOf(fields[f].X, fields[f].Y));
                    }
                }
            }
            // Whole tiles of the dry gap: distances are whole, so "distance <= gap" equals "distance <= floor(gap)".
            int gap = flood.EnemyResourceGapTiles.FloorToInt();
            // Team that may flood, ascending. Range: 0 to 254.
            foreach (int team in FloodingTeams(sim, field))
            {
                // Anchors of all other teams, in ascending team then unit order.
                List<int> enemyAnchors = new List<int>();
                // Team entry in ascending team order: key team, value its anchor tiles.
                foreach (KeyValuePair<int, List<int>> pair in anchorsByTeam)
                {
                    if (pair.Key != team)
                    {
                        enemyAnchors.AddRange(pair.Value);
                    }
                }
                field.NoFlood[team] = enemyAnchors.Count == 0 ? null : FloodRules.NoFloodMask(field.Width, field.Height, enemyAnchors, gap);
            }
        }

        /// <summary>Teams that may flood in this match, ascending: the teams of players in flooding factions and every team that owns water now.</summary>
        private SortedSet<int> FloodingTeams(Simulation sim, FloodField field)
        {
            // Team IDs found, ascending. Range: 0 to 254.
            SortedSet<int> teams = new SortedSet<int>();
            // Player in ascending ID order.
            foreach (Player player in sim.Players.All)
            {
                if (_floodFactions.Contains(player.FactionId))
                {
                    teams.Add(player.Team.Value);
                }
            }
            // Tile index being checked for water. Unit: tile index. Range: 0 to TileCount - 1.
            for (int i = 0; i < field.TileCount; i++)
            {
                if (field.Team[i] != FloodField.NoTeam)
                {
                    teams.Add(field.Team[i]);
                }
            }
            return teams;
        }

        /// <summary>The active flood sources of one team in ascending unit ID order; the caller resolves their release tiles.</summary>
        private List<SourceInfo> SourcesOf(Simulation sim, int team)
        {
            // Result list, in ascending unit ID because the registry iterates that way.
            List<SourceInfo> sources = new List<SourceInfo>();
            // Unit tested as a flood source, ascending ID.
            foreach (Unit unit in sim.Units.All)
            {
                if (!IsActiveSource(sim, unit) || TeamOfOwner(sim, unit.Owner).Value != team)
                {
                    continue;
                }
                // This source: its tile, per-update output and capacity.
                SourceInfo info = new SourceInfo
                {
                    Tile = sim.Terrain.TileOf(unit.Position),
                    OutputDrops = DropsPerUpdate(sim, unit.Definition.Flood.OutputPerSecond),
                    CapacityDrops = FloodMath.RoundToDrops(unit.Definition.Flood.Capacity),
                };
                sources.Add(info);
            }
            return sources;
        }

        /// <summary>One flood update: limits, tide, redistribution, write-back, events and healing, in the spec's order.</summary>
        private void Update(Simulation sim, int tick)
        {
            // Flood tuning: ranges and rates read by this method. Unit: see FloodTuning.
            FloodTuning flood = sim.Tuning.Flood;
            // The live terrain grid that receives the written-back water.
            TerrainGrid terrain = sim.Terrain;
            // The working field for this update.
            FloodField field = ReadField(sim);
            // Water state before this update, to find the changed tiles at the end.
            int[] originalDrops = (int[])field.Drops.Clone();
            int[] originalTeam = (int[])field.Team.Clone();
            BuildMasks(sim, field);
            // Teams that may flood, ascending. Range: 0 to 254.
            SortedSet<int> teams = FloodingTeams(sim, field);
            // Drops removed per team by the limits step, for the two removal reasons.
            SortedDictionary<int, int> gapRemoved = new SortedDictionary<int, int>();
            // Same, for water lost because a footprint pushed it off a tile. Unit: drops.
            SortedDictionary<int, int> displacedRemoved = new SortedDictionary<int, int>();

            // Step 2: limits, ascending tile index.
            for (int i = 0; i < field.TileCount; i++)
            {
                if (field.Drops[i] <= 0)
                {
                    continue;
                }
                // Owner of the water on the tile; unowned water is left alone.
                int team = field.Team[i];
                if (team == FloodField.NoTeam)
                {
                    continue;
                }
                if (field.Footprint[i])
                {
                    // Water the footprint pushed off the map (no neighbour could take it).
                    int lost = FloodRules.Displace(field, i);
                    AddTo(displacedRemoved, team, lost);
                }
                else if (field.IsNoFlood(team, i))
                {
                    AddTo(gapRemoved, team, field.Drops[i]);
                    field.Drops[i] = 0;
                    field.Team[i] = FloodField.NoTeam;
                }
            }

            // Step 3: tide, per team ascending.
            Fix64 level = TideClock.Level(tick, TideTicks, flood.LowTideVolumeFraction);
            // Team whose tide is applied, ascending. Range: 0 to 254.
            foreach (int team in teams)
            {
                // This team's active flood sources, ascending unit ID.
                List<SourceInfo> sources = SourcesOf(sim, team);
                // Total capacity of the team's sources, its high-tide volume at tide level 1. Unit: drops.
                int capacity = 0;
                // Sum of the sources' per-update output. Unit: drops.
                int outputSum = 0;
                // Source of this team, ascending unit ID.
                foreach (SourceInfo info in sources)
                {
                    capacity += info.CapacityDrops;
                    outputSum += info.OutputDrops;
                }
                // Volume the team may hold now. Unit: drops.
                int allowed = (Fix64.FromInt(capacity) * level).FloorToInt();
                // Water the team holds now. Unit: drops. Range: 0 upward (long: many full tiles can pass int range on big maps).
                long held = 0;
                // Tile index being summed. Unit: tile index. Range: 0 to TileCount - 1.
                for (int i = 0; i < field.TileCount; i++)
                {
                    if (field.Team[i] == team)
                    {
                        held += field.Drops[i];
                    }
                }
                if (held < allowed)
                {
                    // Source of this team, ascending unit ID.
                    foreach (SourceInfo info in sources)
                    {
                        // Tile this source releases onto, -1 when none accepts. Unit: tile index.
                        int origin = FloodRules.Origin(field, info.Tile, team, flood.FloodSourceReleaseRadiusTiles);
                        if (origin < 0)
                        {
                            continue;
                        }
                        // Drops released by this source now: its output, the volume still missing and the room at the release tile.
                        long gain = Math.Min(info.OutputDrops, Math.Min(allowed - held, FloodRules.Room(field, origin)));
                        if (gain <= 0)
                        {
                            continue;
                        }
                        field.Drops[origin] += (int)gain;
                        field.Team[origin] = team;
                        held += gain;
                    }
                }
                else if (held > allowed)
                {
                    // Drops the tide takes back now: the excess, limited by the sources' output or the minimum retreat speed.
                    long retreat = Math.Min(held - allowed, Math.Max(outputSum, _minRetreatDrops));
                    // Release tiles where the retreat starts. Unit: tile indices. Range: 0 to TileCount - 1, in source order.
                    List<int> seeds = new List<int>();
                    // Source of this team, ascending unit ID.
                    foreach (SourceInfo info in sources)
                    {
                        // Tile this source releases onto, -1 when none accepts. Unit: tile index.
                        int origin = FloodRules.Origin(field, info.Tile, team, flood.FloodSourceReleaseRadiusTiles);
                        if (origin >= 0)
                        {
                            seeds.Add(origin);
                        }
                    }
                    FloodRules.Retreat(field, team, (int)retreat, seeds);
                }
            }

            // Step 4: redistribution.
            FloodRules.Redistribute(field, flood.FlowPassesPerUpdate);

            // Step 5: write back every changed tile, ascending, through the terrain mutator.
            for (int i = 0; i < field.TileCount; i++)
            {
                if (field.Drops[i] != originalDrops[i] || field.Team[i] != originalTeam[i])
                {
                    terrain.SetWater(sim, i, FloodMath.ToDepth(field.Drops[i]), field.Team[i] == FloodField.NoTeam ? TeamId.None : new TeamId((byte)field.Team[i]));
                }
            }
            // Gap-removed counter in ascending team: key team, value drops removed. Unit: drops.
            foreach (KeyValuePair<int, int> pair in gapRemoved)
            {
                if (pair.Value > 0)
                {
                    sim.Events.Add(new FloodWaterRemovedEvent(tick, new TeamId((byte)pair.Key), pair.Value, ReasonResourceGap));
                }
            }
            // Displaced-removed counter in ascending team: key team, value drops removed. Unit: drops.
            foreach (KeyValuePair<int, int> pair in displacedRemoved)
            {
                if (pair.Value > 0)
                {
                    sim.Events.Add(new FloodWaterRemovedEvent(tick, new TeamId((byte)pair.Key), pair.Value, ReasonDisplacedOff));
                }
            }

            // Step 6: healing of water-layer units on their own team's flooded tiles.
            Heal(sim);
        }

        /// <summary>Adds <paramref name="amount"/> to a per-team counter.</summary>
        private static void AddTo(SortedDictionary<int, int> counters, int team, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            counters.TryGetValue(team, out int current);
            counters[team] = current + amount;
        }

        /// <summary>Heals water-layer units (definition layer) with health above 0 and below max standing on shallow or deep water owned by their own team.</summary>
        private void Heal(Simulation sim)
        {
            if (HealPerUpdate <= Fix64.Zero)
            {
                return;
            }
            // The live terrain grid this method reads and (through SetWater) changes.
            TerrainGrid terrain = sim.Terrain;
            // Unit that may be healed, ascending ID.
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Definition.Layer != MovementLayer.Water || unit.Health <= Fix64.Zero || unit.Health >= unit.Definition.MaxHealth)
                {
                    continue;
                }
                // Tile under the unit and the owner's team.
                int tile = terrain.TileOf(unit.Position);
                // Team of the unit's owner, None for a neutral unit.
                TeamId team = TeamOfOwner(sim, unit.Owner);
                if (team.IsValid && terrain.DepthClass(tile) >= 1 && terrain[tile].WaterTeam == team)
                {
                    unit.SetHealth(unit.Health + HealPerUpdate);
                }
            }
        }

        /// <summary>
        /// True when the unit may cast the wave: its definition allows it and its owner's faction uses this mechanic.
        /// </summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="unit">The would-be caster.</param>
        public bool CanCast(Simulation sim, Unit unit)
        {
            return unit.Definition.Flood.CanCastWave && sim.Players.TryGet(unit.Owner, out Player? player) && _floodFactions.Contains(player.FactionId);
        }

        /// <summary>
        /// Casts the wave: adds water to the covered tiles that accept the caster's team (never a wall, footprint, enemy water
        /// or no-flood tile), shoves enemy non-air units standing in the area, announces it and starts the caster's cooldown.
        /// The caller (CastWaveCommand) has already checked every rejection rule.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="caster">The casting unit.</param>
        /// <param name="dirX">X step of the wave direction. Unit: tile step. Range: -1, 0 or 1.</param>
        /// <param name="dirY">Y step of the wave direction. Unit: tile step. Range: -1, 0 or 1 (not both 0).</param>
        internal void CastWave(Simulation sim, Unit caster, int dirX, int dirY)
        {
            if (!sim.IsStepping)
            {
                throw new InvalidOperationException("CastWave can only run inside Simulation.Step (through CastWaveCommand).");
            }
            // Flood tuning: ranges and rates read by this method. Unit: see FloodTuning.
            FloodTuning flood = sim.Tuning.Flood;
            // The live terrain grid the wave floods and reads.
            TerrainGrid terrain = sim.Terrain;
            // Tick of the cast and the caster's team.
            int tick = sim.Clock.Tick;
            // Team of the caster's owner; the water it adds belongs to it.
            TeamId team = TeamOfOwner(sim, caster.Owner);
            // The working field for this cast.
            FloodField field = ReadField(sim);
            BuildMasks(sim, field);
            // Caster tile coordinates and the half width of the wave. Unit: tiles.
            int tile = terrain.TileOf(caster.Position);
            // Caster's tile column. Unit: tiles. Range: 0 to Width - 1.
            int cx = terrain.XOf(tile);
            // Caster's tile row. Unit: tiles. Range: 0 to Height - 1.
            int cy = terrain.YOf(tile);
            // Half the wave width, rounded down. Unit: tiles. Range: 0 upward ((wave_width_tiles - 1) / 2).
            int half = (flood.WaveWidthTiles - 1) / 2;
            // Covered tiles in ascending index order, each once.
            SortedSet<int> area = new SortedSet<int>();
            // Distance along the wave direction. Unit: tiles. Range: 1 to wave_length_tiles.
            for (int s = 1; s <= flood.WaveLengthTiles; s++)
            {
                // Offset across the wave direction. Unit: tiles. Range: -half to half.
                for (int o = -half; o <= half; o++)
                {
                    // Tile at distance s along the direction and offset o across it.
                    int x = cx + s * dirX - o * dirY;
                    // Row of the covered tile. Unit: tiles. Range: may fall off the map; Contains checks.
                    int y = cy + s * dirY + o * dirX;
                    if (terrain.Contains(x, y))
                    {
                        area.Add(terrain.IndexOf(x, y));
                    }
                }
            }
            // Tiles that gained water.
            int flooded = 0;
            // Covered tile index, ascending. Unit: tile index.
            foreach (int index in area)
            {
                if (!FloodRules.Accepts(field, index, team.Value, null))
                {
                    continue;
                }
                // Drops added: a full wave depth or whatever room is left.
                int gain = Math.Min(WaveDrops, FloodRules.Room(field, index));
                if (gain <= 0)
                {
                    continue;
                }
                field.Drops[index] += gain;
                field.Team[index] = team.Value;
                terrain.SetWater(sim, index, FloodMath.ToDepth(field.Drops[index]), team);
                flooded++;
            }
            sim.Events.Add(new WaveCastEvent(tick, caster.Id, caster.Owner, (sbyte)dirX, (sbyte)dirY, flooded));
            PushEnemies(sim, team, area, dirX, dirY);
            _nextWaveTick[caster.Id] = tick + WaveCooldownTicks;
        }

        /// <summary>Shoves every enemy non-air unit standing on an area tile (never a Rooted, Rooting or Uprooting HQ), ascending unit ID, wave_push_tiles single steps along the direction, stopping before the first impassable step.</summary>
        private void PushEnemies(Simulation sim, TeamId casterTeam, SortedSet<int> area, int dirX, int dirY)
        {
            // The live terrain grid this method reads and (through SetWater) changes.
            TerrainGrid terrain = sim.Terrain;
            // Single steps each unit is shoved. Unit: tiles. Range: 0 to the wave_push_tiles tuning maximum.
            int pushTiles = sim.Tuning.Flood.WavePushTiles;
            // Unit that may be shoved, ascending ID.
            foreach (Unit unit in sim.Units.All)
            {
                // Team of the unit's owner (None for a neutral unit).
                TeamId team = TeamOfOwner(sim, unit.Owner);
                // Layer the unit currently moves on (a status or override may change it). Range: Ground, Water or Air.
                MovementLayer layer = unit.EffectiveLayer;
                if (!team.IsValid || team == casterTeam || layer == MovementLayer.Air || HqSystem.IsMovementLocked(unit) || !area.Contains(terrain.TileOf(unit.Position)))
                {
                    continue;
                }
                // Tile and position while stepping.
                int tile = terrain.TileOf(unit.Position);
                // Position reached so far while stepping. Unit: tiles.
                FixVec2 position = unit.Position;
                // Single step of the push. Unit: tiles. Range: 0 to wave_push_tiles - 1.
                for (int step = 0; step < pushTiles; step++)
                {
                    // Tile one step ahead. Unit: tiles.
                    int nx = terrain.XOf(tile) + dirX;
                    // Row one step ahead. Unit: tiles. Range: may fall off the map; Contains checks.
                    int ny = terrain.YOf(tile) + dirY;
                    if (!terrain.Contains(nx, ny))
                    {
                        break;
                    }
                    // Index of the tile one step ahead.
                    int next = terrain.IndexOf(nx, ny);
                    if (!terrain.IsStepPassable(tile, next, layer))
                    {
                        break;
                    }
                    tile = next;
                    position = new FixVec2(position.X + Fix64.FromInt(dirX), position.Y + Fix64.FromInt(dirY));
                }
                if (position != unit.Position)
                {
                    // Where the unit stood before the push. Unit: tiles.
                    FixVec2 from = unit.Position;
                    unit.SetPosition(position);
                    sim.Events.Add(new UnitPushedEvent(sim.Clock.Tick, unit.Id, from, position));
                }
            }
        }
    }
}
