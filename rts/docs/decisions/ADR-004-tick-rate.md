<!-- rts/docs/decisions/ADR-004-tick-rate.md: why the simulation runs at a fixed 20 ticks per second. -->
# ADR-004: The simulation runs at a fixed 20 ticks per second

Status: Accepted, 26 October 2026.

## Context

A deterministic lockstep simulation advances in discrete ticks. The tick rate sets command latency, how smooth movement looks before interpolation, how much CPU each headless match costs (500 games per matchup per map in the balance harness) and how design durations convert to ticks. Rule 6 says one match clock on the simulation tick drives tides, Planetary Cycles, warnings and Core capture.

Reference points: StarCraft II simulates at 16 game loops per second (22.4 at "faster" speed); Age of Empires II at about 20 updates per second; Supreme Commander at 10; most modern lockstep RTS designs sit between 10 and 30 with rendering interpolated between ticks.

## Decision

- `TicksPerSecond = 20`, so one tick is 50 milliseconds. The value is read from `data/tuning/match.json` (`ticks_per_second`) at match start and stored on the `MatchClock`; it is a tuning value, not a compile-time constant, but changing it changes every replay and is a versioning event.
- Every design duration converts exactly: tide 210 s = 4200 ticks, Planetary Cycle 240 s = 4800 ticks, warning 60 s = 1200 ticks, Core capture 20 s = 400 ticks, root 10 s = 200 ticks, uproot 5 s = 100 ticks, Momentum drain delay 1.5 s = 30 ticks, Grounded 4 s = 80 ticks. Nothing in the design doc needs sub-50 ms resolution.
- The renderer interpolates between the last two tick states, so on-screen motion is as smooth as the frame rate regardless of tick rate.
- Command latency: a command issued during tick N is scheduled for tick N + input delay. Phase 0 uses an input delay of 1 tick locally; Phase 2 networking will raise it to 2 to 4 ticks (100 to 200 ms), which is within the usual RTS range.
- Time in the simulation is only ever a tick count (`int`, range 0 to 2 147 483 647, which is over three years of play). Seconds exist only in data files and in UI text.

## Why 20 and not 10, 30 or 60

- 10 ticks per second makes a 100 ms step: ability timing and Momentum stacks (1 per second) feel coarse, and interpolation hides less at that step size.
- 30 or 60 doubles or triples the headless cost of the balance harness and the bandwidth of lockstep messages for no gameplay gain; nothing in the design has sub-50 ms interactions.
- 20 divides every design duration into whole ticks and keeps a 500-game harness run affordable on one developer machine.

## Consequences

- All `*_seconds` tuning values become `int` ticks at load via `MatchClock.SecondsToTicks`, which rounds to nearest and throws if the result is not representable.
- The state hash interval (`state_hash_interval_ticks`, default 20) is one hash per second.
- Changing the rate later is possible (it is data), but it invalidates existing replays and retunes every duration, so it needs a new ADR.
