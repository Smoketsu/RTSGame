// rts/tests/Sim.Tests/Ai/AiArmyMixTests.cs
// Tests C-8 to C-15 of rts/docs/specs/skirmish_ai_v3.md package C: the counter scores and mix weights (C.4), rule T (tech on demand), rule SD
// (static defence after raids and against an unanswered layer), rule RS (rare priority), the profile checks and the switch dependencies.
// Part of Rts.Sim.Tests. Every test builds a real greybox_1v1 simulation (AiWorldView has no public constructor), spawns units with script
// commands, writes the enemy model into the AiMemory directly (the model itself is tested by AiIntelTests) and runs AiCounter or AiBrain.Decide.
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Skirmish AI v3 package C: counter-mix, tech on demand, static defence, rare priority.</summary>
    [TestFixture]
    public class AiArmyMixTests
    {
        /// <summary>Player slot 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and removals. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The shipped stage 2 profile.</summary>
        private static AiProfile S2 => AiProfile.FromDataSet(Data, "aggressive_s2");

        /// <summary>The text of the shipped stage 2 profile, line endings normalised.</summary>
        private static string S2Text => File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s2.json")).Replace("\r\n", "\n");

        /// <summary>The stage 2 profile with text edits applied (each old text must occur).</summary>
        private static AiProfile S2Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = S2Text;
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s2.json", Data.Content);
        }

        /// <summary>The skai composition text of the shipped stage 2 profile.</summary>
        private const string SkaiBase = "{ \"skai.windrunner\": 3, \"skai.talon_archer\": 2, \"skai.roc_rider\": 1, \"skai.stormcaller\": 1 }";

        /// <summary>A started greybox_1v1 simulation with the given factions in slots 0 and 1.</summary>
        private static Simulation Greybox(string faction0 = "skai", string faction1 = "skai")
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
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

        /// <summary>Removes a unit with a script command and steps once.</summary>
        private void Remove(Simulation sim, EntityId unit)
        {
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, unit));
            sim.Step();
        }

        /// <summary>Steps the simulation until its clock reads <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>Sets the stock of a player (test-only access to the internal setter).</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>A base for P0 with the HQ at (7, 7), six workers (one gathering) and a first producer; the stock is 5000 common, 0 rare. Returns the first worker.</summary>
        private EntityId Base(Simulation sim, string hq, string worker, string producer, string producerX = "13.5", string producerY = "6.5")
        {
            Spawn(sim, hq, P0, "7", "7");
            // The first worker, sent to gather so the player has income.
            EntityId gatherer = EntityId.None;
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                EntityId id = Spawn(sim, worker, P0, "6", (8 + i / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
                gatherer = i == 0 ? id : gatherer;
            }
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, gatherer, 0));
            sim.Step();
            Spawn(sim, producer, P0, producerX, producerY);
            SetStock(sim, P0, 5000, 0);
            return gatherer;
        }

        /// <summary>Decides for P0 with the given memory (an AttackThreshold is set so the first think does not reset the stance).</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory)
        {
            if (memory.AttackThreshold < 0)
            {
                memory.AttackThreshold = 8;
            }
            // The faction's plan.
            AiFactionPlan plan = profile.RequireFaction(sim.Players.TryGet(P0, out Player? self) ? self.FactionId : "skai");
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, plan, memory, new DeterministicRandom(1));
        }

        /// <summary>The Build intents for one definition.</summary>
        private static List<AiIntent> BuildsOf(List<AiIntent> intents, string definition)
        {
            return intents.FindAll(i => i.Kind == AiIntentKind.Build && i.DefinitionId == definition);
        }

        /// <summary>Writes <paramref name="count"/> units of an enemy type into the model as just seen.</summary>
        private static void Believe(AiMemory memory, string type, int count, int tick)
        {
            memory.EnemyTypes[type] = new EnemyTypeEstimate { Count = Fix64.FromInt(count), LastSeenTick = tick };
        }

        /// <summary>Asserts a fixed-point value is within 0.000001 of the expected decimal.</summary>
        private static void Near(Fix64 actual, string expected)
        {
            Assert.That(Math.Abs((actual - Fix64.Parse(expected)).Raw), Is.LessThanOrEqualTo(Fix64.Parse("0.000001").Raw), actual.ToString());
        }

        /// <summary>The mix weights of a plan against a model, as "id=weight" pairs for messages.</summary>
        private static SortedDictionary<string, int> MixOf(Simulation sim, AiProfile profile, string faction, AiMemory memory)
        {
            return AiCounter.Mix(AiWorldView.Build(sim, P0), profile, profile.RequireFaction(faction), memory);
        }

        /// <summary>C-8: water against 4 Roc Riders: only the Spoutling can hit air, so it takes the counter share.</summary>
        [Test]
        public void C8_WaterAgainstRocRiders()
        {
            Simulation sim = Greybox("water", "skai");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            AiMemory memory = new AiMemory();
            Believe(memory, "skai.roc_rider", 4, sim.Clock.Tick);
            AiWorldView view = AiWorldView.Build(sim, P0);
            List<FightEntry> enemies = AiCounter.Entries(view, AiIntel.ModelCounts(view, S2, memory));
            Near(AiCounter.Score(Data.Content.Units["water.spoutling"], enemies, S2, view), "0.0013333");
            Assert.That(AiCounter.Score(Data.Content.Units["water.grasper"], enemies, S2, view), Is.EqualTo(Fix64.Zero));
            Assert.That(AiCounter.Score(Data.Content.Units["water.shellback"], enemies, S2, view), Is.EqualTo(Fix64.Zero));
            Assert.That(AiCounter.Score(Data.Content.Units["water.ink_lobber"], enemies, S2, view), Is.EqualTo(Fix64.Zero));
            Assert.That(AiCounter.Score(Data.Content.Units["water.reef_runner"], enemies, S2, view), Is.EqualTo(Fix64.Zero));
            SortedDictionary<string, int> mix = MixOf(sim, S2, "water", memory);
            Assert.That(mix["water.grasper"], Is.EqualTo(150));
            Assert.That(mix["water.shellback"], Is.EqualTo(50));
            Assert.That(mix["water.ink_lobber"], Is.EqualTo(50));
            Assert.That(mix["water.spoutling"], Is.EqualTo(750));
            Assert.That(mix["water.reef_runner"], Is.EqualTo(0));
        }

        /// <summary>C-9: Skai against 6 Pummelers: only the (untouchable) Roc Rider is kept in the counter share.</summary>
        [Test]
        public void C9_SkaiAgainstPummelers()
        {
            Simulation sim = Greybox("skai", "water");
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            AiMemory memory = new AiMemory();
            Believe(memory, "water.grasper", 6, sim.Clock.Tick);
            AiWorldView view = AiWorldView.Build(sim, P0);
            List<FightEntry> enemies = AiCounter.Entries(view, AiIntel.ModelCounts(view, S2, memory));
            Near(AiCounter.Score(Data.Content.Units["skai.windrunner"], enemies, S2, view), "0.005");
            Near(AiCounter.Score(Data.Content.Units["skai.talon_archer"], enemies, S2, view), "0.0045");
            Near(AiCounter.Score(Data.Content.Units["skai.stormcaller"], enemies, S2, view), "0.0014178");
            Near(AiCounter.Score(Data.Content.Units["skai.roc_rider"], enemies, S2, view), "0.0224");
            SortedDictionary<string, int> mix = MixOf(sim, S2, "skai", memory);
            Assert.That(mix["skai.windrunner"], Is.EqualTo(128));
            Assert.That(mix["skai.talon_archer"], Is.EqualTo(85));
            Assert.That(mix["skai.roc_rider"], Is.EqualTo(742));
            Assert.That(mix["skai.stormcaller"], Is.EqualTo(42));
        }

        /// <summary>C-10: nothing known: the mix is the base composition scaled to 1000, and rule P trains what it trains with the counter-mix off.</summary>
        [Test]
        public void C10_NothingKnown()
        {
            Simulation sim = Greybox();
            Base(sim, "skai.great_aerie", "skai.fledgling", "skai.roost");
            Spawn(sim, "skai.windspire", P0, "6.5", "14.5");
            SortedDictionary<string, int> mix = MixOf(sim, S2, "skai", new AiMemory());
            Assert.That(mix["skai.windrunner"], Is.EqualTo(428));
            Assert.That(mix["skai.talon_archer"], Is.EqualTo(285));
            Assert.That(mix["skai.roc_rider"], Is.EqualTo(142));
            Assert.That(mix["skai.stormcaller"], Is.EqualTo(142));
            AiProfile off = S2Edited("\"use_counter_mix\": true", "\"use_counter_mix\": false", "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false",
                "\"use_rare_priority\": true", "\"use_rare_priority\": false", "\"use_static_defence\": true", "\"use_static_defence\": false");
            List<AiIntent> withMix = Decide(sim, S2, new AiMemory());
            List<AiIntent> without = Decide(sim, off, new AiMemory());
            List<string> trainsWith = withMix.FindAll(i => i.Kind == AiIntentKind.Train).ConvertAll(i => i.DefinitionId + "@" + i.Unit);
            List<string> trainsWithout = without.FindAll(i => i.Kind == AiIntentKind.Train).ConvertAll(i => i.DefinitionId + "@" + i.Unit);
            Assert.That(trainsWith, Is.EqualTo(trainsWithout));
            Assert.That(trainsWith.Count, Is.GreaterThan(0));
        }

        /// <summary>C-11: rule T builds the Windspire when the Stormcaller weighs 300 of 1000 and nothing trains it; not while a Windspire is under construction or stands.</summary>
        [Test]
        public void C11_TechOnDemand()
        {
            AiProfile profile = S2Edited(SkaiBase, "{ \"skai.windrunner\": 7, \"skai.stormcaller\": 3 }");
            Simulation sim = Greybox();
            EntityId worker = Base(sim, "skai.great_aerie", "skai.fledgling", "skai.roost");
            Assert.That(MixOf(sim, profile, "skai", new AiMemory())["skai.stormcaller"], Is.EqualTo(300));
            Assert.That(BuildsOf(Decide(sim, profile, new AiMemory()), "skai.windspire").Count, Is.EqualTo(1));
            // A Windspire under construction: the worker builds one.
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, worker, "skai.windspire", 6, 15));
            // Ticks stepped while waiting for the structure to appear. Unit: ticks. Range: 0 to 600.
            for (int i = 0; i < 600 && !HasUnderConstruction(sim, "skai.windspire"); i++)
            {
                sim.Step();
            }
            Assert.That(HasUnderConstruction(sim, "skai.windspire"), Is.True, "the Windspire is being built");
            Assert.That(BuildsOf(Decide(sim, profile, new AiMemory()), "skai.windspire").Count, Is.EqualTo(0));
            // A complete one: nothing to build either.
            Simulation done = Greybox();
            Base(done, "skai.great_aerie", "skai.fledgling", "skai.roost");
            Spawn(done, "skai.windspire", P0, "6.5", "14.5");
            Assert.That(BuildsOf(Decide(done, profile, new AiMemory()), "skai.windspire").Count, Is.EqualTo(0));
        }

        /// <summary>True when P0 has an own structure of the definition under construction.</summary>
        private static bool HasUnderConstruction(Simulation sim, string definition)
        {
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0).OwnUnits)
            {
                if (unit.IsUnderConstruction && unit.Definition.Id == definition)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>C-12: raided at 100 s and again at 130 s: one Perch Tower beside the HQ toward the raiders; with two there already, a third raid builds none.</summary>
        [Test]
        public void C12_RaidedTwice()
        {
            // The spec's value of static_defence_after_raids (the shipped aggressive_s2 uses 1, see the changelog).
            AiProfile twice = S2Edited("\"static_defence_after_raids\": 1", "\"static_defence_after_raids\": 2");
            Simulation sim = Greybox();
            Base(sim, "skai.great_aerie", "skai.fledgling", "skai.roost");
            AiMemory memory = new AiMemory();
            StepTo(sim, 2000);
            EntityId raider = Spawn(sim, "skai.windrunner", P1, "11", "11");
            Assert.That(BuildsOf(Decide(sim, twice, memory), "skai.perch_tower").Count, Is.EqualTo(0));
            Assert.That(memory.RaidLog[Single(memory.RaidLog.Keys)].Count, Is.EqualTo(1));
            Remove(sim, raider);
            StepTo(sim, 2300);
            Assert.That(BuildsOf(Decide(sim, twice, memory), "skai.perch_tower").Count, Is.EqualTo(0));
            StepTo(sim, 2600);
            raider = Spawn(sim, "skai.windrunner", P1, "11", "11");
            List<AiIntent> second = BuildsOf(Decide(sim, twice, memory), "skai.perch_tower");
            Assert.That(second.Count, Is.EqualTo(1));
            // Chebyshev distance of the site from the HQ tile (7, 7): 2 to 4, and on the raiders' side (not behind the HQ).
            int ring = Math.Max(Math.Abs(second[0].TileX - 7), Math.Abs(second[0].TileY - 7));
            Assert.That(ring, Is.InRange(2, 4));
            Assert.That(second[0].TileX + second[0].TileY, Is.GreaterThan(14));
            // Two defences stand there now: a third raid builds nothing.
            Remove(sim, raider);
            Spawn(sim, "skai.perch_tower", P0, "10.5", "9.5");
            Spawn(sim, "skai.perch_tower", P0, "9.5", "10.5");
            StepTo(sim, 3300);
            Spawn(sim, "skai.windrunner", P1, "11", "11");
            Assert.That(BuildsOf(Decide(sim, twice, memory), "skai.perch_tower").Count, Is.EqualTo(0));
            Assert.That(memory.RaidLog[Single(memory.RaidLog.Keys)].Count, Is.GreaterThanOrEqualTo(2));
        }

        /// <summary>The only element of a one-element collection.</summary>
        private static EntityId Single(IEnumerable<EntityId> ids)
        {
            // Elements seen. Unit: count.
            int seen = 0;
            EntityId only = EntityId.None;
            foreach (EntityId id in ids)
            {
                only = id;
                seen++;
            }
            Assert.That(seen, Is.EqualTo(1));
            return only;
        }

        /// <summary>C-13: water that knows of 6 air supply and has no way to hit air builds a Coral Turret at the HQ; with 25 rare and a Tidepool (a Spoutling is affordable) it does not.</summary>
        [Test]
        public void C13_UnansweredAir()
        {
            Simulation sim = Greybox("water", "skai");
            Base(sim, "water.grand_shell", "water.tender", "water.tidepool");
            AiMemory memory = new AiMemory();
            Believe(memory, "skai.roc_rider", 2, sim.Clock.Tick);
            List<AiIntent> turrets = BuildsOf(Decide(sim, S2, memory), "water.coral_turret");
            Assert.That(turrets.Count, Is.EqualTo(1));
            Assert.That(Math.Max(Math.Abs(turrets[0].TileX - 7), Math.Abs(turrets[0].TileY - 7)), Is.InRange(2, 4));
            SetStock(sim, P0, 5000, 25);
            AiMemory again = new AiMemory();
            Believe(again, "skai.roc_rider", 2, sim.Clock.Tick);
            Assert.That(BuildsOf(Decide(sim, S2, again), "water.coral_turret").Count, Is.EqualTo(0));
        }

        /// <summary>C-14: rare units weigh 400 of 1000 and no rare field is served: the depot goes to the rare flank at 100 s, before the 240 s clock; without rule RS it waits.</summary>
        [Test]
        public void C14_RarePriority()
        {
            AiProfile rare = S2Edited(SkaiBase, "{ \"skai.windrunner\": 3, \"skai.roc_rider\": 2 }");
            AiProfile plain = S2Edited(SkaiBase, "{ \"skai.windrunner\": 3, \"skai.roc_rider\": 2 }", "\"use_rare_priority\": true", "\"use_rare_priority\": false");
            Simulation sim = Greybox();
            Base(sim, "skai.great_aerie", "skai.fledgling", "skai.roost");
            StepTo(sim, 2000);
            Assert.That(BuildsOf(Decide(sim, plain, new AiMemory()), "skai.perch_depot").Count, Is.EqualTo(0));
            List<AiIntent> depots = BuildsOf(Decide(sim, rare, new AiMemory()), "skai.perch_depot");
            Assert.That(depots.Count, Is.EqualTo(1));
            // Field 10 is at tile (12, 49): the depot stands 3 to 6 tiles from it.
            Assert.That(Math.Max(Math.Abs(depots[0].TileX - 12), Math.Abs(depots[0].TileY - 49)), Is.InRange(3, 6));
        }

        /// <summary>C-15: every shipped profile's static_defence_max_per_base is within supply.json's static_defences_per_base_max.</summary>
        [Test]
        public void C15_ProfilesRespectTheBaseCap()
        {
            // Profiles checked. Unit: count.
            int checkedProfiles = 0;
            foreach (string id in Data.AiProfiles.Keys)
            {
                Assert.That(AiProfile.FromDataSet(Data, id).StaticDefenceMaxPerBase, Is.LessThanOrEqualTo(Data.Tuning.Supply.StaticDefencesPerBaseMax), id);
                checkedProfiles++;
            }
            Assert.That(checkedProfiles, Is.GreaterThanOrEqualTo(9));
        }

        /// <summary>The switch dependencies of 0.1: tech and rare priority need the counter-mix, static defence needs the enemy model; each error names the file and the field.</summary>
        [Test]
        public void C17_SwitchDependenciesAreValidated()
        {
            AiProfile Edit(params string[] pairs) => S2Edited(pairs);
            DataValidationException tech = Expect.Throws<DataValidationException>(() => Edit("\"use_counter_mix\": true", "\"use_counter_mix\": false", "\"use_rare_priority\": true", "\"use_rare_priority\": false"));
            Assert.That(tech.Message, Does.Contain("use_tech_on_demand").And.Contain("use_counter_mix").And.Contain("aggressive_s2.json"));
            DataValidationException rare = Expect.Throws<DataValidationException>(() => Edit("\"use_counter_mix\": true", "\"use_counter_mix\": false", "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false"));
            Assert.That(rare.Message, Does.Contain("use_rare_priority").And.Contain("use_counter_mix"));
            DataValidationException defence = Expect.Throws<DataValidationException>(() => Edit("\"use_enemy_model\": true", "\"use_enemy_model\": false", "\"use_counter_mix\": true", "\"use_counter_mix\": false",
                "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false", "\"use_rare_priority\": true", "\"use_rare_priority\": false"));
            Assert.That(defence.Message, Does.Contain("use_static_defence").And.Contain("use_enemy_model"));
        }
    }
}
