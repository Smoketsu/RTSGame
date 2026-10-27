<!-- rts/docs/decisions/ADR-008-lockstep.md: how online matches stay in step: a transport-agnostic peer model with a fixed input delay, per-tick command bundles, per-interval hash exchange, desync detection, disconnect pauses, a pause budget and reconnect by replaying the command log. -->
# ADR-008: Deterministic lockstep over a transport-agnostic peer model

Status: Accepted, 26 October 2026 (start of Phase 2).

## Context

The development plan's Phase 2 exit criterion is "two players on different machines finish an online 1v1 with no desync". The simulation is already deterministic (ADR-001 to ADR-005): the same seed, data and command stream give the same state hash on every machine. Networking therefore only has to deliver the same commands for the same tick to every machine, and prove with hashes that it did. The design doc fixes the player-facing rules: a disconnect pauses the match for up to 5 minutes, once per player; each player has 120 s of voluntary pause time; a player who does not return has lost. The transport is Steam networking sockets in the shipped game, but the protocol must run headless in tests (`rts/tools`, CI) and never name Steam.

Options considered:

1. **Deterministic lockstep with a fixed input delay** (every peer runs the full simulation; only commands travel). Bandwidth is tiny, any player count works, replays and reconnects fall out of the command log, and anti-cheat (Phase 5) can re-simulate. The cost is that a tick cannot advance until every peer's input for it has arrived, so a slow peer slows everyone; the input delay hides normal latency.
2. **Server-authoritative state replication**: needs a server simulation plus state serialisation of every unit, breaks the "everything is a command" rule, and still needs determinism for replays.
3. **Rollback (GGPO-style)**: hides latency better, but rolling back an RTS with hundreds of units and flow fields means snapshotting the whole simulation every tick; not worth it for a game with a 150 ms input delay budget.

## Decision

### Peer model and transport

- A **session** is a fixed **roster** (`LockstepRoster`): which `PeerId` controls which `PlayerId`s, set by the lobby, identical on every peer. Any peer count; a peer may run several players.
- The protocol runs over **`ITransport`**: reliable, ordered delivery per (sender, receiver) pair, plus `PeerConnected` / `PeerDisconnected` reports, pulled by the protocol (`TryReceive`) on the host's thread. The Unity project adapts Steam networking sockets' reliable channel to it; tests use `LoopbackTransport`, an in-process hub with latency in host ticks and optional (off by default) reorder and drop knobs. The protocol does no retransmission and no timing-based disconnect detection.
- **`LockstepPeer`** wraps one `Simulation`. The host calls `Update(elapsedMs)` from its loop; the protocol never reads a clock, so pause timing uses the host's integer milliseconds. The library is engine-free `netstandard2.1` (`Rts.Net`) under the simulation's determinism rules.
- The **referee** is the lowest-numbered roster peer that is connected and live. Every decision all peers must agree on is made by the referee alone and announced to the others; the others apply what they are told. In the shipped 1v1 the referee is the lobby owner in practice, and when one of two peers drops the other is the referee by construction.

### Ticks, bundles and ordering

- `input_delay_ticks` (`online.json`, placeholder 3 = 150 ms at 20 ticks per second): a command issued while the local simulation is at tick t is scheduled for tick t + delay. `LockstepPeer.NextInputTick` tells hosts (and `Rts.Ai` sinks) the tick to stamp.
- Before stepping tick t a peer sends its **command bundle** for tick t + delay (empty when it issued nothing) to every peer; the bundles for ticks 0 to delay - 1 are sent empty at start. One bundle per peer per tick, always.
- A tick advances only when the peer holds, for every roster peer whose **required ranges** include that tick, that peer's bundle. Every required bundle's commands are submitted to the simulation, which orders them by `(issuer, sequence)` as it always has (ADR-003), so arrival order never affects the outcome. A bundle may only carry commands issued by the sending peer's players, or protocol commands (issuer None) with a sequence in the protocol range: bit 31 set, the peer id in bits 20 to 27, a counter below; these cannot collide with the match script's issuer-None sequences.
- Every peer keeps every bundle it applies: the **command log**, which reconnects (below) and replays (ADR-009) read.
- Hosts may step at most `catch_up_max_ticks_per_update` ticks per `Update` (placeholder 200), which bounds the burst after a stall or during a replay.

### Hash exchange and desync detection

- Every `StateHashedEvent` (every `state_hash_interval_ticks`, ADR-005) is sent to every peer. Hashes are compared per remote peer in **ascending tick order**, never skipping a tick, so the first mismatch found is the **first mismatching tick**. On a mismatch the peer stops stepping and raises `Desynced(tick, localHash, remoteHash, peer)`. Hashes that arrive before the local simulation reaches their tick wait.

### Pauses

