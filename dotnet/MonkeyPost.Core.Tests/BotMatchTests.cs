using System;
using System.Collections.Generic;
using System.Linq;
using MonkeyPost.Core.Bots;
using Xunit;
using Xunit.Abstractions;

namespace MonkeyPost.Core.Tests
{
    public class BotMatchTests
    {
        const float Dt = 1f / 60f;
        const int Matches = 30;

        readonly ITestOutputHelper _output;

        public BotMatchTests(ITestOutputHelper output) => _output = output;

        sealed class Tally
        {
            public int HomeGoals, AwayGoals, HomeWins, AwayWins, Draws;
            public readonly Dictionary<MatchEventType, int> Events = new Dictionary<MatchEventType, int>();
        }

        static Tally PlayMany(BotDifficulty home, BotDifficulty away)
        {
            var tally = new Tally();
            for (ulong seed = 1; seed <= Matches; seed++)
            {
                var match = new LocalMatch(new MatchConfig(), home, away, seed);
                Play(match, tally.Events);
                int h = match.Simulation.Score(0), a = match.Simulation.Score(1);
                tally.HomeGoals += h;
                tally.AwayGoals += a;
                if (h > a) tally.HomeWins++;
                else if (a > h) tally.AwayWins++;
                else tally.Draws++;
            }
            return tally;
        }

        static void Play(LocalMatch match, Dictionary<MatchEventType, int> counts)
        {
            var sim = match.Simulation;
            for (int tick = 0; tick < 200_000 && sim.Phase != Phase.Ended; tick++)
            {
                match.Tick(Dt);
                AssertInvariants(sim);
                foreach (var e in sim.Events) counts[e.Type] = counts.GetValueOrDefault(e.Type) + 1;
            }
            Assert.Equal(Phase.Ended, sim.Phase);
        }

        static void AssertInvariants(MatchSimulation sim)
        {
            var c = sim.Config;
            foreach (var p in sim.Players)
            {
                Assert.InRange(p.Position.X, 0f, c.PitchLength);
                Assert.InRange(p.Position.Y, 0f, c.PitchWidth);
                Assert.InRange(p.Stamina, 0f, 1f);
                if (p.IsKeeper) Assert.True(MatchSimulation.InKeeperBox(c, p.Team, p.Position), $"keeper {p.Id} left box at {p.Position}");
            }
            // After a goal the ball sits just over the line, in the goal.
            float slack = sim.Phase == Phase.GoalScored || sim.Phase == Phase.Ended ? 1f : 0f;
            Assert.InRange(sim.Ball.Position.X, -slack, c.PitchLength + slack);
            Assert.InRange(sim.Ball.Position.Y, 0f, c.PitchWidth);
            Assert.True(sim.Ball.Height >= 0f);
            Assert.InRange(sim.Ball.Owner, -1, MatchSimulation.PlayerCount - 1);
        }

        void Report(string label, Tally t)
        {
            _output.WriteLine($"{label}: {Matches} matches, goals {t.HomeGoals}-{t.AwayGoals} " +
                              $"({(t.HomeGoals + t.AwayGoals) / (float)Matches:0.0}/match), W-D-L {t.HomeWins}-{t.Draws}-{t.AwayWins}");
            foreach (var kv in t.Events.OrderBy(kv => kv.Key))
                _output.WriteLine($"  {kv.Key}: {kv.Value / (float)Matches:0.0}/match");
        }

        [Theory]
        [InlineData(BotDifficulty.Easy)]
        [InlineData(BotDifficulty.Medium)]
        [InlineData(BotDifficulty.Hard)]
        public void EvenMatches_ProduceRealFootball(BotDifficulty difficulty)
        {
            var t = PlayMany(difficulty, difficulty);
            Report($"{difficulty} v {difficulty}", t);

            float goalsPerMatch = (t.HomeGoals + t.AwayGoals) / (float)Matches;
            Assert.InRange(goalsPerMatch, 1.5f, 15f);
            float perMatch(MatchEventType type) => t.Events.GetValueOrDefault(type) / (float)Matches;
            Assert.True(perMatch(MatchEventType.Pass) >= 10f, "too few passes");
            Assert.True(perMatch(MatchEventType.Shot) >= 4f, "too few shots");
            Assert.True(perMatch(MatchEventType.Tackle) >= 2f, "too few tackles");
            Assert.True(perMatch(MatchEventType.Save) >= 0.5f, "keepers never save");
        }

        [Fact]
        public void HardBots_BeatEasyBots()
        {
            var t = PlayMany(BotDifficulty.Hard, BotDifficulty.Easy);
            Report("Hard v Easy", t);
            Assert.True(t.HomeWins > t.AwayWins * 3, $"Hard won {t.HomeWins}, Easy won {t.AwayWins}");
            Assert.True(t.HomeGoals > t.AwayGoals * 2);
        }

        [Fact]
        public void SameSeed_PlaysTheSameMatch()
        {
            var a = new LocalMatch(new MatchConfig(), BotDifficulty.Hard, BotDifficulty.Medium, 99);
            var b = new LocalMatch(new MatchConfig(), BotDifficulty.Hard, BotDifficulty.Medium, 99);
            Play(a, new Dictionary<MatchEventType, int>());
            Play(b, new Dictionary<MatchEventType, int>());
            Assert.Equal(a.Simulation.Score(0), b.Simulation.Score(0));
            Assert.Equal(a.Simulation.Score(1), b.Simulation.Score(1));
            Assert.Equal(a.Simulation.Ball.Position, b.Simulation.Ball.Position);
        }

        [Fact]
        public void HumanTeam_ControlsTheBallCarrierAndBotsMoveTheRest()
        {
            var match = new LocalMatch(new MatchConfig(), home: null, away: BotDifficulty.Easy, seed: 3);
            var sim = match.Simulation;
            Assert.True(match.IsHuman(0));
            Assert.False(match.IsHuman(1));
            Assert.Equal(-1, match.ControlledPlayer(1));
            Assert.Throws<InvalidOperationException>(() => match.SetInput(1, new PlayerInput()));

            for (int i = 0; i < 300 && sim.Phase != Phase.Ended; i++)
            {
                // A person who just holds "run right". Control is decided from the state at the
                // start of each tick, so compare against the carrier before the tick.
                int carrier = sim.Ball.Owner;
                match.SetInput(0, new PlayerInput { Move = new Vec2(1f, 0f) });
                match.Tick(Dt);

                int controlled = match.ControlledPlayer(0);
                Assert.Equal(0, MatchSimulation.TeamOf(controlled));
                Assert.NotEqual(0, MatchSimulation.SlotOf(controlled)); // never the keeper
                if (carrier >= 0 && MatchSimulation.TeamOf(carrier) == 0 && !sim.Players[carrier].IsKeeper)
                    Assert.Equal(carrier, controlled);
            }
        }

        [Fact]
        public void HumanSwitch_MovesControlToAnotherTeammate()
        {
            var match = new LocalMatch(new MatchConfig(), home: null, away: BotDifficulty.Easy, seed: 4);
            var sim = match.Simulation;
            // Wait until the away team has the ball, so control isn't pinned to our carrier.
            for (int i = 0; i < 3000 && !(sim.Ball.Owner >= 5); i++) match.Tick(Dt);
            Assert.True(sim.Ball.Owner >= 5);

            int before = match.ControlledPlayer(0);
            match.SetInput(0, new PlayerInput { Switch = true });
            match.Tick(Dt);
            Assert.NotEqual(before, match.ControlledPlayer(0));
        }
    }
}
