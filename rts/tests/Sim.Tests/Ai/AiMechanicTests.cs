// rts/tests/Sim.Tests/Ai/AiMechanicTests.cs
// Tests of packages P1 (casts), P2 (support units: M2-5 to M2-7 and the support half of M2-8), P2b (SN-1 to SN-7) P3 (breach: WB-1 to WB-6 and the breach profile keys) and P4 (rule WL: WL-1 to WL-14 and V-2's wall half on prototype_1v1, the wall profile keys) of rts/docs/specs/ai_mechanic_use.md. P1: M1-1 to M1-5 (Downdraft, rule CG), M2-1 to M2-4 and the cast half of M2-8 (the wave, rule CW), V-1 and V-2 (the wave-aim draw, aim half),
// the profile switch and its validation, and OFF-2 (the old variety stream). OFF-1 (the pins) is the existing hash tests, unchanged. Part of Rts.Sim.Tests. Each case builds a real greybox_1v1 simulation
// (flat at height 1 for x 14 to 33, y 15 to 19), spawns units by script (as AiFightTests does) and runs AiBrain.Decide on the resulting view; the wave aim is set through AiVariety.SetWaveAimForTest.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Package P1 of ai_mechanic_use.md: Downdraft and wave casting.</summary>
    [TestFixture]
    public class AiMechanicTests
    {
        /// <summary>Player slot 0 (the AI under test).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (the enemy).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and orders. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Next sequence number of the AI's own commands in these tests. Unit: counter. Range: 0 upward.</summary>
        private uint _aiSequence;

        /// <summary>The candidate profile (use_casts true).</summary>
        private static AiProfile Candidate => AiProfile.FromDataSet(Data, "aggressive_s5_m");

        /// <summary>The candidate's off-copy for comparison.</summary>
        private static AiProfile OffCopy => AiProfile.FromDataSet(Data, "aggressive_s5_r");

        /// <summary>The candidate with use_casts and use_support_units written false (every other key at its candidate value); P2 turned the second switch on in the candidate, so the P1 off-comparisons switch it off too.</summary>
        private static AiProfile CandidateCastsOff()
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_m.json")).Replace("\r\n", "\n");
            Assert.That(text, Does.Contain("\"use_casts\": true"));
            Assert.That(text, Does.Contain("\"use_support_units\": true"));
            // P4: the wall-style draw also moves the later draws of the candidate, so this off-twin switches use_walls off too (the candidate tests that compare against aggressive_s5_r need the same stream).
            Assert.That(text, Does.Contain("\"use_walls\": true"));
            return AiProfile.Parse(text.Replace("\"use_casts\": true", "\"use_casts\": false").Replace("\"use_support_units\": true", "\"use_support_units\": false").Replace("\"use_walls\": true", "\"use_walls\": false"), "ai/aggressive_s5_m.json", Data.Content);
        }

        /// <summary>The candidate with its own text edited by the given old/new pairs (each old text must occur).</summary>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile CandidateEdited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_m.json")).Replace("\r\n", "\n");
            // Index of the old text of the pair being applied. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text, Does.Contain(oldNewPairs[i]));
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive_s5_m.json", Data.Content);
        }

        /// <summary>A started greybox_1v1 simulation with the given factions in slots 0 and 1.</summary>
        private static Simulation Greybox(string faction0, string faction1)
        {
            // The two players of the case.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
            };
            // The started match of this case (tick 0 upward).
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

        /// <summary>An own HQ for P0 at (7, 7), so the brain thinks (it does nothing without one).</summary>
        private void Hq(Simulation sim, string faction)
        {
            Spawn(sim, faction == "skai" ? "skai.great_aerie" : "water.grand_shell", P0, "7", "7");
        }

        /// <summary>A memory whose variety stream holds the given wave aim.</summary>
        private static AiMemory MemoryWithAim(AiProfile profile, string faction, WaveAim aim)
        {
            // The memory under test, with an attack threshold set so the first think does not draw.
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            memory.Variety = AiVariety.ForTest(profile.RequireFaction(faction), 1);
            memory.Variety.SetWaveAimForTest(aim);
            return memory;
        }

        /// <summary>Decides for P0 on a fresh view.</summary>
        private static List<AiIntent> Decide(Simulation sim, AiProfile profile, AiMemory memory, string faction)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, P0, memory.KnownFieldRemaining), profile, profile.RequireFaction(faction), memory, new DeterministicRandom(1));
        }

        /// <summary>The intents of one kind.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            return intents.FindAll(i => i.Kind == kind);
        }

        /// <summary>The intent list as text (kind, unit, target, definition), for equality checks.</summary>
        private static string Key(List<AiIntent> intents)
        {
            // The text built so far.
            List<string> parts = new List<string>();
            foreach (AiIntent i in intents)
            {
                parts.Add(i.Kind + ":" + i.Unit.Value + ":" + i.Point.X.Raw + "," + i.Point.Y.Raw + ":" + i.DefinitionId + ":" + i.Target.Value + ":" + i.TileX + "," + i.TileY);
            }
            return string.Join("|", parts);
        }

        /// <summary>Submits an intent as P0's command on the current tick and steps once.</summary>
        private void Apply(Simulation sim, AiIntent intent)
        {
            sim.Submit(intent.ToCommand(sim.Clock.Tick, P0, _aiSequence++));
            sim.Step();
        }

        /// <summary>Asserts a cast intent's unit and point.</summary>
        private static void AssertCast(AiIntent intent, EntityId unit, string x, string y)
        {
            Assert.That(intent.Unit, Is.EqualTo(unit));
            Assert.That(intent.Point.X, Is.EqualTo(Fix64.Parse(x)));
            Assert.That(intent.Point.Y, Is.EqualTo(Fix64.Parse(y)));
        }

        // ------------------------------------------------------------------ M1 Downdraft

        /// <summary>Builds M1-1's scene and returns the Stormcaller and the two Roc Riders.</summary>
        private (Simulation Sim, EntityId Caster, EntityId RocA, EntityId RocB) DowndraftScene()
        {
            Simulation sim = Greybox("skai", "skai");
            Hq(sim, "skai");
            EntityId caster = Spawn(sim, "skai.stormcaller", P0, "18.5", "17.5");
            EntityId a = Spawn(sim, "skai.roc_rider", P1, "23.5", "17.5");
            EntityId b = Spawn(sim, "skai.roc_rider", P1, "24.5", "17.5");
            return (sim, caster, a, b);
        }

        /// <summary>M1-1: the Stormcaller casts at the lower-ID Roc Rider (both score 6); both are grounded after the step; the next think is on cooldown.</summary>
        [Test]
        public void M1_1_Downdraft()
        {
            var scene = DowndraftScene();
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            List<AiIntent> intents = Decide(scene.Sim, Candidate, memory, "skai");
            List<AiIntent> casts = Of(intents, AiIntentKind.CastGrounding);
            Assert.That(casts.Count, Is.EqualTo(1));
            // The lower-ID Roc Rider's position now (separation nudges a spawned unit a little off the spawn point, 23.45 not 23.5), which is the tie-break target.
            Assert.That(scene.Sim.Units.TryGet(scene.RocA, out Unit? lower), Is.True);
            Assert.That(casts[0].Unit, Is.EqualTo(scene.Caster));
            Assert.That(casts[0].Point, Is.EqualTo(lower!.Position));
            Apply(scene.Sim, casts[0]);
            Assert.That(scene.Sim.Units.TryGet(scene.RocA, out Unit? a) && a!.EffectiveLayer == MovementLayer.Ground, Is.True);
            Assert.That(scene.Sim.Units.TryGet(scene.RocB, out Unit? b) && b!.EffectiveLayer == MovementLayer.Ground, Is.True);
            // The cast event of the step.
            GroundingCastEvent? cast = null;
            foreach (SimEvent evt in scene.Sim.Events.Current)
            {
                cast = evt as GroundingCastEvent ?? cast;
            }
            Assert.That(cast, Is.Not.Null);
            Assert.That(cast!.UnitsGrounded, Is.EqualTo(2));
            Assert.That(Of(Decide(scene.Sim, Candidate, new AiMemory { AttackThreshold = 8 }, "skai"), AiIntentKind.CastGrounding), Is.Empty, "cooldown 30 s");
        }

        /// <summary>M1-2: one flyer already grounded and the other beyond range 7 give no cast.</summary>
        [Test]
        public void M1_2_BelowTheBarAndOutOfRange()
        {
            Simulation sim = Greybox("skai", "skai");
            Hq(sim, "skai");
            Spawn(sim, "skai.stormcaller", P0, "18.5", "17.5");
            EntityId near = Spawn(sim, "skai.roc_rider", P1, "23.5", "17.5");
            Spawn(sim, "skai.roc_rider", P1, "30.5", "17.5");
            sim.Submit(new ApplyStatusCommand(sim.Clock.Tick, PlayerId.None, _sequence++, near, StatusKind.Grounded, Fix64.One, Fix64.FromInt(30), "test.ground"));
            sim.Step();
            Assert.That(Of(Decide(sim, Candidate, new AiMemory { AttackThreshold = 8 }, "skai"), AiIntentKind.CastGrounding), Is.Empty);
        }

        /// <summary>M1-3: flyers the team does not see give no cast.</summary>
        [Test]
        public void M1_3_Fog()
        {
            Simulation sim = Greybox("skai", "skai");
            Hq(sim, "skai");
            Spawn(sim, "skai.stormcaller", P0, "18.5", "17.5");
            Spawn(sim, "skai.roc_rider", P1, "29.5", "17.5");
            Spawn(sim, "skai.roc_rider", P1, "30.5", "17.5");
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.VisibleEnemies.Count, Is.EqualTo(0), "the scene hides both flyers");
            Assert.That(Of(Decide(sim, Candidate, new AiMemory { AttackThreshold = 8 }, "skai"), AiIntentKind.CastGrounding), Is.Empty);
        }

        /// <summary>M1-4: with use_casts false there is no cast and the intents equal those of aggressive_s5_r on the same view.</summary>
        [Test]
        public void M1_4_Off()
        {
            var scene = DowndraftScene();
            List<AiIntent> off = Decide(scene.Sim, CandidateCastsOff(), new AiMemory { AttackThreshold = 8 }, "skai");
            Assert.That(Of(off, AiIntentKind.CastGrounding), Is.Empty);
            Assert.That(Key(off), Is.EqualTo(Key(Decide(scene.Sim, OffCopy, new AiMemory { AttackThreshold = 8 }, "skai"))));
        }

        /// <summary>M1-5: the same scene twice from scratch gives identical intent lists.</summary>
        [Test]
        public void M1_5_Determinism()
        {
            // Intent lists of two runs.
            string first = Key(Decide(DowndraftScene().Sim, Candidate, new AiMemory { AttackThreshold = 8 }, "skai"));
            string second = Key(Decide(DowndraftScene().Sim, Candidate, new AiMemory { AttackThreshold = 8 }, "skai"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Contain("CastGrounding"));
        }

        // ------------------------------------------------------------------ M2 wave

        /// <summary>M2-1: the strip geometry of flooding.md section 5 (map 96 wide, so index = y * 96 + x).</summary>
        [Test]
        public void M2_1_Strip()
        {
            // Expected east strip: x 19 to 24, y 16 to 18.
            SortedSet<int> east = new SortedSet<int>();
            for (int x = 19; x <= 24; x++)
            {
                for (int y = 16; y <= 18; y++)
                {
                    east.Add(y * 96 + x);
                }
            }
            Assert.That(AiAbilities.WaveTiles(18, 17, 1, 0, 6, 3, 96, 96), Is.EqualTo(east));
            Assert.That(east.Count, Is.EqualTo(18));
            // Expected diagonal strip: (18 + s - o, 17 + s + o).
            SortedSet<int> diagonal = new SortedSet<int>();
            for (int s = 1; s <= 6; s++)
            {
                for (int o = -1; o <= 1; o++)
                {
                    diagonal.Add((17 + s + o) * 96 + 18 + s - o);
                }
            }
            Assert.That(AiAbilities.WaveTiles(18, 17, 1, 1, 6, 3, 96, 96), Is.EqualTo(diagonal));
            Assert.That(diagonal.Count, Is.EqualTo(18));
        }

        /// <summary>M2-2's scene: a Tidecaller for P0 and three Pummelers and a Brinecaster for P1; returns the caster and the Pummelers.</summary>
        private (Simulation Sim, EntityId Caster, List<EntityId> Pummelers) WaveScene(bool onlyFirstPummeler = false)
        {
            Simulation sim = Greybox("water", "water");
            Hq(sim, "water");
            EntityId caster = Spawn(sim, "water.tidecaller", P0, "18.5", "17.5");
            List<EntityId> pummelers = new List<EntityId> { Spawn(sim, "water.grasper", P1, "21.5", "17.5") };
            if (!onlyFirstPummeler)
            {
                pummelers.Add(Spawn(sim, "water.grasper", P1, "22.5", "17.5"));
                pummelers.Add(Spawn(sim, "water.grasper", P1, "23.5", "18.5"));
            }
            Spawn(sim, "water.ink_lobber", P1, "18.5", "19.5");
            return (sim, caster, pummelers);
        }

        /// <summary>M2-2: melee aim, one wave east (score 6); the step reports the cast and pushes all three Pummelers; one Pummeler alone (score 2) gives no cast.</summary>
        [Test]
        public void M2_2_WaveAgainstAMeleeBall()
        {
            var scene = WaveScene();
            AiMemory memory = MemoryWithAim(Candidate, "water", WaveAim.Melee);
            List<AiIntent> casts = Of(Decide(scene.Sim, Candidate, memory, "water"), AiIntentKind.CastWave);
            Assert.That(casts.Count, Is.EqualTo(1));
            AssertCast(casts[0], scene.Caster, "22.5", "17.5");
            // Tick the command is scheduled on (the cooldown starts there). Unit: ticks.
            int castTick = scene.Sim.Clock.Tick;
            Apply(scene.Sim, casts[0]);
            // Direction of the cast and the units pushed, from the step's events.
            int dirX = -9;
            int dirY = -9;
            List<EntityId> pushed = new List<EntityId>();
            foreach (SimEvent evt in scene.Sim.Events.Current)
            {
                if (evt is WaveCastEvent wave)
                {
                    dirX = wave.DirX;
                    dirY = wave.DirY;
                }
                if (evt is UnitPushedEvent push)
                {
                    pushed.Add(push.Unit);
                }
            }
            Assert.That(dirX, Is.EqualTo(1));
            Assert.That(dirY, Is.EqualTo(0));
            foreach (EntityId pummeler in scene.Pummelers)
            {
                Assert.That(pushed, Does.Contain(pummeler));
            }
            // M2-4: the next think is on cooldown and the view reports caster tick + 600.
            AiWorldView after = AiWorldView.Build(scene.Sim, P0);
            Assert.That(Of(Decide(scene.Sim, Candidate, memory, "water"), AiIntentKind.CastWave), Is.Empty);
            foreach (AiUnitView own in after.OwnUnits)
            {
                if (own.Id == scene.Caster)
                {
                    Assert.That(own.WaveReadyTick, Is.EqualTo(castTick + 600));
                }
            }
            // One Pummeler alone: score 2 is below 4.
            var single = WaveScene(true);
            Assert.That(Of(Decide(single.Sim, Candidate, MemoryWithAim(Candidate, "water", WaveAim.Melee), "water"), AiIntentKind.CastWave), Is.Empty);
        }

        /// <summary>M2-3: ranged enemies north of the caster are ignored by the melee aim and pushed by the all aim (direction (0, 1)).</summary>
        [Test]
        public void M2_3_AimAll()
        {
            Simulation sim = Greybox("water", "water");
            Hq(sim, "water");
            EntityId caster = Spawn(sim, "water.tidecaller", P0, "18.5", "17.5");
            Spawn(sim, "water.ink_lobber", P1, "18.5", "19.5");
            Spawn(sim, "water.ink_lobber", P1, "18.5", "20.5");
            Spawn(sim, "water.ink_lobber", P1, "17.5", "21.5");
            Assert.That(Of(Decide(sim, Candidate, MemoryWithAim(Candidate, "water", WaveAim.Melee), "water"), AiIntentKind.CastWave), Is.Empty);
            List<AiIntent> casts = Of(Decide(sim, Candidate, MemoryWithAim(Candidate, "water", WaveAim.All), "water"), AiIntentKind.CastWave);
            Assert.That(casts.Count, Is.EqualTo(1));
            AssertCast(casts[0], caster, "18.5", "21.5");
        }

        /// <summary>M2-8 (cast half): with use_casts false the scene of M2-2 gives the intents of aggressive_s5_r; M2-2 twice gives identical intents.</summary>
        [Test]
        public void M2_8_OffAndDeterminism()
        {
            // Intents of the candidate with casts off and of the off-copy, on one view.
            var scene = WaveScene();
            List<AiIntent> off = Decide(scene.Sim, CandidateCastsOff(), MemoryWithAim(Candidate, "water", WaveAim.Melee), "water");
            Assert.That(Of(off, AiIntentKind.CastWave), Is.Empty);
            Assert.That(Key(off), Is.EqualTo(Key(Decide(scene.Sim, OffCopy, new AiMemory { AttackThreshold = 8 }, "water"))));
            string first = Key(Decide(WaveScene().Sim, Candidate, MemoryWithAim(Candidate, "water", WaveAim.Melee), "water"));
            string second = Key(Decide(WaveScene().Sim, Candidate, MemoryWithAim(Candidate, "water", WaveAim.Melee), "water"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Contain("CastWave"));
        }

        // ------------------------------------------------------------------ switch, validation, variety

        /// <summary>The switch is false in every shipped profile but the candidate, which also has the candidate aim weights; the old values are the spec's.</summary>
        [Test]
        public void Profiles_SwitchAndValues()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseCasts, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd"), id);
                Assert.That(profile.WaveMinEnemySupply, Is.EqualTo(Fix64.FromInt(4)), id);
                Assert.That(profile.WaveMeleeRangeTiles, Is.EqualTo(Fix64.One), id);
                Assert.That(profile.WaveAimMeleeWeight, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd" ? 50 : 100), id);
                Assert.That(profile.WaveAimAllWeight, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd" ? 50 : 0), id);
                Assert.That(profile.WaveMaxCommandsPerThink, Is.EqualTo(4), id);
                Assert.That(profile.GroundingMinAirSupply, Is.EqualTo(3), id);
                Assert.That(profile.GroundingMaxCommandsPerThink, Is.EqualTo(4), id);
            }
        }

        /// <summary>Validation: all-zero aim weights, an unknown aim key and a missing key are errors that name the file and key.</summary>
        [Test]
        public void Profiles_Validation()
        {
            // The candidate's file text.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_m.json")).Replace("\r\n", "\n");
            DataValidationException zero = Expect.Throws<DataValidationException>(() =>
                AiProfile.Parse(text.Replace("{ \"melee\": 50, \"all\": 50 }", "{ \"melee\": 0, \"all\": 0 }"), "ai/aggressive_s5_m.json", Data.Content));
            Assert.That(zero.Message, Does.Contain("aggressive_s5_m.json").And.Contain("wave_aim_weights"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() =>
                AiProfile.Parse(text.Replace("{ \"melee\": 50, \"all\": 50 }", "{ \"melee\": 50, \"all\": 50, \"some\": 1 }"), "ai/aggressive_s5_m.json", Data.Content));
            Assert.That(unknown.Message, Does.Contain("some"));
            DataValidationException missing = Expect.Throws<DataValidationException>(() =>
                AiProfile.Parse(text.Replace("\"grounding_min_air_supply\": 3,", string.Empty), "ai/aggressive_s5_m.json", Data.Content));
            Assert.That(missing.Message, Does.Contain("grounding_min_air_supply"));
        }

        /// <summary>V-1: the same seed twice gives equal aim, opening and offsets for the candidate.</summary>
        [Test]
        public void V1_DrawReproducibility()
        {
            AiProfile profile = Candidate;
            AiFactionPlan plan = profile.RequireFaction("water");
            ulong seed = AiVariety.SeedFor(777, P0);
            AiVariety a = AiVariety.Create(profile, plan, seed, 20);
            AiVariety b = AiVariety.Create(profile, plan, seed, 20);
            Assert.That(a.WaveAim, Is.EqualTo(b.WaveAim));
            Assert.That(a.OpeningId, Is.EqualTo(b.OpeningId));
            Assert.That(a.ExpandOffsetSeconds, Is.EqualTo(b.ExpandOffsetSeconds));
            for (int i = 0; i < 5; i++)
            {
                a.DrawAttackAdvantageOffset(profile);
                b.DrawAttackAdvantageOffset(profile);
                Assert.That(a.AttackAdvantageOffset, Is.EqualTo(b.AttackAdvantageOffset));
            }
        }

        /// <summary>V-2 (aim half): over match seeds 1 to 1000 (player 0, water plan) the aim counts are within 50 of 500 each. The counts are deterministic: melee 498, all 502 (pinned below).</summary>
        [Test]
        public void V2_Distribution()
        {
            // P4: the candidate's use_walls adds a draw before the aim draw; this test keeps the pinned P1 counts by switching use_walls off (V2_WallDistribution covers the full candidate).
            AiProfile profile = CandidateEdited("\"use_walls\": true", "\"use_walls\": false");
            AiFactionPlan plan = profile.RequireFaction("water");
            // Seeds drawn melee and all. Unit: seeds. Range: 0 to 1000.
            int melee = 0;
            int all = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                if (AiVariety.Create(profile, plan, AiVariety.SeedFor(seed, P0), 20).WaveAim == WaveAim.Melee)
                {
                    melee++;
                }
                else
                {
                    all++;
                }
            }
            Assert.That(Math.Abs(melee - 500), Is.LessThanOrEqualTo(50));
            Assert.That(Math.Abs(all - 500), Is.LessThanOrEqualTo(50));
            // Re-pinned 25 Nov 2026 for water.reef_runner weight 30 in aggressive_s5_m (the water plan's weights changed the draws): old melee 498 and all 502.
            Assert.That(melee, Is.EqualTo(499));
            Assert.That(all, Is.EqualTo(501));
        }

        /// <summary>V-2 (wall half): over match seeds 1 to 1000 (player 0, water plan, the full candidate) the wall style counts are within 50 of none 200, funnel 300 and sealed 500. The counts are deterministic and pinned below (none 190, funnel 308, sealed 502); the wave aim counts of the full candidate (the wall draw comes first, so they differ from V2_Distribution's) are within 50 of 500 too.</summary>
        [Test]
        public void V2_WallDistribution()
        {
            AiProfile profile = Candidate;
            AiFactionPlan plan = profile.RequireFaction("water");
            // Seeds drawn per style, and melee-aim seeds. Unit: seeds. Range: 0 to 1000.
            int none = 0;
            int funnel = 0;
            int sealed_ = 0;
            int melee = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                AiVariety variety = AiVariety.Create(profile, plan, AiVariety.SeedFor(seed, P0), 20);
                none += variety.WallStyle == WallStyle.None ? 1 : 0;
                funnel += variety.WallStyle == WallStyle.Funnel ? 1 : 0;
                sealed_ += variety.WallStyle == WallStyle.Sealed ? 1 : 0;
                melee += variety.WaveAim == WaveAim.Melee ? 1 : 0;
            }
            TestContext.WriteLine("wall style counts none " + none + " funnel " + funnel + " sealed " + sealed_ + "; melee aim " + melee);
            Assert.That(Math.Abs(none - 200), Is.LessThanOrEqualTo(50));
            Assert.That(Math.Abs(funnel - 300), Is.LessThanOrEqualTo(50));
            Assert.That(Math.Abs(sealed_ - 500), Is.LessThanOrEqualTo(50));
            Assert.That(Math.Abs(melee - 500), Is.LessThanOrEqualTo(50));
            // Re-pinned 25 Nov 2026 for water.reef_runner weight 30 in aggressive_s5_m: old none 190, funnel 308, sealed 502, melee 484.
            Assert.That(none, Is.EqualTo(201));
            Assert.That(funnel, Is.EqualTo(298));
            Assert.That(sealed_, Is.EqualTo(501));
            Assert.That(melee, Is.EqualTo(482));
            // The old profile makes no wall draw.
            AiProfile off = OffCopy;
            Assert.That(AiVariety.Create(off, off.RequireFaction("water"), AiVariety.SeedFor(1, P0), 20).WallStyle, Is.EqualTo(WallStyle.None));
        }

        /// <summary>OFF-2: an old profile's variety stream is unchanged (aim stays Melee and no draw is spent); the candidate with casts off draws the same opening and offsets as aggressive_s5_r.</summary>
        [Test]
        public void Off2_OldStreams()
        {
            AiProfile off = OffCopy;
            AiProfile twin = CandidateCastsOff();
            Assert.That(AiVariety.IsNeeded(off, off.RequireFaction("water")), Is.True);
            for (ulong seed = 1; seed <= 50; seed++)
            {
                AiVariety a = AiVariety.Create(off, off.RequireFaction("skai"), AiVariety.SeedFor(seed, P0), 20);
                AiVariety b = AiVariety.Create(twin, twin.RequireFaction("skai"), AiVariety.SeedFor(seed, P0), 20);
                Assert.That(a.WaveAim, Is.EqualTo(WaveAim.Melee));
                Assert.That(a.OpeningId, Is.EqualTo(b.OpeningId));
                Assert.That(a.ExpandOffsetSeconds, Is.EqualTo(b.ExpandOffsetSeconds));
                for (int i = 0; i < 3; i++)
                {
                    a.DrawAttackAdvantageOffset(off);
                    b.DrawAttackAdvantageOffset(twin);
                    Assert.That(a.AttackAdvantageOffset, Is.EqualTo(b.AttackAdvantageOffset));
                }
            }
        }

        // ------------------------------------------------------------------ P2 support units

        /// <summary>A started greybox_1v1 match of one faction on both sides, explored, with P0's HQ and 5000 common and 400 rare; returns the simulation.</summary>
        /// <param name="faction">Faction of both players.</param>
        private Simulation SupportBase(string faction)
        {
            Simulation sim = Greybox(faction, faction);
            ExploreKit.ExploreAll(sim);
            Hq(sim, faction);
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(5000, 400);
            return sim;
        }

        /// <summary>Spawns <paramref name="count"/> units of a definition for P0 on a grid in the flat area (x 15 to 23, y 15 and up).</summary>
        /// <param name="sim">The match.</param>
        /// <param name="definition">Unit definition ID.</param>
        /// <param name="count">How many. Unit: units. Range: 0 to 27.</param>
        private void SpawnArmy(Simulation sim, string definition, int count)
        {
            // Unit number. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                Spawn(sim, definition, P0, (15 + i % 9).ToString(), (15 + i / 9).ToString());
            }
        }

        /// <summary>The water scene of M2-5: two idle Tidepools and an army of <paramref name="graspers"/> Graspers (2 supply each) plus <paramref name="tidecallers"/> Tidecallers; returns the lower Tidepool's ID.</summary>
        private EntityId SupportWaterScene(out Simulation sim, int graspers, int tidecallers = 0, int runners = 0)
        {
            sim = SupportBase("water");
            EntityId pool = Spawn(sim, "water.tidepool", P0, "13.5", "10.5");
            Spawn(sim, "water.tidepool", P0, "16.5", "10.5");
            SpawnArmy(sim, "water.grasper", graspers);
            for (int i = 0; i < tidecallers; i++)
            {
                Spawn(sim, "water.tidecaller", P0, (15 + i).ToString(), "19");
            }
            for (int i = 0; i < runners; i++)
            {
                Spawn(sim, "water.reef_runner", P0, (15 + i).ToString(), "18");
            }
            return pool;
        }

        /// <summary>The Train intents as "producer:definition", in order.</summary>
        private static List<string> Trains(List<AiIntent> intents)
        {
            return Of(intents, AiIntentKind.Train).ConvertAll(i => i.Unit.Value + ":" + i.DefinitionId);
        }

        /// <summary>M2-5: 12 army supply gives one Tidecaller before any composition unit; 11 gives none; 3 Tidecallers and 60 supply give none (max 3); one already queued counts as had.</summary>
        [Test]
        public void M2_5_SupportTraining()
        {
            AiProfile profile = Candidate;
            AiFactionPlan plan = profile.RequireFaction("water");
            EntityId pool = SupportWaterScene(out Simulation twelve, 6);
            // The Train intents of the 12-supply think.
            List<string> trains = Trains(Decide(twelve, profile, new AiMemory { AttackThreshold = 8 }, "water"));
            // Position of the Tidecaller purchase in the list.
            int at = trains.IndexOf(pool.Value + ":water.tidecaller");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "a Tidecaller is trained at the lowest-ID Tidepool");
            for (int i = 0; i < at; i++)
            {
                Assert.That(plan.Composition.ContainsKey(trains[i].Substring(trains[i].IndexOf(':') + 1)), Is.False, "no composition unit before the Tidecaller");
            }
            SupportWaterScene(out Simulation eleven, 5, 0, 1);
            Assert.That(Trains(Decide(eleven, profile, new AiMemory { AttackThreshold = 8 }, "water")).Exists(t => t.EndsWith(":water.tidecaller")), Is.False, "11 supply");
            SupportWaterScene(out Simulation full, 27, 3);
            Assert.That(Trains(Decide(full, profile, new AiMemory { AttackThreshold = 8 }, "water")).Exists(t => t.EndsWith(":water.tidecaller")), Is.False, "3 had, 60 supply");
            EntityId queuedPool = SupportWaterScene(out Simulation queued, 6);
            queued.Submit(new TrainUnitCommand(queued.Clock.Tick, P0, _sequence++, queuedPool, "water.tidecaller"));
            queued.Step();
            Assert.That(Trains(Decide(queued, profile, new AiMemory { AttackThreshold = 8 }, "water")).Exists(t => t.EndsWith(":water.tidecaller")), Is.False, "a queued Tidecaller counts as had");
        }

        /// <summary>The Skai scene of M2-6: 10 Talon Archers (20 supply), a Windspire and a Storm Spire, and a profile with the Stormcaller at weight 0 in the composition.</summary>
        private Simulation SupportSkaiScene(out EntityId windspire, out EntityId spire)
        {
            Simulation sim = SupportBase("skai");
            windspire = Spawn(sim, "skai.windspire", P0, "13.5", "10.5");
            spire = Spawn(sim, "skai.storm_spire", P0, "16.5", "6.5");
            // Three Roosts (8 supply each) so the Stormcaller's 3 supply fits under the cap with the 20-supply army.
            Spawn(sim, "skai.roost", P0, "10.5", "13.5");
            Spawn(sim, "skai.roost", P0, "12.5", "13.5");
            Spawn(sim, "skai.roost", P0, "14.5", "13.5");
            SpawnArmy(sim, "skai.talon_archer", 10);
            return sim;
        }

        /// <summary>M2-6: 20 army supply, no Stormcaller Plans: the plans are bought at the Storm Spire; with the plans researched the Stormcaller is trained at the Windspire.</summary>
        [Test]
        public void M2_6_SupportResearch()
        {
            AiProfile profile = CandidateEdited("\"skai.stormcaller\": 10 }", "\"skai.stormcaller\": 0 }", NeedsAir, "\"support_needs\": {},");
            Simulation sim = SupportSkaiScene(out EntityId windspire, out EntityId spire);
            Assert.That(Trains(Decide(sim, profile, new AiMemory { AttackThreshold = 8 }, "skai")), Does.Contain(spire.Value + ":skai.stormcaller_plans"));
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.AddResearch("skai.stormcaller_plans");
            Assert.That(Trains(Decide(sim, profile, new AiMemory { AttackThreshold = 8 }, "skai")), Does.Contain(windspire.Value + ":skai.stormcaller"));
        }

        /// <summary>M2-7: in stance Attack a Tidecaller is moved to the trail point (the armed group's mean, 3 tiles toward home) and gets no attack-move; with the switch off it gets no Move.</summary>
        [Test]
        public void M2_7_Trail()
        {
            Simulation sim = SupportBase("water");
            // The three armed group members, and the Tidecaller at (18.5, 17.5).
            List<EntityId> group = new List<EntityId>
            {
                Spawn(sim, "water.grasper", P0, "29.5", "17.5"),
                Spawn(sim, "water.grasper", P0, "30.5", "17.5"),
                Spawn(sim, "water.grasper", P0, "31.5", "17.5"),
            };
            EntityId tidecaller = Spawn(sim, "water.tidecaller", P0, "18.5", "17.5");
            // Expected trail point: the group's mean position moved 3 tiles toward the HQ.
            Fix64 sumX = Fix64.Zero;
            Fix64 sumY = Fix64.Zero;
            FixVec2 home = FixVec2.Zero;
            foreach (Unit unit in sim.Units.All)
            {
                if (group.Contains(unit.Id))
                {
                    sumX += unit.Position.X;
                    sumY += unit.Position.Y;
                }
                if (unit.Owner == P0 && unit.Definition.Hq != null)
                {
                    home = unit.Position;
                }
            }
            FixVec2 expected = new FixVec2(sumX / 3, sumY / 3).MoveTowards(home, Fix64.FromInt(3));
            // A memory in stance Attack with that group, already staged.
            AiMemory Attacking()
            {
                AiMemory memory = new AiMemory { AttackThreshold = 8, Stance = AiStance.Attack, AttackStaged = true };
                foreach (EntityId id in group)
                {
                    memory.AttackGroup.Add(id);
                }
                return memory;
            }
            List<AiIntent> on = Decide(sim, Candidate, Attacking(), "water");
            List<AiIntent> moves = on.FindAll(i => i.Kind == AiIntentKind.Move && i.Unit == tidecaller);
            Assert.That(moves.Count, Is.EqualTo(1));
            Assert.That(moves[0].Point, Is.EqualTo(expected));
            Assert.That(on.Exists(i => i.Kind == AiIntentKind.AttackMove && i.Unit == tidecaller), Is.False);
            List<AiIntent> off = Decide(sim, CandidateCastsOff(), Attacking(), "water");
            Assert.That(off.Exists(i => i.Kind == AiIntentKind.Move && i.Unit == tidecaller), Is.False);
            Assert.That(off.Exists(i => i.Kind == AiIntentKind.AttackMove && i.Unit == tidecaller), Is.True, "today's stance attack-move");
            // Not attacking: it is sent to the rally point, and a unit already there is left alone.
            Assert.That(Decide(sim, Candidate, new AiMemory { AttackThreshold = 8 }, "water").Exists(i => i.Kind == AiIntentKind.Move && i.Unit == tidecaller), Is.True);
        }

        /// <summary>M2-8 (support half): with both switches false M2-5's scene gives the intents of aggressive_s5_r; the same scene twice gives identical intents.</summary>
        [Test]
        public void M2_8_SupportOffAndDeterminism()
        {
            SupportWaterScene(out Simulation sim, 6);
            Assert.That(Key(Decide(sim, CandidateCastsOff(), new AiMemory { AttackThreshold = 8 }, "water")), Is.EqualTo(Key(Decide(sim, OffCopy, new AiMemory { AttackThreshold = 8 }, "water"))));
            SupportWaterScene(out Simulation a, 6);
            SupportWaterScene(out Simulation b, 6);
            Assert.That(Key(Decide(a, Candidate, new AiMemory { AttackThreshold = 8 }, "water")), Is.EqualTo(Key(Decide(b, Candidate, new AiMemory { AttackThreshold = 8 }, "water"))));
        }

        /// <summary>The support keys: false in every shipped profile but the candidate; empty support_units except in the candidate's plans; bad support_units are errors naming the file and key.</summary>
        [Test]
        public void Profiles_SupportSwitchAndValidation()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseSupportUnits, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd"), id);
                Assert.That(profile.SupportMaxPerType, Is.EqualTo(3), id);
                Assert.That(profile.SupportTrailTiles, Is.EqualTo(Fix64.FromInt(3)), id);
                Assert.That(profile.SupportRegroupTiles, Is.EqualTo(Fix64.FromInt(3)), id);
                foreach (AiFactionPlan plan in profile.Factions.Values)
                {
                    Assert.That(plan.SupportUnits.Count, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd" ? 1 : 0), id);
                }
            }
            AiProfile candidate = Candidate;
            Assert.That(candidate.RequireFaction("water").SupportUnits["water.tidecaller"], Is.EqualTo(12));
            Assert.That(candidate.RequireFaction("skai").SupportUnits["skai.stormcaller"], Is.EqualTo(20));
            DataValidationException other = Expect.Throws<DataValidationException>(() => CandidateEdited("{ \"water.tidecaller\": 12 }", "{ \"skai.stormcaller\": 12 }"));
            Assert.That(other.Message, Does.Contain("aggressive_s5_m.json").And.Contain("skai.stormcaller"));
            DataValidationException range = Expect.Throws<DataValidationException>(() => CandidateEdited("{ \"water.tidecaller\": 12 }", "{ \"water.tidecaller\": 0 }"));
            Assert.That(range.Message, Does.Contain("support_units"));
            DataValidationException structure = Expect.Throws<DataValidationException>(() => CandidateEdited("{ \"water.tidecaller\": 12 }", "{ \"water.tidepool\": 12 }"));
            Assert.That(structure.Message, Does.Contain("water.tidepool"));
        }

        /// <summary>The candidate's skai support_needs entry as written in the file, for the edits of M2-6 and SN-6.</summary>
        private const string NeedsAir = "\"support_needs\": { \"skai.stormcaller\": [\"air\"] },";

        /// <summary>The candidate with the Stormcaller at weight 0 in the composition (support_needs as shipped), as M2-6 uses it.</summary>
        private static AiProfile StormcallerSupportOnly()
        {
            return CandidateEdited("\"skai.stormcaller\": 10 }", "\"skai.stormcaller\": 0 }");
        }

        /// <summary>The Stormcaller's Train intents as "producer:definition" from one think of the Skai scene.</summary>
        /// <param name="sim">The scene.</param>
        /// <param name="memory">The memory carried between thinks.</param>
        private static List<string> StormcallerTrains(Simulation sim, AiMemory memory)
        {
            return Trains(Decide(sim, StormcallerSupportOnly(), memory, "skai")).FindAll(t => t.EndsWith(":skai.stormcaller") || t.EndsWith(":skai.stormcaller_plans"));
        }

        /// <summary>Marks the Stormcaller plans researched for P0.</summary>
        /// <param name="sim">The scene.</param>
        private static void ResearchPlans(Simulation sim)
        {
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.AddResearch("skai.stormcaller_plans");
        }

        /// <summary>SN-1 and SN-3: no sighting, or only a ground enemy: no Stormcaller Train.</summary>
        [Test]
        public void SN_1_3_NoAirSeen()
        {
            Simulation none = SupportSkaiScene(out _, out _);
            ResearchPlans(none);
            Assert.That(StormcallerTrains(none, new AiMemory { AttackThreshold = 8 }), Is.Empty, "SN-1");
            Simulation ground = SupportSkaiScene(out _, out _);
            ResearchPlans(ground);
            Spawn(ground, "skai.talon_archer", P1, "23.5", "17.5");
            Assert.That(StormcallerTrains(ground, new AiMemory { AttackThreshold = 8 }), Is.Empty, "SN-3");
        }

        /// <summary>SN-2 and SN-7 (determinism): a flyer seen unlocks the Stormcaller, and stays unlocked after it is gone; two runs give identical intents.</summary>
        [Test]
        public void SN_2_AirSeenLatches()
        {
            // Think intents of two fresh runs, as text.
            List<string> keys = new List<string>();
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = SupportSkaiScene(out EntityId windspire, out _);
                ResearchPlans(sim);
                EntityId roc = Spawn(sim, "skai.roc_rider", P1, "23.5", "17.5");
                AiMemory memory = new AiMemory { AttackThreshold = 8 };
                Assert.That(StormcallerTrains(sim, memory), Does.Contain(windspire.Value + ":skai.stormcaller"));
                Assert.That(memory.EnemyTypes.ContainsKey("skai.roc_rider"), Is.True);
                sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, roc));
                sim.Step();
                List<string> again = StormcallerTrains(sim, memory);
                Assert.That(memory.EnemyTypes.ContainsKey("skai.roc_rider"), Is.True, "kept after removal");
                Assert.That(again, Does.Contain(windspire.Value + ":skai.stormcaller"), "latch");
                keys.Add(string.Join(",", again));
            }
            Assert.That(keys[0], Is.EqualTo(keys[1]));
        }

        /// <summary>SN-4: the plans research waits for the need.</summary>
        [Test]
        public void SN_4_ResearchWaitsForNeed()
        {
            Simulation none = SupportSkaiScene(out _, out EntityId spire);
            Assert.That(StormcallerTrains(none, new AiMemory { AttackThreshold = 8 }), Is.Empty);
            Simulation seen = SupportSkaiScene(out _, out EntityId seenSpire);
            Spawn(seen, "skai.roc_rider", P1, "23.5", "17.5");
            Assert.That(StormcallerTrains(seen, new AiMemory { AttackThreshold = 8 }), Does.Contain(seenSpire.Value + ":skai.stormcaller_plans"));
            Assert.That(spire.Value, Is.Not.EqualTo(0u));
        }

        /// <summary>SN-5: an unlisted unit (the water Tidecaller) is trained with no enemy seen.</summary>
        [Test]
        public void SN_5_UnlistedUnitUnchanged()
        {
            Assert.That(Candidate.RequireFaction("water").SupportNeeds, Is.Empty);
            SupportWaterScene(out Simulation sim, 6);
            Assert.That(Trains(Decide(sim, Candidate, new AiMemory { AttackThreshold = 8 }, "water")).Exists(t => t.EndsWith(":water.tidecaller")), Is.True);
        }

        /// <summary>SN-6: bad support_needs are errors naming the file and key; every other shipped profile has none.</summary>
        [Test]
        public void SN_6_Validation()
        {
            DataValidationException key = Expect.Throws<DataValidationException>(() => CandidateEdited(NeedsAir, "\"support_needs\": { \"skai.windrunner\": [] },"));
            Assert.That(key.Message, Does.Contain("aggressive_s5_m.json").And.Contain("support_needs"));
            DataValidationException name = Expect.Throws<DataValidationException>(() => CandidateEdited("[\"air\"]", "[\"flying\"]"));
            Assert.That(name.Message, Does.Contain("aggressive_s5_m.json").And.Contain("flying"));
            DataValidationException twice = Expect.Throws<DataValidationException>(() => CandidateEdited("[\"air\"]", "[\"air\", \"air\"]"));
            Assert.That(twice.Message, Does.Contain("aggressive_s5_m.json").And.Contain("twice"));
            DataValidationException model = Expect.Throws<DataValidationException>(() => CandidateEdited("\"use_enemy_model\": true", "\"use_enemy_model\": false"));
            Assert.That(model.Message, Does.Contain("aggressive_s5_m.json").And.Contain("support_needs"));
            foreach (string id in Data.AiProfiles.Keys)
            {
                foreach (AiFactionPlan plan in AiProfile.FromDataSet(Data, id).Factions.Values)
                {
                    Assert.That(plan.SupportNeeds.Count, Is.EqualTo((id == "aggressive_s5_m" || id == "aggressive_s5_pd") && plan.FactionId == "skai" ? 1 : 0), id);
                }
            }
        }

        // ------------------------------------------------------------------ P3 Breach (rule WB)

        /// <summary>The unreachable goal of the breach scenes. Unit: tiles.</summary>
        private static readonly FixVec2 BreachGoal = new FixVec2(Fix64.Parse("40.5"), Fix64.Parse("17.5"));

        /// <summary>The breach scene: a Skai AI with a Talon Marksman at (18.5, 17.5) and, when <paramref name="wallAt"/> is given, a Water wall at that tile centre; returns the simulation and the Talon.</summary>
        /// <param name="wallAt">Tile x of the wall on row 17, or -1 for none.</param>
        /// <param name="unitId">Definition of the P0 unit at (18.5, 17.5).</param>
        private (Simulation Sim, EntityId Unit) BreachScene(int wallAt, string unitId = "skai.talon_archer")
        {
            Simulation sim = Greybox("skai", "water");
            Hq(sim, "skai");
            EntityId unit = Spawn(sim, unitId, P0, "18.5", "17.5");
            if (wallAt >= 0)
            {
                Spawn(sim, "water.coral_wall", P1, wallAt + ".5", "17.5");
            }
            return (sim, unit);
        }

        /// <summary>A memory holding an unreachable-goal record of <paramref name="unit"/> made <paramref name="ageTicks"/> ticks before the current tick.</summary>
        private static AiMemory BreachMemory(Simulation sim, EntityId unit, int ageTicks)
        {
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            memory.UnreachableGoals[unit] = (sim.Clock.Tick - ageTicks, BreachGoal);
            return memory;
        }

        /// <summary>The intents that order <paramref name="unit"/>.</summary>
        private static List<AiIntent> OrdersOf(List<AiIntent> intents, EntityId unit)
        {
            return intents.FindAll(i => i.Unit == unit && i.Kind != AiIntentKind.Train);
        }

        /// <summary>WB-1: a visible wall is attacked by name, and the unit gets no stance order.</summary>
        [Test]
        public void WB_1_VisibleWall()
        {
            (Simulation sim, EntityId talon) = BreachScene(22);
            EntityId wall = EntityId.None;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Definition.Id == "water.coral_wall")
                {
                    wall = unit.Id;
                }
            }
            List<AiIntent> orders = OrdersOf(Decide(sim, Candidate, BreachMemory(sim, talon, 20), "skai"), talon);
            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(AiIntentKind.AttackUnit));
            Assert.That(orders[0].Target, Is.EqualTo(wall));
        }

        /// <summary>WB-2: a remembered wall that is not visible is approached with an attack-move to the standoff point.</summary>
        [Test]
        public void WB_2_RememberedWall()
        {
            (Simulation sim, EntityId talon) = BreachScene(-1);
            AiMemory memory = BreachMemory(sim, talon, 20);
            // A wall seen earlier, out of sight now (a made-up ID that no unit has).
            memory.KnownEnemyStructures.Add(new EntityId(9999), new KnownStructure { Position = new FixVec2(Fix64.Parse("30.5"), Fix64.Parse("17.5")), DefinitionId = "water.coral_wall" });
            List<AiIntent> orders = OrdersOf(Decide(sim, Candidate, memory, "skai"), talon);
            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(AiIntentKind.AttackMove));
            AssertCast(orders[0], talon, "28.5", "17.5");
        }

        /// <summary>WB-3: with no wall known the unit walks 6 tiles toward its goal.</summary>
        [Test]
        public void WB_3_NoKnownWall()
        {
            (Simulation sim, EntityId talon) = BreachScene(-1);
            List<AiIntent> orders = OrdersOf(Decide(sim, Candidate, BreachMemory(sim, talon, 20), "skai"), talon);
            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(AiIntentKind.AttackMove));
            AssertCast(orders[0], talon, "24.5", "17.5");
        }

        /// <summary>WB-4: a record older than wall_breach_memory_seconds (10 s = 200 ticks) is dropped and gives no breach order; with the switch off a fresh record gives none either (the unit gets the intents of aggressive_s5_r).</summary>
        [Test]
        public void WB_4_OldEntryAndOff()
        {
            (Simulation sim, EntityId talon) = BreachScene(-1);
            AiMemory old = BreachMemory(sim, talon, 220);
            Decide(sim, Candidate, old, "skai");
            Assert.That(old.UnreachableGoals.Count, Is.EqualTo(0));
            AiMemory fresh = BreachMemory(sim, talon, 20);
            Decide(sim, Candidate, fresh, "skai");
            Assert.That(fresh.UnreachableGoals.Count, Is.EqualTo(1), "a 1 s old record is kept");
            Assert.That(Key(Decide(sim, OffCopy, BreachMemory(sim, talon, 20), "skai")), Is.EqualTo(Key(Decide(sim, OffCopy, new AiMemory { AttackThreshold = 8 }, "skai"))));
            // The candidate with the two other switches' values but wall breach off: same intents as the candidate with no record.
            AiProfile breachOff = CandidateEdited("\"use_wall_breach\": true", "\"use_wall_breach\": false");
            Assert.That(Key(Decide(sim, breachOff, BreachMemory(sim, talon, 20), "skai")), Is.EqualTo(Key(Decide(sim, breachOff, new AiMemory { AttackThreshold = 8 }, "skai"))));
        }

        /// <summary>WB-5: a flyer (no weapon that hits ground) with a record gets no breach order.</summary>
        [Test]
        public void WB_5_Flyer()
        {
            (Simulation sim, EntityId roc) = BreachScene(-1, "skai.roc_rider");
            AiMemory memory = BreachMemory(sim, roc, 20);
            // The Roc Rider's weapon hits ground, so the layer test is what excludes it: no probe step toward the goal.
            List<AiIntent> orders = OrdersOf(Decide(sim, Candidate, memory, "skai"), roc);
            Assert.That(orders.Exists(i => i.Kind == AiIntentKind.AttackMove && i.Point.X == Fix64.Parse("24.5") && i.Point.Y == Fix64.Parse("17.5")), Is.False);
        }

        /// <summary>WB-6: a unit ringed by eight enemy walls and ordered out is recorded by the controller with its goal; with the switch off nothing is recorded.</summary>
        [Test]
        public void WB_6_Recording()
        {
            foreach (bool on in new[] { true, false })
            {
                (Simulation sim, EntityId talon) = BreachScene(-1);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            Spawn(sim, "water.coral_wall", P1, (18 + dx) + ".5", (17 + dy) + ".5");
                        }
                    }
                }
                sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, _sequence++, talon, BreachGoal));
                SkirmishAiController controller = new SkirmishAiController(P0, on ? Candidate : CandidateEdited("\"use_wall_breach\": true", "\"use_wall_breach\": false"));
                controller.OnMatchStart(sim);
                // Thinks (and steps) until the record shows or 60 ticks pass. Unit: ticks.
                for (int i = 0; i < 60 && controller.Memory.UnreachableGoals.Count == 0; i++)
                {
                    controller.Think(sim, new ListCommandSink(sim.Clock.Tick));
                    sim.Step();
                }
                controller.Think(sim, new ListCommandSink(sim.Clock.Tick));
                if (on)
                {
                    Assert.That(controller.Memory.UnreachableGoals.ContainsKey(talon), Is.True);
                    Assert.That(controller.Memory.UnreachableGoals[talon].Goal, Is.EqualTo(BreachGoal));
                }
                else
                {
                    Assert.That(controller.Memory.UnreachableGoals.Count, Is.EqualTo(0));
                }
            }
        }

        /// <summary>The breach keys: false in every shipped profile but the candidate, old values elsewhere and in the candidate; out-of-range values are errors naming the file and key.</summary>
        [Test]
        public void Profiles_BreachSwitchAndValidation()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.UseWallBreach, Is.EqualTo(id == "aggressive_s5_m" || id == "aggressive_s5_pd"), id);
                Assert.That(profile.WallBreachMemorySeconds, Is.EqualTo(10), id);
                Assert.That(profile.WallBreachStandoffTiles, Is.EqualTo(Fix64.FromInt(2)), id);
                Assert.That(profile.WallProbeStepTiles, Is.EqualTo(Fix64.FromInt(6)), id);
                Assert.That(profile.WallBreachMaxCommandsPerThink, Is.EqualTo(12), id);
            }
            DataValidationException range = Expect.Throws<DataValidationException>(() => CandidateEdited("\"wall_breach_memory_seconds\": 10", "\"wall_breach_memory_seconds\": 0"));
            Assert.That(range.Message, Does.Contain("aggressive_s5_m.json").And.Contain("wall_breach_memory_seconds"));
            DataValidationException standoff = Expect.Throws<DataValidationException>(() => CandidateEdited("\"wall_breach_standoff_tiles\": 2", "\"wall_breach_standoff_tiles\": 0.25"));
            Assert.That(standoff.Message, Does.Contain("wall_breach_standoff_tiles"));
        }

        // ------------------------------------------------------------------ P4 rule WL (walls and gates on the authored lines)

        /// <summary>The five unit IDs of a faction's wall rule: wall, gate, HQ, worker and drop-off depot, found by name only in this test helper (the brain finds them by flags and plan).</summary>
        /// <param name="faction">"water" or "skai".</param>
        private static (string Wall, string Gate, string Hq, string Worker, string Depot) Ids(string faction)
        {
            return faction == "skai"
                ? ("skai.wind_baffle", "skai.wind_gate", "skai.great_aerie", "skai.fledgling", "skai.perch_depot")
                : ("water.coral_wall", "water.coral_gate", "water.grand_shell", "water.tender", "water.pearl_depot");
        }

        /// <summary>
        /// A started prototype_1v1 match: the AI under test (<paramref name="faction"/>) in spawn slot <paramref name="slot"/> (player id = slot), a water enemy in the other slot; the AI has a Rooted HQ at its spawn, ten workers on its plateau,
        /// 5000 common and 400 rare; the whole map is explored; the clock stands past 180 s unless <paramref name="late"/> is false.
        /// </summary>
        /// <param name="faction">Faction of the AI under test.</param>
        /// <param name="slot">Spawn slot (0 or 1) of the AI under test.</param>
        /// <param name="late">True to step to 185 s first.</param>
        /// <param name="explore">True to mark the whole map explored.</param>
        private Simulation WallScene(string faction, int slot = 0, bool late = true, bool explore = true)
        {
            List<PlayerSetup> players = new List<PlayerSetup>();
            for (int i = 0; i < 2; i++)
            {
                players.Add(new PlayerSetup(new PlayerId((byte)i), new TeamId((byte)i), i == slot ? faction : "water", "P" + i));
            }
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            (string wall, string gate, string hq, string worker, string depot) ids = Ids(faction);
            PlayerId me = new PlayerId((byte)slot);
            // Own units are placed on slot 0's plateau and point-mirrored (x, y) -> (96 - x, 96 - y) for slot 1.
            Spawn(sim, ids.hq, me, Mirror("12.5", slot), Mirror("12.5", slot));
            foreach ((string x, string y) at in new[] { ("6.5", "18.5"), ("10.5", "19.5"), ("19.5", "9.5"), ("19.5", "5.5"), ("8.5", "8.5"), ("14.5", "16.5"), ("16.5", "12.5"), ("12.5", "8.5"), ("9.5", "15.5"), ("17.5", "17.5") })
            {
                Spawn(sim, ids.worker, me, Mirror(at.x, slot), Mirror(at.y, slot));
            }
            Assert.That(sim.Players.TryGet(me, out Player? player), Is.True);
            player!.SetStock(5000, 400);
            if (explore)
            {
                ExploreKit.ExploreAll(sim);
            }
            while (late && sim.Clock.Tick < 3700)
            {
                sim.Step();
            }
            return sim;
        }

        /// <summary>A coordinate text point-mirrored through the map centre for slot 1 (96 - v), unchanged for slot 0.</summary>
        /// <param name="value">Coordinate text such as "12.5".</param>
        /// <param name="slot">0 or 1.</param>
        private static string Mirror(string value, int slot)
        {
            return slot == 0 ? value : (Fix64.FromInt(96) - Fix64.Parse(value)).ToString();
        }

        /// <summary>The candidate profile with <paramref name="oldNewPairs"/> applied (see <see cref="CandidateEdited"/>).</summary>
        /// <param name="oldNewPairs">Old text, new text, old text, new text and so on.</param>
        private static AiProfile WallProfile(params string[] oldNewPairs)
        {
            return oldNewPairs.Length == 0 ? Candidate : CandidateEdited(oldNewPairs).WithWallLines(Candidate.WallLines);
        }

        /// <summary>A memory with a variety stream whose wall style is forced.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="faction">Faction of the AI.</param>
        /// <param name="style">The style to force.</param>
        private static AiMemory WallMemory(AiProfile profile, string faction, WallStyle style)
        {
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            memory.Variety = AiVariety.ForTest(profile.RequireFaction(faction), 1);
            memory.Variety.SetWallStyleForTest(style);
            return memory;
        }

        /// <summary>Decides for player <paramref name="me"/> on a fresh view.</summary>
        private static List<AiIntent> DecideFor(Simulation sim, PlayerId me, AiProfile profile, AiMemory memory, string faction)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, me, memory.KnownFieldRemaining), profile, profile.RequireFaction(faction), memory, new DeterministicRandom(1));
        }

        /// <summary>The intents of rule WL: every BuildWallLine and every Build of the faction's gate.</summary>
        /// <param name="intents">All intents of a think.</param>
        /// <param name="faction">Faction of the AI.</param>
        private static List<AiIntent> WallIntents(List<AiIntent> intents, string faction)
        {
            string gate = Ids(faction).Gate;
            return intents.FindAll(i => i.Kind == AiIntentKind.BuildWallLine || (i.Kind == AiIntentKind.Build && i.DefinitionId == gate));
        }

        /// <summary>One wall-rule intent as text without its worker: "wall [(x,y),...]" or "gate (x,y)".</summary>
        private static string Desc(AiIntent intent)
        {
            if (intent.Kind == AiIntentKind.Build)
            {
                return intent.DefinitionId + " (" + intent.TileX + "," + intent.TileY + ")";
            }
            // Tile texts in order.
            List<string> tiles = new List<string>();
            foreach ((int X, int Y) tile in intent.Tiles)
            {
                tiles.Add("(" + tile.X + "," + tile.Y + ")");
            }
            return intent.DefinitionId + " [" + string.Join(",", tiles) + "]";
        }

        /// <summary>The wall-rule intents of a think as one text, intents joined by " | ".</summary>
        private static string WallText(List<AiIntent> intents, string faction)
        {
            List<string> parts = new List<string>();
            foreach (AiIntent intent in WallIntents(intents, faction))
            {
                parts.Add(Desc(intent));
            }
            return string.Join(" | ", parts);
        }

        /// <summary>Submits the wall-rule intents of a think as the AI's commands on the current tick and steps once.</summary>
        private void ApplyWalls(Simulation sim, PlayerId me, List<AiIntent> intents, string faction)
        {
            foreach (AiIntent intent in WallIntents(intents, faction))
            {
                sim.Submit(intent.ToCommand(sim.Clock.Tick, me, _aiSequence++));
            }
            sim.Step();
        }

        /// <summary>The free worker nearest the centre of a tile as rule WL's PickBuilder sees it: not under a build order and not in <paramref name="taken"/>; ties to the lower ID.</summary>
        private static EntityId NearestFree(Simulation sim, PlayerId me, int x, int y, ICollection<EntityId> taken)
        {
            // Best worker so far and its squared distance. Unit: squared tiles.
            EntityId best = EntityId.None;
            Fix64 bestSquared = Fix64.Zero;
            FixVec2 centre = new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half);
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner != me || !unit.Definition.Economy.CanGather || unit.Definition.Economy.FootprintTiles > 0 || unit.BuildState != BuildOrderState.None || taken.Contains(unit.Id))
                {
                    continue;
                }
                Fix64 squared = (unit.Position - centre).LengthSquared();
                if (!best.IsValid || squared < bestSquared)
                {
                    best = unit.Id;
                    bestSquared = squared;
                }
            }
            return best;
        }

        /// <summary>Asserts that each BuildWallLine and gate Build of a think is ordered to the free worker nearest its first tile among those no earlier intent of the think picked.</summary>
        private static void AssertBuilders(Simulation sim, PlayerId me, List<AiIntent> intents)
        {
            // Workers picked by earlier intents of the think (Build, Move and BuildWallLine intents pick a worker).
            HashSet<EntityId> picked = new HashSet<EntityId>();
            foreach (AiIntent intent in intents)
            {
                bool isWall = intent.Kind == AiIntentKind.BuildWallLine;
                if (isWall || intent.Kind == AiIntentKind.Build)
                {
                    // First tile of the order.
                    int x = isWall ? intent.Tiles[0].X : intent.TileX;
                    int y = isWall ? intent.Tiles[0].Y : intent.TileY;
                    if (intent.DefinitionId.EndsWith("gate", StringComparison.Ordinal) || isWall)
                    {
                        Assert.That(intent.Unit, Is.EqualTo(NearestFree(sim, me, x, y, picked)), Desc(intent));
                    }
                    picked.Add(intent.Unit);
                }
                else if (intent.Kind == AiIntentKind.Move)
                {
                    picked.Add(intent.Unit);
                }
            }
        }

        /// <summary>WL-1: main base, style sealed: the north ramp, the east ramp and its gate, by the nearest free workers; pending orders stop a repeat; Skai uses its wall and gate; slot 1 uses the mirrored tiles.</summary>
        [Test]
        public void Wl1_MainSealed()
        {
            foreach ((string faction, int slot) in new[] { ("water", 0), ("skai", 0), ("water", 1) })
            {
                Simulation sim = WallScene(faction, slot);
                PlayerId me = new PlayerId((byte)slot);
                AiProfile profile = WallProfile();
                AiMemory memory = WallMemory(profile, faction, WallStyle.Sealed);
                List<AiIntent> intents = DecideFor(sim, me, profile, memory, faction);
                string wall = Ids(faction).Wall;
                string gate = Ids(faction).Gate;
                string expected = slot == 0
                    ? wall + " [(4,21),(5,21),(6,21),(7,21)] | " + wall + " [(22,7),(22,9),(22,10)] | " + gate + " (22,8)"
                    : wall + " [(91,74),(90,74),(89,74),(88,74)] | " + wall + " [(73,88),(73,86),(73,85)] | " + gate + " (73,87)";
                Assert.That(WallText(intents, faction), Is.EqualTo(expected), faction + " slot " + slot);
                AssertBuilders(sim, me, intents);
                Assert.That(memory.WallSpentCommon, Is.EqualTo(7 * 25 + 50));
                ApplyWalls(sim, me, intents, faction);
                Assert.That(WallText(DecideFor(sim, me, profile, memory, faction), faction), Is.EqualTo(string.Empty), "with the orders pending nothing is ordered again");
            }
        }

        /// <summary>WL-2: style funnel: the same lines without the gate; with a gap radius of 1 the second line keeps only (22,10).</summary>
        [Test]
        public void Wl2_Funnel()
        {
            Simulation sim = WallScene("water");
            AiProfile profile = WallProfile();
            List<AiIntent> intents = DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Funnel), "water");
            Assert.That(WallText(intents, "water"), Is.EqualTo("water.coral_wall [(4,21),(5,21),(6,21),(7,21)] | water.coral_wall [(22,7),(22,9),(22,10)]"));
            Assert.That(Of(intents, AiIntentKind.Build).FindAll(i => i.DefinitionId == "water.coral_gate"), Is.Empty);
            AiProfile gap1 = WallProfile("\"wall_funnel_gap_tiles\": 0", "\"wall_funnel_gap_tiles\": 1");
            Assert.That(WallText(DecideFor(WallScene("water"), P0, gap1, WallMemory(gap1, "water", WallStyle.Funnel), "water"), "water"),
                Is.EqualTo("water.coral_wall [(4,21),(5,21),(6,21),(7,21)] | water.coral_wall [(22,10)]"));
        }

        /// <summary>The expected intents of WL-3 think 1, 2, 3 (natural held, style sealed).</summary>
        private static readonly string[] NaturalThinks =
        {
            "water.coral_wall [(8,27),(9,27),(10,27),(11,27),(12,27),(13,27),(14,27)] | water.coral_wall [(24,25),(25,25),(26,25),(27,25)]",
            "water.coral_wall [(33,15),(34,15),(35,15),(36,15)] | water.coral_wall [(33,17)] | water.coral_gate (33,16)",
            "water.coral_wall [(46,3)]",
        };

        /// <summary>The scene of WL-3: WL-1's with an own complete depot at (27,13).</summary>
        private Simulation NaturalScene()
        {
            Simulation sim = WallScene("water");
            Spawn(sim, "water.pearl_depot", P0, "27.5", "13.5");
            return sim;
        }

        /// <summary>WL-3: natural held: the main's lines are covered; the natural's ring takes three thinks (two lines per think), the gate after its line, then nothing.</summary>
        [Test]
        public void Wl3_NaturalHeld()
        {
            Simulation sim = NaturalScene();
            AiProfile profile = WallProfile();
            AiMemory memory = WallMemory(profile, "water", WallStyle.Sealed);
            for (int think = 0; think < NaturalThinks.Length; think++)
            {
                List<AiIntent> intents = DecideFor(sim, P0, profile, memory, "water");
                Assert.That(WallText(intents, "water"), Is.EqualTo(NaturalThinks[think]), "think " + (think + 1));
                AssertBuilders(sim, P0, intents);
                ApplyWalls(sim, P0, intents, "water");
            }
            Assert.That(WallText(DecideFor(sim, P0, profile, memory, "water"), "water"), Is.EqualTo(string.Empty), "then nothing");
        }

        /// <summary>WL-4: a worker holding an accepted build order of the drop-off at (27,13) holds the natural: the first think is WL-3's.</summary>
        [Test]
        public void Wl4_AboutToExpand()
        {
            Simulation sim = WallScene("water");
            EntityId worker = NearestFree(sim, P0, 27, 13, new List<EntityId>());
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, worker, "water.pearl_depot", 27, 13));
            sim.Step();
            Assert.That(sim.Units.TryGet(worker, out Unit? builder) && builder!.BuildState != BuildOrderState.None, Is.True, "the depot order was accepted");
            AiProfile profile = WallProfile();
            Assert.That(WallText(DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(NaturalThinks[0]));
        }

        /// <summary>WL-5: the basin held as well: only the basin's lines are wanted.</summary>
        [Test]
        public void Wl5_BasinCovers()
        {
            Simulation sim = NaturalScene();
            Spawn(sim, "water.pearl_depot", P0, "37.5", "20.5");
            AiProfile profile = WallProfile();
            Assert.That(WallText(DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"),
                Is.EqualTo("water.coral_wall [(8,27),(9,27),(10,27),(11,27),(12,27),(13,27),(14,27)] | water.coral_wall [(25,30)]"));
        }

        /// <summary>WL-6: with expansion_wall_max_bases 0 the natural is dropped and the main's lines return; a depot at the enemy's natural site holds nothing.</summary>
        [Test]
        public void Wl6_CapAndOrder()
        {
            AiProfile none = WallProfile("\"expansion_wall_max_bases\": 2", "\"expansion_wall_max_bases\": 0");
            Assert.That(WallText(DecideFor(NaturalScene(), P0, none, WallMemory(none, "water", WallStyle.Sealed), "water"), "water"),
                Is.EqualTo("water.coral_wall [(4,21),(5,21),(6,21),(7,21)] | water.coral_wall [(22,7),(22,9),(22,10)] | water.coral_gate (22,8)"));
            Simulation sim = NaturalScene();
            Spawn(sim, "water.pearl_depot", P0, "68.5", "82.5");
            AiProfile profile = WallProfile();
            Assert.That(WallText(DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(NaturalThinks[0]));
            // Only the enemy's natural depot: the AI holds the main alone.
            Simulation alone = WallScene("water");
            Spawn(alone, "water.pearl_depot", P0, "68.5", "82.5");
            Assert.That(WallText(DecideFor(alone, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"),
                Is.EqualTo("water.coral_wall [(4,21),(5,21),(6,21),(7,21)] | water.coral_wall [(22,7),(22,9),(22,10)] | water.coral_gate (22,8)"));
        }

        /// <summary>WL-7: the skips: switch off, style none, before 180 s, an air-only enemy model, an enemy army unit near, too little money (and no reservation), the spending cap, and a map without a file.</summary>
        [Test]
        public void Wl7_Skips()
        {
            AiProfile profile = WallProfile();
            // Baseline: the sealed main fires.
            Assert.That(WallText(DecideFor(WallScene("water"), P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.Not.EqualTo(string.Empty));
            AiProfile off = WallProfile("\"use_walls\": true", "\"use_walls\": false");
            Assert.That(WallText(DecideFor(WallScene("water"), P0, off, WallMemory(off, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(string.Empty), "switch off");
            Assert.That(WallText(DecideFor(WallScene("water"), P0, profile, WallMemory(profile, "water", WallStyle.None), "water"), "water"), Is.EqualTo(string.Empty), "style none");
            Assert.That(WallText(DecideFor(WallScene("water", 0, false), P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(string.Empty), "before 180 s");
            // Enemy model holding only Roc Riders: no ground share.
            AiMemory air = WallMemory(profile, "water", WallStyle.Sealed);
            Simulation airSim = WallScene("water");
            air.EnemyTypes["skai.roc_rider"] = new EnemyTypeEstimate { Count = Fix64.FromInt(4), LastSeenTick = airSim.Clock.Tick };
            Assert.That(WallText(DecideFor(airSim, P0, profile, air, "water"), "water"), Is.EqualTo(string.Empty), "air-only enemy model");
            // A visible enemy army unit 2 tiles from the east line (and 5 tiles from the natural's anchor in the natural scene).
            Simulation near = WallScene("water");
            Spawn(near, "water.grasper", P1, "24.5", "9.5");
            Assert.That(WallText(DecideFor(near, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(string.Empty), "enemy near the main");
            Simulation nearNatural = NaturalScene();
            Spawn(nearNatural, "water.grasper", P1, "24.5", "9.5");
            Assert.That(WallText(DecideFor(nearNatural, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(string.Empty), "enemy near the natural");
            // Free common below one wall: nothing, and the rest of the think equals the same think with the switch off (nothing was reserved).
            Simulation poor = WallScene("water");
            Assert.That(poor.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(24, 400);
            List<AiIntent> withWalls = DecideFor(poor, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water");
            Assert.That(WallText(withWalls, "water"), Is.EqualTo(string.Empty), "no money");
            Assert.That(Key(withWalls), Is.EqualTo(Key(DecideFor(poor, P0, off, WallMemory(off, "water", WallStyle.Sealed), "water"))), "nothing reserved");
            // The spending cap reached.
            AiMemory spent = WallMemory(profile, "water", WallStyle.Sealed);
            spent.WallSpentCommon = profile.WallMaxSpendCommon;
            Assert.That(WallText(DecideFor(WallScene("water"), P0, profile, spent, "water"), "water"), Is.EqualTo(string.Empty), "cap");
            // A map without a wall file: no intent and no exception.
            Simulation greybox = Greybox("water", "water");
            Hq(greybox, "water");
            ExploreKit.ExploreAll(greybox);
            Assert.That(greybox.Players.TryGet(P0, out Player? gbPlayer), Is.True);
            gbPlayer!.SetStock(5000, 400);
            while (greybox.Clock.Tick < 3700)
            {
                greybox.Step();
            }
            Assert.That(WallText(DecideFor(greybox, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Is.EqualTo(string.Empty), "no file for greybox_1v1");
        }

        /// <summary>WL-8: payment: with common for exactly four walls the first line is bought and the second is not (nothing reserved); with budget queues the defence queue pays and no want is recorded.</summary>
        [Test]
        public void Wl8_LinePayment()
        {
            AiProfile profile = WallProfile();
            // The other rules of the think (structures, research) spend or reserve some of the stock before rule WL runs, so the stock is searched: from 25 common upward in steps of one wall, the first stock at which exactly the
            // first line (four walls) is ordered and the second line is not (n = 0 stops the rule without a reservation).
            string firstOnly = "water.coral_wall [(4,21),(5,21),(6,21),(7,21)]";
            int found = -1;
            // Stock tried. Unit: common resource units. Range: 25 to 2000.
            for (int stock = 25; stock <= 2000 && found < 0; stock += 25)
            {
                Simulation scene = WallScene("water");
                Assert.That(scene.Players.TryGet(P0, out Player? player), Is.True);
                player!.SetStock(stock, 400);
                // The walls ordered at this stock, as text.
                string text = WallText(DecideFor(scene, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water");
                TestContext.WriteLine(stock + " common: " + (text.Length == 0 ? "no wall" : text));
                found = text == firstOnly ? stock : found;
            }
            Assert.That(found, Is.GreaterThan(0), "some stock buys exactly the first line");
            Simulation sim;
            // Budget queues on, with income (a worker gathering): walls are paid from the defence queue's account only. A funded account of 100 buys the four walls of the first line, spends 100 from the queue and records no want;
            // an unfunded account buys nothing (the defence queue holds money only while static defence wants it).
            AiProfile queued = WallProfile("\"use_budget_queues\": false", "\"use_budget_queues\": true");
            sim = WallScene("water");
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, NearestFree(sim, P0, 7, 10, new List<EntityId>()), 0));
            sim.Step();
            AiMemory unfunded = WallMemory(queued, "water", WallStyle.Sealed);
            Assert.That(WallText(DecideFor(sim, P0, queued, unfunded, "water"), "water"), Is.EqualTo(string.Empty), "an empty defence account buys no wall");
            AiMemory memory = WallMemory(queued, "water", WallStyle.Sealed);
            memory.BudgetAccounts[AiBudget.Cell(AiBudget.Defence, AiBudget.Common)] = 100;
            // The defence ceiling (12 percent of all spending) needs earlier spending elsewhere: 10 000 common spent by the economy queue.
            memory.BudgetSpent[AiBudget.Cell(0, AiBudget.Common)] = 10000;
            List<AiIntent> bq = DecideFor(sim, P0, queued, memory, "water");
            Assert.That(WallText(bq, "water"), Is.EqualTo(firstOnly), "walls are bought through the defence queue");
            Assert.That(memory.BudgetSpent[AiBudget.Cell(AiBudget.Defence, AiBudget.Common)], Is.EqualTo(100));
            Assert.That(memory.WallSpentCommon, Is.EqualTo(100));
            Assert.That(memory.BudgetWants[AiBudget.Cell(AiBudget.Defence, AiBudget.Common)], Is.EqualTo(0));
        }

        /// <summary>WL-9: the spending cap: with wall_max_spend_common 100 only the first line is ordered; later thinks order nothing, even after a wall is removed.</summary>
        [Test]
        public void Wl9_SpendingCap()
        {
            Simulation sim = WallScene("water");
            AiProfile profile = WallProfile("\"wall_max_spend_common\": 1200", "\"wall_max_spend_common\": 100");
            AiMemory memory = WallMemory(profile, "water", WallStyle.Sealed);
            List<AiIntent> intents = DecideFor(sim, P0, profile, memory, "water");
            Assert.That(WallText(intents, "water"), Is.EqualTo("water.coral_wall [(4,21),(5,21),(6,21),(7,21)]"));
            Assert.That(memory.WallSpentCommon, Is.EqualTo(100));
            ApplyWalls(sim, P0, intents, "water");
            // Run until the four walls stand (5 s each plus the walk).
            for (int i = 0; i < 900; i++)
            {
                sim.Step();
            }
            Unit? wall = WallAt(sim, P0, 5, 21);
            Assert.That(wall, Is.Not.Null, "the walls were built");
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, wall!.Id));
            sim.Step();
            Assert.That(WallText(DecideFor(sim, P0, profile, memory, "water"), "water"), Is.EqualTo(string.Empty));
        }

        /// <summary>The own wall unit standing on a tile, or null.</summary>
        private static Unit? WallAt(Simulation sim, PlayerId owner, int x, int y)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Economy.IsWall && unit.Position.X.FloorToInt() == x && unit.Position.Y.FloorToInt() == y)
                {
                    return unit;
                }
            }
            return null;
        }

        /// <summary>WL-10: a removed wall is rebuilt; a tile refused for good (in WallTilesRefused, or reported by an event the controller reads) is never ordered again.</summary>
        [Test]
        public void Wl10_RebuildAndRefused()
        {
            Simulation sim = WallScene("water");
            AiProfile profile = WallProfile();
            AiMemory memory = WallMemory(profile, "water", WallStyle.Sealed);
            List<AiIntent> intents = DecideFor(sim, P0, profile, memory, "water");
            ApplyWalls(sim, P0, intents, "water");
            for (int i = 0; i < 1500; i++)
            {
                sim.Step();
            }
            Unit? wall = WallAt(sim, P0, 5, 21);
            Assert.That(wall, Is.Not.Null, "the north ramp wall was built");
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, wall!.Id));
            sim.Step();
            Assert.That(WallText(DecideFor(sim, P0, profile, memory, "water"), "water"), Is.EqualTo("water.coral_wall [(5,21)]"));
            // A tile in the refused set is never ordered.
            memory.WallTilesRefused.Add(21 * 96 + 5);
            Assert.That(WallText(DecideFor(sim, P0, profile, memory, "water"), "water"), Is.EqualTo(string.Empty));
            // The controller records the refusal from the event: a line naming a resource-field tile.
            Simulation second = WallScene("water");
            SkirmishAiController controller = new SkirmishAiController(P0, profile);
            controller.OnMatchStart(second);
            EntityId worker = NearestFree(second, P0, 7, 10, new List<EntityId>());
            second.Submit(new BuildWallLineCommand(second.Clock.Tick, P0, _sequence++, worker, "water.coral_wall", new[] { (4, 21), (7, 10) }));
            second.Step();
            controller.Think(second, new ListCommandSink(second.Clock.Tick));
            Assert.That(controller.Memory.WallTilesRefused, Does.Contain(10 * 96 + 7), "the field tile is remembered");
            Assert.That(controller.Memory.WallTilesRefused, Does.Not.Contain(21 * 96 + 4));
        }

        /// <summary>WL-10b: a line the simulation refuses whole (its only tile is a resource field, so every tile is filtered and no skipped-tile event follows) is remembered by the first tile of the order, so the AI stops re-sending it.</summary>
        [Test]
        public void Wl10b_WholeLineRefusalIsRemembered()
        {
            Simulation sim = WallScene("water");
            SkirmishAiController controller = new SkirmishAiController(P0, WallProfile());
            controller.OnMatchStart(sim);
            // The worker that gets the order: any free own worker. Unit: entity ID.
            EntityId worker = NearestFree(sim, P0, 7, 10, new List<EntityId>());
            sim.Submit(new BuildWallLineCommand(sim.Clock.Tick, P0, _sequence++, worker, "water.coral_wall", new[] { (7, 10) }));
            sim.Step();
            controller.Think(sim, new ListCommandSink(sim.Clock.Tick));
            Assert.That(controller.Memory.WallTilesRefused, Does.Contain(10 * 96 + 7), "the first tile of the refused line is remembered");
        }

        /// <summary>WL-10c: a gate tile with a ground unit standing on it (here an enemy the AI's workers see; it costs no supply of the AI's own) is not ordered this think, because the simulation would refuse it as tile_occupied; once the tile is free it is ordered.</summary>
        [Test]
        public void Wl10c_GateWaitsForAnOccupiedTile()
        {
            Simulation sim = WallScene("water");
            AiProfile profile = WallProfile();
            // A visible ground unit of the enemy on the east-ramp gate tile (22,8).
            EntityId blocker = Spawn(sim, "water.tender", new PlayerId(1), "22.5", "8.5");
            Assert.That(WallText(DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Does.Not.Contain("water.coral_gate"), "no gate order onto an occupied tile");
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, blocker));
            sim.Step();
            Assert.That(WallText(DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"), "water"), Does.Contain("water.coral_gate (22,8)"), "the free tile is ordered");
        }

        /// <summary>WL-11: a line never explored: the nearest worker is sent to look (Move) and the other lines go on.</summary>
        [Test]
        public void Wl11_Unexplored()
        {
            // The north-west corridor (8,27) to (14,27) is marked unexplored again (everything else explored) before the clock runs to 185 s.
            Simulation sim = WallScene("water", 0, false, false);
            ExploreKit.ExploreAllExcept(sim, new[] { (8, 27), (9, 27), (10, 27), (11, 27), (12, 27), (13, 27), (14, 27) });
            Spawn(sim, "water.pearl_depot", P0, "27.5", "13.5");
            while (sim.Clock.Tick < 3700)
            {
                sim.Step();
            }
            AiProfile profile = WallProfile();
            Assert.That(AiWorldView.Build(sim, P0).IsExplored(8, 27), Is.False, "test setup: the corridor is unexplored");
            List<AiIntent> intents = DecideFor(sim, P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water");
            Assert.That(WallText(intents, "water"), Is.EqualTo("water.coral_wall [(24,25),(25,25),(26,25),(27,25)]"));
            AiIntent? move = intents.Find(i => i.Kind == AiIntentKind.Move && (i.Point - new FixVec2(Fix64.FromInt(8) + Fix64.Half, Fix64.FromInt(27) + Fix64.Half)).LengthSquared() == Fix64.Zero);
            Assert.That(move, Is.Not.Null, "a worker is sent to the corridor");
            Assert.That(move!.Unit, Is.EqualTo(NearestFree(sim, P0, 8, 27, new List<EntityId>())));
        }

        /// <summary>WL-12: harness accounting: an accepted AI line is charged to the ledger's wall class at the whole cost, and the wall mechanic is credited once a wall completes.</summary>
        [Test]
        public void Wl12_HarnessAccounting()
        {
            Simulation sim = WallScene("water");
            AiProfile profile = WallProfile();
            SkirmishAiController controller = new SkirmishAiController(P0, profile);
            controller.OnMatchStart(sim);
            controller.Memory.Variety!.SetWallStyleForTest(WallStyle.Funnel);
            SummaryObserver observer = new SummaryObserver(30, new[] { 100 });
            observer.Begin(sim);
            List<IPlayerController> controllers = new List<IPlayerController> { controller };
            // Tiles of the accepted lines. Unit: tiles.
            int accepted = 0;
            // Steps taken. Unit: ticks. Range: 0 to 1500.
            for (int i = 0; i < 1500; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                observer.AfterThink(controllers);
                foreach (Command command in controller.LastCommands)
                {
                    accepted += command is BuildWallLineCommand line ? line.Tiles.Count : 0;
                }
                sim.Step();
                observer.AfterStep(sim);
            }
            GameSummary summary = observer.Finish(sim.Clock.Tick - 1)[0];
            Assert.That(accepted, Is.GreaterThan(0));
            Assert.That(summary.Spent["wall"][0], Is.GreaterThan(0));
            Assert.That(summary.Spent["wall"][0] % 25, Is.EqualTo(0));
            Assert.That(summary.Spent["wall"][0], Is.LessThanOrEqualTo(accepted * 25));
            Assert.That(summary.Mechanics.TryGetValue("wall", out int uses) ? uses : 0, Is.GreaterThan(0));
        }

        /// <summary>WL-12b: GameTrace charges an accepted AI wall line the same way SummaryObserver does: the trace's final "spent=" common of player 0 equals the summary's total, which includes a positive wall class.</summary>
        [Test]
        public void Wl12b_TraceWallAccounting()
        {
            Simulation sim = WallScene("water");
            SkirmishAiController controller = new SkirmishAiController(P0, WallProfile());
            controller.OnMatchStart(sim);
            controller.Memory.Variety!.SetWallStyleForTest(WallStyle.Funnel);
            SummaryObserver observer = new SummaryObserver(30, new[] { 100 });
            observer.Begin(sim);
            // The trace's output text.
            StringWriter text = new StringWriter();
            GameTrace trace = new GameTrace(text, 30);
            trace.Begin(sim, "wl12b");
            List<IPlayerController> controllers = new List<IPlayerController> { controller };
            // Steps taken. Unit: ticks. Range: 0 to 1500.
            for (int i = 0; i < 1500; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                observer.AfterThink(controllers);
                trace.AfterThink(sim, controllers);
                sim.Step();
                observer.AfterStep(sim);
                trace.AfterStep(sim);
            }
            GameSummary summary = observer.Finish(sim.Clock.Tick - 1)[0];
            trace.Finish(sim, MatchOutcome.Evaluate(sim.Players), sim.Clock.Tick - 1, 100);
            Assert.That(summary.Spent["wall"][0], Is.GreaterThan(0));
            // The last "spent=<n>c/" of the text is player 0's whole-game total.
            System.Text.RegularExpressions.MatchCollection spent = System.Text.RegularExpressions.Regex.Matches(text.ToString(), "spent=(\\d+)c/");
            Assert.That(spent.Count, Is.GreaterThan(0));
            Assert.That(text.ToString(), Does.Contain("spent=" + summary.SpentCommon + "c/"));
        }

        /// <summary>WL-13: the exit game: ai_vs_ai's two players on prototype_1v1, both aggressive_s5_m, run twice: identical hashes at every hash tick and no AI rejection other than site_occupied. Whether it is decided and which areas were walled is written to the test output.</summary>
        [Test]
        public void Wl13_ExitGame()
        {
            List<ulong> first = PlayExitGame(out string report);
            TestContext.WriteLine(report);
            List<ulong> second = PlayExitGame(out _);
            Assert.That(second, Is.EqualTo(first), "two runs, identical hashes at every hash tick");
            Assert.That(report, Does.Contain("rejected other than site_occupied: 0"), report);
        }

        /// <summary>Plays the exit game up to 12000 ticks; returns the state hash at every hash tick and a one-paragraph report.</summary>
        /// <param name="report">Receives the report text.</param>
        private List<ulong> PlayExitGame(out string report)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai AI"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water AI"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            uint sequence = 0;
            foreach ((string definition, PlayerId owner, string x, string y) in new[]
            {
                ("skai.great_aerie", P0, "12.5", "12.5"), ("skai.fledgling", P0, "11.5", "14.5"), ("skai.fledgling", P0, "14.5", "11.5"), ("skai.fledgling", P0, "10.5", "13.5"), ("skai.fledgling", P0, "13.5", "10.5"),
                ("water.grand_shell", P1, "83.5", "83.5"), ("water.tender", P1, "84.5", "81.5"), ("water.tender", P1, "81.5", "84.5"), ("water.tender", P1, "85.5", "82.5"), ("water.tender", P1, "82.5", "85.5"),
            })
            {
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
            }
            List<IPlayerController> controllers = new List<IPlayerController>
            {
                new SkirmishAiController(P0, AiProfile.FromDataSet(Data, "aggressive_s5_m")),
                new SkirmishAiController(P1, AiProfile.FromDataSet(Data, "aggressive_s5_m")),
            };
            foreach (IPlayerController controller in controllers)
            {
                controller.OnMatchStart(sim);
            }
            // Hashes at the hash ticks, and the counts the report needs.
            List<ulong> hashes = new List<ulong>();
            SortedSet<int> wallTiles = new SortedSet<int>();
            SortedDictionary<string, int> rejections = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int wallOrders = 0;
            int gateOrders = 0;
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            // Tick counter. Unit: ticks. Range: 0 to 12000.
            for (int tick = 0; tick < 12000 && !outcome.IsDecided; tick++)
            {
                AiHost.ThinkAll(sim, controllers);
                sim.Step();
                outcome = MatchOutcome.Evaluate(sim.Players);
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is CommandRejectedEvent rejected && rejected.Command.Issuer.IsValid && rejected.Reason != "site_occupied")
                    {
                        rejections[rejected.Command.Kind + ":" + rejected.Reason] = (rejections.TryGetValue(rejected.Command.Kind + ":" + rejected.Reason, out int n) ? n : 0) + 1;
                    }
                    if (evt is BuildOrderAcceptedEvent order)
                    {
                        wallOrders += order.DefinitionId.Contains("coral_wall") || order.DefinitionId.Contains("wind_baffle") ? 1 : 0;
                        gateOrders += order.DefinitionId.Contains("_gate") ? 1 : 0;
                    }
                }
                if (sim.Clock.Tick % 200 == 0)
                {
                    foreach (Unit unit in sim.Units.All)
                    {
                        if (unit.Definition.Economy.IsWall)
                        {
                            wallTiles.Add(unit.Position.Y.FloorToInt() * 96 + unit.Position.X.FloorToInt());
                        }
                    }
                }
                if (sim.Clock.Tick % Data.Tuning.Match.StateHashIntervalTicks == 0)
                {
                    hashes.Add(sim.ComputeStateHash());
                }
            }
            hashes.Add(sim.ComputeStateHash());
            // Total of rejections other than site_occupied. Unit: commands.
            int rejectedTotal = 0;
            foreach (KeyValuePair<string, int> pair in rejections)
            {
                rejectedTotal += pair.Value;
            }
            // Areas with at least one of their tiles seen standing, as "id n/total", and the wall style each AI drew.
            List<string> walled = new List<string>();
            foreach (AiWallArea area in AiWallLines.Parse(Data.AiWallLines["prototype_1v1"], Data).Areas)
            {
                int standing = 0;
                foreach ((int X, int Y) tile in AiWallLines.AllTiles(area))
                {
                    standing += wallTiles.Contains(tile.Y * 96 + tile.X) ? 1 : 0;
                }
                walled.Add(area.Id + " " + standing + "/" + AiWallLines.AllTiles(area).Count);
            }
            string styles = ((SkirmishAiController)controllers[0]).Memory.Variety!.WallStyle + "/" + ((SkirmishAiController)controllers[1]).Memory.Variety!.WallStyle;
            report = "exit game (wall styles drawn " + styles + "; areas with tiles standing: " + string.Join(", ", walled) + "): ended at tick " + sim.Clock.Tick + (outcome.IsDecided ? " decided" : " undecided") + "; wall-line orders accepted " + wallOrders + " (gate orders " + gateOrders + "); distinct wall tiles seen standing "
                + wallTiles.Count + "; rejected other than site_occupied: " + rejectedTotal + " (" + string.Join(", ", rejections) + ")";
            return hashes;
        }

        /// <summary>Profiles: use_walls is false in every shipped profile but the candidate; the old values of the other keys elsewhere; out-of-range values are errors naming the file and key.</summary>
        [Test]
        public void Profiles_WallSwitchAndValidation()
        {
            foreach (string id in Data.AiProfiles.Keys)
            {
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                bool candidate = id == "aggressive_s5_m" || id == "aggressive_s5_pd";
                Assert.That(profile.UseWalls, Is.EqualTo(candidate), id);
                Assert.That(profile.WallStyleNoneWeight, Is.EqualTo(candidate ? 20 : 100), id);
                Assert.That(profile.WallStyleFunnelWeight, Is.EqualTo(candidate ? 30 : 0), id);
                Assert.That(profile.WallStyleSealedWeight, Is.EqualTo(candidate ? 50 : 0), id);
                Assert.That(profile.WallEarliestSeconds, Is.EqualTo(180), id);
                Assert.That(profile.WallMinEnemyGroundShare, Is.EqualTo(Fix64.Parse("0.5")), id);
                Assert.That(profile.WallThreatTiles, Is.EqualTo(Fix64.FromInt(10)), id);
                Assert.That(profile.WallFunnelGapTiles, Is.EqualTo(candidate ? 0 : 1), id);
                Assert.That(profile.WallMaxLinesPerThink, Is.EqualTo(2), id);
                Assert.That(profile.ExpansionWallMaxBases, Is.EqualTo(candidate ? 2 : 0), id);
                Assert.That(profile.WallMaxSpendCommon, Is.EqualTo(candidate ? 1200 : 0), id);
            }
            DataValidationException range = Expect.Throws<DataValidationException>(() => CandidateEdited("\"wall_funnel_gap_tiles\": 0", "\"wall_funnel_gap_tiles\": 4"));
            Assert.That(range.Message, Does.Contain("aggressive_s5_m.json").And.Contain("wall_funnel_gap_tiles"));
            DataValidationException weights = Expect.Throws<DataValidationException>(() => CandidateEdited("{ \"none\": 20, \"funnel\": 30, \"sealed\": 50 }", "{ \"none\": 0, \"funnel\": 0, \"sealed\": 0 }"));
            Assert.That(weights.Message, Does.Contain("wall_style_weights"));
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => CandidateEdited("{ \"none\": 20, \"funnel\": 30, \"sealed\": 50 }", "{ \"none\": 20, \"funnel\": 30, \"sealed\": 50, \"x\": 1 }"));
            Assert.That(unknown.Message, Does.Contain("wall_style_weights"));
        }

        /// <summary>WL-14: with use_walls and use_wall_breach false nothing of rule WL happens and the intents equal those of aggressive_s5_r; WL-3 twice from scratch gives identical intents.</summary>
        [Test]
        public void Wl14_OffAndDeterminism()
        {
            AiProfile off = WallProfile("\"use_walls\": true", "\"use_walls\": false", "\"use_wall_breach\": true", "\"use_wall_breach\": false");
            AiProfile twin = CandidateCastsOff();
            AiProfile twinOff = AiProfile.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_m.json")).Replace("\r\n", "\n")
                .Replace("\"use_casts\": true", "\"use_casts\": false").Replace("\"use_support_units\": true", "\"use_support_units\": false")
                .Replace("\"use_walls\": true", "\"use_walls\": false").Replace("\"use_wall_breach\": true", "\"use_wall_breach\": false"), "ai/aggressive_s5_m.json", Data.Content);
            Assert.That(twin.UseWalls, Is.False);
            Simulation sim = WallScene("water");
            AiMemory memory = new AiMemory { AttackThreshold = 8 };
            List<AiIntent> mine = DecideFor(sim, P0, twinOff, memory, "water");
            Assert.That(WallIntents(mine, "water"), Is.Empty);
            Assert.That(memory.UnreachableGoals, Is.Empty);
            Assert.That(memory.WallTilesRefused, Is.Empty);
            Assert.That(memory.WallSpentCommon, Is.EqualTo(0));
            Assert.That(Key(mine), Is.EqualTo(Key(DecideFor(WallScene("water"), P0, OffCopy, new AiMemory { AttackThreshold = 8 }, "water"))));
            Assert.That(off.UseWalls, Is.False);
            // Old profiles.
            AiProfile old = OffCopy;
            Assert.That(old.UseWalls, Is.False);
            Assert.That(old.ExpansionWallMaxBases, Is.EqualTo(0));
            Assert.That(old.WallMaxSpendCommon, Is.EqualTo(0));
            // WL-3's first think twice from scratch.
            AiProfile profile = WallProfile();
            string a = Key(DecideFor(NaturalScene(), P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"));
            string b = Key(DecideFor(NaturalScene(), P0, profile, WallMemory(profile, "water", WallStyle.Sealed), "water"));
            Assert.That(a, Is.EqualTo(b));
        }
    }
}
