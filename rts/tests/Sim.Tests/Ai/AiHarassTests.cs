// rts/tests/Sim.Tests/Ai/AiHarassTests.cs
// Tests for package AI-H of rts/docs/specs/field_saturation.md (faction-fair harass speed): the shipped key and the parser (H-0), the chaser
// speed from a model (H-1), from the enemy roster when the model is empty (H-2), the "nothing hits this layer" case (H-3), ratio 0 equal to the
// old rule (H-4) and the ablation abl_s5_no_h playing the pre-AI-H aggressive_s5 bit for bit (H-5, full-game hashes recorded on aggressive_s5 at
// origin/main BEFORE any AI-H code). Part of Rts.Sim.Tests. Views come from a real Skai-versus-Water match on prototype_1v1.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package AI-H: harass raiders are chosen by speed against the enemy's chasers.</summary>
    [TestFixture]
    public class AiHarassTests
    {
        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>A started Skai (player 0) versus Water (player 1) match on prototype_1v1 and the view of player 0 (so the enemy faction is "water").</summary>
        private static AiWorldView SkaiView()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "Skai"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            return AiWorldView.Build(sim, new PlayerId(0));
        }

        /// <summary>The shipped aggressive_s5 text with edits applied (each old text must occur), parsed under its own name.</summary>
        private static AiProfile Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5.json"));
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5.json", Data.Content);
        }

        /// <summary>aggressive_s5 with package AI-H switched on (harass_speed_ratio 1.2, the value it shipped with until 21 Nov 2026), set in the test because the shipped file has it at 0.</summary>
        private static AiProfile OnProfile() => Edited("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": 1.2,");

        /// <summary>A model: the given definition IDs with their counts. Unit: units.</summary>
        private static SortedDictionary<string, Fix64> Model(params (string Id, int Count)[] entries)
        {
            SortedDictionary<string, Fix64> model = new SortedDictionary<string, Fix64>(System.StringComparer.Ordinal);
            foreach ((string id, int count) in entries)
            {
                model[id] = Fix64.FromInt(count);
            }
            return model;
        }

        /// <summary>H-0: shipped values (every profile 0 since AI-H was switched off on aggressive_s5, 21 Nov 2026; the in-test OnProfile 1.2) and the parser (range 0 to 4, type, missing key).</summary>
        [Test]
        public void H0_ShippedValuesAndParser()
        {
            Assert.That(OnProfile().HarassSpeedRatio, Is.EqualTo(Fix64.Parse("1.2")));
            foreach (string id in Data.AiProfiles.Keys)
            {
                Assert.That(AiProfile.FromDataSet(Data, id).HarassSpeedRatio, Is.EqualTo(Fix64.Zero), id);
            }
            // Pairs of (old text, new text) that must be rejected with the file name and the key in the message.
            (string Old, string New)[] bad =
            {
                ("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": 4.5,"),
                ("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": -0.1,"),
                ("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": \"fast\","),
                ("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": true,"),
                ("\"harass_speed_ratio\": 0,", string.Empty),
            };
            foreach ((string oldText, string newText) in bad)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => Edited(oldText, newText));
                Assert.That(error.Message, Does.StartWith("ai/aggressive_s5.json").And.Contain("harass_speed_ratio"), newText);
            }
            // The limits themselves are accepted.
            Assert.That(Edited("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": 4,").HarassSpeedRatio, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(Edited("\"harass_speed_ratio\": 0,", "\"harass_speed_ratio\": 1.2,").HarassSpeedRatio, Is.EqualTo(Fix64.Parse("1.2")));
        }

        /// <summary>The view of player 0 knows its enemy factions from the match setup, sorted, without its own.</summary>
        [Test]
        public void H0_ViewListsEnemyFactions()
        {
            Assert.That(SkaiView().EnemyFactionIds, Is.EqualTo(new[] { "water" }));
        }

        /// <summary>
        /// H-1: a model of 2 Shellbacks (1.8) and 1 Grasper (the Pummeler of the spec text, 3.0), all of which hit ground: the chaser speed is
        /// (1.8 + 1.8 + 3) / 3 = 2.2; a 3.0 unit qualifies at ratio 1.2 (bar 2.64), a 2.5 unit does not.
        /// </summary>
        [Test]
        public void H1_ChaserSpeedFromTheModel()
        {
            AiWorldView view = SkaiView();
            AiProfile profile = OnProfile();
            SortedDictionary<string, Fix64> model = Model(("water.shellback", 2), ("water.grasper", 1));
            Assert.That(AiIntel.ChaserSpeed(view, model, MovementLayer.Ground, out Fix64 chaser), Is.True);
            Assert.That(Fix64.Max(chaser, Fix64.Parse("2.2")) - Fix64.Min(chaser, Fix64.Parse("2.2")) < Fix64.Parse("0.001"), Is.True, chaser.ToString());
            Assert.That(AiIntel.MayHarass(view, profile, model, MovementLayer.Ground, Fix64.Parse("3.0")), Is.True);
            Assert.That(AiIntel.MayHarass(view, profile, model, MovementLayer.Ground, Fix64.Parse("2.5")), Is.False);
            // A type that cannot hit the layer is left out of the mean: the Shellback's static list has no air, so only the Spoutlings (3.0) set it.
            Assert.That(AiIntel.ChaserSpeed(view, Model(("water.shellback", 2), ("water.spoutling", 2)), MovementLayer.Air, out Fix64 airChaser), Is.True);
            Assert.That(airChaser, Is.EqualTo(Fix64.Parse("3.0")), "only the Spoutlings (3.0) hit air");
        }

        /// <summary>H-2: an empty model gives the plain mean over the enemy faction's armed mobile army definitions that can hit the layer.</summary>
        [Test]
        public void H2_EmptyModelUsesTheRosterMean()
        {
            AiWorldView view = SkaiView();
            foreach (MovementLayer layer in new[] { MovementLayer.Ground, MovementLayer.Water, MovementLayer.Air })
            {
                // Mean over water's roster computed here from the data, independently of the code under test.
                Fix64 sum = Fix64.Zero;
                int count = 0;
                foreach (UnitDefinition definition in Data.Content.Units.Values)
                {
                    if (definition.FactionId == "water" && definition.Economy.FootprintTiles <= 0 && !definition.Economy.CanGather && definition.Hq == null
                        && definition.Weapon != null && definition.Weapon.CanTarget(layer))
                    {
                        sum += definition.SpeedTilesPerSecond;
                        count++;
                    }
                }
                Assert.That(count, Is.GreaterThan(0), layer.ToString());
                Assert.That(AiIntel.ChaserSpeed(view, Model(), layer, out Fix64 chaser), Is.True);
                Assert.That(chaser, Is.EqualTo(sum / Fix64.FromInt(count)), layer.ToString());
            }
            // Against water's roster the Windrunner (3.5) harasses and the Talon Archer (2.5) does not.
            AiProfile profile = OnProfile();
            Assert.That(AiIntel.MayHarass(view, profile, Model(), MovementLayer.Ground, Data.Content.Units["skai.windrunner"].SpeedTilesPerSecond), Is.True);
            Assert.That(AiIntel.MayHarass(view, profile, Model(), MovementLayer.Ground, Data.Content.Units["skai.talon_archer"].SpeedTilesPerSecond), Is.False);
        }

        /// <summary>H-3: when no enemy unit can hit the layer (here the enemy faction is neutral and fields no armed mobile unit) the candidate qualifies, a flyer too; with an enemy that can hit air a slow flyer no longer qualifies by being a flyer.</summary>
        [Test]
        public void H3_NothingHitsTheLayer()
        {
            AiWorldView view = SkaiView();
            AiProfile profile = OnProfile();
            // Shellbacks and Graspers: neither hits air by its static layer list.
            SortedDictionary<string, Fix64> model = Model(("water.shellback", 2), ("water.grasper", 1));
            view.EnemyFactionIds = new[] { "neutral" };
            Assert.That(AiIntel.ChaserSpeed(view, model, MovementLayer.Air, out Fix64 none), Is.False);
            Assert.That(none, Is.EqualTo(Fix64.Zero));
            Assert.That(AiIntel.MayHarass(view, profile, model, MovementLayer.Air, Fix64.Parse("0.5")), Is.True, "nothing hits air: even a very slow flyer qualifies");
            Assert.That(AiIntel.MayHarass(view, profile, Model(), MovementLayer.Ground, Fix64.Parse("0.5")), Is.True, "empty model and a neutral enemy: nothing hits the ground either");
            view.EnemyFactionIds = System.Array.Empty<string>();
            Assert.That(AiIntel.MayHarass(view, profile, model, MovementLayer.Air, Fix64.Parse("0.5")), Is.True, "no enemy at all");
            // With water as the enemy, the Spoutling (3.0) and Leviathan (1.5) hit air: roster mean 2.25, bar 2.7; a 1.0 flyer no longer qualifies.
            view.EnemyFactionIds = new[] { "water" };
            Assert.That(AiIntel.MayHarass(view, profile, Model(), MovementLayer.Air, Fix64.Parse("1.0")), Is.False);
        }

        /// <summary>H-4: with harass_speed_ratio 0 the rule is today's (air, or speed at least harass_min_speed_tiles_per_second) for every layer, speed and model.</summary>
        [Test]
        public void H4_RatioZeroIsTheOldRule()
        {
            AiWorldView view = SkaiView();
            AiProfile old = AiProfile.FromDataSet(Data, "abl_s5_no_h");
            Assert.That(old.HarassSpeedRatio, Is.EqualTo(Fix64.Zero));
            foreach (SortedDictionary<string, Fix64> model in new[] { Model(), Model(("water.shellback", 3)), Model(("water.spoutling", 1), ("water.grasper", 2)) })
            {
                foreach (MovementLayer layer in new[] { MovementLayer.Ground, MovementLayer.Water, MovementLayer.Air })
                {
                    // Speed in quarter tiles per second. Range: 0 to 6.
                    for (int quarter = 0; quarter <= 24; quarter++)
                    {
                        Fix64 speed = Fix64.FromInt(quarter) / Fix64.FromInt(4);
                        bool expected = layer == MovementLayer.Air || speed >= old.HarassMinSpeedTilesPerSecond;
                        Assert.That(AiIntel.MayHarass(view, old, model, layer, speed), Is.EqualTo(expected), layer + " " + speed);
                    }
                }
            }
        }

        /// <summary>
        /// H-5: abl_s5_no_h plays the aggressive_s5 of before AI-H bit for bit: the full-game state hashes of three prototype_1v1 games (seed
        /// 20261117, game seeds derived as the harness does) were recorded on aggressive_s5 at origin/main (the AI-P-off tree, passing the AI-P
        /// test P-6 with the same values) before any AI-H code.
        /// </summary>
        [Test]
        public void H5_AblationPlaysThePreAiHGame()
        {
            // (matchup, game, expected final state hash) of the recorded games.
            // Re-pinned for research and unlocks (specs/research_and_unlocks.md R1.9): the AI never researches, so its mixes lose the Stormcaller, Ink Lobber, Thunderhead and Leviathan. Old values skai_vs_water 0xae4f362f8bdc9885, skai_vs_skai 0x235626745316a81d, water_vs_water 0xb171e5cd2bf0bfeb; end ticks were not recorded.
            // Re-pinned for the rally state, now always hashed per unit (specs/interface_support.md I5.4, determinism review of I1 to I5): hashes only; end ticks and winners are unchanged (10997 slot 0, 9559 slot 0, 10907 slot 1). Old values skai_vs_water 0x31dbd80f94a26eae, skai_vs_skai 0x311eec87f47b4d4e, water_vs_water 0x4b44414af49e53e4.
// Re-pinned for roster_numbers_1 (Reef Runner max_health 60 to 66 changes the content digest and every game that trains the Reef Runner): old values skai_vs_water 0x31de6615e47a5804 (end tick 10997), skai_vs_skai 0xc4b816b03df9c616 (9559), water_vs_water 0xa24b5faa0015c722 (10907); new end ticks 11216, 9559 and 15509.
            // Re-pinned 25 Nov 2026 for water.reef_runner weight 30 in the Water compositions (developer decision): old skai_vs_water 0x19bd86827db8fe7f (end tick 11216) and water_vs_water 0x1f1a676c5fa4472a (15509); new end ticks 11531 and 14764; skai_vs_skai unchanged (9559).
            (string Matchup, int Game, ulong Hash)[] recorded =
            {
                ("skai_vs_water", 2, 0x7ff001b72095a475UL),
                ("skai_vs_skai", 3, 0x53c785498c77ec59UL),
                ("water_vs_water", 2, 0xa0b95b56e61cf63aUL),
            };
            foreach ((string matchup, int game, ulong hash) in recorded)
            {
                ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", matchup, game);
                GameRecord record = GameRunner.Play(Data, "prototype_1v1", matchup, game, seed, "abl_s5_no_h", "abl_s5_no_h", 2400);
                Assert.That(record.StateHash, Is.EqualTo(hash), matchup + " game " + game);
            }
        }
    }
}
