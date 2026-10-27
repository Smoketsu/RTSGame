// rts/tests/Replay.Tests/EndToEndTests.cs
// End-to-end replay tests through the real tools: record rts/data/scripts/ai_vs_ai.json (with its AI profiles) with the harness option
// --record, re-simulate it with ReplayCheck and expect the same final hash; a tampered command is reported as a desync at the right
// tick; a different format version or data digest is refused with exit code 2. Also library-level recorder checks on the smoke script.
// Part of Rts.Replay.Tests.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using HarnessProgram = Rts.Tools.BalanceHarness.Program;
using CheckProgram = Rts.Tools.ReplayCheck.Program;

namespace Rts.Replay.Tests
{
    /// <summary>Record, re-simulate, tamper and refuse.</summary>
    public sealed class EndToEndTests
    {
        /// <summary>Folder holding the replay files of this fixture; created in OneTimeSetUp, deleted in OneTimeTearDown.</summary>
        private string _folder = string.Empty;

        /// <summary>The real rts/data folder.</summary>
        private string _dataDir = string.Empty;

        /// <summary>Path of the AI-versus-AI replay recorded once for the whole fixture.</summary>
        private string _aiReplayPath = string.Empty;

        /// <summary>The STATE_HASH line value the harness printed while recording, 16 hex digits.</summary>
        private string _aiFinalHash = string.Empty;

        /// <summary>Records ai_vs_ai.json once (the longest step of the tests) and keeps the file for the tests below.</summary>
        [OneTimeSetUp]
        public void RecordAiMatch()
        {
            _folder = ReplayTestSupport.NewTempDirectory();
            _dataDir = ReplayTestSupport.FindDataDirectory();
            _aiReplayPath = Path.Combine(_folder, "ai_vs_ai.rtsreplay");
            // Harness output and errors of the recording run.
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            int exit = HarnessProgram.Run(new[] { "--data", _dataDir, "--script", Path.Combine(_dataDir, "scripts", "ai_vs_ai.json"), "--record", _aiReplayPath }, output, error);
            Assert.That(exit, Is.EqualTo(0), error.ToString());
            _aiFinalHash = ValueOf(output.ToString(), "STATE_HASH=");
            Assert.That(File.Exists(_aiReplayPath), Is.True);
            Assert.That(output.ToString(), Does.Contain("REPLAY_RECORDED file="));
        }

        /// <summary>Deletes the temporary folder.</summary>
        [OneTimeTearDown]
        public void Cleanup()
        {
            if (_folder.Length > 0 && Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        /// <summary>Returns the token after <paramref name="prefix"/> on the line that contains it (to the end of the line or the next space).</summary>
        private static string ValueOf(string text, string prefix)
        {
            // Index of the prefix in the text. Range: 0 upward; the assert guards -1.
            int at = text.IndexOf(prefix, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "'" + prefix + "' not found in: " + text);
            // Start and end of the value token.
            int start = at + prefix.Length;
            int end = start;
            while (end < text.Length && text[end] != ' ' && text[end] != '\n' && text[end] != '\r')
            {
                end++;
            }
            return text.Substring(start, end - start);
        }

        /// <summary>Runs ReplayCheck on a replay file against the real data and returns its exit code, stdout and stderr.</summary>
        private static int Check(string replayPath, string dataDir, out string output, out string error)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errWriter = new StringWriter();
            int exit = CheckProgram.Run(new[] { "--data", dataDir, "--replay", replayPath }, outWriter, errWriter);
            output = outWriter.ToString();
            error = errWriter.ToString();
            return exit;
        }

        /// <summary>Reads the fixture's AI replay.</summary>
        private ReplayData ReadAiReplay()
        {
            return ReplayReader.Read(File.ReadAllBytes(_aiReplayPath), CommandCodecRegistry.CreateBuiltIn());
        }

        /// <summary>Writes a replay to a new file in the fixture folder and returns the path.</summary>
        private string WriteReplay(ReplayData replay, string name)
        {
            // Destination path in the fixture folder.
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, ReplayWriter.Write(replay, CommandCodecRegistry.CreateBuiltIn()));
            return path;
        }

