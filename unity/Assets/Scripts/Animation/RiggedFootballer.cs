using System;
using MonkeyPost.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace MonkeyPost.Game
{
    /// <summary>
    /// A rigged footballer (CC0 low-poly model + Mixamo clips, humanoid-retargeted).
    ///
    /// Hybrid animation (owner decision): recorded clips for locomotion, reactions and
    /// celebrations, blended on a PlayableGraph; on top, a procedural layer driven by the physics:
    /// leaning into acceleration, the head following the ball, and two-bone leg IK that puts the
    /// foot exactly where the simulation says it met the ball. Contact is instant with a quick
    /// follow-through (owner decision), so the touchscreen stays responsive.
    /// </summary>
    sealed class RiggedFootballer : IFootballerView, IDisposable
    {
        const float TargetHeight = 1.85f;

        // Ground speeds the in-place locomotion clips represent. Playback speed scales to the
        // simulated speed so the feet don't slide. Tune once the real clips are in.
        const float JogClipSpeed = 3.6f;
        const float SprintClipSpeed = 6.8f;
        const float BackpedalClipSpeed = 2.5f;

        const int IdleInput = 0, JogInput = 1, SprintInput = 2, BackInput = 3, SpecialInput = 4;
        static readonly Color Marker = new Color(1f, 0.85f, 0.1f);

        readonly AnimationLibrary _library;
        readonly Transform _root;
        readonly Animator _animator;
        readonly PlayableGraph _graph;
        readonly AnimationMixerPlayable _locomotion;
        readonly AnimationLayerMixerPlayable _layers;
        readonly AnimationClipPlayable[] _loops = new AnimationClipPlayable[5];
        readonly GameObject _marker;
        readonly float _jogSpeed, _sprintSpeed;

        readonly Transform _hips, _spine, _head;
        readonly Transform _upperLegL, _lowerLegL, _footL, _upperLegR, _lowerLegR, _footR;

        AnimationClipPlayable _oneShot;
        bool _hasOneShot;
        float _oneShotTime, _oneShotLength;

        FootballerPose _pose;
        float _yaw;
        Vector3 _lastVelocity;
        float _lean, _pitch;

        // Foot-on-ball IK.
        float _ikTime = -1f, _ikDuration, _ikPeak, _ikReach;
        bool _ikLeft, _ikLunge;
        Vector3 _ikPoint, _ikDirection;

        public RiggedFootballer(Transform parent, string name, AnimationLibrary library, Color bib, Texture2D skin,
            int number, bool keeper, float jogSpeed, float sprintSpeed)
        {
            _library = library;
            _jogSpeed = jogSpeed;
            _sprintSpeed = sprintSpeed;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);

            Art.Shape(PrimitiveType.Cylinder, "Shadow", _root, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.005f, 0.8f), Art.Shadow);
            _marker = Art.Shape(PrimitiveType.Cylinder, "Control ring", _root, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.006f, 1.1f), Marker).gameObject;

            var model = Object.Instantiate(library.Model, _root);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            NormaliseHeight(model);
            ApplySkin(model, skin);

            _animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create(name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            string special = keeper ? AnimationLibrary.KeeperIdle : AnimationLibrary.Shield;
            string[] loops = { AnimationLibrary.Idle, AnimationLibrary.Jog, AnimationLibrary.Sprint, AnimationLibrary.Backpedal, special };
            string[] fallbacks = { null, AnimationLibrary.Idle, AnimationLibrary.Jog, AnimationLibrary.Jog, AnimationLibrary.Idle };
            _locomotion = AnimationMixerPlayable.Create(_graph, loops.Length);
            for (int i = 0; i < loops.Length; i++)
            {
                _loops[i] = AnimationClipPlayable.Create(_graph, library.Get(loops[i], fallbacks[i]));
                _graph.Connect(_loops[i], 0, _locomotion, i);
            }
            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            _graph.Connect(_locomotion, 0, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            output.SetSourcePlayable(_layers);
            _graph.Play();

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _upperLegL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lowerLegL = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _footL = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _upperLegR = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _lowerLegR = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _footR = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

            AddBib(bib, number);
            model.AddComponent<RigDriver>().Owner = this;
        }

        static void NormaliseHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 0.01f) model.transform.localScale *= TargetHeight / bounds.size.y;
        }

        static void ApplySkin(GameObject model, Texture2D skin)
        {
            if (skin == null) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetTexture("_MainTex", skin);
                block.SetTexture("_BaseMap", skin);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Street football: teams wear coloured bibs over their clothes, numbered on the back.</summary>
        void AddBib(Color colour, int number)
        {
            var chest = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _spine;
            var leftArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (chest == null || leftArm == null || rightArm == null) return;

            float shoulders = Vector3.Distance(leftArm.position, rightArm.position);
            var top = (leftArm.position + rightArm.position) * 0.5f;
            var size = new Vector3(shoulders * 0.95f, shoulders * 1.05f, shoulders * 0.5f);

            var bib = Art.Box("Bib", _root, Vector3.zero, Vector3.one, colour);
            bib.position = top - Vector3.up * size.y * 0.5f;
            bib.rotation = _root.rotation;
            bib.localScale = size;
            bib.SetParent(chest, true);

            var font = BuiltinFont();
            if (font == null) return;
            var label = new GameObject("Number");
            var text = label.AddComponent<TextMesh>();
            text.font = font;
            text.text = number.ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = shoulders * 0.012f;
            text.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.transform.position = top - Vector3.up * size.y * 0.45f - _root.forward * (size.z * 0.5f + 0.01f);
            label.transform.rotation = _root.rotation;
            label.transform.SetParent(chest, true);
        }

        static Font BuiltinFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (Exception) { return null; }
        }

        public void Show(in FootballerPose pose, float dt)
        {
            _pose = pose;
            _root.localPosition = pose.Position;
            if (pose.Facing.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(pose.Facing.x, pose.Facing.y) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, target, 720f * dt);
                _root.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            _marker.SetActive(pose.Controlled);

            UpdateLocomotion(pose);
            UpdateOneShot(dt);
        }

        void UpdateLocomotion(in FootballerPose pose)
        {
            var flat = new Vector2(pose.Velocity.x, pose.Velocity.z);
            float speed = flat.magnitude;
            float idle = 0f, jog = 0f, sprint = 0f, back = 0f, special = 0f;

            bool backwards = speed > 0.5f && Vector2.Dot(pose.Facing, flat / speed) < -0.3f;
            if (backwards)
            {
                back = Mathf.Clamp01(speed / 1.5f);
                idle = 1f - back;
            }
            else if (pose.Shielding || (pose.Keeper && speed < 1.5f))
            {
                special = 1f - Mathf.Clamp01(speed / 3f);
                jog = 1f - special;
            }
            else if (speed < _jogSpeed)
            {
                jog = speed / _jogSpeed;
                idle = 1f - jog;
            }
            else
            {
                sprint = Mathf.Clamp01((speed - _jogSpeed) / (_sprintSpeed - _jogSpeed));
                jog = 1f - sprint;
            }

            _locomotion.SetInputWeight(IdleInput, idle);
            _locomotion.SetInputWeight(JogInput, jog);
            _locomotion.SetInputWeight(SprintInput, sprint);
            _locomotion.SetInputWeight(BackInput, back);
            _locomotion.SetInputWeight(SpecialInput, special);

            _loops[JogInput].SetSpeed(Mathf.Clamp(speed / JogClipSpeed, 0.6f, 2f));
            _loops[SprintInput].SetSpeed(Mathf.Clamp(speed / SprintClipSpeed, 0.6f, 1.6f));
            _loops[BackInput].SetSpeed(Mathf.Clamp(speed / BackpedalClipSpeed, 0.6f, 2f));
        }

        void UpdateOneShot(float dt)
        {
            if (!_hasOneShot) return;
            _oneShotTime += dt;
            float weight = Mathf.Min(1f, _oneShotTime / 0.15f, (_oneShotLength - _oneShotTime) / 0.2f);
            _layers.SetInputWeight(1, Mathf.Clamp01(weight));
            if (_oneShotTime < _oneShotLength) return;

            _graph.Disconnect(_layers, 1);
            _oneShot.Destroy();
            _hasOneShot = false;
            _layers.SetInputWeight(1, 0f);
        }

        void PlayOneShot(AnimationClip clip)
        {
            if (clip == null) return;
            if (_hasOneShot)
            {
                _graph.Disconnect(_layers, 1);
                _oneShot.Destroy();
            }
            _oneShot = AnimationClipPlayable.Create(_graph, clip);
            _oneShot.SetTime(0);
            _graph.Connect(_oneShot, 0, _layers, 1);
            _layers.SetInputWeight(1, 0f);
            _hasOneShot = true;
            _oneShotTime = 0f;
            _oneShotLength = clip.length;
        }

        public void PlayReaction(Reaction reaction) => PlayOneShot(_library.Get(AnimationLibrary.ReactionClip(reaction)));

        public void Celebrate(string celebrationId) =>
            PlayOneShot(_library.Get(Celebrations.ClipName(celebrationId), Celebrations.ClipName("FistPump")));

        public void OnTouch(TouchKind kind, Vector3 point, Vector3 direction)
        {
            if (kind == TouchKind.Block || kind == TouchKind.None) return;
            _ikLeft = Vector3.Dot(point - _root.position, _root.right) < 0f;
            _ikDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : _root.forward;
            // Contact on the back of the ball, a little below its centre.
            _ikPoint = point - _ikDirection * 0.08f - Vector3.up * 0.04f;
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

        /// <summary>The procedural layer, applied after the clips have been evaluated each frame.</summary>
        internal void ApplyProcedural(float dt)
        {
            if (_hips == null) return;
            float smoothing = 1f - Mathf.Exp(-10f * dt);

            // Lean into acceleration: sideways into turns, forward when driving off.
            var acceleration = dt > 1e-4f ? (_pose.Velocity - _lastVelocity) / dt : Vector3.zero;
            _lastVelocity = _pose.Velocity;
            var local = _root.InverseTransformDirection(acceleration);
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-local.x * 1.8f, -14f, 14f), smoothing);
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(local.z * 1.2f, -8f, 10f) + (_pose.Stunned ? 20f : 0f), smoothing);
            if (_spine != null)
                _spine.rotation = Quaternion.AngleAxis(_lean, _root.forward) * Quaternion.AngleAxis(_pitch, _root.right) * _spine.rotation;

            // Eyes on the ball.
            if (_head != null)
            {
                var toBall = _pose.Ball - _head.position;
                float angle = Vector3.Angle(_root.forward, toBall);
                if (toBall.sqrMagnitude > 0.01f && angle < 100f)
                {
                    var turn = Quaternion.FromToRotation(_root.forward, Vector3.RotateTowards(_root.forward, toBall, 60f * Mathf.Deg2Rad, 0f));
                    _head.rotation = Quaternion.Slerp(_head.rotation, turn * _head.rotation, 0.6f);
                }
            }

            // Keeper's stretched leg towards the ball.
            if (_pose.Stretching)
            {
                bool left = Vector3.Dot(_pose.StretchDirection, _root.right) < 0f;
                var target = _root.position + _pose.StretchDirection * 0.75f + Vector3.up * 0.15f;
                if (left) TwoBoneIK.Solve(_upperLegL, _lowerLegL, _footL, target, _root.forward, 1f);
                else TwoBoneIK.Solve(_upperLegR, _lowerLegR, _footR, target, _root.forward, 1f);
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
                float u = Mathf.Clamp01((_ikTime - strike) / (_ikDuration - strike));
                footTarget = _ikPoint + _ikDirection * (0.6f * u * _ikReach) + Vector3.up * (0.35f * Mathf.Sin(u * Mathf.PI) * _ikReach);
                weight = _ikPeak * (1f - u);
            }
            if (_ikLunge) _hips.position -= Vector3.up * (0.25f * weight);

            if (_ikLeft) TwoBoneIK.Solve(_upperLegL, _lowerLegL, _footL, footTarget, _root.forward, weight);
            else TwoBoneIK.Solve(_upperLegR, _lowerLegR, _footR, footTarget, _root.forward, weight);
            if (_ikTime >= _ikDuration) _ikTime = -1f;
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        /// <summary>Runs the procedural layer after the animation system has posed the skeleton.</summary>
        sealed class RigDriver : MonoBehaviour
        {
            public RiggedFootballer Owner;

            void LateUpdate() => Owner?.ApplyProcedural(Time.deltaTime);

            void OnDestroy() => Owner?.Dispose();
        }
    }
}
