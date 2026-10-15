using System;

namespace MonkeyPost.Core.Bots
{
    /// <summary>
    /// Plays every footballer on one team except the one a person is controlling. Decides only
    /// from <see cref="MatchSnapshot"/>, with a deliberately stale view of the ball so easier
    /// teams react late.
    /// </summary>
    public sealed class TeamBrain
    {
        const int Slots = MatchSimulation.PlayersPerTeam;
        const float PressureRadius = 2.5f;
        const float ArriveRadius = 2f;
        const float KeeperClaimRadius = 4f;
        const float KeeperHoldSeconds = 0.6f;
        const float MinPassDistance = 3f;
        const float MaxPassDistance = 20f;
        const float MinPassLaneClearance = 1.2f;

        readonly MatchConfig _config;
        readonly BotProfile _profile;
        readonly DeterministicRandom _rng;
        readonly float[] _nextDecision = new float[Slots];
        readonly PlayerInput[] _plan = new PlayerInput[Slots];

        float _time;
        float _keeperHeldSince = -1f;

        // The team's picture of the ball, refreshed every ReactionSeconds.
        Vec2 _seenBall;
        Vec2 _seenBallVelocity;
        float _seenAt = float.NegativeInfinity;

        public int Team { get; }
        public BotProfile Profile => _profile;

        public TeamBrain(int team, MatchConfig config, BotProfile profile, ulong seed)
        {
            if (team != 0 && team != 1) throw new ArgumentOutOfRangeException(nameof(team));
            Team = team;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rng = new DeterministicRandom(seed);
        }

        /// <summary>Writes inputs for this team's players into <paramref name="inputs"/>, skipping <paramref name="humanControlledId"/>.</summary>
        public void Think(MatchSnapshot view, float dt, int humanControlledId, PlayerInput[] inputs)
        {
            _time += dt;
            if (_time - _seenAt >= _profile.ReactionSeconds)
            {
                _seenBall = view.Ball.Position;
                _seenBallVelocity = view.Ball.Velocity;
                _seenAt = _time;
            }

            var ballGuess = PerceivedBall(0.25f);
            int chaser = ClosestOutfield(view, ballGuess, -1);
            int cover = ClosestOutfield(view, ballGuess, chaser);

            for (int slot = 0; slot < Slots; slot++)
            {
                int id = MatchSimulation.PlayerId(Team, slot);
                if (id == humanControlledId) continue;
                inputs[id] = view.Phase == Phase.Play ? Decide(view, slot, id, chaser, cover) : default;
            }
        }

        PlayerInput Decide(MatchSnapshot view, int slot, int id, int chaser, int cover)
        {
            var me = view.Players[id];
            if (me.IsStunned) return default;

            bool decide = _time >= _nextDecision[slot];
            if (decide) _nextDecision[slot] = _time + _profile.DecisionInterval * _rng.Range(0.8f, 1.2f);

            var ball = view.Ball;
            if (ball.Owner == id) return OnBall(view, me, slot, decide);
            if (me.IsKeeper) return Keeper(view, me);
            if (ball.Owner >= 0 && MatchSimulation.TeamOf(ball.Owner) == Team) return Shape(me, slot, attacking: true);
            if (id == chaser) return Chase(view, me, decide);
            if (id == cover) return Cover(me);
            return Shape(me, slot, attacking: false);
        }

