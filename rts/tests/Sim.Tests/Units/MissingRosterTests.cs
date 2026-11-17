// rts/tests/Sim.Tests/Units/MissingRosterTests.cs
// Acceptance tests R2-1 to R2-5 of rts/docs/specs/missing_roster.md (package R-2): the shipped files of the Skai Thunderhead and the water
// Tidecaller and Leviathan load with the specified values, the producers train them for the right price and time, the Tidecaller casts the
// wave and (developer decision of 18 Nov 2026) is a small mobile flood source whose water follows it, the Thunderhead cannot hit air, and the
// Leviathan sieges structures and may hit air. Part of Rts.Sim.Tests. Shipped data throughout; test code only.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Units
{
    /// <summary>The three roster units added by rts/docs/specs/missing_roster.md.</summary>
    [TestFixture]
    public sealed class MissingRosterTests
    {
        /// <summary>Player slot 0.</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1.</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A started match on the given map with P0 and P1 on separate teams in the given factions.</summary>
        private static Simulation Start(string map, string faction0, string faction1)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), faction0, "A"),
                new PlayerSetup(P1, new TeamId(1), faction1, "B"),
            };
            // The map: a named shipped map, or the 64 by 64 flat test map.
            MapDefinition definition = map == "flat" ? TestWorld.FlatMap() : Data.RequireMap(map);
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, definition));
            sim.Start();
            return sim;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half)));
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

        // ------------------------------------------------------------------ R2-1 load

        /// <summary>R2-1. The shipped files carry exactly the section 3.1 to 3.3 values.</summary>
        [Test]
        public void R2_1_DefinitionsMatchTheSpec()
        {
            UnitDefinition th = Data.Content.Units["skai.thunderhead"];
            Assert.That((th.Id, th.DisplayName, th.FactionId, th.Layer), Is.EqualTo(("skai.thunderhead", "Thunderhead", "skai", MovementLayer.Air)));
            Assert.That(th.MaxHealth, Is.EqualTo(Fix64.FromInt(190)));
            Assert.That(th.SpeedTilesPerSecond, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(th.Supply, Is.EqualTo(4));
            Assert.That(th.Radius, Is.EqualTo(Fix64.Parse("0.8")));
            Assert.That((th.CostCommon, th.CostRare), Is.EqualTo((200, 65)));
            Assert.That(th.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(40)));
            Assert.That(th.ArmourType, Is.EqualTo("armoured"));
            Assert.That(th.SightRangeTiles, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(th.AirborneSightRangeTiles, Is.EqualTo(Fix64.FromInt(6)));
            Assert.That(th.Flood.CanCastWave, Is.False);
            AssertWeapon(th, 44, "blast", "5", "2.5", false);

            UnitDefinition tc = Data.Content.Units["water.tidecaller"];
            Assert.That((tc.Id, tc.DisplayName, tc.FactionId, tc.Layer), Is.EqualTo(("water.tidecaller", "Tidecaller", "water", MovementLayer.Water)));
            Assert.That((tc.MaxHealth, tc.SpeedTilesPerSecond, tc.Radius), Is.EqualTo((Fix64.FromInt(70), Fix64.FromInt(3), Fix64.Parse("0.4"))));
            Assert.That(tc.Supply, Is.EqualTo(2));
            Assert.That((tc.CostCommon, tc.CostRare), Is.EqualTo((100, 25)));
            Assert.That(tc.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(20)));
            Assert.That((tc.ArmourType, tc.SightRangeTiles, tc.AirborneSightRangeTiles), Is.EqualTo(("light", Fix64.FromInt(9), Fix64.FromInt(9))), "airborne sight equals sight for a non-air unit");
            Assert.That(tc.Weapon, Is.Null);
            Assert.That(tc.Flood.CanCastWave, Is.True);
            Assert.That(tc.Flood.OutputPerSecond, Is.EqualTo(Fix64.Parse("0.5")), "half the Tidepool's 1");
            Assert.That(tc.Flood.Capacity, Is.EqualTo(Fix64.FromInt(10)), "a quarter of the Tidepool's 40");

            UnitDefinition lv = Data.Content.Units["water.leviathan"];
            Assert.That((lv.Id, lv.DisplayName, lv.FactionId, lv.Layer), Is.EqualTo(("water.leviathan", "Leviathan", "water", MovementLayer.Water)));
            Assert.That((lv.MaxHealth, lv.SpeedTilesPerSecond, lv.Radius), Is.EqualTo((Fix64.FromInt(320), Fix64.Parse("1.5"), Fix64.Parse("1.2"))));
            Assert.That(lv.Supply, Is.EqualTo(4));
            Assert.That((lv.CostCommon, lv.CostRare), Is.EqualTo((300, 80)));
            Assert.That(lv.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(60)));
            Assert.That((lv.ArmourType, lv.SightRangeTiles, lv.AirborneSightRangeTiles), Is.EqualTo(("armoured", Fix64.FromInt(12), Fix64.FromInt(12))), "airborne sight equals sight for a non-air unit");
            Assert.That(lv.Flood.CanCastWave, Is.False);
            Assert.That(lv.Flood.IsSource, Is.False);
            AssertWeapon(lv, 80, "blast", "9", "3.0", true);
        }

        /// <summary>Asserts a unit's weapon values; <paramref name="hitsAir"/> says whether can_target includes air (ground and water are always included).</summary>
        private static void AssertWeapon(UnitDefinition unit, int damage, string type, string range, string cooldown, bool hitsAir)
        {
            WeaponDefinition weapon = unit.Weapon!;
            Assert.That(weapon.Damage, Is.EqualTo(Fix64.FromInt(damage)));
            Assert.That(weapon.DamageType, Is.EqualTo(type));
            Assert.That(weapon.RangeTiles, Is.EqualTo(Fix64.Parse(range)));
            Assert.That(weapon.CooldownSeconds, Is.EqualTo(Fix64.Parse(cooldown)));
            Assert.That(weapon.HitDelayTicks, Is.EqualTo(2));
            Assert.That(weapon.CanTargetGround && weapon.CanTargetWater, Is.True);
            Assert.That(weapon.CanTargetAir, Is.EqualTo(hitsAir));
        }

        // ------------------------------------------------------------------ R2-3 trainable

        /// <summary>R2-3. One complete producer trains each unit: the stock drops by the cost, the unit appears after build seconds x 20 ticks, supply rises by its supply.</summary>
        [TestCase("skai", "skai.windspire", "skai.thunderhead", 200, 65, 800, 4)]
        [TestCase("water", "water.tidepool", "water.tidecaller", 100, 25, 400, 2)]
        [TestCase("water", "water.shell_forge", "water.leviathan", 300, 80, 1200, 4)]
        public void R2_3_ProducerTrainsTheUnit(string faction, string producerId, string unitId, int common, int rare, int ticks, int supply)
        {
            Simulation sim = Start("greybox_1v1", faction, faction == "skai" ? "water" : "skai");
            EntityId producer = Spawn(sim, producerId, P0, 13, 6);
            Assert.That(sim.Players.TryGet(P0, out Player? player), Is.True);
            player!.SetStock(common + 50, rare + 50);
            // Tick on which the command is applied. Unit: ticks.
            int accepted = sim.Clock.Tick;
            sim.Submit(new TrainUnitCommand(accepted, P0, _sequence++, producer, unitId));
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                Assert.That(evt, Is.Not.InstanceOf<CommandRejectedEvent>(), evt.Describe());
            }
            Assert.That((player.Common, player.Rare), Is.EqualTo((50, 50)));
            Assert.That(player.SupplyUsed, Is.EqualTo(supply));
            // Tick the UnitTrainedEvent arrived on, or -1. Unit: ticks.
            int arrived = -1;
            for (int i = 0; i < ticks + 20 && arrived < 0; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is UnitTrainedEvent trained && trained.DefinitionId == unitId)
                    {
                        arrived = trained.Tick;
                    }
                }
            }
            Assert.That(arrived - accepted, Is.InRange(ticks - 1, ticks + 5), "build seconds x 20 ticks (the event is stamped one tick early, after the command tick), plus at most the spawn retry");
            Assert.That(player.SupplyUsed, Is.EqualTo(supply));
        }

        // ------------------------------------------------------------------ R2-4 wave

        /// <summary>R2-4. A shipped Tidecaller casts the wave: one WaveCastEvent with tiles flooded, an enemy in the strip is pushed once; a Thunderhead is rejected not_wave_caster.</summary>
        [Test]
        public void R2_4_TidecallerCastsTheWave()
        {
            Simulation sim = Start("flat", "water", "skai");
            EntityId caster = Spawn(sim, "water.tidecaller", P0, 10, 20);
            EntityId enemy = Spawn(sim, "skai.windrunner", P1, 13, 20);
            EntityId flyer = Spawn(sim, "skai.thunderhead", P1, 30, 40);
            sim.Submit(new CastWaveCommand(sim.Clock.Tick, P0, _sequence++, caster, new FixVec2(Fix64.FromInt(16) + Fix64.Half, Fix64.FromInt(20) + Fix64.Half)));
            sim.Step();
            // Counts of the events of the cast tick.
            int casts = 0;
            int pushes = 0;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is WaveCastEvent cast)
                {
                    casts++;
                    Assert.That(cast.TilesFlooded, Is.GreaterThan(0));
                }
                if (evt is UnitPushedEvent pushed && pushed.Unit == enemy)
                {
                    pushes++;
                }
            }
            Assert.That((casts, pushes), Is.EqualTo((1, 1)));
            sim.Submit(new CastWaveCommand(sim.Clock.Tick, P1, _sequence++, flyer, new FixVec2(Fix64.FromInt(35), Fix64.FromInt(40))));
            sim.Step();
            // The one rejection of this tick, or null.
            string? reason = null;
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is CommandRejectedEvent rejected)
                {
                    reason = rejected.Reason;
                }
            }
            Assert.That(reason, Is.EqualTo("not_wave_caster"));
        }

        // ------------------------------------------------------------------ mobile flood source (developer decision, 18 Nov 2026)

        /// <summary>Whole drops of water the team 0 holds within <paramref name="radius"/> tiles (Chebyshev) of a tile. Unit: drops. Range: 0 upward.</summary>
        private static long WaterNear(Simulation sim, int cx, int cy, int radius)
        {
            long total = 0;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    total += FloodMath.ToDrops(sim.Terrain.At(x, y).WaterDepth);
                }
            }
            return total;
        }

        /// <summary>The Tidecaller is a flood source from data alone: water appears around its tile, and after it moves the water appears around its new tile (the mechanic reads the unit's current position every update).</summary>
        [Test]
        public void Tidecaller_FloodsAroundItself_AndTheWaterFollowsIt()
        {
            Simulation sim = Start("flat", "water", "skai");
            EntityId caster = Spawn(sim, "water.tidecaller", P0, 10, 10);
            // Run 300 ticks: several flood updates at the first position.
            while (sim.Clock.Tick < 300)
            {
                sim.Step();
            }
            Assert.That(WaterNear(sim, 10, 10, 3), Is.GreaterThan(0), "water around the first position");
            Assert.That(WaterNear(sim, 40, 10, 3), Is.EqualTo(0), "none at the far position yet");
            sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, _sequence++, caster, new FixVec2(Fix64.FromInt(40) + Fix64.Half, Fix64.FromInt(10) + Fix64.Half)));
            while (sim.Clock.Tick < 1000)
            {
                sim.Step();
            }
            sim.Units.TryGet(caster, out Unit? unit);
            Assert.That(unit!.Position.X, Is.GreaterThan(Fix64.FromInt(35)), "the Tidecaller reached the far side");
            Assert.That(WaterNear(sim, 40, 10, 3), Is.GreaterThan(0), "water around the new position");
        }

        // ------------------------------------------------------------------ R2-5 combat

        /// <summary>Hits dealt by one attacker to one target over a run of ticks, and the damage of the first. Unit: count and health points.</summary>
        private static (int Hits, Fix64 First) Hits(Simulation sim, EntityId attacker, EntityId target, int ticks)
        {
            int hits = 0;
            Fix64 first = Fix64.Zero;
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is UnitDamagedEvent damaged && damaged.Attacker == attacker && damaged.Target == target)
                    {
                        first = hits == 0 ? damaged.Damage : first;
                        hits++;
                    }
                }
            }
            return (hits, first);
        }

        /// <summary>R2-5a. A Thunderhead 4 tiles from an enemy Roc Rider fires no shot at it; the Roc Rider shoots the Thunderhead (so the units are in range and awake).</summary>
        [Test]
        public void R2_5_ThunderheadCannotHitAir()
        {
            Simulation sim = Start("flat", "skai", "skai");
            EntityId bomber = Spawn(sim, "skai.thunderhead", P0, 20, 20);
            EntityId rider = Spawn(sim, "skai.roc_rider", P1, 24, 20);
            Assert.That(Hits(sim, bomber, rider, 200).Hits, Is.EqualTo(0));
            Simulation control = Start("flat", "skai", "skai");
            EntityId bomber2 = Spawn(control, "skai.thunderhead", P0, 20, 20);
            EntityId rider2 = Spawn(control, "skai.roc_rider", P1, 24, 20);
            Assert.That(Hits(control, rider2, bomber2, 200).Hits, Is.GreaterThan(0), "control: the Roc Rider does shoot air");
        }

        /// <summary>R2-5b. A Leviathan attacking an enemy Roost lands 80 x 1.25 x 1.5 = 150 per hit (blast against structure 1.25, structure_damage_multiplier 1.5; combat mechanics package 2).</summary>
        [Test]
        public void R2_5_LeviathanSiegesStructures()
        {
            Simulation sim = Start("flat", "water", "skai");
            EntityId siege = Spawn(sim, "water.leviathan", P0, 20, 20);
            EntityId roost = Spawn(sim, "skai.roost", P1, 27, 20);
            (int hits, Fix64 first) = Hits(sim, siege, roost, 200);
            Assert.That(hits, Is.GreaterThan(0));
            Assert.That(first, Is.EqualTo(Fix64.FromInt(150)));
        }

        /// <summary>Developer decision 18 Nov 2026: the Leviathan can target air. It hits an enemy Roc Rider.</summary>
        [Test]
        public void R2_5_LeviathanHitsAir()
        {
            Simulation sim = Start("flat", "water", "skai");
            EntityId siege = Spawn(sim, "water.leviathan", P0, 20, 20);
            EntityId rider = Spawn(sim, "skai.roc_rider", P1, 26, 20);
            Assert.That(Hits(sim, siege, rider, 200).Hits, Is.GreaterThan(0));
        }
    }
}
