// rts/tests/Sim.Tests/Combat/CombatRig.cs
// Shared fixture of the combat-mechanics tests (rts/docs/specs/combat_mechanics.md section 0): a 24 x 24 flat open map, two players on
// different teams, both factions with mechanic "none" (the first optionally carries one test damage modifier), separation and sidestep off, the
// shipped damage table (and splash fraction), and a queue-then-step API. Units are spawned by script commands on tick 0 in call order, so the
// first unit has ID 1, the second ID 2 and so on. Part of Rts.Sim.Tests. Test code only.
using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Sim;
using Rts.Sim.Combat;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Tests.Data;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Combat
{
    /// <summary>A small deterministic combat arena: queue spawns, orders and hooks, then step and read the events.</summary>
    public sealed class CombatRig
    {
        /// <summary>Owner of the attackers. Unit: player ID. Range: 1.</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Owner of the victims. Unit: player ID. Range: 2; on a different team from <see cref="P1"/>.</summary>
        public static readonly PlayerId P2 = new PlayerId(2);

        /// <summary>The started simulation.</summary>
        public Simulation Sim { get; }

        /// <summary>Every event of every step so far, in order.</summary>
        public List<SimEvent> Log { get; } = new List<SimEvent>();

        /// <summary>Next command sequence number. Unit: counter. Range: 0 upward.</summary>
        private uint _sequence;

        /// <summary>Next unit ID a queued spawn will receive. Unit: entity ID number. Range: 1 upward.</summary>
        private uint _nextId = 1;

        /// <summary>Test command that runs a delegate inside the step (step 2), so a test can poke internal state at an exact tick.</summary>
        private sealed class HookCommand : Command
        {
            /// <summary>The code to run.</summary>
            private readonly Action<Simulation> _code;

            /// <summary>Stores the schedule and the code.</summary>
            public HookCommand(int tick, uint sequence, Action<Simulation> code) : base(tick, PlayerId.None, sequence)
            {
                _code = code;
            }

            /// <summary>Kind name: "test_hook".</summary>
            public override string Kind => "test_hook";

            /// <summary>Runs the code.</summary>
            public override CommandResult Apply(Simulation sim)
            {
                _code(sim);
                return CommandResult.Ok;
            }

            /// <summary>Hashes the kind only.</summary>
            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
            }
        }

        /// <summary>Damage modifier answering one fixed factor for every hit of the first player's units.</summary>
        private sealed class FixedModifier : IDamageModifier
        {
            /// <summary>Multiplier answered. Unit: fraction. Range: 0 or more.</summary>
            private readonly Fix64 _factor;

            /// <summary>Stores the factor.</summary>
            public FixedModifier(Fix64 factor)
            {
                _factor = factor;
            }

            /// <summary>Stable ID.</summary>
            public string Id => "a_fixed";

            /// <summary>The factor.</summary>
            public Fix64 OutgoingDamageMultiplier(Simulation sim, Unit attacker)
            {
                return _factor;
            }
        }

        /// <summary>Mechanic that registers one <see cref="FixedModifier"/>.</summary>
        private sealed class FixedModifierMechanic : IFactionMechanic
        {
            /// <summary>The modifier to register.</summary>
            private readonly FixedModifier _modifier;

            /// <summary>Stores the modifier.</summary>
            public FixedModifierMechanic(FixedModifier modifier)
            {
                _modifier = modifier;
            }

            /// <summary>Stable mechanic ID.</summary>
            public string Id => "test_fixed_modifier";

            /// <summary>Registers the modifier.</summary>
            public void OnMatchStart(Simulation sim)
            {
                sim.RegisterDamageModifier(_modifier);
            }

            /// <summary>Nothing per tick.</summary>
            public void OnTick(Simulation sim)
            {
            }

            /// <summary>No state.</summary>
            public void WriteHash(StateHasher hasher)
            {
            }
        }

        /// <summary>
        /// Starts an arena. <paramref name="catalog"/> must hold the units to spawn (start from <see cref="NewCatalog"/>).
        /// </summary>
        /// <param name="catalog">Content, with the shipped combat rules already set.</param>
        /// <param name="modifierFactor">When set, the first player's faction registers a damage modifier answering this factor. Unit: fraction.</param>
        /// <param name="tuning">Tuning override, or null for <see cref="ArenaTuning"/>.</param>
        /// <param name="map">Map override, or null for the 24 x 24 flat map.</param>
        public CombatRig(ContentCatalog catalog, Fix64? modifierFactor = null, Tuning? tuning = null, MapDefinition? map = null)
        {
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            // Mechanic ID for the first player's faction: "none" or the test modifier.
            string mechanic = FactionDefinition.NoMechanic;
            if (modifierFactor.HasValue)
            {
                FixedModifier modifier = new FixedModifier(modifierFactor.Value);
                registry.Register("test_fixed_modifier", () => new FixedModifierMechanic(modifier));
                mechanic = "test_fixed_modifier";
            }
            catalog.AddFaction(new FactionDefinition("arena_a", "Arena A", mechanic, new List<string> { TestWorld.RunnerId }));
            catalog.AddFaction(new FactionDefinition("arena_b", "Arena B", FactionDefinition.NoMechanic, new List<string> { TestWorld.RunnerId }));
            List<PlayerSetup> players = new List<PlayerSetup>
            {
                new PlayerSetup(P1, new TeamId(1), "arena_a", "A"),
                new PlayerSetup(P2, new TeamId(2), "arena_b", "B"),
            };
            MapDefinition arena = map ?? new MapDefinition("arena", "Arena", 24, 24, new List<FixVec2> { FixVec2.FromInts(2, 2), FixVec2.FromInts(22, 22) });
            Sim = new Simulation(new SimulationConfig(new MatchSettings(1, TestWorld.TicksPerSecond, 1), catalog, players, registry,
                tuning ?? ArenaTuning(), arena));
            Sim.Start();
        }

        /// <summary>The shipped tuning with separation, sidestep and crowd arrival switched off so units stay where they are spawned.</summary>
        public static Tuning ArenaTuning()
        {
            // The real data files, edited below.
            List<DataSourceFile> files = TestData.ReadAll();
            // Index of the terrain tuning file in the list.
            int at = files.FindIndex(f => f.Path == "tuning/terrain.json");
            // Text of the terrain file with the three settings zeroed.
            string text = files[at].Text
                .Replace("\"separation_strength\": 0.5", "\"separation_strength\": 0")
                .Replace("\"sidestep_strength\": 0.5", "\"sidestep_strength\": 0")
                .Replace("\"crowd_arrival_radius_tiles\": 3", "\"crowd_arrival_radius_tiles\": 0");
            files[at] = new DataSourceFile(files[at].Path, text);
            return DataLoader.Load(files).Tuning;
        }

        /// <summary>A catalog with TestWorld's units and the shipped damage table and splash fraction.</summary>
        public static ContentCatalog NewCatalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.SetCombatRules(TestWorld.Tuning.Combat.CreateRules());
            return catalog;
        }

        /// <summary>Adds a hand-built unit definition (radius 0.5, speed 2, no cost, sight 8) to a catalog.</summary>
        /// <param name="catalog">Catalog to add to.</param>
        /// <param name="id">Definition ID.</param>
        /// <param name="layer">Movement layer.</param>
        /// <param name="health">Maximum health. Unit: health points. Range: above 0.</param>
        /// <param name="armour">Armour type name: "light", "armoured" or "structure".</param>
        /// <param name="weapon">Weapon, or null.</param>
        /// <param name="footprint">Footprint in tiles; above 0 makes it a structure. Unit: tiles. Range: 0 or more.</param>
        /// <param name="dome">Dome, or null.</param>
        public static void AddUnit(ContentCatalog catalog, string id, MovementLayer layer, int health, string armour, WeaponDefinition? weapon = null,
            int footprint = 0, ShieldDomeDefinition? dome = null)
        {
            // Economy part: a structure when the footprint is above 0.
            UnitEconomyDefinition? economy = footprint > 0
                ? new UnitEconomyDefinition(Fix64.FromInt(5), false, false, new List<string>(), false, footprint, 0)
                : null;
            // Airborne sight is required for flyers.
            Fix64? airSight = layer == MovementLayer.Air ? Fix64.FromInt(4) : (Fix64?)null;
            catalog.AddUnit(new UnitDefinition(id, id, TestWorld.TestFactionId, layer, Fix64.FromInt(health), footprint > 0 ? Fix64.Zero : Fix64.FromInt(2), 1,
                Fix64.Half, 0, 0, armour, Fix64.FromInt(8), weapon, null, economy, airSight, null, dome));
        }

        /// <summary>Queues a spawn on tick 0 and returns the ID the unit will get.</summary>
        /// <param name="definition">Definition ID.</param>
        /// <param name="owner">Owner.</param>
        /// <param name="x">X in tiles (decimal text).</param>
        /// <param name="y">Y in tiles (decimal text).</param>
        public EntityId Add(string definition, PlayerId owner, string x, string y)
        {
            Sim.Submit(new SpawnUnitCommand(0, PlayerId.None, _sequence++, definition, owner, new FixVec2(Fix64.Parse(x), Fix64.Parse(y))));
            return new EntityId(_nextId++);
        }

        /// <summary>Queues a forced attack_unit order on tick 0 (after the spawns queued so far).</summary>
        public void Attack(EntityId attacker, EntityId target)
        {
            Sim.Submit(new AttackUnitCommand(0, PlayerId.None, _sequence++, attacker, target));
        }

        /// <summary>Queues a remove_unit command (issuer 255, the script issuer) for <paramref name="tick"/>.</summary>
        public void Remove(int tick, EntityId unit)
        {
            Sim.Submit(new RemoveUnitCommand(tick, PlayerId.None, _sequence++, unit));
        }

        /// <summary>Queues code to run inside step 2 of <paramref name="tick"/>, after the commands queued before it.</summary>
        public void At(int tick, Action<Simulation> code)
        {
            Sim.Submit(new HookCommand(tick, _sequence++, code));
        }

        /// <summary>Steps <paramref name="ticks"/> ticks, appending each tick's events to <see cref="Log"/>.</summary>
        public void Step(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                Sim.Step();
                Log.AddRange(Sim.Events.Current);
            }
        }

        /// <summary>The live unit with this ID.</summary>
        public Unit Get(EntityId id)
        {
            return Sim.Units.TryGet(id, out Unit? unit) ? unit : throw new InvalidOperationException("No unit " + id);
        }

        /// <summary>Health of a unit. Unit: health points.</summary>
        public Fix64 Health(EntityId id)
        {
            return Get(id).Health;
        }

        /// <summary>All logged events of type <typeparamref name="T"/>, in order.</summary>
        public List<T> Of<T>() where T : SimEvent
        {
            return Log.OfType<T>().ToList();
        }

        /// <summary>Fix64 from decimal text.</summary>
        public static Fix64 F(string text)
        {
            return Fix64.Parse(text);
        }

        /// <summary>
        /// A weapon like the spec's weapon B: blast damage, range 7, cooldown 10 s (so it fires once in a short test), ground and water.
        /// </summary>
        /// <param name="damage">Damage per hit. Unit: health points.</param>
        /// <param name="delay">Hit delay. Unit: ticks. Range: 0 to 10.</param>
        /// <param name="splash">Blast radius. Unit: tiles. Range: 0 to 4.</param>
        /// <param name="air">True to let the weapon target air too.</param>
        /// <param name="drench">True to apply drench.</param>
        public static WeaponDefinition Weapon(int damage, int delay, string splash, bool air = false, bool drench = false)
        {
            return new WeaponDefinition(Fix64.FromInt(damage), "blast", Fix64.FromInt(7), Fix64.FromInt(10), true, true, air, drench, delay, Fix64.Parse(splash));
        }
    }
}
