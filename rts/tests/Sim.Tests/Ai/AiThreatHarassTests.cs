// rts/tests/Sim.Tests/Ai/AiThreatHarassTests.cs
// Tests TH-1 to TH-9 of rts/docs/specs/ai_threat_map.md (package S-TH, rule TH: the harass squad's target and route by the threat map): the weakest target, the route, none eligible, ties, the kept
// defence check, no army known, validation, determinism and the off path. Part of Rts.Sim.Tests.
// Real greybox_1v1 simulations (64 by 64 tiles, cells of 4 tiles, 16 by 16 cells; spawns (8, 8) and (56, 56)), P0 Skai with its HQ at (7, 7), P1 Water; AiBrain.Decide on P0's view. The P0 squad is two
// Windrunners (strength 2 x 75 = 150) at (10, 10) and (11, 10), picked because the test profiles set harass_squad_supply 2, beside four Talon Archers that bring the army supply to harass_min_army_supply (10). Known P1 structures and sightings are written into the AiMemory
// directly (the structures stand outside P0's vision, so the update does not forget them): depot A at (12.5, 50.5) in the south, depot B at (50.5, 14.5) in the east; the old rule's army point is written too.
// Windrunner 75, Talon Archer 100, Coral Turret 100, Pearl Depot 100 (unarmed), HQ 0; 20 ticks per second, so M = 1200 ticks.
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package S-TH: rule TH, the harass target and route by the threat map.</summary>
    [TestFixture]
    public class AiThreatHarassTests
    {
        /// <summary>Player slot 0 (the AI under test, Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the enemy, Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Depot A, in the south (tile centre). Unit: tiles.</summary>
        private static readonly FixVec2 DepotA = new FixVec2(Fix64.Parse("12.5"), Fix64.Parse("50.5"));

        /// <summary>Depot B, in the east (tile centre). Unit: tiles.</summary>
        private static readonly FixVec2 DepotB = new FixVec2(Fix64.Parse("50.5"), Fix64.Parse("14.5"));

        /// <summary>The text of the candidate profile file with rule TH on as the final S-TH state of the file has it.</summary>
        private static string CandidateText => File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_s.json"));

        /// <summary>
        /// A hand-made profile: a copy of aggressive_s5_s with use_threat_harass and use_threat_safety as given, use_counter_mix false (and use_tech_on_demand, use_rare_priority and use_predictor_mix, which need it)
        /// scout_first_seconds 600 (no Windrunner becomes the scout within the 65 s the tests run) and harass_squad_supply 2 (a Windrunner has supply 1, so the shipped 4 would pick four of them and the spec's squad of two, S = 150, needs 2) and the given text edits (each old text must occur once). The openings and jitter only matter to a controller's variety stream, which Decide called directly never has.
        /// </summary>
        /// <param name="harass">Value of use_threat_harass.</param>
        /// <param name="edits">Old and new text pairs.</param>
        private static AiProfile Profile(bool harass, params string[] edits)
        {
            // The file text being edited.
            string text = Flag(Flag(CandidateText, "use_threat_harass", harass), "use_threat_safety", false)
                .Replace("\"use_counter_mix\": true,", "\"use_counter_mix\": false,").Replace("\"use_tech_on_demand\": true,", "\"use_tech_on_demand\": false,")
                .Replace("\"use_rare_priority\": true,", "\"use_rare_priority\": false,").Replace("\"use_predictor_mix\": true,", "\"use_predictor_mix\": false,")
                .Replace("\"harass_squad_supply\": 4,", "\"harass_squad_supply\": 2,").Replace("\"scout_first_seconds\": 40,", "\"scout_first_seconds\": 600,");
            // Index into the old and new pairs. Range: even numbers from 0.
            for (int i = 0; i < edits.Length; i += 2)
            {
                Assert.That(text.Contains(edits[i]), Is.True, edits[i]);
                text = text.Replace(edits[i], edits[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_s.json", Data.Content);
        }

        /// <summary>The text with the boolean key set to <paramref name="value"/>, whichever it was.</summary>
        private static string Flag(string text, string key, bool value)
        {
            return text.Replace("\"" + key + "\": true,", "\"" + key + "\": " + (value ? "true" : "false") + ",").Replace("\"" + key + "\": false,", "\"" + key + "\": " + (value ? "true" : "false") + ",");
        }

        /// <summary>A started greybox_1v1 match, P0 Skai with an HQ at (7, 7), a squad of two Windrunners and four Talon Archers (army supply 12), P1 Water; returns the Windrunners' IDs.</summary>
        private Simulation Game(out List<EntityId> squad)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            squad = new List<EntityId> { Spawn(sim, "skai.windrunner", P0, "10", "10"), Spawn(sim, "skai.windrunner", P0, "11", "10") };
            // Talon Archer number. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                Spawn(sim, "skai.talon_archer", P0, (10 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), "14");
            }
            return sim;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
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

        /// <summary>Puts a known enemy structure in the memory (last seen now).</summary>
        private static void Know(AiMemory memory, int id, string definition, FixVec2 position, int tick)
        {
            memory.KnownEnemyStructures[new EntityId((uint)id)] = new KnownStructure
            {
                DefinitionId = definition,
                Position = position,
                IsHq = definition == "water.grand_shell",
                LastSeenTick = tick,
            };
        }

        /// <summary>Writes <paramref name="count"/> remembered Windrunners (75 each, seen now) at <paramref name="position"/> into the sightings, with IDs from <paramref name="firstId"/>.</summary>
        private static void Sight(AiMemory memory, int firstId, int count, FixVec2 position, int tick)
        {
            // Sighting number. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                memory.ThreatSightings[new EntityId((uint)(firstId + i))] = (position, 75, tick);
            }
        }

        /// <summary>Sets the old rule's single army point (where the enemy army was last seen, now) in the memory.</summary>
        private static void ArmyAt(AiMemory memory, string x, string y, int tick)
        {
            memory.HasLastSeenEnemy = true;
            memory.LastSeenEnemyPosition = new FixVec2(Fix64.Parse(x), Fix64.Parse(y));
            memory.LastSeenEnemyTick = tick;
        }

        /// <summary>Decides for P0 with the profile and memory (an AttackThreshold of 200 supply is set so the first think does not draw and the army stays Massing: the squad's units get no stance orders beside the harass rule).</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            if (memory.AttackThreshold < 0)
            {
                memory.AttackThreshold = 200;
            }
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction("skai"), memory, new DeterministicRandom(1));
        }

        /// <summary>
        /// The goal of the squad's raid orders (all must agree), or null when none of the squad got one. An attack-move to a point within 8 tiles of the HQ (7, 7) is the Massing rally order that
        /// the stance rules give an idle army whether or not a raid starts, so it is not a raid order.
        /// </summary>
        private static FixVec2? SquadGoal(List<AiIntent> intents, List<EntityId> squad)
        {
            // The goal found. Unit: tiles.
            FixVec2? goal = null;
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.AttackMove && squad.Contains(intent.Unit) && (intent.Point - new FixVec2(Fix64.FromInt(7), Fix64.FromInt(7))).LengthSquared() > Fix64.FromInt(64))
                {
                    Assert.That(goal == null || goal.Value == intent.Point, Is.True, "every member goes to one goal");
                    goal = intent.Point;
                }
            }
            return goal;
        }

        /// <summary>True when <paramref name="goal"/> is within 2 tiles of <paramref name="depot"/> (Standable may move the goal a little).</summary>
        private static bool At(FixVec2? goal, FixVec2 depot)
        {
            return goal != null && (goal.Value - depot).LengthSquared() <= Fix64.FromInt(4);
        }

        /// <summary>The intents, the harass memory and both layers as text, for the determinism comparisons.</summary>
        private static string Signature(List<AiIntent> intents, AiMemory memory)
        {
            StringBuilder text = new StringBuilder();
            foreach (AiIntent intent in intents)
            {
                text.Append(intent.Kind).Append(',').Append(intent.Unit.Value).Append(',').Append(intent.Field).Append(',').Append(intent.DefinitionId).Append(',').Append(intent.TileX).Append(',').Append(intent.TileY)
                    .Append(',').Append(intent.Point).Append(',').Append(intent.Target.Value).Append(';');
            }
            text.Append(memory.HarassTarget).Append(',').Append(memory.HarassStartTick).Append(',').Append(memory.NextHarassTick).Append(',').Append(string.Join(",", memory.HarassGroup)).Append('|')
                .Append(string.Join(",", memory.ThreatEnemy)).Append('|').Append(string.Join(",", memory.ThreatOwn));
            return text.ToString();
        }

        /// <summary>The TH-1 situation: depot A with one remembered Windrunner beside it (the old rule's army point, 75 on the map there), depot B with four (300).</summary>
        private AiMemory WeakestTargetMemory(Simulation sim)
        {
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.pearl_depot", DepotA, sim.Clock.Tick);
            Know(memory, 901, "water.pearl_depot", DepotB, sim.Clock.Tick);
            Sight(memory, 800, 1, new FixVec2(Fix64.Parse("13.5"), Fix64.Parse("46.5")), sim.Clock.Tick);
            Sight(memory, 810, 4, DepotB, sim.Clock.Tick);
            ArmyAt(memory, "13.5", "46.5", sim.Clock.Tick);
            return memory;
        }

        /// <summary>The TH-2 situation: depot C (south, at A's place) has Enemy 0 but its line passes a cell with three remembered Windrunners (225); depot D (east, at B's place) has a clear line. D is left out when <paramref name="withD"/> is false.</summary>
        private AiMemory RouteMemory(Simulation sim, bool withD)
        {
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.pearl_depot", DepotA, sim.Clock.Tick);
            if (withD)
            {
                Know(memory, 901, "water.pearl_depot", DepotB, sim.Clock.Tick);
            }
            // Cell (2, 7) = x 8 to 11.99, y 28 to 31.99 lies on the line from the squad centre (10.5, 10) to C.
            Sight(memory, 800, 3, new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("30.5")), sim.Clock.Tick);
            return memory;
        }

        /// <summary>TH-1: depot B holds four remembered Windrunners (300), depot A one (75) near the old rule's army point, B is farther from that point. On: the squad goes to A. Off: to B.</summary>
        [Test]
        public void TH1_WeakestTarget()
        {
            Simulation sim = Game(out List<EntityId> squad);
            AiMemory on = WeakestTargetMemory(sim);
            List<AiIntent> intents = Decide(sim, Profile(true), on);
            Assert.That(AiThreatMap.EnemyAt(on, Profile(true), DepotB), Is.EqualTo(300));
            Assert.That(new List<EntityId>(on.HarassGroup), Is.EqualTo(squad));
            Assert.That(At(SquadGoal(intents, squad), DepotA), Is.True, "on: the weaker target A");
            AiMemory off = WeakestTargetMemory(sim);
            Assert.That(At(SquadGoal(Decide(sim, Profile(false), off), squad), DepotB), Is.True, "off: B, the farthest from the single last seen point");
        }

        /// <summary>TH-2: depot C has Enemy 0 but its line passes three remembered Windrunners (225 there); D's line is clear. On: target D. With D removed: no squad.</summary>
        [Test]
        public void TH2_Route()
        {
            Simulation sim = Game(out List<EntityId> squad);
            AiMemory both = RouteMemory(sim, true);
            List<AiIntent> intents = Decide(sim, Profile(true), both);
            Assert.That(AiThreatMap.EnemyAt(both, Profile(true), DepotA), Is.EqualTo(0), "C itself is empty");
            Assert.That(AiThreatMap.EnemyAt(both, Profile(true), new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("30.5"))), Is.EqualTo(225));
            Assert.That(At(SquadGoal(intents, squad), DepotB), Is.True, "D");
            AiMemory only = RouteMemory(sim, false);
            Assert.That(SquadGoal(Decide(sim, Profile(true), only), squad), Is.Null);
            Assert.That(only.HarassGroup.Count, Is.EqualTo(0));
        }

        /// <summary>TH-3: every candidate has r of at least 150 (two remembered Windrunners at each depot): no intent for any squad unit, HarassGroup empty, NextHarassTick unchanged; 1200 ticks later, with the sightings expired, the squad starts.</summary>
        [Test]
        public void TH3_NoneEligible()
        {
            Simulation sim = Game(out List<EntityId> squad);
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.pearl_depot", DepotA, sim.Clock.Tick);
            Know(memory, 901, "water.pearl_depot", DepotB, sim.Clock.Tick);
            Sight(memory, 800, 2, DepotA, sim.Clock.Tick);
            Sight(memory, 810, 2, DepotB, sim.Clock.Tick);
            // NextHarassTick before the think. Unit: ticks.
            int next = memory.NextHarassTick;
            List<AiIntent> blocked = Decide(sim, Profile(true), memory);
            Assert.That(SquadGoal(blocked, squad), Is.Null);
            foreach (AiIntent intent in blocked)
            {
                Assert.That(squad.Contains(intent.Unit) && intent.Kind == AiIntentKind.AttackUnit, Is.False, "no attack order for a squad unit: " + intent.Kind);
            }
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(0));
            Assert.That(memory.NextHarassTick, Is.EqualTo(next));
            // The sightings age out (M = 1200 ticks); the structures stay known (their tiles are not visible).
            while (sim.Clock.Tick < 1300)
            {
                sim.Step();
            }
            List<AiIntent> started = Decide(sim, Profile(true), memory);
            Assert.That(memory.ThreatSightings.Count, Is.EqualTo(0));
            Assert.That(new List<EntityId>(memory.HarassGroup), Is.EqualTo(squad));
            Assert.That(SquadGoal(started, squad), Is.Not.Null);
        }

        /// <summary>TH-4: two eligible depots with equal r (0): the one farther from the army position; equal again (the army point equidistant): the lower ID.</summary>
        [Test]
        public void TH4_Ties()
        {
            Simulation sim = Game(out List<EntityId> squad);
            // Army point nearer B than A: A is farther, so A (whatever the IDs).
            foreach ((int idA, int idB) in new[] { (900, 901), (901, 900) })
            {
                AiMemory nearB = new AiMemory();
                Know(nearB, idA, "water.pearl_depot", DepotA, sim.Clock.Tick);
                Know(nearB, idB, "water.pearl_depot", DepotB, sim.Clock.Tick);
                ArmyAt(nearB, "40.5", "20.5", sim.Clock.Tick);
                Assert.That(At(SquadGoal(Decide(sim, Profile(true), nearB), squad), DepotA), Is.True, "A is farther from the army point; IDs " + idA + "," + idB);
            }
            // Army point (31.5, 31.5) is 19 tiles in each axis from A (12.5, 50.5) and from B' = (50.5, 12.5): equal squared distances, so the lower ID decides.
            FixVec2 mirror = new FixVec2(Fix64.Parse("50.5"), Fix64.Parse("12.5"));
            foreach ((int idA, int idMirror, FixVec2 expected) in new[] { (900, 901, DepotA), (901, 900, mirror) })
            {
                AiMemory equal = new AiMemory();
                Know(equal, idA, "water.pearl_depot", DepotA, sim.Clock.Tick);
                Know(equal, idMirror, "water.pearl_depot", mirror, sim.Clock.Tick);
                ArmyAt(equal, "31.5", "31.5", sim.Clock.Tick);
                Assert.That(At(SquadGoal(Decide(sim, Profile(true), equal), squad), expected), Is.True, "equal distance, lower ID wins; IDs " + idA + "," + idMirror);
            }
        }

        /// <summary>TH-5: the only eligible depot (one known Coral Turret beside it: 100 below the squad's 104 after a wound) is refused by the existing defence check when the predictor says the squad loses: no squad, as today.</summary>
        [Test]
        public void TH5_DefenceCheckKept()
        {
            Simulation sim = Game(out List<EntityId> squad);
            // The squad at 70 percent health: strength 2 x floor(75 x 0.7) = 104, so the turret's 100 leaves the depot eligible.
            foreach (EntityId id in squad)
            {
                Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
                unit!.SetHealth(unit.Definition.MaxHealth * Fix64.Parse("0.7"));
            }
            AiMemory memory = new AiMemory();
            Know(memory, 900, "water.pearl_depot", DepotA, sim.Clock.Tick);
            Know(memory, 901, "water.coral_turret", DepotA + new FixVec2(Fix64.FromInt(2), Fix64.Zero), sim.Clock.Tick);
            ArmyAt(memory, "13.5", "40.5", sim.Clock.Tick);
            List<AiIntent> intents = Decide(sim, Profile(true), memory);
            Assert.That(AiThreatMap.EnemyAt(memory, Profile(true), DepotA), Is.EqualTo(100));
            Assert.That(memory.HarassGroup.Count, Is.EqualTo(0), "the predictor says the squad loses at the turret");
            Assert.That(SquadGoal(intents, squad), Is.Null);
        }

        /// <summary>TH-6: no enemy army position and no known HQ (the enemy spawn is cleared), one known depot with r 0: on, the squad starts; off, no squad (today's early return).</summary>
        [Test]
        public void TH6_NoArmyKnown()
        {
            Simulation sim = Game(out List<EntityId> squad);
            // Memory with one depot and the enemy spawn (index 1) cleared, so EnemyArmyPosition finds nothing.
            Func<AiMemory> fresh = () =>
            {
                AiMemory memory = new AiMemory();
                memory.OwnSpawnIndex = 0;
                memory.ClearedSpawns.Add(1);
                Know(memory, 900, "water.pearl_depot", DepotA, sim.Clock.Tick);
                return memory;
            };
            AiMemory on = fresh();
            Assert.That(At(SquadGoal(Decide(sim, Profile(true), on), squad), DepotA), Is.True, "on: the squad starts");
            AiMemory off = fresh();
            Assert.That(SquadGoal(Decide(sim, Profile(false), off), squad), Is.Null, "off: no squad");
            Assert.That(off.HarassGroup.Count, Is.EqualTo(0));
        }

        /// <summary>TH-7: use_threat_harass true with use_threat_map false, and separately with use_target_choice false, fails to load naming use_threat_harass; every shipped profile carries the key and only aggressive_s5_s may have it on.</summary>
        [Test]
        public void TH7_Validation()
        {
            string file = "ai/aggressive_s5_s.json";
            // Text with rule TH on, as the validation tests need it whatever the shipped value is.
            string on = Flag(CandidateText, "use_threat_harass", true);
            foreach ((string oldText, string newText) in new[] { ("\"use_threat_map\": true,", "\"use_threat_map\": false,"), ("\"use_target_choice\": true,", "\"use_target_choice\": false,") })
            {
                Assert.That(on.Contains(oldText), Is.True, oldText);
                // The text with the prerequisite switched off. Text.
                string edited = on.Replace(oldText, newText);
                if (oldText.Contains("use_threat_map"))
                {
                    edited = Flag(edited, "use_threat_safety", false);
                }
                DataValidationException error = Expect.Throws<DataValidationException>(() => AiProfile.Parse(edited, file, Data.Content));
                Assert.That(error.Message, Does.Contain(file).And.Contain("use_threat_harass"), newText);
            }
            Assert.That(AiProfile.Parse(on, file, Data.Content).UseThreatHarass, Is.True);
            // A missing key and a wrongly typed one fail naming it.
            string without = System.Text.RegularExpressions.Regex.Replace(CandidateText, "\\n  \"use_threat_harass\":[^\\n]*", string.Empty);
            Assert.That(without, Does.Not.Contain("\"use_threat_harass\":"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiProfile.Parse(without, file, Data.Content)).Message, Does.Contain(file).And.Contain("use_threat_harass"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiProfile.Parse(Flag(CandidateText, "use_threat_harass", true).Replace("\"use_threat_harass\": true,", "\"use_threat_harass\": 1,"), file, Data.Content)).Message, Does.Contain("use_threat_harass"));
            foreach (string id in Data.AiProfiles.Keys)
            {
                Assert.That(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json")), Does.Contain("\"use_threat_harass\":"), id);
                Assert.That(AiProfile.FromDataSet(Data, id).UseThreatHarass && id != "aggressive_s5_s", Is.False, id);
            }
        }

        /// <summary>TH-8: TH-1 and TH-2 run twice give identical intents and memory.</summary>
        [Test]
        public void TH8_Determinism()
        {
            // One run of both situations as text.
            Func<string> run = () =>
            {
                Simulation sim = Game(out List<EntityId> squad);
                AiMemory first = WeakestTargetMemory(sim);
                string one = Signature(Decide(sim, Profile(true), first), first);
                Simulation other = Game(out List<EntityId> otherSquad);
                AiMemory second = RouteMemory(other, true);
                return one + "#" + Signature(Decide(other, Profile(true), second), second);
            };
            Assert.That(run(), Is.EqualTo(run()));
        }

        /// <summary>TH-9: off path. RunSquad intents are identical with the switch on and off once a squad is running toward the same target (the pins of the spec are held by their own tests and are not re-pinned).</summary>
        [Test]
        public void TH9_RunningSquadIgnoresTheSwitch()
        {
            // The second think of a squad started by rule TH toward depot A, decided with the given switch, as text.
            Func<bool, string> second = harass =>
            {
                Simulation sim = Game(out List<EntityId> squad);
                AiMemory memory = WeakestTargetMemory(sim);
                Decide(sim, Profile(true), memory);
                Assert.That(new List<EntityId>(memory.HarassGroup), Is.EqualTo(squad), "the squad runs");
                sim.Step();
                sim.Step();
                return Signature(Decide(sim, Profile(harass), memory), memory);
            };
            Assert.That(second(false), Is.EqualTo(second(true)));
        }
    }
}
