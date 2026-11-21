// rts/tests/Sim.Tests/Campaign/CampaignTests.cs
// Acceptance tests of the campaign scripting layer (rts/docs/specs/campaign_scripting.md section 8): the tutorial runs headless to its last
// objective, determinism, hands off the simulation, the scripted command path, signal latching and event counting, timeouts, defeat, loss beats win,
// validation, AI slots, the cycles option, plus tests of CommandScript.ParseCommandObject. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Ai;
using Rts.Campaign;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

namespace Rts.Sim.Tests.Campaign
{
    /// <summary>The campaign tests.</summary>
    [TestFixture]
    public sealed class CampaignTests
    {
        /// <summary>Lines of a tool output that start with the given text.</summary>
        private static List<string> Lines(string output, string prefix)
        {
            return output.Split('\n').Where(line => line.StartsWith(prefix, System.StringComparison.Ordinal)).ToList();
        }

        /// <summary>Tick written after "tick=" in a log line.</summary>
        private static int TickOf(string line)
        {
            // Text after tick=, up to the next space.
            string rest = line.Substring(line.IndexOf("tick=", System.StringComparison.Ordinal) + 5);
            int space = rest.IndexOf(' ');
            return int.Parse(space < 0 ? rest : rest.Substring(0, space), System.Globalization.CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ 1 tutorial

        /// <summary>Acceptance 1: the shipped tutorial runs headless to its last objective with the shipped inputs.</summary>
        [Test]
        public void TutorialRunsHeadlessToItsLastObjective()
        {
            int code = CampaignTestSupport.RunTool(CampaignTestSupport.ToolArgs(CampaignTestSupport.CampaignDir, "tutorial_01"), out string output, out string error);
            Assert.That(code, Is.EqualTo(0), error);
            Assert.That(output, Does.Contain("OBJECTIVE look_around complete tick=20\n"));
            Assert.That(output, Does.Contain("OBJECTIVE select_rider active tick=21\n"));
            Assert.That(output, Does.Contain("OBJECTIVE select_rider complete tick=31\n"));
            Assert.That(output, Does.Contain("OBJECTIVE move_rider active tick=32\n"));
            // Objective completions in file order.
            int move = output.IndexOf("OBJECTIVE move_rider complete", System.StringComparison.Ordinal);
            int gather = output.IndexOf("OBJECTIVE gather complete", System.StringComparison.Ordinal);
            int roost = output.IndexOf("OBJECTIVE build_roost complete", System.StringComparison.Ordinal);
            Assert.That(move, Is.GreaterThan(0));
            Assert.That(gather, Is.GreaterThan(move));
            Assert.That(roost, Is.GreaterThan(gather));
            // The won line and its tick.
            List<string> won = Lines(output, "MISSION won");
            Assert.That(won.Count, Is.EqualTo(1));
            Assert.That(TickOf(won[0]), Is.InRange(700, 1500));
            // Six message cues, in trigger order.
            string[] expected =
            {
                "Welcome, rider.", "Left-click a unit", "Right-click the ground", "A new Fledgling hatches.", "Select a Fledgling, open the build menu", "Well flown.",
            };
            List<string> cues = Lines(output, "CUE show_message");
            Assert.That(cues.Count, Is.EqualTo(6));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(cues[i], Does.Contain(expected[i]));
            }
            Assert.That(Lines(output, "WARN"), Is.Empty);
            Assert.That(Lines(output, "STATE_HASH="), Has.Count.EqualTo(1));
        }

        // ------------------------------------------------------------ 2 determinism

        /// <summary>Acceptance 2: two tool runs give byte-identical stdout and equal STATE_HASH.</summary>
        [Test]
        public void TwoRunsAreByteIdentical()
        {
            string[] args = CampaignTestSupport.ToolArgs(CampaignTestSupport.CampaignDir, "tutorial_01");
            CampaignTestSupport.RunTool(args, out string first, out _);
            CampaignTestSupport.RunTool(args, out string second, out _);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(Lines(first, "STATE_HASH=")[0], Has.Length.EqualTo("STATE_HASH=".Length + 16));
            Assert.That(Lines(first, "tick="), Is.Not.Empty, "per-interval hash lines are printed");
        }

        // ------------------------------------------------------------ 3 hands off

        /// <summary>Acceptance 3: AfterStep leaves the state hash and the RNG alone, issues commands only as the script issuer, and a signal changes no hash.</summary>
        [Test]
        public void RunnerHandsOffTheSimulation()
        {
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.TutorialText, "tutorial_01");
            MissionInputs inputs = MissionInputs.Parse(CampaignTestSupport.TutorialInputsText, "campaign/inputs/tutorial_01.json", mission, CampaignTestSupport.Data);
            Simulation sim = mission.CreateSimulation(CampaignTestSupport.Data);
            sim.Start();
            MissionRunner runner = new MissionRunner(mission, null);
            runner.Start(sim);
            // Commands the runner submitted so far (queue total minus the inputs'). Unit: commands.
            long inputCommands = 0;
            for (int i = 0; i < 400 && runner.Status == MissionStatus.Running; i++)
            {
                long enqueuedBefore = sim.Commands.TotalEnqueued;
                inputs.BeforeStep(sim, runner);
                inputCommands += sim.Commands.TotalEnqueued - enqueuedBefore;
                sim.Step();
                // State read before AfterStep.
                ulong hash = sim.ComputeStateHash();
                ulong draws = sim.Rng.DrawCount;
                long total = sim.Commands.TotalEnqueued;
                runner.ReportSignal(new CameraMovedSignal(Fix64.FromInt(32), Fix64.FromInt(32)));
                Assert.That(sim.ComputeStateHash(), Is.EqualTo(hash), "ReportSignal changes no hash");
                runner.AfterStep(sim);
                Assert.That(sim.ComputeStateHash(), Is.EqualTo(hash), "AfterStep leaves the state alone (tick " + i + ")");
                Assert.That(sim.Rng.DrawCount, Is.EqualTo(draws));
                Assert.That(sim.Commands.TotalEnqueued - total, Is.GreaterThanOrEqualTo(0));
            }
            // Only the script issuer has a sequence counter: the runner never owns a human or AI slot.
            Assert.That(runner.NextSequence.Keys.ToList(), Is.EqualTo(new List<PlayerId> { PlayerId.None }));
            Assert.That(inputCommands, Is.GreaterThan(0));
        }

