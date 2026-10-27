// rts/tests/Sim.Tests/Statuses/StatusTestWorld.cs
// Fixtures shared by the status and Momentum tests: a simulation builder with custom tuning, map, players and extra
// mechanics, a test drencher unit, a generic "run this code inside a step" command, a scripted drench-hit mechanic and
// small helpers to step to a tick and to read events. Part of Rts.Sim.Tests. Test code only.
using System;
using System.Collections.Generic;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Statuses;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Sim.Tests.Statuses
{
    /// <summary>Builders and helpers for tests of statuses and Momentum.</summary>
    public static class StatusTestWorld
    {
        /// <summary>ID of the test drencher: ground, speed 2, radius 0.5; weapon damage 1, range 5, cooldown 0.05 s (1 tick), hits every layer, applies drench.</summary>
        public const string DrencherId = "test.drencher";

        /// <summary>ID of an immobile, unarmed test unit (speed 0), which Momentum must not track.</summary>
        public const string StatueId = "test.statue";

        /// <summary>ID of the test faction whose mechanic is the scripted drench mechanic.</summary>
        public const string DriverFactionId = "driver";

        /// <summary>ID of the scripted drench mechanic (registered by <see cref="Build"/> when asked).</summary>
        public const string ScriptedMechanicId = "test_scripted_drench";

        /// <summary>ID of the water-like faction (mechanic "flooding") used to prove Momentum ignores other factions.</summary>
        public const string WaterFactionId = "water";

        /// <summary>Player 0 (team 0).</summary>
        public static readonly PlayerId P0 = new PlayerId(0);

        /// <summary>Player 1 (team 1).</summary>
        public static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Adds the test units and factions on top of <see cref="TestWorld.Catalog"/>.</summary>
        public static ContentCatalog Catalog()
        {
            ContentCatalog catalog = TestWorld.Catalog();
            catalog.AddUnit(new UnitDefinition(DrencherId, "Drencher", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.FromInt(2), 1, Fix64.Half, 50, 0, "light", Fix64.FromInt(8),
                new WeaponDefinition(Fix64.One, "kinetic", Fix64.FromInt(5), Fix64.Parse("0.05"), true, true, true, true)));
            catalog.AddUnit(new UnitDefinition(StatueId, "Statue", TestWorld.TestFactionId, MovementLayer.Ground,
                Fix64.FromInt(100), Fix64.Zero, 1, Fix64.Half, 50, 0));
            catalog.AddFaction(new FactionDefinition(DriverFactionId, "Driver", ScriptedMechanicId, new List<string> { TestWorld.RunnerId }));
            catalog.AddFaction(new FactionDefinition(WaterFactionId, "Water", "flooding", new List<string> { TestWorld.RunnerId }));
            return catalog;
        }

        /// <summary>
        /// A started simulation. Defaults: the shipped tuning, the flat 64 by 64 map, 20 ticks per second, hash interval 10,
        /// P0 = test faction and P1 = Skai. <paramref name="scripted"/> registers the scripted drench mechanic.
        /// </summary>
        /// <param name="tuning">Tuning to use, or null for the shipped tuning.</param>
        /// <param name="map">Map to use, or null for the flat map.</param>
        /// <param name="players">Players, or null for <see cref="TestWorld.TwoPlayers"/>.</param>
        /// <param name="scripted">Scripted drench mechanic to register, or null for none.</param>
        /// <param name="ticksPerSecond">Tick rate. Unit: ticks per second. Range: 10 to 60.</param>
        public static Simulation Build(Tuning? tuning = null, MapDefinition? map = null, List<PlayerSetup>? players = null,
            ScriptedDrenchMechanic? scripted = null, int ticksPerSecond = TestWorld.TicksPerSecond)
        {
            FactionMechanicRegistry registry = BuiltInMechanics.CreateRegistry();
            if (scripted != null)
            {
                registry.Register(ScriptedMechanicId, () => scripted);
            }
            SimulationConfig config = new SimulationConfig(
                new MatchSettings(1234, ticksPerSecond, TestWorld.HashInterval),
                Catalog(),
                players ?? TestWorld.TwoPlayers(),
                registry,
                tuning ?? TestWorld.Tuning,
                map ?? TestWorld.FlatMap());
            Simulation sim = new Simulation(config);
            sim.Start();
            return sim;
        }

        /// <summary>Steps until the tick <paramref name="lastTick"/> has been simulated (the next tick to run is lastTick + 1).</summary>
        public static void StepThrough(Simulation sim, int lastTick)
        {
            while (sim.Clock.Tick <= lastTick)
            {
                sim.Step();
            }
        }

        /// <summary>The live unit with this ID; fails with an exception when it is gone.</summary>
        public static Unit UnitOf(Simulation sim, uint id)
        {
            return sim.Units.TryGet(new EntityId(id), out Unit? unit) ? unit : throw new InvalidOperationException("No unit " + id);
        }

        /// <summary>Events of type <typeparamref name="T"/> among the current tick's events, in order.</summary>
        public static List<T> EventsOf<T>(Simulation sim) where T : SimEvent
        {
            // Matches found so far.
            List<T> found = new List<T>();
            foreach (SimEvent evt in sim.Events.Current)
            {
                if (evt is T match)
                {
                    found.Add(match);
                }
            }
            return found;
        }

        /// <summary>Submits a script-issued spawn at tick 0 or later.</summary>
        public static void Spawn(Simulation sim, int tick, uint sequence, string definition, PlayerId owner, FixVec2 position)
        {
            sim.Submit(new SpawnUnitCommand(tick, PlayerId.None, sequence, definition, owner, position));
        }

        /// <summary>Submits a script-issued move order.</summary>
        public static void Move(Simulation sim, int tick, uint sequence, uint unit, FixVec2 goal)
        {
            sim.Submit(new MoveUnitCommand(tick, PlayerId.None, sequence, new EntityId(unit), goal));
        }

        /// <summary>Submits a script-issued apply_status.</summary>
        public static void Apply(Simulation sim, int tick, uint sequence, uint unit, StatusKind kind, string multiplier, string seconds, string key = "script")
        {
            sim.Submit(new ApplyStatusCommand(tick, PlayerId.None, sequence, new EntityId(unit), kind, Fix64.Parse(multiplier), Fix64.Parse(seconds), key));
        }

        /// <summary>Submits code to run inside the step at <paramref name="tick"/> (as a script-issued test command).</summary>
        public static void Run(Simulation sim, int tick, uint sequence, Action<Simulation> code)
        {
            sim.Submit(new ActionCommand(tick, sequence, code));
        }

        /// <summary>Point at tile centre (x + 0.5, y + 0.5), in tiles.</summary>
        public static FixVec2 Centre(int x, int y)
        {
            return new FixVec2(Fix64.FromInt(x) + Fix64.Half, Fix64.FromInt(y) + Fix64.Half);
        }

        /// <summary>Test-only command that runs a delegate inside the step, standing in for ability code that calls the status system.</summary>
        private sealed class ActionCommand : Command
        {
            /// <summary>The code to run.</summary>
            private readonly Action<Simulation> _code;

            public ActionCommand(int tick, uint sequence, Action<Simulation> code) : base(tick, PlayerId.None, sequence)
            {
                _code = code;
            }

            public override string Kind => "test_action";

            public override CommandResult Apply(Simulation sim)
            {
                _code(sim);
                return CommandResult.Ok;
            }

            public override void WriteHash(StateHasher hasher)
            {
                hasher.Write(Kind);
            }
        }
    }

    /// <summary>A test mechanic that fires drench hits on chosen ticks, standing in for a Spoutling that hits at exact moments (it runs in the mechanics step, after combat, like a weapon would).</summary>
    public sealed class ScriptedDrenchMechanic : IFactionMechanic
    {
        /// <summary>One scheduled hit.</summary>
        private readonly List<(int Tick, uint Attacker, uint Target)> _hits = new List<(int, uint, uint)>();

        /// <summary>Schedules a drench hit.</summary>
        /// <param name="tick">Tick the hit lands on. Unit: ticks.</param>
        /// <param name="attacker">Attacker unit ID.</param>
        /// <param name="target">Target unit ID.</param>
        public ScriptedDrenchMechanic Hit(int tick, uint attacker, uint target)
        {
            _hits.Add((tick, attacker, target));
            return this;
        }

        /// <summary>Mechanic ID.</summary>
        public string Id => StatusTestWorld.ScriptedMechanicId;

        /// <summary>Nothing to set up.</summary>
        public void OnMatchStart(Simulation sim)
        {
        }

        /// <summary>Applies the hits scheduled for this tick, in the order scheduled.</summary>
        public void OnTick(Simulation sim)
        {
            foreach ((int tick, uint attackerId, uint targetId) in _hits)
            {
                if (tick == sim.Clock.Tick && sim.Units.TryGet(new EntityId(attackerId), out Unit? attacker) && sim.Units.TryGet(new EntityId(targetId), out Unit? target))
                {
                    StatusSystem.ApplyDrenchHit(sim, attacker, target);
                }
            }
        }

        /// <summary>Holds no state beyond the schedule, which is fixed by the test.</summary>
        public void WriteHash(StateHasher hasher)
        {
            hasher.Write(Id);
        }
    }
}
