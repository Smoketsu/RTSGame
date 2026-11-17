// rts/tests/Sim.Tests/Statuses/GroundingAbilityTests.cs
// Acceptance tests of combat mechanics package 4, the Downdraft grounding ability (rts/docs/specs/combat_mechanics.md 4.7, D1 to D9) plus
// the loader bounds, the hash coverage of Unit.GroundingReadyTick and the content digest. Part of Rts.Sim.Tests.
// World: the 64 by 64 flat test map, P0 (test faction, team 0) owns the casters and allies, P1 (Skai, momentum, team 1) owns the flyers and the
// ground enemy. Caster S has range 7, radius 2, cooldown 30 s (600 ticks) at (5.5, 5.5). Flyers have radius 0.5. Shared Grounded duration 4 s (80 ticks).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Statuses
{
    /// <summary>Tests of the cast_grounding command, its rule and its data.</summary>
    [TestFixture]
    public class GroundingAbilityTests
    {
        /// <summary>Definition ID of the caster (ground, grounding ability, unarmed).</summary>
        private const string CasterId = "test.caster";

        /// <summary>Definition ID of a unit without the ability.</summary>
        private const string PlainId = TestWorld.RunnerId;

        /// <summary>Definition ID of the melee ground unit (damage 1, range 1, cooldown 1 s).</summary>
        private const string MeleeId = "test.melee";

        /// <summary>The caster's ability: range 7, radius 2, cooldown 30 s.</summary>
        private static GroundingAbilityDefinition Ability()
        {
            return new GroundingAbilityDefinition(Fix64.FromInt(7), Fix64.FromInt(2), Fix64.FromInt(30));
        }

        /// <summary>Builds the shared catalog: TestWorld's units plus the caster and the melee unit.</summary>
        private static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.SetCombatRules(TestWorld.Tuning.Combat.CreateRules());
            catalog.AddUnit(new UnitDefinition(CasterId, "Caster", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half,
                50, 0, "light", Fix64.FromInt(8), null, null, null, null, null, null, Ability()));
            catalog.AddUnit(new UnitDefinition(MeleeId, "Melee", TestWorld.TestFactionId, MovementLayer.Ground, Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half,
                50, 0, "light", Fix64.FromInt(8), new WeaponDefinition(Fix64.One, "kinetic", Fix64.One, Fix64.One, true, true, false)));
            return catalog;
        }

        /// <summary>A started match: P0 (test) against P1 (Skai) on the flat map, the shared catalog, the shipped tuning and a hash interval of 10.</summary>
        private static Simulation Build()
        {
            SimulationConfig config = new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval), Catalog(),
                TestWorld.TwoPlayers(), BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.FlatMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Script-issued spawn on tick 0; the unit IDs follow the call order from 1.</summary>
        private static void Spawn(Simulation sim, ref uint sequence, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
        }

        /// <summary>Steps until <paramref name="lastTick"/> has been simulated, collecting every rejection reason on the way.</summary>
        private static List<string> StepThrough(Simulation sim, int lastTick)
        {
            List<string> reasons = new List<string>();
            while (sim.Clock.Tick <= lastTick)
            {
                sim.Step();
                reasons.AddRange(sim.Events.Current.OfType<CommandRejectedEvent>().Select(r => r.Reason));
            }
            return reasons;
        }

        /// <summary>Submits a cast by the script issuer.</summary>
        private static void Cast(Simulation sim, int tick, uint sequence, uint caster, string x, string y, PlayerId? issuer = null)
        {
            sim.Submit(new CastGroundingCommand(tick, issuer ?? PlayerId.None, sequence, new EntityId(caster), new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
        }

        /// <summary>The scene of D1: S (1), F1 (2) at (10.5, 5.5), F2 (3) at (12.0, 5.5), F3 (4) at (13.5, 5.5), F4 (5) owned by P0 at (10.5, 6.5), G (6) a ground enemy at (9.5, 5.5).</summary>
        private static Simulation SceneD1()
        {
            Simulation sim = Build();
            uint seq = 0;
            Spawn(sim, ref seq, CasterId, StatusTestWorld.P0, "5.5", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P1, "10.5", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P1, "12.0", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P1, "13.5", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P0, "10.5", "6.5");
            Spawn(sim, ref seq, TestWorld.RunnerId, StatusTestWorld.P1, "9.5", "5.5");
            return sim;
        }

        /// <summary>D1: the pulse grounds the two enemy flyers inside radius plus their own radius until tick 90; the others are unchanged; the caster waits until tick 610.</summary>
        [Test]
        public void D1_Pulse()
        {
            Simulation sim = SceneD1();
            Cast(sim, 10, 100, 1, "10.5", "5.5");
            StepThrough(sim, 9);
            Assert.That(StatusTestWorld.UnitOf(sim, 2).EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            StepThrough(sim, 10);
            Assert.That(StatusTestWorld.EventsOf<GroundingCastEvent>(sim).Single().UnitsGrounded, Is.EqualTo(2));
            GroundingCastEvent cast = StatusTestWorld.EventsOf<GroundingCastEvent>(sim).Single();
            Assert.That(cast.Caster, Is.EqualTo(new EntityId(1)));
            Assert.That(cast.Owner, Is.EqualTo(StatusTestWorld.P0));
            Assert.That(cast.Target, Is.EqualTo(new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("5.5"))));
            foreach (uint id in new uint[] { 2, 3 })
            {
                Unit grounded = StatusTestWorld.UnitOf(sim, id);
                Assert.That(grounded.EffectiveLayer, Is.EqualTo(MovementLayer.Ground), "F" + (id - 1));
                Assert.That(grounded.Statuses.GroundedUntilTick, Is.EqualTo(90));
                Assert.That(grounded.Statuses.GroundedSourceKey, Is.EqualTo("grounding"));
            }
            foreach (uint id in new uint[] { 4, 5, 6 })
            {
                Assert.That(StatusTestWorld.UnitOf(sim, id).Statuses.IsGrounded, Is.False, "unit " + id);
            }
            Assert.That(StatusTestWorld.UnitOf(sim, 4).EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            Assert.That(StatusTestWorld.UnitOf(sim, 5).EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            Assert.That(StatusTestWorld.UnitOf(sim, 1).GroundingReadyTick, Is.EqualTo(610));
            StepThrough(sim, 89);
            Assert.That(StatusTestWorld.UnitOf(sim, 2).EffectiveLayer, Is.EqualTo(MovementLayer.Ground));
            StepThrough(sim, 90);
            Assert.That(StatusTestWorld.UnitOf(sim, 2).EffectiveLayer, Is.EqualTo(MovementLayer.Air));
            Assert.That(StatusTestWorld.UnitOf(sim, 3).EffectiveLayer, Is.EqualTo(MovementLayer.Air));
        }

        /// <summary>D2: a second cast on tick 609 is rejected on_cooldown; one on tick 610 is accepted.</summary>
        [Test]
        public void D2_Cooldown()
        {
            Simulation sim = SceneD1();
            Cast(sim, 10, 100, 1, "10.5", "5.5");
            Cast(sim, 609, 101, 1, "10.5", "5.5");
            Cast(sim, 610, 102, 1, "10.5", "5.5");
            List<string> reasons = StepThrough(sim, 609);
            Assert.That(reasons, Is.EqualTo(new List<string> { "on_cooldown" }));
            Assert.That(StatusTestWorld.UnitOf(sim, 1).GroundingReadyTick, Is.EqualTo(610));
            reasons = StepThrough(sim, 610);
            Assert.That(reasons, Is.Empty);
            Assert.That(StatusTestWorld.EventsOf<GroundingCastEvent>(sim).Count, Is.EqualTo(1));
            Assert.That(StatusTestWorld.UnitOf(sim, 1).GroundingReadyTick, Is.EqualTo(1210));
        }

        /// <summary>D3: a target 7.0 away is accepted (cooldown starts), one 7.25 away is rejected out_of_range and does not start the cooldown.</summary>
        [Test]
        public void D3_Range()
        {
            Simulation ok = SceneD1();
            Cast(ok, 1, 100, 1, "12.5", "5.5");
            Assert.That(StepThrough(ok, 1), Is.Empty);
            Assert.That(StatusTestWorld.UnitOf(ok, 1).GroundingReadyTick, Is.EqualTo(601));
            Simulation far = SceneD1();
            Cast(far, 1, 100, 1, "12.75", "5.5");
            Assert.That(StepThrough(far, 1), Is.EqualTo(new List<string> { "out_of_range" }));
            Assert.That(StatusTestWorld.UnitOf(far, 1).GroundingReadyTick, Is.EqualTo(0));
        }

        /// <summary>D4: every rejection, and the order when a command fails two checks.</summary>
        [Test]
        public void D4_RejectionsAndOrder()
        {
            Simulation sim = SceneD1();
            uint seq = 10;
            // Unit 7: a second caster that gets a construction counter on tick 1.
            Spawn(sim, ref seq, CasterId, StatusTestWorld.P0, "20.5", "20.5");
            StatusTestWorld.Run(sim, 1, 0, s => StatusTestWorld.UnitOf(s, 7).SetConstructionTicks(500));
            // One command per tick so every reason is attributed to its command.
            Cast(sim, 2, 0, 999, "5.5", "5.5");
            Cast(sim, 3, 0, 1, "5.5", "5.5", StatusTestWorld.P1);
            Cast(sim, 4, 0, 4, "5.5", "5.5");
            Cast(sim, 5, 0, 7, "20.5", "20.5");
            Cast(sim, 6, 0, 1, "20000", "5.5");
            Cast(sim, 7, 0, 1, "40.5", "40.5");
            // Failing two checks: unknown unit and out of bounds; not_owner and out_of_range; no_ability and out_of_bounds; under construction and out_of_range.
            Cast(sim, 8, 0, 999, "20000", "5.5");
            Cast(sim, 9, 0, 1, "40.5", "40.5", StatusTestWorld.P1);
            Cast(sim, 10, 0, 4, "20000", "5.5");
            Cast(sim, 11, 0, 7, "40.5", "40.5");
            // Out of bounds and on cooldown: a first cast succeeds on tick 12, then a second with a far target on tick 13 fails out_of_bounds first.
            Cast(sim, 12, 0, 1, "5.5", "5.5");
            Cast(sim, 13, 0, 1, "20000", "5.5");
            // Out of range and on cooldown: out_of_range first.
            Cast(sim, 14, 0, 1, "40.5", "40.5");
            List<string> reasons = new List<string>();
            List<string> expected = new List<string>
            {
                "unknown_unit", "not_owner", "no_ability", "under_construction", "out_of_bounds", "out_of_range",
                "unknown_unit", "not_owner", "no_ability", "under_construction", "(none)", "out_of_bounds", "out_of_range",
            };
            for (int tick = 2; tick <= 14; tick++)
            {
                List<string> got = StepThrough(sim, tick);
                reasons.Add(got.Count == 0 ? "(none)" : got.Single());
            }
            Assert.That(reasons, Is.EqualTo(expected));
        }

        /// <summary>D5: a second caster grounds an already grounded flyer again on tick 50: until tick 130, refreshed true, one unit counted.</summary>
        [Test]
        public void D5_Refresh()
        {
            Simulation sim = SceneD1();
            uint seq = 10;
            // A second caster, unit 7, next to the first.
            Spawn(sim, ref seq, CasterId, StatusTestWorld.P0, "6.5", "5.5");
            Cast(sim, 10, 100, 1, "10.5", "5.5");
            // Aimed at (9.0, 5.5): F1 (1.5 away) is inside 2 + 0.5, F2 (3.0 away) is not.
            Cast(sim, 50, 101, 7, "9.0", "5.5");
            StepThrough(sim, 49);
            StepThrough(sim, 50);
            Assert.That(StatusTestWorld.EventsOf<GroundingCastEvent>(sim).Single().UnitsGrounded, Is.EqualTo(1));
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Statuses.GroundedUntilTick, Is.EqualTo(130));
            Assert.That(StatusTestWorld.UnitOf(sim, 3).Statuses.GroundedUntilTick, Is.EqualTo(90), "F2 keeps the first cast's end");
            Assert.That(StatusTestWorld.EventsOf<StatusAppliedEvent>(sim).Single().Refreshed, Is.True);
            Assert.That(StatusTestWorld.EventsOf<UnitLayerChangedEvent>(sim), Is.Empty, "no new layer change");
        }

        /// <summary>D6: a melee enemy ordered to attack the grounded flyer on tick 11 is accepted, hits until tick 89, and drops the target on tick 90 when the flyer is Air again.</summary>
        [Test]
        public void D6_MeleeCanHitIt()
        {
            Simulation sim = Build();
            uint seq = 0;
            Spawn(sim, ref seq, CasterId, StatusTestWorld.P0, "5.5", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P1, "10.5", "5.5");
            Spawn(sim, ref seq, MeleeId, StatusTestWorld.P0, "11.5", "5.5");
            Cast(sim, 10, 100, 1, "10.5", "5.5");
            sim.Submit(new AttackUnitCommand(11, StatusTestWorld.P0, 0, new EntityId(3), new EntityId(2)));
            Assert.That(StepThrough(sim, 11), Is.Empty, "accepted");
            Assert.That(StatusTestWorld.UnitOf(sim, 3).AttackTarget, Is.EqualTo(new EntityId(2)));
            StepThrough(sim, 89);
            Fix64 healthAt89 = StatusTestWorld.UnitOf(sim, 2).Health;
            Assert.That(healthAt89, Is.LessThan(Fix64.FromInt(100)), "melee hits landed while it was grounded");
            Assert.That(StatusTestWorld.UnitOf(sim, 3).AttackTarget, Is.EqualTo(new EntityId(2)));
            StepThrough(sim, 90);
            Assert.That(StatusTestWorld.UnitOf(sim, 3).AttackTarget, Is.EqualTo(EntityId.None), "invalid once it is Air again");
            StepThrough(sim, 110);
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Health, Is.EqualTo(healthAt89));
        }

        /// <summary>D7: an enemy Skai flyer with 5 Momentum stacks loses them on the cast's tick (one momentum_lost, reason grounded).</summary>
        [Test]
        public void D7_Momentum()
        {
            Simulation sim = Build();
            uint seq = 0;
            Spawn(sim, ref seq, CasterId, StatusTestWorld.P0, "5.5", "5.5");
            Spawn(sim, ref seq, TestWorld.FlyerId, StatusTestWorld.P1, "10.5", "20.5");
            sim.Submit(new MoveUnitCommand(0, PlayerId.None, seq++, new EntityId(2), new FixVec2(Fix64.FromInt(60), Fix64.Parse("20.5"))));
            StepThrough(sim, 119);
            MomentumMechanic momentum = (MomentumMechanic)sim.Mechanics[MomentumMechanic.MechanicId];
            Assert.That(momentum.TryGetState(new EntityId(2), out MomentumState? state), Is.True);
            Assert.That(state!.Stacks, Is.EqualTo(5));
            // Aim at where the flyer is now; the caster is far away, so move it next to the flyer first (a script-side test hook).
            FixVec2 where = StatusTestWorld.UnitOf(sim, 2).Position;
            StatusTestWorld.Run(sim, 120, 0, s => StatusTestWorld.UnitOf(s, 1).SetPosition(new FixVec2(where.X, where.Y - Fix64.FromInt(3))));
            sim.Submit(new CastGroundingCommand(120, PlayerId.None, 1, new EntityId(1), where));
            StepThrough(sim, 120);
            Assert.That(StatusTestWorld.UnitOf(sim, 2).Statuses.IsGrounded, Is.True);
            Assert.That(state.Stacks, Is.EqualTo(0));
            List<MomentumLostEvent> lost = StatusTestWorld.EventsOf<MomentumLostEvent>(sim);
            Assert.That(lost.Count, Is.EqualTo(1));
            Assert.That(lost[0].Reason, Is.EqualTo("grounded"));
            Assert.That(lost[0].StacksLost, Is.EqualTo(5));
        }

        /// <summary>D8: the codec round-trips the command and the script reader builds the same command; an unknown key fails.</summary>
        [Test]
        public void D8_CodecAndScript()
        {
            CommandCodecRegistry registry = BuiltInMechanics.CreateCodecRegistry();
            CastGroundingCommand original = new CastGroundingCommand(10, new PlayerId(1), 3, new EntityId(7), new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("-5.25")));
            byte[] payload = registry.EncodePayload(original);
            CastGroundingCommand decoded = (CastGroundingCommand)registry.DecodePayload(original.Kind, 10, new PlayerId(1), 3, payload);
            Assert.That(decoded.Caster, Is.EqualTo(original.Caster));
            Assert.That(decoded.Target, Is.EqualTo(original.Target));
            Assert.That(Fingerprint(decoded), Is.EqualTo(Fingerprint(original)));
            Assert.That(original.Kind, Is.EqualTo("cast_grounding"));
            const string line = "{\"type\":\"cast_grounding\",\"tick\":10,\"issuer\":1,\"sequence\":0,\"unit\":1,\"x\":10.5,\"y\":5.5}";
            CommandScript script = CommandScript.Parse(ScriptWith(line), "inline.json");
            CastGroundingCommand parsed = (CastGroundingCommand)script.Commands.Single();
            Assert.That(Fingerprint(parsed), Is.EqualTo(Fingerprint(new CastGroundingCommand(10, new PlayerId(1), 0, new EntityId(1), new FixVec2(Fix64.Parse("10.5"), Fix64.Parse("5.5"))))));
            DataValidationException ex = Expect.Throws<DataValidationException>(() =>
                CommandScript.Parse(ScriptWith(line.Replace("\"y\":5.5", "\"y\":5.5,\"radius\":3")), "inline.json"));
            Assert.That(ex.Message, Does.Contain("radius"));
        }

        /// <summary>A minimal script text around one command line.</summary>
        private static string ScriptWith(string commandLine)
        {
            return "{\"$schema_version\": 1, \"seed\": 1, \"ticks\": 20, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"test\" }, { \"id\": 1, \"team\": 1, \"faction\": \"skai\" } ], \"commands\": [" + commandLine + "]}";
        }

        /// <summary>Hash of a command's scheduling fields and parameters.</summary>
        private static ulong Fingerprint(Command command)
        {
            StateHasher hasher = new StateHasher();
            hasher.Write(command.Tick);
            hasher.Write(command.Issuer);
            hasher.Write(command.Sequence);
            command.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>D9: identical runs hash equal at every tick; a cast one tick later differs from the first tick the other run has cast; the shipped Stormcaller (only) carries the ability.</summary>
        [Test]
        public void D9_Hash()
        {
            List<ulong> a = HashesOfRun(10);
            List<ulong> b = HashesOfRun(10);
            List<ulong> later = HashesOfRun(11);
            Assert.That(b, Is.EqualTo(a));
            // Index of the first tick whose hash differs between the cast on tick 10 and the cast on tick 11; -1 when none.
            int firstDifference = -1;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != later[i])
                {
                    firstDifference = i;
                    break;
                }
            }
            Assert.That(firstDifference, Is.EqualTo(10));
            DataSet shipped = TestData.LoadAll();
            foreach (KeyValuePair<string, UnitDefinition> unit in shipped.Content.Units)
            {
                Assert.That(unit.Value.GroundingAbility != null, Is.EqualTo(unit.Key == "skai.stormcaller"), unit.Key);
            }
            GroundingAbilityDefinition storm = shipped.Content.Units["skai.stormcaller"].GroundingAbility!;
            Assert.That(storm.RangeTiles, Is.EqualTo(Fix64.FromInt(7)));
            Assert.That(storm.RadiusTiles, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(storm.CooldownSeconds, Is.EqualTo(Fix64.FromInt(30)));
        }

        /// <summary>State hash after each of the first 30 ticks of D1's scene with the cast on <paramref name="castTick"/>.</summary>
        private static List<ulong> HashesOfRun(int castTick)
        {
            Simulation sim = SceneD1();
            Cast(sim, castTick, 100, 1, "10.5", "5.5");
            List<ulong> hashes = new List<ulong>();
            for (int tick = 0; tick < 30; tick++)
            {
                StepThrough(sim, tick);
                hashes.Add(sim.ComputeStateHash());
            }
            return hashes;
        }

        /// <summary>Loader bounds: range 0.5 to 32, radius 0.5 to 8, cooldown 1 to 300 seconds, all three keys required, unknown keys rejected; each error names the file and field.</summary>
        [Test]
        public void Loader_Bounds()
        {
            const string file = "units/skai/stormcaller.json";
            Check(file, "\"range_tiles\": 7,\n    // Radius around", "\"range_tiles\": 0.25,\n    // Radius around", "grounding_ability.range_tiles");
            Check(file, "\"range_tiles\": 7,\n    // Radius around", "\"range_tiles\": 33,\n    // Radius around", "grounding_ability.range_tiles");
            Check(file, "\"radius_tiles\": 2,", "\"radius_tiles\": 0.25,", "grounding_ability.radius_tiles");
            Check(file, "\"radius_tiles\": 2,", "\"radius_tiles\": 9,", "grounding_ability.radius_tiles");
            Check(file, "\"cooldown_seconds\": 30\n", "\"cooldown_seconds\": 0.5\n", "grounding_ability.cooldown_seconds");
            Check(file, "\"cooldown_seconds\": 30\n", "\"cooldown_seconds\": 301\n", "grounding_ability.cooldown_seconds");
            Check(file, "\"cooldown_seconds\": 30\n", "\"cooldown_seconds\": 30,\n\"extra\": 1\n", "extra");
        }

        /// <summary>Asserts that a one-text edit makes the loader throw an error naming the file and <paramref name="field"/>.</summary>
        private static void Check(string file, string oldText, string newText, string field)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(TestData.WithEdit(file, oldText, newText)));
            Assert.That(ex.Message, Does.Contain("stormcaller.json"));
            Assert.That(ex.Message, Does.Contain(field));
        }

        /// <summary>The ability's fields are in the content digest (a build without it cannot share a match).</summary>
        [Test]
        public void ContentDigest_CoversTheAbility()
        {
            DataSet baseline = TestData.LoadAll();
            DataSet edited = DataLoader.Load(TestData.WithEdit("units/skai/stormcaller.json", "\"cooldown_seconds\": 30\n", "\"cooldown_seconds\": 31\n"));
            Assert.That(edited.Content.ComputeDigest(), Is.Not.EqualTo(baseline.Content.ComputeDigest()));
        }
    }
}
