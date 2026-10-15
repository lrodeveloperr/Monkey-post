using System;
using System.Collections.Generic;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Authoritative Monkey Post rules. Only the host runs this; everyone else (bots, guest phones,
    /// the local UI) sees the match through <see cref="MatchSnapshot"/>, which hides the post.
    /// </summary>
    public sealed class MatchSimulation
    {
        readonly PlayerState[] _players;
        readonly int[] _seatOccupant;
        readonly List<MatchEvent> _events = new List<MatchEvent>();

        // Chase bookkeeping, measured along the direction of travel.
        float _chaseGap;
        float _runnerRemaining;
        int _chaseSeat = -1;

        public MatchConfig Config { get; }
        public Phase Phase { get; private set; }
        public float TimeRemaining { get; private set; }
        public float PhaseTime { get; private set; }
        public float TimeSinceRoundStart { get; private set; }
        public int RunnerId { get; private set; }
        public int ChaserId { get; private set; } = -1;

        /// <summary>Secret: whether the runner still holds the post.</summary>
        public bool PostInHand { get; private set; }

        /// <summary>Secret: the seat the post lies behind, or -1.</summary>
        public int PostSeat { get; private set; } = -1;

        /// <summary>How far the runner has travelled since dropping. A full lap means the target is Posted.</summary>
        public float RunnerTravelSinceDrop { get; private set; }

        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Events raised by the most recent <see cref="Step"/>.</summary>
        public IReadOnlyList<MatchEvent> Events => _events;

        public int SeatCount => Config.SeatCount;

        public MatchSimulation(MatchConfig config, int firstRunnerId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            if (firstRunnerId < 0 || firstRunnerId >= config.PlayerCount)
                throw new ArgumentOutOfRangeException(nameof(firstRunnerId));

            Config = config;
            _players = new PlayerState[config.PlayerCount];
            _seatOccupant = new int[config.SeatCount];

            int seat = 0;
            for (int id = 0; id < _players.Length; id++)
            {
                var player = new PlayerState { Id = id };
                if (id == firstRunnerId)
                {
                    player.Role = Role.Runner;
                    player.Angle = 0.5f / config.SeatCount;
                }
                else
                {
                    player.Role = Role.Sitter;
                    player.Seat = seat;
                    player.Angle = Ring.SeatAngle(seat, config.SeatCount);
                    _seatOccupant[seat] = id;
                    seat++;
                }
                _players[id] = player;
            }

            RunnerId = firstRunnerId;
            TimeRemaining = config.MatchDurationSeconds;
            StartRound();
            _events.Clear();
        }

        /// <summary>The player sitting in <paramref name="seat"/>, or -1 if it is empty (during a chase).</summary>
        public int SeatOccupant(int seat) => _seatOccupant[seat];

        public void Step(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Count != _players.Length)
                throw new ArgumentException($"Expected {_players.Length} inputs, got {inputs.Count}.", nameof(inputs));
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));

            _events.Clear();
            if (Phase == Phase.Ended) return;

            TimeRemaining -= dt;
            foreach (var player in _players)
                player.CheckCooldown = Math.Max(0f, player.CheckCooldown - dt);

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Phase = Phase.Ended;
                _events.Add(new MatchEvent(MatchEventType.MatchEnded));
                return;
            }

            PhaseTime += dt;
            TimeSinceRoundStart += dt;

            switch (Phase)
            {
                case Phase.Circling:
                    StepCircling(inputs, dt);
                    break;
                case Phase.Chase:
                    StepChase(inputs, dt);
                    break;
                case Phase.Interlude:
                    foreach (var player in _players) Regen(player, dt);
                    if (PhaseTime >= Config.InterludeSeconds) StartRound();
                    break;
            }
        }

        void StepCircling(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            var runner = _players[RunnerId];
            var runnerInput = inputs[RunnerId];
            float moved = Move(runner, runnerInput.Sprint, 1f, dt);

            if (PostInHand)
            {
                if (runnerInput.Drop || TimeSinceRoundStart >= Config.DropDeadlineSeconds)
                    DropPost(runner);
            }
            else
            {
                RunnerTravelSinceDrop += moved;
            }

            foreach (var player in _players)
            {
                if (player.Role != Role.Sitter) continue;
                Regen(player, dt);
                if (!inputs[player.Id].Check || player.CheckCooldown > 0f) continue;

                if (!PostInHand && player.Seat == PostSeat)
                {
                    StartChase(player);
                    return;
                }

                player.Score = Math.Max(0, player.Score + Config.PointsFalseCheck);
                player.CheckCooldown = Config.FalseCheckCooldownSeconds;
                _events.Add(new MatchEvent(MatchEventType.FalseCheck, player.Id, seat: player.Seat));
            }

            if (!PostInHand && RunnerTravelSinceDrop >= 1f)
                ResolvePosted();
        }

        void StepChase(IReadOnlyList<PlayerInput> inputs, float dt)
        {
            var runner = _players[RunnerId];
            var chaser = _players[ChaserId];
            foreach (var player in _players)
                if (player.Role == Role.Sitter) Regen(player, dt);

            float runnerMove = Move(runner, inputs[RunnerId].Sprint, 1f, dt);
            float chaserMove = Move(chaser, inputs[ChaserId].Sprint, Config.ChaserSpeedMultiplier, dt);

            float newGap = _chaseGap + runnerMove - chaserMove;
            bool tagged = newGap <= Config.TagDistance;
            bool reachedSeat = runnerMove >= _runnerRemaining;

            if (tagged && reachedSeat)
            {
                // Both happened within this tick: whichever happened first wins.
                float seatFraction = runnerMove > 0f ? _runnerRemaining / runnerMove : 0f;
                float closing = chaserMove - runnerMove;
                float tagFraction = closing > 0f ? (_chaseGap - Config.TagDistance) / closing : 0f;
                if (tagFraction < seatFraction) reachedSeat = false;
                else tagged = false;
            }

            if (tagged)
            {
                ResolveTagged(runner, chaser);
            }
            else if (reachedSeat)
            {
                ResolveSafe(runner, chaser);
            }
            else
            {
                _chaseGap = newGap;
                _runnerRemaining -= runnerMove;
            }
        }

        void DropPost(PlayerState runner)
        {
            int seat = Ring.SeatJustPassed(runner.Angle, SeatCount);
            PostInHand = false;
            PostSeat = seat;
            RunnerTravelSinceDrop = Ring.ForwardDistance(Ring.SeatAngle(seat, SeatCount), runner.Angle);
            _events.Add(new MatchEvent(MatchEventType.PostDropped, runner.Id, _seatOccupant[seat], seat));
        }

        void StartChase(PlayerState target)
        {
            var runner = _players[RunnerId];
            _chaseSeat = target.Seat;
            float seatAngle = Ring.SeatAngle(_chaseSeat, SeatCount);

            _seatOccupant[_chaseSeat] = -1;
            target.Seat = -1;
            target.Role = Role.Chaser;
            target.Angle = seatAngle;
            ChaserId = target.Id;

            float runnerAhead = Ring.ForwardDistance(seatAngle, runner.Angle);
            _chaseGap = runnerAhead;
            _runnerRemaining = 1f - runnerAhead;

            Phase = Phase.Chase;
            PhaseTime = 0f;
            _events.Add(new MatchEvent(MatchEventType.PostDiscovered, target.Id, runner.Id, _chaseSeat));
        }

        void ResolveSafe(PlayerState runner, PlayerState chaser)
        {
            int seat = _chaseSeat;
            runner.Score += Config.PointsRunnerSafe;
            Sit(runner, seat);

            chaser.Role = Role.Runner;
            RunnerId = chaser.Id;
            ChaserId = -1;

            _events.Add(new MatchEvent(MatchEventType.RunnerSafe, runner.Id, chaser.Id, seat));
            BeginInterlude();
        }

        void ResolveTagged(PlayerState runner, PlayerState chaser)
        {
            int seat = _chaseSeat;
            chaser.Score += Config.PointsTag;
            Sit(chaser, seat);
            ChaserId = -1;

            _events.Add(new MatchEvent(MatchEventType.RunnerTagged, chaser.Id, runner.Id, seat));
            BeginInterlude();
        }

        void ResolvePosted()
        {
            int seat = PostSeat;
            var runner = _players[RunnerId];
            var target = _players[_seatOccupant[seat]];

            target.Score = Math.Max(0, target.Score + Config.PointsPostedTarget);
            target.Role = Role.Runner;
            target.Seat = -1;
            RunnerId = target.Id;

            runner.Score += Config.PointsPostedRunner;
            Sit(runner, seat);

            _events.Add(new MatchEvent(MatchEventType.Posted, runner.Id, target.Id, seat));
            BeginInterlude();
        }

        void Sit(PlayerState player, int seat)
        {
            player.Role = Role.Sitter;
            player.Seat = seat;
            player.Angle = Ring.SeatAngle(seat, SeatCount);
            player.IsSprinting = false;
            _seatOccupant[seat] = player.Id;
        }

        void BeginInterlude()
        {
            Phase = Phase.Interlude;
            PhaseTime = 0f;
            PostInHand = false;
            PostSeat = -1;
            _chaseSeat = -1;
            foreach (var player in _players) player.IsSprinting = false;
        }

        void StartRound()
        {
            Phase = Phase.Circling;
            PhaseTime = 0f;
            TimeSinceRoundStart = 0f;
            PostInHand = true;
            PostSeat = -1;
            RunnerTravelSinceDrop = 0f;
            _events.Add(new MatchEvent(MatchEventType.RoundStarted, RunnerId));
        }

        float Move(PlayerState player, bool wantsSprint, float multiplier, float dt)
        {
            bool sprinting = wantsSprint && player.Stamina > 0f;
            float speed = (sprinting ? Config.SprintSpeed : Config.JogSpeed) * multiplier;
            if (sprinting) player.Stamina = Math.Max(0f, player.Stamina - Config.StaminaDrainPerSecond * dt);
            else Regen(player, dt);
            player.IsSprinting = sprinting;

            float distance = speed * dt;
            player.Angle = Ring.Wrap(player.Angle + distance);
            return distance;
        }

        void Regen(PlayerState player, float dt) =>
            player.Stamina = Math.Min(1f, player.Stamina + Config.StaminaRegenPerSecond * dt);
    }
}
