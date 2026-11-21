// rts/src/Game/Assets/Scripts/Presentation/CoreView.cs
// The Core on the ground (player_interface.md UI-9): a ring at sim.Map.Core (Centre, RadiusTiles) in the holder's colour (grey when
// nobody holds it), a capture arc inside it filling CaptureProgressTicks / CaptureTicks in the capturing team's colour, and a pulse
// of the ring while IsContested. Colours are each team's first player's colour (SimulationHost.ColorOf). Shown only while the match
// has cycles (the Core belongs to Planetary Cycles) and the map has a Core. Lives on the SimulationHost object, added by
// AutoBootstrap. Placeholder for the Core model and art. Part of Rts.Game (presentation only).
using Rts.Game.Bootstrap;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Cycles;
using Rts.Sim.Data;
using UnityEngine;

namespace Rts.Game.Presentation
{
    /// <summary>Draws the Core ring, capture arc and contested pulse.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimulationHost))]
    public sealed class CoreView : MonoBehaviour
    {
        /// <summary>Ring colour while nobody holds the Core. Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color NeutralRing = new Color(0.8f, 0.8f, 0.8f, 0.8f);

        /// <summary>Ring width. Unit: world units.</summary>
        private const float RingWidth = 0.1f;

        /// <summary>Capture arc width. Unit: world units.</summary>
        private const float ArcWidth = 0.18f;

        /// <summary>Pulses per second of a contested ring. Unit: hertz.</summary>
        private const float PulseHertz = 2f;

        /// <summary>Height of the ring above the ground. Unit: world units.</summary>
        private const float Lift = 0.12f;

        /// <summary>Lines drawn this frame. Presentation only.</summary>
        private readonly LinePool _pool = new LinePool("Core Line");

        /// <summary>The host on the same object. Range: non-null after Awake.</summary>
        private SimulationHost _host;

        /// <summary>Finds the host.</summary>
        private void Awake()
        {
            _host = GetComponent<SimulationHost>();
        }

        /// <summary>Destroys the lines.</summary>
        private void OnDestroy()
        {
            _pool.Dispose();
        }

        /// <summary>Draws the ring and arc from the cycle system's Core state.</summary>
        private void LateUpdate()
        {
            _pool.Begin();
            Simulation sim = _host.Match;
            CoreArea core = sim != null ? sim.Map.Core : null;
            if (core != null && sim.Cycles.IsEnabled)
            {
                CycleSystem cycles = sim.Cycles;
                int tile = sim.Terrain.TileOf(core.Centre);
                Vector3 centre = new Vector3(core.Centre.X.ToFloatForPresentation(), sim.Terrain[tile].Height * _host.LayerHeightWorldUnits + Lift, core.Centre.Y.ToFloatForPresentation());
                float radius = core.RadiusTiles.ToFloatForPresentation();
                Color ring = cycles.Holder.IsValid ? TeamColor(sim, cycles.Holder) : NeutralRing;
                float width = RingWidth;
                if (cycles.IsContested)
                {
                    // Pulse: 0 to 1 and back, PulseHertz times a second. Unit: fraction.
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHertz * Mathf.PI * 2f);
                    width = RingWidth * (1f + 1.5f * pulse);
                    ring = Color.Lerp(ring, Color.white, 0.6f * pulse);
                }
                _pool.Circle(centre, radius, ring, width);
                if (cycles.CaptureTeam.IsValid && cycles.CaptureTicks > 0 && cycles.CaptureProgressTicks > 0)
                {
                    _pool.Arc(centre, radius * 0.85f, (float)cycles.CaptureProgressTicks / cycles.CaptureTicks, TeamColor(sim, cycles.CaptureTeam), ArcWidth);
                }
            }
            _pool.End();
        }

        /// <summary>A team's colour: its first player's colour.</summary>
        /// <param name="sim">The running match.</param>
        /// <param name="team">A real team.</param>
        public static Color TeamColor(Simulation sim, TeamId team)
        {
            var members = sim.Players.PlayersOnTeam(team);
            return members.Count > 0 ? SimulationHost.ColorOf(members[0]) : NeutralRing;
        }
    }
}
