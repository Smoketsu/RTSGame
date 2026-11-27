// rts/tools/BalanceHarness/ResponseSeeded.cs
// Seeded response runs (spec: rts/docs/specs/response_suite_guard.md RG-1): --response --profile <id> --seeds N [--seed B] [--jobs J] [--out <file>] plays every selected scenario of
// ai_eval.json N times, game g with match seed SeedDerivation.GameSeed(B, map, script stem, g), one worker process per (scenario, g) through WorkerPool (so RTS_WORKER_CACHE
// resume works), and reports in (response_tests order, g) order whatever the completion order. --out writes one JSON line per game for ResponseGuard.
// A worker (--worker --response ... --game g --seed S) prints the RESPONSE line and the JSON record. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rts.Ai;
using Rts.Sim.Data;
using Rts.Sim.Factions;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Seeded response runs: seed derivation, the worker, the coordinator and the JSON Lines record.</summary>
    public static class ResponseSeeded
    {
        /// <summary>The match seed of game <paramref name="game"/> of a scenario: SeedDerivation.GameSeed(base, the script's map ID, the script file name without ".json", game).</summary>
        /// <param name="data">The loaded data set (resolves the script's map ID, the first map when the script names none).</param>
        /// <param name="dataDirectory">The data folder holding scripts/ai_eval.</param>
        /// <param name="test">The scenario entry.</param>
        /// <param name="baseSeed">Base seed B. Range: any ulong.</param>
        /// <param name="game">Game index g. Unit: games. Range: 0 upward.</param>
        public static ulong DerivedSeed(DataSet data, string dataDirectory, ResponseTest test, ulong baseSeed, int game)
        {
            // Script file path.
            string path = Path.Combine(dataDirectory, ResponseRunner.ScriptFolder, test.Script);
            if (!File.Exists(path))
            {
                throw new HarnessException("response script not found: " + path);
            }
            // The parsed script (only its map ID is used).
            CommandScript script = CommandScript.Parse(File.ReadAllText(path), ResponseRunner.ScriptFolder + "/" + test.Script, BuiltInMechanics.ScriptParsers);
            return SeedDerivation.GameSeed(baseSeed, data.RequireMap(script.MapId).Id, Path.GetFileNameWithoutExtension(test.Script), game);
        }

        /// <summary>The report line of a seeded game: the single-game line with " seed=&lt;g&gt;" appended.</summary>
        /// <param name="result">The game's result.</param>
        /// <param name="game">Game index g. Unit: games. Range: 0 upward.</param>
        public static string Line(ResponseResult result, int game)
        {
            return result.Line() + " seed=" + game.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The JSON Lines record of one game (no trailing newline), keys in a fixed order.</summary>
        /// <param name="result">The game's result.</param>
        /// <param name="game">Game index g. Unit: games. Range: 0 upward.</param>
        /// <param name="seed">The match seed played. Range: any ulong.</param>
        public static string Record(ResponseResult result, int game, ulong seed)
        {
            // The record being built.
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"script\":").Append(Quote(result.Test.Script)).Append(",\"check\":").Append(Quote(result.Test.Check))
                .Append(",\"game\":").Append(game.ToString(CultureInfo.InvariantCulture))
                .Append(",\"seed\":").Append(Quote(seed.ToString(CultureInfo.InvariantCulture)))
                .Append(",\"profile\":").Append(Quote(result.Slot0Profile))
                .Append(",\"content_digest\":").Append(Quote(result.ContentDigest.ToString("x16", CultureInfo.InvariantCulture)))
                .Append(",\"passed\":").Append(result.Passed ? "true" : "false").Append(",\"parts\":[");
            for (int i = 0; i < result.Parts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("{\"number\":").Append(result.Parts[i].Number.ToString(CultureInfo.InvariantCulture)).Append(",\"passed\":").Append(result.Parts[i].Passed ? "true" : "false")
                    .Append(",\"metric\":").Append(Quote(result.Parts[i].Metric)).Append('}');
            }
            sb.Append("],\"final_hash\":").Append(Quote(result.FinalHash.ToString("x16", CultureInfo.InvariantCulture))).Append('}');
            return sb.ToString();
        }

        /// <summary>A JSON string literal of <paramref name="text"/> (backslash, quote and control characters escaped).</summary>
        /// <param name="text">Text to quote.</param>
        private static string Quote(string text)
        {
            // The literal being built.
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in text)
            {
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Worker: plays the one game named by --only (script), --game (g) and --seed (the derived match seed) and prints the RESPONSE line and the record. Exit 0 whether or not the game passed.</summary>
        /// <param name="options">Parsed worker options.</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: two lines.</param>
        public static int RunWorker(BatchOptions options, DataSet data, TextWriter output)
        {
            if (options.Only.Length == 0 || options.Seed == null || options.Profile.Length == 0)
            {
                throw new HarnessException("a response worker needs --only, --seed and --profile. " + BatchOptions.Usage);
            }
            // The scenario entry named by --only, or null.
            ResponseTest? test = Find(AiEvalSettings.Load(options.DataDirectory), options.Only);
            if (test == null)
            {
                throw new HarnessException("no response test matches --only '" + options.Only + "'");
            }
            // The game played.
            ResponseResult result = ResponseRunner.Run(data, options.DataDirectory, test, options.Profile, true, options.Seed);
            output.Write(Line(result, options.TraceGame) + "\n" + Record(result, options.TraceGame, options.Seed.Value) + "\n");
            output.Flush();
            return 0;
        }

        /// <summary>The entry whose script is <paramref name="script"/>, or null.</summary>
        /// <param name="settings">The loaded ai_eval.json.</param>
        /// <param name="script">Script file name.</param>
        private static ResponseTest? Find(AiEvalSettings settings, string script)
        {
            foreach (ResponseTest test in settings.ResponseTests)
            {
                if (test.Script == script)
                {
                    return test;
                }
            }
            return null;
        }

        /// <summary>Coordinator: plays N seeded games per selected scenario in worker processes, prints the lines, the rates and the summary, and writes --out. Exit 0 all passed, 2 any failed, 1 error.</summary>
        /// <param name="options">Parsed options (--response, --profile, --seeds, --seed, --jobs, --out, --only).</param>
        /// <param name="data">The loaded data set (profile check).</param>
        /// <param name="output">stdout: RESPONSE lines, RESPONSE_RATE lines, RESPONSE_SUMMARY.</param>
        public static int RunCoordinator(BatchOptions options, DataSet data, TextWriter output)
        {
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            // Fails early (exit 1) when the profile is missing.
            AiProfile.FromDataSet(data, options.Profile);
            // Games per scenario. Unit: games. Range: 1 to 64.
            int games = options.Seeds!.Value;
            // Base seed B. Range: any ulong.
            ulong baseSeed = options.Seed ?? settings.ResponseGuardBaseSeed;
            // Selected scenarios in file order, and one worker argument list per (scenario, game) in the same order.
            List<ResponseTest> tests = new List<ResponseTest>();
            List<IReadOnlyList<string>> jobs = new List<IReadOnlyList<string>>();
            foreach (ResponseTest test in settings.ResponseTests)
            {
                if (options.Only.Length > 0 && test.Script != options.Only)
                {
                    continue;
                }
                tests.Add(test);
                for (int g = 0; g < games; g++)
                {
                    jobs.Add(new[] { "--response", "--data", options.DataDirectory, "--profile", options.Profile, "--only", test.Script, "--game", g.ToString(CultureInfo.InvariantCulture),
                        "--seed", DerivedSeed(data, options.DataDirectory, test, baseSeed, g).ToString(CultureInfo.InvariantCulture) });
                }
            }
            if (tests.Count == 0)
            {
                throw new HarnessException("no response test matches --only '" + options.Only + "'");
            }
            // Most worker processes at once. Unit: processes. Range: 1 to 64.
            int parallel = options.Jobs ?? Math.Max(1, Math.Min(64, Environment.ProcessorCount));
            // Worker results in job order.
            WorkerResult[] results = WorkerPool.RunAll(jobs, parallel, null);
            // The JSON Lines file text, in (scenario, game) order.
            StringBuilder records = new StringBuilder();
            // Games that passed over all scenarios. Unit: games.
            int passedAll = 0;
            for (int t = 0; t < tests.Count; t++)
            {
                // Games of this scenario that passed. Unit: games. Range: 0 to games.
                int passed = 0;
                for (int g = 0; g < games; g++)
                {
                    // Result of game g of scenario t.
                    WorkerResult result = results[t * games + g];
                    if (result.ExitCode != 0 || result.Lines.Count != 2)
                    {
                        throw new HarnessException("response worker failed for " + tests[t].Script + " game " + g.ToString(CultureInfo.InvariantCulture) + " (exit "
                            + result.ExitCode.ToString(CultureInfo.InvariantCulture) + "): " + result.Error.Trim());
                    }
                    output.Write(result.Lines[0] + "\n");
                    records.Append(result.Lines[1]).Append('\n');
                    if (JsonReader.Parse(result.Lines[1], "response worker record").RequireBool("passed"))
                    {
                        passed++;
                    }
                }
                output.Write("RESPONSE_RATE " + tests[t].Script + " passed=" + passed.ToString(CultureInfo.InvariantCulture) + " of=" + games.ToString(CultureInfo.InvariantCulture) + "\n");
                passedAll += passed;
            }
            // Games played in all. Unit: games.
            int total = tests.Count * games;
            output.Write("RESPONSE_SUMMARY passed=" + passedAll.ToString(CultureInfo.InvariantCulture) + " of=" + total.ToString(CultureInfo.InvariantCulture) + "\n");
            output.Flush();
            if (options.OutGiven)
            {
                File.WriteAllText(options.OutDirectory, records.ToString());
            }
            return passedAll == total ? 0 : 2;
        }
    }
}
