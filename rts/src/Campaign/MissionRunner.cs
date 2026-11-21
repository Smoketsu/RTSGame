// rts/src/Campaign/MissionRunner.cs
// Runs a mission beside a simulation (rts/docs/specs/campaign_scripting.md sections 3 and 4). Part of Rts.Campaign. The host calls Start once after
// Simulation.Start, ReportSignal for camera and selection between steps, and AfterStep after every Simulation.Step. The runner READS
// sim.Events, sim.Units, sim.Players and sim.Clock and SUBMITS ordinary commands with sim.Submit between steps; it calls no other method that changes the simulation
// (architecture rules 2 and 8), never steps, never draws from sim.Rng, and never issues commands as a human or AI slot. Mission state lives here, outside the
// simulation, and is not hashed (like AI memory). Replays need no runner, but only if every command the runner submits is recorded: pass the recorder's
// Submit as the submit sink (see the class summary for the call order). SINGLE-PLAYER ONLY for now: in a lockstep match each peer would run its own runner
// with its own camera and selection signals and submit outside the peers' command bundles, which would desync. Engine-free; Fix64 and integers only;
// collections are lists in file order or SortedDictionary; no clock, no random numbers, no static state.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;

namespace Rts.Campaign
{
    /// <summary>Where an objective stands. Range: the four states of the spec.</summary>
    public enum ObjectiveStatus
    {
        /// <summary>Waiting for its required objectives.</summary>
        Locked,

        /// <summary>Counting; shown in the objective panel.</summary>
        Active,

        /// <summary>Its complete_when held.</summary>
        Completed,

        /// <summary>Its fail_when held; the mission is lost.</summary>
        Failed,
    }

    /// <summary>How the mission stands. Range: the three states of the spec.</summary>
    public enum MissionStatus
    {
        /// <summary>Still being played.</summary>
        Running,

        /// <summary>The victory objective was completed.</summary>
        Won,

        /// <summary>Lost; see <see cref="MissionRunner.EndReason"/>.</summary>
        Lost,
    }

    /// <summary>The runner's memory of one objective. Read it for the objective panel; only the runner changes it.</summary>
    public sealed class ObjectiveState
    {
        /// <summary>The objective this state belongs to.</summary>
        public ObjectiveDefinition Definition { get; }

        /// <summary>Where the objective stands.</summary>
        public ObjectiveStatus Status { get; internal set; }

        /// <summary>First tick whose events count for this objective. Unit: ticks. Range: 0 for objectives active from the start, else the activation tick + 1; -1 while locked.</summary>
        public int FirstCountingTick { get; internal set; } = -1;

        /// <summary>Tick on which the objective completed. Unit: ticks. Range: -1 until completed, then 0 upward.</summary>
        public int CompletedTick { get; internal set; } = -1;

        /// <summary>Counters and latches of the complete_when tree.</summary>
        internal ConditionState[] CompleteStates { get; }

        /// <summary>Counters and latches of the fail_when tree (empty without one).</summary>
        internal ConditionState[] FailStates { get; }

        /// <summary>Builds the locked state.</summary>
        internal ObjectiveState(ObjectiveDefinition definition)
        {
            Definition = definition;
            CompleteStates = ConditionEvaluator.NewStates(definition.Complete);
            FailStates = ConditionEvaluator.NewStates(definition.Fail);
        }
    }

    /// <summary>The runner's memory of one trigger.</summary>
    internal sealed class TriggerState
    {
        /// <summary>The trigger.</summary>
        public readonly TriggerDefinition Definition;

        /// <summary>True once the trigger has run its actions; triggers fire at most once.</summary>
        public bool Fired;

        /// <summary>Counters and latches of a Condition trigger's tree (empty for other forms).</summary>
        public readonly ConditionState[] States;

        /// <summary>Builds the unfired state.</summary>
        public TriggerState(TriggerDefinition definition)
        {
            Definition = definition;
            States = ConditionEvaluator.NewStates(definition.Condition);
        }
    }

    /// <summary>A signal with the tick it arrived before.</summary>
    internal sealed class StampedSignal
    {
        /// <summary>The tick that was about to be stepped when the signal was reported. Unit: ticks. Range: 0 upward.</summary>
        public readonly int Tick;

        /// <summary>The signal.</summary>
        public readonly HostSignal Signal;

        /// <summary>Builds the stamped signal.</summary>
        public StampedSignal(int tick, HostSignal signal)
        {
            Tick = tick;
            Signal = signal;
        }
    }