- Pause bookkeeping is protocol state (`PauseLedger`), not simulation state: every peer holds a copy and every change comes from a referee message, so the copies agree without being hashed. `Player.DisconnectPausesUsed` and `PauseTicksUsed` in the simulation stay at 0 until a simulation command carries the numbers in (see Consequences).
- **Disconnect pause.** When the transport reports a peer gone, the referee announces `PeerDropped(peer, lastTick)`: lastTick is the highest tick for which the referee holds every bundle from that peer since it last joined; its bundles count up to lastTick and are empty after it, and every peer discards anything later. If every player of the dropped peer still has a disconnect pause (`disconnect_pauses_per_player`, design 1) and nothing is paused, the referee counts one against each of them and announces `PauseStarted(reason Disconnect, limit disconnect_pause_max_seconds)`, design 300 s. No peer steps while paused; the simulation never sees the disconnect. If the peer is back before the limit, the pause ends `Resumed` (nothing is charged). If not, the pause ends `Expired` and the referee injects a `SurrenderCommand` for each of the no-show's players as a protocol command, so the match is decided through the ordinary player state machine.
- **No pause left.** A disconnect of a player whose pause is used up (or while another pause is running) gets no pause: the referee injects `SetConnectionStateCommand(Disconnected)` and play continues with that peer's bundles treated as empty; on reconnect the referee injects `Connected` again.
- **Voluntary pause and budget.** A peer sends `PauseRequest(player)`; the referee grants it when nothing is paused and the player has budget left (`pause_budget_seconds_per_player`, design 120 s), with the remaining budget as the limit. It ends on the player's `ResumeRequest` or when the limit runs out. `PauseEnded` carries the referee's measured length, and every peer charges exactly that number to the player's budget, so ledgers agree to the millisecond.
- A pause started under one pause id ends under the same id; starts and ends arriving out of order (a reordering transport) are matched by id.

### Reconnect

- A reconnecting player starts a **fresh `LockstepPeer`** and calls `Join`: it sends `SyncRequest` to the lowest connected roster peer (a non-referee answers `not_referee` and the joiner tries the next one). The referee requires the joiner to be a dropped peer; if nothing is paused it first opens a **resync pause** charged to the joiner's budget (refused with `no_pause_budget` when none is left), then sends `SyncLog`: every bundle it holds, its current tick, its last hash tick and hash, the ledger, the required ranges, the dropped set and the active pause.
- The joiner builds a fresh simulation through the host's factory (the same seed, data, map and setup commands as at match start), replays ticks 0 to RefereeTick - 1 from the log at up to the catch-up cap per `Update`, and sends `SyncDone(checkTick, hash)`. The referee compares; on a match it announces `PeerRejoined(peer, fromTick = refereeTick + delay)`, the first tick no peer can have stepped yet, from which the joiner's bundles are required again, and ends the pause held for it. On a mismatch it answers `SyncRejected(hash_mismatch)` and the joiner stays out.
- Bundles a peer lacks for a tick the referee has demonstrably stepped (the referee's own bundle for tick + delay has arrived) are asked for once with `BundleRequest`; this covers in-flight bundles of a dropped peer and bundles sent before a joiner connected.

### Wire format

- Every message is `uint16 protocol version, byte kind, fields` written by `MessageCodec` with explicit little-endian primitives (`NetWriter` / `NetReader`); strings are length plus UTF-16 code units, the same bytes `StateHasher` hashes. No `BinaryFormatter`, no reflection. Commands inside bundles go through `CommandCodecRegistry`: one `ICommandCodec` per command kind, registered by kind so a faction plug-in's command (the Water `cast_wave`) ships its own codec, mirroring `IScriptCommandParser`; `BuiltInCodecs` is the only composition root that names a faction. A different version, unknown kind, bad enum byte, truncation or trailing bytes is a `NetProtocolException`.

## Consequences

- Latency above the input delay stalls everyone; the HUD should show "waiting for <player>" when `LockstepPeer` is blocked. Tuning the delay is a data change.
- With two peers the referee rule has no edge cases. With three or more, a peer that applied a dropped peer's bundle beyond the referee's `lastTick` (the bundle reached it but not the referee before the drop) detects this when `PeerDropped` arrives, raises `Inconsistent` and must rejoin; two peers believing themselves referee at once (two simultaneous drops) is not handled. Both are acceptable for the Phase 2 1v1 and are revisited with team modes.
- Requests to the simulation, to be made as separate reviewed changes: (1) a `DefeatPlayerCommand` (issuer None only, cause `no_show`) so an expired disconnect pause produces `Defeated` as the design says rather than `Surrendered`; (2) a `RecordPauseCommand(player, disconnectPausesUsed, pauseTicks)` so the simulation's hashed pause counters follow the ledger and replays show pauses. Until then the ledger is the record and the simulation's counters stay at 0.
- The command log is the replay: ADR-009 records the same bundles plus seed, map and data digests.
- Reconnecting replays the whole match from tick 0 (a 20-minute match is 24,000 ticks, replayed at up to 200 per update); a mid-match snapshot would need simulation serialisation, which this design avoids on purpose.
- Messages from any roster peer are trusted; a rogue peer announcing pauses is a Phase 5 anti-cheat concern.

## Alternatives considered

- A dedicated relay or server as the authority instead of the lowest peer: simpler takeover rules, but the shipped game has no server, and the referee rule degrades to the same thing when the lobby owner is peer 0.
- Pause accounting inside the simulation (hashed): would make pause messages simulation commands and tie pause timing to the tick clock, which stands still during a pause; keeping it in the protocol with the referee's millisecond count was simpler and matches the design's wall-clock limits.
- Partial resync (snapshot from the referee): avoided, see above.
