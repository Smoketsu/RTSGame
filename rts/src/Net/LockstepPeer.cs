// rts/src/Net/LockstepPeer.cs
// One peer of a lockstep session: wraps a Simulation, turns local input into per-tick command bundles, steps a tick
// only when every live peer's bundle for it is in, exchanges state hashes at the hash interval, and runs the pause,
// drop, rejoin and reconnect rules of ADR-008. Part of Rts.Net. Engine-free and single-threaded: the host calls
// Update(elapsedMs) from its own loop with the milliseconds since the last call; nothing here reads a clock, spawns
// a thread or touches a file. The transport is anything that implements ITransport (LoopbackTransport in tests,
// a Steam adapter in Unity).
//
// Roles. Every peer is symmetric except the referee: the lowest-numbered roster peer that is connected and live
// (not dropped). The referee alone decides what every peer must agree on: when a peer is dropped and up to which
// tick its bundles count (PeerDropped), when a pause starts and ends and how long it lasted (PauseStarted,
// PauseEnded), when a reconnecting peer is back and from which tick (PeerRejoined), and it injects the protocol's
// own simulation commands (connection state, surrender on a no-show) as ordinary commands in its bundle, issued
// by PlayerId.None with sequence numbers in the protocol range (bit 31 set, peer id in bits 20 to 27), which can
// never collide with the match script's issuer-None sequences.
//
// Tick flow. Before stepping tick t the peer sends its bundle for tick t + InputDelayTicks (so local input at tick
// t applies at t + delay on every peer), then steps t once it holds, for every roster peer p whose required
// ranges contain t, p's bundle for t. Commands of all required bundles are submitted to the simulation, which
// sorts them by (issuer, sequence) itself, so arrival order never matters. Every StateHashedEvent is sent to the
// others; hashes are compared with each peer in ascending tick order and the first mismatch stops the peer with a
// Desynced notice naming that tick.
//
// Reconnect. A fresh LockstepPeer instance calls Join: it asks the referee for the SyncLog (every bundle the referee
// holds, the ledger, the required ranges and the active pause; sent as SyncLogPart messages under the transport's
// payload cap), builds a fresh Simulation through the factory, replays ticks 0 to RefereeTick - 1 at up to
// CatchUpMaxTicksPerUpdate per Update, reports the hash at the referee's last hash tick, and once the referee verifies
// it, PeerRejoined names the first tick its bundles are required for again: the referee's NextInputTick, one past the
// last bundle the referee has sent, which no peer can have stepped because stepping a tick needs the referee's bundle
// for it. A peer that finds itself past that tick raises Inconsistent. A sync request from a peer the referee still
// holds live is a restart the transport has not reported: the referee drops it first, then answers.
//
// Recording. The match factory returns a LockstepMatch (empty started simulation plus setup commands); the peer raises
// SimulationCreated before submitting anything, CommandSubmitted for every command it submits (setup and bundles) and
// Stepped after every step, so a host-side ReplayRecorder (ADR-009) attached in SimulationCreated records the whole match.
using System;
using System.Collections.Generic;
using Rts.Net.Codecs;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Teams;

namespace Rts.Net
{
    /// <summary>Where a peer is in its life. The pause state is separate (<see cref="LockstepPeer.ActivePause"/>).</summary>
    public enum LockstepPhase : byte
    {
        /// <summary>Built, neither <see cref="LockstepPeer.Start"/> nor <see cref="LockstepPeer.Join"/> called yet.</summary>
        Created = 0,

        /// <summary>Live: steps whenever the bundles are in.</summary>
        Running = 1,

        /// <summary>Live but a pause is active: no stepping until it ends.</summary>
        Paused = 2,

        /// <summary>Reconnecting: waiting for or replaying the sync log, then waiting to be rejoined.</summary>
        Joining = 3,

        /// <summary>A hash mismatch was found; the peer no longer steps.</summary>
        Desynced = 4,

        /// <summary>The session is over for this peer (reconnect refused, or an inconsistency found).</summary>
        Failed = 5,
    }

    /// <summary>One peer of a lockstep session. See the file header for the protocol.</summary>
    public sealed class LockstepPeer
    {
        /// <summary>Bit set in the sequence of every command the protocol injects (issuer None), above the script's range. Range: fixed constant.</summary>
        public const uint ProtocolSequenceFlag = 0x80000000u;

        /// <summary>Bit position of the injecting peer's id inside a protocol sequence number. Range: fixed constant.</summary>
        public const int ProtocolSequencePeerShift = 20;

        /// <summary>Mask of the per-peer counter inside a protocol sequence number (bits 0 to 19). Range: fixed constant.</summary>
        public const uint ProtocolSequenceCounterMask = (1u << ProtocolSequencePeerShift) - 1;

        /// <summary>Milliseconds per second, for the tick accumulator. Range: fixed constant.</summary>
        private const long MsPerSecond = 1000;

        /// <summary>The active pause as this peer sees it.</summary>
        private sealed class PauseState
        {
            /// <summary>The referee's announcement.</summary>
            public PauseStartedMessage Start;

            /// <summary>Time this peer has counted since the pause began. Unit: milliseconds. Range: 0 upward.</summary>
            public int ElapsedMs;

            /// <summary>Stores the announcement with 0 elapsed.</summary>
            public PauseState(PauseStartedMessage start)
            {
                Start = start;
            }
        }

        // ---------------------------------------------------------------- configuration

        /// <summary>Who controls which players.</summary>
        public LockstepRoster Roster { get; }

        /// <summary>This peer's id.</summary>
        public PeerId LocalPeer { get; }

        /// <summary>The protocol limits.</summary>
        public LockstepSettings Settings { get; }

        /// <summary>The transport messages travel over.</summary>
        private readonly ITransport _transport;

        /// <summary>Builds a fresh match (empty started simulation plus setup commands); called at Start and on every reconnect.</summary>
        private readonly Func<LockstepMatch> _factory;

        /// <summary>Encodes and decodes every message.</summary>
        private readonly MessageCodec _codec;

        // ---------------------------------------------------------------- session state

        /// <summary>The simulation being stepped; null until Start or the sync log arrives.</summary>
        private Simulation? _sim;

        /// <summary>Roster peers reachable through the transport, plus this peer itself.</summary>
        private readonly SortedSet<PeerId> _connected = new SortedSet<PeerId>();

        /// <summary>Roster peers outside the live set (dropped by the referee, or this peer while joining).</summary>
        private readonly SortedSet<PeerId> _dropped = new SortedSet<PeerId>();

        /// <summary>Ticks for which each roster peer's bundle is required, ascending ranges per peer.</summary>
        private readonly SortedDictionary<PeerId, List<TickRange>> _ranges = new SortedDictionary<PeerId, List<TickRange>>();

        /// <summary>Every bundle held, by tick then peer: the command log.</summary>
        private readonly SortedDictionary<int, SortedDictionary<PeerId, CommandBundleMessage>> _bundles = new SortedDictionary<int, SortedDictionary<PeerId, CommandBundleMessage>>();

        /// <summary>Highest bundle tick held from each peer, for the resend rule. Unit: ticks. Range: -1 upward.</summary>
        private readonly SortedDictionary<PeerId, int> _highestBundleTick = new SortedDictionary<PeerId, int>();

        /// <summary>Local commands not yet sent, by their tick.</summary>
        private readonly SortedDictionary<int, List<Command>> _pendingLocal = new SortedDictionary<int, List<Command>>();

        /// <summary>Tick of the last bundle this peer sent. Unit: ticks. Range: -1 upward.</summary>
        private int _lastSentTick = -1;

        /// <summary>Tick for which <see cref="BeforeStep"/> was last raised, so a blocked tick does not raise it twice. Unit: ticks. Range: -1 upward.</summary>
        private int _thoughtTick = -1;

