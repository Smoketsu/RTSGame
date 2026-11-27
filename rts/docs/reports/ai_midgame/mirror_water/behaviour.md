<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_mid against baseline aggressive_s5

- Base seed 20261209, 48 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_mid passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.028 metric_max=0.046 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.027 metric_max=0.049 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_mid games=48 held=21 violated=21 unjudged=6 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=27 violating_moves=21 unjudged_moves=6
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=48 held=21 violated=23 unjudged=4 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=27 violating_moves=23 unjudged_moves=4
    BEHAVIOUR workers_at profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=25.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=24.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=0 worse=0 ties=24 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR income_by profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=6805.000 metric_max=7090.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_fast_expand games=12 metric_median=6945.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_grasper_pressure games=10 metric_median=6805.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_leviathan_tech games=16 metric_median=6795.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_standard games=10 metric_median=6605.000
    BEHAVIOUR income_by profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=6820.000 metric_max=7160.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=12 metric_median=6955.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=6820.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=16 metric_median=6815.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=10 metric_median=6605.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=12 worse=10 ties=2 p=0.8318 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR econ_gap profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=113.000 metric_max=204.000 bar=1000.000 PASS total_gap_sum=4065 model_error_sum=-852 gap_judged=48
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_fast_expand games=12 metric_median=90.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_grasper_pressure games=10 metric_median=18.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_leviathan_tech games=16 metric_median=144.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_standard games=10 metric_median=116.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=110.000 metric_max=160.000 bar=1000.000 PASS total_gap_sum=3861 model_error_sum=-914 gap_judged=48
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=12 metric_median=91.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=10 metric_median=20.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=16 metric_median=133.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=10 metric_median=112.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=5 worse=5 ties=14 p=1.0000 mde_permille=943 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_trade profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=450.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=50.000 metric_max=425.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=10 worse=13 ties=1 p=0.6776 mde_permille=793 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_squads profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=3.000 metric_max=5.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=3.000 metric_max=5.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=9 worse=14 ties=1 p=0.4049 mde_permille=793 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR won profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=1.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=5 worse=9 ties=10 p=0.4240 mde_permille=875 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=2.000 metric_max=21.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=2.000 metric_max=34.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=13 worse=9 ties=2 p=0.5235 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=0.969 metric_max=1.625 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=1.028 metric_max=1.556 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=8 worse=14 ties=2 p=0.2863 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR trained_unit profile=aggressive_s5_mid games=48 held=48 violated=0 unjudged=0 metric_median=25.000 metric_max=41.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=48 held=48 violated=0 unjudged=0 metric_median=24.000 metric_max=43.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=24 unjudged_pairs=0 better=9 worse=13 ties=2 p=0.5235 mde_permille=799 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 0 | 0 | 24 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| income_by | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 12 | 10 | 2 | 0.8318 | 799 | NO_MEASURABLE_EFFECT | water | 0 |
| depot_losses_young | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 5 | 5 | 14 | 1.0000 | 943 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_trade | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 10 | 13 | 1 | 0.6776 | 793 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_squads | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 9 | 14 | 1 | 0.4049 | 793 | NO_MEASURABLE_EFFECT | water | 0 |
| won | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 5 | 9 | 10 | 0.4240 | 875 | NO_MEASURABLE_EFFECT | water | 0 |
| worker_losses_pre_overrun | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 13 | 9 | 2 | 0.5235 | 799 | NO_MEASURABLE_EFFECT | water | 0 |
| army_supply_ratio_at | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 8 | 14 | 2 | 0.2863 | 799 | NO_MEASURABLE_EFFECT | water | 0 |
| trained_unit | aggressive_s5_mid | aggressive_s5 | 24 | 0 | 9 | 13 | 2 | 0.5235 | 799 | NO_MEASURABLE_EFFECT | water | 0 |
