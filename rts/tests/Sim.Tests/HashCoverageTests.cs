// rts/tests/Sim.Tests/HashCoverageTests.cs
// Proves the state hash covers what it must (ADR-005): for every field added to units and players in Phase 1, changing that
// field alone changes Simulation.ComputeStateHash, so a desync in it would be caught at the next hash tick. Also proves the
// content digest (hashed at tick 0) covers the tuning text and the map, and that the speed-modifier IDs are hashed.
// Part of Rts.Sim.Tests. Uses the internal setters (InternalsVisibleTo) on units and players; nothing here steps the simulation
// between a change and the hash, so each case isolates one field.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests
{
    /// <summary>Every Phase 1 unit and player field feeds the state hash; the content digest covers tuning and map.</summary>
    [TestFixture]
    public class HashCoverageTests
    {
        /// <summary>A small world with one unit of each kind a case needs, and the IDs to find them again.</summary>
        private sealed class World
        {
            /// <summary>The started simulation (economy map, test faction against Skai).</summary>
            public Simulation Sim = null!;

            /// <summary>An armed ground unit of P0 (the shooter).</summary>
            public Unit Shooter = null!;

            /// <summary>An enemy unit of P1 the shooter may target.</summary>
            public Unit Enemy = null!;

            /// <summary>P0's HQ (Rooted).</summary>
            public Unit Hq = null!;

            /// <summary>P0's worker, already given a gather order and a load so each gather field can change alone.</summary>
            public Unit Worker = null!;

            /// <summary>A flying unit of P1, for the layer override and the statuses.</summary>
            public Unit Flyer = null!;

            /// <summary>P0's barracks (a producer).</summary>
            public Unit Barracks = null!;
        }

        /// <summary>Builds the same world every time: spawns through script commands, then sets a baseline gather order and load directly.</summary>
        private static World Build()
        {
            World w = new World();
            // Fresh sequence numbers are drawn from the shared kit, so two builds still produce equal hashes (sequence is not hashed).
            w.Sim = TestWorld.StartedEconomy();
            w.Shooter = Get(w.Sim, Spawn(w.Sim, TestWorld.ShooterId, P0, 20, 20));
            w.Enemy = Get(w.Sim, Spawn(w.Sim, TestWorld.TankId, P1, 50, 50));
            w.Hq = Get(w.Sim, Spawn(w.Sim, TestWorld.HqId, P0, 12, 12));
            w.Worker = Get(w.Sim, Spawn(w.Sim, TestWorld.WorkerId, P0, 14, 12));
            w.Flyer = Get(w.Sim, Spawn(w.Sim, TestWorld.FlyerId, P1, 40, 40));
            w.Barracks = Get(w.Sim, Spawn(w.Sim, TestWorld.BarracksId, P0, 25, 25));
            // Baseline orders, so each case below changes exactly one field of an already non-default state.
            w.Shooter.SetAttackTarget(w.Enemy.Id, false);
            w.Worker.SetGather(GatherState.ToField, 0, 2);
            w.Worker.SetCarried(ResourceKind.Common, 3);
            w.Flyer.Statuses.InsertSlow(new SlowEntry("base", Fix64.Half, 500, P0));
            return w;
        }

        /// <summary>State hash of the world as it stands.</summary>
        private static ulong HashOf(World w)
        {
            return w.Sim.ComputeStateHash();
        }

        /// <summary>Each case names one field and changes only it.</summary>
        private static List<KeyValuePair<string, Action<World>>> Cases()
        {
            return new List<KeyValuePair<string, Action<World>>>
            {
                Case("Unit.CooldownTicksRemaining", w => w.Shooter.SetCooldownTicks(7)),
                Case("Unit.AttackTarget", w => w.Shooter.SetAttackTarget(w.Flyer.Id, false)),
                Case("Unit.IsAttackTargetForced", w => w.Shooter.SetAttackTarget(w.Enemy.Id, true)),
                Case("Unit.IsAttackMoving + AttackMoveGoal", w => w.Shooter.SetAttackMove(FixVec2.FromInts(30, 30))),
                Case("Unit.HqState", w => w.Hq.SetHqState(HqState.Uprooted, 0)),
                Case("Unit.HqTransitionTicksRemaining", w => w.Hq.SetHqState(HqState.Rooted, 9)),
                Case("Unit.LastStandUses + LastStandTicksRemaining", w => w.Hq.StartLastStand(10)),
                Case("Unit.GatherState", w => w.Worker.SetGather(GatherState.ToDropOff, 0, 2)),
                Case("Unit.GatherFieldIndex", w => w.Worker.SetGather(GatherState.ToField, 1, 2)),
                Case("Unit.GatherTicksRemaining", w => w.Worker.SetGather(GatherState.ToField, 0, 5)),
                Case("Unit.CarriedAmount", w => w.Worker.SetCarried(ResourceKind.Common, 4)),
                Case("Unit.CarriedKind", w => w.Worker.SetCarried(ResourceKind.Rare, 3)),
                Case("Unit.ProductionQueue + ProductionTicksRemaining", w => w.Barracks.EnqueueProduction(TestWorld.RunnerId, 50)),
                Case("Unit.ConstructionTicksRemaining", w => w.Barracks.SetConstructionTicks(30)),
                Case("Unit.Statuses (Grounded)", w => w.Flyer.Statuses.SetGrounded(100, "x")),
                Case("Unit.Statuses (slow multiplier)", w => w.Flyer.Statuses.ReplaceSlowAt(0, new SlowEntry("base", Fix64.Parse("0.25"), 500, P0))),
                Case("Unit.Statuses (slow end tick)", w => w.Flyer.Statuses.ReplaceSlowAt(0, new SlowEntry("base", Fix64.Half, 501, P0))),
                Case("Unit.Statuses (drench counter)", w => w.Flyer.Statuses.SetDrench(2, 9)),
                Case("Unit.LastSelfMoveTick", w => w.Shooter.MarkSelfMoved(w.Sim.Clock.Tick + 3)),
                Case("Unit.HasLayerOverride + LayerOverride", w => w.Flyer.SetLayerOverride(MovementLayer.Ground)),
                Case("Unit.StatusSpeedMultiplier", w => w.Flyer.SetStatusSpeedMultiplier(Fix64.Half)),
                Case("Unit.Health", w => w.Enemy.SetHealth(Fix64.FromInt(7))),
                Case("Unit.Position", w => w.Shooter.SetPosition(FixVec2.FromInts(21, 20))),
                Case("Unit.MoveTarget", w => w.Shooter.SetMoveTarget(FixVec2.FromInts(33, 33))),
                Case("Player.Common", w => PlayerOf(w.Sim, P0).SetStock(PlayerOf(w.Sim, P0).Common + 1, PlayerOf(w.Sim, P0).Rare)),
                Case("Player.Rare", w => PlayerOf(w.Sim, P0).SetStock(PlayerOf(w.Sim, P0).Common, PlayerOf(w.Sim, P0).Rare + 1)),
                Case("Player.SupplyUsed", w => PlayerOf(w.Sim, P0).SetSupply(PlayerOf(w.Sim, P0).SupplyUsed + 1, PlayerOf(w.Sim, P0).SupplyCap)),
                Case("Player.SupplyCap", w => PlayerOf(w.Sim, P0).SetSupply(PlayerOf(w.Sim, P0).SupplyUsed, PlayerOf(w.Sim, P0).SupplyCap + 1)),
                Case("ResourceFields remaining", w => w.Sim.Resources[0].Take(1)),
            };
        }

        /// <summary>Pairs a case name with its change.</summary>
        private static KeyValuePair<string, Action<World>> Case(string name, Action<World> change)
        {
            return new KeyValuePair<string, Action<World>>(name, change);
        }

        /// <summary>Two fresh worlds built the same way hash the same (so a difference below is the change, not the setup).</summary>
        [Test]
        public void SameSetup_SameHash()
        {
            Assert.That(HashOf(Build()), Is.EqualTo(HashOf(Build())));
        }

        /// <summary>Changing any one Phase 1 unit or player field alone changes the state hash.</summary>
        [Test]
        public void EachPhaseOneField_AloneChangesTheStateHash()
        {
            // Names of the cases whose change left the hash unchanged.
            List<string> unhashed = new List<string>();
            foreach (KeyValuePair<string, Action<World>> test in Cases())
            {
                World w = Build();
                // Hash before the change, then after exactly one change.
                ulong before = HashOf(w);
                test.Value(w);
                if (HashOf(w) == before)
                {
                    unhashed.Add(test.Key);
                }
            }
            Assert.That(unhashed, Is.Empty, "fields that do not reach the state hash: " + string.Join(", ", unhashed));
        }

        /// <summary>The same change applied to two equal worlds gives equal hashes (the hash depends on the value, not on identity).</summary>
        [Test]
        public void SameChange_SameHash()
        {
            foreach (KeyValuePair<string, Action<World>> test in Cases())
            {
                World a = Build();
                World b = Build();
                test.Value(a);
                test.Value(b);
                Assert.That(HashOf(a), Is.EqualTo(HashOf(b)), test.Key);
            }
        }

        // ------------------------------------------------------------------ speed modifier IDs

        /// <summary>A mechanic that registers one speed modifier with a chosen ID, to show the ID reaches the hash.</summary>
        private sealed class SpeedIdMechanic : IFactionMechanic
        {
            /// <summary>The ID of the modifier this instance registers. Range: any non-empty text.</summary>
            private readonly string _modifierId;

            /// <summary>Remembers the modifier ID.</summary>
            public SpeedIdMechanic(string modifierId)
            {
                _modifierId = modifierId;
            }

            /// <summary>Mechanic ID used in the test faction. Range: fixed text.</summary>
            public string Id => "test_speed_id";

            /// <summary>Registers the modifier.</summary>
            public void OnMatchStart(Simulation sim)
            {
                sim.RegisterSpeedModifier(new FixedSpeedModifier(_modifierId));
            }

            /// <summary>Does nothing.</summary>
            public void OnTick(Simulation sim)
            {
            }

            /// <summary>Hashes only the mechanic ID, so any hash difference comes from the modifier list.</summary>
            public void WriteHash(StateHasher hasher)
            {
                hasher.Write(Id);
            }
        }

        /// <summary>A speed modifier that changes nothing.</summary>
        private sealed class FixedSpeedModifier : ISpeedModifier
        {
            /// <summary>Stable ID. Range: any non-empty text.</summary>
            public string Id { get; }

            /// <summary>Remembers the ID.</summary>
            public FixedSpeedModifier(string id)
            {
                Id = id;
            }

            /// <summary>Always 1. Unit: multiplier of base speed.</summary>
            public Fix64 SpeedMultiplier(Simulation sim, Unit unit)
            {
                return Fix64.One;
            }
        }

        /// <summary>Starts a match whose "speedy" faction registers a speed modifier called <paramref name="modifierId"/>.</summary>
        private static Simulation StartWithSpeedModifier(string modifierId)
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddFaction(new FactionDefinition("speedy", "Speedy", "test_speed_id", new List<string> { TestWorld.RunnerId }));
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            registry.Register("test_speed_id", () => new SpeedIdMechanic(modifierId));
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "speedy", "A"),
                new PlayerSetup(P1, new TeamId(1), TestWorld.TestFactionId, "B"),
            };
            SimulationConfig config = new SimulationConfig(new MatchSettings(1, TestWorld.TicksPerSecond, TestWorld.HashInterval), catalog, players, registry,
                TestWorld.Tuning, TestWorld.FlatMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Two matches that differ only in a registered speed modifier's ID hash differently (they would order the speed chain differently).</summary>
        [Test]
        public void SpeedModifierIds_AreHashed()
        {
            Simulation a = StartWithSpeedModifier("alpha");
            Simulation b = StartWithSpeedModifier("beta");
            Simulation again = StartWithSpeedModifier("alpha");
            Assert.That(a.ComputeStateHash(), Is.EqualTo(again.ComputeStateHash()));
            Assert.That(a.ComputeStateHash(), Is.Not.EqualTo(b.ComputeStateHash()));
        }

        // ------------------------------------------------------------------ content digest: tuning text and map

        /// <summary>Builds a match from a loaded data set on the greybox map (Skai against Water).</summary>
        private static Simulation FromData(DataSet data, string mapId = "greybox_1v1")
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "A"),
                new PlayerSetup(P1, new TeamId(1), "water", "B"),
            };
            SimulationConfig config = new SimulationConfig(data.CreateMatchSettings(1), data.Content, players, BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(mapId));
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Two different tuning texts give different Tuning.SourceDigest and different content digests; equal texts give equal ones.</summary>
        [Test]
        public void TuningText_ChangesTheSourceDigestAndTheContentDigest()
        {
            DataSet baseline = TestData.LoadAll();
            DataSet same = TestData.LoadAll();
            // A change that does not alter any parsed number: an extra comment line in a tuning file.
            DataSet commentOnly = DataLoader.Load(TestData.WithEdit("tuning/economy.json", "\"base_supply\": 10", "\"base_supply\": 10 /* edited */"));
            // A real number change.
            DataSet changedNumber = DataLoader.Load(TestData.WithEdit("tuning/economy.json", "\"base_supply\": 10", "\"base_supply\": 11"));
            Assert.That(baseline.Tuning.SourceDigest, Is.Not.EqualTo(0UL));
            Assert.That(same.Tuning.SourceDigest, Is.EqualTo(baseline.Tuning.SourceDigest));
            Assert.That(commentOnly.Tuning.SourceDigest, Is.Not.EqualTo(baseline.Tuning.SourceDigest), "the digest covers the text, not just the parsed numbers");
            Assert.That(changedNumber.Tuning.SourceDigest, Is.Not.EqualTo(baseline.Tuning.SourceDigest));
            Assert.That(changedNumber.Tuning.SourceDigest, Is.Not.EqualTo(commentOnly.Tuning.SourceDigest));
            ulong digest = FromData(baseline).ContentDigest;
            Assert.That(FromData(same).ContentDigest, Is.EqualTo(digest));
            Assert.That(FromData(commentOnly).ContentDigest, Is.Not.EqualTo(digest));
            Assert.That(FromData(changedNumber).ContentDigest, Is.Not.EqualTo(digest));
        }

        /// <summary>A map edit (spawn point, a height digit, a resource field) changes the map hash and the content digest.</summary>
        [Test]
        public void MapEdit_ChangesTheMapHashAndTheContentDigest()
        {
            DataSet baseline = TestData.LoadAll();
            DataSet moved = DataLoader.Load(TestData.WithEdit("maps/greybox_1v1.json", "{ \"x\": 8, \"y\": 8 }", "{ \"x\": 9, \"y\": 8 }"));
            Assert.That(MapHash(moved.RequireMap("greybox_1v1")), Is.Not.EqualTo(MapHash(baseline.RequireMap("greybox_1v1"))));
            Assert.That(MapHash(DataLoader.Load(TestData.ReadAll()).RequireMap("greybox_1v1")), Is.EqualTo(MapHash(baseline.RequireMap("greybox_1v1"))));
            Assert.That(FromData(moved).ContentDigest, Is.Not.EqualTo(FromData(baseline).ContentDigest));
            // Hand-built maps: a different spawn, a different height and a different field amount each change the hash.
            MapDefinition flat = TestWorld.EconomyMap();
            MapDefinition otherSpawn = new MapDefinition("test_economy", "Test Economy", TestWorld.FlatMapSize, TestWorld.FlatMapSize,
                new List<FixVec2> { FixVec2.FromInts(5, 4), FixVec2.FromInts(60, 60) }, null, null, flat.ResourceFields, null);
            MapDefinition otherAmount = new MapDefinition("test_economy", "Test Economy", TestWorld.FlatMapSize, TestWorld.FlatMapSize,
                flat.Spawns, null, null, new List<ResourceField> { new ResourceField(10, 10, ResourceKind.Common, 13), flat.ResourceFields[1], flat.ResourceFields[2] }, null);
            Assert.That(MapHash(otherSpawn), Is.Not.EqualTo(MapHash(flat)));
            Assert.That(MapHash(otherAmount), Is.Not.EqualTo(MapHash(flat)));
        }

        /// <summary>The hash of a map alone.</summary>
        private static ulong MapHash(MapDefinition map)
        {
            StateHasher hasher = new StateHasher();
            map.WriteHash(hasher);
            return hasher.Hash;
        }
    }
}
