// rts/tests/Sim.Tests/Data/RosterNumbers2Tests.cs
// Acceptance tests 1 and 2 of rts/docs/specs/roster_numbers_2.md section 5.3: the Spoutling loads with health 90 and weapon damage 10, the Shellback with build time 20 s, and every other
// number of the two units is unchanged. Part of Rts.Sim.Tests.
using NUnit.Framework;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Spoutling and Shellback data after roster_numbers_2.</summary>
    [TestFixture]
    public class RosterNumbers2Tests
    {
        /// <summary>Test 1: Spoutling health 90 (was 70) and damage 10 (was 5); cost, supply, speed, range, cooldown, build time, damage type and drench are unchanged.</summary>
        [Test]
        public void Spoutling_LoadsWithTheNewNumbers()
        {
            // The loaded unit definition. Range: water.spoutling from the shipped data.
            UnitDefinition unit = TestData.LoadAll().Content.Units["water.spoutling"];
            Assert.That(unit.MaxHealth, Is.EqualTo(Fix64.FromInt(90)));
            Assert.That(unit.Weapon, Is.Not.Null);
            Assert.That(unit.Weapon!.Damage, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(unit.CostCommon, Is.EqualTo(100));
            Assert.That(unit.CostRare, Is.EqualTo(25));
            Assert.That(unit.Supply, Is.EqualTo(2));
            Assert.That(unit.SpeedTilesPerSecond, Is.EqualTo(Fix64.FromInt(3)));
            Assert.That(unit.Weapon.RangeTiles, Is.EqualTo(Fix64.FromInt(5)));
            Assert.That(unit.Weapon.CooldownSeconds, Is.EqualTo(Fix64.One));
            Assert.That(unit.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(20)));
            Assert.That(unit.Weapon.DamageType, Is.EqualTo("drench"));
            Assert.That(unit.Weapon.AppliesDrench, Is.True);
        }

        /// <summary>Test 2: Shellback build time 20 s (was 28); health, cost, supply, speed, damage and range are unchanged.</summary>
        [Test]
        public void Shellback_LoadsWithTheNewBuildTime()
        {
            // The loaded unit definition. Range: water.shellback from the shipped data.
            UnitDefinition unit = TestData.LoadAll().Content.Units["water.shellback"];
            Assert.That(unit.Economy.BuildSeconds, Is.EqualTo(Fix64.FromInt(20)));
            Assert.That(unit.MaxHealth, Is.EqualTo(Fix64.FromInt(220)));
            Assert.That(unit.CostCommon, Is.EqualTo(175));
            Assert.That(unit.Supply, Is.EqualTo(3));
            Assert.That(unit.SpeedTilesPerSecond, Is.EqualTo(Fix64.Parse("1.8")));
            Assert.That(unit.Weapon, Is.Not.Null);
            Assert.That(unit.Weapon!.Damage, Is.EqualTo(Fix64.FromInt(10)));
            Assert.That(unit.Weapon.RangeTiles, Is.EqualTo(Fix64.FromInt(2)));
        }
    }
}
