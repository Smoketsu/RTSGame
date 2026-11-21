<!-- rts/docs/specs/balance_measurement.md: build spec for P-006 package M (measurement in the balance harness: paired games, stated power with a third verdict, an opponent pool, behaviour checks, the standing balance report) and the first half of package C (the fight predictor checked against the measured --fights sweep). Harness and data only, plus one additive factory in rts/src/Ai for forced openings; no change under rts/src/Sim and none to AI play. Part of rts/docs/specs (index: README.md); read with proposals/P-006-ai-built-for-balancing.md. -->
# Spec: Balance measurement (P-006 packages M and C, first half)

## 0. Goal, sources and ground rules

**Goal.** P-006 section 1: two of four AI packages were measured for two hours each and left "no change" because 216 unpaired games cannot see an effect below about 7 points. This spec makes every comparison state what it could have seen, removes noise by pairing games, measures against more than one opponent, checks behaviour before win rate, prints the balance report the AI exists to feed, and measures how often `FightPredictor` names the right winner.

**Sources read.** `proposals/P-006-ai-built-for-balancing.md`; `tools/BalanceHarness/README.md`, `LadderRunner.cs`, `GameRunner.cs`, `GameRecord.cs`, `SeedDerivation.cs`, `HarnessStats.cs`, `ReportWriter.cs`, `FightRunner.cs`, `FightRecord.cs`, `FightReport.cs`, `GameTrace.cs`, `AiEvalSettings.cs`; `src/Ai/FightPredictor.cs`, `AiVariety.cs`, `SkirmishAiController.cs` (seeding), `AiProfile.cs`; `Sim/Core/DeterministicRandom.cs`; the Sim event classes; `specs/skirmish_ai_v4.md` package S; `reports/fights_2026-11-18/README.md`; `reports/ai_h/README.md`; `data/ai/aggressive_s3|s4r|s5.json`; `data/scripts/ai_eval/ai_eval.json`; `data/scripts/fights/fights.json`. Written against `main` at `25fe03e`.

**Ground rules.**
- No change under `rts/src/Sim`. No change to how any AI plays. The one change under `rts/src/Ai` is the additive factory of M.3.1, used only by the harness.
- Harness code may use `double` for formatting (as `HarnessStats` does), but every **verdict** in this spec (sign test, interval, power, check bars) is computed in integers (`System.Numerics.BigInteger` where needed), so it is exact and platform independent. Fractions from data files are read with the existing `RequireFix64` and converted exactly to integer parts per 10 000 (`(value * 10000).RoundToInt()`; exact for any value written with at most 4 decimals). Call that unit "pt" (parts per 10 000) below.
- Every number is a key in a data file under `rts/data/scripts/` (outside the content digest, like `ai_eval.json`), commented in the house format `// <what>. Unit: . Range: . Source: balance_measurement.md <section>; placeholder.` All values are **placeholders** unless the table says otherwise.
- Every new output (stdout, `.md`, `.jsonl`) depends only on data, options and records: never on `--jobs`, scheduling or the clock (elapsed time goes to stderr only, as today). Records are sorted with `BatchRunner.CompareGames` before use.
- Files and classes follow the documentation standard of `CLAUDE.md`. Settings files reject unknown keys and name the file and field on every error (as `AiEvalSettings` does).

---

## Package M: measurement first (harness only)

### M.1 Paired games

#### M.1.1 What the harness does today, and what the variety stream is keyed by

- Ladder game g of a cell: slot s plays profile A (baseline) when `(g / 2) % 2 == s`, else B (candidate); factions swap on odd g (`GameRunner.FactionInSlot`); seed `SeedDerivation.GameSeed(base, map, matchup, g)`. Games g and g+2 already have the same factions per slot and opposite profiles, but different seeds, so they are not a pair.
- Every random stream an AI game uses is keyed by **(match seed, player slot)**, never by profile: the simulation's generator (match seed); the controller stream `SkirmishAiController.SeedFor(seed, slot)`; the variety stream `AiVariety.SeedFor(seed, slot)` (SplitMix64 of the controller seed xor `VarietySalt`). The AI-H report confirms it: the self-ladder and the strength ladder repeated 109 of 216 games bit for bit.
- What a stream **yields** depends on the profile. `AiVariety.Create` draws, in this order: the opening (`NextInt(sum of the plan's opening weights)`, only if the plan has openings); one band step per unit with weight above 0 in the drawn opening's composition (only if `composition_jitter_fraction > 0`); one per unit with weight above 0 in the plan composition (same condition); the expansion offset (only if `expand_jitter_seconds > 0`). Later draws (`DrawAttackAdvantageOffset`, the controller's attack-supply jitter) happen when play reaches them.
- `NextInt` uses rejection sampling, so the number of raw draws per call can differ between bounds; for bounds up to 1600 the chance is below 4e-7 per call. This spec ignores it and says so in the report header.

#### M.1.2 The paired layout (`--ladder --paired`)

Game g of a cell, in paired mode:

| Quantity | Rule |
| --- | --- |
| pair index | `p = g / 2` |
| candidate slot | `c = g % 2` (even g: candidate in slot 0) |
| profile of slot s | B when `s == c`, else A |
| faction of slot s | `GameRunner.FactionInSlot(matchup, p, s)` (factions swap on odd **pair**) |
| seed | `SeedDerivation.GameSeed(base, map, matchup, p)` (both games of a pair share it) |

So a pair holds the map, the faction, spawn and every random stream of each slot, and swaps only which profile drives which slot. `--games` must still be a multiple of 4 (an even number of pairs, so each faction order appears equally). Unpaired mode is unchanged byte for byte.

#### M.1.3 What "the same draw" means (pairing kind)

Per faction F, the harness computes the **pairing kind** of (A, B) before the run:

- **full** when, for F, both plans have the same openings (same IDs, weights and file order), every opening and the plan composition have the same set of unit IDs with weight above 0 in both profiles, and the profiles have equal `composition_jitter_fraction`, `expand_jitter_seconds` and `attack_advantage_jitter`. Then each slot draws the same opening, the same band steps and the same expansion offset in both games of a pair (the weights the steps apply to may differ: that is part of the profile under test).
- **seed** otherwise (for example `aggressive_s3`, which has no openings and no jitter, against `aggressive_s5`; or a forced-opening member of M.3). Only the streams are shared; each profile maps them through its own library.

Both kinds give an unbiased paired comparison, because the library is part of what is compared; `full` removes more noise. The kind per faction is printed in the report header and on the last stdout line. No attempt is made to force the baseline's opening onto the candidate: that would need an AI change and would compare something other than the profiles.

The report also counts **openings held**: complete pairs whose `opening0` is equal in both games and whose `opening1` is equal in both games. Under `full` this must equal the number of complete pairs (test P-2).

