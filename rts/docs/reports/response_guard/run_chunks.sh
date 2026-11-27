# rts/docs/reports/response_guard/run_chunks.sh
# Runs the Harness test group of rts/tests/Sim.Tests one test class at a time (so no command nears the two-hour limit) and appends each class's summary line to suite_chunks.md.
cd "$(dirname "$0")/../../../.." || exit 1
out=rts/docs/reports/response_guard/suite_chunks.md
for class in ResponseGuardTests ResponseTests HarnessUnitTests StatisticsTests HarnessTraceTests HarnessBatchTests LadderTests MeasurementTests ThreatMetricsTests ProductionDefenceMetricsTests EconomyMetricsTests EconomyModelTests ResearchAccountingTests ReefDefenceHarnessTests PoolTests PredictTests PredictKiteTests BehaviourTests BalanceTests HeldOutTests FightTests; do
  line=$(dotnet test rts/tests/Sim.Tests --no-build --filter "FullyQualifiedName~Rts.Sim.Tests.Harness.$class" 2>&1 | grep -E "^(Passed|Failed)!|No test matches" | tail -1)
  echo "- $class: ${line:-no summary line}" >> "$out"
done
echo "- done" >> "$out"
