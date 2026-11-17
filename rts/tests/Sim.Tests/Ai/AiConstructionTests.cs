// rts/tests/Sim.Tests/Ai/AiConstructionTests.cs
// Tests of the skirmish AI under the worker construction rule (rts/docs/specs/worker_construction.md section 9.3 and 11.2, WCA-1 to WCA-8): the
// builder is the nearest free worker, a pending build order counts as a structure under construction, a worker that is placing is not idle, an
// unexplored site is scouted before the order, the sites of pending orders are skipped, cancelled orders are handled, static defences count
// pending orders, and an enemy's build order is hidden. Each test builds a real simulation on a shipped map and runs AiBrain.Decide on the view.
// Part of Rts.Sim.Tests. Test code only.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Ai
{
    [TestFixture]
    public class AiConstructionTests
    {
        /// <summary>The Skai player (slot 0).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>The Water player (slot 1).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Simulation ticks per second in the shipped tuning. Unit: ticks per second.</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data set, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Script sequence number for the next command. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The shipped aggressive profile.</summary>
        private static AiProfile Aggressive => AiProfile.FromDataSet(Data, "aggressive");

        /// <summary>
        /// A started simulation with both HQs on the map spawns and <paramref name="workers"/> workers each beside their HQ. With
        /// <paramref name="explore"/> every tile counts as explored, otherwise only what the units see.
        /// </summary>
        private Simulation Game(string mapId, int workers, bool explore)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(mapId)));
            sim.Start();
            // The spawn points of the two players. Unit: tiles.
            FixVec2 s0 = sim.Map.Spawns[0];
            FixVec2 s1 = sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to workers - 1.
            for (int i = 0; i < workers; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "water.tender", P1, s1 - offset);
            }
            if (explore)
            {
                ExploreKit.ExploreAll(sim);
            }
            SetStock(sim, P0, 500, 0);
            SetStock(sim, P1, 500, 0);
            return sim;
        }

        /// <summary>Spawns a unit with a script command, steps once and returns its ID.</summary>
        private EntityId Spawn(Simulation sim, string definition, PlayerId owner, FixVec2 position)
        {
            sim.Submit(new SpawnUnitCommand(sim.Clock.Tick, PlayerId.None, _sequence++, definition, owner, position));
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

        /// <summary>Sets the stock of a player.</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>Own workers of a player in ascending ID order.</summary>
        private static List<EntityId> Workers(Simulation sim, PlayerId owner)
        {
            List<EntityId> ids = new List<EntityId>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Economy.CanGather)
                {
                    ids.Add(unit.Id);
                }
            }
            return ids;
        }

        /// <summary>The unit with an ID (the test fails when it does not exist).</summary>
        private static Unit UnitOf(Simulation sim, EntityId id)
        {
            Assert.That(sim.Units.TryGet(id, out Unit? unit), Is.True, "unit " + id);
            return unit!;
        }

        /// <summary>Decides for P0 with the given (or a fresh) memory.</summary>
        private static List<AiIntent> DecideP0(Simulation sim, AiMemory? memory = null)
        {
            AiProfile profile = Aggressive;
            return AiBrain.Decide(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory(), new DeterministicRandom(1));
        }

        /// <summary>The build intents for a definition, in order.</summary>
        private static List<AiIntent> Builds(List<AiIntent> intents, string definition)
        {
            List<AiIntent> found = new List<AiIntent>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == AiIntentKind.Build && intent.DefinitionId == definition)
                {
                    found.Add(intent);
                }
            }
            return found;
        }

        /// <summary>Submits a build order for a P0 worker and steps once.</summary>
        private void Order(Simulation sim, EntityId worker, string definition, int x, int y)
        {
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, worker, definition, x, y));
            sim.Step();
        }

        /// <summary>WCA-1: the builder is the free worker nearest the site (ties to the lower ID); a worker with a build order is not a candidate.</summary>
        [Test]
        public void WCA1_BuilderIsTheNearestFreeWorker()
        {
            Simulation sim = Game("greybox_1v1", 1, true);
            // The first build intent: the Roost, and its site.
            List<AiIntent> first = Builds(DecideP0(sim), "skai.roost");
            Assert.That(first.Count, Is.EqualTo(1));
            // Centre of the site tile. Unit: tiles.
            FixVec2 site = new FixVec2(Fix64.FromInt(first[0].TileX) + Fix64.Half, Fix64.FromInt(first[0].TileY) + Fix64.Half);
            // W3 stands 3 tiles east of the site and W4 3 tiles north of it: the same distance, W3 has the lower ID.
            EntityId idle = Spawn(sim, "skai.fledgling", P0, site + new FixVec2(Fix64.FromInt(3), Fix64.Zero));
            EntityId gathering = Spawn(sim, "skai.fledgling", P0, site + new FixVec2(Fix64.Zero, Fix64.FromInt(3)));
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, gathering, 0));
            sim.Step();
            SetStock(sim, P0, 500, 0);
            List<AiIntent> second = Builds(DecideP0(sim), "skai.roost");
            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That((second[0].TileX, second[0].TileY), Is.EqualTo((first[0].TileX, first[0].TileY)), "the site does not move");
            Assert.That(second[0].Unit, Is.EqualTo(idle), "equal distance: the lower ID, although the other one is gathering");
            // W3 gets a build order elsewhere (a depot beside field 6, explored): the next worker is W4.
            Order(sim, idle, "skai.perch_depot", 21, 2);
            SetStock(sim, P0, 500, 0);
            List<AiIntent> third = Builds(DecideP0(sim), "skai.roost");
            Assert.That(third.Count, Is.EqualTo(1));
            Assert.That(third[0].Unit, Is.EqualTo(gathering), "the worker with a build order is skipped");
        }

        /// <summary>WCA-2: a pending order counts as the structure under construction: no second supply building and the build order step is met.</summary>
        [Test]
        public void WCA2_PendingOrderCountsAsBuilt()
        {
            Simulation sim = Game("greybox_1v1", 2, true);
            List<AiIntent> first = Builds(DecideP0(sim), "skai.roost");
            Assert.That(first.Count, Is.EqualTo(1), "the AI wants its first Roost (supply building and first build order step)");
            Order(sim, first[0].Unit, "skai.roost", first[0].TileX, first[0].TileY);
            Assert.That(sim.Units.TryGet(first[0].Unit, out Unit? worker) && worker.BuildState != BuildOrderState.None, Is.True, "the order is pending");
            SetStock(sim, P0, 500, 0);
            Assert.That(Builds(DecideP0(sim), "skai.roost"), Is.Empty, "no second Roost while the first is ordered");
        }

        /// <summary>WCA-3: a worker that is placing (no gather loop, no move target) is not idle: no Gather or Move for it.</summary>
        [Test]
        public void WCA3_PlacingWorkerIsNotIdle()
        {
            Simulation sim = Game("greybox_1v1", 1, true);
            EntityId worker = Workers(sim, P0)[0];
            Assert.That(DecideP0(sim).Exists(i => i.Kind == AiIntentKind.Gather && i.Unit == worker), Is.True, "an idle worker is sent to gather");
            // Order a Roost next to the worker and wait until it is placing.
            Order(sim, worker, "skai.roost", 11, 8);
            Unit unit = UnitOf(sim, worker);
            // Ticks stepped while the worker walks. Unit: ticks. Range: 0 to 400.
            for (int i = 0; i < 400 && unit.BuildState != BuildOrderState.Placing; i++)
            {
                sim.Step();
            }
            Assert.That(unit.BuildState, Is.EqualTo(BuildOrderState.Placing));
            Assert.That(unit.HasMoveTarget, Is.False);
            SetStock(sim, P0, 500, 0);
            List<AiIntent> intents = DecideP0(sim);
            Assert.That(intents.Exists(i => i.Unit == worker && (i.Kind == AiIntentKind.Gather || i.Kind == AiIntentKind.Move)), Is.False);
        }

        /// <summary>
        /// WCA-4: a depot due at a site nobody has seen: the think sends the builder to the site centre instead of ordering, and sets the retry time;
        /// once the tile is explored and build_retry_seconds have passed it builds there.
        /// </summary>
        [Test]
        public void WCA4_UnexploredSiteIsScoutedFirst()
        {
            Simulation sim = Game("prototype_1v1", 2, false);
            // The first producers stand, so the first thing the AI wants to build is the expansion depot.
            Spawn(sim, "skai.roost", P0, sim.Map.Spawns[0] + new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            Spawn(sim, "water.tidepool", P1, sim.Map.Spawns[1] - new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            // Ticks to let the game reach the expansion time. Unit: ticks.
            while (sim.Clock.Tick < 240 * TicksPerSecond)
            {
                sim.Step();
            }
            sim.Submit(new GatherCommand(sim.Clock.Tick, P0, _sequence++, Workers(sim, P0)[1], 0));
            sim.Step();
            SetStock(sim, P0, 200, 0);
            Assert.That(sim.Vision.IsTileExplored(new TeamId(0), 27, 13), Is.False, "the depot site beside the first natural field is unexplored");
            AiMemory memory = new AiMemory();
            List<AiIntent> intents = DecideP0(sim, memory);
            Assert.That(Builds(intents, "skai.perch_depot"), Is.Empty, "no order before the site is explored");
            // The move that scouts the site.
            AiIntent? scout = intents.Find(i => i.Kind == AiIntentKind.Move && (i.Point - new FixVec2(Fix64.Parse("27.5"), Fix64.Parse("13.5"))).LengthSquared() == Fix64.Zero);
            Assert.That(scout, Is.Not.Null, "the builder is sent to the site centre; intents: " + string.Join(";", intents.ConvertAll(i => i.Kind + "@" + i.Point + "/" + i.TileX + "," + i.TileY)));
            Assert.That(UnitOf(sim, scout!.Unit).Definition.Economy.CanGather, Is.True);
            Assert.That(memory.LastBuildAttemptTick.ContainsKey("skai.perch_depot"), Is.True, "the retry timer is set");
            // The walk explores the tile; after the retry time the AI builds there.
            ExploreKit.ExploreAll(sim);
            // Ticks of build_retry_seconds. Unit: ticks. Range: 20 to 1200.
            int retry = 5 * TicksPerSecond;
            for (int i = 0; i < retry; i++)
            {
                sim.Step();
            }
            SetStock(sim, P0, 200, 0);
            List<AiIntent> later = Builds(DecideP0(sim, memory), "skai.perch_depot");
            Assert.That(later.Count, Is.EqualTo(1));
            Assert.That((later[0].TileX, later[0].TileY), Is.EqualTo((27, 13)));
        }

        /// <summary>WCA-5: the site of an own pending order is skipped: the next build goes on another tile.</summary>
        [Test]
        public void WCA5_PendingSitesAreSkipped()
        {
            Simulation sim = Game("greybox_1v1", 3, true);
            List<AiIntent> first = Builds(DecideP0(sim), "skai.roost");
            Assert.That(first.Count, Is.EqualTo(1));
            // A depot ordered on the Roost's first-choice tile.
            Order(sim, Workers(sim, P0)[2], "skai.perch_depot", first[0].TileX, first[0].TileY);
            SetStock(sim, P0, 500, 0);
            List<AiIntent> second = Builds(DecideP0(sim), "skai.roost");
            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That((second[0].TileX, second[0].TileY), Is.Not.EqualTo((first[0].TileX, first[0].TileY)), "the tile with a pending order is not offered again");
        }

        /// <summary>
        /// WCA-6: cancelled own orders: site_blocked remembers the tile and clears the retry time; site_occupied only clears the retry time;
        /// new_order changes neither.
        /// </summary>
        [Test]
        public void WCA6_CancelHandling()
        {
            // Reason, whether a blocking structure (true) or a unit (false) is put on the site, and the expectations.
            foreach ((string reason, bool structure) in new[] { ("site_blocked", true), ("site_occupied", false), ("new_order", false) })
            {
                Simulation sim = Game("greybox_1v1", 1, true);
                SkirmishAiController controller = new SkirmishAiController(P0, Aggressive);
                controller.OnMatchStart(sim);
                EntityId worker = Workers(sim, P0)[0];
                // Site 5 tiles from the worker, east.
                Order(sim, worker, "skai.roost", 13, 9);
                controller.Memory.LastBuildAttemptTick["skai.roost"] = sim.Clock.Tick;
                SimulationCommandSink sink = new SimulationCommandSink(sim);
                if (reason == "new_order")
                {
                    sim.Submit(new StopCommand(sim.Clock.Tick, P0, _sequence++, worker));
                }
                else
                {
                    Spawn(sim, structure ? "water.coral_turret" : "water.tender", P1, new FixVec2(Fix64.FromInt(13) + Fix64.Half, Fix64.FromInt(9) + Fix64.Half));
                }
                // Reason found in the events of the last tick, or empty.
                string seen = string.Empty;
                // Ticks stepped while waiting for the cancellation. Unit: ticks. Range: 0 to 600.
                for (int i = 0; i < 600 && seen.Length == 0; i++)
                {
                    sim.Step();
                    foreach (SimEvent evt in sim.Events.Current)
                    {
                        if (evt is BuildOrderCancelledEvent cancelled)
                        {
                            seen = cancelled.Reason;
                        }
                    }
                }
                Assert.That(seen, Is.EqualTo(reason));
                controller.Think(sim, sink);
                Assert.That(controller.Memory.RejectedSites.Contains(sim.Terrain.IndexOf(13, 9)), Is.EqualTo(reason == "site_blocked"), reason);
                Assert.That(controller.Memory.LastBuildAttemptTick.ContainsKey("skai.roost"), Is.EqualTo(reason == "new_order"), reason);
            }
        }

        /// <summary>WCA-7: a pending static defence order counts as a static defence under construction and as one standing near the HQ.</summary>
        [Test]
        public void WCA7_StaticDefenceCountsPendingOrders()
        {
            Simulation sim = Game("greybox_1v1", 2, true);
            AiProfile profile = Aggressive;
            AiBrain.EconomyProbe before = AiBrain.Probe(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), new AiMemory())!;
            Assert.That(before.StaticDefenceUnderConstruction, Is.False);
            Assert.That(before.StaticDefencesAtHq, Is.EqualTo(0));
            Order(sim, Workers(sim, P0)[0], "skai.perch_tower", 11, 9);
            AiBrain.EconomyProbe after = AiBrain.Probe(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), new AiMemory())!;
            Assert.That(after.StaticDefenceUnderConstruction, Is.True);
            Assert.That(after.StaticDefencesAtHq, Is.EqualTo(1));
        }

        /// <summary>WCA-8: an enemy worker's build order is hidden: its view shows no build state and no site.</summary>
        [Test]
        public void WCA8_EnemyBuildOrdersAreHidden()
        {
            Simulation sim = Game("greybox_1v1", 1, true);
            // A Water worker next to the Skai HQ so that Skai sees it, with a build order of its own.
            EntityId enemy = Spawn(sim, "water.tender", P1, sim.Map.Spawns[0] + new FixVec2(Fix64.FromInt(3), Fix64.FromInt(3)));
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P1, _sequence++, enemy, "water.tidepool", 12, 12));
            sim.Step();
            Assert.That(UnitOf(sim, enemy).BuildState, Is.Not.EqualTo(BuildOrderState.None), "the enemy order is pending in the simulation");
            AiUnitView? seen = null;
            foreach (AiUnitView unit in AiWorldView.Build(sim, P0).VisibleEnemies)
            {
                if (unit.Id == enemy)
                {
                    seen = unit;
                }
            }
            Assert.That(seen, Is.Not.Null, "Skai sees the enemy worker");
            Assert.That(seen!.BuildState, Is.EqualTo(BuildOrderState.None));
            Assert.That(seen.BuildDefinitionId, Is.EqualTo(string.Empty));
            Assert.That(seen.BuildTileX, Is.EqualTo(-1));
            Assert.That(seen.BuildTileY, Is.EqualTo(-1));
            Assert.That(seen.HasBuildOrder, Is.False);
        }
    }
}
