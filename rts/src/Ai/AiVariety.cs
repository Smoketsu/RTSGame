// rts/src/Ai/AiVariety.cs
// The skirmish AI's variety stream (skirmish_ai_v4_rl.md package R): a second seeded generator, separate from the controller's own one
// (SkirmishAiController.Rng, whose draw sequence must not change), from which the AI draws its match-start variation (an opening of its
// faction plan, a jittered base composition and an expansion clock offset) and, each time the army starts massing, an offset of the
// predicted-advantage bar (package L, the opening library, is the first draw and the handover of the opening's values to the plan's).
// Part of Rts.Ai. It is created by SkirmishAiController.OnMatchStart only when the profile asks for variety (AiVariety.IsNeeded), is held by
// AiMemory.Variety, is not simulation state and is never hashed. Pure integer arithmetic (Fix64 for ratios), no clock, no System.Random: the
// same (match seed, player, profile) gives the same draws in the same order (R.2): opening, opening composition band, main composition band,
// expansion offset at creation, then (only when use_walls, rts/docs/specs/ai_mechanic_use.md section 6) the wall style, then (only when use_casts) the wave aim; then one advantage offset
// per DrawAttackAdvantageOffset call.
using System;
using System.Collections.Generic;
using Rts.Sim.Core;
using Rts.Sim.Teams;

namespace Rts.Ai
{
    /// <summary>Which enemies the AI's wave pushes (rule CW, ai_mechanic_use.md M2.3), drawn once per match.</summary>
    public enum WaveAim : byte
    {
        /// <summary>Only enemy units whose weapon range is at most wave_melee_range_tiles count.</summary>
        Melee = 0,

        /// <summary>Every enemy ground or water army unit with a weapon counts.</summary>
        All = 1,
    }

    /// <summary>How rule WL walls the authored lines (ai_mechanic_use.md M3.2), drawn once per match.</summary>
    public enum WallStyle : byte
    {
        /// <summary>No walls this match.</summary>
        None = 0,

        /// <summary>Every line tile but the gate's opening gets a wall; no gate is built.</summary>
        Funnel = 1,

        /// <summary>Every line tile gets a wall and the gate tile a gate.</summary>
        Sealed = 2,
    }

    /// <summary>The variety stream and the values drawn from it for one AI player. Mutable (the generator advances); owned by one controller.</summary>
    public sealed class AiVariety
    {
        /// <summary>
        /// Salt mixed into the per-player seed before the SplitMix64 finaliser so the variety stream differs from the controller's stream
        /// (ASCII "VARIETY1"). An algorithm constant, not tuning. Range: fixed.
        /// </summary>
        public const ulong VarietySalt = 0x5641524945545931UL;

        /// <summary>SplitMix64 increment (golden-ratio constant), as in the harness' SeedDerivation. Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;

        /// <summary>First SplitMix64 multiplier. Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixMultiplier1 = 0xBF58476D1CE4E5B9UL;

        /// <summary>Second SplitMix64 multiplier. Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixMultiplier2 = 0x94D049BB133111EBUL;

        /// <summary>The variety generator (PCG32). Not the simulation's and not the controller's.</summary>
        private readonly DeterministicRandom _rng;

        /// <summary>The opening drawn at match start; null when the plan has no openings.</summary>
        public AiOpening? Opening { get; private set; }

        /// <summary>ID of the drawn opening, or "" when there is none. For records and traces.</summary>
        public string OpeningId => Opening == null ? string.Empty : Opening.Id;

        /// <summary>True while the opening's composition and timing values apply: there is an opening and <paramref name="tick"/> is before the handover.</summary>
        /// <param name="tick">The tick being decided. Unit: ticks. Range: 0 upward.</param>
        public bool InOpening(int tick)
        {
            return Opening != null && tick < HandoverTick;
        }

        /// <summary>The plan the brain sees from tick <see cref="HandoverTick"/> on (and before it when there is no opening): the profile's plan with its composition jittered.</summary>
        public AiFactionPlan MainPlan { get; private set; }

