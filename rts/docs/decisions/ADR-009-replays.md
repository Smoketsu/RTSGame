<!-- rts/docs/decisions/ADR-009-replays.md: what a replay holds, its binary layout and version rule, and how ReplayCheck re-simulates it. -->
# ADR-009: Replays are the applied command stream plus hash checkpoints, in a versioned little-endian binary file

Status: Accepted, Phase 2.

## Context

The simulation is deterministic and everything is a command (CLAUDE.md, architecture rule 2), so a match is fully described by its inputs: seed, map, players, the data it ran on and the commands applied on each tick. A replay is that description. It serves three uses: watching a match again, checking determinism after a code or data change (`tools/ReplayCheck`), and, in Phase 5, re-simulating a submitted match for anti-cheat. A replay must stay valid and checkable across builds, or fail loudly: silently playing an old replay on changed rules would show a match that never happened.

## Decision

### What a replay holds

1. **Format version** (u32, currently 1).
2. **Content digest** of the data set: `Simulation.ContentDigest`, the FNV-1a digest of every unit and faction definition, the tuning file texts and the map, computed by `Simulation.Start` and already part of every state hash. It changes when any of that data changes. Planetary Cycle definitions and AI profiles are not part of it today; cycles will have to be mixed into `ContentDigest` when they enter the simulation (Phase 2 step 4), and until then a changed cycle file shows up as a hash desync instead of a refusal. AI profiles are not needed to play a replay because the AI's output is already in the command stream.
3. **Seed** (u64), **map ID**, **hash interval** (`state_hash_interval_ticks`), **tick count** (steps run) and the **player setup** (slot, team, faction ID, display name).
4. **The full applied command stream** in (tick, issuer, sequence) order, the order the simulation applies them in. "Applied" means every command that reached a stepped tick, accepted or rejected, whoever submitted it: the match script, a human, the AI, a network peer. Commands scheduled at or after the tick count never ran and are not stored. Rejected commands are kept because a rejection is a deterministic outcome of the state and a replay is a record of inputs, not outcomes.
5. **Hash checkpoints**: the `StateHashedEvent` hash at every multiple of the hash interval below the tick count (ADR-005).
6. **Final hash**: `Simulation.ComputeStateHash()` after the last step (clock at the tick count).

### Binary layout

Little-endian throughout; every field written explicitly in this order (no reflection, no `BinaryWriter`, no struct dumps). A string is a u32 byte length then UTF-8 bytes (at most 65 535 bytes, strict UTF-8). Fix64 and FixVec2 are their raw i64 values; entity IDs are u32; player and team IDs are one byte (255 = none).

```
magic            8 bytes   ASCII "RTSREPLY"
format version   u32
content digest   u64
seed             u64
hash interval    u32       ticks
tick count       u32       ticks
map ID           string
player count     u32
  per player:    id u8, team u8, faction string, display name string
command count    u32
  per command:   tick i32, issuer u8, sequence u32, kind string, payload length u32, payload bytes
checkpoint count u32
  per checkpoint: tick i32, hash u64
final hash       u64
checksum         u64       FNV-1a (ADR-005 hasher) over every byte before it
```

The payload of a command is written by the codec registered for its kind (`CommandCodecRegistry` in `Rts.Replay`): one explicit codec per command class, fields in a fixed order, no field omitted. The length prefix lets a reader report an unknown kind or an under- or over-read payload precisely. Faction plug-ins add the codec for their own kinds by registering it by kind, the same way they register their mechanic; the Water `cast_wave` codec ships built in. A new command class without a codec fails a test that enumerates every `Command` subclass in the simulation assembly.

### Compatibility rule

- A replay is played only by a build that reads its **format version** and only against the **data set** whose content digest it carries. Anything else is refused with a clear message and no simulation step runs: ReplayCheck prints `REPLAY REFUSED: <reason>` on stderr and exits 2. There is no silent conversion; a version bump comes with a new ADR saying what changed, and an old-version reader may be added later if old replays must stay playable.
- The version is read first, before the checksum and every other field, because a different version may lay out the rest differently.
- The version is raised whenever the layout or the meaning of a field changes, including when a command's parameters change. Match options not carried in the header (for example a non-default `IHandOverPolicy`) must be the defaults; a host that changes them has to add them to the header with a version bump.
- A damaged file (bad magic, truncation, checksum mismatch, unknown command kind, invalid field) is a `ReplayFormatException`, also exit 2.

### Recording

The simulation has no "command applied" hook (events go out, never in), so recording is a host-side wrapper: `ReplayRecorder` is attached to a started simulation at tick 0, the host submits every command through `ReplayRecorder.Submit` (or reports it with `Record`) and steps through `ReplayRecorder.Step` (or calls `AfterStep`). AI controllers submit through an `ICommandSink`; the BalanceHarness wraps each controller (`RecordingController`) so its sink also records. `Finish` drops commands past the last stepped tick and refuses to build a replay whose checkpoints are incomplete.

### Verification (ReplayCheck)

`ReplayVerifier.CreateSimulation` builds the match from the header (seed, map, players, the data folder given to the tool) and starts it. `Verify` checks the content digest and hash interval, submits all commands, steps the recorded tick count and compares every `StateHashedEvent` with the checkpoint of that tick, then the final hash. The first difference is reported as `REPLAY DESYNC tick=<n> expected=<hash> actual=<hash>` (exit 1); a final-hash-only difference is reported at the tick count; a clean run prints `REPLAY OK final=<hash>` (exit 0). A tampered or dropped command first shows at the next checkpoint at or after its tick, so the reported tick is within one hash interval of the cause.

## Consequences

- A 4 300-tick AI match with 500 commands is about 23 kB, so replays can be kept for every match and attached to bug reports.
- ReplayCheck doubles as a determinism regression test across code changes: a replay recorded before a change must still verify after it unless the change was meant to alter the rules, in which case the data digest or the format version moves with it.
- The replay codec is self-contained in `Rts.Replay`. The lockstep network layer (ADR-008) has its own command codec; both encode the same command objects and should be unified into one shared codec later, with the replay payloads kept byte-compatible or the version bumped.
- Cycle definitions are not in `ContentDigest` yet (see above).
- Replays do not store the AI profiles or the script, so a replay cannot say why a command was issued, only that it was.

## Alternatives considered

- **Store the script and the AI profiles and re-run the AI** to regenerate commands: smaller files, but every AI change would invalidate old replays and the replay would depend on AI code, not just the simulation.
- **Snapshot the state periodically** for fast seeking: not needed for verification; can be added as a later optional section under a new version.
- **JSON replays:** human readable but large, and number formatting is a determinism hazard. Binary with an explicit layout is exact and compact.
- **One `BinaryWriter`/`BinaryFormatter` dump of the command objects:** ties the file to class layouts and the host's endianness; the explicit codec per kind keeps the format a deliberate contract.
