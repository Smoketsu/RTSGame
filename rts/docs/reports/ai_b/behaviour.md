<!-- behaviour.md: written by the balance harness --behaviour (rts/tools/BalanceHarness/BehaviourRunner.cs); not a source file. -->
# Behaviour checks: candidate aggressive_s5_b against baseline abl_s5b_no_b

- Base seed 20261117, 24 games per cell in the paired layout (balance_measurement.md M.1.2), game cap 2400 s; a slot-game is one slot of one game; a profile passes a check when violated / judged is at most the check's max_violation_fraction.
- Determinism spot check: 2 games re-run in-process with the observer, 0 mismatches.
- Checks not declared in behaviour_checks.json (not run): none.

**BEHAVIOUR_SUMMARY candidate=aggressive_s5_b passed=3 of=4**

    BEHAVIOUR static_defence_share profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=0.039 metric_max=0.076 bar=0.150 PASS
    BEHAVIOUR static_defence_share profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=0.026 metric_max=0.048 bar=0.150 PASS
    BEHAVIOUR hq_move_new_income profile=aggressive_s5_b games=72 held=62 violated=7 unjudged=3 metric_median=0.000 metric_max=1.000 bar=0.000 FAIL moves=16 violating_moves=7 unjudged_moves=3
    BEHAVIOUR hq_move_new_income profile=abl_s5b_no_b games=72 held=18 violated=36 unjudged=18 metric_median=1.000 metric_max=1.000 bar=0.000 FAIL moves=57 violating_moves=36 unjudged_moves=19
    BEHAVIOUR workers_at profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=21.000 metric_max=24.000 bar=0.000 PASS
    BEHAVIOUR workers_at profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=24.000 metric_max=26.000 bar=0.000 PASS
    BEHAVIOUR first_drop_off profile=aggressive_s5_b games=72 held=72 violated=0 unjudged=0 metric_median=82.000 metric_max=119.000 bar=7200.000 PASS
    BEHAVIOUR first_drop_off profile=abl_s5b_no_b games=72 held=72 violated=0 unjudged=0 metric_median=102.000 metric_max=114.000 bar=7200.000 PASS
