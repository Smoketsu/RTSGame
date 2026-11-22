// rts/tests/Sim.Tests/Harness/ResearchAccountingTests.cs
// Test RA-8 of rts/docs/specs/research_and_unlocks.md R2.4 (harness accounting, R2.3): research orders are charged to the "research" ledger class, completed research counts in the slot's Completed
// map, and BalanceReport lists a research row for each of the four items. Part of Rts.Sim.Tests.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim.Data;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;
using Rts.Tools.BalanceHarness;

namespace Rts.Sim.Tests.Harness
{
    /// <summary>Harness accounting of research.</summary>
    [TestFixture]
    public sealed class ResearchAccountingTests
    {
        /// <summary>RA-8a: SpendResearch adds a cost to the totals and to the research class only.</summary>
        [Test]
        public void Ledger_ChargesResearchToItsOwnClass()
        {
            DataSet data = TestData.LoadAll();
            PlayerLedger ledger = new PlayerLedger();
            ledger.SpendResearch(data.Content.Researches["skai.thunderhead_plans"]);
            Assert.That(ledger.ByClass[PlayerLedger.ClassResearch], Is.EqualTo(new[] { 25, 175 }));
            Assert.That(ledger.SpentCommon, Is.EqualTo(25));
            Assert.That(ledger.SpentRare, Is.EqualTo(175));
            Assert.That(ledger.ByClass[PlayerLedger.ClassArmy], Is.EqualTo(new[] { 0, 0 }));
        }

        /// <summary>RA-8b: a game on the research candidate completes research; the slot's Completed holds it, the research class holds at least its cost, and the report lists the four items.</summary>
        [Test]
        public void Game_CompletedAndLedgerAndReportRows()
        {
            DataSet data = TestData.LoadAll();
            ulong seed = SeedDerivation.GameSeed(20261026UL, "prototype_1v1", "skai_vs_water", 0);
            GameRecord game = GameRunner.Play(data, "prototype_1v1", "skai_vs_water", 0, seed, "aggressive_s5_r", "aggressive_s5_r", 600, null, -1, -1, new SummaryObserver(30, new[] { 300, 600 }));
            // Rare cost of the research the slots completed, and what the ledgers charged for research.
            int completedRare = 0;
            int chargedRare = 0;
            int completedItems = 0;
            foreach (GameSummary summary in new[] { game.Summary0!, game.Summary1! })
            {
                foreach (KeyValuePair<string, int> pair in summary.Completed)
                {
                    if (data.Content.Researches.TryGetValue(pair.Key, out ResearchDefinition? research))
                    {
                        completedRare += research.CostRare * pair.Value;
                        completedItems += pair.Value;
                    }
                }
                chargedRare += summary.Spent[PlayerLedger.ClassResearch][1];
            }
            Assert.That(completedItems, Is.GreaterThan(0), "the candidate finished some research in 600 s");
            Assert.That(chargedRare, Is.GreaterThanOrEqualTo(completedRare));
            List<UseRow> rows = BalanceReport.UnitUse(new List<GameRecord> { game }, data);
            foreach (string id in new[] { "skai.stormcaller_plans", "skai.thunderhead_plans", "water.ink_lobber_plans", "water.leviathan_plans" })
            {
                Assert.That(rows.Find(r => r.Kind == "research" && r.Id == id), Is.Not.Null, id);
            }
        }
    }
}
