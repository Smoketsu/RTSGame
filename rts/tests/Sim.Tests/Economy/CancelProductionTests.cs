// rts/tests/Sim.Tests/Economy/CancelProductionTests.cs
// Acceptance tests IS-12 to IS-20 of package I4 of rts/docs/specs/interface_support.md: cancel_production removes one entry of a production queue (a unit or a
// research item) and refunds it in full, keeps the head's countdown when a waiting entry goes, restarts the next entry at its full length when the head goes,
// and frees the reserved supply. IS-18 (research) is a second part of the ResearchTests class at the end of this file because it needs that fixture. Part of Rts.Sim.Tests.
// Economy setup: TestWorld.StartedEconomy (20 ticks/s, P0 200 common), a complete barracks at (12, 16) that trains test.worker (50 common, supply 1, 100 ticks).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Commands.Codec;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;
using static Rts.Sim.Tests.Economy.EconomyTestKit;

namespace Rts.Sim.Tests.Economy
{
    /// <summary>The cancel_production command.</summary>
    [TestFixture]
    public sealed class CancelProductionTests
    {
        /// <summary>The worker definition ID the barracks trains.</summary>
        private const string Worker = TestWorld.WorkerId;

        /// <summary>A started economy simulation with a complete barracks of P0; the clock is at tick T (the first tick not yet run).</summary>
        private static Simulation WithBarracks(out EntityId barracks)
        {
            Simulation sim = TestWorld.StartedEconomy();
            barracks = Spawn(sim, TestWorld.BarracksId, P0, 12, 16);
            return sim;
        }

        /// <summary>Queues <paramref name="count"/> workers on one tick (the current one) and steps once; returns that tick.</summary>
        private static int QueueWorkers(Simulation sim, EntityId barracks, int count)
        {
            int t = sim.Clock.Tick;
            for (int i = 0; i < count; i++)
            {
                sim.Submit(new TrainUnitCommand(t, P0, Seq(), barracks, Worker));
            }
            sim.Step();
            return t;
        }

        /// <summary>A cancel command for the current tick, issued by P0.</summary>
        private static CancelProductionCommand Cancel(Simulation sim, EntityId producer, int index, string definition)
        {
            return new CancelProductionCommand(sim.Clock.Tick, P0, Seq(), producer, index, definition);
        }

        /// <summary>Steps until the clock reads <paramref name="tick"/> and returns the tick of each UnitTrainedEvent seen on the way.</summary>
        private static List<int> TrainedTicksUntil(Simulation sim, int tick)
        {
            // Ticks on which a unit appeared.
            List<int> ticks = new List<int>();
            while (sim.Clock.Tick < tick)
            {
                sim.Step();
                ticks.AddRange(Of<UnitTrainedEvent>(new List<SimEvent>(sim.Events.Current)).Select(e => e.Tick));
            }
            return ticks;
        }

