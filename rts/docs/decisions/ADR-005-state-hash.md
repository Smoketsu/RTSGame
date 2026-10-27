<!-- rts/docs/decisions/ADR-005-state-hash.md: how the full simulation state is hashed for desync detection and determinism tests. -->
# ADR-005: State hash is 64-bit FNV-1a over a canonical byte stream

Status: Accepted, 26 October 2026.

## Context

Determinism is checked by hashing the whole simulation state at regular ticks and comparing hashes between two runs (tests, balance harness) or between machines (Phase 2 lockstep). The hash must be identical on every platform and .NET runtime, cheap enough to run every second in a headless match, and good enough that a real divergence is essentially never masked by a collision. It does not need to resist an attacker: anti-cheat (Phase 5) re-simulates replays rather than trusting hashes.

Options considered:

1. **FNV-1a 64-bit** over a canonical byte stream: 10 lines, no dependencies, no lookup tables, trivially portable.
2. **xxHash64 / MurmurHash3**: faster on large inputs and better avalanche, but more code to audit; our inputs are small (kilobytes per tick).
3. **`object.GetHashCode()` / `string.GetHashCode()`**: randomised per process on .NET Core, so banned.
4. **SHA-256**: cryptographic strength we do not need, and `System.Security.Cryptography` is an engine-adjacent dependency we would rather keep out of the simulation.

## Decision

- `StateHasher` (`rts/src/Sim/Core/StateHasher.cs`) is a small writer: `Write(int)`, `Write(uint)`, `Write(long)`, `Write(ulong)`, `Write(byte)`, `Write(bool)`, `Write(Fix64)` (its raw `long`), `Write(string)` (UTF-16 code units, then a length). Every value is fed as little-endian bytes into a running 64-bit FNV-1a (`offset 0xcbf29ce484222325`, `prime 0x100000001b3`).
- The simulation defines **what** is hashed and in **which order**: the tick, the match clock, the RNG state, the next entity ID, every player in ascending `PlayerId`, every entity in ascending `EntityId` with every simulation-relevant field, then every subsystem in a fixed order. Anything that affects future simulation must be in the hash; anything purely presentational (names, colours) must not be.
- The hash is computed every `state_hash_interval_ticks` ticks (default 20, data-driven) and emitted as a `StateHashedEvent(tick, hash)`. Tests and the headless runner also call `Simulation.ComputeStateHash()` directly at the end.
- The hash of an empty stream is the FNV offset basis, and the hash is never `0` by construction, so `0` can mean "not computed" in network messages.

## Consequences

- A collision between two genuinely different states is 1 in 2⁶⁴ per comparison, which is negligible for desync detection.
- Adding a simulation field without adding it to the hash is a bug; the Opus/Fable review of every simulation change checks for it, and a test asserts that changing each hashed field changes the hash.
- If profiling ever shows the hash as a cost, swapping the mixing function for xxHash64 is a one-file change and a new ADR; the writer API and the hashing order stay.
