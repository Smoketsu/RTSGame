// rts/tools/BalanceHarness/SummaryObserver.cs
// The read-only game summary observer (spec: rts/docs/specs/balance_measurement.md M.4.2): GameRunner.Play calls Begin, AfterThink (before the step) and AfterStep (after it) and Finish, exactly as it calls GameTrace.
// It reads the commands the AIs submitted, the events of each step and the state after it, and builds one GameSummary per player slot; it never submits a command or steps the simulation, so the final state hash and
// the play signature of a game are the same with and without it. Spending comes from the shared PlayerLedger (the accounting of GameTrace); mechanic uses from MechanicDetectors. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Collects a <see cref="GameSummary"/> per player slot while a game runs.</summary>
    public sealed class SummaryObserver : IDetectorContext
    {
        /// <summary>Everything collected for one slot.</summary>
        private sealed class Slot
        {
            /// <summary>The spending account.</summary>
            public PlayerLedger Ledger { get; } = new PlayerLedger();

            /// <summary>The bank at each bank time, null until the time is reached. Unit: resource units.</summary>
            public int[]?[] Bank { get; }

            /// <summary>[common, rare] deposited per bucket so far. Unit: resource units.</summary>
            public List<int[]> Income { get; } = new List<int[]>();

            /// <summary>HQ moves as [uproot tick, root tick or -1, distance in tenths]; the distance is set when the HQ roots. Unit: ticks, ticks, tenths of a tile.</summary>
            public List<int[]> Moves { get; } = new List<int[]>();

            /// <summary>Units trained by definition ID.</summary>
            public SortedDictionary<string, int> Trained { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Tick of the first unit trained or research completed, by definition ID. Unit: ticks. Range: 0 upward.</summary>
            public SortedDictionary<string, int> FirstTick { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Structures (and research items) completed by definition ID.</summary>
            public SortedDictionary<string, int> Completed { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Mechanic uses by mechanic ID.</summary>
            public SortedDictionary<string, int> Mechanics { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>Own workers at each bank time, null until the time is reached. Unit: workers. Range: 0 upward.</summary>
            public int?[] Workers { get; }

            /// <summary>Completion ticks of own non-HQ drop-offs, in event order (ascending). Unit: ticks. Range: 0 upward.</summary>
            public List<int> DropOffs { get; } = new List<int>();

            /// <summary>Bundles the bank and worker arrays.</summary>
            public Slot(int bankTimes)
            {
                Bank = new int[bankTimes][];
                Workers = new int?[bankTimes];
            }
        }

        /// <summary>An HQ move whose root has not been seen yet: the index into the slot's move list and the HQ position at the uproot.</summary>
        private sealed class OpenMove
        {
            /// <summary>The owning slot's number. Range: 0 to 254.</summary>
            public byte Slot { get; }

            /// <summary>Index of the move in the slot's list. Range: 0 upward.</summary>
            public int Index { get; }

            /// <summary>HQ position at the uproot. Unit: tiles.</summary>
            public FixVec2 From { get; }

            /// <summary>Bundles the values.</summary>
            public OpenMove(byte slot, int index, FixVec2 from)
            {
                Slot = slot;
                Index = index;
                From = from;
            }
        }

        /// <summary>A unit seen at some point: owner and definition (kept after death so later events can still be attributed).</summary>
        private sealed class Known
        {
            /// <summary>Owning player.</summary>
            public PlayerId Owner { get; }

            /// <summary>The unit's definition.</summary>
            public UnitDefinition Definition { get; }

            /// <summary>Bundles the values.</summary>
            public Known(PlayerId owner, UnitDefinition definition)
            {
                Owner = owner;
                Definition = definition;
            }
        }

        /// <summary>Seconds per income bucket. Unit: seconds. Range: 5 to 600 (summary_bucket_seconds).</summary>
        private readonly int _bucketSeconds;

        /// <summary>Seconds of each bank snapshot, ascending. Unit: seconds. Range: 1 to 8 entries of 1 to 7200 (summary_bank_times_seconds).</summary>
        private readonly IReadOnlyList<int> _bankSeconds;

        /// <summary>Bucket length in ticks, set by <see cref="Begin"/>. Unit: ticks. Range: 1 upward; 1 before Begin.</summary>
        private int _bucketTicks = 1;

        /// <summary>Tick of each bank snapshot, set by <see cref="Begin"/>. Unit: ticks. Range: 1 upward.</summary>
        private int[] _bankTicks = Array.Empty<int>();

        /// <summary>The slots by player number, ascending.</summary>
        private readonly SortedDictionary<byte, Slot> _slots = new SortedDictionary<byte, Slot>();

        /// <summary>Every unit seen so far.</summary>
        private readonly SortedDictionary<EntityId, Known> _known = new SortedDictionary<EntityId, Known>();

        /// <summary>HQ moves that have not rooted yet, by HQ entity.</summary>
        private readonly SortedDictionary<EntityId, OpenMove> _open = new SortedDictionary<EntityId, OpenMove>();

        /// <summary>Train and build commands submitted for the next step and not yet resolved, with their slot.</summary>
        private readonly List<KeyValuePair<byte, Command>> _pending = new List<KeyValuePair<byte, Command>>();

        /// <summary>The simulation being watched, set by <see cref="Begin"/>; read only.</summary>
        private Simulation? _sim;

        /// <summary>Effective layer of every unit seen, as of the last step it existed in (read by the detectors).</summary>
        private readonly SortedDictionary<EntityId, MovementLayer> _layers = new SortedDictionary<EntityId, MovementLayer>();

        /// <summary>Creates an observer.</summary>
        /// <param name="bucketSeconds">Seconds per income bucket. Unit: seconds. Range: 5 to 600.</param>
        /// <param name="bankSeconds">Seconds of the bank snapshots, ascending. Unit: seconds. Range: 1 to 8 entries of 1 to 7200.</param>
        public SummaryObserver(int bucketSeconds, IReadOnlyList<int> bankSeconds)
        {
            _bucketSeconds = bucketSeconds;
            _bankSeconds = bankSeconds;
        }

        /// <summary>Called once after the controllers started and before the first step.</summary>
        /// <param name="sim">The started simulation.</param>
        public void Begin(Simulation sim)
        {
            _sim = sim;
            _bucketTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(_bucketSeconds));
            _bankTicks = new int[_bankSeconds.Count];
            for (int i = 0; i < _bankSeconds.Count; i++)
            {
                _bankTicks[i] = sim.Clock.SecondsToTicks(Fix64.FromInt(_bankSeconds[i]));
            }
            foreach (Player player in sim.Players.All)
            {
                _slots.Add(player.Id.Value, new Slot(_bankSeconds.Count));
            }
            Learn(sim);
        }

        /// <summary>Called after the controllers thought and before the step: notes the train and build commands each submitted.</summary>
        /// <param name="controllers">The game's controllers.</param>
        public void AfterThink(IReadOnlyList<IPlayerController> controllers)
        {
            foreach (IPlayerController controller in controllers)
            {
                if (!(controller is SkirmishAiController ai) || !_slots.ContainsKey(ai.Player.Value))
                {
                    continue;
                }
                foreach (Command command in ai.LastCommands)
                {
                    if (command is TrainUnitCommand || command is BuildStructureCommand)
                    {
                        _pending.Add(new KeyValuePair<byte, Command>(ai.Player.Value, command));
                    }
                }
            }
        }

        /// <summary>Called right after the step: reads its events and the state.</summary>
        /// <param name="sim">The simulation after the step.</param>
        public void AfterStep(Simulation sim)
        {
            Learn(sim);
            // Commands the simulation refused this step.
            HashSet<Command> rejected = new HashSet<Command>();
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                if (simEvent is CommandRejectedEvent reject)
                {
                    rejected.Add(reject.Command);
                }
            }
            foreach (KeyValuePair<byte, Command> entry in _pending)
            {
                if (rejected.Contains(entry.Value))
                {
                    continue;
                }
                // Definition ID the command orders.
                string definitionId = entry.Value is TrainUnitCommand train ? train.DefinitionId : ((BuildStructureCommand)entry.Value).DefinitionId;
                if (sim.Content.Units.TryGetValue(definitionId, out UnitDefinition? definition))
                {
                    _slots[entry.Key].Ledger.Spend(definition);
                }
                else if (sim.Content.Researches.TryGetValue(definitionId, out ResearchDefinition? research))
                {
                    _slots[entry.Key].Ledger.SpendResearch(research);
                }
            }
            _pending.Clear();
            // Mechanic uses of this step's events, credited in event order.
            List<PlayerId> credited = new List<PlayerId>();
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                ReadEvent(sim, simEvent);
                foreach (MechanicDetector detector in MechanicDetectors.All)
                {
                    credited.Clear();
                    detector.Detect(simEvent, this, credited);
                    foreach (PlayerId player in credited)
                    {
                        if (_slots.TryGetValue(player.Value, out Slot? slot))
                        {
                            slot.Mechanics[detector.Id] = (slot.Mechanics.TryGetValue(detector.Id, out int n) ? n : 0) + 1;
                        }
                    }
                }
            }
            for (int i = 0; i < _bankTicks.Length; i++)
            {
                if (sim.Clock.Tick == _bankTicks[i])
                {
                    foreach (Player player in sim.Players.All)
                    {
                        _slots[player.Id.Value].Bank[i] = new[] { player.Common, player.Rare };
                        // Own workers now alive: can gather, no footprint. Unit: workers. Range: 0 upward.
                        int workers = 0;
                        foreach (Unit unit in sim.Units.All)
                        {
                            if (unit.Owner == player.Id && unit.Definition.Economy.CanGather && unit.Definition.Economy.FootprintTiles == 0)
                            {
                                workers++;
                            }
                        }
                        _slots[player.Id.Value].Workers[i] = workers;
                    }
                }
            }
        }

        /// <summary>Called once after the last step: closes the open HQ moves and returns the summaries, one per slot in ascending order.</summary>
        /// <param name="lastTick">Last tick stepped. Unit: ticks. Range: 0 upward.</param>
        public GameSummary[] Finish(int lastTick)
        {
            // Buckets that have started by the last tick. Unit: buckets. Range: 1 upward.
            int buckets = lastTick / _bucketTicks + 1;
            List<GameSummary> summaries = new List<GameSummary>();
            foreach (Slot slot in _slots.Values)
            {
                while (slot.Income.Count < buckets)
                {
                    slot.Income.Add(new int[2]);
                }
                summaries.Add(new GameSummary(slot.Ledger.SpentCommon, slot.Ledger.SpentRare, slot.Ledger.ByClass, slot.Bank, slot.Income, slot.Moves, slot.Trained, slot.Completed, slot.Mechanics, slot.Workers, slot.DropOffs, slot.FirstTick));
            }
            return summaries.ToArray();
        }

        // ------------------------------------------------------------------ IDetectorContext

        /// <inheritdoc />
        public PlayerId OwnerOf(EntityId unit)
        {
            return _known.TryGetValue(unit, out Known? known) ? known.Owner : PlayerId.None;
        }

        /// <inheritdoc />
        public UnitDefinition? DefinitionOf(EntityId unit)
        {
            return _known.TryGetValue(unit, out Known? known) ? known.Definition : null;
        }

        /// <inheritdoc />
        public MovementLayer? LayerOf(EntityId unit)
        {
            return _layers.TryGetValue(unit, out MovementLayer layer) ? layer : (MovementLayer?)null;
        }

        /// <inheritdoc />
        public IReadOnlyList<PlayerId> PlayersOfTeam(TeamId team)
        {
            List<PlayerId> players = new List<PlayerId>();
            if (_sim != null)
            {
                foreach (Player player in _sim.Players.All)
                {
                    if (player.Team == team)
                    {
                        players.Add(player.Id);
                    }
                }
            }
            return players;
        }

        // ------------------------------------------------------------------ events

        /// <summary>Remembers the owner and definition of every unit now in the simulation, and its layer.</summary>
        private void Learn(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (!_known.ContainsKey(unit.Id))
                {
                    _known.Add(unit.Id, new Known(unit.Owner, unit.Definition));
                }
                _layers[unit.Id] = unit.EffectiveLayer;
            }
        }

        /// <summary>Folds one event into the slots: income, refunds, trained and completed counts, HQ moves.</summary>
        private void ReadEvent(Simulation sim, SimEvent simEvent)
        {
            if (simEvent is ResourceDepositedEvent deposit && _slots.TryGetValue(deposit.Owner.Value, out Slot? depositor))
            {
                // Bucket the deposit falls in. Unit: buckets. Range: 0 upward.
                int bucket = deposit.Tick / _bucketTicks;
                while (depositor.Income.Count <= bucket)
                {
                    depositor.Income.Add(new int[2]);
                }
                depositor.Income[bucket][deposit.Resource == ResourceKind.Common ? 0 : 1] += deposit.Amount;
            }
            else if (simEvent is BuildOrderCancelledEvent cancelled && _slots.TryGetValue(cancelled.Owner.Value, out Slot? canceller))
            {
                canceller.Ledger.Refund(sim.Content.Units.TryGetValue(cancelled.DefinitionId, out UnitDefinition? cancelledDefinition) ? cancelledDefinition : null, cancelled.RefundCommon, cancelled.RefundRare);
            }
            else if (simEvent is UnitTrainedEvent trained && _known.TryGetValue(trained.Unit, out Known? newUnit) && _slots.TryGetValue(newUnit.Owner.Value, out Slot? producer))
            {
                producer.Trained[trained.DefinitionId] = (producer.Trained.TryGetValue(trained.DefinitionId, out int count) ? count : 0) + 1;
                producer.FirstTick.TryAdd(trained.DefinitionId, trained.Tick);
            }
            else if (simEvent is StructureCompletedEvent done && _known.TryGetValue(done.Structure, out Known? built) && _slots.TryGetValue(built.Owner.Value, out Slot? builder))
            {
                builder.Completed[done.DefinitionId] = (builder.Completed.TryGetValue(done.DefinitionId, out int count) ? count : 0) + 1;
                if (built.Definition.Economy.IsDropOff && built.Definition.Hq == null)
                {
                    builder.DropOffs.Add(done.Tick);
                }
            }
            else if (simEvent is ResearchCompletedEvent researched && _slots.TryGetValue(researched.Owner.Value, out Slot? researcher))
            {
                researcher.Completed[researched.ResearchId] = (researcher.Completed.TryGetValue(researched.ResearchId, out int count) ? count : 0) + 1;
                researcher.FirstTick.TryAdd(researched.ResearchId, researched.Tick);
            }
            else if (simEvent is HqStateChangedEvent hq && _known.TryGetValue(hq.Unit, out Known? hqKnown) && _slots.TryGetValue(hqKnown.Owner.Value, out Slot? hqSlot) && sim.Units.TryGet(hq.Unit, out Unit? hqUnit))
            {
                if (hq.From == HqState.Rooted && hq.To == HqState.Uprooting)
                {
                    hqSlot.Moves.Add(new[] { hq.Tick, -1, 0 });
                    _open[hq.Unit] = new OpenMove(hqKnown.Owner.Value, hqSlot.Moves.Count - 1, hqUnit.Position);
                }
                else if (hq.From == HqState.Rooting && hq.To == HqState.Rooted && _open.TryGetValue(hq.Unit, out OpenMove? move))
                {
                    int[] entry = hqSlot.Moves[move.Index];
                    entry[1] = hq.Tick;
                    entry[2] = ((hqUnit.Position - move.From).Length() * 10).RoundToInt();
                    _open.Remove(hq.Unit);
                }
            }
        }
    }
}
