<!-- rts/docs/decisions/ADR-001-fixed-point.md: why the simulation uses its own Q32.32 fixed-point type instead of float or a library. -->
# ADR-001: Fixed-point maths is our own Q32.32 `Fix64`

Status: Accepted, 26 October 2026.

## Context

The simulation must be deterministic across machines (lockstep multiplayer, replays, the balance harness and ranked replay re-simulation all depend on it). IEEE floating point is not reproducible across CPUs, compilers and JIT settings, so the simulation folder bans `float`, `double` and `decimal` outright. We need one number type for positions, speeds, health, timers and tuning values.

Options considered:

1. **Write our own Q32.32 type** (`Fix64`: a `long` holding value × 2^32).
2. **FixedMath.Net** (MIT, Q31.32, well known, last meaningful release years ago).
3. **Unity.Mathematics / Unity physics fixed-point forks**: engine-tied, so they violate the engine-free rule.
4. **Integers only** (e.g. positions in 1/1024 tile): simple but every call site reinvents scaling and overflow rules.

## Decision

We write our own `Fix64` in `rts/src/Sim/Core/Fix64.cs`:

- Storage: one `long` raw value; 32 integer bits (range about −2.1 billion to +2.1 billion) and 32 fraction bits (resolution about 2.3 × 10⁻¹⁰).
- Operations: add, subtract, multiply (128-bit intermediate on the magnitudes, truncating toward zero), divide (long division on the magnitudes, truncating toward zero), floor, ceiling, round, abs, min, max, clamp, sign, square root (digit-by-digit integer root, exact to the last fraction bit), comparison.
- Overflow policy: add, subtract and multiply **saturate** at `MaxValue`/`MinValue` and never wrap. Division by zero throws (`DivideByZeroException`); a throw is still deterministic, and a silent "infinity" is not a value the design wants.
- Parsing: `Fix64.Parse("1.5")` reads decimal text digit by digit so data files never pass through `double`. The fraction is rounded to the nearest 2^-32 on the magnitude (halves away from zero).
- No implicit conversion from `float`/`double`. Conversion to `float` exists only for the renderer and is marked for presentation use.

## Why our own and not FixedMath.Net

FixedMath.Net would be the permissive-licence candidate. It loses on three points: its `Q31.32` sign handling differs from the Q32.32 we document, so every range comment would have to carry a caveat; its trigonometry uses lookup tables we do not need in Phase 0 and would have to audit for determinism; and it is unmaintained, so any fix becomes a fork anyway. Our own type is about 400 lines, fully commented to the project standard, and every operation has a unit test. If a well-tested library clearly wins later (for example for trigonometry in Phase 1 pathfinding), a new ADR can replace this one; the API is small enough that a swap is mechanical.

## Consequences

- Every simulation number is `Fix64` or an integer type. Tuning files hold decimal text; the loader converts with `Fix64.Parse`.
- Multiplying two large values can saturate; design values stay far below 2 billion, and the tests cover the saturation edges.
- Square root and division are slower than float; Phase 0 has no hot loop that cares. If profiling in Phase 1 shows a problem we add a faster reciprocal path, with tests.
- Trigonometry is deliberately absent. Phase 1 adds what pathfinding needs (likely a lookup-table `Atan2` and `Sin`/`Cos`) in a follow-up ADR.