        /// <summary>This peer's hashes by hash tick.</summary>
        private readonly SortedDictionary<int, ulong> _ownHashes = new SortedDictionary<int, ulong>();

        /// <summary>Remote hashes by peer then hash tick.</summary>
        private readonly SortedDictionary<PeerId, SortedDictionary<int, ulong>> _remoteHashes = new SortedDictionary<PeerId, SortedDictionary<int, ulong>>();

        /// <summary>Next hash tick to compare with each peer. Unit: ticks. Range: multiples of the hash interval.</summary>
        private readonly SortedDictionary<PeerId, int> _nextCompareTick = new SortedDictionary<PeerId, int>();

        /// <summary>Bundles asked from the referee, keyed (peer id shifted by 32) | tick, with the referee's highest bundle tick at the time of the ask (a re-ask is allowed once that advances). Unit of the value: ticks.</summary>
        private readonly SortedDictionary<ulong, int> _requestedBundles = new SortedDictionary<ulong, int>();

        /// <summary>Elapsed time not yet turned into steps. Unit: milliseconds times ticks per second (one tick = 1000). Range: 0 to the catch-up cap times 1000.</summary>
        private long _tickAccumulator;

        /// <summary>Counter for this peer's protocol sequence numbers. Range: 0 to <see cref="ProtocolSequenceCounterMask"/>.</summary>
        private uint _protocolCounter;

        /// <summary>The active pause, or null.</summary>
        private PauseState? _pause;

        /// <summary>Next pause number the referee hands out. Range: 1 upward.</summary>
        private uint _nextPauseId = 1;

        /// <summary>Pause ends that arrived before their start (reordered transport), by pause id.</summary>
        private readonly SortedDictionary<uint, PauseEndedMessage> _earlyPauseEnds = new SortedDictionary<uint, PauseEndedMessage>();

        /// <summary>Pause starts that arrived while another pause was active (reordered transport), in arrival order.</summary>
        private readonly List<PauseStartedMessage> _pendingPauseStarts = new List<PauseStartedMessage>();

        /// <summary>The sync log being replayed, or null.</summary>
        private SyncLog? _syncLog;

        /// <summary>Joiner side: sync log parts received so far from the join target, in index order; emptied once joined into <see cref="_syncLog"/>.</summary>
        private readonly List<SyncLogPartMessage> _syncLogParts = new List<SyncLogPartMessage>();

        /// <summary>The peer this joiner asked for the log, or None.</summary>
        private PeerId _joinTarget = PeerId.None;

        /// <summary>Referee side: peers that received a sync log and owe a SyncDone, with the hash tick they must report.</summary>
        private readonly SortedDictionary<PeerId, int> _syncing = new SortedDictionary<PeerId, int>();

        /// <summary>Referee side: players this referee marked Disconnected in the simulation (a drop with no pause left), to mark Connected again on rejoin.</summary>
        private readonly SortedSet<PlayerId> _markedDisconnected = new SortedSet<PlayerId>();

        /// <summary>Notices raised during the current Update.</summary>
        private readonly List<LockstepNotice> _notices = new List<LockstepNotice>();

        // ---------------------------------------------------------------- public view

        /// <summary>Where the peer is in its life.</summary>
        public LockstepPhase Phase { get; private set; } = LockstepPhase.Created;

        /// <summary>The simulation this peer steps. Throws before Start or before the sync log arrives.</summary>
        public Simulation Simulation => _sim ?? throw new InvalidOperationException("The peer has no simulation yet (call Start, or Join and wait for the sync log).");

        /// <summary>True once a simulation exists.</summary>
        public bool HasSimulation => _sim != null;

        /// <summary>Pause counters of every player.</summary>
        public PauseLedger Ledger { get; }

        /// <summary>The active pause announcement, or null.</summary>
        public PauseStartedMessage? ActivePause => _pause?.Start;

        /// <summary>Milliseconds this peer has counted on the active pause; 0 when none. Unit: milliseconds. Range: 0 upward.</summary>
        public int PauseElapsedMs => _pause?.ElapsedMs ?? 0;

        /// <summary>True while this peer is in the live set (its bundles are required and it steps with the others).</summary>
        public bool IsLive => !_dropped.Contains(LocalPeer) && Phase != LockstepPhase.Created;

        /// <summary>The referee as this peer sees it: the lowest roster peer that is connected and live; None when there is none.</summary>
        public PeerId Referee
        {
            get
            {
                foreach (PeerId peer in Roster.Peers)
                {
                    if (_connected.Contains(peer) && !_dropped.Contains(peer))
                    {
                        return peer;
                    }
                }
                return PeerId.None;
            }
        }

        /// <summary>True when this peer is the referee.</summary>
        public bool IsReferee => Referee == LocalPeer;

        /// <summary>Peers outside the live set, ascending.</summary>
        public IEnumerable<PeerId> DroppedPeers => _dropped;

        /// <summary>Tick a command submitted now is scheduled for: the current tick plus the input delay, or the tick after the last sent bundle, whichever is later. Unit: ticks.</summary>
        public int NextInputTick => Math.Max(Simulation.Clock.Tick + Settings.InputDelayTicks, _lastSentTick + 1);

        /// <summary>Notices raised by the most recent <see cref="Update"/>, in order.</summary>
        public IReadOnlyList<LockstepNotice> Notices => _notices;

        /// <summary>Highest hash tick up to which this peer's hashes and <paramref name="peer"/>'s were compared equal; -1 before the first comparison. Unit: ticks.</summary>
        /// <param name="peer">A remote roster peer.</param>
        public int ComparedThroughTick(PeerId peer)
        {
            return _nextCompareTick.TryGetValue(peer, out int next) ? next - Simulation.Settings.StateHashIntervalTicks : -1;
        }

        /// <summary>Raised for every notice as it happens, for hosts that prefer callbacks to polling <see cref="Notices"/>.</summary>
        public event Action<LockstepNotice>? NoticeRaised;

        /// <summary>
        /// Raised once per tick, right before this peer closes its bundle for tick + delay and tries to step: the host runs its
        /// AI or flushes player input here, submitting through <see cref="SubmitLocal"/>. Not raised while joining or paused.
        /// </summary>
        public event Action<LockstepPeer>? BeforeStep;

        /// <summary>
        /// Raised with each simulation the factory builds (at Start and on every reconnect) before any command is submitted to
        /// it: a host attaches its replay recorder here. The simulation is started, at tick 0 and holds no command.
        /// </summary>
        public event Action<Simulation>? SimulationCreated;

        /// <summary>Raised after every command this peer submits to its simulation (setup commands, then every applied bundle's commands), in submission order: a host's recorder records it.</summary>
        public event Action<Command>? CommandSubmitted;

        /// <summary>Raised after every simulation step, before the tick's hash is sent, while <see cref="Simulation.Events"/> still holds the tick's events: a host's recorder collects the checkpoint.</summary>
        public event Action<Simulation>? Stepped;

        /// <summary>Every bundle this peer applies or will apply, ascending by (tick, peer): bundles of required ranges only.</summary>
        public IEnumerable<CommandBundleMessage> CommandLog
        {
            get
            {
                foreach (KeyValuePair<int, SortedDictionary<PeerId, CommandBundleMessage>> tick in _bundles)
                {
                    foreach (KeyValuePair<PeerId, CommandBundleMessage> entry in tick.Value)
                    {
                        if (IsRequired(entry.Key, tick.Key))
                        {
                            yield return entry.Value;
                        }
                    }
                }
            }
        }

