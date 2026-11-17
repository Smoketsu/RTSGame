// rts/tests/Sim.Tests/Ai/AiEconomyTests.cs
// Tests for package B of rts/docs/specs/skirmish_ai_v2.md (the AI economy: expansion and anti-freeze): field memory (B-1), served
// fields (B-2), the worker target (B-3), depots by time and by remaining share (B-4, B-5), the limits and the safety rule of
// depots (B-6, B-7), income-gated reservations (B-8), rebalancing and rare fields (B-9, B-10), the long haul (B-11), HQ relocation
// (B-12 to B-17), workers following the HQ (B-18), all in (B-19), determinism (B-20) and profile validation (B-21); plus the
// expected depot tiles on prototype_1v1. Part of Rts.Sim.Tests. Each test builds a real simulation on a shipped map, spawns units
// with script commands, steps to the wanted tick and runs AiBrain.Decide on the resulting view (the view type cannot be built by hand).
// Field indices on greybox_1v1: 0 (4, 10) and 1 (10, 4) P0 main, 2 (59, 53) and 3 (53, 59) P1 main, 4 (8, 52) and 5 (55, 11) rare,
// 6 (21, 10) and 7 (21, 4) P0 natural, 8 (42, 53) and 9 (42, 59) P1 natural, 10 (12, 49) and 11 (51, 14) flank fields.
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
    /// <summary>Skirmish AI v2 package B: expansion, relocation and the anti-freeze rules.</summary>
    [TestFixture]
    public class AiEconomyTests
    {
        /// <summary>Player slot 0 (Skai).</summary>
        private static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player slot 1 (Water).</summary>
        private static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Ticks per second of the shipped match settings. Unit: ticks per second. Range: 20 (asserted in <see cref="Game"/>).</summary>
        private const int TicksPerSecond = 20;

        /// <summary>The shipped data, loaded once.</summary>
        private static DataSet? _data;

        /// <summary>The shipped data set.</summary>
        private static DataSet Data => _data ??= TestData.LoadAll();

        /// <summary>Next script sequence number for spawns and applied intents. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>The shipped aggressive profile.</summary>
        private static AiProfile Aggressive => AiProfile.FromDataSet(Data, "aggressive");

        /// <summary>Converts seconds to ticks at the shipped rate. Unit: ticks.</summary>
        private static int Ticks(int seconds)
        {
            return seconds * TicksPerSecond;
        }

        /// <summary>
        /// A started two-player match on <paramref name="mapId"/>: each player has its HQ at its spawn, <paramref name="workers"/>
        /// workers beside it and one producer (Roost or Tidepool, so the build order has nothing to buy), cycles off, seed 1234.
        /// </summary>
        private Simulation Game(string mapId = "greybox_1v1", int workers = 2, bool producers = true, FixVec2? hq0 = null, FixVec2? hq1 = null)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap(mapId)));
            sim.Start();
            Assert.That(sim.Clock.TicksPerSecond, Is.EqualTo(TicksPerSecond));
            // The spawn points of the two players. Unit: tiles.
            FixVec2 s0 = hq0 ?? sim.Map.Spawns[0];
            FixVec2 s1 = hq1 ?? sim.Map.Spawns[1];
            Spawn(sim, "skai.great_aerie", P0, s0);
            Spawn(sim, "water.grand_shell", P1, s1);
            // Worker index. Range: 0 to workers - 1.
            for (int i = 0; i < workers; i++)
            {
                // Offset of this worker from the HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "skai.fledgling", P0, s0 + offset);
                Spawn(sim, "water.tender", P1, s1 - offset);
            }
            if (producers)
            {
                Spawn(sim, "skai.roost", P0, s0 + new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
                Spawn(sim, "water.tidepool", P1, s1 - new FixVec2(Fix64.FromInt(5), Fix64.FromInt(-2)));
            }
            Assert.That(Hq(sim, P0).HqState, Is.EqualTo(HqState.Rooted), "the Skai HQ starts Rooted beside its fields");
            Assert.That(Hq(sim, P1).HqState, Is.EqualTo(HqState.Rooted), "the Water HQ starts Rooted beside its fields");
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

        /// <summary>Spawns a unit at a tile centre, given as the tile.</summary>
        private EntityId SpawnAtTile(Simulation sim, string definition, PlayerId owner, int x, int y)
        {
            return Spawn(sim, definition, owner, new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half));
        }

        /// <summary>Steps until the clock reaches <paramref name="tick"/>.</summary>
        private static void StepTo(Simulation sim, int tick)
        {
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
            }
        }

        /// <summary>The lowest-ID HQ of a player.</summary>
        private static Unit Hq(Simulation sim, PlayerId owner)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner == owner && unit.Definition.Hq != null)
                {
                    return unit;
                }
            }
            throw new AssertionException("no HQ");
        }

        /// <summary>The worker IDs of a player in ascending order.</summary>
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

        /// <summary>Sets a player's stock (test-only access to the internal setter).</summary>
        private static void SetStock(Simulation sim, PlayerId player, int common, int rare)
        {
            Assert.That(sim.Players.TryGet(player, out Player? record), Is.True);
            record!.SetStock(common, rare);
        }

        /// <summary>Orders a worker to gather a field and steps once, so it is no longer idle.</summary>
        private void Gather(Simulation sim, PlayerId owner, EntityId worker, int field)
        {
            sim.Submit(new GatherCommand(sim.Clock.Tick, owner, _sequence++, worker, field));
            sim.Step();
        }

        /// <summary>Takes everything out of a field.</summary>
        private static void Empty(Simulation sim, int field)
        {
            sim.Resources[field].Take(1000000);
        }

        /// <summary>Leaves only <paramref name="remaining"/> in a field.</summary>
        private static void Leave(Simulation sim, int field, int remaining)
        {
            sim.Resources[field].Take(sim.Resources[field].Remaining - remaining);
        }

        /// <summary>Decides for a player (the faction's plan) with the given (or a fresh) memory and generator.</summary>
        private static List<AiIntent> Decide(Simulation sim, PlayerId player, AiProfile profile, AiMemory? memory = null, DeterministicRandom? rng = null)
        {
            return AiBrain.Decide(AiWorldView.Build(sim, player), profile, profile.RequireFaction(player == P0 ? "skai" : "water"),
                memory ?? new AiMemory(), rng ?? new DeterministicRandom(1));
        }

        /// <summary>Decides for P0 with the shipped aggressive profile.</summary>
        private static List<AiIntent> DecideP0(Simulation sim, AiMemory? memory = null)
        {
            return Decide(sim, P0, Aggressive, memory);
        }

        /// <summary>The shipped aggressive profile with text edits applied (each old text must occur once).</summary>
        private static AiProfile Edited(params string[] oldNewPairs)
        {
            // The file text being edited.
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive.json"));
            // Index into the old/new pairs. Range: even numbers from 0.
            for (int i = 0; i < oldNewPairs.Length; i += 2)
            {
                Assert.That(text.Contains(oldNewPairs[i]), Is.True, oldNewPairs[i]);
                text = text.Replace(oldNewPairs[i], oldNewPairs[i + 1]);
            }
            return AiProfile.Parse(text, "ai/aggressive.json", Data.Content);
        }

        /// <summary>The intents of one kind, in order.</summary>
        private static List<AiIntent> Of(List<AiIntent> intents, AiIntentKind kind)
        {
            List<AiIntent> found = new List<AiIntent>();
            foreach (AiIntent intent in intents)
            {
                if (intent.Kind == kind)
                {
                    found.Add(intent);
                }
            }
            return found;
        }

        /// <summary>The build intents for <paramref name="definition"/>, in order.</summary>
        private static List<AiIntent> Builds(List<AiIntent> intents, string definition)
        {
            List<AiIntent> found = new List<AiIntent>();
            foreach (AiIntent intent in Of(intents, AiIntentKind.Build))
            {
                if (intent.DefinitionId == definition)
                {
                    found.Add(intent);
                }
            }
            return found;
        }

        /// <summary>Submits the intents as commands of <paramref name="player"/> and steps once.</summary>
        private void Apply(Simulation sim, PlayerId player, List<AiIntent> intents)
        {
            foreach (AiIntent intent in intents)
            {
                sim.Submit(intent.ToCommand(sim.Clock.Tick, player, _sequence++));
            }
            sim.Step();
        }

        /// <summary>The probe of P0's economy on the current view.</summary>
        private static AiBrain.EconomyProbe Probe(Simulation sim, AiMemory? memory = null, AiProfile? custom = null)
        {
            AiProfile profile = custom ?? Aggressive;
            return AiBrain.Probe(AiWorldView.Build(sim, P0), profile, profile.RequireFaction("skai"), memory ?? new AiMemory())!;
        }

        /// <summary>A known-amount array holding every field's start amount, for hand edits.</summary>
        private static int[] StartAmounts(Simulation sim)
        {
            int[] known = new int[sim.Resources.Count];
            // Field index. Range: 0 to field count - 1.
            for (int i = 0; i < known.Length; i++)
            {
                known[i] = sim.Resources[i].Initial;
            }
            return known;
        }

        // ------------------------------------------------------------------ B-1 to B-3

        /// <summary>B-1: the first think knows the start amounts; a visible field updates, a field out of sight keeps its last known amount.</summary>
        [Test]
        public void B1_FieldMemory_VisibleFieldsUpdate_HiddenFieldsKeepTheLastKnownAmount()
        {
            Simulation sim = Game();
            AiMemory memory = new AiMemory();
            DecideP0(sim, memory);
            for (int i = 0; i < sim.Resources.Count; i++)
            {
                Assert.That(memory.KnownFieldRemaining[i], Is.EqualTo(sim.Resources[i].Initial), "field " + i);
            }
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.IsVisible(4, 10), Is.True, "main field 0 is in sight");
            Assert.That(view.IsVisible(42, 59), Is.False, "field 9 is far from every Skai unit");
            Leave(sim, 0, 2000);
            Leave(sim, 9, 1000);
            DecideP0(sim, memory);
            Assert.That(memory.KnownFieldRemaining[0], Is.EqualTo(2000), "visible: live amount");
            Assert.That(memory.KnownFieldRemaining[9], Is.EqualTo(3000), "hidden: last known amount");
            Empty(sim, 9);
            DecideP0(sim, memory);
            Assert.That(memory.KnownFieldRemaining[9], Is.EqualTo(3000), "still hidden");
        }

        /// <summary>B-2: served fields: near a complete depot, near a depot under construction; not near an Uprooted HQ; not when known empty.</summary>
        [Test]
        public void B2_ServedFields()
        {
            // Complete depot 8 tiles from field 6 (21, 10): tile (21, 2).
            Simulation sim = Game();
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 2);
            Assert.That(Probe(sim).Served, Does.Contain(6));
            Assert.That(Probe(sim).Served, Does.Contain(0), "the Rooted HQ still serves its own fields");

            // Depot under construction at the same place.
            sim = Game();
            SetStock(sim, P0, 500, 0);
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, Workers(sim, P0)[0], "skai.perch_depot", 21, 2));
            sim.Step();
            Assert.That(Probe(sim).Served, Does.Contain(6), "a depot under construction already serves");

            // Uprooted HQ: field 0 is 4.3 tiles away but an Uprooted HQ is not a drop-off.
            sim = Game();
            sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, _sequence++, Hq(sim, P0).Id));
            // Steps spent waiting for the uproot. Range: 0 to 400.
            for (int i = 0; i < 400 && Hq(sim, P0).HqState != HqState.Uprooted; i++)
            {
                sim.Step();
            }
            Assert.That(Hq(sim, P0).HqState, Is.EqualTo(HqState.Uprooted));
            Assert.That(Probe(sim).Served, Is.Empty);

            // A field known empty is not served.
            sim = Game();
            Empty(sim, 0);
            Assert.That(Probe(sim).Served, Does.Not.Contain(0));
            Assert.That(Probe(sim).Served, Does.Contain(1));
        }

        /// <summary>B-3: the worker target is workers_per_field x served common + rare_workers_per_field x served rare; at least workers_per_field while any field is live.</summary>
        [Test]
        public void B3_WorkerTarget()
        {
            // HQ serves fields 0 and 1; a depot at tile (8, 60) serves the rare field 4 (8.0 tiles) but not field 10 (11.7 tiles).
            Simulation sim = Game();
            SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
            AiBrain.EconomyProbe probe = Probe(sim);
            Assert.That(probe.Served, Is.EqualTo(new List<int> { 0, 1, 4 }));
            Assert.That(probe.WantedWorkers, Is.EqualTo(8), "min(24, 3 x 2 + 2 x 1)");
            // No served field but live fields elsewhere: workers_per_field.
            sim = Game();
            sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, _sequence++, Hq(sim, P0).Id));
            // Steps spent waiting for the uproot. Range: 0 to 400.
            for (int i = 0; i < 400 && Hq(sim, P0).HqState != HqState.Uprooted; i++)
            {
                sim.Step();
            }
            probe = Probe(sim);
            Assert.That(probe.Served, Is.Empty);
            Assert.That(probe.WantedWorkers, Is.EqualTo(3));
        }

        // ------------------------------------------------------------------ B-4 to B-8

        /// <summary>B-4: at 240 s with no depot, bank 200 and one gathering worker, one depot is built at the expected tile by the lowest-ID gathering worker.</summary>
        [Test]
        public void B4_ExpandByTime_Greybox()
        {
            Simulation sim = Game();
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 200, 0);
            SetStock(sim, P1, 200, 0);
            List<EntityId> skaiWorkers = Workers(sim, P0);
            Gather(sim, P0, skaiWorkers[1], 0);
            List<EntityId> waterWorkers = Workers(sim, P1);
            Gather(sim, P1, waterWorkers[1], 2);
            SetStock(sim, P0, 200, 0);
            SetStock(sim, P1, 200, 0);
            AiMemory memory = new AiMemory();
            List<AiIntent> intents = DecideP0(sim, memory);
            List<AiIntent> builds = Builds(intents, "skai.perch_depot");
            Assert.That(builds.Count, Is.EqualTo(1));
            Assert.That(builds[0].TileX, Is.EqualTo(21));
            Assert.That(builds[0].TileY, Is.EqualTo(7));
            Assert.That(builds[0].Unit, Is.EqualTo(skaiWorkers[1]), "the lowest-ID gathering worker, not the idle lowest-ID worker");
            Assert.That(memory.DepotsPlaced, Is.EqualTo(0), "DepotsPlaced counts depots among the own units, not orders");
            Apply(sim, P0, intents);
            DecideP0(sim, memory);
            Assert.That(memory.DepotsPlaced, Is.EqualTo(1), "the depot now stands (under construction)");
            // The mirror: Water at slot 1 builds its Pearl Depot at (42, 56).
            AiMemory waterMemory = new AiMemory();
            List<AiIntent> waterBuilds = Builds(Decide(sim, P1, Aggressive, waterMemory), "water.pearl_depot");
            Assert.That(waterBuilds.Count, Is.EqualTo(1));
            Assert.That(waterBuilds[0].TileX, Is.EqualTo(42));
            Assert.That(waterBuilds[0].TileY, Is.EqualTo(56));
            Assert.That(waterBuilds[0].Unit, Is.EqualTo(waterWorkers[1]));
        }

        /// <summary>
        /// B-4 on prototype_1v1 (96 x 96, developer-approved layout): the depot tiles the rule gives beside the first natural field,
        /// computed with an independent script from the spec's rule: player 0 (HQ at (12.5, 12.5), target field 3 at (26, 16)) builds
        /// at (27, 13); player 1 (HQ at (83.5, 83.5), target field 11 at (69, 79)) builds at (68, 82), the exact mirror (95 - x, 95 - y).
        /// </summary>
        [Test]
        public void B4_ExpandByTime_Prototype()
        {
            Simulation sim = Game("prototype_1v1");
            StepTo(sim, Ticks(240));
            Gather(sim, P0, Workers(sim, P0)[1], 0);
            Gather(sim, P1, Workers(sim, P1)[1], 8);
            SetStock(sim, P0, 200, 0);
            SetStock(sim, P1, 200, 0);
            List<AiIntent> skai = Builds(DecideP0(sim), "skai.perch_depot");
            Assert.That(skai.Count, Is.EqualTo(1));
            Assert.That(skai[0].TileX, Is.EqualTo(27));
            Assert.That(skai[0].TileY, Is.EqualTo(13));
            List<AiIntent> water = Builds(Decide(sim, P1, Aggressive), "water.pearl_depot");
            Assert.That(water.Count, Is.EqualTo(1));
            Assert.That(water[0].TileX, Is.EqualTo(68));
            Assert.That(water[0].TileY, Is.EqualTo(82));
        }

        /// <summary>
        /// B-4b: a depot site that the AI cannot see is blocked (an enemy structure in the fog) is rejected tile_blocked by the simulation;
        /// the AI must learn from that and not place on the same tile again while it still cannot see it (the player at the north-east
        /// spawn of prototype_1v1 kept re-sending one rejected depot for hundreds of seconds). Water's depot site beside its first
        /// natural field is (68, 82); an enemy depot is put there out of Water's sight.
        /// </summary>
        [Test]
        public void B4b_RejectedDepotSiteIsNotRetried()
        {
            Simulation sim = Game("prototype_1v1");
            StepTo(sim, Ticks(240));
            Gather(sim, P1, Workers(sim, P1)[1], 8);
            SpawnAtTile(sim, "skai.perch_depot", P0, 68, 82);
            SetStock(sim, P1, 400, 0);
            // The controller of the Water player (starts from a fresh memory).
            SkirmishAiController controller = new SkirmishAiController(P1, Aggressive);
            controller.OnMatchStart(sim);
            // Tiles of the Water depot placements submitted over the next 40 seconds, one entry per placement.
            List<(int X, int Y)> placed = new List<(int X, int Y)>();
            // Tick at which the loop stops. Unit: ticks.
            int end = sim.Clock.Tick + Ticks(40);
            // Sink that submits the controller's commands to the simulation.
            SimulationCommandSink sink = new SimulationCommandSink(sim);
            while (sim.Clock.Tick < end)
            {
                controller.Think(sim, sink);
                foreach (Command command in controller.LastCommands)
                {
                    if (command is BuildStructureCommand build && build.DefinitionId == "water.pearl_depot")
                    {
                        placed.Add((build.TileX, build.TileY));
                    }
                }
                sim.Step();
                SetStock(sim, P1, 400, 0);
            }
            Assert.That(placed.Count, Is.GreaterThan(0), "Water tries a depot at all");
            Assert.That(placed.Contains((68, 82)), Is.True, "the hidden enemy depot is on Water's first choice");
            Assert.That(placed.FindAll(t => t.X == 68 && t.Y == 82).Count, Is.EqualTo(1), "the rejected tile is tried once");
        }

        /// <summary>
        /// B-4c: two Water AIs on prototype_1v1 (exactly 180-degree symmetric) place their first structures (supply, production, depot)
        /// on exact mirror tiles (95 - x, 95 - y), in the same order. Checked over the first <c>checkedPlacements</c> placements, played
        /// before the armies decide anything about the sites (the lines up to there are identical for both sides).
        /// </summary>
        [Test]
        public void B4c_FirstPlacementsAreMirrorImages()
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "water", "Water A"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water B"),
            };
            Simulation sim = new Simulation(new SimulationConfig(Data.CreateMatchSettings(1234), Data.Content, players,
                BuiltInMechanics.CreateRegistry(), Data.Tuning, Data.RequireMap("prototype_1v1")));
            sim.Start();
            Spawn(sim, "water.grand_shell", P0, sim.Map.Spawns[0]);
            Spawn(sim, "water.grand_shell", P1, sim.Map.Spawns[1]);
            // Worker index. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                // Offset of this worker from its HQ. Unit: tiles.
                FixVec2 offset = new FixVec2(Fix64.FromInt(-2 + i % 3), Fix64.FromInt(1 + i / 3));
                Spawn(sim, "water.tender", P0, sim.Map.Spawns[0] + offset);
                Spawn(sim, "water.tender", P1, sim.Map.Spawns[1] - offset);
            }
            // Placements to compare per player. Unit: placements. Range: 1 to 12.
            const int checkedPlacements = 4;
            List<IPlayerController> controllers = new List<IPlayerController>
            {
                new SkirmishAiController(P0, Aggressive),
                new SkirmishAiController(P1, Aggressive),
            };
            AiHost.StartAll(sim, controllers);
            // Placements as "definition x,y" per player slot, in submission order.
            List<string>[] placed = { new List<string>(), new List<string>() };
            // Tile of each placement per slot, parallel to <c>placed</c>.
            List<(int X, int Y)>[] tiles = { new List<(int X, int Y)>(), new List<(int X, int Y)>() };
            while (sim.Clock.Tick < Ticks(200) && (placed[0].Count < checkedPlacements || placed[1].Count < checkedPlacements))
            {
                AiHost.ThinkAll(sim, controllers);
                // Controller index. Range: 0 to 1.
                for (int c = 0; c < 2; c++)
                {
                    foreach (Command command in ((SkirmishAiController)controllers[c]).LastCommands)
                    {
                        if (command is BuildStructureCommand build)
                        {
                            placed[c].Add(build.DefinitionId);
                            tiles[c].Add((build.TileX, build.TileY));
                        }
                    }
                }
                sim.Step();
            }
            Assert.That(placed[0].Count, Is.GreaterThanOrEqualTo(checkedPlacements));
            Assert.That(placed[1].Count, Is.GreaterThanOrEqualTo(checkedPlacements));
            // Placement index. Range: 0 to checkedPlacements - 1.
            for (int k = 0; k < checkedPlacements; k++)
            {
                Assert.That(placed[1][k], Is.EqualTo(placed[0][k]), "placement " + k + " is the same structure");
                Assert.That(tiles[1][k], Is.EqualTo((95 - tiles[0][k].X, 95 - tiles[0][k].Y)), "placement " + k + " is the mirror tile");
            }
        }

        /// <summary>B-5: at 100 s a depot is built early when the served fields hold 40 percent, and not at 60 percent.</summary>
        [Test]
        public void B5_ExpandByRemainingShare()
        {
            // Share left in each of the two main fields, and whether a depot is expected.
            foreach ((int left, bool expected) in new[] { (1200, true), (1800, false) })
            {
                Simulation sim = Game();
                Leave(sim, 0, left);
                Leave(sim, 1, left);
                StepTo(sim, Ticks(100));
                Gather(sim, P0, Workers(sim, P0)[1], 0);
                SetStock(sim, P0, 200, 0);
                Assert.That(Builds(DecideP0(sim), "skai.perch_depot").Count, Is.EqualTo(expected ? 1 : 0), "left " + left);
            }
        }

        /// <summary>B-6: no depot under a threat, none at max_live_depots, a depot whose fields are all empty is not live, none while one is under construction.</summary>
        [Test]
        public void B6_ExpandLimits()
        {
            // Threat: an enemy army unit stands 6 tiles from the HQ.
            Simulation sim = Game();
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 500, 0);
            SpawnAtTile(sim, "water.grasper", P1, 14, 8);
            Assert.That(Builds(DecideP0(sim), "skai.perch_depot"), Is.Empty, "a threat stops expansion");

            // max_live_depots reached: two complete depots, each with live fields in reach.
            // (A depot is due at 240 s + 150 s per depot standing, so the clock is set past 240 + 2 x 150 s.)
            sim = Game();
            StepTo(sim, Ticks(600));
            SetStock(sim, P0, 500, 0);
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            SpawnAtTile(sim, "skai.perch_depot", P0, 9, 49);
            Assert.That(Probe(sim).LiveDepots, Is.EqualTo(2));
            Assert.That(Builds(DecideP0(sim), "skai.perch_depot"), Is.Empty, "2 live depots is the limit");

            // A complete depot whose fields are all known empty is not live (limit 1: a live one blocks, an empty one does not).
            AiProfile one = Edited("\"max_live_depots\": 2", "\"max_live_depots\": 1");
            sim = Game();
            StepTo(sim, Ticks(400));
            SetStock(sim, P0, 500, 0);
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            Assert.That(Probe(sim).LiveDepots, Is.EqualTo(1));
            Assert.That(Builds(Decide(sim, P0, one), "skai.perch_depot"), Is.Empty, "one live depot with a limit of 1");
            Empty(sim, 6);
            Empty(sim, 7);
            sim.Step();
            Assert.That(Probe(sim).LiveDepots, Is.EqualTo(0), "its fields are known empty");
            Assert.That(Builds(Decide(sim, P0, one), "skai.perch_depot").Count, Is.EqualTo(1), "so a new depot is allowed");

            // None while a depot is under construction.
            sim = Game();
            StepTo(sim, Ticks(400));
            SetStock(sim, P0, 500, 0);
            sim.Submit(new BuildStructureCommand(sim.Clock.Tick, P0, _sequence++, Workers(sim, P0)[0], "skai.perch_depot", 21, 7));
            sim.Step();
            Assert.That(Probe(sim).LiveDepots, Is.EqualTo(1));
            SetStock(sim, P0, 500, 0);
            Assert.That(Builds(DecideP0(sim), "skai.perch_depot"), Is.Empty, "one depot at a time");
        }

        /// <summary>B-7: an unsafe nearest field is skipped for the next safe one; only unsafe fields and served ones still live: no depot; starved: the nearest unsafe field.</summary>
        [Test]
        public void B7_Safety()
        {
            // An enemy structure remembered at (24.5, 10.5): 3 tiles from field 6 and 6.7 from field 7, out of sight of the Skai HQ.
            // Next safe field by distance from the HQ: field 10 at (12, 49), whose depot site is (9, 49).
            Simulation sim = Game();
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 500, 0);
            AiMemory memory = EnemyStructureAt24(sim);
            List<AiIntent> builds = Builds(DecideP0(sim, memory), "skai.perch_depot");
            Assert.That(builds.Count, Is.EqualTo(1));
            Assert.That(builds[0].TileX, Is.EqualTo(9));
            Assert.That(builds[0].TileY, Is.EqualTo(49));

            // Only unsafe candidates (every other field known empty) while the HQ's fields are live: nothing.
            sim = Game();
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 500, 0);
            memory = EnemyStructureAt24(sim);
            memory.KnownFieldRemaining = StartAmounts(sim);
            foreach (int empty in new[] { 2, 3, 4, 5, 8, 9, 10, 11 })
            {
                memory.KnownFieldRemaining[empty] = 0;
            }
            Assert.That(Builds(DecideP0(sim, memory), "skai.perch_depot"), Is.Empty);

            // The same while starved (the HQ's fields are empty): the nearest unsafe field, 6, gets the depot at (21, 7).
            sim = Game();
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 500, 0);
            Empty(sim, 0);
            Empty(sim, 1);
            memory = EnemyStructureAt24(sim);
            memory.KnownFieldRemaining = StartAmounts(sim);
            foreach (int empty in new[] { 2, 3, 4, 5, 8, 9, 10, 11 })
            {
                memory.KnownFieldRemaining[empty] = 0;
            }
            builds = Builds(DecideP0(sim, memory), "skai.perch_depot");
            Assert.That(builds.Count, Is.EqualTo(1));
            Assert.That(builds[0].TileX, Is.EqualTo(21));
            Assert.That(builds[0].TileY, Is.EqualTo(7));
        }

        /// <summary>A memory that already knows an enemy structure at (24.5, 10.5), a spot the Skai team does not see.</summary>
        private static AiMemory EnemyStructureAt24(Simulation sim)
        {
            Assert.That(AiWorldView.Build(sim, P0).IsVisible(24, 10), Is.False, "the remembered structure must be out of sight or intel forgets it");
            AiMemory memory = new AiMemory();
            memory.KnownEnemyStructures.Add(new EntityId(900), new KnownStructure
            {
                Position = new FixVec2(Fix64.Parse("24.5"), Fix64.Parse("10.5")),
                IsHq = false,
                LastSeenTick = 0,
            });
            return memory;
        }

        /// <summary>
        /// B-8: bank 80, a depot due (100), no gathering worker and an idle Roost: the Roost trains a Windrunner (75). With one gathering
        /// worker the depot's 100 is reserved and nothing is trained.
        /// </summary>
        [Test]
        public void B8_ReservationsAreHeldOnlyWithIncome()
        {
            Simulation sim = Game(workers: 6);
            StepTo(sim, Ticks(240));
            SetStock(sim, P0, 80, 0);
            List<AiIntent> intents = DecideP0(sim);
            Assert.That(Of(intents, AiIntentKind.Train).Count, Is.EqualTo(1), "no income: nothing is reserved, the bank is spent");
            Assert.That(Of(intents, AiIntentKind.Train)[0].DefinitionId, Is.EqualTo("skai.windrunner"));
            Assert.That(Builds(intents, "skai.perch_depot"), Is.Empty, "80 cannot pay the depot");

            sim = Game(workers: 6);
            StepTo(sim, Ticks(240));
            Gather(sim, P0, Workers(sim, P0)[0], 0);
            SetStock(sim, P0, 80, 0);
            intents = DecideP0(sim);
            Assert.That(Of(intents, AiIntentKind.Train), Is.Empty, "income: the depot's 100 is reserved, 80 buys nothing");
            Assert.That(Builds(intents, "skai.perch_depot"), Is.Empty);
        }

        // ------------------------------------------------------------------ B-9 to B-11

        /// <summary>B-9: five workers on field 0 (cap 3) and none on field 1: the two highest-ID workers move; with a limit of 1 only the highest.</summary>
        [Test]
        public void B9_Rebalance()
        {
            foreach ((AiProfile profile, int moved) in new[] { (Aggressive, 2), (Edited("\"rebalance_workers_per_think\": 2", "\"rebalance_workers_per_think\": 1"), 1) })
            {
                Simulation sim = Game(workers: 0);
                // Five workers sent to field 0.
                List<EntityId> sent = new List<EntityId>();
                // Worker number. Range: 0 to 4.
                for (int i = 0; i < 5; i++)
                {
                    EntityId worker = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                    Gather(sim, P0, worker, 0);
                    sent.Add(worker);
                }
                List<AiIntent> gathers = Of(Decide(sim, P0, profile), AiIntentKind.Gather);
                Assert.That(gathers.Count, Is.EqualTo(moved));
                // Expected movers, highest ID first.
                for (int i = 0; i < moved; i++)
                {
                    Assert.That(gathers[i].Unit, Is.EqualTo(sent[4 - i]));
                    Assert.That(gathers[i].Field, Is.EqualTo(1));
                }
            }
        }

        /// <summary>B-10: with the common fields full and a served rare field empty of workers, two idle workers go to the rare field and a third to the nearest served field.</summary>
        [Test]
        public void B10_RareFieldsGetTheirOwnCap()
        {
            Simulation sim = Game(workers: 0);
            SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
            // Six workers fill fields 0 and 1 (cap 3 each).
            // Worker number. Range: 0 to 5.
            for (int i = 0; i < 6; i++)
            {
                EntityId worker = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                Gather(sim, P0, worker, i < 3 ? 0 : 1);
            }
            EntityId a = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
            EntityId b = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
            EntityId c = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
            Assert.That(Probe(sim).Served, Does.Contain(4));
            List<AiIntent> gathers = Of(DecideP0(sim), AiIntentKind.Gather);
            Assert.That(gathers.Count, Is.EqualTo(3));
            Assert.That(gathers[0].Unit, Is.EqualTo(a));
            Assert.That(gathers[0].Field, Is.EqualTo(4));
            Assert.That(gathers[1].Unit, Is.EqualTo(b));
            Assert.That(gathers[1].Field, Is.EqualTo(4));
            Assert.That(gathers[2].Unit, Is.EqualTo(c));
            Assert.That(gathers[2].Field, Is.EqualTo(0), "the rare field is at its cap: the nearest served field takes the third worker");
        }

        /// <summary>
        /// C2-7: with rare_bank_soft_cap 300 and the rare bank at 300, a served rare field's cap is 0: its two workers move to the common
        /// fields (highest ID first), an idle worker goes to a common field and the worker target drops the rare field's 2; at 299 or with
        /// the cap off nothing moves.
        /// </summary>
        [Test]
        public void C2_7_RareWorkerCap()
        {
            AiProfile capped = Edited("\"rare_bank_soft_cap\": 0,", "\"rare_bank_soft_cap\": 300,");
            // A game with a depot serving rare field 4, one worker on common field 0 and two on the rare field; returns the two rare workers.
            Simulation Setup(int rare, out EntityId rareLow, out EntityId rareHigh, out EntityId idle)
            {
                Simulation sim = Game(workers: 0);
                SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
                Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 0);
                rareLow = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                rareHigh = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                Gather(sim, P0, rareLow, 4);
                Gather(sim, P0, rareHigh, 4);
                idle = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                SetStock(sim, P0, 0, rare);
                return sim;
            }
            Simulation atCap = Setup(300, out EntityId low, out EntityId high, out EntityId idle);
            Assert.That(Probe(atCap, null, capped).WantedWorkers, Is.EqualTo(6), "two served common fields x 3 and 0 for the rare field");
            List<AiIntent> gathers = Of(Decide(atCap, P0, capped), AiIntentKind.Gather);
            Assert.That(gathers.Count, Is.EqualTo(3));
            Assert.That(gathers[0].Unit, Is.EqualTo(idle));
            Assert.That(gathers[1].Unit, Is.EqualTo(high), "the higher ID leaves the rare field first");
            Assert.That(gathers[2].Unit, Is.EqualTo(low));
            foreach (AiIntent gather in gathers)
            {
                Assert.That(gather.Field, Is.Not.EqualTo(4));
            }
            Simulation below = Setup(299, out _, out _, out EntityId idleBelow);
            Assert.That(Probe(below, null, capped).WantedWorkers, Is.EqualTo(8));
            List<AiIntent> kept = Of(Decide(below, P0, capped), AiIntentKind.Gather);
            Assert.That(kept.Count, Is.EqualTo(1), "only the idle worker is sent");
            Assert.That(kept[0].Unit, Is.EqualTo(idleBelow));
            Simulation off = Setup(5000, out _, out _, out EntityId idleOff);
            Assert.That(Probe(off).WantedWorkers, Is.EqualTo(8));
            List<AiIntent> same = Of(DecideP0(off), AiIntentKind.Gather);
            Assert.That(same.Count, Is.EqualTo(1));
            Assert.That(same[0].Unit, Is.EqualTo(idleOff));
        }

        /// <summary>
        /// C2 review item 2: with the rare bank at the cap the two rare workers move to a served common field even when both common fields
        /// are full (3 workers each); below the cap they stay.
        /// </summary>
        [Test]
        public void C2_7b_RareWorkersLeaveForAFullCommonField()
        {
            AiProfile capped = Edited("\"rare_bank_soft_cap\": 0,", "\"rare_bank_soft_cap\": 300,");
            // A game where common fields 0 and 1 hold 3 workers each (at their cap) and rare field 4 holds two; returns the two rare workers.
            Simulation Setup(int rare, out EntityId rareLow, out EntityId rareHigh)
            {
                Simulation sim = Game(workers: 0);
                SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
                // Field index of the common fields. Range: 0 to 1.
                for (int field = 0; field < 2; field++)
                {
                    // Worker number on the field. Range: 0 to 2.
                    for (int i = 0; i < 3; i++)
                    {
                        Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), field);
                    }
                }
                rareLow = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                rareHigh = SpawnAtTile(sim, "skai.fledgling", P0, 6, 9);
                Gather(sim, P0, rareLow, 4);
                Gather(sim, P0, rareHigh, 4);
                SetStock(sim, P0, 0, rare);
                return sim;
            }
            Simulation atCap = Setup(300, out EntityId low, out EntityId high);
            Assert.That(Probe(atCap, null, capped).Served, Does.Contain(4));
            List<AiIntent> gathers = Of(Decide(atCap, P0, capped), AiIntentKind.Gather);
            Assert.That(gathers.Count, Is.EqualTo(2), "both rare workers leave although the common fields are full");
            Assert.That(gathers[0].Unit, Is.EqualTo(high), "the higher ID leaves first");
            Assert.That(gathers[1].Unit, Is.EqualTo(low));
            foreach (AiIntent gather in gathers)
            {
                Assert.That(gather.Field, Is.InRange(0, 1));
            }
            Simulation below = Setup(299, out _, out _);
            Assert.That(Of(Decide(below, P0, capped), AiIntentKind.Gather), Is.Empty, "below the cap nobody moves");
        }

        /// <summary>Review item 6: with rare_bank_return_cap 200 the rare fields close at a bank of 300 and open again only below 200 (no walking back and forth at the cap).</summary>
        [Test]
        public void C2_7c_RareCapHasHysteresis()
        {
            AiProfile capped = Edited("\"rare_bank_soft_cap\": 0,", "\"rare_bank_soft_cap\": 300,", "\"rare_bank_return_cap\": 0,", "\"rare_bank_return_cap\": 200,");
            // The match: a depot serving rare field 4, one worker on common field 0 and two on the rare field.
            Simulation sim = Game(workers: 0);
            SpawnAtTile(sim, "skai.perch_depot", P0, 8, 60);
            Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 0);
            Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 4);
            Gather(sim, P0, SpawnAtTile(sim, "skai.fledgling", P0, 6, 9), 4);
            // The one memory carried through the bank levels.
            AiMemory memory = new AiMemory();
            // Workers wanted at each bank level: closed at 300, still closed at 250, open at 199, closed again only at 300.
            foreach ((int rare, int wanted) in new[] { (250, 8), (300, 6), (250, 6), (199, 8), (250, 8), (300, 6) })
            {
                SetStock(sim, P0, 0, rare);
                Assert.That(Probe(sim, memory, capped).WantedWorkers, Is.EqualTo(wanted), "rare " + rare);
            }
        }

        /// <summary>B-11: starved with the HQ Rooted and a live field 43 tiles away: an idle worker is sent to it.</summary>
        [Test]
        public void B11_LongHaul()
        {
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            SetStock(sim, P0, 200, 0);
            AiMemory memory = new AiMemory();
            memory.KnownFieldRemaining = new int[sim.Resources.Count];
            memory.KnownFieldRemaining[11] = 3000;
            List<AiIntent> gathers = Of(DecideP0(sim, memory), AiIntentKind.Gather);
            Assert.That(gathers, Is.Not.Empty);
            foreach (AiIntent gather in gathers)
            {
                Assert.That(gather.Field, Is.EqualTo(11));
            }
        }

        // ------------------------------------------------------------------ B-12 to B-17

        /// <summary>The expected relocation destination for a field: its centre moved 3 tiles toward <paramref name="from"/>. Unit: tiles.</summary>
        private static FixVec2 Destination(Simulation sim, int field, FixVec2 from)
        {
            // Centre of the field's tile. Unit: tiles.
            FixVec2 centre = new FixVec2(Fix64.FromInt(sim.Resources[field].X) + Fix64.Half, Fix64.FromInt(sim.Resources[field].Y) + Fix64.Half);
            return centre.MoveTowards(from, Fix64.FromInt(3));
        }

        /// <summary>Uproots P0's HQ with a command and steps until it is Uprooted.</summary>
        private void UprootAndWait(Simulation sim)
        {
            sim.Submit(new UprootHqCommand(sim.Clock.Tick, P0, _sequence++, Hq(sim, P0).Id));
            // Steps spent waiting. Range: 0 to 400.
            for (int i = 0; i < 400 && Hq(sim, P0).HqState != HqState.Uprooted; i++)
            {
                sim.Step();
            }
            Assert.That(Hq(sim, P0).HqState, Is.EqualTo(HqState.Uprooted));
        }

        /// <summary>B-12: home share 0.05, two live depots (the limit) and a safe unserved field: the HQ uproots and heads for it; next think it is sent there.</summary>
        [Test]
        public void B12_RelocationTrigger()
        {
            Simulation sim = Game();
            Leave(sim, 0, 150);
            Leave(sim, 1, 150);
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            SpawnAtTile(sim, "skai.perch_depot", P0, 9, 49);
            StepTo(sim, Ticks(10));
            SetStock(sim, P0, 500, 0);
            Assert.That(Probe(sim).LiveDepots, Is.EqualTo(2));
            AiMemory memory = new AiMemory();
            List<AiIntent> intents = DecideP0(sim, memory);
            Assert.That(Of(intents, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
            Assert.That(Of(intents, AiIntentKind.UprootHq)[0].Unit, Is.EqualTo(Hq(sim, P0).Id));
            Assert.That(memory.RelocationActive, Is.True);
            Assert.That(memory.RelocationField, Is.EqualTo(11), "nearest safe field not served by a depot and not a home field: (51, 14)");
            // Destination: 3 tiles from the field centre toward the HQ.
            FixVec2 expected = Destination(sim, 11, Hq(sim, P0).Position);
            Assert.That((memory.RelocationDestination - expected).LengthSquared() < Fix64.Half, Is.True);
            Apply(sim, P0, intents);
            // Steps spent waiting for the uproot. Range: 0 to 400.
            for (int i = 0; i < 400 && Hq(sim, P0).HqState != HqState.Uprooted; i++)
            {
                sim.Step();
            }
            List<AiIntent> next = DecideP0(sim, memory);
            Assert.That(Of(next, AiIntentKind.RootHq), Is.Empty, "the nearly empty home fields must not pull the HQ back down");
            Assert.That(Of(next, AiIntentKind.Move).Count, Is.EqualTo(1));
            Assert.That(Of(next, AiIntentKind.Move)[0].Unit, Is.EqualTo(Hq(sim, P0).Id));
            Assert.That(Of(next, AiIntentKind.Move)[0].Point, Is.EqualTo(memory.RelocationDestination));
        }

        /// <summary>B-13: home share 0, no depot, bank 40 and no income: the HQ uproots (a depot cannot be paid), no depot is built.</summary>
        [Test]
        public void B13_RescueRelocation()
        {
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            StepTo(sim, Ticks(10));
            SetStock(sim, P0, 40, 0);
            List<AiIntent> intents = DecideP0(sim);
            Assert.That(Of(intents, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
            Assert.That(Builds(intents, "skai.perch_depot"), Is.Empty);
        }

        /// <summary>B-14: home share 0, no live depot of 2, bank 200 and income: a depot is built and the HQ stays.</summary>
        [Test]
        public void B14_DepotBeforeRelocation()
        {
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            StepTo(sim, Ticks(10));
            Gather(sim, P0, Workers(sim, P0)[1], 6);
            SetStock(sim, P0, 200, 0);
            List<AiIntent> intents = DecideP0(sim);
            Assert.That(Builds(intents, "skai.perch_depot").Count, Is.EqualTo(1));
            Assert.That(Of(intents, AiIntentKind.UprootHq), Is.Empty);
        }

        /// <summary>B-15: an Uprooted idle HQ beside a live field roots; once Rooted the relocation ends (count 1) and a new one waits 60 s.</summary>
        [Test]
        public void B15_RootAndCooldown()
        {
            Simulation sim = Game();
            UprootAndWait(sim);
            // Plain Uprooted HQ beside live fields, no relocation: root.
            List<AiIntent> intents = DecideP0(sim);
            Assert.That(Of(intents, AiIntentKind.RootHq).Count, Is.EqualTo(1));
            // With a running relocation to field 0 the HQ roots too (the target is in reach).
            AiMemory memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 0;
            memory.RelocationDestination = FixVec2.FromInts(5, 9);
            memory.RelocationStartTick = sim.Clock.Tick;
            memory.FailedRelocationFields.Add(11);
            intents = DecideP0(sim, memory);
            Assert.That(Of(intents, AiIntentKind.RootHq).Count, Is.EqualTo(1));
            Apply(sim, P0, intents);
            // Steps spent waiting for the root. Range: 0 to 600.
            for (int i = 0; i < 600 && Hq(sim, P0).HqState != HqState.Rooted; i++)
            {
                sim.Step();
            }
            Assert.That(Hq(sim, P0).HqState, Is.EqualTo(HqState.Rooted));
            // The home fields run dry, so a rescue relocation is due as soon as the cooldown allows.
            Empty(sim, 0);
            Empty(sim, 1);
            SetStock(sim, P0, 40, 0);
            // Tick of the think that sees the HQ Rooted.
            int rootedTick = sim.Clock.Tick;
            intents = DecideP0(sim, memory);
            Assert.That(memory.RelocationActive, Is.False);
            Assert.That(memory.Relocations, Is.EqualTo(1));
            Assert.That(memory.FailedRelocationFields, Is.Empty, "cleared once the HQ roots");
            Assert.That(Of(intents, AiIntentKind.UprootHq), Is.Empty, "inside the cooldown");
            StepTo(sim, rootedTick + Ticks(30));
            SetStock(sim, P0, 40, 0);
            Assert.That(Of(DecideP0(sim, memory), AiIntentKind.UprootHq), Is.Empty, "30 s later: still cooling down");
            StepTo(sim, rootedTick + Ticks(61));
            SetStock(sim, P0, 40, 0);
            Assert.That(Of(DecideP0(sim, memory), AiIntentKind.UprootHq).Count, Is.EqualTo(1), "61 s later it can fire");
        }

        /// <summary>B-16: the target field is found empty during the move: the HQ is sent to the next field's destination.</summary>
        [Test]
        public void B16_RetargetOnDepletion()
        {
            Simulation sim = Game();
            UprootAndWait(sim);
            AiMemory memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 11;
            memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
            memory.RelocationStartTick = sim.Clock.Tick;
            memory.KnownFieldRemaining = StartAmounts(sim);
            memory.KnownFieldRemaining[11] = 0;
            List<AiIntent> intents = DecideP0(sim, memory);
            Assert.That(memory.RelocationField, Is.EqualTo(6), "the nearest live field that is not a home field");
            // The HQ's own move intents (the workers follow it with moves of their own).
            List<AiIntent> hqMoves = new List<AiIntent>();
            foreach (AiIntent move in Of(intents, AiIntentKind.Move))
            {
                if (move.Unit == Hq(sim, P0).Id)
                {
                    hqMoves.Add(move);
                }
            }
            Assert.That(hqMoves.Count, Is.EqualTo(1));
            // Expected stand-off point beside field 6, 3 tiles toward the HQ.
            FixVec2 expected = Destination(sim, 6, Hq(sim, P0).Position);
            Assert.That((hqMoves[0].Point - expected).LengthSquared() < Fix64.Half, Is.True);
        }

        /// <summary>B-17: 121 s after the uproot without rooting the HQ chooses again from where it stands and restarts the timer; at 119 s it keeps its target.</summary>
        [Test]
        public void B17_Timeout()
        {
            Simulation sim = Game();
            UprootAndWait(sim);
            StepTo(sim, Ticks(130));
            foreach ((int age, bool retarget) in new[] { (Ticks(119), false), (Ticks(121), true) })
            {
                AiMemory memory = new AiMemory();
                memory.RelocationActive = true;
                memory.RelocationField = 11;
                memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
                memory.RelocationStartTick = sim.Clock.Tick - age;
                DecideP0(sim, memory);
                Assert.That(memory.RelocationField, Is.EqualTo(retarget ? 6 : 11), "age " + age);
                Assert.That(memory.RelocationStartTick, Is.EqualTo(retarget ? sim.Clock.Tick : sim.Clock.Tick - age));
            }
        }

        /// <summary>B-18: relocating with no drop-off, idle workers follow the HQ; with a complete depot serving a live field they gather there instead.</summary>
        [Test]
        public void B18_WorkersFollowTheHq()
        {
            Simulation sim = Game();
            UprootAndWait(sim);
            AiMemory memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 11;
            memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
            memory.RelocationStartTick = sim.Clock.Tick;
            List<AiIntent> intents = DecideP0(sim, memory);
            // Move intents of the workers.
            int workerMoves = 0;
            foreach (AiIntent move in Of(intents, AiIntentKind.Move))
            {
                if (move.Unit != Hq(sim, P0).Id)
                {
                    workerMoves++;
                    Assert.That(move.Point, Is.EqualTo(memory.RelocationDestination));
                }
            }
            Assert.That(workerMoves, Is.EqualTo(2));
            Assert.That(Of(intents, AiIntentKind.Gather), Is.Empty);

            // With a complete depot serving field 6 the workers gather there.
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 11;
            memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
            memory.RelocationStartTick = sim.Clock.Tick;
            intents = DecideP0(sim, memory);
            Assert.That(Of(intents, AiIntentKind.Gather).Count, Is.EqualTo(2));
            foreach (AiIntent gather in Of(intents, AiIntentKind.Gather))
            {
                Assert.That(gather.Field, Is.EqualTo(6));
            }
        }

        // ------------------------------------------------------------------ review fixes (16 Nov 2026)

        /// <summary>Moves P0's Uprooted HQ to <paramref name="point"/> with a command and steps until it arrives.</summary>
        private void WalkHq(Simulation sim, FixVec2 point)
        {
            sim.Submit(new MoveUnitCommand(sim.Clock.Tick, P0, _sequence++, Hq(sim, P0).Id, point));
            // Steps spent walking. Range: 0 to 2000.
            for (int i = 0; i < 2000 && (i == 0 || Hq(sim, P0).HasMoveTarget); i++)
            {
                sim.Step();
            }
            Assert.That(Hq(sim, P0).HasMoveTarget, Is.False);
        }

        /// <summary>Review fix 1: an Uprooted HQ 6 to 10 tiles from the only live field (too far to root, a home field so no candidate) walks to it and roots; also when its target empties.</summary>
        [Test]
        public void R1_HqNearALiveFieldIsNeverStuckUprooted()
        {
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            UprootAndWait(sim);
            WalkHq(sim, new FixVec2(Fix64.Parse("14.5"), Fix64.Parse("8.5")));
            // Field 6 at (21, 10) is 6.7 tiles away: out of root reach (6), inside the home radius (10). It is the only field known live
            // (fields 6 and 7), so the candidate list is empty.
            AiMemory memory = new AiMemory();
            memory.KnownFieldRemaining = new int[sim.Resources.Count];
            memory.KnownFieldRemaining[6] = 3000;
            memory.KnownFieldRemaining[7] = 3000;
            List<AiIntent> intents = DecideP0(sim, memory);
            Assert.That(Of(intents, AiIntentKind.RootHq), Is.Empty);
            // The HQ's own moves.
            List<AiIntent> hqMoves = new List<AiIntent>();
            foreach (AiIntent move in Of(intents, AiIntentKind.Move))
            {
                if (move.Unit == Hq(sim, P0).Id)
                {
                    hqMoves.Add(move);
                }
            }
            Assert.That(hqMoves.Count, Is.EqualTo(1));
            Assert.That(memory.RelocationField, Is.EqualTo(6));
            Assert.That((hqMoves[0].Point - Destination(sim, 6, Hq(sim, P0).Position)).LengthSquared() < Fix64.Half, Is.True);
            // Walk there: now the HQ roots.
            WalkHq(sim, hqMoves[0].Point);
            Assert.That(Of(DecideP0(sim, memory), AiIntentKind.RootHq).Count, Is.EqualTo(1));

            // The same when the relocation target empties during the walk and the only live field is a home field.
            sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            UprootAndWait(sim);
            WalkHq(sim, new FixVec2(Fix64.Parse("14.5"), Fix64.Parse("8.5")));
            memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 11;
            memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
            memory.RelocationStartTick = sim.Clock.Tick;
            memory.KnownFieldRemaining = new int[sim.Resources.Count];
            memory.KnownFieldRemaining[6] = 3000;
            memory.KnownFieldRemaining[7] = 3000;
            DecideP0(sim, memory);
            Assert.That(memory.RelocationField, Is.EqualTo(6), "field 11 is known empty and no candidate is left: the nearest live field");
        }

        /// <summary>Review fix 2: a field a relocation timed out on is remembered and skipped when re-targeting (until the HQ roots).</summary>
        [Test]
        public void R2_TimedOutFieldsAreNotChosenAgain()
        {
            Simulation sim = Game();
            UprootAndWait(sim);
            StepTo(sim, Ticks(300));
            AiMemory memory = new AiMemory();
            memory.RelocationActive = true;
            memory.RelocationField = 11;
            memory.RelocationDestination = Destination(sim, 11, Hq(sim, P0).Position);
            memory.RelocationStartTick = sim.Clock.Tick - Ticks(121);
            DecideP0(sim, memory);
            Assert.That(memory.RelocationField, Is.EqualTo(6));
            Assert.That(memory.FailedRelocationFields, Is.EqualTo(new List<int> { 11 }));
            // Field 6 times out too: the next choice is neither 6 nor 11.
            memory.RelocationStartTick = sim.Clock.Tick - Ticks(121);
            DecideP0(sim, memory);
            Assert.That(memory.RelocationField, Is.EqualTo(7));
            Assert.That(memory.FailedRelocationFields, Is.EqualTo(new List<int> { 6, 11 }));
        }

        /// <summary>Review fix 3: a relocation candidate whose own surrounding fields are nearly empty is skipped.</summary>
        [Test]
        public void R3_NearlyEmptySurroundingsAreNotACandidate()
        {
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            StepTo(sim, Ticks(10));
            SetStock(sim, P0, 40, 0);
            AiMemory memory = new AiMemory();
            memory.KnownFieldRemaining = StartAmounts(sim);
            // Fields 6 and 7 hold 300 each of 6000 (5 percent, at or under the 10 percent limit) but are far above the minimum remaining.
            memory.KnownFieldRemaining[6] = 300;
            memory.KnownFieldRemaining[7] = 300;
            List<AiIntent> intents = DecideP0(sim, memory);
            Assert.That(Of(intents, AiIntentKind.UprootHq).Count, Is.EqualTo(1));
            Assert.That(memory.RelocationField, Is.EqualTo(10), "not the nearly empty natural pair");
        }

        /// <summary>Review fix 4: the view marks a footprint as blocked only where the team sees the tile.</summary>
        [Test]
        public void R4_UnseenEnemyFootprintsAreNotInTheView()
        {
            Simulation sim = Game();
            SpawnAtTile(sim, "water.tidepool", P1, 40, 40);
            SpawnAtTile(sim, "water.tidepool", P1, 14, 8);
            Assert.That(sim.Terrain.At(40, 40).IsBlocked, Is.True, "the simulation blocks the footprint");
            AiWorldView view = AiWorldView.Build(sim, P0);
            Assert.That(view.IsVisible(40, 40), Is.False);
            Assert.That(view.IsBlocked(40, 40), Is.False, "an unseen enemy footprint is not known");
            Assert.That(view.IsVisible(14, 8), Is.True);
            Assert.That(view.IsBlocked(14, 8), Is.True, "a seen footprint is");
        }

        /// <summary>Review fix 5: on a point-symmetric map the two players' HQ-area sites and depot sites are exact 180-degree mirrors.</summary>
        [Test]
        public void R5_SiteSearchesMirrorBetweenThePlayers()
        {
            // Prototype at the spawns; grey-box with the HQs on mirrored tiles ((8, 8) and (55, 55); its P1 spawn is one tile off the mirror).
            foreach ((string map, FixVec2? h0, FixVec2? h1, int size) in new[]
            {
                ("prototype_1v1", (FixVec2?)null, (FixVec2?)null, 96),
                ("greybox_1v1", (FixVec2?)new FixVec2(Fix64.Parse("8.5"), Fix64.Parse("8.5")), (FixVec2?)new FixVec2(Fix64.Parse("55.5"), Fix64.Parse("55.5")), 64),
            })
            {
                // HQ-area structure sites: nothing is built yet, so the first build order step places a Roost and a Tidepool.
                Simulation sim = Game(map, 2, false, h0, h1);
                SetStock(sim, P0, 1000, 0);
                SetStock(sim, P1, 1000, 0);
                List<AiIntent> skai = Builds(DecideP0(sim), "skai.roost");
                List<AiIntent> water = Builds(Decide(sim, P1, Aggressive), "water.tidepool");
                Assert.That(skai.Count, Is.EqualTo(1), map);
                Assert.That(water.Count, Is.EqualTo(1), map);
                Assert.That(water[0].TileX, Is.EqualTo(size - 1 - skai[0].TileX), map + " HQ-area column");
                Assert.That(water[0].TileY, Is.EqualTo(size - 1 - skai[0].TileY), map + " HQ-area row");
                // Depot sites at 240 s.
                sim = Game(map, 2, true, h0, h1);
                StepTo(sim, Ticks(240));
                Gather(sim, P0, Workers(sim, P0)[1], 0);
                Gather(sim, P1, Workers(sim, P1)[1], map == "greybox_1v1" ? 2 : 8);
                SetStock(sim, P0, 200, 0);
                SetStock(sim, P1, 200, 0);
                skai = Builds(DecideP0(sim), "skai.perch_depot");
                water = Builds(Decide(sim, P1, Aggressive), "water.pearl_depot");
                Assert.That(skai.Count, Is.EqualTo(1), map);
                Assert.That(water.Count, Is.EqualTo(1), map);
                Assert.That(water[0].TileX, Is.EqualTo(size - 1 - skai[0].TileX), map + " depot column");
                Assert.That(water[0].TileY, Is.EqualTo(size - 1 - skai[0].TileY), map + " depot row");
            }
        }

        /// <summary>Review fix 7: a hidden field reports the remembered (or start) amount in the view, and unsafe fields are left out of the long haul.</summary>
        [Test]
        public void R7_HiddenAmountsAreMasked_AndTheLongHaulSkipsUnsafeFields()
        {
            Simulation sim = Game();
            Leave(sim, 9, 1000);
            Assert.That(AiWorldView.Build(sim, P0).Fields[9].Remaining, Is.EqualTo(3000), "hidden: start amount without a memory");
            Assert.That(AiWorldView.Build(sim, P0, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 2500, 0, 0 }).Fields[9].Remaining, Is.EqualTo(2500), "hidden: remembered amount");
            Assert.That(AiWorldView.Build(sim, P0).Fields[0].Remaining, Is.EqualTo(3000), "visible: live amount");

            // Starved with only field 8 known live: it is 13.7 tiles from the Water spawn, so unsafe: no worker is sent there.
            sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            SetStock(sim, P0, 200, 0);
            AiMemory memory = new AiMemory();
            memory.KnownFieldRemaining = new int[sim.Resources.Count];
            memory.KnownFieldRemaining[8] = 3000;
            Assert.That(Of(DecideP0(sim, memory), AiIntentKind.Gather), Is.Empty);
        }

        // ------------------------------------------------------------------ B-19 to B-21

        /// <summary>B-19: with the home mined out but a depot-served field live the drawn threshold holds; with every field known empty one army unit attacks at once.</summary>
        [Test]
        public void B19_AllInMeansNoFieldOnTheMapHasResources()
        {
            // Home mined out, field 6 served by a depot and live: not all in (threshold 8, army 1 Windrunner = 2 supply or less).
            Simulation sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            SpawnAtTile(sim, "skai.perch_depot", P0, 21, 7);
            SpawnAtTile(sim, "skai.windrunner", P0, 12, 12);
            AiMemory memory = new AiMemory();
            DecideP0(sim, memory);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Massing));
            Assert.That(memory.AttackThreshold, Is.GreaterThanOrEqualTo(8));

            // Every field known empty: threshold 1, the army attacks.
            sim = Game();
            Empty(sim, 0);
            Empty(sim, 1);
            SpawnAtTile(sim, "skai.windrunner", P0, 12, 12);
            memory = new AiMemory();
            memory.KnownFieldRemaining = new int[sim.Resources.Count];
            DecideP0(sim, memory);
            Assert.That(memory.Stance, Is.EqualTo(AiStance.Attack));
            Assert.That(memory.AttacksStarted, Is.EqualTo(1));
        }

        /// <summary>B-20: the same view, profile, memory state and seed give identical intents; a think with only economy decisions draws nothing.</summary>
        [Test]
        public void B20_Determinism()
        {
            // The two runs' intents as text and the generator draws after the second think.
            List<string> signatures = new List<string>();
            List<ulong> draws = new List<ulong>();
            // Run number. Range: 0 to 1.
            for (int run = 0; run < 2; run++)
            {
                Simulation sim = Game();
                StepTo(sim, Ticks(240));
                Gather(sim, P0, Workers(sim, P0)[1], 0);
                SetStock(sim, P0, 200, 0);
                AiMemory memory = new AiMemory();
                DeterministicRandom rng = new DeterministicRandom(99);
                // The first think draws the attack threshold and builds the depot; the second is the one that must draw nothing.
                string text = string.Empty;
                List<AiIntent> both = Decide(sim, P0, Aggressive, memory, rng);
                ulong drawsAfterFirst = rng.DrawCount;
                both.AddRange(Decide(sim, P0, Aggressive, memory, rng));
                foreach (AiIntent intent in both)
                {
                    text += intent.Kind + ":" + intent.Unit.Value + ":" + intent.Field + ":" + intent.DefinitionId + ":" + intent.TileX + ":" + intent.TileY + ":" + intent.Point + ";";
                }
                signatures.Add(text);
                draws.Add(rng.DrawCount - drawsAfterFirst);
            }
            Assert.That(signatures[1], Is.EqualTo(signatures[0]));
            Assert.That(signatures[0], Does.Contain("Build:"));
            Assert.That(draws[0], Is.EqualTo(0UL), "the second think made only economy decisions");
            Assert.That(draws[1], Is.EqualTo(0UL));
        }

        /// <summary>B-21: a missing rare_workers_per_field and a drop_off_building that is no drop-off give errors naming the file and the field.</summary>
        [Test]
        public void B21_ProfileValidation()
        {
            string text = File.ReadAllText(Path.Combine(TestData.FindDataDirectory(), "ai", "aggressive.json"));
            string without = text.Replace("\"rare_workers_per_field\": 2,", string.Empty);
            Assert.That(without, Is.Not.EqualTo(text));
            DataValidationException missing = Expect.Throws<DataValidationException>(() => AiProfile.Parse(without, "ai/aggressive.json", Data.Content));
            Assert.That(missing.Message, Does.StartWith("ai/aggressive.json"));
            Assert.That(missing.Message, Does.Contain("field 'rare_workers_per_field' is missing"));
            string wrong = text.Replace("\"drop_off_building\": \"skai.perch_depot\"", "\"drop_off_building\": \"skai.roost\"");
            Assert.That(wrong, Is.Not.EqualTo(text));
            DataValidationException notDropOff = Expect.Throws<DataValidationException>(() => AiProfile.Parse(wrong, "ai/aggressive.json", Data.Content));
            Assert.That(notDropOff.Message, Does.Contain("factions.skai.drop_off_building").And.Contain("is_drop_off"));
            // The key is optional: without it the plan builds no depots.
            string absent = text.Replace("\"drop_off_building\": \"skai.perch_depot\",", string.Empty);
            Assert.That(AiProfile.Parse(absent, "ai/aggressive.json", Data.Content).RequireFaction("skai").DropOffBuildingId, Is.EqualTo(string.Empty));
            Assert.That(Aggressive.RequireFaction("skai").DropOffBuildingId, Is.EqualTo("skai.perch_depot"));
            Assert.That(Aggressive.RequireFaction("water").DropOffBuildingId, Is.EqualTo("water.pearl_depot"));
        }
    }
}
