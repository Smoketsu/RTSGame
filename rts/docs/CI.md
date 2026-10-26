<!-- rts/docs/CI.md -->
# Continuous integration

[![CI](https://github.com/Smoketsu/rtsgame/actions/workflows/ci.yml/badge.svg)](https://github.com/Smoketsu/rtsgame/actions/workflows/ci.yml)

This page explains `.github/workflows/ci.yml`: what each job checks, how to read a determinism failure, and how to run the same commands on your own machine. CI runs on every push to every branch and on every pull request. A newer run on the same branch or pull request cancels the older one. CI uses no secrets.

The simulation is an engine-free C# library (`rts/src/Sim`), so everything here runs with the plain .NET 8 SDK. Unity is not involved.

## What each job checks

### 1. `build-and-test`

Restores, builds and tests `rts/RtsGame.sln` in Release configuration.

- **Build.** `rts/Directory.Build.props` turns warnings into errors, so any compiler or nullability warning fails the job.
- **Tests.** Every NUnit test in `rts/tests/Sim.Tests` runs (including `DeterminismTests`).
- **Artifact.** The TRX test report (`results.trx`, artifact name `test-results-trx`) is uploaded even when tests fail, so you can open it in Visual Studio or any TRX viewer.

### 2. `determinism` (starts after `build-and-test` passes)

Proves the Phase 0 exit criterion: the same inputs always give the same simulation.

- **Headless double run.** The balance harness plays `rts/data/scripts/smoke.json` (also its default script, and the one the Unity host plays) against `rts/data` in two separate processes. Each prints one `tick=<n> hash=<16 hex>` line per state-hash interval and a final `STATE_HASH=<16 hex>` line. The job fails if either run exits non-zero, if a run does not print exactly one `STATE_HASH=` line, if the two final hashes differ, or if any per-tick line differs. On success the hash appears in the job summary.
- **NUnit determinism tests.** `dotnet test` with the filter `FullyQualifiedName~DeterminismTests` runs on its own as a named step, and fails if the filter matches no test.

### 3. `data-files`

Runs `.github/scripts/validate_jsonc.py` (Python 3 standard library only).

- Every `rts/data/**/*.json` file, which includes the command scripts in `rts/data/scripts/`, must be valid JSON once `//` and `/* */` comments are removed. Trailing commas, duplicate keys in one object, `NaN` and `Infinity` are rejected.
- Every one of those files must start with a `//` header comment line (path on line 1, as the documentation standard requires).

Failures are printed as `path:line: problem`.

## Reading a determinism failure

A determinism failure means the same script, data and seed produced two different simulations. That is always a real bug, never flakiness. It is never fixed by re-running.

1. Open the `Run the headless smoke script twice` step. The error annotation shows both hashes, then a `diff` of the two outputs. The first differing `tick=<n>` line is the earliest tick at which the states diverged. The cause is at or just before that tick.
2. If only the final `STATE_HASH=` differs and no `tick=` line does, the divergence happened after the last hashed tick. Lower `state_hash_interval_ticks` in `rts/data/tuning/match.json` locally to narrow it down.
3. If the step says `STATE_HASH missing`, the runner printed no hash line or crashed. Read the stdout and stderr dumped in the log.
4. Look for the usual causes, which are the architecture rules in the project brief:
   - iterating a `Dictionary` or `HashSet` instead of a sorted collection;
   - `float`, `double`, `System.Random`, `DateTime` or `Stopwatch` in `rts/src/Sim`;
   - state that changes the future but is missing from `StateHasher`, or hashed in a different order;
   - state kept in a static field that survives between runs;
   - reading files, threads or the clock from simulation code.
5. If the NUnit step failed but the double run passed, run `DeterminismTests` locally (below).

## Running the same commands locally

Install the .NET 8 SDK (`dotnet --version` should print `8.x`), then run from the repository root:

```bash
# build-and-test
dotnet restore rts/RtsGame.sln
dotnet build rts/RtsGame.sln --configuration Release --no-restore
dotnet test rts/RtsGame.sln --configuration Release --no-build \
  --logger "trx;LogFileName=results.trx" --results-directory rts/build/TestResults

# determinism: the headless runner twice, then compare the hashes (--script is optional; it defaults to <data>/scripts/smoke.json)
dotnet run --project rts/tools/BalanceHarness/Rts.Tools.BalanceHarness.csproj --configuration Release -- \
  --data rts/data --script rts/data/scripts/smoke.json > /tmp/run1.out
dotnet run --project rts/tools/BalanceHarness/Rts.Tools.BalanceHarness.csproj --configuration Release -- \
  --data rts/data --script rts/data/scripts/smoke.json > /tmp/run2.out
grep '^STATE_HASH=' /tmp/run1.out /tmp/run2.out   # the two hashes must be identical
diff /tmp/run1.out /tmp/run2.out                  # no output means identical

# determinism: the NUnit tests only
dotnet test rts/tests/Sim.Tests/Rts.Sim.Tests.csproj --configuration Release \
  --filter "FullyQualifiedName~DeterminismTests"

# data-files
python3 -I .github/scripts/validate_jsonc.py
```

Build output lands in `rts/build/` (see `rts/Directory.Build.props`), which is git-ignored. To get past warnings-as-errors while experimenting, add `-p:TreatWarningsAsErrors=false`; CI never does.