        /// <summary>Builds a peer. Nothing happens until <see cref="Start"/> or <see cref="Join"/>.</summary>
        /// <param name="roster">Who controls which players; must contain <paramref name="localPeer"/>.</param>
        /// <param name="localPeer">This peer's id.</param>
        /// <param name="transport">This peer's transport; its LocalPeer must equal <paramref name="localPeer"/>.</param>
        /// <param name="matchFactory">Builds a fresh match: an empty started simulation and the setup commands the peer submits to it.</param>
        /// <param name="codecs">Command codecs for every kind the match can issue.</param>
        /// <param name="settings">The protocol limits.</param>
        public LockstepPeer(LockstepRoster roster, PeerId localPeer, ITransport transport, Func<LockstepMatch> matchFactory, CommandCodecRegistry codecs, LockstepSettings settings)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (!roster.Contains(localPeer))
            {
                throw new ArgumentException("Peer " + localPeer + " is not in the roster.", nameof(localPeer));
            }
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (transport.LocalPeer != localPeer)
            {
                throw new ArgumentException("The transport belongs to " + transport.LocalPeer + ", not " + localPeer + ".", nameof(transport));
            }
            LocalPeer = localPeer;
            _factory = matchFactory ?? throw new ArgumentNullException(nameof(matchFactory));
            _codec = new MessageCodec(codecs ?? throw new ArgumentNullException(nameof(codecs)));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Ledger = new PauseLedger(roster.AllPlayers, settings);
            foreach (PeerId peer in roster.Peers)
            {
                _ranges.Add(peer, new List<TickRange> { new TickRange(0, int.MaxValue) });
                _highestBundleTick.Add(peer, -1);
                if (peer != localPeer)
                {
                    _remoteHashes.Add(peer, new SortedDictionary<int, ulong>());
                    _nextCompareTick.Add(peer, 0);
                }
            }
        }

        // ---------------------------------------------------------------- host entry points

        /// <summary>
        /// Starts a fresh match: builds the simulation, takes the transport's connected peers as the session, sends the empty
        /// bundles for the first delay ticks. Every other roster peer must already be connected (the lobby waits for them);
        /// otherwise this throws rather than start a match that would at once drop the missing peers and spend their only
        /// disconnect pause.
        /// </summary>
        public void Start()
        {
            if (Phase != LockstepPhase.Created)
            {
                throw new InvalidOperationException("Start can only be called once, on a new peer.");
            }
            foreach (PeerId peer in Roster.Peers)
            {
                if (peer != LocalPeer && !_transport.IsConnected(peer))
                {
                    throw new InvalidOperationException("Roster peer " + peer + " is not connected; every peer must be connected before Start.");
                }
            }
            _sim = CreateSimulation();
            SeedConnected();
            Phase = LockstepPhase.Running;
            SendBundlesUpTo(Settings.InputDelayTicks - 1);
        }

        /// <summary>Reconnects to a running session: asks the lowest connected roster peer for the command log. The simulation exists once the log arrives.</summary>
        public void Join()
        {
            if (Phase != LockstepPhase.Created)
            {
                throw new InvalidOperationException("Join can only be called once, on a new peer.");
            }
            SeedConnected();
            _dropped.Add(LocalPeer);
            Phase = LockstepPhase.Joining;
            foreach (PeerId peer in Roster.Peers)
            {
                if (peer != LocalPeer && _connected.Contains(peer))
                {
                    _joinTarget = peer;
                    break;
                }
            }
            if (!_joinTarget.IsValid)
            {
                Phase = LockstepPhase.Failed;
                Raise(NoticeKind.SyncRejected, -1, PeerId.None, PlayerId.None, "no_peer_connected");
                return;
            }
            _transport.Send(_joinTarget, _codec.Encode(new SyncRequestMessage()));
        }

        /// <summary>
        /// Queues a local command. Its issuer must be one of this peer's players and its tick at least <see cref="NextInputTick"/>
        /// (hosts stamp commands with that tick). Allowed while running or paused, not while joining.
        /// </summary>
        /// <param name="command">The command.</param>
        public void SubmitLocal(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if ((Phase != LockstepPhase.Running && Phase != LockstepPhase.Paused) || !IsLive)
            {
                throw new InvalidOperationException("Local commands are only accepted while the peer is live (phase " + Phase + ").");
            }
            if (Roster.PeerOf(command.Issuer) != LocalPeer)
            {
                throw new ArgumentException("Command " + command + " is not issued by a player of " + LocalPeer + ".", nameof(command));
            }
            QueueLocal(command);
        }

        /// <summary>Asks for a voluntary pause on behalf of a local player; the referee grants it while the player has budget and nothing is paused.</summary>
        /// <param name="player">One of this peer's players.</param>
        public void RequestPause(PlayerId player)
        {
            RequireLocalPlayer(player);
            if (IsReferee)
            {
                HandlePauseRequest(LocalPeer, new PauseRequestMessage(player));
                return;
            }
            SendToReferee(new PauseRequestMessage(player));
        }

        /// <summary>Asks to end the local player's own voluntary pause.</summary>
        /// <param name="player">One of this peer's players.</param>
        public void RequestResume(PlayerId player)
        {
            RequireLocalPlayer(player);
            if (IsReferee)
            {
                HandleResumeRequest(LocalPeer, new ResumeRequestMessage(player));
                return;
            }
            SendToReferee(new ResumeRequestMessage(player));
        }