        /// <summary>IS-12: a waiting entry is cancelled; the head keeps its countdown and the worker still appears on T + 99.</summary>
        [Test]
        public void Is12_WaitingEntry()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            int supply = PlayerOf(sim, P0).SupplyUsed;
            int t = QueueWorkers(sim, barracks, 2);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(100));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply + 2));
            StepCollect(sim, 9);
            Assert.That(sim.Clock.Tick, Is.EqualTo(t + 10));
            List<SimEvent> events = Do(sim, Cancel(sim, barracks, 1, Worker));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply + 1));
            Assert.That(Get(sim, barracks).ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(Get(sim, barracks).ProductionTicksRemaining, Is.EqualTo(89));
            ProductionCancelledEvent cancelled = Of<ProductionCancelledEvent>(events).Single();
            Assert.That((cancelled.Tick, cancelled.Producer, cancelled.Owner, cancelled.DefinitionId, cancelled.Index, cancelled.RefundCommon, cancelled.RefundRare),
                Is.EqualTo((t + 10, barracks, P0, Worker, 1, 50, 0)));
            Assert.That(TrainedTicksUntil(sim, t + 150), Is.EqualTo(new List<int> { t + 99 }));
        }

        /// <summary>IS-13: the head is cancelled while in progress; its progress is lost and the second worker starts at the full 100 ticks.</summary>
        [Test]
        public void Is13_HeadInProgress()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            int t = QueueWorkers(sim, barracks, 2);
            StepCollect(sim, 49);
            Assert.That(sim.Clock.Tick, Is.EqualTo(t + 50));
            Do(sim, Cancel(sim, barracks, 0, Worker));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            Assert.That(Get(sim, barracks).ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(Get(sim, barracks).ProductionTicksRemaining, Is.EqualTo(99));
            Assert.That(TrainedTicksUntil(sim, t + 200), Is.EqualTo(new List<int> { t + 149 }), "none on T + 99, one on T + 149");
        }

        /// <summary>IS-14: the only entry is cancelled; everything returns to its value before the order and nothing appears.</summary>
        [Test]
        public void Is14_OnlyEntry()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            int supply = PlayerOf(sim, P0).SupplyUsed;
            int t = QueueWorkers(sim, barracks, 1);
            StepCollect(sim, 4);
            AssertAccepted(sim, Cancel(sim, barracks, 0, Worker));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply));
            Assert.That(Get(sim, barracks).ProductionQueue.Count, Is.EqualTo(0));
            Assert.That(Get(sim, barracks).ProductionTicksRemaining, Is.EqualTo(0));
            Assert.That(TrainedTicksUntil(sim, t + 200), Is.Empty);
        }

        /// <summary>The state a rejection must leave alone: common, supply, queue length, head countdown.</summary>
        private static (int, int, int, int) Snapshot(Simulation sim, EntityId barracks)
        {
            return (PlayerOf(sim, P0).Common, PlayerOf(sim, P0).SupplyUsed, Get(sim, barracks).ProductionQueue.Count, Get(sim, barracks).ProductionTicksRemaining);
        }

        /// <summary>IS-15: every rejection reason; nothing changes and no event is raised.</summary>
        [Test]
        public void Is15_Rejections()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            EntityId idle = Spawn(sim, TestWorld.BarracksId, P0, 14, 16);
            QueueWorkers(sim, barracks, 2);
            (int, int, int, int) before = Snapshot(sim, barracks);
            (PlayerId Issuer, EntityId Producer, int Index, string Definition, string Reason)[] cases =
            {
                (P0, new EntityId(999), 0, Worker, "unknown_unit"),
                (P1, barracks, 0, Worker, "not_owner"),
                (P0, barracks, 2, Worker, "no_entry"),
                (P0, barracks, -1, Worker, "no_entry"),
                (P0, idle, 0, Worker, "no_entry"),
                (P0, barracks, 1, TestWorld.RunnerId, "entry_mismatch"),
                (P0, barracks, 5, TestWorld.RunnerId, "no_entry"),
            };
            foreach ((PlayerId issuer, EntityId producer, int index, string definition, string reason) in cases)
            {
                List<SimEvent> events = Do(sim, new CancelProductionCommand(sim.Clock.Tick, issuer, Seq(), producer, index, definition));
                Assert.That(Reasons(events), Is.EqualTo(new List<string> { reason }), reason);
                Assert.That(Of<ProductionCancelledEvent>(events), Is.Empty, reason);
            }
            // The queue ran on during those ticks (one tick each); only the head countdown may differ.
            Assert.That(Snapshot(sim, barracks).Item1, Is.EqualTo(before.Item1));
            Assert.That(Snapshot(sim, barracks).Item2, Is.EqualTo(before.Item2));
            Assert.That(Snapshot(sim, barracks).Item3, Is.EqualTo(before.Item3));
            Assert.That(Snapshot(sim, barracks).Item4, Is.EqualTo(before.Item4 - cases.Length));
        }

        /// <summary>IS-16: two cancels on one tick apply in (issuer, sequence) order, each against the queue as the previous left it.</summary>
        [Test]
        public void Is16_SameTick()
        {
            Simulation sim = WithBarracks(out EntityId barracks);
            QueueWorkers(sim, barracks, 3);
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(50));
            int tick = sim.Clock.Tick;
            sim.Submit(new CancelProductionCommand(tick, P0, Seq(), barracks, 1, Worker));
            sim.Submit(new CancelProductionCommand(tick, P0, Seq(), barracks, 1, Worker));
            sim.Step();
            Assert.That(Reasons(new List<SimEvent>(sim.Events.Current)), Is.Empty);
            Assert.That(Of<ProductionCancelledEvent>(new List<SimEvent>(sim.Events.Current)).Count, Is.EqualTo(2));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            Assert.That(Get(sim, barracks).ProductionQueue.Count, Is.EqualTo(1));

            // Two queued, index 1 cancelled twice: the second finds no entry.
            Simulation two = WithBarracks(out EntityId b2);
            QueueWorkers(two, b2, 2);
            int tick2 = two.Clock.Tick;
            two.Submit(new CancelProductionCommand(tick2, P0, Seq(), b2, 1, Worker));
            two.Submit(new CancelProductionCommand(tick2, P0, Seq(), b2, 1, Worker));
            two.Step();
            List<SimEvent> events = new List<SimEvent>(two.Events.Current);
            Assert.That(Of<ProductionCancelledEvent>(events).Count, Is.EqualTo(1));
            Assert.That(Reasons(events), Is.EqualTo(new List<string> { "no_entry" }));
        }

        /// <summary>IS-17: an HQ that is switching (Uprooting) pauses its queue but its entry can still be cancelled, with a full refund.</summary>
        [Test]
        public void Is17_PausedHq()
        {
            Simulation sim = TestWorld.StartedEconomy();
            EntityId hq = Spawn(sim, TestWorld.HqId, P0, 12, 12);
            int supply = PlayerOf(sim, P0).SupplyUsed;
            AssertAccepted(sim, new TrainUnitCommand(sim.Clock.Tick, P0, Seq(), hq, Worker));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(150));
            AssertAccepted(sim, new UprootHqCommand(sim.Clock.Tick, P0, Seq(), hq));
            Assert.That(Get(sim, hq).HqState, Is.EqualTo(HqState.Uprooting));
            AssertAccepted(sim, Cancel(sim, hq, 0, Worker));
            Assert.That(PlayerOf(sim, P0).Common, Is.EqualTo(200));
            Assert.That(PlayerOf(sim, P0).SupplyUsed, Is.EqualTo(supply));
            Assert.That(Get(sim, hq).ProductionQueue.Count, Is.EqualTo(0));
        }

        /// <summary>The command, the codec and the script reader agree: payload round trip, script line, and two identical runs with cancels end in equal hashes (IS-19, IS-20).</summary>
        [Test]
        public void Is19_Is20_CodecScriptAndDeterminism()
        {
            CancelProductionCommand original = new CancelProductionCommand(30, P1, 9, new EntityId(77), 3, "water.tender");
            CommandCodecRegistry registry = BuiltInMechanics.CreateCodecRegistry();
            CancelProductionCommand decoded = (CancelProductionCommand)registry.DecodePayload(original.Kind, original.Tick, original.Issuer, original.Sequence, registry.EncodePayload(original));
            Assert.That((decoded.Kind, decoded.Producer, decoded.Index, decoded.DefinitionId), Is.EqualTo(("cancel_production", new EntityId(77), 3, "water.tender")));

            string line = "{ \"type\": \"cancel_production\", \"tick\": 30, \"issuer\": 1, \"sequence\": 9, \"producer\": 77, \"index\": 3, \"definition\": \"water.tender\" }";
            string text = "{ \"$schema_version\": 1, \"seed\": 7, \"ticks\": 40, \"players\": [ { \"id\": 0, \"team\": 0, \"faction\": \"skai\" } ], \"commands\": [ " + line + " ] }";
            CancelProductionCommand scripted = (CancelProductionCommand)CommandScript.Parse(text, "test/cancel.json").Commands.Single();
            Assert.That((scripted.Tick, scripted.Issuer, scripted.Sequence, scripted.Producer, scripted.Index, scripted.DefinitionId),
                Is.EqualTo((30, P1, 9u, new EntityId(77), 3, "water.tender")));
            Expect.Throws<DataValidationException>(() => CommandScript.Parse(text.Replace("\"index\": 3", "\"index\": 20"), "test/cancel.json"));

            // The same orders twice give the same state hash.
            ulong Run()
            {
                Simulation sim = WithBarracks(out EntityId barracks);
                QueueWorkers(sim, barracks, 3);
                StepCollect(sim, 20);
                Do(sim, new CancelProductionCommand(sim.Clock.Tick, P0, 1, barracks, 1, Worker));
                StepCollect(sim, 30);
                Do(sim, new CancelProductionCommand(sim.Clock.Tick, P0, 2, barracks, 0, Worker));
                StepCollect(sim, 150);
                return sim.ComputeStateHash();
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }

    /// <summary>The research case of cancel_production (IS-18), with the research fixture of ResearchTests.</summary>
    public sealed partial class ResearchTests
    {
        /// <summary>IS-18: a queued research item is cancelled; common and rare return, the item can be queued again and is paid again.</summary>
        [Test]
        public void Is18_CancelResearch()
        {
            Simulation sim = WithLab(out EntityId lab, out _);
            (int, int) before = (PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare);
            int t = sim.Clock.Tick;
            AssertAccepted(sim, Train(sim, lab, PlansA));
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.Not.EqualTo(before));
            StepTo(sim, t + 10);
            List<SimEvent> events = Do(sim, new CancelProductionCommand(sim.Clock.Tick, P0, Seq(), lab, 0, PlansA));
            Assert.That(Reasons(events), Is.Empty);
            Assert.That(Of<ProductionCancelledEvent>(events).Single().RefundRare, Is.EqualTo(50));
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.EqualTo(before));
            Assert.That(EconomyRules.IsResearchQueued(sim, P0, PlansA), Is.False);
            Assert.That(Of<ResearchCompletedEvent>(StepTo(sim, t + 100)), Is.Empty);
            Assert.That(PlayerOf(sim, P0).HasResearched(PlansA), Is.False);
            AssertAccepted(sim, Train(sim, lab, PlansA));
            Assert.That((PlayerOf(sim, P0).Common, PlayerOf(sim, P0).Rare), Is.EqualTo((before.Item1 - 25, before.Item2 - 50)));
        }
    }
}
