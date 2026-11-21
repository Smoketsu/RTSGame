// rts/src/Game/Assets/Scripts/Bootstrap/MatchLauncher.cs
// Carries the next match request across a scene reload (player_interface_2.md rule 1: one match per scene load). The setup screen,
// restart and the menus call Load, which stores the request in Pending and reloads the active scene; after the reload
// SimulationHost.Awake starts Pending. Presentation state only: the simulation never reads it. Pending is reset when a Play session
// starts, so a session never inherits the previous one's request when domain reload is off. Part of Rts.Game.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rts.Game.Bootstrap
{
    /// <summary>What kind of match a request starts.</summary>
    public enum MatchKind
    {
        /// <summary>The host's Inspector command script (hash checks and verification scripts).</summary>
        Script,

        /// <summary>A skirmish from the setup screen: map, slots, seed, local AI opponents.</summary>
        Skirmish,

        /// <summary>Playback of a replay file (UI-15).</summary>
        Replay,
    }

    /// <summary>Who plays a setup slot.</summary>
    public enum SlotKind
    {
        /// <summary>A person at this machine.</summary>
        Human,

        /// <summary>A local skirmish AI controller.</summary>
        Ai,

        /// <summary>Nobody: the slot is left out of the match.</summary>
        Closed,
    }

    /// <summary>One player slot of a skirmish request.</summary>
    public sealed class MatchSlot
    {
        /// <summary>Player slot, also the map spawn index. Range: 0 to the map's spawn count - 1.</summary>
        public int Slot;

        /// <summary>Team. Range: 0 to the slot count - 1 (shown as 1 upward).</summary>
        public int Team;

        /// <summary>Faction ID (a faction with an HQ). Range: an ID from the content catalog.</summary>
        public string FactionId;

        /// <summary>Who plays the slot.</summary>
        public SlotKind Kind;

        /// <summary>AI profile ID for an AI slot (a file under rts/data/ai), else null.</summary>
        public string ProfileId;
    }

    /// <summary>Everything needed to start one match after a scene reload.</summary>
    public sealed class MatchRequest
    {
        /// <summary>What the request starts.</summary>
        public MatchKind Kind;

        /// <summary>Map ID for a skirmish. Range: an ID under rts/data/maps.</summary>
        public string MapId;

        /// <summary>Match seed for a skirmish. Unit: none. Range: 0 to 2147483647.</summary>
        public int Seed;

        /// <summary>True when a new seed is drawn at each start (and restart). Unit: on/off.</summary>
        public bool RandomSeed;

        /// <summary>The skirmish slots in ascending slot order, closed ones included.</summary>
        public List<MatchSlot> Slots = new List<MatchSlot>();

        /// <summary>Replay file to play for kind Replay, else null. Unit: absolute file path.</summary>
        public string ReplayPath;
    }

    /// <summary>The pending match request and the scene reload that starts it.</summary>
    public static class MatchLauncher
    {
        /// <summary>The request the next scene load starts, or null for the menu or the Inspector script.</summary>
        public static MatchRequest Pending { get; private set; }

        /// <summary>True after Load(null): the next scene load shows the main menu even when the host is in Script mode. Unit: on/off.</summary>
        public static bool ShowMenu { get; private set; }

        /// <summary>Stores the request and reloads the active scene, which starts it (null returns to the main menu).</summary>
        /// <param name="request">The match to start, or null.</param>
        public static void Load(MatchRequest request)
        {
            Pending = request;
            ShowMenu = request == null;
            // Build index of the scene to reload; -1 for an unsaved scene, which cannot be reloaded by index.
            int index = SceneManager.GetActiveScene().buildIndex;
            if (index < 0)
            {
                Debug.LogError("[MatchLauncher] The active scene is not in the Build Settings, so it cannot be reloaded. Open Assets/Scenes/Bootstrap.unity (RTS > Create Bootstrap Scene).");
                return;
            }
            SceneManager.LoadScene(index);
        }

        /// <summary>Clears the request when a Play session starts (also with domain reload off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Pending = null;
            ShowMenu = false;
        }

        /// <summary>
        /// The request to restart: the same one, with a newly drawn seed when its Random switch is on (UnityEngine.Random: an input to the
        /// match, never simulation code).
        /// </summary>
        /// <param name="request">The request the running match was started from.</param>
        public static MatchRequest RestartOf(MatchRequest request)
        {
            if (request == null || !request.RandomSeed)
            {
                return request;
            }
            MatchRequest copy = new MatchRequest { Kind = request.Kind, MapId = request.MapId, Seed = Random.Range(0, int.MaxValue), RandomSeed = true, ReplayPath = request.ReplayPath };
            copy.Slots.AddRange(request.Slots);
            return copy;
        }

        /// <summary>Quits the game: stops Play Mode in the editor, quits a player (the replay is written when the host is destroyed).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
