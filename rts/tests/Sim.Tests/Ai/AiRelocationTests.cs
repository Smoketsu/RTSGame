// rts/tests/Sim.Tests/Ai/AiRelocationTests.cs
// Tests for package AI-HQ of rts/docs/specs/field_saturation.md (HQ cluster relocation, rule HQ-C): shipped key values and parser rejections
// (HQ-0), the five spec rows HQ-1 to HQ-5 with the spec's numbers, the one-way latch against moving back (HQ-6) and the ablation abl_s5_no_hq
// playing the pre-AI-HQ aggressive_s5 bit for bit (HQ-7: full-game hashes recorded on aggressive_s5 at origin/main BEFORE any AI-HQ code).
// Part of Rts.Sim.Tests. Real simulations on greybox_1v1 (P0's home fields are 0 and 1, start 3000 each; the common fields 6 (21, 10) and 7 (21, 4)
// form the 6-slot cluster at tile (21, 7); 8 (42, 53) and 9 (42, 59) lie beside the enemy producer; 10 (12, 49) with the rare field 4 and 11 (51, 14) with the rare field 5 are the other clusters),
// AiBrain.Decide on the views.
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
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package AI-HQ: the HQ moves to a cluster of fields with at least twice home's live gather slots.</summary>
    [TestFixture]
    public class AiRelocationTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A started Skai-versus-Water match on greybox_1v1: HQs rooted at the spawns, two workers each and one producer each, every site explored.</summary>
        private Simulation Game()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to 1.
            for (int i = 0; i < 2; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i), Fix64.One);
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

        /// <summary>Spawns a unit at a tile centre.</summary>
        private EntityId SpawnAtTile(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            return Spawn(sim, definition, owner, new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half));
        }

        /// <summary>The lowest-ID HQ of P0.</summary>
        private static Unit Hq(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Hq != null)
                {
                    return unit;
                }
            }
            throw new AssertionException("no HQ");
        }

        /// <summary>Leaves only <paramref name="remaining"/> in a field.</summary>
        private static void Leave(Simulation sim, int field, int remaining)
        {
            sim.Resources[field].Take(sim.Resources[field].Remaining - remaining);
        }

        /// <summary>An AI memory that knows the given fields to be empty and every other field at its start amount (visible fields are updated at the first think).</summary>
        private static AiMemory Knowing(Simulation sim, params int[] empty)
        {
            AiMemory memory = new AiMemory();
            // Known amounts per field. Unit: resource units.
            int[] known = new int[sim.Resources.Count];
            // Field index. Range: 0 to field count - 1.
            for (int i = 0; i < known.Length; i++)
            {
                known[i] = sim.Resources[i].Initial;
            }
            foreach (int field in empty)
            {
                known[field] = 0;
            }
            memory.KnownFieldRemaining = known;
            return memory;
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>The shipped profile <paramref name="baseId"/> with text edits applied (each old text must occur), parsed under its own name.</summary>
        private static AiProfile Edited(string baseId, params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", baseId + ".json"));
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/" + baseId + ".json", Data.Content);
        }

        /// <summary>The shipped profile with at most <paramref name="cap"/> live depots, so that many depots are "at the cap" (HQ-1).</summary>
        private static AiProfile DepotCap(string baseId, int cap)
        {
            return Edited(baseId, "\"max_live_depots\": 4,", "\"max_live_depots\": " + cap + ",");
        }

        /// <summary>Decides for P0 and returns the intents of one kind.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory, AiIntentKind kind)
        {
            List<AiIntent> all = AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
            List<AiIntent> found = new List<AiIntent>();
            foreach (AiIntent intent in all)
            {
                if (intent.Kind == kind)
                {
                    found.Add(intent);
                }
            }
            return found;
        }

        /// <summary>
        /// The scene: home fields 0 and 1 hold <paramref name="homeKnown"/> in total (field 1 at 200, below expand_min_field_remaining, so home has
        /// 3 live slots), one complete depot stands at tile (9, 49) (serving field 10 and the rare field 4), with <paramref name="natural"/> a second
        /// at tile (21, 7) (serving fields 6 and 7); the bank is 500. Served fields are no candidates. Fields 6 and 7 together hold 6 slots (new unless
        /// the natural depot stands), fields 8 and 9 lie beside the enemy producer (unsafe), the other clusters hold 5.
        /// </summary>
        private Simulation Scene(int homeKnown, bool natural = false)
        {
            Simulation sim = Game();
            Leave(sim, 1, 200);
            Leave(sim, 0, homeKnown - 200);
            SpawnAtTile(sim, "skai.perch_depot", P0, 9, 49);
            if (natural)
            {
                SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            }
            StepTo(sim, 200);
            Assert.That(sim.Players.TryGet(P0, out Player? record), Is.True);
            record!.SetStock(500, 0);
            return sim;
        }

        /// <summary>HQ-0: shipped values (aggressive_s5 true, 2, 0.35; every other profile false, 1; abl_s5_no_hq 0.1) and the parser.</summary>
        [Test]
        public void HQ0_ShippedValuesAndParser()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                bool on = id == "aggressive_s5";
                Assert.That(profile.UseClusterRelocation, Is.EqualTo(on), id);
                Assert.That(profile.RelocateGainRatio, Is.EqualTo(on ? Fix64.FromInt(2) : Fix64.One), id);
            }
            Assert.That(AiProfile.FromDataSet(Data, "aggressive_s5").RelocateWhenHomeRemainingFraction, Is.EqualTo(Fix64.Parse("0.35")));
            Assert.That(AiProfile.FromDataSet(Data, "abl_s5_no_hq").RelocateWhenHomeRemainingFraction, Is.EqualTo(Fix64.Parse("0.1")));
            // Pairs of (key, old text, new text) that must be rejected with the file name and the key in the message.
            (string Key, string Old, string New)[] bad =
            {
                ("use_cluster_relocation", "\"use_cluster_relocation\": true,", "\"use_cluster_relocation\": \"yes\","),
                ("use_cluster_relocation", "\"use_cluster_relocation\": true,", "\"use_cluster_relocation\": 1,"),
                ("use_cluster_relocation", "\"use_cluster_relocation\": true,", string.Empty),
                ("relocate_gain_ratio", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": 0.9,"),
                ("relocate_gain_ratio", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": 10.5,"),
                ("relocate_gain_ratio", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": \"big\","),
                ("relocate_gain_ratio", "\"relocate_gain_ratio\": 2,", string.Empty),
            };
            foreach ((string key, string oldText, string newText) in bad)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => Edited("aggressive_s5", oldText, newText));
                Assert.That(error.Message, Does.StartWith("ai/aggressive_s5.json").And.Contain(key), newText);
            }
            // The limits themselves are accepted.
            Assert.That(Edited("aggressive_s5", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": 1,").RelocateGainRatio, Is.EqualTo(Fix64.One));
            Assert.That(Edited("aggressive_s5", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": 10,").RelocateGainRatio, Is.EqualTo(Fix64.FromInt(10)));
        }

        /// <summary>HQ-1: home at 30 percent with 3 live slots, a safe cluster with 6 new live slots (fields 6 and 7, served by no depot), live depots at the cap (1 of 1): the relocation starts there.</summary>
        [Test]
        public void HQ1_ClusterWithTwiceTheSlotsIsChosenAtTheDepotCap()
        {
            Simulation sim = Scene(1800);
            AiMemory memory = new AiMemory();
            List<AiIntent> uproot = Decide(sim, DepotCap("aggressive_s5", 1), memory, AiIntentKind.UprootHq);
            Assert.That(uproot.Count, Is.EqualTo(1));
            Assert.That(uproot[0].Unit, Is.EqualTo(Hq(sim).Id));
            Assert.That(memory.RelocationActive, Is.True);
            Assert.That(memory.RelocationField == 6 || memory.RelocationField == 7, Is.True, "the unserved 6-slot cluster, not the 5-slot ones: " + memory.RelocationField);
            Assert.That(memory.ClusterLeftFields, Is.EqualTo(new[] { 1 }), "only the home field at or below 0.35 (field 1 at 200 of 3000; field 0 holds 1600) is latched");
        }

        /// <summary>HQ-2: home at 40 percent (above 0.35): no relocation, whatever the clusters hold.</summary>
        [Test]
        public void HQ2_HomeAtFortyPercentStays()
        {
            Simulation sim = Scene(2400);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory(), AiIntentKind.UprootHq), Is.Empty);
        }

        /// <summary>HQ-3: the best candidate has 5 slots, home 3, ratio 2 (5 below 6): none; at ratio 1.5 (5 at least 4.5) it starts.</summary>
        [Test]
        public void HQ3_GainBelowTheRatioStays()
        {
            Simulation sim = Scene(1800);
            // The 6-slot cluster is known dead (the AI only learns what it sees), so the best new cluster is a 5-slot one.
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), Knowing(sim, 6, 7), AiIntentKind.UprootHq), Is.Empty);
            AiProfile relaxed = Edited("aggressive_s5", "\"max_live_depots\": 4,", "\"max_live_depots\": 1,", "\"relocate_gain_ratio\": 2,", "\"relocate_gain_ratio\": 1.5,");
            Assert.That(Decide(sim, relaxed, Knowing(sim, 6, 7), AiIntentKind.UprootHq).Count, Is.EqualTo(1));
        }

        /// <summary>HQ-4: army stance Defend: no relocation (the same scene as HQ-1 starts one in another stance).</summary>
        [Test]
        public void HQ4_DefendStanceStays()
        {
            Simulation sim = Scene(1800);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory { Stance = AiStance.Defend, AttackThreshold = 10 }, AiIntentKind.UprootHq), Is.Empty);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory { Stance = AiStance.Massing, AttackThreshold = 10 }, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
        }

        /// <summary>
        /// HQ-5: switch off: today's choice on the same views. At 30 percent the old rule (0.1) does nothing; at 5 percent with two live depots
        /// (the cap) and a safe unserved field it heads for field 11, as the B-12 test of the old rule does.
        /// </summary>
        [Test]
        public void HQ5_SwitchOffKeepsTheOldRule()
        {
            AiProfile off = DepotCap("abl_s5_no_hq", 2);
            Assert.That(Decide(Scene(1800, true), off, new AiMemory(), AiIntentKind.UprootHq), Is.Empty);
            AiMemory memory = new AiMemory();
            Assert.That(Decide(Scene(300, true), off, memory, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
            Assert.That(memory.RelocationField, Is.EqualTo(11), "nearest safe field not served by a depot and not a home field");
            Assert.That(memory.ClusterLeftFields, Is.Empty, "no latch with the switch off");
        }

        /// <summary>
        /// HQ-6: the latch. The fields of a home that was left are never a target again: with the 6-slot cluster (fields 6 and 7) latched, the same
        /// scene as HQ-1 has only 5-slot clusters left (below 2 x 3) and the HQ stays; without the latch it moves. The latch is filled by the move (HQ-1).
        /// </summary>
        [Test]
        public void HQ6_LeftHomeIsNeverATargetAgain()
        {
            Simulation sim = Scene(1800);
            AiMemory latched = new AiMemory();
            latched.ClusterLeftFields.Add(6);
            latched.ClusterLeftFields.Add(7);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), latched, AiIntentKind.UprootHq), Is.Empty);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory(), AiIntentKind.UprootHq).Count, Is.EqualTo(1));
        }

        /// <summary>
        /// HQ-8: home fields just under the live threshold (2 x 290 left, expand_min_field_remaining 300): home has 0 live slots, so the cluster rule
        /// does not run ("anything beats 0" is not a win) and the old rule decides: home is under 0.35, two live depots are the cap, field 11 is the
        /// nearest unserved field. Nothing is latched.
        /// </summary>
        [Test]
        public void HQ8_ExhaustedHomeFallsThroughToTheOldRule()
        {
            Simulation sim = Scene(1800, true);
            Leave(sim, 0, 290);
            Leave(sim, 1, 290);
            AiMemory memory = new AiMemory();
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 2), memory, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
            Assert.That(memory.RelocationField, Is.EqualTo(11), "the old rule's nearest safe unserved field");
            Assert.That(memory.ClusterLeftFields, Is.Empty);
            // At 600 each home is live (6 slots) and the 6-slot cluster is no 2x gain: the cluster rule runs and stays.
            Simulation live = Scene(1800);
            Leave(live, 0, 600);
            Leave(live, 1, 600);
            Assert.That(Decide(live, DepotCap("aggressive_s5", 1), new AiMemory(), AiIntentKind.UprootHq), Is.Empty);
        }

        /// <summary>HQ-9: an unsafe cluster is skipped: with a seen enemy structure beside fields 6 and 7 the HQ stays (the other clusters have 5 slots); without it the HQ-1 move happens.</summary>
        [Test]
        public void HQ9_UnsafeClusterIsSkipped()
        {
            Simulation sim = Scene(1800);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory(), AiIntentKind.UprootHq).Count, Is.EqualTo(1), "safe: the move happens");
            // A worker of ours sees an enemy producer 5 tiles from the cluster.
            SpawnAtTile(sim, "skai.fledgling", P0, 22, 7);
            SpawnAtTile(sim, "water.tidepool", P1, 26, 7);
            StepTo(sim, sim.Clock.Tick + 40);
            Assert.That(Decide(sim, DepotCap("aggressive_s5", 1), new AiMemory(), AiIntentKind.UprootHq), Is.Empty, "unsafe: skipped");
        }

        /// <summary>
        /// HQ-10: the latch also holds in the fallback. An Uprooted HQ whose target is found empty, with no candidate left, may not root beside the
        /// latched home fields and heads for the nearest live field that is not latched (field 6, served by a depot); with no latch it roots at home.
        /// </summary>
        [Test]
        public void HQ10_FallbackRespectsTheLatch()
        {
            // Index into the field list. Range: 0 to 11.
            foreach (bool latch in new[] { true, false })
            {
                Simulation sim = Game();
                SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
                // Live fields known: home 0 and 1 and the depot-served field 6; the rest is known empty.
                List<int> empty = new List<int>();
                for (int field = 0; field < sim.Resources.Count; field++)
                {
                    if (field != 0 && field != 1 && field != 6)
                    {
                        empty.Add(field);
                    }
                }
                StepTo(sim, 200);
                sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, _sequence++, Hq(sim).Id));
                for (int i = 0; i < 400 && Hq(sim).HqState != HqState.Uprooted; i++)
                {
                    sim.Step();
                }
                AiMemory memory = Knowing(sim, empty.ToArray());
                memory.RelocationActive = true;
                memory.RelocationField = 11;
                memory.RelocationDestination = Hq(sim).Position;
                memory.RelocationStartTick = sim.Clock.Tick;
                if (latch)
                {
                    memory.ClusterLeftFields.Add(0);
                    memory.ClusterLeftFields.Add(1);
                }
                List<AiIntent> roots = Decide(sim, DepotCap("aggressive_s5", 2), memory, AiIntentKind.RootHq);
                if (latch)
                {
                    Assert.That(roots, Is.Empty, "the latched home fields do not let the HQ root where it stands");
                    Assert.That(memory.RelocationField, Is.EqualTo(6), "it heads for the nearest live field that is not latched");
                }
                else
                {
                    Assert.That(roots.Count, Is.EqualTo(1), "without the latch the HQ roots at the live home fields beside it");
                }
            }
        }

        /// <summary>HQ-11: a cluster served by an own complete depot is no target even when it is the biggest: with the depot at (21, 7) the 6-slot cluster of fields 6 and 7 brings no new income and the HQ stays (5 is below 2 x 3).</summary>
        [Test]
        public void HQ11_DepotServedClusterBringsNoNewIncome()
        {
            Assert.That(Decide(Scene(1800, true), DepotCap("aggressive_s5", 2), new AiMemory(), AiIntentKind.UprootHq), Is.Empty);
        }

        /// <summary>
        /// HQ-7: abl_s5_no_hq plays the aggressive_s5 of before AI-HQ bit for bit: the full-game state hashes of three prototype_1v1 games (seed
        /// 20261117, game seeds derived as the harness does, both seats the same profile) were recorded on aggressive_s5 at origin/main (AI-H
        /// merged) before any AI-HQ code.
        /// </summary>
        [Test]
        public void HQ7_AblationPlaysThePreAiHqGame()
        {
            // (matchup, game, expected final state hash) of the recorded games.
            (string Matchup, int Game, ulong Hash)[] recorded =
            {
                ("skai_vs_water", 2, 0xae4f362f8bdc9885UL),
                ("skai_vs_skai", 3, 0xf6ee85b4dc84a331UL),
                ("water_vs_water", 2, 0xb171e5cd2bf0bfebUL),
            };
            foreach ((string matchup, int game, ulong hash) in recorded)
            {
                ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", matchup, game);
                GameRecord record = GameRunner.Play(Data, "prototype_1v1", matchup, game, seed, "abl_s5_no_hq", "abl_s5_no_hq", 2400);
                Assert.That(record.StateHash, Is.EqualTo(hash), matchup + " game " + game);
            }
        }
    }
}
