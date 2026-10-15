using Godot;
using MonkeyPost.Core;

namespace MonkeyPost.Game
{
    /// <summary>
    /// A rigged footballer (CC0 low-poly model + Mixamo clips).
    ///
    /// Hybrid animation (owner decision): recorded clips for locomotion, reactions and
    /// celebrations, blended by an AnimationTree; on top, a skeleton modifier driven by the
    /// physics: leaning into acceleration, the head following the ball, and two-bone leg IK that
    /// puts the foot exactly where the simulation met the ball. Contact is instant with a quick
    /// follow-through (owner decision), so the touchscreen stays responsive.
    /// </summary>
    sealed class RiggedFootballer : IFootballerView
    {
        const float TargetHeight = 1.85f;

        // Ground speeds the in-place clips represent; playback scales to the simulated speed so
        // the feet don't slide. Tune once the real clips are in.
        const float JogClipSpeed = 3.6f;
        const float SprintClipSpeed = 6.8f;

        static readonly Color Marker = new Color(1f, 0.85f, 0.1f);

        readonly CharacterAssets _assets;
        readonly Node3D _root;
        readonly Node3D _model;
        readonly Skeleton3D _skeleton;
        readonly AnimationTree _tree;
        readonly AnimationNodeAnimation _action;
        readonly MeshInstance3D _marker;
        readonly float _jogSpeed, _sprintSpeed;

        readonly int _hips, _spine, _head, _upperLegL, _lowerLegL, _footL, _upperLegR, _lowerLegR, _footR;

        FootballerPose _pose;
        float _yaw;
        Vector3 _lastVelocity;
        float _lean, _pitch;

        float _ikTime = -1f, _ikDuration, _ikPeak, _ikReach;
        bool _ikLeft, _ikLunge;
        Vector3 _ikPoint, _ikDirection;

        public RiggedFootballer(Node3D parent, string name, CharacterAssets assets, Color bib, StandardMaterial3D skin,
            int number, bool keeper, float jogSpeed, float sprintSpeed)
        {
            _assets = assets;
            _jogSpeed = jogSpeed;
            _sprintSpeed = sprintSpeed;
            _root = new Node3D { Name = name };
            parent.AddChild(_root);
            Art.Cylinder("Shadow", _root, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.005f, 0.8f), Art.Shadow);
            _marker = Art.Cylinder("Control ring", _root, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.006f, 1.1f), Marker);

            _model = assets.Model.Instantiate<Node3D>();
            _model.Name = "Model";
            _root.AddChild(_model);
            // Imported characters face +Z; footballers here face −Z.
            _model.Rotation = new Vector3(0f, Mathf.Pi, 0f);
            NormaliseHeight();
            if (skin != null)
                foreach (var mesh in _model.FindChildren("*", nameof(MeshInstance3D), true, false))
                    ((MeshInstance3D)mesh).MaterialOverride = skin;

            _skeleton = CharacterAssets.Find<Skeleton3D>(_model);
            _hips = Bone("Hips");
            _spine = Bone("Spine");
            _head = Bone("Head");
            _upperLegL = Bone("LeftUpLeg");
            _lowerLegL = Bone("LeftLeg");
            _footL = Bone("LeftFoot");
            _upperLegR = Bone("RightUpLeg");
            _lowerLegR = Bone("RightLeg");
            _footR = Bone("RightFoot");

            var player = new AnimationPlayer { Name = "AnimationPlayer" };
            _model.AddChild(player);
            player.RootNode = player.GetPathTo(_model);
            player.AddAnimationLibrary("", assets.Library);

            _action = Clip(CharacterAssets.Idle);
            _tree = new AnimationTree { Name = "AnimationTree", TreeRoot = BuildTree(keeper) };
            _model.AddChild(_tree);
            _tree.AnimPlayer = _tree.GetPathTo(player);
            _tree.Active = true;

