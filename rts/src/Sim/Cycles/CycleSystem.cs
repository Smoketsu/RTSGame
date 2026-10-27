// rts/src/Sim/Cycles/CycleSystem.cs
// Planetary Cycles and Core capture. Part of Rts.Sim.Cycles (spec: rts/docs/specs/planetary_cycles.md). Owned by the Simulation as
// Simulation.Cycles; it runs as step 9 of Simulation.Step (after the faction mechanics, before vision), so it reads positions after
// movement, combat deaths, economy spawns and wave pushes. One match clock drives everything (architecture rule 6): the cycle shifts
// every planetary_cycle_interval_seconds, is locked and announced planetary_cycle_warning_seconds earlier, and a team that keeps a
// unit alone in the Core radius for core_capture_seconds becomes the holder and may pick the next cycle (the same play style never
// comes twice in a row). With three or more teams, holding the Core for ffa_core_win_cumulative_seconds wins the match.
//
// The core never names a faction or a mechanic; cycles are core rules. Per-tick order inside Step: (1) drop a holder or capturer that
// left the match, (2) scan who stands in the Core, (3) the capture state machine, (4) hold accrual, (5) the Core win, (6) lock the
// next cycle, (7) shift to it and flip quake paths, (8) quake damage to static defences. A cycle that shifts on tick S is read by
// movement and combat from tick S + 1; quake damage and path toggles happen on tick S itself.
//
// Determinism: integers and Fix64 only; units scanned in ascending ID order, teams and cycles in ascending ID order; the only
// random draw is one NextInt per lock without a valid pick; state is hashed by WriteHash (everything that changes later ticks).
// The match has cycles only when SimulationConfig carried a cycle list; without one the Core capture still runs.
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Terrain;
using Rts.Sim.Units;

namespace Rts.Sim.Cycles
{
    /// <summary>The state and per-tick rules of Planetary Cycles, quake paths and Core capture for one match.</summary>
    public sealed class CycleSystem
    {
        /// <summary>Method name in <see cref="CycleWarningEvent"/> when the holder picked the cycle. Range: fixed text.</summary>
        public const string MethodPicked = "picked";

        /// <summary>Method name when a script (issuer None) picked the cycle. Range: fixed text.</summary>
        public const string MethodScripted = "scripted";

        /// <summary>Method name when no valid pick existed and the generator chose. Range: fixed text.</summary>
        public const string MethodRandom = "random";

        /// <summary>Cause written into the player state change of a team defeated by the Core win. Range: fixed text "core_held".</summary>
        public const string CauseCoreHeld = "core_held";

        /// <summary>Reason written into <see cref="UnitRelocatedEvent"/> when a closing quake path moved the unit. Range: fixed text "quake_path".</summary>
        public const string ReasonQuakePath = "quake_path";

        /// <summary>The map (Core and quake paths).</summary>
        private readonly MapDefinition _map;

        /// <summary>Every cycle of the match in ascending ordinal ID order. Range: zero or more.</summary>
        private readonly IReadOnlyList<CycleDefinition> _cycles;

        /// <summary>The match clock, for the schedule read API.</summary>
        private readonly MatchClock _clock;

        /// <summary>The players and teams.</summary>
        private readonly PlayerRegistry _players;

        /// <summary>Match tuning (cycle timings, Core timings, relocation radius).</summary>
        private readonly MatchTuning _tuning;

        /// <summary>Cumulative uncontested hold time per team, every team of the match, ascending. Unit: ticks. Range: 0 to <see cref="WinTicks"/>. Hashed.</summary>
        private readonly SortedDictionary<TeamId, int> _holdTicks = new SortedDictionary<TeamId, int>();

        /// <summary>Tile indexes of each quake path, ascending, one array per path in map order. Fixed for the match.</summary>
        private readonly int[][] _pathTiles;

        /// <summary>True while the path is in its closed state (walled where it could be). Hashed.</summary>
        private readonly bool[] _pathClosed;

        /// <summary>Per path and tile (same order as <see cref="_pathTiles"/>): true when this system put a Wall blocker on the tile. Hashed.</summary>
        private readonly bool[][] _tileClosedByCycle;

        /// <summary>Holder and capture progress.</summary>
        private CoreState _core = CoreState.Empty();

