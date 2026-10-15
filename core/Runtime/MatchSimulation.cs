using System;
using System.Collections.Generic;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Authoritative 5-a-side Monkey Post match: 4 outfield players and a feet-only keeper per
    /// team, tiny goals, walled pitch, one clock. In local multiplayer only the host runs this;
    /// guests and bots see the match through <see cref="MatchSnapshot"/>.
    /// Players 0–4 are the home team (0 = keeper), 5–9 the away team (5 = keeper).
    ///
    /// The ball is always a free rigid body (<see cref="BallPhysics"/>). Dribbling is a series of
    /// real touches, passes and shots are planned with the same physics, and players have bodies
    /// the ball deflects off. Step it at 60 Hz or faster; the ball substeps at 240 Hz.
    /// </summary>
    public sealed class MatchSimulation
    {
        public const int PlayersPerTeam = 5;
        public const int PlayerCount = PlayersPerTeam * 2;
        public const int KickoffSlot = 4;

        const float WallHitSoundSpeed = 3f;
        const float BlockEventSpeed = 8f;
        const float TouchErrorJog = 0.04f;
        const float TouchErrorSprint = 0.10f;
        const float TouchPushPerLead = 2.4f;
        const float AssistRadius = 1.6f;

        // Formation as (fraction of pitch length from own goal, fraction of width): GK, 2 DEF, 2 ATT.
        static readonly float[] FormationDepth = { 0.06f, 0.25f, 0.25f, 0.42f, 0.42f };
        static readonly float[] FormationWidth = { 0.5f, 0.3f, 0.7f, 0.35f, 0.65f };

        readonly PlayerState[] _players = new PlayerState[PlayerCount];
        readonly int[] _score = new int[2];
        readonly List<MatchEvent> _events = new List<MatchEvent>();
        readonly DeterministicRandom _rng;
        readonly SurfaceProfile _surface;

        public MatchConfig Config { get; }
        public Phase Phase { get; private set; }
        public float TimeRemaining { get; private set; }
        public float PhaseTime { get; private set; }
        public int KickoffTeam { get; private set; }
        public int LastTouchPlayer { get; private set; } = -1;

        /// <summary>The last player to play the ball on purpose (kick, touch, control), ignoring deflections.</summary>
        public int LastKickPlayer { get; private set; } = -1;

        /// <summary>Full time has passed with the scores level and the next goal wins.</summary>
        public bool IsGoldenGoal { get; private set; }

        public BallState Ball { get; } = new BallState();
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Events raised by the most recent <see cref="Step"/>.</summary>
        public IReadOnlyList<MatchEvent> Events => _events;

        public MatchSimulation(MatchConfig config, ulong seed, int firstKickoffTeam = 0)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            if (firstKickoffTeam != 0 && firstKickoffTeam != 1) throw new ArgumentOutOfRangeException(nameof(firstKickoffTeam));

            Config = config;
            _surface = config.SurfaceProfile;
            _rng = new DeterministicRandom(seed);
            Ball.Radius = config.BallRadius;
            for (int id = 0; id < PlayerCount; id++)
                _players[id] = new PlayerState { Id = id, Team = TeamOf(id), Slot = SlotOf(id) };

            TimeRemaining = config.MatchDurationSeconds;
            ResetForKickoff(firstKickoffTeam);
            _events.Clear();
        }

        public int Score(int team) => _score[team];

        public static int TeamOf(int playerId) => playerId / PlayersPerTeam;
        public static int SlotOf(int playerId) => playerId % PlayersPerTeam;
        public static int PlayerId(int team, int slot) => team * PlayersPerTeam + slot;

        /// <summary>+1 if the team attacks towards increasing X, -1 otherwise.</summary>
        public static float AttackDirection(int team) => team == 0 ? 1f : -1f;

        public static Vec2 GoalCentre(MatchConfig c, int team) =>
            new Vec2(team == 0 ? 0f : c.PitchLength, c.PitchWidth * 0.5f);

        public static Vec2 FormationSpot(MatchConfig c, int team, int slot)
        {
            float depth = FormationDepth[slot] * c.PitchLength;
            return new Vec2(team == 0 ? depth : c.PitchLength - depth, FormationWidth[slot] * c.PitchWidth);
        }

        public static bool InKeeperBox(MatchConfig c, int team, Vec2 p)
        {
            float depth = team == 0 ? p.X : c.PitchLength - p.X;
            return depth <= c.KeeperBoxDepth && Math.Abs(p.Y - c.PitchWidth * 0.5f) <= c.KeeperBoxHalfWidth;
        }

        public void Step(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Count != PlayerCount)
                throw new ArgumentException($"Expected {PlayerCount} inputs, got {inputs.Count}.", nameof(inputs));
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));

            _events.Clear();
            if (Phase == Phase.Ended) return;

            if (!IsGoldenGoal)
            {
                TimeRemaining -= dt;
                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    if (Config.GoldenGoalIfLevel && _score[0] == _score[1])
                    {
                        IsGoldenGoal = true;
                        _events.Add(new MatchEvent(MatchEventType.GoldenGoal));
                    }
                    else
                    {
                        EndMatch();
                        return;
                    }
                }
            }

            PhaseTime += dt;
            switch (Phase)
            {
                case Phase.Kickoff:
                    if (PhaseTime >= Config.KickoffSeconds)
                    {
                        Phase = Phase.Play;
                        PhaseTime = 0f;
                    }
                    break;
                case Phase.GoalScored:
                    foreach (var p in _players) PlayerMotion.Brake(p, Config, dt);
                    if (PhaseTime < Config.GoalCelebrationSeconds) break;
                    if (IsGoldenGoal) EndMatch();
                    else ResetForKickoff(KickoffTeam);
                    break;
                case Phase.Play:
                    StepPlay(inputs, dt);
                    break;
            }
        }

        void StepPlay(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            foreach (var p in _players)
            {
                p.StunTime = Math.Max(0f, p.StunTime - dt);
                p.KickCooldown = Math.Max(0f, p.KickCooldown - dt);
                p.TackleCooldown = Math.Max(0f, p.TackleCooldown - dt);
                p.TouchCooldown = Math.Max(0f, p.TouchCooldown - dt);
                p.StretchTime = Math.Max(0f, p.StretchTime - dt);
                p.PendingTime = Math.Max(0f, p.PendingTime - dt);
                if (p.PendingTime <= 0f) p.PendingKick = KickType.None;
            }

            foreach (var p in _players) TakeActions(p, inputs[p.Id]);
            foreach (var p in _players) MovePlayer(p, inputs[p.Id], dt);
            SeparatePlayers();

            int substeps = Math.Max(1, (int)Math.Ceiling(dt / BallPhysics.MaxStep - 1e-4f));
            float h = dt / substeps;
            for (int i = 0; i < substeps && Phase == Phase.Play; i++) StepBall(h);
            if (Phase == Phase.Play) DribbleAssist(dt);
        }

        // ---- Players ---------------------------------------------------------------------------

        void TakeActions(PlayerState p, PlayerInput input)
        {
            if (p.StunTime > 0f) return;

            if ((input.Shoot || input.Pass) && (Ball.Owner == p.Id || Ball.Owner < 0))
            {
                // Kicks happen when the ball is in reach: now, or within a short window
                // (first-time shots and passes, or a carrier whose last touch ran a little long).
                p.PendingKick = input.Shoot ? KickType.Shot : KickType.Pass;
                p.PendingAim = input.Aim;
                p.PendingPower = input.Power;
                p.PendingDirection = input.Move;
                p.PendingTime = Config.PendingKickSeconds;
            }

            if (input.Tackle && p.TackleCooldown <= 0f)
            {
                if (p.IsKeeper) Stretch(p);
                else TryTackle(p);
            }
        }

        void MovePlayer(PlayerState p, PlayerInput input, float dt)
        {
            if (p.StunTime > 0f)
            {
                p.DesiredMove = Vec2.Zero;
                PlayerMotion.Brake(p, Config, dt);
            }
            else
            {
                var move = input.Move.ClampLength(1f);
                if (p.PendingKick != KickType.None && !InKickReach(p))
                {
                    // Step towards the ball to strike it.
                    move = (Ball.Position - p.Position).Normalized;
                }
                p.DesiredMove = move;
                PlayerMotion.Steer(p, move, input.Sprint, Ball.Owner == p.Id, Config, dt);
            }

            p.Position += p.Velocity * dt;
            ConstrainPlayer(p);
        }

        void ConstrainPlayer(PlayerState p)
        {
            float r = Config.PlayerRadius;
            float midY = Config.PitchWidth * 0.5f;
            var before = p.Position;
            float x = Clamp(p.Position.X, r, Config.PitchLength - r);
            float y = Clamp(p.Position.Y, r, Config.PitchWidth - r);

            float depth = p.Team == 0 ? x : Config.PitchLength - x;
            if (p.IsKeeper)
            {
                // The keeper may not leave their box.
                depth = Math.Min(depth, Config.KeeperBoxDepth);
                y = Clamp(y, midY - Config.KeeperBoxHalfWidth, midY + Config.KeeperBoxHalfWidth);
            }
            else if (depth < Config.KeeperBoxDepth + r && Math.Abs(y - midY) < Config.KeeperBoxHalfWidth + r)
            {
                // Outfield players may not park in front of their own tiny goal: push out of the box
                // by the shortest route.
                float outFront = Config.KeeperBoxDepth + r - depth;
                float outSide = Config.KeeperBoxHalfWidth + r - Math.Abs(y - midY);
                if (outFront <= outSide) depth = Config.KeeperBoxDepth + r;
                else y = midY + (y >= midY ? 1f : -1f) * (Config.KeeperBoxHalfWidth + r);
            }

            x = p.Team == 0 ? depth : Config.PitchLength - depth;
            p.Position = new Vec2(x, y);

            // Kill velocity into whatever stopped us.
            var pushed = p.Position - before;
            if (pushed.LengthSquared > 1e-8f)
            {
                var n = pushed.Normalized;
                float into = Vec2.Dot(p.Velocity, n);
                if (into < 0f) p.Velocity -= n * into;
            }
        }

        void SeparatePlayers()
        {
            float minDistance = Config.PlayerRadius * 2f;
            for (int i = 0; i < _players.Length; i++)
            {
                for (int j = i + 1; j < _players.Length; j++)
                {
                    var a = _players[i];
                    var b = _players[j];
                    var delta = b.Position - a.Position;
                    float distance = delta.Length;
                    if (distance >= minDistance) continue;

                    var normal = distance > 1e-4f ? delta / distance : new Vec2(0f, 1f);
                    var push = normal * ((minDistance - distance) * 0.5f);
                    a.Position -= push;
                    b.Position += push;

                    // Equal-mass, mostly inelastic shove: momentum carries into the other player.
                    float closing = Vec2.Dot(a.Velocity - b.Velocity, normal);
                    if (closing > 0f)
                    {
                        var impulse = normal * (closing * (1f + Config.PlayerCollisionRestitution) * 0.5f);
                        a.Velocity -= impulse;
                        b.Velocity += impulse;
                    }
                    ConstrainPlayer(a);
                    ConstrainPlayer(b);
                }
            }
        }

        // ---- Ball ------------------------------------------------------------------------------

        void StepBall(float h)
        {
            BallPhysics.Integrate(ref Ball.Body, Config, _surface, _rng, h);
            ResolvePendingKicks();
            TouchWhileDribbling();
            PlayerContacts();
            KeepAboveGround();
            // Walls last, so nothing (a body or a leg) can leave the ball outside the cage.
            if (CollideWallsAndGoals()) return;
            CollidePosts();

            if (Ball.Owner >= 0)
            {
                var owner = _players[Ball.Owner];
                if (owner.StunTime > 0f || Vec2.Distance(owner.Position, Ball.Position) > Config.LoseBallDistance)
                    Ball.Owner = -1;
            }
        }

        /// <summary>A contact (e.g. a leg striking down on the ball) can't push it into the ground.</summary>
        void KeepAboveGround()
        {
            var c = Ball.Body.Centre;
            if (c.Z >= Config.BallRadius) return;
            Ball.Body.Centre = new Vec3(c.X, c.Y, Config.BallRadius);
            var v = Ball.Body.Velocity;
            if (v.Z < 0f) Ball.Body.Velocity = new Vec3(v.X, v.Y, -v.Z * _surface.Restitution);
        }

        /// <summary>Returns true if a goal was scored.</summary>
        bool CollideWallsAndGoals()
        {
            float r = Config.BallRadius;
            var c = Ball.Body.Centre;
            float midY = Config.PitchWidth * 0.5f;
            bool inMouth = Math.Abs(c.Y - midY) <= Config.GoalWidth * 0.5f && Ball.Height <= Config.GoalHeight;

            if (c.X < r)
            {
                if (inMouth) { GoalScored(1); return true; }
                HitWall(new Vec3(r, c.Y, c.Z), new Vec3(1f, 0f, 0f));
            }
            else if (c.X > Config.PitchLength - r)
            {
                if (inMouth) { GoalScored(0); return true; }
                HitWall(new Vec3(Config.PitchLength - r, c.Y, c.Z), new Vec3(-1f, 0f, 0f));
            }

            c = Ball.Body.Centre;
            if (c.Y < r) HitWall(new Vec3(c.X, r, c.Z), new Vec3(0f, 1f, 0f));
            else if (c.Y > Config.PitchWidth - r) HitWall(new Vec3(c.X, Config.PitchWidth - r, c.Z), new Vec3(0f, -1f, 0f));
            return false;
        }

        void HitWall(Vec3 clampedCentre, Vec3 normal)
        {
            Ball.Body.Centre = clampedCentre;
            float speedIn = -Vec3.Dot(Ball.Body.Velocity, normal);
            if (BallPhysics.Contact(ref Ball.Body, normal, Vec3.Zero, _surface.WallRestitution, Config.WallFriction, Config.BallRadius)
                && speedIn > WallHitSoundSpeed)
            {
                _events.Add(new MatchEvent(MatchEventType.WallHit));
            }
        }

        void CollidePosts()
        {
            if (Ball.Height > Config.GoalHeight) return;
            float r = Config.BallRadius;
            float midY = Config.PitchWidth * 0.5f;
            float postOffset = Config.GoalWidth * 0.5f + Config.PostRadius;
            foreach (float x in new[] { 0f, Config.PitchLength })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var post = new Vec2(x, midY + side * postOffset);
                    var delta = Ball.Position - post;
                    float distance = delta.Length;
                    float minDistance = r + Config.PostRadius;
                    if (distance >= minDistance || distance < 1e-5f) continue;

                    var n = delta / distance;
                    var pushed = post + n * minDistance;
                    Ball.Body.Centre = new Vec3(pushed, Ball.Body.Centre.Z);
                    if (BallPhysics.Contact(ref Ball.Body, new Vec3(n, 0f), Vec3.Zero, Config.PostRestitution, 0.3f, r))
                        _events.Add(new MatchEvent(MatchEventType.Post, LastTouchPlayer));
                }
            }
        }

        bool InKickReach(PlayerState p) =>
            Ball.Height <= Config.KickHeight && Vec2.Distance(p.Position, Ball.Position) <= Config.KickReach;

        void ResolvePendingKicks()
        {
            foreach (var p in _players)
            {
                if (p.PendingKick == KickType.None || p.KickCooldown > 0f || p.StunTime > 0f) continue;
                if (Ball.Owner >= 0 && Ball.Owner != p.Id && TeamOf(Ball.Owner) == p.Team) continue;
                if (!InKickReach(p)) continue;

                if (p.PendingKick == KickType.Shot) Shoot(p);
                else Pass(p);
                p.PendingKick = KickType.None;
                return;
            }
        }

        void Shoot(PlayerState p)
        {
            var goal = GoalCentre(Config, 1 - p.Team);
            float aim = Clamp(p.PendingAim, -1f, 1f);
            var target = new Vec2(goal.X, goal.Y + aim * Config.GoalWidth * 0.5f);
            var plan = ShotPhysics.PlanShot(Config, Ball.Body.Centre, target, p.PendingPower, aim);

            // Accuracy: harder is wilder, and striking it off balance (facing away) is worse.
            var flat = plan.Velocity.XY;
            float offBalance = OffBalance(p, flat);
            float spread = Config.ShotSpreadBase + Config.ShotSpreadPerPower * Clamp(p.PendingPower, 0f, 1f)
                           + Config.OffBalanceSpread * offBalance;
            flat = flat.Rotate((_rng.NextFloat() + _rng.NextFloat() - 1f) * spread) * (1f - 0.15f * offBalance);
            plan.Velocity = new Vec3(flat, plan.Velocity.Z);

            Kick(p, plan);
            _events.Add(new MatchEvent(MatchEventType.Shot, p.Id, team: p.Team));
        }

        void Pass(PlayerState p)
        {
            var aim = p.PendingDirection.LengthSquared > 0.04f ? p.PendingDirection.Normalized : p.Facing;

            PlayerState receiver = null;
            float bestScore = float.MaxValue;
            foreach (var mate in _players)
            {
                if (mate.Team != p.Team || mate.Id == p.Id) continue;
                var toMate = mate.Position - Ball.Position;
                float distance = toMate.Length;
                if (distance < 1f) continue;
                float angle = (float)Math.Acos(Clamp(Vec2.Dot(toMate / distance, aim), -1f, 1f));
                if (angle > Config.PassConeRadians) continue;
                float score = angle * 8f + distance * 0.15f;
                if (score < bestScore)
                {
                    bestScore = score;
                    receiver = mate;
                }
            }

            KickPlan plan;
            if (receiver == null)
            {
                plan = ShotPhysics.PlanPass(Config, Ball.Position, Ball.Position + aim * 10f, p.IsKeeper ? 2f : 1f);
            }
            else
            {
                // Lead a moving receiver: estimate the travel time and aim where they'll be.
                var target = receiver.Position;
                for (int i = 0; i < 2; i++)
                {
                    float distance = Vec2.Distance(target, Ball.Position);
                    float speed = ShotPhysics.PlanPass(Config, Ball.Position, target, p.IsKeeper ? 2f : 1f).Velocity.XY.Length;
                    float time = distance / Math.Max(1f, (speed + Config.PassArrivalSpeed) * 0.5f);
                    target = receiver.Position + receiver.Velocity * time;
                }
                plan = ShotPhysics.PlanPass(Config, Ball.Position, target, p.IsKeeper ? 2f : 1f);
            }

            float offBalance = OffBalance(p, plan.Velocity.XY);
            if (offBalance > 0f)
            {
                var flat = plan.Velocity.XY.Rotate(_rng.Range(-1f, 1f) * 0.12f * offBalance);
                plan.Velocity = new Vec3(flat, 0f);
                plan.Spin = BallPhysics.RollingSpin(flat, Config.BallRadius);
            }

            Kick(p, plan);
            _events.Add(new MatchEvent(MatchEventType.Pass, p.Id, receiver?.Id ?? -1, p.Team));
        }

        /// <summary>0 when facing the kick direction, rising to 1 when kicking back over the shoulder.</summary>
        static float OffBalance(PlayerState p, Vec2 kick)
        {
            if (kick.LengthSquared < 1e-6f) return 0f;
            float angle = (float)Math.Acos(Clamp(Vec2.Dot(p.Facing, kick.Normalized), -1f, 1f));
            return Clamp((angle - 1.05f) / 1.6f, 0f, 1f);
        }

        void Kick(PlayerState p, KickPlan plan)
        {
            Ball.Owner = -1;
            Ball.Body.Velocity = plan.Velocity;
            Ball.Body.Spin = plan.Spin;
            p.KickCooldown = Config.KickCooldownSeconds;
            if (plan.Velocity.XY.LengthSquared > 1e-6f) p.Facing = plan.Velocity.XY.Normalized;
            LastTouchPlayer = LastKickPlayer = p.Id;
        }

        void TouchWhileDribbling()
        {
            if (Ball.Owner < 0) return;
            var p = _players[Ball.Owner];
            if (p.TouchCooldown > 0f || p.PendingKick != KickType.None || p.StunTime > 0f) return;
            if (Ball.Height > Config.KickHeight) return;

            var offset = Ball.Position - p.Position;
            if (offset.Length > Config.TouchReach) return;

            var direction = DribbleDirection(p);
            float lead = p.IsSprinting ? Config.DribbleLeadSprint : Config.DribbleLeadJog;
            float ahead = Vec2.Dot(offset, direction);
            float runningAway = Vec2.Dot(Ball.Velocity - p.Velocity, direction);
            // Already pushed out ahead and still running away: no touch needed yet.
            if (ahead > lead * 0.5f && runningAway > 0.5f) return;

            float speed = Math.Max(0f, Vec2.Dot(p.Velocity, direction));
            Vec2 velocity;
            if (p.DesiredMove.LengthSquared < 0.04f && speed < 0.5f && !p.IsKeeper)
            {
                // Standing still: cushion the ball and keep it at the feet.
                if ((Ball.Velocity - p.Velocity).Length < 0.3f) return;
                velocity = p.Velocity;
            }
            else
            {
                float error = (p.IsSprinting ? TouchErrorSprint : TouchErrorJog) * (1f - 0.5f * Config.DribbleAssist);
                velocity = direction.Rotate(_rng.Range(-1f, 1f) * error) * (speed + lead * TouchPushPerLead);
            }

            Ball.Body.Velocity = new Vec3(velocity, Math.Min(0f, Ball.Body.Velocity.Z));
            Ball.Body.Spin = BallPhysics.RollingSpin(velocity, Config.BallRadius);
            p.TouchCooldown = Config.TouchCooldownSeconds;
            LastTouchPlayer = LastKickPlayer = p.Id;
        }

        static Vec2 DribbleDirection(PlayerState p)
        {
            if (p.IsKeeper)
            {
                // A keeper on the ball always keeps it in front of them, away from their goal.
                var outward = new Vec2(AttackDirection(p.Team), 0f);
                return Vec2.Dot(p.DesiredMove, outward) > 0.2f ? p.DesiredMove.Normalized : outward;
            }
            if (p.DesiredMove.LengthSquared > 0.04f) return p.DesiredMove.Normalized;
            if (p.Velocity.LengthSquared > 0.25f) return p.Velocity.Normalized;
            return p.Facing;
        }

        /// <summary>
        /// The touch assist: gently steers a dribbled ball towards the ideal spot ahead of the
        /// carrier, so free-ball dribbling stays playable on a touchscreen.
        /// </summary>
        void DribbleAssist(float dt)
        {
            if (Ball.Owner < 0 || Config.DribbleAssist <= 0f || Ball.Height > 0.05f) return;
            var p = _players[Ball.Owner];
            if (p.StunTime > 0f || Vec2.Distance(p.Position, Ball.Position) > AssistRadius) return;

            var direction = DribbleDirection(p);
            float lead = (p.IsSprinting ? Config.DribbleLeadSprint : Config.DribbleLeadJog) * 0.7f;
            var leadPoint = p.Position + direction * lead;
            var desired = p.Velocity + (leadPoint - Ball.Position) * 2.5f;
            float blend = Clamp(Config.DribbleAssist * 3f * dt, 0f, 1f);
            var velocity = Ball.Velocity + (desired - Ball.Velocity) * blend;
            Ball.Body.Velocity = new Vec3(velocity, 0f);
            Ball.Body.Spin = BallPhysics.RollingSpin(velocity, Config.BallRadius);
        }

        void PlayerContacts()
        {
            // First, can someone bring the ball under control?
            PlayerState controller = null;
            float bestDistance = float.MaxValue;
            foreach (var p in _players)
            {
                if (!CanControl(p)) continue;
                float distance = Vec2.Distance(p.Position, Ball.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    controller = p;
                }
            }
            if (controller != null) FirstTouch(controller);

            // Then bodies and keepers' legs deflect the ball.
            foreach (var p in _players)
            {
                if (p == controller || p.Id == Ball.Owner || p.KickCooldown > 0f) continue;
                BodyContact(p);
                if (p.IsKeeper && p.StretchTime > 0f) FootContact(p);
            }
        }

        bool CanControl(PlayerState p)
        {
            if (p.Id == Ball.Owner || p.StunTime > 0f || p.KickCooldown > 0f || p.PendingKick != KickType.None) return false;
            if (Ball.Height > Config.ControlHeight) return false;
            float reach = p.IsKeeper ? Config.KeeperControlReach : Config.ControlReach;
            float distance = Vec2.Distance(p.Position, Ball.Position);
            if (distance > reach) return false;

            var relative = Ball.Body.Velocity - new Vec3(p.Velocity, 0f);
            if (relative.Length > Config.ControlMaxSpeed) return false;

            if (Ball.Owner >= 0)
            {
                var owner = _players[Ball.Owner];
                if (owner.Team == p.Team) return false;
                // An opponent can only pick off a dribbled ball between touches, when it's away
                // from the carrier's feet and closer to them. Otherwise they must tackle.
                float ownerDistance = Vec2.Distance(owner.Position, Ball.Position);
                if (ownerDistance < 0.9f || ownerDistance < distance) return false;
            }
            return true;
        }

        void FirstTouch(PlayerState p)
        {
            var relative = Ball.Body.Velocity - new Vec3(p.Velocity, 0f);
            float incoming = relative.Length;
            float error = Math.Max(0f, incoming - Config.FirstTouchCleanSpeed) * Config.FirstTouchErrorPerSpeed;
            if (Ball.Height > 0.3f) error *= 1.5f;
            error = Math.Min(error, 0.8f);

            // Kill the ball and set it moving the way the player wants to go; fast or bouncing
            // balls are harder to kill, so the touch can run away from them.
            // Keepers always take their touch away from their own goal.
            var direction = p.IsKeeper ? new Vec2(AttackDirection(p.Team), 0f) : DribbleDirection(p);
            var miss = new Vec2(1f, 0f).Rotate(_rng.Range(0f, 6.2831853f)) * (incoming * error);
            var velocity = p.Velocity + direction * 1.2f + miss;
            Ball.Body.Velocity = new Vec3(velocity, Math.Min(0f, Ball.Body.Velocity.Z) * 0.3f);
            Ball.Body.Spin = BallPhysics.RollingSpin(velocity, Config.BallRadius);

            Ball.Owner = p.Id;
            p.TouchCooldown = Config.TouchCooldownSeconds;
            LastTouchPlayer = LastKickPlayer = p.Id;
        }

        void BodyContact(PlayerState p)
        {
            float r = Config.BallRadius;
            if (Ball.Height > Config.BodyHeight) return;
            float bodyRadius = p.IsKeeper ? Config.KeeperBodyRadius : Config.BodyRadius;
            var delta = Ball.Position - p.Position;
            float distance = delta.Length;
            float minDistance = bodyRadius + r;
            if (distance >= minDistance) return;

            var n = distance > 1e-4f ? delta / distance : -Ball.Velocity.Normalized;
            if (n == Vec2.Zero) n = p.Facing;
            Ball.Body.Centre = new Vec3(p.Position + n * minDistance, Ball.Body.Centre.Z);
            Deflect(p, new Vec3(n, 0f));
        }

        void FootContact(PlayerState keeper)
        {
            float r = Config.BallRadius;
            var foot = new Vec3(keeper.Position + keeper.StretchDirection * Config.KeeperStretchReach, Config.KeeperFootRadius);
            var delta = Ball.Body.Centre - foot;
            float distance = delta.Length;
            float minDistance = Config.KeeperFootRadius + r;
            if (distance >= minDistance || distance < 1e-5f) return;

            var n = delta / distance;
            Ball.Body.Centre = foot + n * minDistance;
            Deflect(keeper, n);
        }

        void Deflect(PlayerState p, Vec3 normal)
        {
            var before = Ball.Body.Velocity;
            if (!BallPhysics.Contact(ref Ball.Body, normal, new Vec3(p.Velocity, 0f), Config.BodyRestitution, Config.BodyFriction, Config.BallRadius))
                return;
            LastTouchPlayer = p.Id;
            if (Ball.Owner >= 0 && Ball.Owner != p.Id) Ball.Owner = -1;
            // A block deflects the ball; the blocker can't instantly kill the rebound at their feet.
            p.KickCooldown = Math.Max(p.KickCooldown, 0.2f);

            if (before.Length < BlockEventSpeed) return;
            bool goalbound = Vec2.Dot(before.XY, new Vec2(-AttackDirection(p.Team), 0f)) > 0f;
            var type = p.IsKeeper && goalbound ? MatchEventType.Save : MatchEventType.Block;
            _events.Add(new MatchEvent(type, p.Id, team: p.Team));
        }

        void TryTackle(PlayerState tackler)
        {
            tackler.TackleCooldown = Config.TackleCooldownSeconds;
            if (Ball.Owner < 0) return;
            var victim = _players[Ball.Owner];
            if (victim.Team == tackler.Team) return;

            var toBall = Ball.Position - tackler.Position;
            float distance = toBall.Length;
            if (distance > Config.TackleRange || Ball.Height > Config.KickHeight) return;

            // Timing matters: a ball between touches (away from the carrier's feet) is easier to win.
            float looseness = Clamp((Vec2.Distance(victim.Position, Ball.Position) - 0.35f) / 0.9f, 0f, 1f);
            float facing = distance > 0.01f ? Math.Max(0f, Vec2.Dot(tackler.Facing, toBall / distance)) : 1f;
            float chance = Config.TackleSuccessChance * (0.6f + 0.4f * facing) * (0.55f + 0.45f * looseness);

            if (_rng.Chance(chance))
            {
                Ball.Owner = -1;
                var knock = tackler.Facing.Rotate(_rng.Range(-0.4f, 0.4f)) * Config.TackleKnockSpeed + tackler.Velocity * 0.3f;
                Ball.Body.Velocity = new Vec3(knock, 0f);
                Ball.Body.Spin = BallPhysics.RollingSpin(knock, Config.BallRadius);
                victim.StunTime = Config.TackledStunSeconds;
                victim.PendingKick = KickType.None;
                LastTouchPlayer = LastKickPlayer = tackler.Id;
                _events.Add(new MatchEvent(MatchEventType.Tackle, tackler.Id, victim.Id, tackler.Team));
            }
            else
            {
                tackler.StunTime = Config.TackleMissStunSeconds;
                _events.Add(new MatchEvent(MatchEventType.TackleMissed, tackler.Id, victim.Id, tackler.Team));
            }
        }

        void Stretch(PlayerState keeper)
        {
            keeper.TackleCooldown = Config.TackleCooldownSeconds;
            keeper.StretchTime = Config.KeeperStretchSeconds;
            var toBall = Ball.Position - keeper.Position;
            keeper.StretchDirection = toBall.LengthSquared > 1e-4f ? toBall.Normalized : keeper.Facing;
        }

        // ---- Match flow ------------------------------------------------------------------------

        void GoalScored(int team)
        {
            _score[team]++;
            Ball.Owner = -1;
            Ball.Body.Velocity = Vec3.Zero;
            Ball.Body.Spin = Vec3.Zero;
            Phase = Phase.GoalScored;
            PhaseTime = 0f;
            KickoffTeam = 1 - team;
            _events.Add(new MatchEvent(MatchEventType.Goal, GoalScorer(team), team: team));
        }

        /// <summary>
        /// Who gets the goal: a shot that deflects in off a defender or the keeper is still the
        /// shooter's goal. It's only an own goal if the defending side played it in themselves.
        /// </summary>
        int GoalScorer(int scoringTeam)
        {
            if (LastTouchPlayer < 0 || TeamOf(LastTouchPlayer) == scoringTeam) return LastTouchPlayer;
            bool deflected = LastKickPlayer != LastTouchPlayer;
            if (deflected && LastKickPlayer >= 0 && TeamOf(LastKickPlayer) == scoringTeam) return LastKickPlayer;
            return LastTouchPlayer;
        }

        void EndMatch()
        {
            Phase = Phase.Ended;
            _events.Add(new MatchEvent(MatchEventType.MatchEnded));
        }

        void ResetForKickoff(int team)
        {
            foreach (var p in _players)
            {
                p.Position = FormationSpot(Config, p.Team, p.Slot);
                p.Velocity = Vec2.Zero;
                p.Facing = new Vec2(AttackDirection(p.Team), 0f);
                p.StunTime = p.KickCooldown = p.TackleCooldown = p.TouchCooldown = p.StretchTime = 0f;
                p.PendingKick = KickType.None;
                p.PendingTime = 0f;
                p.DesiredMove = Vec2.Zero;
                p.IsSprinting = false;
            }

            var taker = _players[PlayerId(team, KickoffSlot)];
            var centre = new Vec2(Config.PitchLength * 0.5f, Config.PitchWidth * 0.5f);
            taker.Position = centre - taker.Facing * 0.5f;

            Ball.Owner = taker.Id;
            Ball.Body = new BallBody { Centre = new Vec3(centre, Config.BallRadius) };
            LastTouchPlayer = LastKickPlayer = taker.Id;

            KickoffTeam = team;
            Phase = Phase.Kickoff;
            PhaseTime = 0f;
            _events.Add(new MatchEvent(MatchEventType.Kickoff, taker.Id, team: team));
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        // ---- Test and training-mode setup ------------------------------------------------------

        internal void SkipKickoff()
        {
            Phase = Phase.Play;
            PhaseTime = 0f;
        }

        internal void PlacePlayer(int id, Vec2 position, Vec2 facing)
        {
            var p = _players[id];
            p.Position = position;
            p.Velocity = Vec2.Zero;
            p.Facing = facing.Normalized;
        }

        internal void PlaceBall(Vec2 position, Vec2 velocity, int owner = -1, float height = 0f, float verticalVelocity = 0f, Vec3 spin = default)
        {
            Ball.Body = new BallBody
            {
                Centre = new Vec3(position, height + Config.BallRadius),
                Velocity = new Vec3(velocity, verticalVelocity),
                Spin = spin,
            };
            Ball.Owner = owner;
            if (owner >= 0) LastTouchPlayer = LastKickPlayer = owner;
        }
    }
}
