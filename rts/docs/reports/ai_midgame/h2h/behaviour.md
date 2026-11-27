<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_mid against baseline aggressive_s5

- Base seed 20261209, 144 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Pairing kind per faction (M.1.3): skai:full,water:full.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_mid passed=12 of=13**

    BEHAVIOUR static_defence_share profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=0.031 metric_max=0.048 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.025 metric_max=0.057 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_mid games=144 held=77 violated=53 unjudged=14 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=69 violating_moves=53 unjudged_moves=14
    BEHAVIOUR hq_move_new_income profile=aggressive_s5 games=144 held=75 violated=54 unjudged=15 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=70 violating_moves=54 unjudged_moves=15
    BEHAVIOUR workers_at profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=23.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=23.000 metric_max=27.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=102.000 metric_max=120.000 bar=7200.000 PASS
    BEHAVIOUR_PAIRED check=first_drop_off candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR income_by profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=6790.000 metric_max=7360.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_air games=21 metric_median=7090.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_fast_expand games=17 metric_median=7070.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_standard games=22 metric_median=7005.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=skai opening=skai_windrunner_pressure games=12 metric_median=6900.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_fast_expand games=18 metric_median=6520.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_grasper_pressure games=13 metric_median=6400.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_leviathan_tech games=19 metric_median=6220.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5_mid faction=water opening=water_standard games=22 metric_median=6215.000
    BEHAVIOUR income_by profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=6765.000 metric_max=7370.000 bar=0.000 PASS
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_air games=21 metric_median=7120.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_fast_expand games=17 metric_median=7080.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_standard games=22 metric_median=7000.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=12 metric_median=6915.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_fast_expand games=18 metric_median=6605.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=6655.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_leviathan_tech games=19 metric_median=6340.000
    BEHAVIOUR_BY_OPENING check=income_by profile=aggressive_s5 faction=water opening=water_standard games=22 metric_median=6160.000
    BEHAVIOUR_PAIRED check=income_by candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=27 worse=38 ties=7 p=0.2145 mde_permille=674 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR econ_gap profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=91.000 metric_max=222.000 bar=1000.000 PASS total_gap_sum=14322 model_error_sum=-347 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_air games=21 metric_median=79.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_fast_expand games=17 metric_median=42.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_standard games=22 metric_median=83.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=skai opening=skai_windrunner_pressure games=12 metric_median=38.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_fast_expand games=18 metric_median=125.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_grasper_pressure games=13 metric_median=65.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_leviathan_tech games=19 metric_median=184.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5_mid faction=water opening=water_standard games=22 metric_median=147.000
    BEHAVIOUR econ_gap profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=93.000 metric_max=211.000 bar=1000.000 PASS total_gap_sum=13389 model_error_sum=-1410 gap_judged=144
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_air games=21 metric_median=79.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_fast_expand games=17 metric_median=41.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_standard games=22 metric_median=93.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=skai opening=skai_windrunner_pressure games=12 metric_median=45.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_fast_expand games=18 metric_median=127.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_grasper_pressure games=13 metric_median=44.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_leviathan_tech games=19 metric_median=171.000
    BEHAVIOUR_BY_OPENING check=econ_gap profile=aggressive_s5 faction=water opening=water_standard games=22 metric_median=155.000
    BEHAVIOUR depot_losses_young profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=3.000 bar=none PASS
    BEHAVIOUR depot_losses_young profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=2.000 bar=none PASS
    BEHAVIOUR_PAIRED check=depot_losses_young candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=17 worse=8 ties=47 p=0.1078 mde_permille=781 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_trade profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=100.000 metric_max=700.000 bar=none PASS
    BEHAVIOUR harass_trade profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=100.000 metric_max=850.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_trade candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=30 worse=26 ties=16 p=0.6889 mde_permille=688 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR harass_squads profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR harass_squads profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=6.000 bar=none PASS
    BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=33 worse=9 ties=30 p=0.0003 mde_permille=717 verdict=BETTER faction=water skipped_pairs=0
    BEHAVIOUR won profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR won profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=1.000 bar=none PASS
    BEHAVIOUR_PAIRED check=won candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=0 worse=0 ties=72 p=1.0000 mde_permille=1072 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=3.000 metric_max=26.000 bar=none PASS
    BEHAVIOUR worker_losses_pre_overrun profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=4.000 metric_max=17.000 bar=none PASS
    BEHAVIOUR_PAIRED check=worker_losses_pre_overrun candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=38 worse=23 ties=11 p=0.0722 mde_permille=680 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=1.000 metric_max=19.000 bar=none PASS
    BEHAVIOUR army_supply_ratio_at profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=1.000 metric_max=3.727 bar=none PASS
    BEHAVIOUR_PAIRED check=army_supply_ratio_at candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=39 worse=27 ties=6 p=0.1753 mde_permille=673 verdict=NO_MEASURABLE_EFFECT faction=water skipped_pairs=0
    BEHAVIOUR trained_unit profile=aggressive_s5_mid games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=33.000 bar=none PASS
    BEHAVIOUR trained_unit profile=aggressive_s5 games=144 held=144 violated=0 unjudged=0 metric_median=0.000 metric_max=35.000 bar=none PASS
    BEHAVIOUR_PAIRED check=trained_unit candidate=aggressive_s5_mid baseline=aggressive_s5 pairs=72 unjudged_pairs=0 better=23 worse=46 ties=3 p=0.0076 mde_permille=669 verdict=WORSE faction=water skipped_pairs=0

## Paired

Sign test of the pair sums at alpha 0.0500 (ai_threat_map.md 7.3); mde_permille is the share of decisive pairs in which the candidate must be better to be seen with power 0.80.

| check | candidate | baseline | pairs | unjudged pairs | better | worse | ties | p | mde permille | verdict | faction | skipped pairs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| first_drop_off | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| income_by | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 27 | 38 | 7 | 0.2145 | 674 | NO_MEASURABLE_EFFECT | water | 0 |
| depot_losses_young | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 17 | 8 | 47 | 0.1078 | 781 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_trade | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 30 | 26 | 16 | 0.6889 | 688 | NO_MEASURABLE_EFFECT | water | 0 |
| harass_squads | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 33 | 9 | 30 | 0.0003 | 717 | BETTER | water | 0 |
| won | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 0 | 0 | 72 | 1.0000 | 1072 | NO_MEASURABLE_EFFECT | water | 0 |
| worker_losses_pre_overrun | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 38 | 23 | 11 | 0.0722 | 680 | NO_MEASURABLE_EFFECT | water | 0 |
| army_supply_ratio_at | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 39 | 27 | 6 | 0.1753 | 673 | NO_MEASURABLE_EFFECT | water | 0 |
| trained_unit | aggressive_s5_mid | aggressive_s5 | 72 | 0 | 23 | 46 | 3 | 0.0076 | 669 | WORSE | water | 0 |
