// rts/tools/BalanceHarness/GameRecord.cs
// The immutable result records of the batch harness and their JSON-line form (spec: rts/docs/specs/balance_harness.md section 3): GameRecord for
// one AI-versus-AI game and HqFloorRecord for one HQ floor trial. A worker process writes one line per record to stdout; the coordinator reads
// them back through Rts.Sim.Data.JsonReader and writes the same lines to results.jsonl and hq_floor.jsonl. Part of Rts.Tools.BalanceHarness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rts.Sim.Data;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>One killed HQ in a game.</summary>
    public sealed class HqKill
    {
        /// <summary>Faction of the HQ that died. Range: a faction ID.</summary>
        public string Faction { get; }

        /// <summary>Tick of the first damage above 0 the HQ took. Unit: ticks. Range: 0 upward.</summary>
        public int FirstDamageTick { get; }

        /// <summary>Tick the HQ was removed. Unit: ticks. Range: FirstDamageTick upward.</summary>
        public int DeathTick { get; }

        /// <summary>Bundles the values.</summary>
        public HqKill(string faction, int firstDamageTick, int deathTick)
        {
            Faction = faction;
            FirstDamageTick = firstDamageTick;
            DeathTick = deathTick;
        }
    }

    /// <summary>The result of one game. Immutable.</summary>
    public sealed class GameRecord
    {
        /// <summary>Result text for a win of slot 0.</summary>
        public const string Slot0 = "slot0";

        /// <summary>Result text for a win of slot 1.</summary>
        public const string Slot1 = "slot1";

        /// <summary>Result text for a match with no team left standing.</summary>
        public const string Draw = "draw";

        /// <summary>Result text for a match still undecided at the tick limit.</summary>
        public const string Undecided = "undecided";

        /// <summary>Map ID of the cell.</summary>
        public string Map { get; }

        /// <summary>Matchup ID of the cell ("a_vs_b").</summary>
        public string Matchup { get; }

        /// <summary>Game index in the cell. Unit: games. Range: 0 to N-1.</summary>
        public int Game { get; }

        /// <summary>Derived seed of the game. Range: any ulong.</summary>
        public ulong Seed { get; }

        /// <summary>Faction in slot 0 after the side swap.</summary>
        public string Slot0Faction { get; }

        /// <summary>Faction in slot 1 after the side swap.</summary>
        public string Slot1Faction { get; }

        /// <summary>AI profile ID slot 0 played by (a rts/data/ai file name without .json).</summary>
        public string Slot0Profile { get; }

        /// <summary>AI profile ID slot 1 played by.</summary>
        public string Slot1Profile { get; }

        /// <summary>"slot0", "slot1", "draw" or "undecided".</summary>
        public string Result { get; }

        /// <summary>Last tick stepped. Unit: ticks. Range: 0 upward.</summary>
        public int EndTick { get; }

        /// <summary>State hash after the last tick. Range: any ulong, written as 16 hex digits.</summary>
        public ulong StateHash { get; }

        /// <summary>
        /// Hash of every AI command of the game plus the end tick and result (skirmish_ai_v4.md S.1), independent of the seed: two games that play the same commands
        /// have the same value. Range: any ulong, written as 16 hex digits.
        /// </summary>
        public ulong PlaySignature { get; }

        /// <summary>Content digest of the data the game ran on (same for every game of one data set). Range: any ulong, written as 16 hex digits.</summary>
        public ulong ContentDigest { get; }

        /// <summary>Every HQ that died, in ascending entity order.</summary>
        public IReadOnlyList<HqKill> HqKills { get; }

        /// <summary>Bundles the values.</summary>
        public GameRecord(string map, string matchup, int game, ulong seed, string slot0Faction, string slot1Faction, string slot0Profile, string slot1Profile, string result,
            int endTick, ulong stateHash, ulong playSignature, ulong contentDigest, IReadOnlyList<HqKill> hqKills)
        {
            Map = map;
            Matchup = matchup;
            Game = game;
            Seed = seed;
            Slot0Faction = slot0Faction;
            Slot1Faction = slot1Faction;
            Slot0Profile = slot0Profile;
            Slot1Profile = slot1Profile;
            Result = result;
            EndTick = endTick;
            StateHash = stateHash;
            PlaySignature = playSignature;
            ContentDigest = contentDigest;
            HqKills = hqKills;
        }

        /// <summary>The record as one JSON line (no trailing newline), keys in a fixed order.</summary>
        public string ToJsonLine()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"map\":").Append(Quote(Map)).Append(",\"matchup\":").Append(Quote(Matchup))
                .Append(",\"game\":").Append(Game.ToString(CultureInfo.InvariantCulture))
                .Append(",\"seed\":").Append(Quote(Seed.ToString(CultureInfo.InvariantCulture)))
                .Append(",\"slot0\":").Append(Quote(Slot0Faction)).Append(",\"slot1\":").Append(Quote(Slot1Faction))
                .Append(",\"profile_slot0\":").Append(Quote(Slot0Profile)).Append(",\"profile_slot1\":").Append(Quote(Slot1Profile))
                .Append(",\"result\":").Append(Quote(Result))
                .Append(",\"end_tick\":").Append(EndTick.ToString(CultureInfo.InvariantCulture))
                .Append(",\"state_hash\":").Append(Quote(StateHash.ToString("x16", CultureInfo.InvariantCulture)))
                .Append(",\"play_signature\":").Append(Quote(PlaySignature.ToString("x16", CultureInfo.InvariantCulture)))
                .Append(",\"content_digest\":").Append(Quote(ContentDigest.ToString("x16", CultureInfo.InvariantCulture)))
                .Append(",\"hq_kills\":[");
            for (int i = 0; i < HqKills.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("{\"faction\":").Append(Quote(HqKills[i].Faction))
                    .Append(",\"first_damage_tick\":").Append(HqKills[i].FirstDamageTick.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"death_tick\":").Append(HqKills[i].DeathTick.ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>Parses a line written by <see cref="ToJsonLine"/>; throws <see cref="HarnessException"/> when it is malformed.</summary>
        /// <param name="line">One JSON line.</param>
        public static GameRecord Parse(string line)
        {
            try
            {
                JsonValue root = JsonReader.Parse(line, "worker output");
                // Kills read from the array.
                List<HqKill> kills = new List<HqKill>();
                foreach (JsonValue item in root.RequireArray("hq_kills"))
                {
                    kills.Add(new HqKill(item.RequireNonEmptyString("faction"), item.RequireInt("first_damage_tick", 0, int.MaxValue), item.RequireInt("death_tick", 0, int.MaxValue)));
                }
                // Result text; must be one of the four known values.
                string result = root.RequireString("result");
                if (result != Slot0 && result != Slot1 && result != Draw && result != Undecided)
                {
                    throw new HarnessException("malformed worker line (unknown result '" + result + "'): " + line);
                }
                return new GameRecord(root.RequireNonEmptyString("map"), root.RequireNonEmptyString("matchup"), root.RequireInt("game", 0, int.MaxValue),
                    ulong.Parse(root.RequireString("seed"), NumberStyles.None, CultureInfo.InvariantCulture),
                    root.RequireNonEmptyString("slot0"), root.RequireNonEmptyString("slot1"),
                    root.RequireNonEmptyString("profile_slot0"), root.RequireNonEmptyString("profile_slot1"), result, root.RequireInt("end_tick", 0, int.MaxValue),
                    ulong.Parse(root.RequireString("state_hash"), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
                    ulong.Parse(root.RequireString("play_signature"), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
                    ulong.Parse(root.RequireString("content_digest"), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture), kills);
            }
            catch (Exception ex) when (ex is DataValidationException || ex is FormatException || ex is OverflowException)
            {
                throw new HarnessException("malformed worker line (" + ex.Message + "): " + line);
            }
        }

        /// <summary>JSON string literal of <paramref name="text"/> (quote and backslash escaped; harness IDs hold nothing else special).</summary>
        internal static string Quote(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    /// <summary>The result of one HQ floor trial. Immutable.</summary>
    public sealed class HqFloorRecord
    {
        /// <summary>Faction whose units attack.</summary>
        public string AttackerFaction { get; }

        /// <summary>Unit definition ID of the attackers.</summary>
        public string Unit { get; }

        /// <summary>Attackers placed. Unit: units. Range: 1 upward (hq_floor_supply / unit supply).</summary>
        public int Count { get; }

        /// <summary>Unit definition ID of the defending HQ.</summary>
        public string DefenderHq { get; }

        /// <summary>"rooted" or "uprooted": the HQ's state when the trial starts.</summary>
        public string HqState { get; }

        /// <summary>Tick of the first damage above 0 to the HQ, or -1 when it took none. Unit: ticks. Range: -1 upward.</summary>
        public int FirstDamageTick { get; }

        /// <summary>Tick the HQ was removed, or -1 when it survived. Unit: ticks. Range: -1 upward.</summary>
        public int DeathTick { get; }

        /// <summary>Death tick minus first damage tick, or -1 when the HQ survived. Unit: ticks. Range: -1 upward.</summary>
        public int TtkTicks { get; }

        /// <summary>Bundles the values.</summary>
        public HqFloorRecord(string attackerFaction, string unit, int count, string defenderHq, string hqState, int firstDamageTick, int deathTick)
        {
            AttackerFaction = attackerFaction;
            Unit = unit;
            Count = count;
            DefenderHq = defenderHq;
            HqState = hqState;
            FirstDamageTick = firstDamageTick;
            DeathTick = deathTick;
            TtkTicks = deathTick >= 0 && firstDamageTick >= 0 ? deathTick - firstDamageTick : -1;
        }

        /// <summary>The record as one JSON line (no trailing newline), keys in a fixed order.</summary>
        public string ToJsonLine()
        {
            return "{\"attacker_faction\":" + GameRecord.Quote(AttackerFaction) + ",\"unit\":" + GameRecord.Quote(Unit)
                + ",\"count\":" + Count.ToString(CultureInfo.InvariantCulture) + ",\"defender_hq\":" + GameRecord.Quote(DefenderHq)
                + ",\"hq_state\":" + GameRecord.Quote(HqState) + ",\"first_damage_tick\":" + FirstDamageTick.ToString(CultureInfo.InvariantCulture)
                + ",\"death_tick\":" + DeathTick.ToString(CultureInfo.InvariantCulture) + ",\"ttk_ticks\":" + TtkTicks.ToString(CultureInfo.InvariantCulture) + "}";
        }

        /// <summary>Parses a line written by <see cref="ToJsonLine"/>; throws <see cref="HarnessException"/> when it is malformed.</summary>
        /// <param name="line">One JSON line.</param>
        public static HqFloorRecord Parse(string line)
        {
            try
            {
                JsonValue root = JsonReader.Parse(line, "worker output");
                return new HqFloorRecord(root.RequireNonEmptyString("attacker_faction"), root.RequireNonEmptyString("unit"), root.RequireInt("count", 1, int.MaxValue),
                    root.RequireNonEmptyString("defender_hq"), root.RequireNonEmptyString("hq_state"),
                    root.RequireInt("first_damage_tick", -1, int.MaxValue), root.RequireInt("death_tick", -1, int.MaxValue));
            }
            catch (Exception ex) when (ex is DataValidationException)
            {
                throw new HarnessException("malformed worker line (" + ex.Message + "): " + line);
            }
        }
    }
}
