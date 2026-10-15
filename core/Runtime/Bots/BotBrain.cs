namespace MonkeyPost.Core.Bots
{
    /// <summary>
    /// Plays one seat. Sees only its own <see cref="MatchSnapshot"/>, exactly what a human in that
    /// seat would see, so it never knows where the post is unless it is the runner.
    /// </summary>
    public sealed class BotBrain
    {
        /// <summary>Late checks only start once the runner has had time to lap the ring.</summary>
        const float LateCheckMinRoundSeconds = 5f;
        /// <summary>How close (in laps) an approaching runner must be to trigger a late check.</summary>
        const float LateCheckApproachDistance = 0.12f;
        const float BluffSeconds = 1f;
        const float BluffMinStamina = 0.3f;

        readonly BotProfile _profile;
        readonly DeterministicRandom _rng;

        float _time;
        bool _hasPrevious;
        float _prevRunnerAngle;
        int _prevRunnerId = -1;
        Phase _prevPhase;

        // Sitter memory.
        float _evaluateAt = -1f;
        float _checkAt = -1f;
        bool _lateCheckRolled;

        // Runner memory.
        int _targetSeat = -1;
        int _lastPassedSeat = -1;
        float _bluffUntil = -1f;
        float _sprintAt = -1f;

        public int PlayerId { get; }
        public BotProfile Profile => _profile;

        public BotBrain(int playerId, BotProfile profile, ulong seed)
        {
            PlayerId = playerId;
            _profile = profile;
            _rng = new DeterministicRandom(seed);
        }

        public PlayerInput Think(MatchSnapshot view, float dt)
        {
            _time += dt;
            var me = view.Players[PlayerId];
            var runner = view.Players[view.RunnerId];
            bool continuing = _hasPrevious && _prevPhase == view.Phase && _prevRunnerId == view.RunnerId;

            var input = new PlayerInput();
            switch (view.Phase)
            {
                case Phase.Circling when me.Role == Role.Sitter:
                    input.Check = ThinkSitter(view, me, runner, continuing);
                    break;
                case Phase.Circling when me.Role == Role.Runner:
                    ThinkRunner(view, me, continuing, ref input);
                    break;
                case Phase.Chase when me.Role != Role.Sitter:
                    // Runner and chaser both go flat out; stamina runs out on its own.
                    input.Sprint = true;
                    break;
            }

            _hasPrevious = true;
            _prevRunnerAngle = runner.Angle;
            _prevRunnerId = view.RunnerId;
            _prevPhase = view.Phase;
            return input;
        }

        bool ThinkSitter(MatchSnapshot view, PlayerSnapshot me, PlayerSnapshot runner, bool continuing)
        {
            if (!continuing)
            {
                _evaluateAt = -1f;
                _checkAt = -1f;
                _lateCheckRolled = false;
                return false;
            }

            float seatAngle = Ring.SeatAngle(me.Seat, view.SeatCount);
            float toSeatBefore = Ring.ForwardDistance(_prevRunnerAngle, seatAngle);
            float moved = Ring.ForwardDistance(_prevRunnerAngle, runner.Angle);

            if (moved < 0.5f && moved >= toSeatBefore)
            {
                // The runner just went behind me: watch them for a moment before deciding.
                _evaluateAt = _time + _profile.TellWindowSeconds;
                _lateCheckRolled = false;
            }

            if (_evaluateAt >= 0f && _time >= _evaluateAt)
            {
                _evaluateAt = -1f;
                float chance = _profile.CheckOnPassChance + (runner.IsSprinting ? _profile.SprintTellBonus : 0f);
                if (_rng.Chance(chance)) ScheduleCheck(1f);
            }

            if (!_lateCheckRolled
                && view.TimeSinceRoundStart >= LateCheckMinRoundSeconds
                && Ring.ForwardDistance(runner.Angle, seatAngle) < LateCheckApproachDistance)
            {
                // The runner is about to come round again; if the post is behind me I'm about to be Posted.
                _lateCheckRolled = true;
                if (_rng.Chance(_profile.LateCheckChance)) ScheduleCheck(0.5f);
            }

            if (_checkAt >= 0f && _time >= _checkAt)
            {
                _checkAt = -1f;
                return me.CheckCooldown <= 0f;
            }
            return false;
        }

        void ScheduleCheck(float reactionScale)
        {
            float delay = _profile.ReactionSeconds * reactionScale * _rng.Range(0.75f, 1.25f);
            float at = _time + delay;
            if (_checkAt < 0f || at < _checkAt) _checkAt = at;
        }

        void ThinkRunner(MatchSnapshot view, PlayerSnapshot me, bool continuing, ref PlayerInput input)
        {
            int seatCount = view.SeatCount;
            int passed = Ring.SeatJustPassed(me.Angle, seatCount);

            if (!continuing)
            {
                _targetSeat = -1;
                _bluffUntil = -1f;
                _sprintAt = -1f;
                _lastPassedSeat = passed;
            }

            if (view.ViewerHoldsPost)
            {
                if (_targetSeat < 0) _targetSeat = PickTarget(view);

                bool newSeat = passed != _lastPassedSeat;
                if (passed == _targetSeat)
                {
                    float behindTarget = Ring.ForwardDistance(Ring.SeatAngle(_targetSeat, seatCount), me.Angle);
                    if (behindTarget < 0.4f / seatCount)
                    {
                        input.Drop = true;
                        _sprintAt = _time + _profile.SprintDelayAfterDrop * _rng.Range(0.75f, 1.25f);
                    }
                }
                else if (newSeat && _rng.Chance(_profile.BluffChance))
                {
                    _bluffUntil = _time + BluffSeconds;
                }

                input.Sprint = _time < _bluffUntil && me.Stamina > BluffMinStamina;
            }
            else
            {
                // Post is down (possibly dropped automatically): get clear once the delay has passed.
                if (_sprintAt < 0f) _sprintAt = _time + _profile.SprintDelayAfterDrop;
                input.Sprint = _time >= _sprintAt;
            }

            _lastPassedSeat = passed;
        }

        int PickTarget(MatchSnapshot view)
        {
            if (_rng.Chance(_profile.TargetLeaderChance))
            {
                int best = -1, bestScore = int.MinValue;
                foreach (var p in view.Players)
                {
                    if (p.Role != Role.Sitter || p.Score <= bestScore) continue;
                    best = p.Seat;
                    bestScore = p.Score;
                }
                if (best >= 0) return best;
            }
            return _rng.Next(view.SeatCount);
        }
    }
}
