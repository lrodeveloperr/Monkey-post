using Godot;
using MonkeyPost.Core;
using MonkeyPost.Core.Bots;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Runs a <see cref="LocalMatch"/> at a fixed 60 Hz and draws it smoothly at any frame rate by
    /// interpolating between the last two ticks. The person always plays the home team.
    /// </summary>
    sealed class MatchPresenter
    {
        public const int HumanTeam = 0;
        const float TickSeconds = 1f / 60f;
        const float SnapDistance = 3f;
        const float GoalBannerSeconds = 2.2f;

        static readonly Color HomeKit = new Color(0.10f, 0.55f, 0.28f);
        static readonly Color HomeShorts = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color HomeKeeper = new Color(0.95f, 0.80f, 0.15f);
        static readonly Color AwayKit = new Color(0.85f, 0.25f, 0.15f);
        static readonly Color AwayShorts = new Color(0.12f, 0.12f, 0.14f);
        static readonly Color AwayKeeper = new Color(0.45f, 0.30f, 0.75f);
        static readonly Color[] SkinTones =
        {
            new Color(0.36f, 0.24f, 0.16f), new Color(0.45f, 0.30f, 0.20f),
            new Color(0.28f, 0.18f, 0.12f), new Color(0.52f, 0.36f, 0.24f),
        };

        readonly MatchConfig _config;
        readonly IFootballerView[] _footballers = new IFootballerView[MatchSimulation.PlayerCount];
        readonly int[] _touchSerials = new int[MatchSimulation.PlayerCount];
        readonly BallView _ball;
        readonly Vector3[] _previous = new Vector3[MatchSimulation.PlayerCount];
        readonly Vector3[] _current = new Vector3[MatchSimulation.PlayerCount];
        Vector3 _ballPrevious, _ballCurrent;
        float _ballHeightPrevious, _ballHeightCurrent;

        LocalMatch _match;
        PlayerInput _pending;
        float _accumulator;
        float _cameraX;
        float _safePowerDistance = -1f;
        float _safePower = 1f;

        public MatchSimulation Simulation => _match?.Simulation;
        public BotDifficulty Difficulty { get; private set; }
        public string OpponentName { get; private set; } = "";
        public float GoalBanner { get; private set; }
        public int LastGoalTeam { get; private set; } = -1;
        public string SkillBanner { get; private set; }
        public float SkillBannerTime { get; private set; }
        public bool Finished => _match != null && _match.Simulation.Phase == Phase.Ended;
        public bool UsingRiggedCharacters { get; }

        public MatchPresenter(MatchConfig config, Node3D parent)
        {
            _config = config;
            var root = new Node3D { Name = "Match" };
            parent.AddChild(root);

            // Rigged, animated characters once the art is imported; primitive placeholders until then.
            var assets = CharacterAssets.Load();
            UsingRiggedCharacters = assets != null;
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                int team = MatchSimulation.TeamOf(id);
                int slot = MatchSimulation.SlotOf(id);
                bool keeper = slot == 0;
                var kit = team == 0 ? (keeper ? HomeKeeper : HomeKit) : (keeper ? AwayKeeper : AwayKit);
                var shorts = team == 0 ? HomeShorts : AwayShorts;
                if (assets != null)
                {
                    int number = keeper ? 1 : slot + (team == 0 ? 1 : 5);
                    _footballers[id] = new RiggedFootballer(root, $"Player{id}", assets, kit, assets.Skin(id), number, keeper,
                        config.JogSpeed, config.SprintSpeed);
                }
                else
                {
                    _footballers[id] = new PrimitiveFootballer(root, $"Player{id}", kit, shorts, SkinTones[id % SkinTones.Length]);
                }
            }
            _ball = new BallView(root);
            _cameraX = config.PitchLength / 2f;
        }

        /// <param name="demo">Computer v computer (attract mode and automated checks): the home team is a bot too.</param>
        public void StartMatch(BotDifficulty difficulty, ulong seed, bool demo = false)
        {
            Difficulty = difficulty;
            OpponentName = TeamNames.Pick(seed);
            _match = new LocalMatch(_config, home: demo ? difficulty : (BotDifficulty?)null, away: difficulty, seed: seed);
            _pending = default;
            _accumulator = 0f;
            GoalBanner = 0f;
            LastGoalTeam = -1;
            for (int id = 0; id < MatchSimulation.PlayerCount; id++) _touchSerials[id] = _match.Simulation.Players[id].TouchSerial;
            Capture();
            Capture();
        }

        public bool HumanHasBall
        {
            get
            {
                if (_match == null) return false;
                int owner = _match.Simulation.Ball.Owner;
                return owner >= 0 && _match.IsHuman(HumanTeam) && owner == _match.ControlledPlayer(HumanTeam);
            }
        }

        /// <summary>The highest shot power that stays under knee height from where the ball is now.</summary>
        public float SafeShotPower
        {
            get
            {
                if (_match == null) return 1f;
                float goalX = MatchSimulation.GoalCentre(_config, 1 - HumanTeam).X;
                float distance = Mathf.Abs(goalX - _match.Simulation.Ball.Position.X);
                // The prediction runs the real ball physics, so only redo it when the distance changes.
                if (Mathf.Abs(distance - _safePowerDistance) > 0.25f)
                {
                    _safePowerDistance = distance;
                    _safePower = ShotPhysics.MaxSafePower(_config, distance);
                }
                return _safePower;
            }
        }

        public void Update(float dt, PlayerInput input)
        {
            if (_match == null) return;

            // Movement follows the latest frame; presses are kept until a tick consumes them.
            _pending.Move = input.Move;
            _pending.Sprint = input.Sprint;
            _pending.Aim = input.Aim;
            if (input.Pass) _pending.Pass = true;
            if (input.ThroughBall) _pending.ThroughBall = true;
            if (input.Skill.LengthSquared > 0.01f) _pending.Skill = input.Skill;
            if (input.Switch) _pending.Switch = true;
            if (input.Tackle) _pending.Tackle = true;
            if (input.Shoot)
            {
                _pending.Shoot = true;
                _pending.Power = input.Power;
            }

            _accumulator += Mathf.Min(dt, 0.25f);
            while (_accumulator >= TickSeconds && !Finished)
            {
                if (_match.IsHuman(HumanTeam)) _match.SetInput(HumanTeam, _pending);
                _match.Tick(TickSeconds);
                Capture();
                AnimateTouches();
                foreach (var e in _match.Simulation.Events)
                {
                    AnimateEvent(e);
                    if (e.Type == MatchEventType.Goal)
                    {
                        GoalBanner = GoalBannerSeconds;
                        LastGoalTeam = e.Team;
                    }
                    else if (e.Type == MatchEventType.SkillBeat)
                    {
                        bool ours = e.Team == HumanTeam;
                        bool nutmeg = e.Detail == (int)SkillMove.Nutmeg;
                        SkillBanner = ours ? (nutmeg ? "NUTMEG!" : "SKILL!") : (nutmeg ? "NUTMEGGED!" : null);
                        SkillBannerTime = SkillBanner != null ? 1.3f : 0f;
                    }
                }
                _pending.Pass = _pending.Switch = _pending.Tackle = _pending.Shoot = _pending.ThroughBall = false;
                _pending.Skill = Vec2.Zero;
                _accumulator -= TickSeconds;
            }
            GoalBanner = Mathf.Max(0f, GoalBanner - dt);
            SkillBannerTime = Mathf.Max(0f, SkillBannerTime - dt);
        }

        /// <summary>Every new touch in the simulation becomes a foot-on-ball animation.</summary>
        void AnimateTouches()
        {
            var sim = _match.Simulation;
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                var p = sim.Players[id];
                if (p.TouchSerial == _touchSerials[id]) continue;
                _touchSerials[id] = p.TouchSerial;
                var point = Art.ToWorld(p.LastTouchPoint);
                var direction = Art.ToWorld(p.LastTouchDirection);
                _footballers[id].OnTouch(p.LastTouchKind, point, direction);
            }
        }

        /// <summary>Reactions and celebrations (owner decision: a few short reactions, celebrations on goals).</summary>
        void AnimateEvent(MatchEvent e)
        {
            switch (e.Type)
            {
                case MatchEventType.Goal:
                    if (e.PlayerA >= 0 && MatchSimulation.TeamOf(e.PlayerA) == e.Team)
                    {
                        string celebration = e.Team == HumanTeam ? Celebrations.Equipped : Celebrations.RandomFree(e.PlayerA);
                        _footballers[e.PlayerA].Celebrate(celebration);
                    }
                    break;
                case MatchEventType.ShotOffTarget:
                case MatchEventType.Post:
                    if (e.PlayerA >= 0) _footballers[e.PlayerA].PlayReaction(Reaction.HeadInHands);
                    break;
                case MatchEventType.Tackle:
                    _footballers[e.PlayerA].PlayReaction(Reaction.FistClench);
                    if (e.PlayerB >= 0) _footballers[e.PlayerB].PlayReaction(Reaction.Stumble);
                    break;
                case MatchEventType.SkillBeat:
                    if (e.Detail == (int)SkillMove.Nutmeg && e.PlayerB >= 0) _footballers[e.PlayerB].PlayReaction(Reaction.LookBack);
                    break;
            }
        }

        void Capture()
        {
            var sim = _match.Simulation;
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                _previous[id] = _current[id];
                _current[id] = Art.ToWorld(sim.Players[id].Position);
                if ((_current[id] - _previous[id]).LengthSquared() > SnapDistance * SnapDistance) _previous[id] = _current[id];
            }
            _ballPrevious = _ballCurrent;
            _ballHeightPrevious = _ballHeightCurrent;
            _ballCurrent = Art.ToWorld(sim.Ball.Position);
            _ballHeightCurrent = sim.Ball.Height;
            if ((_ballCurrent - _ballPrevious).LengthSquared() > SnapDistance * SnapDistance)
            {
                _ballPrevious = _ballCurrent;
                _ballHeightPrevious = _ballHeightCurrent;
            }
        }

        /// <summary>Draws footballers, ball and camera, interpolated between the last two ticks.</summary>
        public void Render(Camera3D camera, float dt, double time)
        {
            if (_match == null)
            {
                PlaceCamera(camera, _config.PitchLength / 2f + Mathf.Sin((float)time * 0.15f) * 6f, dt);
                return;
            }

            var sim = _match.Simulation;
            float alpha = Mathf.Clamp(_accumulator / TickSeconds, 0f, 1f);
            int controlled = _match.ControlledPlayer(HumanTeam);
            var ballPosition = _ballPrevious.Lerp(_ballCurrent, alpha);
            float ballHeight = Mathf.Lerp(_ballHeightPrevious, _ballHeightCurrent, alpha);
            var ballCentre = ballPosition + Vector3.Up * (ballHeight + _config.BallRadius);
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                var p = sim.Players[id];
                var pose = new FootballerPose
                {
                    Position = _previous[id].Lerp(_current[id], alpha),
                    Velocity = Art.ToWorld(p.Velocity),
                    Facing = new Vector2(p.Facing.X, p.Facing.Y),
                    Controlled = id == controlled,
                    Stunned = p.StunTime > 0f,
                    Sprinting = p.IsSprinting,
                    Shielding = p.IsShielding,
                    Stretching = p.StretchTime > 0f,
                    StretchDirection = Art.ToWorld(p.StretchDirection),
                    Keeper = p.IsKeeper,
                    Ball = ballCentre,
                };
                _footballers[id].Show(pose, dt);
            }

            _ball.Show(ballPosition, ballHeight, sim.Ball.Spin, dt);
            PlaceCamera(camera, ballPosition.X, dt);
        }

        void PlaceCamera(Camera3D camera, float targetX, float dt)
        {
            // Side-on broadcast view: follow the ball along the pitch, showing roughly half of it.
            const float halfView = 9f;
            float clamped = Mathf.Clamp(targetX, halfView, _config.PitchLength - halfView);
            _cameraX = Mathf.Lerp(_cameraX, clamped, 1f - Mathf.Exp(-4f * dt));
            float midY = _config.PitchWidth / 2f;
            camera.Position = new Vector3(_cameraX, 13.5f, 9.5f);
            camera.LookAt(Art.P(_cameraX, 0f, midY - 2.5f), Vector3.Up);
        }
    }
}