        /// <summary>True when the match has at least one cycle. Hashed.</summary>
        public bool IsEnabled { get; }

        /// <summary>ID of the cycle in force, or empty before the first shift. Hashed. Range: "" or a cycle ID.</summary>
        public string ActiveCycleId { get; private set; } = string.Empty;

        /// <summary>Tick the active cycle started, or -1 before the first shift. Unit: ticks. Range: -1 or a shift tick. Hashed.</summary>
        public int ActiveSinceTick { get; private set; } = -1;

        /// <summary>ID of the locked next cycle, or empty until the lock tick. Hashed. Range: "" or a cycle ID.</summary>
        public string NextCycleId { get; private set; } = string.Empty;

        /// <summary>True from the lock tick to the shift tick. Hashed.</summary>
        public bool IsNextLocked { get; private set; }

        /// <summary>ID of an accepted pick waiting for the lock, or empty. Hashed. Range: "" or a cycle ID.</summary>
        public string PendingPickId { get; private set; } = string.Empty;

        /// <summary>Team of the player who made the pending pick; <see cref="TeamId.None"/> for a script pick or no pick. Hashed.</summary>
        public TeamId PendingPickTeam { get; private set; } = TeamId.None;

        /// <summary>The team that holds the Core, or <see cref="TeamId.None"/>. Hashed.</summary>
        public TeamId Holder => _core.Holder;

        /// <summary>The team currently capturing the Core, or <see cref="TeamId.None"/>. Hashed.</summary>
        public TeamId CaptureTeam => _core.CaptureTeam;

        /// <summary>Counted ticks of the capturing team. Unit: ticks. Range: 0 to <see cref="CaptureTicks"/> - 1. Hashed.</summary>
        public int CaptureProgressTicks => _core.CaptureProgressTicks;

        /// <summary>True while two or more teams stand in the Core. Hashed.</summary>
        public bool IsContested { get; private set; }

        /// <summary>The team that won by holding the Core, or <see cref="TeamId.None"/>. Set once; freezes capture and accrual. Hashed.</summary>
        public TeamId CoreWinner { get; private set; } = TeamId.None;

        /// <summary>Ticks between shifts (planetary_cycle_interval_seconds). Unit: ticks. Range: 1 upward; set at match start (0 before).</summary>
        public int IntervalTicks { get; private set; }

        /// <summary>Ticks between the lock and the shift (planetary_cycle_warning_seconds). Unit: ticks. Range: 0 to <see cref="IntervalTicks"/> - 1; set at match start.</summary>
        public int WarningTicks { get; private set; }

        /// <summary>Counted ticks needed to capture the Core (core_capture_seconds). Unit: ticks. Range: 1 upward; set at match start.</summary>
        public int CaptureTicks { get; private set; }

        /// <summary>Cumulative hold ticks that win the match (ffa_core_win_cumulative_seconds). Unit: ticks. Range: 1 upward; set at match start.</summary>
        public int WinTicks { get; private set; }

        /// <summary>True when the match started with at least core_win_min_teams teams, so the cumulative-hold win is on.</summary>
        public bool IsCoreWinEnabled { get; private set; }

        /// <summary>Every cycle of the match in ascending ID order (empty when the match has none).</summary>
        public IReadOnlyList<CycleDefinition> Definitions => _cycles;

        /// <summary>The cycle in force, or null before the first shift or when the match has no cycles.</summary>
        public CycleDefinition? ActiveCycle => Find(ActiveCycleId);

        /// <summary>The locked next cycle, or null until the lock tick.</summary>
        public CycleDefinition? NextCycle => Find(NextCycleId);

        /// <summary>The pending pick, or null when there is none.</summary>
        public CycleDefinition? PendingPick => Find(PendingPickId);

        /// <summary>
        /// The next shift that has not happened yet, seen from the clock's tick (between steps: the tick about to run). Unit: ticks.
        /// Range: a multiple of <see cref="IntervalTicks"/>, at least one interval. 0 when the match has no cycles.
        /// </summary>
        public int NextShiftTick => IsEnabled ? CycleRules.NextShiftTickAtOrAfter(_clock.Tick, IntervalTicks) : 0;