        PlayerInput OnBall(MatchSnapshot view, PlayerSnapshot me, int slot, bool decide)
        {
            if (me.IsKeeper)
            {
                // Feet-only keeper: get rid of it quickly to an open teammate.
                if (_keeperHeldSince < 0f) _keeperHeldSince = _time;
                if (_time - _keeperHeldSince < KeeperHoldSeconds) return default;
                _keeperHeldSince = -1f;
                int target = BestPassTarget(view, me, requireForward: false);
                var direction = target >= 0
                    ? (view.Players[target].Position - me.Position).Normalized
                    : new Vec2(MatchSimulation.AttackDirection(Team), 0f);
                return new PlayerInput { Move = direction, Pass = true };
            }

            if (!decide)
            {
                var carry = _plan[slot];
                carry.Pass = carry.Shoot = false;
                return carry;
            }

            var oppGoal = MatchSimulation.GoalCentre(_config, 1 - Team);
            float distanceToGoal = Vec2.Distance(me.Position, oppGoal);
            bool goodAngle = Math.Abs(me.Position.Y - oppGoal.Y) < distanceToGoal * 0.8f + 1f;

            if (distanceToGoal < _profile.ShootRange && goodAngle)
            {
                var keeper = view.Players[MatchSimulation.PlayerId(1 - Team, 0)];
                float awayFromKeeper = keeper.Position.Y > oppGoal.Y ? -1f : 1f;
                float aim = _rng.Chance(_profile.AimSmartness)
                    ? awayFromKeeper * _rng.Range(0.35f, 0.8f)
                    : _rng.Range(-0.8f, 0.8f);
                // Shoot harder from further out, but softly enough to stay under knee height.
                float power = 0.35f + distanceToGoal * 0.035f + _rng.Range(-_profile.PowerError, _profile.PowerError);
                return new PlayerInput { Shoot = true, Aim = aim, Power = Math.Max(0.25f, Math.Min(0.95f, power)) };
            }

            float nearestOpponent = NearestOpponentDistance(view, me.Position);
            int mate = BestPassTarget(view, me, requireForward: nearestOpponent >= PressureRadius);
            if (mate >= 0 && (nearestOpponent < PressureRadius || _rng.Chance(_profile.PassVision * 0.25f)))
            {
                return new PlayerInput { Move = (view.Players[mate].Position - me.Position).Normalized, Pass = true };
            }

            _plan[slot] = new PlayerInput
            {
                Move = DribbleDirection(view, me, oppGoal),
                Sprint = nearestOpponent > 3f && me.Stamina > 0.3f,
            };
            return _plan[slot];
        }

        Vec2 DribbleDirection(MatchSnapshot view, PlayerSnapshot me, Vec2 goal)
        {
            var direction = (goal - me.Position).Normalized;
            foreach (var p in view.Players)
            {
                if (p.Team == Team) continue;
                var toOpponent = p.Position - me.Position;
                float distance = toOpponent.Length;
                if (distance > 4f || distance < 1e-3f) continue;
                if (Vec2.Dot(toOpponent / distance, direction) < 0.3f) continue;

                // Steer round opponents in front, away from whichever side they're on.
                var side = direction.Perpendicular;
                float sign = Vec2.Dot(toOpponent, side) > 0f ? -1f : 1f;
                direction += side * (sign * (1f - distance / 4f) * 1.2f);
            }
            return direction.Normalized;
        }

        int BestPassTarget(MatchSnapshot view, PlayerSnapshot me, bool requireForward)
        {
            float attack = MatchSimulation.AttackDirection(Team);
            int best = -1;
            float bestScore = float.MinValue;
            for (int slot = 1; slot < Slots; slot++)
            {
                var mate = view.Players[MatchSimulation.PlayerId(Team, slot)];
                if (mate.Id == me.Id) continue;
                float distance = Vec2.Distance(me.Position, mate.Position);
                if (distance < MinPassDistance || distance > MaxPassDistance) continue;

                float clearance = float.MaxValue;
                foreach (var p in view.Players)
                {
                    if (p.Team == Team) continue;
                    clearance = Math.Min(clearance, Vec2.DistanceToSegment(p.Position, me.Position, mate.Position));
                }
                if (clearance < MinPassLaneClearance) continue;

                float progress = (mate.Position.X - me.Position.X) * attack;
                if (requireForward && progress < 2f) continue;

                float score = progress * 0.6f + Math.Min(clearance, 4f) - distance * 0.1f
                              + _rng.NextFloat() * (1f - _profile.PassVision) * 6f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = mate.Id;
                }
            }
            return best;
        }