    /// <summary>
    /// Plays one mission. One instance per match; not thread-safe. SINGLE-PLAYER ONLY for now: in a lockstep match every peer would run it with its own
    /// camera and selection signals and submit outside the peers' bundles, which would desync.
    /// Replays: every command the runner submits goes through the submit sink passed to the constructor (default <c>sim.Submit</c>), so a host that
    /// records passes <c>ReplayRecorder.Submit</c>. The recorder refuses a simulation that already holds queued commands, so the host's call order is:
    /// (1) <c>sim = mission.CreateSimulation(data)</c>; (2) <c>sim.Start()</c>; (3) <c>new ReplayRecorder(sim, mission.PlayerSetups())</c>;
    /// (4) <c>new MissionRunner(mission, presenter, recorder.Submit)</c>; (5) <c>runner.Start(sim)</c>, which submits the setup commands through the sink;
    /// (6) controllers' <c>OnMatchStart</c>; then per tick: inputs <c>BeforeStep(sim, runner, recorder.Submit)</c>, <c>sim.Step()</c>, <c>recorder.AfterStep()</c>,
    /// <c>runner.AfterStep(sim)</c>; at the end <c>recorder.Finish()</c>. A replay verifies with no runner attached.
    /// </summary>
    public sealed class MissionRunner
    {
        /// <summary>The mission played.</summary>
        public MissionDefinition Mission { get; }

        /// <summary>Whether the mission is running, won or lost.</summary>
        public MissionStatus Status { get; private set; } = MissionStatus.Running;

        /// <summary>Why the mission ended: "victory" is not used; a loss gives "defeat", "timeout", "objective_failed:&lt;id&gt;" or the lose_mission reason; empty while running and after a win. Range: snake_case text, or empty.</summary>
        public string EndReason { get; private set; } = string.Empty;

        /// <summary>Tick of the last step when the mission ended. Unit: ticks. Range: -1 while running, then 0 upward.</summary>
        public int EndTick { get; private set; } = -1;

        /// <summary>Objective states in file order. Range: one per objective of the mission.</summary>
        public IReadOnlyList<ObjectiveState> Objectives => _objectives;

        /// <summary>Output lines: OBJECTIVE, CUE, WARN and MISSION lines in the order they happened. Range: grows by a few lines per tick at most.</summary>
        public IReadOnlyList<string> Log => _log;

        /// <summary>Next sequence number per issuer the runner owns (the script issuer 255 and every script slot). Range: 0 upward each; the script issuer continues after the setup commands.</summary>
        public IReadOnlyDictionary<PlayerId, uint> NextSequence => _nextSequence;

        /// <summary>Where cues go, or null for none.</summary>
        private readonly IMissionPresenter? _presenter;

        /// <summary>Objective memory in file order.</summary>
        private readonly List<ObjectiveState> _objectives = new List<ObjectiveState>();

        /// <summary>Trigger memory in file order.</summary>
        private readonly List<TriggerState> _triggers = new List<TriggerState>();

        /// <summary>Counters of the defeat_when tree (empty without one).</summary>
        private readonly ConditionState[] _defeatStates;

        /// <summary>Signals reported and not yet counted, in arrival order.</summary>
        private readonly List<StampedSignal> _pendingSignals = new List<StampedSignal>();

        /// <summary>Next command sequence per issuer the runner owns.</summary>
        private readonly SortedDictionary<PlayerId, uint> _nextSequence = new SortedDictionary<PlayerId, uint>();

        /// <summary>Output lines.</summary>
        private readonly List<string> _log = new List<string>();

        /// <summary>The simulation passed to <see cref="Start"/>; null before.</summary>
        private Simulation? _sim;

        /// <summary>Reason of a loss requested during this AfterStep (lose_mission), or null.</summary>
        private string? _loseRequest;

        /// <summary>True when a win_mission action ran during this AfterStep.</summary>
        private bool _winRequest;

        /// <summary>Where the runner's commands go; null until <see cref="Start"/> (then the constructor's sink or <c>sim.Submit</c>).</summary>
        private Action<Command>? _submit;

        /// <summary>The sink passed to the constructor, or null for <c>sim.Submit</c>.</summary>
        private readonly Action<Command>? _sink;

        /// <summary>Tick of the last <see cref="AfterStep"/> processed. Unit: ticks. Range: -1 before the first.</summary>
        private int _lastTick = -1;

