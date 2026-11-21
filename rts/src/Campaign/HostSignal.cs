// rts/src/Campaign/HostSignal.cs
// Host signals: what the host (Unity, or the headless test inputs) reports about camera and selection to the mission runner
// (rts/docs/specs/campaign_scripting.md sections 1 and 4). Part of Rts.Campaign. Camera and selection are presentation: the simulation never sees
// them and a signal never reaches it; it can only change which commands the mission submits. Immutable, engine-free, Fix64 and integers only.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;

namespace Rts.Campaign
{
    /// <summary>Base of the signals a host reports with <see cref="MissionRunner.ReportSignal"/>.</summary>
    public abstract class HostSignal
    {
        /// <summary>Signal type name as written in an inputs file. Range: "camera_moved" or "selection_changed".</summary>
        public abstract string Kind { get; }
    }

    /// <summary>The camera now looks at a point of the map.</summary>
    public sealed class CameraMovedSignal : HostSignal
    {
        /// <summary>Camera focus on the ground, x. Unit: tiles. Range: any; the condition compares it with a circle.</summary>
        public Fix64 X { get; }

        /// <summary>Camera focus on the ground, y. Unit: tiles. Range: any.</summary>
        public Fix64 Y { get; }

        /// <summary>Builds the signal.</summary>
        /// <param name="x">Focus x. Unit: tiles.</param>
        /// <param name="y">Focus y. Unit: tiles.</param>
        public CameraMovedSignal(Fix64 x, Fix64 y)
        {
            X = x;
            Y = y;
        }

        /// <inheritdoc />
        public override string Kind => "camera_moved";
    }

    /// <summary>The player's selection changed to a set of units.</summary>
    public sealed class SelectionChangedSignal : HostSignal
    {
        /// <summary>Selected entity IDs, in the order the host lists them; resolved against live units when a condition reads them. Range: 0 to 500 IDs.</summary>
        public IReadOnlyList<EntityId> Units { get; }

        /// <summary>Builds the signal; the list is copied.</summary>
        /// <param name="units">Selected entity IDs.</param>
        public SelectionChangedSignal(IEnumerable<EntityId> units)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }
            Units = new List<EntityId>(units);
        }

        /// <inheritdoc />
        public override string Kind => "selection_changed";
    }
}
