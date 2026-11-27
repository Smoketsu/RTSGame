// rts/tests/Sim.Tests/Ai/AiMidgameTests.cs
// Acceptance tests of package MG-1 (rts/docs/specs/ai_midgame_window.md section 3.3, tests 1 to 6 and 10): the optional profile keys fight_kite_factor (rule FK) and
// use_defence_hold (rule DH), the candidate profile aggressive_s5_mid, and rule FK in D.1 and D.2. The rule DH tests 7 to 9 live in AiRaidDefenceTests (DH7 to DH9)
// because they use that class's private raid scene helpers. Part of Rts.Sim.Tests. No test pins a game hash or an exact unit count of a full game.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Tests of rules FK and DH (ai_midgame_window.md MG-1).</summary>
    [TestFixture]
    public class AiMidgameTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the enemy).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The text of a shipped profile file, line endings normalised.</summary>
        /// <param name="id">Profile ID. Unit: text. Range: a shipped profile.</param>
        private static string Text(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json")).Replace("\r\n", "\n");
        }

        /// <summary>aggressive_s5 with the given old/new text pairs applied (each old text must occur) and parsed.</summary>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile Base(params string[] oldNewPairs)
        {
            // The file text being edited. Unit: text.
            string text = Text("aggressive_s5");
            // Index of the old text of the pair being applied. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5.json", Data.Content);
        }

        /// <summary>aggressive_s5 with the fight kite factor key added, then the caller's pairs.</summary>
        /// <param name="factor">The factor as JSON text. Unit: fraction. Range: as the test needs.</param>
        /// <param name="more">Further old text, new text pairs.</param>
        private static AiProfile WithFactor(string factor, params string[] more)
        {
            // The pairs: the key added after use_supply_ahead, then the caller's.
            List<string> pairs = new List<string> { "\"use_supply_ahead\": false,", "\"use_supply_ahead\": false, \"fight_kite_factor\": " + factor + "," };
            pairs.AddRange(more);
            return Base(pairs.ToArray());
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
            sim.Step();
            // One event of the step. Unit: none.
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is UnitSpawnedEvent spawned)
                {
                    return spawned.Unit;
                }
            }
            throw new AssertionException("spawn of " + definition + " failed");
        }

        /// <summary>A started greybox_1v1 simulation, Water in slot 0 (the AI) and Skai in slot 1, with Water's HQ at (7, 7).</summary>
        private Simulation WaterVsSkai()
        {
            // The two players. Unit: none.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "water", "A"),
                new PlayerSetup(P1, new TeamId(1), "skai", "B"),
            };
            // The match of this case (tick 0 upward). Unit: none (object).
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            return sim;
        }

        /// <summary>Spawns <paramref name="count"/> units in rows of 8 from tile (<paramref name="x"/>, <paramref name="y"/>).</summary>
        private List<EntityId> Row(Simulation sim, string definition, PlayerId owner, int count, int x, int y)
        {
            // The units spawned. Unit: entity IDs.
            List<EntityId> ids = new List<EntityId>();
            // Unit number. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                ids.Add(Spawn(sim, definition, owner, (x + i % 8).ToString(), (y + i / 8).ToString()));
            }
            return ids;
        }

        /// <summary>Decides for P0 with the profile's Water plan and a fresh generator, and returns the intents.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            if (memory.AttackThreshold < 0)
            {
                memory.AttackThreshold = 8;
            }
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("water"), memory, new DeterministicRandom(1));
        }

        /// <summary>The intents as text, for equality checks.</summary>
        private static string Key(List<AiIntent> intents)
        {
            return string.Join(";", intents.ConvertAll(i => i.Kind + ":" + i.Unit.Value + ":" + i.DefinitionId + ":" + i.Field + ":" + i.Point.X.ToString() + "," + i.Point.Y.ToString()));
        }

        /// <summary>The D.1 case: an own army of <paramref name="count"/> units of <paramref name="own"/> at (30, 30), the enemy model holding <paramref name="foes"/> Windrunners; returns the stance after one think.</summary>
        private AiStance AttackStance(AiProfile profile, string own, int count, int foes)
        {
            Simulation sim = WaterVsSkai();
            Row(sim, own, P0, count, 30, 30);
            // The AI memory of this case. Unit: none (object).
            AiMemory memory = new AiMemory();
            memory.EnemyTypes["skai.windrunner"] = new EnemyTypeEstimate { Count = Fix64.FromInt(foes), LastSeenTick = sim.Clock.Tick };
            Decide(sim, profile, memory);
            return memory.Stance;
        }

        /// <summary>The D.2 case: an attack group of <paramref name="count"/> Pummelers at (30, 30), engaged with <paramref name="foes"/> visible Windrunners at (34, 30), 400 ticks into the attack; returns the memory after one think.</summary>
        private AiMemory RetreatCase(AiProfile profile, int count, int foes)
        {
            Simulation sim = WaterVsSkai();
            // The attack group. Unit: entity IDs.
            List<EntityId> group = Row(sim, "water.grasper", P0, count, 30, 30);
            Row(sim, "skai.windrunner", P1, foes, 34, 30);
            // The AI memory of this case, in the middle of an attack. Unit: none (object).
            AiMemory memory = new AiMemory { Stance = AiStance.Attack, AttackStaged = true, AttackStartSupply = 8, AttackThreshold = 8, HasAttackTarget = true, AttackStartTick = sim.Clock.Tick - 400 };
            // One unit of the group. Unit: entity ID.
            foreach (EntityId id in group)
            {
                memory.AttackGroup.Add(id);
            }
            Decide(sim, profile, memory);
            return memory;
        }

        /// <summary>MG test 1: aggressive_s5 is off; every profile file loads and only the candidate has the keys on.</summary>
        [Test]
        public void MG1_OldProfilesAreOff()
        {
            // The parsed baseline. Unit: none (object).
            AiProfile baseline = AiProfile.FromDataSet(Data, "aggressive_s5");
            Assert.That(baseline.FightKiteFactor, Is.EqualTo(Fix64.One));
            Assert.That(baseline.UseDefenceHold, Is.False);
            // One shipped profile: its ID and tree. Unit: none. Range: each profile once.
            foreach (KeyValuePair<string, JsonValue> pair in Data.AiProfiles)
            {
                // The profile loaded from the data set. Unit: none (object).
                AiProfile loaded = AiProfile.FromDataSet(Data, pair.Key);
                if (pair.Key != "aggressive_s5_mid")
                {
                    Assert.That(loaded.FightKiteFactor, Is.EqualTo(Fix64.One), pair.Key);
                    Assert.That(loaded.UseDefenceHold, Is.False, pair.Key);
                }
            }
        }

        /// <summary>MG test 2: the candidate holds 0.4 and true and differs from aggressive_s5 in nothing else (every JSON leaf but id, $doc and the two keys).</summary>
        [Test]
        public void MG2_CandidateDiffersOnlyInTheKeys()
        {
            // The parsed candidate. Unit: none (object).
            AiProfile candidate = AiProfile.FromDataSet(Data, "aggressive_s5_mid");
            Assert.That(candidate.FightKiteFactor, Is.EqualTo(Fix64.Parse("0.4")));
            Assert.That(candidate.UseDefenceHold, Is.True);
            // Leaves of the baseline: path to text. Unit: none. Range: one entry per leaf.
            SortedDictionary<string, string> a = new SortedDictionary<string, string>(StringComparer.Ordinal);
            // Leaves of the candidate: path to text. Unit: none. Range: one entry per leaf.
            SortedDictionary<string, string> b = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Flatten(Data.AiProfiles["aggressive_s5"], string.Empty, a);
            Flatten(Data.AiProfiles["aggressive_s5_mid"], string.Empty, b);
            Assert.That(b.Remove("fight_kite_factor"), Is.True);
            Assert.That(b.Remove("use_defence_hold"), Is.True);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            // One leaf of the baseline. Unit: none. Range: each leaf once.
            foreach (KeyValuePair<string, string> pair in a)
            {
                Assert.That(b.TryGetValue(pair.Key, out string? other), Is.True, pair.Key);
                Assert.That(other, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        /// <summary>Adds every leaf of a parsed tree to <paramref name="into"/> as path = text, leaving out the root id and $doc.</summary>
        /// <param name="node">The tree being flattened.</param>
        /// <param name="path">The path of <paramref name="node"/>, empty for the root.</param>
        /// <param name="into">Receives path to text.</param>
        private static void Flatten(JsonValue node, string path, SortedDictionary<string, string> into)
        {
            if (path == "id" || path == "$doc")
            {
                return;
            }
            switch (node.Kind)
            {
                case JsonKind.Object:
                    // One member of the object. Unit: none.
                    foreach (KeyValuePair<string, JsonValue> member in node.Members)
                    {
                        Flatten(member.Value, path.Length == 0 ? member.Key : path + "/" + member.Key, into);
                    }
                    break;
                case JsonKind.Array:
                    // Index of the item. Range: 0 to node.Items.Count - 1.
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        Flatten(node.Items[i], path + "[" + i + "]", into);
                    }
                    break;
                case JsonKind.String:
                    into[path] = node.AsString();
                    break;
                case JsonKind.Bool:
                    into[path] = node.AsBool().ToString();
                    break;
                case JsonKind.Number:
                    into[path] = node.NumberText;
                    break;
                default:
                    into[path] = "null";
                    break;
            }
        }

        /// <summary>MG test 3: each rejected edit fails to load with a message naming its key.</summary>
        [Test]
        public void MG3_LoaderErrors()
        {
            // Each case: the key the message must name, and the builder of the rejected profile.
            (string Key, Func<AiProfile> Make)[] cases =
            {
                ("fight_kite_factor", () => WithFactor("1.5")),
                ("fight_kite_factor", () => WithFactor("-0.1")),
                ("fight_kite_factor", () => WithFactor("0.4", "\"predictor_model\": \"formula\"", "\"predictor_model\": \"sim\"")),
                ("fight_kite_factor", () => WithFactor("0.4", "\"use_unit_control\": true", "\"use_unit_control\": false")),
                ("use_defence_hold", () => Base("\"use_supply_ahead\": false,", "\"use_supply_ahead\": false, \"use_defence_hold\": true,", "\"use_measured_defence\": true", "\"use_measured_defence\": false")),
            };
            // The key to find in the message and the builder. Unit: text and delegate.
            foreach ((string key, Func<AiProfile> make) in cases)
            {
                // The loader error. Unit: none (object).
                DataValidationException error = Expect.Throws<DataValidationException>(() => make());
                Assert.That(error.Message, Does.Contain(key));
            }
        }

        /// <summary>MG test 4: fight_kite_factor 1 present gives the same intents as the key absent.</summary>
        [Test]
        public void MG4_FactorOneIsAbsent()
        {
            // Intents of each profile on the same view. Unit: text.
            string[] runs = new string[2];
            // The two profiles: absent and 1. Unit: none (objects).
            AiProfile[] profiles = { Base(), WithFactor("1") };
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = WaterVsSkai();
                Row(sim, "water.grasper", P0, 8, 30, 30);
                // The AI memory of this run. Unit: none (object).
                AiMemory memory = new AiMemory();
                memory.EnemyTypes["skai.windrunner"] = new EnemyTypeEstimate { Count = Fix64.FromInt(12), LastSeenTick = sim.Clock.Tick };
                runs[run] = Key(Decide(sim, profiles[run], memory)) + "|" + memory.Stance;
            }
            Assert.That(runs[1], Is.EqualTo(runs[0]));
        }

        /// <summary>MG test 5: rule FK in D.1. Pummelers believed to be kited by Windrunners: the base profile attacks, factor 0.4 stays Massing. Reef Runners are not kited: same decision.</summary>
        [Test]
        public void MG5_FkAttack()
        {
            // The profile with the kite factor 0.4. Unit: none (object).
            AiProfile fk = WithFactor("0.4");
            Assert.That(AttackStance(Base(), "water.grasper", 8, 12), Is.EqualTo(AiStance.Attack));
            Assert.That(AttackStance(fk, "water.grasper", 8, 12), Is.EqualTo(AiStance.Massing));
            Assert.That(AttackStance(fk, "water.reef_runner", 8, 12), Is.EqualTo(AttackStance(Base(), "water.reef_runner", 8, 12)));
        }

        /// <summary>MG test 6: rule FK in D.2. An attack group of Pummelers engaged with Windrunners: the base profile stays in the attack, factor 0.4 retreats once.</summary>
        [Test]
        public void MG6_FkRetreat()
        {
            // The memories after one think with each profile. Unit: none (objects).
            AiMemory baseMemory = RetreatCase(Base(), 8, 12);
            AiMemory fkMemory = RetreatCase(WithFactor("0.4"), 8, 12);
            Assert.That(baseMemory.Retreats, Is.EqualTo(0));
            Assert.That(fkMemory.Retreats, Is.EqualTo(1));
            Assert.That(fkMemory.Stance, Is.EqualTo(AiStance.Massing));
        }

        /// <summary>MG test 10: skai_vs_water, aggressive_s5_mid against itself on prototype_1v1 to 300 s, twice with the same seed: equal state hashes.</summary>
        [Test]
        public void MG10_Deterministic()
        {
            // The first finished game. Unit: none (object).
            GameRecord a = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 0, 20261026UL, "aggressive_s5_mid", "aggressive_s5_mid", 300, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            // The second finished game, same inputs. Unit: none (object).
            GameRecord b = GameRunner.PlayLadder(Data, "prototype_1v1", "skai_vs_water", 0, 20261026UL, "aggressive_s5_mid", "aggressive_s5_mid", 300, true, 0UL, null, new SummaryObserver(30, new[] { 300 }));
            Assert.That(b.StateHash, Is.EqualTo(a.StateHash));
        }
    }
}
