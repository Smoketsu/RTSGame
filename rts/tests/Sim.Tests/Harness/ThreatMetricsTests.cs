// rts/tests/Sim.Tests/Harness/ThreatMetricsTests.cs
// Acceptance tests H-S1 to H-S6 of rts/docs/specs/ai_threat_map.md section 9 (package S-M): drop_off_life and the harass counters from hand-built event streams over a real simulation (as
// ProductionDefenceMetricsTests), the three new checks, the paired behaviour test and its stated power, observation only, and the --behaviour output with and without paired_direction.
// Part of Rts.Sim.Tests. The command tests start worker processes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Tests of the package S-M summary fields, checks and paired test.</summary>
    [TestFixture]
    public class ThreatMetricsTests
    {
        /// <summary>Player 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ticks per second of the shipped match settings. Unit: ticks per second. Range: 20.</summary>
        private const int Tps = 20;

        /// <summary>Loaded data, shared by the tests.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next sequence number of a spawn command. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A started Skai-versus-Skai greybox_1v1 simulation with each HQ on its spawn point.</summary>
        private Simulation Scene()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "A"),
                new PlayerSetup(P1, new TeamId(1), "skai", "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, new FixVec2(Fix64.Parse("8.5"), Fix64.Parse("8.5")));
            Spawn(sim, "skai.great_aerie", P1, new FixVec2(Fix64.Parse("56.5"), Fix64.Parse("56.5")));
            return sim;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, FixVec2 position)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, position));
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    return spawned.Unit;
                }
            }
            throw new AssertionException("spawn of " + definition + " failed");
        }

        /// <summary>A point of the greybox map near P0's HQ, <paramref name="dx"/> tiles east of it.</summary>
        private static FixVec2 NearP0(int dx)
        {
            return new FixVec2(Fix64.Parse("8.5") + Fix64.FromInt(dx), Fix64.Parse("12.5"));
        }

        /// <summary>A configured check from an entry written as JSON text, knowing the tick rate and a 2400 s window.</summary>
        private static IBehaviourCheck Check(string id, string entry)
        {
            IBehaviourCheck check = BehaviourChecks.Create(id, new BehaviourContext(10000, 600, null, Tps, null, 2400));
            check.ReadSettings(JsonReader.Parse(entry, "test"));
            return check;
        }

        /// <summary>A throwaway game record for checks that read only the summary.</summary>
        private static GameRecord Game()
        {
            return new GameRecord("prototype_1v1", "water_vs_water", 0, 1UL, "water", "water", "a", "b", GameRecord.Undecided, 1, 1UL, 1UL, 1UL, new List<HqKill>());
        }

        /// <summary>A synthetic summary with the given drop-off ticks, drop-off lives, harass counters and production metrics (null for none).</summary>
        private static GameSummary Summary(int[]? dropOffTicks = null, int[][]? life = null, HarassMetrics? harass = null, ProductionMetrics? production = null)
        {
            return new GameSummary(0, 0, new SortedDictionary<string, int[]>(StringComparer.Ordinal), new List<int[]?>(), new int[0][], new int[0][], new SortedDictionary<string, int>(),
                new SortedDictionary<string, int>(), new SortedDictionary<string, int>(), null, dropOffTicks, null, production, null, null, life, harass);
        }

        /// <summary>Production metrics with only the overrun second set.</summary>
        private static ProductionMetrics Overrun(int? second)
        {
            return new ProductionMetrics(0, 0, 0, 0, new List<int?>(), new List<int?>(), new List<int?>(), new List<int?>(), null, second);
        }

        /// <summary>H-S1: a depot placed at 100 s and killed by the enemy at 180 s and a depot placed at 200 s and alive give drop_off_life [[2000, 3600], [4000, -1]]; a kill by the owner's own team or of a non-drop-off is not a loss.</summary>
        [Test]
        public void HS1_DropOffLife()
        {
            Simulation sim = Scene();
            ThreatMetricsCollector collector = new ThreatMetricsCollector(int.MaxValue);
            collector.Begin(sim);
            EntityId killer = Spawn(sim, "skai.windrunner", P1, new FixVec2(Fix64.Parse("40.5"), Fix64.Parse("40.5")));
            EntityId first = Spawn(sim, "skai.perch_depot", P0, NearP0(6));
            collector.Observe(sim, 100 * Tps, new List<SimEvent>());
            collector.Observe(sim, 180 * Tps, new SimEvent[] { new UnitKilledEvent(180 * Tps, first, killer, P0, P1) });
            Spawn(sim, "skai.perch_depot", P0, NearP0(10));
            collector.Observe(sim, 200 * Tps, new List<SimEvent>());
            IReadOnlyList<int[]> life = collector.Lives()[0];
            Assert.That(life.Count, Is.EqualTo(2));
            Assert.That(life[0], Is.EqualTo(new[] { 2000, 3600 }));
            Assert.That(life[1], Is.EqualTo(new[] { 4000, -1 }));
            Assert.That(collector.Lives()[1].Count, Is.EqualTo(0), "the HQ is not a drop-off life and P1 placed none");
            // The summary writes and reads it back.
            GameSummary summary = Summary(null, new[] { life[0], life[1] });
            GameSummary parsed = GameSummary.Parse(JsonReader.Parse(summary.ToJson(), "test"));
            Assert.That(parsed.DropOffLife.Count, Is.EqualTo(2));
            Assert.That(parsed.DropOffLife[0], Is.EqualTo(new[] { 2000, 3600 }));
            Assert.That(parsed.DropOffLife[1], Is.EqualTo(new[] { 4000, -1 }));
        }

        /// <summary>H-S2: depot_losses_young with young_seconds 120: the first depot (80 s) counts; with overrun_second 170 it does not; a depot killed at 400 s after placement at 200 s (200 s) does not; a kill after the window does not.</summary>
        [Test]
        public void HS2_DepotLossesYoung()
        {
            IBehaviourCheck check = Check("depot_losses_young", "{\"enabled\": true, \"young_seconds\": 120, \"max_violation_fraction\": 1, \"paired_direction\": \"lower\"}");
            int[][] life = { new[] { 2000, 3600 }, new[] { 4000, -1 } };
            BehaviourResult plain = check.Evaluate(Summary(null, life), Game());
            Assert.That(plain.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(plain.Num, Is.EqualTo(1));
            Assert.That(check.Evaluate(Summary(null, life, null, Overrun(170)), Game()).Num, Is.EqualTo(0), "the kill at 180 s is after the overrun at 170 s");
            Assert.That(check.Evaluate(Summary(null, life, null, Overrun(181)), Game()).Num, Is.EqualTo(1), "the kill at 180 s is before an overrun at 181 s");
            Assert.That(check.Evaluate(Summary(null, life, null, Overrun(null)), Game()).Num, Is.EqualTo(1));
            Assert.That(check.Evaluate(Summary(null, new[] { new[] { 4000, 8000 } }), Game()).Num, Is.EqualTo(0), "200 s old");
            Assert.That(check.Evaluate(Summary(null, new[] { new[] { 4000, 6400 } }), Game()).Num, Is.EqualTo(1), "120 s old counts (edge inclusive)");
            Assert.That(check.Evaluate(Summary(null, new[] { new[] { 2400 * Tps - 100, 2400 * Tps + 100 } }), Game()).Num, Is.EqualTo(0), "killed after the 2400 s window");
            Assert.That(check.Evaluate(Summary(), Game()).Num, Is.EqualTo(0), "an old record has no lives");
            // young_seconds out of range and an unknown key fail naming the key.
            Assert.That(Expect.Throws<DataValidationException>(() => Check("depot_losses_young", "{\"enabled\": true, \"young_seconds\": 0, \"max_violation_fraction\": 1}")).Message, Does.Contain("young_seconds"));
            Assert.That(Expect.Throws<DataValidationException>(() => Check("depot_losses_young", "{\"enabled\": true, \"young_seconds\": 1801, \"max_violation_fraction\": 1}")).Message, Does.Contain("young_seconds"));
            Assert.That(Expect.Throws<DataValidationException>(() => Check("depot_losses_young", "{\"enabled\": true, \"young_seconds\": 5, \"max_violation_fraction\": 1, \"extra\": 1}")).Message, Does.Contain("extra"));
        }

        /// <summary>
        /// H-S3: harass attribution with a controller whose HarassGroup is {u1} from think 3: u1 kills a worker (50) and a Windrunner (75), u2 (not in the set) kills a worker, u1 dies (75):
        /// squads 1, worker_kills 1, kills_value 125, losses_value 75 and harass_trade 50; a squad that ends and starts again counts twice.
        /// </summary>
        [Test]
        public void HS3_HarassAttribution()
        {
            Simulation sim = Scene();
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive_s5");
            SkirmishAiController stub0 = new SkirmishAiController(P0, profile);
            SkirmishAiController stub1 = new SkirmishAiController(P1, profile);
            List<IPlayerController> controllers = new List<IPlayerController> { stub0, stub1 };
            EntityId u1 = Spawn(sim, "skai.windrunner", P0, NearP0(2));
            EntityId u2 = Spawn(sim, "skai.windrunner", P0, NearP0(3));
            EntityId worker1 = Spawn(sim, "skai.fledgling", P1, new FixVec2(Fix64.Parse("50.5"), Fix64.Parse("50.5")));
            EntityId worker2 = Spawn(sim, "skai.fledgling", P1, new FixVec2(Fix64.Parse("51.5"), Fix64.Parse("50.5")));
            EntityId raider = Spawn(sim, "skai.windrunner", P1, new FixVec2(Fix64.Parse("52.5"), Fix64.Parse("50.5")));
            EntityId guard = Spawn(sim, "skai.windrunner", P1, new FixVec2(Fix64.Parse("53.5"), Fix64.Parse("50.5")));
            ThreatMetricsCollector collector = new ThreatMetricsCollector(int.MaxValue);
            collector.Begin(sim);
            // Thinks 1 and 2: no squad. Think 3: the squad {u1}.
            collector.AfterThink(100, controllers);
            collector.AfterThink(110, controllers);
            stub0.Memory.HarassGroup.Add(u1);
            collector.AfterThink(120, controllers);
            collector.AfterThink(130, controllers);
            collector.Observe(sim, 140, new SimEvent[]
            {
                new UnitKilledEvent(140, worker1, u1, P1, P0),
                new UnitKilledEvent(141, raider, u1, P1, P0),
                new UnitKilledEvent(142, worker2, u2, P1, P0),
                new UnitKilledEvent(143, u1, guard, P0, P1),
            });
            HarassMetrics slot0 = collector.Harass()[0];
            Assert.That(slot0.Squads, Is.EqualTo(1));
            Assert.That(slot0.WorkerKills, Is.EqualTo(1));
            Assert.That(slot0.KillsValue, Is.EqualTo(125));
            Assert.That(slot0.LossesValue, Is.EqualTo(75));
            HarassMetrics slot1 = collector.Harass()[1];
            Assert.That(slot1.Squads + slot1.WorkerKills + slot1.KillsValue + slot1.LossesValue, Is.EqualTo(0), "the other controller has no squad (the killed raider was not its harass set)");
            IBehaviourCheck trade = Check("harass_trade", "{\"enabled\": true, \"max_violation_fraction\": 1, \"paired_direction\": \"higher\"}");
            BehaviourResult result = trade.Evaluate(Summary(null, null, slot0), Game());
            Assert.That(result.Outcome, Is.EqualTo(CheckOutcome.Held));
            Assert.That(result.Num, Is.EqualTo(50));
            IBehaviourCheck squads = Check("harass_squads", "{\"enabled\": true, \"max_violation_fraction\": 1, \"paired_direction\": \"higher\"}");
            Assert.That(squads.Evaluate(Summary(null, null, slot0), Game()).Num, Is.EqualTo(1));
            // The squad ends (empty set) and a new one starts: a second squad. A set that stays non-empty is not counted again.
            stub0.Memory.HarassGroup.Clear();
            collector.AfterThink(150, controllers);
            stub0.Memory.HarassGroup.Add(u2);
            collector.AfterThink(160, controllers);
            stub0.Memory.HarassGroup.Add(u1);
            collector.AfterThink(170, controllers);
            Assert.That(collector.Harass()[0].Squads, Is.EqualTo(2));
            // The summary writes and reads the counters back.
            GameSummary parsed = GameSummary.Parse(JsonReader.Parse(Summary(null, null, slot0).ToJson(), "test"));
            Assert.That(parsed.Harass.Squads, Is.EqualTo(1));
            Assert.That(parsed.Harass.WorkerKills, Is.EqualTo(1));
            Assert.That(parsed.Harass.KillsValue, Is.EqualTo(125));
            Assert.That(parsed.Harass.LossesValue, Is.EqualTo(75));
            // Events and thinks after the window are not counted.
            ThreatMetricsCollector windowed = new ThreatMetricsCollector(135);
            windowed.Begin(sim);
            windowed.AfterThink(100, controllers);
            windowed.Observe(sim, 140, new SimEvent[] { new UnitKilledEvent(140, worker1, u1, P1, P0) });
            Assert.That(windowed.Harass()[0].KillsValue, Is.EqualTo(0));
        }

        /// <summary>One game record of the paired layout: pair <paramref name="pair"/>, game <paramref name="game"/> (candidate in slot 0 in the first game of a pair, slot 1 in the second) with first_drop_off seconds per slot (0 for none, so Unjudged).</summary>
        private static GameRecord PairedGame(int pair, int game, int candidateSeconds, int baselineSeconds)
        {
            // Candidate slot of this game. Range: 0 or 1.
            int candidateSlot = game % 2;
            GameSummary candidate = Summary(candidateSeconds == 0 ? new int[0] : new[] { candidateSeconds * Tps });
            GameSummary baseline = Summary(baselineSeconds == 0 ? new int[0] : new[] { baselineSeconds * Tps });
            return new GameRecord("prototype_1v1", "water_vs_water", game, 1UL, "water", "water", candidateSlot == 0 ? "cand" : "base", candidateSlot == 0 ? "base" : "cand", GameRecord.Slot0, 100, 1UL, 1UL, 1UL,
                new List<HqKill>(), string.Empty, string.Empty, pair, candidateSlot, string.Empty, candidateSlot == 0 ? candidate : baseline, candidateSlot == 0 ? baseline : candidate);
        }

        /// <summary>The records of <paramref name="better"/> pairs the candidate wins (100 s against 200 s per slot), <paramref name="worse"/> it loses (300 against 200), <paramref name="ties"/> equal and <paramref name="unjudged"/> with a missing drop-off.</summary>
        private static List<GameRecord> Pairs(int better, int worse, int ties, int unjudged)
        {
            List<GameRecord> records = new List<GameRecord>();
            // Pair index. Range: 0 upward.
            int pair = 0;
            for (int i = 0; i < better + worse + ties + unjudged; i++)
            {
                int candidate = i < better ? 100 : i < better + worse ? 300 : 200;
                int baseline = 200;
                for (int game = 0; game < 2; game++)
                {
                    // The last games of the list are the unjudged ones: the candidate's slot of game 0 has no drop-off.
                    bool left = i >= better + worse + ties && game == 0;
                    records.Add(PairedGame(pair, 2 * pair + game, left ? 0 : candidate, baseline));
                }
                pair++;
            }
            return records;
        }

        /// <summary>The paired row of first_drop_off (lower is better) over the records, candidate "cand" against baseline "base".</summary>
        private static PairedCheckRow Paired(List<GameRecord> records, string direction = "lower")
        {
            IBehaviourCheck check = Check("first_drop_off", "{\"enabled\": true, \"max_seconds\": 7200, \"max_violation_fraction\": 0, \"paired_direction\": \"" + direction + "\"}");
            return BehaviourRunner.PairedTest(check, direction, records, "base", "cand", 500, 28016);
        }

        /// <summary>H-S4: the paired test on hand-made records: 9 better and 1 worse gives p 0.0215 BETTER; 8 and 2 gives p 0.1094 NO_MEASURABLE_EFFECT; 1 and 9 gives WORSE; a pair with an Unjudged slot-game is counted in unjudged_pairs and left out; the power function gives 666, 703, 786 and 1072.</summary>
        [Test]
        public void HS4_PairedTest()
        {
            PairedCheckRow nine = Paired(Pairs(9, 1, 0, 0));
            Assert.That(nine.Better, Is.EqualTo(9));
            Assert.That(nine.Worse, Is.EqualTo(1));
            Assert.That(nine.Pairs, Is.EqualTo(10));
            Assert.That(PairedStats.P4(nine.PPt), Is.EqualTo("0.0215"));
            Assert.That(nine.Verdict, Is.EqualTo("BETTER"));
            Assert.That(nine.Line(), Is.EqualTo("BEHAVIOUR_PAIRED check=first_drop_off candidate=cand baseline=base pairs=10 unjudged_pairs=0 better=9 worse=1 ties=0 p=0.0215 mde_permille=943 verdict=BETTER"));
            PairedCheckRow eight = Paired(Pairs(8, 2, 0, 0));
            Assert.That(PairedStats.P4(eight.PPt), Is.EqualTo("0.1094"));
            Assert.That(eight.Verdict, Is.EqualTo("NO_MEASURABLE_EFFECT"));
            PairedCheckRow one = Paired(Pairs(1, 9, 0, 0));
            Assert.That(one.Verdict, Is.EqualTo("WORSE"));
            // "higher" reverses better and worse.
            PairedCheckRow reversed = Paired(Pairs(9, 1, 0, 0), "higher");
            Assert.That(reversed.Better, Is.EqualTo(1));
            Assert.That(reversed.Worse, Is.EqualTo(9));
            Assert.That(reversed.Verdict, Is.EqualTo("WORSE"));
            // Ties and unjudged pairs: 9 better, 1 worse, 2 ties and 3 with a missing slot-game give pairs 12 (judged) and unjudged_pairs 3, and the same test.
            PairedCheckRow mixed = Paired(Pairs(9, 1, 2, 3));
            Assert.That(mixed.Better, Is.EqualTo(9));
            Assert.That(mixed.Worse, Is.EqualTo(1));
            Assert.That(mixed.Ties, Is.EqualTo(2));
            Assert.That(mixed.UnjudgedPairs, Is.EqualTo(3));
            Assert.That(mixed.Pairs, Is.EqualTo(12));
            Assert.That(mixed.Verdict, Is.EqualTo("BETTER"));
            Assert.That(mixed.TableRow(), Does.StartWith("| first_drop_off | cand | base | 12 | 3 | 9 | 1 | 2 | 0.0215 | "));
            // The stated power (M.2 for a share of decisive pairs).
            Assert.That(PairedStats.MdeSharePermille(72, 28016, 500), Is.EqualTo(666));
            Assert.That(PairedStats.MdeSharePermille(48, 28016, 500), Is.EqualTo(703));
            Assert.That(PairedStats.MdeSharePermille(24, 28016, 500), Is.EqualTo(786));
            Assert.That(PairedStats.MdeSharePermille(4, 28016, 500), Is.EqualTo(1072), "D' = D_min = 6");
            Assert.That(PairedStats.MdeSharePermille(36, 28016, 500), Is.EqualTo(734));
            Assert.That(PairedStats.MdeSharePermille(12, 28016, 500), Is.EqualTo(905));
            // The settings reject a direction that is neither lower nor higher, naming the key, and a check that has no paired_direction key.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), BehaviourSettings.RelativePath));
            DataValidationException bad = Expect.Throws<DataValidationException>(() => BehaviourSettings.Parse(text.Replace("\"paired_direction\": \"lower\"", "\"paired_direction\": \"sideways\"")));
            Assert.That(bad.Message, Does.StartWith(BehaviourSettings.SourceName).And.Contain("paired_direction"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => BehaviourSettings.Parse(text.Replace("\"max_share\": 0.15,", "\"max_share\": 0.15, \"paired_direction\": \"lower\",")));
            Assert.That(unknown.Message, Does.Contain("paired_direction"));
            BehaviourSettings shipped = BehaviourSettings.Load(TestData.FindDataDirectory());
            Assert.That(shipped.PairedDirections["first_drop_off"], Is.EqualTo("lower"));
            Assert.That(shipped.PairedDirections["income_by"], Is.EqualTo("higher"));
            Assert.That(shipped.PairedDirections["depot_losses_young"], Is.EqualTo("lower"));
            Assert.That(shipped.PairedDirections["harass_trade"], Is.EqualTo("higher"));
            Assert.That(shipped.PairedDirections["harass_squads"], Is.EqualTo("higher"));
            Assert.That(shipped.PairedDirections.Count, Is.EqualTo(5));
        }

        /// <summary>H-S5: a game with and without the observer has the same state hash, play signature and result; the summary carries drop_off_life and harass; a record without the new keys parses (empty lives, zero harass) and the line round-trips.</summary>
        [Test]
        public void HS5_ObservationOnly_AndOldRecordsParse()
        {
            ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", "water_vs_water", 0);
            GameRecord plain = GameRunner.Play(Data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240);
            AiEvalSettings settings = AiEvalSettings.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_eval", "ai_eval.json")));
            GameRecord watched = GameRunner.Play(Data, "prototype_1v1", "water_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 240, null, -1, -1, settings.NewObserver());
            Assert.That(watched.StateHash, Is.EqualTo(plain.StateHash));
            Assert.That(watched.PlaySignature, Is.EqualTo(plain.PlaySignature));
            Assert.That(watched.Result, Is.EqualTo(plain.Result));
            string line = watched.ToJsonLine();
            Assert.That(line, Does.Contain("\"drop_off_life\":["));
            Assert.That(line, Does.Contain("\"harass\":["));
            Assert.That(GameRecord.Parse(line).ToJsonLine(), Is.EqualTo(line));
            // The same record without the two new keys (as written before S-M).
            string old = Regex.Replace(line, ",\"drop_off_life\":\\[(\\[[-0-9,]*\\],?)*\\],\"harass\":\\[[-0-9,]*\\]", string.Empty);
            Assert.That(old, Does.Not.Contain("drop_off_life"));
            Assert.That(old, Does.Not.Contain("\"harass\""));
            GameRecord parsed = GameRecord.Parse(old);
            Assert.That(parsed.Summary0!.DropOffLife.Count, Is.EqualTo(0));
            Assert.That(parsed.Summary0.Harass.Squads, Is.EqualTo(0));
            Assert.That(parsed.Summary0.Harass.KillsValue, Is.EqualTo(0));
            Assert.That(parsed.StateHash, Is.EqualTo(watched.StateHash));
            Assert.That(watched.Summary0!.Production, Is.Not.Null, "behaviour games carry the production metrics, so overrun_second is written");
        }

        /// <summary>
        /// H-S6: a behaviour run with no paired_direction in any entry prints the BEHAVIOUR lines and the summary count only (no BEHAVIOUR_PAIRED line, no Paired table); with paired_direction on one check the line follows
        /// that check's lines and behaviour.md gets the Paired table. The new checks print ordinary BEHAVIOUR lines.
        /// </summary>
        [Test]
        public void HS6_BehaviourRun_PairedLinesOnlyWhenDeclared()
        {
            string data = HarnessTestSupport.CopyData();
            const string entries = "\"static_defence_share\": { \"enabled\": true, \"max_share\": 0.15, \"max_violation_fraction\": 0.05 }, "
                + "\"depot_losses_young\": { \"enabled\": true, \"young_seconds\": 120, \"max_violation_fraction\": 1 }, "
                + "\"harass_trade\": { \"enabled\": true, \"max_violation_fraction\": 1 }, "
                + "\"harass_squads\": { \"enabled\": true, \"max_violation_fraction\": 1";
            File.WriteAllText(Path.Combine(data, BehaviourSettings.RelativePath), "{ \"$schema_version\": 1, \"$doc\": \"x\", \"games_per_cell\": 8, \"rare_weight\": 1, \"checks\": { " + entries + " } } }");
            string folder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            string[] args = { "--data", data, "--behaviour", "--profile-a", "abl_s5_no_h", "--profile-b", "aggressive_s5", "--games", "8", "--matchups", "water_vs_water", "--max-game-seconds", "60", "--jobs", "4", "--out", folder };
            int exit = HarnessTestSupport.Run(args, out string stdout, out string stderr);
            string[] lines = stdout.TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(9), stderr + stdout);
            Assert.That(stdout, Does.Not.Contain("BEHAVIOUR_PAIRED"));
            Assert.That(lines[2], Does.StartWith("BEHAVIOUR depot_losses_young profile=aggressive_s5 games=8 held=8 violated=0 unjudged=0 "));
            Assert.That(lines[8], Does.StartWith("BEHAVIOUR_SUMMARY candidate=aggressive_s5 passed="));
            Assert.That(lines[8], Does.EndWith(" of=4"));
            Assert.That(File.ReadAllText(Path.Combine(folder, "behaviour.md")), Does.Not.Contain("## Paired"));
            Assert.That(exit, Is.AnyOf(0, 2));
            // The same run with paired_direction on harass_squads.
            File.WriteAllText(Path.Combine(data, BehaviourSettings.RelativePath), "{ \"$schema_version\": 1, \"$doc\": \"x\", \"games_per_cell\": 8, \"rare_weight\": 1, \"checks\": { " + entries + ", \"paired_direction\": \"higher\" } } }");
            string pairedFolder = Path.Combine(HarnessTestSupport.NewFolder(), "out");
            args[args.Length - 1] = pairedFolder;
            HarnessTestSupport.Run(args, out string pairedOut, out string pairedErr);
            string[] pairedLines = pairedOut.TrimEnd('\n').Split('\n');
            Assert.That(pairedLines.Length, Is.EqualTo(10), pairedErr + pairedOut);
            Assert.That(pairedLines[7], Does.StartWith("BEHAVIOUR harass_squads profile=abl_s5_no_h "));
            Assert.That(pairedLines[8], Does.Match(@"^BEHAVIOUR_PAIRED check=harass_squads candidate=aggressive_s5 baseline=abl_s5_no_h pairs=\d+ unjudged_pairs=\d+ better=\d+ worse=\d+ ties=\d+ p=\d\.\d{4} mde_permille=\d+ verdict=(BETTER|WORSE|NO_MEASURABLE_EFFECT)$"));
            Assert.That(File.ReadAllText(Path.Combine(pairedFolder, "behaviour.md")), Does.Contain("## Paired"));
        }
    }
}
