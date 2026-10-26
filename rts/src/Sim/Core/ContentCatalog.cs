// rts/src/Sim/Core/ContentCatalog.cs
// Every data-defined definition a match can reference, keyed by stable ID in ordinal order.
// Part of Rts.Sim.Core. The data loader (Rts.Sim.Data) fills it from data/ files; tests may fill it in
// code. The simulation only reads it.
using System;
using System.Collections.Generic;
using Rts.Sim.Combat;
using Rts.Sim.Factions;
using Rts.Sim.Units;

namespace Rts.Sim.Core
{
    /// <summary>Unit and faction definitions for a match. Add-only; built before <see cref="Simulation.Start"/>.</summary>
    public sealed class ContentCatalog
    {
        /// <summary>Unit definitions by ID, ordinal order.</summary>
        private readonly SortedDictionary<string, UnitDefinition> _units = new SortedDictionary<string, UnitDefinition>(StringComparer.Ordinal);

        /// <summary>Faction definitions by ID, ordinal order.</summary>
        private readonly SortedDictionary<string, FactionDefinition> _factions = new SortedDictionary<string, FactionDefinition>(StringComparer.Ordinal);

        /// <summary>Rules the combat system plays by (damage table, acquire bonus). Defaults to <see cref="CombatRules.Neutral"/>; the data loader sets the rules from tuning/combat.json.</summary>
        public CombatRules Combat { get; private set; } = CombatRules.Neutral;

        /// <summary>True once <see cref="Freeze"/> has run (at <see cref="Simulation.Start"/>); Add methods then throw.</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>Unit definitions by ID. Iteration is in ordinal ID order.</summary>
        public IReadOnlyDictionary<string, UnitDefinition> Units => _units;

        /// <summary>Faction definitions by ID. Iteration is in ordinal ID order.</summary>
        public IReadOnlyDictionary<string, FactionDefinition> Factions => _factions;

        /// <summary>Adds a unit definition. A duplicate ID throws so two data files cannot silently collide.</summary>
        public void AddUnit(UnitDefinition definition)
        {
            RequireNotFrozen();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (_units.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Duplicate unit definition id '" + definition.Id + "'.");
            }
            _units.Add(definition.Id, definition);
        }

        /// <summary>Adds a faction definition. A duplicate ID throws.</summary>
        public void AddFaction(FactionDefinition definition)
        {
            RequireNotFrozen();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (_factions.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Duplicate faction definition id '" + definition.Id + "'.");
            }
            _factions.Add(definition.Id, definition);
        }

        /// <summary>Replaces the combat rules. Only allowed before the catalog is frozen at <see cref="Simulation.Start"/>.</summary>
        /// <param name="rules">The rules for this match; not null.</param>
        public void SetCombatRules(CombatRules rules)
        {
            RequireNotFrozen();
            Combat = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>Makes the catalog read-only. Internal: called by <see cref="Simulation.Start"/>.</summary>
        internal void Freeze()
        {
            IsFrozen = true;
        }

        /// <summary>Throws when the catalog is frozen, so no definition changes after a match starts.</summary>
        private void RequireNotFrozen()
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException("The content catalog is frozen once a match has started.");
            }
        }

        /// <summary>
        /// FNV-1a digest of every definition that affects simulation (IDs, layers, stats, costs, armour, sight, weapons, HQ and economy rules, the combat rules, mechanic IDs, HQ unit IDs and unit
        /// lists), in ordinal ID order. Display names are left out. Two catalogs with the same digest simulate the same.
        /// </summary>
        public ulong ComputeDigest()
        {
            // Hasher that accumulates the content digest for this call. Range: fresh per call.
            StateHasher hasher = new StateHasher();
            hasher.Write(_units.Count);
            foreach (UnitDefinition unit in _units.Values)
            {
                hasher.Write(unit.Id);
                hasher.Write(unit.FactionId);
                hasher.Write((byte)unit.Layer);
                hasher.Write(unit.MaxHealth);
                hasher.Write(unit.SpeedTilesPerSecond);
                hasher.Write(unit.Supply);
                hasher.Write(unit.Radius);
                hasher.Write(unit.CostCommon);
                hasher.Write(unit.CostRare);
                hasher.Write(unit.ArmourType);
                hasher.Write(unit.SightRangeTiles);
                hasher.Write(unit.Weapon != null);
                if (unit.Weapon != null)
                {
                    unit.Weapon.WriteHash(hasher);
                }
                hasher.Write(unit.Hq != null);
                if (unit.Hq != null)
                {
                    unit.Hq.WriteHash(hasher);
                }
                unit.Economy.WriteHash(hasher);
            }
            hasher.Write(_factions.Count);
            foreach (FactionDefinition faction in _factions.Values)
            {
                hasher.Write(faction.Id);
                hasher.Write(faction.MechanicId);
                hasher.Write(faction.HqUnitId);
                hasher.Write(faction.UnitIds.Count);
                foreach (string unitId in faction.UnitIds)
                {
                    hasher.Write(unitId);
                }
            }
            Combat.WriteHash(hasher);
            return hasher.Hash;
        }

        /// <summary>Checks every faction's unit list names a known unit and every unit's armour and damage types exist in the combat rules. Returns the first problem or null.</summary>
        public string? Validate()
        {
            // Combat problem found, if any (unknown armour or damage type).
            string? combatProblem = Combat.Validate(_units.Values);
            if (combatProblem != null)
            {
                return combatProblem;
            }
            foreach (FactionDefinition faction in _factions.Values)
            {
                foreach (string unitId in faction.UnitIds)
                {
                    if (!_units.ContainsKey(unitId))
                    {
                        return "Faction '" + faction.Id + "' lists unknown unit '" + unitId + "'.";
                    }
                }
                if (faction.HqUnitId != null && (!_units.TryGetValue(faction.HqUnitId, out UnitDefinition? hq) || hq.Hq == null))
                {
                    return "Faction '" + faction.Id + "' names '" + faction.HqUnitId + "' as its HQ unit, which is unknown or has no HQ rules.";
                }
            }
            foreach (UnitDefinition unit in _units.Values)
            {
                foreach (string trained in unit.Economy.Trains)
                {
                    if (!_units.ContainsKey(trained))
                    {
                        return "Unit '" + unit.Id + "' trains unknown unit '" + trained + "'.";
                    }
                }
                if (unit.Hq != null && !_units.ContainsKey(unit.Hq.WorkerUnitId))
                {
                    return "Unit '" + unit.Id + "' has HQ worker '" + unit.Hq.WorkerUnitId + "', which is unknown.";
                }
            }
            return null;
        }
    }
}