        // ------------------------------------------------------------ 4 scripted command path

        /// <summary>Acceptance 4: hatch_helper submits exactly one script command (sequence 4, after the four setup commands), applied the tick after move_rider completes.</summary>
        [Test]
        public void HatchHelperUsesTheScriptIssuerAndSequenceFour()
        {
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.TutorialText, "tutorial_01");
            MissionInputs inputs = MissionInputs.Parse(CampaignTestSupport.TutorialInputsText, "campaign/inputs/tutorial_01.json", mission, CampaignTestSupport.Data);
            // Tick on which entity 5 was reported spawned. Range: -1 until seen.
            int spawnedAt = -1;
            // Definition and owner of the spawned unit.
            string definition = string.Empty;
            PlayerId owner = PlayerId.None;
            // Next sequence of the script issuer once the setup is done.
            uint afterSetup = 0;
            Simulation sim = CampaignTestSupport.Drive(mission, inputs, 300, (tick, s, r) =>
            {
                if (tick == 0)
                {
                    afterSetup = r.NextSequence[PlayerId.None];
                }
            }, (tick, s, r) =>
            {
                foreach (SimEvent simEvent in s.Events.Current)
                {
                    if (simEvent is UnitSpawnedEvent spawned && spawned.Unit.Value == 5)
                    {
                        spawnedAt = spawned.Tick;
                        definition = spawned.DefinitionId;
                        owner = spawned.Owner;
                    }
                }
            }, out MissionRunner runner);
            Assert.That(afterSetup, Is.EqualTo(4u));
            Assert.That(runner.NextSequence[PlayerId.None], Is.EqualTo(5u), "exactly one command after the four setup commands");
            Assert.That(spawnedAt, Is.EqualTo(runner.Objectives[2].CompletedTick + 1));
            Assert.That(definition, Is.EqualTo("skai.fledgling"));
            Assert.That(owner, Is.EqualTo(new PlayerId(0)));
            Assert.That(sim.Units.Contains(new EntityId(5)), Is.True);
        }

