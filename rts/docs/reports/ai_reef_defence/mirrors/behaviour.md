<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_rr against baseline aggressive_s5

- Base seed 20261117, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:seed.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_rr passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=0.031 metric_max=0.053 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.029 metric_max=0.042 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_rr games=96 held=32 violated=40 unjudged=24 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=66 violating_moves=40 unjudged_moves=24
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=96 held=45 violated=28 unjudged=23 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=51 violating_moves=28 unjudged_moves=23
    BEHAVIOUR workers_at profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=14 worse=0 ties=10 p=0.0001 mde_permille=875 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR income_by profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=6990.000 metric_max=7365.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_air games=12 metric_median=7145.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_fast_expand games=12 metric_median=7000.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_standard games=19 metric_median=7085.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=skai opening=skai_windrunner_pressure games=5 metric_median=7035.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_fast_expand games=16 metric_median=6965.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_grasper_pressure games=5 metric_median=6755.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_leviathan_tech games=18 metric_median=6985.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_rr faction=water opening=water_standard games=9 metric_median=6710.000
    BEHAVIOUR income_by profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=6960.000 metric_max=7365.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=12 metric_median=7145.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=12 metric_median=7000.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=19 metric_median=7085.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=5 metric_median=7035.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=16 metric_median=6925.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=5 metric_median=6945.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=18 metric_median=6520.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=9 metric_median=6655.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=22 worse=1 ties=1 p=0.0000 mde_permille=793 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR econ_gap profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=92.000 metric_max=156.000 bar=1000.000 PASS total_gap_sum=6130 model_error_sum=-2515 gap_judged=96
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_air games=12 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_fast_expand games=12 metric_median=63.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_standard games=19 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=skai opening=skai_windrunner_pressure games=5 metric_median=29.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_fast_expand games=16 metric_median=96.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_grasper_pressure games=5 metric_median=35.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_leviathan_tech games=18 metric_median=111.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_rr faction=water opening=water_standard games=9 metric_median=102.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=84.000 metric_max=194.000 bar=1000.000 PASS total_gap_sum=7072 model_error_sum=-2293 gap_judged=96
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=12 metric_median=75.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=12 metric_median=63.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=19 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=5 metric_median=29.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=16 metric_median=76.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=5 metric_median=17.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=18 metric_median=170.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=9 metric_median=108.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=6 worse=3 ties=15 p=0.5078 mde_permille=967 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR harass_trade profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=50.000 metric_max=630.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=475.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=19 worse=4 ties=1 p=0.0026 mde_permille=793 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR harass_squads profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=4.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=24 worse=0 ties=0 p=0.0000 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR won profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=1.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=19 worse=0 ties=5 p=0.0000 mde_permille=822 verdict=BETTER faction=water skipped_pairs=24
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=3.000 metric_max=22.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=4.000 metric_max=22.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=16 worse=7 ties=1 p=0.0931 mde_permille=793 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=1.000 metric_max=3.700 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=1.000 metric_max=3.700 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=14 worse=9 ties=1 p=0.4049 mde_permille=793 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=24
    BEHAVIOUR trained_unit profile=aggressive_s5_rr games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=54.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=96 held=96 violated=0 unjudged=0 metric_median=0.000 metric_max=9.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_rr baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=24 worse=0 ties=0 p=0.0000 mde_permille=786 verdict=BETTER faction=water skipped_pairs=24

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 14 | 0 | 10 | 0.0001 | 875 | BETTER | water | 24 |
| income_by | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 22 | 1 | 1 | 0.0000 | 793 | BETTER | water | 24 |
| depot_losses_young | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 6 | 3 | 15 | 0.5078 | 967 | NO_MEASURABLE_EFFECT | water | 24 |
| harass_trade | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 19 | 4 | 1 | 0.0026 | 793 | BETTER | water | 24 |
| harass_squads | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 24 | 0 | 0 | 0.0000 | 786 | BETTER | water | 24 |
| won | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 19 | 0 | 5 | 0.0000 | 822 | BETTER | water | 24 |
| worker_losses_pre_overrun | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 16 | 7 | 1 | 0.0931 | 793 | NO_MEASURABLE_EFFECT | water | 24 |
| army_supply_ratio_at | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 14 | 9 | 1 | 0.4049 | 793 | NO_MEASURABLE_EFFECT | water | 24 |
| trained_unit | aggressive_s5_rr | aggressive_s5 | 24 | 0 | 24 | 0 | 0 | 0.0000 | 786 | BETTER | water | 24 |
