// rts/tools/BalanceHarness/MechanicDetectors.cs
// The registry of mechanic detectors (spec: rts/docs/specs/balance_measurement.md M.5.3): for each mechanic ID, which simulation event counts as one use and by whom, and when the mechanic is applicable to a
// faction (the faction has a definition with the capability). SummaryObserver feeds every event of every step through the detectors; the balance report reads the counts. Adding a detector is one entry in
// <see cref="All"/>. A detector reads an event and, through <see cref="IDetectorContext"/>, the owner and definition of the units the event names; it never changes anything. Tool-side only. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Factions.Skai;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>What a detector may ask about the world while it reads one event.</summary>
    public interface IDetectorContext
    {
        /// <summary>Owner of a unit, or <see cref="PlayerId.None"/> when the unit is unknown or neutral.</summary>
        /// <param name="unit">Entity ID.</param>
        PlayerId OwnerOf(EntityId unit);

        /// <summary>Definition of a unit, or null when it is unknown.</summary>
        /// <param name="unit">Entity ID.</param>
        UnitDefinition? DefinitionOf(EntityId unit);

        /// <summary>The unit's effective movement layer at this step, or null when the unit is gone or unknown.</summary>
        /// <param name="unit">Entity ID.</param>
        MovementLayer? LayerOf(EntityId unit);

        /// <summary>Every player of a team, in ascending player order.</summary>
        /// <param name="team">Team ID.</param>
        IReadOnlyList<PlayerId> PlayersOfTeam(TeamId team);
    }

    /// <summary>One mechanic detector: an ID, the rule for when it applies to a faction and the rule that turns an event into uses.</summary>
    public sealed class MechanicDetector
    {
        /// <summary>Mechanic ID, as in the balance report and the summary's "mechanics" map. Range: lower-case letters and underscores.</summary>
        public string Id { get; }

        /// <summary>One-line description of what is detected, for the report.</summary>
        public string What { get; }

        /// <summary>Applicability: faction ID, content and whether the map has a Core; true when the faction can use the mechanic.</summary>
        public Func<ContentCatalog, string, bool, bool> Applicable { get; }

        /// <summary>Detection: the event and the context; adds one entry to the list for each use (the player credited).</summary>
        public Action<SimEvent, IDetectorContext, List<PlayerId>> Detect { get; }

        /// <summary>Opponent condition (spec ai_mechanic_use 7.3): when set, a slot-game counts only if the other slot trained or completed a unit of this layer; null for no condition.</summary>
        public MovementLayer? OpponentLayer { get; }

        /// <summary>Bundles the values.</summary>
        public MechanicDetector(string id, string what, Func<ContentCatalog, string, bool, bool> applicable, Action<SimEvent, IDetectorContext, List<PlayerId>> detect, MovementLayer? opponentLayer = null)
        {
            OpponentLayer = opponentLayer;
            Id = id;
            What = what;
            Applicable = applicable;
            Detect = detect;
        }
    }

    /// <summary>The registry of detectors, the only place a mechanic is declared for the balance report.</summary>
    public static class MechanicDetectors
    {
        /// <summary>The detectors in report order.</summary>
        public static readonly IReadOnlyList<MechanicDetector> All = new List<MechanicDetector>
        {
            new MechanicDetector("downdraft", "GroundingCastEvent (the caster's owner)", (c, f, core) => AnyUnit(c, f, d => d.GroundingAbility != null),
                (e, x, o) => { if (e is GroundingCastEvent g) { Add(o, g.Owner); } }, MovementLayer.Air),
            new MechanicDetector("wave", "WaveCastEvent (the owner)", (c, f, core) => AnyUnit(c, f, d => d.Flood.CanCastWave),
                (e, x, o) => { if (e is WaveCastEvent w) { Add(o, w.Owner); } }),
            new MechanicDetector("drench", "DrenchHitEvent (the attacker's owner)", (c, f, core) => AnyUnit(c, f, d => d.Weapon != null && d.Weapon.AppliesDrench),
                (e, x, o) => { if (e is DrenchHitEvent d) { Add(o, x.OwnerOf(d.Attacker)); } }),
            new MechanicDetector("splash", "BlastEvent with victims (the attacker's owner)", (c, f, core) => AnyUnit(c, f, d => d.Weapon != null && d.Weapon.SplashRadiusTiles > Fix64.Zero),
                (e, x, o) => { if (e is BlastEvent b && b.Victims > 0) { Add(o, b.AttackerOwner); } }),
            new MechanicDetector("shield_dome", "ShieldDomeHitEvent that absorbed damage (the dome's owner)", (c, f, core) => AnyUnit(c, f, d => d.ShieldDome != null),
                (e, x, o) => { if (e is ShieldDomeHitEvent s && s.Absorbed > Fix64.Zero) { Add(o, x.OwnerOf(s.Dome)); } }),
            new MechanicDetector("momentum", "MomentumChangedEvent that raised the stacks (the unit's owner)",
                (c, f, core) => c.Factions.TryGetValue(f, out FactionDefinition? fd) && string.Equals(fd.MechanicId, MomentumMechanic.MechanicId, StringComparison.Ordinal),
                (e, x, o) => { if (e is MomentumChangedEvent m && m.Stacks > m.PreviousStacks) { Add(o, x.OwnerOf(m.Unit)); } }),
            new MechanicDetector("anti_air_on_water", "ShotFiredEvent of a CanTargetAirOnWater weapon without CanTargetAir at a target whose layer is Air", (c, f, core) => AnyUnit(c, f, d => d.Weapon != null && d.Weapon.CanTargetAirOnWater),
                (e, x, o) =>
                {
                    if (e is ShotFiredEvent s && x.DefinitionOf(s.Attacker)?.Weapon is WeaponDefinition w && w.CanTargetAirOnWater && !w.CanTargetAir && x.LayerOf(s.Target) == MovementLayer.Air)
                    {
                        Add(o, x.OwnerOf(s.Attacker));
                    }
                }),
            new MechanicDetector("flood_source", "StructureCompletedEvent of a flood source", (c, f, core) => AnyUnit(c, f, d => d.Flood.IsSource),
                (e, x, o) => Structure(e, x, o, d => d.Flood.IsSource)),
            new MechanicDetector("wall", "StructureCompletedEvent of a wall that is not a gate", (c, f, core) => AnyUnit(c, f, d => d.Economy.IsWall && !d.Economy.IsGate),
                (e, x, o) => Structure(e, x, o, d => d.Economy.IsWall && !d.Economy.IsGate)),
            new MechanicDetector("gate", "StructureCompletedEvent of a gate", (c, f, core) => AnyUnit(c, f, d => d.Economy.IsWall && d.Economy.IsGate),
                (e, x, o) => Structure(e, x, o, d => d.Economy.IsWall && d.Economy.IsGate)),
            new MechanicDetector("static_defence", "StructureCompletedEvent of a static defence", (c, f, core) => AnyUnit(c, f, d => d.Economy.IsStaticDefence),
                (e, x, o) => Structure(e, x, o, d => d.Economy.IsStaticDefence)),
            new MechanicDetector("expansion", "StructureCompletedEvent of a drop-off that is not an HQ", (c, f, core) => AnyUnit(c, f, d => d.Economy.IsDropOff && d.Hq == null),
                (e, x, o) => Structure(e, x, o, d => d.Economy.IsDropOff && d.Hq == null)),
            new MechanicDetector("rare", "ResourceDepositedEvent of the rare resource", (c, f, core) => true,
                (e, x, o) => { if (e is ResourceDepositedEvent r && r.Resource == ResourceKind.Rare) { Add(o, r.Owner); } }),
            new MechanicDetector("hq_move", "HqStateChangedEvent to Uprooting (the HQ's owner)", (c, f, core) => AnyUnit(c, f, d => d.Hq != null),
                (e, x, o) => { if (e is HqStateChangedEvent h && h.To == HqState.Uprooting) { Add(o, x.OwnerOf(h.Unit)); } }),
            new MechanicDetector("hq_last_stand", "HqLastStandEvent (the HQ's owner)", (c, f, core) => AnyUnit(c, f, d => d.Hq != null),
                (e, x, o) => { if (e is HqLastStandEvent h) { Add(o, x.OwnerOf(h.Unit)); } }),
            new MechanicDetector("core_capture", "CoreCaptureStartedEvent (every player of the team)", (c, f, core) => core,
                (e, x, o) => { if (e is CoreCaptureStartedEvent s) { o.AddRange(x.PlayersOfTeam(s.Team)); } }),
            new MechanicDetector("cycle_pick", "CyclePickedEvent by a player (not the script)", (c, f, core) => true,
                (e, x, o) => { if (e is CyclePickedEvent p) { Add(o, p.Player); } }),
        };

        /// <summary>The detector with ID <paramref name="id"/>; throws <see cref="HarnessException"/> when there is none.</summary>
        /// <param name="id">Mechanic ID.</param>
        public static MechanicDetector Require(string id)
        {
            foreach (MechanicDetector detector in All)
            {
                if (detector.Id == id)
                {
                    return detector;
                }
            }
            throw new HarnessException("unknown mechanic '" + id + "'");
        }

        /// <summary>True when the faction has a definition for which <paramref name="test"/> holds.</summary>
        private static bool AnyUnit(ContentCatalog content, string factionId, Func<UnitDefinition, bool> test)
        {
            foreach (UnitDefinition definition in content.Units.Values)
            {
                if (definition.FactionId == factionId && test(definition))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Credits <paramref name="player"/> when it is a real player (not <see cref="PlayerId.None"/>).</summary>
        private static void Add(List<PlayerId> owners, PlayerId player)
        {
            if (player.IsValid)
            {
                owners.Add(player);
            }
        }

        /// <summary>A StructureCompletedEvent whose structure's definition satisfies <paramref name="test"/> counts for the structure's owner.</summary>
        private static void Structure(SimEvent simEvent, IDetectorContext context, List<PlayerId> owners, Func<UnitDefinition, bool> test)
        {
            if (simEvent is StructureCompletedEvent done)
            {
                // The completed structure's definition (from its ID through the event, else the context).
                UnitDefinition? definition = context.DefinitionOf(done.Structure);
                if (definition != null && test(definition))
                {
                    Add(owners, context.OwnerOf(done.Structure));
                }
            }
        }
    }
}
