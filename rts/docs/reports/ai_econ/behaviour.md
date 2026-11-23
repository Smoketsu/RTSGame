<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_e against baseline aggressive_s5

- Base seed 20261117, 24 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_e passed=5 of=6**

    BEHAVIOUR static_defence_share profile=aggressive_s5_e games=72 held=72 violated=0 unjudged=0 metric_median=0.025 metric_max=0.054 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=72 held=72 violated=0 unjudged=0 metric_median=0.024 metric_max=0.044 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_e games=72 held=44 violated=16 unjudged=12 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=28 violating_moves=16 unjudged_moves=12
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=72 held=22 violated=43 unjudged=7 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=51 violating_moves=43 unjudged_moves=7
    BEHAVIOUR workers_at profile=aggressive_s5_e games=72 held=72 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=72 held=72 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_e games=72 held=72 violated=0 unjudged=0 metric_median=112.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=72 held=72 violated=0 unjudged=0 metric_median=112.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR income_by profile=aggressive_s5_e games=72 held=72 violated=0 unjudged=0 metric_median=6875.000 metric_max=7345.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=skai opening=skai_air games=11 metric_median=7245.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=skai opening=skai_fast_expand games=5 metric_median=6990.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=skai opening=skai_standard games=17 metric_median=7035.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=skai opening=skai_windrunner_pressure games=3 metric_median=7180.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=water opening=water_fast_expand games=8 metric_median=6740.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=water opening=water_grasper_pressure games=5 metric_median=6555.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=water opening=water_leviathan_tech games=12 metric_median=6285.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_e faction=water opening=water_standard games=11 metric_median=6320.000
    BEHAVIOUR income_by profile=aggressive_s5 games=72 held=72 violated=0 unjudged=0 metric_median=6910.000 metric_max=7330.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=11 metric_median=7190.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=5 metric_median=6950.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=17 metric_median=7095.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=3 metric_median=7035.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=8 metric_median=6785.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=5 metric_median=6510.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=12 metric_median=6615.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=11 metric_median=6325.000
    BEHAVIOUR econ_gap profile=aggressive_s5_e games=72 held=72 violated=0 unjudged=0 metric_median=96.000 metric_max=229.000 bar=1000.000 PASS total_gap_sum=5904 model_error_sum=-1835 gap_judged=72
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=skai opening=skai_air games=11 metric_median=77.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=skai opening=skai_fast_expand games=5 metric_median=62.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=skai opening=skai_standard games=17 metric_median=96.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=skai opening=skai_windrunner_pressure games=3 metric_median=29.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=water opening=water_fast_expand games=8 metric_median=92.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=water opening=water_grasper_pressure games=5 metric_median=64.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=water opening=water_leviathan_tech games=12 metric_median=176.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_e faction=water opening=water_standard games=11 metric_median=147.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=72 held=72 violated=0 unjudged=0 metric_median=91.000 metric_max=209.000 bar=1000.000 PASS total_gap_sum=6031 model_error_sum=-1400 gap_judged=72
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=11 metric_median=74.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=5 metric_median=60.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=17 metric_median=77.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=3 metric_median=42.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=8 metric_median=95.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=5 metric_median=65.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=12 metric_median=161.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=11 metric_median=144.000
