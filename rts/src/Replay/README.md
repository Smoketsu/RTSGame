<!-- rts/src/Replay/README.md: the replay library (assembly Rts.Replay): recorder, versioned binary reader and writer, the re-simulating verifier. Command parameters are encoded by the shared codec in `Rts.Sim.Commands.Codec`. -->
# Replay (assembly Rts.Replay)

Engine-free (`netstandard2.1`, no file access, no floating point) library for versioned replays. The decision, the binary layout and the compatibility rule are in [ADR-009](../../docs/decisions/ADR-009-replays.md).

| File | Holds |
| --- | --- |
| `ReplayData.cs` | `ReplayData` (header, players, commands, checkpoints, final hash), `ReplayCheckpoint`, `ReplayFormat` (magic, version, limits) |
| `ReplayBinary.cs` | `ReplayBinaryWriter` / `ReplayBinaryReader`: explicit little-endian fields for the file framing, length-prefixed UTF-8 strings, checked reads |
| `ReplayWriter.cs` / `ReplayReader.cs` | bytes to and from `ReplayData`; the reader refuses another format version before reading anything else |
| `ReplayRecorder.cs` | host-side recorder: submit commands and step through it (each command is checked against the codec registry first); `Finish()` returns the `ReplayData` |
| `ReplayVerifier.cs` | `CreateSimulation` (from header plus data set) and `Verify` (re-simulate, compare every checkpoint and the final hash) |
| `ReplayExceptions.cs` | `ReplayFormatException` (damaged file) and `ReplayRefusedException` (other version or data digest) |

Recording from a host:

The recorder refuses a simulation that already has queued commands, because the simulation cannot list them: attach it straight after `Start` and submit every setup command through it. Online, `LockstepPeer` raises `SimulationCreated`, `CommandSubmitted` and `Stepped` so a host records an online match the same way.

```csharp
ReplayRecorder recorder = new ReplayRecorder(sim, players);   // sim started, still at tick 0
recorder.Submit(command);                                      // instead of sim.Submit; AI sinks forward here too
recorder.Step();                                               // instead of sim.Step
byte[] file = ReplayWriter.Write(recorder.Finish(), BuiltInMechanics.CreateCodecRegistry());
```

The headless harness does this with `--record <file>`; `tools/ReplayCheck` verifies the file. A faction plug-in with its own command kind adds a `CommandCodec<T>` (in `Rts.Sim.Commands.Codec`) to the registry before reading or writing; the built-in ones are registered by `BuiltInMechanics.CreateCodecRegistry()`. The same registry and encoding are used by the lockstep protocol (`Rts.Net`, ADR-008), so a command that plays online can always be recorded.

The header carries a cycles-enabled flag (format version 2): `ReplayRecorder.Finish` sets it from the simulation, and `ReplayVerifier.CreateSimulation` passes the data set's Planetary Cycle list only when it is set.
