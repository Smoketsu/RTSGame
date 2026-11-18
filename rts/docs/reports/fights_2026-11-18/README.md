<!-- rts/docs/reports/fights_2026-11-18/README.md: package F4 report of the equal-value fight sweep (rts/docs/specs/micro_benchmark.md section 9): what was run, the check results, the stalemates and the measured wins and loses now in matchups.json. -->
# Fight sweep, 18 Nov 2026 (package F4)

- Ran on: commit 51c048f (`origin/main`) plus the filled `rts/data/scripts/fights/matchups.json` of this commit (the file is outside the content digest).
- Command: `dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --fights` (after `dotnet build rts/RtsGame.sln -c Release`).
- Wall time: 22.1 s for the final run (23.2 s for the first run, before the matchups file was filled), 4 cores, Release. Far below the 5 minute limit.
- Data digest: tuning d522ef6d82fa09f1, content b0014f3f65365bb8.
- Files: `fights.md` (report), `fights.jsonl` (one record per fight), `hq_floor.jsonl` (60 HQ floor trials).

```
FIGHTS fights=780 decided=765 draws=0 stalemates=13 unresolved=2 mismatched=0 checks_passed=2 of=8 dome_absorbed=12
```

Not the expected 4 of 8: filling the matchups file made two checks stricter, see "Why 2 of 8". With the file empty the sweep gave 3 of 8 (rule 3 passed only because it had no counters to test).

## Checks

| Check | Result | Plain explanation |
| --- | --- | --- |
| rule1_no_immunity | PASS | Every enemy unit and HQ can be hit by at least 2 mobile and 1 static answers. |
| rule2_multiplier_band | FAIL | The damage table has drench against structure at 0.25, below the allowed 0.5, and drench's spread is 4 to 1 where 2 to 1 is allowed (Coral Turret and Spoutling damage). |
| rule3_counter_margin | FAIL | Measured `loses` entries are each unit's worst loss, which breaks the 3-to-1 exchange cap (for example Leviathan beats Roc Rider 11.2 to 1, Shellback beats Stormcaller with no loss). This is the Q12 prediction, not a new balance defect. |
| rule4_no_dominant_unit | PASS | No unit beats every unit of its own faction on flat. |
| rule4_every_unit_wins | FAIL | The Spoutling beats no unit of another faction on flat. |
| rule5_two_answers | FAIL | Water has 2 unit answers to massed armour, but no non-unit answer (terrain) is measured, and the rule needs 1 of each. |
| rule6_hq_floor | FAIL | Several 30-supply armies kill a Rooted HQ in under 15 s (Brinecasters x15 in 6.6 s against the Great Aerie, Thunderheads x7 in 8.4 s, Stormcallers x10 in 11.0 s, Windrunners x30 in 13.9 s). |
| matchups_agree | FAIL | The Spoutling entry has `wins: null`, and the check reports a null `wins` on an armed unit as a disagreement, the same finding as rule4_every_unit_wins. |

### Why 2 of 8

`matchups_agree` cannot pass while the measured Spoutling `wins` is null (spec 3.2 says so explicitly). Every other entry agrees with the sweep. Per the task, no data or threshold was changed to make it pass. Once `wins` is filled, rule 3 tests each `loses` as well, so it now fails where it passed on an empty file.

## Stalemates (13) and unresolved fights (2)

All are on the Shellback except one. A `no_damage` stalemate counts only damage events, and hits absorbed by the Shellback's shield dome emit none.

| Case | Finding | Reason |
| --- | --- | --- |
| flat/750/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/750/water.shellback/skai.roc_rider | stalemate | dome_absorbed |
| flat/750/water.shellback/skai_ai_mix | stalemate | dome_absorbed |
| flat/750/skai_ai_mix/water.shellback | stalemate | dome_absorbed |
| flat/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| flat/1500/water.shellback/skai.roc_rider | unresolved | time_cap (180 s) |
| flat/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed |
| flat/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed |
| high_ground/1500/skai.stormcaller/water.shellback | stalemate | dome_absorbed |
| high_ground/1500/water.spoutling/water.shellback | stalemate | dome_absorbed |
| flooded/1500/skai_ai_mix/water.grasper | stalemate | no_damage (no Shellback; ends at 159.9 s with 0.245 and 0.067 of value left) |
| choke/1500/skai.roc_rider/water.shellback | stalemate | dome_absorbed |
| choke/1500/water.shellback/skai.roc_rider | unresolved | time_cap (180 s) |
| choke/1500/skai_ai_mix/water.shellback | stalemate | dome_absorbed |
| choke/1500/water.shellback/skai_ai_mix | stalemate | dome_absorbed |

(That is 13 stalemates and 2 unresolved: the table lists 15 fights.)

Reading (inference, not checked in the event log): the 12 Shellback stalemates and both unresolved fights look like dome grinds. The Shellback cannot hit air, the Roc Rider and the Skai mix are mostly ranged or air, and the dome absorbs ranged shots without a damage event, so the idle timer expires with both sides alive. The two time_cap cases are the same pairing (Shellback in seat 0 against Roc Rider at 1500) where some hits still land. The Grasper against `skai_ai_mix` on flooded is not a dome grind: the Pummeler cannot hit air and the surviving Skai units are probably out of reach or idle; not examined.

## Measured wins and loses in matchups.json (case flat, army value 1500)

| Unit | Wins against | Loses to |
| --- | --- | --- |
| skai.roc_rider | water.grasper | water.leviathan |
| skai.stormcaller | water.spoutling | water.shellback |
| skai.talon_archer | water.spoutling | water.leviathan |
| skai.thunderhead | water.grasper | skai.roc_rider |
| skai.windrunner | water.spoutling | water.leviathan |
| water.grasper | water.spoutling | skai.roc_rider |
| water.ink_lobber | water.spoutling | skai.roc_rider |
| water.leviathan | skai.windrunner | water.shellback |
| water.reef_runner | water.spoutling | skai.roc_rider |
| water.shellback | skai.stormcaller | skai.thunderhead |
| water.spoutling | null (beats no enemy unit) | water.leviathan |

Notes: the Spoutling is the only unit with no win, and the Stormcaller, Talon Marksman and Windrunner all beat only the Spoutling. The Shellback "loses" to the Thunderhead although it stalemates against the Roc Rider (the Thunderhead has blast).

## What the sweep cannot exercise

- Downdraft (Stormcaller) and waves (Tidecaller): there is no cast control in the fight orders, so neither is used.
- Formations are not varied: one formation (width and spacing from `fights.json`) for every fight.
- The unarmed Tidecaller is not a side; no HQ, economy or strategic AI is involved (plain attack-move, idle units re-target every 2 s).
