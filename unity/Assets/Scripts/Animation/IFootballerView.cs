using MonkeyPost.Core;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>Everything the match presenter tells a footballer's visuals each frame.</summary>
    struct FootballerPose
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector2 Facing;
        public bool Controlled;
        public bool Stunned;
        public bool Sprinting;
        public bool Shielding;
        public bool Stretching;
        /// <summary>World direction of the keeper's stretched leg.</summary>
        public Vector3 StretchDirection;
        public bool Keeper;
        /// <summary>Where the ball is (centre, world), for the head to follow it.</summary>
        public Vector3 Ball;
    }

    enum Reaction
    {
        /// <summary>Missed or blazed it over.</summary>
        HeadInHands,
        /// <summary>Won a tackle.</summary>
        FistClench,
        /// <summary>Was tackled.</summary>
        Stumble,
        /// <summary>Was nutmegged.</summary>
        LookBack,
    }

    /// <summary>
    /// A footballer's visuals. Two implementations: the rigged, animated character (when the art
    /// assets are in the project) and the primitive placeholder (always available).
    /// </summary>
    interface IFootballerView
    {
        void Show(in FootballerPose pose, float dt);

        /// <summary>The foot met the ball at <paramref name="point"/> (ball centre, world) sending it along <paramref name="direction"/>.</summary>
        void OnTouch(TouchKind kind, Vector3 point, Vector3 direction);

        void PlayReaction(Reaction reaction);

        /// <summary>Plays a goal celebration by id (see <see cref="Celebrations"/>).</summary>
        void Celebrate(string celebrationId);
    }
}
