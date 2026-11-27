<!-- rts/docs/specs/roster_numbers_2.md -->
<!-- Build spec (27 Nov 2026), written after the developer's decision "Water numbers just straight up need a buff": where the numbers gap between Water and Skai sits (income, production, trades, air), nine small game trials on scratch data showing that no number package tried moves AI wins, the smallest guarded package proposed (Shellback build time, Spoutling damage and health), and a confirming measurement fixed before any run (base seed 20261203, data-paired design against aggressive_s5, a straight-fight ceiling, mirror guards, response suite, suite green, verdict rule) with its build packages and pins. No code change, no new unit. Part of rts/docs/specs (index: README.md); built by a Sonnet builder; the sim-reviewer pass is still required because the change moves every pinned hash. -->
# Spec: roster numbers 2 (a Water buff)

Written 27 Nov 2026 against `main` at `c31e4c9`. Every value in this spec is a **placeholder** for playtesting. The roster is open for number changes only (developer, 27 Nov: "Water numbers just straight up need a buff"); no new unit and no new mechanic.

## 0. Goal, sources, ground rules

**Goal.** Find where the numbers gap between Water and Skai sits, propose the smallest set of number changes in `rts/data` expected to help Water, and fix the confirming measurement before anything is run. Skai wins every AI balance run (72 of 72; 71 of 72 with `aggressive_s5` on seed 20261201), while Water wins most equal-cost attack-move fights.

**Sources read.** `CLAUDE.md`; `specs/roster_numbers_1.md` and `reports/roster_numbers_1/README.md`; `reports/ai_reef_on/README.md`; `specs/ai_kite_correction.md` sections 5 and 6; on the unmerged branch `origin/review/ai-kite-build`: `reports/ai_kite/stats_main.md`, `main/behaviour.jsonl` and the four traces; every unit file of both factions; `tuning/{economy,terrain,combat,statuses}.json`; `scripts/fights/{fights,matchups}.json`; `tools/BalanceHarness/README.md`.

**Ground rules.**
- **Data only.** Three values in two unit files, with their tuning comments and description text (section 2). No `rts/src` or `rts/tools` change. Tests change only where a pin moves or a test asserts a changed value (section 5).
- **Determinism.** The values are read by the existing loader into `Fix64` (damage, health) and whole ticks (build time). No new rule, random draw or iteration. The content digest changes, so every state hash moves. The sim-reviewer checks that nothing else changed.
- **Nothing is tuned on the confirming seed** (20261203). The designer never ran it. Trials used seed 20261127, which is now spent.

## 1. Diagnosis

Labels: **measured** (a harness run named in 1.6), **data** (arithmetic on `rts/data`), **trace** (a committed trace: one game, an example, not a median), **inference**.

### 1.1 Income and production are not the gap before 180 s (measured)

These are medians over the 72 Water slot-games of `aggressive_s5` in the K-B main run (`ai_kite/main/behaviour.jsonl`, seed 20261201, Skai played by `aggressive_s5_kite`, whose Skai plan equals `aggressive_s5`). Each income figure is common deposited per 30-s bucket.

| Measure | Water | Skai |
| --- | --- | --- |
| Income, sum of the six buckets to 180 s | 2727 | 2805 (Water 3 percent lower) |
| Income, buckets 210 to 300 s | 780, 823, 845, 865 | 855, 848, 890, 1030 |
| Workers at 150 / 180 / 210 s | 15 / 17 / 17 | 15 / 17 / 20 |
| Army supply at 180 / 300 / 450 s | 10 / 23 / 9 | 13 / 33 / 68 |
| Common in the bank at 300 s | 90 | 740 |
| Worker losses before the overrun | 5 | 1 |
| Seconds with an enemy raid at home (`raid_seconds`) | 112 | 18.5 |

**Data:** supply bought per common is the same for both factions' early units. Pummeler and Talon cost 2 supply per 100 (0.020). Reef Runner and Windrunner cost 1 per 75 (0.013). Shellback costs 3 per 175 (0.017). So costs and supply values do not create a 23-to-33 gap.

