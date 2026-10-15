using System.Collections.Generic;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// The character model and animation clips, loaded from Resources. Files are named by
    /// convention (see docs/ANIMATION_ASSETS.md); the importer renames each clip after its file.
    /// If the essentials aren't there yet, <see cref="Available"/> is false and the game uses
    /// the primitive placeholder footballers.
    /// </summary>
    sealed class AnimationLibrary
    {
        public const string ModelPath = "Characters/Footballer";
        public const string SkinsPath = "Characters/Skins";
        public const string ClipsPath = "Animations";

        public const string Idle = "Loop_Idle";
        public const string Jog = "Loop_Jog";
        public const string Sprint = "Loop_Sprint";
        public const string Backpedal = "Loop_Backpedal";
        public const string Shield = "Loop_Shield";
        public const string KeeperIdle = "Loop_KeeperIdle";

        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();

        public GameObject Model { get; }
        public Texture2D[] Skins { get; }

        /// <summary>Model plus the three core locomotion clips are present.</summary>
        public bool Available => Model != null && Has(Idle) && Has(Jog) && Has(Sprint);

        public AnimationLibrary()
        {
            Model = Resources.Load<GameObject>(ModelPath);
            Skins = Resources.LoadAll<Texture2D>(SkinsPath);
            foreach (var clip in Resources.LoadAll<AnimationClip>(ClipsPath))
            {
                // Skip Unity's internal preview clips inside FBX files.
                if (clip.name.StartsWith("__preview__")) continue;
                _clips[clip.name] = clip;
            }
        }

        public bool Has(string name) => _clips.ContainsKey(name);

        /// <summary>The named clip, or <paramref name="fallback"/>'s clip, or null.</summary>
        public AnimationClip Get(string name, string fallback = null)
        {
            if (_clips.TryGetValue(name, out var clip)) return clip;
            if (fallback != null && _clips.TryGetValue(fallback, out clip)) return clip;
            return null;
        }

        public static string ReactionClip(Reaction reaction) => "React_" + reaction;
    }
}
