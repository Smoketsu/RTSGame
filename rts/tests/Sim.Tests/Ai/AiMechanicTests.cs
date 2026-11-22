// rts/tests/Sim.Tests/Ai/AiMechanicTests.cs
// Tests of package P1 (casts) of rts/docs/specs/ai_mechanic_use.md: M1-1 to M1-5 (Downdraft, rule CG), M2-1 to M2-4 and the cast half of M2-8 (the wave, rule CW), V-1 and V-2 (the wave-aim draw, aim half),
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
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

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

        /// <summary>The candidate with use_casts written false (every other key at its candidate value).</summary>
        private static AiProfile CandidateCastsOff()
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive_s5_m.json")).Replace("\r\n", "\n");
            Assert.That(text, Does.Contain("\"use_casts\": true"));
            return AiProfile.Parse(text.Replace("\"use_casts\": true", "\"use_casts\": false"), "ai/aggressive_s5_m.json", Data.Content);
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
                Assert.That(profile.UseCasts, Is.EqualTo(id == "aggressive_s5_m"), id);
                Assert.That(profile.WaveMinEnemySupply, Is.EqualTo(Fix64.FromInt(4)), id);
                Assert.That(profile.WaveMeleeRangeTiles, Is.EqualTo(Fix64.One), id);
                Assert.That(profile.WaveAimMeleeWeight, Is.EqualTo(id == "aggressive_s5_m" ? 50 : 100), id);
                Assert.That(profile.WaveAimAllWeight, Is.EqualTo(id == "aggressive_s5_m" ? 50 : 0), id);
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
            AiProfile profile = Candidate;
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
            Assert.That(melee, Is.EqualTo(498));
            Assert.That(all, Is.EqualTo(502));
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
    }
}