**Reading:** Skai floats 740 common at 300 s and Water floats 90. Skai's army is capped by its producers and supply buildings, not by its income. Water spends everything it earns and still fields less.

### 1.2 The gap opens between 210 and 300 s, in trades (trace)

In both K-B Skai-against-Water traces (`ai_kite/traces/skai_vs_water_game0.txt` and `game1.txt`), Water's army is larger at 180 s: 6 Pummelers and 3 Reef Runners (15 supply) against 8 Windrunners and 2 Talons (12). From 210 to 300 s Water loses 8 Pummelers and 6 to 8 Reef Runners. Skai loses 4 to 7 Windrunners. By 300 s Water is at 58 of 58 supply, Skai at 66 of 66. This is the kited fight of `roster_numbers_1.md` 1.2: the Pummeler loses by 0.6 to 0.7 of value whenever the AI kites it.

### 1.3 After 300 s Water has no dry-ground answer to the Roc Rider (measured, data)

Skai trains 12 to 15 Roc Riders per game (first at about 270 s). In every trial below, Skai's army supply at 450 s is 63 to 66, against Water's 17 to 24. Water units that can hit air on dry ground: the Spoutling, the Coral Turret and the Leviathan (tech: 300 common and 80 rare, 60 s build, needs a workshop and research). The Reef Runner and Shellback hit air only while standing on a flooded tile.

Equal-cost attack-move fights on the default sweep (flat case, margin = Water's value left minus Skai's):

