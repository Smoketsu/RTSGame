<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_kite against baseline aggressive_s5

- Base seed 20261201, 144 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_kite passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=0.021 metric_max=0.061 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.020 metric_max=0.050 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_kite games=144 held=73 violated=51 unjudged=20 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=71 violating_moves=51 unjudged_moves=20
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=144 held=81 violated=53 unjudged=10 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=68 violating_moves=53 unjudged_moves=10
    BEHAVIOUR workers_at profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=23.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=23.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR income_by profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=6760.000 metric_max=7425.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_air games=21 metric_median=7115.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_fast_expand games=16 metric_median=7145.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_standard games=22 metric_median=7025.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_windrunner_pressure games=13 metric_median=6910.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_fast_expand games=18 metric_median=6615.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_grasper_pressure games=13 metric_median=6665.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_leviathan_tech games=18 metric_median=6350.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_standard games=23 metric_median=6075.000
    BEHAVIOUR income_by profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=6760.000 metric_max=7425.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=21 metric_median=7115.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=16 metric_median=7145.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=22 metric_median=6880.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=6900.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=18 metric_median=6575.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=6665.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=18 metric_median=6350.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=23 metric_median=6075.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=20 worse=18 ties=34 p=0.8714 mde_permille=728 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR econ_gap profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=94.000 metric_max=211.000 bar=1000.000 PASS total_gap_sum=13304 model_error_sum=-1248 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_air games=21 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_fast_expand games=16 metric_median=39.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_standard games=22 metric_median=86.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_windrunner_pressure games=13 metric_median=42.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_fast_expand games=18 metric_median=123.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_grasper_pressure games=13 metric_median=48.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_leviathan_tech games=18 metric_median=155.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_standard games=23 metric_median=150.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=94.000 metric_max=211.000 bar=1000.000 PASS total_gap_sum=13650 model_error_sum=-1157 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=21 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=16 metric_median=39.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=22 metric_median=89.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=42.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=18 metric_median=129.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=48.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=18 metric_median=155.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=23 metric_median=163.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=5 worse=3 ties=64 p=0.7266 mde_permille=996 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_trade profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=100.000 metric_max=925.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=50.000 metric_max=1275.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=22 worse=21 ties=29 p=1.0000 mde_permille=714 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_squads profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=4.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=48 worse=3 ties=21 p=0.0000 mde_permille=697 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR won profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=1.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=3 worse=1 ties=68 p=0.6250 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=25.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=4.000 metric_max=25.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=18 worse=14 ties=40 p=0.5966 mde_permille=748 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=1.029 metric_max=16.500 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.971 metric_max=30.000 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=21 worse=22 ties=29 p=1.0000 mde_permille=714 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR trained_unit profile=aggressive_s5_kite games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=71.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=43.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=71 worse=1 ties=0 p=0.0000 mde_permille=666 verdict=BETTER faction=water skipped_pairs=0

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| income_by | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 20 | 18 | 34 | 0.8714 | 728 | NO_MEASURABLE_EFFECT | water | 0 |
| depot_losses_young | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 5 | 3 | 64 | 0.7266 | 996 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_trade | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 22 | 21 | 29 | 1.0000 | 714 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_squads | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 48 | 3 | 21 | 0.0000 | 697 | BETTER | water | 0 |
| won | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 3 | 1 | 68 | 0.6250 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| worker_losses_pre_overrun | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 18 | 14 | 40 | 0.5966 | 748 | NO_MEASURABLE_EFFECT | water | 0 |
| army_supply_ratio_at | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 21 | 22 | 29 | 1.0000 | 714 | NO_MEASURABLE_EFFECT | water | 0 |
| trained_unit | aggressive_s5_kite | aggressive_s5 | 72 | 0 | 71 | 1 | 0 | 0.0000 | 666 | BETTER | water | 0 |