        /// <summary>The plan the brain sees before <see cref="HandoverTick"/>; the same object as <see cref="MainPlan"/> when there is no opening.</summary>
        public AiFactionPlan OpeningPlan { get; private set; }

        /// <summary>Tick at which <see cref="OpeningPlan"/> gives way to <see cref="MainPlan"/>. Unit: ticks. Range: 0 upward; 0 with no opening.</summary>
        public int HandoverTick { get; private set; }

        /// <summary>Seconds added to the first depot clocks (rule E), drawn once at match start. Unit: seconds. Range: -expand_jitter_seconds to +expand_jitter_seconds; 0 when the key is 0.</summary>
        public int ExpandOffsetSeconds { get; private set; }

        /// <summary>Offset added to attack_min_advantage until the next <see cref="DrawAttackAdvantageOffset"/>. Unit: ratio. Range: -attack_advantage_jitter to +attack_advantage_jitter in steps of 0.01; 0 before the first draw.</summary>
        public Fix64 AttackAdvantageOffset { get; private set; }

        /// <summary>The wave aim drawn at match start when use_casts is on; <see cref="WaveAim.Melee"/> otherwise (no draw is made).</summary>
        public WaveAim WaveAim { get; private set; }

        /// <summary>The wall style drawn at match start when use_walls is on; none otherwise (no draw is made).</summary>
        public WallStyle WallStyle { get; private set; }

        /// <summary>Sets the wall style by hand (tests only).</summary>
        /// <param name="style">Value for <see cref="WallStyle"/>.</param>
        internal void SetWallStyleForTest(WallStyle style)
        {
            WallStyle = style;
        }

        /// <summary>Sets the wave aim by hand (tests only).</summary>
        /// <param name="aim">Value for <see cref="WaveAim"/>.</param>
        internal void SetWaveAimForTest(WaveAim aim)
        {
            WaveAim = aim;
        }

        /// <summary>Private: use <see cref="Create"/> (or build by hand in tests through the properties).</summary>
        private AiVariety(DeterministicRandom rng, AiFactionPlan plan)
        {
            _rng = rng;
            MainPlan = plan;
            OpeningPlan = plan;
        }

        /// <summary>A variety object with no draws made, for tests that set the offsets by hand.</summary>
        /// <param name="plan">The plan both <see cref="MainPlan"/> and <see cref="OpeningPlan"/> start as.</param>
        /// <param name="seed">Seed of the generator. Range: any ulong.</param>
        internal static AiVariety ForTest(AiFactionPlan plan, ulong seed)
        {
            return new AiVariety(new DeterministicRandom(seed), plan);
        }

        /// <summary>Sets the two offsets by hand (tests only).</summary>
        /// <param name="expandOffsetSeconds">Value for <see cref="ExpandOffsetSeconds"/>. Unit: seconds.</param>
        /// <param name="attackAdvantageOffset">Value for <see cref="AttackAdvantageOffset"/>. Unit: ratio.</param>
        internal void SetOffsetsForTest(int expandOffsetSeconds, Fix64 attackAdvantageOffset)
        {
            ExpandOffsetSeconds = expandOffsetSeconds;
            AttackAdvantageOffset = attackAdvantageOffset;
        }

        /// <summary>
        /// The variety generator seed of a player: SplitMix64 of (the controller's seed xor <see cref="VarietySalt"/>). Range: any ulong.
        /// The finaliser decorrelates this stream from the controller's although both start from the same per-player seed.
        /// </summary>
        /// <param name="matchSeed">The match seed (MatchSettings.Seed).</param>
        /// <param name="player">The AI's player slot.</param>
        public static ulong SeedFor(ulong matchSeed, PlayerId player)
        {
            unchecked
            {
                // SplitMix64 working value. Range: any ulong.
                ulong z = (SkirmishAiController.SeedFor(matchSeed, player) ^ VarietySalt) + SplitMixIncrement;
                z = (z ^ (z >> 30)) * SplitMixMultiplier1;
                z = (z ^ (z >> 27)) * SplitMixMultiplier2;
                return z ^ (z >> 31);
            }
        }

