// rts/src/Game/Assets/Scripts/Presentation/ModelLibrary.cs
// Loads unit models and their animation clips from Resources by data ID (player_interface_4.md UI-17 section 2): Model(id) is
// Resources.Load<GameObject>(folder + id), Clips(id) the clips inside that model by name. Both are cached per ID, a missing model too
// (null means "draw the cube"). No ID is ever typed in code: the IDs come from the simulation's spawn events. Presentation only.
// Part of Rts.Game.
using System.Collections.Generic;
using UnityEngine;

namespace Rts.Game.Presentation
{
    /// <summary>Cached lookups of model prefabs and clips by data ID.</summary>
    public sealed class ModelLibrary
    {
        /// <summary>Resources folder prefix of the models, with the trailing slash ("Models/").</summary>
        private readonly string _folder;

        /// <summary>Model prefab per data ID; null when the ID has no model. Cached after the first lookup.</summary>
        private readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();

        /// <summary>Clips by name per data ID; empty for a static model or none. Cached after the first lookup.</summary>
        private readonly Dictionary<string, Dictionary<string, AnimationClip>> _clips = new Dictionary<string, Dictionary<string, AnimationClip>>();

        /// <summary>Makes a library reading from a Resources folder.</summary>
        /// <param name="folder">Resources folder prefix with its trailing slash, for example "Models/".</param>
        public ModelLibrary(string folder)
        {
            _folder = folder;
        }

        /// <summary>The model prefab for a data ID, or null when there is none.</summary>
        /// <param name="id">A data ID (unit definition, or an HQ ID plus the uprooted suffix).</param>
        public GameObject Model(string id)
        {
            if (!_models.TryGetValue(id, out GameObject model))
            {
                model = Resources.Load<GameObject>(_folder + id);
                _models.Add(id, model);
            }
            return model;
        }

        /// <summary>The animation clips inside a model, by clip name; empty when it has none.</summary>
        /// <param name="id">A data ID with a model.</param>
        public IReadOnlyDictionary<string, AnimationClip> Clips(string id)
        {
            if (!_clips.TryGetValue(id, out Dictionary<string, AnimationClip> clips))
            {
                clips = new Dictionary<string, AnimationClip>();
                foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(_folder + id))
                {
                    // Unity adds a hidden preview clip per take; only the named clips are kept.
                    if (!clip.name.StartsWith("__preview__"))
                    {
                        clips[clip.name] = clip;
                    }
                }
                _clips.Add(id, clips);
            }
            return clips;
        }
    }
}
