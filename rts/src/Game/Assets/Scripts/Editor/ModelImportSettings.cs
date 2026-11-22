// rts/src/Game/Assets/Scripts/Editor/ModelImportSettings.cs
// Editor menu "RTS/Apply Model Import Settings" (player_interface_4.md UI-17 section 1): walks every model under
// Assets/Resources/Models (by folder, no ID list), sets the import settings, extracts each model's textures and materials to
// Assets/Resources/Models/Materials (textures in a folder per model, one .mat per model material, GPU instancing on), and logs what it
// reads back per file: scale settings, bounds at scale 1 (longest x/z side 1.0 and lowest point 0, within 0.01), rig type, clips with their
// loop flags, and each renderer's material and texture size. A file whose bounds are off is reported with "OFF" for the cloud session
// (rts/tools/Models3D). Files with animation takes are rigged: Generic rig from this model, root node "root", one clip per take named as
// the take, looping only for idle and move, with the root bone's motion baked into the pose (the game never applies root motion, and
// the clips lift the root to keep feet above the ground); the read-back also counts each rigged file's bone scale curves. Every other file gets no rig. Editor only; never touches the simulation. Part of Rts.Game.Editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Rts.Game.EditorTools
{
    /// <summary>Applies and reads back the model import settings.</summary>
    public static class ModelImportSettings
    {
        /// <summary>Folder holding the model files.</summary>
        private const string ModelFolder = "Assets/Resources/Models";

        /// <summary>Folder the materials and textures are extracted to.</summary>
        private const string MaterialFolder = ModelFolder + "/Materials";

        /// <summary>Name of the rig's root bone (rts/docs/art/models/unity/README.md part 2).</summary>
        private const string RootBone = "root";

        /// <summary>Clips that loop; every other clip plays once.</summary>
        private static readonly string[] LoopingClips = { "idle", "move" };

        /// <summary>Allowed deviation of the normalised bounds. Unit: world units at scale 1.</summary>
        private const float BoundsTolerance = 0.01f;

        /// <summary>Menu entry: applies the settings and logs the read-back.</summary>
        [MenuItem("RTS/Apply Model Import Settings")]
        public static void Apply()
        {
            Debug.Log(Run());
        }

        /// <summary>Applies the settings to every model in the folder and returns the read-back report, one line per file.</summary>
        public static string Run()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(ModelFolder, "Materials");
            }
            List<string> paths = ModelPaths();
            foreach (string path in paths)
            {
                ApplySettings(path);
            }
            foreach (string path in paths)
            {
                ExtractMaterials(path);
            }
            StringBuilder report = new StringBuilder("[ModelImportSettings] " + paths.Count + " model files\n");
            foreach (string path in paths)
            {
                report.Append(ReadBack(path)).Append('\n');
            }
            return report.ToString();
        }

        /// <summary>Every model file under the folder (subfolders excluded), ordinal path order.</summary>
        private static List<string> ModelPaths()
        {
            List<string> paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') == ModelFolder)
                {
                    paths.Add(path);
                }
            }
            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        /// <summary>Model, rig and animation settings for one file, then a reimport.</summary>
        /// <param name="path">Asset path of the model.</param>
        private static void ApplySettings(string path)
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            if (importer.animationType == ModelImporterAnimationType.None || !importer.importAnimation)
            {
                // Takes are only listed while animation import is on, so a file set static earlier is re-read before deciding.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            bool rigged = importer.importedTakeInfos.Length > 0;
            if (rigged)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.motionNodeName = RootPath(path);
                // One clip per take, named as the take (without any "Armature|" prefix Blender may add), looping only for idle and move.
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.name = ClipName(clip.takeName);
                    clip.loopTime = Array.IndexOf(LoopingClips, clip.name) >= 0;
                    // Root motion is off at runtime, so the root bone's own keys (ground_clamp lift, lunges, the flyers' bob) are baked
                    // into the pose as authored instead of being extracted and dropped (README part 3).
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
            importer.SaveAndReimport();
        }

        /// <summary>Transform path of the root bone inside the model ("Armature/root"), or the bone name when it cannot be found.</summary>
        /// <param name="path">Asset path of a rigged model.</param>
        private static string RootPath(string path)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform bone = model != null ? Find(model.transform, RootBone) : null;
            return bone != null ? AnimationUtility.CalculateTransformPath(bone, model.transform) : RootBone;
        }

        /// <summary>Depth-first search for a child transform by name.</summary>
        /// <param name="parent">Where to start.</param>
        /// <param name="name">Transform name.</param>
        private static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
                Transform found = Find(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>A clip name from a take name: the part after the last '|'.</summary>
        /// <param name="takeName">Take name from the file.</param>
        private static string ClipName(string takeName)
        {
            return takeName.Substring(takeName.LastIndexOf('|') + 1);
        }

        /// <summary>Extracts a model's embedded textures (to Materials/&lt;file name&gt;/) and materials (Materials/&lt;file name&gt;[_n].mat), turns on instancing.</summary>
        /// <param name="path">Asset path of the model.</param>
        private static void ExtractMaterials(string path)
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string textureFolder = MaterialFolder + "/" + name;
            if (!AssetDatabase.IsValidFolder(textureFolder))
            {
                AssetDatabase.CreateFolder(MaterialFolder, name);
                importer.ExtractTextures(textureFolder);
                AssetDatabase.Refresh();
            }
            Texture2D texture = FixTextureExtensions(textureFolder);
            // Embedded materials still inside the model file, in file order.
            List<Material> embedded = new List<Material>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Material material)
                {
                    embedded.Add(material);
                }
            }
            for (int i = 0; i < embedded.Count; i++)
            {
                string destination = MaterialFolder + "/" + name + (embedded.Count > 1 ? "_" + i : "") + ".mat";
                string error = AssetDatabase.ExtractAsset(embedded[i], destination);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning("[ModelImportSettings] " + path + ": " + error);
                }
            }
            if (embedded.Count > 0)
            {
                importer.SaveAndReimport();
            }
            // This model's extracted materials: <name>.mat or <name>_<n>.mat.
            foreach (string file in Directory.GetFiles(MaterialFolder, name + "*.mat"))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                if (stem != name && !Regex.IsMatch(stem, "^" + Regex.Escape(name) + "_[0-9]+$"))
                {
                    continue;
                }
                Material material = AssetDatabase.LoadAssetAtPath<Material>(file.Replace('\\', '/'));
                if (material == null)
                {
                    continue;
                }
                material.enableInstancing = true;
                if (material.mainTexture == null && texture != null)
                {
                    // The export names its embedded texture without an extension, so the material lost it; each model has one colour map.
                    material.mainTexture = texture;
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Gives extracted texture files without an extension (the export embeds them as "&lt;id&gt;.fbm") their real one from the file header
        /// (.jpg or .png), so Unity imports them as textures. Returns the folder's only texture, or null when there is not exactly one.
        /// </summary>
        /// <param name="folder">A model's texture folder.</param>
        private static Texture2D FixTextureExtensions(string folder)
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".meta" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga")
                {
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(file);
                bool jpeg = bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
                bool png = bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                if (!jpeg && !png)
                {
                    continue;
                }
                string asset = file.Replace('\\', '/');
                string target = Path.ChangeExtension(asset, jpeg ? ".jpg" : ".png");
                AssetDatabase.DeleteAsset(asset);
                File.WriteAllBytes(target, bytes);
                AssetDatabase.ImportAsset(target);
            }
            // Textures in the folder after the rename.
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            return guids.Length == 1 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        /// <summary>World bounds of a renderer's mesh; a skinned mesh is baked in its bind pose first (its renderer bounds are a loose box).</summary>
        /// <param name="renderer">A renderer of a scene copy.</param>
        private static Bounds WorldBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skinned))
            {
                return renderer.bounds;
            }
            Mesh baked = new Mesh();
            skinned.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            Bounds bounds = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                bounds.Encapsulate(skinned.transform.TransformPoint(vertex));
            }
            UnityEngine.Object.DestroyImmediate(baked);
            return bounds;
        }

        /// <summary>One report line: settings, bounds at scale 1, rig, clips and materials of a model as Unity imported it.</summary>
        /// <param name="path">Asset path of the model.</param>
        private static string ReadBack(string path)
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            StringBuilder line = new StringBuilder(Path.GetFileNameWithoutExtension(path) + ": scale " + importer.globalScale + " fileScale " + importer.useFileScale
                + " collider " + importer.addCollider + " rig " + importer.animationType);
            // A scene copy, so renderer bounds are computed; destroyed below.
            GameObject copy = UnityEngine.Object.Instantiate(model);
            try
            {
                Renderer[] renderers = copy.GetComponentsInChildren<Renderer>();
                Bounds bounds = WorldBounds(renderers[0]);
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(WorldBounds(renderer));
                }
                float side = Mathf.Max(bounds.size.x, bounds.size.z);
                bool off = Mathf.Abs(side - 1f) > BoundsTolerance || Mathf.Abs(bounds.min.y) > BoundsTolerance;
                line.Append(" | bounds x " + bounds.size.x.ToString("0.000") + " z " + bounds.size.z.ToString("0.000") + " y " + bounds.size.y.ToString("0.000")
                    + " minY " + bounds.min.y.ToString("0.000") + " rot " + model.transform.localEulerAngles + (off ? " OFF" : " ok"));
                foreach (Renderer renderer in renderers)
                {
                    Material material = renderer.sharedMaterial;
                    Texture texture = material != null ? material.mainTexture : null;
                    line.Append(" | " + (material != null ? AssetDatabase.GetAssetPath(material) : "no material") + " tex "
                        + (texture != null ? texture.width + "x" + texture.height : "none") + (material != null && material.enableInstancing ? " inst" : ""));
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        line.Append(" skinned");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
            if (importer.animationType != ModelImporterAnimationType.None)
            {
                line.Append(" | root " + importer.motionNodeName + " | clips");
                foreach (ModelImporterClipAnimation clip in importer.clipAnimations)
                {
                    line.Append(" " + clip.name + (clip.loopTime ? ":loop" : ":once") + (clip.lockRootHeightY && clip.keepOriginalPositionY ? ":baked" : ":rootmotion"));
                }
                // Bone scale curves the imported clips kept (the Tidecaller bell and the Ink Lobber base squash need them).
                int scaleCurves = 0;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            if (binding.propertyName.StartsWith("m_LocalScale"))
                            {
                                scaleCurves++;
                            }
                        }
                    }
                }
                line.Append(" | scale curves " + scaleCurves);
            }
            return line.ToString();
        }
    }
}
