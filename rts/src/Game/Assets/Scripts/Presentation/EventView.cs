// rts/src/Game/Assets/Scripts/Presentation/EventView.cs
// Draws combat, shield, status, Momentum and movement events in the world (player_interface.md UI-7), each only when the local
// team can see it (FogView, UI-6): a tracer for each ShotFiredEvent that reaches its target on LandTick (a short flash for melee);
// a health bar over a unit for a moment after UnitDamagedEvent; an expanding ring for UnitKilledEvent and BlastEvent; a bubble around
// every shield dome that is up, brighter for a moment after ShieldDomeHitEvent; a splash ring for DrenchHitEvent; a trail behind each
// unit with Momentum stacks, longer with more stacks; and a red cross at the goal of an own unit's MoveUnreachableEvent. Notices for
// tide, flood removal, quake paths and relocations go through the message line; snapping a relocated cube is done by the host.
// Placeholders for art effects, sound and damage numbers (out of scope). Lives on the SimulationHost object, added by AutoBootstrap.
// Part of Rts.Game (presentation only).
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Units;
using UnityEngine;
// Unity 6.6 added UnityEngine.EntityId; this alias makes every EntityId in this file the simulation's entity ID.
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Presentation
{
    /// <summary>World drawing of combat and movement events for the local team.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulationHost))]
    public sealed class EventView : MonoBehaviour
    {
        /// <summary>A shot in flight or a melee flash.</summary>
        private struct Tracer
        {
            /// <summary>Shooter. Range: may die before the shot lands.</summary>
            public EntityId Attacker;

            /// <summary>Target. Range: may die before the shot lands.</summary>
            public EntityId Target;

            /// <summary>Last known shooter position. Unit: world units.</summary>
            public Vector3 From;

            /// <summary>Last known target position. Unit: world units.</summary>
            public Vector3 To;

            /// <summary>Tick the shot was fired. Unit: ticks.</summary>
            public int FireTick;

            /// <summary>Tick the shot lands. Unit: ticks. Range: FireTick or later.</summary>
            public int LandTick;

            /// <summary>True for a melee strike (drawn as a short flash). Unit: on/off.</summary>
            public bool IsMelee;

            /// <summary>Real time a melee flash ends. Unit: seconds of Time.unscaledTime.</summary>
            public float MeleeUntil;
        }

        /// <summary>A ring that grows to its radius and fades.</summary>
        private struct Ring
        {
            /// <summary>Centre. Unit: world units.</summary>
            public Vector3 Centre;

            /// <summary>Final radius. Unit: tiles.</summary>
            public float Radius;

            /// <summary>Colour, RGBA 0 to 1.</summary>
            public Color Color;

            /// <summary>Real time the ring started and ends. Unit: seconds of Time.unscaledTime.</summary>
            public float Start;

            /// <summary>Real time the ring ends. Unit: seconds of Time.unscaledTime.</summary>
            public float End;
        }

        /// <summary>How long a health bar stays after damage. Unit: seconds (real time).</summary>
        private const float HealthBarSeconds = 1.5f;

        /// <summary>How long a death or blast ring lasts. Unit: seconds (real time).</summary>
        private const float RingSeconds = 0.6f;

        /// <summary>How long a melee flash or dome hit flash lasts. Unit: seconds (real time).</summary>
        private const float FlashSeconds = 0.2f;

        /// <summary>How long an unreachable-goal cross stays. Unit: seconds (real time).</summary>
        private const float CrossSeconds = 1.5f;

        /// <summary>Trail length per Momentum stack. Unit: tiles.</summary>
        private const float TrailPerStack = 0.35f;

        /// <summary>Lines drawn this frame. Presentation only.</summary>
        private readonly LinePool _pool = new LinePool("Event Line");

        /// <summary>Shots in flight and melee flashes.</summary>
        private readonly List<Tracer> _tracers = new List<Tracer>();

        /// <summary>Death, blast and drench rings.</summary>
        private readonly List<Ring> _rings = new List<Ring>();

        /// <summary>Real time each damaged unit's health bar disappears. Unit: seconds of Time.unscaledTime.</summary>
        private readonly Dictionary<EntityId, float> _healthBars = new Dictionary<EntityId, float>();

        /// <summary>Real time each dome's hit flash ends. Unit: seconds of Time.unscaledTime.</summary>
        private readonly Dictionary<EntityId, float> _domeHits = new Dictionary<EntityId, float>();

        /// <summary>Goals of own unreachable moves and the real time each cross ends. Unit: world units; seconds.</summary>
        private readonly List<(Vector3 Goal, float Until)> _crosses = new List<(Vector3, float)>();

        /// <summary>Each cube's position last frame, for the direction of Momentum trails. Unit: world units.</summary>
        private readonly Dictionary<EntityId, Vector3> _lastPositions = new Dictionary<EntityId, Vector3>();

        /// <summary>Each cube's last non-zero movement direction on the ground plane. Unit: unit vector.</summary>
        private readonly Dictionary<EntityId, Vector3> _headings = new Dictionary<EntityId, Vector3>();

        /// <summary>Scratch list of units whose health bar expired this frame.</summary>
        private readonly List<EntityId> _expired = new List<EntityId>();

        /// <summary>The host on the same object. Range: non-null after Awake.</summary>
        private SimulationHost _host;

        /// <summary>The local command path on the same object. Range: non-null after Awake.</summary>
        private LocalCommands _commands;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>Finds the host and the command path.</summary>
        private void Awake()
        {
            _host = GetComponent<SimulationHost>();
            _commands = GetComponent<LocalCommands>();
        }

        /// <summary>Unsubscribes and destroys the lines.</summary>
        private void OnDestroy()
        {
            if (_subscribed && _host != null)
            {
                _host.EventRaised -= OnSimEvent;
            }
            _pool.Dispose();
        }

        /// <summary>Draws every live effect after the host has positioned the cubes.</summary>
        private void LateUpdate()
        {
            _pool.Begin();
            if (_commands != null && _commands.IsReady)
            {
                if (!_subscribed)
                {
                    _host.EventRaised += OnSimEvent;
                    _subscribed = true;
                }
                Simulation sim = _host.Match;
                TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
                // Real time now. Unit: seconds.
                float now = Time.unscaledTime;
                DrawTracers(sim);
                DrawRings(now);
                DrawHealthBars(sim, team, now);
                DrawDomesAndTrails(sim, team, now);
                DrawCrosses(now);
            }
            _pool.End();
        }

        /// <summary>Records the events of the tick that just ran that the local team can see.</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            if (!_commands.IsReady)
            {
                return;
            }
            Simulation sim = _host.Match;
            TeamId team = sim.Players.TeamOf(_commands.LocalPlayer);
            float now = Time.unscaledTime;
            if (simEvent is ShotFiredEvent shot)
            {
                // A shot shows when either end is in sight.
                if (Visible(sim, team, shot.Attacker) || Visible(sim, team, shot.Target))
                {
                    _tracers.Add(new Tracer
                    {
                        Attacker = shot.Attacker, Target = shot.Target,
                        From = CubePosition(shot.Attacker), To = CubePosition(shot.Target),
                        FireTick = shot.Tick, LandTick = shot.LandTick, IsMelee = shot.IsMelee, MeleeUntil = now + FlashSeconds,
                    });
                }
            }
            else if (simEvent is UnitDamagedEvent damaged)
            {
                if (Visible(sim, team, damaged.Target))
                {
                    _healthBars[damaged.Target] = now + HealthBarSeconds;
                }
            }
            else if (simEvent is UnitKilledEvent killed)
            {
                // The unit is already gone, so its cube's last position (still known this frame) marks the spot.
                if (_host.TryGetViewTransform(killed.Victim, out Transform cube) && cube.GetComponent<Renderer>() is Renderer renderer && renderer.enabled)
                {
                    AddRing(cube.position, 0.8f, new Color(1f, 0.5f, 0.15f, 1f), now);
                }
            }
            else if (simEvent is BlastEvent blast)
            {
                if (FogView.CanSeePoint(sim, team, blast.Centre))
                {
                    AddRing(Ground(sim, blast.Centre), blast.RadiusTiles.ToFloatForPresentation(), new Color(1f, 0.6f, 0.2f, 1f), now);
                }
            }
            else if (simEvent is ShieldDomeHitEvent domeHit)
            {
                _domeHits[domeHit.Dome] = now + FlashSeconds;
            }
            else if (simEvent is DrenchHitEvent drench)
            {
                if (Visible(sim, team, drench.Target))
                {
                    AddRing(CubePosition(drench.Target), 0.5f, new Color(0.3f, 0.7f, 1f, 1f), now);
                }
            }
            else if (simEvent is UnitRemovedEvent removed)
            {
                // Forget per-unit presentation state of units that are gone.
                _lastPositions.Remove(removed.Unit);
                _headings.Remove(removed.Unit);
                _domeHits.Remove(removed.Unit);
            }
            else if (simEvent is MoveUnreachableEvent unreachable)
            {
                if (sim.Units.TryGet(unreachable.Unit, out Unit mover) && mover.Owner == _commands.LocalPlayer)
                {
                    _crosses.Add((Ground(sim, unreachable.Goal), now + CrossSeconds));
                }
            }
        }

        /// <summary>Draws each shot as a short segment travelling from shooter to target, landing on LandTick; melee as a brief flash.</summary>
        /// <param name="sim">The running match.</param>
        private void DrawTracers(Simulation sim)
        {
            // Render time in ticks: the picture is one tick behind the simulation, blended by the host's alpha. Unit: ticks.
            float renderTick = sim.Clock.Tick - 1 + _host.InterpolationAlpha;
            float now = Time.unscaledTime;
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                Tracer tracer = _tracers[i];
                if (_host.TryGetViewTransform(tracer.Attacker, out Transform from))
                {
                    tracer.From = from.position;
                }
                if (_host.TryGetViewTransform(tracer.Target, out Transform to))
                {
                    tracer.To = to.position;
                }
                _tracers[i] = tracer;
                if (tracer.IsMelee)
                {
                    if (now > tracer.MeleeUntil)
                    {
                        _tracers.RemoveAt(i);
                        continue;
                    }
                    _pool.Line(tracer.From, tracer.To, new Color(1f, 1f, 1f, 0.9f), 0.05f);
                    continue;
                }
                // Fraction of the flight done. Unit: fraction. Range: 0 to 1 while in flight.
                float flight = Mathf.Max(1, tracer.LandTick - tracer.FireTick);
                float progress = (renderTick - tracer.FireTick) / flight;
                if (progress > 1f)
                {
                    _tracers.RemoveAt(i);
                    continue;
                }
                progress = Mathf.Clamp01(progress);
                Vector3 head = Vector3.Lerp(tracer.From, tracer.To, progress);
                Vector3 tail = Vector3.Lerp(tracer.From, tracer.To, Mathf.Max(0f, progress - 0.3f));
                _pool.Line(tail, head, new Color(1f, 0.9f, 0.3f, 1f), 0.06f);
            }
        }

        /// <summary>Draws death, blast and drench rings growing to their radius; drops finished ones.</summary>
        /// <param name="now">Real time now. Unit: seconds.</param>
        private void DrawRings(float now)
        {
            _rings.RemoveAll(ring => ring.End <= now);
            foreach (Ring ring in _rings)
            {
                float age = (now - ring.Start) / (ring.End - ring.Start);
                _pool.Circle(ring.Centre, Mathf.Max(0.05f, ring.Radius * age), ring.Color, 0.07f);
            }
        }

        /// <summary>Draws a short health bar over each recently damaged unit still alive and visible.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local team.</param>
        /// <param name="now">Real time now. Unit: seconds.</param>
        private void DrawHealthBars(Simulation sim, TeamId team, float now)
        {
            Camera camera = Camera.main;
            _expired.Clear();
            foreach (KeyValuePair<EntityId, float> bar in _healthBars)
            {
                if (bar.Value <= now || !sim.Units.TryGet(bar.Key, out Unit unit) || !_host.TryGetViewTransform(bar.Key, out Transform cube))
                {
                    _expired.Add(bar.Key);
                    continue;
                }
                if (camera == null || !FogView.CanSee(sim, team, unit))
                {
                    continue;
                }
                float max = unit.Definition.MaxHealth.ToFloatForPresentation();
                float fraction = max > 0f ? Mathf.Clamp01(unit.Health.ToFloatForPresentation() / max) : 0f;
                // Bar width and its left end, centred above the cube along the camera's right axis. Unit: world units.
                float width = Mathf.Max(0.8f, cube.localScale.x);
                Vector3 right = camera.transform.right;
                Vector3 left = cube.position + Vector3.up * (cube.localScale.y * 0.5f + 0.35f) - right * (width * 0.5f);
                _pool.Line(left, left + right * width, new Color(0.1f, 0.1f, 0.1f, 0.9f), 0.12f);
                _pool.Line(left, left + right * (width * fraction), Color.Lerp(new Color(1f, 0.2f, 0.2f, 1f), new Color(0.3f, 1f, 0.35f, 1f), fraction), 0.09f);
            }
            foreach (EntityId id in _expired)
            {
                _healthBars.Remove(id);
            }
        }

        /// <summary>Draws a bubble around each visible shield dome that is up (brighter just after a hit) and a trail behind units with Momentum.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local team.</param>
        /// <param name="now">Real time now. Unit: seconds.</param>
        private void DrawDomesAndTrails(Simulation sim, TeamId team, float now)
        {
            MomentumMechanic momentum = sim.Mechanics.TryGetValue(MomentumMechanic.MechanicId, out IFactionMechanic mechanic) ? mechanic as MomentumMechanic : null;
            foreach (Unit unit in sim.Units.All)
            {
                if (!_host.TryGetViewTransform(unit.Id, out Transform cube))
                {
                    continue;
                }
                // Heading from the cube's movement since last frame, kept while the unit stands still.
                Vector3 position = cube.position;
                if (_lastPositions.TryGetValue(unit.Id, out Vector3 last))
                {
                    Vector3 step = new Vector3(position.x - last.x, 0f, position.z - last.z);
                    if (step.sqrMagnitude > 1e-6f)
                    {
                        _headings[unit.Id] = step.normalized;
                    }
                }
                _lastPositions[unit.Id] = position;
                if (!FogView.CanSee(sim, team, unit))
                {
                    continue;
                }
                if (unit.Definition.ShieldDome != null && !unit.ShieldDown)
                {
                    bool hit = _domeHits.TryGetValue(unit.Id, out float until) && until > now;
                    _pool.Circle(Ground(sim, unit.Position) + Vector3.up * 0.1f, unit.Definition.ShieldDome.RadiusTiles.ToFloatForPresentation(),
                        hit ? new Color(0.6f, 0.9f, 1f, 1f) : new Color(0.4f, 0.7f, 1f, 0.45f), hit ? 0.12f : 0.05f);
                }
                if (momentum != null && momentum.TryGetState(unit.Id, out MomentumState state) && state.Stacks > 0 && _headings.TryGetValue(unit.Id, out Vector3 heading))
                {
                    _pool.Line(position, position - heading * (TrailPerStack * state.Stacks), new Color(0.7f, 0.95f, 1f, 0.8f), 0.08f);
                }
            }
        }

        /// <summary>Draws a red cross at each recent unreachable goal of an own unit.</summary>
        /// <param name="now">Real time now. Unit: seconds.</param>
        private void DrawCrosses(float now)
        {
            _crosses.RemoveAll(cross => cross.Until <= now);
            Color red = new Color(1f, 0.25f, 0.2f, 1f);
            foreach ((Vector3 goal, float _) in _crosses)
            {
                Vector3 lift = Vector3.up * 0.1f;
                _pool.Line(goal + lift + new Vector3(-0.3f, 0f, -0.3f), goal + lift + new Vector3(0.3f, 0f, 0.3f), red, 0.07f);
                _pool.Line(goal + lift + new Vector3(-0.3f, 0f, 0.3f), goal + lift + new Vector3(0.3f, 0f, -0.3f), red, 0.07f);
            }
        }

        /// <summary>Adds a ring that grows to its radius over RingSeconds.</summary>
        /// <param name="centre">World centre.</param>
        /// <param name="radius">Final radius. Unit: tiles.</param>
        /// <param name="color">Colour.</param>
        /// <param name="now">Real time now. Unit: seconds.</param>
        private void AddRing(Vector3 centre, float radius, Color color, float now)
        {
            _rings.Add(new Ring { Centre = centre, Radius = radius, Color = color, Start = now, End = now + RingSeconds });
        }

        /// <summary>True when the unit lives and the local team may see it drawn.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">The local team.</param>
        /// <param name="id">A unit.</param>
        private static bool Visible(Simulation sim, TeamId team, EntityId id)
        {
            return sim.Units.TryGet(id, out Unit unit) && FogView.CanSee(sim, team, unit);
        }

        /// <summary>The unit's cube position, or the origin when it has no cube.</summary>
        /// <param name="id">A unit.</param>
        private Vector3 CubePosition(EntityId id)
        {
            return _host.TryGetViewTransform(id, out Transform cube) ? cube.position : Vector3.zero;
        }

        /// <summary>World point on the terrain surface for a simulation position.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="position">Position in tiles.</param>
        private Vector3 Ground(Simulation sim, FixVec2 position)
        {
            int layer = sim.Terrain[sim.Terrain.TileOf(position)].Height;
            return new Vector3(position.X.ToFloatForPresentation(), layer * _host.LayerHeightWorldUnits, position.Y.ToFloatForPresentation());
        }
    }
}
