using Godot;
using MonkeyPost.Core;

namespace MonkeyPost.Game
{
    /// <summary>
    /// A chunky placeholder footballer built from primitives, with a procedural run cycle, kicks,
    /// reactions and a simple celebration. Used until the rigged character assets are imported.
    /// Local forward is −Z. Positive rotation about X swings a hanging limb forward.
    /// </summary>
    sealed class PrimitiveFootballer : IFootballerView
    {
        static readonly Color Marker = new Color(1f, 0.85f, 0.1f);

        readonly Node3D _root;
        readonly Node3D _body;
        readonly Node3D _legLeft, _legRight, _armLeft, _armRight;
        readonly MeshInstance3D _marker;
        float _phase;
        float _yaw;

        float _kickTime = -1f;
        bool _kickLeft;
        float _kickStrength;
        Reaction? _reaction;
        float _reactionTime = -1f;
        float _celebrationTime = -1f;

        public PrimitiveFootballer(Node3D parent, string name, Color kit, Color shorts, Color skin)
        {
            _root = new Node3D { Name = name };
            parent.AddChild(_root);

            Art.Cylinder("Shadow", _root, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.005f, 0.8f), Art.Shadow);
            _marker = Art.Cylinder("Control ring", _root, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.006f, 1.1f), Marker);

            _body = Art.Pivot("Body", _root, Vector3.Zero);
            _legLeft = Limb("Leg L", new Vector3(-0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            _legRight = Limb("Leg R", new Vector3(0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            Art.Box("Shorts", _body, new Vector3(0f, 0.88f, 0f), new Vector3(0.48f, 0.28f, 0.3f), shorts);
            Art.Box("Shirt", _body, new Vector3(0f, 1.3f, 0f), new Vector3(0.56f, 0.62f, 0.32f), kit);
            _armLeft = Limb("Arm L", new Vector3(-0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            _armRight = Limb("Arm R", new Vector3(0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            Art.Sphere("Head", _body, new Vector3(0f, 1.86f, 0f), new Vector3(0.4f, 0.42f, 0.4f), skin);
        }

        Node3D Limb(string name, Vector3 joint, Vector3 size, Color colour)
        {
            var pivot = Art.Pivot(name, _body, joint);
            Art.Box(name, pivot, new Vector3(0f, -size.Y / 2f, 0f), size, colour);
            return pivot;
        }

        public void OnTouch(TouchKind kind, Vector3 point, Vector3 direction)
        {
            if (kind == TouchKind.Block || kind == TouchKind.None) return;
            // Kick with the foot on the ball's side.
            var local = _root.ToLocal(point);
            _kickLeft = local.X < 0f;
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
            _root.Position = pose.Position;
            if (pose.Facing.LengthSquared() > 0.01f)
            {
                _yaw = Mathf.RotateToward(_yaw, Art.Yaw(pose.Facing), Mathf.Tau * 2f * dt);
                _root.Rotation = new Vector3(0f, _yaw, 0f);
            }

            float speed = new Vector2(pose.Velocity.X, pose.Velocity.Z).Length();
            float stride = Mathf.Clamp(speed / 5f, 0f, 1f);
            _phase += dt * (4f + speed * 1.6f);
            float swing = Mathf.Sin(_phase) * 40f * stride;
            float legL = swing, legR = -swing, armL = -swing * 0.8f, armR = swing * 0.8f;
            float sideR = 0f;

            if (_kickTime >= 0f)
            {
                // Strike, then follow through: the leg swings forward and back down.
                _kickTime += dt;
                float t = _kickTime / 0.35f;
                float kick = t < 1f ? Mathf.Sin(t * Mathf.Pi) * 75f * _kickStrength : 0f;
                if (_kickLeft) legL = kick; else legR = kick;
                if (t >= 1f) _kickTime = -1f;
            }
            if (pose.Stretching) { legR = 20f; sideR = 65f; }

            if (_reactionTime >= 0f)
            {
                _reactionTime += dt;
                float k = Mathf.Sin(Mathf.Clamp(_reactionTime / 1.2f, 0f, 1f) * Mathf.Pi);
                switch (_reaction)
                {
                    case Reaction.HeadInHands: armL = armR = 150f * k; break;
                    case Reaction.FistClench: armR = 120f * k; break;
                }
                if (_reactionTime > 1.2f) _reactionTime = -1f;
            }

            float hop = 0f;
            if (_celebrationTime >= 0f)
            {
                _celebrationTime += dt;
                armL = armR = 160f;
                hop = Mathf.Abs(Mathf.Sin(_celebrationTime * 6f)) * 0.25f;
                if (_celebrationTime > 2.4f) _celebrationTime = -1f;
            }

            _legLeft.RotationDegrees = new Vector3(legL, 0f, 0f);
            _legRight.RotationDegrees = new Vector3(legR, 0f, sideR);
            _armLeft.RotationDegrees = new Vector3(armL, 0f, 0f);
            _armRight.RotationDegrees = new Vector3(armR, 0f, 0f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * stride + hop;
            _body.Position = new Vector3(0f, bob, 0f);
            bool stumbling = pose.Stunned || (_reaction == Reaction.Stumble && _reactionTime >= 0f);
            // Negative X tilts the top of the body forward (towards −Z).
            _body.RotationDegrees = new Vector3(stumbling ? -25f : -8f * stride, 0f, 0f);

            _marker.Visible = pose.Controlled;
        }
    }
}
