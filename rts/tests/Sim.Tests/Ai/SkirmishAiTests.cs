// rts/tests/Sim.Tests/Ai/SkirmishAiTests.cs
// Tests for the Phase 1 skirmish AI (rts/src/Ai, spec rts/docs/specs/skirmish_ai.md section 7): the AI generator seed, hands off
// the simulation, think cadence, the opening (gather and train within the first second), placement of the first structure,
// composition by largest deficit, the attack trigger, retreat and regroup, defence, never acting on enemies the team cannot see,
// profile validation with file-and-field errors and the loader's ai/ route, a 2000-tick AI-versus-AI match on the test economy
// map with no rejected command, and the Phase 1 exit test: two full runs of rts/data/scripts/ai_vs_ai.json on greybox_1v1 give
// identical hashes and a decided outcome within 6000 ticks. Part of Rts.Sim.Tests. Test code only (may use System.IO).
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
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Ai
{
    /// <summary>Skirmish AI behaviour and determinism.</summary>
    [TestFixture]
    public class SkirmishAiTests
    {
        /// <summary>Player slot 0 (Skai in the greybox scenarios).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water in the greybox scenarios).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The Skai start spawn on greybox_1v1 is (8, 8); the Water one, the attack target here, is (56, 56). Unit: tiles.</summary>
        private static readonly FixVec2 WaterSpawn = FixVec2.FromInts(56, 56);

        /// <summary>The real data folder, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set (every file under rts/data).</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawn commands. Unit: counter. Range: 0 upward.</summary>
        private uint _scriptSequence;

        // ---------------------------------------------------------------- helpers

        /// <summary>Text of a shipped AI profile file.</summary>
        private static string ProfileText(string id)
        {
            return File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", id + ".json"));
        }

        /// <summary>A shipped profile with text edits applied (each old text must occur once).</summary>
        private static AiProfile EditedProfile(string id, params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = ProfileText(id);
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, "profile text should contain " + oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/" + id + ".json", Data.Content);
        }

        /// <summary>The aggressive profile's numbers with <paramref name="factionsJson"/> as its factions object, validated against <paramref name="content"/>.</summary>
        private static AiProfile WithFactions(string factionsJson, ContentCatalog content)
        {
            // The shipped text and the start of its factions object.
            string text = ProfileText("aggressive");
            // Index where the factions object starts in the shipped text.
            int start = text.IndexOf("\"factions\": {", StringComparison.Ordinal);
            return AiProfile.Parse(text.Substring(0, start) + "\"factions\": " + factionsJson + " }", "ai/aggressive.json", content);
        }

        /// <summary>A plan object for the in-code test faction (TestWorld.Catalog): worker, barracks, runners.</summary>
        private const string TestFactionPlan = "{ \"test\": { \"worker_unit\": \"test.worker\", \"supply_building\": \"test.barracks\", "
            + "\"build_order\": [ { \"structure\": \"test.barracks\", \"count\": 2, \"at_supply_used\": 0 } ], \"composition\": { \"test.runner\": 1 }, "
            + "\"attack_tide_min\": 0, \"attack_tide_max\": 1, \"attack_tide_override_supply\": 20 } }";

        /// <summary>The aggressive profile with no jitter and no staging, so thresholds and targets are exact.</summary>
        private static AiProfile ExactAggressive(params string[] extra)
        {
            // Text edits: no jitter, no staging, then the caller's extra pairs.
            List<string> pairs = new List<string> { "\"attack_supply_jitter\": 2", "\"attack_supply_jitter\": 0", "\"stage_distance_tiles\": 14", "\"stage_distance_tiles\": 0" };
            pairs.AddRange(extra);
            return EditedProfile("aggressive", pairs.ToArray());
        }

        /// <summary>A started greybox_1v1 simulation (Skai slot 0, Water slot 1, real data) with nothing spawned yet.</summary>
        private static Simulation Greybox(ulong seed = 1234)
        {
            // The two player slots of the match.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            // The simulation under test.
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(seed), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("greybox_1v1")));
            sim.Start();
            return sim;
        }

        /// <summary>Spawns a unit through a script command on the next step and steps once; returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, string x, string y)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _scriptSequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
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

        /// <summary>Greybox with both HQs Rooted (Skai at (7, 7), Water at (57, 57)), no workers.</summary>
        private Simulation GreyboxWithHqs()
        {
            // The simulation under test.
            Simulation sim = Greybox();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            Spawn(sim, "water.grand_shell", P1, "57", "57");
            return sim;
        }

        /// <summary>Steps (with no commands) until <paramref name="controller"/> thinks on the current tick, then thinks into a list.</summary>
        private static List<Command> ThinkNow(Simulation sim, SkirmishAiController controller)
        {
            if (controller.Rng == null)
            {
                controller.OnMatchStart(sim);
            }
            while (!controller.IsThinkTick(sim.Clock.Tick))
            {
                sim.Step();
            }
            // Collects (or submits) the controller's commands.
            ListCommandSink sink = new ListCommandSink(sim.Clock.Tick);
            controller.Think(sim, sink);
            return sink.Commands;
        }

        /// <summary>The commands of type T among <paramref name="commands"/>, in order.</summary>
        private static List<T> OfType<T>(List<Command> commands) where T : Command
        {
            // Matching commands in order.
            List<T> found = new List<T>();
            foreach (Command command in commands)
            {
                if (command is T match)
                {
                    found.Add(match);
                }
            }
            return found;
        }

        /// <summary>Result of one AI-controlled run.</summary>
        private sealed class RunResult
        {
            /// <summary>Every StateHashedEvent hash in order.</summary>
            public List<ulong> Hashes { get; } = new List<ulong>();

            /// <summary>Rejections of AI-issued commands, as "tick kind reason".</summary>
            public List<string> Rejections { get; } = new List<string>();

            /// <summary>Violations of the vision rule found while the AIs played.</summary>
            public List<string> VisionViolations { get; } = new List<string>();

            /// <summary>Outcome after the last step.</summary>
            public MatchOutcome? Outcome { get; set; }

            /// <summary>Last tick stepped. Unit: ticks.</summary>
            public int LastTick { get; set; } = -1;

            /// <summary>Final state hash.</summary>
            public ulong FinalHash { get; set; }

            /// <summary>Commands the AIs submitted, all players together.</summary>
            public int CommandCount { get; set; }
        }

        /// <summary>
        /// Runs a started simulation with AI controllers the way the harness does (think in ascending slot order, then step) until
        /// the outcome is decided or <paramref name="maxTicks"/> ticks have run. Checks every AI command against the vision rule:
        /// an attack_unit target must be visible to the issuer's team, and an attack-move or move goal must be on a tile the team has
        /// explored or be a map spawn (public knowledge).
        /// </summary>
        private static RunResult RunAi(Simulation sim, List<SkirmishAiController> controllers, int maxTicks)
        {
            // What the run produced.
            RunResult result = new RunResult();
            foreach (SkirmishAiController controller in controllers)
            {
                controller.OnMatchStart(sim);
            }
            // Controllers in ascending player order.
            controllers.Sort((a, b) => a.Player.CompareTo(b.Player));
            // Loop counter. Unit: ticks or repetitions.
            for (int i = 0; i < maxTicks; i++)
            {
                // Collects (or submits) the controller's commands.
                SimulationCommandSink sink = new SimulationCommandSink(sim);
                foreach (SkirmishAiController controller in controllers)
                {
                    controller.Think(sim, sink);
                    result.CommandCount += controller.LastCommands.Count;
                    CheckVision(sim, controller, result.VisionViolations);
                }
                result.LastTick = sim.Clock.Tick;
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is StateHashedEvent hashed)
                    {
                        result.Hashes.Add(hashed.Hash);
                    }
                    else if (evt is CommandRejectedEvent rejected && rejected.Command.Issuer.IsValid)
                    {
                        result.Rejections.Add(rejected.Tick + " " + rejected.Command.Kind + " " + rejected.Reason);
                    }
                }
                result.Outcome = MatchOutcome.Evaluate(sim.Players);
                if (result.Outcome.IsDecided)
                {
                    break;
                }
            }
            result.FinalHash = sim.ComputeStateHash();
            return result;
        }

        /// <summary>Adds a line to <paramref name="violations"/> for each of the controller's last commands that uses hidden information.</summary>
        private static void CheckVision(Simulation sim, SkirmishAiController controller, List<string> violations)
        {
            // Team of the controller's player, whose vision is checked.
            TeamId team = sim.Players.TeamOf(controller.Player);
            foreach (Command command in controller.LastCommands)
            {
                if (command is AttackUnitCommand attack)
                {
                    if (!sim.Units.TryGet(attack.Target, out Unit? target) || !sim.Vision.IsUnitVisibleTo(team, target))
                    {
                        violations.Add(command + " names unseen " + attack.Target);
                    }
                    continue;
                }
                // Goal of a movement order, if this is one.
                FixVec2? goal = command is AttackMoveCommand am ? am.Goal : command is MoveUnitCommand mv ? mv.Target : (FixVec2?)null;
                if (goal == null)
                {
                    continue;
                }
                // True when the goal comes from public map data: a spawn point, or the staging point short of one (the spawn moved
                // stage_distance_tiles toward the own HQ), each give or take the 1.5-tile standable adjustment.
                bool isPublic = false;
                foreach (FixVec2 spawn in sim.Map.Spawns)
                {
                    isPublic |= (spawn - goal.Value).LengthSquared() <= Fix64.FromInt(3);
                    foreach (Unit own in sim.Units.All)
                    {
                        if (own.Owner == controller.Player && own.Definition.Hq != null)
                        {
                            // Staging point short of this spawn for this HQ. Unit: tiles.
                            FixVec2 stage = spawn.MoveTowards(own.Position, controller.Profile.StageDistanceTiles);
                            isPublic |= (stage - goal.Value).LengthSquared() <= Fix64.FromInt(3);
                        }
                    }
                }
                if (!isPublic && !sim.Vision.IsTileExplored(team, goal.Value.X.FloorToInt(), goal.Value.Y.FloorToInt()))
                {
                    violations.Add(command + " goes to unexplored " + goal.Value);
                }
            }
        }

        /// <summary>Parses rts/data/scripts/ai_vs_ai.json, builds and starts its simulation, submits its commands and creates its AI controllers.</summary>
        private static Simulation StartAiVsAi(out List<SkirmishAiController> controllers, out CommandScript script)
        {
            // Path of the exit script in the real data folder.
            string path = Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_vs_ai.json");
            script = CommandScript.Parse(File.ReadAllText(path), "scripts/ai_vs_ai.json", BuiltInMechanics.ScriptParsers);
            // The simulation under test.
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(script.Seed), Data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(script.MapId)));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            controllers = new List<SkirmishAiController>();
            foreach (KeyValuePair<PlayerId, string> pair in script.AiProfiles)
            {
                controllers.Add(new SkirmishAiController(pair.Key, AiProfile.FromDataSet(Data, pair.Value)));
            }
            return sim;
        }

        // ---------------------------------------------------------------- tests

        /// <summary>Spec test 1: the AI seed formula and its generator, which is the simulation's own PCG32 class seeded independently.</summary>
        [Test]
        public void Seed_FollowsSpecFormula_AndIsAPrivateGenerator()
        {
            Assert.That(SkirmishAiController.SeedFor(1234, P0), Is.EqualTo(0x9E3779B97F4A78C7UL));
            Assert.That(SkirmishAiController.SeedFor(1234, P1), Is.EqualTo(0x3C6EF372FE94FCF8UL));
            // The spec's pinned draws: player 0's first four NextInt(2), player 1's first NextInt(7).
            DeterministicRandom first = new DeterministicRandom(SkirmishAiController.SeedFor(1234, P0));
            Assert.That(new[] { first.NextInt(2), first.NextInt(2), first.NextInt(2), first.NextInt(2) }, Is.EqualTo(new[] { 0, 1, 1, 0 }));
            Assert.That(new DeterministicRandom(SkirmishAiController.SeedFor(1234, P1)).NextInt(7), Is.EqualTo(1));
            // The simulation under test.
            Simulation sim = Greybox(1234);
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, AiProfile.FromDataSet(Data, "aggressive"));
            controller.OnMatchStart(sim);
            // A generator seeded identically, for comparison.
            DeterministicRandom reference = new DeterministicRandom(0x9E3779B97F4A78C7UL);
            // Draw index. Range: 0 to 7.
            for (int i = 0; i < 8; i++)
            {
                Assert.That(controller.Rng!.NextInt(7), Is.EqualTo(reference.NextInt(7)));
            }
            Assert.That(sim.Rng.DrawCount, Is.EqualTo(0UL), "the AI never draws from the simulation's generator");
        }

        /// <summary>Spec test 2: thinking reads only; the simulation RNG, state hash and command queue are unchanged.</summary>
        [Test]
        public void Think_LeavesTheSimulationUntouched()
        {
            // The simulation under test.
            Simulation sim = StartAiVsAi(out List<SkirmishAiController> controllers, out _);
            RunAi(sim, controllers, 40);
            // The Water AI (slot 1).
            SkirmishAiController water = controllers[1];
            while (!water.IsThinkTick(sim.Clock.Tick))
            {
                sim.Step();
            }
            // Simulation RNG state before the think.
            ulong state = sim.Rng.State;
            // Simulation RNG draw count before the think.
            ulong draws = sim.Rng.DrawCount;
            // State hash before the think.
            ulong hash = sim.ComputeStateHash();
            // Commands waiting in the simulation queue before the think.
            int pending = sim.Commands.PendingCount;
            // Collects (or submits) the controller's commands.
            ListCommandSink sink = new ListCommandSink(sim.Clock.Tick);
            water.Think(sim, sink);
            Assert.That(water.Memory.ThinkCount, Is.GreaterThan(0));
            Assert.That(sim.Rng.State, Is.EqualTo(state));
            Assert.That(sim.Rng.DrawCount, Is.EqualTo(draws));
            Assert.That(sim.ComputeStateHash(), Is.EqualTo(hash));
            Assert.That(sim.Commands.PendingCount, Is.EqualTo(pending), "a list sink submits nothing");
        }

        /// <summary>Spec test 3: think_interval_seconds 0.5 is 10 ticks; player 1 thinks on ticks 9, 19, 29 and never on 10.</summary>
        [Test]
        public void Cadence_IsStaggeredByPlayerSlot()
        {
            // The simulation under test.
            Simulation sim = Greybox();
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P1, AiProfile.FromDataSet(Data, "defensive"));
            controller.OnMatchStart(sim);
            Assert.That(controller.ThinkTicks, Is.EqualTo(10));
            Assert.That(new[] { controller.IsThinkTick(9), controller.IsThinkTick(19), controller.IsThinkTick(29) }, Is.All.True);
            Assert.That(new[] { controller.IsThinkTick(0), controller.IsThinkTick(10), controller.IsThinkTick(20) }, Is.All.False);
        }

        /// <summary>The opening: within the first second each AI sends its four workers to gather and trains a worker at the Rooted HQ, all accepted.</summary>
        [Test]
        public void Opening_GathersAndTrainsWithinTheFirstSecond()
        {
            // The simulation under test.
            Simulation sim = StartAiVsAi(out List<SkirmishAiController> controllers, out _);
            // Commands per player in the first second.
            SortedDictionary<PlayerId, List<Command>> issued = new SortedDictionary<PlayerId, List<Command>> { { P0, new List<Command>() }, { P1, new List<Command>() } };
            foreach (SkirmishAiController controller in controllers)
            {
                controller.OnMatchStart(sim);
            }
            // Rejection descriptions seen.
            List<string> rejections = new List<string>();
            while (sim.Clock.Tick < sim.Clock.TicksPerSecond)
            {
                foreach (SkirmishAiController controller in controllers)
                {
                    controller.Think(sim, new SimulationCommandSink(sim));
                    issued[controller.Player].AddRange(controller.LastCommands);
                }
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is CommandRejectedEvent rejected)
                    {
                        rejections.Add(rejected.Describe());
                    }
                }
            }
            Assert.That(rejections, Is.Empty);
            foreach (KeyValuePair<PlayerId, List<Command>> pair in issued)
            {
                // Gather orders of this player.
                List<GatherCommand> gathers = OfType<GatherCommand>(pair.Value);
                // Train orders of this player.
                List<TrainUnitCommand> trains = OfType<TrainUnitCommand>(pair.Value);
                Assert.That(gathers.Count, Is.EqualTo(4), pair.Key + " sends its four workers to gather");
                Assert.That(trains.Count, Is.GreaterThanOrEqualTo(1), pair.Key + " trains a worker");
                Assert.That(trains[0].DefinitionId, Is.EqualTo(pair.Key == P0 ? "skai.fledgling" : "water.tender"));
                // Fields the gather orders use: only the player's own main fields (0 and 1 for Skai, 2 and 3 for Water).
                foreach (GatherCommand gather in gathers)
                {
                    Assert.That(gather.FieldIndex, pair.Key == P0 ? Is.EqualTo(0).Or.EqualTo(1) : Is.EqualTo(2).Or.EqualTo(3));
                }
            }
        }

        /// <summary>The first structure goes on a free, unblocked, non-ramp tile of the HQ's height, rings 3 to 10 from the HQ, clear of the fields; it is accepted.</summary>
        [Test]
        public void FirstStructure_IsPlacedOnAValidSiteNearTheHq()
        {
            // The simulation under test.
            Simulation sim = GreyboxWithHqs();
            Spawn(sim, "skai.fledgling", P0, "6", "9");
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, AiProfile.FromDataSet(Data, "aggressive"));
            // Commands of the think.
            List<Command> commands = ThinkNow(sim, controller);
            // Build orders of the think.
            List<BuildStructureCommand> builds = OfType<BuildStructureCommand>(commands);
            Assert.That(builds.Count, Is.EqualTo(1));
            // The one build order.
            BuildStructureCommand build = builds[0];
            Assert.That(build.DefinitionId, Is.EqualTo("skai.roost"));
            // Chebyshev distance from the HQ tile (7, 7). Unit: tiles.
            int ring = Math.Max(Math.Abs(build.TileX - 7), Math.Abs(build.TileY - 7));
            Assert.That(ring, Is.InRange(3, 10));
            foreach (ResourceField field in sim.Map.ResourceFields)
            {
                Assert.That(Math.Max(Math.Abs(field.X - build.TileX), Math.Abs(field.Y - build.TileY)), Is.GreaterThan(2));
            }
            // Row and column offsets of the 3x3 block. Range: -1 to 1.
            for (int dy = -1; dy <= 1; dy++)
            {
                // Column offset in the 3x3 block. Range: -1 to 1.
                for (int dx = -1; dx <= 1; dx++)
                {
                    // One tile of the 3x3 block around the site.
                    Rts.Sim.Terrain.TerrainTile tile = sim.Terrain.At(build.TileX + dx, build.TileY + dy);
                    Assert.That(tile.IsBlocked || tile.IsRamp, Is.False);
                    Assert.That(tile.Height, Is.EqualTo(sim.Terrain.At(7, 7).Height));
                }
            }
            foreach (Command command in commands)
            {
                sim.Submit(command);
            }
            sim.Step();
            foreach (SimEvent evt in sim.Events.Current)
            {
                Assert.That(evt, Is.Not.InstanceOf<CommandRejectedEvent>(), evt.Describe());
            }
            Assert.That(sim.Terrain.At(build.TileX, build.TileY).IsBlocked, Is.True, "the new structure's footprint blocks its tile");
        }

        /// <summary>Spec test 6: the composition rule trains the unit with the largest deficit w*(N+1) - c*W, ties to the lowest ordinal ID.</summary>
        [Test]
        public void Composition_TrainsTheLargestDeficit()
        {
            // The spec example weights: windrunner 3, roc 1 (W = 4).
            SortedDictionary<string, int> weights = new SortedDictionary<string, int>(StringComparer.Ordinal) { { "skai.roc_rider", 1 }, { "skai.windrunner", 3 } };
            // Producer filter accepting every unit.
            Func<string, bool> any = id => true;
            Assert.That(AiBrain.PickByDeficit(weights, new SortedDictionary<string, int> { { "skai.windrunner", 2 }, { "skai.roc_rider", 0 } }, any), Is.EqualTo("skai.roc_rider"));
            Assert.That(AiBrain.PickByDeficit(weights, new SortedDictionary<string, int> { { "skai.windrunner", 2 }, { "skai.roc_rider", 1 } }, any), Is.EqualTo("skai.windrunner"));
            Assert.That(AiBrain.PickByDeficit(weights, new SortedDictionary<string, int>(), id => id == "skai.windrunner"), Is.EqualTo("skai.windrunner"), "only units the producer trains");
            Assert.That(AiBrain.PickByDeficit(weights, new SortedDictionary<string, int>(), id => false), Is.Null);
        }

        /// <summary>Spec test 7: with threshold 8 (no jitter), army supply 7 gives no attack; 8 gives one attack-move per army unit, ascending ID, to the enemy spawn.</summary>
        [Test]
        public void AttackTrigger_FiresWhenArmySupplyReachesTheThreshold()
        {
            // The simulation under test.
            Simulation sim = GreyboxWithHqs();
            // Seven Windrunners (1 supply each) at the rally area.
            for (int i = 0; i < 7; i++)
            {
                Spawn(sim, "skai.windrunner", P0, "10", "10");
            }
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, ExactAggressive());
            // Commands of the first think (7 supply).
            List<Command> first = ThinkNow(sim, controller);
            Assert.That(controller.Memory.AttackThreshold, Is.EqualTo(8));
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Massing));
            foreach (AttackMoveCommand move in OfType<AttackMoveCommand>(first))
            {
                Assert.That(move.Goal, Is.Not.EqualTo(WaterSpawn), "no attack below the threshold");
            }
            // The eighth Windrunner, which crosses the threshold.
            EntityId eighth = Spawn(sim, "skai.windrunner", P0, "10", "10");
            // Attack-move orders of the second think.
            List<AttackMoveCommand> attack = OfType<AttackMoveCommand>(ThinkNow(sim, controller));
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(controller.Memory.AttackStartSupply, Is.EqualTo(8));
            Assert.That(attack.Count, Is.EqualTo(8));
            // Previous unit ID, for the ascending-order check.
            uint previous = 0;
            foreach (AttackMoveCommand move in attack)
            {
                Assert.That(move.Goal, Is.EqualTo(WaterSpawn));
                Assert.That(move.Unit.Value, Is.GreaterThan(previous));
                previous = move.Unit.Value;
            }
            Assert.That(attack[7].Unit, Is.EqualTo(eighth));
        }

        /// <summary>Spec test 9: an attack that falls below retreat_army_fraction of its start supply moves every army unit home and waits regroup_seconds before attacking again.</summary>
        [Test]
        public void Retreat_MovesHomeAndRegroupsBeforeTheNextAttack()
        {
            // The simulation under test.
            Simulation sim = GreyboxWithHqs();
            // Loop counter. Unit: ticks or repetitions.
            for (int i = 0; i < 7; i++)
            {
                Spawn(sim, "skai.windrunner", P0, "20", "20");
            }
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, ExactAggressive());
            controller.OnMatchStart(sim);
            controller.Memory.AttackThreshold = 8;
            controller.Memory.Stance = AiStance.Attack;
            controller.Memory.AttackStaged = true;
            controller.Memory.AttackStartSupply = 30;
            // 7 supply left of 30 is below 0.25 x 30 = 7.5.
            List<Command> commands = ThinkNow(sim, controller);
            // Tick of the retreat think. Unit: ticks.
            int retreatTick = sim.Clock.Tick;
            // Movement orders of the think.
            List<MoveUnitCommand> moves = OfType<MoveUnitCommand>(commands);
            Assert.That(moves.Count, Is.EqualTo(7));
            foreach (MoveUnitCommand move in moves)
            {
                Assert.That(move.Target, Is.EqualTo(FixVec2.FromInts(7, 7)), "home is the HQ");
            }
            Assert.That(controller.Memory.Retreats, Is.EqualTo(1));
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Massing));
            Assert.That(controller.Memory.RegroupUntilTick, Is.EqualTo(retreatTick + 200), "regroup_seconds 10 at 20 ticks per second");
            foreach (Command command in commands)
            {
                sim.Submit(command);
            }
            sim.Step();
            // Back at 8 supply during the regroup window: no attack yet.
            Spawn(sim, "skai.windrunner", P0, "9", "9");
            ThinkNow(sim, controller);
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Massing));
            Assert.That(controller.Memory.AttacksStarted, Is.EqualTo(0));
            // After the window the attack starts again.
            while (sim.Clock.Tick < controller.Memory.RegroupUntilTick)
            {
                sim.Step();
            }
            ThinkNow(sim, controller);
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(controller.Memory.AttacksStarted, Is.EqualTo(1));
        }

        /// <summary>Spec test 10: a visible enemy soldier inside the base draws the army; one outside defend_radius_tiles does not; a hit on an own unit in the base also does.</summary>
        [Test]
        public void Defend_ReactsToVisibleThreatsAndHitsInsideTheBase()
        {
            // The simulation under test.
            Simulation sim = GreyboxWithHqs();
            Spawn(sim, "skai.windrunner", P0, "5", "5");
            Spawn(sim, "skai.windrunner", P0, "5", "6");
            // An enemy 20 tiles from the HQ, outside defend_radius_tiles.
            EntityId far = Spawn(sim, "water.grasper", P1, "27.5", "7.5");
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, ExactAggressive());
            // Commands while the only enemy is far away.
            List<Command> quiet = ThinkNow(sim, controller);
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Massing), "20 tiles away is outside defend_radius_tiles 12");
            Assert.That(OfType<AttackUnitCommand>(quiet), Is.Empty);
            sim.Submit(new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, _scriptSequence++, far));
            sim.Step();
            Spawn(sim, "water.grasper", P1, "17.5", "7.5");
            // Commands once an enemy is inside the base.
            List<Command> defend = ThinkNow(sim, controller);
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Defend));
            // Movement orders of the think.
            List<AttackMoveCommand> moves = OfType<AttackMoveCommand>(defend);
            Assert.That(moves.Count, Is.EqualTo(2));
            foreach (AttackMoveCommand move in moves)
            {
                Assert.That((move.Goal - FixVec2.FromInts(17, 7)).LengthSquared(), Is.LessThanOrEqualTo(Fix64.FromInt(4)), "attack-move to the threat");
            }

            // A hit report on an own unit inside the base, with no enemy in sight, also calls the army.
            Simulation quietSim = GreyboxWithHqs();
            Spawn(quietSim, "skai.windrunner", P0, "5", "5");
            // An own worker inside the base that was hit.
            EntityId worker = Spawn(quietSim, "skai.fledgling", P0, "11.5", "11.5");
            // A fresh AI for the hit-report case.
            SkirmishAiController second = new SkirmishAiController(P0, ExactAggressive());
            second.OnMatchStart(quietSim);
            second.Memory.RecentDamage.Add(worker, new DamageReport { Tick = quietSim.Clock.Tick, Position = FixVec2.FromInts(11, 11) });
            // Attack-moves toward the hit.
            List<AttackMoveCommand> toHit = OfType<AttackMoveCommand>(ThinkNow(quietSim, second));
            Assert.That(second.Memory.Stance, Is.EqualTo(AiStance.Defend));
            Assert.That(toHit.Count, Is.EqualTo(1));
            Assert.That(toHit[0].Goal, Is.EqualTo(FixVec2.FromInts(11, 11)));
        }

        /// <summary>An enemy inside defend_radius_tiles but outside the team's sight is ignored and never named; once an own unit sees it, the army reacts.</summary>
        [Test]
        public void HiddenEnemy_IsNeverTargeted_UntilTheTeamSeesIt()
        {
            // The simulation under test.
            Simulation sim = GreyboxWithHqs();
            Spawn(sim, "skai.windrunner", P0, "9", "9");
            // 16 tiles north of the HQ: inside defend_radius_tiles 20, outside every Skai unit's sight.
            EntityId hidden = Spawn(sim, "water.grasper", P1, "7.5", "23.5");
            // The AI under test.
            SkirmishAiController controller = new SkirmishAiController(P0, ExactAggressive("\"defend_radius_tiles\": 12", "\"defend_radius_tiles\": 20"));
            Assert.That(sim.Vision.IsUnitVisibleTo(new TeamId(0), EconomyGet(sim, hidden)), Is.False, "precondition: the grasper is in the fog");
            // Commands while the grasper is hidden.
            List<Command> blind = ThinkNow(sim, controller);
            foreach (AiUnitView seen in AiWorldView.Build(sim, P0).VisibleEnemies)
            {
                Assert.That(seen.Id, Is.Not.EqualTo(hidden));
            }
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Massing));
            foreach (Command command in blind)
            {
                Assert.That(command, Is.Not.InstanceOf<AttackUnitCommand>());
                if (command is AttackMoveCommand move)
                {
                    Assert.That((move.Goal - FixVec2.FromInts(7, 23)).LengthSquared(), Is.GreaterThan(Fix64.FromInt(25)), "no order toward the hidden unit");
                }
            }
            // A scout 6 tiles from the grasper reveals it.
            Spawn(sim, "skai.windrunner", P0, "7.5", "17.5");
            Assert.That(sim.Vision.IsUnitVisibleTo(new TeamId(0), EconomyGet(sim, hidden)), Is.True);
            ThinkNow(sim, controller);
            Assert.That(controller.Memory.Stance, Is.EqualTo(AiStance.Defend));
        }

        /// <summary>The unit with this ID; fails when it is gone.</summary>
        private static Unit EconomyGet(Simulation sim, EntityId id)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True);
            return unit!;
        }

        /// <summary>Profile validation errors name the file and the field.</summary>
        [Test]
        public void ProfileValidation_NamesFileAndField()
        {
            // Pairs of (edit old, edit new, expected message start).
            string[][] cases =
            {
                new[] { "  \"attack_army_supply\": 8,\n", "", "ai/aggressive.json: field 'attack_army_supply' is missing" },
                new[] { "\"max_commands_per_think\": 40", "\"max_commands_per_think\": 0", "ai/aggressive.json: field 'max_commands_per_think'" },
                new[] { "\"think_interval_seconds\": 0.5,", "\"think_interval_seconds\": 0.5, \"bogus\": 1,", "ai/aggressive.json: field 'bogus' is not a known key" },
                new[] { "\"supply_building\": \"skai.roost\"", "\"supply_building\": \"skai.windrunner\"", "ai/aggressive.json: field 'factions.skai.supply_building' must be a structure" },
                new[] { "\"worker_unit\": \"skai.fledgling\"", "\"worker_unit\": \"water.tender\"", "ai/aggressive.json: field 'factions.skai.worker_unit' names 'water.tender', which belongs to faction 'water'" },
                new[] { "\"skai.windrunner\": 3", "\"skai.nope\": 3", "ai/aggressive.json: field 'factions.skai.composition.skai.nope' names an unknown unit" },
                new[] { "\"id\": \"aggressive\"", "\"id\": \"other\"", "ai/aggressive.json: field 'id' must be 'aggressive'" },
            };
            foreach (string[] test in cases)
            {
                // The validation error of this case.
                DataValidationException error = Expect.Throws<DataValidationException>(() => EditedProfile("aggressive", test[0], test[1]));
                Assert.That(error.Message, Does.StartWith(test[2]), test[1]);
            }
            // A profile without a plan for the player's faction fails when the controller starts, naming the missing faction.
            Assert.That(Expect.Throws<DataValidationException>(() => WithFactions("{ \"atlantis\": {} }", Data.Content)).Message,
                Does.StartWith("ai/aggressive.json: field 'factions.atlantis' names an unknown faction"));
            // The test catalog the test-faction plan is checked against.
            ContentCatalog content = TestWorld.Catalog();
            // An AI whose profile has no plan for its faction (water).
            SkirmishAiController noPlan = new SkirmishAiController(P1, WithFactions(TestFactionPlan, content));
            Assert.That(Expect.Throws<DataValidationException>(() => noPlan.OnMatchStart(Greybox())).Message,
                Does.StartWith("ai/aggressive.json: field 'factions.water' is missing"));
        }

        /// <summary>The loader routes ai/ (header and id checked), the shipped profiles validate against the content, and a misnamed file is rejected.</summary>
        [Test]
        public void Loader_RoutesAiFolder_AndShippedProfilesValidate()
        {
            Assert.That(Data.AiProfiles.Keys, Is.SupersetOf(new[] { "aggressive", "defensive" }));
            foreach (string id in Data.AiProfiles.Keys)
            {
                // The profile being checked or used.
                AiProfile profile = AiProfile.FromDataSet(Data, id);
                Assert.That(profile.Id, Is.EqualTo(id));
                Assert.That(profile.RequireFaction("skai").WorkerUnitId, Is.EqualTo("skai.fledgling"));
                Assert.That(profile.RequireFaction("water").WorkerUnitId, Is.EqualTo("water.tender"));
            }
            // Error for an ai/ file whose id does not match its name.
            DataValidationException misnamed = Expect.Throws<DataValidationException>(
                () => DataLoader.Load(TestData.WithEdit("ai/defensive.json", "\"id\": \"defensive\"", "\"id\": \"other\"")));
            Assert.That(misnamed.Message, Does.StartWith("ai/defensive.json: field 'id' must be 'defensive'"));
            Assert.That(Expect.Throws<DataValidationException>(() => AiProfile.FromDataSet(Data, "missing")).Message,
                Does.StartWith("ai/missing.json"));
        }

        /// <summary>A 2000-tick AI-versus-AI match on the test economy map (test faction both sides) never has an AI command rejected and never uses hidden information.</summary>
        [Test]
        public void TestEconomyMap_2000TickAiMatch_HasNoRejectedCommands()
        {
            // The two player slots of the match.
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), TestWorld.TestFactionId, "A"),
                new PlayerSetup(P1, new TeamId(1), TestWorld.TestFactionId, "B"),
            };
            // The simulation under test.
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(77, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                TestWorld.Catalog(), players, BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, TestWorld.EconomyMap()));
            sim.Start();
            // HQs next to field 0 (12 units, mined out early) and field 2 (1500 units), two workers each.
            Spawn(sim, TestWorld.HqId, P0, "12", "12");
            Spawn(sim, TestWorld.WorkerId, P0, "13", "11");
            Spawn(sim, TestWorld.WorkerId, P0, "11", "13");
            Spawn(sim, TestWorld.HqId, P1, "52", "52");
            Spawn(sim, TestWorld.WorkerId, P1, "53", "51");
            Spawn(sim, TestWorld.WorkerId, P1, "51", "53");
            // A profile for the test faction: the aggressive numbers with a test plan.
            AiProfile profile = WithFactions(TestFactionPlan, TestWorld.Catalog());
            // The two AI controllers.
            List<SkirmishAiController> controllers = new List<SkirmishAiController> { new SkirmishAiController(P0, profile), new SkirmishAiController(P1, profile) };
            // What the run produced.
            RunResult result = RunAi(sim, controllers, 2000);
            Assert.That(result.CommandCount, Is.GreaterThan(20), "the AIs actually played");
            Assert.That(result.Rejections, Is.Empty);
            Assert.That(result.VisionViolations, Is.Empty);
            Assert.That(controllers[0].Memory.ThinkCount, Is.GreaterThan(150));
        }

        /// <summary>
        /// The Phase 1 exit test: two full AI-versus-AI runs of rts/data/scripts/ai_vs_ai.json (Skai aggressive vs Water defensive on
        /// greybox_1v1, seed 1234) produce identical per-interval and final hashes and reach a decided outcome within 6000 ticks, with
        /// no rejected AI command and no use of hidden information.
        /// </summary>
        [Test]
        public void ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided()
        {
            // The two runs of the exit script.
            RunResult[] runs = new RunResult[2];
            // Run index. Range: 0 to 1.
            for (int run = 0; run < 2; run++)
            {
                // The simulation under test.
                Simulation sim = StartAiVsAi(out List<SkirmishAiController> controllers, out CommandScript script);
                Assert.That(script.Ticks, Is.EqualTo(6000));
                Assert.That(controllers.Count, Is.EqualTo(2));
                runs[run] = RunAi(sim, controllers, script.Ticks);
            }
            Assert.That(runs[1].Hashes, Is.EqualTo(runs[0].Hashes));
            Assert.That(runs[1].FinalHash, Is.EqualTo(runs[0].FinalHash));
            Assert.That(runs[1].LastTick, Is.EqualTo(runs[0].LastTick));
            Assert.That(runs[0].Outcome!.IsDecided, Is.True, "decided within 6000 ticks");
            Assert.That(runs[0].Outcome!.IsDraw, Is.False);
            Assert.That(runs[0].LastTick, Is.LessThan(6000));
            Assert.That(runs[0].Rejections, Is.Empty);
            Assert.That(runs[0].VisionViolations, Is.Empty);
        }
    }
}
