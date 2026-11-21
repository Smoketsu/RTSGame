// rts/tests/Sim.Tests/Ai/AiPlacementTests.cs
// Tests for package AI-P of rts/docs/specs/field_saturation.md (producers at the base with the fewest producers, static defence by threat):
// the shipped key values and the parser (P-0), producer sites (P-1, P-2), SD-4 and its gates (P-3), the effective cap (P-4), the hysteresis
// (P-5: the trigger band and the exposed-base band) and the ablation abl_s5_no_p playing the pre-AI-P aggressive_s5 bit for bit (P-6, with the
// full-game hashes recorded on aggressive_s5 BEFORE any AI-P code). Part of Rts.Sim.Tests. Real simulations on prototype_1v1 (P0 HQ at
// (12.5, 12.5); main fields (7, 10) and (10, 7); natural fields (27, 9) and (26, 16); a rare field (30, 13)), AiBrain.ProbePlacement / Decide on the views.
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
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package AI-P: producer placement and static defence by threat.</summary>
    [TestFixture]
    public class AiPlacementTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ticks per second of the shipped match settings. Unit: ticks per second. Range: 20 (asserted in <see cref="Game"/>).</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A started Skai-versus-Water match on prototype_1v1: HQs rooted at the spawns, two workers each, one roost each, every site explored.</summary>
        private Simulation Game()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(TicksPerSecond));
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to 1.
            for (int i = 0; i < 2; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "water.tender", P1, s1 - offset);
            }
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

        /// <summary>The unit with the ID, or a failed assertion.</summary>
        private static Unit UnitOf(Simulation sim, EntityId id)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
            return unit!;
        }

        /// <summary>Sets a player's stock.</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>The shipped aggressive_s5 profile with text edits applied (each old text must occur once).</summary>
        private static AiProfile Edited(params string[] oldNewPairs)
        {
            return Parse(EditedText(oldNewPairs));
        }

        /// <summary>
        /// The AI-P switch-on edits (old/new pairs): aggressive_s5 was switched off on 19 Nov 2026 after the AI-P gate failed, so the tests that
        /// need AI-P on start from the shipped text with the values it had when AI-P was on (rts/docs/reports/ai_p/README.md, "New profile keys").
        /// </summary>
        private static readonly string[] AiPOn =
        {
            "\"use_forward_producers\": false,", "\"use_forward_producers\": true,",
            "\"use_threat_defence\": false,", "\"use_threat_defence\": true,",
            "\"threat_defence_min_enemy_supply\": 0,", "\"threat_defence_min_enemy_supply\": 8,",
            "\"threat_defence_min_enemy_advantage\": 0,", "\"threat_defence_min_enemy_advantage\": 1,",
            "\"threat_defence_max_value_share\": 0,", "\"threat_defence_max_value_share\": 0.4,",
            "\"threat_defence_release_fraction\": 1,", "\"threat_defence_release_fraction\": 0.75,",
            "\"static_defence_max_per_base\": 2,", "\"static_defence_max_per_base\": 6,",
        };

        /// <summary>The AI-P-on profile (aggressive_s5 text with the AI-P switch-on edits). Unit: profile.</summary>
        private static AiProfile OnProfile()
        {
            return Edited();
        }

        /// <summary>The text of aggressive_s5.json with the AI-P switch-on edits and then the given edits applied.</summary>
        private static string EditedText(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5.json"));
            // Index into the switch-on pairs. Range: even numbers from 0.
            for (int j = 0; j < AiPOn.Length; j += 2)
            {
                Assert.That(text.Contains(AiPOn[j]), Is.True, AiPOn[j]);
                text = text.Replace(AiPOn[j], AiPOn[j + 1]);
            }
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return text;
        }

        /// <summary>Parses profile text under the name aggressive_s5.</summary>
        private static AiProfile Parse(string text)
        {
            return AiProfile.Parse(text, "ai/aggressive_s5.json", Data.Content);
        }

        /// <summary>The placement probe of P0 on the current view.</summary>
        private static AiBrain.PlacementProbe Probe(Simulation sim, AiProfile profile, AiMemory? memory = null)
        {
            return AiBrain.ProbePlacement(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory())!;
        }

        /// <summary>Chebyshev distance between two tiles. Unit: tiles. Range: 0 upward.</summary>
        private static int Chebyshev(int ax, int ay, int bx, int by)
        {
            return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        }

        /// <summary>
        /// The producer scenario: P0 has its HQ, two more producers at the HQ (the roost of the match setup is the first of three), and a complete
        /// depot at the natural (tile 31, 10) more than base_radius_tiles from every producer. Returns the depot's ID.
        /// </summary>
        private EntityId HqWithProducersAndDepot(Simulation sim, bool depotProducers)
        {
            SpawnAtTile(sim, "skai.roost", P0, 17, 10);
            SpawnAtTile(sim, "skai.roost", P0, 8, 18);
            SpawnAtTile(sim, "skai.roost", P0, 12, 19);
            EntityId depot = SpawnAtTile(sim, "skai.perch_depot", P0, 31, 10);
            if (depotProducers)
            {
                SpawnAtTile(sim, "skai.roost", P0, 34, 13);
                SpawnAtTile(sim, "skai.roost", P0, 34, 7);
                SpawnAtTile(sim, "skai.roost", P0, 34, 4);
            }
            return depot;
        }

        /// <summary>The static defence Build intents one Decide of P0 makes on the current view (a fresh memory unless given).</summary>
        private static List<AiIntent> DefenceOrders(Simulation sim, AiProfile profile, AiMemory? memory = null)
        {
            // Intents of one think.
            List<AiIntent> intents = AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory(), new DeterministicRandom(1));
            // The static defence builds among them.
            List<AiIntent> builds = new List<AiIntent>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Build && Data.Content.Units[intent.DefinitionId].Economy.IsStaticDefence)
                {
                    builds.Add(intent);
                }
            }
            return builds;
        }

        /// <summary>The profile the SD-4 tests use: aggressive_s5 with the raid step out of the way (a raid needs 20 starts) and the given extra edits (old/new pairs).</summary>
        private static AiProfile Sd4Profile(params string[] more)
        {
            // The edit pairs: the raid count first, then the caller's.
            List<string> pairs = new List<string> { "\"static_defence_after_raids\": 1,", "\"static_defence_after_raids\": 20," };
            pairs.AddRange(more);
            return Edited(pairs.ToArray());
        }

        /// <summary>
        /// The threat scenario: the producer scenario's depot (31, 10) is complete, six windrunners (value 450) stand at the HQ (more than
        /// defend_radius_tiles from the depot), P0 has 2000 common, <paramref name="windrunnersAtDepot"/> more windrunners stand at the depot and
        /// <paramref name="graspers"/> Water graspers (2 supply each) stand beside it.
        /// </summary>
        private Simulation Threat(int graspers, int windrunnersAtDepot = 0)
        {
            Simulation sim = Game();
            HqWithProducersAndDepot(sim, false);
            // Windrunner index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                SpawnAtTile(sim, "skai.windrunner", P0, 8 + i, 15);
            }
            for (int i = 0; i < windrunnersAtDepot; i++)
            {
                SpawnAtTile(sim, "skai.windrunner", P0, 28 + i % 4, 8 + i / 4);
            }
            // Grasper index. Range: 0 to graspers - 1.
            for (int i = 0; i < graspers; i++)
            {
                SpawnAtTile(sim, "water.grasper", P1, 34 + i % 3, 9 + i / 3);
            }
            SetStock(sim, P0, 2000, 0);
            return sim;
        }

        /// <summary>P-0: every profile except the two diagnostic ones (aggressive_s5 included, switched off on 19 Nov 2026) carries the AI-P keys at the old values; the diagnostics and the switch-on edits carry the on values.</summary>
        [Test]
        public void P0_ShippedValues()
        {
            // The diagnostic profiles of the AI-P report keep their own (partly on) values.
            AiProfile p1 = AiProfile.FromDataSet(Data, "abl_s5_p1_only");
            Assert.That(p1.UseForwardProducers, Is.True);
            Assert.That(p1.UseThreatDefence, Is.False);
            AiProfile p2 = AiProfile.FromDataSet(Data, "abl_s5_p2_only");
            Assert.That(p2.UseForwardProducers, Is.False);
            Assert.That(p2.UseThreatDefence, Is.True);
            Assert.That(p2.StaticDefenceMaxPerBase, Is.EqualTo(6));
            // The AI-P-on edits parse to the values AI-P shipped with.
            AiProfile s5 = OnProfile();
            Assert.That(s5.UseForwardProducers, Is.True);
            Assert.That(s5.UseThreatDefence, Is.True);
            Assert.That(s5.ThreatDefenceMinEnemySupply, Is.EqualTo(8));
            Assert.That(s5.ThreatDefenceMinEnemyAdvantage, Is.EqualTo(Fix64.One));
            Assert.That(s5.ThreatDefenceMaxValueShare, Is.EqualTo(Fix64.Parse("0.4")));
            Assert.That(s5.ThreatDefenceReleaseFraction, Is.EqualTo(Fix64.Parse("0.75")));
            Assert.That(s5.StaticDefenceMaxPerBase, Is.EqualTo(6));
            foreach (string file in Directory.GetFiles(Path.Combine(TestData.FindDataDirectory(), "ai"), "*.json"))
            {
                // Profile ID of the file.
                string id = Path.GetFileNameWithoutExtension(file);
                if (id == "abl_s5_p1_only" || id == "abl_s5_p2_only")
                {
                    continue;
                }
                AiProfile old = AiProfile.FromDataSet(Data, id);
                Assert.That(old.UseForwardProducers || old.UseThreatDefence, Is.False, id);
                Assert.That(old.ThreatDefenceMinEnemySupply, Is.EqualTo(0), id);
                Assert.That(old.ThreatDefenceMinEnemyAdvantage, Is.EqualTo(Fix64.Zero), id);
                Assert.That(old.ThreatDefenceMaxValueShare, Is.EqualTo(Fix64.Zero), id);
                Assert.That(old.ThreatDefenceReleaseFraction, Is.EqualTo(Fix64.One), id);
            }
            // aggressive_s5 is switched off (19 Nov 2026) and so, like the ablation, has static_defence_max_per_base 2.
            Assert.That(AiProfile.FromDataSet(Data, "abl_s5_no_p").StaticDefenceMaxPerBase, Is.EqualTo(2));
            Assert.That(AiProfile.FromDataSet(Data, "aggressive_s5").StaticDefenceMaxPerBase, Is.EqualTo(2));
        }

        /// <summary>P-0: the parser rejects out-of-range, wrongly typed and missing values of the six keys, and use_threat_defence without use_static_defence, naming the file and the key.</summary>
        [Test]
        public void P0_ParserRejectsBadValues()
        {
            // Pairs of (old text, new text, key named in the message).
            (string Old, string New, string Key)[] bad =
            {
                ("\"threat_defence_min_enemy_supply\": 8,", "\"threat_defence_min_enemy_supply\": 201,", "threat_defence_min_enemy_supply"),
                ("\"threat_defence_min_enemy_supply\": 8,", "\"threat_defence_min_enemy_supply\": -1,", "threat_defence_min_enemy_supply"),
                ("\"threat_defence_min_enemy_advantage\": 1,", "\"threat_defence_min_enemy_advantage\": 100.5,", "threat_defence_min_enemy_advantage"),
                ("\"threat_defence_min_enemy_advantage\": 1,", "\"threat_defence_min_enemy_advantage\": -0.5,", "threat_defence_min_enemy_advantage"),
                ("\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": 2.5,", "threat_defence_max_value_share"),
                ("\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": -0.1,", "threat_defence_max_value_share"),
                ("\"threat_defence_release_fraction\": 0.75,", "\"threat_defence_release_fraction\": 1.5,", "threat_defence_release_fraction"),
                ("\"threat_defence_release_fraction\": 0.75,", "\"threat_defence_release_fraction\": -0.5,", "threat_defence_release_fraction"),
                ("\"use_forward_producers\": true,", "\"use_forward_producers\": 1,", "use_forward_producers"),
                ("\"use_threat_defence\": true,", "\"use_threat_defence\": \"yes\",", "use_threat_defence"),
                ("\"use_forward_producers\": true,", string.Empty, "use_forward_producers"),
                ("\"use_threat_defence\": true,", string.Empty, "use_threat_defence"),
                ("\"threat_defence_min_enemy_supply\": 8,", string.Empty, "threat_defence_min_enemy_supply"),
                ("\"threat_defence_min_enemy_advantage\": 1,", string.Empty, "threat_defence_min_enemy_advantage"),
                ("\"threat_defence_max_value_share\": 0.4,", string.Empty, "threat_defence_max_value_share"),
                ("\"threat_defence_release_fraction\": 0.75,", string.Empty, "threat_defence_release_fraction"),
                ("\"use_static_defence\": true,", "\"use_static_defence\": false,", "use_threat_defence"),
            };
            foreach ((string oldText, string newText, string key) in bad)
            {
                // The edited text under test.
                string edited = EditedText(oldText, newText);
                DataValidationException error = Expect.Throws<DataValidationException>(() => Parse(edited));
                Assert.That(error.Message, Does.StartWith("ai/aggressive_s5.json").And.Contain(key), newText);
            }
            // The limits themselves are accepted.
            AiProfile limits = Edited("\"threat_defence_min_enemy_supply\": 8,", "\"threat_defence_min_enemy_supply\": 200,", "\"threat_defence_min_enemy_advantage\": 1,", "\"threat_defence_min_enemy_advantage\": 100,",
                "\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": 2,", "\"threat_defence_release_fraction\": 0.75,", "\"threat_defence_release_fraction\": 0,");
            Assert.That(limits.ThreatDefenceMaxValueShare, Is.EqualTo(Fix64.FromInt(2)));
        }

        /// <summary>
        /// P-1: HQ with 3 producers, one complete depot with live fields and 0 producers: the producer site lies on the depot's rings (Chebyshev
        /// build_ring_min_tiles to build_ring_max_tiles from the depot tile) on the depot's height. Equal counts: the HQ ring. A depot with no good
        /// site: the HQ ring. A depot with no live field in reach is not a base.
        /// </summary>
        [Test]
        public void P1_ProducerGoesToTheBaseWithFewest()
        {
            AiProfile profile = OnProfile();
            Simulation sim = Game();
            EntityId depot = HqWithProducersAndDepot(sim, false);
            // The depot's tile. Unit: tiles.
            int dx = UnitOf(sim, depot).Position.X.FloorToInt();
            int dy = UnitOf(sim, depot).Position.Y.FloorToInt();
            AiWorldView view = AiWorldView.Build(sim, P0);
            AiBrain.PlacementProbe probe = Probe(sim, profile);
            Assert.That(probe.ProducerSite, Is.Not.Null);
            (int X, int Y) site = probe.ProducerSite!.Value;
            // Ring distance of the site from the depot tile. Unit: tiles.
            int ring = Chebyshev(site.X, site.Y, dx, dy);
            Assert.That(ring, Is.InRange(profile.BuildRingMinTiles, profile.BuildRingMaxTiles), "site " + site);
            Assert.That(view.HeightAt(site.X, site.Y), Is.EqualTo(view.HeightAt(dx, dy)), "the depot's height");
            Assert.That(probe.ProducerSite, Is.Not.EqualTo(probe.HqRingSite), "not the HQ ring");

            // Equal counts (3 and 3): the HQ ring.
            Simulation equal = Game();
            HqWithProducersAndDepot(equal, true);
            AiBrain.PlacementProbe tie = Probe(equal, profile);
            Assert.That(tie.ProducerSite, Is.EqualTo(tie.HqRingSite), "equal counts: the HQ ring");

            // The depot has no good site (every tile within the largest ring marked unreachable): the HQ ring.
            AiMemory blocked = new AiMemory();
            for (int y = dy - profile.BuildRingMaxTiles; y <= dy + profile.BuildRingMaxTiles; y++)
            {
                for (int x = dx - profile.BuildRingMaxTiles; x <= dx + profile.BuildRingMaxTiles; x++)
                {
                    if (view.Contains(x, y))
                    {
                        blocked.UnreachableSites.Add(view.IndexOf(x, y));
                    }
                }
            }
            AiBrain.PlacementProbe none = Probe(sim, profile, blocked);
            Assert.That(none.ProducerSite, Is.Not.Null);
            Assert.That(none.ProducerSite, Is.EqualTo(none.HqRingSite), "no good site at the depot: the HQ ring");

            // A depot whose fields are all empty is not a base: the HQ ring.
            Simulation dry = Game();
            HqWithProducersAndDepot(dry, false);
            foreach (int field in new[] { 2, 3, 16 })
            {
                dry.Resources[field].Take(1000000);
            }
            dry.Step();
            AiMemory known = new AiMemory();
            Probe(dry, profile, known);
            AiBrain.PlacementProbe empty = Probe(dry, profile, known);
            Assert.That(empty.ProducerSite, Is.EqualTo(empty.HqRingSite), "no live field in the depot's reach: not a base");
        }

        /// <summary>P-1 end to end: rule X (every producer busy, bank above extra_producer_bank_common) places the new producer at the depot with the switch on and on the HQ ring with it off.</summary>
        [Test]
        public void P1_RuleXBuildsAtTheDepot()
        {
            Simulation sim = Game();
            HqWithProducersAndDepot(sim, false);
            SetStock(sim, P0, 3000, 3000);
            // Every producer is busy: one windrunner queued at each roost.
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Id == "skai.roost")
                {
                    sim.Submit(new TrainUnitCommand(sim.Clock.Tick, P0, _sequence++, unit.Id, "skai.windrunner"));
                }
            }
            sim.Step();
            SetStock(sim, P0, 3000, 3000);
            // Site of the producer build one think under the given profile decides, or null.
            (int X, int Y)? Site(AiProfile profile)
            {
                foreach (AiIntent intent in AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1)))
                {
                    if (intent.Kind == AiIntentKind.Build && Data.Content.Units[intent.DefinitionId].Economy.Trains.Count > 0)
                    {
                        return (intent.TileX, intent.TileY);
                    }
                }
                return null;
            }
            (int X, int Y)? on = Site(OnProfile());
            (int X, int Y)? off = Site(AiProfile.FromDataSet(Data, "abl_s5_no_p"));
            Assert.That(on, Is.Not.Null, "rule X fires with the switch on");
            Assert.That(off, Is.Not.Null, "rule X fires with the switch off");
            Assert.That(Chebyshev(on!.Value.X, on.Value.Y, 31, 10), Is.LessThanOrEqualTo(10), "on the depot's rings: " + on);
            Assert.That(Chebyshev(off!.Value.X, off.Value.Y, 12, 12), Is.LessThanOrEqualTo(10), "on the HQ's rings: " + off);
        }

        /// <summary>P-2: switch off: the producer site equals the HQ-ring search on the same view (today's TryFindSite), also in the scenario where P-1 moves it.</summary>
        [Test]
        public void P2_SwitchOffKeepsTheHqRing()
        {
            Simulation sim = Game();
            HqWithProducersAndDepot(sim, false);
            AiBrain.PlacementProbe off = Probe(sim, AiProfile.FromDataSet(Data, "abl_s5_no_p"));
            Assert.That(off.ProducerSite, Is.Not.Null);
            Assert.That(off.ProducerSite, Is.EqualTo(off.HqRingSite));
            AiBrain.PlacementProbe on = Probe(sim, OnProfile());
            Assert.That(on.HqRingSite, Is.EqualTo(off.HqRingSite), "the ring search itself is unchanged by the key");
            Assert.That(on.ProducerSite, Is.Not.EqualTo(off.ProducerSite));
        }

        /// <summary>P-3: SD-4. 10 supply of modelled enemy beside the depot with nothing to stop it: one defence on the depot's defence ring (2 to 4 tiles); 6 supply: none; 8 supply (the threshold): one.</summary>
        [Test]
        public void P3_ThreatDefenceAtTheExposedBase()
        {
            AiProfile profile = Sd4Profile();
            Simulation sim = Threat(5);
            AiBrain.PlacementProbe probe = Probe(sim, profile);
            Assert.That(probe.ExposedBase, Is.Not.EqualTo(EntityId.None));
            Assert.That(UnitOf(sim, probe.ExposedBase).Definition.Id, Is.EqualTo("skai.perch_depot"), "the depot is the exposed base");
            Assert.That(probe.EnemySupply, Is.EqualTo(Fix64.FromInt(10)));
            List<AiIntent> orders = DefenceOrders(sim, profile);
            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(Chebyshev(orders[0].TileX, orders[0].TileY, 31, 10), Is.InRange(profile.StaticDefenceRingMinTiles, profile.StaticDefenceRingMaxTiles));
            Assert.That(DefenceOrders(Threat(3), profile), Is.Empty, "6 supply: below threat_defence_min_enemy_supply");
            Assert.That(DefenceOrders(Threat(4), profile).Count, Is.EqualTo(1), "8 supply: at the threshold");
            Assert.That(DefenceOrders(Threat(5), AiProfile.FromDataSet(Data, "abl_s5_no_p")), Is.Empty, "switch off: nothing (the raid step is not reached either)");
        }

        /// <summary>P-3: the advantage gate: an own army beside the depot that beats the 10 supply gives an enemy advantage below 1 and no defence; the same army with the threshold at 0 builds.</summary>
        [Test]
        public void P3_AdvantageGate()
        {
            AiProfile profile = Sd4Profile();
            Simulation sim = Threat(5, 12);
            AiBrain.PlacementProbe probe = Probe(sim, profile);
            Assert.That(probe.Advantage, Is.LessThan(Fix64.One), "the own army favours us: " + probe.Advantage);
            Assert.That(DefenceOrders(sim, profile), Is.Empty);
            Assert.That(DefenceOrders(sim, Sd4Profile("\"threat_defence_min_enemy_advantage\": 1,", "\"threat_defence_min_enemy_advantage\": 0,")).Count, Is.EqualTo(1),
                "advantage 0 needed: the gate is the only thing that stopped it");
            // No own units near the depot: the advantage is the predictor's cap, far above 1.
            Assert.That(Probe(Threat(5), profile).Advantage, Is.GreaterThan(Fix64.One));
        }

        /// <summary>P-3 literal numbers: the trigger rule on the spec's own examples (10 supply with advantage 1.5 builds; 7 supply, or advantage 0.9, does not).</summary>
        [Test]
        public void P3_TriggerRuleLiterals()
        {
            AiProfile profile = OnProfile();
            Assert.That(AiBrain.ThreatDefenceHolds(profile, false, Fix64.FromInt(10), Fix64.Parse("1.5")), Is.True);
            Assert.That(AiBrain.ThreatDefenceHolds(profile, false, Fix64.FromInt(7), Fix64.Parse("1.5")), Is.False);
            Assert.That(AiBrain.ThreatDefenceHolds(profile, false, Fix64.FromInt(10), Fix64.Parse("0.9")), Is.False);
            Assert.That(AiBrain.ThreatDefenceHolds(profile, false, Fix64.FromInt(8), Fix64.One), Is.True, "both at the threshold");
            Assert.That(AiBrain.ThreatDefenceHolds(profile, false, Fix64.Zero, Fix64.FromInt(100)), Is.False, "an empty model never triggers");
        }

        /// <summary>P-3: at the per-base cap none, one below it one; above the value share none, within it one.</summary>
        [Test]
        public void P3_CapAndValueShare()
        {
            // Edit pairs: the advantage threshold goes to 0 (a standing tower fights and would change the answer) and the share to 1 (it must not be the limit).
            string[] open =
            {
                "\"threat_defence_min_enemy_advantage\": 1,", "\"threat_defence_min_enemy_advantage\": 0,",
                "\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": 1,",
                "\"static_defence_max_per_base\": 6,", "\"static_defence_max_per_base\": 1,",
            };
            Simulation capped = Threat(5);
            SpawnAtTile(capped, "skai.perch_tower", P0, 29, 7);
            SetStock(capped, P0, 2000, 0);
            Assert.That(DefenceOrders(capped, Sd4Profile(open)), Is.Empty, "at the cap");
            open[5] = "\"static_defence_max_per_base\": 2,";
            Assert.That(DefenceOrders(capped, Sd4Profile(open)).Count, Is.EqualTo(1), "one below the cap");
            // A tower costs 100 and the army is worth 450: a share of 0.2 allows 90 (none), 0.25 allows 112.5 (one).
            Simulation sim = Threat(5);
            Assert.That(DefenceOrders(sim, Sd4Profile("\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": 0.2,")), Is.Empty, "above the value share");
            Assert.That(DefenceOrders(sim, Sd4Profile("\"threat_defence_max_value_share\": 0.4,", "\"threat_defence_max_value_share\": 0.25,")).Count, Is.EqualTo(1), "within the value share");
        }

        /// <summary>P-4: the effective cap is min(profile, Sim) with the switch on (6 and 6: 6; a Sim cap of 4: 4) and the profile's own value with it off; the view carries the shipped Sim value.</summary>
        [Test]
        public void P4_EffectiveCap()
        {
            Assert.That(AiBrain.EffectiveDefenceCap(6, 6, true), Is.EqualTo(6));
            Assert.That(AiBrain.EffectiveDefenceCap(6, 4, true), Is.EqualTo(4));
            Assert.That(AiBrain.EffectiveDefenceCap(2, 4, true), Is.EqualTo(2));
            Assert.That(AiBrain.EffectiveDefenceCap(6, 4, false), Is.EqualTo(6), "switch off: the profile value alone (old behaviour)");
            AiWorldView view = AiWorldView.Build(Game(), P0);
            Assert.That(view.StaticDefencesPerBaseMax, Is.EqualTo(6));
            Assert.That(view.StaticDefencesPerBaseMax, Is.EqualTo(Data.Tuning.Supply.StaticDefencesPerBaseMax));
            Assert.That(view.BaseRadiusTiles, Is.EqualTo(Data.Tuning.Economy.BaseRadiusTiles));
        }

        /// <summary>
        /// P-5 (trigger band): the enemy supply alternates between 8 and 7 around the threshold 8 for 20 thinks. With threat_defence_release_fraction
        /// 0.75 SD-4 arms once and stays armed; with 1 (no band) it flips on every think.
        /// </summary>
        [Test]
        public void P5_TriggerHasHysteresis()
        {
            AiProfile band = OnProfile();
            AiProfile none = Edited("\"threat_defence_release_fraction\": 0.75,", "\"threat_defence_release_fraction\": 1,");
            // Number of changes of the armed state over the thinks, for the profile. Unit: changes. Range: 0 to 20.
            int Changes(AiProfile profile)
            {
                // Armed state carried through the thinks, and the changes counted.
                bool armed = false;
                int changes = 0;
                // Think number. Range: 0 to 19.
                for (int think = 0; think < 20; think++)
                {
                    bool next = AiBrain.ThreatDefenceHolds(profile, armed, Fix64.FromInt(think % 2 == 0 ? 8 : 7), Fix64.FromInt(2));
                    changes += next != armed ? 1 : 0;
                    armed = next;
                }
                return changes;
            }
            Assert.That(Changes(band), Is.EqualTo(1), "armed once, never released");
            Assert.That(Changes(none), Is.EqualTo(20), "without the band the trigger flips every think");
            // The band is finite: well below the threshold the rule releases.
            Assert.That(AiBrain.ThreatDefenceHolds(band, true, Fix64.FromInt(5), Fix64.FromInt(2)), Is.False, "5 supply is below 0.75 x 8");
            Assert.That(AiBrain.ThreatDefenceHolds(band, true, Fix64.FromInt(10), Fix64.Parse("0.7")), Is.False, "advantage 0.7 is below 0.75 x 1");
            Assert.That(AiBrain.ThreatDefenceHolds(band, true, Fix64.FromInt(10), Fix64.Parse("0.8")), Is.True, "advantage 0.8 is inside the band");
        }

        /// <summary>
        /// P-5 (exposed-base band): the enemy point is 10.5 tiles from the HQ and 8.6 from the depot. The previous exposed base (the HQ) is kept with the
        /// band 0.75 and left with no band; an enemy 3.8 tiles from the depot moves it whatever the band.
        /// </summary>
        [Test]
        public void P5_ExposedBaseHasHysteresis()
        {
            Simulation sim = Game();
            EntityId depot = HqWithProducersAndDepot(sim, false);
            // The HQ's ID.
            EntityId hq = EntityId.None;
            foreach (Unit unit in sim.Units.All)
            {
                hq = unit.Owner == P0 && unit.Definition.Hq != null ? unit.Id : hq;
            }
            AiProfile band = OnProfile();
            AiProfile none = Edited("\"threat_defence_release_fraction\": 0.75,", "\"threat_defence_release_fraction\": 1,");
            // Exposed base with the previous one remembered as the HQ (or None) and the last seen enemy army at x. Unit: ID.
            EntityId Exposed(AiProfile profile, int x, EntityId previous)
            {
                AiMemory memory = new AiMemory
                {
                    ExposedBase = previous,
                    HasLastSeenEnemy = true,
                    LastSeenEnemyPosition = new FixVec2(Fix64.FromInt(x), Fix64.FromInt(12)),
                    LastSeenEnemyTick = sim.Clock.Tick,
                };
                return Probe(sim, profile, memory).ExposedBase;
            }
            Assert.That(Exposed(band, 23, hq), Is.EqualTo(hq), "8.6 against 10.5 tiles: inside the band, the HQ stays");
            Assert.That(Exposed(none, 23, hq), Is.EqualTo(depot), "no band: the nearer depot");
            Assert.That(Exposed(band, 28, hq), Is.EqualTo(depot), "3.8 tiles from the depot against 15.5: it moves");
            Assert.That(Exposed(band, 23, EntityId.None), Is.EqualTo(depot), "no previous base: the nearest");
        }

        /// <summary>
        /// P-6: the ablation abl_s5_no_p plays the aggressive_s5 of before AI-P bit for bit: the full-game state hashes of three prototype_1v1 games
        /// (seed 20261117, game seeds derived as the harness does) were recorded on aggressive_s5 at origin/main 97f4d97, before any AI-P code.
        /// </summary>
        [Test]
        public void P6_AblationPlaysThePreAiPGame()
        {
            // (matchup, game, expected final state hash) of the recorded games.
            // Re-pinned for research and unlocks (specs/research_and_unlocks.md R1.9): the AI never researches, so its mixes lose the Stormcaller, Ink Lobber, Thunderhead and Leviathan. Old values skai_vs_water 0xae4f362f8bdc9885, skai_vs_skai 0x235626745316a81d, water_vs_water 0xb171e5cd2bf0bfeb; end ticks were not recorded.
            (string Matchup, int Game, ulong Hash)[] recorded =
            {
                ("skai_vs_water", 2, 0x31dbd80f94a26eaeUL),
                ("skai_vs_skai", 3, 0x311eec87f47b4d4eUL),
                ("water_vs_water", 2, 0x4b44414af49e53e4UL),
            };
            foreach ((string matchup, int game, ulong hash) in recorded)
            {
                ulong seed = SeedDerivation.GameSeed(20261117UL, "prototype_1v1", matchup, game);
                GameRecord record = GameRunner.Play(Data, "prototype_1v1", matchup, game, seed, "abl_s5_no_p", "abl_s5_no_p", 2400);
                Assert.That(record.StateHash, Is.EqualTo(hash), matchup + " game " + game);
                // aggressive_s5 played the same games until AI-H was switched on (19 Nov 2026); abl_s5_no_h is the pre-AI-H file, tested in AiHarassTests.
            }
        }
    }
}
