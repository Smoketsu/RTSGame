// rts/tests/Sim.Tests/Ai/AiWallLineDataTests.cs
// Data tests of the authored AI wall lines (rts/data/ai/walls/<map id>.json) of rts/docs/specs/ai_mechanic_use.md. P4-0: WD-4 (the loader route and the digest). Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Factions;
using Rts.Sim.Tests.Data;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Tests of the authored wall-line data and of the loader route that carries it.</summary>
    [TestFixture]
    public class AiWallLineDataTests
    {
        /// <summary>Path of the authored file of prototype_1v1, data-root-relative. Range: fixed.</summary>
        private const string ProtoWalls = "ai/walls/prototype_1v1.json";

        /// <summary>Plays the first ticks of smoke.json on the given data and returns the content digest and the state hash.</summary>
        /// <param name="data">Loaded data set.</param>
        /// <param name="ticks">Ticks to step. Unit: ticks. Range: 1 to 1000.</param>
        private static (ulong Digest, ulong Hash) RunSmoke(DataSet data, int ticks)
        {
            // The smoke script from the real data folder.
            CommandScript script = CommandScript.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts", "smoke.json")), "scripts/smoke.json", BuiltInMechanics.ScriptParsers);
            // The simulation under test.
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(script.Seed), data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap(script.MapId)));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            // Tick counter. Unit: ticks. Range: 0 to ticks.
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
            }
            return (sim.ContentDigest, sim.ComputeStateHash());
        }

        /// <summary>WD-4: the wall file is routed to AiWallLines, not AiProfiles; misplaced and mismatched files are rejected; the digest and state hash ignore it.</summary>
        [Test]
        public void Wd4_LoaderRouteAndDigest()
        {
            // Everything as shipped.
            DataSet data = TestData.LoadAll();
            Assert.That(data.AiWallLines.Keys, Does.Contain("prototype_1v1"));
            Assert.That(data.AiProfiles.Keys, Does.Not.Contain("prototype_1v1"));
            Assert.That(data.AiProfiles.Keys, Does.Contain("aggressive_s5_m"));

            // The shipped files plus one file one level too deep under ai/, which stays rejected.
            List<DataSourceFile> deep = TestData.ReadAll();
            deep.Add(new DataSourceFile("ai/other/x.json", "{ \"$schema_version\": 1, \"$doc\": \"x\", \"id\": \"x\" }"));
            DataValidationException wrongPlace = Expect.Throws<DataValidationException>(() => DataLoader.Load(deep));
            Assert.That(wrongPlace.Message, Does.Contain("ai/other/x.json").And.Contain("wrong place"));

            // A wall file whose id differs from its stem is rejected.
            DataValidationException badId = Expect.Throws<DataValidationException>(() =>
                DataLoader.Load(TestData.WithEdit(ProtoWalls, "\"id\": \"prototype_1v1\"", "\"id\": \"prototype_x\"")));
            Assert.That(badId.Message, Does.Contain(ProtoWalls));

            // The same data without the wall files: same digest, same state hash after 200 ticks.
            DataSet without = DataLoader.Load(TestData.Without(ProtoWalls));
            Assert.That(without.AiWallLines, Is.Empty);
            (ulong Digest, ulong Hash) withWalls = RunSmoke(data, 200);
            (ulong Digest, ulong Hash) withoutWalls = RunSmoke(without, 200);
            Assert.That(withoutWalls.Digest, Is.EqualTo(withWalls.Digest));
            Assert.That(withoutWalls.Hash, Is.EqualTo(withWalls.Hash));
        }
    }
}
