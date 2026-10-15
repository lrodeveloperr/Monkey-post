using Xunit;

namespace MonkeyPost.Core.Tests
{
    public class SnapshotAndRingTests
    {
        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(1f, 0f)]
        [InlineData(1.25f, 0.25f)]
        [InlineData(-0.25f, 0.75f)]
        public void Wrap_KeepsAnglesInOneLap(float angle, float expected)
        {
            Assert.Equal(expected, Ring.Wrap(angle), 5);
        }

        [Fact]
        public void ForwardDistance_GoesAnticlockwiseAcrossZero()
        {
            Assert.Equal(0.2f, Ring.ForwardDistance(0.9f, 0.1f), 5);
            Assert.Equal(0.8f, Ring.ForwardDistance(0.1f, 0.9f), 5);
        }

        [Theory]
        [InlineData(0.00f, 0)]
        [InlineData(0.19f, 0)]
        [InlineData(0.21f, 1)]
        [InlineData(0.99f, 4)]
        public void SeatJustPassed_IsTheSeatAtOrBehind(float angle, int expected)
        {
            Assert.Equal(expected, Ring.SeatJustPassed(angle, 5));
        }

        [Fact]
        public void Snapshot_HidesThePostFromEveryoneButTheRunner()
        {
            var sim = new MatchSimulation(new MatchConfig(), firstRunnerId: 0);

            Assert.True(MatchSnapshot.Capture(sim, 0).ViewerHoldsPost);
            Assert.False(MatchSnapshot.Capture(sim, 3).ViewerHoldsPost);

            var drop = new PlayerInput[sim.Players.Count];
            drop[0].Drop = true;
            sim.Step(drop, 1f / 30f);
            Assert.False(sim.PostInHand);

            var runnerView = MatchSnapshot.Capture(sim, 0);
            Assert.False(runnerView.ViewerHoldsPost);
            Assert.Equal(sim.PostSeat, runnerView.ViewerKnownPostSeat);

            for (int viewer = 1; viewer < sim.Players.Count; viewer++)
            {
                var view = MatchSnapshot.Capture(sim, viewer);
                Assert.False(view.ViewerHoldsPost);
                Assert.Equal(-1, view.ViewerKnownPostSeat);
            }
            Assert.Equal(-1, MatchSnapshot.Capture(sim, -1).ViewerKnownPostSeat);
        }

        [Fact]
        public void DeterministicRandom_IsReproducible()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextULong(), b.NextULong());

            var r = new DeterministicRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.NextFloat();
                Assert.InRange(f, 0f, 0.99999994f);
                Assert.InRange(r.Next(5), 0, 4);
            }
        }
    }
}
