<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_s against baseline aggressive_s5

- Base seed 20261117, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_s passed=8 of=9**

    BEHAVIOUR static_defence_share profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.026 metric_max=0.052 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.025 metric_max=0.055 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_s games=144 held=53 violated=66 unjudged=25 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=94 violating_moves=66 unjudged_moves=26
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=144 held=57 violated=71 unjudged=16 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=89 violating_moves=71 unjudged_moves=16
    BEHAVIOUR workers_at profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=109.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=109.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR income_by profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=6875.000 metric_max=7425.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_air games=20 metric_median=7135.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_fast_expand games=14 metric_median=7000.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_standard games=28 metric_median=7045.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_windrunner_pressure games=10 metric_median=6965.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_fast_expand games=20 metric_median=6785.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_grasper_pressure games=10 metric_median=6635.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_leviathan_tech games=22 metric_median=6335.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_standard games=20 metric_median=6325.000
    BEHAVIOUR income_by profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=6890.000 metric_max=7435.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=20 metric_median=7120.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=14 metric_median=7025.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=28 metric_median=7095.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=10 metric_median=6940.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=20 metric_median=6785.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=6635.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=22 metric_median=6335.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=6325.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=10 worse=23 ties=39 p=0.0351 mde_permille=744 verdict=WORSE
    BEHAVIOUR econ_gap profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=91.000 metric_max=208.000 bar=1000.000 PASS total_gap_sum=12309 model_error_sum=-2536 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_air games=20 metric_median=78.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_fast_expand games=14 metric_median=49.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_standard games=28 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_windrunner_pressure games=10 metric_median=37.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_fast_expand games=20 metric_median=95.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_grasper_pressure games=10 metric_median=56.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_leviathan_tech games=22 metric_median=180.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_standard games=20 metric_median=144.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=85.000 metric_max=208.000 bar=1000.000 PASS total_gap_sum=11998 model_error_sum=-2594 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=20 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=14 metric_median=49.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=28 metric_median=83.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=10 metric_median=38.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=20 metric_median=95.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=56.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=22 metric_median=180.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=144.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=10 worse=7 ties=55 p=0.6291 mde_permille=840 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR harass_trade profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=850.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=790.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=20 worse=22 ties=30 p=0.8776 mde_permille=717 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR harass_squads profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=10 worse=19 ties=43 p=0.1360 mde_permille=761 verdict=NO_MEASURABLE_EFFECT

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_s | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT |
| income_by | aggressive_s5_s | aggressive_s5 | 72 | 0 | 10 | 23 | 39 | 0.0351 | 744 | WORSE |
| depot_losses_young | aggressive_s5_s | aggressive_s5 | 72 | 0 | 10 | 7 | 55 | 0.6291 | 840 | NO_MEASURABLE_EFFECT |
| harass_trade | aggressive_s5_s | aggressive_s5 | 72 | 0 | 20 | 22 | 30 | 0.8776 | 717 | NO_MEASURABLE_EFFECT |
| harass_squads | aggressive_s5_s | aggressive_s5 | 72 | 0 | 10 | 19 | 43 | 0.1360 | 761 | NO_MEASURABLE_EFFECT |