        /// <summary>The recorded AI match re-simulates to the same final hash, every checkpoint matches, and the replay holds the AI's commands.</summary>
        [Test]
        public void RecordedAiMatchResimulatesToTheSameFinalHash()
        {
            int exit = Check(_aiReplayPath, _dataDir, out string output, out string error);
            Assert.That(exit, Is.EqualTo(0), error + output);
            Assert.That(output.Trim(), Is.EqualTo("REPLAY OK final=" + _aiFinalHash));
            ReplayData replay = ReadAiReplay();
            // The script has 10 spawn commands; everything else came from the two AI controllers, so a replay without AI commands would be too short.
            Assert.That(replay.Commands.Count, Is.GreaterThan(50), "the AI's commands must be in the replay");
            Assert.That(replay.Checkpoints.Count, Is.EqualTo((replay.TickCount + replay.HashIntervalTicks - 1) / replay.HashIntervalTicks));
            Assert.That(replay.FinalHash.ToString("x16", CultureInfo.InvariantCulture), Is.EqualTo(_aiFinalHash));
            Assert.That(replay.MapId, Is.EqualTo("greybox_1v1"));
            Assert.That(replay.Players.Count, Is.EqualTo(2));
        }

        /// <summary>Recording must not change play: the harness prints the same hash lines with and without --record.</summary>
        [Test]
        public void RecordingDoesNotChangeTheRun()
        {
            // Smoke script path and a replay file for the recorded run.
            string script = Path.Combine(_dataDir, "scripts", "smoke.json");
            string recorded = Path.Combine(_folder, "smoke.rtsreplay");
            StringWriter plainOut = new StringWriter();
            StringWriter recordedOut = new StringWriter();
            Assert.That(HarnessProgram.Run(new[] { "--data", _dataDir, "--script", script }, plainOut, new StringWriter()), Is.EqualTo(0));
            Assert.That(HarnessProgram.Run(new[] { "--data", _dataDir, "--script", script, "--record", recorded }, recordedOut, new StringWriter()), Is.EqualTo(0));
            // Recorded output without its trailing REPLAY_RECORDED line.
            string recordedLines = recordedOut.ToString().Substring(0, recordedOut.ToString().IndexOf("REPLAY_RECORDED", StringComparison.Ordinal));
            Assert.That(recordedLines, Is.EqualTo(plainOut.ToString()));
            Assert.That(Check(recorded, _dataDir, out string output, out string error), Is.EqualTo(0), error);
            Assert.That(output, Does.StartWith("REPLAY OK final="));
        }

        /// <summary>A command added between checkpoints is reported at the next checkpoint tick, with different expected and actual hashes, and exit code 1.</summary>
        [Test]
        public void ATamperedCommandIsReportedAsADesyncAtTheRightTick()
        {
            ReplayData replay = ReadAiReplay();
            // Tick of the added command: five intervals in, plus five ticks, so it is between two checkpoints.
            int tamperTick = replay.HashIntervalTicks * 5 + 5;
            Assume.That(replay.TickCount, Is.GreaterThan(tamperTick + replay.HashIntervalTicks), "the recorded match is too short for this test");
            // First checkpoint tick at or after the added command; the command's effect first shows there.
            int expectedDesyncTick = (tamperTick / replay.HashIntervalTicks + 1) * replay.HashIntervalTicks;
            List<Command> tampered = new List<Command>(replay.Commands)
            {
                new SpawnUnitCommand(tamperTick, PlayerId.None, 100000, "skai.fledgling", new PlayerId(0), FixVec2.FromInts(8, 8)),
            };
            ReplayData changed = new ReplayData(replay.ContentDigest, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, tampered, replay.Checkpoints, replay.FinalHash);
            int exit = Check(WriteReplay(changed, "tampered_add.rtsreplay"), _dataDir, out string output, out string error);
            Assert.That(exit, Is.EqualTo(1), error + output);
            Assert.That(output, Does.StartWith("REPLAY DESYNC tick=" + expectedDesyncTick.ToString(CultureInfo.InvariantCulture) + " expected="));
            Assert.That(output, Does.Contain(" actual="));
            // The two hashes in the message must differ.
            Assert.That(ValueOf(output, "expected="), Is.Not.EqualTo(ValueOf(output, "actual=")));
        }

