// rts/src/Game/Assets/Scripts/Bootstrap/AutoBootstrap.cs
// Makes pressing Play in ANY scene show the simulation: after the first scene has loaded it adds a
// SimulationHost, a camera, a directional light and a ground plane if the scene does not already have them.
// Part of Rts.Game (Unity side, presentation only). Delete this file once a real entry scene exists; the
// Ensure* helpers are also used by the editor menu item RTS > Create Bootstrap Scene.
//
// Camera: a fixed three-quarter top-down view of the 64x64 greybox map, from (32, 40, -10) looking at the
// map centre (32, 0, 32). Simulation tile (x, y) is Unity world (x, 0, y), one tile = one world unit.
using UnityEngine;

namespace Rts.Game.Bootstrap
{
    /// <summary>Runtime bootstrap. Every Ensure* method is idempotent: it does nothing if the thing already exists.</summary>
    public static class AutoBootstrap
    {
        /// <summary>Camera position. Unit: world units. Range: any; chosen so the whole 64x64 map is in view at 60 degrees field of view.</summary>
        public static readonly Vector3 CameraPosition = new Vector3(32f, 40f, -10f);

        /// <summary>Point the camera looks at: the map centre on the ground. Unit: world units.</summary>
        public static readonly Vector3 MapCentre = new Vector3(32f, 0f, 32f);

        /// <summary>Map edge length. Unit: tiles. Range: 1 upward; 64 is the greybox map size.</summary>
        public const float MapSizeTiles = 64f;

        /// <summary>Name of the ground plane object, used to avoid creating it twice.</summary>
        private const string GroundName = "Greybox Ground";

        /// <summary>Runs once after the first scene has loaded in Play mode (editor and players).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            EnsureHost();
            EnsureCamera();
            EnsureLight();
            EnsureGround();
        }

        /// <summary>Creates the "SimulationHost" object with a SimulationHost component unless one is already in a loaded scene.</summary>
        /// <returns>The existing or new host.</returns>
        public static SimulationHost EnsureHost()
        {
            // An existing host, for example from the saved Bootstrap scene.
            SimulationHost existing = Object.FindFirstObjectByType<SimulationHost>();
            if (existing != null)
            {
                return existing;
            }
            GameObject hostObject = new GameObject("SimulationHost");
            return hostObject.AddComponent<SimulationHost>();
        }

        /// <summary>Creates the three-quarter top-down camera (tagged MainCamera, with an audio listener) unless the scene has any camera.</summary>
        /// <returns>The existing first camera or the new one.</returns>
        public static Camera EnsureCamera()
        {
            Camera existing = Object.FindFirstObjectByType<Camera>();
            if (existing != null)
            {
                return existing;
            }
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            cameraObject.transform.position = CameraPosition;
            cameraObject.transform.LookAt(MapCentre);
            return camera;
        }

        /// <summary>Creates a directional light unless the scene already has one.</summary>
        /// <returns>The existing or new directional light.</returns>
        public static Light EnsureLight()
        {
            // Every light in the loaded scenes; order does not matter.
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light existing in lights)
            {
                if (existing.type == LightType.Directional)
                {
                    return existing;
                }
            }
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return light;
        }

        /// <summary>
        /// Creates a dark ground plane covering the 64x64 map at y = 0 so the cubes have something to sit on.
        /// Runtime only: it uses an instanced material, so the editor scene creator does not call it.
        /// </summary>
        public static void EnsureGround()
        {
            if (GameObject.Find(GroundName) != null)
            {
                return;
            }
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
            // A Unity plane is 10x10 units at scale 1, so scale by MapSizeTiles / 10 on X and Z.
            float scale = MapSizeTiles / 10f;
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            ground.transform.position = new Vector3(MapSizeTiles / 2f, 0f, MapSizeTiles / 2f);
            Collider groundCollider = ground.GetComponent<Collider>();
            if (groundCollider != null)
            {
                Object.Destroy(groundCollider);
            }
            ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.24f, 0.22f, 1f);
        }
    }
}
