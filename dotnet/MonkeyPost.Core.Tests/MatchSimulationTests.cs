using System;
using System.Linq;
using Xunit;

namespace MonkeyPost.Core.Tests
{
    public class MatchSimulationTests
    {
        const float Dt = 1f / 30f;
        const int Runner = 0;
        // With player 0 as first runner, players 1..5 sit on seats 0..4 (angles 0, .2, .4, .6, .8).
        const int TargetSeat = 1;
        const int Target = 2;

        static MatchSimulation NewMatch(MatchConfig config = null) => new MatchSimulation(config ?? new MatchConfig(), Runner);

        static PlayerInput[] NoInput(MatchSimulation sim) => new PlayerInput[sim.Players.Count];

        static bool Has(MatchSimulation sim, MatchEventType type) => sim.Events.Any(e => e.Type == type);

        /// <summary>Jogs the runner until just past <see cref="TargetSeat"/>, then drops the post there.</summary>
        static void DropBehindTarget(MatchSimulation sim)
        {
            float seatAngle = Ring.SeatAngle(TargetSeat, sim.SeatCount);
            for (int i = 0; i < 1000 && Ring.ForwardDistance(seatAngle, sim.Players[Runner].Angle) > 0.5f; i++)
                sim.Step(NoInput(sim), Dt);

            var inputs = NoInput(sim);
            inputs[Runner].Drop = true;
            sim.Step(inputs, Dt);
            Assert.True(Has(sim, MatchEventType.PostDropped));
        }

        static void Check(MatchSimulation sim, int player)
        {
            var inputs = NoInput(sim);
            inputs[player].Check = true;
            sim.Step(inputs, Dt);
        }

        /// <summary>Steps with the given sprint settings until the phase changes.</summary>
        static void RunUntilPhaseChanges(MatchSimulation sim, bool runnerSprints, bool chaserSprints)
        {
            var phase = sim.Phase;
            for (int i = 0; i < 10_000 && sim.Phase == phase; i++)
            {
                var inputs = NoInput(sim);
                inputs[sim.RunnerId].Sprint = runnerSprints;
                if (sim.ChaserId >= 0) inputs[sim.ChaserId].Sprint = chaserSprints;
                sim.Step(inputs, Dt);
            }
            Assert.NotEqual(phase, sim.Phase);
        }

        [Fact]
        public void NewMatch_SeatsEveryoneButTheRunner()
        {
            var sim = NewMatch();

            Assert.Equal(Phase.Circling, sim.Phase);
            Assert.Equal(5, sim.SeatCount);
            Assert.Equal(Role.Runner, sim.Players[Runner].Role);
            Assert.Equal(-1, sim.Players[Runner].Seat);
            Assert.True(sim.PostInHand);
            for (int seat = 0; seat < sim.SeatCount; seat++)
            {
                var occupant = sim.Players[sim.SeatOccupant(seat)];
                Assert.Equal(Role.Sitter, occupant.Role);
                Assert.Equal(seat, occupant.Seat);
            }
        }

        [Fact]
        public void Drop_LandsBehindTheSeatJustPassed()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);

