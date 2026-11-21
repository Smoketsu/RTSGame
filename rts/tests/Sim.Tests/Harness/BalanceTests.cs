// rts/tests/Sim.Tests/Harness/BalanceTests.cs
// Acceptance tests R-1 to R-6 of rts/docs/specs/balance_measurement.md (package M, section M.5): the faction and opening matrices, the use shares, the mechanic detectors, the unspent medians, the four trust checks
// at their bar edges, and the re-render and determinism of --balance. The matrix, use, detector and check tests use synthetic records; R-6 starts worker processes. Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Balance report tests.</summary>
    [TestFixture]
    public class BalanceTests
    {
        /// <summary>Deletes the temporary folders.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            HarnessTestSupport.Cleanup();
        }

        /// <summary>A synthetic game record: factions, result, play signature, openings and optional summaries.</summary>
        private static GameRecord Game(int game, string f0, string f1, string result, ulong signature, string o0 = "", string o1 = "", GameSummary? s0 = null, GameSummary? s1 = null)
        {
            return new GameRecord("prototype_1v1", f0 + "_vs_" + f1, game, (ulong)game, f0, f1, "p", "p", result, 100, 1UL, signature, 2UL, new List<HqKill>(), o0, o1, -1, -1, string.Empty, s0, s1);
        }

        /// <summary>A synthetic summary with the given trained counts, mechanic counts and bank snapshots (null entries for games that ended earlier).</summary>
        private static GameSummary Summary(Dictionary<string, int>? trained = null, Dictionary<string, int>? mechanics = null, params int[]?[] banks)
        {
            List<int[]?> bank = new List<int[]?>(banks);
            return new GameSummary(0, 0, new SortedDictionary<string, int[]>(), bank, new List<int[]>(), new List<int[]>(), new SortedDictionary<string, int>(trained ?? new Dictionary<string, int>(), StringComparer.Ordinal),
                new SortedDictionary<string, int>(), new SortedDictionary<string, int>(mechanics ?? new Dictionary<string, int>(), StringComparer.Ordinal));
        }

        /// <summary>The Wilson z of the shipped tuning.</summary>
        private static double Z(DataSet data)
        {
            return HarnessStats.ToDouble(data.Tuning.HarnessBatch.ConfidenceZ);
        }

        /// <summary>R-1: faction and opening cells count wins over decided games, distinct games by signature, and the Wilson interval equals HarnessStats.Wilson on the distinct counts; the diagonal is the seat share.</summary>
        [Test]
        public void Matrices_CountDistinctGamesAndWilson()
        {
            DataSet data = TestData.LoadAll();
            // Skai in slot 0 against water: slot 0 wins with signatures 1, 2, 3, 3 (one duplicate), slot 1 wins with 4 and 5, one game undecided.
            List<GameRecord> records = new List<GameRecord>
            {
                Game(0, "skai", "water", GameRecord.Slot0, 1, "a", "x"), Game(1, "skai", "water", GameRecord.Slot0, 2, "a", "y"), Game(2, "skai", "water", GameRecord.Slot0, 3, "b", "x"),
                Game(3, "skai", "water", GameRecord.Slot0, 3, "b", "x"), Game(4, "skai", "water", GameRecord.Slot1, 4, "b", "y"), Game(5, "skai", "water", GameRecord.Slot1, 5, "a", "x"),
                Game(6, "skai", "water", GameRecord.Undecided, 6, "a", "x"),
                // One game with the factions the other way round: water in slot 0 wins.
                Game(7, "water", "skai", GameRecord.Slot0, 7, "x", "a"),
                // Mirror games: slot 0 wins twice, slot 1 once.
                Game(8, "skai", "skai", GameRecord.Slot0, 8, "a", "b"), Game(9, "skai", "skai", GameRecord.Slot0, 9, "b", "a"), Game(10, "skai", "skai", GameRecord.Slot1, 10, "a", "a"),
            };
            WinCount skaiWater = BalanceReport.FactionCell(records, "skai", "water");
            Assert.That(skaiWater.Decided, Is.EqualTo(7), "games 0 to 5 and the swapped game 7; game 6 is undecided");
            Assert.That(skaiWater.Wins, Is.EqualTo(4), "skai wins games 0 to 3; games 4, 5 and 7 are water wins");
            Assert.That(skaiWater.Distinct, Is.EqualTo(6), "games 2 and 3 share a play signature");
            Assert.That(skaiWater.DistinctWins, Is.EqualTo(3));
            WinCount cross = BalanceReport.FactionCell(records, "skai", "water");
            Assert.That(cross.Wilson(Z(data)).Low, Is.EqualTo(HarnessStats.Wilson(cross.DistinctWins, 0, cross.Distinct - cross.DistinctWins, Z(data)).Low));
            Assert.That(cross.Wilson(Z(data)).High, Is.EqualTo(HarnessStats.Wilson(cross.DistinctWins, 0, cross.Distinct - cross.DistinctWins, Z(data)).High));
            // Seat: the skai mirror, slot 0 won 2 of 3.
            WinCount seat = BalanceReport.FactionCell(records, "skai", "skai");
            Assert.That(seat.Decided, Is.EqualTo(3));
            Assert.That(seat.Wins, Is.EqualTo(2));
            // Opening cell (skai a) against (water x): games 0 and 5 (game 6 undecided); slot 0 won game 0 and lost game 5; game 7 has skai a in slot 1 against water x, which won.
            WinCount ax = BalanceReport.OpeningCell(records, "skai", "a", "water", "x");
            Assert.That(ax.Decided, Is.EqualTo(3));
            Assert.That(ax.Wins, Is.EqualTo(1));
            // Mirror opening cell (a, b): game 8 (slot 0 a won) and game 9 (slot 1 plays a and lost): wins 1 of 2; the same-opening cell (a, a) counts game 10 once, with no winner.
            WinCount ab = BalanceReport.OpeningCell(records, "skai", "a", "skai", "b");
            Assert.That(ab.Decided, Is.EqualTo(2));
            Assert.That(ab.Wins, Is.EqualTo(1));
            WinCount aa = BalanceReport.OpeningCell(records, "skai", "a", "skai", "a");
            Assert.That(aa.Decided, Is.EqualTo(1));
            Assert.That(aa.Wins, Is.EqualTo(0));
            Assert.That(BalanceReport.OpeningsOf(records, "skai"), Is.EqualTo(new[] { "a", "b" }));
        }

        /// <summary>R-2: unit and mechanic shares use only slot-games of the owning faction and only applicable mechanics.</summary>
        [Test]
        public void UseShares_UseOnlyTheOwningFactionAndApplicableMechanics()
        {
            DataSet data = TestData.LoadAll();
            // Two skai-versus-water games (skai slot 0) and one water mirror game: skai has 2 slot-games, water 2 + 2 = 4.
            GameSummary skaiUsed = Summary(new Dictionary<string, int> { ["skai.windrunner"] = 2 }, new Dictionary<string, int> { ["rare"] = 1 });
            GameSummary skaiIdle = Summary();
            GameSummary waterUsed = Summary(new Dictionary<string, int> { ["skai.windrunner"] = 1, ["water.grasper"] = 3 }, new Dictionary<string, int> { ["wave"] = 1 });
            List<GameRecord> records = new List<GameRecord>
            {
                Game(0, "skai", "water", GameRecord.Slot0, 1, "", "", skaiUsed, waterUsed), Game(1, "skai", "water", GameRecord.Slot1, 2, "", "", skaiIdle, Summary()),
                Game(2, "water", "water", GameRecord.Slot0, 3, "", "", Summary(), Summary()),
            };
            List<UseRow> units = BalanceReport.UnitUse(records, data);
            UseRow windrunner = units.Find(r => r.Faction == "skai" && r.Id == "skai.windrunner")!;
            Assert.That(windrunner.SlotGames, Is.EqualTo(2));
            Assert.That(windrunner.Used, Is.EqualTo(1), "the water slot that trained a windrunner does not count for skai");
            UseRow grasper = units.Find(r => r.Faction == "water" && r.Id == "water.grasper")!;
            Assert.That(grasper.SlotGames, Is.EqualTo(4));
            Assert.That(grasper.Used, Is.EqualTo(1));
            Assert.That(units.Find(r => r.Faction == "water" && r.Id == "skai.windrunner"), Is.Null, "a unit is only listed for its own faction");
            // The HQ is neither trainable nor buildable.
            Assert.That(units.Find(r => r.Id == data.Content.Factions["skai"].HqUnitId), Is.Null);
            List<UseRow> mechanics = BalanceReport.MechanicUse(records, data);
            UseRow rareSkai = mechanics.Find(r => r.Faction == "skai" && r.Id == "rare")!;
            Assert.That(rareSkai.SlotGames, Is.EqualTo(2));
            Assert.That(rareSkai.Used, Is.EqualTo(1));
            Assert.That(mechanics.Find(r => r.Faction == "skai" && r.Id == "wave"), Is.Null, "skai cannot cast the Water wave");
            UseRow waveWater = mechanics.Find(r => r.Faction == "water" && r.Id == "wave")!;
            Assert.That(waveWater.SlotGames, Is.EqualTo(4));
            Assert.That(waveWater.Used, Is.EqualTo(1));
            Assert.That(rareSkai.ShareText, Is.EqualTo("0.500"));
        }

        /// <summary>A fake detector context: owners, definitions and layers by entity, and the players of team 0.</summary>
        private sealed class FakeContext : IDetectorContext
        {
            /// <summary>Owner and definition by entity.</summary>
            public Dictionary<EntityId, KeyValuePair<PlayerId, UnitDefinition?>> Units { get; } = new Dictionary<EntityId, KeyValuePair<PlayerId, UnitDefinition?>>();

            /// <summary>Layer by entity.</summary>
            public Dictionary<EntityId, MovementLayer> Layers { get; } = new Dictionary<EntityId, MovementLayer>();

            /// <inheritdoc />
            public PlayerId OwnerOf(EntityId unit) => Units.TryGetValue(unit, out KeyValuePair<PlayerId, UnitDefinition?> known) ? known.Key : PlayerId.None;

            /// <inheritdoc />
            public UnitDefinition? DefinitionOf(EntityId unit) => Units.TryGetValue(unit, out KeyValuePair<PlayerId, UnitDefinition?> known) ? known.Value : null;

            /// <inheritdoc />
            public MovementLayer? LayerOf(EntityId unit) => Layers.TryGetValue(unit, out MovementLayer layer) ? layer : (MovementLayer?)null;

            /// <inheritdoc />
            public IReadOnlyList<PlayerId> PlayersOfTeam(TeamId team) => new[] { new PlayerId(0), new PlayerId(2) };
        }

        /// <summary>The owners a detector credits for one event.</summary>
        private static List<PlayerId> Detect(string mechanic, SimEvent simEvent, IDetectorContext context)
        {
            List<PlayerId> credited = new List<PlayerId>();
            MechanicDetectors.Require(mechanic).Detect(simEvent, context, credited);
            return credited;
        }

        /// <summary>The first definition for which the test holds (the data must have one).</summary>
        private static UnitDefinition Find(DataSet data, Func<UnitDefinition, bool> test, string what)
        {
            foreach (UnitDefinition definition in data.Content.Units.Values)
            {
                if (test(definition))
                {
                    return definition;
                }
            }
            throw new AssertionException("the data has no definition with " + what);
        }

        /// <summary>R-3: each detector of M.5.3 credits the right player for one synthetic event and nothing for the near miss; applicability follows the definitions of the faction.</summary>
        [Test]
        public void Detectors_CreditTheOwnerAndNothingElse()
        {
            DataSet data = TestData.LoadAll();
            PlayerId p0 = new PlayerId(0);
            PlayerId p1 = new PlayerId(1);
            FakeContext context = new FakeContext();
            EntityId e1 = new EntityId(1), e2 = new EntityId(2), e3 = new EntityId(3);
            // Definitions with the capabilities, found in the data.
            UnitDefinition reefRunner = Find(data, d => d.Weapon != null && d.Weapon.CanTargetAirOnWater && !d.Weapon.CanTargetAir, "an air-on-water weapon without static anti-air");
            UnitDefinition hq = Find(data, d => d.Hq != null, "an HQ");
            UnitDefinition source = Find(data, d => d.Flood.IsSource, "a flood source");
            UnitDefinition wall = Find(data, d => d.Economy.IsWall && !d.Economy.IsGate, "a wall");
            UnitDefinition gate = Find(data, d => d.Economy.IsGate, "a gate");
            UnitDefinition tower = Find(data, d => d.Economy.IsStaticDefence, "a static defence");
            UnitDefinition depot = Find(data, d => d.Economy.IsDropOff && d.Hq == null, "a depot");
            // downdraft, wave, splash, rare, cycle_pick: the event carries its owner.
            Assert.That(Detect("downdraft", new GroundingCastEvent(1, e1, p1, FixVec2.Zero, 3), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("wave", new WaveCastEvent(1, e1, p0, 1, 0, 4), context), Is.EqualTo(new[] { p0 }));
            Assert.That(Detect("splash", new BlastEvent(1, e1, p1, FixVec2.Zero, Fix64.One, 2), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("splash", new BlastEvent(1, e1, p1, FixVec2.Zero, Fix64.One, 0), context), Is.Empty, "a blast without victims");
            Assert.That(Detect("rare", new ResourceDepositedEvent(1, e1, e2, p0, ResourceKind.Rare, 5), context), Is.EqualTo(new[] { p0 }));
            Assert.That(Detect("rare", new ResourceDepositedEvent(1, e1, e2, p0, ResourceKind.Common, 5), context), Is.Empty);
            Assert.That(Detect("cycle_pick", new CyclePickedEvent(1, p1, new TeamId(1), "x"), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("cycle_pick", new CyclePickedEvent(1, PlayerId.None, new TeamId(1), "x"), context), Is.Empty, "the script is not a player");
            // drench: the attacker's owner.
            context.Units[e1] = new KeyValuePair<PlayerId, UnitDefinition?>(p0, reefRunner);
            Assert.That(Detect("drench", new DrenchHitEvent(1, e2, e1, 1, false), context), Is.EqualTo(new[] { p0 }));
            Assert.That(Detect("drench", new DrenchHitEvent(1, e1, e3, 1, false), context), Is.Empty, "an unknown attacker");
            // shield_dome: the dome's owner, only when it absorbed.
            Assert.That(Detect("shield_dome", new ShieldDomeHitEvent(1, e1, e2, e3, Fix64.One, Fix64.One), context), Is.EqualTo(new[] { p0 }));
            Assert.That(Detect("shield_dome", new ShieldDomeHitEvent(1, e1, e2, e3, Fix64.Zero, Fix64.One), context), Is.Empty);
            // momentum: only a rise.
            Assert.That(Detect("momentum", new MomentumChangedEvent(1, e1, 1, 2), context), Is.EqualTo(new[] { p0 }));
            Assert.That(Detect("momentum", new MomentumChangedEvent(1, e1, 2, 1), context), Is.Empty);
            // anti_air_on_water: the attacker's weapon flags and the target's layer now.
            context.Layers[e2] = MovementLayer.Air;
            Assert.That(Detect("anti_air_on_water", new ShotFiredEvent(1, e1, e2, 3, false), context), Is.EqualTo(new[] { p0 }));
            context.Layers[e2] = MovementLayer.Ground;
            Assert.That(Detect("anti_air_on_water", new ShotFiredEvent(1, e1, e2, 3, false), context), Is.Empty, "the target is not in the air");
            context.Units[e3] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, Find(data, d => d.Weapon != null && d.Weapon.CanTargetAir, "an anti-air weapon"));
            context.Layers[e2] = MovementLayer.Air;
            Assert.That(Detect("anti_air_on_water", new ShotFiredEvent(1, e3, e2, 3, false), context), Is.Empty, "a weapon that hits air anywhere does not count");
            // Structures: the owner of the completed structure, by its definition.
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, source);
            Assert.That(Detect("flood_source", new StructureCompletedEvent(1, e2, source.Id), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("wall", new StructureCompletedEvent(1, e2, source.Id), context), Is.Empty);
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, wall);
            Assert.That(Detect("wall", new StructureCompletedEvent(1, e2, wall.Id), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("gate", new StructureCompletedEvent(1, e2, wall.Id), context), Is.Empty);
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, gate);
            Assert.That(Detect("gate", new StructureCompletedEvent(1, e2, gate.Id), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("wall", new StructureCompletedEvent(1, e2, gate.Id), context), Is.Empty, "a gate is not counted as a wall");
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, tower);
            Assert.That(Detect("static_defence", new StructureCompletedEvent(1, e2, tower.Id), context), Is.EqualTo(new[] { p1 }));
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, depot);
            Assert.That(Detect("expansion", new StructureCompletedEvent(1, e2, depot.Id), context), Is.EqualTo(new[] { p1 }));
            context.Units[e2] = new KeyValuePair<PlayerId, UnitDefinition?>(p1, hq);
            Assert.That(Detect("expansion", new StructureCompletedEvent(1, e2, hq.Id), context), Is.Empty, "an HQ is not an expansion");
            // HQ events: the HQ's owner.
            Assert.That(Detect("hq_move", new HqStateChangedEvent(1, e2, HqState.Rooted, HqState.Uprooting), context), Is.EqualTo(new[] { p1 }));
            Assert.That(Detect("hq_move", new HqStateChangedEvent(1, e2, HqState.Uprooting, HqState.Uprooted), context), Is.Empty);
            Assert.That(Detect("hq_last_stand", new HqLastStandEvent(1, e2, Fix64.One, 20), context), Is.EqualTo(new[] { p1 }));
            // core_capture: every player of the team.
            Assert.That(Detect("core_capture", new CoreCaptureStartedEvent(1, new TeamId(0)), context), Is.EqualTo(new[] { p0, new PlayerId(2) }));
            // Applicability: every detector applies to some faction of the data, and core_capture follows the map.
            foreach (MechanicDetector detector in MechanicDetectors.All)
            {
                bool any = false;
                foreach (string faction in data.Content.Factions.Keys)
                {
                    any |= detector.Applicable(data.Content, faction, true);
                }
                Assert.That(any, Is.True, detector.Id);
            }
            Assert.That(MechanicDetectors.Require("core_capture").Applicable(data.Content, "skai", false), Is.False);
            Assert.That(MechanicDetectors.Require("momentum").Applicable(data.Content, "skai", true), Is.True);
            Assert.That(MechanicDetectors.Require("momentum").Applicable(data.Content, "water", true), Is.False);
            Assert.That(MechanicDetectors.Require("wave").Applicable(data.Content, "skai", true), Is.False);
        }

        /// <summary>R-4: the unspent medians are lower medians (element (n - 1) / 2 of the ascending list) over the slot-games still running; games that ended before a time are excluded and n shows it.</summary>
        [Test]
        public void Unspent_LowerMedianOverRunningGames()
        {
            // Five skai slot-games (the opponent has no summary): banks at time 0 are 100 to 500, at time 1 only three games were still running.
            List<GameRecord> records = new List<GameRecord>();
            int[] common = { 400, 100, 500, 200, 300 };
            for (int i = 0; i < 5; i++)
            {
                int[]? second = i < 3 ? new[] { common[i] * 2, 50 } : null;
                records.Add(Game(i, "skai", "water", GameRecord.Slot0, (ulong)i + 1, "", "", Summary(null, null, new[] { common[i], 10 * i }, second), null));
            }
            List<UnspentRow> rows = BalanceReport.Unspent(records, new[] { 300, 600 });
            UnspentRow first = rows.Find(r => r.Faction == "skai" && r.Seconds == 300)!;
            Assert.That(first.N, Is.EqualTo(5));
            Assert.That(first.MedianCommon, Is.EqualTo(300));
            Assert.That(first.MedianRare, Is.EqualTo(20));
            UnspentRow second2 = rows.Find(r => r.Faction == "skai" && r.Seconds == 600)!;
            Assert.That(second2.N, Is.EqualTo(3), "two games ended before 600 s");
            Assert.That(second2.MedianCommon, Is.EqualTo(800), "banks 800, 200 and 1000: the lower median is the middle one");
            // An even count takes the lower of the two middle values.
            records.RemoveAt(4);
            Assert.That(BalanceReport.Unspent(records, new[] { 300 }).Find(r => r.Faction == "skai")!.MedianCommon, Is.EqualTo(200), "100, 200, 400, 500 give 200");
            // The faction with no slot-game (water has no summary) has n 0 and no median.
            UnspentRow none = BalanceReport.Unspent(records, new[] { 300 }).Find(r => r.Faction == "water")!;
            Assert.That(none.N, Is.EqualTo(0));
        }

        /// <summary>The shipped balance settings text with edits applied (each old text must occur).</summary>
        private static BalanceSettings Settings(params string[] oldNewPairs)
        {
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), BalanceSettings.RelativePath));
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return BalanceSettings.Parse(text);
        }

        /// <summary>The status of the named check.</summary>
        private static string Status(IReadOnlyList<GameRecord> records, BalanceSettings settings, PredictorResult? predictor, string name, out string detail)
        {
            DataSet data = TestData.LoadAll();
            foreach (BalanceCheck check in BalanceReport.Checks(records, data, settings, new[] { 300 }, predictor))
            {
                if (check.Name == name)
                {
                    detail = check.Detail;
                    return check.Status;
                }
            }
            throw new AssertionException("no check " + name);
        }

        /// <summary>R-5: each of the four checks PASS and FAIL at the bar edge; the predictor check is NOT_MEASURED without fights data.</summary>
        [Test]
        public void Checks_PassAndFailAtTheEdge()
        {
            BalanceSettings shipped = Settings();
            // Use: ten skai slot-games (five mirror games), the windrunner used in exactly one of them is 0.100 (at the bar, not below); other units are at 0.000.
            List<GameRecord> use = new List<GameRecord>();
            for (int i = 0; i < 5; i++)
            {
                use.Add(Game(i, "skai", "skai", GameRecord.Slot0, (ulong)i + 1, "", "", i == 0 ? Summary(new Dictionary<string, int> { ["skai.windrunner"] = 1 }) : Summary(), Summary()));
            }
            Assert.That(Status(use, shipped, null, "balance_use", out string useDetail), Is.EqualTo("FAIL"));
            Assert.That(useDetail, Does.Not.Contain("skai/skai.windrunner "), "exactly at the bar is not below it");
            Assert.That(useDetail, Does.Contain("below 0.100"));
            Assert.That(Status(use, Settings("\"min_use_share\": 0.10", "\"min_use_share\": 0"), null, "balance_use", out _), Is.EqualTo("PASS"));
            // Predictor: 85 of 100 passes, 84 fails, nothing measured is NOT_MEASURED.
            Assert.That(Status(use, shipped, new PredictorResult(100, 85), "balance_predictor", out _), Is.EqualTo("PASS"));
            Assert.That(Status(use, shipped, new PredictorResult(100, 84), "balance_predictor", out _), Is.EqualTo("FAIL"));
            Assert.That(Status(use, shipped, null, "balance_predictor", out _), Is.EqualTo("NOT_MEASURED"));
            // Unspent: the median common 400 and rare 150 pass, 401 and 151 fail.
            List<GameRecord> bank400 = new List<GameRecord> { Game(0, "skai", "water", GameRecord.Slot0, 1, "", "", Summary(null, null, new[] { 400, 150 }), null) };
            List<GameRecord> bank401 = new List<GameRecord> { Game(0, "skai", "water", GameRecord.Slot0, 1, "", "", Summary(null, null, new[] { 401, 150 }), null) };
            List<GameRecord> rare151 = new List<GameRecord> { Game(0, "skai", "water", GameRecord.Slot0, 1, "", "", Summary(null, null, new[] { 400, 151 }), null) };
            Assert.That(Status(bank400, shipped, null, "balance_unspent", out _), Is.EqualTo("PASS"));
            Assert.That(Status(bank401, shipped, null, "balance_unspent", out string over), Is.EqualTo("FAIL"));
            Assert.That(over, Does.Contain("skai@300s common=401"));
            Assert.That(Status(rare151, shipped, null, "balance_unspent", out _), Is.EqualTo("FAIL"));
            // Openings: ten distinct mirror games of opening a against b, a winning 1 of 10 (0.10, the lower bar), 0 of 10 fails; 9 of 10 (0.90) passes, 10 of 10 fails.
            Assert.That(Status(Mirror(1), shipped, null, "balance_openings", out _), Is.EqualTo("PASS"));
            Assert.That(Status(Mirror(0), shipped, null, "balance_openings", out string lost), Is.EqualTo("FAIL"));
            Assert.That(lost, Does.Contain("skai/a 0/10"));
            Assert.That(Status(Mirror(9), shipped, null, "balance_openings", out _), Is.EqualTo("PASS"));
            Assert.That(Status(Mirror(10), shipped, null, "balance_openings", out _), Is.EqualTo("FAIL"));
        }

        /// <summary>Ten distinct skai mirror games, opening a in slot 0 against b in slot 1, with a winning the given number of them. Both openings have exactly ten mirror games, so b wins the rest.</summary>
        private static List<GameRecord> Mirror(int aWins)
        {
            List<GameRecord> records = new List<GameRecord>();
            for (int i = 0; i < 10; i++)
            {
                records.Add(Game(i, "skai", "skai", i < aWins ? GameRecord.Slot0 : GameRecord.Slot1, (ulong)i + 1, "a", "b", Summary(), Summary()));
            }
            return records;
        }

        /// <summary>The arguments of a small --balance run: one mirror cell of 4 games with 60 s caps and the committed fight sweep.</summary>
        private static string[] BalanceArgs(string folder, params string[] extra)
        {
            string fights = Path.GetFullPath(Path.Combine(TestData.FindDataDirectory(), "..", "docs", "reports", "fights_2026-11-18", "fights.jsonl"));
            List<string> args = new List<string> { "--data", TestData.FindDataDirectory(), "--balance", "--games", "4", "--matchups", "water_vs_water", "--max-game-seconds", "60", "--out", folder };
            args.AddRange(extra);
            if (Array.IndexOf(extra, "--from") < 0)
            {
                args.AddRange(new[] { "--fights-from", fights });
            }
            return args.ToArray();
        }

        /// <summary>R-6: --balance with --jobs 1 and --jobs 4 writes identical files; --from re-renders the written balance.jsonl to a byte-identical balance.md; the stdout has four CHECK lines and the BALANCE line, and the exit code is 0.</summary>
        [Test]
        public void Command_IsDeterministic_AndReRenders()
        {
            string one = Path.Combine(HarnessTestSupport.NewFolder(), "one");
            string four = Path.Combine(HarnessTestSupport.NewFolder(), "four");
            string again = Path.Combine(HarnessTestSupport.NewFolder(), "again");
            Assert.That(HarnessTestSupport.Run(BalanceArgs(one, "--jobs", "1"), out string stdout1, out string error1), Is.EqualTo(0), error1);
            Assert.That(HarnessTestSupport.Run(BalanceArgs(four, "--jobs", "4"), out string stdout4, out string error4), Is.EqualTo(0), error4);
            Assert.That(stdout4, Is.EqualTo(stdout1));
            Assert.That(File.ReadAllText(Path.Combine(four, "balance.jsonl")), Is.EqualTo(File.ReadAllText(Path.Combine(one, "balance.jsonl"))));
            Assert.That(File.ReadAllText(Path.Combine(four, "balance.md")), Is.EqualTo(File.ReadAllText(Path.Combine(one, "balance.md"))));
            Assert.That(HarnessTestSupport.Run(BalanceArgs(again, "--from", Path.Combine(one, "balance.jsonl")), out string stdoutFrom, out string errorFrom), Is.EqualTo(0), errorFrom);
            Assert.That(File.ReadAllText(Path.Combine(again, "balance.md")), Is.EqualTo(File.ReadAllText(Path.Combine(one, "balance.md"))));
            Assert.That(File.ReadAllText(Path.Combine(again, "balance.jsonl")), Is.EqualTo(File.ReadAllText(Path.Combine(one, "balance.jsonl"))));
            Assert.That(stdoutFrom, Is.EqualTo(stdout1));
            string[] lines = stdout1.TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(5), stdout1);
            Assert.That(lines[0], Does.StartWith("CHECK balance_use "));
            Assert.That(lines[1], Does.StartWith("CHECK balance_predictor "));
            Assert.That(lines[2], Does.StartWith("CHECK balance_unspent "));
            Assert.That(lines[3], Does.StartWith("CHECK balance_openings "));
            Assert.That(lines[4], Does.StartWith("BALANCE profile=aggressive_s5 games=4 decided=").And.Contain(" checks_passed="));
            string report = File.ReadAllText(Path.Combine(one, "balance.md"));
            Assert.That(report, Does.Contain("## Faction matrix").And.Contain("## Opening matrix").And.Contain("## Unit use").And.Contain("## Mechanic use").And.Contain("## Unspent resources").And.Contain("## Trust checks"));
        }
    }
}
