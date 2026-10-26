// rts/src/Sim/Data/Tuning/Tuning.cs
// All tuning files in one object: one typed class per file in rts/data/tuning. Part of Rts.Sim.Data.
// DataLoader builds it once at load time; simulation systems read the part they own (Rule 3: no tuning number in code).
using System;

namespace Rts.Sim.Data
{
    /// <summary>The tuning files of rts/data/tuning, parsed and validated. Immutable.</summary>
    public sealed class Tuning
    {
        /// <summary>tuning/match.json: tick rate, hash interval, match length, tides, Planetary Cycles and the Core.</summary>
        public MatchTuning Match { get; }

        /// <summary>tuning/hq.json: the mobile HQ (root, uproot, survivability floor, Last Stand, FFA reveal).</summary>
        public HqTuning Hq { get; }

        /// <summary>tuning/momentum.json: the Skai Riders Momentum mechanic.</summary>
        public MomentumTuning Momentum { get; }

        /// <summary>tuning/statuses.json: shared statuses (Grounded) and the Spoutling drench rule.</summary>
        public StatusTuning Status { get; }

        /// <summary>tuning/flood.json: Water faction flood limits and depth speed multipliers.</summary>
        public FloodTuning Flood { get; }

        /// <summary>tuning/supply.json: supply cap and static defence limits.</summary>
        public SupplyTuning Supply { get; }

        /// <summary>tuning/earth.json: Earth Tailoring prototype (Recasting, Attunement).</summary>
        public EarthTuning Earth { get; }

        /// <summary>tuning/online.json: pauses, disconnects and the ranked unlock threshold.</summary>
        public OnlineTuning Online { get; }

        /// <summary>tuning/harness.json: balance harness run sizes and hard limits.</summary>
        public HarnessTuning Harness { get; }

        /// <summary>tuning/terrain.json: per-layer speed multipliers, depth thresholds and pathfinder limits.</summary>
        public TerrainTuning Terrain { get; }

        /// <summary>tuning/combat.json: damage types, armour types, the damage multiplier table and the attack-move acquire bonus.</summary>
        public CombatTuning Combat { get; }

        /// <summary>Bundles the parsed files; none may be null.</summary>
        public Tuning(MatchTuning match, HqTuning hq, MomentumTuning momentum, StatusTuning status, FloodTuning flood,
            SupplyTuning supply, EarthTuning earth, OnlineTuning online, HarnessTuning harness, TerrainTuning terrain, CombatTuning combat)
        {
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Match = match ?? throw new ArgumentNullException(nameof(match));
            Hq = hq ?? throw new ArgumentNullException(nameof(hq));
            Momentum = momentum ?? throw new ArgumentNullException(nameof(momentum));
            Status = status ?? throw new ArgumentNullException(nameof(status));
            Flood = flood ?? throw new ArgumentNullException(nameof(flood));
            Supply = supply ?? throw new ArgumentNullException(nameof(supply));
            Earth = earth ?? throw new ArgumentNullException(nameof(earth));
            Online = online ?? throw new ArgumentNullException(nameof(online));
            Harness = harness ?? throw new ArgumentNullException(nameof(harness));
        }
    }
}
