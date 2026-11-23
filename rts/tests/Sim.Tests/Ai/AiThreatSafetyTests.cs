// rts/tests/Sim.Tests/Ai/AiThreatSafetyTests.cs
// Tests TS-1 to TS-10 of rts/docs/specs/ai_threat_map.md (package S-TS, rule TS: IsUnsafe reads the threat map): a lone scout, a group, the own army, remembered sightings, structures, the spawn and HQ
// clause, expansion, relocation, determinism, and the off path with the parsing rules. Part of Rts.Sim.Tests.
// Real simulations on prototype_1v1 (96 by 96 tiles, cells of 4 tiles; P0 HQ rooted at (12.5, 12.5); both players Skai so a P1 Windrunner is a unit of its own faction), AiBrain.ProbeSafety and AiBrain.Decide on
// P0's view. F is field 3 at (26, 16) (cell (6, 4), 14.3 tiles from the P0 HQ) where the test says "visible", and field 7 at (40, 22) (cell (10, 5), far from every P0 unit) where it says "out of vision".
// Values: Windrunner 75, Talon Archer 100, Coral Turret 100, Pearl Depot 100 (unarmed), HQ 0; 20 ticks per second, so M = 1200 ticks and the margin is 150.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// <summary>Package S-TS: rule TS, safety by the threat map.</summary>
    [TestFixture]
    public class AiThreatSafetyTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Skai too).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Profile file text edited by <see cref="Profile"/>.</summary>
        private static string CandidateText => File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_s.json"));

        /// <summary>
        /// A hand-made profile: a copy of aggressive_s5_s with use_threat_safety as given, use_counter_mix false (and use_tech_on_demand, use_rare_priority and use_predictor_mix, which need it) and the given text edits (each old text must occur once). The openings and jitter only matter to a
        /// controller's variety stream, which Decide called directly never has.
        /// </summary>
        /// <param name="safety">Value of use_threat_safety.</param>
        /// <param name="edits">Old and new text pairs.</param>
        private static AiProfile Profile(bool safety, params string[] edits)
        {
            // The file text being edited.
            string text = CandidateText.Replace("\"use_threat_safety\": true,", "\"use_threat_safety\": " + (safety ? "true" : "false") + ",").Replace("\"use_counter_mix\": true,", "\"use_counter_mix\": false,").Replace("\"use_tech_on_demand\": true,", "\"use_tech_on_demand\": false,")
                .Replace("\"use_rare_priority\": true,", "\"use_rare_priority\": false,").Replace("\"use_predictor_mix\": true,", "\"use_predictor_mix\": false,");
            // Index into the old and new pairs. Range: even numbers from 0.
            for (int i = 0; i < edits.Length; i += 2)
            {
                Assert.That(text.Contains(edits[i]), Is.True, edits[i]);
                text = text.Replace(edits[i], edits[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_s.json", Data.Content);
        }

        /// <summary>A started Skai-versus-Skai match on prototype_1v1: HQs at the spawns, two workers each and one Roost for P0, every tile explored for both teams.</summary>
        private Simulation Game()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "skai", "Skai"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(20));
            // Spawn points. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "skai.great_aerie", P1, s1);
            // Worker index. Range: 0 to 1.
            for (int i = 0; i < 2; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "skai.fledgling", P1, s1 - offset);
            }
            Spawn(sim, "skai.roost", P0, s0 + new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
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
            throw new AssertionException("spawn of " + definition + " failed at " + position);
        }

        /// <summary>Spawns a unit at whole-tile coordinates (the tile corner).</summary>
        private EntityId SpawnAt(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            return Spawn(sim, definition, owner, FixVec2.FromInts(x, y));
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Restores every unit to full health, so strengths are the data values (a few ticks of fighting may have hurt someone).</summary>
        private static void HealAll(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                unit.SetHealth(unit.Definition.MaxHealth);
            }
        }

        /// <summary>Sets a player's stock.</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>The centre of field <paramref name="index"/> as the AI sees it. Unit: tiles.</summary>
        private static FixVec2 FieldCentre(Simulation sim, int index)
        {
            return AiWorldView.Build(sim, P0).Fields[index].Centre;
        }

        /// <summary>The target order from the P0 HQ and the unsafe flag of the centre of field <paramref name="field"/>, after one think's intel and map update with <paramref name="profile"/> and <paramref name="memory"/>.</summary>
        private static (List<int> Order, bool Unsafe) Report(Simulation sim, AiProfile profile, AiMemory memory, int field)
        {
            AiWorldView view = AiWorldView.Build(sim, P0);
            (List<int> Order, List<bool> Unsafe) result = AiBrain.ProbeSafety(view, profile, profile.RequireFaction("skai"), memory, new[] { view.Fields[field].Centre })!.Value;
            return (result.Order, result.Unsafe[0]);
        }

        /// <summary>Puts a visible P0 observer beside F (field 3) and a P1 group of the given definitions in F's cell (6, 4), then heals everyone; no step follows, so the view is the one built.</summary>
        private void VisibleGroup(Simulation sim, params string[] enemies)
        {
            // Observer a few tiles west of F: it sees the cell and adds no strength (a worker).
            SpawnAt(sim, "skai.fledgling", P0, 22, 17);
            // Tile offsets of the group's units inside cell (6, 4) = x 24 to 27, y 16 to 19. Range: whole tiles.
            (int X, int Y)[] spots = { (25, 17), (25, 18), (26, 18), (24, 18), (26, 17), (24, 17) };
            // Unit index. Range: 0 to enemies.Length - 1.
            for (int i = 0; i < enemies.Length; i++)
            {
                SpawnAt(sim, enemies[i], P1, spots[i].X, spots[i].Y);
            }
            HealAll(sim);
        }

        /// <summary>The position of <paramref name="field"/> in <paramref name="order"/>, or -1 when it is not listed.</summary>
        private static int Rank(List<int> order, int field)
        {
            return order.IndexOf(field);
        }

        /// <summary>TS-1: one visible P1 Windrunner in F's cell. On: F is safe (75 against 0 + 150) and comes in the target order by distance among the safe fields. Off: F is unsafe (the old army clause) and is listed after them, or not at all.</summary>
        [Test]
        public void TS1_LoneScout()
        {
            Simulation sim = Game();
            VisibleGroup(sim, "skai.windrunner");
            Assert.That(AiWorldView.Build(sim, P0).VisibleEnemies.Count, Is.EqualTo(1));
            (List<int> Order, bool Unsafe) on = Report(sim, Profile(true), new AiMemory(), 3);
            Assert.That(on.Unsafe, Is.False);
            Assert.That(on.Order.GetRange(0, 3), Is.EqualTo(new List<int> { 0, 1, 3 }), "the two home fields, then F, the nearest of the rest");
            (List<int> Order, bool Unsafe) off = Report(sim, Profile(false), new AiMemory(), 3);
            Assert.That(off.Unsafe, Is.True);
            Assert.That(Rank(off.Order, 3) == -1 || Rank(off.Order, 3) > Rank(off.Order, 16), Is.True, "F is after the safe fields (or left out while not starved)");
        }

        /// <summary>TS-2: three Windrunners and one Talon Archer visible in F's cell (325): unsafe with the switch on and off.</summary>
        [Test]
        public void TS2_Group()
        {
            Simulation sim = Game();
            VisibleGroup(sim, "skai.windrunner", "skai.windrunner", "skai.windrunner", "skai.talon_archer");
            AiMemory memory = new AiMemory();
            Assert.That(Report(sim, Profile(true), memory, 3).Unsafe, Is.True);
            Assert.That(AiThreatMap.EnemyAt(memory, Profile(true), FieldCentre(sim, 3)), Is.EqualTo(325));
            Assert.That(Report(sim, Profile(false), new AiMemory(), 3).Unsafe, Is.True);
        }

        /// <summary>TS-3: TS-2 plus three own Talon Archers in F's cell: with the switch on it is safe (325 against 300 + 150); with it off it is unsafe.</summary>
        [Test]
        public void TS3_OwnArmyCounts()
        {
            Simulation sim = Game();
            VisibleGroup(sim, "skai.windrunner", "skai.windrunner", "skai.windrunner", "skai.talon_archer");
            SpawnAt(sim, "skai.talon_archer", P0, 24, 16);
            SpawnAt(sim, "skai.talon_archer", P0, 25, 16);
            SpawnAt(sim, "skai.talon_archer", P0, 26, 16);
            HealAll(sim);
            AiMemory memory = new AiMemory();
            Assert.That(Report(sim, Profile(true), memory, 3).Unsafe, Is.False);
            Assert.That(AiThreatMap.OwnAt(memory, Profile(true), FieldCentre(sim, 3)), Is.EqualTo(300));
            Assert.That(AiThreatMap.EnemyAt(memory, Profile(true), FieldCentre(sim, 3)), Is.EqualTo(325));
            Assert.That(Report(sim, Profile(false), new AiMemory(), 3).Unsafe, Is.True);
        }

        /// <summary>TS-4: memory (sightings written into memory, out of vision at field 7): three Windrunners and a Talon Archer seen 200 ticks ago are aged to 62 x 3 + 83 = 269 (unsafe); seen 800 ticks ago, 25 x 3 + 33 = 108 (safe).</summary>
        [Test]
        public void TS4_Memory()
        {
            Simulation sim = Game();
            StepTo(sim, 1000);
            FixVec2 centre = FieldCentre(sim, 7);
            Assert.That(AiWorldView.Build(sim, P0).IsVisible(centre.X.FloorToInt(), centre.Y.FloorToInt()), Is.False);
            // Sighting age in ticks, then the enemy value expected in F's cell and whether F is unsafe. Unit: ticks, resource units.
            foreach ((int age, int expected, bool isUnsafe) in new[] { (200, 269, true), (800, 108, false) })
            {
                AiMemory memory = new AiMemory();
                memory.ThreatSightings[new EntityId(900)] = (centre, 75, sim.Clock.Tick - age);
                memory.ThreatSightings[new EntityId(901)] = (centre, 75, sim.Clock.Tick - age);
                memory.ThreatSightings[new EntityId(902)] = (centre, 75, sim.Clock.Tick - age);
                memory.ThreatSightings[new EntityId(903)] = (centre, 100, sim.Clock.Tick - age);
                Assert.That(Report(sim, Profile(true), memory, 7).Unsafe, Is.EqualTo(isUnsafe), "age " + age);
                Assert.That(AiThreatMap.EnemyAt(memory, Profile(true), centre), Is.EqualTo(expected), "age " + age);
            }
        }

        /// <summary>TS-5: structures. A known P1 Pearl Depot within 14 tiles of F and nothing else: safe with the switch on, unsafe with it off. One known Coral Turret in F's cell: safe (100); two: unsafe (200).</summary>
        [Test]
        public void TS5_Structures()
        {
            Simulation sim = Game();
            FixVec2 centre = FieldCentre(sim, 7);
            // A depot 5 tiles east of F, last seen at the start. Unit: tiles.
            AiMemory depot = new AiMemory();
            depot.KnownEnemyStructures.Add(new EntityId(500), new KnownStructure { DefinitionId = "water.pearl_depot", Position = centre + FixVec2.FromInts(5, 0), LastSeenTick = 0 });
            Assert.That(Report(sim, Profile(true), depot, 7).Unsafe, Is.False);
            Assert.That(Report(sim, Profile(false), depot, 7).Unsafe, Is.True);
            AiMemory one = new AiMemory();
            one.KnownEnemyStructures.Add(new EntityId(501), new KnownStructure { DefinitionId = "water.coral_turret", Position = centre, LastSeenTick = 0 });
            Assert.That(Report(sim, Profile(true), one, 7).Unsafe, Is.False, "100 is within the margin");
            AiMemory two = new AiMemory();
            two.KnownEnemyStructures.Add(new EntityId(501), new KnownStructure { DefinitionId = "water.coral_turret", Position = centre, LastSeenTick = 0 });
            two.KnownEnemyStructures.Add(new EntityId(502), new KnownStructure { DefinitionId = "water.coral_turret", Position = centre, LastSeenTick = 0 });
            Assert.That(Report(sim, Profile(true), two, 7).Unsafe, Is.True, "200 is above the margin");
        }

        /// <summary>TS-6: a field within 14 tiles of an uncleared enemy spawn (field 8 at (88, 85), 5 tiles from the spawn), or of a known enemy HQ, is unsafe with the switch on and off; a field with neither is safe.</summary>
        [Test]
        public void TS6_SpawnAndHqClause()
        {
            Simulation sim = Game();
            foreach (bool safety in new[] { true, false })
            {
                Assert.That(Report(sim, Profile(safety), new AiMemory(), 8).Unsafe, Is.True, "spawn, switch " + safety);
                Assert.That(Report(sim, Profile(safety), new AiMemory(), 7).Unsafe, Is.False, "control, switch " + safety);
                AiMemory hq = new AiMemory();
                hq.KnownEnemyStructures.Add(new EntityId(502), new KnownStructure { DefinitionId = "skai.great_aerie", Position = FieldCentre(sim, 7) + FixVec2.FromInts(0, 8), IsHq = true, LastSeenTick = 0 });
                Assert.That(Report(sim, Profile(safety), hq, 7).Unsafe, Is.True, "HQ, switch " + safety);
            }
        }

        /// <summary>The first depot order of P0 on the current view with <paramref name="profile"/> and a fresh memory: its tile ((-1, -1) when none is ordered) and every intent plus the layers as text for the determinism test.</summary>
        private static (int TileX, int TileY, string Signature) DepotOrder(Simulation sim, AiProfile profile)
        {
            AiWorldView view = AiWorldView.Build(sim, P0);
            AiMemory memory = new AiMemory();
            List<AiIntent> intents = AiBrain.Decide(view, profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
            // The signature of every intent and the layers. Text.
            StringBuilder signature = new StringBuilder();
            // Tile of the first depot order, or (-1, -1). Unit: tiles.
            int tileX = -1;
            int tileY = -1;
            foreach (AiIntent intent in intents)
            {
                signature.Append(intent.Kind).Append(',').Append(intent.Unit.Value).Append(',').Append(intent.Field).Append(',').Append(intent.DefinitionId).Append(',').Append(intent.TileX).Append(',').Append(intent.TileY)
                    .Append(',').Append(intent.Point).Append(',').Append(intent.Target.Value).Append(';');
                if (intent.Kind == AiIntentKind.Build && intent.DefinitionId == "skai.perch_depot" && tileX < 0)
                {
                    tileX = intent.TileX;
                    tileY = intent.TileY;
                }
            }
            signature.Append(string.Join(",", memory.ThreatEnemy)).Append('|').Append(string.Join(",", memory.ThreatOwn));
            return (tileX, tileY, signature.ToString());
        }

        /// <summary>Squared distance from a depot tile to the centre of field <paramref name="field"/>. Unit: squared tiles.</summary>
        private static long SquaredFrom(Simulation sim, (int TileX, int TileY, string Signature) order, int field)
        {
            // Offsets from the field centre. Unit: tiles (Fix64).
            Fix64 dx = Fix64.FromInt(order.TileX) - FieldCentre(sim, field).X;
            Fix64 dy = Fix64.FromInt(order.TileY) - FieldCentre(sim, field).Y;
            return (dx * dx + dy * dy).FloorToInt();
        }

        /// <summary>A game at 400 s (the expansion clock is due) with P0 holding 500 common and TS-1's scout at the nearest unserved field (F, field 3), seen by an observer.</summary>
        private Simulation ExpansionGame()
        {
            Simulation sim = Game();
            StepTo(sim, 8000);
            SetStock(sim, P0, 500, 0);
            VisibleGroup(sim, "skai.windrunner");
            return sim;
        }

        /// <summary>TS-7: with TS-1's scout at the nearest unserved field, rule E (clock due, common 500) orders the depot beside that field with the switch on and beside a different field with it off.</summary>
        [Test]
        public void TS7_Expansion()
        {
            Simulation onGame = ExpansionGame();
            (int TileX, int TileY, string Signature) on = DepotOrder(onGame, Profile(true));
            Simulation offGame = ExpansionGame();
            (int TileX, int TileY, string Signature) off = DepotOrder(offGame, Profile(false));
            Assert.That(on.TileX, Is.GreaterThanOrEqualTo(0), "on: a depot is ordered");
            Assert.That(off.TileX, Is.GreaterThanOrEqualTo(0), "off: a depot is ordered");
            Assert.That(SquaredFrom(onGame, on, 3), Is.LessThanOrEqualTo(36), "on: the depot at " + on.TileX + "," + on.TileY + " stands within 6 tiles of F (field 3)");
            Assert.That(SquaredFrom(offGame, off, 3), Is.GreaterThan(100), "off: the depot at " + off.TileX + "," + off.TileY + " stands more than 10 tiles from F, which the scout made unsafe");
        }

        /// <summary>Runs one think of P0 with a relocation to field 3 under way (HQ Uprooted) and the given group beside its destination, and returns the field the relocation targets afterwards.</summary>
        private int RelocationTarget(bool safety, params string[] enemies)
        {
            Simulation sim = Game();
            sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, _sequence++, FindHq(sim)));
            // Steps spent waiting for the HQ to uproot. Range: 0 to 400.
            for (int i = 0; i < 400; i++)
            {
                sim.Step();
            }
            AiWorldView before = AiWorldView.Build(sim, P0);
            // Destination of the relocation to field 3: 3 tiles from its centre toward the HQ (as DestinationFor with the profile's stand-off). Unit: tiles.
            FixVec2 destination = before.Fields[3].Centre.MoveTowards(HqPosition(sim), Fix64.FromInt(3));
            // Group in the destination's cell, seen by an observer.
            SpawnAt(sim, "skai.fledgling", P0, destination.X.FloorToInt() - 3, destination.Y.FloorToInt());
            // Unit index. Range: 0 to enemies.Length - 1.
            for (int i = 0; i < enemies.Length; i++)
            {
                SpawnAt(sim, enemies[i], P1, destination.X.FloorToInt() + i % 2, destination.Y.FloorToInt() + i / 2);
            }
            HealAll(sim);
            AiProfile profile = Profile(safety);
            AiMemory memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 3;
            memory.RelocationDestination = destination;
            memory.RelocationStartTick = sim.Clock.Tick;
            AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
            return memory.RelocationField;
        }

        /// <summary>The ID of P0's HQ.</summary>
        private static EntityId FindHq(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == P0 && unit.Definition.Hq != null)
                {
                    return unit.Id;
                }
            }
            throw new AssertionException("no HQ");
        }

        /// <summary>The position of P0's HQ. Unit: tiles.</summary>
        private static FixVec2 HqPosition(Simulation sim)
        {
            Assert.That(sim.Units.TryGet(FindHq(sim), out Unit? hq), Is.True);
            return hq!.Position;
        }

        /// <summary>TS-8: a running relocation whose destination cell holds TS-2's group re-targets (switch on); with only TS-1's scout there it does not (switch on) while the old rule re-targets.</summary>
        [Test]
        public void TS8_Relocation()
        {
            Assert.That(RelocationTarget(true, "skai.windrunner", "skai.windrunner", "skai.windrunner", "skai.talon_archer"), Is.Not.EqualTo(3), "group: re-targets");
            Assert.That(RelocationTarget(true, "skai.windrunner"), Is.EqualTo(3), "scout, switch on: keeps the target");
            Assert.That(RelocationTarget(false, "skai.windrunner"), Is.Not.EqualTo(3), "scout, switch off: the old rule re-targets");
        }

        /// <summary>TS-9: TS-3 and TS-7 run twice give identical intents and memory.</summary>
        [Test]
        public void TS9_Determinism()
        {
            Assert.That(DepotOrder(ExpansionGame(), Profile(true)).Signature, Is.EqualTo(DepotOrder(ExpansionGame(), Profile(true)).Signature));
            // The TS-3 layers and verdict as text.
            Func<string> ownArmy = () =>
            {
                Simulation sim = Game();
                VisibleGroup(sim, "skai.windrunner", "skai.windrunner", "skai.windrunner", "skai.talon_archer");
                SpawnAt(sim, "skai.talon_archer", P0, 24, 16);
                SpawnAt(sim, "skai.talon_archer", P0, 25, 16);
                SpawnAt(sim, "skai.talon_archer", P0, 26, 16);
                HealAll(sim);
                AiMemory memory = new AiMemory();
                (List<int> Order, bool Unsafe) report = Report(sim, Profile(true), memory, 3);
                return string.Join(",", report.Order) + report.Unsafe + string.Join(",", memory.ThreatEnemy) + "|" + string.Join(",", memory.ThreatOwn) + "|" + memory.ThreatSightings.Count;
            };
            Assert.That(ownArmy(), Is.EqualTo(ownArmy()));
        }

        /// <summary>
        /// TS-10: off path and validation. A copy of aggressive_s5 with threat_unsafe_margin 0 gives the same record line as aggressive_s5 (the key is unread while the switch is off; the pins of the spec are held by their
        /// own tests, which are not re-pinned); use_threat_safety true with use_threat_map false fails to load naming use_threat_safety; the margin is range-checked; only aggressive_s5_s has the switch on and every profile carries the margin.
        /// </summary>
        [Test]
        public void TS10_OffPathAndValidation()
        {
            // aggressive_s5 against the same with margin 0 over a full game (the record line carries the end tick, result and final state hash).
            AiProfile plain = AiProfile.FromDataSet(Data, "aggressive_s5");
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5.json"));
            Assert.That(text.Contains("\"threat_unsafe_margin\": 150,"), Is.True);
            AiProfile zero = AiProfile.Parse(text.Replace("\"threat_unsafe_margin\": 150,", "\"threat_unsafe_margin\": 0,"), "ai/aggressive_s5.json", Data.Content);
            Assert.That(zero.ThreatUnsafeMargin, Is.EqualTo(0));
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord baseline = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, plain, plain, "aggressive_s5", "aggressive_s5", 600);
            GameRecord changed = GameRunner.Play(Data, "prototype_1v1", "skai_vs_water", 0, seed, zero, zero, "aggressive_s5", "aggressive_s5", 600);
            Assert.That(changed.ToJsonLine(), Is.EqualTo(baseline.ToJsonLine()));
            // Validation, each failure naming the file and the key.
            string file = "ai/aggressive_s5_s.json";
            // Old text, new text and the key the error must name.
            (string Old, string New, string Key)[] bad =
            {
                ("\"use_threat_map\": true,", "\"use_threat_map\": false,", "use_threat_safety"),
                ("\"threat_unsafe_margin\": 150,", "\"threat_unsafe_margin\": -1,", "threat_unsafe_margin"),
                ("\"threat_unsafe_margin\": 150,", "\"threat_unsafe_margin\": 100001,", "threat_unsafe_margin"),
                ("\"use_threat_safety\": true,", "\"use_threat_safety\": 1,", "use_threat_safety"),
            };
            foreach ((string oldText, string newText, string key) in bad)
            {
                DataValidationException error = Expect.Throws<DataValidationException>(() => AiProfile.Parse(CandidateText.Replace(oldText, newText), file, Data.Content));
                Assert.That(error.Message, Does.Contain(file).And.Contain(key), newText);
            }
            Assert.That(AiProfile.Parse(CandidateText.Replace("\"threat_unsafe_margin\": 150,", "\"threat_unsafe_margin\": 100000,"), file, Data.Content).ThreatUnsafeMargin, Is.EqualTo(100000));
            // A missing key fails naming it.
            foreach (string key in new[] { "use_threat_safety", "threat_unsafe_margin" })
            {
                string without = System.Text.RegularExpressions.Regex.Replace(CandidateText, "\\n  \"" + key + "\":[^\\n]*", string.Empty);
                DataValidationException missing = Expect.Throws<DataValidationException>(() => AiProfile.Parse(without, file, Data.Content));
                Assert.That(missing.Message, Does.Contain(file).And.Contain(key));
            }
            // Every shipped profile carries both keys; only aggressive_s5_s has the switch on.
            foreach (string path in Directory.GetFiles(Path.Combine(TestData.FindDataDirectory(), "ai"), "*.json"))
            {
                // Profile ID from the file name.
                string id = Path.GetFileNameWithoutExtension(path);
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseThreatSafety, Is.EqualTo(id == "aggressive_s5_s"), id);
                Assert.That(profile.ThreatUnsafeMargin, Is.EqualTo(150), id);
            }
        }
    }
}
