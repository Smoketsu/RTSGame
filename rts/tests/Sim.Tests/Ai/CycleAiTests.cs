// rts/tests/Sim.Tests/Ai/CycleAiTests.cs
// Tests for the skirmish AI's Planetary Cycle behaviour (rts/docs/specs/planetary_cycles.md section 5, acceptance tests 21 and 22): the
// Core holder picks the first allowed cycle of its faction's cycle_preference exactly once, the massing army rallies at the Core while
// the AI does not hold it (rally_at_core), the profile keys are validated, quake damage is not mistaken for an attack, and the full
// AI-versus-AI script plays the same twice with cycles on and shows a cycle shift. Part of Rts.Sim.Tests.
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

namespace Rts.Sim.Tests.Ai
{
    /// <summary>The AI's cycle pick and Core rally.</summary>
    [TestFixture]
    public class CycleAiTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>A greybox_1v1 simulation (Skai slot 0, Water slot 1, cycles on) with Skai's HQ rooted at (7, 7) and one Windrunner at (8, 8).</summary>
        private Simulation Greybox(out EntityId windrunner)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players, BuiltInMechanics.CreateRegistry(),
                Data.Tuning, Data.RequireMap("greybox_1v1"), Data.Cycles.Values));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, "7", "7");
            windrunner = Spawn(sim, "skai.windrunner", P0, "8", "8");
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

        /// <summary>The intents the brain decides for P0 on this view.</summary>
        private static List<AiIntent> Decide(AiWorldView view, AiProfile profile)
        {
            return AiBrain.Decide(view, profile, profile.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1));
        }

        /// <summary>A world view of P0 in which its team holds the Core, windstorm is active and the other two cycles may follow.</summary>
        private static AiWorldView HolderView(Simulation sim)
        {
            AiWorldView view = AiWorldView.Build(sim, P0);
            view.CoreHolder = view.Team;
            view.ActiveCycleId = "windstorm";
            view.AllowedNextCycleIds = new List<string> { "core_surge", "quake" };
            return view;
        }

        /// <summary>The cycle IDs of the PickCycle intents among <paramref name="intents"/>.</summary>
        private static List<string> Picks(List<AiIntent> intents)
        {
            List<string> picks = new List<string>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.PickCycle)
                {
                    picks.Add(intent.DefinitionId);
                }
            }
            return picks;
        }

        // ---------------------------------------------------------------- 21. the pick

        [Test]
        public void Holder_PicksTheFirstAllowedPreferenceOnce()
        {
            Simulation sim = Greybox(out _);
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive");
            Assert.That(profile.RequireFaction("skai").CyclePreference, Is.EqualTo(new List<string> { "windstorm", "core_surge", "quake" }));
            AiWorldView view = HolderView(sim);
            // Windstorm is first in the preference but may not repeat; the next one is core_surge.
            List<AiIntent> intents = Decide(view, profile);
            Assert.That(Picks(intents), Is.EqualTo(new List<string> { "core_surge" }));
            Command command = intents.Find(i => i.Kind == AiIntentKind.PickCycle)!.ToCommand(sim.Clock.Tick, P0, 0);
            Assert.That(command, Is.InstanceOf<PickCycleCommand>());
            Assert.That(((PickCycleCommand)command).CycleId, Is.EqualTo("core_surge"));
            Assert.That(command.Issuer, Is.EqualTo(P0));
            // The next think, with that pick pending: nothing more.
            view.PendingPickId = "core_surge";
            Assert.That(Picks(Decide(view, profile)), Is.Empty);
            // While the next cycle is locked: nothing.
            view.PendingPickId = string.Empty;
            view.IsNextCycleLocked = true;
            Assert.That(Picks(Decide(view, profile)), Is.Empty);
        }

        [Test]
        public void NonHolder_DoesNotPick_AndFallsBackToTheFirstAllowedCycle()
        {
            Simulation sim = Greybox(out _);
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive");
            AiWorldView notHolder = AiWorldView.Build(sim, P0);
            notHolder.AllowedNextCycleIds = new List<string> { "core_surge", "quake" };
            Assert.That(Picks(Decide(notHolder, profile)), Is.Empty, "only the holder picks");
            // A holder whose preferences are all disallowed picks the first allowed cycle.
            AiWorldView holder = HolderView(sim);
            holder.AllowedNextCycleIds = new List<string> { "a_new_cycle" };
            Assert.That(Picks(Decide(holder, profile)), Is.EqualTo(new List<string> { "a_new_cycle" }));
            // No cycles in the match: nothing to pick.
            AiWorldView noCycles = HolderView(sim);
            noCycles.HasCycles = false;
            Assert.That(Picks(Decide(noCycles, profile)), Is.Empty);
        }

        [Test]
        public void Holder_PreferenceOfTheFactionDecides()
        {
            // A Water AI prefers core_surge, quake, windstorm (placeholder order): after windstorm it still picks core_surge; after core_surge, quake.
            Simulation sim = Greybox(out _);
            Spawn(sim, "water.grand_shell", P1, "57", "57");
            AiProfile profile = AiProfile.FromDataSet(Data, "aggressive");
            AiWorldView view = AiWorldView.Build(sim, P1);
            view.CoreHolder = view.Team;
            view.AllowedNextCycleIds = new List<string> { "quake", "windstorm" };
            List<AiIntent> intents = AiBrain.Decide(view, profile, profile.RequireFaction("water"), new AiMemory(), new DeterministicRandom(1));
            Assert.That(Picks(intents), Is.EqualTo(new List<string> { "quake" }));
        }

        // ---------------------------------------------------------------- rally at the Core

        /// <summary>The attack-move targets the brain gives the idle Windrunner this think.</summary>
        private static List<FixVec2> RallyPoints(List<AiIntent> intents)
        {
            List<FixVec2> points = new List<FixVec2>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.AttackMove)
                {
                    points.Add(intent.Point);
                }
            }
            return points;
        }

        [Test]
        public void RallyAtCore_MassingArmyGathersAtTheCoreUntilTheTeamHoldsIt()
        {
            Simulation sim = Greybox(out _);
            // A second idle army unit far from any rally point, so the holder case below still has a unit to order.
            Spawn(sim, "skai.windrunner", P0, "20", "12");
            AiProfile aggressive = AiProfile.FromDataSet(Data, "aggressive");
            Assert.That(aggressive.RallyAtCore, Is.True);
            Assert.That(AiProfile.FromDataSet(Data, "defensive").RallyAtCore, Is.False);
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.HasCore, Is.True);
            List<FixVec2> toCore = RallyPoints(Decide(view, aggressive));
            Assert.That(toCore, Is.EqualTo(new List<FixVec2> { view.CoreCentre, view.CoreCentre }), "both idle army units walk to the Core centre");
            // Once the team holds the Core the army rallies by the old rule (toward the enemy, rally_distance_tiles from the HQ).
            view.CoreHolder = view.Team;
            List<FixVec2> byHq = RallyPoints(Decide(view, aggressive));
            Assert.That(byHq.Count, Is.GreaterThanOrEqualTo(1));
            foreach (FixVec2 point in byHq)
            {
                Assert.That(point, Is.Not.EqualTo(view.CoreCentre));
            }
            // A profile without rally_at_core never goes to the Core.
            AiWorldView again = AiWorldView.Build(sim, P0);
            AiProfile defensive = AiProfile.FromDataSet(Data, "defensive");
            foreach (FixVec2 point in RallyPoints(AiBrain.Decide(again, defensive, defensive.RequireFaction("skai"), new AiMemory(), new DeterministicRandom(1))))
            {
                Assert.That(point, Is.Not.EqualTo(again.CoreCentre));
            }
        }

        // ---------------------------------------------------------------- the view and the controller

        [Test]
        public void WorldView_CopiesThePublicCycleAndCoreState()
        {
            Simulation sim = Greybox(out _);
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.HasCycles, Is.True);
            Assert.That(view.ActiveCycleId, Is.EqualTo(string.Empty));
            Assert.That(view.AllowedNextCycleIds, Is.EqualTo(new List<string> { "core_surge", "quake", "windstorm" }));
            Assert.That(view.NextShiftTick, Is.EqualTo(4800));
            Assert.That(view.CoreCentre, Is.EqualTo(FixVec2.FromInts(32, 32)));
            Assert.That(view.CoreRadiusTiles, Is.EqualTo(Fix64.FromInt(4)));
            Assert.That(view.CoreHolder.IsValid, Is.False);
            Assert.That(view.OwnTeamHoldsCore, Is.False);
            Assert.That(view.CoreWinTicks, Is.EqualTo(9600));
        }

        [Test]
        public void Controller_IgnoresDamageFromTheEnvironment()
        {
            Simulation sim = Greybox(out _);
            SkirmishAiController controller = new SkirmishAiController(P0, AiProfile.FromDataSet(Data, "aggressive"));
            controller.OnMatchStart(sim);
            // A hit by no attacker (a Quake) on an own unit must not become a damage report.
            sim.Step();
            Assert.That(controller.Memory.RecentDamage, Is.Empty);
            ListCommandSink sink = new ListCommandSink(sim.Clock.Tick);
            controller.Think(sim, sink);
            Assert.That(controller.Memory.RecentDamage, Is.Empty);
        }

        // ---------------------------------------------------------------- profile validation

        /// <summary>The shipped aggressive profile text with one edit.</summary>
        private static string EditedProfile(string oldText, string newText)
        {
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive.json")).Replace("\r\n", "\n");
            Assert.That(text.Contains(oldText), Is.True, oldText);
            return text.Replace(oldText, newText);
        }

        [Test]
        public void Profile_CyclePreference_IsValidated()
        {
            List<string> ids = new List<string>(Data.Cycles.Keys);
            DataValidationException unknown = Expect.Throws<DataValidationException>(() => AiProfile.Parse(
                EditedProfile("[\"windstorm\", \"core_surge\", \"quake\"]", "[\"windstorm\", \"core_surge\", \"tornado\"]"), "ai/aggressive.json", Data.Content, ids));
            Assert.That(unknown.Message, Does.Contain("unknown cycle 'tornado'"));
            DataValidationException twice = Expect.Throws<DataValidationException>(() => AiProfile.Parse(
                EditedProfile("[\"windstorm\", \"core_surge\", \"quake\"]", "[\"windstorm\", \"windstorm\", \"quake\"]"), "ai/aggressive.json", Data.Content, ids));
            Assert.That(twice.Message, Does.Contain("twice"));
            DataValidationException missing = Expect.Throws<DataValidationException>(() => AiProfile.Parse(
                EditedProfile("[\"windstorm\", \"core_surge\", \"quake\"]", "[\"windstorm\", \"core_surge\"]"), "ai/aggressive.json", Data.Content, ids));
            Assert.That(missing.Message, Does.Contain("must list every cycle"));
            DataValidationException noKey = Expect.Throws<DataValidationException>(() => AiProfile.Parse(
                EditedProfile("\"rally_at_core\": true,", string.Empty), "ai/aggressive.json", Data.Content, ids));
            Assert.That(noKey.Message, Does.Contain("rally_at_core").And.Contain("is missing"));
        }

        // ---------------------------------------------------------------- 22. harness determinism

        /// <summary>Plays rts/data/scripts/ai_vs_ai.json with cycles on for <paramref name="ticks"/> ticks (past the end of the match) and returns the final hash and the cycle_started count.</summary>
        private static (ulong hash, int shifts, int captures) PlayAiVersusAi(int ticks)
        {
            CommandScript script = CommandScript.Parse(File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "scripts", "ai_vs_ai.json")),
                "scripts/ai_vs_ai.json", BuiltInMechanics.ScriptParsers);
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(script.Seed), Data.Content, script.Players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(script.MapId), Data.Cycles.Values));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (KeyValuePair<PlayerId, string> pair in script.AiProfiles)
            {
                controllers.Add(new SkirmishAiController(pair.Key, AiProfile.FromDataSet(Data, pair.Value)));
            }
            AiHost.StartAll(sim, controllers);
            int shifts = 0;
            int captures = 0;
            for (int i = 0; i < ticks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is CycleStartedEvent)
                    {
                        shifts++;
                    }
                    else if (evt is CoreCapturedEvent)
                    {
                        captures++;
                    }
                }
            }
            return (sim.ComputeStateHash(), shifts, captures);
        }

        [Test]
        public void AiVersusAi_WithCyclesOn_IsDeterministicAndShowsACycleShift()
        {
            (ulong hash, int shifts, int captures) first = PlayAiVersusAi(10000);
            (ulong hash, int shifts, int captures) second = PlayAiVersusAi(10000);
            Assert.That(second.hash, Is.EqualTo(first.hash));
            Assert.That(second.shifts, Is.EqualTo(first.shifts));
            Assert.That(first.shifts, Is.GreaterThanOrEqualTo(1), "a cycle starts on tick 4800");
        }
    }
}
