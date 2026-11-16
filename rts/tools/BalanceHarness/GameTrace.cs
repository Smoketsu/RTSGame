// rts/tools/BalanceHarness/GameTrace.cs
// Read-only observer of one AI-versus-AI game for the --trace mode of the harness: every N game-seconds it writes one line per player (bank, income,
// supply, workers, structures, army, trained and lost, HQ, the commands the AI issued, the commands the simulation rejected, attack state), and at the
// end a summary (outcome, totals, first ticks, the last commands of each player). GameRunner.Play calls the four hooks (Begin, AfterThink, AfterStep,
// Finish) between the same calls it always makes; the trace only reads simulation state, events and the controllers' LastCommands and never submits or
// steps, so the game (and its final state hash) is the same with or without it. Output is for people, so it may use floating point (it is a tool, not
// the simulation). Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Collects and writes the timeline of one game.</summary>
    public sealed class GameTrace
    {
        /// <summary>Game-seconds between timeline lines used by --trace. Unit: seconds. Range: 1 to 600. Source: task brief for the AI trace (16 Nov 2026).</summary>
        public const int DefaultIntervalSeconds = 30;

        /// <summary>How many of a player's latest commands the summary lists. Unit: commands. Range: 1 to 100. Source: task brief for the AI trace.</summary>
        public const int LastCommandCount = 20;

        /// <summary>How many stance changes per player the summary lists. Unit: lines. Range: 1 to 1000. Source: tool output size limit chosen for the AI trace.</summary>
        public const int MaxStanceLines = 60;

        /// <summary>Where the text goes.</summary>
        private readonly TextWriter _out;

        /// <summary>Game-seconds between timeline lines. Unit: seconds. Range: 1 to 600.</summary>
        private readonly int _intervalSeconds;

        /// <summary>Timeline interval in ticks, set by <see cref="Begin"/>. Unit: ticks. Range: 1 upward; 0 before Begin.</summary>
        private int _intervalTicks;

        /// <summary>Match tick rate, set by <see cref="Begin"/>. Unit: ticks per second. Range: 10 to 60; 1 before Begin.</summary>
        private int _ticksPerSecond = 1;

        /// <summary>Per-player records in ascending player order.</summary>
        private readonly SortedDictionary<byte, PlayerTrace> _players = new SortedDictionary<byte, PlayerTrace>();

        /// <summary>Every unit ever seen, by entity ID (kept after death so events about removed units can still be attributed).</summary>
        private readonly SortedDictionary<EntityId, Known> _known = new SortedDictionary<EntityId, Known>();

        /// <summary>Commands submitted for the next step and not yet resolved (accepted or rejected).</summary>
        private readonly List<CommandEntry> _pending = new List<CommandEntry>();

        /// <summary>Why each player left the match: the cause text of its PlayerStateChangedEvent, by player slot.</summary>
        private readonly SortedDictionary<byte, string> _defeats = new SortedDictionary<byte, string>();

        /// <summary>Creates a trace that writes to <paramref name="output"/>.</summary>
        /// <param name="output">Destination; not null.</param>
        /// <param name="intervalSeconds">Game-seconds between lines. Unit: seconds. Range: 1 to 600.</param>
        public GameTrace(TextWriter output, int intervalSeconds)
        {
            _out = output;
            _intervalSeconds = intervalSeconds;
        }

        /// <summary>Called once after the controllers started and before the first step.</summary>
        /// <param name="sim">The started simulation.</param>
        /// <param name="header">Description of the game, written as the first line.</param>
        public void Begin(Simulation sim, string header)
        {
            _ticksPerSecond = sim.Clock.TicksPerSecond;
            _intervalTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(_intervalSeconds));
            foreach (Player player in sim.Players.All)
            {
                _players.Add(player.Id.Value, new PlayerTrace(player.Id, player.FactionId));
            }
            _out.Write("# " + header + "\n");
            _out.Write("# line format per player: bank=common/rare  +income=common/rare since last line  supply=used/cap  workers=total gather=n idle=n  "
                + "done=[structures complete]  building=[under construction]  army=[units]  trained=[since last line]  lost=[since last line]  "
                + "hq=state hp=health/max @(x,y)  cmds=[commands issued since last line, by kind]  rejected=[by reason]  atk=attack-moving/army  "
                + "armyDist=tiles from army centre to enemy HQ  atkDist=same for the attack-moving part  "
                + "armyAt=army centre (x,y) in tiles  nearFields=resource left in the fields within 12 tiles of the own HQ (C common, R rare)  stance=the AI army stance (Massing, Attack, Defend, Retreat) after its last think  attacks=attacks started so far  retreats=retreats so far\n");
        }

        /// <summary>Called after AiHost.ThinkAll and before Step: notes the commands each controller just submitted.</summary>
        /// <param name="sim">The simulation between steps (its clock tick is the tick the commands are scheduled for).</param>
        /// <param name="controllers">The game's controllers; every one must be a <see cref="SkirmishAiController"/>.</param>
        public void AfterThink(Simulation sim, IReadOnlyList<IPlayerController> controllers)
        {
            foreach (IPlayerController controller in controllers)
            {
                if (!(controller is SkirmishAiController ai) || !_players.TryGetValue(ai.Player.Value, out PlayerTrace? trace))
                {
                    continue;
                }
                trace.Memory = ai.Memory;
                if (ai.Memory.Stance != trace.LastStance)
                {
                    trace.StanceChanges.Add("tick " + sim.Clock.Tick.ToString(CultureInfo.InvariantCulture) + " (" + Seconds(sim.Clock.Tick) + "s) " + trace.LastStance + " -> "
                        + ai.Memory.Stance + " (army threshold " + ai.Memory.AttackThreshold.ToString(CultureInfo.InvariantCulture) + " supply, attacks started "
                        + ai.Memory.AttacksStarted.ToString(CultureInfo.InvariantCulture) + ", retreats " + ai.Memory.Retreats.ToString(CultureInfo.InvariantCulture) + ")");
                    if (ai.Memory.Stance == AiStance.Attack && trace.FirstStanceAttackTick < 0)
                    {
                        trace.FirstStanceAttackTick = sim.Clock.Tick;
                    }
                    trace.LastStance = ai.Memory.Stance;
                }
                foreach (Command command in ai.LastCommands)
                {
                    // The log entry of this command; its result is filled in after the step.
                    CommandEntry entry = new CommandEntry(trace, command);
                    _pending.Add(entry);
                    trace.Log.Add(entry);
                    trace.Intents.Add(command.Kind);
                    if (trace.FirstAttackOrderTick < 0 && (command is AttackMoveCommand || command is AttackUnitCommand))
                    {
                        trace.FirstAttackOrderTick = command.Tick;
                    }
                }
            }
        }

        /// <summary>Called right after Step: reads the events of the tick just stepped and writes a timeline line when the clock reached a multiple of the interval.</summary>
        /// <param name="sim">The simulation after the step.</param>
        public void AfterStep(Simulation sim)
        {
            // Tick that was just stepped. Unit: ticks. Range: 0 upward.
            int stepped = sim.Clock.Tick - 1;
            foreach (Unit unit in sim.Units.All)
            {
                if (!_known.ContainsKey(unit.Id))
                {
                    _known.Add(unit.Id, new Known(unit.Owner, unit.Definition));
                }
            }
            // Commands the simulation refused this tick, by reference to the command object.
            Dictionary<Command, string> rejected = new Dictionary<Command, string>();
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                if (simEvent is CommandRejectedEvent reject)
                {
                    rejected[reject.Command] = reject.Reason;
                    if (_players.TryGetValue(reject.Command.Issuer.Value, out PlayerTrace? issuer))
                    {
                        issuer.Rejected.Add(reject.Reason);
                        issuer.TotalRejected.Add(reject.Reason);
                    }
                }
            }
            foreach (CommandEntry entry in _pending)
            {
                if (rejected.TryGetValue(entry.Command, out string? reason))
                {
                    entry.Result = "REJECTED " + reason;
                    continue;
                }
                entry.Result = "ok";
                if (entry.Command is BuildStructureCommand build && sim.Content.Units.TryGetValue(build.DefinitionId, out UnitDefinition? structure))
                {
                    entry.Owner.Spend(structure);
                    entry.Owner.TotalPlaced.Add(build.DefinitionId);
                    entry.Owner.Sites.Add("tick " + entry.Command.Tick.ToString(CultureInfo.InvariantCulture) + " " + build.DefinitionId + " tile ("
                        + build.TileX.ToString(CultureInfo.InvariantCulture) + "," + build.TileY.ToString(CultureInfo.InvariantCulture) + ")");
                    if (entry.Owner.FirstStructureTick < 0)
                    {
                        entry.Owner.FirstStructureTick = stepped;
                    }
                }
                else if (entry.Command is TrainUnitCommand train && sim.Content.Units.TryGetValue(train.DefinitionId, out UnitDefinition? trained))
                {
                    entry.Owner.Spend(trained);
                }
            }
            _pending.Clear();
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                ReadEvent(sim, simEvent);
            }
            if (sim.Clock.Tick % _intervalTicks == 0)
            {
                WriteLines(sim, sim.Clock.Tick, false);
            }
        }

        /// <summary>Called once after the last step: writes a final timeline line and the summary.</summary>
        /// <param name="sim">The finished simulation.</param>
        /// <param name="outcome">Outcome after the last step.</param>
        /// <param name="lastTick">Last tick stepped. Unit: ticks. Range: 0 upward.</param>
        /// <param name="maxGameSeconds">The time cap the game ran under. Unit: seconds. Range: 1 to 7200.</param>
        public void Finish(Simulation sim, MatchOutcome outcome, int lastTick, int maxGameSeconds)
        {
            _out.Write("\n== final state at tick " + lastTick.ToString(CultureInfo.InvariantCulture) + " ==\n");
            WriteLines(sim, lastTick + 1, true);
            _out.Write("\n== summary ==\n");
            if (!outcome.IsDecided)
            {
                _out.Write("outcome: undecided, time cap of " + maxGameSeconds.ToString(CultureInfo.InvariantCulture) + " s reached\n");
            }
            else if (outcome.IsDraw)
            {
                _out.Write("outcome: draw\n");
            }
            else
            {
                _out.Write("outcome: team " + outcome.Winner.Value.ToString(CultureInfo.InvariantCulture) + " wins (player slot "
                    + outcome.Winner.Value.ToString(CultureInfo.InvariantCulture) + ")\n");
            }
            foreach (KeyValuePair<byte, string> defeat in _defeats)
            {
                _out.Write("defeat: P" + defeat.Key.ToString(CultureInfo.InvariantCulture) + " " + defeat.Value + "\n");
            }
            _out.Write("game length: " + lastTick.ToString(CultureInfo.InvariantCulture) + " ticks = " + Seconds(lastTick) + " s\n");
            foreach (PlayerTrace trace in _players.Values)
            {
                _out.Write("\n" + trace.Label + " totals\n");
                _out.Write("  gathered=" + trace.TotalCommon.ToString(CultureInfo.InvariantCulture) + "c/" + trace.TotalRare.ToString(CultureInfo.InvariantCulture)
                    + "r  spent=" + trace.SpentCommon.ToString(CultureInfo.InvariantCulture) + "c/" + trace.SpentRare.ToString(CultureInfo.InvariantCulture) + "r\n");
                _out.Write("  trained=" + trace.TotalTrained.Describe() + "\n");
                _out.Write("  lost=" + trace.TotalLost.Describe() + "\n");
                _out.Write("  structures placed=" + trace.TotalPlaced.Describe() + "  completed=" + trace.TotalCompleted.Describe() + "\n");
                _out.Write("  structure sites: " + string.Join("; ", trace.Sites) + "\n");
                _out.Write("  damage dealt to enemy HQ=" + F1(trace.HqDamage) + "\n");
                _out.Write("  first: worker trained=" + TickText(trace.FirstWorkerTick) + "  structure placed=" + TickText(trace.FirstStructureTick)
                    + "  army unit=" + TickText(trace.FirstArmyTick) + "  attack order=" + TickText(trace.FirstAttackOrderTick)
                    + "  damage to enemy HQ=" + TickText(trace.FirstHqDamageTick) + "  first stance Attack=" + TickText(trace.FirstStanceAttackTick) + "\n");
                _out.Write("  stance changes (" + trace.StanceChanges.Count.ToString(CultureInfo.InvariantCulture) + " in all, first " + MaxStanceLines.ToString(CultureInfo.InvariantCulture) + " listed):\n");
                for (int i = 0; i < trace.StanceChanges.Count && i < MaxStanceLines; i++)
                {
                    _out.Write("    " + trace.StanceChanges[i] + "\n");
                }
                _out.Write("  commands rejected, whole game=" + trace.TotalRejected.Describe() + "\n");
                _out.Write("  last " + LastCommandCount.ToString(CultureInfo.InvariantCulture) + " commands:\n");
                // First log index to print. Range: 0 to log size.
                int from = Math.Max(0, trace.Log.Count - LastCommandCount);
                for (int i = from; i < trace.Log.Count; i++)
                {
                    _out.Write("    " + trace.Log[i].Describe(sim) + "\n");
                }
            }
            _out.Flush();
        }

        /// <summary>Folds one event of the stepped tick into the per-player records.</summary>
        private void ReadEvent(Simulation sim, SimEvent simEvent)
        {
            if (simEvent is ResourceDepositedEvent deposit && _players.TryGetValue(deposit.Owner.Value, out PlayerTrace? depositor))
            {
                depositor.AddIncome(deposit.Resource, deposit.Amount);
            }
            else if (simEvent is UnitTrainedEvent trained && _known.TryGetValue(trained.Unit, out Known? newUnit) && _players.TryGetValue(newUnit.Owner.Value, out PlayerTrace? producer))
            {
                producer.Trained.Add(trained.DefinitionId);
                producer.TotalTrained.Add(trained.DefinitionId);
                // Tick the unit appeared. Unit: ticks. Range: 0 upward.
                int tick = trained.Tick;
                if (newUnit.Definition.Economy.CanGather && producer.FirstWorkerTick < 0)
                {
                    producer.FirstWorkerTick = tick;
                }
                else if (Classify(newUnit.Definition) == UnitClass.Army && producer.FirstArmyTick < 0)
                {
                    producer.FirstArmyTick = tick;
                }
            }
            else if (simEvent is StructureCompletedEvent done && _known.TryGetValue(done.Structure, out Known? built) && _players.TryGetValue(built.Owner.Value, out PlayerTrace? builder))
            {
                builder.TotalCompleted.Add(done.DefinitionId);
            }
            else if (simEvent is UnitRemovedEvent removed && _known.TryGetValue(removed.Unit, out Known? gone) && _players.TryGetValue(gone.Owner.Value, out PlayerTrace? loser))
            {
                loser.Lost.Add(gone.Definition.Id);
                loser.TotalLost.Add(gone.Definition.Id + "(" + removed.Reason + ")");
            }
            else if (simEvent is UnitDamagedEvent hit && hit.Damage > Fix64.Zero && _known.TryGetValue(hit.Target, out Known? target) && target.Definition.Hq != null
                && _known.TryGetValue(hit.Attacker, out Known? attacker) && sim.Players.AreEnemies(attacker.Owner, target.Owner) && _players.TryGetValue(attacker.Owner.Value, out PlayerTrace? damager))
            {
                damager.HqDamage += hit.Damage;
                if (damager.FirstHqDamageTick < 0)
                {
                    damager.FirstHqDamageTick = hit.Tick;
                }
            }
            else if (simEvent is PlayerStateChangedEvent changed && changed.Current != PlayerState.Active)
            {
                _defeats[changed.Player.Value] = changed.Current + " (" + changed.Cause + ") at tick " + changed.Tick.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Writes one line per player describing the state now, then clears the since-last-line counters.</summary>
        /// <param name="sim">The simulation between steps.</param>
        /// <param name="tick">Tick shown as the line time. Unit: ticks. Range: 0 upward.</param>
        /// <param name="isFinal">True for the closing line (not labelled with a rounded time).</param>
        private void WriteLines(Simulation sim, int tick, bool isFinal)
        {
            foreach (PlayerTrace trace in _players.Values)
            {
                if (!sim.Players.TryGet(trace.Id, out Player? player))
                {
                    continue;
                }
                // Counters filled by one pass over the player's units.
                int workers = 0, gathering = 0, idle = 0, armyCount = 0, attackMoving = 0, armyN = 0, atkN = 0;
                // Units by definition for each group.
                CountMap done = new CountMap(), building = new CountMap(), army = new CountMap();
                // HQ description and position of this player's HQ (first one), and position of the nearest enemy HQ. Unit: tiles.
                string hq = "none";
                // This player's HQ position (tiles); only meaningful when hq is not "none".
                FixVec2 ownHq = FixVec2.Zero;
                FixVec2 enemyHq = FixVec2.Zero;
                bool haveEnemyHq = false;
                // Sums of army positions. Unit: tiles.
                double armyX = 0, armyY = 0, atkX = 0, atkY = 0;
                foreach (Unit unit in sim.Units.All)
                {
                    if (unit.Owner == trace.Id)
                    {
                        UnitClass kind = Classify(unit.Definition);
                        if (kind == UnitClass.Hq)
                        {
                            ownHq = unit.Position;
                            hq = unit.HqState + " hp=" + F0(unit.Health) + "/" + F0(unit.Definition.MaxHealth) + " @(" + F1(unit.Position.X) + "," + F1(unit.Position.Y) + ")";
                        }
                        else if (kind == UnitClass.Structure)
                        {
                            (unit.IsUnderConstruction ? building : done).Add(unit.Definition.Id);
                        }
                        else if (kind == UnitClass.Worker)
                        {
                            workers++;
                            if (unit.GatherState == GatherState.Idle)
                            {
                                idle++;
                            }
                            else
                            {
                                gathering++;
                            }
                        }
                        else
                        {
                            army.Add(unit.Definition.Id);
                            armyCount++;
                            armyN++;
                            armyX += unit.Position.X.ToFloatForPresentation();
                            armyY += unit.Position.Y.ToFloatForPresentation();
                            if (unit.IsAttackMoving)
                            {
                                attackMoving++;
                                atkN++;
                                atkX += unit.Position.X.ToFloatForPresentation();
                                atkY += unit.Position.Y.ToFloatForPresentation();
                            }
                        }
                    }
                    else if (!haveEnemyHq && unit.Definition.Hq != null && sim.Players.AreEnemies(unit.Owner, trace.Id))
                    {
                        enemyHq = unit.Position;
                        haveEnemyHq = true;
                    }
                }
                // Distances from the army centre (all, attack-moving) to the enemy HQ, as text. Unit: tiles.
                string armyDist = haveEnemyHq && armyN > 0 ? F1d(Dist(armyX / armyN, armyY / armyN, enemyHq)) : "n/a";
                string atkDist = haveEnemyHq && atkN > 0 ? F1d(Dist(atkX / atkN, atkY / atkN, enemyHq)) : "n/a";
                // Resource left in the fields within the base radius of the own HQ, as "C1200,R600" (kind letter and amount left), or "-" without an HQ or fields.
                StringBuilder near = new StringBuilder();
                if (hq != "none")
                {
                    // Squared base radius. Unit: squared tiles. Source: tuning/economy.json base_radius_tiles.
                    Fix64 baseRadiusSquared = sim.Tuning.Economy.BaseRadiusTiles * sim.Tuning.Economy.BaseRadiusTiles;
                    foreach (ResourceFieldState field in sim.Resources.All)
                    {
                        if ((new FixVec2(Fix64.FromInt(field.X) + Fix64.Half, Fix64.FromInt(field.Y) + Fix64.Half) - ownHq).LengthSquared() <= baseRadiusSquared)
                        {
                            near.Append(near.Length > 0 ? "," : string.Empty).Append(field.Kind == ResourceKind.Common ? 'C' : 'R').Append(field.Remaining.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
                // Army centre as text. Unit: tiles.
                string armyAt = armyN > 0 ? "(" + F1d(armyX / armyN) + "," + F1d(armyY / armyN) + ")" : "n/a";
                _out.Write("t=" + Seconds(tick) + "s " + trace.Label
                    + " bank=" + player.Common.ToString(CultureInfo.InvariantCulture) + "/" + player.Rare.ToString(CultureInfo.InvariantCulture)
                    + " +income=" + trace.IncomeCommon.ToString(CultureInfo.InvariantCulture) + "/" + trace.IncomeRare.ToString(CultureInfo.InvariantCulture)
                    + " supply=" + player.SupplyUsed.ToString(CultureInfo.InvariantCulture) + "/" + player.SupplyCap.ToString(CultureInfo.InvariantCulture)
                    + " workers=" + workers.ToString(CultureInfo.InvariantCulture) + " gather=" + gathering.ToString(CultureInfo.InvariantCulture)
                    + " idle=" + idle.ToString(CultureInfo.InvariantCulture)
                    + " done=" + done.Describe() + " building=" + building.Describe() + " army=" + army.Describe()
                    + " trained=" + trace.Trained.Describe() + " lost=" + trace.Lost.Describe()
                    + " hq=" + hq
                    + " cmds=" + trace.Intents.Describe() + " rejected=" + trace.Rejected.Describe()
                    + " atk=" + attackMoving.ToString(CultureInfo.InvariantCulture) + "/" + armyCount.ToString(CultureInfo.InvariantCulture)
                    + " armyDist=" + armyDist + " atkDist=" + atkDist
                    + " armyAt=" + armyAt + " nearFields=" + (near.Length > 0 ? near.ToString() : "-")
                    + " stance=" + trace.LastStance + " attacks=" + (trace.Memory?.AttacksStarted ?? 0).ToString(CultureInfo.InvariantCulture)
                    + " retreats=" + (trace.Memory?.Retreats ?? 0).ToString(CultureInfo.InvariantCulture)
                    + (isFinal ? " (final)" : string.Empty) + "\n");
                trace.ResetInterval();
            }
        }

        /// <summary>Distance between a point given in tiles as doubles and a fixed-point position. Unit: tiles.</summary>
        private static double Dist(double x, double y, FixVec2 point)
        {
            // Offsets on each axis. Unit: tiles.
            double dx = x - point.X.ToFloatForPresentation();
            double dy = y - point.Y.ToFloatForPresentation();
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Kinds of unit the trace tells apart.</summary>
        private enum UnitClass
        {
            /// <summary>The mobile HQ.</summary>
            Hq,

            /// <summary>A building, including static defences.</summary>
            Structure,

            /// <summary>A unit that can gather.</summary>
            Worker,

            /// <summary>Everything else that moves: the army.</summary>
            Army,
        }

        /// <summary>The trace class of a definition: HQ first, then structure, worker, army.</summary>
        private static UnitClass Classify(UnitDefinition definition)
        {
            if (definition.Hq != null)
            {
                return UnitClass.Hq;
            }
            if (definition.Economy.FootprintTiles > 0)
            {
                return UnitClass.Structure;
            }
            return definition.Economy.CanGather ? UnitClass.Worker : UnitClass.Army;
        }

        /// <summary>Whole seconds of a tick count at the match tick rate.</summary>
        private string Seconds(int tick)
        {
            return (tick / _ticksPerSecond).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"t=12s (tick 240)" or "never" for a first-tick value (-1 means it did not happen).</summary>
        private string TickText(int tick)
        {
            return tick < 0 ? "never" : Seconds(tick) + "s (tick " + tick.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>Fixed-point value with one decimal. Unit: as the value.</summary>
        private static string F1(Fix64 value)
        {
            return F1d(value.ToFloatForPresentation());
        }

        /// <summary>Double with one decimal, invariant culture.</summary>
        private static string F1d(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <summary>Fixed-point value rounded to a whole number.</summary>
        private static string F0(Fix64 value)
        {
            return value.RoundToInt().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Owner and definition of a unit seen at some point of the game.</summary>
        private sealed class Known
        {
            /// <summary>Owning player.</summary>
            public PlayerId Owner { get; }

            /// <summary>The unit's definition.</summary>
            public UnitDefinition Definition { get; }

            /// <summary>Stores the values.</summary>
            public Known(PlayerId owner, UnitDefinition definition)
            {
                Owner = owner;
                Definition = definition;
            }
        }

        /// <summary>One command an AI issued and what the simulation did with it.</summary>
        private sealed class CommandEntry
        {
            /// <summary>The player record that issued it.</summary>
            public PlayerTrace Owner { get; }

            /// <summary>The command object (also the key of the rejection event).</summary>
            public Command Command { get; }

            /// <summary>"pending" until the step, then "ok" or "REJECTED reason".</summary>
            public string Result { get; set; } = "pending";

            /// <summary>Stores the values.</summary>
            public CommandEntry(PlayerTrace owner, Command command)
            {
                Owner = owner;
                Command = command;
            }

            /// <summary>One readable line: tick, kind, parameters, result.</summary>
            public string Describe(Simulation sim)
            {
                // Parameters of the command as text.
                string detail;
                switch (Command)
                {
                    case TrainUnitCommand train:
                        detail = train.Producer + " " + train.DefinitionId;
                        break;
                    case BuildStructureCommand build:
                        detail = build.Worker + " " + build.DefinitionId + " at tile (" + build.TileX.ToString(CultureInfo.InvariantCulture) + "," + build.TileY.ToString(CultureInfo.InvariantCulture) + ")";
                        break;
                    case AttackMoveCommand attackMove:
                        detail = attackMove.Unit + " to (" + F1(attackMove.Goal.X) + "," + F1(attackMove.Goal.Y) + ")";
                        break;
                    case MoveUnitCommand move:
                        detail = move.Unit + " to (" + F1(move.Target.X) + "," + F1(move.Target.Y) + ")";
                        break;
                    case GatherCommand gather:
                        detail = gather.Unit + " field " + gather.FieldIndex.ToString(CultureInfo.InvariantCulture);
                        break;
                    case AttackUnitCommand attack:
                        detail = attack.Unit + " target " + attack.Target;
                        break;
                    case RootHqCommand root:
                        detail = root.Unit.ToString();
                        break;
                    default:
                        detail = string.Empty;
                        break;
                }
                return "tick " + Command.Tick.ToString(CultureInfo.InvariantCulture) + " " + Command.Kind + " " + detail + " -> " + Result;
            }
        }

        /// <summary>A multiset of names printed as "name:count,name:count" in ordinal order, or "-" when empty.</summary>
        private sealed class CountMap
        {
            /// <summary>Count per name. Unit: count. Range: 1 upward per entry.</summary>
            private readonly SortedDictionary<string, int> _counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Adds one to <paramref name="name"/>.</summary>
            public void Add(string name)
            {
                _counts[name] = _counts.TryGetValue(name, out int now) ? now + 1 : 1;
            }

            /// <summary>Forgets everything.</summary>
            public void Clear()
            {
                _counts.Clear();
            }

            /// <summary>The text form.</summary>
            public string Describe()
            {
                if (_counts.Count == 0)
                {
                    return "-";
                }
                // Text built entry by entry.
                StringBuilder text = new StringBuilder();
                foreach (KeyValuePair<string, int> pair in _counts)
                {
                    text.Append(text.Length > 0 ? "," : string.Empty).Append(pair.Key).Append(':').Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        /// <summary>Everything the trace knows about one player.</summary>
        private sealed class PlayerTrace
        {
            /// <summary>The player slot.</summary>
            public PlayerId Id { get; }

            /// <summary>"P0 skai" style label.</summary>
            public string Label { get; }

            /// <summary>Income of common resource since the last line. Unit: resource. Range: 0 upward.</summary>
            public int IncomeCommon { get; private set; }

            /// <summary>Income of rare resource since the last line. Unit: resource. Range: 0 upward.</summary>
            public int IncomeRare { get; private set; }

            /// <summary>Common resource gathered over the whole game. Unit: resource. Range: 0 upward.</summary>
            public int TotalCommon { get; private set; }

            /// <summary>Rare resource gathered over the whole game. Unit: resource. Range: 0 upward.</summary>
            public int TotalRare { get; private set; }

            /// <summary>Common resource spent on accepted train and build commands. Unit: resource. Range: 0 upward.</summary>
            public int SpentCommon { get; private set; }

            /// <summary>Rare resource spent on accepted train and build commands. Unit: resource. Range: 0 upward.</summary>
            public int SpentRare { get; private set; }

            /// <summary>Units trained since the last line, by definition.</summary>
            public CountMap Trained { get; } = new CountMap();

            /// <summary>Units trained over the whole game.</summary>
            public CountMap TotalTrained { get; } = new CountMap();

            /// <summary>Units lost since the last line, by definition.</summary>
            public CountMap Lost { get; } = new CountMap();

            /// <summary>Units lost over the whole game, by definition and removal reason.</summary>
            public CountMap TotalLost { get; } = new CountMap();

            /// <summary>Structures placed over the whole game.</summary>
            public CountMap TotalPlaced { get; } = new CountMap();

            /// <summary>Structures completed over the whole game.</summary>
            public CountMap TotalCompleted { get; } = new CountMap();

            /// <summary>Commands issued since the last line, by kind.</summary>
            public CountMap Intents { get; } = new CountMap();

            /// <summary>Commands rejected since the last line, by reason.</summary>
            public CountMap Rejected { get; } = new CountMap();

            /// <summary>Commands rejected over the whole game, by reason .</summary>
            public CountMap TotalRejected { get; } = new CountMap();

            /// <summary>Every command issued, in order.</summary>
            public List<CommandEntry> Log { get; } = new List<CommandEntry>();

            /// <summary>The controller's memory (stance, counters); null until its first think is seen.</summary>
            public AiMemory? Memory { get; set; }

            /// <summary>Stance after the previous think. Starts as Massing, the memory's start value.</summary>
            public AiStance LastStance { get; set; } = AiStance.Massing;

            /// <summary>Every stance change, as text with its tick.</summary>
            public List<string> StanceChanges { get; } = new List<string>();

            /// <summary>Tick the stance first became Attack (a real attack, not a rally move), or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstStanceAttackTick { get; set; } = -1;

            /// <summary>Every structure placement as text: tick, definition, tile.</summary>
            public List<string> Sites { get; } = new List<string>();

            /// <summary>Damage dealt to enemy HQs. Unit: health points. Range: 0 upward.</summary>
            public Fix64 HqDamage { get; set; } = Fix64.Zero;

            /// <summary>Tick the first worker was trained, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstWorkerTick { get; set; } = -1;

            /// <summary>Tick the first structure was placed, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstStructureTick { get; set; } = -1;

            /// <summary>Tick the first army unit was trained, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstArmyTick { get; set; } = -1;

            /// <summary>Tick of the first attack-move or attack-unit command, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstAttackOrderTick { get; set; } = -1;

            /// <summary>Tick of the first damage to an enemy HQ, or -1. Unit: ticks. Range: -1 upward.</summary>
            public int FirstHqDamageTick { get; set; } = -1;

            /// <summary>Creates the record.</summary>
            public PlayerTrace(PlayerId id, string faction)
            {
                Id = id;
                Label = "P" + id.Value.ToString(CultureInfo.InvariantCulture) + " " + faction;
            }

            /// <summary>Adds gathered resource to the interval and the game totals.</summary>
            public void AddIncome(ResourceKind kind, int amount)
            {
                if (kind == ResourceKind.Common)
                {
                    IncomeCommon += amount;
                    TotalCommon += amount;
                }
                else
                {
                    IncomeRare += amount;
                    TotalRare += amount;
                }
            }

            /// <summary>Adds the cost of a definition to the spent totals.</summary>
            public void Spend(UnitDefinition definition)
            {
                SpentCommon += definition.CostCommon;
                SpentRare += definition.CostRare;
            }

            /// <summary>Clears the since-last-line counters.</summary>
            public void ResetInterval()
            {
                IncomeCommon = 0;
                IncomeRare = 0;
                Trained.Clear();
                Lost.Clear();
                Intents.Clear();
                Rejected.Clear();
            }
        }
    }
}
