// rts/src/Game/Assets/Scripts/Hud/AbilityTargeting.cs
// Point targeting for the two cast abilities (player_interface.md UI-5): Downdraft (grounding_ability) and the water wave
// (flood.can_cast_wave). Begin(kind) is called by the command card's ability buttons; while active the mode holds PointerCapture,
// a left click on the map casts at the clicked point (quantised to integer cells, as SelectionController does), and Esc or a right
// click cancels. Downdraft draws each selected caster's range circle and the radius circle at the cursor; the wave draws an aim
// line from the caster that would cast to the cursor (shortcut: a line only; switch to FloodingMechanic.PreviewWave, interface
// support I2, for the strip of tiles once it is on main). Caster choice: Downdraft, the nearest ready caster with the target in
// range, else the nearest caster; the wave, the nearest ready caster, else the nearest (so the simulation's reason is shown). Also
// draws a short pulse where a Downdraft lands, a short line along a cast wave, and a small ring above every grounded unit (the
// Grounded marker; read from Unit.Statuses each frame, which is what StatusAppliedEvent and StatusExpiredEvent report). Presentation only: commands are the only output.
using System.Collections.Generic;
using Rts.Game.Bootstrap;
using Rts.Game.Controls;
using Rts.Game.UnitSelection;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Water;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Hud
{
    /// <summary>The cast abilities the HUD can target.</summary>
    public enum AbilityKind
    {
        /// <summary>Downdraft: grounds enemy flyers in a radius (UnitDefinition.GroundingAbility).</summary>
        Grounding,

        /// <summary>The water wave: floods a strip and pushes enemies (UnitDefinition.Flood.CanCastWave).</summary>
        Wave,
    }

    /// <summary>Targeting circles and lines for Downdraft and the wave, and the cast commands.</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityTargeting : MonoBehaviour
    {
        /// <summary>Colour of range circles and the aim line. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color rangeColor = new Color(0.55f, 0.85f, 1f, 0.8f);

        /// <summary>Colour of the effect radius at the cursor and of cast pulses. Unit: RGBA, each channel 0 to 1.</summary>
        [SerializeField] private Color effectColor = new Color(1f, 0.85f, 0.3f, 0.9f);

        /// <summary>Points per circle. Unit: line vertices.</summary>
        private const int CircleSegments = 48;

        /// <summary>Height of circles and lines above the terrain. Unit: world units.</summary>
        private const float Lift = 0.08f;

        /// <summary>How long a cast pulse or wave line stays. Unit: seconds (real time).</summary>
        private const float EffectSeconds = 0.8f;

        /// <summary>Height of the Grounded marker above the terrain. Unit: world units.</summary>
        private const float GroundedMarkerHeight = 1.6f;

        /// <summary>Radius of the Grounded marker. Unit: world units.</summary>
        private const float GroundedMarkerRadius = 0.35f;

        /// <summary>Length of the line drawn along a cast wave. Unit: tiles.</summary>
        private const float WaveLineTiles = 6f;

        /// <summary>The ability being targeted, or null while the mode is off.</summary>
        private AbilityKind? _kind;

        /// <summary>Lines in use this frame and kept between frames; index 0 onward. Range: 0 to the most lines seen.</summary>
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();

        /// <summary>Selected own casters of the targeted ability, ascending EntityId. Presentation scratch list.</summary>
        private readonly List<Unit> _casters = new List<Unit>();

        /// <summary>Pulses and wave lines still showing: world centre, end point (wave) or radius (pulse in x), kind and end time.</summary>
        private readonly List<(Vector3 From, Vector3 To, bool IsPulse, float Radius, float Until)> _effects = new List<(Vector3, Vector3, bool, float, float)>();

        /// <summary>The local command path. Range: null until found.</summary>
        private LocalCommands _commands;

        /// <summary>The selection source (selected units, ground picking). Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>True once subscribed to the host's events. Unit: on/off.</summary>
        private bool _subscribed;

        /// <summary>Number of lines used this frame. Range: 0 upward.</summary>
        private int _used;

        /// <summary>True while an ability is being targeted. Unit: on/off.</summary>
        public bool IsActive => _kind.HasValue;

        /// <summary>Starts targeting an ability; replaces any targeting in progress.</summary>
        /// <param name="kind">The ability.</param>
        public void Begin(AbilityKind kind)
        {
            _kind = kind;
            PointerCapture.Capture(this);
        }

        /// <summary>Stops targeting.</summary>
        public void Cancel()
        {
            _kind = null;
            PointerCapture.Release(this);
        }

        /// <summary>True when units of this definition have the ability.</summary>
        /// <param name="definition">A unit definition.</param>
        /// <param name="kind">The ability.</param>
        public static bool Has(UnitDefinition definition, AbilityKind kind)
        {
            return kind == AbilityKind.Grounding ? definition.GroundingAbility != null : definition.Flood.CanCastWave;
        }

        /// <summary>Ticks until the caster may use the ability again; 0 when ready. Unit: ticks. Range: 0 upward.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="caster">A unit with the ability.</param>
        /// <param name="kind">The ability.</param>
        public static int CooldownTicks(Simulation sim, Unit caster, AbilityKind kind)
        {
            if (kind == AbilityKind.Grounding)
            {
                return Mathf.Max(0, caster.GroundingReadyTick - sim.Clock.Tick);
            }
            return Flooding(sim) != null && Flooding(sim).TryGetNextWaveTick(caster.Id, out int next) ? Mathf.Max(0, next - sim.Clock.Tick) : 0;
        }

        /// <summary>The flooding mechanic when the match runs it, else null.</summary>
        /// <param name="sim">The running match.</param>
        private static FloodingMechanic Flooding(Simulation sim)
        {
            return sim.Mechanics.TryGetValue(FloodingMechanic.MechanicId, out IFactionMechanic mechanic) ? mechanic as FloodingMechanic : null;
        }

        /// <summary>Releases the pointer, unsubscribes and destroys the lines.</summary>
        private void OnDestroy()
        {
            PointerCapture.Release(this);
            if (_subscribed && _commands != null && _commands.Host != null)
            {
                _commands.Host.EventRaised -= OnSimEvent;
            }
            foreach (LineRenderer line in _lines)
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }
        }

        /// <summary>Draws effects, and while targeting: circles or the aim line, the cast on a left click, cancel on Esc or a right click.</summary>
        private void Update()
        {
            if (_commands == null)
            {
                _commands = FindAnyObjectByType<LocalCommands>();
            }
            if (_selection == null)
            {
                _selection = FindAnyObjectByType<SelectionController>();
            }
            _used = 0;
            if (_commands != null && _selection != null && _commands.IsReady)
            {
                if (!_subscribed)
                {
                    _commands.Host.EventRaised += OnSimEvent;
                    _subscribed = true;
                }
                DrawEffects();
                DrawGroundedMarkers(_commands.Host.Match);
                if (IsActive)
                {
                    UpdateTargeting(_commands.Host.Match);
                }
            }
            for (int i = _used; i < _lines.Count; i++)
            {
                _lines[i].gameObject.SetActive(false);
            }
        }

        /// <summary>One frame of targeting: collect casters, draw, and cast or cancel.</summary>
        /// <param name="sim">The running match.</param>
        private void UpdateTargeting(Simulation sim)
        {
            AbilityKind kind = _kind.Value;
            _casters.Clear();
            foreach (EntityId id in _selection.Selected)
            {
                if (sim.Units.TryGet(id, out Unit unit) && unit.Owner == _commands.LocalPlayer && Has(unit.Definition, kind))
                {
                    _casters.Add(unit);
                }
            }
            _casters.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (_casters.Count == 0 || GameInput.GetKeyDown(KeyCode.Escape) || GameInput.GetMouseButtonDown(1))
            {
                Cancel();
                return;
            }
            if (kind == AbilityKind.Grounding)
            {
                foreach (Unit caster in _casters)
                {
                    DrawCircle(ToWorld(caster.Position), caster.Definition.GroundingAbility.RangeTiles.ToFloatForPresentation(), rangeColor);
                }
            }
            Camera camera = Camera.main;
            // Ground point under the cursor; only meaningful when picked.
            Vector3 point = Vector3.zero;
            if (camera == null || !_selection.TryPickGround(camera.ScreenPointToRay(GameInput.mousePosition), out point))
            {
                return;
            }
            FixVec2 target = _selection.ToSimPoint(point);
            Unit chosen = ChooseCaster(sim, kind, target);
            if (kind == AbilityKind.Grounding)
            {
                DrawCircle(point, chosen.Definition.GroundingAbility.RadiusTiles.ToFloatForPresentation(), effectColor);
            }
            else
            {
                DrawLine(ToWorld(chosen.Position), point, effectColor);
            }
            // True while the cursor is over a HUD panel; a click there belongs to the HUD.
            bool overHud = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (GameInput.GetMouseButtonDown(0) && !overHud)
            {
                Command cast = kind == AbilityKind.Grounding
                    ? new CastGroundingCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), chosen.Id, target)
                    : (Command)new CastWaveCommand(_commands.Tick, _commands.LocalPlayer, _commands.NextSequence(), chosen.Id, target);
                _commands.Submit(cast);
                Cancel();
            }
        }

        /// <summary>
        /// The caster that casts at the target: the nearest ready one (with the target in range, for Downdraft), else the nearest; distances are
        /// squared fixed-point distances to the target, ties lowest EntityId.
        /// </summary>
        /// <param name="sim">The running match.</param>
        /// <param name="kind">The ability.</param>
        /// <param name="target">Target point in tiles.</param>
        private Unit ChooseCaster(Simulation sim, AbilityKind kind, FixVec2 target)
        {
            Unit nearestReady = null;
            Fix64 readyDistance = Fix64.Zero;
            Unit nearest = null;
            Fix64 nearestDistance = Fix64.Zero;
            foreach (Unit caster in _casters)
            {
                Fix64 dx = caster.Position.X - target.X;
                Fix64 dy = caster.Position.Y - target.Y;
                Fix64 distance = dx * dx + dy * dy;
                // _casters is in ascending ID order, so strict comparisons keep the lowest ID on a tie.
                if (nearest == null || distance < nearestDistance)
                {
                    nearest = caster;
                    nearestDistance = distance;
                }
                bool ready = CooldownTicks(sim, caster, kind) == 0;
                if (ready && kind == AbilityKind.Grounding)
                {
                    Fix64 range = caster.Definition.GroundingAbility.RangeTiles;
                    ready = distance <= range * range;
                }
                if (ready && (nearestReady == null || distance < readyDistance))
                {
                    nearestReady = caster;
                    readyDistance = distance;
                }
            }
            return nearestReady ?? nearest;
        }

        /// <summary>Starts a pulse where a Downdraft lands and a line along a cast wave (casts of every player; fog of war will filter them, UI-6).</summary>
        /// <param name="simEvent">An event of the tick that just ran.</param>
        private void OnSimEvent(SimEvent simEvent)
        {
            Simulation sim = _commands.Host.Match;
            if (simEvent is GroundingCastEvent grounding)
            {
                // Radius of the caster's ability when it is still alive; one tile otherwise. Unit: tiles.
                float radius = sim.Units.TryGet(grounding.Caster, out Unit caster) && caster.Definition.GroundingAbility != null
                    ? caster.Definition.GroundingAbility.RadiusTiles.ToFloatForPresentation() : 1f;
                Vector3 centre = ToWorld(grounding.Target);
                _effects.Add((centre, centre, true, radius, Time.unscaledTime + EffectSeconds));
            }
            else if (simEvent is WaveCastEvent wave && sim.Units.TryGet(wave.Caster, out Unit waveCaster))
            {
                Vector3 from = ToWorld(waveCaster.Position);
                Vector3 direction = new Vector3(wave.DirX, 0f, wave.DirY).normalized;
                _effects.Add((from, from + direction * WaveLineTiles, false, 0f, Time.unscaledTime + EffectSeconds));
            }
        }

        /// <summary>Draws live pulses (growing to their radius) and wave lines; drops finished ones.</summary>
        private void DrawEffects()
        {
            // Real time now. Unit: seconds.
            float now = Time.unscaledTime;
            _effects.RemoveAll(effect => effect.Until <= now);
            foreach ((Vector3 from, Vector3 to, bool isPulse, float radius, float until) in _effects)
            {
                // Fraction of the effect's life gone. Unit: fraction. Range: 0 to 1.
                float age = 1f - (until - now) / EffectSeconds;
                if (isPulse)
                {
                    DrawCircle(from, Mathf.Max(0.1f, radius * age), effectColor);
                }
                else
                {
                    DrawLine(from, Vector3.Lerp(from, to, age), effectColor);
                }
            }
        }

        /// <summary>Draws a small ring above each grounded unit (all players; fog of war will filter enemies, UI-6).</summary>
        /// <param name="sim">The running match.</param>
        private void DrawGroundedMarkers(Simulation sim)
        {
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Statuses.IsGrounded)
                {
                    DrawCircle(ToWorld(unit.Position) + Vector3.up * GroundedMarkerHeight, GroundedMarkerRadius, effectColor);
                }
            }
        }

        /// <summary>World point on the terrain surface for a simulation position.</summary>
        /// <param name="position">Position in tiles.</param>
        private Vector3 ToWorld(FixVec2 position)
        {
            Simulation sim = _commands.Host.Match;
            // Elevation layer under the position. Unit: layers.
            int layer = sim.Terrain[sim.Terrain.TileOf(position)].Height;
            return new Vector3(position.X.ToFloatForPresentation(), layer * _commands.Host.LayerHeightWorldUnits, position.Y.ToFloatForPresentation());
        }

        /// <summary>Draws a horizontal circle.</summary>
        /// <param name="centre">World centre (its height is used for the whole circle).</param>
        /// <param name="radius">Radius. Unit: tiles (world units).</param>
        /// <param name="color">Line colour, RGBA 0 to 1.</param>
        private void DrawCircle(Vector3 centre, float radius, Color color)
        {
            LineRenderer line = NextLine(color, CircleSegments, true);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(angle) * radius, Lift, Mathf.Sin(angle) * radius));
            }
        }

        /// <summary>Draws a straight line between two world points, lifted above the terrain.</summary>
        /// <param name="from">Start point.</param>
        /// <param name="to">End point.</param>
        /// <param name="color">Line colour, RGBA 0 to 1.</param>
        private void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            LineRenderer line = NextLine(color, 2, false);
            line.SetPosition(0, from + Vector3.up * Lift);
            line.SetPosition(1, to + Vector3.up * Lift);
        }

        /// <summary>The next free line renderer, created when the pool is too small, set up for this use.</summary>
        /// <param name="color">Line colour.</param>
        /// <param name="points">Vertex count.</param>
        /// <param name="loop">True to close the line into a loop.</param>
        private LineRenderer NextLine(Color color, int points, bool loop)
        {
            if (_used == _lines.Count)
            {
                GameObject lineObject = new GameObject("Ability Line " + _used);
                LineRenderer created = lineObject.AddComponent<LineRenderer>();
                // Sprites/Default is always included in builds and draws the vertex colour unlit.
                created.material = new Material(Shader.Find("Sprites/Default"));
                created.widthMultiplier = 0.08f;
                created.useWorldSpace = true;
                _lines.Add(created);
            }
            LineRenderer line = _lines[_used++];
            line.gameObject.SetActive(true);
            line.positionCount = points;
            line.loop = loop;
            line.startColor = color;
            line.endColor = color;
            return line;
        }
    }
}
