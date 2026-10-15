using System;
using MonkeyPost.Core.Bots;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Runs a match on this device: the simulation plus a bot for every seat not controlled by a
    /// person. Used for offline play and by the host in local multiplayer.
    /// </summary>
    public sealed class LocalMatch
    {
        readonly BotBrain[] _bots;
        readonly PlayerInput[] _inputs;
        readonly MatchSnapshot _botView = new MatchSnapshot();

        public MatchSimulation Simulation { get; }

        /// <param name="bots">One entry per player: a difficulty for bot seats, null for human seats.</param>
        public LocalMatch(MatchConfig config, BotDifficulty?[] bots, ulong seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (bots == null) throw new ArgumentNullException(nameof(bots));
            if (bots.Length != config.PlayerCount)
                throw new ArgumentException($"Expected {config.PlayerCount} seats, got {bots.Length}.", nameof(bots));

            var rng = new DeterministicRandom(seed);
            Simulation = new MatchSimulation(config, rng.Next(config.PlayerCount));

            _inputs = new PlayerInput[config.PlayerCount];
            _bots = new BotBrain[config.PlayerCount];
            for (int id = 0; id < bots.Length; id++)
            {
                if (bots[id] is BotDifficulty difficulty)
                    _bots[id] = new BotBrain(id, BotProfile.For(difficulty), rng.NextULong());
            }
        }

        public bool IsBot(int playerId) => _bots[playerId] != null;

        /// <summary>Sets a human player's controls for the next tick.</summary>
        public void SetInput(int playerId, PlayerInput input)
        {
            if (_bots[playerId] != null) throw new InvalidOperationException($"Player {playerId} is a bot.");
            _inputs[playerId] = input;
        }

        public void Tick(float dt)
        {
            for (int id = 0; id < _bots.Length; id++)
            {
                if (_bots[id] == null) continue;
                _botView.CopyFrom(Simulation, id);
                _inputs[id] = _bots[id].Think(_botView, dt);
            }
            Simulation.Step(_inputs, dt);
        }
    }
}