#### M.1.4 Records

Paired runs append to every game record, after the last existing key: `"pair":<p>,"candidate_slot":<0|1>`. Unpaired lines stay byte-identical (no new key). `GameRecord.Parse` reads the keys when present (`Pair` -1 and `CandidateSlot` -1 when absent). Everything that derives the candidate slot from the game index (`LadderTally.CandidateSlot`, `MirrorCross`, the per-seat table, the spot check, worker argument lists, `--trace --profile-a --profile-b --game N`) uses `record.CandidateSlot` when `record.Pair >= 0`, and `--paired` is passed to workers and accepted by `--trace`.

#### M.1.5 The statistic

Pair outcomes, from the candidate's point of view (each game is "slot0" or "slot1" won, a draw, or undecided):

| Pair | Name | Meaning |
| --- | --- | --- |
| candidate won both | W ("candidate both") | discordant: the profile decided it, for the candidate |
| baseline won both | L ("baseline both") | discordant: the profile decided it, for the baseline |
| one each (the same slot won both games) | S ("concordant") | the seat, faction or draw decided it; no information about the profiles |
| any game drawn or undecided | incomplete | left out of the test, counted (Q-3) |

**Distinct pairs.** Pair signature = FNV-1a (`StateHasher`, fresh) over the play signature of the candidate-in-slot-0 game, then that of the other game. Complete pairs with equal pair signatures are the same experiment; W, L, S and n count **distinct** complete pairs (as skirmish_ai_v4.md S counts distinct games). The plain counts over all complete pairs are printed next to them. n = W + L + S; D = W + L; discordant share r = D / n.

**Sign test.** Under "no difference" W is binomial(D, 1/2). Two-sided exact p = min(1, 2 x sum over k = 0..min(W, L) of C(D, k) / 2^D), computed with `BigInteger`; D = 0 gives p = 1. Significant when `2 x sum x 10000 <= alpha_pt x 2^D` (integers; `alpha_pt` = `effect_alpha` in pt). p is printed rounded half up to 4 decimals.

**Interval on q = W / D** (the candidate's share of discordant pairs): exact Clopper-Pearson on a grid of 1/1000, rounded outward, by bisection on j in 0..1000:
- `qL` = the largest j/1000 with `2 x 10000 x sum over k = W..D of C(D,k) j^k (1000-j)^(D-k) <= alpha_pt x 1000^D` (0 when W = 0);
- `qH` = the smallest j/1000 with the same inequality for the sum over k = 0..W (1 when W = D).

**Candidate's advantage**, in points of game win rate (the candidate's game win fraction is 1/2 + (W - L)/(2n) when draws are excluded): `adv = 1000 x (W - L) / (2n)` tenths of a point, rounded half away from zero; interval `[floor((2 jL - 1000) x D / (2n)), ceil((2 jH - 1000) x D / (2n))]` tenths of a point (jL, jH the grid numerators). Printed with a sign and one decimal (`+14.5`, `[+3.0, +17.7]`).

**Rows.** `ladder.md` gains `## Pairs` with one row per cell, then `mirror` (cells with equal factions), `cross` (the rest) and `all`. Columns: pairs, complete, distinct complete (n), concordant (S), candidate both (W), baseline both (L), discordant share r (3 decimals), openings held, p, advantage with interval, MDE (M.2). In a cross pair the candidate plays both factions, so a faction that wins every cross game makes every cross pair concordant: the cross row then shows D = 0 and adds nothing to `all`, which is the honest reading (today Skai has won 36 of 36 cross games as either profile). The verdict uses the `all` row only; the other rows carry no verdict (they print p for reading only).

#### M.1.6 Output and exit codes

The existing tables, warnings and the opening section stay (they read game records). The last stdout line in paired mode:

```
PAIRED candidate=<id> baseline=<id> pairs=<n> complete=<n> distinct=<n> concordant=<S> candidate_both=<W> baseline_both=<L> p=<0.0000> advantage=<+x.x> interval=[<+x.x>,<+x.x>] mde=<x.x> pairing=<faction>:<full|seed>,... verdict=<PASS|FAIL|NO_MEASURABLE_EFFECT|INCOMPLETE>
```

Exit codes: 0 PASS, 2 FAIL or INCOMPLETE, 3 determinism spot check failed, 4 NO_MEASURABLE_EFFECT, 1 error. INCOMPLETE when complete pairs (not distinct) are fewer than `paired_min_complete_fraction` of all pairs. `--non-regression` has no meaning in paired mode (error if both are given; the pool gate in M.3 is the non-regression test).

### M.2 Stated power and the third verdict

**Verdicts** (paired `all` row; the unpaired form below uses the same rule):
- **PASS**: significant (M.1.5) and W > L.
- **FAIL**: significant and W < L.
- **NO_MEASURABLE_EFFECT**: not significant. The line and the report state the MDE: effects smaller than it could not have been seen. P-006 decision 3: such a package is switched off.

**Minimum detectable effect (MDE)**, in points of game win rate, at two-sided confidence `effect_alpha` and power from `effect_z_power`, normal approximation. Let `Z = round(10000 x (effect_z_alpha + effect_z_power))` (an integer; 28016 for 1.96 + 0.8416).

