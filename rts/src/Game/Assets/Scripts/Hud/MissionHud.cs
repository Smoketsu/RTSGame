// rts/src/Game/Assets/Scripts/Hud/MissionHud.cs
// What a campaign mission shows (player_interface_3.md UI-16 section 5), added to the HUD only when the host runs a mission: the
// objective panel top left (mission name, elapsed time against max_seconds, objectives in file order: Locked hidden, Active plain,
// Completed ticked, Failed red), up to three mission messages top centre (newest on top), pulsing ground rings under highlighted units,
// rings for highlighted areas, a steady ring for the hint area of each Active objective, and camera_focus cues, which centre the camera
// once when they arrive. Cues expire in game time: tick + SecondsToTicks(seconds), so pausing pauses them. Reads host.Mission and
// host.MissionCues only; it never calls the simulation or the runner. Part of Rts.Game (presentation only).
using System.Collections.Generic;
using System.Text;
using Rts.Campaign;
using Rts.Game.Bootstrap;
using Rts.Game.CameraRig;
using Rts.Game.Presentation;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Units;
using UnityEngine;
using UnityEngine.UI;
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Hud
{
    /// <summary>Objective panel, mission messages, highlight rings and camera cues of a running mission.</summary>
    [DisallowMultipleComponent]
    public sealed class MissionHud : MonoBehaviour
    {
        /// <summary>Most mission messages shown at once.</summary>
        private const int MaxMessages = 3;

        /// <summary>Objective panel width. Unit: reference pixels (1920 x 1080 canvas).</summary>
        private const float PanelWidth = 360f;

        /// <summary>Height of the rings above the ground. Unit: world units.</summary>
        private const float Lift = 0.08f;

        /// <summary>Ring line width. Unit: world units.</summary>
        private const float RingWidth = 0.12f;

        /// <summary>Radius of the ring under a highlighted unit. Unit: tiles.</summary>
        private const float UnitRingRadius = 0.9f;

        /// <summary>Pulse rate of unit and area highlight rings. Unit: cycles per second (real time).</summary>
        private const float PulseHz = 1.5f;

        /// <summary>Highlight colour (units and areas). Unit: RGBA 0 to 1.</summary>
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        /// <summary>Hint ring colour. Unit: RGBA 0 to 1.</summary>
        private static readonly Color HintColor = new Color(0.4f, 0.9f, 1f, 0.9f);

        /// <summary>The objective panel. Range: non-null after Awake.</summary>
        private Image _panel;

        /// <summary>Objective panel text (title, time, objectives; rich text). Range: non-null after Awake.</summary>
        private Text _objectives;

        /// <summary>The mission message box. Range: non-null after Awake.</summary>
        private Image _messageBox;

        /// <summary>Mission message text. Range: non-null after Awake.</summary>
        private Text _messages;

        /// <summary>Ground rings for highlights and hints. Range: non-null after Awake.</summary>
        private LinePool _rings;

        /// <summary>Cues already looked at for camera_focus. Unit: cues. Range: 0 to MissionCues.Count.</summary>
        private int _cuesSeen;

        /// <summary>The host. Range: null until found.</summary>
        private SimulationHost _host;

        /// <summary>The camera controller. Range: null until found.</summary>
        private RtsCameraController _camera;

        /// <summary>Builds the panel and the message box, hidden.</summary>
        private void Awake()
        {
            _panel = HudBuild.Image(transform, "Mission Objectives", new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(PanelWidth, 60f), HudBuild.PanelColor);
            _objectives = HudBuild.Text(_panel.transform, "Objectives Text", new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(PanelWidth - 20f, 400f), 17, TextAnchor.UpperLeft);
            _objectives.supportRichText = true;
            _messageBox = HudBuild.Image(transform, "Mission Messages", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(720f, 40f), HudBuild.PanelColor);
            _messages = HudBuild.Text(_messageBox.transform, "Messages Text", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(700f, 300f), 20, TextAnchor.UpperCenter);
            _rings = new LinePool("Mission Ring");
            _panel.gameObject.SetActive(false);
            _messageBox.gameObject.SetActive(false);
        }

        /// <summary>Releases the ring lines.</summary>
        private void OnDestroy()
        {
            _rings?.Dispose();
        }

        /// <summary>Refreshes the panel, the messages and camera cues.</summary>
        private void Update()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<SimulationHost>();
            }
            MissionRunner mission = _host != null ? _host.Mission : null;
            Simulation sim = _host != null ? _host.Match : null;
            _panel.gameObject.SetActive(mission != null && sim != null);
            if (mission == null || sim == null)
            {
                _messageBox.gameObject.SetActive(false);
                return;
            }
            // The tick about to run; cues and objectives are dated in these ticks. Range: 0 upward.
            int tick = sim.Clock.Tick;
            int ticksPerSecond = sim.Clock.TicksPerSecond;
            IReadOnlyList<MissionCue> cues = _host.MissionCues;

            // camera_focus: pan once when the cue arrives (open question 2: at once). The move reaches the mission as a camera signal.
            for (; _cuesSeen < cues.Count; _cuesSeen++)
            {
                MissionCue cue = cues[_cuesSeen];
                if (cue.Kind == CueKind.CameraFocus)
                {
                    if (_camera == null)
                    {
                        _camera = FindAnyObjectByType<RtsCameraController>();
                    }
                    if (_camera != null)
                    {
                        _camera.CentreOn(cue.X.ToFloatForPresentation(), cue.Y.ToFloatForPresentation());
                    }
                }
            }

            StringBuilder text = new StringBuilder();
            text.Append("<b>").Append(mission.Mission.DisplayName).Append("</b>\n");
            text.Append(PlaybackBar.Clock(tick, ticksPerSecond)).Append(" / ").Append(PlaybackBar.Clock(mission.Mission.MaxSeconds * ticksPerSecond, ticksPerSecond)).Append('\n');
            // Lines in the panel, for its height. Range: 2 upward.
            int lines = 2;
            foreach (ObjectiveState objective in mission.Objectives)
            {
                switch (objective.Status)
                {
                    case ObjectiveStatus.Active:
                        text.Append("• ").Append(objective.Definition.Text).Append('\n');
                        break;
                    case ObjectiveStatus.Completed:
                        text.Append("<color=#7CE07C>✓ ").Append(objective.Definition.Text).Append("</color>\n");
                        break;
                    case ObjectiveStatus.Failed:
                        text.Append("<color=#FF5050>✗ ").Append(objective.Definition.Text).Append("</color>\n");
                        break;
                    default:
                        continue;
                }
                lines++;
            }
            _objectives.text = text.ToString().TrimEnd('\n');
            _panel.rectTransform.sizeDelta = new Vector2(PanelWidth, 16f + lines * 22f);

            // Messages still showing, newest on top.
            StringBuilder messages = new StringBuilder();
            // Messages shown. Range: 0 to MaxMessages.
            int shown = 0;
            for (int i = cues.Count - 1; i >= 0 && shown < MaxMessages; i--)
            {
                if (cues[i].Kind == CueKind.ShowMessage && tick < ExpiryTick(sim, cues[i]))
                {
                    messages.Append(shown > 0 ? "\n" : "").Append(cues[i].Text);
                    shown++;
                }
            }
            _messageBox.gameObject.SetActive(shown > 0);
            if (shown > 0)
            {
                _messages.text = messages.ToString();
                _messageBox.rectTransform.sizeDelta = new Vector2(720f, 16f + _messages.preferredHeight);
            }
        }

        /// <summary>Draws the highlight and hint rings after the cubes have moved.</summary>
        private void LateUpdate()
        {
            _rings.Begin();
            MissionRunner mission = _host != null ? _host.Mission : null;
            Simulation sim = _host != null ? _host.Match : null;
            if (mission != null && sim != null)
            {
                int tick = sim.Clock.Tick;
                // Pulse factor of the highlight rings. Unit: fraction. Range: 0.85 to 1.15.
                float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * PulseHz * Mathf.PI * 2f);
                foreach (MissionCue cue in _host.MissionCues)
                {
                    if (tick >= ExpiryTick(sim, cue))
                    {
                        continue;
                    }
                    if (cue.Kind == CueKind.HighlightUnits)
                    {
                        foreach (EntityId id in cue.Units)
                        {
                            // Dead or unknown units are skipped; the ring follows the blended cube, at the unit's ground height.
                            if (_host.TryGetViewTransform(id, out Transform view) && sim.Units.TryGet(id, out Unit unit))
                            {
                                Vector3 centre = new Vector3(view.position.x, Ground(sim, sim.Terrain.TileOf(unit.Position)), view.position.z);
                                _rings.Circle(centre, UnitRingRadius * pulse, HighlightColor, RingWidth);
                            }
                        }
                    }
                    else if (cue.Kind == CueKind.HighlightArea)
                    {
                        _rings.Circle(GroundPoint(sim, new FixVec2(cue.X, cue.Y)), cue.RadiusTiles.ToFloatForPresentation() * pulse, HighlightColor, RingWidth);
                    }
                }
                foreach (ObjectiveState objective in mission.Objectives)
                {
                    HintArea hint = objective.Definition.Hint;
                    if (objective.Status == ObjectiveStatus.Active && hint != null)
                    {
                        _rings.Circle(GroundPoint(sim, hint.Centre), hint.RadiusTiles.ToFloatForPresentation(), HintColor, RingWidth);
                    }
                }
            }
            _rings.End();
        }

        /// <summary>The tick at which a cue stops showing: its tick plus its seconds in ticks (game time). Unit: ticks.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="cue">A cue.</param>
        private static int ExpiryTick(Simulation sim, MissionCue cue)
        {
            return cue.Tick + sim.Clock.SecondsToTicks(cue.Seconds);
        }

        /// <summary>World point on the ground (plus the ring lift) at a simulation point.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="point">Point in tiles.</param>
        private Vector3 GroundPoint(Simulation sim, FixVec2 point)
        {
            return new Vector3(point.X.ToFloatForPresentation(), Ground(sim, sim.Terrain.TileOf(point)), point.Y.ToFloatForPresentation());
        }

        /// <summary>Ring height over a tile: its elevation layer times the host's layer height, plus the lift. Unit: world units.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="tile">Tile index (TileOf clamps to the map). Range: 0 to tile count - 1.</param>
        private float Ground(Simulation sim, int tile)
        {
            return sim.Terrain[tile].Height * _host.LayerHeightWorldUnits + Lift;
        }
    }
}
