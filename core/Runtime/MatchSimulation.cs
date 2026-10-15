using System;
using System.Collections.Generic;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Authoritative 5-a-side Monkey Post match: 4 outfield players and a feet-only keeper per
    /// team, tiny goals, walled pitch, one clock. In local multiplayer only the host runs this;
    /// guests and bots see the match through <see cref="MatchSnapshot"/>.
    /// Players 0–4 are the home team (0 = keeper), 5–9 the away team (5 = keeper).
    /// </summary>
    public sealed class MatchSimulation
    {
        public const int PlayersPerTeam = 5;
        public const int PlayerCount = PlayersPerTeam * 2;
        public const int KickoffSlot = 4;

        /// <summary>Ball physics substeps per tick, so fast shots can't tunnel through players.</summary>
        const int BallSubsteps = 3;

        // Formation as (fraction of pitch length from own goal, fraction of width): GK, 2 DEF, 2 ATT.
        static readonly float[] FormationDepth = { 0.06f, 0.25f, 0.25f, 0.42f, 0.42f };
        static readonly float[] FormationWidth = { 0.5f, 0.3f, 0.7f, 0.35f, 0.65f };

        readonly PlayerState[] _players = new PlayerState[PlayerCount];
        readonly int[] _score = new int[2];
        readonly List<MatchEvent> _events = new List<MatchEvent>();
        readonly DeterministicRandom _rng;

        public MatchConfig Config { get; }
        public Phase Phase { get; private set; }
        public float TimeRemaining { get; private set; }
        public float PhaseTime { get; private set; }
        public int KickoffTeam { get; private set; }
        public int LastTouchPlayer { get; private set; } = -1;
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
            _rng = new DeterministicRandom(seed);
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

            TimeRemaining -= dt;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Phase = Phase.Ended;
                _events.Add(new MatchEvent(MatchEventType.MatchEnded));
                return;
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
                    if (PhaseTime >= Config.GoalCelebrationSeconds) ResetForKickoff(KickoffTeam);
                    break;
                case Phase.Play:
                    StepPlay(inputs, dt);
                    break;
            }
        }

        void StepPlay(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            foreach (var player in _players) MovePlayer(player, inputs[player.Id], dt);
            SeparatePlayers();

            foreach (var player in _players)
            {
                if (player.StunTime > 0f) continue;
                var input = inputs[player.Id];
                if (Ball.Owner == player.Id)
                {
                    if (input.Shoot) Shoot(player, input);
                    else if (input.Pass) Pass(player, input);
                }
                else if (input.Tackle && player.TackleCooldown <= 0f)
                {
                    TryTackle(player);
                }
            }

            if (Ball.Owner >= 0)
            {
                Dribble(_players[Ball.Owner]);
                return;
            }

            float h = dt / BallSubsteps;
            for (int i = 0; i < BallSubsteps && Ball.Owner < 0 && Phase == Phase.Play; i++)
            {
                StepBall(h);
                if (Phase == Phase.Play) TryClaimBall();
            }
        }

        void MovePlayer(PlayerState p, PlayerInput input, float dt)
        {
            p.StunTime = Math.Max(0f, p.StunTime - dt);
            p.KickCooldown = Math.Max(0f, p.KickCooldown - dt);
            p.TackleCooldown = Math.Max(0f, p.TackleCooldown - dt);

            var desired = Vec2.Zero;
            bool sprinting = false;
            if (p.StunTime <= 0f)
            {
                var move = input.Move.ClampLength(1f);
                sprinting = input.Sprint && p.Stamina > 0f && move.LengthSquared > 0.01f;
                float speed = sprinting ? Config.SprintSpeed : Config.JogSpeed;
                if (Ball.Owner == p.Id) speed *= Config.DribbleSpeedFactor;
                desired = move * speed;
            }

            p.Stamina = sprinting
                ? Math.Max(0f, p.Stamina - Config.StaminaDrainPerSecond * dt)
                : Math.Min(1f, p.Stamina + Config.StaminaRegenPerSecond * dt);
            p.IsSprinting = sprinting;

            p.Velocity = Vec2.MoveTowards(p.Velocity, desired, Config.Acceleration * dt);
            p.Position += p.Velocity * dt;
            if (p.Velocity.LengthSquared > 0.01f) p.Facing = p.Velocity.Normalized;
            ConstrainPlayer(p);
        }

        void ConstrainPlayer(PlayerState p)
        {
            float r = Config.PlayerRadius;
            float midY = Config.PitchWidth * 0.5f;
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
                    ConstrainPlayer(a);
                    ConstrainPlayer(b);
                }
            }
        }

        void Dribble(PlayerState owner)
        {
            var target = owner.Position + owner.Facing * Config.DribbleOffset;
            float r = Config.BallRadius;
            Ball.Position = new Vec2(Clamp(target.X, r, Config.PitchLength - r), Clamp(target.Y, r, Config.PitchWidth - r));
            Ball.Velocity = owner.Velocity;
            Ball.Height = 0f;
            Ball.VerticalVelocity = 0f;
        }

        void Shoot(PlayerState p, PlayerInput input)
        {
            float power = Clamp(input.Power, 0f, 1f);
            float aim = Clamp(input.Aim, -1f, 1f);
            var goal = GoalCentre(Config, 1 - p.Team);
            var target = new Vec2(goal.X, goal.Y + aim * Config.GoalWidth * 0.5f);

            var direction = (target - Ball.Position).Normalized;
            if (direction == Vec2.Zero) direction = new Vec2(AttackDirection(p.Team), 0f);
            float spread = Config.ShotSpreadBase + Config.ShotSpreadPerPower * power;
            direction = direction.Rotate((_rng.NextFloat() + _rng.NextFloat() - 1f) * spread);

            float speed = Config.ShotMinSpeed + (Config.ShotMaxSpeed - Config.ShotMinSpeed) * power;
            Kick(p, direction * speed, Config.ShotMaxLift * power * power);
            _events.Add(new MatchEvent(MatchEventType.Shot, p.Id, team: p.Team));
        }

        void Pass(PlayerState p, PlayerInput input)
        {
            var aim = input.Move.LengthSquared > 0.04f ? input.Move.Normalized : p.Facing;

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

            if (receiver == null)
            {
                Kick(p, aim * Config.PassMinSpeed * 1.4f, 0f);
                _events.Add(new MatchEvent(MatchEventType.Pass, p.Id, team: p.Team));
                return;
            }

            float d = Vec2.Distance(receiver.Position, Ball.Position);
            float speed = Clamp(Config.PassMinSpeed + Config.PassSpeedPerMetre * d, Config.PassMinSpeed, Config.PassMaxSpeed);
            var lead = receiver.Position + receiver.Velocity * (d / speed * 0.8f);
            Kick(p, (lead - Ball.Position).Normalized * speed, 0f);
            _events.Add(new MatchEvent(MatchEventType.Pass, p.Id, receiver.Id, p.Team));
        }

        void Kick(PlayerState p, Vec2 velocity, float lift)
        {
            Ball.Owner = -1;
            Ball.Velocity = velocity;
            Ball.VerticalVelocity = lift;
            p.KickCooldown = Config.KickCooldownSeconds;
            if (velocity.LengthSquared > 1e-6f) p.Facing = velocity.Normalized;
            LastTouchPlayer = p.Id;
        }

        void TryTackle(PlayerState tackler)
        {
            tackler.TackleCooldown = Config.TackleCooldownSeconds;
            if (Ball.Owner < 0) return;
            var victim = _players[Ball.Owner];
            if (victim.Team == tackler.Team) return;

            var toBall = Ball.Position - tackler.Position;
            float distance = toBall.Length;
            if (distance > Config.TackleRange) return;

            float facing = distance > 0.01f ? Math.Max(0f, Vec2.Dot(tackler.Facing, toBall / distance)) : 1f;
            float chance = Config.TackleSuccessChance * (0.6f + 0.4f * facing);
            if (_rng.Chance(chance))
            {
                Ball.Owner = -1;
                Ball.Velocity = tackler.Facing.Rotate(_rng.Range(-0.4f, 0.4f)) * Config.TackleKnockSpeed;
                victim.StunTime = Config.TackledStunSeconds;
                victim.KickCooldown = Math.Max(victim.KickCooldown, Config.TackledStunSeconds + 0.1f);
                LastTouchPlayer = tackler.Id;
                _events.Add(new MatchEvent(MatchEventType.Tackle, tackler.Id, victim.Id, tackler.Team));
            }
            else
            {
                tackler.StunTime = Config.TackleMissStunSeconds;
                _events.Add(new MatchEvent(MatchEventType.TackleMissed, tackler.Id, victim.Id, tackler.Team));
            }
        }

        void StepBall(float h)
        {
            if (Ball.Height > 0f || Ball.VerticalVelocity > 0f)
            {
                Ball.VerticalVelocity -= Config.Gravity * h;
                Ball.Height += Ball.VerticalVelocity * h;
                if (Ball.Height <= 0f)
                {
                    Ball.Height = 0f;
                    Ball.VerticalVelocity = Ball.VerticalVelocity < -1f ? -Ball.VerticalVelocity * Config.GroundRestitution : 0f;
                }
            }

            float speed = Ball.Velocity.Length;
            if (speed > 0f)
            {
                float drag = Ball.Height <= 0f ? Config.GroundFriction : Config.AirDrag;
                Ball.Velocity *= Math.Max(0f, speed - drag * h) / speed;
            }

            var p = Ball.Position + Ball.Velocity * h;
            var v = Ball.Velocity;
            float r = Config.BallRadius;
            float midY = Config.PitchWidth * 0.5f;
            bool inMouth = Math.Abs(p.Y - midY) <= Config.GoalWidth * 0.5f && Ball.Height <= Config.GoalHeight;

            if (p.X < r)
            {
                if (inMouth) { GoalScored(1); return; }
                p = new Vec2(2f * r - p.X, p.Y);
                v = new Vec2(-v.X * Config.WallRestitution, v.Y);
            }
            else if (p.X > Config.PitchLength - r)
            {
                if (inMouth) { GoalScored(0); return; }
                p = new Vec2(2f * (Config.PitchLength - r) - p.X, p.Y);
                v = new Vec2(-v.X * Config.WallRestitution, v.Y);
            }

            if (p.Y < r)
            {
                p = new Vec2(p.X, 2f * r - p.Y);
                v = new Vec2(v.X, -v.Y * Config.WallRestitution);
            }
            else if (p.Y > Config.PitchWidth - r)
            {
                p = new Vec2(p.X, 2f * (Config.PitchWidth - r) - p.Y);
                v = new Vec2(v.X, -v.Y * Config.WallRestitution);
            }

            Ball.Position = p;
            Ball.Velocity = v;
        }

        void TryClaimBall()
        {
            PlayerState best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in _players)
            {
                if (p.StunTime > 0f || p.KickCooldown > 0f || Ball.Height > Config.ControlHeight) continue;
                float reach = p.IsKeeper ? Config.KeeperBlockRadius : Config.ControlRadius;
                float distance = Vec2.Distance(p.Position, Ball.Position);
                if (distance <= reach && distance < bestDistance)
                {
                    best = p;
                    bestDistance = distance;
                }
            }
            if (best == null) return;

            float speed = Ball.Velocity.Length;
            if (speed > Config.ControlMaxSpeed)
            {
                Deflect(best, speed);
                return;
            }

            Ball.Owner = best.Id;
            Ball.Velocity = Vec2.Zero;
            Ball.Height = 0f;
            Ball.VerticalVelocity = 0f;
            LastTouchPlayer = best.Id;
        }

        void Deflect(PlayerState blocker, float speed)
        {
            var ownGoalDirection = new Vec2(-AttackDirection(blocker.Team), 0f);
            bool wasGoalbound = Vec2.Dot(Ball.Velocity, ownGoalDirection) > 0f;

            var away = (Ball.Position - blocker.Position).Normalized;
            if (away == Vec2.Zero) away = -Ball.Velocity.Normalized;
            Ball.Velocity = away.Rotate(_rng.Range(-0.6f, 0.6f)) * (speed * 0.45f);
            Ball.VerticalVelocity = _rng.Range(1.5f, 3f);
            blocker.KickCooldown = 0.25f;
            LastTouchPlayer = blocker.Id;

            var type = blocker.IsKeeper && wasGoalbound ? MatchEventType.Save : MatchEventType.Block;
            _events.Add(new MatchEvent(type, blocker.Id, team: blocker.Team));
        }

        void GoalScored(int team)
        {
            _score[team]++;
            Ball.Velocity = Vec2.Zero;
            Ball.VerticalVelocity = 0f;
            Phase = Phase.GoalScored;
            PhaseTime = 0f;
            KickoffTeam = 1 - team;
            _events.Add(new MatchEvent(MatchEventType.Goal, LastTouchPlayer, team: team));
        }

        void ResetForKickoff(int team)
        {
            foreach (var p in _players)
            {
                p.Position = FormationSpot(Config, p.Team, p.Slot);
                p.Velocity = Vec2.Zero;
                p.Facing = new Vec2(AttackDirection(p.Team), 0f);
                p.StunTime = 0f;
                p.KickCooldown = 0f;
                p.TackleCooldown = 0f;
                p.IsSprinting = false;
            }

            var taker = _players[PlayerId(team, KickoffSlot)];
            var centre = new Vec2(Config.PitchLength * 0.5f, Config.PitchWidth * 0.5f);
            taker.Position = centre - taker.Facing * Config.DribbleOffset;

            Ball.Owner = taker.Id;
            Ball.Position = centre;
            Ball.Velocity = Vec2.Zero;
            Ball.Height = 0f;
            Ball.VerticalVelocity = 0f;
            LastTouchPlayer = taker.Id;

            KickoffTeam = team;
            Phase = Phase.Kickoff;
            PhaseTime = 0f;
            _events.Add(new MatchEvent(MatchEventType.Kickoff, taker.Id, team: team));
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        // Test and training-mode setup.

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

        internal void PlaceBall(Vec2 position, Vec2 velocity, int owner = -1, float height = 0f, float verticalVelocity = 0f)
        {
            Ball.Position = position;
            Ball.Velocity = velocity;
            Ball.Owner = owner;
            Ball.Height = height;
            Ball.VerticalVelocity = verticalVelocity;
            if (owner >= 0) LastTouchPlayer = owner;
        }
    }
}
