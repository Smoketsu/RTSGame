// rts/tools/BalanceHarness/PredictSettings.cs
// Reads rts/data/scripts/fights/predict.json (spec: rts/docs/specs/balance_measurement.md C.8): the profile that supplies the predictor numbers, the calibration bin edges, the row-size
// limits and the proposed thresholds of the decision rule (C.7). The file lives under scripts/ so DataLoader skips it (outside the content digest). Unknown keys are errors and every error
// names the file and the field. Fractions are kept as integer parts per 10 000 ("pt", exact for any value written with at most 4 decimals) so the decision rule is computed in integers;
// the bin edges are Fix64. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Sim.Core;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The parsed predict.json. Immutable after <see cref="Parse"/>.</summary>
    public sealed class PredictSettings
    {
        /// <summary>Source name used in every error message. Range: fixed path relative to rts/data.</summary>
        public const string SourceName = "scripts/fights/predict.json";

        /// <summary>Path of the file relative to the data folder. Range: fixed.</summary>
        public const string RelativePath = "scripts/fights/predict.json";

        /// <summary>Profile whose predictor numbers are used. Range: a file name under rts/data/ai without .json.</summary>
        public string PredictorProfile { get; private set; } = string.Empty;

        /// <summary>Lower edges of the calibration bins. Unit: ratio. Range: 2 to 12 entries, ascending, 1 to 10000, the first is 1.</summary>
        public IReadOnlyList<Fix64> MarginBinEdges { get; private set; } = Array.Empty<Fix64>();

        /// <summary>Decided fights a row needs to count as not small. Unit: decided fights. Range: 1 to 1000.</summary>
        public int MinFightsPerRow { get; private set; }

        /// <summary>Decided fights with a mechanic present that its accuracy needs to enter the decision rule. Unit: decided fights. Range: 1 to 1000.</summary>
        public int MechanicMinFights { get; private set; }

        /// <summary>SIMULATE below this headline accuracy. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int SimulateBelowAccuracyPt { get; private set; }

        /// <summary>SIMULATE below this accuracy on fights with no unmodelled mechanic. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int SimulateBelowCleanAccuracyPt { get; private set; }

        /// <summary>SIMULATE when a bin's win share is below a lower bin's by more than this. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int CalibrationMaxInversionPt { get; private set; }

        /// <summary>FIX_FORMULA needs every judged mechanic at or above this accuracy. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int FixMinMechanicAccuracyPt { get; private set; }

        /// <summary>Under FIX_FORMULA the mechanics below this accuracy are listed to be added. Unit: parts per 10 000. Range: 0 to 10000.</summary>
        public int FixMinAccuracyPt { get; private set; }

        /// <summary>Reads and parses the file from <paramref name="dataDirectory"/>; throws <see cref="HarnessException"/> when it is missing.</summary>
        /// <param name="dataDirectory">The data folder (rts/data).</param>
        public static PredictSettings Load(string dataDirectory)
        {
            // Full path of the settings file.
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path))
            {
                throw new HarnessException("settings file not found: " + path);
            }
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Parses the text of predict.json.</summary>
        /// <param name="text">File text (comments allowed).</param>
        public static PredictSettings Parse(string text)
        {
            JsonValue root = JsonReader.Parse(text, SourceName);
            root.RejectUnknownKeys("predictor_profile", "margin_bin_edges", "min_fights_per_row", "mechanic_min_fights", "simulate_below_accuracy", "simulate_below_clean_accuracy",
                "calibration_max_inversion", "fix_min_mechanic_accuracy", "fix_min_accuracy");
            PredictSettings s = new PredictSettings();
            s.PredictorProfile = root.RequireNonEmptyString("predictor_profile");
            // The edge list node, checked for size, order and the first edge.
            JsonValue edges = root.Require("margin_bin_edges");
            if (edges.Items.Count < 2 || edges.Items.Count > 12)
            {
                throw edges.Fail("must hold 2 to 12 entries (found " + edges.Items.Count.ToString(CultureInfo.InvariantCulture) + ")");
            }
            // Edges read so far. Unit: ratio.
            List<Fix64> list = new List<Fix64>();
            foreach (JsonValue item in edges.Items)
            {
                // The edge as Fix64, from decimal text (no floating point).
                Fix64 edge;
                try
                {
                    edge = Fix64.Parse(item.AsString());
                }
                catch (FormatException ex)
                {
                    throw item.Fail("is not a decimal number (" + ex.Message + ")");
                }
                if (edge < Fix64.One || edge > Fix64.FromInt(10000))
                {
                    throw item.Fail("must be 1 to 10000 (found " + edge + ")");
                }
                if (list.Count == 0 && edge != Fix64.One)
                {
                    throw item.Fail("the first edge must be 1 (found " + edge + ")");
                }
                if (list.Count > 0 && edge <= list[list.Count - 1])
                {
                    throw item.Fail("must be above the edge before it (found " + edge + ")");
                }
                list.Add(edge);
            }
            s.MarginBinEdges = list;
            s.MinFightsPerRow = root.RequireInt("min_fights_per_row", 1, 1000);
            s.MechanicMinFights = root.RequireInt("mechanic_min_fights", 1, 1000);
            s.SimulateBelowAccuracyPt = ReadPt(root, "simulate_below_accuracy");
            s.SimulateBelowCleanAccuracyPt = ReadPt(root, "simulate_below_clean_accuracy");
            s.CalibrationMaxInversionPt = ReadPt(root, "calibration_max_inversion");
            s.FixMinMechanicAccuracyPt = ReadPt(root, "fix_min_mechanic_accuracy");
            s.FixMinAccuracyPt = ReadPt(root, "fix_min_accuracy");
            return s;
        }

        /// <summary>A fraction member with at most 4 decimals, as parts per 10 000 (exact: no floating point); also used by <see cref="AiEvalSettings"/>.</summary>
        /// <param name="root">The settings object.</param>
        /// <param name="key">Member name.</param>
        /// <param name="min">Smallest allowed value as decimal text. Range: "0" upward.</param>
        /// <param name="max">Largest allowed value as decimal text. Range: up to "1".</param>
        internal static int ReadPt(JsonValue root, string key, string min = "0", string max = "1")
        {
            JsonValue node = root.Require(key);
            // Value as Fix64 (range-checked), and its written text for the decimals check.
            Fix64 value = node.AsFix64(Fix64.Parse(min), Fix64.Parse(max));
            // Position of the decimal point in the text; -1 for a whole number. Range: -1 upward.
            int dot = node.NumberText.IndexOf('.');
            if (dot >= 0 && node.NumberText.Length - dot - 1 > 4)
            {
                throw node.Fail("must have at most 4 decimals (found " + node.NumberText + ")");
            }
            return (value * 10000).RoundToInt();
        }
    }
}
