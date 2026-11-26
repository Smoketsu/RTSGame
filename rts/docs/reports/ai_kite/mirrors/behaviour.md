<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_kite against baseline aggressive_s5

- Base seed 20261201, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_kite passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=0.029 metric_max=0.051 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.026 metric_max=0.051 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_kite games=96 held=33 violated=43 unjudged=20 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=67 violating_moves=43 unjudged_moves=20
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=96 held=47 violated=37 unjudged=12 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=50 violating_moves=37 unjudged_moves=12
    BEHAVIOUR workers_at profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=0 worse=0 ties=24 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR income_by profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=6905.000 metric_max=7230.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_air games=8 metric_median=6805.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_fast_expand games=12 metric_median=6965.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_standard games=15 metric_median=7075.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=skai opening=skai_windrunner_pressure games=13 metric_median=6870.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_fast_expand games=13 metric_median=6990.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_grasper_pressure games=13 metric_median=6875.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_leviathan_tech games=13 metric_median=6980.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_kite faction=water opening=water_standard games=9 metric_median=6645.000
    BEHAVIOUR income_by profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=6915.000 metric_max=7230.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=8 metric_median=6805.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=12 metric_median=6965.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=15 metric_median=7075.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=6870.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=13 metric_median=6980.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=6800.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=13 metric_median=7010.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=9 metric_median=6630.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=11 worse=11 ties=2 p=1.0000 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR econ_gap profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=83.000 metric_max=160.000 bar=1000.000 PASS total_gap_sum=5636 model_error_sum=-2355 gap_judged=96
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_air games=8 metric_median=119.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_fast_expand games=12 metric_median=66.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_standard games=15 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=skai opening=skai_windrunner_pressure games=13 metric_median=30.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_fast_expand games=13 metric_median=95.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_grasper_pressure games=13 metric_median=22.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_leviathan_tech games=13 metric_median=112.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_kite faction=water opening=water_standard games=9 metric_median=111.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=83.000 metric_max=156.000 bar=1000.000 PASS total_gap_sum=5672 model_error_sum=-2100 gap_judged=96
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=8 metric_median=119.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=12 metric_median=66.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=15 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=30.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=13 metric_median=86.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=21.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=13 metric_median=111.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=9 metric_median=115.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=7 worse=1 ties=16 p=0.0703 mde_permille=996 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR harass_trade profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=100.000 metric_max=600.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=600.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=19 worse=5 ties=0 p=0.0066 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR harass_squads profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=5.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=4.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=23 worse=1 ties=0 p=0.0000 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR won profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=1.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=20 worse=0 ties=4 p=0.0000 mde_permille=814 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=5.000 metric_max=21.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=5.000 metric_max=39.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=13 worse=11 ties=0 p=0.8388 mde_permille=786 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=1.042 metric_max=2.467 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.943 metric_max=2.467 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=18 worse=6 ties=0 p=0.0227 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR trained_unit profile=aggressive_s5_kite games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=72.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=48.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_kite baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=24 worse=0 ties=0 p=0.0000 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 0 | 0 | 24 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 24 |
| income_by | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 11 | 11 | 2 | 1.0000 | 799 | NO_MEASURABLE_EFFECT | water | 24 |
| depot_losses_young | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 7 | 1 | 16 | 0.0703 | 996 | NO_MEASURABLE_EFFECT | water | 24 |
| harass_trade | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 19 | 5 | 0 | 0.0066 | 786 | BETTER | water | 24 |
| harass_squads | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 23 | 1 | 0 | 0.0000 | 786 | BETTER | water | 24 |
| won | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 20 | 0 | 4 | 0.0000 | 814 | BETTER | water | 24 |
| worker_losses_pre_overrun | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 13 | 11 | 0 | 0.8388 | 786 | NO_MEASURABLE_EFFECT | water | 24 |
| army_supply_ratio_at | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 18 | 6 | 0 | 0.0227 | 786 | BETTER | water | 24 |
| trained_unit | aggressive_s5_kite | aggressive_s5 | 24 | 0 | 24 | 0 | 0 | 0.0000 | 786 | BETTER | water | 24 |
