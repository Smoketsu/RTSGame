<!-- rts/src/Campaign/README.md: the engine-free campaign scripting library (assembly Rts.Campaign): what it contains and the host contract. Spec: rts/docs/specs/campaign_scripting.md. -->
# Rts.Campaign

Missions as data, run beside the simulation. Engine-free, `netstandard2.1`, no floating point, no file access (hosts pass text).

- `MissionLoader.Parse(text, path, data, BuiltInMechanics.ScriptParsers)` gives an immutable `MissionDefinition` or throws `DataValidationException` naming the file and field.
- `MissionRunner` reads `sim.Events` and read-only state after each step and calls only `sim.Submit` (as the script issuer 255 and `script` slots). Host loop: `mission.CreateSimulation(data)`, `sim.Start()`, `runner.Start(sim)`, then per tick `ReportSignal(...)` (camera and selection, from Unity or `MissionInputs`), `sim.Step()`, `runner.AfterStep(sim)`; stop when `runner.Status != Running`.
- `IMissionPresenter.Show(MissionCue)` receives message, highlight and camera cues; `runner.Objectives` and `runner.Log` feed the objective panel.
- `MissionInputs` is the headless scripted player. `ConditionEvaluator` is the pure condition logic.