            Assert.False(sim.PostInHand);
            Assert.Equal(TargetSeat, sim.PostSeat);
            var drop = sim.Events.Single(e => e.Type == MatchEventType.PostDropped);
            Assert.Equal(Target, drop.PlayerB);
            Assert.False(drop.IsPublic);
        }

        [Fact]
        public void CheckingWithPostBehind_StartsChaseAndVacatesSeat()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);
            Check(sim, Target);

            Assert.Equal(Phase.Chase, sim.Phase);
            Assert.Equal(Target, sim.ChaserId);
            Assert.Equal(Role.Chaser, sim.Players[Target].Role);
            Assert.Equal(-1, sim.SeatOccupant(TargetSeat));
            Assert.True(Has(sim, MatchEventType.PostDiscovered));
        }

        [Fact]
        public void QuickCheck_ChaserTagsRunner()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);
            Check(sim, Target);
            RunUntilPhaseChanges(sim, runnerSprints: true, chaserSprints: true);

            Assert.Equal(Phase.Interlude, sim.Phase);
            var tag = sim.Events.Single(e => e.Type == MatchEventType.RunnerTagged);
            Assert.Equal(Target, tag.PlayerA);
            Assert.Equal(Runner, tag.PlayerB);
            Assert.Equal(sim.Config.PointsTag, sim.Players[Target].Score);
            Assert.Equal(TargetSeat, sim.Players[Target].Seat);
            Assert.Equal(Target, sim.SeatOccupant(TargetSeat));
            Assert.Equal(Runner, sim.RunnerId);
        }

        [Fact]
        public void SlowCheck_RunnerReachesSeatSafely()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);

            // Runner sprints well clear before the target notices.
            for (int i = 0; i < 45; i++)
            {
                var inputs = NoInput(sim);
                inputs[Runner].Sprint = true;
                sim.Step(inputs, Dt);
            }
            Check(sim, Target);
            Assert.Equal(Phase.Chase, sim.Phase);
            RunUntilPhaseChanges(sim, runnerSprints: false, chaserSprints: false);

            Assert.True(Has(sim, MatchEventType.RunnerSafe));
            Assert.Equal(sim.Config.PointsRunnerSafe, sim.Players[Runner].Score);
            Assert.Equal(Role.Sitter, sim.Players[Runner].Role);
            Assert.Equal(TargetSeat, sim.Players[Runner].Seat);
            Assert.Equal(Target, sim.RunnerId);
            Assert.Equal(Role.Runner, sim.Players[Target].Role);
            Assert.Equal(-1, sim.ChaserId);
        }

        [Fact]
        public void UnnoticedPost_IsPostedAfterAFullLap()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);
            RunUntilPhaseChanges(sim, runnerSprints: false, chaserSprints: false);

            var posted = sim.Events.Single(e => e.Type == MatchEventType.Posted);
            Assert.Equal(Runner, posted.PlayerA);
            Assert.Equal(Target, posted.PlayerB);
            Assert.Equal(sim.Config.PointsPostedRunner, sim.Players[Runner].Score);
            Assert.Equal(0, sim.Players[Target].Score); // -2, clamped at zero
            Assert.Equal(Target, sim.RunnerId);
            Assert.Equal(TargetSeat, sim.Players[Runner].Seat);
        }

        [Fact]
        public void FalseCheck_CostsAPointAndStartsCooldown()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);
            RunUntilPhaseChanges(sim, false, false); // Posted: player 0 now sits on seat 1 with 5 points.
            RunUntilPhaseChanges(sim, false, false); // Interlude -> next round.
            Assert.Equal(Phase.Circling, sim.Phase);

            Check(sim, Runner);
            Assert.Equal(sim.Config.PointsPostedRunner - 1, sim.Players[Runner].Score);
            Assert.True(sim.Players[Runner].CheckCooldown > 0f);
            Assert.True(Has(sim, MatchEventType.FalseCheck));

            // Checking again during the cooldown does nothing.
            Check(sim, Runner);
            Assert.False(Has(sim, MatchEventType.FalseCheck));
            Assert.Equal(sim.Config.PointsPostedRunner - 1, sim.Players[Runner].Score);
        }

        [Fact]
        public void FalseCheck_NeverTakesScoreBelowZero()
        {
            var sim = NewMatch();
            Check(sim, Target);
            Assert.True(Has(sim, MatchEventType.FalseCheck));
            Assert.Equal(0, sim.Players[Target].Score);
        }

        [Fact]
        public void HoldingThePostTooLong_DropsItAutomatically()
        {
            var sim = NewMatch();
            for (int i = 0; i < 10_000 && sim.PostInHand; i++) sim.Step(NoInput(sim), Dt);

            Assert.False(sim.PostInHand);
            Assert.True(sim.TimeSinceRoundStart >= sim.Config.DropDeadlineSeconds);
            Assert.Equal(Ring.SeatJustPassed(sim.Players[Runner].Angle, sim.SeatCount), sim.PostSeat);
        }

        [Fact]
        public void Interlude_StartsNextRoundWithPostInHand()
        {
            var sim = NewMatch();
            DropBehindTarget(sim);
            RunUntilPhaseChanges(sim, false, false);
            Assert.Equal(Phase.Interlude, sim.Phase);

            RunUntilPhaseChanges(sim, false, false);
            Assert.Equal(Phase.Circling, sim.Phase);
            Assert.True(sim.PostInHand);
            Assert.True(Has(sim, MatchEventType.RoundStarted));
        }

        [Fact]
        public void Clock_EndsMatchOnceAndThenFreezes()
        {
            var sim = NewMatch(new MatchConfig { MatchDurationSeconds = 2f });
            int ends = 0;
            for (int i = 0; i < 120; i++)
            {
                sim.Step(NoInput(sim), Dt);
                ends += sim.Events.Count(e => e.Type == MatchEventType.MatchEnded);
            }

            Assert.Equal(1, ends);
            Assert.Equal(Phase.Ended, sim.Phase);
            Assert.Equal(0f, sim.TimeRemaining);
        }

        [Fact]
        public void Step_RejectsWrongInputCount()
        {
            var sim = NewMatch();
            Assert.Throws<ArgumentException>(() => sim.Step(new PlayerInput[2], Dt));
        }

        [Fact]
        public void Config_RejectsTooFewPlayers()
        {
            Assert.Throws<ArgumentException>(() => new MatchSimulation(new MatchConfig { PlayerCount = 2 }, 0));
        }
    }
}
