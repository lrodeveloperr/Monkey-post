using System.Collections.Generic;
using System.Linq;
using MonkeyPost.Core.Bots;
using Xunit;
using Xunit.Abstractions;

namespace MonkeyPost.Core.Tests
{
    public class BotMatchTests
    {
        const float Dt = 1f / 30f;
        const int MatchesPerDifficulty = 40;

        readonly ITestOutputHelper _output;

        public BotMatchTests(ITestOutputHelper output) => _output = output;

        static LocalMatch AllBots(BotDifficulty difficulty, ulong seed) =>
            new LocalMatch(new MatchConfig(), Enumerable.Repeat((BotDifficulty?)difficulty, 6).ToArray(), seed);

        static Dictionary<MatchEventType, int> Play(LocalMatch match)
        {
            var counts = new Dictionary<MatchEventType, int>();
            var sim = match.Simulation;
            for (int tick = 0; tick < 100_000 && sim.Phase != Phase.Ended; tick++)
            {
                match.Tick(Dt);
                AssertInvariants(sim);
                foreach (var e in sim.Events)
                    counts[e.Type] = counts.TryGetValue(e.Type, out int n) ? n + 1 : 1;
            }
            Assert.Equal(Phase.Ended, sim.Phase);
            return counts;
        }

        static void AssertInvariants(MatchSimulation sim)
        {
            Assert.Equal(1, sim.Players.Count(p => p.Role == Role.Runner));
            Assert.Equal(Role.Runner, sim.Players[sim.RunnerId].Role);
            int chasers = sim.Players.Count(p => p.Role == Role.Chaser);
            // The clock can run out mid-chase, which freezes the chaser where they stand.
            bool chaseUnderway = sim.Phase == Phase.Chase || (sim.Phase == Phase.Ended && sim.ChaserId >= 0);
            Assert.Equal(chaseUnderway ? 1 : 0, chasers);

            foreach (var p in sim.Players)
            {
                Assert.True(p.Score >= 0);
                Assert.InRange(p.Stamina, 0f, 1f);
                if (p.Role == Role.Sitter) Assert.Equal(p.Id, sim.SeatOccupant(p.Seat));
                else Assert.Equal(-1, p.Seat);
            }

            int emptySeats = Enumerable.Range(0, sim.SeatCount).Count(s => sim.SeatOccupant(s) < 0);
            Assert.Equal(chasers, emptySeats);
        }

        [Theory]
        [InlineData(BotDifficulty.Easy)]
        [InlineData(BotDifficulty.Medium)]
        [InlineData(BotDifficulty.Hard)]
        public void BotMatches_FinishWithEveryOutcomeHappening(BotDifficulty difficulty)
        {
            var totals = new Dictionary<MatchEventType, int>();
            for (ulong seed = 1; seed <= MatchesPerDifficulty; seed++)
            {
                foreach (var kv in Play(AllBots(difficulty, seed)))
                    totals[kv.Key] = totals.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;
            }

            _output.WriteLine($"{difficulty}, {MatchesPerDifficulty} matches:");
            foreach (var kv in totals.OrderBy(kv => kv.Key))
                _output.WriteLine($"  {kv.Key}: {kv.Value} ({kv.Value / (float)MatchesPerDifficulty:0.0} per match)");

            // Every way a round can end should actually happen with bots playing.
            Assert.True(totals.GetValueOrDefault(MatchEventType.RunnerSafe) > 0, "no Safe outcomes");
            Assert.True(totals.GetValueOrDefault(MatchEventType.RunnerTagged) > 0, "no Tagged outcomes");
            Assert.True(totals.GetValueOrDefault(MatchEventType.Posted) > 0, "no Posted outcomes");
        }

        [Fact]
        public void SameSeed_PlaysTheSameMatch()
        {
            var a = AllBots(BotDifficulty.Hard, 99);
            var b = AllBots(BotDifficulty.Hard, 99);
            Play(a);
            Play(b);
            Assert.Equal(a.Simulation.Players.Select(p => p.Score), b.Simulation.Players.Select(p => p.Score));
        }

        [Fact]
        public void HumanSeat_IsNotDrivenByABot()
        {
            var seats = new BotDifficulty?[] { null, BotDifficulty.Easy, BotDifficulty.Easy, BotDifficulty.Easy, BotDifficulty.Easy, BotDifficulty.Easy };
            var match = new LocalMatch(new MatchConfig(), seats, 5);

            Assert.False(match.IsBot(0));
            Assert.True(match.IsBot(1));
            Assert.Throws<System.InvalidOperationException>(() => match.SetInput(1, new PlayerInput()));

            match.SetInput(0, new PlayerInput { Sprint = true });
            match.Tick(Dt);
        }
    }
}
