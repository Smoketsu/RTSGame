// rts/tools/BalanceHarness/HqFloorTrial.cs
// The HQ survivability floor trials of the balance harness (spec: rts/docs/specs/balance_harness.md section 4). For one (attacker faction, unit
// type, defender faction, HQ state) it builds a flat arena in memory, places hq_floor_supply worth of that unit at a fixed standoff from the defender's
// HQ, orders them to attack on tick 1 and measures how long the HQ lives once damaged. Plan lists every trial a matchup set needs. Pure of
// processes and files; the worker and the tests call Run. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One planned HQ floor trial.</summary>
    public sealed class HqFloorSpec
    {
        /// <summary>Faction whose units attack.</summary>
        public string AttackerFaction { get; }

        /// <summary>Unit definition ID of the attackers.</summary>
        public string Unit { get; }

        /// <summary>Faction whose HQ defends.</summary>
        public string DefenderFaction { get; }

        /// <summary>True when the HQ starts Rooted, false when it starts Uprooted.</summary>
        public bool Rooted { get; }

        /// <summary>Bundles the values.</summary>
        public HqFloorSpec(string attackerFaction, string unit, string defenderFaction, bool rooted)
        {
            AttackerFaction = attackerFaction;
            Unit = unit;
            DefenderFaction = defenderFaction;
            Rooted = rooted;
        }

        /// <summary>The worker argument form "attacker:unit:defender:rooted|uprooted".</summary>
        public string ToArgument()
        {
            return AttackerFaction + ":" + Unit + ":" + DefenderFaction + ":" + (Rooted ? "rooted" : "uprooted");
        }

        /// <summary>Parses <see cref="ToArgument"/>; throws <see cref="HarnessException"/> when malformed.</summary>
        /// <param name="text">The argument value.</param>
        public static HqFloorSpec Parse(string text)
        {
            // The four parts.
            string[] parts = text.Split(':');
            if (parts.Length != 4 || (parts[3] != "rooted" && parts[3] != "uprooted"))
            {
                throw new HarnessException("--hq-floor must be <attacker>:<unit>:<defender>:<rooted|uprooted> (found '" + text + "')");
            }
            return new HqFloorSpec(parts[0], parts[1], parts[2], parts[3] == "rooted");
        }
    }

    /// <summary>Plans and runs HQ floor trials.</summary>
    public static class HqFloorTrial
    {
        /// <summary>Attackers per column of the starting formation. Unit: units. Range: fixed by the spec, 5.</summary>
        private const int ColumnSize = 5;

        /// <summary>
        /// Every trial the matchups need: for each ordered (attacker faction, defender faction) pair from the matchups (both orders, mirrors once),
        /// each mobile, supplied, non-HQ unit of the attacker with a weapon that can target the defender HQ's layer, Rooted then Uprooted. Sorted by
        /// attacker, unit, defender, state (Rooted first within a pair).
        /// </summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="matchups">Matchup IDs "a_vs_b".</param>
        public static List<HqFloorSpec> Plan(DataSet data, IReadOnlyList<string> matchups)
        {
            // Ordered pairs without duplicates, in ordinal order.
            SortedSet<string> pairs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string matchup in matchups)
            {
                string[] sides = GameRunner.SplitMatchup(matchup);
                pairs.Add(sides[0] + ":" + sides[1]);
                pairs.Add(sides[1] + ":" + sides[0]);
            }
            List<HqFloorSpec> specs = new List<HqFloorSpec>();
            foreach (string pair in pairs)
            {
                string[] sides = pair.Split(':');
                UnitDefinition hq = HqOf(data, sides[1]);
                // Attacker units in ordinal ID order.
                SortedDictionary<string, UnitDefinition> units = new SortedDictionary<string, UnitDefinition>(StringComparer.Ordinal);
                foreach (UnitDefinition unit in data.Content.Units.Values)
                {
                    if (unit.FactionId == sides[0])
                    {
                        units.Add(unit.Id, unit);
                    }
                }
                foreach (UnitDefinition unit in units.Values)
                {
                    if (IsFloorAttacker(unit, hq))
                    {
                        specs.Add(new HqFloorSpec(sides[0], unit.Id, sides[1], true));
                        specs.Add(new HqFloorSpec(sides[0], unit.Id, sides[1], false));
                    }
                }
            }
            return specs;
        }

        /// <summary>True when <paramref name="unit"/> is mobile, has supply, is not an HQ and has a weapon that can target the layer of <paramref name="hq"/>.</summary>
        private static bool IsFloorAttacker(UnitDefinition unit, UnitDefinition hq)
        {
            return unit.SpeedTilesPerSecond > Fix64.Zero && unit.Supply > 0 && unit.Hq == null && unit.Economy.FootprintTiles == 0
                && unit.Weapon != null && unit.Weapon.CanTarget(hq.Layer);
        }

        /// <summary>The HQ unit definition of a faction; throws <see cref="HarnessException"/> when the faction has none.</summary>
        private static UnitDefinition HqOf(DataSet data, string factionId)
        {
            if (!data.Content.Factions.TryGetValue(factionId, out FactionDefinition? faction) || faction.HqUnitId == null)
            {
                throw new HarnessException("faction '" + factionId + "' has no hq_unit");
            }
            return data.Content.Units[faction.HqUnitId];
        }

        /// <summary>Runs one trial and returns its record.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="spec">The trial.</param>
        public static HqFloorRecord Run(DataSet data, HqFloorSpec spec)
        {
            HarnessBatchTuning batch = data.Tuning.HarnessBatch;
            UnitDefinition hqDefinition = HqOf(data, spec.DefenderFaction);
            if (!data.Content.Units.TryGetValue(spec.Unit, out UnitDefinition? attacker) || attacker.Supply <= 0)
            {
                throw new HarnessException("HQ floor trial: unit '" + spec.Unit + "' is not a supplied unit");
            }
            // Attackers placed: the floor army's supply divided by the unit's supply (integer division). Unit: units. Range: 0 upward.
            int count = data.Tuning.Harness.HqFloorSupply / attacker.Supply;
            if (count < 1)
            {
                throw new HarnessException("HQ floor trial: unit '" + spec.Unit + "' costs more supply than hq_floor_supply");
            }
            // Arena side and its middle row. Unit: tiles.
            int size = batch.HqArenaSizeTiles;
            int mid = size / 2;
            MapDefinition arena = new MapDefinition("hq_floor_arena", "HQ floor arena", size, size,
                new List<FixVec2> { new FixVec2(Fix64.FromInt(4), Fix64.FromInt(4)), new FixVec2(Fix64.FromInt(size - 4), Fix64.FromInt(size - 4)) },
                null, null, new List<ResourceField> { new ResourceField(8, mid, ResourceKind.Common, 1000000) }, null);
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(new PlayerId(0), new TeamId(0), spec.AttackerFaction, spec.AttackerFaction + " attackers"),
                new PlayerSetup(new PlayerId(1), new TeamId(1), spec.DefenderFaction, spec.DefenderFaction + " defender"),
            };
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(batch.BaseSeed < 0 ? 0UL : (ulong)batch.BaseSeed), data.Content, players,
                BuiltInMechanics.CreateRegistry(), data.Tuning, arena, null));
            sim.Start();
            // HQ position: 12.5 puts a Rooted HQ within root distance of the field; the arena middle leaves the field out of range. Unit: tiles.
            FixVec2 hqPosition = spec.Rooted
                ? new FixVec2(Fix64.Parse("12.5"), Fix64.FromInt(mid) + Fix64.Half)
                : new FixVec2(Fix64.FromInt(mid) + Fix64.Half, Fix64.FromInt(mid) + Fix64.Half);
            // Tile of the HQ. Unit: tiles.
            int hqTileX = hqPosition.X.FloorToInt();
            int hqTileY = hqPosition.Y.FloorToInt();
            // Distance from the HQ tile to the first attacker column: weapon reach plus HQ radius rounded up, plus the standoff. Unit: tiles.
            Fix64 reach = (hqDefinition.Weapon != null ? hqDefinition.Weapon.RangeTiles : Fix64.Zero) + hqDefinition.Radius;
            int gap = reach.CeilingToInt() + batch.HqFloorStandoffTiles;
            uint sequence = 0;
            sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, hqDefinition.Id, new PlayerId(1), hqPosition));
            for (int i = 0; i < count; i++)
            {
                // Column and row of the attacker in the formation. Unit: tiles.
                int column = i / ColumnSize;
                int row = i % ColumnSize;
                sim.Submit(new SpawnUnitCommand(0, PlayerId.None, sequence++, attacker.Id, new PlayerId(0),
                    new FixVec2(Fix64.FromInt(hqTileX + gap + column) + Fix64.Half, Fix64.FromInt(hqTileY - 2 + row) + Fix64.Half)));
            }
            // Ticks the trial may run. Unit: ticks.
            int maxTicks = sim.Clock.SecondsToTicks(Fix64.FromInt(batch.HqFloorMaxTestSeconds));
            // Entity ID of the defending HQ, and the attackers in ascending ID order.
            EntityId hqId = default;
            List<EntityId> attackers = new List<EntityId>();
            // First damage tick and death tick of the HQ; -1 until seen.
            int firstDamage = -1;
            int death = -1;
            for (int tick = 0; tick < maxTicks && death < 0; tick++)
            {
                sim.Step();
                if (tick == 0)
                {
                    // Units spawned by the tick-0 commands, in ascending ID order (the registry is sorted).
                    SortedSet<EntityId> mine = new SortedSet<EntityId>();
                    foreach (Unit unit in sim.Units.All)
                    {
                        if (unit.Owner.Value == 1)
                        {
                            hqId = unit.Id;
                        }
                        else
                        {
                            mine.Add(unit.Id);
                        }
                    }
                    attackers.AddRange(mine);
                    if (attackers.Count != count || !sim.Units.Contains(hqId))
                    {
                        throw new HarnessException("HQ floor trial " + spec.ToArgument() + ": the arena start did not spawn the expected units");
                    }
                    uint attackSequence = 0;
                    foreach (EntityId id in attackers)
                    {
                        sim.Submit(new AttackUnitCommand(1, new PlayerId(0), attackSequence++, id, hqId));
                    }
                }
                foreach (SimEvent simEvent in sim.Events.Current)
                {
                    if (simEvent is UnitDamagedEvent damaged && damaged.Target.Equals(hqId) && firstDamage < 0 && damaged.Damage > Fix64.Zero)
                    {
                        firstDamage = damaged.Tick;
                    }
                    else if (simEvent is UnitRemovedEvent removed && removed.Unit.Equals(hqId))
                    {
                        death = removed.Tick;
                    }
                }
            }
            return new HqFloorRecord(spec.AttackerFaction, spec.Unit, count, hqDefinition.Id, spec.Rooted ? "rooted" : "uprooted", firstDamage, death);
        }
    }
}
