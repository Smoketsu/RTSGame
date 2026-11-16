// rts/tools/BalanceHarness/SeedDerivation.cs
// Derives the simulation seed of one batch game from the base seed, the map, the matchup and the game index (spec: rts/docs/specs/balance_harness.md
// section 4). Pure and platform independent: FNV-1a 64 over the UTF-8 text "<map>/<matchup>", xor the base seed and the game index, then the SplitMix64
// finaliser. Every process (coordinator, workers, tests) therefore derives the same seed for the same game. Part of Rts.Tools.BalanceHarness.
using System.Text;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Pure seed derivation for batch games.</summary>
    public static class SeedDerivation
    {
        /// <summary>FNV-1a 64-bit offset basis. Range: fixed constant of the algorithm.</summary>
        private const ulong FnvOffset = 0xCBF29CE484222325UL;

        /// <summary>FNV-1a 64-bit prime. Range: fixed constant of the algorithm.</summary>
        private const ulong FnvPrime = 0x100000001B3UL;

        /// <summary>SplitMix64 increment (golden-ratio constant). Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;

        /// <summary>First SplitMix64 multiplier. Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixMultiplier1 = 0xBF58476D1CE4E5B9UL;

        /// <summary>Second SplitMix64 multiplier. Range: fixed constant of the algorithm.</summary>
        private const ulong SplitMixMultiplier2 = 0x94D049BB133111EBUL;

        /// <summary>FNV-1a 64 hash of the UTF-8 bytes of <paramref name="text"/>.</summary>
        /// <param name="text">Text to hash; not null.</param>
        public static ulong Fnv1a64(string text)
        {
            // Running hash. Range: any ulong.
            ulong hash = FnvOffset;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                unchecked
                {
                    hash = (hash ^ b) * FnvPrime;
                }
            }
            return hash;
        }

        /// <summary>The seed of game <paramref name="gameIndex"/> of the cell (<paramref name="mapId"/>, <paramref name="matchupId"/>).</summary>
        /// <param name="baseSeed">The batch's base seed. Range: any ulong (the data file allows 0 to 2147483647).</param>
        /// <param name="mapId">Map ID, for example "greybox_1v1".</param>
        /// <param name="matchupId">Matchup ID, for example "skai_vs_water".</param>
        /// <param name="gameIndex">Game index within the cell. Unit: games. Range: 0 upward.</param>
        public static ulong GameSeed(ulong baseSeed, string mapId, string matchupId, int gameIndex)
        {
            unchecked
            {
                // Cell hash mixed with the base seed and the game index, before the finaliser.
                ulong x = baseSeed ^ Fnv1a64(mapId + "/" + matchupId) ^ (ulong)gameIndex;
                // SplitMix64 working value. Range: any ulong.
                ulong z = x + SplitMixIncrement;
                z = (z ^ (z >> 30)) * SplitMixMultiplier1;
                z = (z ^ (z >> 27)) * SplitMixMultiplier2;
                return z ^ (z >> 31);
            }
        }
    }
}
