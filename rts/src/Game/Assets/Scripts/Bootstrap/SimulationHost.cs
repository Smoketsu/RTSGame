// rts/src/Game/Assets/Scripts/Bootstrap/SimulationHost.cs
// The Phase 0 bridge between Unity and the engine-free simulation (assembly Rts.Sim). It owns one Simulation,
// reads the command script <data dir>/scripts/smoke.json (the same file the headless harness runs) with
// Rts.Sim.Data.CommandScript, builds the players from it, submits every scripted command through Simulation.Submit
// before the first Step, steps the simulation on a fixed accumulator until the script's tick count is reached, and
// draws every unit as a placeholder cube positioned from Unit.Position. Part of Rts.Game (Unity side, presentation only).
//
// Rules this file follows (docs/decisions and the architecture rules):
//   - Unity only renders. The simulation is never written to except through Simulation.Submit (rule 2); no
//     Unit, FixVec2 or Fix64 value is ever modified from here, and nothing read back from a Transform, float
//     or Time value flows into a command. Fix64 -> float conversion goes one way only (ToFloatForPresentation).
//   - Wall-clock time (Time.deltaTime) only decides HOW MANY ticks run this frame, never what a tick does.
//   - Rendering interpolates between the previous and current tick state (ADR-004), one tick behind the sim.
//   - Phase 1 interaction: the host exposes read-only views for presentation code (TryGetViewTransform, UnitViews,
//     EventRaised, LayerHeightWorldUnits). TerrainView, SelectionController and RtsCameraController read them; the
//     only way player input reaches the simulation is LocalCommands.Submit, which calls SimulationHost.Submit.
//   - Cubes stand on the terrain: world Y = tile elevation layer x LayerHeightWorldUnits + the cube's offset above ground.
//   - Session recording (player_interface.md UI-0): with "Record Session" on, a Rts.Replay.ReplayRecorder is attached right
//     after Start, every scripted and player command goes through it (Submit) and every tick is stepped through it. When the
//     host is destroyed (leaving Play Mode, quitting) it writes <repo>/rts/build/replays/<local time>.rtsreplay in the editor,
//     <persistentDataPath>/replays in a player; check it with rts/tools/ReplayCheck. The wall clock only names the file.
//   - Start modes (player_interface_2.md UI-12): Awake always loads the data. Script mode starts the Inspector script at once, as
//     before; Menu mode (UI-13, the default) starts MatchLauncher.Pending when one is stored (after the scene reload it causes)
//     and otherwise stays idle so AutoBootstrap shows the main menu. A pending request (restart) starts in either mode, and
//     after Load(null) (Main Menu) even Script mode stays idle. StartMatch builds either kind the same way.
//   - Local AI (UI-12): one SkirmishAiController per AI slot (the script's "ai" keys or the setup's AI slots), sorted by player.
//     Each controller's Think runs exactly once immediately before every stepped tick, inside StepOnce, in ascending player order,
//     as the harness does; its commands go through Submit, so the recorder has them and the replay passes ReplayCheck. AiHost.ThinkAll
//     is not used because its sink submits around the recorder.
//   - Replay playback (player_interface_2.md UI-15): a request of kind Replay reads the file and steps a Rts.Replay.ReplayPlayback
//     instead of a recorder: no recorder, no AI, LocalCommands drops every order, fog is off (FogView.Observer), and stepping stops
//     at the replay's last tick or its first desync. PlaybackSpeed scales real time into ticks (0 pauses); maxStepsPerFrame stays.
//   - Campaign missions (player_interface_3.md UI-16): a request of kind Mission parses <campaign>/missions/<id>.json, refuses any
//     mission without exactly one human slot, builds the simulation from the mission, attaches the recorder, then the MissionRunner
//     (its setup commands go through Submit, so into the replay). Each step: camera and selection signals (or the scripted inputs
//     when playMissionInputs is on), AI, step, runner.AfterStep, views; stepping stops when the mission is won or lost, never on
//     MatchOutcome. Signals are reported only here, inside the step loop.
//   - Match end (UI-10): after each step MatchOutcome.Evaluate; once decided the host stops stepping (as the harness does), logs the
//     result and writes the replay at once. Outcome exposes the result to the HUD.
//
// Data directory (where every *.json under it is read and handed to Rts.Sim.Data.DataLoader):
//   Editor:  <repo>/rts/data, resolved as Application.dataPath + "/../../../data" (dataPath is
//            rts/src/Game/Assets). No copy step is needed and the editor always sees the committed data.
//   Players: Application.streamingAssetsPath + "/data", so the build must contain a copy of rts/data in
//            Assets/StreamingAssets/data (a build-time copy step is a Phase 1 follow-up; see docs/UNITY_SETUP.md).
//   The Inspector field "Data Directory Override" beats both when it is not empty.
//
// Scripted match (one shared file, so Unity and the headless harness cannot drift apart):
//   The script is <data dir>/<scriptRelativePath>, "scripts/smoke.json" by default. It supplies the seed, the tick
//   count, the player slots (id, team, faction, name) and every command with its tick, issuer and sequence. All
//   ticks are known up front, so every command is submitted before the first Step; nothing is submitted
//   in response to events. The Inspector seed override beats the script's seed only when it is not negative.
//   When the script's tick count has been stepped the host stops stepping, logs the final state hash once, and the
//   cubes stay where the last tick left them. Compare the Console "tick=N hash=..." lines with the harness output
//   of `dotnet run --project rts/tools/BalanceHarness -- --data rts/data` (see docs/UNITY_SETUP.md).
using System;
using System.Collections.Generic;
using System.IO;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;
using Rts.Ai;
using Rts.Campaign;
using Rts.Game.CameraRig;
using Rts.Game.Presentation;
using Rts.Game.UnitSelection;
using Rts.Replay;
using UnityEngine;
// Unity 6.6 added UnityEngine.EntityId; this alias makes every EntityId in this file the simulation's entity ID.
using EntityId = Rts.Sim.Core.EntityId;

