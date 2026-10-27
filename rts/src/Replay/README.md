<!-- rts/src/Replay/README.md: the replay library (assembly Rts.Replay): recorder, versioned binary reader and writer, per-command codecs and the re-simulating verifier. -->
# Replay (assembly Rts.Replay)

Engine-free (`netstandard2.1`, no file access, no floating point) library for versioned replays. The decision, the binary layout and the compatibility rule are in [ADR-009](../../docs/decisions/ADR-009-replays.md).

| File | Holds |
| --- | --- |
| `ReplayData.cs` | `ReplayData` (header, players, commands, checkpoints, final hash), `ReplayCheckpoint`, `ReplayFormat` (magic, version, limits) |
| `ReplayBinary.cs` | `ReplayBinaryWriter` / `ReplayBinaryReader`: explicit little-endian fields, length-prefixed UTF-8 strings, checked reads |
| `CommandCodecs.cs` | `ICommandCodec`, `CommandCodec<T>`, `CommandCodecRegistry.CreateBuiltIn()`: one codec per command kind, plug-ins register theirs by kind |
| `ReplayWriter.cs` / `ReplayReader.cs` | bytes to and from `ReplayData`; the reader refuses another format version before reading anything else |
| `ReplayRecorder.cs` | host-side recorder: submit commands and step through it; `Finish()` returns the `ReplayData` |
| `ReplayVerifier.cs` | `CreateSimulation` (from header plus data set) and `Verify` (re-simulate, compare every checkpoint and the final hash) |
| `ReplayExceptions.cs` | `ReplayFormatException` (damaged file) and `ReplayRefusedException` (other version or data digest) |

Recording from a host:

The recorder refuses a simulation that already has queued commands, because the simulation cannot list them: attach it straight after `Start` and submit every setup command through it. Online, `LockstepPeer` raises `SimulationCreated`, `CommandSubmitted` and `Stepped` so a host records an online match the same way.

```csharp
ReplayRecorder recorder = new ReplayRecorder(sim, players);   // sim started, still at tick 0
recorder.Submit(command);                                      // instead of sim.Submit; AI sinks forward here too
recorder.Step();                                               // instead of sim.Step
byte[] file = ReplayWriter.Write(recorder.Finish(), CommandCodecRegistry.CreateBuiltIn());
```

The headless harness does this with `--record <file>`; `tools/ReplayCheck` verifies the file. A faction plug-in with its own command kind adds a `CommandCodec<T>` to the registry before reading or writing.

The lockstep network layer has its own command codec; the two are to be unified later (ADR-009, Consequences).
