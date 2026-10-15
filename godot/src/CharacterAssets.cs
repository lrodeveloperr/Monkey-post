using System.Collections.Generic;
using Godot;

namespace MonkeyPost.Game
{
    /// <summary>
    /// The footballer model, skins and Mixamo clips, loaded from res://assets by fixed file names
    /// (see docs/ANIMATION_ASSETS.md). Returns null from <see cref="Load"/> until the essentials
    /// are present, so the game falls back to primitive placeholders.
    /// </summary>
    sealed class CharacterAssets
    {
        public const string ModelPath = "res://assets/characters/Footballer.fbx";
        const string SkinPath = "res://assets/characters/skins/skin_{0}.png";
        const string ClipPath = "res://assets/animations/{0}.fbx";
        const int MaxSkins = 16;

        public const string Idle = "Loop_Idle";
        public const string Jog = "Loop_Jog";
        public const string Sprint = "Loop_Sprint";
        public const string Backpedal = "Loop_Backpedal";
        public const string Shield = "Loop_Shield";
        public const string KeeperIdle = "Loop_KeeperIdle";

        static readonly string[] ClipNames =
        {
            Idle, Jog, Sprint, Backpedal, Shield, KeeperIdle,
            "React_HeadInHands", "React_FistClench", "React_Stumble", "React_LookBack",
            "Celebrate_FistPump", "Celebrate_RunOff", "Celebrate_KneeSlide", "Celebrate_Dance",
            "Celebrate_Salute", "Celebrate_Backflip", "Celebrate_Shush", "Celebrate_Bow",
        };

        public PackedScene Model { get; private set; }
        public Godot.AnimationLibrary Library { get; private set; }
        readonly List<Texture2D> _skins = new List<Texture2D>();
        readonly Dictionary<Texture2D, StandardMaterial3D> _skinMaterials = new Dictionary<Texture2D, StandardMaterial3D>();

        public static CharacterAssets Load()
        {
            if (!ResourceLoader.Exists(ModelPath)) return null;
            var assets = new CharacterAssets { Model = GD.Load<PackedScene>(ModelPath), Library = new Godot.AnimationLibrary() };
            if (assets.Model == null) return null;

            for (int i = 1; i <= MaxSkins; i++)
            {
                string path = string.Format(SkinPath, i);
                if (ResourceLoader.Exists(path)) assets._skins.Add(GD.Load<Texture2D>(path));
            }

            // Clips are retargeted by bone name onto the model's skeleton path.
            string skeletonPath = SkeletonPathIn(assets.Model);
            if (skeletonPath == null) return null;
            foreach (string name in ClipNames)
            {
                var clip = LoadClip(string.Format(ClipPath, name), skeletonPath);
                if (clip == null) continue;
                clip.LoopMode = name.StartsWith("Loop_") ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                assets.Library.AddAnimation(name, clip);
            }

            bool essentials = assets.Library.HasAnimation(Idle) && assets.Library.HasAnimation(Jog) && assets.Library.HasAnimation(Sprint);
            return essentials ? assets : null;
        }

        public bool Has(string clip) => Library.HasAnimation(clip);

        public StandardMaterial3D Skin(int id)
        {
            if (_skins.Count == 0) return null;
            var texture = _skins[id % _skins.Count];
            if (!_skinMaterials.TryGetValue(texture, out var material))
            {
                material = new StandardMaterial3D { AlbedoTexture = texture, Roughness = 1f };
                _skinMaterials[texture] = material;
            }
            return material;
        }

        static string SkeletonPathIn(PackedScene scene)
        {
            var root = scene.Instantiate();
            var skeleton = Find<Skeleton3D>(root);
            string path = skeleton != null ? root.GetPathTo(skeleton).ToString() : null;
            root.Free();
            return path;
        }

        /// <summary>
        /// Takes the first animation from an imported clip file, keeps only its bone tracks and points
        /// them at the model's skeleton (both come from the same Mixamo rig, so bone names match).
        /// </summary>
        static Animation LoadClip(string path, string skeletonPath)
        {
            if (!ResourceLoader.Exists(path)) return null;
            var root = GD.Load<PackedScene>(path)?.Instantiate();
            if (root == null) return null;
            var player = Find<AnimationPlayer>(root);
            Animation clip = null;
            if (player != null)
            {
                foreach (string name in player.GetAnimationList())
                {
                    if (name == "RESET") continue;
                    clip = (Animation)player.GetAnimation(name).Duplicate();
                    break;
                }
            }
            root.Free();
            if (clip == null) return null;

            for (int t = clip.GetTrackCount() - 1; t >= 0; t--)
            {
                string trackPath = clip.TrackGetPath(t).ToString();
                int colon = trackPath.IndexOf(':');
                if (colon < 0)
                {
                    clip.RemoveTrack(t);  // node tracks (e.g. root motion): the simulation moves the players
                    continue;
                }
                clip.TrackSetPath(t, new NodePath(skeletonPath + trackPath.Substring(colon)));
            }
            return clip;
        }

        public static T Find<T>(Node node) where T : Node
        {
            if (node is T match) return match;
            foreach (var child in node.GetChildren())
            {
                var found = Find<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
