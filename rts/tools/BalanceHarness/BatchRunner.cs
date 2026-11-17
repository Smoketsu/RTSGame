// rts/tools/BalanceHarness/BatchRunner.cs
// Batch mode of the balance harness (spec: rts/docs/specs/balance_harness.md sections 4 and 5). The coordinator (--batch) resolves maps and matchups,
// splits every cell into chunks, runs them and the HQ floor trials in worker processes (this same assembly with --worker), sorts the records,
// re-runs the first games of every cell for the determinism spot check and writes report.md, results.jsonl and hq_floor.jsonl. A worker
// (--worker) plays its games and prints one JSON line per record. Exit codes: 0 pass or deferred, 1 error, 2 hard limit failed, 3 determinism failed.
// The report never depends on --jobs or scheduling: records are sorted by (map, matchup, game) before anything else. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Coordinator and worker entry points of the batch mode.</summary>
    public static class BatchRunner
    {
        /// <summary>Runs the batch or worker mode for already parsed options.</summary>
        /// <param name="options">Parsed options.</param>
        /// <param name="output">stdout: progress lines and the final result line (coordinator) or record lines (worker).</param>
        /// <param name="error">stderr: elapsed wall time and errors.</param>
        public static int Run(BatchOptions options, TextWriter output, TextWriter error)
        {
            DataSet data = DataLoader.Load(Program.ReadDataFolder(options.DataDirectory));
            if (options.Ladder && !options.IsWorker)
            {
                return LadderRunner.Run(options, data, output, error);
            }
            if (options.Response)
            {
                return ResponseRunner.RunCli(options, data, output, error);
            }
            if (options.Trace)
            {
                return RunTrace(options, data, output);
            }
            return options.IsWorker ? RunWorker(options, data, output, error) : RunCoordinator(options, data, output, error);
        }

        // ------------------------------------------------------------------ trace

        /// <summary>
        /// --trace: plays the one game a batch would play for this map, matchup, base seed and game index (same seed derivation, AI profile and cap as
        /// <see cref="RunWorker"/>) and writes its timeline to --out (a file) or to <paramref name="output"/>.
        /// </summary>
        private static int RunTrace(BatchOptions options, DataSet data, TextWriter output)
        {
            HarnessBatchTuning batch = data.Tuning.HarnessBatch;
            // Base seed and tick limit after the command-line overrides, as in a batch.
            ulong baseSeed = options.Seed ?? (ulong)batch.BaseSeed;
            int maxSeconds = options.MaxGameSeconds ?? batch.MaxGameSeconds;
            // Seed of the game, derived exactly as the batch workers do.
            ulong seed = SeedDerivation.GameSeed(baseSeed, options.WorkerMap, options.WorkerMatchup, options.TraceGame);
            // The file written when --out names one; otherwise the timeline goes to <paramref name="output"/>. Not the default batch folder.
            bool toFile = options.OutDirectory != BatchOptions.DefaultOutDirectory;
            using (TextWriter text = toFile ? new StreamWriter(options.OutDirectory) : TextWriter.Null)
            {
                GameTrace trace = new GameTrace(toFile ? text : output, GameTrace.DefaultIntervalSeconds);
                GameRecord record = GameRunner.Play(data, options.WorkerMap, options.WorkerMatchup, options.TraceGame, seed, batch.AiProfile, batch.AiProfile, maxSeconds, trace);
                output.Write(record.ToJsonLine() + "\n");
            }
            output.Flush();
            return 0;
        }

        // ------------------------------------------------------------------ worker

        /// <summary>One worker call: a chunk of games or one HQ floor trial, one JSON line per record on <paramref name="output"/>.</summary>
        private static int RunWorker(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            if (options.TestFault == BatchOptions.FaultWorkerExit)
            {
                error.Write("test fault: worker asked to exit with code 1\n");
                return 1;
            }
            if (options.WorkerHqFloor.Length > 0)
            {
                output.Write(HqFloorTrial.Run(data, HqFloorSpec.Parse(options.WorkerHqFloor)).ToJsonLine() + "\n");
                output.Flush();
                return 0;
            }
            HarnessBatchTuning batch = data.Tuning.HarnessBatch;
            // Base seed and tick limit after the command-line overrides.
            ulong baseSeed = options.Seed ?? (ulong)batch.BaseSeed;
            int maxSeconds = options.MaxGameSeconds ?? batch.MaxGameSeconds;
            for (int game = options.WorkerFirst; game < options.WorkerFirst + options.WorkerCount; game++)
            {
                ulong seed = SeedDerivation.GameSeed(baseSeed, options.WorkerMap, options.WorkerMatchup, game);
                if (options.TestFault == BatchOptions.FaultPerturb && options.WorkerIsRerun && game == options.WorkerFirst)
                {
                    // Test switch: the re-run plays a different seed, so the spot check must report a mismatch.
                    seed ^= 1UL;
                }
                // Profiles of the two slots: the batch profile twice, or in a ladder worker the seat rule of GameRunner.LadderProfileInSlot.
                bool ladder = options.ProfileA.Length > 0;
                string profile0 = ladder ? GameRunner.LadderProfileInSlot(game, 0, options.ProfileA, options.ProfileB) : batch.AiProfile;
                string profile1 = ladder ? GameRunner.LadderProfileInSlot(game, 1, options.ProfileA, options.ProfileB) : batch.AiProfile;
                output.Write(GameRunner.Play(data, options.WorkerMap, options.WorkerMatchup, game, seed, profile0, profile1, maxSeconds).ToJsonLine() + "\n");
                output.Flush();
            }
            return 0;
        }

        // ------------------------------------------------------------------ coordinator

        /// <summary>One chunk of games of one cell.</summary>
        internal sealed class Chunk
        {
            /// <summary>Map ID.</summary>
            public string Map { get; }

            /// <summary>Matchup ID.</summary>
            public string Matchup { get; }

            /// <summary>First game index. Unit: games. Range: 0 upward.</summary>
            public int First { get; }

            /// <summary>Games in the chunk. Unit: games. Range: 1 upward.</summary>
            public int Count { get; }

            /// <summary>Games of the cell finished once this chunk and every earlier chunk of the cell are done. Unit: games.</summary>
            public int DoneAfter => First + Count;

            /// <summary>Bundles the values.</summary>
            public Chunk(string map, string matchup, int first, int count)
            {
                Map = map;
                Matchup = matchup;
                First = first;
                Count = count;
            }
        }

        /// <summary>The coordinator.</summary>
        private static int RunCoordinator(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            // Wall clock for the elapsed line on stderr only (tool code, never fed to a simulation).
            Stopwatch clock = Stopwatch.StartNew();
            HarnessBatchTuning batch = data.Tuning.HarnessBatch;
            if (data.Tuning.Hq.SurvivabilityFloorSeconds != data.Tuning.Harness.HqFloorSeconds || data.Tuning.Hq.SurvivabilityFloorSupply != data.Tuning.Harness.HqFloorSupply)
            {
                throw new HarnessException("tuning/hq.json survivability_floor_seconds must equal tuning/harness.json hq_floor_seconds");
            }
            // Settings after the command-line overrides.
            List<string> matchups = ResolveMatchups(data, options.Matchups ?? batch.Matchups);
            List<string> maps = ResolveMaps(data, options.Maps ?? batch.Maps);
            int games = options.Games ?? (options.Ci ? batch.CiGamesPerMatchupPerMap : data.Tuning.Harness.GamesPerMatchupPerMap);
            ulong baseSeed = options.Seed ?? (ulong)batch.BaseSeed;
            int maxSeconds = options.MaxGameSeconds ?? batch.MaxGameSeconds;
            int verify = Math.Min(options.Verify ?? batch.VerifyGamesPerCell, games);
            int jobs = options.Jobs ?? Math.Min(64, Math.Max(1, Environment.ProcessorCount));
            // Fails early (exit 1) when the profile file is missing.
            Rts.Ai.AiProfile.FromDataSet(data, batch.AiProfile);

            // Common worker arguments.
            List<string> common = new List<string> { "--data", options.DataDirectory, "--seed", baseSeed.ToString(CultureInfo.InvariantCulture),
                "--max-game-seconds", maxSeconds.ToString(CultureInfo.InvariantCulture), "--games", games.ToString(CultureInfo.InvariantCulture) };
            if (options.TestFault.Length > 0)
            {
                common.Add("--test-fault");
                common.Add(options.TestFault);
            }

            // Chunks in (map, matchup, first game) order, then the floor trials: job list for the pool.
            List<Chunk> chunks = new List<Chunk>();
            List<IReadOnlyList<string>> jobList = new List<IReadOnlyList<string>>();
            foreach (string map in maps)
            {
                foreach (string matchup in matchups)
                {
                    for (int first = 0; first < games; first += batch.ChunkGames)
                    {
                        Chunk chunk = new Chunk(map, matchup, first, Math.Min(batch.ChunkGames, games - first));
                        chunks.Add(chunk);
                        List<string> args = new List<string>(common) { "--map", map, "--matchup", matchup, "--first", first.ToString(CultureInfo.InvariantCulture),
                            "--count", chunk.Count.ToString(CultureInfo.InvariantCulture) };
                        jobList.Add(args);
                    }
                }
            }
            List<HqFloorSpec> specs = HqFloorTrial.Plan(data, matchups);
            foreach (HqFloorSpec spec in specs)
            {
                jobList.Add(new List<string>(common) { "--hq-floor", spec.ToArgument() });
            }

            // Progress lines are printed in chunk order as soon as the chunk and all before it are done, so stdout never depends on scheduling.
            bool[] finished = new bool[jobList.Count];
            int printed = 0;
            WorkerResult[] results = WorkerPool.RunAll(jobList, jobs, (index, all) =>
            {
                finished[index] = true;
                while (printed < chunks.Count && finished[printed])
                {
                    output.Write("cell " + chunks[printed].Map + " " + chunks[printed].Matchup + " " + chunks[printed].DoneAfter.ToString(CultureInfo.InvariantCulture)
                        + "/" + games.ToString(CultureInfo.InvariantCulture) + "\n");
                    printed++;
                }
            });
            // Records read from the chunk results, and their expected counts checked.
            List<GameRecord> records = new List<GameRecord>();
            for (int c = 0; c < chunks.Count; c++)
            {
                List<GameRecord> got = ReadGames(results[c], chunks[c].Map, chunks[c].Matchup, chunks[c].First, chunks[c].Count);
                records.AddRange(got);
            }
            List<HqFloorRecord> floors = new List<HqFloorRecord>();
            for (int s = 0; s < specs.Count; s++)
            {
                WorkerResult result = results[chunks.Count + s];
                CheckWorker(result, "HQ floor trial " + specs[s].ToArgument());
                if (result.Lines.Count != 1)
                {
                    throw new HarnessException("HQ floor trial " + specs[s].ToArgument() + " returned " + result.Lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, expected 1");
                }
                floors.Add(HqFloorRecord.Parse(result.Lines[0]));
            }
            records.Sort(CompareGames);
            floors.Sort(CompareFloors);

            // Determinism spot check: the first games of every cell run again in new processes.
            List<VerifyRow> verifyRows = new List<VerifyRow>();
            if (verify > 0)
            {
                List<IReadOnlyList<string>> reruns = new List<IReadOnlyList<string>>();
                List<string[]> cellKeys = new List<string[]>();
                foreach (string map in maps)
                {
                    foreach (string matchup in matchups)
                    {
                        cellKeys.Add(new[] { map, matchup });
                        reruns.Add(new List<string>(common) { "--map", map, "--matchup", matchup, "--first", "0", "--count", verify.ToString(CultureInfo.InvariantCulture), "--rerun" });
                    }
                }
                WorkerResult[] rerunResults = WorkerPool.RunAll(reruns, jobs, null);
                for (int k = 0; k < cellKeys.Count; k++)
                {
                    List<GameRecord> again = ReadGames(rerunResults[k], cellKeys[k][0], cellKeys[k][1], 0, verify);
                    // Mismatching games in this cell. Unit: games.
                    int mismatches = 0;
                    for (int g = 0; g < verify; g++)
                    {
                        GameRecord original = records.Find(r => r.Map == cellKeys[k][0] && r.Matchup == cellKeys[k][1] && r.Game == g)!;
                        if (original.Result != again[g].Result || original.EndTick != again[g].EndTick || original.StateHash != again[g].StateHash)
                        {
                            mismatches++;
                        }
                    }
                    verifyRows.Add(new VerifyRow(cellKeys[k][0], cellKeys[k][1], verify, mismatches));
                }
            }

            ReportInput input = new ReportInput
            {
                Data = data, BaseSeed = baseSeed, GamesPerCell = games, AiProfile = batch.AiProfile, MaxGameSeconds = maxSeconds,
                Games = records, Floors = floors, Verify = verifyRows,
            };
            Directory.CreateDirectory(options.OutDirectory);
            File.WriteAllText(Path.Combine(options.OutDirectory, "report.md"), ReportWriter.Build(input), new UTF8Encoding(false));
            StringBuilder gameLines = new StringBuilder();
            foreach (GameRecord record in records)
            {
                gameLines.Append(record.ToJsonLine()).Append('\n');
            }
            File.WriteAllText(Path.Combine(options.OutDirectory, "results.jsonl"), gameLines.ToString(), new UTF8Encoding(false));
            StringBuilder floorLines = new StringBuilder();
            foreach (HqFloorRecord record in floors)
            {
                floorLines.Append(record.ToJsonLine()).Append('\n');
            }
            File.WriteAllText(Path.Combine(options.OutDirectory, "hq_floor.jsonl"), floorLines.ToString(), new UTF8Encoding(false));

            // Verdict: a determinism failure wins over a failed hard limit.
            bool deterministic = true;
            foreach (VerifyRow row in verifyRows)
            {
                deterministic &= row.Mismatches == 0;
            }
            bool limitFailed = ReportWriter.EvaluateFloor(input).Result == ReportWriter.Fail || ReportWriter.EvaluateMedianCap(input).Result == ReportWriter.Fail;
            output.Write("HARNESS RESULT=" + (!deterministic ? "DETERMINISM_FAIL" : limitFailed ? "FAIL" : "PASS") + "\n");
            output.Flush();
            error.Write("elapsed " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s\n");
            return !deterministic ? 3 : limitFailed && !options.ReportOnly ? 2 : 0;
        }

        /// <summary>Checks the worker's exit code; throws <see cref="HarnessException"/> carrying its stderr when it failed.</summary>
        internal static void CheckWorker(WorkerResult result, string what)
        {
            if (result.ExitCode != 0)
            {
                throw new HarnessException("worker for " + what + " failed with exit code " + result.ExitCode.ToString(CultureInfo.InvariantCulture) + ": " + result.Error.Trim());
            }
        }

        /// <summary>Reads and checks the game records of one worker call: exit code 0, the expected count and the expected cell and game indices.</summary>
        internal static List<GameRecord> ReadGames(WorkerResult result, string map, string matchup, int first, int count)
        {
            // What the worker was asked for, for messages.
            string what = "games " + first.ToString(CultureInfo.InvariantCulture) + "+" + count.ToString(CultureInfo.InvariantCulture) + " of " + map + " " + matchup;
            CheckWorker(result, what);
            if (result.Lines.Count != count)
            {
                throw new HarnessException("worker for " + what + " returned " + result.Lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, expected " + count.ToString(CultureInfo.InvariantCulture)
                    + (result.Error.Length > 0 ? ": " + result.Error.Trim() : string.Empty));
            }
            List<GameRecord> list = new List<GameRecord>();
            for (int i = 0; i < count; i++)
            {
                GameRecord record = GameRecord.Parse(result.Lines[i]);
                if (record.Map != map || record.Matchup != matchup || record.Game != first + i)
                {
                    throw new HarnessException("worker for " + what + " returned a record for another game: " + result.Lines[i]);
                }
                list.Add(record);
            }
            return list;
        }

        /// <summary>Orders game records by map, matchup, game (ordinal).</summary>
        internal static int CompareGames(GameRecord a, GameRecord b)
        {
            int c = string.CompareOrdinal(a.Map, b.Map);
            if (c == 0)
            {
                c = string.CompareOrdinal(a.Matchup, b.Matchup);
            }
            return c != 0 ? c : a.Game.CompareTo(b.Game);
        }

        /// <summary>Orders floor records by attacker, unit, defender HQ, state (Rooted first).</summary>
        private static int CompareFloors(HqFloorRecord a, HqFloorRecord b)
        {
            int c = string.CompareOrdinal(a.AttackerFaction, b.AttackerFaction);
            if (c == 0)
            {
                c = string.CompareOrdinal(a.Unit, b.Unit);
            }
            if (c == 0)
            {
                c = string.CompareOrdinal(a.DefenderHq, b.DefenderHq);
            }
            return c != 0 ? c : -string.CompareOrdinal(a.HqState, b.HqState);
        }

        /// <summary>
        /// Validates matchup IDs: "a_vs_b" with a before or equal to b in ordinal order, both factions present with an hq_unit. Duplicates are dropped and the
        /// result is in ordinal order. Throws <see cref="HarnessException"/> (exit 1) with a hint for a reversed pair.
        /// </summary>
        public static List<string> ResolveMatchups(DataSet data, IReadOnlyList<string> ids)
        {
            SortedSet<string> sorted = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                string[] sides = GameRunner.SplitMatchup(id);
                if (string.CompareOrdinal(sides[0], sides[1]) > 0)
                {
                    throw new HarnessException("matchup '" + id + "' must list its factions in ordinal order; use '" + sides[1] + "_vs_" + sides[0] + "'");
                }
                foreach (string side in sides)
                {
                    if (!data.Content.Factions.TryGetValue(side, out Rts.Sim.Factions.FactionDefinition? faction))
                    {
                        throw new HarnessException("matchup '" + id + "': unknown faction '" + side + "'");
                    }
                    if (faction.HqUnitId == null)
                    {
                        throw new HarnessException("matchup '" + id + "': faction '" + side + "' has no hq_unit");
                    }
                }
                sorted.Add(id);
            }
            return new List<string>(sorted);
        }

        /// <summary>Validates map IDs (each must exist and have exactly 2 spawns) or, for an empty list, returns every 2-spawn map in ordinal order.</summary>
        public static List<string> ResolveMaps(DataSet data, IReadOnlyList<string> ids)
        {
            SortedSet<string> sorted = new SortedSet<string>(StringComparer.Ordinal);
            if (ids.Count == 0)
            {
                foreach (KeyValuePair<string, MapDefinition> pair in data.Maps)
                {
                    if (pair.Value.Spawns.Count == 2)
                    {
                        sorted.Add(pair.Key);
                    }
                }
            }
            foreach (string id in ids)
            {
                if (!data.Maps.TryGetValue(id, out MapDefinition? map))
                {
                    throw new HarnessException("unknown map '" + id + "' (known: " + string.Join(", ", data.Maps.Keys) + ")");
                }
                if (map.Spawns.Count != 2)
                {
                    throw new HarnessException("map '" + id + "' has " + map.Spawns.Count.ToString(CultureInfo.InvariantCulture) + " spawns; the batch harness plays 2-spawn maps");
                }
                sorted.Add(id);
            }
            if (sorted.Count == 0)
            {
                throw new HarnessException("no map with exactly 2 spawns to play");
            }
            return new List<string>(sorted);
        }
    }
}
