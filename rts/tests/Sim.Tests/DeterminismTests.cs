// rts/tests/Sim.Tests/DeterminismTests.cs
// The Phase 0 exit test: the same scripted command stream run twice gives identical state hashes at every
// hash interval and at the end, and a different seed or different commands give a different hash.
// Part of Rts.Sim.Tests. CI runs this on every push.
using System.Collections.Generic;
using NUnit.Framework;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;

namespace Rts.Sim.Tests
{
    [TestFixture]
    public sealed class DeterminismTests
    {
        /// <summary>Ticks to run in each scripted match. Range: enough to cover several hash intervals and arrivals.</summary>
        private const int MatchTicks = 400;

        /// <summary>Builds the scripted command stream: script-issued spawns, player-issued moves and a removal.</summary>
        private static List<Command> Script()
        {
            List<Command> script = new List<Command>();
            PlayerId p0 = new PlayerId(0);
            PlayerId p1 = new PlayerId(1);
            // Spawns come from the script issuer (None) with sequences 0 to 9: P0's units get IDs 1 to 5, P1's 6 to 10.
            for (int i = 0; i < 5; i++)
            {
                script.Add(new SpawnUnitCommand(0, PlayerId.None, (uint)i, TestWorld.RunnerId, p0, FixVec2.FromInts(i, 0)));
                script.Add(new SpawnUnitCommand(0, PlayerId.None, (uint)(5 + i), TestWorld.RunnerId, p1, FixVec2.FromInts(i, 20)));
            }
            for (int i = 0; i < 5; i++)
            {
                script.Add(new MoveUnitCommand(1, p0, (uint)i, new EntityId((uint)(i + 1)), FixVec2.FromInts(10 + i, 10)));
                script.Add(new MoveUnitCommand(1, p1, (uint)i, new EntityId((uint)(i + 6)), FixVec2.FromInts(10 + i, 10)));
            }
            script.Add(new RemoveUnitCommand(50, p1, 10, new EntityId(7)));
            script.Add(new MoveUnitCommand(120, p0, 10, new EntityId(1), FixVec2.FromInts(0, 0)));
            return script;
        }

        /// <summary>Runs the script on a fresh simulation and returns every reported hash followed by the final hash.</summary>
        private static List<ulong> Run(ulong seed, List<Command> script)
        {
            Simulation sim = TestWorld.Started(seed);
            foreach (Command command in script)
            {
                sim.Submit(command);
            }
            List<ulong> hashes = new List<ulong>();
            for (int tick = 0; tick < MatchTicks; tick++)
            {
                sim.Step();
                foreach (SimEvent evt in sim.Events.Current)
                {
                    if (evt is StateHashedEvent hashed)
                    {
                        hashes.Add(hashed.Hash);
                    }
                }
            }
            hashes.Add(sim.ComputeStateHash());
            return hashes;
        }

        [Test]
        public void SameScriptTwice_IdenticalHashes()
        {
            List<ulong> first = Run(777, Script());
            List<ulong> second = Run(777, Script());
            Assert.That(first.Count, Is.EqualTo(MatchTicks / TestWorld.HashInterval + 1));
            Assert.That(second, Is.EqualTo(first));
        }

        /// <summary>
        /// Final hash of <see cref="Script"/> with seed 777 after <see cref="MatchTicks"/> ticks, re-pinned on 27 Oct 2026 after the Phase 1 review fixes (content digest covers tuning and map).
        /// Re-pinned once more in Phase 2 (rts/docs/specs/planetary_cycles.md section 2, "This changes every state hash once"): the
        /// state hash now includes the cycle and Core state (CycleSystem.WriteHash, after the resource fields), the "cycle" speed and damage
        /// modifier IDs, the quake paths in the map hash, the cycle list in the content digest, and new tuning keys (core_win_min_teams,
        /// quake_path_relocate_radius_tiles, starting_worker_count) in the tuning text. The loader also hashes line-ending-normalised text
        /// now, so the value is the same on Windows (CRLF) and Unix (LF) checkouts. Re-pinned again when this merged with Phase 2
        /// step 1, whose tuning/online.json keys (input_delay_ticks, catch_up_max_ticks_per_update) are also in the tuning text.
        /// Re-pinned for the Phase 2 cycles review fixes: tuning/economy.json gained starting_worker_search_radius_tiles (a key and its
        /// comment are in the tuning text, which is in the content digest). No behaviour of this script changed.
        /// Re-pinned for P-003 package A (rts/docs/specs/p003_rules.md): new combat tuning key, hit_delay_ticks and worker weapons and the Shellback dome in
        /// the unit texts, and new hashed state (pending hit queue, dome fields). No other behaviour of this script was intended to change.
        /// Re-pinned for P-003 package B (specs/p003_rules.md B.6): six new tuning/terrain.json keys are in the tuning text, and units of this script may now separate.
        /// Re-pinned for worker construction (specs/worker_construction.md): new tuning key worker_placement_seconds and workers walk to build sites.
        /// Re-pinned for combat mechanics package 1 (specs/combat_mechanics.md): new combat.json key splash_damage_fraction, new weapon field SplashRadiusTiles and four new pending-hit fields are in the content digest and the hash. Old value 0xc152b29fd0252a8d. Hash only: with the digest and the new fields left out of the hash, the old and new code give the same final hash (5508489595702149084).
        /// Re-pinned for combat mechanics package 2 (specs/combat_mechanics.md): weapon field StructureDamageMultiplier is in the content digest. Old value 0xdb52606c6d2af2b6; hash only.
        /// Re-pinned for F0 (specs/micro_benchmark.md 3.3, developer decision 4 of 18 Nov 2026: Rooted HQ floor 20 s to 15 s in hq.json and harness.json, both in the content digest). Old value 0xc15f1f545726486b; hash only (TestWorld loads the shipped tuning).
        /// Re-pinned for combat mechanics package 3 (specs/combat_mechanics.md: can_target_air_on_water is in the weapon digest, Reef Runner and Shellback carry it). Old value 0xaf43113163592096; hash only.
        /// A change here means the simulation no longer produces the same match from the same inputs: either a
        /// determinism bug or an intended change to hashing or behaviour. If intended, record why in the changelog and
        /// update the value; replays recorded before the change will no longer re-simulate.
        /// </summary>
        private const ulong GoldenFinalHash = 0xba66686db1dce26dUL;

        [Test]
        public void GoldenHash_IsPinned()
        {
            // Unlike SameScriptTwice, this catches drift between processes, machines and builds, not just within one run.
            List<ulong> hashes = Run(777, Script());
            Assert.That(hashes[hashes.Count - 1], Is.EqualTo(GoldenFinalHash));
        }

        [Test]
        public void DifferentSeed_DifferentHash()
        {
            List<ulong> a = Run(1, Script());
            List<ulong> b = Run(2, Script());
            Assert.That(a[a.Count - 1], Is.Not.EqualTo(b[b.Count - 1]));
        }

        [Test]
        public void DifferentCommands_DifferentHash()
        {
            List<Command> altered = Script();
            altered.Add(new MoveUnitCommand(200, new PlayerId(0), 11, new EntityId(3), FixVec2.FromInts(1, 1)));
            List<ulong> a = Run(5, Script());
            List<ulong> b = Run(5, altered);
            Assert.That(a[a.Count - 1], Is.Not.EqualTo(b[b.Count - 1]));
        }
    }
}