        /// <summary>
        /// The tick on which the next shift is (or was) locked and announced: <see cref="NextShiftTick"/> minus the warning. Unit: ticks.
        /// Range: before the current tick once <see cref="IsNextLocked"/>. 0 when the match has no cycles.
        /// </summary>
        public int NextLockTick => IsEnabled ? NextShiftTick - WarningTicks : 0;

        /// <summary>Builds the system; call <see cref="OnMatchStart"/> from <see cref="Simulation.Start"/> before stepping.</summary>
        /// <param name="map">The map (Core and quake paths).</param>
        /// <param name="cycles">The match's cycles, ascending ID, possibly empty.</param>
        /// <param name="clock">The match clock.</param>
        /// <param name="players">The players and teams.</param>
        /// <param name="tuning">Match tuning.</param>
        internal CycleSystem(MapDefinition map, IReadOnlyList<CycleDefinition> cycles, MatchClock clock, PlayerRegistry players, MatchTuning tuning)
        {
            _map = map;
            _cycles = cycles;
            _clock = clock;
            _players = players;
            _tuning = tuning;
            IsEnabled = cycles.Count > 0;
            foreach (TeamId team in players.Teams)
            {
                _holdTicks.Add(team, 0);
            }
            // Number of quake paths on the map. Range: 0 to 16.
            int pathCount = map.QuakePaths.Count;
            _pathTiles = new int[pathCount][];
            _pathClosed = new bool[pathCount];
            _tileClosedByCycle = new bool[pathCount][];
            // Path index. Unit: path number. Range: 0 to pathCount - 1.
            for (int i = 0; i < pathCount; i++)
            {
                // Tile indexes of the path sorted ascending so every walk over them has a fixed order.
                List<int> sorted = new List<int>(map.QuakePaths[i].TileIndexes);
                sorted.Sort();
                _pathTiles[i] = sorted.ToArray();
                _tileClosedByCycle[i] = new bool[sorted.Count];
            }
        }

        /// <summary>The cycle with this ID, or null for the empty string or an unknown ID.</summary>
        private CycleDefinition? Find(string id)
        {
            if (id.Length == 0)
            {
                return null;
            }
            foreach (CycleDefinition cycle in _cycles)
            {
                if (string.Equals(cycle.Id, id, StringComparison.Ordinal))
                {
                    return cycle;
                }
            }
            return null;
        }

        /// <summary>The cycles that may start next: every cycle whose play style differs from the active one (all before the first shift), ascending ID.</summary>
        public IReadOnlyList<CycleDefinition> AllowedNextCycles()
        {
            return CycleRules.Allowed(_cycles, ActiveCycle);
        }

        /// <summary>Cumulative uncontested hold time of a team. Unit: ticks. Range: 0 to <see cref="WinTicks"/>; 0 for an unknown team.</summary>
        /// <param name="team">The team to read.</param>
        public int HoldTicks(TeamId team)
        {
            return _holdTicks.TryGetValue(team, out int ticks) ? ticks : 0;
        }

        /// <summary>
        /// The weapon range <paramref name="unit"/> fights with: for a mobile, non-HQ, non-flying unit with a ranged weapon the
        /// weapon range plus the active cycle's range bonus (never below melee range), otherwise the weapon's own range. Unit: tiles.
        /// Combat uses this for both fire reach and acquire reach.
        /// </summary>
        /// <param name="unit">The attacker.</param>
        /// <param name="weapon">Its weapon.</param>
        public Fix64 EffectiveRangeTiles(Unit unit, WeaponDefinition weapon)
        {
            // The cycle in force, or null.
            CycleDefinition? active = ActiveCycle;
            if (active == null || active.Effects.SurfaceRangedRangeBonusTiles == Fix64.Zero)
            {
                return weapon.RangeTiles;
            }
            if (unit.Definition.SpeedTilesPerSecond <= Fix64.Zero || unit.Definition.Hq != null
                || unit.EffectiveLayer == MovementLayer.Air || weapon.IsMelee)
            {
                return weapon.RangeTiles;
            }
            return Fix64.Max(weapon.RangeTiles + active.Effects.SurfaceRangedRangeBonusTiles, WeaponDefinition.MeleeMaxRangeTiles);
        }