        PlayerInput Keeper(MatchSnapshot view, PlayerSnapshot me)
        {
            _keeperHeldSince = -1f;
            var goal = MatchSimulation.GoalCentre(_config, Team);
            var ball = PerceivedBall(_profile.KeeperAnticipation);

            // Rush out to clear a loose ball in the box if nobody else will get there first.
            if (view.Ball.Owner < 0
                && Vec2.Distance(me.Position, view.Ball.Position) < KeeperClaimRadius
                && Vec2.Distance(me.Position, view.Ball.Position) < NearestOpponentDistance(view, view.Ball.Position))
            {
                return new PlayerInput { Move = (view.Ball.Position - me.Position).Normalized, Sprint = true };
            }

            // Otherwise stand on the line between ball and goal, close to the goal.
            var toBall = ball - goal;
            var spot = goal + toBall.Normalized * Math.Min(0.7f, toBall.Length * 0.1f);
            float maxSide = _config.GoalWidth * 0.5f + 0.5f;
            spot = new Vec2(spot.X, Math.Max(goal.Y - maxSide, Math.Min(goal.Y + maxSide, spot.Y)));
            return Arrive(me, spot, sprintBeyond: 3f);
        }

        PlayerInput Chase(MatchSnapshot view, PlayerSnapshot me, bool decide)
        {
            var target = PerceivedBall(0.25f);
            float distance = Vec2.Distance(me.Position, target);
            var input = new PlayerInput
            {
                Move = (target - me.Position).Normalized,
                Sprint = distance > 2.5f && me.Stamina > 0.2f,
            };

            var ball = view.Ball;
            if (decide && ball.Owner >= 0 && MatchSimulation.TeamOf(ball.Owner) != Team
                && Vec2.Distance(me.Position, ball.Position) <= _config.TackleRange * 0.9f
                && _rng.Chance(_profile.TackleChance))
            {
                input.Tackle = true;
            }
            return input;
        }

        PlayerInput Cover(PlayerSnapshot me)
        {
            var ball = PerceivedBall(0f);
            var goal = MatchSimulation.GoalCentre(_config, Team);
            return Arrive(me, ball + (goal - ball).Normalized * 4f, sprintBeyond: 6f);
        }

        PlayerInput Shape(PlayerSnapshot me, int slot, bool attacking)
        {
            var home = MatchSimulation.FormationSpot(_config, Team, slot);
            var ball = PerceivedBall(0f);
            float push = (attacking ? 0.18f : -0.03f) * _config.PitchLength * MatchSimulation.AttackDirection(Team);
            float pull = attacking ? 0.25f : 0.35f;
            var spot = new Vec2(home.X + push, home.Y + (ball.Y - home.Y) * pull);
            return Arrive(me, spot, sprintBeyond: 6f);
        }

        PlayerInput Arrive(PlayerSnapshot me, Vec2 target, float sprintBeyond)
        {
            var delta = target - me.Position;
            float distance = delta.Length;
            if (distance < 0.25f) return default;
            return new PlayerInput
            {
                Move = delta / distance * Math.Min(1f, distance / ArriveRadius),
                Sprint = distance > sprintBeyond && me.Stamina > 0.4f,
            };
        }

        Vec2 PerceivedBall(float lookAhead) =>
            _seenBall + _seenBallVelocity * (_time - _seenAt + lookAhead);

        int ClosestOutfield(MatchSnapshot view, Vec2 point, int exclude)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int slot = 1; slot < Slots; slot++)
            {
                int id = MatchSimulation.PlayerId(Team, slot);
                if (id == exclude) continue;
                float distance = Vec2.Distance(view.Players[id].Position, point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = id;
                }
            }
            return best;
        }

        float NearestOpponentDistance(MatchSnapshot view, Vec2 point)
        {
            float best = float.MaxValue;
            foreach (var p in view.Players)
                if (p.Team != Team) best = Math.Min(best, Vec2.Distance(p.Position, point));
            return best;
        }
    }
}
