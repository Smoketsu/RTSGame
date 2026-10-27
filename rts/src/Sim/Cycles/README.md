<!-- rts/src/Sim/Cycles/README.md: placeholder for Planetary Cycles, Core capture and the FFA Core win, all on the match clock; not built yet. -->
# Cycles (Phase 2 placeholder)

Not built yet. Phase 2 (Vertical slice, Apr-Jun 2027) adds here:

- Planetary Cycles every 240 s, with a 60 s warning before each one.
- Core capture, which takes 20 s.
- FFA Core win at 8 cumulative minutes (480 s) on the match clock.

All timings run on the match clock, in ticks, not on wall-clock time. Values are in `rts/data/tuning/match.json`.