        /// <summary>Builds a runner for a mission.</summary>
        /// <param name="mission">The validated mission.</param>
        /// <param name="presenter">Receives cues; null for none (they still go to the log).</param>
        /// <param name="submit">Receives every command the runner submits (setup and actions); null means <c>sim.Submit</c>. Pass <c>ReplayRecorder.Submit</c> to record.</param>
        public MissionRunner(MissionDefinition mission, IMissionPresenter? presenter, Action<Command>? submit = null)
        {
            _sink = submit;
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _presenter = presenter;
            foreach (ObjectiveDefinition objective in mission.Objectives)
            {
                _objectives.Add(new ObjectiveState(objective));
            }
            foreach (TriggerDefinition trigger in mission.Triggers)
            {
                _triggers.Add(new TriggerState(trigger));
            }
            _defeatStates = ConditionEvaluator.NewStates(mission.DefeatWhen);
            _nextSequence[PlayerId.None] = 0;
            foreach (MissionPlayer player in mission.Players)
            {
                if (player.IsScript)
                {
                    _nextSequence[player.Setup.Id] = 0;
                }
            }
        }

        /// <summary>
        /// Starts the mission on a started simulation that has not stepped yet: submits the setup commands for tick 0 (issuer 255, sequences 0 upward
        /// in file order), activates the objectives that need nothing, and fires the triggers of mission_started and of those activations.
        /// </summary>
        /// <param name="sim">The started simulation, between steps.</param>
        public void Start(Simulation sim)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            _submit = _sink ?? sim.Submit;
            if (Status != MissionStatus.Running || _log.Count > 0 || sim.Clock.Tick != 0)
            {
                throw new InvalidOperationException("MissionRunner.Start needs a fresh runner and a simulation at tick 0.");
            }
            foreach (JsonValue node in Mission.Setup)
            {
                Submit(sim, node, PlayerId.None, sim.Clock.Tick);
            }
            // Objectives activated now, by index.
            List<int> activated = new List<int>();
            foreach (ObjectiveState objective in _objectives)
            {
                if (objective.Definition.Requires.Count == 0)
                {
                    Activate(objective, 0, 0);
                    activated.Add(objective.Definition.Index);
                }
            }
            FireTriggers(sim, 0, true, activated, new List<int>());
            Finish(0);
        }

        /// <summary>
        /// Reports a camera or selection signal from the host. It is stamped with the tick about to be stepped and counted by that tick's
        /// <see cref="AfterStep"/>; a signal for a condition that is not counting then is dropped. It never reaches the simulation.
        /// </summary>
        /// <param name="signal">The signal.</param>
        public void ReportSignal(HostSignal signal)
        {
            if (signal == null)
            {
                throw new ArgumentNullException(nameof(signal));
            }
            if (_sim == null)
            {
                throw new InvalidOperationException("MissionRunner.ReportSignal needs Start first.");
            }
            if (Status != MissionStatus.Running)
            {
                return;
            }
            _pendingSignals.Add(new StampedSignal(_sim.Clock.Tick, signal));
        }

