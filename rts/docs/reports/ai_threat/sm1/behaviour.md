<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_s against baseline aggressive_s5

- Base seed 20261117, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_s passed=8 of=9**

    BEHAVIOUR static_defence_share profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.027 metric_max=0.053 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.025 metric_max=0.053 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_s games=144 held=45 violated=82 unjudged=17 metric_median=1.000 metric_max=2.000 bar=0.000 FAIL moves=106 violating_moves=83 unjudged_moves=17
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=144 held=62 violated=62 unjudged=20 metric_median=0.000 metric_max=2.000 bar=0.000 FAIL moves=85 violating_moves=64 unjudged_moves=20
    BEHAVIOUR workers_at profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=109.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=109.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR income_by profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=6910.000 metric_max=7365.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_air games=20 metric_median=7120.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_fast_expand games=14 metric_median=7005.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_standard games=28 metric_median=7080.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=skai opening=skai_windrunner_pressure games=10 metric_median=6925.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_fast_expand games=20 metric_median=6765.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_grasper_pressure games=10 metric_median=6635.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_leviathan_tech games=22 metric_median=6415.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_s faction=water opening=water_standard games=20 metric_median=6325.000
    BEHAVIOUR income_by profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=6900.000 metric_max=7435.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=20 metric_median=7120.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=14 metric_median=7000.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=28 metric_median=7090.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=10 metric_median=6940.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=20 metric_median=6765.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=6635.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=22 metric_median=6380.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=6325.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=16 worse=9 ties=47 p=0.2295 mde_permille=781 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR econ_gap profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=91.000 metric_max=215.000 bar=1000.000 PASS total_gap_sum=11976 model_error_sum=-2732 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_air games=20 metric_median=79.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_fast_expand games=14 metric_median=36.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_standard games=28 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=skai opening=skai_windrunner_pressure games=10 metric_median=35.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_fast_expand games=20 metric_median=99.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_grasper_pressure games=10 metric_median=56.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_leviathan_tech games=22 metric_median=178.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_s faction=water opening=water_standard games=20 metric_median=144.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=88.000 metric_max=200.000 bar=1000.000 PASS total_gap_sum=11941 model_error_sum=-2714 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=20 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=14 metric_median=49.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=28 metric_median=83.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=10 metric_median=38.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=20 metric_median=99.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=56.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=22 metric_median=177.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=144.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=3.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=4 worse=11 ties=57 p=0.1185 mde_permille=862 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR harass_trade profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=675.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=850.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=19 worse=24 ties=29 p=0.5424 mde_permille=714 verdict=NO_MEASURABLE_EFFECT
    BEHAVIOUR harass_squads profile=aggressive_s5_s games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_s baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=14 worse=13 ties=45 p=1.0000 mde_permille=770 verdict=NO_MEASURABLE_EFFECT

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_s | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT |
| income_by | aggressive_s5_s | aggressive_s5 | 72 | 0 | 16 | 9 | 47 | 0.2295 | 781 | NO_MEASURABLE_EFFECT |
| depot_losses_young | aggressive_s5_s | aggressive_s5 | 72 | 0 | 4 | 11 | 57 | 0.1185 | 862 | NO_MEASURABLE_EFFECT |
| harass_trade | aggressive_s5_s | aggressive_s5 | 72 | 0 | 19 | 24 | 29 | 0.5424 | 714 | NO_MEASURABLE_EFFECT |
| harass_squads | aggressive_s5_s | aggressive_s5 | 72 | 0 | 14 | 13 | 45 | 1.0000 | 770 | NO_MEASURABLE_EFFECT |
