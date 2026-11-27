<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_mid against baseline aggressive_s5

- Base seed 20261209, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_mid passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.030 metric_max=0.048 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.029 metric_max=0.047 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_mid games=48 held=16 violated=22 unjudged=10 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=33 violating_moves=22 unjudged_moves=10
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=48 held=14 violated=23 unjudged=11 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=34 violating_moves=23 unjudged_moves=11
    BEHAVIOUR workers_at profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=109.000 metric_max=116.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=109.000 metric_max=116.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=0 worse=0 ties=24 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR income_by profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=7005.000 metric_max=7245.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_air games=12 metric_median=7100.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_fast_expand games=8 metric_median=6860.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_standard games=14 metric_median=6855.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_windrunner_pressure games=14 metric_median=7035.000
    BEHAVIOUR income_by profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=7030.000 metric_max=7245.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=12 metric_median=7100.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=8 metric_median=6860.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=14 metric_median=6995.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=14 metric_median=6895.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=9 worse=5 ties=10 p=0.4240 mde_permille=875 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR econ_gap profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=75.000 metric_max=197.000 bar=1000.000 PASS total_gap_sum=2629 model_error_sum=-1230 gap_judged=48
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_air games=12 metric_median=78.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_fast_expand games=8 metric_median=67.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_standard games=14 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_windrunner_pressure games=14 metric_median=32.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=75.000 metric_max=164.000 bar=1000.000 PASS total_gap_sum=2532 model_error_sum=-1256 gap_judged=48
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=12 metric_median=78.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=8 metric_median=63.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=14 metric_median=84.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=14 metric_median=41.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=3.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=5 worse=4 ties=15 p=1.0000 mde_permille=967 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR harass_trade profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=150.000 metric_max=800.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=50.000 metric_max=600.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=13 worse=9 ties=2 p=0.5235 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR harass_squads profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=5.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=5.000 metric_max=7.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=6 worse=11 ties=7 p=0.3323 mde_permille=840 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR won profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=1.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=3 worse=1 ties=20 p=0.6250 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=5.000 metric_max=27.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=5.000 metric_max=24.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=8 worse=10 ties=6 p=0.8145 mde_permille=831 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=1.074 metric_max=2.571 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.897 metric_max=3.000 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=12 worse=2 ties=10 p=0.0129 mde_permille=875 verdict=BETTER faction=skai skipped_pairs=0
    BEHAVIOUR trained_unit profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=0.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=0.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=0 worse=0 ties=24 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=skai skipped_pairs=0

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 0 | 0 | 24 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | skai | 0 |
| income_by | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 9 | 5 | 10 | 0.4240 | 875 | NO_MEASURABLE_EFFECT | skai | 0 |
| depot_losses_young | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 5 | 4 | 15 | 1.0000 | 967 | NO_MEASURABLE_EFFECT | skai | 0 |
| harass_trade | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 13 | 9 | 2 | 0.5235 | 799 | NO_MEASURABLE_EFFECT | skai | 0 |
| harass_squads | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 6 | 11 | 7 | 0.3323 | 840 | NO_MEASURABLE_EFFECT | skai | 0 |
| won | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 3 | 1 | 20 | 0.6250 | 1072 | NO_MEASURABLE_EFFECT | skai | 0 |
| worker_losses_pre_overrun | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 8 | 10 | 6 | 0.8145 | 831 | NO_MEASURABLE_EFFECT | skai | 0 |
| army_supply_ratio_at | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 12 | 2 | 10 | 0.0129 | 875 | BETTER | skai | 0 |
| trained_unit | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 0 | 0 | 24 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | skai | 0 |