            if (_skeleton != null)
            {
                _skeleton.AddChild(new ProceduralLayer { Footballer = this });
                AddBib(bib, number);
            }
        }

        AnimationNodeBlendTree BuildTree(bool keeper)
        {
            var loco = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = _sprintSpeed };
            loco.AddBlendPoint(Clip(CharacterAssets.Idle), 0f);
            loco.AddBlendPoint(Clip(CharacterAssets.Jog), _jogSpeed);
            loco.AddBlendPoint(Clip(CharacterAssets.Sprint), _sprintSpeed);

            string special = keeper ? CharacterAssets.KeeperIdle : CharacterAssets.Shield;
            var tree = new AnimationNodeBlendTree();
            tree.AddNode("loco", loco);
            tree.AddNode("speed", new AnimationNodeTimeScale());
            tree.AddNode("back", Clip(_assets.Has(CharacterAssets.Backpedal) ? CharacterAssets.Backpedal : CharacterAssets.Jog));
            tree.AddNode("special", Clip(_assets.Has(special) ? special : CharacterAssets.Idle));
            tree.AddNode("stance", new AnimationNodeBlend3());
            tree.AddNode("oneshot", new AnimationNodeOneShot { FadeInTime = 0.15f, FadeOutTime = 0.2f });
            tree.AddNode("action", _action);

            tree.ConnectNode("speed", 0, "loco");
            tree.ConnectNode("stance", 0, "back");     // −1: backpedal
            tree.ConnectNode("stance", 1, "speed");    //  0: run cycle
            tree.ConnectNode("stance", 2, "special");  // +1: shield / keeper stance
            tree.ConnectNode("oneshot", 0, "stance");
            tree.ConnectNode("oneshot", 1, "action");
            tree.ConnectNode("output", 0, "oneshot");
            return tree;
        }

        static AnimationNodeAnimation Clip(string name) => new AnimationNodeAnimation { Animation = name };

        int Bone(string suffix)
        {
            if (_skeleton == null) return -1;
            for (int i = 0; i < _skeleton.GetBoneCount(); i++)
                if (_skeleton.GetBoneName(i).EndsWith(suffix)) return i;
            return -1;
        }

        void NormaliseHeight()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var node in _model.FindChildren("*", nameof(MeshInstance3D), true, false))
            {
                var mesh = (MeshInstance3D)node;
                var box = mesh.GlobalTransform * mesh.GetAabb();
                min = Mathf.Min(min, box.Position.Y);
                max = Mathf.Max(max, box.End.Y);
            }
            float height = max - min;
            if (height > 0.01f) _model.Scale *= TargetHeight / height;
        }

        /// <summary>Street football: teams wear coloured bibs over their clothes, numbered on the back.</summary>
        void AddBib(Color colour, int number)
        {
            int chest = Bone("Spine2");
            if (chest < 0) chest = Bone("Spine1");
            int leftArm = Bone("LeftArm"), rightArm = Bone("RightArm");
            if (chest < 0 || leftArm < 0 || rightArm < 0) return;

            // Measured in skeleton space from the rest pose.
            var left = _skeleton.GetBoneGlobalRest(leftArm).Origin;
            var right = _skeleton.GetBoneGlobalRest(rightArm).Origin;
            var chestRest = _skeleton.GetBoneGlobalRest(chest);
            float shoulders = left.DistanceTo(right);
            var size = new Vector3(shoulders * 0.95f, shoulders * 1.05f, shoulders * 0.5f);
            var centre = (left + right) * 0.5f - Vector3.Up * size.Y * 0.5f;

            var attachment = new BoneAttachment3D { BoneName = _skeleton.GetBoneName(chest) };
            _skeleton.AddChild(attachment);
            // Undo the bone's rest orientation so the bib sits square on the body.
            var holder = new Node3D { Name = "Bib", Transform = chestRest.AffineInverse() * new Transform3D(Basis.Identity, centre) };
            attachment.AddChild(holder);
            Art.Box("Bib", holder, Vector3.Zero, size, colour);

            // The back is −Z in the model's own space; Label3D text faces +Z, so turn it round.
            var label = new Label3D
            {
                Text = number.ToString(),
                PixelSize = shoulders * 0.004f,
                FontSize = 96,
                Modulate = Colors.White,
                OutlineSize = 0,
                Position = new Vector3(0f, size.Y * 0.05f, -size.Z * 0.5f - 0.005f),
                Rotation = new Vector3(0f, Mathf.Pi, 0f),
            };
            holder.AddChild(label);
        }

        public void Show(in FootballerPose pose, float dt)
        {
            _pose = pose;
            _root.Position = pose.Position;
            if (pose.Facing.LengthSquared() > 0.01f)
            {
                _yaw = Mathf.RotateToward(_yaw, Art.Yaw(pose.Facing), Mathf.Tau * 2f * dt);
                _root.Rotation = new Vector3(0f, _yaw, 0f);
            }
            _marker.Visible = pose.Controlled;

            var flat = new Vector2(pose.Velocity.X, pose.Velocity.Z);
            float speed = flat.Length();
            var facingWorld = new Vector2(pose.Facing.X, -pose.Facing.Y);
            bool backwards = speed > 0.5f && facingWorld.Dot(flat / speed) < -0.3f;
            float stance = backwards ? -Mathf.Clamp(speed / 1.5f, 0f, 1f)
                : pose.Shielding || (pose.Keeper && speed < 1.5f) ? 1f - Mathf.Clamp(speed / 3f, 0f, 1f)
                : 0f;

            float scale = speed < 0.5f ? 1f
                : speed < _jogSpeed ? Mathf.Lerp(1f, _jogSpeed / JogClipSpeed, speed / _jogSpeed)
                : Mathf.Lerp(_jogSpeed / JogClipSpeed, _sprintSpeed / SprintClipSpeed,
                    Mathf.Clamp((speed - _jogSpeed) / (_sprintSpeed - _jogSpeed), 0f, 1f));

            _tree.Set("parameters/loco/blend_position", speed);
            _tree.Set("parameters/stance/blend_amount", stance);
            _tree.Set("parameters/speed/scale", Mathf.Clamp(scale, 0.6f, 2f));
        }

        void PlayOneShot(string clip)
        {
            if (!_assets.Has(clip)) return;
            _action.Animation = clip;
            _tree.Set("parameters/oneshot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
        }

        public void PlayReaction(Reaction reaction) => PlayOneShot("React_" + reaction);

        public void Celebrate(string celebrationId)
        {
            string clip = Celebrations.ClipName(celebrationId);
            PlayOneShot(_assets.Has(clip) ? clip : Celebrations.ClipName("FistPump"));
        }

        public void OnTouch(TouchKind kind, Vector3 point, Vector3 direction)
        {
            if (kind == TouchKind.Block || kind == TouchKind.None) return;
            var right = _root.GlobalBasis.X;
            _ikLeft = (point - _root.GlobalPosition).Dot(right) < 0f;
            _ikDirection = direction.LengthSquared() > 1e-6f ? direction.Normalized() : -_root.GlobalBasis.Z;
            // Contact on the back of the ball, a little below its centre.
            _ikPoint = point - _ikDirection * 0.08f - Vector3.Up * 0.04f;
            _ikLunge = kind == TouchKind.Tackle;
            switch (kind)
            {
                case TouchKind.Shot: _ikDuration = 0.38f; _ikPeak = 1f; _ikReach = 1f; break;
                case TouchKind.Pass: _ikDuration = 0.3f; _ikPeak = 0.9f; _ikReach = 0.7f; break;
                case TouchKind.Tackle: _ikDuration = 0.45f; _ikPeak = 1f; _ikReach = 0.5f; break;
                case TouchKind.Skill: _ikDuration = 0.25f; _ikPeak = 0.8f; _ikReach = 0.4f; break;
                case TouchKind.Control: _ikDuration = 0.22f; _ikPeak = 0.6f; _ikReach = 0.2f; break;
                default: _ikDuration = 0.18f; _ikPeak = 0.55f; _ikReach = 0.3f; break;
            }
            _ikTime = 0f;
        }

        /// <summary>The procedural layer, run by the skeleton after the clips have posed it.</summary>
        internal void ApplyProcedural(float dt)
        {
            if (_skeleton == null || _hips < 0) return;
            var toSkeleton = _skeleton.GlobalTransform.AffineInverse();
            var forward = -_root.GlobalBasis.Z;
            var right = _root.GlobalBasis.X;
            float smoothing = 1f - Mathf.Exp(-10f * dt);

            // Lean into acceleration: sideways into turns, forward when driving off.
            var acceleration = dt > 1e-4f ? (_pose.Velocity - _lastVelocity) / dt : Vector3.Zero;
            _lastVelocity = _pose.Velocity;
            float sideways = acceleration.Dot(right), ahead = acceleration.Dot(forward);
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(sideways * 1.8f, -14f, 14f), smoothing);
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(ahead * 1.2f, -8f, 10f) + (_pose.Stunned ? 20f : 0f), smoothing);
            if (_spine >= 0)
            {
                var lean = new Quaternion(toSkeleton.Basis * forward, Mathf.DegToRad(-_lean))
                         * new Quaternion((toSkeleton.Basis * right).Normalized(), Mathf.DegToRad(-_pitch));
                RotateBone(_spine, lean.Normalized());
            }

            // Eyes on the ball.
            if (_head >= 0)
            {
                var headPosition = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_head).Origin;
                var toBall = _pose.Ball - headPosition;
                if (toBall.LengthSquared() > 0.01f && forward.AngleTo(toBall) < Mathf.DegToRad(100f))
                {
                    var limited = forward.Slerp(toBall.Normalized(), Mathf.Min(1f, Mathf.DegToRad(60f) / Mathf.Max(1e-3f, forward.AngleTo(toBall))));
                    var turn = FromTo(toSkeleton.Basis * forward, toSkeleton.Basis * limited);
                    RotateBone(_head, Quaternion.Identity.Slerp(turn, 0.6f));
                }
            }

            // Keeper's stretched leg towards the ball.
            if (_pose.Stretching)
            {
                bool left = _pose.StretchDirection.Dot(right) < 0f;
                var target = _root.GlobalPosition + _pose.StretchDirection * 0.75f + Vector3.Up * 0.15f;
                SolveLeg(left, toSkeleton * target, toSkeleton.Basis * forward, 1f);
                return;
            }

            if (_ikTime < 0f) return;
            _ikTime += dt;
            const float strike = 0.06f;
            float weight;
            Vector3 footTarget;
            if (_ikTime < strike)
            {
                weight = _ikTime / strike * _ikPeak;
                footTarget = _ikPoint;
            }
            else
            {
                // Follow-through: the foot swings on through and up, easing back to the clip.
                float u = Mathf.Clamp((_ikTime - strike) / (_ikDuration - strike), 0f, 1f);
                footTarget = _ikPoint + _ikDirection * (0.6f * u * _ikReach) + Vector3.Up * (0.35f * Mathf.Sin(u * Mathf.Pi) * _ikReach);
                weight = _ikPeak * (1f - u);
            }
            if (_ikLunge)
            {
                var hips = _skeleton.GetBonePosePosition(_hips);
                _skeleton.SetBonePosePosition(_hips, hips - toSkeleton.Basis * Vector3.Up * (0.25f * weight));
            }
            // The target is for the ankle, which sits just above the contact point.
            SolveLeg(_ikLeft, toSkeleton * (footTarget + Vector3.Up * 0.06f), toSkeleton.Basis * forward, weight);
            if (_ikTime >= _ikDuration) _ikTime = -1f;
        }

        void SolveLeg(bool left, Vector3 target, Vector3 kneeHint, float weight)
        {
            if (left) TwoBoneIK(_upperLegL, _lowerLegL, _footL, target, kneeHint, weight);
            else TwoBoneIK(_upperLegR, _lowerLegR, _footR, target, kneeHint, weight);
        }

        /// <summary>
        /// Geometric two-bone IK in skeleton space: place the knee in the plane towards the hint at
        /// the distance the bone lengths allow, aim the thigh at it, then aim the shin at the target.
        /// </summary>
        void TwoBoneIK(int upper, int lower, int end, Vector3 target, Vector3 kneeHint, float weight)
        {
            if (upper < 0 || lower < 0 || end < 0 || weight <= 0f) return;
            weight = Mathf.Clamp(weight, 0f, 1f);
            var upperBefore = _skeleton.GetBonePoseRotation(upper);
            var lowerBefore = _skeleton.GetBonePoseRotation(lower);

            var a = _skeleton.GetBoneGlobalPose(upper).Origin;
            var b = _skeleton.GetBoneGlobalPose(lower).Origin;
            var c = _skeleton.GetBoneGlobalPose(end).Origin;
            float lab = a.DistanceTo(b), lbc = b.DistanceTo(c);
            if (lab < 1e-4f || lbc < 1e-4f) return;

            var toTarget = target - a;
            float reach = Mathf.Clamp(toTarget.Length(), 0.01f, lab + lbc - 0.001f);
            var along = toTarget.Normalized();
            var pole = kneeHint - along * kneeHint.Dot(along);
            pole = pole.LengthSquared() > 1e-6f ? pole.Normalized() : (b - a - along * (b - a).Dot(along)).Normalized();
            float x = (lab * lab - lbc * lbc + reach * reach) / (2f * reach);
            float h = Mathf.Sqrt(Mathf.Max(0f, lab * lab - x * x));
            var knee = a + along * x + pole * h;

            RotateBone(upper, FromTo(b - a, knee - a));
            var newKnee = _skeleton.GetBoneGlobalPose(lower).Origin;
            var newEnd = _skeleton.GetBoneGlobalPose(end).Origin;
            RotateBone(lower, FromTo(newEnd - newKnee, a + along * reach - newKnee));

            _skeleton.SetBonePoseRotation(upper, upperBefore.Slerp(_skeleton.GetBonePoseRotation(upper), weight));
            _skeleton.SetBonePoseRotation(lower, lowerBefore.Slerp(_skeleton.GetBonePoseRotation(lower), weight));
        }

        /// <summary>Applies a skeleton-space rotation to a bone (its children follow).</summary>
        void RotateBone(int bone, Quaternion rotation)
        {
            var global = _skeleton.GetBoneGlobalPose(bone).Basis.Orthonormalized();
            int parent = _skeleton.GetBoneParent(bone);
            var parentGlobal = parent >= 0 ? _skeleton.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
            var local = parentGlobal.Inverse() * (new Basis(rotation) * global);
            _skeleton.SetBonePoseRotation(bone, local.GetRotationQuaternion());
        }

        static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            from = from.Normalized();
            to = to.Normalized();
            var axis = from.Cross(to);
            float angle = from.AngleTo(to);
            if (axis.LengthSquared() < 1e-10f || angle < 1e-5f) return Quaternion.Identity;
            return new Quaternion(axis.Normalized(), angle);
        }
    }

    /// <summary>Runs a footballer's procedural layer after the animation has posed the skeleton each frame.</summary>
    public partial class ProceduralLayer : SkeletonModifier3D
    {
        internal RiggedFootballer Footballer;

        public override void _ProcessModificationWithDelta(double delta) => Footballer?.ApplyProcedural((float)delta);
    }
}
