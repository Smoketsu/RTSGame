// rts/src/Game/Assets/Scripts/Editor/CreateBootstrapScene.cs
// Editor-only menu item "RTS > Create Bootstrap Scene": builds and saves Assets/Scenes/Bootstrap.unity containing
// the SimulationHost object, a three-quarter top-down camera with its RtsCameraController, a directional light, a TerrainView
// and a SelectionController (so their Inspector fields can be tuned and saved), then adds the scene to the
// Build Settings if it is not there. Part of Rts.Game.Editor (never included in a player build).
// The scene is optional: AutoBootstrap makes Play work in any scene without it.
using System.IO;
using Rts.Game.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rts.Game.EditorTools
{
    /// <summary>Menu command that creates the bootstrap scene asset.</summary>
    public static class CreateBootstrapScene
    {
        /// <summary>Project-relative folder for scenes. Unit: asset path.</summary>
        private const string ScenesFolder = "Assets/Scenes";

        /// <summary>Project-relative path of the scene this command writes. Unit: asset path. Overwritten if it exists.</summary>
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        /// <summary>Creates, fills, saves and registers the scene. Disabled during Play mode.</summary>
        [MenuItem("RTS/Create Bootstrap Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RTS] Stop Play mode before creating the bootstrap scene.");
                return;
            }
            // Offer to save the scene the developer is editing before it is replaced; false means they cancelled.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AutoBootstrap.EnsureHost();
            AutoBootstrap.EnsureCamera();
            AutoBootstrap.EnsureCameraController();
            AutoBootstrap.EnsureLight();
            AutoBootstrap.EnsureTerrainView();
            AutoBootstrap.EnsureSelection();

            Directory.CreateDirectory(ScenesFolder);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[RTS] Could not save " + ScenePath + ".");
                return;
            }
            AddToBuildSettings(ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[RTS] Saved " + ScenePath + ". Press Play to run the simulation. Commit the new .meta file too.");
        }

        /// <summary>Appends the scene to EditorBuildSettings.scenes (enabled) unless a scene with that path is already listed.</summary>
        /// <param name="path">Project-relative scene path.</param>
        private static void AddToBuildSettings(string path)
        {
            // Scenes already listed in File > Build Profiles / Build Settings.
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene entry in existing)
            {
                if (entry.path == path)
                {
                    return;
                }
            }
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
