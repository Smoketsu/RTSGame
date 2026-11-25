<!-- rts/docs/reports/ai_reef_defence/traces/summary.md -->
<!-- Trace facts reported by RR-B (spec 5.4 and guard RR-G4), read from the trace files in this folder by a throwaway script. Part of rts/docs/reports/ai_reef_defence. -->
# Trace summary

- skai_vs_water_game0, baseline Water (P1): first Reef Runner beyond the scout trained by the sample at t=210 s (cumulative Reef Runners trained: 3 by the end of the trace); army at t=180 s: water.grasper:1,water.shellback:2
- skai_vs_water_game1, candidate Water (P1): first Reef Runner beyond the scout trained by the sample at t=120 s (cumulative Reef Runners trained: 25 by the end of the trace); army at t=180 s: water.reef_runner:2,water.shellback:1

- RR-G4: no sample line of any of the four traces lists a rejection reason (every `rejected=` field is `-`), and no trace ends with a rejection total, so no candidate count exceeds the baseline count of the same matchup and seed.
