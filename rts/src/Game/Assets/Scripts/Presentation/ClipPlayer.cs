// rts/src/Game/Assets/Scripts/Presentation/ClipPlayer.cs
// Plays one clip at a time on a rigged unit model through the Playables API (player_interface_4.md UI-17 section 3): no Animator
// Controller asset; each change destroys the old graph and builds a new one with AnimationPlayableUtilities.PlayClip. The host picks
// the clip after each step (death, attack, move, idle) and only calls Play when the choice changes, or to restart an attack. The
// Animator is culled completely while its renderers are hidden (fog) and never applies root motion. Presentation only. Part of Rts.Game.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rts.Game.Presentation
{
    /// <summary>One rigged model's clip playback.</summary>
    public sealed class ClipPlayer
    {
        /// <summary>Looping idle clip name (rts/docs/art/models/unity/README.md part 2).</summary>
        public const string Idle = "idle";

        /// <summary>Looping move clip name.</summary>
        public const string Move = "move";

        /// <summary>One-shot attack clip name.</summary>
        public const string Attack = "attack";

        /// <summary>One-shot death clip name.</summary>
        public const string Death = "death";

        /// <summary>The model's Animator. Range: non-null.</summary>
        private readonly Animator _animator;

        /// <summary>The model's clips by name.</summary>
        private readonly IReadOnlyDictionary<string, AnimationClip> _clips;

        /// <summary>The graph playing the current clip; invalid before the first Play and after Dispose.</summary>
        private PlayableGraph _graph;

        /// <summary>The current clip's playable; invalid before the first Play.</summary>
        private AnimationClipPlayable _playable;

        /// <summary>The clip being played.</summary>
        private AnimationClip _clip;

        /// <summary>Name of the clip being played, or null before the first Play.</summary>
        public string Current { get; private set; }

        /// <summary>Wraps a rigged model's Animator and clips.</summary>
        /// <param name="animator">The model instance's Animator.</param>
        /// <param name="clips">Its clips by name.</param>
        public ClipPlayer(Animator animator, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            _animator = animator;
            _clips = clips;
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _animator.applyRootMotion = false;
        }

        /// <summary>True when the model has a clip of that name.</summary>
        /// <param name="name">Clip name.</param>
        public bool Has(string name)
        {
            return _clips.ContainsKey(name);
        }

        /// <summary>Length of a clip. Unit: seconds at speed 1. Range: 0 when the clip is missing.</summary>
        /// <param name="name">Clip name.</param>
        public float Length(string name)
        {
            return _clips.TryGetValue(name, out AnimationClip clip) ? clip.length : 0f;
        }

        /// <summary>True while a one-shot clip (attack, death) has not reached its end. Unit: on/off.</summary>
        public bool PlayingOnce => _playable.IsValid() && _clip != null && !_clip.isLooping && _playable.GetTime() < _clip.length;

        /// <summary>Plays a clip; the same clip keeps playing (only its speed changes) unless restart is set. A missing clip is ignored.</summary>
        /// <param name="name">Clip name.</param>
        /// <param name="speed">Playback speed. Unit: times the clip's own rate. Range: above 0.</param>
        /// <param name="restart">True to start the clip again from its first frame even when it is already playing.</param>
        public void Play(string name, float speed, bool restart)
        {
            if (!restart && name == Current && _playable.IsValid())
            {
                _playable.SetSpeed(speed);
                return;
            }
            if (!_clips.TryGetValue(name, out AnimationClip clip))
            {
                return;
            }
            Dispose();
            _playable = AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);
            _playable.SetSpeed(speed);
            _clip = clip;
            Current = name;
        }

        /// <summary>Destroys the current graph, if any.</summary>
        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
