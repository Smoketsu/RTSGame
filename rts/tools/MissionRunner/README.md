<!-- rts/tools/MissionRunner/README.md: the headless tool that plays a campaign mission with its scripted inputs and prints the mission log, state hashes and result. Spec: rts/docs/specs/campaign_scripting.md section 5. -->
# MissionRunner

Loads `rts/data`, parses and validates a mission from the campaign folder, plays it headless and prints the result. Uses `Rts.Campaign`; does the file IO the library may not.

```
dotnet run --project rts/tools/MissionRunner -- --data rts/data --campaign rts/campaign --mission tutorial_01 [--inputs rts/campaign/inputs/tutorial_01.json] [--ticks N]
```

- `--inputs` defaults to `<campaign>/inputs/<mission>.json` when that file exists; without inputs nobody plays the human slot.
- `--ticks N` stops the run after N steps (tests); a mission still running then is reported as a timeout.
- Output: the runner log (`OBJECTIVE <id> active|complete tick=<t>`, `CUE ...`, `WARN tick=<t> <rejected command>`), one `tick=<n> hash=<16 hex>` line per state hash, then `MISSION won tick=<t>`, `MISSION lost tick=<t> reason=<r>` or `MISSION timeout tick=<t>`, then `STATE_HASH=<16 hex>`.
- Exit codes: 0 won, 2 lost or timed out, 1 any error (arguments, data, mission validation; the message names the file and field, on stderr).
- Slots with controller `ai:<profile>` get a skirmish AI controller; the runner itself only issues commands as the script issuer (255) and `script` slots.
