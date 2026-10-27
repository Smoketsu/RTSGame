// rts/tests/Sim.Tests/TeamModelTests.cs
// Tests for the player and team model (rts/src/Sim/Teams): alliance queries for 2v2, ascending iteration
// order, the player state machine driven by SurrenderCommand and SetConnectionStateCommand, rejection
// reasons, MatchOutcome for 1v1, 2v2, 1v1v1 and 4-player FFA, the hand-over policy hook, and the state
// hash (states and counters hashed, display names not). Part of Rts.Sim.Tests.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;

namespace Rts.Sim.Tests
{
    /// <summary>Player and team model tests. Every match here uses the "test" faction so no mechanic interferes.</summary>
    [TestFixture]
    public sealed class TeamModelTests
    {
        /// <summary>Shorthand for a player ID.</summary>
        /// <param name="value">Slot index, 0 to 254.</param>
        private static PlayerId P(byte value) => new PlayerId(value);

        /// <summary>Shorthand for a team ID.</summary>
        /// <param name="value">Team index, 0 to 254.</param>
        private static TeamId T(byte value) => new TeamId(value);

        /// <summary>Builds lobby setups from (player, team) pairs, in the order given, with the test faction.</summary>
        /// <param name="slots">Pairs of player slot and team index.</param>
        private static List<PlayerSetup> Setups(params (byte player, byte team)[] slots)
        {
            // The setups being built, in the order given.
            List<PlayerSetup> setups = new List<PlayerSetup>();
            foreach ((byte player, byte team) in slots)
            {
                setups.Add(new PlayerSetup(P(player), T(team), TestWorld.TestFactionId, "Player " + player));
            }
            return setups;
        }

        /// <summary>A started simulation with the test catalog and the given players.</summary>
        /// <param name="setups">Lobby setups.</param>
        /// <param name="policy">Hand-over policy to install, or null for the default.</param>
        private static Simulation Start(List<PlayerSetup> setups, IHandOverPolicy? policy = null)
        {
            // Match configuration: seed 1234, test tick rate and hash interval.
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                TestWorld.Catalog(),
                setups,
                BuiltInMechanics.CreateRegistry());
            // The simulation under test.
            Simulation sim = new Simulation(config);
            if (policy != null)
            {
                sim.SetHandOverPolicy(policy);
            }
            sim.Start();
            return sim;
        }

        /// <summary>State of one player, failing the test if the player is missing.</summary>
        /// <param name="sim">Simulation to read.</param>
        /// <param name="id">Player slot.</param>
        private static Player Get(Simulation sim, byte id)
        {
            Assert.That(sim.Players.TryGet(P(id), out Player? player), Is.True);
            return player!;
        }