        /// <summary>True when the profile asks for any variety, so the controller must create an <see cref="AiVariety"/>.</summary>
        /// <param name="profile">The profile.</param>
        /// <param name="plan">The player's faction plan.</param>
        public static bool IsNeeded(AiProfile profile, AiFactionPlan plan)
        {
            return plan.Openings.Count > 0 || profile.CompositionJitterFraction > Fix64.Zero || profile.ExpandJitterSeconds > 0 || profile.AttackAdvantageJitter > Fix64.Zero
                || profile.UseCasts || profile.UseWalls;
        }

        /// <summary>
        /// One weight moved by a percentage step: 0 stays 0; otherwise clamp((w x (100 + d) + 50) / 100, 1, 100) in integers, so a band never
        /// adds a unit the data excluded, never removes one it included and stays inside 0 to 100.
        /// </summary>
        /// <param name="w">The base weight. Unit: weight. Range: 0 to 100.</param>
        /// <param name="d">The step in percent. Unit: percent. Range: -50 to 50.</param>
        public static int Jitter(int w, int d)
        {
            if (w == 0)
            {
                return 0;
            }
            // Moved weight, rounded to nearest. Unit: weight.
            int moved = (w * (100 + d) + 50) / 100;
            return Math.Max(1, Math.Min(100, moved));
        }

        /// <summary>
        /// Draws the match-start values in the fixed order of R.2 from a generator seeded with <paramref name="seed"/>: the opening (by weight,
        /// when the plan has openings), the opening's composition band and the plan's composition band (when composition_jitter_fraction is
        /// above 0), then the expansion offset (when expand_jitter_seconds is above 0).
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <param name="plan">The player's faction plan.</param>
        /// <param name="seed">Variety seed, from <see cref="SeedFor"/>. Range: any ulong.</param>
        /// <param name="ticksPerSecond">Simulation ticks per second, for the handover time. Unit: ticks per second. Range: 1 upward.</param>
        public static AiVariety Create(AiProfile profile, AiFactionPlan plan, ulong seed, int ticksPerSecond)
        {
            // The generator of this player's variety stream.
            DeterministicRandom rng = new DeterministicRandom(seed);
            // The variety record this player's match-start draws fill in: the drawn opening, the handover tick, the two plans and the expansion offset.
            AiVariety variety = new AiVariety(rng, plan);
            // Build order of both plans: the opening's whole-game build order, or the plan's own.
            IReadOnlyList<AiBuildStep> buildOrder = plan.BuildOrder;
            // The opening's composition (after its band), when an opening was drawn.
            IReadOnlyDictionary<string, int>? openingComposition = null;
            if (plan.Openings.Count > 0)
            {
                // Sum of the opening weights. Unit: weight. Range: 1 to 1600 (validated above 0).
                int total = 0;
                // One opening of the plan, in file order. Range: a validated opening of the plan.
                foreach (AiOpening candidate in plan.Openings)
                {
                    total += candidate.Weight;
                }
                // The draw: a uniform integer from 0 to total - 1 that picks the opening. Unit: weight. Range: 0 to total - 1.
                int draw = rng.NextInt(total);
                // Weight sum of the openings walked so far, in file order. Unit: weight. Range: 0 to total.
                int running = 0;
                // One opening being tested against the draw, in file order. Range: a validated opening of the plan.
                foreach (AiOpening candidate in plan.Openings)
                {
                    running += candidate.Weight;
                    if (running > draw)
                    {
                        variety.Opening = candidate;
                        break;
                    }
                }
                buildOrder = variety.Opening!.BuildOrder;
                openingComposition = profile.CompositionJitterFraction > Fix64.Zero
                    ? JitterComposition(variety.Opening.Composition, profile, rng) : variety.Opening.Composition;
                variety.HandoverTick = variety.Opening.HandoverSeconds * ticksPerSecond;
            }
            // The plan's own composition after its band (draw 3), or unchanged.
            IReadOnlyDictionary<string, int> mainComposition = profile.CompositionJitterFraction > Fix64.Zero
                ? JitterComposition(plan.Composition, profile, rng) : plan.Composition;
            variety.MainPlan = plan.WithOrders(buildOrder, mainComposition);
            variety.OpeningPlan = openingComposition == null ? variety.MainPlan : plan.WithOrders(buildOrder, openingComposition);
            if (profile.ExpandJitterSeconds > 0)
            {
                variety.ExpandOffsetSeconds = rng.NextIntInclusive(-profile.ExpandJitterSeconds, profile.ExpandJitterSeconds);
            }
            if (profile.UseWalls)
            {
                // The wall style draw: a uniform integer from 0 to the weight sum - 1, mapped through the cumulative weights in the order none, funnel, sealed. Unit: weight. Range: 0 to 299.
                int styleDraw = rng.NextInt(profile.WallStyleNoneWeight + profile.WallStyleFunnelWeight + profile.WallStyleSealedWeight);
                variety.WallStyle = styleDraw < profile.WallStyleNoneWeight ? WallStyle.None
                    : styleDraw < profile.WallStyleNoneWeight + profile.WallStyleFunnelWeight ? WallStyle.Funnel : WallStyle.Sealed;
            }
            if (profile.UseCasts)
            {
                // The wave aim draw: a uniform integer from 0 to the weight sum - 1; melee takes the first WaveAimMeleeWeight values. Unit: weight. Range: 0 to 199.
                int aimDraw = rng.NextInt(profile.WaveAimMeleeWeight + profile.WaveAimAllWeight);
                variety.WaveAim = aimDraw < profile.WaveAimMeleeWeight ? WaveAim.Melee : WaveAim.All;
            }
            return variety;
        }

