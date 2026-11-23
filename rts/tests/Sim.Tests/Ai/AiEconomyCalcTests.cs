// rts/tests/Sim.Tests/Ai/AiEconomyCalcTests.cs
// Tests of package E of rts/docs/specs/ai_economy_calc.md (the AI economy by calculation): the walk-ticks extraction (EC-1), the worker cap of rule EW (EC-2, EC-3), profile parsing and the candidate
// file (EC-8, EC-9), the off path (EC-10), determinism (EC-11) and the trace column (EC-13), then rule ED (package E2): the depot net value and home-full test (EC-4), Expand integration (EC-5), the starved case (EC-6), the rare field (EC-7) and a mirror run (EC-12). Part of Rts.Sim.Tests.
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

        // ------------------------------------------------------------------ package E2: rule ED

        /// <summary>The candidate profile's text with the early-expansion gate off (so the clock gate is the plain 240 s) and the given (old, new) text pairs applied; each old text must occur.</summary>
        private static AiProfile Candidate(params string[] oldNewPairs)
        {
            // The text being edited.
            string text = ProfileText("aggressive_s5_e").Replace("\"use_early_expansion\": true,", "\"use_early_expansion\": false,");
            // Index into the pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text, Does.Contain(oldNewPairs[i]), oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_e.json", Data.Content);
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Own worker IDs of P0, ascending.</summary>
        private static List<EntityId> Workers(Simulation sim)
        {
            List<EntityId> ids = new List<EntityId>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Economy.CanGather)
                {
                    ids.Add(unit.Id);
                }
            }
            return ids;
        }

        /// <summary>Sets P0's stock.</summary>
        private static void SetStock(Simulation sim, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>A prototype_1v1 match with <paramref name="workers"/> workers per side at <paramref name="seconds"/>, one worker gathering (so the AI has income) and 300 common in the bank.</summary>
        private Simulation Base(int workers, int seconds)
        {
            Simulation sim = Game("prototype_1v1", workers);
            StepTo(sim, seconds * TicksPerSecond);
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, Workers(sim)[1], 0));
            sim.Step();
            SetStock(sim, 300, 0);
            return sim;
        }

        /// <summary>
        /// Up to <paramref name="thinks"/> decisions of P0 on the current view with one memory (nothing is applied; a field with no depot site is marked and the next think tries the next field). Returns the depot orders
        /// of the think that ordered one (or 0) and the explanation record of the last think run.
        /// </summary>
        private static (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) Think(Simulation sim, AiProfile profile, AiMemory? memory = null, int thinks = 6)
        {
            memory ??= new AiMemory();
            for (int think = 0; think < thinks; think++)
            {
                // Depot orders of this think. Unit: orders. Range: 0 upward.
                int orders = 0;
                foreach (AiIntent intent in AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1)))
                {
                    orders += intent.Kind == AiIntentKind.Build && intent.DefinitionId == "skai.perch_depot" ? 1 : 0;
                }
                if (orders > 0)
                {
                    return (orders, memory.EconomyNote);
                }
            }
            return (0, memory.EconomyNote);
        }

        /// <summary>EC-4: the DepotNet worked example of 2.3 (525 at 180 s, -100 at 60 s, 380 with headroom 3) and the home-full test (false at 7 workers, true at 8; wanted 10, lead 600, build 240).</summary>
        [Test]
        public void EC4_DepotNetAndHomeFull()
        {
            // Spec numbers: depot cost 100, build 400 ticks, placement 40, a site 20 tiles away at speed 2.5 (160 ticks): lead 600; worker build 240, cost 50; one common field, 3 slots, trip cycle 60, 10000 left.
            int walk = (Fix64.FromInt(20) * 20 / Fix64.Parse("2.5")).CeilingToInt();
            Assert.That(walk, Is.EqualTo(160));
            int lead = walk + 40 + 400;
            Assert.That(lead, Is.EqualTo(600));
            (int Slots, int Cycle, int Known)[] field = { (3, 60, 10000) };
            Assert.That(AiBrain.DepotNetValue(field, 3600, lead, 240, 40, 5, 50, 20, 100), Is.EqualTo(525));
            Assert.That(AiBrain.DepotNetValue(field, 1200, lead, 240, 40, 5, 50, 20, 100), Is.EqualTo(-100), "horizon 60 s: worker 1 earns 6 loads (30), which does not repay 50");
            Assert.That(AiBrain.DepotNetValue(field, 3600, lead, 240, 40, 5, 50, 3, 100), Is.EqualTo(380), "headroom 3 counts workers 1 to 3 only: 630 - 150 - 100");
            Assert.That(AiBrain.DepotNetValue(field, 3600, lead, 240, 40, 5, 50, 0, 100), Is.EqualTo(-100), "no headroom: only the depot's cost");
            Assert.That(AiBrain.DepotNetValue(new (int Slots, int Cycle, int Known)[0], 3600, lead, 240, 40, 5, 50, 20, 100), Is.EqualTo(-100), "no new field");
            // A second, farther field is walked after the first one fills; the order given is the order walked.
            (int Slots, int Cycle, int Known)[] two = { (3, 60, 10000), (3, 100, 10000) };
            Assert.That(AiBrain.DepotNetValue(two, 3600, lead, 240, 40, 5, 50, 20, 100), Is.GreaterThan(525));
            Assert.That(AiBrain.HomeFull(false, 7, lead, 240, 10), Is.False);
            Assert.That(AiBrain.HomeFull(false, 8, lead, 240, 10), Is.True);
            Assert.That(AiBrain.HomeFull(true, 0, lead, 240, 10), Is.True, "starved: always full");
        }

        /// <summary>
        /// EC-5: Expand before the clock gate (100 s, gate at 240 s, low false). Home full and DepotNet above 0 with the bank at 100 or more: a depot is ordered at the step-1 site and the note says Fired; DepotNet at most 0 (no
        /// headroom): none; home not full: none. After the clock gate with DepotNet at most 0 a depot is ordered (not Fired). The threat gate, the live-depot limit and a depot under construction still block.
        /// </summary>
        [Test]
        public void EC5_ExpandIntegration()
        {
            AiProfile on = Candidate();
            // The base target: workers wanted for the fresh base, and the lead time and net value the first think reports.
            int wanted = Probe(Game("prototype_1v1", 2), on).WantedWorkers;
            Assert.That(wanted, Is.GreaterThan(4));
            // Workers per side so that home is full for any lead (wanted itself).
            (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) full = Think(Base(wanted, 100), on);
            Assert.That(full.Orders, Is.EqualTo(1), "home full and a positive net value: a depot before the clock gate");
            Assert.That(full.Note.Fired, Is.True);
            Assert.That(full.Note.DepotNet, Is.GreaterThan(0));
            Assert.That(full.Note.Wanted, Is.EqualTo(wanted));
            UnitDefinition depot = Data.Content.Units["skai.perch_depot"];
            // Lead without the walk: placement plus the depot's build time. Unit: ticks.
            MatchClock clock = new MatchClock(TicksPerSecond);
            Assert.That(full.Note.Lead, Is.GreaterThan(Data.Tuning.Economy.WorkerPlacementTicks(clock) + clock.SecondsToTicks(depot.Economy.BuildSeconds)), "the walk is part of the lead");
            Assert.That(full.Note.Tick, Is.EqualTo(100 * TicksPerSecond + 1));
            // No headroom (max_workers at the target): DepotNet is the depot's cost below 0, so no depot.
            (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) capped = Think(Base(wanted, 100), Candidate("\"max_workers\": 48,", "\"max_workers\": " + wanted + ","));
            Assert.That(capped.Orders, Is.EqualTo(0));
            Assert.That(capped.Note.DepotNet, Is.EqualTo(-(depot.CostCommon + depot.CostRare)));
            // Home not full: fewer workers than wanted less the workers the HQ can finish during the lead time.
            int workerBuild = Math.Max(1, new MatchClock(TicksPerSecond).SecondsToTicks(Data.Content.Units["skai.fledgling"].Economy.BuildSeconds));
            int few = wanted - full.Note.Lead / workerBuild - 1;
            Assert.That(few, Is.GreaterThan(1));
            (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) notFull = Think(Base(few, 100), on);
            Assert.That(notFull.Orders, Is.EqualTo(0), "home not full: no depot before the clock gate");
            Assert.That(notFull.Note.DepotNet, Is.GreaterThan(0), "the net value alone is positive; it is home that is not full");
            // After the clock gate a depot is ordered although DepotNet is at most 0 (the clock is kept), and it is not a calculated order.
            (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) late = Think(Base(wanted, 250), Candidate("\"max_workers\": 48,", "\"max_workers\": " + wanted + ","));
            Assert.That(late.Orders, Is.EqualTo(1));
            Assert.That(late.Note.DepotNet, Is.LessThanOrEqualTo(0));
            Assert.That(late.Note.Fired, Is.False);
            // The live-depot limit blocks.
            Assert.That(Think(Base(wanted, 100), Candidate("\"max_live_depots\": 4,", "\"max_live_depots\": 0,")).Orders, Is.EqualTo(0));
            // A seen threat of the grasper's supply (the gate is 4) blocks, as for rule E.
            Simulation threatened = Base(wanted, 100);
            Spawn(threatened, "water.grasper", P1, threatened.Map.Spawns[0] + new FixVec2(Fix64.FromInt(3), Fix64.FromInt(3)));
            SetStock(threatened, 300, 0);
            Assert.That(Think(threatened, Candidate("\"expand_block_threat_supply\": 4,", "\"expand_block_threat_supply\": 0,")).Orders, Is.EqualTo(0), "any threat blocks");
            // A depot under construction blocks a second one: apply the first order, then think again.
            Simulation building = Base(wanted, 100);
            AiMemory memory = new AiMemory();
            foreach (AiIntent intent in AiBrain.Decide(AiWorldView.Build(building, P0), on, on.RequireFaction("skai"), memory, new DeterministicRandom(1)))
            {
                if (intent.Kind == AiIntentKind.Build)
                {
                    building.Submit(intent.ToCommand(building.Clock.Tick, P0, _sequence++));
                }
            }
            building.Step();
            Assert.That(Think(building, on).Orders, Is.EqualTo(0), "the pending depot order blocks a second depot");
        }

        /// <summary>EC-6: with every field at home empty (no served field) home counts as full and a depot is ordered with DepotNet above 0 even before the clock gate.</summary>
        [Test]
        public void EC6_Starved()
        {
            AiProfile on = Candidate();
            Simulation sim = Base(6, 100);
            // The own HQ, to find the fields at home.
            Unit hq = null!;
            foreach (Unit unit in sim.Units.All)
            {
                hq = unit.Owner == P0 && unit.Definition.Hq != null ? unit : hq;
            }
            // Fields within reach of the HQ are emptied.
            for (int i = 0; i < sim.Resources.Count; i++)
            {
                FixVec2 centre = sim.Terrain.CentreOf(sim.Terrain.IndexOf(sim.Resources[i].X, sim.Resources[i].Y));
                if ((centre - hq.Position).LengthSquared() <= on.FieldMaxDistanceTiles * on.FieldMaxDistanceTiles)
                {
                    sim.Resources[i].Take(1000000);
                }
            }
            sim.Step();
            // One think of memory so the empty fields are known, as in AI-E's E-2.
            AiMemory memory = new AiMemory();
            Probe(sim, on, memory);
            Assert.That(Probe(sim, on, memory).Served, Is.Empty, "nothing is served");
            (int Orders, (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note) result = Think(sim, on, memory);
            Assert.That(result.Orders, Is.EqualTo(1));
            Assert.That(result.Note.DepotNet, Is.GreaterThan(0));
        }

        /// <summary>
        /// EC-7: the first target's site (27.5, 13.5) has two common fields (indices 2 and 3) and one rare field (16, 600 left) in reach. With the rare cap on DepotNet equals DepotNetValue over the two common fields
        /// only; with it off it equals DepotNetValue over all three, the rare field walked between them (trip cycle 40 ties with field 3, so field 3 then 16, then field 2 at 48). Both runs want the same workers, so the
        /// bank change moves nothing but the cap. The cycles are the 40 ticks of a load plus the walk from the site, read from a run at this site; a different site rule needs them read again (the site is asserted by the lead).
        /// </summary>
        [Test]
        public void EC7_RareFieldAddsOnlyWhileUncapped()
        {
            // rare_cap_yields_to_saturation false keeps the rare cap on whatever the common fields hold.
            string[] noYield = { "\"rare_cap_yields_to_saturation\": true,", "\"rare_cap_yields_to_saturation\": false," };
            // Explanation record of the first think with the given rare bank. Unit: record (see AiMemory.EconomyNote).
            (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) Note(int rareBank)
            {
                Simulation sim = Base(8, 100);
                SetStock(sim, 300, rareBank);
                return Think(sim, Candidate(noYield), null, 1).Note;
            }
            (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) capped = Note(5000);
            (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) open = Note(0);
            Assert.That(open.Wanted, Is.EqualTo(capped.Wanted), "the bank does not change the worker target, so the headroom is the same");
            Assert.That(open.Lead, Is.EqualTo(capped.Lead));
            Assert.That(capped.Lead, Is.EqualTo(561), "the site (27.5, 13.5) the cycles below were read at");
            // Workers the AI may still add. Unit: workers. Range: 0 upward.
            int headroom = 48 - capped.Wanted;
            // Fields as (slots, trip cycle in ticks, known amount): the common fields 3 and 2, and the rare field 16 between them.
            (int Slots, int Cycle, int Known) common3 = (3, 40, 3000);
            (int Slots, int Cycle, int Known) common2 = (3, 48, 3000);
            (int Slots, int Cycle, int Known) rare16 = (2, 40, 600);
            // Build time of a worker and cost of the depot. Unit: ticks and resource units.
            int build = new MatchClock(TicksPerSecond).SecondsToTicks(Data.Content.Units["skai.fledgling"].Economy.BuildSeconds);
            UnitDefinition depot = Data.Content.Units["skai.perch_depot"];
            long OverFields(params (int Slots, int Cycle, int Known)[] fields) => AiBrain.DepotNetValue(fields, 3600, capped.Lead, build, 40, Data.Tuning.Economy.GatherAmountPerTrip, 50, headroom, depot.CostCommon + depot.CostRare);
            Assert.That(capped.DepotNet, Is.EqualTo(OverFields(common3, common2)), "capped: the common fields only");
            Assert.That(open.DepotNet, Is.EqualTo(OverFields(common3, rare16, common2)), "open: the rare field in reach adds by the same formula");
            Assert.That(open.DepotNet, Is.GreaterThan(capped.DepotNet));
        }

        /// <summary>
        /// EC-12: a greybox run, two aggressive_s5_e Skai AIs on prototype_1v1 for 600 s. Assigned workers on a served field never exceed its cap + 1 for more than one think in a row (as E-6); every think whose note is Fired
        /// has DepotNet above 0 and home full; a field with less than a worker's cost left has cap 0.
        /// </summary>
        [Test]
        public void EC12_MirrorRun()
        {
            AiProfile profile = Shipped("aggressive_s5_e");
            // A field with 30 left (less than the 50 a worker costs) has cap 0.
            Simulation small = Game("greybox_1v1", 2);
            small.Resources[0].Take(small.Resources[0].Remaining - 30);
            small.Step();
            AiMemory smallMemory = new AiMemory();
            Probe(small, profile, smallMemory);
            AiBrain.EconomyProbe afterTake = Probe(small, profile, smallMemory);
            Assert.That(afterTake.Served, Does.Contain(0));
            Assert.That(afterTake.ServedCaps[new List<int>(afterTake.Served).IndexOf(0)], Is.EqualTo(0));

            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai A"),
                new PlayerSetup(P1, new TeamId(1), "skai", "Skai B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, sim.Map.Spawns[0]);
            Spawn(sim, "skai.great_aerie", P1, sim.Map.Spawns[1]);
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, sim.Map.Spawns[0] + offset);
                Spawn(sim, "skai.fledgling", P1, sim.Map.Spawns[1] - offset);
            }
            SkirmishAiController first = new SkirmishAiController(P0, profile);
            List<IPlayerController> controllers = new List<IPlayerController> { first, new SkirmishAiController(P1, profile) };
            ExploreKit.ExploreAll(sim);
            AiHost.StartAll(sim, controllers);
            // Ticks between two thinks (think_interval_seconds). Unit: ticks. Range: 1 upward.
            int thinkTicks = (profile.ThinkIntervalSeconds * TicksPerSecond).RoundToInt();
            // Memory of the probe (field knowledge only).
            AiMemory probeMemory = new AiMemory();
            // Consecutive thinks each field index has been more than one over its cap. Unit: thinks.
            SortedDictionary<int, int> over = new SortedDictionary<int, int>();
            // Build time of a worker, to judge "home full". Unit: ticks. Range: 1 upward.
            int workerBuild = Math.Max(1, sim.Clock.SecondsToTicks(Data.Content.Units["skai.fledgling"].Economy.BuildSeconds));
            // Thinks whose note was Fired, and thinks checked. Unit: thinks.
            int fired = 0;
            int checkedThinks = 0;
            while (sim.Clock.Tick < 600 * TicksPerSecond)
            {
                AiHost.ThinkAll(sim, controllers);
                AiWorldView view = AiWorldView.Build(sim, P0);
                AiBrain.EconomyProbe? probe = AiBrain.Probe(view, profile, profile.RequireFaction("skai"), probeMemory);
                if (probe != null && sim.Clock.Tick % thinkTicks == 0)
                {
                    checkedThinks++;
                    // Workers per field index right now, and own workers alive plus queued. Unit: workers.
                    int[] assigned = new int[sim.Resources.Count];
                    int workers = 0;
                    foreach (AiUnitView unit in view.OwnUnits)
                    {
                        if (unit.IsWorker && unit.GatherFieldIndex >= 0)
                        {
                            assigned[unit.GatherFieldIndex]++;
                        }
                        workers += unit.IsWorker ? 1 : 0;
                        foreach (string queued in unit.Queue)
                        {
                            workers += queued == "skai.fledgling" ? 1 : 0;
                        }
                    }
                    // The think's explanation record, when this think wrote one.
                    (int Tick, int Wanted, long DepotNet, int Lead, bool Fired) note = first.Memory.EconomyNote;
                    if (note.Tick == sim.Clock.Tick && note.Fired)
                    {
                        fired++;
                        Assert.That(note.DepotNet, Is.GreaterThan(0), "fired at tick " + note.Tick);
                        Assert.That(AiBrain.HomeFull(probe.Served.Count == 0, workers, note.Lead, workerBuild, note.Wanted), Is.True, "home full when fired at tick " + note.Tick);
                    }
                    // True when some served field below its cap could take a worker; with none the surplus has nowhere to go and the check does not apply. Unit: boolean.
                    bool room = false;
                    for (int k = 0; k < probe.Served.Count; k++)
                    {
                        room |= probe.ServedCaps[k] > 0 && assigned[probe.Served[k]] < probe.ServedCaps[k];
                    }
                    for (int k = 0; k < probe.Served.Count; k++)
                    {
                        int field = probe.Served[k];
                        over.TryGetValue(field, out int run);
                        over[field] = room && probe.ServedCaps[k] > 0 && assigned[field] > probe.ServedCaps[k] + 1 ? run + 1 : 0;
                        Assert.That(over[field], Is.LessThanOrEqualTo(1), "field " + field + " over cap + 1 for two thinks at tick " + sim.Clock.Tick);
                        // A field with less than a worker's cost left takes no worker.
                        Assert.That(sim.Resources[field].Remaining >= 50 || probe.ServedCaps[k] == 0, Is.True, "field " + field + " nearly empty at tick " + sim.Clock.Tick);
                    }
                }
                sim.Step();
            }
            Assert.That(checkedThinks, Is.GreaterThan(50));
            TestContext.Out.WriteLine("EC12 fired_thinks=" + fired + " checked_thinks=" + checkedThinks);
        }
    }
}
