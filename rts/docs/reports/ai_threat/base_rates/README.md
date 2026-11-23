<!-- rts/docs/reports/ai_threat/base_rates/README.md -->
<!-- Base rates of the package S behaviour metrics for the baseline aggressive_s5 alone on prototype_1v1 (the SM-1 configuration with no candidate), measured at commit 4806bf8 (S-MAP and S-M built, no rule TS or TH). It judges nothing and tunes nothing: it only says how many pairs could be decisive for bars TS-B1 and TH-B1 of rts/docs/specs/ai_threat_map.md section 10.2. Part of rts/docs/reports/ai_threat. -->
# Package S base rates (baseline `aggressive_s5` only)

Measured 23 Nov 2026 on commit `4806bf8` (branch `review/ai-threat-build`). No candidate, no bar judged, nothing tuned.

## Command and time

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a aggressive_s5 --profile-b aggressive_s5 --maps prototype_1v1 --games 48 --jobs 4 --out rts/build/harness/base_rates
```

(`--no-build` was added after a Release build of the same commit.) Base seed and matchups of `ai_eval.json` (seed 20261117; `skai_vs_skai`, `skai_vs_water`, `water_vs_water`), 48 games per cell, so 144 games and 72 pairs, 288 slot-games; game cap 2400 s, summary window 600 s. Wall clock **3142 s (52 min 22 s)**, started 2026-11-23 18:51:24 UTC, alone on 4 cores. Determinism spot check: 2 games re-run, 0 mismatches. Exit code 2 comes from the older check `hq_move_new_income` (138 of 288 slot-games violated; information, not a package S bar); every new check holds by construction. The full `behaviour.md` is in this folder.

**Reading rule for this run.** The harness accepted the same profile for both sides. Then the second game of every pair is the same game as the first with the two sides swapped (72 of 72 pairs have the same `state_hash` and end tick in both games), so the pair sums of the two sides are equal in every pair: all `BEHAVIOUR_PAIRED` lines read 72 ties, p 1.0000. That is expected and says nothing about decisiveness. The per-pair number below is **Y, the baseline's sum over its two slot-games of the pair**, which here equals the whole underlying game's total over both seats. In the real SM-1 run Y is `aggressive_s5`'s sum over its two slot-games, which is the same kind of number.

`overrun_second` **is written** in the `--behaviour` records (every one of the 288 summaries has the key; 210 of 288 slot-games reached the overrun, 73 percent), because `settings.NewObserver()` always passes the `ai_eval.json` metric settings. No change was needed.

## TS-B1 event: a drop-off lost young

Event: an own non-HQ drop-off killed by an enemy at most 120 s after it first existed, before the slot's `overrun_second`, up to 600 s (check `depot_losses_young`).

| Y per pair (young depots lost by `aggressive_s5` in the pair) | 0 | 1 | 2 | 3 |
| --- | --- | --- | --- | --- |
| pairs | 57 | 10 | 4 | 1 |

- **15 of 72 pairs (21 percent) had at least one such loss**; 21 depots in all over the 72 distinct games. By matchup: `skai_vs_skai` 8 of 24 pairs, `skai_vs_water` 4 of 24, `water_vs_water` 3 of 24.
- Context: about 5.0 drop-offs placed per slot-game; 2.0 per slot-game lost at any time (586 over 288 slot-games), so most losses are not young or are after the overrun.

## TH-B1 and TH-B2 metrics: harass squads, value killed, value lost

Counted from the controller's harass group up to 600 s. Y per pair, by matchup (min / p25 / median / p75 / max):

| matchup (pairs) | squads started | value killed | value lost | trade (killed - lost) |
| --- | --- | --- | --- | --- |
| `skai_vs_skai` (24) | 7 / 8 / 10 / 11 / 12 | 225 / 350 / 450 / 650 / 825 | 0 / 150 / 275 / 475 / 1150 | -500 / -50 / 150 / 300 / 625 (16 pairs above 0, 8 below) |
| `skai_vs_water` (24) | 3 / 4 / 4 / 5 / 7 | 100 / 150 / 250 / 500 / 1125 | 0 / 0 / 75 / 75 / 575 | 50 / 150 / 200 / 400 / 950 (24 above 0) |
| `water_vs_water` (24) | 0 / 0 / 0 / 0 / 1 | 0 | 0 | 0 in all 24 |
| all (72) | 0 / 0 / 4 / 8 / 12 | 0 / 0 / 250 / 500 / 1125 | 0 / 0 / 0 / 275 / 1150 | -500 / 0 / 100 / 250 / 950 (40 above 0, 8 below, 24 at 0) |

- Per slot-game: every Skai slot-game (144 of 144) started at least one squad (672 squads, 368 worker kills, value killed 43,130, value lost 22,630); Water started a squad in 12 of 144 slot-games (18 squads, 8 worker kills, 600 killed, 150 lost). Water-mirror pairs have no harass: 23 of 24 pairs have no squad.
- **Pairs with at least one squad: 49 of 72. Pairs whose trade is not 0: 48 of 72.**

## Largest possible number of decisive pairs (from the spec's power table, section 10.3)

A pair is decisive when the candidate's and the baseline's pair sums differ. Once a candidate changes play, the games diverge and any sum can change, so the bound that matters is how many pairs can show the primary metric moving **in the better direction**.

| bar | pairs that can be better | largest D | better pairs needed for BETTER (exact sign test, alpha 0.05) | share the candidate must win to be seen with power 0.80 (`mde_permille`) |
| --- | --- | --- | --- | --- |
| TS-B1 `depot_losses_young` (lower) | 15 (the pairs where the baseline lost a young depot; a candidate can only remove a loss there) | **15** | 12 of 15 (80 percent) | 862 |
| TH-B1 `harass_trade` (higher) | 48 to 49 (the pairs where Skai harassed; trade is 0 in all 23 or 24 other pairs, and Water-mirror play started 1 squad in 1 pair) | **48** (49 if the one Water-mirror pair with a squad can move) | 32 of 48 (667 permille) or 32 of 49 | 703 (701) |

- The table of section 10.3 has rows D 12 (10 needed, 905) and D 24 (18, 786); the TS-B1 row is between them and was computed with the same formula (`PairedStats.MdeSharePermille`, Z 28016, D_min 6). The TH-B1 row is the table's D 48 row.
- Reading: **TS-B1 can pass only if the rule removes a young depot loss in at least 12 of the 15 pairs where the baseline has one, and adds none elsewhere**; any pair where the candidate loses a young depot and the baseline does not is a worse pair and lowers the chance (D grows by one, W does not). With D 15 the bar needs a large and consistent effect; it is decidable in principle, but only just. **TH-B1 is decidable** with 48 pairs (about 2 in 3 pairs better), but only for Skai pairs; the Water mirror (24 pairs) is all ties.
- The guard TH-B2 `harass_squads` has the same 48 to 49 pairs. The guards `first_drop_off` and `income_by` are not new and not covered here.