        /// <summary>Reasons of every command rejection in the last stepped tick, in order.</summary>
        /// <param name="sim">Simulation to read.</param>
        private static List<string> Rejections(Simulation sim)
        {
            // Rejection reasons found so far.
            List<string> reasons = new List<string>();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is CommandRejectedEvent rejected)
                {
                    reasons.Add(rejected.Reason);
                }
            }
            return reasons;
        }

        /// <summary>Every PlayerStateChangedEvent of the last stepped tick, in order.</summary>
        /// <param name="sim">Simulation to read.</param>
        private static List<PlayerStateChangedEvent> StateChanges(Simulation sim)
        {
            // State-change events found so far.
            List<PlayerStateChangedEvent> changes = new List<PlayerStateChangedEvent>();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is PlayerStateChangedEvent change)
                {
                    changes.Add(change);
                }
            }
            return changes;
        }

        /// <summary>Submits a surrender by the player itself on the current tick and steps once.</summary>
        /// <param name="sim">Simulation to drive.</param>
        /// <param name="id">Player who surrenders.</param>
        private static void SurrenderAndStep(Simulation sim, byte id)
        {
            sim.Submit(new SurrenderCommand(sim.Clock.Tick, P(id), 0, P(id)));
            sim.Step();
        }

        /// <summary>Hash of a registry on its own.</summary>
        /// <param name="registry">Registry to hash.</param>
        private static ulong HashOf(PlayerRegistry registry)
        {
            // Fresh hasher so nothing else is mixed in.
            StateHasher hasher = new StateHasher();
            registry.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>2v2: teammates are allied, opponents are enemies, and the team queries list the right players.</summary>
        [Test]
        public void TwoVsTwo_AllianceQueries()
        {
            PlayerRegistry players = new PlayerRegistry(Setups((0, 0), (1, 0), (2, 1), (3, 1)));
            Assert.That(players.Count, Is.EqualTo(4));
            Assert.That(players.TeamCount, Is.EqualTo(2));
            Assert.That(players.AreAllied(P(0), P(1)), Is.True);
            Assert.That(players.AreAllied(P(2), P(3)), Is.True);
            Assert.That(players.AreAllied(P(0), P(2)), Is.False);
            Assert.That(players.AreEnemies(P(0), P(2)), Is.True);
            Assert.That(players.AreEnemies(P(1), P(3)), Is.True);
            Assert.That(players.AreEnemies(P(0), P(1)), Is.False);
            // A player is allied with itself and never its own enemy.
            Assert.That(players.AreAllied(P(2), P(2)), Is.True);
            Assert.That(players.AreEnemies(P(2), P(2)), Is.False);
            Assert.That(players.AlliesOf(P(0)), Is.EqualTo(new[] { P(1) }));
            Assert.That(players.AlliesOf(P(3)), Is.EqualTo(new[] { P(2) }));
            Assert.That(players.TeamOf(P(3)), Is.EqualTo(T(1)));
            Assert.That(players.PlayersOnTeam(T(1)), Is.EqualTo(new[] { P(2), P(3) }));
            Assert.That(players.Teams, Is.EqualTo(new[] { T(0), T(1) }));
            Assert.That(players.FirstActiveAllyOf(P(1)), Is.EqualTo(P(0)));
        }

        /// <summary>Unknown IDs and the neutral player are never allied or enemies and have no team or allies.</summary>
        [Test]
        public void UnknownPlayers_AreNeitherAlliedNorEnemies()
        {
            PlayerRegistry players = new PlayerRegistry(Setups((0, 0), (1, 1)));
            Assert.That(players.AreAllied(P(0), P(9)), Is.False);
            Assert.That(players.AreEnemies(P(0), P(9)), Is.False);
            Assert.That(players.AreEnemies(P(0), PlayerId.None), Is.False);
            Assert.That(players.TeamOf(P(9)), Is.EqualTo(TeamId.None));
            Assert.That(players.AlliesOf(P(9)), Is.Empty);
            Assert.That(players.PlayersOnTeam(T(7)), Is.Empty);
            Assert.That(players.FirstActiveAllyOf(P(9)), Is.EqualTo(PlayerId.None));
            // 1v1: nobody has an ally.
            Assert.That(players.AlliesOf(P(0)), Is.Empty);
            Assert.That(players.FirstActiveAllyOf(P(0)), Is.EqualTo(PlayerId.None));
        }

        /// <summary>Players, teams and team members come back in ascending ID order whatever the lobby order.</summary>
        [Test]
        public void Iteration_IsAscendingRegardlessOfLobbyOrder()
        {
            PlayerRegistry players = new PlayerRegistry(Setups((3, 1), (0, 2), (2, 1), (1, 0), (4, 1)));
            // Player IDs in the order All yields them.
            List<PlayerId> order = new List<PlayerId>();
            foreach (Player player in players.All)
            {
                order.Add(player.Id);
            }
            Assert.That(order, Is.EqualTo(new[] { P(0), P(1), P(2), P(3), P(4) }));
            Assert.That(players.Teams, Is.EqualTo(new[] { T(0), T(1), T(2) }));
            Assert.That(players.PlayersOnTeam(T(1)), Is.EqualTo(new[] { P(2), P(3), P(4) }));
            Assert.That(players.AlliesOf(P(3)), Is.EqualTo(new[] { P(2), P(4) }));
        }

        /// <summary>Duplicate player IDs and an empty lobby are refused.</summary>
        [Test]
        public void Registry_RejectsDuplicatesAndEmptyLobby()
        {
            Expect.Throws<ArgumentException>(() => new PlayerRegistry(Setups((0, 0), (0, 1))));
            Expect.Throws<ArgumentException>(() => new PlayerRegistry(new List<PlayerSetup>()));
        }

        /// <summary>Two registries from the same lobby (any order, any display names) hash the same; a different team hashes differently.</summary>
        [Test]
        public void Hash_IsStableAcrossIdenticalRegistries_AndIgnoresDisplayNames()
        {
            PlayerRegistry first = new PlayerRegistry(Setups((0, 0), (1, 0), (2, 1)));
            PlayerRegistry second = new PlayerRegistry(new List<PlayerSetup>
            {
                new PlayerSetup(P(2), T(1), TestWorld.TestFactionId, "Somebody else"),
                new PlayerSetup(P(0), T(0), TestWorld.TestFactionId, "Renamed"),
                new PlayerSetup(P(1), T(0), TestWorld.TestFactionId, "Another name"),
            });
            Assert.That(HashOf(second), Is.EqualTo(HashOf(first)));
            PlayerRegistry otherTeams = new PlayerRegistry(Setups((0, 0), (1, 1), (2, 1)));
            Assert.That(HashOf(otherTeams), Is.Not.EqualTo(HashOf(first)));
        }

        /// <summary>Every player starts Active at tick 0 with zero counters.</summary>
        [Test]
        public void Players_StartActiveWithZeroCounters()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1)));
            foreach (Player player in sim.Players.All)
            {
                Assert.That(player.State, Is.EqualTo(PlayerState.Active));
                Assert.That(player.IsActive, Is.True);
                Assert.That(player.StateSinceTick, Is.EqualTo(0));
                Assert.That(player.DisconnectCount, Is.EqualTo(0));
                Assert.That(player.DisconnectPausesUsed, Is.EqualTo(0));
                Assert.That(player.PauseTicksUsed, Is.EqualTo(0));
            }
            Assert.That(sim.HandOverPolicy, Is.SameAs(StateOnlyHandOverPolicy.Instance));
        }

        /// <summary>A surrender command changes the state, emits the event, and changes the state hash.</summary>
        [Test]
        public void Surrender_ChangesStateEmitsEventAndHashesDifferently()
        {
            Simulation surrendered = Start(Setups((0, 0), (1, 1)));
            Simulation control = Start(Setups((0, 0), (1, 1)));
            surrendered.Step(3);
            control.Step(3);
            Assert.That(surrendered.ComputeStateHash(), Is.EqualTo(control.ComputeStateHash()));

            surrendered.Submit(new SurrenderCommand(3, P(1), 0, P(1)));
            surrendered.Step();
            control.Step();
            Assert.That(Get(surrendered, 1).State, Is.EqualTo(PlayerState.Surrendered));
            Assert.That(Get(surrendered, 1).StateSinceTick, Is.EqualTo(3));
            Assert.That(Get(surrendered, 0).State, Is.EqualTo(PlayerState.Active));
            Assert.That(Rejections(surrendered), Is.Empty);
            // The state-change events of tick 3.
            List<PlayerStateChangedEvent> changes = StateChanges(surrendered);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Player, Is.EqualTo(P(1)));
            Assert.That(changes[0].Team, Is.EqualTo(T(1)));
            Assert.That(changes[0].Previous, Is.EqualTo(PlayerState.Active));
            Assert.That(changes[0].Current, Is.EqualTo(PlayerState.Surrendered));
            Assert.That(changes[0].Cause, Is.EqualTo("surrender"));
            Assert.That(changes[0].Tick, Is.EqualTo(3));
            Assert.That(surrendered.ComputeStateHash(), Is.Not.EqualTo(control.ComputeStateHash()));
        }

        /// <summary>The same surrender on the same tick in two matches gives identical hashes (determinism).</summary>
        [Test]
        public void Surrender_IsDeterministic()
        {
            Simulation a = Start(Setups((0, 0), (1, 1), (2, 2)));
            Simulation b = Start(Setups((2, 2), (1, 1), (0, 0)));
            a.Submit(new SurrenderCommand(5, P(2), 0, P(2)));
            b.Submit(new SurrenderCommand(5, P(2), 0, P(2)));
            a.Step(12);
            b.Step(12);
            Assert.That(a.ComputeStateHash(), Is.EqualTo(b.ComputeStateHash()));
            Assert.That(a.LastReportedHash, Is.EqualTo(b.LastReportedHash));
        }

        /// <summary>Illegal transitions and wrong issuers are rejected with a reason and change nothing.</summary>
        [Test]
        public void InvalidTransitions_AreRejectedWithReason()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1), (2, 2)));
            // Tick 0: P1 tries to surrender for P0; the script surrenders an unknown slot; P0 reports itself connected
            // while already active; the script disconnects P2.
            sim.Submit(new SurrenderCommand(0, P(1), 0, P(0)));
            sim.Submit(new SurrenderCommand(0, PlayerId.None, 0, P(9)));
            sim.Submit(new SetConnectionStateCommand(0, P(0), 0, P(0), ConnectionState.Connected));
            sim.Submit(new SetConnectionStateCommand(0, PlayerId.None, 1, P(2), ConnectionState.Disconnected));
            sim.Step();
            // Commands run in (issuer, sequence) order: P0's, then P1's, then the script's two.
            Assert.That(Rejections(sim), Is.EqualTo(new[] { "already_active", "not_self", "unknown_player" }));
            Assert.That(Get(sim, 0).State, Is.EqualTo(PlayerState.Active));
            Assert.That(Get(sim, 2).State, Is.EqualTo(PlayerState.Disconnected));

            // Tick 1: P2 is disconnected again, and P2 (disconnected) tries to surrender.
            sim.Submit(new SetConnectionStateCommand(1, PlayerId.None, 0, P(2), ConnectionState.Disconnected));
            sim.Submit(new SurrenderCommand(1, P(2), 0, P(2)));
            sim.Step();
            Assert.That(Rejections(sim), Is.EqualTo(new[] { "player_disconnected", "already_disconnected" }));
            Assert.That(Get(sim, 2).DisconnectCount, Is.EqualTo(1));

            // Tick 2: P1 surrenders; tick 3: P1 surrenders again and tries to reconnect.
            SurrenderAndStep(sim, 1);
            Assert.That(Rejections(sim), Is.Empty);
            sim.Submit(new SurrenderCommand(3, P(1), 0, P(1)));
            sim.Submit(new SetConnectionStateCommand(3, P(1), 1, P(1), ConnectionState.Disconnected));
            sim.Step();
            Assert.That(Rejections(sim), Is.EqualTo(new[] { "already_surrendered", "player_out_of_match" }));
            Assert.That(Get(sim, 1).State, Is.EqualTo(PlayerState.Surrendered));
            Assert.That(Get(sim, 1).StateSinceTick, Is.EqualTo(2));
            Assert.That(StateChanges(sim), Is.Empty);
        }

        /// <summary>The pure transition table: legal moves out of Active and Disconnected, nothing out of terminal states.</summary>
        [Test]
        public void TransitionTable_MatchesDesign()
        {
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Active, PlayerState.Surrendered), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Active, PlayerState.Disconnected), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Active, PlayerState.Defeated), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Active, PlayerState.Eliminated), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Disconnected, PlayerState.Active), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Disconnected, PlayerState.Defeated), Is.True);
            Assert.That(PlayerStateRules.CanTransition(PlayerState.Disconnected, PlayerState.Eliminated), Is.True);
            Assert.That(PlayerStateRules.TransitionProblem(PlayerState.Disconnected, PlayerState.Surrendered), Is.EqualTo("player_disconnected"));
            Assert.That(PlayerStateRules.TransitionProblem(PlayerState.Active, PlayerState.Active), Is.EqualTo("already_active"));
            foreach (PlayerState terminal in new[] { PlayerState.Surrendered, PlayerState.Defeated, PlayerState.Eliminated })
            {
                Assert.That(PlayerStateRules.IsTerminal(terminal), Is.True);
                Assert.That(PlayerStateRules.IsInMatch(terminal), Is.False);
                Assert.That(PlayerStateRules.TransitionProblem(terminal, PlayerState.Active), Is.EqualTo("player_out_of_match"));
                Assert.That(PlayerStateRules.TransitionProblem(terminal, PlayerState.Disconnected), Is.EqualTo("player_out_of_match"));
            }
            Assert.That(PlayerStateRules.IsInMatch(PlayerState.Active), Is.True);
            Assert.That(PlayerStateRules.IsInMatch(PlayerState.Disconnected), Is.True);
        }

        /// <summary>
        /// Disconnect and reconnect: the counter and state are hashed, a disconnect alone does not decide a 1v1, and the
        /// disconnected player's team is standing but has no Active player.
        /// </summary>
        [Test]
        public void DisconnectAndReconnect_TrackCounterAndKeepTeamStanding()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1)));
            Simulation control = Start(Setups((0, 0), (1, 1)));
            sim.Submit(new SetConnectionStateCommand(0, PlayerId.None, 0, P(1), ConnectionState.Disconnected));
            sim.Step();
            control.Step();
            Assert.That(Get(sim, 1).State, Is.EqualTo(PlayerState.Disconnected));
            Assert.That(Get(sim, 1).DisconnectCount, Is.EqualTo(1));
            Assert.That(StateChanges(sim)[0].Cause, Is.EqualTo("disconnect"));
            // The outcome while P1 is away.
            MatchOutcome away = MatchOutcome.Evaluate(sim.Players);
            Assert.That(away.IsDecided, Is.False);
            Assert.That(away.StandingTeams, Is.EqualTo(new[] { T(0), T(1) }));
            Assert.That(away.TeamsWithActivePlayers, Is.EqualTo(new[] { T(0) }));

            sim.Submit(new SetConnectionStateCommand(4, P(1), 0, P(1), ConnectionState.Connected));
            sim.Step(5);
            control.Step(5);
            Assert.That(Get(sim, 1).State, Is.EqualTo(PlayerState.Active));
            Assert.That(Get(sim, 1).StateSinceTick, Is.EqualTo(4));
            Assert.That(Get(sim, 1).DisconnectCount, Is.EqualTo(1));
            Assert.That(MatchOutcome.Evaluate(sim.Players).TeamsWithActivePlayers, Is.EqualTo(new[] { T(0), T(1) }));
            // Back to Active, but the history (count, since-tick) still affects the future, so it is hashed.
            Assert.That(sim.ComputeStateHash(), Is.Not.EqualTo(control.ComputeStateHash()));
        }

        /// <summary>1v1: a surrender decides the match and the opponent wins.</summary>
        [Test]
        public void OneVsOne_SurrenderDecidesForOpponent()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1)));
            Assert.That(MatchOutcome.Evaluate(sim.Players).IsDecided, Is.False);
            SurrenderAndStep(sim, 0);
            // The outcome after P0 surrendered.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(T(1)));
            Assert.That(outcome.IsDraw, Is.False);
        }

        /// <summary>1v1: both surrender on the same tick, so no team is left: decided, a draw, no winner.</summary>
        [Test]
        public void OneVsOne_BothSurrender_IsDraw()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1)));
            sim.Submit(new SurrenderCommand(0, P(0), 0, P(0)));
            sim.Submit(new SurrenderCommand(0, P(1), 0, P(1)));
            sim.Step();
            // The outcome with nobody left.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.IsDraw, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(TeamId.None));
            Assert.That(outcome.StandingTeams, Is.Empty);
        }

        /// <summary>2v2: one surrender leaves the team standing via the ally; the second decides it for the other team.</summary>
        [Test]
        public void TwoVsTwo_TeamStandsUntilLastPlayerLeaves()
        {
            Simulation sim = Start(Setups((0, 0), (1, 0), (2, 1), (3, 1)));
            SurrenderAndStep(sim, 0);
            // The outcome after one of team 0 surrendered.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.False);
            Assert.That(outcome.StandingTeams, Is.EqualTo(new[] { T(0), T(1) }));
            // The ally who would receive the hand-over in Phase 3.
            Assert.That(sim.Players.FirstActiveAllyOf(P(0)), Is.EqualTo(P(1)));
            // Alliances are structural: surrendering does not change them.
            Assert.That(sim.Players.AreAllied(P(0), P(1)), Is.True);

            SurrenderAndStep(sim, 1);
            outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(T(1)));
            Assert.That(sim.Players.FirstActiveAllyOf(P(0)), Is.EqualTo(PlayerId.None));
        }

        /// <summary>1v1v1: one surrender leaves two teams; the second decides it.</summary>
        [Test]
        public void OneVsOneVsOne_DecidedWhenOneTeamRemains()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1), (2, 2)));
            SurrenderAndStep(sim, 1);
            // The outcome after the first surrender.
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.False);
            Assert.That(outcome.StandingTeams, Is.EqualTo(new[] { T(0), T(2) }));
            Assert.That(outcome.Winner, Is.EqualTo(TeamId.None));

            SurrenderAndStep(sim, 0);
            outcome = MatchOutcome.Evaluate(sim.Players);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo(T(2)));
        }

        /// <summary>4-player FFA: three players surrender one by one; only the third surrender decides the match.</summary>
        [Test]
        public void FourPlayerFfa_DecidedOnlyAfterThirdSurrender()
        {
            Simulation sim = Start(Setups((0, 0), (1, 1), (2, 2), (3, 3)));
            // Surrender order and the standing count expected after each.
            byte[] order = { 3, 0, 2 };
            // Teams standing after each surrender in order.
            int[] standingAfter = { 3, 2, 1 };
            for (int i = 0; i < order.Length; i++)
            {
                SurrenderAndStep(sim, order[i]);
                // The outcome after surrender number i + 1.
                MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
                Assert.That(outcome.StandingTeams.Count, Is.EqualTo(standingAfter[i]));
                Assert.That(outcome.IsDecided, Is.EqualTo(i == order.Length - 1), "after surrender " + (i + 1));
            }
            Assert.That(MatchOutcome.Evaluate(sim.Players).Winner, Is.EqualTo(T(1)));
        }

        /// <summary>The hand-over policy is told about leaves, disconnects and reconnects, after the state change.</summary>
        [Test]
        public void HandOverPolicy_ReceivesHooksAfterStateChange()
        {
            // Policy that records each call.
            RecordingPolicy policy = new RecordingPolicy();
            Simulation sim = Start(Setups((0, 0), (1, 0), (2, 1)), policy);
            Assert.That(sim.HandOverPolicy, Is.SameAs(policy));
            sim.Submit(new SetConnectionStateCommand(0, PlayerId.None, 0, P(1), ConnectionState.Disconnected));
            sim.Submit(new SetConnectionStateCommand(1, PlayerId.None, 0, P(1), ConnectionState.Connected));
            sim.Submit(new SurrenderCommand(2, P(1), 0, P(1)));
            sim.Step(3);
            Assert.That(policy.Calls, Is.EqualTo(new[]
            {
                "disconnected P1 disconnected",
                "reconnected P1 active",
                "left P1 surrendered from active ally P0",
            }));
            Expect.Throws<InvalidOperationException>(() => sim.SetHandOverPolicy(StateOnlyHandOverPolicy.Instance));
        }

        /// <summary>Test policy that records every hook as text, including the state seen and the hand-over target.</summary>
        private sealed class RecordingPolicy : IHandOverPolicy
        {
            /// <summary>One line per hook call, in call order.</summary>
            public List<string> Calls { get; } = new List<string>();

            /// <summary>Policy ID for the state hash.</summary>
            public string Id => "recording_test";

            /// <summary>Writes the number of recorded calls so the hash reflects the policy's state.</summary>
            public void WriteHash(StateHasher hasher)
            {
                hasher.Write(Calls.Count);
            }

            /// <summary>Records the leave with the state, previous state and first active ally.</summary>
            /// <param name="sim">The stepping simulation.</param>
            /// <param name="player">The player who left.</param>
            /// <param name="previousState">State before leaving.</param>
            public void OnPlayerLeftMatch(Simulation sim, Player player, PlayerState previousState)
            {
                Calls.Add("left " + player.Id + " " + PlayerStateRules.Name(player.State) + " from " + PlayerStateRules.Name(previousState)
                    + " ally " + sim.Players.FirstActiveAllyOf(player.Id));
            }

            /// <summary>Records the disconnect with the state seen.</summary>
            /// <param name="sim">The stepping simulation.</param>
            /// <param name="player">The player who dropped.</param>
            public void OnPlayerDisconnected(Simulation sim, Player player)
            {
                Calls.Add("disconnected " + player.Id + " " + PlayerStateRules.Name(player.State));
            }

            /// <summary>Records the reconnect with the state seen.</summary>
            /// <param name="sim">The stepping simulation.</param>
            /// <param name="player">The player who returned.</param>
            public void OnPlayerReconnected(Simulation sim, Player player)
            {
                Calls.Add("reconnected " + player.Id + " " + PlayerStateRules.Name(player.State));
            }
        }
    }
}
