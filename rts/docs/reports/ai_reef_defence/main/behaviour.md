<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_rr against baseline aggressive_s5

- Base seed 20261117, 144 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:seed.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_rr passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=0.022 metric_max=0.051 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.023 metric_max=0.056 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_rr games=144 held=72 violated=61 unjudged=11 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=72 violating_moves=61 unjudged_moves=11
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=144 held=73 violated=58 unjudged=13 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=73 violating_moves=58 unjudged_moves=13
    BEHAVIOUR workers_at profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=22.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=14 worse=0 ties=58 p=0.0001 mde_permille=875 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR income_by profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=6905.000 metric_max=7435.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_air games=19 metric_median=7095.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_fast_expand games=16 metric_median=7270.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_standard games=24 metric_median=7090.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_windrunner_pressure games=13 metric_median=6970.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_fast_expand games=15 metric_median=6630.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_grasper_pressure games=20 metric_median=6465.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_leviathan_tech games=17 metric_median=6565.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_standard games=20 metric_median=5950.000
    BEHAVIOUR income_by profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=6705.000 metric_max=7445.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=19 metric_median=7090.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=16 metric_median=7145.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=24 metric_median=6825.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=6935.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=15 metric_median=6615.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=20 metric_median=6520.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=17 metric_median=6115.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=5790.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=51 worse=20 ties=1 p=0.0003 mde_permille=667 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR econ_gap profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=83.000 metric_max=211.000 bar=1000.000 PASS total_gap_sum=12260 model_error_sum=-1628 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_air games=19 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_fast_expand games=16 metric_median=31.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_standard games=24 metric_median=83.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_windrunner_pressure games=13 metric_median=40.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_fast_expand games=15 metric_median=119.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_grasper_pressure games=20 metric_median=79.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_leviathan_tech games=17 metric_median=146.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_standard games=20 metric_median=170.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=87.000 metric_max=281.000 bar=1000.000 PASS total_gap_sum=15023 model_error_sum=-295 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=19 metric_median=77.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=16 metric_median=40.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=24 metric_median=87.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=13 metric_median=38.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=15 metric_median=131.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=20 metric_median=71.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=17 metric_median=187.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=20 metric_median=159.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=3.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=17 worse=14 ties=41 p=0.7201 mde_permille=752 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_trade profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=100.000 metric_max=1075.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=925.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=36 worse=16 ties=20 p=0.0078 mde_permille=695 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR harass_squads profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=5.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=64 worse=1 ties=7 p=0.0000 mde_permille=674 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR won profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=2.000 metric_max=22.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=4.000 metric_max=18.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=40 worse=25 ties=7 p=0.0817 mde_permille=674 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=1.071 metric_max=12.333 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.933 metric_max=10.500 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=46 worse=25 ties=1 p=0.0170 mde_permille=667 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR trained_unit profile=aggressive_s5_rr games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=43.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=18.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=72 worse=0 ties=0 p=0.0000 mde_permille=666 verdict=BETTER faction=water skipped_pairs=0

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 14 | 0 | 58 | 0.0001 | 875 | BETTER | water | 0 |
| income_by | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 51 | 20 | 1 | 0.0003 | 667 | BETTER | water | 0 |
| depot_losses_young | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 17 | 14 | 41 | 0.7201 | 752 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_trade | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 36 | 16 | 20 | 0.0078 | 695 | BETTER | water | 0 |
| harass_squads | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 64 | 1 | 7 | 0.0000 | 674 | BETTER | water | 0 |
| won | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| worker_losses_pre_overrun | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 40 | 25 | 7 | 0.0817 | 674 | NO_MEASURABLE_EFFECT | water | 0 |
| army_supply_ratio_at | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 46 | 25 | 1 | 0.0170 | 667 | BETTER | water | 0 |
| trained_unit | aggressive_s5_rr | aggressive_s5 | 72 | 0 | 72 | 0 | 0 | 0.0000 | 666 | BETTER | water | 0 |