| Water side against `skai.roc_rider` | Margins (flat, both values, both seats) |
| --- | --- |
| Spoutling (the designated anti-air unit) | -0.70 to -0.75 |
| Shellback | -0.06 to -0.31 |
| Pummeler, Reef Runner, Ink Lobber | -1.00 (cannot hit air) |
| Leviathan | +0.79 to +0.91 |
| `water_ai_mix` (3 Pummeler, Shellback, Ink Lobber, Spoutling) | -0.15 to +0.71 (the Spoutling's drench grounds the Roc so the others can hit it) |

The Spoutling loses every fight in the sweep (`matchups.json` lists no win for it), including the anti-air fight it exists for.

### 1.4 Number changes move the 300 s army, not the wins (measured)

Nine trials (1.6) changed Water numbers on scratch data. Seven of them were single levers or packages that stayed inside the unit identities. Every one gave **0 Water wins in 36 games**. The largest dose, T8 (eight values, health up 15 to 18 percent on three core units), gave 1. The trials did move the 300 s army ratio (up to 25 better against 10 worse pairs) and the overrun (up to 33 s later).

### 1.5 Where the numbers gap is, and what numbers can do

1. **The kited Pummeler, 210 to 300 s** (1.2). This is Water's whole army deficit at 300 s. `roster_numbers_1` kept it as a designed weakness. The numbers that would remove it either break the melee identity (range 3, or base speed above 4.375 so the Windrunner cannot kite it) or do not help: speed 3.5 (T1) did nothing in games, and health +20 (RN1 T2) did nothing in kited fights.
2. **Production of the unit that wins kited fights.** The Shellback still wins when kited (+0.81 to +0.89, RN1 1.2). It comes from one Shell Forge at 28 s each, about 9 per game. Its build time is the one number that raises its output without changing any equal-cost fight.
3. **No dry-ground answer to the Roc Rider, 300 to 450 s** (1.3). The deciding collapse happens here: 66 against 17 to 24 at 450 s in every trial. The Spoutling is the designated answer and loses by 0.72.
4. **Not the gap:** income to 180 s, worker durability (losses come after the trades, 1.1), unit cost per supply, Tender speed (T5 moved nothing).

**Inference, stated plainly:** AI wins respond very little to Water numbers. A buff big enough to move them measurably (about 8 extra wins in 72 pairs, 4.3) would be larger than T8, and T8 already lifts straight-fight margins past the soft-counter cap of 0.35 (Reef Runner against Talon +0.11 to +0.48). The rest of the loss is AI behaviour: Water attacks into kiting, hits its supply cap at 210 to 300 s while spending on turrets and tech, and has no plan against massed Roc Riders. That is outside a numbers spec (Q-5).

### 1.6 Trials (designer's worktree at `c31e4c9`, Release, uncommitted scratch copies of `rts/data`, all deleted)

Games: `--balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --games 36 --seed 20261127 --fights-from rts/docs/reports/roster_numbers_1/sweep/fights.jsonl`, with `RTS_CHUNK_GAMES=1`. Pairs are matched by game index against the base run on the same seed; each pair has one Water slot-game. "Ratio" is Water's army supply over Skai's at 300 s, given as pairs better / worse. Fights: default `--fights` sweep, the 42 flat cross-faction pairs; "max rise" is the largest margin increase among the 23 pairs Water already wins.

| Trial | Change (old to new) | Water wins of 36 (new-only / base-only) | Ratio better / worse; median | Overrun median (base 315 s) | Fights: mean margin (base +0.018); max rise; pairs crossing +0.35 |
| --- | --- | --- | --- | --- | --- |
| base | none | 0 | 0.728 | 315 | |
| T1 | Pummeler speed 3.0 to 3.5 | 0 (0/0) | 9 / 18; 0.702 | 304 | not run |
| T2 | Shellback `build_seconds` 28 to 20 | 0 (0/0) | 18 / 12; 0.748 | 326 | no fight changes (build time is not in a fight) |
| T3 | Reef Runner range 4 to 5 | 0 (0/0) | 15 / 21; 0.667 | 321 | not run |
| T4 | dry multiplier 0.85 to 1.0, Shellback build 20, Reef Runner damage 6 to 7 | 0 (0/0) | 20 / 16; 0.735 | 348.5 | +0.044; +0.26 (Reef against Talon); 2 (Reef against Talon and Windrunner) |
| T5 | Tidepool `provides_supply` 8 to 10, Tender speed 2.0 to 2.5 | 0 (0/0) | 16 / 20; 0.771 | 308 | not run |
| T6 | Spoutling damage 5 to 8, health 70 to 90 | 0 (0/0) | 9 / 15; 0.719 | 330.5 | +0.066; +0.21 (`water_ai_mix` against Roc); 0 |
| T7 | T4 + T6 | 0 (0/0) | 21 / 15; 0.826 | 332 | +0.093; +0.26; 2 |
| T8 | T7 + health: Pummeler 110 to 130, Reef Runner 66 to 75, Shellback 220 to 260 | 1 (1/0) | 25 / 10; 0.784 | 343.5 | +0.112; +0.37; 3 |
| **P** | **Shellback build 20, Spoutling damage 5 to 10, health 70 to 90** | **0 (0/0)** | **20 / 16; 0.783** | **342.5** | **+0.096; +0.18 (`water_ai_mix` against Roc); 0** |

Fight-only variants of the Spoutling (no games): S1 damage 10 and health 90 (the values in P): against Roc -0.72 to -0.10, Stormcaller -0.78 to +0.08, Windrunner -0.79 to -0.36, Talon -0.82 to -0.46. S2 damage 9 and health 100: Roc -0.12. S3 damage 12 and health 70: Roc -0.28. In P the median Shellbacks trained per Water game went from 9 to 12, more in 29 pairs and fewer in 5. Median Spoutlings went from 10 to 11.

One more check: the default sweep on `main` matches `reports/roster_numbers_1/sweep/fights.jsonl` in every flat cross-faction margin, so that file is still the current sweep.

## 2. The change

### 2.1 Change list

| # | File | Key | Old | New | Unit | Reason | Buffs straight fights Water already wins? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `rts/data/units/water/shellback.json` | `build_seconds` | 28 | **20** | seconds | Gap 2 of 1.5. More of the one Water unit that beats Skai's early army even when kited, from the same single Forge. It enters no equal-cost fight. | **No change to any fight margin.** It raises the Shellback's output per producer-second from 6.25 to 8.75 value per second, the highest in the game (Pummeler 5.56, Talon 5.0). For human players this is the main risk; see guard G-8 and Q-3. |
| 2 | `rts/data/units/water/spoutling.json` | `weapon.damage` | 5 | **10** | health points per hit | Gap 3 of 1.5. The designated anti-air unit should come close to an even trade with the Roc Rider (S1: -0.72 to -0.10) and help the drench mix ground it sooner. | **Barely.** The Spoutling wins no fight today. Its ground fights still lose (Windrunner -0.36, Talon -0.46). Fights Water already wins rise only where a Spoutling is in the mix: `water_ai_mix` against Roc +0.37 to +0.55 (the largest rise, +0.18). One Skai claim flips: the Stormcaller no longer beats the Spoutling (-0.78 to +0.08). |
| 3 | `rts/data/units/water/spoutling.json` | `max_health` | 70 | **90** | health points | Same as row 2. With 70 health it dies in 5 Roc hits; 90 needs 7, so the Spoutling lives long enough for its drench to ground a Roc (3 hits, `spoutling_drench_hits_to_ground`). | Same as row 2 (measured together as S1 and P). |

Considered and not proposed (each was measured in 1.6):
- Pummeler speed (T1): no effect in games.
- Reef Runner range (T3): no effect in games.
- The dry multiplier and Reef Runner damage (T4): the Reef Runner's fights against the Talon and Windrunner cross the 0.35 cap.
- Tidepool supply and Tender speed (T5): no effect in games.
- The T8 health increases: they buff fights Water already wins by up to +0.37, and still give only 1 win in 36.
- Any Skai change: the brief is a Water buff.

### 2.2 Text that changes with it (no other number)

- `shellback.json`: the comment above `build_seconds` becomes exactly
  `// Time to train this unit at a producer; 20 (was 28) so the single Shell Forge fields more of the unit that still wins when kited (rts/docs/specs/roster_numbers_2.md 2.1). Unit: seconds. Range: 0.05 to 3600. Source: rts/docs/specs/roster_numbers_2.md 2.1 (was Economy); placeholder.`
  Append to `$doc`: " Build time lowered to 20 s by rts/docs/specs/roster_numbers_2.md."
- `spoutling.json`: the comment above `max_health` becomes exactly
  `// Health at full; 90 (was 70) so it survives long enough against skai.roc_rider for its drench to ground it. Unit: health points. Range: 1 to 100000 (design placeholders sit between 40 and 160). Source: rts/docs/specs/roster_numbers_2.md 2.1 (was placeholder for Phase 1 unit docs); placeholder.`
  The comment above `weapon.damage` becomes exactly
  `// Damage per hit before the damage table; 10 (was 5) so the anti-air unit comes close to an even trade with skai.roc_rider at equal cost while still losing its ground fights. Unit: health points. Range: 0.01 to 100000. Source: rts/docs/specs/roster_numbers_2.md 2.1 (was placeholder for Phase 1 unit docs); placeholder.`
  Append to `$doc`: " Damage 10 and health 90 set by rts/docs/specs/roster_numbers_2.md."
- `rts/data/scripts/fights/matchups.json`: no value changes (Q-4 default). The comment line of `skai.stormcaller` gets the sentence " Its win over water.spoutling no longer holds after rts/docs/specs/roster_numbers_2.md (matchups_agree reports it); left for the developer (Q-4)."

## 3. Determinism and state hash

There is nothing new to hash. The changed values reach the simulation through `UnitDefinition` (Fix64 health and damage, build time converted to whole ticks by the existing loader) and through the content digest, which is already in the hash. No step-order change.

## 4. Confirming measurement (fixed before any run; nothing is tuned after a result)

### 4.1 Runs

The **base seed is 20261203** for every game run. It is unspent: it is not in any file on any branch at `c31e4c9`, and the designer never ran it. Spent seeds are 5551, 20261117, 20261201, 31337 and (by this spec's trials) 20261127. Build with `dotnet build rts/RtsGame.sln -c Release` and run from the repository root. `<dir>` is `rts/docs/reports/roster_numbers_2`. "Before" means `main` with no data change; "after" means the commit with section 2. The code is identical in both.

```
RTS_CHUNK_GAMES=1 RTS_WORKER_CACHE=<dir>/cache_<run> dotnet run --project rts/tools/BalanceHarness -c Release --no-build -- --data rts/data --balance --profile aggressive_s5 --maps prototype_1v1 --matchups skai_vs_water --games 72 --seed 20261203 --fights-from rts/docs/reports/roster_numbers_1/sweep/fights.jsonl --jobs 4 --out <dir>/<run>
```

| Run | When | `<run>` | Differences from the command above |
| --- | --- | --- | --- |
| M-before | before | `main_before` | none |
| M-after | after | `main_after` | none |
| X-before / X-after | before / after | `mirrors_before` / `mirrors_after` | `--matchups skai_vs_skai,water_vs_water --games 24` |
| F-after | after | `sweep` | `--fights --jobs 4 --out <dir>/sweep` (default settings, no balance flags) |
| H-before / H-after | before / after | `bars_before` / `bars_after` | `--fights --fight-settings rts/data/scripts/fights/roster_numbers_1.json --jobs 4` |
| R-before / R-after | before / after | `response_before.txt` / `response_after.txt` | `--response --profile aggressive_s5`, stdout to the file |

- **Resumable pieces.** Every game run uses `RTS_CHUNK_GAMES=1` and an `RTS_WORKER_CACHE` folder inside `<dir>`. After every 24 finished games, and after each run, commit the cache folder and the outputs and push. Rerunning the same command after a container restart resumes from the cache.
- **Pairing across data sets.** The game seed depends on (base seed, map, matchup, game index), not on the data (`SeedDerivation.GameSeed`). So game g of M-before and game g of M-after form a pair with the same seed and seats, one Water slot-game each. No harness flag pairs two data sets, so the builder joins `main_before/balance.jsonl` and `main_after/balance.jsonl` on (`matchup`, `game`) with a throwaway script (not committed, as for `ai_kite/stats_main.md`). The builder commits the per-pair table as `<dir>/pairs.md` with these columns: game, Water slot, Water won before / after, Water army supply at 300 s over Skai's (`army_supply[1]` of each summary) before / after, Shellbacks trained before / after.
- **Suite in chunks**, as `reports/ai_reef_on/suite_chunks.md`: record each chunk's totals in `<dir>/suite_chunks.md` and push after each chunk.

### 4.2 Bars

Every paired bar uses the exact two-sided sign test at α = 0.05 on the decisive pairs (ties left out), from Water's point of view.

| ID | Run | Measure (direction) | Kind | Pass when |
| --- | --- | --- | --- | --- |
| B-1 Water wins (the point) | M | Water won (higher) | primary | BETTER |
| B-2 Army kept | M | army ratio at 300 s (higher); a pair with a null sample is left out | secondary | not WORSE |
| B-3 The change reached the games | M | Shellbacks trained (higher) | mechanism | BETTER |
| G-1 Straight-fight ceiling | F-after against `reports/roster_numbers_1/sweep/fights.jsonl` | the flat cross-faction pairs (Water unit or composition against Skai unit or composition), mean Water margin over the valid fights of the pair (both values, both seats) | guard | (a) no pair Water wins in the old file rises by more than **+0.20**; (b) no pair rises from +0.35 or below to above +0.35; (c) the mean over all pairs is at most **+0.12** (old +0.018) |
| G-2 Held-out ceiling | H-before against H-after | the flat cross-faction pairs of the bar set (army values 375 to 2400, arena 96 x 48) | guard | (a) and (b) of G-1, plus Spoutling against Roc Rider mean margin at most +0.35 (the soft-counter cap of `roster_numbers_1.md` 3.2) |
| G-3 Only Spoutling fights change | F-after against the RN1 sweep | every record without a `water.spoutling` unit on either side (directly or in a composition) is identical, ignoring `seed` and `state_hash`; the `FIGHTS` line may differ only in counts explained by Spoutling records; every `CHECK` line is identical, except that `matchups_agree` adds the Stormcaller line (designer's S1: `skai.stormcaller: claims it beats water.spoutling on flat; measured seat 0 loss (left 0.00), seat 1 win (left 0.33)`) | guard | holds |
| G-4 Water mirror | X | (a) `water_vs_water`: decided games after are not fewer than before minus 2 (of 24); (b) in F-after, every Water-against-Water record without a Spoutling side is identical (part of G-3) | guard | holds |
| G-5 Skai mirror | X | every `skai_vs_skai` game after has the same `result`, `end_tick` and `play_signature` as the same game before (the `state_hash` differs through the content digest) | guard | 24 of 24 |
| G-6 Response suite | R | no scenario that passes before fails after | guard | holds |
| G-7 Determinism, suite, pins | M-after, suite | the spot check prints 0 mismatches; full suite green after the re-pins of section 5; every re-pin is explained by the content digest or the three values; no assertion loosened | guard | holds |
| G-8 Shellback production (human-player guard) | M-after | median Shellbacks trained per Water game at most 2 times the M-before median, and the Shellback share of trained Water army units at most 0.35 | guard | holds |

G-1 to G-3 were fixed from the designer's S1 run: max rise +0.18, nothing crossing +0.35, mean +0.096, and a matchups diff of the Stormcaller only. They have small headroom on purpose. The designer has not run G-2, G-4 to G-6 or G-8. In trial P, G-8's measures were median 12 against 9 Shellbacks.

### 4.3 Stated power (before the run)

Baseline Water wins are about 0 to 1 of 72 (0 of 36 on 20261127; 1 of 72 on 20261201), so D, the number of decisive pairs, is about the number of pairs where only the changed data's Water wins. The thresholds and power for 72 pairs are those of `ai_kite_correction.md` 5.3, because the design is the same: 8 decisive pairs need 8 better, 10 need 9, 12 need 10. With nothing the other way, power is 0.22 for 4 new-only wins, 0.56 for 6 and 0.82 for 8. **Minimum detectable effect at power 0.8: about 8 of 72 pairs (11 percent) in which only the changed data's Water wins.** B-2 needs, for example, 45 better of 72 decisive pairs. B-3 in trial P was 29 against 5, so it should pass easily.

### 4.4 Verdict rule

- **PASS**: B-1 BETTER, B-2 not WORSE, B-3 BETTER, and G-1 to G-8 all hold.
- **NO_MEASURABLE_EFFECT**: B-1 not significant (and not WORSE), B-2 not WORSE, B-3 BETTER, and G-1 to G-8 all hold.
- **FAIL**: any bar WORSE, B-3 not BETTER, or any guard fails.

On PASS the change is merged. On NO_MEASURABLE_EFFECT it is merged only if the developer agrees (Q-1 default: merge, because the guards show it is safe and it closes two fight-level holes). On FAIL the data change is reverted on the branch and the report goes to the developer. There is **no revision round on 20261203**. A new value is a new spec revision with a new unspent seed.

### 4.5 Expected result (stated before the run)

- **B-1: NO_MEASURABLE_EFFECT.** The expectation is 0 to 2 Water wins of 72. No trial reached 2 of 36, and P had 0.
- **B-2:** not WORSE, most likely not significant (P: 20 / 16).
- **B-3: BETTER** (P: 29 / 5).
- **The overrun about 25 s later** (P: 342.5 against 315; reported, not barred).
- **Guards:** G-1, G-3 and G-5 are expected to hold. G-6 is the risk: `roster_numbers_1` failed its response guard on a scenario played with the changed unit. G-8 is expected to hold (P: 12 against 9).
- **Skai still wins at least 70 of 72.**

### 4.6 Reported, not judged

Medians over the Water slot-games before and after: trained units per type; army supply at 180, 300 and 450 s (own and Skai's); income per 30-s bucket to 300 s; worker losses before the overrun; the overrun second; game end tick. Also: Water wins before and after; Roc Riders trained by Skai; both response files side by side; the G-1 and G-2 pair tables with old and new margins.

## 5. Build packages, pins, tests

### 5.1 Order (one Sonnet session, then the sim-reviewer)

1. **RN2-B (before).** On a branch from `main` with nothing changed, run M-before, X-before, H-before and R-before. Commit the outputs and caches and push (every 24 games for the game runs).
2. **RN2-D (data).** Make section 2's three value changes and the text of 2.2. Run `--fights --only flat/1500/water.spoutling/skai.roc_rider` once as a smoke check that the data loads.
3. **RN2-A (after).** Run M-after, X-after, F-after, H-after and R-after, with the same commit-and-push rule. Write `pairs.md`. Judge B-1 to B-3, G-1 to G-6 and G-8 from the outputs **before touching any test**. If a guard fails: stop, revert the data change on the branch, report.
4. **RN2-P (pins).** Re-pin per 5.2 and run the suite in chunks (G-7).
5. Write the report `<dir>/README.md` (path header) with the commands, wall times, every bar row (D, better / worse / ties, p, verdict), the guards with evidence, 4.6, the re-pins (old to new), and the files. Give changelog bullets to the coordinator.

### 5.2 Pins expected to move (every one gets a comment "re-pinned for roster_numbers_2" with old and new values)

| Pin | Where | Current value |
| --- | --- | --- |
| smoke hash | `rts/tests/Sim.Tests/Harness/HarnessUnitTests.cs:178` | `df73e9584e791bc0` |
| economy_smoke hash | `HarnessUnitTests.cs:180`, `rts/tests/Sim.Tests/Economy/WorkerConstructionTests.cs:740` | `b724ff589cd6aa1b` |
| `aggressive_s5` and `aggressive_s3` traces | `rts/tests/Sim.Tests/Ai/FightSimTests.cs:115` and `:117` | end tick 9750, `91c318f5a0ad05cb`; end tick 9378, `e5cf4f79454bd01a` (the s3 assertion is reached only after the s5 one passes: re-run the test after re-pinning s5) |
| `V2ReferenceHash` | `rts/tests/Sim.Tests/Ai/SkirmishAiTests.cs:883` | `0xd4d353b73e0eae16` |
| `BlindTraceEndTick`, `BlindTraceHash` | `SkirmishAiTests.cs` near line 919 | 10355, `82998543e32dcfe0` |
| `S3SelfTraceHash` | `rts/tests/Sim.Tests/Ai/AiVarietyTests.cs:35` | `f441e8708886d80c` |
| ablation games (skai_vs_water 2, skai_vs_skai 3, water_vs_water 2) | `AiHarassTests.cs:213-215`, `AiPlacementTests.cs:601-603` | `0x7ff001b72095a475`, `0x53c785498c77ec59`, `0xa0b95b56e61cf63a` (the skai_vs_skai hash may also move, because the content digest is in the hash) |
| predictor advantages | `AiArmyMixTests.cs` `C2_1`, `C2_2`, `C2_5` | re-pin entries that move because the Spoutling's numbers changed (its own entry, and any entry in a situation whose believed enemy or own pool holds a Spoutling); any `AssertMix` weight change: **stop** |
| `predict.jsonl` / `predict.md` | `rts/docs/reports/predict_fights/` (read by `FightSimTests.OldProfiles_PredictJsonlByteIdentical`) | regenerate with the test's own command; note it in that folder's README |
| distribution pins | `AiMechanicTests` `V2_Distribution`, `V2_WallDistribution` | re-pin only if they fail |
| tests that assert a changed value or play with it | wherever the suite reports, for example `StatusTests`, `WaterAntiAirTests`, `FightTests`, `EconomyDataTests` | update the expected value only where it reads Spoutling damage or health or Shellback build time, and list each one; a failure not explained by the three values or the digest: **stop** |
| documents | `rts/docs/HANDOFF_UNITY.md` (smoke and economy_smoke hashes), `tutorial_01` headless hash `6cf2bdc218cfbb9f` | coordinator updates them from the report |

Must not change: any file under `rts/src` or `rts/tools`; any other unit, tuning, map or AI file; `fights.json`, `heldout*.json`, `roster_numbers_1.json`; the committed sweep `reports/roster_numbers_1/sweep/fights.jsonl` (an input).

### 5.3 Acceptance tests (NUnit, `rts/tests/Sim.Tests`)

1. Data test (the file that holds unit-value checks, or a new `RosterNumbers2Tests`): the Spoutling's `MaxHealth` is `Fix64.FromInt(90)` and its weapon damage is `Fix64.FromInt(10)`. Its cost (100 common, 25 rare), supply 2, speed 3, range 5, cooldown 1.0, build 20 s, drench damage type and `applies_drench` are unchanged.
2. Same file: the Shellback's build time is 20 s (as the loader stores it: ticks or `Fix64` seconds, whichever `UnitDefinition` holds). Its health 220, cost 175, supply 3, speed 1.8, damage 10 and range 2 are unchanged.
3. `FightTests`: `--fights --only flat/1500/water.spoutling/skai.roc_rider` exits 0. Pin `result` and `left0` to three decimals from the builder's F-after run.
4. `FightTests`: `--fights --only flat/1500/skai.windrunner/water.grasper` gives the same line as `reports/roster_numbers_1/sweep/fights.jsonl` with `seed` and `state_hash` removed (a fight without a Spoutling side is untouched).
5. The re-pins of 5.2, each with its comment.

## 6. Out of scope

Any Skai number; any Water number beyond the three; the dry-speed multiplier (Q-2); the Pummeler's kited weakness; the AI (supply planning, attack choice, a plan against massed Roc Riders: Q-5); maps; new units (section 7).

## 7. Is a new unit needed?

Not proven, so not designed. The one hole a number could not fully close in the trials is a **mobile dry-ground answer to the Roc Rider**. Even with double damage and 90 health the Spoutling still loses to it at equal cost (S1: -0.10). The units that win are the Leviathan, which is late tech, and the Water mix once a Spoutling grounds the Roc. The developer's rule that Water units hit fliers only on water (P-005 decision 3) may mean this gap is intended: Water should fight Rocs over flood water. If the developer wants Water to hold dry ground against massed Rocs before Leviathan tech, the first step is the Spoutling's numbers (a larger damage step is a later revision with a new seed). A new unit is needed only if the Spoutling cannot do this without becoming a general ground fighter.

## Questions for the developer (builders use the defaults)

| # | Question | Default |
| --- | --- | --- |
| Q-1 | The expected verdict is NO_MEASURABLE_EFFECT: the trials show AI wins barely respond to Water numbers. Merge the change on that verdict if every guard holds? | Yes: it closes two fight-level holes and the guards keep straight fights inside the ceiling. |
| Q-2 | `water_unit_dry_speed_multiplier` 0.85 is Water's "weak on dry land" identity from the design doc. Raise it (T4 used 1.0)? | No; out of this spec. |
| Q-3 | The Shellback at 20 s gives 8.75 value per producer-second, the highest in the game. Accept, with G-8 as the guard against Shellback-only armies? | Yes. |
| Q-4 | `matchups.json` says the Stormcaller beats the Spoutling. After the change it no longer does. Change the Stormcaller's `wins` entry, or leave it so `matchups_agree` reports it? | Leave it, with a note in the comment (2.2); the check is not a bar. |
| Q-5 | The deciding collapse is at 300 to 450 s (Skai army 63 to 66 against 17 to 24 in every trial) and looks like AI behaviour: attacking into kiting, the supply cap at 210 to 300 s, no plan against massed Rocs. Should the next step be an AI spec on that window rather than more numbers? | Yes, after this spec's report. |
| Q-6 | Accept that the win bar can detect only about 8 extra wins in 72 (power 0.8)? | Yes. |