        // ---------------------------------------------------------------- match start

        /// <summary>Registers the cycle speed and damage modifiers (Id "cycle"). Internal: <see cref="Simulation.Start"/> only, inside the window where modifiers may be registered. Registered even when the match has no cycles, so the hashed modifier lists do not depend on data.</summary>
        /// <param name="sim">The starting simulation.</param>
        internal void RegisterModifiers(Simulation sim)
        {
            sim.RegisterSpeedModifier(new CycleSpeedModifier(this));
            sim.RegisterDamageModifier(new CycleDamageModifier(this));
        }

        /// <summary>
        /// Converts the durations to ticks, checks them, decides whether the Core win is on and closes every path that is closed outside a
        /// Quake. Internal: <see cref="Simulation.Start"/> only. Throws <see cref="InvalidOperationException"/> naming the tuning key when
        /// a duration rounds to an unusable tick count, or when the cycles do not offer two play styles.
        /// </summary>
        /// <param name="sim">The starting simulation (inside its start window).</param>
        internal void OnMatchStart(Simulation sim)
        {
            IntervalTicks = _tuning.PlanetaryCycleIntervalTicks(_clock);
            WarningTicks = _tuning.PlanetaryCycleWarningTicks(_clock);
            CaptureTicks = _tuning.CoreCaptureTicks(_clock);
            WinTicks = _tuning.FfaCoreWinCumulativeTicks(_clock);
            IsCoreWinEnabled = _players.TeamCount >= _tuning.CoreWinMinTeams;
            if (CaptureTicks < 1)
            {
                throw new InvalidOperationException("core_capture_seconds must be at least one tick at " + _clock.TicksPerSecond + " ticks per second.");
            }
            if (WinTicks < 1)
            {
                throw new InvalidOperationException("ffa_core_win_cumulative_seconds must be at least one tick at " + _clock.TicksPerSecond + " ticks per second.");
            }
            if (IsEnabled)
            {
                if (IntervalTicks < 1 || WarningTicks >= IntervalTicks)
                {
                    throw new InvalidOperationException("planetary_cycle_warning_seconds must be shorter than planetary_cycle_interval_seconds in whole ticks (found "
                        + WarningTicks + " and " + IntervalTicks + " ticks).");
                }
                // Distinct play styles among the cycles; the no-repeat rule needs two.
                SortedSet<CycleFavour> styles = new SortedSet<CycleFavour>();
                foreach (CycleDefinition cycle in _cycles)
                {
                    styles.Add(cycle.Favours);
                }
                if (styles.Count < 2)
                {
                    throw new InvalidOperationException("Planetary Cycles need cycles of at least two play styles so the no-repeat rule can always pick.");
                }
            }
            ApplyPaths(sim, false);
        }

        // ---------------------------------------------------------------- commands

        /// <summary>Records an accepted pick, replacing any earlier one. Internal: <see cref="Commands.PickCycleCommand"/> only, after it has validated the pick.</summary>
        /// <param name="cycleId">ID of the picked cycle.</param>
        /// <param name="team">The picking player's team, or <see cref="TeamId.None"/> for a script pick.</param>
        internal void SetPendingPick(string cycleId, TeamId team)
        {
            PendingPickId = cycleId;
            PendingPickTeam = team;
        }

        // ---------------------------------------------------------------- step

        /// <summary>Runs one tick of the cycle system (step 9). Internal: <see cref="Simulation.Step"/> only.</summary>
        /// <param name="sim">The stepping simulation.</param>
        internal void Step(Simulation sim)
        {
            // Tick being simulated. Unit: ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            DropSidesOutOfMatch(sim, tick);
            // The Core of the map, or null for a map without one.
            CoreArea? core = sim.Map.Core;
            if (core != null && !CoreWinner.IsValid)
            {
                StepCore(sim, tick, core);
            }
            if (IsEnabled)
            {
                // Shift number locked on this tick, or 0 when none.
                int lockedShift = CycleRules.LockedShiftNumber(tick, IntervalTicks, WarningTicks);
                if (lockedShift > 0)
                {
                    LockNext(sim, tick, lockedShift);
                }
                // Shift number that happens on this tick, or 0 when none.
                int shift = CycleRules.ShiftNumber(tick, IntervalTicks);
                if (shift > 0)
                {
                    Shift(sim, tick, shift);
                }
                QuakeDamage(sim, tick);
            }
        }

