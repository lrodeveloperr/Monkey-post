using MonkeyPost.Core;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// A chunky placeholder footballer built from primitives, with a procedural run cycle, kicks,
    /// reactions and a simple celebration. Used until the rigged character assets are imported.
    /// </summary>
    sealed class FootballerView : IFootballerView
    {
        static readonly Color Marker = new Color(1f, 0.85f, 0.1f);

        readonly Transform _root;
        readonly Transform _body;
        readonly Transform _legLeft, _legRight, _armLeft, _armRight;
        readonly GameObject _marker;
        float _phase;
        float _yaw;

        // One-off actions.
        float _kickTime = -1f;
        bool _kickLeft;
        float _kickStrength;
        Reaction? _reaction;
        float _reactionTime = -1f;
        float _celebrationTime = -1f;

        public FootballerView(Transform parent, string name, Color kit, Color shorts, Color skin)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);

            Art.Shape(PrimitiveType.Cylinder, "Shadow", _root, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.005f, 0.8f), Art.Shadow);
            _marker = Art.Shape(PrimitiveType.Cylinder, "Control ring", _root, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.006f, 1.1f), Marker).gameObject;

            _body = Art.Pivot("Body", _root, Vector3.zero);
            _legLeft = Limb("Leg L", new Vector3(-0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            _legRight = Limb("Leg R", new Vector3(0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            Art.Box("Shorts", _body, new Vector3(0f, 0.88f, 0f), new Vector3(0.48f, 0.28f, 0.3f), shorts);
            Art.Box("Shirt", _body, new Vector3(0f, 1.3f, 0f), new Vector3(0.56f, 0.62f, 0.32f), kit);
            _armLeft = Limb("Arm L", new Vector3(-0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            _armRight = Limb("Arm R", new Vector3(0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            Art.Shape(PrimitiveType.Sphere, "Head", _body, new Vector3(0f, 1.86f, 0f), new Vector3(0.4f, 0.42f, 0.4f), skin);
        }

        Transform Limb(string name, Vector3 joint, Vector3 size, Color colour)
        {
            var pivot = Art.Pivot(name, _body, joint);
            Art.Box(name, pivot, new Vector3(0f, -size.y / 2f, 0f), size, colour);
            return pivot;
        }

        public void OnTouch(TouchKind kind, Vector3 point, Vector3 direction)
        {
            if (kind == TouchKind.Block) return;
            // Kick with the foot on the ball's side.
            var local = _root.InverseTransformPoint(point);
            _kickLeft = local.x < 0f;
            _kickStrength = kind == TouchKind.Shot ? 1f : kind == TouchKind.Pass || kind == TouchKind.Tackle ? 0.7f : 0.35f;
            _kickTime = 0f;
        }

        public void PlayReaction(Reaction reaction)
        {
            _reaction = reaction;
            _reactionTime = 0f;
        }

        public void Celebrate(string celebrationId) => _celebrationTime = 0f;

        public void Show(in FootballerPose pose, float dt)
        {
            _root.localPosition = pose.Position;
            if (pose.Facing.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(pose.Facing.x, pose.Facing.y) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, target, 720f * dt);
                _root.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }

            float speed = new Vector2(pose.Velocity.x, pose.Velocity.z).magnitude;
            float stride = Mathf.Clamp01(speed / 5f);
            _phase += dt * (4f + speed * 1.6f);
            float swing = Mathf.Sin(_phase) * 40f * stride;
            float legL = swing, legR = -swing, armL = -swing * 0.8f, armR = swing * 0.8f;
            float sideR = 0f;

            if (_kickTime >= 0f)
            {
                // Strike, then follow through: the leg swings forward and back down.
                _kickTime += dt;
                float t = _kickTime / 0.35f;
                float kick = t < 1f ? Mathf.Sin(t * Mathf.PI) * -75f * _kickStrength : 0f;
                if (_kickLeft) legL = kick; else legR = kick;
                if (t >= 1f) _kickTime = -1f;
            }
            if (pose.Stretching) { legR = -20f; sideR = 65f; }

            if (_reactionTime >= 0f)
            {
                _reactionTime += dt;
                float k = Mathf.Sin(Mathf.Clamp01(_reactionTime / 1.2f) * Mathf.PI);
                switch (_reaction)
                {
                    case Reaction.HeadInHands: armL = armR = -150f * k; break;
                    case Reaction.FistClench: armR = -120f * k; break;
                }
                if (_reactionTime > 1.2f) _reactionTime = -1f;
            }

            float hop = 0f;
            if (_celebrationTime >= 0f)
            {
                _celebrationTime += dt;
                armL = armR = -160f;
                hop = Mathf.Abs(Mathf.Sin(_celebrationTime * 6f)) * 0.25f;
                if (_celebrationTime > 2.4f) _celebrationTime = -1f;
            }

            _legLeft.localRotation = Quaternion.Euler(legL, 0f, 0f);
            _legRight.localRotation = Quaternion.Euler(legR, 0f, sideR);
            _armLeft.localRotation = Quaternion.Euler(armL, 0f, 0f);
            _armRight.localRotation = Quaternion.Euler(armR, 0f, 0f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * stride + hop;
            _body.localPosition = new Vector3(0f, bob, 0f);
            bool stumbling = pose.Stunned || (_reaction == Reaction.Stumble && _reactionTime >= 0f);
            _body.localRotation = stumbling ? Quaternion.Euler(25f, 0f, 0f) : Quaternion.Euler(8f * stride, 0f, 0f);

            _marker.SetActive(pose.Controlled);
        }
    }
}
