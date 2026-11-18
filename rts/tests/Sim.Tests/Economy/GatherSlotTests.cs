// rts/tests/Sim.Tests/Economy/GatherSlotTests.cs
// Tests SAT-1 to SAT-17 of specs/field_saturation.md (package SAT): gather slots per field, the first-come queue (GatherState.Waiting),
// depletion, death, orders, push-away, separation, the hash, income of one base against two on prototype_1v1, any player count and
// determinism. Part of Rts.Sim.Tests. S, R and G are read from the tuning so the tests survive a retune. Fixture map: common field 0 at
// (10, 10) with 1500, rare field 1 at (30, 10) with 1500, common field 2 at (50, 50) with 15 (exactly three loads), common field 3 at
// (10, 40) with 1500; rooted test HQs stand beside the fields as drop-offs. Workers are spawned within interact range of their field and
// ordered in one tick, so "arrival tick" is the command tick A (the worker queues in step 7 of tick A).
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Tests.Harness;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>Field saturation: gather slots and the waiting queue (package SAT).</summary>
    [TestFixture]
    public class GatherSlotTests
    {
        /// <summary>The fixture map: see the file header. Unit: map definition.</summary>
        private static MapDefinition SlotMap(int field0Amount = 1500)
        {
            return new MapDefinition("test_slots", "Test Slots", TestWorld.FlatMapSize, TestWorld.FlatMapSize,
                new List<FixVec2> { FixVec2.FromInts(4, 4), FixVec2.FromInts(60, 60), FixVec2.FromInts(60, 4) }, null, null,
                new List<ResourceField>
                {
                    new ResourceField(10, 10, ResourceKind.Common, field0Amount),
                    new ResourceField(30, 10, ResourceKind.Rare, 1500),
                    new ResourceField(50, 50, ResourceKind.Common, 15),
                    new ResourceField(10, 40, ResourceKind.Common, 1500),
                }, null);
        }

        /// <summary>A started economy simulation on <see cref="SlotMap"/> with P0's HQ at (12, 12), rooted, as the drop-off of field 0.</summary>
        private static Simulation World()
        {
            Simulation sim = TestWorld.StartedEconomy(1234, null, SlotMap());
            Spawn(sim, TestWorld.HqId, P0, 12, 12);
            return sim;
        }

        /// <summary>Spawns <paramref name="count"/> test workers of P0 on the tile (<paramref name="x"/>, <paramref name="y"/>) of a field, in ascending ID order.</summary>
        private static List<EntityId> Crowd(Simulation sim, int count, int x, int y)
        {
            // IDs in spawn order. Unit: entity IDs. Range: count entries.
            List<EntityId> ids = new List<EntityId>();
            // Worker index. Range: 0 to count - 1.
            for (int i = 0; i < count; i++)
            {
                ids.Add(Spawn(sim, TestWorld.WorkerId, P0, x, y));
            }
            return ids;
        }

        /// <summary>Submits gather orders for every worker at the current tick and steps once; returns that tick (the arrival tick A).</summary>
        private static int GatherAll(Simulation sim, List<EntityId> workers, int field)
        {
            // The tick the orders are applied on and the workers queue. Unit: ticks.
            int tick = sim.Clock.Tick;
            foreach (EntityId id in workers)
            {
                sim.Submit(new GatherCommand(tick, P0, Seq(), id, field));
            }
            sim.Step();
            return tick;
        }

        /// <summary>Steps until the tick <paramref name="tick"/> has been processed (the state read next is the end of that tick).</summary>
        private static void RunThrough(Simulation sim, int tick)
        {
            while (sim.Clock.Tick <= tick)
            {
                sim.Step();
            }
        }

        /// <summary>Number of the given workers in the given gather state.</summary>
        private static int Count(Simulation sim, List<EntityId> workers, GatherState state)
        {
            // Workers found in the state. Unit: workers.
            int count = 0;
            foreach (EntityId id in workers)
            {
                if (Get(sim, id).GatherState == state)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Gather slots of common fields. Unit: workers.</summary>
        private static int S => TestWorld.Tuning.Economy.GatherSlotsCommon;

        /// <summary>Gather slots of rare fields. Unit: workers.</summary>
        private static int R => TestWorld.Tuning.Economy.GatherSlotsRare;

        /// <summary>Ticks of one load. Unit: ticks.</summary>
        private static int G => TestWorld.Tuning.Economy.GatherTicksPerTrip(new MatchClock(TestWorld.TicksPerSecond));

        /// <summary>SAT-1: both keys are required and range checked; the shipped values are 3 and 2.</summary>
        [Test]
        public void Sat1_Loader()
        {
            Assert.That(TestWorld.Tuning.Economy.GatherSlots(ResourceKind.Common), Is.EqualTo(3));
            Assert.That(TestWorld.Tuning.Economy.GatherSlots(ResourceKind.Rare), Is.EqualTo(2));
            // Each key with its shipped line, and the replacements that must fail.
            foreach (string key in new[] { "gather_slots_per_common_field", "gather_slots_per_rare_field" })
            {
                // The shipped value text of the key.
                string shipped = key == "gather_slots_per_common_field" ? "3" : "2";
                foreach (string bad in new[] { string.Empty, "\"" + key + "\": 0,", "\"" + key + "\": 17," })
                {
                    DataValidationException error = Expect.Throws<DataValidationException>(() => DataLoader.Load(
                        TestData.WithEdit("tuning/economy.json", "\"" + key + "\": " + shipped + ",", bad)));
                    Assert.That(error.File, Is.EqualTo("tuning/economy.json"));
                    Assert.That(error.Message, Does.Contain(key));
                }
            }
        }

        /// <summary>SAT-2: one worker takes its first load on tick A + G as before SAT; it is Waiting on A and Gathering on A + 1.</summary>
        [Test]
        public void Sat2_TimingBelowSaturation()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            Unit worker = Get(sim, w[0]);
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.Waiting));
            Assert.That(worker.GatherQueuedTick, Is.EqualTo(a));
            RunThrough(sim, a + 1);
            Assert.That(worker.GatherState, Is.EqualTo(GatherState.Gathering));
            Assert.That(worker.GatherQueuedTick, Is.EqualTo(-1));
            RunThrough(sim, a + G - 1);
            Assert.That(worker.CarriedAmount, Is.EqualTo(0));
            RunThrough(sim, a + G);
            Assert.That(worker.CarriedAmount, Is.EqualTo(TestWorld.Tuning.Economy.GatherAmountPerTrip));
        }

        /// <summary>SAT-3: S + 1 arrivals on one tick: S gather, the highest ID waits and gets a slot the tick after the first load.</summary>
        [Test]
        public void Sat3_OverflowWaits()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 1);
            // Every worker but the last (highest ID) holds a slot.
            for (int i = 0; i < S; i++)
            {
                Assert.That(Get(sim, w[i]).GatherState, Is.EqualTo(GatherState.Gathering), "worker " + i);
            }
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Waiting));
            RunThrough(sim, a + G);
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Waiting), "the slots free in step 7 of the tick the loads are taken");
            RunThrough(sim, a + G + 1);
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Gathering));
        }

        /// <summary>
        /// Field full by S staggered gatherers (the first arrives on A, the others on A + 1, so one slot frees on A + G + 1 and the next on
        /// A + G + 2); two more workers are sent on ticks <paramref name="firstSend"/> and <paramref name="secondSend"/> (offsets from A)
        /// and the method returns them after the tick A + G + 1. With <paramref name="lowerHasGrant"/> the lower-ID late worker is given one earlier slot grant first.
        /// </summary>
        private static List<EntityId> StaggeredFullField(Simulation sim, int firstSend, int secondSend, bool lowerHasGrant = false)
        {
            List<EntityId> gatherers = Crowd(sim, S, 10, 10);
            List<EntityId> late = Crowd(sim, 2, 10, 10);
            if (lowerHasGrant)
            {
                Get(sim, late[0]).AddGatherSlotGrant();
            }
            // Tick of the first gatherer's order.
            int a = sim.Clock.Tick;
            GatherAll(sim, new List<EntityId> { gatherers[0] }, 0);
            GatherAll(sim, gatherers.GetRange(1, S - 1), 0);
            // Late workers in ID order: late[0] is the lower ID.
            if (firstSend == secondSend)
            {
                RunThrough(sim, a + firstSend - 1);
                GatherAll(sim, late, 0);
            }
            else
            {
                // late[1] (higher ID) is sent first.
                RunThrough(sim, a + firstSend - 1);
                GatherAll(sim, new List<EntityId> { late[1] }, 0);
                RunThrough(sim, a + secondSend - 1);
                GatherAll(sim, new List<EntityId> { late[0] }, 0);
            }
            RunThrough(sim, a + G + 1);
            return late;
        }

        /// <summary>SAT-4: the worker that started waiting first gets the freed slot even with the higher ID; on equal ticks and equal grant counts the lower ID wins; on equal ticks, fewer grants beats the lower ID.</summary>
        [Test]
        public void Sat4_FirstComeFirstServed()
        {
            Simulation sim = World();
            List<EntityId> late = StaggeredFullField(sim, 3, 4);
            Assert.That(Get(sim, late[1]).GatherState, Is.EqualTo(GatherState.Gathering), "queued earlier, higher ID");
            Assert.That(Get(sim, late[0]).GatherState, Is.EqualTo(GatherState.Waiting));
            Simulation tie = World();
            List<EntityId> same = StaggeredFullField(tie, 3, 3);
            Assert.That(Get(tie, same[0]).GatherState, Is.EqualTo(GatherState.Gathering), "same tick, lower ID");
            Assert.That(Get(tie, same[1]).GatherState, Is.EqualTo(GatherState.Waiting));
            // Same tick, but the lower ID already has a grant: fewer grants beats the lower ID.
            Simulation grants = World();
            List<EntityId> fewer = StaggeredFullField(grants, 3, 3, true);
            Assert.That(Get(grants, fewer[1]).GatherState, Is.EqualTo(GatherState.Gathering), "same tick, higher ID with fewer grants");
            Assert.That(Get(grants, fewer[0]).GatherState, Is.EqualTo(GatherState.Waiting));
        }

        /// <summary>Loads deposited per worker by the events of a run; also the field's remaining after it.</summary>
        private static SortedDictionary<EntityId, int> RunAndCountDeposits(Simulation sim, int ticks)
        {
            // Deposits per worker. Range: 0 upward.
            SortedDictionary<EntityId, int> loads = new SortedDictionary<EntityId, int>();
            foreach (ResourceDepositedEvent deposit in Of<ResourceDepositedEvent>(StepCollect(sim, ticks)))
            {
                loads.TryGetValue(deposit.Worker, out int before);
                loads[deposit.Worker] = before + 1;
            }
            return loads;
        }

        /// <summary>SAT-5: with 2S + 2 workers on one field for 120 s nobody starves: every worker deposits at least floor(total / workers) - 1 loads (the spec's bound), and no single wait in the queue is longer than ceil((N - 1) / S) x G plus a walking allowance (0 ticks: walking is outside the wait).</summary>
        [Test]
        public void Sat5_NoStarvation()
        {
            Simulation sim = World();
            int n = 2 * S + 2;
            List<EntityId> w = Crowd(sim, n, 10, 10);
            GatherAll(sim, w, 0);
            // Deposits per worker. Range: 0 upward.
            SortedDictionary<EntityId, int> loads = new SortedDictionary<EntityId, int>();
            // Ticks each worker has been Waiting in a row. Unit: ticks. Range: 0 to 2400 (the 120 s run at 20 ticks per second).
            SortedDictionary<EntityId, int> run = new SortedDictionary<EntityId, int>();
            // Longest single wait seen. Unit: ticks. Range: 0 to 2400 (the 120 s run at 20 ticks per second).
            int longest = 0;
            for (int t = 0; t < 120 * TestWorld.TicksPerSecond; t++)
            {
                sim.Step();
                foreach (ResourceDepositedEvent deposit in Of<ResourceDepositedEvent>(new List<SimEvent>(sim.Events.Current)))
                {
                    loads.TryGetValue(deposit.Worker, out int before);
                    loads[deposit.Worker] = before + 1;
                }
                foreach (EntityId id in w)
                {
                    run.TryGetValue(id, out int waited);
                    waited = Get(sim, id).GatherState == GatherState.Waiting ? waited + 1 : 0;
                    run[id] = waited;
                    longest = System.Math.Max(longest, waited);
                }
            }
            // Total loads delivered. Unit: loads.
            int total = 0;
            foreach (int count in loads.Values)
            {
                total += count;
            }
            Assert.That(total, Is.GreaterThan(0));
            TestContext.WriteLine("SAT-5 loads per worker: " + string.Join(",", loads.Values) + " total " + total + ", longest wait " + longest + " ticks");
            foreach (EntityId id in w)
            {
                loads.TryGetValue(id, out int mine);
                Assert.That(mine, Is.GreaterThanOrEqualTo(total / n - 1), id + " delivered " + mine + " of " + total);
            }
            // Bound of the queue: the N - 1 others take S at a time, G ticks each round. Allowance: walking is outside the wait, so 0 ticks unless measured otherwise.
            Assert.That(longest, Is.LessThanOrEqualTo(((n - 1 + S - 1) / S) * G), "longest wait");
        }

        /// <summary>
        /// SAT-5b: at one field shared by two players (one team, so the test HQs do not shoot), workers ordered on the same tick share it fairly at 120 s and at 480 s.
        /// A deterministic queue always lets someone go first, so a constant head start of up to one load per worker is unavoidable: the players' loads may differ by at
        /// most the workers per player, and the 8 workers' loads by at most 2. A gap that grows between 120 s and 480 s would be a rate bias, which the same bound at 480 s catches.
        /// </summary>
        [TestCase(120)]
        [TestCase(480)]
        public void Sat5b_SharedFieldIsFair(int seconds)
        {
            // Field 0 is made big enough (10000 units = 2000 loads) not to run dry in 480 s.
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                TestWorld.Catalog(), new List<PlayerSetup>
                {
                    new PlayerSetup(P0, new TeamId(0), TestWorld.TestFactionId, "Alice"),
                    new PlayerSetup(P1, new TeamId(0), TestWorld.TestFactionId, "Bob"),
                }, BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, SlotMap(10000)));
            sim.Start();
            // The HQs mirror each other across the field, both within reach of every standing spot beside it, so walks are equal for both players
            // (a first fixture with HQs at (12, 12) and (8, 8) let separation seat one worker 2 ticks further from its drop-off: first come first served then correctly served it last, 80 loads against 91 in 480 s).
            Spawn(sim, TestWorld.HqId, P0, 11, 10);
            Spawn(sim, TestWorld.HqId, P1, 9, 10);
            // Workers per player. Unit: workers. Range: 1 to 8.
            int each = S + 1;
            List<EntityId> a = Crowd(sim, each, 10, 10);
            List<EntityId> b = new List<EntityId>();
            for (int i = 0; i < each; i++)
            {
                b.Add(Spawn(sim, TestWorld.WorkerId, P1, 10, 10));
            }
            // The tick both groups are ordered on, so they queue together. Unit: ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            foreach (EntityId id in a)
            {
                sim.Submit(new GatherCommand(tick, P0, Seq(), id, 0));
            }
            foreach (EntityId id in b)
            {
                sim.Submit(new GatherCommand(tick, P1, Seq(), id, 0));
            }
            // Loads per worker. Range: 0 upward.
            SortedDictionary<EntityId, int> loads = RunAndCountDeposits(sim, seconds * TestWorld.TicksPerSecond);
            // Loads per player (loadsA, loadsB) and the smallest (min) and largest (max) per worker. Unit: loads. Range: 0 to 2000 (field 10000 units at 5 per load); min starts at int.MaxValue until a worker is read.
            int loadsA = 0;
            int loadsB = 0;
            int min = int.MaxValue;
            int max = 0;
            foreach (EntityId id in a)
            {
                loads.TryGetValue(id, out int mine);
                loadsA += mine;
                min = System.Math.Min(min, mine);
                max = System.Math.Max(max, mine);
            }
            foreach (EntityId id in b)
            {
                loads.TryGetValue(id, out int mine);
                loadsB += mine;
                min = System.Math.Min(min, mine);
                max = System.Math.Max(max, mine);
            }
            TestContext.WriteLine("SAT-5b " + seconds + " s: P0 " + loadsA + ", P1 " + loadsB + "; per worker " + string.Join(",", loads.Values));
            Assert.That(loadsA, Is.GreaterThan(0));
            Assert.That(System.Math.Abs(loadsA - loadsB), Is.LessThanOrEqualTo(each));
            Assert.That(max - min, Is.LessThanOrEqualTo(2));
        }

        /// <summary>SAT-6: the throughput cap is S x amount per G ticks, and it is reached (at least 90 percent of the bound).</summary>
        [Test]
        public void Sat6_ThroughputCap()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, 2 * S + 2, 10, 10);
            GatherAll(sim, w, 0);
            // Ticks run, counted from the order (the order tick itself included).
            int t = 1200;
            StepCollect(sim, t - 1);
            // Resource units taken from the field. Unit: resource units.
            int taken = 1500 - sim.Resources[0].Remaining;
            // Upper bound of the spec. Unit: resource units.
            int bound = S * TestWorld.Tuning.Economy.GatherAmountPerTrip * (t / G + 1);
            Assert.That(taken, Is.LessThanOrEqualTo(bound));
            Assert.That(taken, Is.GreaterThanOrEqualTo(bound * 9 / 10));
        }

        /// <summary>SAT-7: a rare field has R slots; one more worker waits.</summary>
        [Test]
        public void Sat7_RareUsesRareSlots()
        {
            Simulation sim = World();
            Spawn(sim, TestWorld.HqId, P0, 28, 12);
            List<EntityId> w = Crowd(sim, R + 1, 30, 10);
            int a = GatherAll(sim, w, 1);
            RunThrough(sim, a + 1);
            Assert.That(Count(sim, w, GatherState.Gathering), Is.EqualTo(R));
            Assert.That(Count(sim, w, GatherState.Waiting), Is.EqualTo(1));
        }

        /// <summary>SAT-8: S gatherers empty a field of exactly S loads while 2 wait: one FieldDepletedEvent, the waiting workers end Idle with nothing carried within one tick.</summary>
        [Test]
        public void Sat8_Depletion()
        {
            Simulation sim = World();
            Spawn(sim, TestWorld.HqId, P0, 52, 52);
            Assert.That(S * TestWorld.Tuning.Economy.GatherAmountPerTrip, Is.EqualTo(15), "fixture: field 2 holds exactly S loads");
            List<EntityId> w = Crowd(sim, S + 2, 50, 50);
            int a = GatherAll(sim, w, 2);
            List<SimEvent> events = StepCollect(sim, G + 1);
            Assert.That(Of<FieldDepletedEvent>(events).Count, Is.EqualTo(1));
            Assert.That(sim.Clock.Tick, Is.EqualTo(a + G + 2));
            for (int i = S; i < S + 2; i++)
            {
                Unit waiting = Get(sim, w[i]);
                Assert.That(waiting.GatherState, Is.EqualTo(GatherState.Idle), "waiting worker " + i);
                Assert.That(waiting.CarriedAmount, Is.EqualTo(0));
            }
        }

        /// <summary>SAT-9: a gatherer that dies on tick t frees its slot on tick t.</summary>
        [Test]
        public void Sat9_DeathFreesSlot()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 5);
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Waiting));
            Do(sim, new RemoveUnitCommand(sim.Clock.Tick, PlayerId.None, Seq(), w[0]));
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Gathering));
        }

        /// <summary>SAT-10: a move order to a gatherer on tick t frees its slot on tick t.</summary>
        [Test]
        public void Sat10_OrderFreesSlot()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 5);
            Do(sim, new MoveUnitCommand(sim.Clock.Tick, P0, Seq(), w[0], At(20, 20)));
            Assert.That(Get(sim, w[0]).GatherState, Is.EqualTo(GatherState.Idle));
            Assert.That(Get(sim, w[S]).GatherState, Is.EqualTo(GatherState.Gathering));
        }

        /// <summary>SAT-11: a Waiting worker pushed out of range goes back to ToField (queue tick -1) and queues again on arrival with a new tick.</summary>
        [Test]
        public void Sat11_PushedAway()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 5);
            Unit waiting = Get(sim, w[S]);
            Assert.That(waiting.GatherState, Is.EqualTo(GatherState.Waiting));
            waiting.SetPosition(waiting.Position + At(6, 0));
            sim.Step();
            Assert.That(waiting.GatherState, Is.EqualTo(GatherState.ToField));
            Assert.That(waiting.GatherQueuedTick, Is.EqualTo(-1));
            // Tick it queues again.
            int again = -1;
            for (int i = 0; i < 200 && again < 0; i++)
            {
                sim.Step();
                if (waiting.GatherState == GatherState.Waiting)
                {
                    again = waiting.GatherQueuedTick;
                }
            }
            Assert.That(again, Is.GreaterThan(a + 5));
        }

        /// <summary>SAT-12: a Waiting worker given a build order leaves the queue; after placing it returns to ToField and queues with a new tick.</summary>
        [Test]
        public void Sat12_BuildOrderAndBack()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 5);
            Unit waiting = Get(sim, w[S]);
            Assert.That(waiting.GatherState, Is.EqualTo(GatherState.Waiting));
            PlaceStructure(sim, w[S], TestWorld.BarracksId, 8, 8);
            Assert.That(waiting.GatherState, Is.EqualTo(GatherState.ToField), "back on its field after placing");
            Assert.That(waiting.GatherQueuedTick, Is.EqualTo(-1));
            // Tick it queues again.
            int again = -1;
            for (int i = 0; i < 200 && again < 0; i++)
            {
                sim.Step();
                if (waiting.GatherState == GatherState.Waiting || waiting.GatherState == GatherState.Gathering)
                {
                    again = i;
                }
            }
            Assert.That(again, Is.GreaterThanOrEqualTo(0), "it rejoins the field");
        }

        /// <summary>SAT-13: a Waiting worker touching another unit is not moved by unit separation.</summary>
        [Test]
        public void Sat13_Separation()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, S + 1, 10, 10);
            int a = GatherAll(sim, w, 0);
            RunThrough(sim, a + 2);
            Unit waiting = Get(sim, w[S]);
            Assert.That(waiting.GatherState, Is.EqualTo(GatherState.Waiting));
            // Where it stands before an idle runner is put on top of it. Unit: tiles.
            FixVec2 before = waiting.Position;
            Spawn(sim, TestWorld.RunnerId, P0, 10, 10);
            StepCollect(sim, 5);
            Assert.That(waiting.Position, Is.EqualTo(before));
        }

        /// <summary>SAT-14: GatherQueuedTick is hashed (a different value gives a different hash) in a fixed-width int (4 bytes).</summary>
        [Test]
        public void Sat14_Hash()
        {
            Simulation x = World();
            Simulation y = World();
            EntityId idX = Crowd(x, 1, 10, 10)[0];
            EntityId idY = Crowd(y, 1, 10, 10)[0];
            Assert.That(x.ComputeStateHash(), Is.EqualTo(y.ComputeStateHash()));
            Get(x, idX).SetGatherWaiting(0, 7);
            Get(y, idY).SetGatherWaiting(0, 8);
            Assert.That(x.ComputeStateHash(), Is.Not.EqualTo(y.ComputeStateHash()));
            // The unit's hash input, written twice with different queue ticks: same length.
            StateHasher hx = new StateHasher();
            StateHasher hy = new StateHasher();
            Get(x, idX).WriteHash(hx);
            Get(y, idY).WriteHash(hy);
            Assert.That(hx.BytesWritten, Is.EqualTo(hy.BytesWritten));
            // The field costs one int.
            StateHasher one = new StateHasher();
            one.Write(7);
            Assert.That(one.BytesWritten, Is.EqualTo(4));
        }

        /// <summary>
        /// Plays prototype_1v1 for 300 s with <paramref name="workers"/> workers of player 0 (HQ rooted at its spawn). Returns the common gathered (stock gain) and
        /// counts worker-ticks spent Waiting. When <paramref name="depot"/> is set, a complete Perch Depot stands at the natural
        /// (27, 13), the site the AI rule gives for field 3, and the workers are split over fields 0 to 3 instead of 0 and 1.
        /// </summary>
        private static int PlayIncome(bool depot, int workers, out int waitingTicks)
        {
            DataSet data = TestData.LoadAll();
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P0, new TeamId(0), "skai", "Skai"),
                new PlayerSetup(P1, new TeamId(1), "water", "Water"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(1234), data.Content, players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, data.RequireMap("prototype_1v1")));
            sim.Start();
            Spawn(sim, "skai.great_aerie", P0, 12, 12);
            Spawn(sim, "water.grand_shell", P1, 83, 83);
            if (depot)
            {
                Spawn(sim, "skai.perch_depot", P0, 27, 13);
            }
            // Field tiles of the main (0, 1) and natural (2, 3) fields. Unit: tiles.
            int[][] tiles = { new[] { 7, 10 }, new[] { 10, 7 }, new[] { 27, 9 }, new[] { 26, 16 } };
            // Workers on each field, by field index. Range: 0 to 7.
            int[] perField = new int[4];
            // Fields in use: 2 for one base, 4 for two (workers split as evenly as possible, lower field index first).
            int used = depot ? 4 : 2;
            for (int i = 0; i < workers; i++)
            {
                perField[i % used]++;
            }
            // Orders for the first tick: (worker, field).
            List<(EntityId Worker, int Field)> orders = new List<(EntityId, int)>();
            for (int field = 0; field < 4; field++)
            {
                for (int i = 0; i < perField[field]; i++)
                {
                    orders.Add((Spawn(sim, "skai.fledgling", P0, tiles[field][0], tiles[field][1]), field));
                }
            }
            // Stock before the run. Unit: resource units.
            int start = PlayerOf(sim, P0).Common;
            foreach ((EntityId worker, int field) in orders)
            {
                sim.Submit(new GatherCommand(sim.Clock.Tick, P0, Seq(), worker, field));
            }
            waitingTicks = 0;
            // Ticks to run: 300 s.
            int ticks = 300 * sim.Clock.TicksPerSecond;
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                foreach ((EntityId worker, int _) in orders)
                {
                    if (Get(sim, worker).GatherState == GatherState.Waiting)
                    {
                        waitingTicks++;
                    }
                }
            }
            return PlayerOf(sim, P0).Common - start;
        }

        /// <summary>
        /// SAT-15: on prototype_1v1 with shipped data, one base is capped near 2 x S x amount per G ticks and a second base lifts income by at least
        /// 25 percent (14 workers). Prints the income per minute for 6, 10, 14, 20 and 24 workers (the spec's table 1.2) for the SAT-M report.
        /// </summary>
        [Test]
        public void Sat15_IncomeOneBaseVersusTwo()
        {
            // Spec bound for one base over 6000 ticks. Unit: resource units.
            int bound = 2 * S * TestWorld.Tuning.Economy.GatherAmountPerTrip * (6000 / G + 1);
            // Worker counts of the spec's table. Range: 1 upward.
            int[] counts = { 6, 10, 14, 20, 24 };
            // Gathered at 14 workers, one and two bases. Unit: resource units per 300 s.
            int oneAt14 = 0;
            int twoAt14 = 0;
            foreach (int n in counts)
            {
                int oneBase = PlayIncome(false, n, out int waitOne);
                int twoBases = PlayIncome(true, n, out int waitTwo);
                TestContext.WriteLine("SAT-15 table: workers " + n + ", 1 base " + oneBase / 5 + " per minute (waiting worker-ticks " + waitOne + "), 2 bases "
                    + twoBases / 5 + " per minute (waiting worker-ticks " + waitTwo + ")");
                Assert.That(oneBase, Is.LessThanOrEqualTo(bound), "one base at " + n + " workers");
                if (n == 14)
                {
                    oneAt14 = oneBase;
                    twoAt14 = twoBases;
                }
            }
            Assert.That(twoAt14 * 4, Is.GreaterThanOrEqualTo(oneAt14 * 5));
        }

        /// <summary>Runs three players of three teams with S workers each on field 0 for 1200 ticks; returns the state hash of every tick and the largest Gathering count seen.</summary>
        private static List<ulong> ThreePlayerRun(out int maxGathering)
        {
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), TestWorld.TestFactionId, "A"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), TestWorld.TestFactionId, "B"),
                new PlayerSetup(new PlayerId(2), new TeamId(2), TestWorld.TestFactionId, "C"),
            };
            Simulation sim = new Simulation(new SimulationConfig(new MatchSettings(1234, TestWorld.TicksPerSecond, TestWorld.HashInterval),
                TestWorld.Catalog(), players, BuiltInMechanics.CreateRegistry(), TestWorld.Tuning, SlotMap()));
            sim.Start();
            // Worker orders: (owner, worker).
            List<(PlayerId Owner, EntityId Worker)> orders = new List<(PlayerId, EntityId)>();
            // HQ tiles beside field 0, one per player. Unit: tiles.
            int[][] hqs = { new[] { 12, 12 }, new[] { 8, 12 }, new[] { 12, 8 } };
            for (int p = 0; p < 3; p++)
            {
                PlayerId owner = new PlayerId((byte)p);
                Spawn(sim, TestWorld.HqId, owner, hqs[p][0], hqs[p][1]);
                for (int i = 0; i < S; i++)
                {
                    orders.Add((owner, Spawn(sim, TestWorld.WorkerId, owner, 10, 10)));
                }
            }
            foreach ((PlayerId owner, EntityId worker) in orders)
            {
                sim.Submit(new GatherCommand(sim.Clock.Tick, owner, Seq(), worker, 0));
            }
            List<ulong> hashes = new List<ulong>();
            maxGathering = 0;
            for (int i = 0; i < 1200; i++)
            {
                sim.Step();
                // Workers holding a slot of field 0 this tick. Unit: workers.
                int gathering = 0;
                foreach (Unit unit in sim.Units.All)
                {
                    if (unit.GatherState == GatherState.Gathering && unit.GatherFieldIndex == 0)
                    {
                        gathering++;
                    }
                }
                maxGathering = System.Math.Max(maxGathering, gathering);
                hashes.Add(sim.ComputeStateHash());
            }
            return hashes;
        }

        /// <summary>SAT-16: three players share one field's S slots (never more than S Gathering) and two runs agree on every tick's hash.</summary>
        [Test]
        public void Sat16_AnyPlayerCount()
        {
            List<ulong> first = ThreePlayerRun(out int maxFirst);
            List<ulong> second = ThreePlayerRun(out int maxSecond);
            Assert.That(maxFirst, Is.EqualTo(S), "the field is contested and full");
            Assert.That(maxSecond, Is.LessThanOrEqualTo(S));
            Assert.That(second, Is.EqualTo(first));
        }

        /// <summary>SAT-17: two runs of economy_smoke.json give identical output; so do two runs of a 6-worker one-field script.</summary>
        [Test]
        public void Sat17_Determinism()
        {
            string folder = TestData.FindDataDirectory();
            string script = Path.Combine(folder, "scripts", "economy_smoke.json");
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--script", script }, out string one, out _), Is.EqualTo(0));
            Assert.That(HarnessTestSupport.Run(new[] { "--data", folder, "--script", script }, out string two, out _), Is.EqualTo(0));
            Assert.That(two, Is.EqualTo(one));
            // Six workers on one field, played twice.
            List<ulong> first = SixWorkerRun();
            Assert.That(SixWorkerRun(), Is.EqualTo(first));
        }

        /// <summary>Six workers on one field for 600 ticks; the state hash of every tick.</summary>
        private static List<ulong> SixWorkerRun()
        {
            Simulation sim = World();
            List<EntityId> w = Crowd(sim, 6, 10, 10);
            GatherAll(sim, w, 0);
            List<ulong> hashes = new List<ulong>();
            for (int i = 0; i < 600; i++)
            {
                sim.Step();
                hashes.Add(sim.ComputeStateHash());
            }
            return hashes;
        }
    }
}