        /// <summary>Dropping a recorded command changes a later hash; a changed seed desyncs at tick 0.</summary>
        [Test]
        public void ADroppedCommandAndAChangedSeedDesync()
        {
            ReplayData replay = ReadAiReplay();
            // Seed changed: the RNG state is part of the very first hash.
            ReplayData seedChanged = new ReplayData(replay.ContentDigest, replay.Seed + 1, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Assert.That(Check(WriteReplay(seedChanged, "seed.rtsreplay"), _dataDir, out string seedOut, out string seedErr), Is.EqualTo(1), seedErr);
            Assert.That(seedOut, Does.StartWith("REPLAY DESYNC tick=0 "));
            // The first script command (the HQ spawn) removed: nothing exists to play, so the first checkpoint already differs.
            List<Command> withoutFirst = new List<Command>(replay.Commands);
            withoutFirst.RemoveAt(0);
            ReplayData dropped = new ReplayData(replay.ContentDigest, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, withoutFirst, replay.Checkpoints, replay.FinalHash);
            Assert.That(Check(WriteReplay(dropped, "dropped.rtsreplay"), _dataDir, out string dropOut, out string dropErr), Is.EqualTo(1), dropErr);
            Assert.That(dropOut, Does.StartWith("REPLAY DESYNC tick=0 "));
        }

        /// <summary>A wrong recorded final hash with matching checkpoints is a desync reported at the tick count.</summary>
        [Test]
        public void AWrongFinalHashIsReportedAtTheTickCount()
        {
            ReplayData replay = ReadAiReplay();
            ReplayData changed = new ReplayData(replay.ContentDigest, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash ^ 1UL);
            Assert.That(Check(WriteReplay(changed, "final.rtsreplay"), _dataDir, out string output, out string error), Is.EqualTo(1), error);
            Assert.That(output, Does.StartWith("REPLAY DESYNC tick=" + replay.TickCount.ToString(CultureInfo.InvariantCulture) + " "));
        }

        /// <summary>A replay with a different format version is refused (exit 2) with a message naming the versions.</summary>
        [Test]
        public void ADifferentFormatVersionIsRefused()
        {
            byte[] bytes = File.ReadAllBytes(_aiReplayPath);
            bytes[8] = 9;
            string path = Path.Combine(_folder, "version9.rtsreplay");
            File.WriteAllBytes(path, bytes);
            Assert.That(Check(path, _dataDir, out string output, out string error), Is.EqualTo(2));
            Assert.That(output, Is.Empty);
            Assert.That(error, Does.StartWith("REPLAY REFUSED: ").And.Contain("format version 9").And.Contain("reads version 1"));
        }

        /// <summary>A replay with a different content digest is refused before any step runs, and the message names both digests.</summary>
        [Test]
        public void ADifferentDataDigestIsRefused()
        {
            ReplayData replay = ReadAiReplay();
            ReplayData otherDigest = new ReplayData(replay.ContentDigest ^ 0xFFUL, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Assert.That(Check(WriteReplay(otherDigest, "digest.rtsreplay"), _dataDir, out string output, out string error), Is.EqualTo(2));
            Assert.That(output, Is.Empty);
            // The replay claims the altered digest; the data in use has the real one. Both appear in the message.
            Assert.That(error, Does.StartWith("REPLAY REFUSED: ")
                .And.Contain("recorded with data digest " + otherDigest.ContentDigest.ToString("x16", CultureInfo.InvariantCulture))
                .And.Contain("has digest " + replay.ContentDigest.ToString("x16", CultureInfo.InvariantCulture)));
        }

        /// <summary>The same replay checked against a data folder with one edited tuning file is refused (exit 2), not reported as a desync.</summary>
        [Test]
        public void EditedDataIsRefused()
        {
            // Copy of the data folder with one tuning value changed.
            string editedData = Path.Combine(_folder, "edited_data");
            ReplayTestSupport.CopyDirectory(_dataDir, editedData);
            // Tuning file edited, and its text before and after.
            string tuningFile = Path.Combine(editedData, "tuning", "harness.json");
            string text = File.ReadAllText(tuningFile);
            Assert.That(text, Does.Contain("\"hq_floor_seconds\": 20"));
            File.WriteAllText(tuningFile, text.Replace("\"hq_floor_seconds\": 20", "\"hq_floor_seconds\": 21"));
            Assert.That(Check(_aiReplayPath, editedData, out string output, out string error), Is.EqualTo(2));
            Assert.That(output, Is.Empty);
            Assert.That(error, Does.Contain("data digest"));
        }

        /// <summary>Unreadable inputs exit with code 2 and a message: damaged replay, missing replay, missing data folder, bad command line.</summary>
        [Test]
        public void UnreadableInputsExitWithRefusal()
        {
            byte[] bytes = File.ReadAllBytes(_aiReplayPath);
            bytes[60] ^= 0x10;
            string damaged = Path.Combine(_folder, "damaged.rtsreplay");
            File.WriteAllBytes(damaged, bytes);
            Assert.That(Check(damaged, _dataDir, out _, out string damagedErr), Is.EqualTo(2));
            Assert.That(damagedErr, Does.Contain("checksum"));
            Assert.That(Check(Path.Combine(_folder, "missing.rtsreplay"), _dataDir, out _, out string missingErr), Is.EqualTo(2));
            Assert.That(missingErr, Does.Contain("not found"));
            Assert.That(Check(_aiReplayPath, Path.Combine(_folder, "no_such_data"), out _, out string dataErr), Is.EqualTo(2));
            Assert.That(dataErr, Does.Contain("data folder not found"));
            Assert.That(CheckProgram.Run(new string[0], new StringWriter(), new StringWriter()), Is.EqualTo(2));
        }

        /// <summary>The recorder drops commands scheduled after the last stepped tick and keeps the ones that ran.</summary>
        [Test]
        public void RecorderKeepsOnlyCommandsThatRan()
        {
            DataSet data = ReplayTestSupport.LoadData();
            CommandScript script = ReplayTestSupport.LoadScript("smoke.json");
            // 150 ticks stepped, so the commands at ticks 150 and later (noop at 150, moves at 200 and 300, removal at 250) never ran.
            ReplayData replay = ReplayTestSupport.RecordScript(data, script, 150);
            Assert.That(replay.TickCount, Is.EqualTo(150));
            Assert.That(replay.Commands.Count, Is.EqualTo(9), "4 spawns, 4 scripted moves and the player move at tick 100");
            foreach (Command command in replay.Commands)
            {
                Assert.That(command.Tick, Is.LessThan(150));
            }
            // Smoke script has 600 ticks; a full recording keeps all 13 commands.
            Assert.That(ReplayTestSupport.RecordScript(data, script, 600).Commands.Count, Is.EqualTo(13));
        }

        /// <summary>A recording made in the library verifies, and the same data and seed always give the same bytes (determinism of the format).</summary>
        [Test]
        public void LibraryRecordingVerifiesAndIsReproducible()
        {
            DataSet data = ReplayTestSupport.LoadData();
            CommandScript script = ReplayTestSupport.LoadScript("smoke.json");
            ReplayData first = ReplayTestSupport.RecordScript(data, script, 300);
            ReplayData second = ReplayTestSupport.RecordScript(data, script, 300);
            CommandCodecRegistry registry = CommandCodecRegistry.CreateBuiltIn();
            Assert.That(ReplayWriter.Write(second, registry), Is.EqualTo(ReplayWriter.Write(first, registry)));
            ReplayVerification result = ReplayVerifier.Verify(first, ReplayVerifier.CreateSimulation(first, data));
            Assert.That(result.IsMatch, Is.True);
            Assert.That(result.FinalHash, Is.EqualTo(first.FinalHash));
            Assert.That(result.DesyncTick, Is.EqualTo(-1));
        }

        /// <summary>Verify refuses a different data digest at library level, and a recorder refuses a simulation that is not at tick 0.</summary>
        [Test]
        public void LibraryGuards()
        {
            DataSet data = ReplayTestSupport.LoadData();
            CommandScript script = ReplayTestSupport.LoadScript("smoke.json");
            ReplayData replay = ReplayTestSupport.RecordScript(data, script, 40);
            ReplayData other = new ReplayData(replay.ContentDigest + 1, replay.Seed, replay.MapId, replay.HashIntervalTicks, replay.TickCount,
                replay.Players, replay.Commands, replay.Checkpoints, replay.FinalHash);
            Simulation fresh = ReplayVerifier.CreateSimulation(other, data);
            Expect.Throws<ReplayRefusedException>(() => ReplayVerifier.Verify(other, fresh));
            // A simulation that has already stepped cannot be verified or recorded.
            Simulation stepped = ReplayTestSupport.StartSimulation(data, script);
            stepped.Step();
            Expect.Throws<InvalidOperationException>(() => ReplayVerifier.Verify(replay, stepped));
            Expect.Throws<InvalidOperationException>(() => new ReplayRecorder(stepped, script.Players));
            // Stepping around the recorder leaves checkpoints missing, which Finish reports instead of writing a replay that cannot verify.
            Simulation direct = ReplayTestSupport.StartSimulation(data, script);
            ReplayRecorder recorder = new ReplayRecorder(direct, script.Players);
            direct.Step(40);
            Expect.Throws<InvalidOperationException>(() => recorder.Finish());
        }
    }
}
