// rts/tools/BalanceHarness/FightRunner.cs
// Plays one equal-value fight (spec: rts/docs/specs/micro_benchmark.md sections 4.3 to 4.6) and returns its FightRecord. The fight is built in memory like the HQ floor arena
// (HqFloorTrial): an arena MapDefinition per terrain case, tick-S SpawnUnitCommands from the formation, a read-only controller that submits plain attack-move orders as the seats'
// players (4.4), and a read-only observer that decides the result after every step from events and state (4.5). Nothing here feeds back into a simulation other than through Submit.
// Tool-side only; the clock-free statistics use doubles. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>A played fight: its record and how many orders each seat's controller issued.</summary>
    public sealed class FightRun
    {
        /// <summary>The result record.</summary>
        public FightRecord Record { get; }

        /// <summary>Orders submitted for seat 0 and seat 1. Unit: commands. Range: 0 upward.</summary>
        public IReadOnlyList<int> Commands { get; }

        /// <summary>Bundles the values.</summary>
        public FightRun(FightRecord record, IReadOnlyList<int> commands)
        {
            Record = record;
            Commands = commands;
        }
    }

    /// <summary>Plays fights.</summary>
    public static class FightRunner
    {
        /// <summary>Fight seed: <see cref="SeedDerivation.GameSeed"/> with the mode name "fights" as the map and the fight ID as the matchup, game 0.</summary>
        /// <param name="baseSeed">Base seed. Range: any ulong.</param>
        /// <param name="fightId">Fight ID.</param>
        public static ulong SeedOf(ulong baseSeed, string fightId)
        {
            return SeedDerivation.GameSeed(baseSeed, "fights", fightId, 0);
        }

        /// <summary>Plays one fight.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="settings">The parsed settings.</param>
        /// <param name="spec">The planned fight.</param>
        /// <param name="baseSeed">Base seed of the seed derivation. Range: any ulong.</param>
        public static FightRun Run(DataSet data, FightSettings settings, FightSpec spec, ulong baseSeed)
        {
            ulong seed = SeedOf(baseSeed, spec.Id);
            bool flooded = spec.Case.Kind == FightKinds.Flooded;
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), spec.Seat0.Faction, "seat 0"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), spec.Seat1.Faction, "seat 1"),
            };
            if (flooded)
            {
                // The flood belongs to seat 1's team (slot 2, team 1).
                players.Add(new PlayerSetup(new PlayerId(2), new TeamId(1), data.Content.Units[settings.FloodSourceUnit].FactionId, "flood"));
            }
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(seed), data.Content, players, BuiltInMechanics.CreateRegistry(), data.Tuning,
                FightPlan.BuildArena(settings, spec.Case), null));
            sim.Start();
            // Army spawn tick: after the flood warm-up in the flooded case, else 0. Unit: ticks.
            int spawnTick = flooded ? sim.Clock.SecondsToTicks(settings.FloodWarmupSeconds) : 0;
            // Arena centre and front columns. Unit: tiles.
            int cx = FightPlan.CentreX(settings);
            int cy = FightPlan.CentreY(settings);
            int f0 = FightPlan.FrontColumn0(settings);
            int f1 = FightPlan.FrontColumn1(settings);
            uint sequence = 0;
            if (flooded)
            {
                foreach (KeyValuePair<int, int> offset in settings.FloodSourceOffsets)
                {
                    sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, settings.FloodSourceUnit, new PlayerId(2),
                        new FixVec2(Fix64.FromInt(cx + offset.Key) + Fix64.Half, Fix64.FromInt(cy + offset.Value) + Fix64.Half)));
                }
            }
            for (int seat = 0; seat < 2; seat++)
            {
                foreach (FightSpawn spawn in FightPlan.Formation(data, settings, spec, seat))
                {
                    sim.Submit(new SpawnUnitCommand(spawnTick, PlayerId.None, sequence++, spawn.Unit, new PlayerId((byte)seat), spawn.Position));
                }
            }
            // Orders per seat, and the controller's targets for tick S + 1: the enemy front-rank centre of each seat. Unit: tiles (fixed-point).
            int[] commands = new int[2];
            FixVec2[] goal = { new FixVec2(Fix64.FromInt(f1) + Fix64.Half, Fix64.FromInt(cy) + Fix64.Half), new FixVec2(Fix64.FromInt(f0) + Fix64.Half, Fix64.FromInt(cy) + Fix64.Half) };
            // Seat 1 holds on the plateau and gets no command.
            bool[] attacks = { true, spec.Case.Kind != FightKinds.HighGround };
            // Idle re-targeting interval, stalemate confirmation, idle limit and cap. Unit: ticks.
            int reissue = sim.Clock.SecondsToTicks(settings.ReissueSeconds);
            int confirm = sim.Clock.SecondsToTicks(settings.StalemateConfirmSeconds);
            int idleLimit = sim.Clock.SecondsToTicks(settings.StalemateIdleSeconds);
            int cap = sim.Clock.SecondsToTicks(settings.MaxFightSeconds);
            // Flooded lane tiles measured at army spawn, or -1 outside the flooded kind. Unit: tiles.
            int floodedLane = -1;
            // Army units per seat (ascending entity ID), their entity-to-seat map, start values; filled after the spawn tick.
            List<Unit>[] army = { new List<Unit>(), new List<Unit>() };
            SortedDictionary<EntityId, int> seatOf = new SortedDictionary<EntityId, int>();
            // Last tick with damage to an army unit, the consecutive ticks neither side could hurt the other, and the result.
            int lastDamage = spawnTick;
            int cannotStreak = 0;
            string result = string.Empty;
            string reason = string.Empty;
            int endTick = 0;
            int alive0 = 0;
            int alive1 = 0;
            for (int t = 0; result.Length == 0; t++)
            {
                if (t == spawnTick && flooded)
                {
                    floodedLane = CountFloodedLane(sim, settings, spec.Id);
                }
                sim.Step();
                if (t < spawnTick)
                {
                    continue;
                }
                if (t == spawnTick)
                {
                    foreach (Unit unit in sim.Units.All)
                    {
                        if (unit.Owner.IsValid && unit.Owner.Value <= 1 && FightSettings.IsArmy(unit.Definition))
                        {
                            army[unit.Owner.Value].Add(unit);
                            seatOf[unit.Id] = unit.Owner.Value;
                        }
                    }
                }
                foreach (SimEvent simEvent in sim.Events.Current)
                {
                    if (simEvent is UnitDamagedEvent damaged && damaged.Damage > Fix64.Zero && seatOf.ContainsKey(damaged.Target))
                    {
                        lastDamage = t;
                    }
                }
                // Live units per seat, and the layers of the enemy units each seat's weapons would have to hit.
                List<Unit> live0 = Alive(sim, army[0]);
                List<Unit> live1 = Alive(sim, army[1]);
                alive0 = live0.Count;
                alive1 = live1.Count;
                endTick = t - spawnTick;
                if (alive0 == 0 || alive1 == 0)
                {
                    result = alive0 == 0 && alive1 == 0 ? FightRecord.Draw : alive0 == 0 ? FightRecord.Seat1Won : FightRecord.Seat0Won;
                    reason = alive0 == 0 && alive1 == 0 ? "both_eliminated" : "eliminated";
                    break;
                }
                cannotStreak = CanHurt(sim, live0, live1) || CanHurt(sim, live1, live0) ? 0 : cannotStreak + 1;
                if (cannotStreak >= confirm)
                {
                    result = FightRecord.Stalemate;
                    reason = "cannot_target";
                }
                else if (t - lastDamage >= idleLimit)
                {
                    result = FightRecord.Stalemate;
                    reason = "no_damage";
                }
                else if (t - spawnTick >= cap)
                {
                    result = FightRecord.Unresolved;
                    reason = "time_cap";
                }
                else if (t == spawnTick || (t - spawnTick) % reissue == 0)
                {
                    // Orders for the next tick: the opening attack-move at S + 1, then the idle re-targeting every R ticks (m >= 1).
                    Control(sim, t == spawnTick, attacks, goal, new List<Unit>[] { live0, live1 }, commands);
                }
            }
            return new FightRun(BuildRecord(sim, spec, army, result, reason, endTick, alive0, alive1, floodedLane, seed), commands);
        }

        /// <summary>Submits the orders for the next tick (spec 4.4): <paramref name="opening"/> sends every unit of an attacking seat to the enemy front-rank centre, otherwise each idle unit goes to the nearest enemy it can hit.</summary>
        private static void Control(Simulation sim, bool opening, bool[] attacks, FixVec2[] goal, List<Unit>[] live, int[] commands)
        {
            // Tick the orders apply on. Unit: ticks.
            int next = sim.Clock.Tick;
            for (int seat = 0; seat < 2; seat++)
            {
                if (!attacks[seat])
                {
                    continue;
                }
                // Sequence of this issuer on this tick. Unit: sequence number.
                uint sequence = 0;
                foreach (Unit unit in live[seat])
                {
                    FixVec2 target = goal[seat];
                    if (!opening)
                    {
                        if (unit.HasMoveTarget || unit.AttackTarget != EntityId.None)
                        {
                            continue;
                        }
                        Unit? nearest = null;
                        Fix64 best = Fix64.Zero;
                        // Enemy units ascending by ID; a strictly closer one wins, so ties go to the lower ID.
                        foreach (Unit enemy in live[1 - seat])
                        {
                            if (!CombatSystem.CanAttack(sim, unit, enemy))
                            {
                                continue;
                            }
                            Fix64 distance = (enemy.Position - unit.Position).LengthSquared();
                            if (nearest == null || distance < best)
                            {
                                nearest = enemy;
                                best = distance;
                            }
                        }
                        if (nearest == null)
                        {
                            continue;
                        }
                        target = nearest.Position;
                    }
                    sim.Submit(new AttackMoveCommand(next, new PlayerId((byte)seat), sequence++, unit.Id, target));
                    commands[seat]++;
                }
            }
        }

        /// <summary>The units of <paramref name="army"/> still in the simulation, in the list's (ascending ID) order.</summary>
        private static List<Unit> Alive(Simulation sim, List<Unit> army)
        {
            List<Unit> live = new List<Unit>();
            foreach (Unit unit in army)
            {
                if (sim.Units.Contains(unit.Id))
                {
                    live.Add(unit);
                }
            }
            return live;
        }

        /// <summary>
        /// True when some unit of <paramref name="own"/> can hit some unit of <paramref name="enemy"/> right now: the weapon's layer list, or flyers while
        /// the shooter stands on water (CombatSystem.CanAttack, combat_mechanics.md 3.1), so a conditional target rule never declares a false stalemate.
        /// </summary>
        private static bool CanHurt(Simulation sim, List<Unit> own, List<Unit> enemy)
        {
            foreach (Unit unit in own)
            {
                foreach (Unit target in enemy)
                {
                    if (CombatSystem.CanAttack(sim, unit, target))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Counts the flooded lane tiles and throws a settings error when fewer than flood_min_lane_fraction are flooded.</summary>
        private static int CountFloodedLane(Simulation sim, FightSettings settings, string fightId)
        {
            // Lane tiles and how many of them are flooded. Unit: tiles.
            int lane = 0;
            int flooded = 0;
            for (int y = 0; y < settings.ArenaHeightTiles; y++)
            {
                for (int x = 0; x < settings.ArenaWidthTiles; x++)
                {
                    if (FightPlan.IsLaneTile(settings, x, y))
                    {
                        lane++;
                        flooded += sim.Terrain.At(x, y).IsFlooded ? 1 : 0;
                    }
                }
            }
            // Measured share of the lane. Unit: fraction. Range: 0 to 1.
            double fraction = lane == 0 ? 0.0 : (double)flooded / lane;
            if (fraction < settings.FloodMinLaneFraction)
            {
                throw new HarnessException("fight " + fightId + ": settings error, only " + flooded.ToString(CultureInfo.InvariantCulture) + " of " + lane.ToString(CultureInfo.InvariantCulture)
                    + " lane tiles are flooded when the armies spawn (fraction " + fraction.ToString("0.000", CultureInfo.InvariantCulture) + ", flood_min_lane_fraction "
                    + settings.FloodMinLaneFraction.ToString("0.###", CultureInfo.InvariantCulture) + "); raise flood_warmup_seconds or add flood sources");
            }
            return flooded;
        }

        /// <summary>Assembles the record: the armies, the value left of each seat and the final state hash.</summary>
        private static FightRecord BuildRecord(Simulation sim, FightSpec spec, List<Unit>[] army, string result, string reason, int endTick, int alive0, int alive1, int floodedLane, ulong seed)
        {
            double[] left = new double[2];
            for (int seat = 0; seat < 2; seat++)
            {
                // Sum of value times health fraction over live units, over the start value. Unit: fraction.
                Fix64 sum = Fix64.Zero;
                foreach (Unit unit in army[seat])
                {
                    if (sim.Units.Contains(unit.Id))
                    {
                        sum += Fix64.FromInt(FightSettings.ValueOf(unit.Definition)) * unit.Health / unit.Definition.MaxHealth;
                    }
                }
                left[seat] = RoundThree(Math.Min(1.0, HarnessStats.ToDouble(sum / Fix64.FromInt(seat == 0 ? spec.Value0 : spec.Value1))));
            }
            // Tenths of a second, rounded half up. Unit: tenths of seconds.
            long tenths = ((long)endTick * 10 * 2 + sim.Clock.TicksPerSecond) / (2L * sim.Clock.TicksPerSecond);
            string seconds = (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
            return new FightRecord(spec.Id, spec.Case.Id, spec.ArmyValue, spec.Seat0.Id, spec.Seat1.Id, Counts(spec.Seat0, spec.Multiplier0), Counts(spec.Seat1, spec.Multiplier1),
                spec.Value0, spec.Value1, spec.Mismatch, result, reason, endTick, seconds, left[0], left[1], alive0, alive1, floodedLane, seed, sim.ComputeStateHash());
        }

        /// <summary>Unit counts of a side's army: recipe counts times the multiplier, in ordinal unit ID order.</summary>
        private static IReadOnlyDictionary<string, int> Counts(FightSide side, int multiplier)
        {
            SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in side.Recipe)
            {
                counts[pair.Key] = pair.Value * multiplier;
            }
            return counts;
        }

        /// <summary>Rounds to three decimals through text, so the value equals what <see cref="FightRecord.Parse"/> reads back.</summary>
        private static double RoundThree(double value)
        {
            return double.Parse(value.ToString("0.000", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
