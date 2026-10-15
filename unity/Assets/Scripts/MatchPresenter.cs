using MonkeyPost.Core;
using MonkeyPost.Core.Bots;
using UnityEngine;

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
        readonly FootballerView[] _footballers = new FootballerView[MatchSimulation.PlayerCount];
        readonly BallView _ball;
        readonly Vector3[] _previous = new Vector3[MatchSimulation.PlayerCount];
        readonly Vector3[] _current = new Vector3[MatchSimulation.PlayerCount];
        Vector3 _ballPrevious, _ballCurrent;
        float _ballHeightPrevious, _ballHeightCurrent;

        LocalMatch _match;
        PlayerInput _pending;
        float _safePowerDistance = -1f;
        float _safePower = 1f;
        float _accumulator;
        float _cameraX;

        public MatchSimulation Simulation => _match?.Simulation;
        public BotDifficulty Difficulty { get; private set; }
        public string OpponentName { get; private set; } = "";
        public float GoalBanner { get; private set; }
        public int LastGoalTeam { get; private set; } = -1;

        /// <summary>Text and time left for a skill-move banner ("NUTMEG!"), or null.</summary>
        public string SkillBanner { get; private set; }
        public float SkillBannerTime { get; private set; }
        public bool Finished => _match != null && _match.Simulation.Phase == Phase.Ended;

        public MatchPresenter(MatchConfig config, Transform parent)
        {
            _config = config;
            var root = new GameObject("Match").transform;
            root.SetParent(parent, false);

            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                int team = MatchSimulation.TeamOf(id);
                bool keeper = MatchSimulation.SlotOf(id) == 0;
                var kit = team == 0 ? (keeper ? HomeKeeper : HomeKit) : (keeper ? AwayKeeper : AwayKit);
                var shorts = team == 0 ? HomeShorts : AwayShorts;
                _footballers[id] = new FootballerView(root, $"Player {id}", kit, shorts, SkinTones[id % SkinTones.Length]);
            }
            _ball = new BallView(root);
            _cameraX = config.PitchLength / 2f;
        }

        public void StartMatch(BotDifficulty difficulty, ulong seed)
        {
            Difficulty = difficulty;
            OpponentName = TeamNames.Pick(seed);
            _match = new LocalMatch(_config, home: null, away: difficulty, seed: seed);
            _pending = default;
            _accumulator = 0f;
            GoalBanner = 0f;
            LastGoalTeam = -1;
            Capture();
            Capture();
        }

        public bool HumanHasBall
        {
            get
            {
                if (_match == null) return false;
                int owner = _match.Simulation.Ball.Owner;
                return owner >= 0 && owner == _match.ControlledPlayer(HumanTeam);
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
                _match.SetInput(HumanTeam, _pending);
                _match.Tick(TickSeconds);
                Capture();
                foreach (var e in _match.Simulation.Events)
                {
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

        void Capture()
        {
            var sim = _match.Simulation;
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                _previous[id] = _current[id];
                _current[id] = Art.ToWorld(sim.Players[id].Position);
                if ((_current[id] - _previous[id]).sqrMagnitude > SnapDistance * SnapDistance) _previous[id] = _current[id];
            }
            _ballPrevious = _ballCurrent;
            _ballHeightPrevious = _ballHeightCurrent;
            _ballCurrent = Art.ToWorld(sim.Ball.Position);
            _ballHeightCurrent = sim.Ball.Height;
            if ((_ballCurrent - _ballPrevious).sqrMagnitude > SnapDistance * SnapDistance)
            {
                _ballPrevious = _ballCurrent;
                _ballHeightPrevious = _ballHeightCurrent;
            }
        }

        /// <summary>Draws footballers, ball and camera, interpolated between the last two ticks.</summary>
        public void Render(Camera camera, float dt)
        {
            if (_match == null)
            {
                PlaceCamera(camera, _cameraX + Mathf.Sin(Time.time * 0.15f) * 6f, dt);
                return;
            }

            var sim = _match.Simulation;
            float alpha = Mathf.Clamp01(_accumulator / TickSeconds);
            int controlled = _match.ControlledPlayer(HumanTeam);
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
            {
                var p = sim.Players[id];
                _footballers[id].Show(
                    Vector3.Lerp(_previous[id], _current[id], alpha),
                    new Vector2(p.Facing.X, p.Facing.Y),
                    p.Velocity.Length,
                    id == controlled,
                    p.StunTime > 0f,
                    p.StretchTime > 0f,
                    dt);
            }

            var ballPosition = Vector3.Lerp(_ballPrevious, _ballCurrent, alpha);
            float ballHeight = Mathf.Lerp(_ballHeightPrevious, _ballHeightCurrent, alpha);
            _ball.Show(ballPosition, ballHeight, sim.Ball.Spin, dt);

            PlaceCamera(camera, ballPosition.x, dt);
        }

        void PlaceCamera(Camera camera, float targetX, float dt)
        {
            // Side-on broadcast view: follow the ball along the pitch, showing roughly half of it.
            const float halfView = 9f;
            float clamped = Mathf.Clamp(targetX, halfView, _config.PitchLength - halfView);
            _cameraX = Mathf.Lerp(_cameraX, clamped, 1f - Mathf.Exp(-4f * dt));
            float midZ = _config.PitchWidth / 2f;
            camera.transform.position = new Vector3(_cameraX, 13.5f, -9.5f);
            camera.transform.LookAt(new Vector3(_cameraX, 0f, midZ - 1.5f));
        }
    }
}
