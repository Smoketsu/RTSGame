<!-- rts/docs/specs/ai_kite_correction.md -->
<!-- Build spec (26 Nov 2026) for the kite correction in the skirmish AI's fight prediction (Q-2 of ai_reef_runner_defence.md, developer decision of 25 Nov): the formula predictor believes the AI's own kite rule D.6.2, so a slower, shorter-ranged unit deals a fraction of its damage to a ranged unit that would kite it. Behind an optional faction-plan key predictor_kite_factor (absent = off), read only by the predictor mix (unit choice), on only in a new candidate profile aggressive_s5_kite. Packages K-1 (Rts.Ai and the profile), K-H (harness accuracy check), K-B (measurement with bars fixed before any run), every trial the designer ran and the open questions. Part of rts/docs/specs (index: README.md); built by Sonnet builders. -->
# Spec: kite correction in the AI's fight prediction

Written 26 Nov 2026 against `origin/main` at `c7f2b37` (Reef Runner weights on in `aggressive_s5`: `water.reef_runner` 30 in Water's main mix and all four openings; `aggressive_s5` trace pin `91c318f5a0ad05cb`, end tick 9750). Every number here is a **placeholder** for playtesting.

DRAFT: trials running; section 6 is filled as results arrive.