        /// <summary>Step 1: a holder or a capturing team with no player left in the match loses its place.</summary>
        private void DropSidesOutOfMatch(Simulation sim, int tick)
        {
            if (_core.Holder.IsValid && !TeamInMatch(_core.Holder))
            {
                // The team that held the Core and is gone.
                TeamId gone = _core.Holder;
                sim.Events.Add(new CoreLostEvent(tick, gone, "holder_out"));
                _core.Holder = TeamId.None;
                if (PendingPickTeam == gone)
                {
                    SetPendingPick(string.Empty, TeamId.None);
                }
            }
            if (_core.CaptureTeam.IsValid && !TeamInMatch(_core.CaptureTeam))
            {
                sim.Events.Add(new CoreCaptureResetEvent(tick, _core.CaptureTeam, _core.CaptureProgressTicks, "holder_out"));
                _core.CaptureTeam = TeamId.None;
                _core.CaptureProgressTicks = 0;
            }
        }

        /// <summary>True when at least one player of the team is still in the match (Active or Disconnected).</summary>
        private bool TeamInMatch(TeamId team)
        {
            foreach (PlayerId id in _players.PlayersOnTeam(team))
            {
                if (_players.TryGet(id, out Player? player) && player.IsInMatch)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Steps 2 to 5: who stands in the Core, the capture state machine, hold accrual and the Core win.</summary>
        private void StepCore(Simulation sim, int tick, CoreArea core)
        {
            // Teams with a counting unit in the Core, ascending, distinct.
            SortedSet<TeamId> presentSet = new SortedSet<TeamId>();
            foreach (Unit unit in sim.Units.All)
            {
                if (CycleRules.CountsForCore(sim, unit, core, out TeamId team))
                {
                    presentSet.Add(team);
                }
            }
            List<TeamId> present = new List<TeamId>(presentSet);
            // True when two or more teams are present.
            bool contested = present.Count >= 2;
            if (contested != IsContested)
            {
                IsContested = contested;
                sim.Events.Add(new CoreContestedChangedEvent(tick, contested));
            }
            // Changes made by the capture state machine this tick, in order.
            List<CoreChange> changes = new List<CoreChange>();
            CycleRules.CaptureStep(ref _core, present, CaptureTicks, changes);
            foreach (CoreChange change in changes)
            {
                switch (change.Kind)
                {
                    case CoreChangeKind.CaptureStarted:
                        sim.Events.Add(new CoreCaptureStartedEvent(tick, change.Team));
                        break;
                    case CoreChangeKind.CaptureReset:
                        sim.Events.Add(new CoreCaptureResetEvent(tick, change.Team, change.ProgressTicks, change.Reason));
                        break;
                    case CoreChangeKind.Lost:
                        sim.Events.Add(new CoreLostEvent(tick, change.Team, change.Reason));
                        break;
                    case CoreChangeKind.Captured:
                        sim.Events.Add(new CoreCapturedEvent(tick, change.Team, change.PreviousHolder));
                        if (PendingPickTeam.IsValid && PendingPickTeam != change.Team)
                        {
                            // The pick belonged to a team that no longer holds the Core.
                            SetPendingPick(string.Empty, TeamId.None);
                        }
                        break;
                }
            }
            if (_core.Holder.IsValid)
            {
                // True when a team other than the holder stands in the Core, which pauses the holder's accrual.
                bool otherPresent = false;
                foreach (TeamId team in present)
                {
                    if (team != _core.Holder)
                    {
                        otherPresent = true;
                    }
                }
                // The holder's cumulative hold time before this tick. Unit: ticks. Range: 0 to WinTicks.
                int held = HoldTicks(_core.Holder);
                if (!otherPresent && held < WinTicks)
                {
                    _holdTicks[_core.Holder] = held + 1;
                }
                if (IsCoreWinEnabled && HoldTicks(_core.Holder) >= WinTicks)
                {
                    CoreWinner = _core.Holder;
                    sim.Events.Add(new CoreWinEvent(tick, CoreWinner, HoldTicks(CoreWinner)));
                    DefeatOtherTeams(sim, CoreWinner);
                }
            }
        }

        /// <summary>The Core win: every player in the match who is not on the winning team is Defeated with cause core_held.</summary>
        private void DefeatOtherTeams(Simulation sim, TeamId winner)
        {
            foreach (Player player in _players.All)
            {
                if (player.Team != winner && player.IsInMatch)
                {
                    PlayerTransitions.TryChange(sim, player.Id, PlayerState.Defeated, CauseCoreHeld, out _);
                }
            }
        }

        /// <summary>Step 6: chooses and announces the next cycle for shift number <paramref name="k"/>.</summary>
        private void LockNext(Simulation sim, int tick, int k)
        {
            // Cycles that may come next (no repeated play style), ascending ID.
            List<CycleDefinition> allowed = CycleRules.Allowed(_cycles, ActiveCycle);
            // The pending pick, or null.
            CycleDefinition? pick = Find(PendingPickId);
            // The cycle chosen, how it was chosen and which team chose it.
            CycleDefinition next;
            string method;
            TeamId chosenBy = TeamId.None;
            if (pick != null && (!PendingPickTeam.IsValid || PendingPickTeam == _core.Holder) && allowed.Contains(pick))
            {
                next = pick;
                if (PendingPickTeam.IsValid)
                {
                    method = MethodPicked;
                    chosenBy = PendingPickTeam;
                }
                else
                {
                    method = MethodScripted;
                }
            }
            else
            {
                // The one random draw of this system: a uniform index into the allowed cycles.
                next = allowed[sim.Rng.NextInt(allowed.Count)];
                method = MethodRandom;
            }
            NextCycleId = next.Id;
            IsNextLocked = true;
            SetPendingPick(string.Empty, TeamId.None);
            sim.Events.Add(new CycleWarningEvent(tick, next.Id, CycleRules.ShiftTick(k, IntervalTicks), method, chosenBy));
        }

        /// <summary>Step 7: the locked cycle becomes active; quake paths flip when the quake flag changes.</summary>
        private void Shift(Simulation sim, int tick, int k)
        {
            if (!IsNextLocked)
            {
                // Defensive: a shift always has a lock before it, but never shift without a chosen cycle.
                LockNext(sim, tick, k);
            }
            // The cycle that ends, or null for the first shift.
            CycleDefinition? previous = ActiveCycle;
            // The cycle that starts.
            CycleDefinition active = Find(NextCycleId) ?? throw new InvalidOperationException("Locked cycle '" + NextCycleId + "' is not in the match.");
            ActiveCycleId = active.Id;
            ActiveSinceTick = tick;
            NextCycleId = string.Empty;
            IsNextLocked = false;
            sim.Events.Add(new CycleStartedEvent(tick, active.Id, previous != null ? previous.Id : string.Empty, CycleRules.ShiftTick(k + 1, IntervalTicks)));
            // Whether the quake paths were flipped before this shift (never for the first shift).
            bool wasToggled = previous != null && previous.Effects.TogglesQuakePaths;
            if (wasToggled != active.Effects.TogglesQuakePaths)
            {
                ApplyPaths(sim, active.Effects.TogglesQuakePaths);
            }
        }

        /// <summary>Step 8: once a second while the active cycle damages defences, every static defence loses a share of its health down to a floor.</summary>
        private void QuakeDamage(Simulation sim, int tick)
        {
            // The cycle in force, or null.
            CycleDefinition? active = ActiveCycle;
            if (active == null || active.Effects.DefenceDamagePerSecondFraction <= Fix64.Zero
                || (tick - ActiveSinceTick) % sim.Clock.TicksPerSecond != 0)
            {
                return;
            }
            foreach (Unit unit in sim.Units.All)
            {
                if (!unit.Definition.Economy.IsStaticDefence || unit.Health <= Fix64.Zero || unit.IsUnderConstruction || unit.IsInvulnerable)
                {
                    continue;
                }
                // Damage of one application. Unit: health points. Range: 0 to 5% of max health.
                Fix64 amount = unit.Definition.MaxHealth * active.Effects.DefenceDamagePerSecondFraction;
                // Health the cycle never takes the unit below. Unit: health points.
                Fix64 floor = unit.Definition.MaxHealth * active.Effects.DefenceDamageMinHealthFraction;
                if (unit.Health <= floor)
                {
                    continue;
                }
                // Health before this application, for the event's damage figure. Unit: health points.
                Fix64 before = unit.Health;
                unit.SetHealth(Fix64.Max(floor, before - amount));
                sim.Events.Add(new UnitDamagedEvent(tick, EntityId.None, unit.Id, before - unit.Health, unit.Health));
            }
        }

        // ---------------------------------------------------------------- quake paths

        /// <summary>
        /// Sets every quake path to the state implied by whether the active cycle toggles paths: a path is closed when
        /// <c>closed_outside_quake</c> differs from <paramref name="toggled"/>. Paths are processed in map order. Units standing on a tile
        /// that is about to close are moved to the nearest standable tile within the relocation radius first.
        /// </summary>
        /// <param name="sim">The stepping simulation.</param>
        /// <param name="toggled">True while the active cycle flips quake paths.</param>
        private void ApplyPaths(Simulation sim, bool toggled)
        {
            // Number of quake paths. Range: 0 to 16.
            int pathCount = _pathTiles.Length;
            if (pathCount == 0)
            {
                return;
            }
            // Tiles of every path that ends this call closed; units are never relocated onto them.
            bool[] closing = new bool[sim.Terrain.TileCount];
            // Path index. Unit: path number. Range: 0 to pathCount - 1.
            for (int i = 0; i < pathCount; i++)
            {
                if (WantsClosed(i, toggled))
                {
                    foreach (int tile in _pathTiles[i])
                    {
                        closing[tile] = true;
                    }
                }
            }
            for (int i = 0; i < pathCount; i++)
            {
                SetPath(sim, i, WantsClosed(i, toggled), closing);
            }
        }

        /// <summary>True when path <paramref name="index"/> should be closed: closed outside a Quake XOR the quake flag.</summary>
        private bool WantsClosed(int index, bool toggled)
        {
            return _map.QuakePaths[index].ClosedOutsideQuake != toggled;
        }

        /// <summary>
        /// Opens or closes one path. Opening removes the Wall this system placed. Closing, tile by tile in ascending index, skips a tile
        /// that already carries a blocker, relocates the ground and water units standing on it (the tile stays open when one of them has
        /// nowhere to go) and then adds a Wall. Emits <see cref="QuakePathChangedEvent"/>.
        /// </summary>
        private void SetPath(Simulation sim, int index, bool closed, bool[] closing)
        {
            // Tiles whose blocker was added or removed by this call. Unit: tiles. Range: 0 to 64.
            int changed = 0;
            // Tile indexes of the path, ascending.
            int[] tiles = _pathTiles[index];
            // Tiles of the path this system has walled, same order.
            bool[] walled = _tileClosedByCycle[index];
            // Position in the path's tile list. Unit: list position. Range: 0 to tile count - 1.
            for (int t = 0; t < tiles.Length; t++)
            {
                if (!closed)
                {
                    if (walled[t])
                    {
                        sim.Terrain.RemoveBlockers(sim, tiles[t], TileBlockers.Wall);
                        walled[t] = false;
                        changed++;
                    }
                    continue;
                }
                if (walled[t] || sim.Terrain[tiles[t]].IsBlocked)
                {
                    // Already walled by this system, or a footprint or another wall holds the tile: leave it.
                    continue;
                }
                if (!RelocateUnitsOff(sim, tiles[t], closing))
                {
                    // A unit stands here with nowhere to go; the tile stays open this time.
                    continue;
                }
                sim.Terrain.AddBlockers(sim, tiles[t], TileBlockers.Wall);
                walled[t] = true;
                changed++;
            }
            _pathClosed[index] = closed;
            sim.Events.Add(new QuakePathChangedEvent(sim.Clock.Tick, index, _map.QuakePaths[index].Id, closed, changed));
        }

        /// <summary>
        /// Moves every ground or water unit standing on <paramref name="tile"/> to the centre of the first standable tile on Chebyshev
        /// rings 1 to quake_path_relocate_radius_tiles (ascending tile index per ring) that is not in <paramref name="closing"/>. Returns
        /// false, moving nobody, when some unit has no such tile.
        /// </summary>
        private bool RelocateUnitsOff(Simulation sim, int tile, bool[] closing)
        {
            // Destinations chosen for the units on the tile, in unit ID order.
            List<KeyValuePair<Unit, int>> moves = new List<KeyValuePair<Unit, int>>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.EffectiveLayer == MovementLayer.Air || sim.Terrain.TileOf(unit.Position) != tile)
                {
                    continue;
                }
                // Tile to move the unit to, or -1 when none qualifies.
                int destination = FindStandableTile(sim, tile, unit.EffectiveLayer, closing);
                if (destination < 0)
                {
                    return false;
                }
                moves.Add(new KeyValuePair<Unit, int>(unit, destination));
            }
            foreach (KeyValuePair<Unit, int> move in moves)
            {
                // Where the unit stood and where it goes. Unit: tiles.
                FixVec2 from = move.Key.Position;
                FixVec2 to = sim.Terrain.CentreOf(move.Value);
                move.Key.SetPosition(to);
                sim.Events.Add(new UnitRelocatedEvent(sim.Clock.Tick, move.Key.Id, from, to, ReasonQuakePath));
            }
            return true;
        }