namespace Rts.Game.Bootstrap
{
    /// <summary>
    /// Runs one match from the data files and shows it as cubes. Put one on a GameObject at the origin with
    /// unit scale (AutoBootstrap does this for you). Awake loads and starts the match; Update steps it;
    /// LateUpdate positions the cubes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationHost : MonoBehaviour
    {
        /// <summary>The cube that draws one simulation unit, plus the two tick states it blends between.</summary>
        private sealed class UnitView
        {
            /// <summary>The simulation unit this cube draws. Range: valid while the unit lives.</summary>
            public EntityId Id;

            /// <summary>The cube's transform. Range: non-null until the unit is removed.</summary>
            public Transform Transform;

            /// <summary>World position at the end of the previous tick. Unit: Unity world units (1 = one tile).</summary>
            public Vector3 Previous;

            /// <summary>World position at the end of the latest tick. Unit: Unity world units (1 = one tile).</summary>
            public Vector3 Current;

            /// <summary>Cube centre height above the terrain surface under it. Unit: world units. Range: 0.5 (ground and water) or 2 (air).</summary>
            public float Height;
        }

        /// <summary>Read-only handle to one unit's cube, handed to presentation code (selection, rings). Holds no simulation state.</summary>
        public readonly struct UnitViewRef
        {
            /// <summary>The simulation unit this cube draws. Range: valid while the unit lives.</summary>
            public readonly EntityId Id;

            /// <summary>The cube's transform. Range: non-null; destroyed when the unit is removed, so do not keep it across frames without a null check.</summary>
            public readonly Transform Transform;

            /// <summary>Cube centre height above the terrain under it. Unit: world units. Range: 0.5 (ground and water) or 2 (air).</summary>
            public readonly float GroundOffset;

            /// <summary>Builds the handle from its fields.</summary>
            /// <param name="id">Simulation unit.</param>
            /// <param name="transform">The unit's cube.</param>
            /// <param name="groundOffset">Cube centre height above the terrain, in world units.</param>
            public UnitViewRef(EntityId id, Transform transform, float groundOffset)
            {
                Id = id;
                Transform = transform;
                GroundOffset = groundOffset;
            }
        }

        /// <summary>
        /// Seed override for the simulation RNG. Unit: none. Range: -1 to 2147483647. -1 (default) means "use the script's
        /// seed" (1234 in smoke.json, which is what makes the hashes match the headless runner); 0 or more replaces it.
        /// </summary>
        [SerializeField, Min(-1)] private int matchSeed = -1;

        /// <summary>
        /// Command script to play, relative to the data directory, forward slashes. Unit: file path. Range: an existing
        /// JSON file in the CommandScript format under the data directory. Default "scripts/economy_smoke.json" (the Phase 1
        /// economy scenario on greybox_1v1); use "scripts/smoke.json" for the Phase 0 movement scenario, the file the headless
        /// harness runs by default. Pass the same file to the harness with --script to compare hashes.
        /// </summary>
        [SerializeField] private string scriptRelativePath = "scripts/economy_smoke.json";

        /// <summary>
        /// Keep stepping the simulation after the script's tick count so the player can keep ordering units. Unit: on/off.
        /// Default on. Off restores the Phase 0 behaviour (stop at the script's last tick; orders are then never executed).
        /// The final-hash line is logged either way when the script's tick count is reached.
        /// </summary>
        [SerializeField] private bool continueAfterScript = true;

        /// <summary>
        /// World height of one terrain elevation layer. Unit: world units per layer (one tile = 1 world unit wide).
        /// Range: 0.1 to 2; default 0.5. TerrainView draws layer N at world Y = N x this value and units stand on it.
        /// </summary>
        [SerializeField, Range(0.1f, 2f)] private float layerHeightWorldUnits = 0.5f;

        /// <summary>
        /// Most ticks stepped in one rendered frame. Unit: ticks per frame. Range: 1 to 60; default 10. A slow frame
        /// (editor pause, breakpoint, hitch) would otherwise ask for a huge catch-up and every catch-up frame would be
        /// slower still (spiral of death). Time beyond the cap is dropped, so the match runs slower than real time
        /// while the machine cannot keep up.
        /// </summary>
        [SerializeField, Range(1, 60)] private int maxStepsPerFrame = 10;

        /// <summary>Log every StateHashedEvent to the Console as "tick=N hash=XXXXXXXXXXXXXXXX". Unit: on/off.</summary>
        [SerializeField] private bool logStateHashes = true;

        /// <summary>How the host starts a match when the scene loads.</summary>
        public enum StartMode
        {
            /// <summary>Start the Inspector command script at once (hash checks, verification scripts).</summary>
            Script,

            /// <summary>Start MatchLauncher.Pending if one is stored, else stay idle and let the main menu show (player_interface_2.md UI-13).</summary>
            Menu,
        }

        /// <summary>How the match starts when the scene loads. Default Menu; Script for hash checks and verification scripts.</summary>
        [SerializeField] private StartMode startMode = StartMode.Menu;

        /// <summary>Log the simulation's refusals of AI commands to the Console. Unit: on/off. Default off (they are frequent and expected).</summary>
        [SerializeField] private bool logAiRejections = false;

        /// <summary>Record every command and hash checkpoint and write a replay file when the host is destroyed. Unit: on/off. Default on.</summary>
        [SerializeField] private bool recordSession = true;

        /// <summary>Absolute path of a data directory to use instead of the default lookup. Unit: file path. Range: empty (default lookup) or an existing folder.</summary>
        [SerializeField] private string dataDirectoryOverride = "";

        /// <summary>Absolute path of the campaign folder (missions/, inputs/) instead of the one beside the data folder. Unit: file path. Range: empty (default) or an existing folder.</summary>
        [SerializeField] private string campaignDirectoryOverride = "";

        /// <summary>Play a mission's scripted inputs (<campaign>/inputs/<id>.json) instead of reporting camera and selection: the parity check of player_interface_3.md 7.2. Unit: on/off. Default off.</summary>
        [SerializeField] private bool playMissionInputs = false;

        /// <summary>Cube edge length per unit of Definition.Radius. Unit: world units per tile of radius. Range: 0.1 to 4; 2 makes the cube as wide as the unit's collision circle.</summary>
        [SerializeField, Range(0.1f, 4f)] private float cubeScalePerRadius = 2f;

        /// <summary>Smallest cube edge so tiny units stay visible. Unit: world units. Range: 0.1 to 2.</summary>
        [SerializeField, Range(0.1f, 2f)] private float minimumCubeSize = 0.4f;

        /// <summary>Cube centre height above the terrain for ground and water units. Unit: world units. Range: 0 to 5; 0.5 rests a unit cube on the ground.</summary>
        private const float GroundHeight = 0.5f;

        /// <summary>Cube centre height above the terrain for air units. Unit: world units. Range: 0 to 10.</summary>
        private const float AirHeight = 2f;

        /// <summary>Player colours by player slot: 0 blue, 1 red, then fallbacks. Unit: linear-agnostic RGBA, each channel 0 to 1.</summary>
        private static readonly Color[] PlayerColors =
        {
            new Color(0.20f, 0.40f, 1.00f, 1f),
            new Color(1.00f, 0.25f, 0.20f, 1f),
            new Color(0.25f, 0.80f, 0.30f, 1f),
            new Color(1.00f, 0.85f, 0.20f, 1f),
        };

        /// <summary>Colour for neutral units (no owner). Unit: RGBA, each channel 0 to 1.</summary>
        private static readonly Color NeutralColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        /// <summary>The match. Null until Awake succeeds. Read-only from the host's point of view except for Submit.</summary>
        private Simulation _sim;

        /// <summary>The replay being played (kind Replay), else null.</summary>
        private ReplayPlayback _playback;

        /// <summary>The campaign mission runner (kind Mission), else null.</summary>
        private MissionRunner _mission;

        /// <summary>Cues the mission runner has shown, oldest first; the HUD draws them. Range: non-null.</summary>
        private readonly MissionCueQueue _cues = new MissionCueQueue();

        /// <summary>The mission's scripted inputs when playMissionInputs is on, else null.</summary>
        private MissionInputs _inputs;

        /// <summary>Camera focus last reported to the mission, in tiles; null before the first report.</summary>
        private FixVec2? _lastFocus;

        /// <summary>Selection last reported to the mission, ascending entity ID; null before the first report.</summary>
        private List<EntityId> _lastSelection;

        /// <summary>The camera controller, for the mission's camera signal. Range: null until found.</summary>
        private RtsCameraController _camera;

        /// <summary>The selection controller, for the mission's selection signal and sim points. Range: null until found.</summary>
        private SelectionController _selection;

        /// <summary>Folder the data was read from. Range: an existing folder after Awake succeeds.</summary>
        private string _dataDirectory;

        /// <summary>The AI controllers of this match, ascending player slot. Empty without AI slots.</summary>
        private readonly List<IPlayerController> _controllers = new List<IPlayerController>();

        /// <summary>Players played by an AI controller, for filtering their refusals and keeping the local player off them.</summary>
        private readonly HashSet<PlayerId> _aiPlayers = new HashSet<PlayerId>();

        /// <summary>Where the AI controllers' commands go: this host's Submit (and so the recorder). Range: non-null after Awake.</summary>
        private HostCommandSink _aiSink;

        /// <summary>The match result once one team (or none) is left standing; null while the match goes on. Set after the deciding step.</summary>
        private MatchOutcome _outcome;

        /// <summary>Records the session for the replay file. Null when recordSession is off or before Awake succeeds.</summary>
        private ReplayRecorder _recorder;

        /// <summary>Seconds of real time not yet turned into ticks. Unit: seconds. Range: 0 up to one tick duration after each Update.</summary>
        private float _accumulatorSeconds;

        /// <summary>Real seconds per tick, 1 / TicksPerSecond. Unit: seconds. Range: 1/60 to 1/10.</summary>
        private float _tickSeconds;

        /// <summary>Fraction of the way from the previous tick to the next, used to blend cube positions. Unit: fraction. Range: 0 to 1.</summary>
        private float _alpha;

        /// <summary>How many ticks the script asks for; stepping stops when the clock reaches it. Unit: ticks. Range: 0 to 1000000 (CommandScript.MaxTicks).</summary>
        private int _scriptTicks;

        /// <summary>True once the script's tick count has been stepped and the final hash logged; no further ticks run unless continueAfterScript is on. Unit: on/off.</summary>
        private bool _scriptFinished;

        /// <summary>Cube per live simulation unit. Presentation state only, never hashed or fed back.</summary>
        private readonly Dictionary<EntityId, UnitView> _views = new Dictionary<EntityId, UnitView>();

        /// <summary>One material per player slot, shared by all of that player's cubes (no per-cube material instances to leak).</summary>
        private readonly Dictionary<byte, Material> _playerMaterials = new Dictionary<byte, Material>();

        /// <summary>Material of the first cube ever made, used as the template so the project's default shader is reused.</summary>
        private Material _templateMaterial;

        /// <summary>Parent of every cube, so the Hierarchy stays tidy. A separate root at the origin with unit scale.</summary>
        private Transform _unitRoot;

        /// <summary>The running match, for read-only inspection by UI code later. Never mutate it; use Simulation.Submit.</summary>
        public Simulation Match => _sim;

        /// <summary>The loaded data set (units, maps, AI profiles); null when loading failed. Read by the setup screen.</summary>
        public DataSet Data { get; private set; }

        /// <summary>The request the running match was started from, or null while idle.</summary>
        public MatchRequest CurrentRequest { get; private set; }

        /// <summary>The reason the last start failed (data error, StandardMatchStart refusal), or null. Shown by the setup screen.</summary>
        public string StartError { get; private set; }

        /// <summary>The local human slot of a skirmish, overriding LocalCommands' Inspector slot; null in Script mode.</summary>
        public int? LocalSlotOverride { get; private set; }

        /// <summary>Where the camera should start (the local player's spawn in a skirmish), or null for the map centre. Unit: tiles (x, y).</summary>
        public Vector2? StartFocus { get; private set; }

        /// <summary>The mission runner of a Mission request (objectives, status), else null. Read-only for the HUD.</summary>
        public MissionRunner Mission => _mission;

        /// <summary>Every cue the mission has shown so far, oldest first. Empty outside missions.</summary>
        public IReadOnlyList<MissionCue> MissionCues => _cues.Cues;

        /// <summary>The campaign folder: the Inspector override, else "campaign" beside the data folder (rts/campaign in the editor). Unit: absolute folder path.</summary>
        public string CampaignDirectory => !string.IsNullOrEmpty(campaignDirectoryOverride) ? campaignDirectoryOverride
            : Path.GetFullPath(Path.Combine(_dataDirectory ?? ResolveDataDirectory(), "..", "campaign"));

        /// <summary>Collects the mission's cues for the HUD; it never calls the simulation or the runner (player_interface_3.md rule 2).</summary>
        private sealed class MissionCueQueue : IMissionPresenter
        {
            /// <summary>Every cue shown, oldest first.</summary>
            public readonly List<MissionCue> Cues = new List<MissionCue>();

            /// <summary>Appends a cue; called by the runner during Start and AfterStep.</summary>
            /// <param name="cue">The cue.</param>
            public void Show(MissionCue cue)
            {
                Cues.Add(cue);
            }
        }

        /// <summary>The replay playback of a Replay request, else null.</summary>
        public ReplayPlayback Playback => _playback;

        /// <summary>The replay file's data of a Replay request (players, recorded final hash), else null.</summary>
        public ReplayData PlaybackData { get; private set; }

        /// <summary>True while a replay plays: no input reaches the simulation. Unit: on/off.</summary>
        public bool IsPlayback => _playback != null;

        /// <summary>Real-time multiplier of a replay's stepping. Unit: times real time. Range: 0 (paused) to 8; 1 outside playback.</summary>
        public float PlaybackSpeed { get; set; } = 1f;

        /// <summary>Shows another player's resources and units (replay viewpoint). Ignored outside playback.</summary>
        /// <param name="slot">A player slot of the replay.</param>
        public void ViewPlayer(int slot)
        {
            if (IsPlayback)
            {
                LocalSlotOverride = slot;
            }
        }

        /// <summary>True while the host has loaded data but runs no match (the setup screen shows).</summary>
        public bool IsIdle => _sim == null && Data != null;

        /// <summary>True when an AI controller plays this player in the running match.</summary>
        /// <param name="player">A player slot.</param>
        public bool IsAiPlayer(PlayerId player)
        {
            return _aiPlayers.Contains(player);
        }

        /// <summary>The match result once decided (MatchOutcome.IsDecided), else null. Stepping stops when it is set (UI-10).</summary>
        public MatchOutcome Outcome => _outcome;

        /// <summary>How far between the last two ticks the picture currently is. Unit: fraction. Range: 0 to 1.</summary>
        public float InterpolationAlpha => _alpha;

        /// <summary>World height of one elevation layer. Unit: world units per layer. Range: 0.1 to 2 (Inspector field).</summary>
        public float LayerHeightWorldUnits => layerHeightWorldUnits;

        /// <summary>
        /// Raised once for every simulation event of each tick that ran (and of the scripted submissions before the first tick),
        /// oldest first, after the host has handled it itself. Listeners must only read; they must never change the simulation.
        /// An exception thrown by a listener is logged and does not stop the tick.
        /// </summary>
        public event Action<SimEvent> EventRaised;

        /// <summary>
        /// Every live cube, in no particular order. Snapshot-free: do not add or remove units while enumerating (the host only does
        /// that inside Update, so enumerating from another script's Update or OnGUI is safe).
        /// </summary>
        public IEnumerable<UnitViewRef> UnitViews
        {
            get
            {
                foreach (UnitView view in _views.Values)
                {
                    yield return new UnitViewRef(view.Id, view.Transform, view.Height);
                }
            }
        }

        /// <summary>Finds the cube of a unit.</summary>
        /// <param name="id">The simulation unit.</param>
        /// <param name="transform">The cube's transform when found; null otherwise.</param>
        /// <returns>True when the unit has a live cube.</returns>
        public bool TryGetViewTransform(EntityId id, out Transform transform)
        {
            if (_views.TryGetValue(id, out UnitView view) && view.Transform != null)
            {
                transform = view.Transform;
                return true;
            }
            transform = null;
            return false;
        }

        /// <summary>Loads data and the command script, builds the simulation, starts it and queues every scripted command. Disables itself on failure.</summary>
        private void Awake()
        {
            _aiSink = new HostCommandSink(this);
            // Observer view belongs to playback only; a static, so reset on every load.
            FogView.Observer = false;
            try
            {
                // Folder holding the data tree (tuning/, units/, factions/, ...).
                _dataDirectory = ResolveDataDirectory();
                Data = DataLoader.Load(ReadDataFiles(_dataDirectory));
            }
            catch (Exception exception)
            {
                StartError = exception.Message;
                Debug.LogError("[SimulationHost] Could not load the data: " + exception.Message);
                Debug.LogException(exception);
                enabled = false;
                return;
            }
            MatchRequest pending = MatchLauncher.Pending;
            if (pending != null)
            {
                StartMatch(pending);
            }
            else if (startMode == StartMode.Script && !MatchLauncher.ShowMenu)
            {
                StartMatch(new MatchRequest { Kind = MatchKind.Script });
            }
            // Otherwise the host stays idle and AutoBootstrap shows the main menu.
        }

        /// <summary>
        /// The players of a skirmish request: one per open slot, ascending slot, named "Player" for the human and "AI n" for AI slots
        /// (text only). Also used by the setup screen to check a request before starting it.
        /// </summary>
        /// <param name="request">A skirmish request.</param>
        public static List<PlayerSetup> SkirmishPlayers(MatchRequest request)
        {
            List<PlayerSetup> players = new List<PlayerSetup>();
            foreach (MatchSlot slot in request.Slots)
            {
                if (slot.Kind == SlotKind.Closed)
                {
                    continue;
                }
                string name = slot.Kind == SlotKind.Human ? "Player" : "AI " + (slot.Slot + 1);
                players.Add(new PlayerSetup(new PlayerId((byte)slot.Slot), new TeamId((byte)slot.Team), slot.FactionId, name));
            }
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            return players;
        }

        /// <summary>
        /// Builds, starts and records one match (player_interface_2.md UI-12, API order): players, start commands (StandardMatchStart for a
        /// skirmish, the script's for Script), simulation, recorder, commands through Submit, then the AI controllers. A failure is logged and
        /// kept in StartError; the host stays idle.
        /// </summary>
        /// <param name="request">The match to start.</param>
        public void StartMatch(MatchRequest request)
        {
            if (request.Kind == MatchKind.Replay)
            {
                StartPlayback(request);
                return;
            }
            if (request.Kind == MatchKind.Mission)
            {
                StartMission(request);
                return;
            }
            try
            {
                IReadOnlyList<PlayerSetup> players;
                MapDefinition map;
                ulong seed;
                List<Command> commands;
                // AI profile ID per player slot, ascending slot.
                SortedDictionary<int, string> aiSlots = new SortedDictionary<int, string>();
                // Text for the start log line.
                string description;
                if (request.Kind == MatchKind.Script)
                {
                    // The command script: players, seed, tick count and every command. Error messages name its path and field.
                    CommandScript script = ReadScript(_dataDirectory);
                    _scriptTicks = script.Ticks;
                    // Seed: the script's, unless the Inspector override is 0 or more.
                    seed = matchSeed >= 0 ? (ulong)matchSeed : script.Seed;
                    // Map named by the script, or the first map in ordinal order.
                    map = Data.RequireMap(script.MapId);
                    players = script.Players;
                    commands = new List<Command>(script.Commands);
                    foreach (KeyValuePair<PlayerId, string> pair in script.AiProfiles)
                    {
                        aiSlots[pair.Key.Value] = pair.Value;
                    }
                    description = "script " + scriptRelativePath + " (" + script.Commands.Count + " commands, " + script.Players.Count + " players, " + _scriptTicks + " ticks)";
                }
                else if (request.Kind == MatchKind.Skirmish)
                {
                    map = Data.RequireMap(request.MapId);
                    players = SkirmishPlayers(request);
                    // Throws InvalidOperationException (no spawn, no HQ, too few worker tiles) before anything is built.
                    commands = StandardMatchStart.Commands(Data, map, players);
                    // No tick limit: the match runs until it is decided or the player leaves.
                    _scriptTicks = int.MaxValue;
                    seed = (ulong)request.Seed;
                    foreach (MatchSlot slot in request.Slots)
                    {
                        if (slot.Kind == SlotKind.Ai)
                        {
                            aiSlots[slot.Slot] = slot.ProfileId;
                        }
                        else if (slot.Kind == SlotKind.Human)
                        {
                            LocalSlotOverride = slot.Slot;
                            FixVec2 spawn = map.Spawns[slot.Slot];
                            StartFocus = new Vector2(spawn.X.ToFloatForPresentation(), spawn.Y.ToFloatForPresentation());
                        }
                    }
                    description = "skirmish on " + request.MapId + " (" + players.Count + " players, " + aiSlots.Count + " AI)";
                }
                else
                {
                    throw new InvalidOperationException("Unknown match kind " + request.Kind + ".");
                }
                // Controllers are made before the simulation, so a profile that fails to parse stops the start cleanly. Ascending slot.
                List<IPlayerController> controllers = new List<IPlayerController>();
                foreach (KeyValuePair<int, string> pair in aiSlots)
                {
                    controllers.Add(new SkirmishAiController(new PlayerId((byte)pair.Key), AiProfile.FromDataSet(Data, pair.Value)));
                }
                _scriptFinished = false;

                // Core settings: tick rate and hash interval from data/tuning/match.json.
                MatchSettings settings = Data.CreateMatchSettings(seed);
                SimulationConfig config = new SimulationConfig(settings, Data.Content, players, BuiltInMechanics.CreateRegistry(), Data.Tuning, map,
                    Data.Cycles.Values);
                _sim = new Simulation(config);
                _sim.Start();
                if (recordSession)
                {
                    // Attached before any command is queued: the recorder refuses a simulation that already holds commands.
                    _recorder = new ReplayRecorder(_sim, players, BuiltInMechanics.CreateCodecRegistry());
                }

                _tickSeconds = 1f / settings.TicksPerSecond;
                _unitRoot = new GameObject("Simulation Units").transform;

                // Every start tick is known now, so submit the whole stream before the first Step (the clock is at 0,
                // so Submit accepts every tick >= 0). The simulation orders commands by (tick, issuer, sequence) itself.
                foreach (Command command in commands)
                {
                    if (command.Tick >= _scriptTicks && !continueAfterScript)
                    {
                        Debug.LogWarning("[SimulationHost] " + command + " is scheduled at or after the last tick (" + _scriptTicks + ") and will not run.");
                    }
                    Submit(command);
                }
                _controllers.AddRange(controllers);
                foreach (IPlayerController controller in controllers)
                {
                    _aiPlayers.Add(controller.Player);
                }
                AiHost.StartAll(_sim, _controllers);
                ProcessEvents();
                if (_scriptTicks == 0)
                {
                    // A script that asks for zero ticks never steps.
                    _scriptFinished = true;
                    Debug.Log("[SimulationHost] script finished at tick 0, final hash " + _sim.ComputeStateHash().ToString("x16"));
                }
                CurrentRequest = request;
                StartError = null;
                Debug.Log("[SimulationHost] Match started from " + _dataDirectory + ": " + description + ", " + settings.TicksPerSecond
                    + " ticks per second, seed " + settings.Seed + ".");
            }
            catch (Exception exception)
            {
                StartError = exception.Message;
                Debug.LogError("[SimulationHost] Could not start the match: " + exception.Message);
                Debug.LogException(exception);
                _sim = null;
                _recorder = null;
                _controllers.Clear();
                _aiPlayers.Clear();
                LocalSlotOverride = null;
                StartFocus = null;
            }
        }

        /// <summary>
        /// Starts playing a replay file: reads it, builds a ReplayPlayback (which refuses other data, map or hash interval with a
        /// ReplayRefusedException), and shows it from the first player's view with fog off. No recorder, no AI. A failure is kept in StartError.
        /// </summary>
        /// <param name="request">A Replay request with its file path.</param>
        private void StartPlayback(MatchRequest request)
        {
            try
            {
                ReplayData replay = ReplayReader.Read(File.ReadAllBytes(request.ReplayPath), BuiltInMechanics.CreateCodecRegistry());
                ReplayPlayback playback = new ReplayPlayback(replay, Data);
                _playback = playback;
                PlaybackData = replay;
                _sim = playback.Sim;
                _recorder = null;
                _scriptTicks = int.MaxValue;
                _scriptFinished = false;
                PlaybackSpeed = 1f;
                LocalSlotOverride = replay.Players.Count > 0 ? replay.Players[0].Id.Value : 0;
                FogView.Observer = true;
                _tickSeconds = 1f / _sim.Clock.TicksPerSecond;
                _unitRoot = new GameObject("Simulation Units").transform;
                ProcessEvents();
                CurrentRequest = request;
                StartError = null;
                Debug.Log("[SimulationHost] Replay started: " + Path.GetFileName(request.ReplayPath) + " on " + replay.MapId + ", " + replay.TickCount + " ticks, "
                    + replay.Players.Count + " players.");
            }
            catch (Exception exception) when (exception is ReplayException || exception is IOException || exception is InvalidOperationException || exception is DataValidationException)
            {
                StartError = exception.Message;
                Debug.LogError("[SimulationHost] Could not play the replay: " + exception.Message);
                _playback = null;
                PlaybackData = null;
                _sim = null;
                LocalSlotOverride = null;
                FogView.Observer = false;
            }
        }

        /// <summary>
        /// Starts a campaign mission in the order of player_interface_3.md section 3: parse, single-player check, AI controllers, simulation
        /// from the mission, recorder, runner (setup commands through Submit), AI start, local slot and camera start. A failure is kept in StartError.
        /// </summary>
        /// <param name="request">A Mission request with its mission ID (file name without .json).</param>
        private void StartMission(MatchRequest request)
        {
            try
            {
                // The mission file and its parse; a DataValidationException names the file and field.
                string path = Path.Combine(CampaignDirectory, "missions", request.MissionId + ".json");
                MissionDefinition mission = MissionLoader.Parse(File.ReadAllText(path), "missions/" + request.MissionId + ".json", Data, BuiltInMechanics.ScriptParsers);
                // The one human slot. Range: a slot of the mission.
                MissionPlayer human = null;
                // Human slots in the mission. Range: exactly 1 to start.
                int humans = 0;
                foreach (MissionPlayer player in mission.Players)
                {
                    if (player.IsHuman)
                    {
                        human = player;
                        humans++;
                    }
                }
                if (humans != 1)
                {
                    throw new InvalidOperationException("Missions are single-player only (" + mission.Id + " has " + humans + " human slots).");
                }
                MissionInputs inputs = null;
                if (playMissionInputs)
                {
                    string inputsPath = Path.Combine(CampaignDirectory, "inputs", request.MissionId + ".json");
                    inputs = MissionInputs.Parse(File.ReadAllText(inputsPath), "inputs/" + request.MissionId + ".json", mission, Data);
                }
                // AI controllers of "ai:" slots, ascending slot (same as script mode).
                List<IPlayerController> controllers = new List<IPlayerController>();
                foreach (MissionPlayer player in mission.Players)
                {
                    if (player.AiProfile != null)
                    {
                        controllers.Add(new SkirmishAiController(player.Setup.Id, AiProfile.FromDataSet(Data, player.AiProfile)));
                    }
                }
                controllers.Sort((a, b) => a.Player.CompareTo(b.Player));
                MapDefinition map = Data.RequireMap(mission.MapId);

                _sim = mission.CreateSimulation(Data);
                _sim.Start();
                if (recordSession)
                {
                    // Before the runner submits its setup commands: the recorder refuses a simulation that already holds commands.
                    _recorder = new ReplayRecorder(_sim, mission.PlayerSetups(), BuiltInMechanics.CreateCodecRegistry());
                }
                _cues.Cues.Clear();
                _mission = new MissionRunner(mission, _cues, Submit);
                _mission.Start(_sim);
                _inputs = inputs;
                _controllers.AddRange(controllers);
                foreach (IPlayerController controller in controllers)
                {
                    _aiPlayers.Add(controller.Player);
                }
                AiHost.StartAll(_sim, _controllers);
                LocalSlotOverride = human.Setup.Id.Value;
                FixVec2 spawn = map.Spawns[human.Setup.Id.Value];
                StartFocus = new Vector2(spawn.X.ToFloatForPresentation(), spawn.Y.ToFloatForPresentation());
                // No script tick limit: the mission's max_seconds ends it.
                _scriptTicks = int.MaxValue;
                _scriptFinished = false;
                _lastFocus = null;
                _lastSelection = null;
                _tickSeconds = 1f / _sim.Clock.TicksPerSecond;
                _unitRoot = new GameObject("Simulation Units").transform;
                ProcessEvents();
                CurrentRequest = request;
                StartError = null;
                Debug.Log("[SimulationHost] Mission started: " + mission.Id + " (" + mission.DisplayName + ") on " + mission.MapId + ", seed " + mission.Seed
                    + (inputs != null ? ", playing its scripted inputs." : "."));
            }
            catch (Exception exception)
            {
                StartError = exception.Message;
                Debug.LogError("[SimulationHost] Could not start the mission: " + exception.Message);
                _sim = null;
                _recorder = null;
                _mission = null;
                _inputs = null;
                _controllers.Clear();
                _aiPlayers.Clear();
                LocalSlotOverride = null;
                StartFocus = null;
            }
        }

        /// <summary>
        /// Reports the camera focus and the selection to the mission when they changed since the last report, or both when the last step
        /// activated an objective (so an earlier selection still counts). At most one signal of each kind per step; called only from StepOnce.
        /// </summary>
        private void ReportMissionSignals()
        {
            if (_camera == null)
            {
                _camera = FindAnyObjectByType<RtsCameraController>();
            }
            if (_selection == null)
            {
                _selection = FindAnyObjectByType<SelectionController>();
            }
            if (_camera == null || _selection == null)
            {
                return;
            }
            // True when an objective started counting on this tick (activated by the last AfterStep, or at Start for tick 0).
            bool activated = false;
            foreach (ObjectiveState objective in _mission.Objectives)
            {
                if (objective.Status == ObjectiveStatus.Active && objective.FirstCountingTick == _sim.Clock.Tick)
                {
                    activated = true;
                }
            }
            // Camera focus in tiles through integer cells, as every other sim point from Unity.
            FixVec2 focus = _selection.ToSimPoint(new Vector3(_camera.FocusTiles.x, 0f, _camera.FocusTiles.y));
            if (activated || _lastFocus == null || _lastFocus.Value.X != focus.X || _lastFocus.Value.Y != focus.Y)
            {
                _mission.ReportSignal(new CameraMovedSignal(focus.X, focus.Y));
                _lastFocus = focus;
            }
            // The selection, ascending entity ID.
            List<EntityId> selection = new List<EntityId>(_selection.Selected);
            selection.Sort();
            bool changed = _lastSelection == null || _lastSelection.Count != selection.Count;
            for (int i = 0; !changed && i < selection.Count; i++)
            {
                changed = selection[i] != _lastSelection[i];
            }
            if (activated || changed)
            {
                _mission.ReportSignal(new SelectionChangedSignal(selection));
                _lastSelection = selection;
            }
        }

        /// <summary>The AI controllers' sink: schedules for the current tick and submits through the host (and so through the recorder).</summary>
        private sealed class HostCommandSink : ICommandSink
        {
            /// <summary>The host the commands go to. Range: non-null.</summary>
            private readonly SimulationHost _host;

            /// <summary>Creates the sink for a host.</summary>
            /// <param name="host">The host.</param>
            public HostCommandSink(SimulationHost host)
            {
                _host = host;
            }

            /// <summary>The tick the AI schedules for: the current clock tick, which the next step applies. Unit: ticks.</summary>
            public int ScheduleTick => _host._sim.Clock.Tick;

            /// <summary>Queues and records an AI command; a command the codec cannot encode is logged, not thrown into the step loop.</summary>
            /// <param name="command">The AI's command.</param>
            public void Submit(Command command)
            {
                try
                {
                    _host.Submit(command);
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    Debug.LogWarning("[SimulationHost] AI command refused at submission " + command + ": " + exception.Message);
                }
            }
        }

        /// <summary>Turns elapsed real time into whole ticks, at most maxStepsPerFrame of them.</summary>
        private void Update()
        {
            if (_sim == null)
            {
                return;
            }
            if (!ShouldStep)
            {
                // The script is over and continueAfterScript is off: nothing steps, the cubes rest on the last tick.
                _alpha = 1f;
                return;
            }
            // Real time since the previous frame, times the replay speed in playback; zero while Time.timeScale is 0, which pauses the match.
            _accumulatorSeconds += Time.deltaTime * (IsPlayback ? PlaybackSpeed : 1f);
            // Ticks stepped this frame.
            int steps = 0;
            while (_accumulatorSeconds >= _tickSeconds && steps < maxStepsPerFrame && ShouldStep)
            {
                StepOnce();
                _accumulatorSeconds -= _tickSeconds;
                steps++;
            }
            if (!ShouldStep)
            {
                _alpha = 1f;
                return;
            }
            if (_accumulatorSeconds >= _tickSeconds)
            {
                // Hit the cap with time left over: drop whole ticks, keep the fractional part so motion stays smooth.
                _accumulatorSeconds = Mathf.Repeat(_accumulatorSeconds, _tickSeconds);
            }
            _alpha = Mathf.Clamp01(_accumulatorSeconds / _tickSeconds);
        }

        /// <summary>True while ticks should still run: the script has not finished, or continueAfterScript keeps the match going. Unit: on/off.</summary>
        private bool ShouldStep => IsPlayback ? !_playback.IsFinished
            : _mission != null ? _mission.Status == MissionStatus.Running
            : _outcome == null && (!_scriptFinished || continueAfterScript);

        /// <summary>Positions every cube between its previous and current tick position. Runs after all Update calls.</summary>
        private void LateUpdate()
        {
            foreach (UnitView view in _views.Values)
            {
                view.Transform.position = Vector3.Lerp(view.Previous, view.Current, _alpha);
            }
        }

        /// <summary>
        /// Queues a command for its tick, through the session recorder when recording. The only way commands enter the match; player
        /// input reaches it through LocalCommands.Submit. Throws InvalidOperationException for a tick in the past and ArgumentException
        /// for a command the replay codec cannot encode.
        /// </summary>
        /// <param name="command">The command; not null.</param>
        public void Submit(Command command)
        {
            if (_recorder != null)
            {
                _recorder.Submit(command);
            }
            else
            {
                _sim.Submit(command);
            }
        }

        /// <summary>Writes the recorded session, then releases the cubes, materials and simulation reference.</summary>
        private void OnDestroy()
        {
            WriteReplay();
            foreach (Material material in _playerMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _playerMaterials.Clear();
            _views.Clear();
            EventRaised = null;
            if (_unitRoot != null)
            {
                Destroy(_unitRoot.gameObject);
            }
            _sim = null;
        }

        /// <summary>Runs exactly one simulation tick and updates the picture state from it.</summary>
        private void StepOnce()
        {
            if (_mission != null)
            {
                // Step (a) of player_interface_3.md section 3: the scripted inputs, or Unity's camera and selection.
                if (_inputs != null)
                {
                    _inputs.BeforeStep(_sim, _mission, Submit);
                }
                else
                {
                    ReportMissionSignals();
                }
            }
            // Each AI thinks exactly once before every stepped tick, ascending player (player_interface_2.md rule 2).
            foreach (IPlayerController controller in _controllers)
            {
                controller.Think(_sim, _aiSink);
            }
            if (_playback != null)
            {
                _playback.Step();
            }
            else if (_recorder != null)
            {
                _recorder.Step();
            }
            else
            {
                _sim.Step();
            }
            // The mission reads the step's events exactly once, before the views (step d).
            _mission?.AfterStep(_sim);
            // Shift every existing cube's blend window forward before new events add or remove cubes.
            RefreshViewStates();
            ProcessEvents();
            if (_mission != null)
            {
                // A one-team match is "decided" from tick 0, so a mission stops only on its own status (rule 4).
                if (_mission.Status != MissionStatus.Running)
                {
                    Debug.Log("[SimulationHost] mission " + _mission.Status + " at tick " + _mission.EndTick + (_mission.EndReason.Length > 0 ? " (" + _mission.EndReason + ")" : "") + ", final hash "
                        + _sim.ComputeStateHash().ToString("x16"));
                    WriteReplay();
                }
                return;
            }
            if (!_scriptFinished && _sim.Clock.Tick >= _scriptTicks)
            {
                // The clock counts ticks already stepped, so this is the state after exactly the script's tick count,
                // the same state the headless harness prints as STATE_HASH.
                _scriptFinished = true;
                Debug.Log("[SimulationHost] script finished at tick " + _sim.Clock.Tick + ", final hash " + _sim.ComputeStateHash().ToString("x16"));
            }
            // As the headless harness does: after each step, stop once one team or none is left standing, and write the replay then.
            // A replay stops at its own last tick instead.
            MatchOutcome outcome = MatchOutcome.Evaluate(_sim.Players);
            if (!IsPlayback && outcome.IsDecided)
            {
                _outcome = outcome;
                Debug.Log("[SimulationHost] match decided at tick " + _sim.Clock.Tick + ": " + outcome + ", final hash " + _sim.ComputeStateHash().ToString("x16"));
                WriteReplay();
            }
        }

        /// <summary>
        /// Writes the recorded session to a replay file named after the local time. Does nothing when nothing was recorded or no tick ran.
        /// Failures are logged; they never stop the editor leaving Play Mode.
        /// </summary>
        private void WriteReplay()
        {
            if (_recorder == null || _sim == null || _sim.Clock.Tick == 0)
            {
                return;
            }
            try
            {
                // Replay file path in the shared replay folder; the wall clock only names the file, never feeds the simulation.
                string path = ReplayFiles.NewPath();
                File.WriteAllBytes(path, ReplayWriter.Write(_recorder.Finish(), BuiltInMechanics.CreateCodecRegistry()));
                Debug.Log("[SimulationHost] Replay written: " + path + " (" + _sim.Clock.Tick + " ticks, " + _recorder.CommandCount + " commands).");
            }
            catch (Exception exception)
            {
                Debug.LogError("[SimulationHost] Could not write the replay: " + exception.Message);
            }
            _recorder = null;
        }

        /// <summary>For each cube: previous := current, current := the unit's position after the tick that just ran.</summary>
        private void RefreshViewStates()
        {
            foreach (UnitView view in _views.Values)
            {
                if (_sim.Units.TryGet(view.Id, out Unit unit))
                {
                    view.Previous = view.Current;
                    view.Current = ToWorld(unit.Position, view.Height);
                }
            }
        }

        /// <summary>Reads the events of the tick that just ran: spawns, removals, hashes, rejections, layer changes (grounding), wave pushes and quake relocations.</summary>
        private void ProcessEvents()
        {
            // Events of the latest tick, oldest first; valid until the next Step.
            IReadOnlyList<SimEvent> events = _sim.Events.Current;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent simEvent = events[i];
                NotifyListeners(simEvent);
                if (simEvent is UnitSpawnedEvent spawned)
                {
                    CreateView(spawned);
                }
                else if (simEvent is UnitRemovedEvent removed)
                {
                    DestroyView(removed.Unit);
                }
                else if (simEvent is StateHashedEvent hashed)
                {
                    if (logStateHashes)
                    {
                        Debug.Log("tick=" + hashed.Tick + " hash=" + hashed.Hash.ToString("x16"));
                    }
                }
                else if (simEvent is CommandRejectedEvent rejected)
                {
                    if (logAiRejections || !_aiPlayers.Contains(rejected.Command.Issuer))
                    {
                        Debug.LogWarning("[SimulationHost] " + rejected.Describe());
                    }
                }
                else if (simEvent is UnitLayerChangedEvent layerChanged)
                {
                    // A grounded flyer drops to ground height (and rises again when the status ends), without a blend.
                    SnapView(layerChanged.Unit, layerChanged.To == MovementLayer.Air ? AirHeight : GroundHeight);
                }
                else if (simEvent is UnitRelocatedEvent relocated)
                {
                    // A quake relocation is instant, so the cube jumps instead of sliding.
                    if (_views.TryGetValue(relocated.Unit, out UnitView relocatedView))
                    {
                        SnapView(relocated.Unit, relocatedView.Height);
                    }
                }
                else if (simEvent is UnitPushedEvent pushed)
                {
                    // A wave push is instant in the simulation, so the cube jumps instead of sliding.
                    if (_views.TryGetValue(pushed.Unit, out UnitView pushedView))
                    {
                        SnapView(pushed.Unit, pushedView.Height);
                    }
                }
            }
        }

        /// <summary>Puts a unit's cube at its current simulation position with a new height above ground, with no blend from the previous tick.</summary>
        /// <param name="id">The unit.</param>
        /// <param name="height">Cube centre height above the terrain. Unit: world units. Range: GroundHeight or AirHeight.</param>
        private void SnapView(EntityId id, float height)
        {
            if (_views.TryGetValue(id, out UnitView view) && _sim.Units.TryGet(id, out Unit unit))
            {
                view.Height = height;
                view.Current = ToWorld(unit.Position, height);
                view.Previous = view.Current;
            }
        }

        /// <summary>Hands one event to the EventRaised listeners; a throwing listener is logged and skipped so it cannot break the tick.</summary>
        /// <param name="simEvent">Event of the tick that just ran.</param>
        private void NotifyListeners(SimEvent simEvent)
        {
            Action<SimEvent> handlers = EventRaised;
            if (handlers == null)
            {
                return;
            }
            try
            {
                handlers(simEvent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>Makes the cube for a newly spawned unit, tinted for its owner and sized from its radius.</summary>
        /// <param name="spawned">The spawn event of the tick that just ran.</param>
        private void CreateView(UnitSpawnedEvent spawned)
        {
            if (_views.ContainsKey(spawned.Unit))
            {
                return;
            }
            // The live unit; missing when it was spawned and removed within the same tick, in which case nothing is drawn.
            if (!_sim.Units.TryGet(spawned.Unit, out Unit unit))
            {
                return;
            }
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = spawned.DefinitionId + " " + spawned.Unit;
            cube.transform.SetParent(_unitRoot, false);

            // Presentation only: cubes need no physics, so drop the collider the primitive comes with.
            Collider boxCollider = cube.GetComponent<Collider>();
            if (boxCollider != null)
            {
                Destroy(boxCollider);
            }

            // Cube edge length. Unit: world units (tiles). Range: minimumCubeSize upward.
            float size = Mathf.Max(minimumCubeSize, unit.Definition.Radius.ToFloatForPresentation() * cubeScalePerRadius);
            cube.transform.localScale = new Vector3(size, size, size);

            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (_templateMaterial == null)
            {
                _templateMaterial = cubeRenderer.sharedMaterial;
            }
            cubeRenderer.sharedMaterial = GetPlayerMaterial(spawned.Owner);

            // Height by movement layer: air units float, everything else sits on the ground.
            float height = unit.Definition.Layer == MovementLayer.Air ? AirHeight : GroundHeight;
            UnitView view = new UnitView
            {
                Id = spawned.Unit,
                Transform = cube.transform,
                Height = height,
                Previous = ToWorld(spawned.Position, height),
                Current = ToWorld(unit.Position, height),
            };
            cube.transform.position = view.Previous;
            _views.Add(spawned.Unit, view);
        }

        /// <summary>Destroys the cube of a removed unit, if there is one.</summary>
        /// <param name="id">The removed unit.</param>
        private void DestroyView(EntityId id)
        {
            if (_views.TryGetValue(id, out UnitView view))
            {
                Destroy(view.Transform.gameObject);
                _views.Remove(id);
            }
        }

        /// <summary>The colour that marks a player everywhere (cubes, Core ring, HUD): slot 0 blue, 1 red, then the fallbacks; grey for no owner.</summary>
        /// <param name="owner">A player, or None.</param>
        public static Color ColorOf(PlayerId owner)
        {
            return owner.IsValid ? PlayerColors[owner.Value % PlayerColors.Length] : NeutralColor;
        }

        /// <summary>The shared material for a player slot, created on first use.</summary>
        /// <param name="owner">Owning player, or None for neutral.</param>
        private Material GetPlayerMaterial(PlayerId owner)
        {
            // Dictionary key: the player slot, 255 for neutral.
            byte key = owner.Value;
            if (!_playerMaterials.TryGetValue(key, out Material material))
            {
                material = new Material(_templateMaterial);
                material.name = "Player " + key;
                material.color = ColorOf(owner);
                _playerMaterials.Add(key, material);
            }
            return material;
        }

        /// <summary>
        /// Converts a simulation position to Unity world space. Simulation X is world X, simulation Y is world Z
        /// (the ground plane is X/Z in Unity), one tile is one world unit, and world Y is the elevation of the tile
        /// under the position (layer x layerHeightWorldUnits) plus the given offset. One-way, presentation only.
        /// </summary>
        /// <param name="position">Simulation position in tiles.</param>
        /// <param name="heightAboveGround">Height of the cube centre above the terrain surface, in world units.</param>
        private Vector3 ToWorld(FixVec2 position, float heightAboveGround)
        {
            // Elevation layer of the tile under the position. Unit: layer index. Range: 0 to 7.
            int layer = _sim.Terrain[_sim.Terrain.TileOf(position)].Height;
            return new Vector3(position.X.ToFloatForPresentation(), layer * layerHeightWorldUnits + heightAboveGround, position.Y.ToFloatForPresentation());
        }

        /// <summary>
        /// Reads and parses the command script at dataDirectory + scriptRelativePath. Throws with a message naming the
        /// path when the file is missing and a DataValidationException naming file and field when it is invalid.
        /// </summary>
        /// <param name="dataDirectory">Existing data folder the script path is relative to.</param>
        private CommandScript ReadScript(string dataDirectory)
        {
            // Absolute path of the script file.
            string path = Path.Combine(dataDirectory, scriptRelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Command script not found: " + path);
            }
            return CommandScript.Parse(File.ReadAllText(path), scriptRelativePath.Replace('\\', '/'), BuiltInMechanics.ScriptParsers);
        }

        /// <summary>Picks the data folder: Inspector override, else the repo's rts/data in the editor, else StreamingAssets/data.</summary>
        private string ResolveDataDirectory()
        {
            if (!string.IsNullOrEmpty(dataDirectoryOverride))
            {
                return dataDirectoryOverride;
            }
#if UNITY_EDITOR
            // rts/data as seen from rts/src/Game/Assets: three levels up, then data.
            string repoData = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "data"));
            if (Directory.Exists(repoData))
            {
                return repoData;
            }
#endif
            return Path.Combine(Application.streamingAssetsPath, "data");
        }

        /// <summary>
        /// Reads every *.json under a folder into DataSourceFile objects whose path is relative to that folder with
        /// forward slashes ("units/skai/windrunner.json"), in ordinal path order. The loader never touches the disk.
        /// </summary>
        /// <param name="root">Existing data folder.</param>
        private static List<DataSourceFile> ReadDataFiles(string root)
        {
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("Data directory not found: " + root);
            }
            // Root without a trailing separator, so a substring after it is the relative path.
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] paths = Directory.GetFiles(fullRoot, "*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.Ordinal);
            List<DataSourceFile> files = new List<DataSourceFile>(paths.Length);
            foreach (string path in paths)
            {
                // Path below the data root, always with '/' separators.
                string relative = path.Substring(fullRoot.Length + 1).Replace('\\', '/');
                files.Add(new DataSourceFile(relative, File.ReadAllText(path)));
            }
            if (files.Count == 0)
            {
                throw new InvalidOperationException("No .json files under " + root);
            }
            return files;
        }
    }
}