        // ------------------------------------------------------------ 5 latch and count

        /// <summary>Acceptance 5 (signals): a camera signal before the condition is counting does not count later; one while active completes it that tick.</summary>
        [Test]
        public void SignalsLatchOnlyWhileTheObjectiveIsActive()
        {
            string objectives = "[ { \"id\": \"wait\", \"text\": \"Wait.\", \"requires\": [], \"complete_when\": { \"type\": \"seconds_active\", \"seconds\": 2 } },"
                + " { \"id\": \"look\", \"text\": \"Look.\", \"requires\": [\"wait\"], \"complete_when\": { \"type\": \"camera_in_area\", \"x\": \"32\", \"y\": \"32\", \"radius_tiles\": \"6\" } } ]";
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.Mission("latch_test", objectives, "[]", "look"), "latch_test");
            CampaignTestSupport.Drive(mission, null, 60, (tick, s, r) =>
            {
                // Signals at ticks 0 and 10 arrive while look is still locked; a third at tick 60 arrives while it is active.
                if (tick == 0 || tick == 10 || tick == 60)
                {
                    r.ReportSignal(new CameraMovedSignal(Fix64.FromInt(32), Fix64.FromInt(32)));
                }
                if (tick == 59)
                {
                    Assert.That(r.Objectives[1].Status, Is.EqualTo(ObjectiveStatus.Active), "the early signals did not count");
                }
            }, null, out MissionRunner early);
            Assert.That(early.Objectives[1].Status, Is.EqualTo(ObjectiveStatus.Active), "stopped before tick 60 ran");
            // Run again to tick 61: the signal at 60 completes the objective on tick 60.
            CampaignTestSupport.Drive(mission, null, 61, (tick, s, r) =>
            {
                if (tick == 10 || tick == 60)
                {
                    r.ReportSignal(new CameraMovedSignal(Fix64.FromInt(32), Fix64.FromInt(32)));
                }
            }, null, out MissionRunner runner);
            Assert.That(runner.Objectives[1].Status, Is.EqualTo(ObjectiveStatus.Completed));
            Assert.That(runner.Objectives[1].CompletedTick, Is.EqualTo(60));
            Assert.That(runner.Status, Is.EqualTo(MissionStatus.Won));
        }

        /// <summary>Acceptance 5 (events): events of the tick an objective activates do not count; events of later ticks do.</summary>
        [Test]
        public void EventsCountFromTheTickAfterActivation()
        {
            string objectives = "[ { \"id\": \"first\", \"text\": \"First.\", \"requires\": [], \"complete_when\": { \"type\": \"event_count\", \"event\": \"unit_spawned\", \"owner\": 0, \"min_count\": 4 } },"
                + " { \"id\": \"second\", \"text\": \"Second.\", \"requires\": [\"first\"], \"complete_when\": { \"type\": \"event_count\", \"event\": \"unit_spawned\", \"owner\": 0, \"min_count\": 1 } } ]";
            // Without a trigger nothing spawns after tick 0: the four spawns of tick 0 completed first and must not count for second.
            MissionDefinition quiet = CampaignTestSupport.Parse(CampaignTestSupport.Mission("count_quiet", objectives, "[]", "second"), "count_quiet");
            CampaignTestSupport.Drive(quiet, null, 40, null, null, out MissionRunner quietRunner);
            Assert.That(quietRunner.Objectives[0].CompletedTick, Is.EqualTo(0));
            Assert.That(quietRunner.Objectives[1].Status, Is.EqualTo(ObjectiveStatus.Active));
            Assert.That(quietRunner.Objectives[1].FirstCountingTick, Is.EqualTo(1));
            // With a trigger that spawns a unit when second activates, the spawn on the next tick counts.
            string trigger = "[ { \"id\": \"hatch\", \"when\": { \"objective_activated\": \"second\" }, \"actions\": [ { \"type\": \"commands\", \"commands\": ["
                + " { \"type\": \"spawn_unit\", \"definition\": \"skai.fledgling\", \"owner\": 0, \"x\": \"8.5\", \"y\": \"9.5\" } ] } ] } ]";
            MissionDefinition loud = CampaignTestSupport.Parse(CampaignTestSupport.Mission("count_loud", objectives, trigger, "second"), "count_loud");
            CampaignTestSupport.Drive(loud, null, 40, null, null, out MissionRunner loudRunner);
            Assert.That(loudRunner.Objectives[1].CompletedTick, Is.EqualTo(1));
            Assert.That(loudRunner.Status, Is.EqualTo(MissionStatus.Won));
        }

        // ------------------------------------------------------------ 6 timeout

        /// <summary>Acceptance 6: without the move_unit input the run stays on move_rider and, capped at 2000 ticks, reports a timeout with exit 2.</summary>
        [Test]
        public void MissingMoveInputLeavesMoveRiderActiveAndTimesOutAtTheCap()
        {
            string inputs = CampaignTestSupport.TutorialInputsText;
            string withoutMove = string.Join("\n", inputs.Split('\n').Where(line => !line.Contains("\"move_unit\"")));
            Assert.That(withoutMove, Is.Not.EqualTo(inputs));
            string folder = CampaignTestSupport.TempCampaign(new Dictionary<string, string> { { "tutorial_01", CampaignTestSupport.TutorialText } },
                new Dictionary<string, string> { { "tutorial_01", withoutMove } });
            int code = CampaignTestSupport.RunTool(CampaignTestSupport.ToolArgs(folder, "tutorial_01", "--ticks", "2000"), out string output, out string error);
            Assert.That(code, Is.EqualTo(2), error);
            Assert.That(output, Does.Contain("OBJECTIVE move_rider active tick=32"));
            Assert.That(output, Does.Not.Contain("OBJECTIVE move_rider complete"));
            Assert.That(output, Does.Contain("MISSION timeout tick=1999\n"));
        }

        /// <summary>Acceptance 6: with max_seconds 30 and no cap the mission times out on tick 599 with exit 2.</summary>
        [Test]
        public void ShortTimeLimitTimesOutOnTick599()
        {
            string shortMission = CampaignTestSupport.TutorialText.Replace("\"max_seconds\": 900", "\"max_seconds\": 30");
            Assert.That(shortMission, Is.Not.EqualTo(CampaignTestSupport.TutorialText));
            string folder = CampaignTestSupport.TempCampaign(new Dictionary<string, string> { { "tutorial_01", shortMission } },
                new Dictionary<string, string> { { "tutorial_01", CampaignTestSupport.TutorialInputsText } });
            int code = CampaignTestSupport.RunTool(CampaignTestSupport.ToolArgs(folder, "tutorial_01"), out string output, out string error);
            Assert.That(code, Is.EqualTo(2), error);
            Assert.That(Lines(output, "MISSION"), Is.EqualTo(new List<string> { "MISSION timeout tick=599" }));
        }

        // ------------------------------------------------------------ 7 and 8 defeat, loss beats win

        /// <summary>Acceptance 7: a trigger that removes the HQ on mission_started loses the mission on tick 0 with reason defeat, exit 2.</summary>
        [Test]
        public void RemovingTheHqLosesWithDefeatOnTickZero()
        {
            string triggers = "[ { \"id\": \"kill_hq\", \"when\": { \"mission_started\": true }, \"actions\": [ { \"type\": \"commands\", \"issuer\": \"script\", \"commands\": ["
                + " { \"type\": \"remove_unit\", \"unit\": 1 } ] } ] } ]";
            string text = CampaignTestSupport.Mission("defeat_test", CampaignTestSupport.NeverObjectives, triggers, "never",
                ", \"defeat_when\": { \"type\": \"player_state\", \"player\": 0, \"state\": \"defeated\" }");
            string folder = CampaignTestSupport.TempCampaign(new Dictionary<string, string> { { "defeat_test", text } }, new Dictionary<string, string>());
            int code = CampaignTestSupport.RunTool(CampaignTestSupport.ToolArgs(folder, "defeat_test"), out string output, out string error);
            Assert.That(code, Is.EqualTo(2), error);
            Assert.That(Lines(output, "MISSION"), Is.EqualTo(new List<string> { "MISSION lost tick=0 reason=defeat" }));
        }

        /// <summary>Acceptance 8: when one tick completes the victory objective and a trigger loses the mission, the mission is lost.</summary>
        [Test]
        public void LossBeatsWinOnTheSameTick()
        {
            string objectives = "[ { \"id\": \"done\", \"text\": \"Done.\", \"requires\": [], \"complete_when\": { \"type\": \"event_count\", \"event\": \"unit_spawned\", \"min_count\": 1 } } ]";
            string triggers = "[ { \"id\": \"fail\", \"when\": { \"objective_completed\": \"done\" }, \"actions\": [ { \"type\": \"lose_mission\", \"reason\": \"test_loss\" } ] } ]";
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.Mission("loss_wins", objectives, triggers, "done"), "loss_wins");
            CampaignTestSupport.Drive(mission, null, 10, null, null, out MissionRunner runner);
            Assert.That(runner.Objectives[0].Status, Is.EqualTo(ObjectiveStatus.Completed));
            Assert.That(runner.Status, Is.EqualTo(MissionStatus.Lost));
            Assert.That(runner.EndReason, Is.EqualTo("test_loss"));
        }

        // ------------------------------------------------------------ 9 validation

        /// <summary>Parses mutated tutorial text and returns the validation error; the replaced text must exist.</summary>
        private static DataValidationException MissionError(string from, string to)
        {
            string text = CampaignTestSupport.TutorialText;
            Assert.That(text, Does.Contain(from));
            string mutated = text.Replace(from, to);
            return Expect.Throws<DataValidationException>(() => CampaignTestSupport.Parse(mutated, "tutorial_01"));
        }

        /// <summary>Acceptance 9: requires naming a later objective is an error at objectives[1].requires[0].</summary>
        [Test]
        public void RequiresMustNameAnEarlierObjective()
        {
            DataValidationException error = MissionError("\"requires\": [\"look_around\"]", "\"requires\": [\"gather\"]");
            Assert.That(error.Field, Is.EqualTo("objectives[1].requires[0]"));
            Assert.That(error.File, Does.EndWith("tutorial_01.json"));
        }

        /// <summary>Acceptance 9: a duplicate objective ID is an error.</summary>
        [Test]
        public void DuplicateObjectiveIdIsRejected()
        {
            DataValidationException error = MissionError("\"id\": \"select_rider\"", "\"id\": \"look_around\"");
            Assert.That(error.Field, Is.EqualTo("objectives[1].id"));
        }

        /// <summary>Acceptance 9: an unknown condition type is an error.</summary>
        [Test]
        public void UnknownConditionTypeIsRejected()
        {
            DataValidationException error = MissionError("\"type\": \"units_in_area\"", "\"type\": \"unit_count\"");
            Assert.That(error.Field, Is.EqualTo("objectives[2].complete_when.type"));
        }

        /// <summary>Acceptance 9: a commands action issued as a human slot is an error.</summary>
        [Test]
        public void CommandsActionCannotIssueAsAHumanSlot()
        {
            DataValidationException error = MissionError("\"issuer\": \"script\"", "\"issuer\": 0");
            Assert.That(error.Field, Is.EqualTo("triggers[3].actions[1].issuer"));
        }

        /// <summary>Acceptance 9: a bad coordinate in a command object names the full field path.</summary>
        [Test]
        public void BadCoordinateNamesTheCommandField()
        {
            DataValidationException error = MissionError("\"x\": \"8.5\", \"y\": \"9.5\"", "\"x\": \"abc\", \"y\": \"9.5\"");
            Assert.That(error.Field, Is.EqualTo("triggers[3].actions[1].commands[0].x"));
            Assert.That(error.Message, Does.Contain("tutorial_01.json"));
        }

        /// <summary>Acceptance 9: an unknown unit definition is an error.</summary>
        [Test]
        public void UnknownUnitDefinitionIsRejected()
        {
            DataValidationException error = MissionError("\"skai.great_aerie\"", "\"skai.nope\"");
            Assert.That(error.Field, Is.EqualTo("setup[0].definition"));
        }

        /// <summary>Acceptance 9: an inputs file naming an objective the mission does not have is an error.</summary>
        [Test]
        public void InputsMustNameAnObjectiveOfTheMission()
        {
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.TutorialText, "tutorial_01");
            string text = CampaignTestSupport.TutorialInputsText.Replace("\"on_objective\": \"look_around\"", "\"on_objective\": \"fly\"");
            DataValidationException error = Expect.Throws<DataValidationException>(() => MissionInputs.Parse(text, "campaign/inputs/tutorial_01.json", mission, CampaignTestSupport.Data));
            Assert.That(error.Field, Is.EqualTo("inputs[0].on_objective"));
        }

        /// <summary>Acceptance 9: a validation error through the tool gives exit 1 and a message with the file and field.</summary>
        [Test]
        public void ToolReportsValidationErrorsWithExitOne()
        {
            string mutated = CampaignTestSupport.TutorialText.Replace("\"skai.great_aerie\"", "\"skai.nope\"");
            string folder = CampaignTestSupport.TempCampaign(new Dictionary<string, string> { { "tutorial_01", mutated } }, new Dictionary<string, string>());
            int code = CampaignTestSupport.RunTool(CampaignTestSupport.ToolArgs(folder, "tutorial_01"), out string output, out string error);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output, Is.Empty);
            Assert.That(error, Does.Contain("tutorial_01.json").And.Contain("setup[0].definition"));
        }

        /// <summary>The id must equal the file stem, and a command object must not carry tick, issuer or sequence.</summary>
        [Test]
        public void IdMustEqualTheFileStemAndCommandsMustNotCarryATick()
        {
            DataValidationException id = Expect.Throws<DataValidationException>(() => CampaignTestSupport.Parse(CampaignTestSupport.TutorialText, "other_name"));
            Assert.That(id.Field, Is.EqualTo("id"));
            DataValidationException tick = MissionError("\"definition\": \"skai.great_aerie\", \"owner\": 0,", "\"definition\": \"skai.great_aerie\", \"tick\": 3, \"owner\": 0,");
            Assert.That(tick.Field, Is.EqualTo("setup[0].tick"));
        }

        /// <summary>The shipped tutorial parses, and every shipped mission file loads against the shipped data.</summary>
        [Test]
        public void ShippedMissionsParse()
        {
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.TutorialText, "tutorial_01");
            Assert.That(mission.Objectives.Count, Is.EqualTo(5));
            Assert.That(mission.Triggers.Count, Is.EqualTo(6));
            Assert.That(mission.Objectives[mission.VictoryObjective].Id, Is.EqualTo("build_roost"));
        }

        // ------------------------------------------------------------ 10 AI slot

        /// <summary>Acceptance 10: an "ai:defensive" slot gets a controller whose commands carry issuer 1; the runner never issues as slot 1.</summary>
        [Test]
        public void AiSlotCommandsCarryTheAiIssuerAndTheRunnerNeverIssuesAsIt()
        {
            string players = "[ { \"id\": 0, \"team\": 0, \"faction\": \"skai\", \"name\": \"You\", \"controller\": \"human\" },"
                + " { \"id\": 1, \"team\": 1, \"faction\": \"water\", \"name\": \"Water\", \"controller\": \"ai:defensive\" } ]";
            string setup = "[ { \"type\": \"spawn_unit\", \"definition\": \"skai.great_aerie\", \"owner\": 0, \"x\": \"7\", \"y\": \"7\" },"
                + " { \"type\": \"spawn_unit\", \"definition\": \"water.grand_shell\", \"owner\": 1, \"x\": \"58\", \"y\": \"58\" },"
                + " { \"type\": \"spawn_unit\", \"definition\": \"water.tender\", \"owner\": 1, \"x\": \"57\", \"y\": \"56\" },"
                + " { \"type\": \"spawn_unit\", \"definition\": \"water.tender\", \"owner\": 1, \"x\": \"56\", \"y\": \"57\" } ]";
            MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.Mission("ai_test", CampaignTestSupport.NeverObjectives, "[]", "never", players: players, setup: setup), "ai_test");
            Assert.That(mission.Players[1].AiProfile, Is.EqualTo("defensive"));
            Simulation sim = mission.CreateSimulation(CampaignTestSupport.Data);
            sim.Start();
            MissionRunner runner = new MissionRunner(mission, null);
            runner.Start(sim);
            List<IPlayerController> controllers = new List<IPlayerController>
            {
                new SkirmishAiController(new PlayerId(1), AiProfile.FromDataSet(CampaignTestSupport.Data, "defensive")),
            };
            AiHost.StartAll(sim, controllers);
            // Commands the AI submitted in total, and how many carried another issuer.
            int aiCommands = 0;
            int wrongIssuer = 0;
            for (int i = 0; i < 400; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                foreach (Command command in ((SkirmishAiController)controllers[0]).LastCommands)
                {
                    aiCommands++;
                    wrongIssuer += command.Issuer == new PlayerId(1) ? 0 : 1;
                }
                sim.Step();
                runner.AfterStep(sim);
            }
            Assert.That(aiCommands, Is.GreaterThan(0), "the AI played");
            Assert.That(wrongIssuer, Is.EqualTo(0));
            Assert.That(runner.NextSequence.ContainsKey(new PlayerId(1)), Is.False);
            Assert.That(runner.NextSequence.ContainsKey(new PlayerId(0)), Is.False);
        }

        // ------------------------------------------------------------ 11 cycles option

        /// <summary>Acceptance 11: planetary_cycles true shows cycle_warning on tick 3600 of a five-minute mission; false shows none.</summary>
        [Test]
        public void CyclesOptionControlsTheWarning()
        {
            foreach (bool cycles in new[] { true, false })
            {
                MissionDefinition mission = CampaignTestSupport.Parse(CampaignTestSupport.Mission("cycles_test", CampaignTestSupport.NeverObjectives, "[]", "never", maxSeconds: 300, cycles: cycles),
                    "cycles_test");
                // Ticks on which a cycle_warning was reported.
                List<int> warnings = new List<int>();
                CampaignTestSupport.Drive(mission, null, 3700, null, (tick, s, r) =>
                {
                    foreach (SimEvent simEvent in s.Events.Current)
                    {
                        if (simEvent is CycleWarningEvent)
                        {
                            warnings.Add(simEvent.Tick);
                        }
                    }
                }, out MissionRunner runner);
                Assert.That(warnings, cycles ? Is.EqualTo(new List<int> { 3600 }) : Is.Empty);
                Assert.That(runner.EndReason, Is.EqualTo("timeout").Or.EqualTo(string.Empty));
            }
        }

        // ------------------------------------------------------------ CommandScript.ParseCommandObject

        /// <summary>ParseCommandObject builds the command with the given tick, issuer and sequence, accepts faction commands, and rejects supplied tick, issuer and sequence.</summary>
        [Test]
        public void ParseCommandObjectUsesTheSuppliedHeader()
        {
            JsonValue move = JsonReader.Parse("{ \"type\": \"move_unit\", \"unit\": 4, \"x\": \"18.5\", \"y\": 8 }", "test.json");
            Command command = CommandScript.ParseCommandObject(move, 77, new PlayerId(3), 9, "test.json", BuiltInMechanics.ScriptParsers);
            Assert.That(command, Is.TypeOf<MoveUnitCommand>());
            Assert.That(command.Tick, Is.EqualTo(77));
            Assert.That(command.Issuer, Is.EqualTo(new PlayerId(3)));
            Assert.That(command.Sequence, Is.EqualTo(9u));
            JsonValue wave = JsonReader.Parse("{ \"type\": \"cast_wave\", \"unit\": 4, \"x\": 1, \"y\": 2 }", "test.json");
            Assert.That(CommandScript.ParseCommandObject(wave, 0, PlayerId.None, 0, "test.json", BuiltInMechanics.ScriptParsers).Kind, Is.EqualTo("cast_wave"));
            JsonValue pick = JsonReader.Parse("{ \"type\": \"pick_cycle\", \"cycle\": \"quake\" }", "test.json");
            Assert.That(CommandScript.ParseCommandObject(pick, 0, PlayerId.None, 0, "test.json", BuiltInMechanics.ScriptParsers), Is.TypeOf<PickCycleCommand>());
            JsonValue withTick = JsonReader.Parse("{ \"type\": \"noop\", \"tick\": 5 }", "test.json");
            DataValidationException error = Expect.Throws<DataValidationException>(() => CommandScript.ParseCommandObject(withTick, 0, PlayerId.None, 0, "test.json", BuiltInMechanics.ScriptParsers));
            Assert.That(error.Field, Is.EqualTo("tick"));
        }
    }
}