        /// <summary>The first tile on rings 1 to the relocation radius around <paramref name="tile"/> that the layer can stand on and that is not closing, or -1.</summary>
        private int FindStandableTile(Simulation sim, int tile, MovementLayer layer, bool[] closing)
        {
            // Centre tile column and row. Unit: tiles.
            int cx = sim.Terrain.XOf(tile);
            int cy = sim.Terrain.YOf(tile);
            // Largest ring to search. Unit: tiles. Range: 0 to 8.
            int radius = _tuning.QuakePathRelocateRadiusTiles;
            // Ring number (Chebyshev distance). Unit: tiles. Range: 1 to radius.
            for (int ring = 1; ring <= radius; ring++)
            {
                // Row, then column: ascending tile index within the ring.
                for (int y = cy - ring; y <= cy + ring; y++)
                {
                    for (int x = cx - ring; x <= cx + ring; x++)
                    {
                        if (!sim.Terrain.Contains(x, y) || (Math.Abs(x - cx) != ring && Math.Abs(y - cy) != ring))
                        {
                            continue;
                        }
                        // Index of the candidate tile.
                        int candidate = sim.Terrain.IndexOf(x, y);
                        if (!closing[candidate] && sim.Terrain.IsStandable(candidate, layer))
                        {
                            return candidate;
                        }
                    }
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- hash

        /// <summary>
        /// Feeds everything that changes later ticks into the hash, in a fixed order: enabled flag, active cycle and its start tick, the
        /// locked next cycle, the pending pick and its team, holder, capturing team and progress, contested flag, cumulative hold ticks per
        /// team (ascending), the Core winner, then per quake path its closed flag, tile count and per-tile wall flags. Cached tick values
        /// and the cycle definitions are pure functions of data already in the content digest and are not written.
        /// </summary>
        /// <param name="hasher">The hasher being filled by <see cref="Simulation.ComputeStateHash"/> or a test.</param>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(IsEnabled);
            hasher.Write(ActiveCycleId);
            hasher.Write(ActiveSinceTick);
            hasher.Write(NextCycleId);
            hasher.Write(IsNextLocked);
            hasher.Write(PendingPickId);
            hasher.Write(PendingPickTeam);
            hasher.Write(_core.Holder);
            hasher.Write(_core.CaptureTeam);
            hasher.Write(_core.CaptureProgressTicks);
            hasher.Write(IsContested);
            hasher.Write(_holdTicks.Count);
            foreach (KeyValuePair<TeamId, int> pair in _holdTicks)
            {
                hasher.Write(pair.Key);
                hasher.Write(pair.Value);
            }
            hasher.Write(CoreWinner);
            hasher.Write(_pathTiles.Length);
            // Path index. Unit: path number. Range: 0 to path count - 1.
            for (int i = 0; i < _pathTiles.Length; i++)
            {
                hasher.Write(_pathClosed[i]);
                hasher.Write(_tileClosedByCycle[i].Length);
                foreach (bool walled in _tileClosedByCycle[i])
                {
                    hasher.Write(walled);
                }
            }
        }
    }
}
