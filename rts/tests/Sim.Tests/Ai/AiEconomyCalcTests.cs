// rts/tests/Sim.Tests/Ai/AiEconomyCalcTests.cs
// Tests of package E of rts/docs/specs/ai_economy_calc.md (the AI economy by calculation): the walk-ticks extraction (EC-1), the worker cap of rule EW (EC-2, EC-3), profile parsing and the candidate
// file (EC-8, EC-9), the off path (EC-10), determinism (EC-11) and the trace column (EC-13). Rule ED's tests (EC-4 to EC-7, EC-12) are added by package E2 in this file. Part of Rts.Sim.Tests.
// Real simulations on shipped maps (greybox_1v1: field 0 (4, 10) and 1 (10, 4) are P0's main fields), AiBrain.Probe on the views, and short full games through GameRunner.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package E: worker caps by calculation, the two profile keys, the candidate profile and the trace column.</summary>
    [TestFixture]
    public class AiEconomyCalcTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ticks per second of the shipped match settings. Unit: ticks per second. Range: 20 (asserted in <see cref="Game"/>).</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A started Skai-versus-Water match on <paramref name="mapId"/>: HQs rooted at the spawns, <paramref name="workers"/> workers each, one producer each, every site explored.</summary>
        private Simulation Game(string mapId, int workers)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(mapId)));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(TicksPerSecond));
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to workers - 1.
            for (int i = 0; i < workers; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "water.tender", P1, s1 - offset);
            }
            Spawn(sim, "skai.roost", P0, s0 + new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            Spawn(sim, "water.tidepool", P1, s1 - new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            ExploreKit.ExploreAll(sim);
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

        /// <summary>The shipped profile <paramref name="id"/>.</summary>
        private static AiProfile Shipped(string id)
        {
            return AiProfile.FromDataSet(Data, id);
        }

        /// <summary>The text of a shipped profile file.</summary>
        private static string ProfileText(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json"));
        }

        /// <summary>Probe of P0's economy on the current view.</summary>
        private static AiBrain.EconomyProbe Probe(Simulation sim, AiProfile profile, AiMemory? memory = null)
        {
            return AiBrain.Probe(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory())!;
        }

        /// <summary>EC-1: the extracted walk-ticks function and the AI-E cap on its two worked examples (5 and 12) are unchanged.</summary>
        [Test]
        public void EC1_WalkTicksExtraction()
        {
            // Interact range of the worked examples. Unit: tiles.
            Fix64 interact = Fix64.Parse("1.5");
            Assert.That(AiBrain.WalkTicks(Fix64.Parse("5.4"), Fix64.Parse("1.2"), interact, Fix64.Parse("2.5"), 20), Is.EqualTo(20));
            Assert.That(AiBrain.WalkTicks(Fix64.FromInt(9), Fix64.Half, interact, Fix64.FromInt(2), 20), Is.EqualTo(110));
            Assert.That(AiBrain.WalkTicks(Fix64.FromInt(2), Fix64.Half, interact, Fix64.FromInt(2), 20), Is.EqualTo(0), "no walk beyond the interact ranges");
            Assert.That(AiBrain.WalkTicks(Fix64.FromInt(9), Fix64.Half, interact, Fix64.Zero, 20), Is.EqualTo(0), "speed 0 counts as no walk time");
            Assert.That(AiBrain.SaturationCap(3, Fix64.Parse("5.4"), Fix64.Parse("1.2"), interact, Fix64.Parse("2.5"), 20, 40, 0), Is.EqualTo(5));
            Assert.That(AiBrain.SaturationCap(3, Fix64.FromInt(9), Fix64.Half, interact, Fix64.FromInt(2), 20, 40, 0), Is.EqualTo(12));
        }

        /// <summary>EC-2: the worked examples of 2.2: caps 5, 1, 0, 13 and 0 for the five cases; Loads is 0 for a span of 0 or less; with K 0 the cap is 0.</summary>
        [Test]
        public void EC2_CalcCapWorkedExamples()
        {
            // Spec numbers: slots 3, G 40, A 5, worker cost 50, build 240 ticks, horizon 180 s = 3600 ticks.
            const int slots = 3;
            const int hold = 40;
            const int amount = 5;
            const int cost = 50;
            const int build = 240;
            Assert.That(AiBrain.Loads(0, 5, slots, hold, 60), Is.EqualTo(0));
            Assert.That(AiBrain.Loads(-5, 5, slots, hold, 60), Is.EqualTo(0));
            Assert.That(AiBrain.Loads(3360, 4, slots, hold, 60), Is.EqualTo(224));
            Assert.That(AiBrain.Loads(3360, 5, slots, hold, 60), Is.EqualTo(252));
            Assert.That(AiBrain.Loads(3360, 6, slots, hold, 60), Is.EqualTo(252), "the slots cap the loads");
            Assert.That(AiBrain.Gain(3360, 5, slots, hold, 60, 10000, amount), Is.EqualTo(140), "the 5th worker adds 28 loads");
            Assert.That(AiBrain.Gain(3360, 6, slots, hold, 60, 10000, amount), Is.EqualTo(0));
            // Full field, normal walk (T 60): AI-E's cap 5.
            Assert.That(AiBrain.CalcCap(3600 - build, slots, hold, 60, 10000, amount, cost), Is.EqualTo(5));
            // The same field with 300 left: the 1st worker adds 280, the 2nd 20.
            Assert.That(AiBrain.CalcCap(3600 - build, slots, hold, 60, 300, amount, cost), Is.EqualTo(1));
            // Horizon 20 s (span 160): the 1st adds 2 loads.
            Assert.That(AiBrain.CalcCap(400 - build, slots, hold, 60, 10000, amount, cost), Is.EqualTo(0));
            // Far field: 10 tiles from a radius-0.5 depot, Water speed 2.0, interact 1.5: W 130, T 170.
            int walk = AiBrain.WalkTicks(Fix64.FromInt(10), Fix64.Half, Fix64.Parse("1.5"), Fix64.FromInt(2), 20);
            Assert.That(walk, Is.EqualTo(130));
            Assert.That(AiBrain.CalcCap(3600 - build, slots, hold, hold + walk, 10000, amount, cost), Is.EqualTo(13));
            Assert.That(AiBrain.CalcCap(1200 - build, slots, hold, hold + walk, 10000, amount, cost), Is.EqualTo(0), "horizon 60 s: 5 loads (25) do not repay 50");
            // Nothing left to gather.
            Assert.That(AiBrain.CalcCap(3600 - build, slots, hold, 60, 0, amount, cost), Is.EqualTo(0));
            // A remainder below the worker's cost.
            Assert.That(AiBrain.CalcCap(3600 - build, slots, hold, 60, cost - 1, amount, cost), Is.EqualTo(0));
        }

        /// <summary>
        /// EC-3: with the switch on, the served caps are the CalcCapOf values (recomputed here from the game's numbers), WantedWorkers is min(max_workers, their sum) and the sum is not below the first target-order
        /// field's cap; with the switch off the caps equal AI-E's on the same view.
        /// </summary>
        [Test]
        public void EC3_ServedCapsAndWantedWorkers()
        {
            Simulation sim = Game("greybox_1v1", 2);
            AiProfile on = Shipped("aggressive_s5_e");
            AiProfile off = Shipped("aggressive_s5");
            Assert.That(on.UseEconomyCalc, Is.True);
            Assert.That(off.UseEconomyCalc, Is.False);
            // The own HQ, the only drop-off of the match.
            Unit hq = null!;
            foreach (Unit unit in sim.Units.All)
            {
                hq = unit.Owner == P0 && unit.Definition.Hq != null ? unit : hq;
            }
            UnitDefinition worker = Data.Content.Units["skai.fledgling"];
            // Interact range as the economy uses it. Unit: tiles. Range: 0.5 to 4.
            Fix64 interact = Fix64.Max(Data.Tuning.Economy.InteractRangeTiles, Data.Tuning.Terrain.ArrivalRadiusTiles);
            // Ticks one slot is held per load, and the span the next worker earns over. Unit: ticks.
            int hold = Data.Tuning.Economy.GatherTicksPerTrip(sim.Clock);
            long span = sim.Clock.SecondsToTicks(Fix64.FromInt(on.EconomyHorizonSeconds)) - Math.Max(1, sim.Clock.SecondsToTicks(worker.Economy.BuildSeconds));
            AiBrain.EconomyProbe probeOn = Probe(sim, on);
            AiBrain.EconomyProbe probeOff = Probe(sim, off);
            Assert.That(probeOn.Served.Count, Is.GreaterThan(0));
            Assert.That(probeOn.ServedCaps.Count, Is.EqualTo(probeOn.Served.Count));
            // Sum of the served caps. Unit: workers.
            int sum = 0;
            // Index into the served fields. Range: 0 to count - 1.
            for (int i = 0; i < probeOn.Served.Count; i++)
            {
                ResourceFieldState field = sim.Resources[probeOn.Served[i]];
                // Distance to the HQ, capped, and the walk. Unit: tiles, ticks.
                FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y));
                Fix64 distance = Fix64.Min((centre - hq.Position).Length(), on.FieldMaxDistanceTiles);
                int walk = AiBrain.WalkTicks(distance, hq.Definition.Radius, interact, worker.SpeedTilesPerSecond, TicksPerSecond);
                int slots = Data.Tuning.Economy.GatherSlots(field.Kind);
                int expected = AiBrain.CalcCap(span, slots, hold, hold + walk, field.Remaining, Data.Tuning.Economy.GatherAmountPerTrip, worker.CostCommon + worker.CostRare);
                Assert.That(probeOn.ServedCaps[i], Is.EqualTo(expected), "field " + probeOn.Served[i]);
                sum += expected;
            }
            Assert.That(probeOn.WantedWorkers, Is.EqualTo(Math.Min(on.MaxWorkers, sum)));
            // The same view with the switch off: AI-E's caps.
            for (int i = 0; i < probeOff.Served.Count; i++)
            {
                ResourceFieldState field = sim.Resources[probeOff.Served[i]];
                FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(field.X, field.Y));
                int aiE = AiBrain.SaturationCap(Data.Tuning.Economy.GatherSlots(field.Kind), Fix64.Min((centre - hq.Position).Length(), off.FieldMaxDistanceTiles), hq.Definition.Radius, interact,
                    worker.SpeedTilesPerSecond, TicksPerSecond, hold, off.SaturationExtraWorkersPerField);
                Assert.That(probeOff.ServedCaps[i], Is.EqualTo(aiE), "AI-E field " + probeOff.Served[i]);
            }
            // At a main field with the normal walk E agrees with AI-E (spec 0: "on a full field with a normal walk it equals AI-E's cap with extra 0").
            Assert.That(probeOn.ServedCaps[0], Is.EqualTo(probeOff.ServedCaps[0]));
        }

        /// <summary>EC-8: every shipped profile has both keys; only aggressive_s5_e has use_economy_calc true; use_economy_calc with either AI-E switch false, a horizon of 9 or 1801 and a missing key fail naming the file and the key.</summary>
        [Test]
        public void EC8_ParsingAndValidation()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                string text = ProfileText(id);
                Assert.That(text, Does.Contain("\"use_economy_calc\":"), id);
                Assert.That(text, Does.Contain("\"economy_horizon_seconds\": 180,"), id);
                AiProfile profile = Shipped(id);
                Assert.That(profile.UseEconomyCalc, Is.EqualTo(id == "aggressive_s5_e"), id);
                Assert.That(profile.EconomyHorizonSeconds, Is.EqualTo(180), id);
            }
            // The candidate's text, edited; each must fail with the file name and the key in the message.
            string candidate = ProfileText("aggressive_s5_e");
            string file = "ai/aggressive_s5_e.json";
            DataValidationException workers = Expect.Throws<DataValidationException>(() => AiProfile.Parse(candidate.Replace("\"use_saturation_workers\": true,", "\"use_saturation_workers\": false,"), file, Data.Content));
            Assert.That(workers.Message, Does.Contain(file).And.Contain("use_economy_calc"));
            DataValidationException expansion = Expect.Throws<DataValidationException>(() => AiProfile.Parse(candidate.Replace("\"use_saturation_expansion\": true,", "\"use_saturation_expansion\": false,"), file, Data.Content));
            Assert.That(expansion.Message, Does.Contain(file).And.Contain("use_economy_calc"));
            foreach (string horizon in new[] { "9", "1801" })
            {
                DataValidationException range = Expect.Throws<DataValidationException>(() => AiProfile.Parse(candidate.Replace("\"economy_horizon_seconds\": 180,", "\"economy_horizon_seconds\": " + horizon + ","), file, Data.Content));
                Assert.That(range.Message, Does.Contain(file).And.Contain("economy_horizon_seconds"));
            }
            Assert.That(AiProfile.Parse(candidate.Replace("\"economy_horizon_seconds\": 180,", "\"economy_horizon_seconds\": 10,"), file, Data.Content).EconomyHorizonSeconds, Is.EqualTo(10));
            Assert.That(AiProfile.Parse(candidate.Replace("\"economy_horizon_seconds\": 180,", "\"economy_horizon_seconds\": 1800,"), file, Data.Content).EconomyHorizonSeconds, Is.EqualTo(1800));
            foreach (string key in new[] { "use_economy_calc", "economy_horizon_seconds" })
            {
                // The candidate's text without the line that holds the key.
                string without = Regex.Replace(candidate, "\\n  \"" + key + "\":[^\\n]*", "");
                Assert.That(without, Does.Not.Contain("\"" + key + "\":"));
                DataValidationException missing = Expect.Throws<DataValidationException>(() => AiProfile.Parse(without, file, Data.Content));
                Assert.That(missing.Message, Does.Contain(file).And.Contain(key));
            }
        }

        /// <summary>EC-9: aggressive_s5_e.json differs from aggressive_s5.json only in line 1, $doc, id and use_economy_calc (line diff after removing comment lines).</summary>
        [Test]
        public void EC9_CandidateDiffersInOneSwitch()
        {
            // Lines of each file that are not comment lines (a line whose first non-blank characters are //), in order.
            List<string> baseline = NonCommentLines(ProfileText("aggressive_s5"));
            List<string> candidate = NonCommentLines(ProfileText("aggressive_s5_e"));
            Assert.That(candidate.Count, Is.EqualTo(baseline.Count));
            // The lines that differ. Unit: lines.
            List<string> changed = new List<string>();
            for (int i = 0; i < baseline.Count; i++)
            {
                if (baseline[i] != candidate[i])
                {
                    changed.Add(candidate[i].Trim());
                }
            }
            Assert.That(changed.Count, Is.EqualTo(3), string.Join("\n", changed));
            Assert.That(changed[0], Does.StartWith("\"$doc\":"));
            Assert.That(changed[1], Is.EqualTo("\"id\": \"aggressive_s5_e\","));
            Assert.That(changed[2], Is.EqualTo("\"use_economy_calc\": true,"));
            Assert.That(ProfileText("aggressive_s5_e").Split('\n')[0], Does.StartWith("// rts/data/ai/aggressive_s5_e.json"));
        }

        /// <summary>The non-comment lines of a file's text, in order.</summary>
        private static List<string> NonCommentLines(string text)
        {
            List<string> lines = new List<string>();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        /// <summary>
        /// EC-10: a copy of aggressive_s5 with economy_horizon_seconds 600 plays the same game as aggressive_s5 (the key is unread while the switch is off: same end tick, state hash and play signature). The pins of section 0
        /// (smoke, economy_smoke, the aggressive_s5 and aggressive_s3 traces, V2ReferenceHash, BlindTraceHash) are held by their existing tests, which are not re-pinned.
        /// </summary>
        [Test]
        public void EC10_OffPath_HorizonIsUnreadWhileOff()
        {
            AiProfile plain = Shipped("aggressive_s5");
            AiProfile longer = AiProfile.Parse(ProfileText("aggressive_s5").Replace("\"economy_horizon_seconds\": 180,", "\"economy_horizon_seconds\": 600,"), "ai/aggressive_s5.json", Data.Content);
            Assert.That(longer.EconomyHorizonSeconds, Is.EqualTo(600));
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord a = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, plain, plain, "aggressive_s5", "aggressive_s5", 300);
            GameRecord b = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, longer, longer, "aggressive_s5", "aggressive_s5", 300);
            Assert.That(b.ToJsonLine(), Is.EqualTo(a.ToJsonLine()));
        }

        /// <summary>
        /// EC-11: two games of aggressive_s5_e (seed 20261026, skai_vs_water, 600 s) give the same record line; each slot draws the opening aggressive_s5 draws with that seed, so no random draw was added.
        /// </summary>
        [Test]
        public void EC11_Determinism_AndNoNewDraw()
        {
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord first = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_e", "aggressive_s5_e", 600);
            GameRecord second = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_e", "aggressive_s5_e", 600);
            Assert.That(second.ToJsonLine(), Is.EqualTo(first.ToJsonLine()));
            GameRecord reference = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 60);
            Assert.That(first.Opening0, Is.Not.Empty);
            Assert.That(first.Opening0, Is.EqualTo(reference.Opening0));
            Assert.That(first.Opening1, Is.EqualTo(reference.Opening1));
        }

        /// <summary>EC-13: an aggressive_s5 trace has no econ= column; an aggressive_s5_e trace has it on every player sample line (the byte comparison with the trace before E1 is the builder's cmp, in the report).</summary>
        [Test]
        public void EC13_TraceColumn()
        {
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            StringWriter plain = new StringWriter();
            GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5", "aggressive_s5", 120, new GameTrace(plain, 30));
            Assert.That(plain.ToString(), Does.Not.Contain("econ="));
            StringWriter candidate = new StringWriter();
            GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_e", "aggressive_s5_e", 120, new GameTrace(candidate, 30));
            // Player sample lines: "t=30s <label> bank=...".
            int samples = 0;
            foreach (string line in candidate.ToString().Split('\n'))
            {
                if (Regex.IsMatch(line, @"^t=\S+ .* bank="))
                {
                    samples++;
                    Assert.That(line, Does.Match(@" econ=w\d+/n-?\d+/l\d+( \(final\))?$"), line);
                }
            }
            Assert.That(samples, Is.GreaterThanOrEqualTo(8));
        }
    }
}