- Unpaired, n independent games with a winner: MDE = (z_a + z_b) / (2 sqrt(n)). Printed as the smallest integer m (tenths of a point) with `400 x n x m^2 >= Z^2`.
- Paired, n distinct complete pairs, D discordant: MDE = (z_a + z_b) x sqrt(D') / (2n), with D' = max(D, D_min), where D_min is the smallest D for which an all-won sign test can be significant (smallest D with `2 x 10000 <= alpha_pt x 2^D`; 6 at alpha 0.05). Printed as the smallest m with `400 x n^2 x m^2 >= Z^2 x D'`.
- Planning: pairs needed for a target MDE of m tenths at discordant share r: n >= Z^2 x r / (400 m^2); games = 2n. Pairing beats unpaired games by the factor sqrt(2r): it helps only when r < 1/2, that is when seat, faction and draws decide many games.

**Unpaired runs** (`--ladder` without `--paired`) gain one line just before the `LADDER` line, information only (the exit code stays bar-based so existing scripts keep working):

```
EFFECT candidate=<id> baseline=<id> games=<n> wins=<w> p=<0.0000> advantage=<+x.x> interval=[<+x.x>,<+x.x>] mde=<x.x> verdict=<PASS|FAIL|NO_MEASURABLE_EFFECT>
```

where n = distinct play signatures among games with a winner, w = distinct candidate-winning signatures, p and the interval are the M.1.5 formulas with D = n and W = w, and advantage = `1000 x (w - n/2) / n` tenths, interval `[jL - 500, jH - 500]` tenths (rounded outward as above). `ladder.md` repeats the line under the LADDER line.

**Worked numbers for 216 games** (alpha 0.05 two-sided, power 0.80, Z = 28016; values from the integer formulas):

| Design | n | Assumption | MDE (power 0.80) |
| --- | --- | --- | --- |
| unpaired, all cells | 216 games | all distinct | 9.6 points (detection at power 0.5: 6.7, the "about 7" of P-006) |
| unpaired, mirror cells only | 144 games | cross games decided by faction | 11.7 |
| paired, all cells | 108 pairs | r = 1.0 | 13.5 (worse than unpaired) |
| paired, all cells | 108 pairs | r = 0.5 (games in a pair independent) | 9.6 (same as unpaired) |
| paired, all cells | 108 pairs | r = 0.25 (D = 27) | 6.8 |
| paired, all cells | 108 pairs | r = 0.10 (D = 11) | 4.4 |
| paired, all cells | 108 pairs | D = 0 (D' = 6) | 3.2 |
| paired, mirror pairs only | 72 pairs | r = 0.5 / 0.25 / 0.10 (D = 36 / 18 / 7) | 11.7 / 8.3 / 5.2 |

Cross-check with P-006: unpaired 5.0 points needs n >= 785 games (`400 x 785 x 50^2 >= 28016^2`). Smallest W for significance (two-sided 0.05): 10 of D = 11, 20 of 27, 35 of 54, 65 of 108. r is unknown until the first paired run (M.8); the planning row is filled from it.

### M.3 Opponent pool

#### M.3.1 Forced openings (the one `rts/src/Ai` addition)

`AiProfile` has a private constructor and `Rts.Ai` grants internals only to the test assembly, so the harness cannot build a profile variant. Add:

```
public AiProfile WithForcedOpenings(IReadOnlyDictionary<string, string> openingByFaction)
```

It returns a copy (same root, id and every scalar) in which each named faction's plan has `Openings` = the one named opening with weight 1; other factions unchanged. It throws `ArgumentException` naming the faction and opening when either is missing. The draw stays one `NextInt(1)` call (exactly one raw draw), so later variety draws stay aligned with the unforced profile's whenever the unforced call also took one raw draw. No AI file changes otherwise; `V2ReferenceHash` and every pinned AI hash stay green. Review: `sim-reviewer` (Opus) reads this one method.

#### M.3.2 Data file `rts/data/scripts/ai_eval/pool.json`

Root keys: `$schema_version` 1, `$doc`, `pool_maps` (list; empty = `ladder_maps`), `pool_matchups`, `stage1_pairs_per_cell`, `stage2_pairs_per_cell`, `better_bonferroni`, `members`. A member: `id` (1 to 48 lower-case letters, digits, underscores; unique), `profile` (a profile ID), `force_opening` (optional object faction ID to opening ID, validated through `WithForcedOpenings`), `stage` (1 or 2). Default members:

| id | profile | force_opening | stage |
| --- | --- | --- | --- |
| `s3` | `aggressive_s3` | none | 1 |
| `s4r` | `aggressive_s4r` | none | 1 |
| `s5` | `aggressive_s5` | none | 1 |
| `s5_standard` | `aggressive_s5` | skai `skai_standard`, water `water_standard` | 2 |
| `s5_pressure` | `aggressive_s5` | skai `skai_windrunner_pressure`, water `water_grasper_pressure` | 2 |
| `s5_fast_expand` | `aggressive_s5` | skai `skai_fast_expand`, water `water_fast_expand` | 2 |
| `s5_tech` | `aggressive_s5` | skai `skai_air`, water `water_leviathan_tech` | 2 |

Each opening is forced once; one member forces one opening per faction (pairing in file order of the themes), so four members cover eight openings in every cell instead of eight members in two cells each.

#### M.3.3 Command

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --pool --profile-b <candidate> [--stage 1|2|all] [--pairs N] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>]
```

- For each member of the chosen stage (default 1), in file order, a paired ladder (M.1) with the member as baseline, on `pool_maps` x `pool_matchups`, `stageX_pairs_per_cell` pairs per cell (`--pairs` overrides; must be even). A member whose `profile` equals the candidate and has no `force_opening` is skipped and listed as `skipped (candidate)`.
- All chunks of all members go to one `WorkerPool.RunAll` call so the jobs stay busy. Workers get `--pool-member <id>` and resolve it from `pool.json`. `GameRunner.Play` gains an overload taking two `AiProfile` objects; the string overload delegates to it. Records carry `"member":"<id>"` after `candidate_slot`.
- Determinism spot check: the first `ladder_verify_games` games of the first member, re-run in-process.
- Writes `pool.jsonl` and `pool.md` (one `## Pairs` table per member, as M.1.5, then the gate table) to `--out` (default `rts/build/harness/pool`).
- stdout: one line per member `MEMBER <id> <PAIRED-line fields from complete= on>`, then the last line `POOL candidate=<id> stage=<s> members=<n> skipped=<n> better=<n> worse=<n> no_effect=<n> incomplete=<n> games=<n> verdict=<PASS|FAIL|NO_MEASURABLE_EFFECT|INCOMPLETE>`.

#### M.3.4 The gate

Per member, from its `all` row: **worse** when significant at `effect_alpha` with W < L; **better** when W > L and significant at `effect_alpha / m` (Bonferroni over the m members run) if `better_bonferroni`, else at `effect_alpha`. Pool verdict: FAIL if any member is worse; else INCOMPLETE if any member is INCOMPLETE; else PASS if at least one is better; else NO_MEASURABLE_EFFECT. Exit codes as M.1.6. The MDE per member is printed at `effect_alpha` (no z for the corrected level is kept); `pool.md` says that "better" needs a larger effect than the printed MDE when the correction is on.

#### M.3.5 Compute cost

Games = members x cells x pairs per cell x 2. At the stated rate of 216 games in 47 minutes at 4 jobs (4.6 games a minute; the AI-H ladder ran 216 in 65 minutes, 3.3 a minute):

| Run | Members x cells x pairs | Games | Time at 4.6 / 3.3 games a minute |
| --- | --- | --- | --- |
| stage 1 (default keys: mirror cells only, 46 pairs) | 3 x 2 x 46 | 552 | 120 / 167 min |
| stage 2 (24 pairs) | 4 x 2 x 24 | 384 | 84 / 116 min |
| all seven members at 72 pairs in all three cells (the naive pool) | 7 x 3 x 72 | 3024 | 657 min |

To stay near two hours: run stage 1 on every candidate and stage 2 once per package before it is marked done (Q-5); play mirror cells only while the cross cell is decided by faction (Q-4; it costs a third of the games and today every cross pair is concordant); size pairs from r measured in M.8. At 92 pairs per member the stage-1 MDE is 10.4 points at r = 0.5 and 7.4 at r = 0.25.

### M.4 Behaviour checks

#### M.4.1 General form

A behaviour check is a **named predicate over one slot's game summary** (and its game record), evaluated for every slot-game of a profile over N games and reported as counts. Checks are registered by ID in one harness file, `BehaviourChecks.cs` (the only composition root for checks, as `BuiltInMechanics` is for mechanics), and declared with their bar values in `rts/data/scripts/ai_eval/behaviour_checks.json`. A later package adds a check by adding one registry entry and one data entry.

```
interface IBehaviourCheck {
    string Id { get; }
    void ReadSettings(JsonValue entry);                      // validates its own keys, names file and field on error
    CheckOutcome Evaluate(GameSummary slot, GameRecord game); // Held | Violated | Unjudged, plus an integer metric (num, den)
}
```

A check passes for a profile when `violated x 10000 <= max_violation_fraction_pt x judged` (judged = held + violated).

#### M.4.2 Game summary (`SummaryObserver`, read-only)

A read-only observer passed to `GameRunner.Play` (beside the existing optional `GameTrace`; it reads events and state after each step and never submits or steps, so the final state hash and play signature are unchanged, test B-1). Per slot it collects:

| Field | Content | Unit |
| --- | --- | --- |
| `spent_common`, `spent_rare` | net spending: cost of every accepted `train_unit` and `build_structure` command minus `BuildOrderCancelledEvent` refunds (the accounting of `GameTrace.PlayerTrace`, moved into a shared `PlayerLedger` class used by both) | resource units |
| `spent` | the same split by class of the definition: `hq` (Hq set), `worker` (CanGather, no footprint), `army` (other mobile), `static_defence` (IsStaticDefence), `wall` (IsWall or IsGate), `drop_off` (IsDropOff, no Hq), `structure` (other footprint); each `[common, rare]` | resource units |
| `bank` | `[Common, Rare]` of the player after the step at each `summary_bank_times_seconds`; `null` when the game ended earlier | resource units |
| `income` | `[common, rare]` deposited (`ResourceDepositedEvent`) per bucket of `summary_bucket_seconds`, bucket k covering ticks [k B, (k+1) B) | resource units per bucket |
| `hq_moves` | per HQ move `[uproot_tick, root_tick, distance_tenths]`: uproot = `HqStateChangedEvent` Rooted to Uprooting; root = the next Rooting to Rooted of that unit (-1 if it never came); distance between the HQ positions at those ticks in tenths of a tile (`(length x 10).RoundToInt()`, 0 when root is -1) | ticks, tenths of a tile |
| `trained` | definition ID to count of `UnitTrainedEvent` | units |
| `completed` | definition ID to count of `StructureCompletedEvent` | structures |
| `mechanics` | mechanic ID to count (M.5.3) | uses |

Records of `--behaviour` and `--balance` runs append `"summary0":{...},"summary1":{...}` (keys in the order above, maps in ordinal key order). Other modes do not run the observer.

#### M.4.3 Worked example 1: `static_defence_share`

Metric = `(spent.static_defence[0] + w x spent.static_defence[1]) / (spent_common + w x spent_rare)` with w = `rare_weight`; 0 when the denominator is 0. With the metric as numerator / denominator, held when `numerator x 10000 <= max_share_pt x denominator` (integers); never Unjudged. This is P-006's "tower spending stays under its budget share"; the bar is a placeholder until package B sets the defence band.

#### M.4.4 Worked example 2: `hq_move_new_income`

For each move with `root_tick >= 0` and `distance_tenths >= 10 x min_distance_tiles`: let u = bucket of `uproot_tick`, t = bucket of `root_tick`, K = `window_buckets`, G = `settle_buckets`. Before = sum of `common + w x rare` over buckets u-K .. u-1; after = the same over t+G .. t+G+K-1. The move is **unjudged** when u < K or the after window runs past the last bucket; **violating** when `after x 10000 < before x (10000 + min_gain_pt)`. Per slot-game: Violated if any judged move violates; Unjudged if it has moves and none is judged; Held otherwise (including no moves). Metric = violating moves. The report line adds `moves=<n> violating_moves=<n> unjudged_moves=<n>`. This is P-006's "no HQ move without new income" (the AI-HQ review rule).

#### M.4.5 Command

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --behaviour --profile-a <baseline> --profile-b <candidate> [--checks id,id] [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>]
```

Plays the paired layout of M.1 (`games_per_cell` from `behaviour_checks.json`, multiple of 4) with the summary observer, evaluates every enabled check (or those of `--checks`) for both profiles, writes `behaviour.jsonl` and `behaviour.md` (default `rts/build/harness/behaviour`). stdout, one line per check and profile (candidate first), then the summary:

```
BEHAVIOUR <check> profile=<id> games=<slot-games> held=<n> violated=<n> unjudged=<n> metric_median=<0.000> metric_max=<0.000> bar=<0.000> PASS|FAIL [extra fields]
BEHAVIOUR_SUMMARY candidate=<id> passed=<n> of=<m>
```

Median is the lower median (`HarnessStats.Percentile` 50) of the per-slot-game metric over judged slot-games, printed as num/den to 3 decimals. Exit 0 every candidate check passes, 2 any fails, 3 spot check, 1 error. Baseline lines are information. P-006 M item 4 ("a ladder is run only when the behaviour test passes") is a process rule for the coordinator, not enforced in code.

### M.5 The balance report (`--balance`)

#### M.5.1 Command and games

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --balance [--profile <id>] [--games N] [--maps a,b] [--matchups x,y] [--jobs J] [--seed S] [--max-game-seconds T] [--out <dir>] [--from <balance.jsonl>] [--fights-from <fights.jsonl>] [--strict]
```

Self-play of one profile (default `profile` of `balance_report.json`) in both slots, the batch seat rule (factions swap on odd g), seeds from `base_seed`, with the summary observer. `--from` re-renders the report from a written `balance.jsonl` without playing (byte-identical output, test R-6). Check 2 needs the predictor evaluation of package C: the command runs it (the `--fights` sweep, about 22 s at 4 jobs) unless `--fights-from` names a `fights.jsonl`; before package C is built check 2 reads `NOT_MEASURED`. Writes `balance.jsonl` and `balance.md` (default `rts/build/harness/balance`). stdout: one `CHECK balance_<name> PASS|FAIL|NOT_MEASURED <detail>` per check of M.5.5, then `BALANCE profile=<id> games=<n> decided=<n> distinct=<n> checks_passed=<k> of=4`. Exit 0 (failed checks included), 2 a check failed and `--strict` was given, 3 spot check, 1 error.

#### M.5.2 Sections of `balance.md`, in order

1. Settings bullets: profile, base seed, games per cell, cap, content digest, distinct games, undecided and draws.
2. **Faction matrix**: rows and columns are factions in ordinal order; cell (F, G), F != G: F's wins / decided games between F and G, Wilson interval over distinct games (`confidence_z`, as S.2). Diagonal: slot-0 win share in the F mirror, labelled "seat".
3. **Opening matrix**, one table per faction pair (mirror pairs too): rows the openings of the row faction, columns those of the column faction; cell: wins of the row opening / decided games between them, distinct games, Wilson interval; in mirror tables the diagonal (same opening both sides) shows games only. Below it the existing opening rows (`LadderRunner.OpeningRows`).
4. **Unit use**: for every definition of each faction that is trainable (in some `Economy.Trains` list of that faction) or buildable (footprint above 0, no Hq): share of slot-games of that faction with `trained` or `completed` at least 1, with counts. Ordinal ID order.
5. **Mechanic use** (M.5.3): share of applicable slot-games with a count of at least 1; the mechanics that cannot be measured are listed with the reason.
6. **Unspent resources**: per faction and per time of `summary_bank_times_seconds`: lower median of `bank` common and rare over slot-games still running (n printed).
7. **Trust checks** (M.5.5) with values and bars.

#### M.5.3 Mechanics: what the simulation reports today

Counted per slot by the summary observer (owner of a unit-keyed event = the unit's owner, kept from `UnitSpawnedEvent` and state; applicability = the faction has a definition with the capability):

| Mechanic ID | Detected from | Applicable when the faction has |
| --- | --- | --- |
| `downdraft` | `GroundingCastEvent` (Owner) | a definition with `GroundingAbility` |
| `wave` | `WaveCastEvent` (Owner) | `Flood.CanCastWave` |
| `drench` | `DrenchHitEvent` (Attacker's owner) | a weapon with `AppliesDrench` |
| `splash` | `BlastEvent` with Victims above 0 (AttackerOwner) | a weapon with `SplashRadiusTiles` above 0 |
| `shield_dome` | `ShieldDomeHitEvent` with Absorbed above 0 (dome's owner) | `ShieldDome` |
| `momentum` | `MomentumChangedEvent` with Stacks above PreviousStacks (unit's owner) | faction `MechanicId == MomentumMechanic.MechanicId` |
| `anti_air_on_water` | `ShotFiredEvent` whose attacker's weapon has `CanTargetAirOnWater` and not `CanTargetAir`, and whose target's `EffectiveLayer` is Air at that step (event plus a state read; no event says it) | a weapon with `CanTargetAirOnWater` |
| `flood_source` | `StructureCompletedEvent` of a definition with `Flood.IsSource` | such a definition |
| `wall`, `gate` | `StructureCompletedEvent`, `Economy.IsWall` without / with `IsGate` | such a definition |
| `static_defence` | `StructureCompletedEvent`, `Economy.IsStaticDefence` | such a definition |
| `expansion` | `StructureCompletedEvent`, `IsDropOff` without Hq | such a definition |
| `rare` | `ResourceDepositedEvent` with Resource Rare | always |
| `hq_move` | `HqStateChangedEvent` to Uprooting | an Hq definition |
| `hq_last_stand` | `HqLastStandEvent` | an Hq definition |
| `core_capture` | `CoreCaptureStartedEvent` (every player of the Team) | the map has a Core |
| `cycle_pick` | `CyclePickedEvent` with a player `Player` (not the script) | always |

Not measurable as "used" from events or state today: focus fire, kiting and retreats (AI micro; only its commands show it), regeneration and Windstorm range (passive or global, not a choice), tech choices as such (covered by the unit-use table, since each tech structure is a definition). The registry of mechanic detectors lives in `MechanicDetectors.cs`; adding one is one entry.

#### M.5.4 Data file `rts/data/scripts/ai_eval/balance_report.json`

Keys in M.6.

#### M.5.5 The four checks of P-006 section 6 (bars are placeholders for the developer)

1. `balance_use`: every unit of M.5.2 item 4 and every measurable applicable mechanic is used in at least `min_use_share` of its slot-games. FAIL lists the items below. Mechanics that cannot be measured are listed, do not block (Q-7).
2. `balance_predictor`: package C's accuracy over decided fights is at least `predictor_min_accuracy`. NOT_MEASURED without package C.
3. `balance_unspent`: at every time of `summary_bank_times_seconds` and for every faction, the median unspent common is at most `max_median_unspent_common` and the rare at most `max_median_unspent_rare`.
4. `balance_openings`: no opening with at least `opening_min_games` distinct mirror games against other openings has a distinct-game win fraction below `opening_mirror_min` or above `opening_mirror_max` ("wins or loses nearly every mirror game"; wider than the existing 0.35 to 0.65 warning band, which stays).

### M.6 Settings (all new values; every one a placeholder unless noted)

| File (under `rts/data/scripts/`) | Key | Unit | Range | Value |
| --- | --- | --- | --- | --- |
| `ai_eval/ai_eval.json` | `effect_alpha` | fraction (two-sided) | 0.0001 to 0.5 | 0.05 (the usual level; placeholder) |
| same | `effect_z_alpha` | z-score; must match `effect_alpha` two-sided | 0 to 6 | 1.96 |
| same | `effect_z_power` | z-score of the power (0.8416 = 80 percent) | 0 to 6 | 0.8416 |
| same | `paired_min_complete_fraction` | fraction of pairs | 0 to 1 | 0.9 |
| same | `summary_bucket_seconds` | seconds | 5 to 600 | 30 |
| same | `summary_bank_times_seconds` | list of seconds, ascending | 1 to 8 entries, 1 to 7200 | [300, 600, 900] |
| `ai_eval/pool.json` | `pool_maps` | list of map IDs | 0 to 32 (empty = `ladder_maps`) | [] |
| same | `pool_matchups` | list of matchup IDs | 1 to 16 | ["skai_vs_skai", "water_vs_water"] (Q-4) |
| same | `stage1_pairs_per_cell` | pairs, even | 2 to 1000 | 46 |
| same | `stage2_pairs_per_cell` | pairs, even | 2 to 1000 | 24 |
| same | `better_bonferroni` | boolean | true or false | true (Q-6) |
| same | `members` | list (M.3.2) | 1 to 32 | the M.3.2 table |
| `ai_eval/behaviour_checks.json` | `games_per_cell` | games, multiple of 4 | 4 to 1000 | 24 (72 games, about 16 min) |
| same | `rare_weight` | ratio, rare against common | 0 to 20 | 1 (as `counter_rare_cost_weight`) |
| same, check `static_defence_share` | `enabled`, `max_share`, `max_violation_fraction` | boolean, fraction, fraction | -, 0 to 1, 0 to 1 | true, 0.15, 0.05 |
| same, check `hq_move_new_income` | `enabled`, `min_distance_tiles`, `window_buckets`, `settle_buckets`, `min_gain_fraction`, `max_violation_fraction` | boolean, tiles, buckets, buckets, fraction, fraction | -, 0 to 200, 1 to 20, 0 to 20, 0 to 10, 0 to 1 | true, 4, 2, 1, 0.10, 0 |
| `ai_eval/balance_report.json` | `profile` | profile ID | a file under `rts/data/ai` | `aggressive_s5` |
| same | `base_seed` | seed | 0 to 2147483647 | 20261121 |
| same | `games_per_cell` | games, multiple of 2 | 2 to 10000 | 72 |
| same | `maps`, `matchups` | lists | as `ladder_maps`, `ladder_matchups` | ["prototype_1v1"], the three ladder matchups |
| same | `max_game_seconds` | seconds | 60 to 7200 | 2400 |
| same | `min_use_share` | fraction of slot-games | 0 to 1 | 0.10 |
| same | `predictor_min_accuracy` | fraction of decided fights | 0 to 1 | 0.85 |
| same | `max_median_unspent_common` | resource units | 0 to 100000 | 400 |
| same | `max_median_unspent_rare` | resource units | 0 to 100000 | 150 |
| same | `opening_mirror_min`, `opening_mirror_max` | fraction | 0 to 1 | 0.10, 0.90 |
| same | `opening_min_games` | distinct games | 1 to 1000 | 10 |

### M.7 Acceptance tests (`rts/tests/Sim.Tests/Harness/MeasurementTests.cs` unless stated; real games use `prototype_1v1` and a 120 s cap)

- **P-1 Paired layout.** For `skai_vs_water`, games 0 to 7 in paired mode: pair g/2, candidate slot g%2, slot-0 faction skai for pairs 0 and 2 and water for 1 and 3, both games of a pair have `GameSeed(base, map, matchup, p)`.
- **P-2 Full pairing holds the draws.** Kind of (`aggressive_s5`, `abl_s5_no_h`) is `full` for both factions; for 8 seeds and both slots, `AiVariety.Create` with either profile gives the same `OpeningId`, `ExpandOffsetSeconds` and jitter steps (compare `JitterComposition` output on equal weights); in a played paired game pair `opening0` and `opening1` are equal across the two games, and openings held = complete pairs.
- **P-3 Pairing kind.** (`aggressive_s5`, `aggressive_s3`) is `seed` for both factions; (`aggressive_s5`, `s5_standard` member) is `seed` for both.
- **P-4 Self-pairing.** `--ladder --paired --profile-a aggressive_s5 --profile-b aggressive_s5 --games 8 --matchups water_vs_water`: within each pair the two games have equal state hashes; every complete pair is concordant; W = L = 0, p = 1.0000, verdict NO_MEASURABLE_EFFECT, exit 4.
- **P-5 Sign test.** Synthetic pairs with W 10, L 1, S 20: p 0.0117 (exactly 3/256), PASS; W 1, L 10: FAIL; W 9, L 2: p 0.0654 (67/1024), NO_MEASURABLE_EFFECT; W 0, L 0: p 1.0000.
- **P-6 Interval and advantage.** W 10, D 11, n 31: jL 587, jH 998; advantage +14.5, interval [+3.0, +17.7]. W 20, D 27: jL 537, jH 889.
- **P-7 Incomplete and distinct.** A pair with one draw and a pair with one undecided game are incomplete and out of the test; two complete pairs with equal pair signatures count once in n; complete below `paired_min_complete_fraction` gives INCOMPLETE, exit 2.
- **P-8 MDE.** Unpaired n 216: 9.6; n 144: 11.7; paired n 108 with D 54, 27, 11, 0: 9.6, 6.8, 4.4, 3.2; D_min at alpha 0.05 is 6.
- **P-9 Unpaired unchanged.** A small unpaired ladder writes `ladder.jsonl` lines without `pair` keys, the `LADDER` line format and exit code unchanged, and an `EFFECT` line just before it.
- **P-10 Determinism.** A paired run of 8 games, `abl_s5_no_h` against `aggressive_s5`, with `--jobs 1` and with `--jobs 4`: identical `ladder.jsonl` and `ladder.md`; the spot check re-runs paired games with the paired seat rule (0 mismatches); `--trace --paired --game 3` reproduces that record's line.
- **O-1 Pool file.** Unknown key, duplicate member ID, odd pairs, unknown profile, and a `force_opening` naming a missing opening each fail with the file and field in the message.
- **O-2 Forced opening.** `WithForcedOpenings({skai: skai_air})`: for 20 seeds the skai `OpeningId` is `skai_air`; water draws equal the unforced profile's; the forced profile's later jitter steps equal the unforced profile's for seeds where the unforced draw was also `skai_air`.
- **O-3 Gate.** Synthetic member results (none worse, one better) PASS; (one better, one worse) FAIL; (all not significant) NO_MEASURABLE_EFFECT; with 3 members and Bonferroni a member at p 0.03 with W > L is not "better"; a member INCOMPLETE and none worse gives INCOMPLETE.
- **O-4 Skip and cost.** Candidate `aggressive_s5`: member `s5` skipped, forced members not; `games=` equals the number of records written.
- **B-1 Observer is read-only.** One game with and without the summary observer: equal state hash and play signature. `spent_common` and `spent_rare` equal the `GameTrace` totals of the same game.
- **B-2 Static defence share.** Synthetic summaries: spent 1000 common with 150 static defence holds at 0.15 (edge inclusive); 151 violates; spent 0 holds; rare counts with `rare_weight`.
- **B-3 HQ move.** Synthetic buckets with `min_gain_fraction` 0.10: before 600, after 700 holds (7 000 000 is not below 6 600 000); after 650 violates (6 500 000 is below 6 600 000); after 660 holds (edge); a move with u < K or with the after window past the end is unjudged; a move shorter than `min_distance_tiles` is ignored; root -1 is ignored.
- **B-4 Registry.** A data entry with an ID not in the registry fails naming the entry; a registered check missing from the data is not run and `behaviour.md` lists it as "not declared".
- **B-5 Command.** `--behaviour --games 8 --matchups water_vs_water` prints one line per check and profile in the stated format and `BEHAVIOUR_SUMMARY`; exit 0 or 2 matches the lines.
- **R-1 Matrices.** Synthetic records: faction and opening matrix cells, distinct counting and the Wilson bounds equal `HarnessStats.Wilson` on the distinct counts.
- **R-2 Use shares.** Synthetic summaries: unit and mechanic shares use only slot-games of the owning faction and only applicable mechanics.
- **R-3 Detectors.** One unit test per detector of M.5.3 on a synthetic event plus definitions (owner attribution included; `anti_air_on_water` needs the air target and the weapon flags).
- **R-4 Unspent.** Lower median; games that ended before a time are excluded and n shows it.
- **R-5 Checks.** Each of the four checks PASS and FAIL on synthetic input at the bar edge; check 2 NOT_MEASURED without fights data.
- **R-6 Re-render and determinism.** `--balance --games 4` then `--from` its `balance.jsonl`: byte-identical `balance.md`; `--jobs 1` and `--jobs 4` identical files.

### M.8 Tier, build order and first measurement

- **Tier.** Sonnet `builder` for all of M (tool code and data). `sim-reviewer` (Opus) reviews `AiProfile.WithForcedOpenings` and the paired seat rule (records, spot check, seeds). `docs-writer` (Haiku) updates `tools/BalanceHarness/README.md` (new modes and options).
- **Order.** M1 paired layout, statistic, power and verdicts (M.1, M.2) -> M3 forced openings and pool (M.3) -> M4 summary observer and behaviour checks (M.4) -> M5 balance report (M.5; check 2 after package C). Each step is mergeable with its tests.
- **First measurement (after M1; report under `rts/docs/reports/`).** `--ladder --paired --profile-a abl_s5_no_h --profile-b aggressive_s5 --jobs 4` (216 games, P-006 section 5: AI-H's paired re-measurement): record r per cell, the verdict and the MDE, and fill the planning row of M.2. Then one `--balance` run of `aggressive_s5` as the first standing report.

---

## Package C, first half: the fight predictor against measured fights

### C.1 Scope and non-goals

Measure only. No change to `FightPredictor`, to the fight sweep, its records, `fights.md` or its checks. The fix or the simulation is the second half of C and needs its own spec after the developer applies the decision rule of C.7.

### C.2 Command

```
dotnet run --project rts/tools/BalanceHarness -c Release -- --data rts/data --predict-fights [--from <fights.jsonl>] [--profile <id>] [--jobs J] [--seed S] [--out <dir>]
```

Without `--from` it runs the `--fights` sweep exactly as `--fights` does (same plan, workers, spot check, `fights.jsonl` and `fights.md`), then evaluates; with `--from` it parses each line with `FightRecord.Parse` and evaluates against the current unit data (an unknown unit ID is an error naming the line; the header notes the records may come from other data). `--profile` (default `predictor_profile`) supplies `PredictorSettings.From(profile)`. Writes `predict.jsonl` and `predict.md` to `--out` (default `rts/build/harness/fights`). Last stdout line:

```
PREDICT fights=<n> judged=<n> decided=<n> correct=<n> accuracy=<0.000> clean=<correct>/<decided> no_winner_agree=<a>/<b> decision=<FIX_FORMULA|SIMULATE|DEVELOPER_CHOICE> (proposed thresholds)
```

Exit 0 (any decision), 3 spot check, 1 error.

### C.3 Prediction of one fight

- Side A = seat 0, side B = seat 1. Entries: one `FightEntry(definition, count, count x MaxHealth)` per unit of `units0` / `units1`, in ordinal ID order (the record's maps are already ordinal).
- `aUphill` = the case's kind is `high_ground` (seat 1 holds the plateau and seat 0 shoots up at it; `FightRunner`).
- `FightPredictor.Predict(a, b, aUphill, data.Content.Combat, settings)`.
- Predicted class: `none` when `TimeAKillsB == TimeBKillsA` (this includes both at `MaxSeconds`, neither side can kill the other); else `seat0` when `AWins`, else `seat1`.
- Predicted margin: `min(MaxAdvantage, max(tA, tB) / max(min(tA, tB), MinSeconds))` in Fix64, where tA, tB are the two returned times (both already in [MinSeconds, MaxSeconds]); 1 for class `none`.

### C.4 Scoring

- Fights with `mismatch` true are left out (as the checks do) and counted.
- **Decided** (result `seat0` or `seat1`): correct when the predicted class equals the result; a predicted `none` is wrong. Headline accuracy = correct / decided.
- **No winner** (`stalemate`, `unresolved`, `draw`): not in the headline. Agreement when the predicted class is `none`; reported as `no_winner_agree=<agreeing>/<no-winner fights>`. Their own table lists each with the predicted class, the reason, and the value-left leader (`left0` against `left1`, "even" when equal) against the predicted winner.
- judged = decided + no-winner fights.

### C.5 Breakdowns (tables in `predict.md`; every row: fights, decided, correct, accuracy, Wilson interval at `confidence_z`; rows with fewer than `min_fights_per_row` decided fights marked "small")

1. Overall, and by seat order agreement: pairs of fights that differ only in seat order where the predictor's classes are mirror images but the measured winners are not (seat splits the predictor cannot see).
2. By **unordered unit pair** (sides, 78 rows today, 10 fights each), sorted by accuracy ascending (compared as `correct_1 x decided_2` against `correct_2 x decided_1`), ties by pair ID ordinal; and by **side** (13 rows).
3. By **terrain case** (`flat`, `high_ground`, `flooded`, `choke`).
4. By **army value**, and by (case, value) (today `flat/750` is the only 750 row, so value and case are confounded; the table says so).
5. By **unmodelled mechanic** (the list in the header of `FightPredictor.cs`). Present, from definitions and the case only:

| Mechanic | Present when |
| --- | --- |
| `drench` (slow and grounding) | one side has a weapon with `AppliesDrench` and the other side a unit with `Layer` Air |
| `splash` | some unit's weapon has `SplashRadiusTiles` above 0 |
| `momentum` | some unit with speed above 0 whose faction's `MechanicId` is `MomentumMechanic.MechanicId` |
| `flooding` | case kind `flooded` |
| `windstorm_range` | never: fights run without cycles (`FightRunner` passes none) |
| `regeneration` | some unit has a `ShieldDome` (domes regenerate; no unit health regenerates in the data) |
| `hit_delay` | some unit's weapon has `HitDelayTicks` above 0 |
| `focus_fire` | never: fight control is attack-move with idle re-targeting |

Columns per mechanic: decided fights present / accuracy present, accuracy absent, and decided fights where it is the **only** mechanic present / their accuracy. A final row `clean`: decided fights with none present.

### C.6 Calibration table

Bins of the predicted margin from `margin_bin_edges` (Fix64 text, ascending, first 1): bin i covers [edge_i, edge_i+1), the last is open. Per bin over decided fights with predicted class not `none`: fights, predicted winner won, share with Wilson interval, and the lower median of the winner's value left. A predicted-`none` row comes first. Read: "when it predicts a win by a ratio in this bin, it wins this often". The bins near the AI's `attack_min_advantage` and `retreat_max_advantage` matter most; the header prints those two values of `--profile`.

### C.7 Decision rule (PROPOSED thresholds for the developer to accept or change)

Let A = headline accuracy, A_clean = accuracy of the `clean` row, A_M = accuracy present for each mechanic with at least `mechanic_min_fights` decided fights present.

1. **SIMULATE** when A_clean < `simulate_below_clean_accuracy` (the formula is wrong even where it claims to model everything), or A < `simulate_below_accuracy`, or calibration is inverted: some bin with at least `min_fights_per_row` fights has a win share below that of a lower bin by more than `calibration_max_inversion`.
2. Else **FIX_FORMULA** when every A_M is at least `fix_min_mechanic_accuracy`: add to the formula the mechanics whose A_M is below `fix_min_accuracy` (listed under the decision).
3. Else **DEVELOPER_CHOICE**: no reason to abandon the formula, but at least one mechanic is badly predicted; the listed rows decide between fixing those effects and simulating.

`predict.md` prints each threshold with "proposed", the measured value next to it and which rule fired.

### C.8 Settings `rts/data/scripts/fights/predict.json` (new file, read by `PredictSettings`; all placeholders)

| Key | Unit | Range | Value |
| --- | --- | --- | --- |
| `predictor_profile` | profile ID | a file under `rts/data/ai` | `aggressive_s5` |
| `margin_bin_edges` | ratio, Fix64 text, ascending, first "1" | 2 to 12 entries, 1 to 10000 | ["1", "1.1", "1.25", "1.5", "2", "3"] |
| `min_fights_per_row` | decided fights | 1 to 1000 | 10 |
| `mechanic_min_fights` | decided fights | 1 to 1000 | 20 |
| `simulate_below_accuracy` | fraction | 0 to 1 | 0.75 |
| `simulate_below_clean_accuracy` | fraction | 0 to 1 | 0.85 |
| `calibration_max_inversion` | fraction | 0 to 1 | 0.10 |
| `fix_min_mechanic_accuracy` | fraction | 0 to 1 | 0.70 |
| `fix_min_accuracy` | fraction | 0 to 1 | 0.85 |

### C.9 Acceptance tests (`rts/tests/Sim.Tests/Harness/PredictTests.cs`)

- **C-1 Entries.** A record with `units0` {"water.grasper": 3} gives one entry, count 3, health 3 x MaxHealth; compositions give entries in ordinal order.
- **C-2 Uphill.** `aUphill` is true only for `high_ground`, and always for side A = seat 0.
- **C-3 Classes and margin.** Synthetic predictions: equal times give `none` and margin 1; both at MaxSeconds give `none`; A faster gives `seat0` with margin tB / tA, capped at MaxAdvantage.
- **C-4 Scoring.** Mismatched fights excluded; predicted `none` on a decided fight is wrong; a stalemate predicted `none` agrees and is not in the headline.
- **C-5 Mechanic presence.** Synthetic records for each row of the C.5 table (drench needs an air unit on the other side; `windstorm_range` and `focus_fire` are never present); `only` counts.
- **C-6 Calibration bins.** A margin equal to an edge goes to the upper bin; Fix64 comparison, no double.
- **C-7 Committed sweep.** `--predict-fights --from rts/docs/reports/fights_2026-11-18/fights.jsonl` runs, reports `fights=780`, decided 765, no-winner 15, mismatched 0, and prints a decision.
- **C-8 Determinism.** `--from` twice: identical `predict.md` and `predict.jsonl`; sweep mode with `--jobs 1` and `--jobs 4`: identical files.
- **C-9 Decision rule.** Synthetic accuracies at each threshold edge give the three decisions.

### C.10 Tier and order

Sonnet `builder`; no review beyond the normal harness review (no `rts/src/Sim` or `rts/src/Ai` change). Independent of M: may be built in parallel with M1 to M4, and before M5 so that balance check 2 is measured. Measurement after the build: run it on a fresh sweep and attach `predict.md` in `rts/docs/reports/`.

---

## Out of scope

The second half of C (fixing the formula or a fight simulation); packages B, E, S and A of P-006; any change to `rts/src/Sim`, to AI play or to `--fights`; enforcing "behaviour before ladder" in code; difficulty levels; sequential (early-stopping) tests; reading arxiv papers (P-006 decision 6).

## What makes pairing harder than P-006 assumes

1. Pairs can hold the random streams but not what they yield: the stream is keyed by (seed, slot), and each profile maps it through its own opening library. Candidates with a changed library, and every forced-opening member, get only `seed` pairing, which removes less noise.
2. Forced openings cannot be built from the harness: `AiProfile` has a private constructor and no internals for the harness, so M.3.1 adds one method to `rts/src/Ai` (or the pool needs four profile copies that drift from `aggressive_s5`, Q-1).
3. The cross cell is decided by faction (Skai 36 of 36 in every recent ladder), so every cross pair is concordant: a third of a ladder's games carry no information about the AI, paired or not.
4. The candidate's slot is derived from the game index in six places (tally, mirror and cross rows, seat table, spot check, worker arguments, trace replay); paired records must carry it explicitly.
5. Pairing only helps when r < 1/2 (gain sqrt(2r)); r is unmeasured. With r near 1/2 a 216-game paired ladder sees no more than an unpaired one (9.6 points), and a two-hour pool buys about 10 points per member.
6. The stated rate of 216 games in 47 minutes was 65 minutes in the AI-H report; budgets in this spec are in games so they hold on either machine.

## Questions for the developer (the builder uses the defaults)

- **Q-1 Forced openings.** Add `AiProfile.WithForcedOpenings` to `rts/src/Ai` (additive, no play change), or keep the AI untouched and add four profile files with all but one opening at weight 0? Default: the method.
- **Q-2 Paired by default.** Make `--paired` the default ladder mode? Default: no, until the first measurement (M.8) shows r below 0.5.
- **Q-3 Draws and undecided games in pairs.** Leave such pairs out of the test, or score a draw as half a win? Default: leave out and count them (draws were 0 of 216 in recent ladders).
- **Q-4 Cross cells in the pool.** Play mirror cells only while one faction wins every cross game? Default: mirror only (`pool_matchups`); put `skai_vs_water` back once the cross cell is not one-sided.
- **Q-5 Pool stages.** Stage 1 (three profiles) on every candidate and stage 2 (forced openings) once before a package is marked done? Default: yes.
- **Q-6 Several tests in the gate.** Correct "better against at least one" for the number of members (Bonferroni), and should "not worse" also require the interval's lower end above a margin (non-inferiority) instead of "not significantly worse"? Default: Bonferroni on "better"; "not worse" = not significantly worse, as P-006 words it.
- **Q-7 Unmeasurable mechanics in check 1.** May the use check pass while some mechanic cannot be measured? Default: yes, listed under the check.
- **Q-8 Bars.** `max_share` 0.15 for static defence, unspent medians 400 common and 150 rare, `min_use_share` 0.10, `predictor_min_accuracy` 0.85, opening extremes 0.10 and 0.90, and the C.7 thresholds are all placeholders. Default: use them for the first reports and set them from those reports.
