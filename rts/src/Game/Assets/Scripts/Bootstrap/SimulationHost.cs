// rts/src/Game/Assets/Scripts/Bootstrap/SimulationHost.cs
// The Phase 0 bridge between Unity and the engine-free simulation (assembly Rts.Sim). It owns one Simulation,
// feeds it scripted commands through Simulation.Submit, steps it on a fixed accumulator, and draws every unit
// as a placeholder cube positioned from Unit.Position. Part of Rts.Game (Unity side, presentation only).
//
// Rules this file follows (docs/decisions and the architecture rules):
//   - Unity only renders. The simulation is never written to except through Simulation.Submit (rule 2); no
//     Unit, FixVec2 or Fix64 value is ever modified from here, and nothing read back from a Transform, float
//     or Time value flows into a command. Fix64 -> float conversion goes one way only (ToFloatForPresentation).
//   - Wall-clock time (Time.deltaTime) only decides HOW MANY ticks run this frame, never what a tick does.
//   - Rendering interpolates between the previous and current tick state (ADR-004), one tick behind the sim.
//
// Data directory (where every *.json under it is read and handed to Rts.Sim.Data.DataLoader):
//   Editor:  <repo>/rts/data, resolved as Application.dataPath + "/../../../data" (dataPath is
//            rts/src/Game/Assets). No copy step is needed and the editor always sees the committed data.
//   Players: Application.streamingAssetsPath + "/data", so the build must contain a copy of rts/data in
//            Assets/StreamingAssets/data (a build-time copy step is a Phase 1 follow-up; see docs/UNITY_SETUP.md).
//   The Inspector field "Data Directory Override" beats both when it is not empty.
//
// Scripted commands (all issuer PlayerId.None with one running sequence number, so their order is fixed):
//   tick 0:               spawn four units, one skai.windrunner and one water.spoutling per player (table in BuildScript).
//   tick 0 + TicksPerSecond (one second later): move each spawned unit to its table target.
// The same table must be used by the headless smoke script for the state hashes to be comparable.
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
using UnityEngine;

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
        /// <summary>One scripted unit: where it spawns, where it is later sent, and the ID the simulation gave it.</summary>
        private sealed class ScriptedUnit
        {
            /// <summary>Owning player slot. Unit: player ID. Range: 0 to 254 (this script uses 0 and 1).</summary>
            public readonly byte Player;

            /// <summary>Unit definition ID from data/units, for example "skai.windrunner".</summary>
            public readonly string DefinitionId;

            /// <summary>Spawn tile X. Unit: tiles. Range: 0 to 63 on the 64x64 greybox map.</summary>
            public readonly int SpawnX;

            /// <summary>Spawn tile Y (world Z in Unity). Unit: tiles. Range: 0 to 63.</summary>
            public readonly int SpawnY;

            /// <summary>Move target tile X. Unit: tiles. Range: 0 to 63.</summary>
            public readonly int TargetX;

            /// <summary>Move target tile Y (world Z in Unity). Unit: tiles. Range: 0 to 63.</summary>
            public readonly int TargetY;

            /// <summary>True once the matching UnitSpawnedEvent has been seen and the move order submitted.</summary>
            public bool MoveSubmitted;

            /// <summary>Builds one table row.</summary>
            public ScriptedUnit(byte player, string definitionId, int spawnX, int spawnY, int targetX, int targetY)
            {
                Player = player;
                DefinitionId = definitionId;
                SpawnX = spawnX;
                SpawnY = spawnY;
                TargetX = targetX;
                TargetY = targetY;
                MoveSubmitted = false;
            }
        }

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

            /// <summary>Cube centre height. Unit: world units. Range: 0.5 (ground and water) or 2 (air).</summary>
            public float Height;
        }

        /// <summary>Seed for the simulation RNG. Unit: none. Range: 0 to 2147483647. 1234 matches the headless smoke script.</summary>
        [SerializeField, Min(0)] private int matchSeed = 1234;

        /// <summary>
        /// Most ticks stepped in one rendered frame. Unit: ticks per frame. Range: 1 to 60; default 10. A slow frame
        /// (editor pause, breakpoint, hitch) would otherwise ask for a huge catch-up and every catch-up frame would be
        /// slower still (spiral of death). Time beyond the cap is dropped, so the match runs slower than real time
        /// while the machine cannot keep up.
        /// </summary>
        [SerializeField, Range(1, 60)] private int maxStepsPerFrame = 10;

        /// <summary>Log every StateHashedEvent to the Console as "tick=N hash=XXXXXXXXXXXXXXXX". Unit: on/off.</summary>
        [SerializeField] private bool logStateHashes = true;

        /// <summary>Absolute path of a data directory to use instead of the default lookup. Unit: file path. Range: empty (default lookup) or an existing folder.</summary>
        [SerializeField] private string dataDirectoryOverride = "";

        /// <summary>Cube edge length per unit of Definition.Radius. Unit: world units per tile of radius. Range: 0.1 to 4; 2 makes the cube as wide as the unit's collision circle.</summary>
        [SerializeField, Range(0.1f, 4f)] private float cubeScalePerRadius = 2f;

        /// <summary>Smallest cube edge so tiny units stay visible. Unit: world units. Range: 0.1 to 2.</summary>
        [SerializeField, Range(0.1f, 2f)] private float minimumCubeSize = 0.4f;

        /// <summary>Cube centre height for ground and water units. Unit: world units. Range: 0 to 5; 0.5 rests a unit cube on the ground.</summary>
        private const float GroundHeight = 0.5f;

        /// <summary>Cube centre height for air units. Unit: world units. Range: 0 to 10.</summary>
        private const float AirHeight = 2f;

        /// <summary>Whole seconds between a scripted unit spawning and its move order. Unit: seconds. Range: 1 upward; multiplied by the match tick rate to get ticks.</summary>
        private const int ScriptedMoveDelaySeconds = 1;

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

        /// <summary>Seconds of real time not yet turned into ticks. Unit: seconds. Range: 0 up to one tick duration after each Update.</summary>
        private float _accumulatorSeconds;

        /// <summary>Real seconds per tick, 1 / TicksPerSecond. Unit: seconds. Range: 1/60 to 1/10.</summary>
        private float _tickSeconds;

        /// <summary>Fraction of the way from the previous tick to the next, used to blend cube positions. Unit: fraction. Range: 0 to 1.</summary>
        private float _alpha;

        /// <summary>Next command sequence number for issuer None. Unit: count. Range: 0 upward; never reused.</summary>
        private uint _nextSequence;

        /// <summary>The scripted spawn/move table. Filled by BuildScript.</summary>
        private readonly List<ScriptedUnit> _script = new List<ScriptedUnit>();

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

        /// <summary>How far between the last two ticks the picture currently is. Unit: fraction. Range: 0 to 1.</summary>
        public float InterpolationAlpha => _alpha;

        /// <summary>Loads data, builds the simulation, starts it and queues the scripted spawns. Disables itself on failure.</summary>
        private void Awake()
        {
            try
            {
                // Folder holding the data tree (tuning/, units/, factions/, ...).
                string dataDirectory = ResolveDataDirectory();
                List<DataSourceFile> files = ReadDataFiles(dataDirectory);
                DataSet dataSet = DataLoader.Load(files);

                // Core settings: tick rate and hash interval from data/tuning/match.json, seed from the Inspector.
                MatchSettings settings = dataSet.CreateMatchSettings((ulong)Mathf.Max(0, matchSeed));

                // Two players on two teams: player 0 plays skai, player 1 plays water.
                PlayerSetup[] players =
                {
                    new PlayerSetup(new PlayerId(0), new TeamId(0), "skai", "Skai"),
                    new PlayerSetup(new PlayerId(1), new TeamId(1), "water", "Water"),
                };

                SimulationConfig config = new SimulationConfig(settings, dataSet.Content, players, BuiltInMechanics.CreateRegistry());
                _sim = new Simulation(config);
                _sim.Start();

                _tickSeconds = 1f / settings.TicksPerSecond;
                _unitRoot = new GameObject("Simulation Units").transform;
                BuildScript();
                SubmitScriptedSpawns();
                ProcessEvents();
                Debug.Log("[SimulationHost] Match started from " + dataDirectory + ": " + files.Count + " data files, "
                    + settings.TicksPerSecond + " ticks per second, seed " + settings.Seed + ".");
            }
            catch (Exception exception)
            {
                Debug.LogError("[SimulationHost] Could not start the simulation: " + exception.Message);
                Debug.LogException(exception);
                _sim = null;
                enabled = false;
            }
        }

        /// <summary>Turns elapsed real time into whole ticks, at most maxStepsPerFrame of them.</summary>
        private void Update()
        {
            if (_sim == null)
            {
                return;
            }
            // Real time since the previous frame; zero while Time.timeScale is 0, which pauses the match.
            _accumulatorSeconds += Time.deltaTime;
            // Ticks stepped this frame.
            int steps = 0;
            while (_accumulatorSeconds >= _tickSeconds && steps < maxStepsPerFrame)
            {
                StepOnce();
                _accumulatorSeconds -= _tickSeconds;
                steps++;
            }
            if (_accumulatorSeconds >= _tickSeconds)
            {
                // Hit the cap with time left over: drop whole ticks, keep the fractional part so motion stays smooth.
                _accumulatorSeconds = Mathf.Repeat(_accumulatorSeconds, _tickSeconds);
            }
            _alpha = Mathf.Clamp01(_accumulatorSeconds / _tickSeconds);
        }

        /// <summary>Positions every cube between its previous and current tick position. Runs after all Update calls.</summary>
        private void LateUpdate()
        {
            foreach (UnitView view in _views.Values)
            {
                view.Transform.position = Vector3.Lerp(view.Previous, view.Current, _alpha);
            }
        }

        /// <summary>Releases the cubes, materials and simulation reference.</summary>
        private void OnDestroy()
        {
            foreach (Material material in _playerMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _playerMaterials.Clear();
            _views.Clear();
            if (_unitRoot != null)
            {
                Destroy(_unitRoot.gameObject);
            }
            _sim = null;
        }

        /// <summary>Runs exactly one simulation tick and updates the picture state from it.</summary>
        private void StepOnce()
        {
            _sim.Step();
            // Shift every existing cube's blend window forward before new events add or remove cubes.
            RefreshViewStates();
            ProcessEvents();
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

        /// <summary>Reads the events of the tick that just ran: spawns, removals, hashes and rejections.</summary>
        private void ProcessEvents()
        {
            // Events of the latest tick, oldest first; valid until the next Step.
            IReadOnlyList<SimEvent> events = _sim.Events.Current;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent simEvent = events[i];
                if (simEvent is UnitSpawnedEvent spawned)
                {
                    CreateView(spawned);
                    SubmitScriptedMove(spawned);
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
                    Debug.LogWarning("[SimulationHost] " + rejected.Describe());
                }
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
                material.color = owner.IsValid ? PlayerColors[key % PlayerColors.Length] : NeutralColor;
                _playerMaterials.Add(key, material);
            }
            return material;
        }

        /// <summary>
        /// Converts a simulation position to Unity world space. Simulation X is world X, simulation Y is world Z
        /// (the ground plane is X/Z in Unity), one tile is one world unit. One-way, presentation only.
        /// </summary>
        /// <param name="position">Simulation position in tiles.</param>
        /// <param name="height">World Y of the cube centre in world units.</param>
        private static Vector3 ToWorld(FixVec2 position, float height)
        {
            return new Vector3(position.X.ToFloatForPresentation(), height, position.Y.ToFloatForPresentation());
        }

        /// <summary>
        /// Fills the scripted table: each player gets one skai.windrunner and one water.spoutling regardless of
        /// faction (the commands are scripted, so faction rules do not apply), spawned in their own corner and sent
        /// across the 64x64 map. Keep this table identical to the headless smoke script for hash comparisons.
        /// </summary>
        private void BuildScript()
        {
            _script.Clear();
            //                          player  definition           spawn   target
            _script.Add(new ScriptedUnit(0, "skai.windrunner", 8, 8, 56, 56));
            _script.Add(new ScriptedUnit(0, "water.spoutling", 10, 8, 32, 32));
            _script.Add(new ScriptedUnit(1, "skai.windrunner", 56, 56, 8, 8));
            _script.Add(new ScriptedUnit(1, "water.spoutling", 54, 56, 34, 32));
        }

        /// <summary>Submits one spawn command per table row for tick 0. IDs come out as E1 to E4 in table order.</summary>
        private void SubmitScriptedSpawns()
        {
            foreach (ScriptedUnit row in _script)
            {
                // Tick the spawn runs on; the clock is at 0 here, and Submit requires tick >= Clock.Tick.
                int tick = _sim.Clock.Tick;
                _sim.Submit(new SpawnUnitCommand(tick, PlayerId.None, _nextSequence++, row.DefinitionId,
                    new PlayerId(row.Player), FixVec2.FromInts(row.SpawnX, row.SpawnY)));
            }
        }

        /// <summary>
        /// When a scripted unit's spawn event arrives, schedules its move order ScriptedMoveDelaySeconds later.
        /// Matches the event to the first table row with the same owner and definition that has no move yet.
        /// </summary>
        /// <param name="spawned">The spawn event just read.</param>
        private void SubmitScriptedMove(UnitSpawnedEvent spawned)
        {
            foreach (ScriptedUnit row in _script)
            {
                if (row.MoveSubmitted || row.Player != spawned.Owner.Value || row.DefinitionId != spawned.DefinitionId)
                {
                    continue;
                }
                row.MoveSubmitted = true;
                // Tick the move runs on: one second after the spawn, never before the current tick.
                int tick = Math.Max(spawned.Tick + _sim.Settings.TicksPerSecond * ScriptedMoveDelaySeconds, _sim.Clock.Tick);
                _sim.Submit(new MoveUnitCommand(tick, PlayerId.None, _nextSequence++, spawned.Unit,
                    FixVec2.FromInts(row.TargetX, row.TargetY)));
                return;
            }
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
