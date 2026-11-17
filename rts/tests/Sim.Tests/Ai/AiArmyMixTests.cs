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
            // These tests are about what the AI builds, not about scouting: every site counts as explored (specs/worker_construction.md).
            ExploreKit.ExploreAll(sim);
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

        /// <summary>Asserts the predictor advantages of the pool units against the spec's C2.8 values, each within 0.001 (units not listed are not checked).</summary>
        private static void AssertAdvantages(Simulation sim, AiProfile profile, string faction, AiMemory memory, params (string Id, string Expected)[] expected)
        {
            // Advantage per pool unit, filled by the predictor mix. Unit: ratio.
            SortedDictionary<string, Fix64> advantages = new SortedDictionary<string, Fix64>(StringComparer.Ordinal);
            AiCounter.PredictorMix(AiWorldView.Build(sim, P0), profile, profile.RequireFaction(faction), memory, advantages);
            foreach ((string id, string want) in expected)
            {
                // Distance from the spec's value. Unit: ratio. Range: 0 upward.
                Fix64 error = (advantages[id] - Fix64.Parse(want)).Abs();
                Assert.That(error <= Fix64.Parse("0.001"), Is.True, id + " advantage " + advantages[id] + " expected " + want);
            }
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
            // Changed by the missing roster and the developer's decision of 18 Nov 2026 that the Leviathan can target air. Leviathan score (hand-calculated): 80 / 3.0 = 26.667 dps (blast 1.0 against
            // armoured) x 320 health / (incoming 14 / 1.2 x 0.75 = 8.75 x value (300 + 2 x 80 = 460)^2) = 0.0046089, above the Spoutling's 0.0013333; keep 0.5 x 0.0046089 = 0.0023 drops the
            // Spoutling, so the Leviathan takes the whole counter share 0.7 x 1000 = 700 and the Spoutling keeps its base 50 (it was 750).
            Near(AiCounter.Score(Data.Content.Units["water.leviathan"], enemies, S2, view), "0.0046089");
            Assert.That(mix["water.spoutling"], Is.EqualTo(50));
            Assert.That(mix["water.leviathan"], Is.EqualTo(700));
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
            // Changed by the Thunderhead (specs/missing_roster.md). Its score (hand-calculated, Pummelers are armoured and cannot hit air): 44 / 2.5 = 17.6 dps x 190 health / (1 x (200 + 2 x 65 = 330)^2) =
            // 0.0307071, the top score; keep 0.5 x 0.0307071 = 0.01535 keeps the Roc Rider (0.0224) too. Kept sum 0.0531071: the Roc Rider gets 0.7 x 1000 x 0.0224 / 0.0531071 = 295.2 plus its
            // base 42.86 = 338, the Thunderhead 404.7 -> 404 (the Roc Rider was 742).
            Near(AiCounter.Score(Data.Content.Units["skai.thunderhead"], enemies, S2, view), "0.0307071");
            Assert.That(mix["skai.roc_rider"], Is.EqualTo(338));
            Assert.That(mix["skai.thunderhead"], Is.EqualTo(404));
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

        // ------------------------------------------------------------------ Package C2 (skirmish_ai_v3.md C2.8)

        /// <summary>The shipped package C2 candidate profile, as loaded (mix_eval_budget_common was retuned after the spec; see the C2 report).</summary>
        private static AiProfile S2bShipped => AiProfile.FromDataSet(Data, "aggressive_s2b");

        /// <summary>The package C2 candidate with the spec's mix_eval_budget_common 600, the value the C2.8 test numbers assume (every other value as shipped; the shipped file retuned the budget to 1200 and keeps the spec's counter_weight 0.7).</summary>
        private static AiProfile S2b
        {
            get
            {
                // The shipped file text with the spec's evaluation budget put back.
                string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s2b.json")).Replace("\r\n", "\n");
                // The key and its shipped value, which must be present or the replace below would silently do nothing.
                const string budgetPattern = "\"mix_eval_budget_common\": [0-9]+,";
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text, budgetPattern), Is.True, "aggressive_s2b mix_eval_budget_common");
                return AiProfile.Parse(System.Text.RegularExpressions.Regex.Replace(text, budgetPattern, "\"mix_eval_budget_common\": 600,"), "ai/aggressive_s2b.json", Data.Content);
            }
        }

        /// <summary>The ID of P0's HQ.</summary>
        private static EntityId HqId(Simulation sim)
        {
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0).OwnUnits)
            {
                if (unit.IsHq)
                {
                    return unit.Id;
                }
            }
            throw new AssertionException("no HQ");
        }

        /// <summary>Asserts a mix against the expected weights (units not listed must be 0).</summary>
        private static void AssertMix(SortedDictionary<string, int> mix, params (string Id, int Weight)[] expected)
        {
            // Expected weight per unit ID.
            SortedDictionary<string, int> want = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach ((string Id, int Weight) pair in expected)
            {
                want[pair.Id] = pair.Weight;
            }
            foreach (KeyValuePair<string, int> pair in mix)
            {
                want.TryGetValue(pair.Key, out int weight);
                Assert.That(pair.Value, Is.EqualTo(weight), pair.Key);
            }
        }

        /// <summary>The standard water mix of C2-1 and C2-5 (a Pummeler-heavy counter share).</summary>
        private static (string Id, int Weight)[] PummelerMix => new[] { ("water.grasper", 850), ("water.shellback", 50), ("water.ink_lobber", 50), ("water.spoutling", 50), ("water.reef_runner", 0) };

        /// <summary>A coordinate text for the i-th spawned unit, so units do not stack. Unit: tiles.</summary>
        private static string Row(int i)
        {
            return (9 + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>C2-1: water against 6 Pummelers keeps only the Pummeler, with or without rare in the bank.</summary>
        [Test]
        public void C2_1_WaterAgainstPummelers()
        {
            Simulation sim = Greybox("water", "water");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            AiMemory memory = new AiMemory();
            Believe(memory, "water.grasper", 6, sim.Clock.Tick);
            AssertMix(MixOf(sim, S2b, "water", memory), PummelerMix);
            AssertAdvantages(sim, S2b, "water", memory, ("water.grasper", "1.0000"), ("water.ink_lobber", "0.1736"), ("water.reef_runner", "0.4878"), ("water.shellback", "0.5514"), ("water.spoutling", "0.1228"));
            SetStock(sim, P0, 0, 400);
            // Changed by the Leviathan (specs/missing_roster.md R2-8). With rare 400 its advantage is 1.2208 (R2-7 arithmetic at budget 600: n = min(600 / 300, 500 / 80) = 2), above the Pummeler's 1.0000, so the
            // Leviathan alone is kept and takes the 700 counter share; the Pummeler keeps its base 150. Without rare (above) its advantage is 0.4323, not kept, and the mix is unchanged.
            AssertMix(MixOf(sim, S2b, "water", memory), ("water.grasper", 150), ("water.shellback", 50), ("water.ink_lobber", 50), ("water.spoutling", 50), ("water.leviathan", 700), ("water.reef_runner", 0));
            AssertAdvantages(sim, S2b, "water", memory, ("water.leviathan", "1.2208"), ("water.grasper", "1.0000"), ("water.ink_lobber", "0.4728"), ("water.spoutling", "0.2823"));
        }

        /// <summary>C2-2: water against 4 Roc Riders buys Spoutlings; with 4 Spoutlings already owned it adds Shellbacks.</summary>
        [Test]
        public void C2_2_WaterAgainstRocRiders()
        {
            Simulation sim = Greybox("water", "skai");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            AiMemory memory = new AiMemory();
            Believe(memory, "skai.roc_rider", 4, sim.Clock.Tick);
            // Changed by the Leviathan and the developer's decision of 18 Nov 2026 that it can target air (specs/missing_roster.md R2-8): at rare 0 it buys 1.25 units (rare allowance 100 / 80) and,
            // hitting air, its advantage is 1.1372 against the Spoutling's 0.3333 (Lanchester check: 1.25 Leviathans, 400 health, 33.3 dps against 3 Roc Riders, 480 health, 26.25 dps on armour:
            // 480 / 33.3 = 14.4 s to kill them, 400 / 26.25 = 15.2 s to die, plus the range head start). Only the Leviathan is kept, so it takes 700 and the Spoutling falls to its base 50 (it was 750).
            AssertMix(MixOf(sim, S2b, "water", memory), ("water.grasper", 150), ("water.shellback", 50), ("water.ink_lobber", 50), ("water.spoutling", 50), ("water.leviathan", 700));
            AssertAdvantages(sim, S2b, "water", memory, ("water.grasper", "0.0397"), ("water.ink_lobber", "0.0119"), ("water.reef_runner", "0.0225"), ("water.shellback", "0.0778"), ("water.spoutling", "0.3333"), ("water.leviathan", "1.1372"));
            // Own Spoutling number. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                Spawn(sim, "water.spoutling", P0, "9", Row(i));
            }
            // Changed by the Leviathan (same reason): with 4 Spoutlings owned its advantage 0.7826 beats the Shellback's 0.6708, and 0.9 x 0.7826 = 0.7044 drops the Shellback, so the Leviathan alone takes 700 (the Shellback was 750).
            AssertMix(MixOf(sim, S2b, "water", memory), ("water.grasper", 150), ("water.shellback", 50), ("water.ink_lobber", 50), ("water.spoutling", 50), ("water.leviathan", 700));
            AssertAdvantages(sim, S2b, "water", memory, ("water.grasper", "0.4038"), ("water.ink_lobber", "0.1877"), ("water.reef_runner", "0.2692"), ("water.shellback", "0.6708"), ("water.spoutling", "0.3967"), ("water.leviathan", "0.7826"));
        }

        /// <summary>C2-3 and C2-4: Skai against 6 Pummelers keeps the Roc Rider; against 8 Windrunners it keeps Windrunner and Talon.</summary>
        [Test]
        public void C2_3_and_4_Skai()
        {
            Simulation sim = Greybox("skai", "water");
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            AiMemory pummelers = new AiMemory();
            Believe(pummelers, "water.grasper", 6, sim.Clock.Tick);
            // Changed by the Thunderhead (specs/missing_roster.md R2-8): at rare 0 it buys 100 / 65 = 1.54 units and its advantage is 26.0398 against the Roc Rider's 16.4921 (ratio 1.58, close to the
            // 1.57 of R2-6, whose hand calculation is in the spec). 0.9 x 26.0398 = 23.44 drops the Roc Rider, so the Thunderhead alone takes 700 and the Roc Rider keeps its base 42 (it was 742).
            AssertMix(MixOf(sim, S2b, "skai", pummelers), ("skai.windrunner", 128), ("skai.talon_archer", 85), ("skai.roc_rider", 42), ("skai.stormcaller", 42), ("skai.thunderhead", 700));
            AssertAdvantages(sim, S2b, "skai", pummelers, ("skai.roc_rider", "16.4921"), ("skai.stormcaller", "0.1281"), ("skai.talon_archer", "0.4500"), ("skai.windrunner", "0.4878"), ("skai.thunderhead", "26.0398"));
            AiMemory windrunners = new AiMemory();
            Believe(windrunners, "skai.windrunner", 8, sim.Clock.Tick);
            AssertMix(MixOf(sim, S2b, "skai", windrunners), ("skai.windrunner", 581), ("skai.talon_archer", 332), ("skai.roc_rider", 42), ("skai.stormcaller", 42));
            AssertAdvantages(sim, S2b, "skai", windrunners, ("skai.roc_rider", "0.3505"), ("skai.stormcaller", "0.1516"), ("skai.talon_archer", "0.9545"), ("skai.windrunner", "1.0000"));
        }

        /// <summary>C2-5: the traced situation: with 5 Pummelers, 2 Shellbacks and a Spoutling against the blind water army, only the Pummeler is kept.</summary>
        [Test]
        public void C2_5_TracedSituation()
        {
            Simulation sim = Greybox("water", "water");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            // Own army: unit and count; row of the next unit.
            int row = 0;
            foreach ((string id, int count) in new[] { ("water.grasper", 5), ("water.shellback", 2), ("water.spoutling", 1) })
            {
                for (int i = 0; i < count; i++)
                {
                    Spawn(sim, id, P0, "9", Row(row++));
                }
            }
            AiMemory memory = new AiMemory();
            Believe(memory, "water.grasper", 3, sim.Clock.Tick);
            Believe(memory, "water.shellback", 1, sim.Clock.Tick);
            Believe(memory, "water.ink_lobber", 1, sim.Clock.Tick);
            Believe(memory, "water.spoutling", 1, sim.Clock.Tick);
            Believe(memory, "water.reef_runner", 1, sim.Clock.Tick);
            AssertMix(MixOf(sim, S2b, "water", memory), PummelerMix);
            AssertAdvantages(sim, S2b, "water", memory, ("water.grasper", "1.5376"), ("water.ink_lobber", "0.8568"), ("water.reef_runner", "1.1546"), ("water.shellback", "1.3267"), ("water.spoutling", "0.7932"));
        }

        /// <summary>C2-6: implied units never raise the confidence; sightings do; the switch off keeps the package C confidence.</summary>
        [Test]
        public void C2_6_ConfidenceFromSightings()
        {
            Simulation sim = Greybox("water", "water");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            AiMemory memory = new AiMemory();
            memory.KnownEnemyStructures.Add(new EntityId(500), new KnownStructure { DefinitionId = "water.tidepool", Position = FixVec2.FromInts(50, 50) });
            memory.KnownEnemyStructures.Add(new EntityId(501), new KnownStructure { DefinitionId = "water.shell_forge", Position = FixVec2.FromInts(52, 50) });
            AssertMix(MixOf(sim, S2b, "water", memory), ("water.grasper", 500), ("water.shellback", 166), ("water.ink_lobber", 166), ("water.spoutling", 166));
            Believe(memory, "water.grasper", 2, sim.Clock.Tick);
            // Confidence 0.7 x 4 / 8 = 0.35: base share 650 of 1000, so a unit that is not kept holds floor(650 / 6) = 108.
            SortedDictionary<string, int> half = MixOf(sim, S2b, "water", memory);
            Assert.That(half["water.reef_runner"], Is.EqualTo(0));
            Assert.That(half["water.grasper"], Is.GreaterThan(500));
            Assert.That(half["water.shellback"], Is.EqualTo(108));
            // The package C scoring (switch off, aggressive_s2) on the same model: implied units give full confidence 0.7, so an unkept unit (the Ink Lobber or the Spoutling) holds floor(300 / 6) = 50.
            SortedDictionary<string, int> old = MixOf(sim, S2, "water", memory);
            Assert.That(Math.Min(old["water.ink_lobber"], old["water.spoutling"]), Is.EqualTo(50));
        }

        /// <summary>The Decide result as comparable text lines (kind, unit, definition, tile, field).</summary>
        private static List<string> Lines(List<AiIntent> intents)
        {
            return intents.ConvertAll(i => i.Kind + " " + i.Unit + " " + i.DefinitionId + " " + i.TileX + "," + i.TileY + " " + i.Field);
        }

        /// <summary>C2-13: the same inputs twice give the same mix and intents without a random draw; own units given in another order give the same mix.</summary>
        [Test]
        public void C2_13_DeterministicAndPure()
        {
            // Mix of a water base with the own army spawned in the given order.
            SortedDictionary<string, int> MixWith(string[] order)
            {
                Simulation own = Greybox("water", "water");
                Spawn(own, "water.grand_shell", P0, "7", "7");
                for (int i = 0; i < order.Length; i++)
                {
                    Spawn(own, order[i], P0, "9", Row(i));
                }
                AiMemory seen = new AiMemory();
                Believe(seen, "water.grasper", 3, own.Clock.Tick);
                Believe(seen, "water.spoutling", 2, own.Clock.Tick);
                return MixOf(own, S2b, "water", seen);
            }
            SortedDictionary<string, int> a = MixWith(new[] { "water.grasper", "water.shellback", "water.spoutling", "water.grasper" });
            SortedDictionary<string, int> b = MixWith(new[] { "water.spoutling", "water.grasper", "water.grasper", "water.shellback" });
            Assert.That(new List<KeyValuePair<string, int>>(b), Is.EqualTo(new List<KeyValuePair<string, int>>(a)));
            Simulation sim = Greybox("water", "water");
            Base(sim, "water.grand_shell", "water.tender", "water.tidepool");
            Spawn(sim, "water.reef_runner", P1, "11", "11");
            AiFactionPlan plan = S2b.RequireFaction("water");
            DeterministicRandom rng1 = new DeterministicRandom(1);
            DeterministicRandom rng2 = new DeterministicRandom(1);
            AiMemory m1 = new AiMemory { AttackThreshold = 8 };
            AiMemory m2 = new AiMemory { AttackThreshold = 8 };
            List<string> first = Lines(AiBrain.Decide(AiWorldView.Build(sim, P0, m1.KnownFieldRemaining), S2b, plan, m1, rng1));
            List<string> second = Lines(AiBrain.Decide(AiWorldView.Build(sim, P0, m2.KnownFieldRemaining), S2b, plan, m2, rng2));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(rng2.DrawCount, Is.EqualTo(rng1.DrawCount));
            // The package C2 mix draws nothing: the same think with the switch off draws the same number.
            DeterministicRandom rng3 = new DeterministicRandom(1);
            AiMemory m3 = new AiMemory { AttackThreshold = 8 };
            AiBrain.Decide(AiWorldView.Build(sim, P0, m3.KnownFieldRemaining), S2, S2.RequireFaction("water"), m3, rng3);
            Assert.That(rng3.DrawCount, Is.EqualTo(rng1.DrawCount));
        }

        /// <summary>A water base for P0 with an enemy force near the HQ and optionally a hit on the HQ, a standing turret and Pummelers; returns the turret builds of one think.</summary>
        private List<AiIntent> RaidThink(AiProfile profile, string enemyFaction, string[] enemies, bool hit, out AiMemory memory, int grassers = 0, bool turret = false)
        {
            Simulation sim = Greybox("water", enemyFaction);
            Base(sim, "water.grand_shell", "water.tender", "water.tidepool");
            // Extra supply structures so the Pummelers and the turret fit under the supply cap. Number. Range: 0 to 3.
            for (int i = 0; i < 4; i++)
            {
                Spawn(sim, "water.tidepool", P0, (16.5 + 3 * i).ToString(System.Globalization.CultureInfo.InvariantCulture), "6.5");
            }
            // Own Pummeler number. Range: 0 to grassers - 1.
            for (int i = 0; i < grassers; i++)
            {
                Spawn(sim, "water.grasper", P0, "9", (9 + i % 3).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (turret)
            {
                Spawn(sim, "water.coral_turret", P0, "10.5", "9.5");
            }
            // Enemy number. Range: 0 to enemies - 1.
            for (int i = 0; i < enemies.Length; i++)
            {
                Spawn(sim, enemies[i], P1, (11 + i * 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture), "11");
            }
            memory = new AiMemory();
            if (hit)
            {
                memory.RecentDamage[HqId(sim)] = new DamageReport { Tick = sim.Clock.Tick - 10, Position = FixVec2.FromInts(7, 7) };
            }
            return BuildsOf(Decide(sim, profile, memory), "water.coral_turret");
        }

        /// <summary>C2-8: a raid needs strength raid_min_supply: a lone Reef Runner and a hit is 2 and does not count at 3; two Windrunners and a hit is 3 and does; at 1 the lone scout counts.</summary>
        [Test]
        public void C2_8_RaidStrength()
        {
            AiProfile three = S2Edited("\"raid_min_supply\": 1,", "\"raid_min_supply\": 3,");
            List<AiIntent> lone = RaidThink(three, "water", new[] { "water.reef_runner" }, true, out AiMemory loneMemory);
            Assert.That(lone.Count, Is.EqualTo(0));
            Assert.That(loneMemory.RaidLog.Count, Is.EqualTo(0));
            List<AiIntent> pair = RaidThink(three, "skai", new[] { "skai.windrunner", "skai.windrunner" }, true, out AiMemory pairMemory);
            Assert.That(pairMemory.RaidLog.Count, Is.EqualTo(1));
            Assert.That(pair.Count, Is.EqualTo(1));
            Assert.That(Math.Max(Math.Abs(pair[0].TileX - 7), Math.Abs(pair[0].TileY - 7)), Is.InRange(2, 4));
            List<AiIntent> one = RaidThink(S2, "water", new[] { "water.reef_runner" }, true, out AiMemory oneMemory);
            Assert.That(oneMemory.RaidLog.Count, Is.EqualTo(1));
            Assert.That(one.Count, Is.EqualTo(1));
        }

        /// <summary>C2-9: the value cap: 6 Pummelers (600) cannot carry a 100 defence at 0.15, 7 can; with one turret 13 cannot, 14 can; share 0 builds with no army; the unanswered-layer step is never capped.</summary>
        [Test]
        public void C2_9_ValueCap()
        {
            AiProfile capped = S2Edited("\"static_defence_max_value_share\": 0,", "\"static_defence_max_value_share\": 0.15,");
            string[] raider = { "water.reef_runner" };
            Assert.That(RaidThink(capped, "water", raider, false, out _, 6).Count, Is.EqualTo(0));
            Assert.That(RaidThink(capped, "water", raider, false, out _, 7).Count, Is.EqualTo(1));
            Assert.That(RaidThink(capped, "water", raider, false, out _, 13, true).Count, Is.EqualTo(0));
            Assert.That(RaidThink(capped, "water", raider, false, out _, 14, true).Count, Is.EqualTo(1));
            Assert.That(RaidThink(S2, "water", raider, false, out _, 0).Count, Is.EqualTo(1), "share 0 is off");
            // Step 3 (an unanswered layer) ignores the cap: C-13 with no army still orders the turret.
            Simulation sim = Greybox("water", "skai");
            Base(sim, "water.grand_shell", "water.tender", "water.tidepool");
            AiMemory memory = new AiMemory();
            Believe(memory, "skai.roc_rider", 2, sim.Clock.Tick);
            Assert.That(BuildsOf(Decide(sim, capped, memory), "water.coral_turret").Count, Is.EqualTo(1));
        }

        /// <summary>C2-10: the early expansion is its own switch: on at 0 s the rare depot is built at once; rule RS alone only reorders; 60 s gates it; a served rare field does not trigger it.</summary>
        [Test]
        public void C2_10_EarlyExpansion()
        {
            string rareMix = "{ \"skai.windrunner\": 3, \"skai.roc_rider\": 2 }";
            AiProfile early = S2Edited(SkaiBase, rareMix);
            AiProfile noEarly = S2Edited(SkaiBase, rareMix, "\"use_early_expansion\": true", "\"use_early_expansion\": false");
            AiProfile sixty = S2Edited(SkaiBase, rareMix, "\"early_expansion_seconds\": 0", "\"early_expansion_seconds\": 60");
            // (a) the first think that can pay.
            Simulation sim = Greybox();
            Base(sim, "skai.great_aerie", "skai.fledgling", "skai.roost");
            StepTo(sim, 100);
            List<AiIntent> depots = BuildsOf(Decide(sim, early, new AiMemory()), "skai.perch_depot");
            Assert.That(depots.Count, Is.EqualTo(1));
            Assert.That(Math.Max(Math.Abs(depots[0].TileX - 12), Math.Abs(depots[0].TileY - 49)), Is.InRange(3, 6));
            // (b) switch off, rule RS on: nothing at 100 s; at 240 s the depot goes to the rare site.
            StepTo(sim, 2000);
            Assert.That(BuildsOf(Decide(sim, noEarly, new AiMemory()), "skai.perch_depot").Count, Is.EqualTo(0));
            StepTo(sim, 4800);
            List<AiIntent> late = BuildsOf(Decide(sim, noEarly, new AiMemory()), "skai.perch_depot");
            Assert.That(late.Count, Is.EqualTo(1));
            Assert.That(Math.Max(Math.Abs(late[0].TileX - 12), Math.Abs(late[0].TileY - 49)), Is.InRange(3, 6));
            // (c) 60 s: none at 59.5 s, one at 60 s.
            Simulation timed = Greybox();
            Base(timed, "skai.great_aerie", "skai.fledgling", "skai.roost");
            StepTo(timed, 1190);
            Assert.That(BuildsOf(Decide(timed, sixty, new AiMemory()), "skai.perch_depot").Count, Is.EqualTo(0));
            StepTo(timed, 1200);
            Assert.That(BuildsOf(Decide(timed, sixty, new AiMemory()), "skai.perch_depot").Count, Is.EqualTo(1));
            // (d) a rare field already served: nothing before 240 s.
            Simulation served = Greybox();
            Base(served, "skai.great_aerie", "skai.fledgling", "skai.roost");
            Spawn(served, "skai.perch_depot", P0, "9.5", "49.5");
            StepTo(served, 2000);
            Assert.That(BuildsOf(Decide(served, early, new AiMemory()), "skai.perch_depot").Count, Is.EqualTo(0));
            // (e) C2 review item 1: a mix whose rare share (166 of 1000) is below rare_need_weight_share still sends the early depot to the rare site.
            AiProfile lowRare = S2Edited(SkaiBase, "{ \"skai.windrunner\": 5, \"skai.roc_rider\": 1 }");
            Simulation low = Greybox();
            Base(low, "skai.great_aerie", "skai.fledgling", "skai.roost");
            StepTo(low, 100);
            List<AiIntent> lowDepots = BuildsOf(Decide(low, lowRare, new AiMemory()), "skai.perch_depot");
            Assert.That(lowDepots.Count, Is.EqualTo(1));
            Assert.That(Math.Max(Math.Abs(lowDepots[0].TileX - 12), Math.Abs(lowDepots[0].TileY - 49)), Is.InRange(3, 6));
        }

        /// <summary>C2-14: validation: the predictor mix needs the counter-mix, a missing key is named, and the shipped candidate loads with its C2.6 values.</summary>
        [Test]
        public void C2_14_Validation()
        {
            DataValidationException both = Expect.Throws<DataValidationException>(() => S2Edited("\"use_predictor_mix\": false,", "\"use_predictor_mix\": true,", "\"use_counter_mix\": true", "\"use_counter_mix\": false",
                "\"use_tech_on_demand\": true", "\"use_tech_on_demand\": false", "\"use_rare_priority\": true", "\"use_rare_priority\": false"));
            Assert.That(both.Message, Does.Contain("use_predictor_mix").And.Contain("use_counter_mix").And.Contain("aggressive_s2.json"));
            DataValidationException missing = Expect.Throws<DataValidationException>(() => S2Edited("\"raid_min_supply\": 1,", string.Empty));
            Assert.That(missing.Message, Does.Contain("raid_min_supply"));
            AiProfile b = S2bShipped;
            Assert.That(b.UsePredictorMix, Is.True);
            Assert.That(b.RareBankSoftCap, Is.EqualTo(300));
            Assert.That(b.RaidMinSupply, Is.EqualTo(3));
            Assert.That(b.StaticDefenceMaxValueShare == Fix64.Parse("0.15"), Is.True);
            Assert.That(b.MixEvalBudgetCommon, Is.EqualTo(1200), "retuned from 600");
            Assert.That(b.CounterWeight == Fix64.Parse("0.7"), Is.True);
            Assert.That(b.CounterKeepFraction == Fix64.Parse("0.9"), Is.True);
            Assert.That(b.CounterRareCostWeight == Fix64.One, Is.True);
            Assert.That(b.UseEarlyExpansion, Is.True);
            Assert.That(S2.UsePredictorMix, Is.False);
            Assert.That(S2.RaidMinSupply, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- R-1 (rts/docs/specs/missing_roster.md): the pool holds armed units only

        /// <summary>R1-1. A trainable unit with no weapon (the Tidecaller, trained at the Tidepool) is not in the pool; the armed Spoutling from the same producer is.</summary>
        [Test]
        public void Pool_SkipsAnUnarmedTrainableUnit()
        {
            Simulation sim = Greybox("water", "skai");
            AiWorldView view = AiWorldView.Build(sim, P0);
            UnitDefinition unarmed = Data.Content.Units["water.tidecaller"];
            Assert.That(unarmed.Weapon, Is.Null);
            Assert.That(Data.Content.Units["water.tidepool"].Economy.Trains, Does.Contain("water.tidecaller"));
            // Pool unit IDs, ordinal.
            List<string> ids = AiCounter.Pool(view, "water").ConvertAll(u => u.Id);
            Assert.That(ids, Does.Contain("water.spoutling"));
            Assert.That(ids, Does.Not.Contain("water.tidecaller"));
        }

        /// <summary>R1-2. On shipped data the pools hold exactly the armed combat units, in ordinal ID order (the Thunderhead and Leviathan join them, the Tidecaller does not).</summary>
        [Test]
        public void Pool_OnShippedData_IsTheArmedCombatUnits()
        {
            Simulation sim = Greybox("skai", "water");
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(AiCounter.Pool(view, "skai").ConvertAll(u => u.Id),
                Is.EqualTo(new[] { "skai.roc_rider", "skai.stormcaller", "skai.talon_archer", "skai.thunderhead", "skai.windrunner" }));
            Assert.That(AiCounter.Pool(view, "water").ConvertAll(u => u.Id),
                Is.EqualTo(new[] { "water.grasper", "water.ink_lobber", "water.leviathan", "water.reef_runner", "water.shellback", "water.spoutling" }));
        }

        /// <summary>The shipped aggressive_s3 profile (predictor mix: budget 1200, rare allowance 100, keep 0.9, counter weight 0.7).</summary>
        private static AiProfile S3 => AiProfile.FromDataSet(Data, "aggressive_s3");

        /// <summary>R2-6. Skai against 6 Pummelers with rare 400 (aggressive_s3): the Thunderhead alone is kept (advantage 53.731 against the Roc Rider's 34.239), and a free Windspire with 200 / 65 in stock trains it.</summary>
        [Test]
        public void R2_6_SkaiMixKeepsTheThunderhead()
        {
            Simulation sim = Greybox("skai", "water");
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            Spawn(sim, "skai.windspire", P0, "13", "6");
            SetStock(sim, P0, 0, 400);
            AiMemory memory = new AiMemory();
            Believe(memory, "water.grasper", 6, sim.Clock.Tick);
            AssertAdvantages(sim, S3, "skai", memory, ("skai.thunderhead", "53.731"), ("skai.roc_rider", "34.239"));
            AssertMix(MixOf(sim, S3, "skai", memory), ("skai.roc_rider", 42), ("skai.stormcaller", 42), ("skai.talon_archer", 85), ("skai.thunderhead", 700), ("skai.windrunner", 128));
            // Rule P orders it from the Windspire. Common 1000 rather than the spec's 200: the HQ buys a Fledgling first (rule 4b) and would leave less than 200; rare 65 pays one Thunderhead and no more.
            // A complete Storm Spire stands (walls_and_tech.md T.4: the Thunderhead requires it).
            Spawn(sim, "skai.storm_spire", P0, "16", "6");
            SetStock(sim, P0, 1000, 65);
            List<AiIntent> trains = Decide(sim, S3, memory).FindAll(i => i.Kind == AiIntentKind.Train);
            Assert.That(trains.ConvertAll(i => i.DefinitionId), Does.Contain("skai.thunderhead"));
        }

        /// <summary>R2-7. Water against 6 Pummelers (aggressive_s3): with rare 400 the Leviathan alone is kept (advantage 1.2208 against the Pummeler's 1.0000) and the Tidecaller is not in the mix; with rare 0 it is not kept (0.100).</summary>
        [Test]
        public void R2_7_WaterMixKeepsTheLeviathanOnlyWithRare()
        {
            Simulation sim = Greybox("water", "water");
            Spawn(sim, "water.grand_shell", P0, "7", "7");
            AiMemory memory = new AiMemory();
            Believe(memory, "water.grasper", 6, sim.Clock.Tick);
            SetStock(sim, P0, 0, 400);
            AssertAdvantages(sim, S3, "water", memory, ("water.leviathan", "1.2208"), ("water.grasper", "1.0000"));
            SortedDictionary<string, int> rich = MixOf(sim, S3, "water", memory);
            AssertMix(rich, ("water.grasper", 150), ("water.ink_lobber", 50), ("water.leviathan", 700), ("water.reef_runner", 0), ("water.shellback", 50), ("water.spoutling", 50));
            Assert.That(rich.ContainsKey("water.tidecaller"), Is.False);
            SetStock(sim, P0, 0, 0);
            AssertAdvantages(sim, S3, "water", memory, ("water.leviathan", "0.100"));
            AssertMix(MixOf(sim, S3, "water", memory), ("water.grasper", 850), ("water.ink_lobber", 50), ("water.leviathan", 0), ("water.reef_runner", 0), ("water.shellback", 50), ("water.spoutling", 50));
        }
    }
}
