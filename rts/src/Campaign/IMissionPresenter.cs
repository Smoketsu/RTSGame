// rts/src/Campaign/IMissionPresenter.cs
// The cue side of the mission runner: messages, highlights and camera suggestions it asks the host to present
// (rts/docs/specs/campaign_scripting.md section 4). Part of Rts.Campaign. Cues are output only; nothing a presenter does reaches the
// simulation or the runner. The headless tool presents nothing (cues go to the log); Unity shows them on the HUD.
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Campaign
{
    /// <summary>What a cue asks the presenter to do. Range: the four cue actions of the spec.</summary>
    public enum CueKind
    {
        /// <summary>Show <see cref="MissionCue.Text"/> for <see cref="MissionCue.Seconds"/>.</summary>
        ShowMessage,

        /// <summary>Mark a circle for <see cref="MissionCue.Seconds"/>.</summary>
        HighlightArea,

        /// <summary>Mark units for <see cref="MissionCue.Seconds"/>.</summary>
        HighlightUnits,

        /// <summary>The host may pan the camera to the point.</summary>
        CameraFocus,
    }

    /// <summary>One cue. Fields that do not belong to the cue's <see cref="Kind"/> keep their defaults. Immutable.</summary>
    public sealed class MissionCue
    {
        /// <summary>What the cue asks for.</summary>
        public CueKind Kind { get; }

        /// <summary>The simulation tick that was next to be stepped when the cue was issued. Unit: ticks. Range: 0 upward.</summary>
        public int Tick { get; }

        /// <summary>Message text (ShowMessage). Range: 1 to 300 characters; empty for other kinds.</summary>
        public string Text { get; }

        /// <summary>How long the cue stays. Unit: seconds. Range: 1 to 60; zero for CameraFocus.</summary>
        public Fix64 Seconds { get; }

        /// <summary>Centre x (HighlightArea, CameraFocus). Unit: tiles. Range: inside the map.</summary>
        public Fix64 X { get; }

        /// <summary>Centre y (HighlightArea, CameraFocus). Unit: tiles. Range: inside the map.</summary>
        public Fix64 Y { get; }

        /// <summary>Radius (HighlightArea). Unit: tiles. Range: 0.5 to 64; zero for other kinds.</summary>
        public Fix64 RadiusTiles { get; }

        /// <summary>Entity IDs (HighlightUnits). Range: 1 to 64 IDs; empty for other kinds.</summary>
        public IReadOnlyList<EntityId> Units { get; }

        /// <summary>Builds the cue from a mission action.</summary>
        /// <param name="tick">Tick the cue was issued at. Unit: ticks.</param>
        /// <param name="action">The cue action; its kind must be one of the four cue kinds.</param>
        internal MissionCue(int tick, MissionAction action)
        {
            Tick = tick;
            Text = action.Text;
            Seconds = action.Seconds;
            X = action.X;
            Y = action.Y;
            RadiusTiles = action.RadiusTiles;
            Units = action.Units;
            switch (action.Kind)
            {
                case ActionKind.ShowMessage: Kind = CueKind.ShowMessage; break;
                case ActionKind.HighlightArea: Kind = CueKind.HighlightArea; break;
                case ActionKind.HighlightUnits: Kind = CueKind.HighlightUnits; break;
                default: Kind = CueKind.CameraFocus; break;
            }
        }
    }

    /// <summary>Receives the cues of a running mission. Called between steps, from the thread that steps the simulation.</summary>
    public interface IMissionPresenter
    {
        /// <summary>Presents one cue. Must not call the simulation's mutation methods or the runner.</summary>
        /// <param name="cue">The cue.</param>
        void Show(MissionCue cue);
    }
}
