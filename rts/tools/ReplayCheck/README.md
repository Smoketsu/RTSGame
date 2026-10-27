<!-- rts/tools/ReplayCheck/README.md: the headless tool that re-simulates a replay file and verifies its state hashes (ADR-009). -->
# ReplayCheck

Re-simulates a replay from its recorded seed, map, players and command stream, and compares every hash checkpoint and the final hash with the ones recorded. Uses `Rts.Replay`; does the file IO the library may not.

```
dotnet run --project rts/tools/ReplayCheck -- --data rts/data --replay <file>
```

| Output | Exit code | Meaning |
| --- | --- | --- |
| `REPLAY OK final=<16 hex>` | 0 | Every checkpoint and the final hash matched |
| `REPLAY DESYNC tick=<n> expected=<16 hex> actual=<16 hex>` | 1 | First tick whose hash differs (the tick count if only the final hash differs) |
| `REPLAY REFUSED: <reason>` on stderr | 2 | Different format version or data digest, unknown map, damaged or missing file, unloadable data folder, or bad command line |

Record a replay with the harness: `dotnet run --project rts/tools/BalanceHarness -- --data rts/data --script rts/data/scripts/ai_vs_ai.json --record ai.rtsreplay`.

Phase 5 grows this tool into the anti-cheat re-simulator that also flags maphacks.
