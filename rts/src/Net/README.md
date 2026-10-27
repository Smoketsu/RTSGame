<!-- rts/src/Net/README.md: what the lockstep networking assembly (Rts.Net) is, how a host runs a peer, the file map, and the determinism rules it follows. -->
# Net (lockstep networking, assembly `Rts.Net`)

The lockstep protocol of ADR-008 (`rts/docs/decisions/ADR-008-lockstep.md`): peers exchange one command bundle per tick and one state hash per hash interval, a tick is stepped only when every live peer's bundle for it is in, a mismatching hash names the first wrong tick, a disconnect pauses the match (once per player, up to 5 minutes), voluntary pauses draw on a 120 s budget per player, and a reconnecting peer rebuilds its simulation by replaying the command log. Nothing here names Steam: the Unity project adapts Steam networking sockets to `ITransport`; tests use the in-process `LoopbackTransport`.

- Engine-free C# 9, `netstandard2.1`, references only `Rts.Sim` (`Rts.Net.csproj`, and `Rts.Net.asmdef` with `noEngineReferences`; `package.json` makes it the local Unity package `com.rtsgame.net`).
- Same determinism rules as the simulation: no floating point, no clock reads (the host passes elapsed milliseconds in), no threads, no file or socket access, sorted collections only. Everything that must agree between peers is decided by one peer (the referee) and announced.
- Limits are data in `rts/data/tuning/online.json` (`OnlineTuning`): `input_delay_ticks`, `disconnect_pause_max_seconds`, `disconnect_pauses_per_player`, `pause_budget_seconds_per_player`, `catch_up_max_ticks_per_update`.

## Host loop

```
LockstepRoster roster = new LockstepRoster(); roster.Add(new PeerId(0), new PlayerId(0)); roster.Add(new PeerId(1), new PlayerId(1));
LockstepPeer peer = new LockstepPeer(roster, myPeerId, transport, CreateMatch, BuiltInMechanics.CreateCodecRegistry(), LockstepSettings.FromTuning(data.Tuning.Online));
peer.BeforeStep += p => { /* flush player input or run the AI: commands stamped with p.NextInputTick, p.SubmitLocal(command) */ };
peer.NoticeRaised += notice => hud.Show(notice);
peer.SimulationCreated += sim => recorder = new ReplayRecorder(sim, players);   // optional replay (ADR-009): before any command goes in
peer.CommandSubmitted += command => recorder.Record(command);
peer.Stepped += sim => recorder.AfterStep();
peer.Start();                      // every roster peer must be connected; or peer.Join() on a fresh instance to reconnect
while (running) { peer.Update(elapsedMs); render(peer.Simulation); }
```

`CreateMatch` returns a `LockstepMatch`: a `Simulation` built from the same seed, data and map on every peer, started, at tick 0, with nothing queued, plus the setup commands (the script's spawns), which the peer submits itself; the peer calls it at `Start` and again on every reconnect. `peer.Simulation` is the live simulation (a new object after a reconnect). `RequestPause` / `RequestResume` drive voluntary pauses. `Phase` is `Running`, `Paused`, `Joining`, `Desynced` or `Failed`; `Notices` (or the event) report desyncs, pauses, drops, rejoins, inconsistencies and injected commands.

## Command encoding

Commands inside bundles are framed by `MessageCodec` (kind, tick, issuer, sequence, length-prefixed payload); the payload is encoded by the shared command codec in the simulation assembly, `Rts.Sim.Commands.Codec` (ADR-008), the same encoding the replay files use. This assembly has no per-command code. Build the registry with `BuiltInMechanics.CreateCodecRegistry()` (core codecs plus the faction codecs, e.g. the Water `cast_wave`). `LockstepPeer.SubmitLocal` throws `ArgumentException` for a command the registry cannot encode (no codec, a string over 65,535 UTF-8 bytes, a lone surrogate).

## Files

| File | Role |
| --- | --- |
| `PeerId.cs` | `PeerId` (0 to 254, `None` 255) |
| `ITransport.cs` | `ITransport`, `TransportEvent`, `TransportEventKind`, `TransportLimits` (the 512 KiB payload cap): reliable ordered delivery plus connection reports, pulled |
| `LoopbackTransport.cs` | `LoopbackNetwork` hub with latency in host ticks (per link if wanted), an unreported-crash disconnect, reorder and drop knobs (off by default), and its endpoints |
| `NetWriter.cs`, `NetReader.cs` | Explicit little-endian primitives for the message framing; strings as UTF-16 code units; bounds-checked reads. Command parameters are not written here |
| `NetProtocolException.cs` | The one exception for malformed or protocol-breaking input |
| `Messages.cs` | Every message type, `PauseReason`, `PauseEnd`, `TickRange`, ledger and range entries, the `SyncLog` and its `SyncLogPartMessage` |
| `MessageCodec.cs` | Version header, kind byte, fields; `SplitSyncLog` / `JoinSyncLog` (parts under the payload cap, empty bundles run-length encoded); `ProtocolVersion` bumps on any wire change |
| `LockstepMatch.cs` | What the match factory returns: the empty started simulation and the setup commands the peer submits and reports |
| `LockstepSettings.cs` | The limits in ticks and milliseconds, from `OnlineTuning` |
| `LockstepRoster.cs` | Peer to players map |
| `PauseLedger.cs` | Per-player disconnect pauses used and budget spent |
| `LockstepNotice.cs` | What the peer reports to the host |
| `LockstepPeer.cs` | The protocol: bundles, stepping, hashes, referee decisions, pauses, drops, rejoins, reconnect |

## Tests

`rts/tests/Net.Tests`: commands carried in bundles for every command kind (the per-command codec and string-rule tests are in `rts/tests/Sim.Tests/Commands`), every message kind, local submit validation, and a 48,000-tick sync log split under the payload cap; the loopback hub; two peers playing `rts/data/scripts/ai_vs_ai.json` through the protocol to a decided outcome with matching hashes; an injected divergence reported at the first hash tick after it; a disconnect pause and a reconnect that replays the log; the expired disconnect pause; the once-only disconnect pause and the resync pause; the per-player pause budget; a reordering transport. `MultiPeerLockstepTests.cs` runs three- and four-peer sessions: the full match, the referee handover, the rejoin tick, dropped peers' hashes, the pause count on every peer, bundle requests over a slow link, a joiner seeing another rejoin, an unreported restart, forwarded bundles, and an online replay recorded through the peer's hooks. `LockstepSession.cs` there is the reference host loop with AI controllers attached through `BeforeStep`.
