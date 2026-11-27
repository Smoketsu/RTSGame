// rts/tools/BalanceHarness/ResponseGuard.cs
// The response-suite guard RG (spec: rts/docs/specs/response_suite_guard.md 3.1 and RG-2): --response-guard --from-a <baseline.jsonl> --from-b <candidate.jsonl> joins two seeded response
// runs (ResponseSeeded records) on (script, game) and counts, per scenario s, B_s = games the baseline passed and the candidate failed and C_s = the reverse. RG fails when for some
// scenario the exact one-sided sign test of B_s against C_s is at or below response_guard_scenario_alpha (RG-a), or when the same test on the sums is at or below
// response_guard_suite_alpha (RG-b). Integer arithmetic only (BigInteger), alphas in parts per 10 000 from ai_eval.json. Exit 0 holds, 2 fails, 1 error. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The guard verdict over two seeded response runs.</summary>
    public static class ResponseGuard
    {
        /// <summary>One game of one run: the fields the guard reads.</summary>
        private sealed class Game
        {
            /// <summary>Match seed as decimal text. Range: ulong text.</summary>
            public string Seed = string.Empty;

            /// <summary>True when the game passed its check.</summary>
            public bool Passed;
        }

        /// <summary>Exact one-sided sign test: the chance of at least <paramref name="b"/> of b + c discordant pairs going the baseline's way at p = 1/2.</summary>
        /// <param name="b">Pairs the baseline passed and the candidate failed. Unit: pairs. Range: 0 upward.</param>
        /// <param name="c">Pairs the candidate passed and the baseline failed. Unit: pairs. Range: 0 upward.</param>
        /// <param name="alphaPt">Level in parts per 10 000. Unit: parts per 10 000. Range: 1 to 5000.</param>
        /// <param name="pPt">The p-value in parts per 10 000, rounded half up. Unit: parts per 10 000. Range: 0 to 10000.</param>
        /// <returns>True when p is at or below the level (exact: tail x 10000 not above alpha_pt x 2^n).</returns>
        public static bool OneSided(int b, int c, int alphaPt, out int pPt)
        {
            // Discordant pairs.
            int n = b + c;
            // Tail count: sum over k = b..n of C(n, k).
            BigInteger tail = BigInteger.Zero;
            for (int k = b; k <= n; k++)
            {
                tail += PairedStats.Choose(n, k);
            }
            // 2^n.
            BigInteger denominator = BigInteger.Pow(2, n);
            pPt = (int)((tail * 20000 + denominator) / (denominator * 2));
            return tail * 10000 <= alphaPt * denominator;
        }

        /// <summary>Reads one JSON Lines file into scripts in first-appearance order and their games by index.</summary>
        /// <param name="path">File path.</param>
        /// <param name="scripts">Script names in first-appearance order.</param>
        private static SortedDictionary<string, SortedDictionary<int, Game>> Read(string path, List<string> scripts)
        {
            if (!File.Exists(path))
            {
                throw new HarnessException("response records not found: " + path);
            }
            // Games by script then game index.
            SortedDictionary<string, SortedDictionary<int, Game>> byScript = new SortedDictionary<string, SortedDictionary<int, Game>>(StringComparer.Ordinal);
            // Line number being read. Unit: lines. Range: 1 upward.
            int number = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                number++;
                if (line.Trim().Length == 0)
                {
                    continue;
                }
                // The parsed record.
                JsonValue root = JsonReader.Parse(line, path + " line " + number.ToString(CultureInfo.InvariantCulture));
                // Script name and game index of the record.
                string script = root.RequireString("script");
                int index = root.RequireInt("game", 0, int.MaxValue);
                if (!byScript.TryGetValue(script, out SortedDictionary<int, Game>? games))
                {
                    games = new SortedDictionary<int, Game>();
                    byScript[script] = games;
                    scripts.Add(script);
                }
                if (games.ContainsKey(index))
                {
                    throw new HarnessException(path + ": duplicate record " + script + " game " + index.ToString(CultureInfo.InvariantCulture));
                }
                games[index] = new Game { Seed = root.RequireString("seed"), Passed = root.RequireBool("passed") };
            }
            return byScript;
        }

        /// <summary>CLI entry: prints the per-scenario table, the pooled test and the verdict.</summary>
        /// <param name="options">Parsed options (--data for the alphas, --from-a, --from-b).</param>
        /// <param name="output">stdout: RESPONSE_GUARD_SCENARIO lines, RESPONSE_GUARD_SUITE and the verdict.</param>
        public static int RunCli(BatchOptions options, TextWriter output)
        {
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            // Scripts of each file in first-appearance order, and the games of each file.
            List<string> scriptsA = new List<string>();
            List<string> scriptsB = new List<string>();
            SortedDictionary<string, SortedDictionary<int, Game>> a = Read(options.FromA, scriptsA);
            SortedDictionary<string, SortedDictionary<int, Game>> b = Read(options.FromB, scriptsB);
            // Every (script, game) of one file must be in the other, with the same seed.
            Require(a, b, options.FromB, true);
            Require(b, a, options.FromA, false);
            // Reasons the guard fails, and the pooled counts. Unit: pairs.
            List<string> reasons = new List<string>();
            int sumB = 0;
            int sumC = 0;
            StringBuilder report = new StringBuilder();
            foreach (string script in scriptsA)
            {
                // Passes of each side, and the discordant counts of this scenario. Unit: games.
                int passA = 0;
                int passB = 0;
                int onlyA = 0;
                int onlyB = 0;
                foreach (KeyValuePair<int, Game> pair in a[script])
                {
                    // The candidate's game of the same pair.
                    Game other = b[script][pair.Key];
                    passA += pair.Value.Passed ? 1 : 0;
                    passB += other.Passed ? 1 : 0;
                    onlyA += pair.Value.Passed && !other.Passed ? 1 : 0;
                    onlyB += !pair.Value.Passed && other.Passed ? 1 : 0;
                }
                sumB += onlyA;
                sumC += onlyB;
                // p-value of the scenario test, and whether RG-a fires.
                bool collapse = OneSided(onlyA, onlyB, settings.ResponseGuardScenarioAlphaPt, out int pPt);
                report.Append("RESPONSE_GUARD_SCENARIO ").Append(script).Append(" baseline_passed=").Append(passA.ToString(CultureInfo.InvariantCulture))
                    .Append(" candidate_passed=").Append(passB.ToString(CultureInfo.InvariantCulture)).Append(" of=").Append(a[script].Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" B=").Append(onlyA.ToString(CultureInfo.InvariantCulture)).Append(" C=").Append(onlyB.ToString(CultureInfo.InvariantCulture))
                    .Append(" p_pt=").Append(pPt.ToString(CultureInfo.InvariantCulture)).Append(collapse ? " RG-a" : string.Empty).Append('\n');
                if (collapse)
                {
                    reasons.Add("RG-a:" + script);
                }
            }
            // RG-b on the sums.
            bool worse = OneSided(sumB, sumC, settings.ResponseGuardSuiteAlphaPt, out int suitePt);
            report.Append("RESPONSE_GUARD_SUITE B=").Append(sumB.ToString(CultureInfo.InvariantCulture)).Append(" C=").Append(sumC.ToString(CultureInfo.InvariantCulture))
                .Append(" p_pt=").Append(suitePt.ToString(CultureInfo.InvariantCulture)).Append(worse ? " RG-b" : string.Empty).Append('\n');
            if (worse)
            {
                reasons.Add("RG-b");
            }
            report.Append(reasons.Count == 0 ? "RESPONSE_GUARD HOLDS" : "RESPONSE_GUARD FAILS " + string.Join(" ", reasons)).Append('\n');
            output.Write(report.ToString());
            output.Flush();
            return reasons.Count == 0 ? 0 : 2;
        }

        /// <summary>Throws a HarnessException naming the first record of <paramref name="from"/> that <paramref name="other"/> lacks or plays with another seed.</summary>
        /// <param name="from">Records to check.</param>
        /// <param name="other">Records that must contain every one of them.</param>
        /// <param name="otherPath">File name of <paramref name="other"/> for the message.</param>
        /// <param name="checkSeed">True to compare seeds too (done once, on the first direction).</param>
        private static void Require(SortedDictionary<string, SortedDictionary<int, Game>> from, SortedDictionary<string, SortedDictionary<int, Game>> other, string otherPath, bool checkSeed)
        {
            foreach (KeyValuePair<string, SortedDictionary<int, Game>> script in from)
            {
                foreach (KeyValuePair<int, Game> game in script.Value)
                {
                    if (!other.TryGetValue(script.Key, out SortedDictionary<int, Game>? games) || !games.TryGetValue(game.Key, out Game? match))
                    {
                        throw new HarnessException("missing record " + script.Key + " game " + game.Key.ToString(CultureInfo.InvariantCulture) + " in " + otherPath);
                    }
                    if (checkSeed && match.Seed != game.Value.Seed)
                    {
                        throw new HarnessException("seed mismatch for record " + script.Key + " game " + game.Key.ToString(CultureInfo.InvariantCulture) + ": " + game.Value.Seed + " against " + match.Seed);
                    }
                }
            }
        }
    }
}
