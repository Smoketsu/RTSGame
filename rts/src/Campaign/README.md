<!-- rts/src/Campaign/README.md: the engine-free campaign scripting library (assembly Rts.Campaign): what it contains and the host contract. Spec: rts/docs/specs/campaign_scripting.md. -->
# Rts.Campaign

Missions as data, run beside the simulation. Engine-free, `netstandard2.1`, no floating point, no file access (hosts pass text).

- `MissionLoader.Parse(text, path, data, BuiltInMechanics.ScriptParsers)` gives an immutable `MissionDefinition` or throws `DataValidationException` naming the file and field.
- `MissionRunner` reads `sim.Events` and read-only state after each step and calls only `sim.Submit` (as the script issuer 255 and `script` slots). Host loop: `mission.CreateSimulation(data)`, `sim.Start()`, `runner.Start(sim)`, then per tick `ReportSignal(...)` (camera and selection, from Unity or `MissionInputs`), `sim.Step()`, `runner.AfterStep(sim)`; stop when `runner.Status != Running`.
- `IMissionPresenter.Show(MissionCue)` receives message, highlight and camera cues; `runner.Objectives` and `runner.Log` feed the objective panel.
- `MissionInputs` is the headless scripted player. `ConditionEvaluator` is the pure condition logic.

## Replays and host call order

Every command the runner and the scripted inputs submit goes through a submit sink (default `sim.Submit`); pass `ReplayRecorder.Submit` to record. The recorder refuses a simulation that already holds queued commands, so a host calls, in this order:

1. `sim = mission.CreateSimulation(data)`, then `sim.Start()`.
2. `recorder = new ReplayRecorder(sim, mission.PlayerSetups())`.
3. `runner = new MissionRunner(mission, presenter, recorder.Submit)`, then `runner.Start(sim)` (the setup commands go through the sink).
4. Each tick: `inputs.BeforeStep(sim, runner, recorder.Submit)` (headless only), AI `ThinkAll`, `sim.Step()`, `recorder.AfterStep()`, `runner.AfterStep(sim)` (exactly once per step; a repeat throws).
5. At the end `recorder.Finish()`. The replay verifies with no mission runner attached. `rts/tools/MissionRunner --replay <file>` does this (not yet for missions with AI slots).

## Single-player only

The mission runner is single-player only for now. In a lockstep match each peer would run it with its own camera and selection signals and submit outside the peers' command bundles, which would desync.