        /// <summary>
        /// Processes the tick just stepped (t = Clock.Tick - 1): counts events and signals, completes and fails objectives, checks defeat, activates
        /// objectives, fires triggers, then decides win, loss or timeout. Does nothing once the mission has ended. Call after every Simulation.Step.
        /// </summary>
        /// <param name="sim">The simulation between steps (read only except Submit).</param>
        public void AfterStep(Simulation sim)
        {
            if (_sim == null || !ReferenceEquals(sim, _sim))
            {
                throw new InvalidOperationException("MissionRunner.AfterStep needs the simulation given to Start.");
            }
            if (Status != MissionStatus.Running)
            {
                return;
            }
            // The tick just stepped. Unit: ticks. Range: 0 upward.
            int t = sim.Clock.Tick - 1;
            if (t <= _lastTick)
            {
                throw new InvalidOperationException("MissionRunner.AfterStep ran for tick " + Num(t) + " after tick " + Num(_lastTick) + "; call it once per step.");
            }
            _lastTick = t;
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                if (simEvent is CommandRejectedEvent rejected)
                {
                    _log.Add("WARN tick=" + Num(rejected.Tick) + " " + rejected.Describe());
                }
            }
            // 1. Signals stamped for this tick; older ones are dropped (nothing was counting them).
            List<HostSignal> signals = new List<HostSignal>();
            foreach (StampedSignal stamped in _pendingSignals)
            {
                if (stamped.Tick == t)
                {
                    signals.Add(stamped.Signal);
                }
            }
            _pendingSignals.Clear();
            // 2. Counters of every tree that is counting.
            foreach (ObjectiveState objective in _objectives)
            {
                if (objective.Status == ObjectiveStatus.Active)
                {
                    ConditionEvaluator.Count(objective.Definition.Complete, objective.CompleteStates, sim, signals);
                    if (objective.Definition.Fail != null)
                    {
                        ConditionEvaluator.Count(objective.Definition.Fail, objective.FailStates, sim, signals);
                    }
                }
            }
            foreach (TriggerState trigger in _triggers)
            {
                if (!trigger.Fired && trigger.Definition.Condition != null)
                {
                    ConditionEvaluator.Count(trigger.Definition.Condition, trigger.States, sim, signals);
                }
            }
            if (Mission.DefeatWhen != null)
            {
                ConditionEvaluator.Count(Mission.DefeatWhen, _defeatStates, sim, signals);
            }
            // 3. Objectives in file order: complete, else fail.
            List<int> completed = new List<int>();
            foreach (ObjectiveState objective in _objectives)
            {
                if (objective.Status != ObjectiveStatus.Active)
                {
                    continue;
                }
                // Ticks this objective has been counting, this tick included. Unit: ticks.
                int elapsed = t + 1 - objective.FirstCountingTick;
                if (ConditionEvaluator.IsTrue(objective.Definition.Complete, objective.CompleteStates, sim, elapsed))
                {
                    objective.Status = ObjectiveStatus.Completed;
                    objective.CompletedTick = t;
                    completed.Add(objective.Definition.Index);
                    _log.Add("OBJECTIVE " + objective.Definition.Id + " complete tick=" + Num(t));
                }
                else if (objective.Definition.Fail != null && ConditionEvaluator.IsTrue(objective.Definition.Fail, objective.FailStates, sim, elapsed))
                {
                    objective.Status = ObjectiveStatus.Failed;
                    _log.Add("OBJECTIVE " + objective.Definition.Id + " failed tick=" + Num(t));
                    Lose(t, "objective_failed:" + objective.Definition.Id);
                    return;
                }
            }
            // 4. Defeat. A loss ends the mission at once: activation and triggers of this tick are skipped.
            if (Mission.DefeatWhen != null && ConditionEvaluator.IsTrue(Mission.DefeatWhen, _defeatStates, sim, t + 1))
            {
                Lose(t, "defeat");
                return;
            }
            // 5. Activation, in file order, after the completions above.
            List<int> activated = new List<int>();
            foreach (ObjectiveState objective in _objectives)
            {
                if (objective.Status == ObjectiveStatus.Locked && AllRequiredCompleted(objective))
                {
                    Activate(objective, t + 1, t + 1);
                    activated.Add(objective.Definition.Index);
                }
            }
            // 6. Triggers.
            FireTriggers(sim, t, false, activated, completed);
            // 7 and 8.
            Finish(t);
        }

        /// <summary>True when every required objective of <paramref name="objective"/> is completed.</summary>
        private bool AllRequiredCompleted(ObjectiveState objective)
        {
            foreach (int required in objective.Definition.Requires)
            {
                if (_objectives[required].Status != ObjectiveStatus.Completed)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Makes an objective active and logs it.</summary>
        /// <param name="objective">The objective.</param>
        /// <param name="firstCounting">First tick whose events count. Unit: ticks.</param>
        /// <param name="logTick">Tick written to the log line. Unit: ticks.</param>
        private void Activate(ObjectiveState objective, int firstCounting, int logTick)
        {
            objective.Status = ObjectiveStatus.Active;
            objective.FirstCountingTick = firstCounting;
            _log.Add("OBJECTIVE " + objective.Definition.Id + " active tick=" + Num(logTick));
        }

        /// <summary>Fires, in file order, every unfired trigger whose "when" holds now, and runs its actions.</summary>
        /// <param name="sim">The simulation.</param>
        /// <param name="t">The tick just stepped; for Start, 0. Unit: ticks.</param>
        /// <param name="started">True when called from Start (mission_started holds).</param>
        /// <param name="activated">Indexes of objectives activated this tick.</param>
        /// <param name="completed">Indexes of objectives completed this tick.</param>
        private void FireTriggers(Simulation sim, int t, bool started, List<int> activated, List<int> completed)
        {
            foreach (TriggerState trigger in _triggers)
            {
                if (trigger.Fired)
                {
                    continue;
                }
                // True when the trigger's "when" holds this tick.
                bool holds;
                switch (trigger.Definition.WhenKind)
                {
                    case TriggerWhenKind.MissionStarted:
                        holds = started;
                        break;
                    case TriggerWhenKind.ObjectiveActivated:
                        holds = activated.Contains(trigger.Definition.ObjectiveIndex);
                        break;
                    case TriggerWhenKind.ObjectiveCompleted:
                        holds = completed.Contains(trigger.Definition.ObjectiveIndex);
                        break;
                    default:
                        holds = !started && ConditionEvaluator.IsTrue(trigger.Definition.Condition!, trigger.States, sim, t + 1);
                        break;
                }
                if (!holds)
                {
                    continue;
                }
                trigger.Fired = true;
                foreach (MissionAction action in trigger.Definition.Actions)
                {
                    Run(sim, action);
                }
            }
        }

        /// <summary>Runs one action: submit commands, present a cue, or request the end of the mission.</summary>
        private void Run(Simulation sim, MissionAction action)
        {
            switch (action.Kind)
            {
                case ActionKind.Commands:
                    // Tick the commands apply at. Unit: ticks. Range: the next tick or later.
                    int tick = sim.Clock.Tick + sim.Clock.SecondsToTicks(action.DelaySeconds);
                    foreach (JsonValue node in action.CommandObjects)
                    {
                        Submit(sim, node, action.Issuer, tick);
                    }
                    break;
                case ActionKind.WinMission:
                    _winRequest = true;
                    break;
                case ActionKind.LoseMission:
                    _loseRequest ??= action.Reason;
                    break;
                default:
                    MissionCue cue = new MissionCue(sim.Clock.Tick, action);
                    _log.Add(DescribeCue(cue));
                    _presenter?.Show(cue);
                    break;
            }
        }

        /// <summary>Parses a command object with the real tick, issuer and next sequence of that issuer, and submits it.</summary>
        private void Submit(Simulation sim, JsonValue node, PlayerId issuer, int tick)
        {
            // Sequence number for this issuer. Unit: counter. Range: 0 upward.
            uint sequence = _nextSequence[issuer];
            _nextSequence[issuer] = sequence + 1;
            _submit!(CommandScript.ParseCommandObject(node, tick, issuer, sequence, Mission.SourceName, Mission.Parsers));
        }

        /// <summary>Decides the end of the mission after a tick's work: a requested or earlier loss beats a win, a win beats the timeout.</summary>
        /// <param name="t">The tick just stepped. Unit: ticks.</param>
        private void Finish(int t)
        {
            if (_loseRequest != null)
            {
                Lose(t, _loseRequest);
            }
            else if (_winRequest || _objectives[Mission.VictoryObjective].Status == ObjectiveStatus.Completed)
            {
                Status = MissionStatus.Won;
                EndTick = t;
                _log.Add("MISSION won tick=" + Num(t));
            }
            else if (t + 1 >= Mission.MaxSeconds * _sim!.Clock.TicksPerSecond)
            {
                Lose(t, "timeout");
            }
        }

        /// <summary>Ends the mission as lost and logs the final line (a timeout reads "MISSION timeout").</summary>
        private void Lose(int t, string reason)
        {
            Status = MissionStatus.Lost;
            EndReason = reason;
            EndTick = t;
            _log.Add(reason == "timeout" ? "MISSION timeout tick=" + Num(t) : "MISSION lost tick=" + Num(t) + " reason=" + reason);
        }

        /// <summary>The log line of a cue: CUE type tick=n then its fields.</summary>
        private static string DescribeCue(MissionCue cue)
        {
            StringBuilder line = new StringBuilder("CUE ");
            switch (cue.Kind)
            {
                case CueKind.ShowMessage:
                    line.Append("show_message tick=").Append(Num(cue.Tick)).Append(" seconds=").Append(cue.Seconds).Append(" text=").Append(cue.Text);
                    break;
                case CueKind.HighlightArea:
                    line.Append("highlight_area tick=").Append(Num(cue.Tick)).Append(" x=").Append(cue.X).Append(" y=").Append(cue.Y).Append(" radius_tiles=").Append(cue.RadiusTiles)
                        .Append(" seconds=").Append(cue.Seconds);
                    break;
                case CueKind.HighlightUnits:
                    line.Append("highlight_units tick=").Append(Num(cue.Tick)).Append(" units=");
                    for (int i = 0; i < cue.Units.Count; i++)
                    {
                        line.Append(i == 0 ? string.Empty : ",").Append(cue.Units[i].Value.ToString(CultureInfo.InvariantCulture));
                    }
                    line.Append(" seconds=").Append(cue.Seconds);
                    break;
                default:
                    line.Append("camera_focus tick=").Append(Num(cue.Tick)).Append(" x=").Append(cue.X).Append(" y=").Append(cue.Y);
                    break;
            }
            return line.ToString();
        }

        /// <summary>An integer as invariant text.</summary>
        private static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
