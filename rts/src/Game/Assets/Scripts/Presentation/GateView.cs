// rts/src/Game/Assets/Scripts/Presentation/GateView.cs
// Shows gates opening (player_interface.md UI-4, walls_and_tech.md G.5): a gate's cube is drawn low while any unit of the gate
// owner's team stands on its tile or one of the eight tiles around it, and full height otherwise. Purely visual: the simulation has
// no gate state (its own units pass through a gate tile by team passability). Lives on the SimulationHost object, added by
// AutoBootstrap. Shortcut: a placeholder squash of the cube; replace with the gate model's open animation when gate art exists.
// Part of Rts.Game (presentation only).
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Units;
using UnityEngine;
// Unity 6.6 added UnityEngine.EntityId; this alias makes every EntityId in this file the simulation's entity ID.
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Presentation
{
    /// <summary>Squashes gate cubes while a friendly unit is next to them.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulationHost))]
    public sealed class GateView : MonoBehaviour
    {
        /// <summary>Height of an open gate's cube as a fraction of its closed height. Unit: fraction. Range: 0.05 to 1; default 0.25.</summary>
        [SerializeField, Range(0.05f, 1f)] private float openHeightFraction = 0.25f;

        /// <summary>The host on the same object. Range: non-null after Awake.</summary>
        private SimulationHost _host;

        /// <summary>Closed (spawn) scale of each gate cube seen so far, by gate. Presentation state only.</summary>
        private readonly Dictionary<EntityId, Vector3> _closedScale = new Dictionary<EntityId, Vector3>();

        /// <summary>Finds the host.</summary>
        private void Awake()
        {
            _host = GetComponent<SimulationHost>();
        }

        /// <summary>Sets each gate cube's height from whether a unit of its owner's team is on or next to its tile. Runs after the host moved the cubes.</summary>
        private void LateUpdate()
        {
            Simulation sim = _host.Match;
            if (sim == null)
            {
                return;
            }
            foreach (Unit gate in sim.Units.All)
            {
                if (!gate.Definition.Economy.IsGate || !_host.TryGetViewTransform(gate.Id, out Transform cube))
                {
                    continue;
                }
                if (!_closedScale.TryGetValue(gate.Id, out Vector3 closed))
                {
                    closed = cube.localScale;
                    _closedScale.Add(gate.Id, closed);
                }
                bool open = FriendNearby(sim, gate);
                cube.localScale = open ? new Vector3(closed.x, closed.y * openHeightFraction, closed.z) : closed;
            }
        }

        /// <summary>True when a unit of the gate owner's team (not the gate itself) stands on the gate tile or one of its eight neighbours.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="gate">A gate structure.</param>
        private static bool FriendNearby(Simulation sim, Unit gate)
        {
            TeamId team = sim.Players.TeamOf(gate.Owner);
            int gateTile = sim.Terrain.TileOf(gate.Position);
            // Gate tile coordinates. Unit: tiles.
            int gx = gateTile % sim.Terrain.Width;
            int gy = gateTile / sim.Terrain.Width;
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Id == gate.Id || !unit.Owner.IsValid || unit.Definition.Economy.FootprintTiles > 0 || sim.Players.TeamOf(unit.Owner) != team)
                {
                    continue;
                }
                int tile = sim.Terrain.TileOf(unit.Position);
                if (Mathf.Abs(tile % sim.Terrain.Width - gx) <= 1 && Mathf.Abs(tile / sim.Terrain.Width - gy) <= 1)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
