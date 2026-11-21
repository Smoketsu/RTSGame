// rts/tests/Sim.Tests/Data/AbilityNameTests.cs
// Acceptance tests IS-10 and IS-11 of package I3 of rts/docs/specs/interface_support.md: the Stormcaller's Downdraft and the Tidecaller's wave carry a display
// name and a description from data, the loader enforces their presence and length, and changing them leaves the content digest alone. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Data
{
    /// <summary>Ability display names and descriptions in unit data.</summary>
    [TestFixture]
    public class AbilityNameTests
    {
        /// <summary>The Stormcaller's file.</summary>
        private const string StormcallerPath = "units/skai/stormcaller.json";

        /// <summary>The Tidecaller's file.</summary>
        private const string TidecallerPath = "units/water/tidecaller.json";

        /// <summary>The shipped Downdraft name line.</summary>
        private const string NameLine = "\"display_name\": \"Downdraft\",";

        /// <summary>The shipped Downdraft description line.</summary>
        private const string DescriptionLine = "\"description\": \"Slams enemy flyers near the target point to the ground.\"";

        /// <summary>The shipped wave description line.</summary>
        private const string WaveDescriptionLine = "\"wave_description\": \"Floods a strip ahead of the caster and pushes enemies back.\",";

        /// <summary>Asserts loading fails with a message naming the file, the field and the text.</summary>
        private static void AssertRejected(List<DataSourceFile> files, string file, string field, string text)
        {
            DataValidationException ex = Expect.Throws<DataValidationException>(() => DataLoader.Load(files));
            Assert.That(ex.Message, Does.Contain(file), ex.Message);
            Assert.That(ex.Message, Does.Contain(field), ex.Message);
            Assert.That(ex.Message, Does.Contain(text), ex.Message);
        }

        /// <summary>IS-10: the shipped Stormcaller and Tidecaller carry their names and non-empty descriptions.</summary>
        [Test]
        public void Is10_Shipped()
        {
            DataSet data = TestData.LoadAll();
            GroundingAbilityDefinition downdraft = data.Content.Units["skai.stormcaller"].GroundingAbility!;
            Assert.That(downdraft.DisplayName, Is.EqualTo("Downdraft"));
            Assert.That(downdraft.Description, Is.Not.Empty);
            UnitFloodDefinition flood = data.Content.Units["water.tidecaller"].Flood;
            Assert.That(flood.WaveDisplayName, Is.EqualTo("Wave"));
            Assert.That(flood.WaveDescription, Is.Not.Empty);
        }

        /// <summary>IS-11: every missing, empty, too long or misplaced text is rejected with file and field; changing a text leaves the content digest equal.</summary>
        [Test]
        public void Is11_Loader()
        {
            AssertRejected(TestData.WithEdit(StormcallerPath, NameLine, string.Empty), StormcallerPath, "display_name", "grounding_ability");
            AssertRejected(TestData.WithEdit(StormcallerPath, DescriptionLine, "\"description\": \"\""), StormcallerPath, "description", "must not be empty");
            AssertRejected(TestData.WithEdit(StormcallerPath, NameLine, "\"display_name\": \"" + new string('x', 41) + "\","), StormcallerPath, "display_name", "1 to 40 characters");
            AssertRejected(TestData.WithEdit(StormcallerPath, DescriptionLine, "\"description\": \"" + new string('x', 201) + "\""), StormcallerPath, "description", "1 to 200 characters");
            AssertRejected(TestData.WithEdit("units/skai/windrunner.json", "\"id\": \"skai.windrunner\",", "\"id\": \"skai.windrunner\", \"wave_display_name\": \"Wave\","),
                "units/skai/windrunner.json", "wave_display_name", "only allowed with can_cast_wave true");
            AssertRejected(TestData.WithEdit(TidecallerPath, WaveDescriptionLine, string.Empty), TidecallerPath, "wave_description", "wave_description");
            // Limits are inclusive: 40 and 200 characters load.
            Assert.That(DataLoader.Load(TestData.WithEdit(StormcallerPath, NameLine, "\"display_name\": \"" + new string('x', 40) + "\",")).Content.Units["skai.stormcaller"].GroundingAbility!.DisplayName.Length, Is.EqualTo(40));
            // Names are outside the content digest.
            ulong baseline = DataLoader.Load(TestData.ReadAll()).Content.ComputeDigest();
            Assert.That(DataLoader.Load(TestData.WithEdit(StormcallerPath, NameLine, "\"display_name\": \"Other\",")).Content.ComputeDigest(), Is.EqualTo(baseline));
            Assert.That(DataLoader.Load(TestData.WithEdit(TidecallerPath, "\"wave_display_name\": \"Wave\"", "\"wave_display_name\": \"Surge\"")).Content.ComputeDigest(), Is.EqualTo(baseline));
        }
    }
}