        /// <summary>
        /// One host update: drains the transport, advances the pause timer, and steps as many ticks as the elapsed time allows
        /// and the bundles permit (at most <see cref="LockstepSettings.CatchUpMaxTicksPerUpdate"/>). While joining, replays the log instead.
        /// </summary>
        /// <param name="elapsedMs">Host milliseconds since the previous call. Unit: milliseconds. Range: 0 upward.</param>
        public void Update(int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            }
            if (Phase == LockstepPhase.Created)
            {
                throw new InvalidOperationException("Call Start or Join before Update.");
            }
            _notices.Clear();
            DrainTransport();
            if (_pause != null)
            {
                _pause.ElapsedMs = _pause.ElapsedMs > int.MaxValue - elapsedMs ? int.MaxValue : _pause.ElapsedMs + elapsedMs;
                if (IsReferee && _pause.ElapsedMs >= _pause.Start.LimitMs)
                {
                    EndPause(PauseEnd.Expired);
                }
            }
            if (Phase == LockstepPhase.Running)
            {
                // Cap on banked time so a long stall never becomes an unbounded burst. Unit: ms x tps.
                long cap = (long)Settings.CatchUpMaxTicksPerUpdate * MsPerSecond;
                _tickAccumulator += (long)elapsedMs * Simulation.Clock.TicksPerSecond;
                if (_tickAccumulator > cap)
                {
                    _tickAccumulator = cap;
                }
                // Ticks this update may step. Unit: ticks. Range: 0 to the catch-up cap.
                int budget = (int)(_tickAccumulator / MsPerSecond);
                // Ticks stepped so far this update. Unit: ticks.
                int stepped = 0;
                while (stepped < budget && Phase == LockstepPhase.Running && TryStep())
                {
                    stepped++;
                }
                _tickAccumulator -= stepped * MsPerSecond;
            }
            else if (Phase == LockstepPhase.Joining && _syncLog != null)
            {
                Replay();
            }
        }

        // ---------------------------------------------------------------- stepping

        /// <summary>Raises <see cref="BeforeStep"/> once for the tick, sends the bundle for tick + delay, and steps when every required bundle is in.</summary>
        private bool TryStep()
        {
            // The tick about to be stepped. Unit: ticks.
            int tick = Simulation.Clock.Tick;
            if (_thoughtTick != tick)
            {
                _thoughtTick = tick;
                BeforeStep?.Invoke(this);
                if (Phase != LockstepPhase.Running)
                {
                    return false;
                }
            }
            SendBundlesUpTo(tick + Settings.InputDelayTicks);
            if (!CanStep(tick))
            {
                RequestMissingBundles(tick);
                return false;
            }
            StepOnce(tick);
            return true;
        }

        /// <summary>Submits every required bundle for <paramref name="tick"/>, steps, records and sends the hash, compares.</summary>
        private void StepOnce(int tick)
        {
            if (_bundles.TryGetValue(tick, out SortedDictionary<PeerId, CommandBundleMessage>? atTick))
            {
                foreach (KeyValuePair<PeerId, CommandBundleMessage> entry in atTick)
                {
                    if (!IsRequired(entry.Key, tick))
                    {
                        continue;
                    }
                    foreach (Command command in entry.Value.Commands)
                    {
                        SubmitToSimulation(command);
                    }
                }
            }
            Simulation.Step();
            Stepped?.Invoke(Simulation);
            foreach (SimEvent evt in Simulation.Events.Current)
            {
                if (evt is StateHashedEvent hashed)
                {
                    _ownHashes[hashed.Tick] = hashed.Hash;
                    if (IsLive && Phase != LockstepPhase.Joining)
                    {
                        _transport.Broadcast(_codec.Encode(new StateHashMessage(hashed.Tick, hashed.Hash)));
                    }
                    foreach (PeerId peer in Roster.Peers)
                    {
                        if (peer != LocalPeer)
                        {
                            CompareHashes(peer);
                        }
                    }
                }
            }
        }

        /// <summary>True when every roster peer whose ranges include <paramref name="tick"/> has a bundle for it.</summary>
        private bool CanStep(int tick)
        {
            foreach (PeerId peer in Roster.Peers)
            {
                if (IsRequired(peer, tick) && !HasBundle(tick, peer))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Asks the referee for each required bundle of <paramref name="tick"/> this peer lacks, but only when the referee's own
        /// bundles show it has already stepped that tick, so it held every bundle the tick needs: the referee sends its bundle for
        /// t + delay before checking it can step t, so only its bundle for tick + delay + 1 proves tick was stepped. Each bundle
        /// is asked for once per value of the referee's highest bundle tick, so an unanswered ask is repeated as the referee advances.
        /// </summary>
        private void RequestMissingBundles(int tick)
        {
            // The referee to ask; none when this peer is the referee or none is live.
            PeerId referee = Referee;
            if (!referee.IsValid || referee == LocalPeer)
            {
                return;
            }
            // Highest bundle tick held from the referee. Unit: ticks.
            int refereeHighest = _highestBundleTick[referee];
            if (refereeHighest < tick + Settings.InputDelayTicks + 1)
            {
                return;
            }
            foreach (PeerId peer in Roster.Peers)
            {
                if (!IsRequired(peer, tick) || HasBundle(tick, peer))
                {
                    continue;
                }
                // Key of the (peer, tick) pair, so each bundle is asked for once per referee advance.
                ulong key = ((ulong)peer.Value << 32) | (uint)tick;
                if (_requestedBundles.TryGetValue(key, out int askedAt) && askedAt >= refereeHighest)
                {
                    continue;
                }
                _requestedBundles[key] = refereeHighest;
                _transport.Send(referee, _codec.Encode(new BundleRequestMessage(peer, tick)));
            }
        }

        /// <summary>Replays the sync log: steps up to the catch-up cap, then reports the check hash and waits to be rejoined.</summary>
        private void Replay()
        {
            // The log being replayed.
            SyncLog log = _syncLog!;
            // Ticks stepped this update. Unit: ticks. Range: 0 to the cap.
            int stepped = 0;
            while (stepped < Settings.CatchUpMaxTicksPerUpdate && Simulation.Clock.Tick < log.RefereeTick)
            {
                // The tick about to be replayed.
                int tick = Simulation.Clock.Tick;
                if (!CanStep(tick))
                {
                    RequestMissingBundles(tick);
                    return;
                }
                StepOnce(tick);
                stepped++;
            }
            if (Simulation.Clock.Tick < log.RefereeTick)
            {
                return;
            }
            // The hash this peer reached at the referee's check tick; 0 when there is no check tick or (a bug) none was recorded.
            ulong hash = log.CheckTick >= 0 && _ownHashes.TryGetValue(log.CheckTick, out ulong own) ? own : 0;
            _syncLog = null;
            _transport.Send(_joinTarget, _codec.Encode(new SyncDoneMessage(log.CheckTick, hash)));
            Raise(NoticeKind.SyncDone, log.CheckTick, _joinTarget, PlayerId.None, "hash " + hash.ToString("x16"));
        }

        // ---------------------------------------------------------------- bundles

        /// <summary>Sends every bundle from the last sent tick plus one up to <paramref name="upTo"/>, empty where no local command waits. Silent while this peer is dropped.</summary>
        private void SendBundlesUpTo(int upTo)
        {
            if (_dropped.Contains(LocalPeer))
            {
                return;
            }
            // Tick of the bundle being sent. Unit: ticks.
            for (int tick = _lastSentTick + 1; tick <= upTo; tick++)
            {
                // Local commands for this tick, or none.
                IReadOnlyList<Command> commands;
                if (_pendingLocal.TryGetValue(tick, out List<Command>? pending))
                {
                    _pendingLocal.Remove(tick);
                    commands = pending;
                }
                else
                {
                    commands = Array.Empty<Command>();
                }
                // The bundle, stored locally and broadcast.
                CommandBundleMessage bundle = new CommandBundleMessage(tick, LocalPeer, commands);
                StoreBundle(bundle);
                _transport.Broadcast(_codec.Encode(bundle));
                _lastSentTick = tick;
            }
        }

        /// <summary>Keeps a bundle (first one per (tick, peer) wins) after checking its commands belong to its peer. Bundles beyond a dropped peer's last tick are ignored.</summary>
        private void StoreBundle(CommandBundleMessage bundle)
        {
            if (!Roster.Contains(bundle.Peer))
            {
                throw new NetProtocolException("Bundle from " + bundle.Peer + ", which is not in the roster.");
            }
            foreach (Command command in bundle.Commands)
            {
                if (!IsIssuedBy(command, bundle.Peer))
                {
                    throw new NetProtocolException("Bundle from " + bundle.Peer + " carries " + command + ", which that peer may not issue.");
                }
            }
            if (_dropped.Contains(bundle.Peer) && bundle.Peer != LocalPeer && !IsRequired(bundle.Peer, bundle.Tick))
            {
                return;
            }
            if (!_bundles.TryGetValue(bundle.Tick, out SortedDictionary<PeerId, CommandBundleMessage>? atTick))
            {
                atTick = new SortedDictionary<PeerId, CommandBundleMessage>();
                _bundles.Add(bundle.Tick, atTick);
            }
            if (atTick.ContainsKey(bundle.Peer))
            {
                return;
            }
            atTick.Add(bundle.Peer, bundle);
            if (bundle.Tick > _highestBundleTick[bundle.Peer])
            {
                _highestBundleTick[bundle.Peer] = bundle.Tick;
            }
        }

        /// <summary>True when a command may appear in <paramref name="peer"/>'s bundle: issued by one of its players, or a protocol command it injected.</summary>
        private bool IsIssuedBy(Command command, PeerId peer)
        {
            if (command.Issuer.IsValid)
            {
                return Roster.PeerOf(command.Issuer) == peer;
            }
            return (command.Sequence & ProtocolSequenceFlag) != 0 && ((command.Sequence >> ProtocolSequencePeerShift) & 0xFF) == peer.Value;
        }

        /// <summary>True when a bundle for (<paramref name="tick"/>, <paramref name="peer"/>) is held.</summary>
        private bool HasBundle(int tick, PeerId peer)
        {
            return _bundles.TryGetValue(tick, out SortedDictionary<PeerId, CommandBundleMessage>? atTick) && atTick.ContainsKey(peer);
        }

        /// <summary>True when <paramref name="peer"/>'s bundle for <paramref name="tick"/> counts.</summary>
        private bool IsRequired(PeerId peer, int tick)
        {
            foreach (TickRange range in _ranges[peer])
            {
                if (range.Contains(tick))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Highest tick such that every bundle of <paramref name="peer"/> from its open range's start up to it is held; the start minus one when none.</summary>
        private int LastContiguousTick(PeerId peer)
        {
            // The open range's first tick.
            int from = _ranges[peer][_ranges[peer].Count - 1].From;
            // The tick being probed. Unit: ticks.
            int tick = from;
            while (HasBundle(tick, peer))
            {
                tick++;
            }
            return tick - 1;
        }

        /// <summary>Removes <paramref name="peer"/>'s bundles beyond <paramref name="lastTick"/>.</summary>
        private void DiscardBundlesAfter(PeerId peer, int lastTick)
        {
            // Ticks whose entry for the peer must go.
            List<int> ticks = new List<int>();
            foreach (KeyValuePair<int, SortedDictionary<PeerId, CommandBundleMessage>> pair in _bundles)
            {
                if (pair.Key > lastTick && pair.Value.ContainsKey(peer))
                {
                    ticks.Add(pair.Key);
                }
            }
            foreach (int tick in ticks)
            {
                _bundles[tick].Remove(peer);
            }
            if (_highestBundleTick[peer] > lastTick)
            {
                _highestBundleTick[peer] = lastTick;
            }
        }

        /// <summary>Adds a local command to the pending bundle for its tick; the tick must not be sent yet.</summary>
        private void QueueLocal(Command command)
        {
            if (command.Tick < NextInputTick)
            {
                throw new InvalidOperationException("Command " + command + " is scheduled before the next input tick " + NextInputTick + ".");
            }
            if (!_pendingLocal.TryGetValue(command.Tick, out List<Command>? list))
            {
                list = new List<Command>();
                _pendingLocal.Add(command.Tick, list);
            }
            list.Add(command);
        }

        /// <summary>Queues a protocol command (issuer None, protocol sequence) built by <paramref name="make"/> for the next input tick.</summary>
        /// <param name="make">Builds the command from (tick, sequence).</param>
        /// <param name="what">Short text for the notice.</param>
        private void Inject(Func<int, uint, Command> make, string what)
        {
            if (_protocolCounter > ProtocolSequenceCounterMask)
            {
                throw new InvalidOperationException("Protocol sequence counter exhausted.");
            }
            // Sequence with the protocol flag, this peer's id and the counter.
            uint sequence = ProtocolSequenceFlag | ((uint)LocalPeer.Value << ProtocolSequencePeerShift) | _protocolCounter++;
            // The command, scheduled for the next input tick.
            Command command = make(NextInputTick, sequence);
            QueueLocal(command);
            Raise(NoticeKind.CommandInjected, command.Tick, LocalPeer, command is SurrenderCommand s ? s.Player : command is SetConnectionStateCommand c ? c.Player : PlayerId.None, what);
        }

        // ---------------------------------------------------------------- hashes

        /// <summary>Compares hashes with <paramref name="peer"/> in ascending tick order; the first mismatch desyncs this peer.</summary>
        private void CompareHashes(PeerId peer)
        {
            if (Phase == LockstepPhase.Desynced || !_nextCompareTick.TryGetValue(peer, out int next))
            {
                return;
            }
            // The peer's hashes.
            SortedDictionary<int, ulong> remote = _remoteHashes[peer];
            while (_ownHashes.TryGetValue(next, out ulong mine) && remote.TryGetValue(next, out ulong theirs))
            {
                if (mine != theirs)
                {
                    Phase = LockstepPhase.Desynced;
                    Raise(NoticeKind.Desynced, next, peer, PlayerId.None, mine, theirs, "hash_mismatch");
                    return;
                }
                next += Simulation.Settings.StateHashIntervalTicks;
                _nextCompareTick[peer] = next;
            }
        }

        /// <summary>The first hash tick at or after <paramref name="tick"/>. Unit: ticks.</summary>
        private int FirstHashTickAtOrAfter(int tick)
        {
            // The hash interval. Unit: ticks.
            int interval = Simulation.Settings.StateHashIntervalTicks;
            return ((tick + interval - 1) / interval) * interval;
        }

        // ---------------------------------------------------------------- transport and messages

        /// <summary>Takes the transport's connected peers (plus this peer) as the session's connected set.</summary>
        private void SeedConnected()
        {
            _connected.Clear();
            _connected.Add(LocalPeer);
            foreach (PeerId peer in _transport.ConnectedPeers)
            {
                if (Roster.Contains(peer))
                {
                    _connected.Add(peer);
                }
            }
        }

        /// <summary>Pulls every waiting transport event and handles it.</summary>
        private void DrainTransport()
        {
            while (_transport.TryReceive(out TransportEvent evt))
            {
                if (!Roster.Contains(evt.Peer))
                {
                    throw new NetProtocolException("Transport event from " + evt.Peer + ", which is not in the roster.");
                }
                switch (evt.Kind)
                {
                    case TransportEventKind.PeerConnected:
                        _connected.Add(evt.Peer);
                        ReevaluateReferee();
                        break;
                    case TransportEventKind.PeerDisconnected:
                        _connected.Remove(evt.Peer);
                        ReevaluateReferee();
                        break;
                    case TransportEventKind.Message:
                        HandleMessage(evt.Peer, _codec.Decode(evt.Payload ?? throw new NetProtocolException("Message event without payload from " + evt.Peer + ".")));
                        break;
                }
            }
        }

        /// <summary>If this peer is the referee, drops every roster peer that is unreachable and not yet dropped.</summary>
        private void ReevaluateReferee()
        {
            if (!IsReferee)
            {
                return;
            }
            foreach (PeerId peer in Roster.Peers)
            {
                if (peer != LocalPeer && !_connected.Contains(peer) && !_dropped.Contains(peer))
                {
                    DropPeer(peer);
                }
            }
        }

        /// <summary>
        /// Dispatches one decoded message. A bundle forwarded by a peer other than its sender is stored whoever forwards it
        /// (its commands are checked against the bundle's own peer, and the first copy stored wins): the forwarder may have
        /// answered a request while it was the referee and lost that role before the answer arrived.
        /// </summary>
        private void HandleMessage(PeerId from, NetMessage message)
        {
            switch (message)
            {
                case CommandBundleMessage bundle:
                    StoreBundle(bundle);
                    break;
                case StateHashMessage hash:
                    // Hashes of a dropped peer are ignored: beyond its cutoff it may have applied its own bundles that the referee never held.
                    if (from != LocalPeer && !_dropped.Contains(from) && _remoteHashes.TryGetValue(from, out SortedDictionary<int, ulong>? hashes))
                    {
                        hashes[hash.Tick] = hash.Hash;
                        if (_sim != null)
                        {
                            CompareHashes(from);
                        }
                    }
                    break;
                case PauseRequestMessage pauseRequest:
                    if (IsReferee)
                    {
                        HandlePauseRequest(from, pauseRequest);
                    }
                    break;
                case ResumeRequestMessage resumeRequest:
                    if (IsReferee)
                    {
                        HandleResumeRequest(from, resumeRequest);
                    }
                    break;
                case PauseStartedMessage started:
                    ApplyPauseStarted(started);
                    break;
                case PauseEndedMessage ended:
                    ApplyPauseEnded(ended);
                    break;
                case PeerDroppedMessage dropped:
                    ApplyPeerDropped(dropped);
                    break;
                case PeerRejoinedMessage rejoined:
                    ApplyPeerRejoined(rejoined);
                    break;
                case SyncRequestMessage _:
                    HandleSyncRequest(from);
                    break;
                case SyncLogPartMessage part:
                    CollectSyncLogPart(from, part);
                    break;
                case SyncDoneMessage done:
                    HandleSyncDone(from, done);
                    break;
                case SyncRejectedMessage rejected:
                    HandleSyncRejected(from, rejected);
                    break;
                case BundleRequestMessage request:
                    if (_bundles.TryGetValue(request.Tick, out SortedDictionary<PeerId, CommandBundleMessage>? atTick) && atTick.TryGetValue(request.Peer, out CommandBundleMessage? found))
                    {
                        _transport.Send(from, _codec.Encode(found));
                    }
                    break;
                default:
                    throw new NetProtocolException("Unhandled message kind " + message.Kind + " from " + from + ".");
            }
        }

        /// <summary>Sends a message to the referee; silently does nothing when there is none.</summary>
        private void SendToReferee(NetMessage message)
        {
            // The referee as this peer sees it.
            PeerId referee = Referee;
            if (referee.IsValid && referee != LocalPeer)
            {
                _transport.Send(referee, _codec.Encode(message));
            }
        }

        /// <summary>Sends a referee decision to every connected peer after applying it locally.</summary>
        private void Announce(NetMessage message)
        {
            _transport.Broadcast(_codec.Encode(message));
        }

        // ---------------------------------------------------------------- drops and rejoins

        /// <summary>Referee: removes <paramref name="peer"/> from the live set and starts its disconnect pause, or marks its players disconnected when no pause is left.</summary>
        private void DropPeer(PeerId peer)
        {
            // The dropped peer's cutoff tick.
            int lastTick = LastContiguousTick(peer);
            // The decision, applied here and announced.
            PeerDroppedMessage dropped = new PeerDroppedMessage(peer, lastTick);
            ApplyPeerDropped(dropped);
            Announce(dropped);
            _syncing.Remove(peer);
            // The dropped peer's players.
            IReadOnlyList<PlayerId> players = Roster.PlayersOf(peer);
            // True when every player of the peer still has a disconnect pause and no pause is running.
            bool canPause = _pause == null;
            foreach (PlayerId player in players)
            {
                canPause &= Ledger.CanStartDisconnectPause(player);
            }
            if (canPause)
            {
                // The pause is counted against the players in ApplyPauseStarted, on this peer and on every other peer alike.
                StartPause(players[0], peer, PauseReason.Disconnect, Settings.DisconnectPauseMaxMs);
                return;
            }
            foreach (PlayerId player in players)
            {
                // The player marked disconnected in the simulation.
                PlayerId target = player;
                _markedDisconnected.Add(target);
                Inject((tick, sequence) => new SetConnectionStateCommand(tick, PlayerId.None, sequence, target, ConnectionState.Disconnected), "disconnected_no_pause");
            }
        }

        /// <summary>
        /// Applies a drop: closes the peer's open range at the cutoff, discards its later bundles and its hashes beyond the cutoff
        /// (it may have stepped those ticks with bundles the referee never held), checks this peer did not already apply any of them.
        /// </summary>
        private void ApplyPeerDropped(PeerDroppedMessage dropped)
        {
            if (!Roster.Contains(dropped.Peer) || _dropped.Contains(dropped.Peer))
            {
                return;
            }
            _dropped.Add(dropped.Peer);
            if (_remoteHashes.TryGetValue(dropped.Peer, out SortedDictionary<int, ulong>? hashes))
            {
                // Hash ticks beyond the cutoff, collected first because the dictionary cannot change while enumerated.
                List<int> stale = new List<int>();
                foreach (int hashTick in hashes.Keys)
                {
                    if (hashTick > dropped.LastTick)
                    {
                        stale.Add(hashTick);
                    }
                }
                foreach (int hashTick in stale)
                {
                    hashes.Remove(hashTick);
                }
            }
            // The peer's ranges; the last one is open.
            List<TickRange> ranges = _ranges[dropped.Peer];
            // The open range being closed.
            TickRange open = ranges[ranges.Count - 1];
            ranges[ranges.Count - 1] = new TickRange(open.From, dropped.LastTick);
            // True when this peer already stepped a tick beyond the cutoff with the dropped peer's bundle in it.
            bool inconsistent = false;
            if (_sim != null && IsLive)
            {
                // The tick being checked for an applied bundle beyond the cutoff. Unit: ticks.
                for (int tick = dropped.LastTick + 1; tick < Simulation.Clock.Tick; tick++)
                {
                    inconsistent |= HasBundle(tick, dropped.Peer) && open.Contains(tick);
                }
            }
            DiscardBundlesAfter(dropped.Peer, dropped.LastTick);
            if (dropped.Peer == LocalPeer && Phase != LockstepPhase.Joining)
            {
                Phase = LockstepPhase.Failed;
            }
            Raise(NoticeKind.PeerDropped, dropped.LastTick, dropped.Peer, PlayerId.None, "dropped");
            if (inconsistent)
            {
                Phase = LockstepPhase.Failed;
                Raise(NoticeKind.Inconsistent, dropped.LastTick, dropped.Peer, PlayerId.None, "applied_bundles_beyond_cutoff");
            }
        }

        /// <summary>
        /// Applies a rejoin: opens a new required range from the given tick and resets hash comparison from there. A joiner still
        /// waiting for its log only notes the change (the log it gets was written after this decision and carries it). A live peer
        /// that already stepped past the tick raises Inconsistent: it applied an empty bundle where the others apply the joiner's.
        /// </summary>
        private void ApplyPeerRejoined(PeerRejoinedMessage rejoined)
        {
            if (!Roster.Contains(rejoined.Peer) || !_dropped.Contains(rejoined.Peer))
            {
                return;
            }
            _dropped.Remove(rejoined.Peer);
            _ranges[rejoined.Peer].Add(new TickRange(rejoined.FromTick, int.MaxValue));
            if (_sim == null)
            {
                Raise(NoticeKind.PeerRejoined, rejoined.FromTick, rejoined.Peer, PlayerId.None, "rejoined");
                return;
            }
            if (rejoined.Peer != LocalPeer && IsLive && Simulation.Clock.Tick > rejoined.FromTick)
            {
                Phase = LockstepPhase.Failed;
                Raise(NoticeKind.Inconsistent, rejoined.FromTick, rejoined.Peer, PlayerId.None, "stepped_past_rejoin_tick");
                return;
            }
            if (rejoined.Peer == LocalPeer)
            {
                _lastSentTick = rejoined.FromTick - 1;
                foreach (PeerId peer in Roster.Peers)
                {
                    if (peer != LocalPeer)
                    {
                        _nextCompareTick[peer] = FirstHashTickAtOrAfter(rejoined.FromTick);
                    }
                }
                _joinTarget = PeerId.None;
                _tickAccumulator = 0;
                Phase = _pause != null ? LockstepPhase.Paused : LockstepPhase.Running;
            }
            else
            {
                _nextCompareTick[rejoined.Peer] = FirstHashTickAtOrAfter(rejoined.FromTick);
            }
            Raise(NoticeKind.PeerRejoined, rejoined.FromTick, rejoined.Peer, PlayerId.None, "rejoined");
            foreach (PeerId peer in Roster.Peers)
            {
                if (peer != LocalPeer)
                {
                    CompareHashes(peer);
                }
            }
        }

        // ---------------------------------------------------------------- reconnect

        /// <summary>Joiner: a refusal from a peer that is not the referee moves the request to the next connected peer; any other refusal ends the join.</summary>
        private void HandleSyncRejected(PeerId from, SyncRejectedMessage rejected)
        {
            if (Phase != LockstepPhase.Joining || from != _joinTarget)
            {
                return;
            }
            if (rejected.Reason == "not_referee")
            {
                foreach (PeerId peer in Roster.Peers)
                {
                    if (peer.CompareTo(_joinTarget) > 0 && peer != LocalPeer && _connected.Contains(peer))
                    {
                        _joinTarget = peer;
                        _transport.Send(_joinTarget, _codec.Encode(new SyncRequestMessage()));
                        return;
                    }
                }
            }
            Phase = LockstepPhase.Failed;
            Raise(NoticeKind.SyncRejected, -1, from, PlayerId.None, rejected.Reason);
        }

        /// <summary>
        /// Referee: answers a dropped peer's sync request with the log (in parts), opening a resync pause first when nothing is
        /// paused. A request from a peer still held live is a restart the transport has not reported (the old connection died
        /// unseen): the peer is dropped first, with the pause rules of any drop, and then answered.
        /// </summary>
        private void HandleSyncRequest(PeerId from)
        {
            if (!IsReferee)
            {
                _transport.Send(from, _codec.Encode(new SyncRejectedMessage("not_referee")));
                return;
            }
            if (!_dropped.Contains(from))
            {
                DropPeer(from);
            }
            if (_pause == null)
            {
                // The joiner's players and the smallest budget among them.
                IReadOnlyList<PlayerId> players = Roster.PlayersOf(from);
                int budget = int.MaxValue;
                foreach (PlayerId player in players)
                {
                    budget = Math.Min(budget, Ledger.RemainingBudgetMs(player));
                }
                if (budget <= 0)
                {
                    _transport.Send(from, _codec.Encode(new SyncRejectedMessage("no_pause_budget")));
                    Raise(NoticeKind.SyncRejected, -1, from, players[0], "no_pause_budget");
                    return;
                }
                StartPause(players[0], from, PauseReason.Resync, budget);
            }
            // The referee's last stepped hash tick, or -1.
            int checkTick = LastOwnHashTick();
            // Every bundle held, ascending (tick, peer).
            List<CommandBundleMessage> bundles = new List<CommandBundleMessage>();
            foreach (KeyValuePair<int, SortedDictionary<PeerId, CommandBundleMessage>> tick in _bundles)
            {
                foreach (CommandBundleMessage bundle in tick.Value.Values)
                {
                    bundles.Add(bundle);
                }
            }
            // The required ranges of every peer.
            List<PeerRangesEntry> ranges = new List<PeerRangesEntry>();
            foreach (KeyValuePair<PeerId, List<TickRange>> pair in _ranges)
            {
                ranges.Add(new PeerRangesEntry(pair.Key, new List<TickRange>(pair.Value)));
            }
            // The log sent to the joiner, in parts that each fit the transport's payload cap.
            SyncLog log = new SyncLog(Simulation.Clock.Tick, checkTick, checkTick >= 0 ? _ownHashes[checkTick] : 0, bundles,
                Ledger.Snapshot(), ranges, new List<PeerId>(_dropped), _pause?.Start, _nextPauseId);
            _syncing[from] = checkTick;
            foreach (SyncLogPartMessage part in _codec.SplitSyncLog(log))
            {
                _transport.Send(from, _codec.Encode(part));
            }
        }

        /// <summary>Joiner: collects one part of the log from the join target; once the run is complete, joins it and installs the log.</summary>
        private void CollectSyncLogPart(PeerId from, SyncLogPartMessage part)
        {
            if (Phase != LockstepPhase.Joining || from != _joinTarget || _syncLog != null || _sim != null)
            {
                return;
            }
            if (part.Index != _syncLogParts.Count || (_syncLogParts.Count > 0 && part.Count != _syncLogParts[0].Count))
            {
                throw new NetProtocolException("Sync log part " + part.Index + " of " + part.Count + " from " + from + " arrived where part " + _syncLogParts.Count + " was expected.");
            }
            _syncLogParts.Add(part);
            if (_syncLogParts.Count < part.Count)
            {
                return;
            }
            // The complete log.
            SyncLog log = _codec.JoinSyncLog(_syncLogParts);
            _syncLogParts.Clear();
            ApplySyncLog(from, log);
        }

        /// <summary>The highest hash tick this peer has stepped, or -1. Unit: ticks.</summary>
        private int LastOwnHashTick()
        {
            // The result, walked up through the recorded hashes (ascending, so the last key wins).
            int last = -1;
            foreach (int tick in _ownHashes.Keys)
            {
                last = tick;
            }
            return last;
        }

        /// <summary>Joiner: installs the log, builds a fresh simulation and starts replaying.</summary>
        private void ApplySyncLog(PeerId from, SyncLog log)
        {
            if (Phase != LockstepPhase.Joining || from != _joinTarget || _syncLog != null)
            {
                return;
            }
            _sim = CreateSimulation();
            _ownHashes.Clear();
            Ledger.Restore(log.Ledger);
            foreach (PeerRangesEntry entry in log.Ranges)
            {
                if (!_ranges.ContainsKey(entry.Peer) || entry.Ranges.Count == 0)
                {
                    throw new NetProtocolException("Sync log has bad ranges for " + entry.Peer + ".");
                }
                _ranges[entry.Peer] = new List<TickRange>(entry.Ranges);
            }
            _dropped.Clear();
            foreach (PeerId peer in log.Dropped)
            {
                _dropped.Add(peer);
            }
            _dropped.Add(LocalPeer);
            // Bundles that arrived between Join and the log are kept: the log only adds what this peer lacks (first stored wins, and the content is identical).
            foreach (CommandBundleMessage bundle in log.Bundles)
            {
                StoreBundle(bundle);
            }
            _pause = log.ActivePause != null ? new PauseState(log.ActivePause) : null;
            _nextPauseId = log.NextPauseId;
            _syncLog = log;
            Raise(NoticeKind.SyncStarted, log.RefereeTick, from, PlayerId.None, log.Bundles.Count + " bundles");
        }

        /// <summary>
        /// Referee: verifies a joiner's hash, then rejoins it from this peer's next input tick (one past the last bundle it sent,
        /// which no peer can have stepped since stepping needs the referee's bundle) and ends the pause held for it.
        /// </summary>
        private void HandleSyncDone(PeerId from, SyncDoneMessage done)
        {
            if (!IsReferee || !_syncing.TryGetValue(from, out int checkTick))
            {
                return;
            }
            _syncing.Remove(from);
            // The hash the referee holds at the check tick (0 when there is none).
            ulong expected = checkTick >= 0 && _ownHashes.TryGetValue(checkTick, out ulong own) ? own : 0;
            if (done.CheckTick != checkTick || done.Hash != expected)
            {
                _transport.Send(from, _codec.Encode(new SyncRejectedMessage("hash_mismatch")));
                Raise(NoticeKind.SyncRejected, checkTick, from, PlayerId.None, expected, done.Hash, "hash_mismatch");
                if (_pause != null && _pause.Start.Reason == PauseReason.Resync && _pause.Start.Peer == from)
                {
                    EndPause(PauseEnd.Resumed);
                }
                return;
            }
            // The decision, applied here and announced before any further bundle of this peer goes out, so the ordered transport delivers it first.
            PeerRejoinedMessage rejoined = new PeerRejoinedMessage(from, NextInputTick);
            ApplyPeerRejoined(rejoined);
            Announce(rejoined);
            foreach (PlayerId player in Roster.PlayersOf(from))
            {
                if (_markedDisconnected.Remove(player))
                {
                    // The player marked connected again in the simulation.
                    PlayerId target = player;
                    Inject((tick, sequence) => new SetConnectionStateCommand(tick, PlayerId.None, sequence, target, ConnectionState.Connected), "reconnected");
                }
            }
            if (_pause != null && _pause.Start.Peer == from)
            {
                EndPause(PauseEnd.Resumed);
            }
        }

        // ---------------------------------------------------------------- pauses

        /// <summary>Referee: grants a voluntary pause when nothing is paused and the player has budget.</summary>
        private void HandlePauseRequest(PeerId from, PauseRequestMessage request)
        {
            if (Roster.PeerOf(request.Player) != from)
            {
                Raise(NoticeKind.PauseRefused, -1, from, request.Player, "not_own_player");
                return;
            }
            if (_pause != null)
            {
                Raise(NoticeKind.PauseRefused, -1, from, request.Player, "already_paused");
                return;
            }
            // Budget the player has left. Unit: milliseconds.
            int remaining = Ledger.RemainingBudgetMs(request.Player);
            if (remaining <= 0)
            {
                Raise(NoticeKind.PauseRefused, -1, from, request.Player, "no_budget");
                return;
            }
            StartPause(request.Player, PeerId.None, PauseReason.Voluntary, remaining);
        }

        /// <summary>Referee: ends the player's own voluntary pause.</summary>
        private void HandleResumeRequest(PeerId from, ResumeRequestMessage request)
        {
            if (_pause != null && _pause.Start.Reason == PauseReason.Voluntary && _pause.Start.Player == request.Player && Roster.PeerOf(request.Player) == from)
            {
                EndPause(PauseEnd.Resumed);
            }
        }

        /// <summary>Referee: announces a new pause.</summary>
        private void StartPause(PlayerId player, PeerId peer, PauseReason reason, int limitMs)
        {
            // The announcement, applied here and broadcast.
            PauseStartedMessage started = new PauseStartedMessage(_nextPauseId++, player, peer, reason, limitMs);
            ApplyPauseStarted(started);
            Announce(started);
        }

        /// <summary>Referee: ends the active pause with the length this peer counted (capped at the limit) and announces it.</summary>
        private void EndPause(PauseEnd end)
        {
            if (_pause == null)
            {
                return;
            }
            // The decision, applied here and broadcast.
            PauseEndedMessage ended = new PauseEndedMessage(_pause.Start.PauseId, Math.Min(_pause.ElapsedMs, _pause.Start.LimitMs), end);
            ApplyPauseEnded(ended);
            Announce(ended);
        }

        /// <summary>
        /// Applies a pause start: stops stepping; a start arriving during another pause waits its turn. A disconnect pause is
        /// counted against every player of the dropped peer here, on the referee and on every other peer alike, so the ledgers
        /// agree after a referee handover and a joiner inherits the right count through the log.
        /// </summary>
        private void ApplyPauseStarted(PauseStartedMessage started)
        {
            if (_pause != null)
            {
                if (_pause.Start.PauseId != started.PauseId)
                {
                    _pendingPauseStarts.Add(started);
                }
                return;
            }
            _pause = new PauseState(started);
            if (started.Reason == PauseReason.Disconnect && started.Peer.IsValid && Roster.Contains(started.Peer))
            {
                foreach (PlayerId player in Roster.PlayersOf(started.Peer))
                {
                    Ledger.RecordDisconnectPause(player);
                }
            }
            if (started.PauseId >= _nextPauseId)
            {
                _nextPauseId = started.PauseId + 1;
            }
            if (Phase == LockstepPhase.Running)
            {
                Phase = LockstepPhase.Paused;
            }
            Raise(NoticeKind.PauseStarted, CurrentTickOrNone(), started.Peer, started.Player, started.Reason.ToString().ToLowerInvariant() + " " + started.LimitMs + "ms");
            if (_earlyPauseEnds.TryGetValue(started.PauseId, out PauseEndedMessage? early))
            {
                _earlyPauseEnds.Remove(started.PauseId);
                ApplyPauseEnded(early);
            }
        }

        /// <summary>Applies a pause end: charges the ledger with the referee's length, resumes stepping, and on an expired disconnect pause the referee surrenders the no-show's players.</summary>
        private void ApplyPauseEnded(PauseEndedMessage ended)
        {
            if (_pause == null || _pause.Start.PauseId != ended.PauseId)
            {
                _earlyPauseEnds[ended.PauseId] = ended;
                return;
            }
            // The pause that just ended.
            PauseStartedMessage start = _pause.Start;
            _pause = null;
            if (start.Reason != PauseReason.Disconnect)
            {
                Ledger.ChargeBudget(start.Player, ended.ElapsedMs);
            }
            if (Phase == LockstepPhase.Paused)
            {
                Phase = LockstepPhase.Running;
            }
            Raise(NoticeKind.PauseEnded, CurrentTickOrNone(), start.Peer, start.Player, ended.End.ToString().ToLowerInvariant() + " " + ended.ElapsedMs + "ms");
            if (ended.End == PauseEnd.Expired && start.Reason == PauseReason.Disconnect && IsReferee && start.Peer.IsValid)
            {
                foreach (PlayerId player in Roster.PlayersOf(start.Peer))
                {
                    // The player who did not return, surrendered on their behalf.
                    PlayerId target = player;
                    Inject((tick, sequence) => new SurrenderCommand(tick, PlayerId.None, sequence, target), "no_show_surrender");
                }
            }
            if (_pendingPauseStarts.Count > 0)
            {
                // The next queued start.
                PauseStartedMessage next = _pendingPauseStarts[0];
                _pendingPauseStarts.RemoveAt(0);
                ApplyPauseStarted(next);
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Builds a match through the factory, checks the roster covers exactly its players, raises <see cref="SimulationCreated"/>
        /// on the still-empty simulation and then submits the setup commands through <see cref="SubmitToSimulation"/>.
        /// </summary>
        private Simulation CreateSimulation()
        {
            // The new match: an empty started simulation and its setup commands.
            LockstepMatch match = _factory() ?? throw new InvalidOperationException("The match factory returned null.");
            // The new simulation.
            Simulation sim = match.Simulation;
            foreach (Player player in sim.Players.All)
            {
                if (!Roster.PeerOf(player.Id).IsValid)
                {
                    throw new InvalidOperationException("Player " + player.Id + " of the match has no peer in the roster.");
                }
            }
            foreach (PlayerId player in Roster.AllPlayers)
            {
                if (!sim.Players.Contains(player))
                {
                    throw new InvalidOperationException("Roster player " + player + " is not in the match.");
                }
            }
            SimulationCreated?.Invoke(sim);
            foreach (Command command in match.SetupCommands)
            {
                sim.Submit(command);
                CommandSubmitted?.Invoke(command);
            }
            return sim;
        }

        /// <summary>Submits a command to the simulation and reports it through <see cref="CommandSubmitted"/>.</summary>
        /// <param name="command">The command, at or after the current tick.</param>
        private void SubmitToSimulation(Command command)
        {
            Simulation.Submit(command);
            CommandSubmitted?.Invoke(command);
        }

        /// <summary>The current tick, or -1 before a simulation exists (a joiner waiting for the log). Unit: ticks.</summary>
        private int CurrentTickOrNone()
        {
            return _sim != null ? _sim.Clock.Tick : -1;
        }

        /// <summary>Throws unless <paramref name="player"/> is one of this peer's players.</summary>
        private void RequireLocalPlayer(PlayerId player)
        {
            if (Roster.PeerOf(player) != LocalPeer)
            {
                throw new ArgumentException("Player " + player + " is not controlled by " + LocalPeer + ".", nameof(player));
            }
        }

        /// <summary>Records a notice and raises the event.</summary>
        private void Raise(NoticeKind kind, int tick, PeerId peer, PlayerId player, string text)
        {
            Raise(kind, tick, peer, player, 0, 0, text);
        }

        /// <summary>Records a notice with hashes and raises the event.</summary>
        private void Raise(NoticeKind kind, int tick, PeerId peer, PlayerId player, ulong localHash, ulong remoteHash, string text)
        {
            // The notice being recorded.
            LockstepNotice notice = new LockstepNotice(kind, tick, peer, player, localHash, remoteHash, text);
            _notices.Add(notice);
            NoticeRaised?.Invoke(notice);
        }
    }
}
