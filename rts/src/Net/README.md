<!-- rts/src/Net/README.md: placeholder for lockstep networking over Steam, desync detection and disconnect pauses; not built yet. -->
# Net (Phase 2 placeholder)

Not built yet. Phase 2 (Vertical slice, Apr-Jun 2027) adds lockstep networking over Steam here.

- Desync detection by comparing state hashes between peers (ADR-005).
- Disconnect pause: one pause of up to 5 minutes (300 s) per player.
- Pause budget: 120 s of total pause time per player.

Limits are data in `rts/data/tuning/online.json`.
