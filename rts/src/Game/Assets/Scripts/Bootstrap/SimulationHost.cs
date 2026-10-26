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

        /// <summary>
        /// Seed override for the simulation RNG. Unit: none. Range: -1 to 2147483647. -1 (default) means "use the script's
        /// seed" (1234 in smoke.json, which is what makes the hashes match the headless runner); 0 or more replaces it.
        /// </summary>
        [SerializeField, Min(-1)] private int matchSeed = -1;

        /// <summary>
        /// Command script to play, relative to the data directory, forward slashes. Unit: file path. Range: an existing
        /// JSON file in the CommandScript format under the data directory; default "scripts/smoke.json", the same file the
        /// headless harness runs by default.
        /// </summary>
        [SerializeField] private string scriptRelativePath = "scripts/smoke.json";

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

        /// <summary>How many ticks the script asks for; stepping stops when the clock reaches it. Unit: ticks. Range: 0 to 1000000 (CommandScript.MaxTicks).</summary>
        private int _scriptTicks;

        /// <summary>True once the script's tick count has been stepped and the final hash logged; no further ticks run. Unit: on/off.</summary>
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

        /// <summary>How far between the last two ticks the picture currently is. Unit: fraction. Range: 0 to 1.</summary>
        public float InterpolationAlpha => _alpha;

        /// <summary>Loads data and the command script, builds the simulation, starts it and queues every scripted command. Disables itself on failure.</summary>
        private void Awake()
        {
            try
            {
                // Folder holding the data tree (tuning/, units/, factions/, ...).
                string dataDirectory = ResolveDataDirectory();
                List<DataSourceFile> files = ReadDataFiles(dataDirectory);
                DataSet dataSet = DataLoader.Load(files);

                // The command script: players, seed, tick count and every command. Error messages name its path and field.
                CommandScript script = ReadScript(dataDirectory);
                _scriptTicks = script.Ticks;
                _scriptFinished = false;

                // Seed: the script's, unless the Inspector override is 0 or more.
                ulong seed = matchSeed >= 0 ? (ulong)matchSeed : script.Seed;

                // Core settings: tick rate and hash interval from data/tuning/match.json.
                MatchSettings settings = dataSet.CreateMatchSettings(seed);

                // Players come from the script, not from code.
                // Map named by the script, or the first map in ordinal order.
                MapDefinition map = dataSet.RequireMap(script.MapId);
                SimulationConfig config = new SimulationConfig(settings, dataSet.Content, script.Players, BuiltInMechanics.CreateRegistry(), dataSet.Tuning, map);
                _sim = new Simulation(config);
                _sim.Start();

                _tickSeconds = 1f / settings.TicksPerSecond;
                _unitRoot = new GameObject("Simulation Units").transform;

                // Every script tick is known now, so submit the whole stream before the first Step (the clock is at 0,
                // so Submit accepts every tick >= 0). The simulation orders commands by (tick, issuer, sequence) itself.
                foreach (Command command in script.Commands)
                {
                    if (command.Tick >= _scriptTicks)
                    {
                        Debug.LogWarning("[SimulationHost] " + command + " is scheduled at or after the last tick (" + _scriptTicks + ") and will not run.");
                    }
                    _sim.Submit(command);
                }
                ProcessEvents();
                if (_scriptTicks == 0)
                {
                    // A script that asks for zero ticks never steps.
                    _scriptFinished = true;
                    Debug.Log("[SimulationHost] script finished at tick 0, final hash " + _sim.ComputeStateHash().ToString("x16"));
                }
                Debug.Log("[SimulationHost] Match started from " + dataDirectory + ": " + files.Count + " data files, script "
                    + scriptRelativePath + " (" + script.Commands.Count + " commands, " + script.Players.Count + " players, "
                    + _scriptTicks + " ticks), " + settings.TicksPerSecond + " ticks per second, seed " + settings.Seed + ".");
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
            if (_scriptFinished)
            {
                // The script is over: nothing steps, the cubes rest on the last tick.
                _alpha = 1f;
                return;
            }
            // Real time since the previous frame; zero while Time.timeScale is 0, which pauses the match.
            _accumulatorSeconds += Time.deltaTime;
            // Ticks stepped this frame.
            int steps = 0;
            while (_accumulatorSeconds >= _tickSeconds && steps < maxStepsPerFrame && !_scriptFinished)
            {
                StepOnce();
                _accumulatorSeconds -= _tickSeconds;
                steps++;
            }
            if (_scriptFinished)
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
            if (_sim.Clock.Tick >= _scriptTicks)
            {
                // The clock counts ticks already stepped, so this is the state after exactly the script's tick count,
                // the same state the headless harness prints as STATE_HASH.
                _scriptFinished = true;
                Debug.Log("[SimulationHost] script finished at tick " + _sim.Clock.Tick + ", final hash " + _sim.ComputeStateHash().ToString("x16"));
            }
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
            return CommandScript.Parse(File.ReadAllText(path), scriptRelativePath.Replace('\\', '/'));
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
