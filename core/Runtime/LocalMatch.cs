using System;
using MonkeyPost.Core.Bots;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Runs a match on this device. Each team is either a bot team or a person's team; a person
    /// controls whichever footballer is best placed (auto-switch, like most football games) and
    /// bots move their teammates. Used offline and by the host phone in local 1v1.
    /// </summary>
    public sealed class LocalMatch
    {
        /// <summary>A person's control only jumps to a teammate this much closer to the ball.</summary>
        const float SwitchHysteresis = 2f;

        /// <summary>How well a person's AI teammates play.</summary>
        public const BotDifficulty TeammateDifficulty = BotDifficulty.Medium;

        readonly TeamBrain[] _brains = new TeamBrain[2];
        readonly bool[] _human = new bool[2];
        readonly int[] _controlled = { -1, -1 };
        readonly PlayerInput[] _humanInputs = new PlayerInput[2];
        readonly PlayerInput[] _inputs = new PlayerInput[MatchSimulation.PlayerCount];
        readonly MatchSnapshot _view = new MatchSnapshot();

        public MatchSimulation Simulation { get; }

        /// <param name="home">Bot difficulty for the home team, or null if a person plays it.</param>
        /// <param name="away">Bot difficulty for the away team, or null if a person plays it.</param>
        /// <param name="cupRound">Street-cup round (0 = quarter-final): computer teams get slightly stronger each round.</param>
        public LocalMatch(MatchConfig config, BotDifficulty? home, BotDifficulty? away, ulong seed, int cupRound = 0)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            // Computer teams get their physical edges; a person's team never has any.
            config = config.Clone();
            config.HomeTraits = home.HasValue ? BotProfile.TraitsFor(home.Value, cupRound) : new TeamTraits();
            config.AwayTraits = away.HasValue ? BotProfile.TraitsFor(away.Value, cupRound) : new TeamTraits();
            var rng = new DeterministicRandom(seed);
            Simulation = new MatchSimulation(config, rng.NextULong(), rng.Next(2));

            var difficulties = new[] { home, away };
            for (int team = 0; team < 2; team++)
            {
                _human[team] = !difficulties[team].HasValue;
                var profile = BotProfile.For(difficulties[team] ?? TeammateDifficulty);
                _brains[team] = new TeamBrain(team, config, profile, rng.NextULong());
                if (_human[team]) _controlled[team] = MatchSimulation.PlayerId(team, MatchSimulation.KickoffSlot);
            }
        }

        public bool IsHuman(int team) => _human[team];

        /// <summary>The footballer a person is controlling on <paramref name="team"/>, or -1 for a bot team.</summary>
        public int ControlledPlayer(int team) => _controlled[team];

        /// <summary>
        /// Sets a person's controls. Movement and sprint persist until changed; pass, shoot,
        /// tackle and switch are single presses consumed by the next tick.
        /// </summary>
        public void SetInput(int team, PlayerInput input)
        {
            if (!_human[team]) throw new InvalidOperationException($"Team {team} is played by bots.");
            _humanInputs[team] = input;
        }

        public void Tick(float dt)
        {
            _view.CopyFrom(Simulation);
            for (int team = 0; team < 2; team++)
            {
                if (_human[team]) UpdateControl(team, _humanInputs[team].Switch);
                _brains[team].Think(_view, dt, _controlled[team], _inputs);
                if (_human[team])
                {
                    _inputs[_controlled[team]] = _humanInputs[team];
                    ref var held = ref _humanInputs[team];
                    held.Pass = held.Shoot = held.Tackle = held.Switch = false;
                }
            }
            Simulation.Step(_inputs, dt);
        }

        void UpdateControl(int team, bool switchPressed)
        {
            var ball = _view.Ball;
            if (ball.Owner >= 0 && MatchSimulation.TeamOf(ball.Owner) == team)
            {
                // Always control our ball carrier, except the keeper, who clears it automatically.
                if (!_view.Players[ball.Owner].IsKeeper) _controlled[team] = ball.Owner;
                return;
            }

            int nearest = -1, second = -1;
            float nearestDistance = float.MaxValue, secondDistance = float.MaxValue;
            for (int slot = 1; slot < MatchSimulation.PlayersPerTeam; slot++)
            {
                int id = MatchSimulation.PlayerId(team, slot);
                float distance = Vec2.Distance(_view.Players[id].Position, ball.Position);
                if (distance < nearestDistance)
                {
                    second = nearest;
                    secondDistance = nearestDistance;
                    nearest = id;
                    nearestDistance = distance;
                }
                else if (distance < secondDistance)
                {
                    second = id;
                    secondDistance = distance;
                }
            }

            int current = _controlled[team];
            if (switchPressed)
            {
                _controlled[team] = nearest != current ? nearest : second;
                return;
            }

            float currentDistance = Vec2.Distance(_view.Players[current].Position, ball.Position);
            if (nearest != current && currentDistance - nearestDistance > SwitchHysteresis)
                _controlled[team] = nearest;
        }
    }
}