        /// <summary>The plan the brain should see at <paramref name="tick"/>: the opening plan before the handover tick, else the main plan.</summary>
        /// <param name="tick">The tick being decided. Unit: ticks. Range: 0 upward.</param>
        public AiFactionPlan PlanAt(int tick)
        {
            return tick < HandoverTick ? OpeningPlan : MainPlan;
        }

        /// <summary>
        /// Draws a new <see cref="AttackAdvantageOffset"/> when attack_advantage_jitter is above 0: A = round(jitter x 100), the offset is
        /// a uniform integer in -A..A divided by 100. Called by the brain each time the army starts massing.
        /// </summary>
        /// <param name="profile">The profile.</param>
        public void DrawAttackAdvantageOffset(AiProfile profile)
        {
            if (profile.AttackAdvantageJitter <= Fix64.Zero)
            {
                return;
            }
            // Largest step count, in hundredths. Unit: percent. Range: 0 to 50. Rounded, because 0.15 is not exact in Q32.32.
            int steps = (profile.AttackAdvantageJitter * 100).RoundToInt();
            AttackAdvantageOffset = Fix64.FromInt(_rng.NextIntInclusive(-steps, steps)) / 100;
        }

        /// <summary>The composition with every weight above 0 moved by <see cref="Jitter"/> with its own draw, in ordinal ID order.</summary>
        private static IReadOnlyDictionary<string, int> JitterComposition(IReadOnlyDictionary<string, int> composition, AiProfile profile, DeterministicRandom rng)
        {
            // Largest step, in percent. Unit: percent. Range: 0 to 50. Rounded, because the fraction is not exact in Q32.32.
            int band = (profile.CompositionJitterFraction * 100).RoundToInt();
            // The result in ordinal ID order, starting as a copy.
            SortedDictionary<string, int> moved = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in composition)
            {
                moved.Add(pair.Key, pair.Value);
            }
            // Unit IDs in ordinal order (a snapshot, so the dictionary may be changed while walking it).
            foreach (string id in new List<string>(moved.Keys))
            {
                // Base weight of this unit. Unit: weight. Range: 0 to 100.
                int weight = moved[id];
                if (weight > 0)
                {
                    moved[id] = Jitter(weight, rng.NextIntInclusive(-band, band));
                }
            }
            return moved;
        }
    }
}
