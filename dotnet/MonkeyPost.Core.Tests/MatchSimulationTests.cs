using System;
using System.Linq;
using Xunit;

namespace MonkeyPost.Core.Tests
{
    public class MatchSimulationTests
    {
        const float Dt = 1f / 60f;
        const int HomeStriker = 4;
        const int HomeDefender = 1;
        const int AwayKeeper = 5;
        const int AwayStriker = 9;

        /// <summary>No random shot spread, so shots go exactly where aimed.</summary>
        static MatchConfig Precise() => new MatchConfig { ShotSpreadBase = 0f, ShotSpreadPerPower = 0f };

        static MatchSimulation InPlay(MatchConfig config = null)
        {
            var sim = new MatchSimulation(config ?? Precise(), seed: 1);
            sim.SkipKickoff();
            // Park everyone out of the way in a column on the touchline.
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
                sim.PlacePlayer(id, new Vec2(14f + id * 0.9f, 0.5f), new Vec2(1f, 0f));
            return sim;
        }

        static PlayerInput[] NoInput() => new PlayerInput[MatchSimulation.PlayerCount];

        static void Step(MatchSimulation sim, PlayerInput[] inputs = null) => sim.Step(inputs ?? NoInput(), Dt);

        static bool RunUntil(MatchSimulation sim, Func<bool> condition, float seconds = 5f)
        {
            for (int i = 0; i < seconds / Dt; i++)
            {
                if (condition()) return true;
                Step(sim);
            }
            return condition();
        }

        static void GiveBall(MatchSimulation sim, int id, Vec2 at, Vec2 facing)
        {
            sim.PlacePlayer(id, at, facing);
            sim.PlaceBall(at + facing.Normalized * 0.5f, Vec2.Zero, owner: id);
        }

        [Fact]
        public void NewMatch_LinesUpForKickoffWithTakerOnTheBall()
        {
            var sim = new MatchSimulation(new MatchConfig(), seed: 1, firstKickoffTeam: 1);

            Assert.Equal(Phase.Kickoff, sim.Phase);
            Assert.Equal(MatchSimulation.PlayerId(1, MatchSimulation.KickoffSlot), sim.Ball.Owner);
            Assert.Equal(new Vec2(18f, 10f), sim.Ball.Position);
            foreach (var p in sim.Players)
            {
                bool inOwnHalf = p.Team == 0 ? p.Position.X <= 18f : p.Position.X >= 18f;
                Assert.True(inOwnHalf, $"player {p.Id} at {p.Position}");
            }
        }

        [Fact]
        public void Kickoff_FreezesPlayersThenStartsPlay()
        {
            var sim = new MatchSimulation(new MatchConfig(), seed: 1);
            var start = sim.Players[HomeDefender].Position;
            var inputs = NoInput();
            inputs[HomeDefender].Move = new Vec2(1f, 0f);

            Step(sim, inputs);
            Assert.Equal(start, sim.Players[HomeDefender].Position);

            Assert.True(RunUntil(sim, () => sim.Phase == Phase.Play));
            Step(sim, inputs);
            Assert.NotEqual(start, sim.Players[HomeDefender].Position);
        }

        [Fact]
        public void LowShotIntoTheMouth_IsAGoal()
        {
            var sim = InPlay();
            GiveBall(sim, HomeStriker, new Vec2(28f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35.5f, 3f), new Vec2(-1f, 0f)); // keeper out of position

            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = 0.4f;
            Step(sim, inputs);
            Assert.Contains(sim.Events, e => e.Type == MatchEventType.Shot);

            Assert.True(RunUntil(sim, () => sim.Phase == Phase.GoalScored));
            Assert.Equal(1, sim.Score(0));
            Assert.Equal(0, sim.Score(1));
            Assert.Equal(1, sim.KickoffTeam);
        }

        [Fact]
        public void AfterAGoal_ConcedingTeamKicksOff()
        {
            var sim = InPlay();
            GiveBall(sim, HomeStriker, new Vec2(28f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35.5f, 3f), new Vec2(-1f, 0f));
            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = 0.4f;
            Step(sim, inputs);

            Assert.True(RunUntil(sim, () => sim.Phase == Phase.Kickoff, seconds: 10f));
            Assert.Equal(AwayStriker, sim.Ball.Owner);
        }

        [Fact]
        public void FullPowerShotFromTenMetres_FliesOverThePosts()
        {
            var sim = InPlay();
            GiveBall(sim, HomeStriker, new Vec2(25.5f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35.5f, 3f), new Vec2(-1f, 0f));

            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = 1f;
            Step(sim, inputs);

            // The ball should rebound off the end wall above knee height instead of scoring.
            Assert.True(RunUntil(sim, () => sim.Ball.Velocity.X < 0f || sim.Phase != Phase.Play));
            Assert.Equal(Phase.Play, sim.Phase);
            Assert.Equal(0, sim.Score(0));
        }

        [Fact]
        public void ShotWideOfThePosts_BouncesOffTheEndWall()
        {
            var sim = InPlay();
            sim.PlaceBall(new Vec2(30f, 13f), new Vec2(15f, 0f));

            Assert.True(RunUntil(sim, () => sim.Ball.Velocity.X < 0f));
            Assert.Equal(Phase.Play, sim.Phase);
            Assert.True(sim.Ball.Position.X < sim.Config.PitchLength);
        }

        [Fact]
        public void BallBouncesOffTheSideWall()
        {
            var sim = InPlay();
            sim.PlaceBall(new Vec2(10f, 15f), new Vec2(0f, 12f));

            Assert.True(RunUntil(sim, () => sim.Ball.Velocity.Y < 0f));
            Assert.InRange(sim.Ball.Position.Y, 0f, sim.Config.PitchWidth);
        }

        [Fact]
        public void LooseBall_StopsThroughFriction()
        {
            var sim = InPlay();
            sim.PlaceBall(new Vec2(8f, 10f), new Vec2(3f, 0f));
            Assert.True(RunUntil(sim, () => sim.Ball.Velocity == Vec2.Zero));
        }

        [Fact]
        public void Pass_ReachesTheTeammateItWasAimedAt()
        {
            var sim = InPlay();
            GiveBall(sim, HomeDefender, new Vec2(10f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(HomeStriker, new Vec2(18f, 12f), new Vec2(-1f, 0f));

            var inputs = NoInput();
            inputs[HomeDefender].Pass = true;
            inputs[HomeDefender].Move = new Vec2(1f, 0.2f);
            Step(sim, inputs);
            var pass = sim.Events.Single(e => e.Type == MatchEventType.Pass);
            Assert.Equal(HomeStriker, pass.PlayerB);

            Assert.True(RunUntil(sim, () => sim.Ball.Owner == HomeStriker));
        }

        [Fact]
        public void SuccessfulTackle_KnocksTheBallLooseAndStunsTheCarrier()
        {
            var sim = InPlay(new MatchConfig { TackleSuccessChance = 1f });
            GiveBall(sim, AwayStriker, new Vec2(20f, 10f), new Vec2(-1f, 0f));
            sim.PlacePlayer(HomeDefender, new Vec2(18.6f, 10f), new Vec2(1f, 0f));

            var inputs = NoInput();
            inputs[HomeDefender].Tackle = true;
            Step(sim, inputs);

            Assert.Contains(sim.Events, e => e.Type == MatchEventType.Tackle && e.PlayerA == HomeDefender);
            Assert.NotEqual(AwayStriker, sim.Ball.Owner);
            Assert.True(sim.Players[AwayStriker].StunTime > 0f);
        }

        [Fact]
        public void MissedTackle_StunsTheTackler()
        {
            var sim = InPlay(new MatchConfig { TackleSuccessChance = 0f });
            GiveBall(sim, AwayStriker, new Vec2(20f, 10f), new Vec2(-1f, 0f));
            sim.PlacePlayer(HomeDefender, new Vec2(18.6f, 10f), new Vec2(1f, 0f));

            var inputs = NoInput();
            inputs[HomeDefender].Tackle = true;
            Step(sim, inputs);

            Assert.Contains(sim.Events, e => e.Type == MatchEventType.TackleMissed);
            Assert.Equal(AwayStriker, sim.Ball.Owner);
            Assert.True(sim.Players[HomeDefender].StunTime > 0f);
        }

        [Fact]
        public void Keeper_DeflectsAPowerfulShotStraightAtThem()
        {
            var sim = InPlay();
            GiveBall(sim, HomeStriker, new Vec2(28f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35f, 10f), new Vec2(-1f, 0f));

            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = 0.6f;
            Step(sim, inputs);

            bool saved = false;
            RunUntil(sim, () => saved |= sim.Events.Any(e => e.Type == MatchEventType.Save && e.PlayerA == AwayKeeper), 2f);
            Assert.True(saved);
            Assert.Equal(0, sim.Score(0));
        }

        [Fact]
        public void Keeper_CannotLeaveTheirBox()
        {
            var sim = InPlay();
            sim.PlacePlayer(AwayKeeper, new Vec2(34f, 10f), new Vec2(-1f, 0f));
            var inputs = NoInput();
            inputs[AwayKeeper].Move = new Vec2(-1f, 0f);
            inputs[AwayKeeper].Sprint = true;
            for (int i = 0; i < 60; i++) Step(sim, inputs);

            Assert.True(MatchSimulation.InKeeperBox(sim.Config, 1, sim.Players[AwayKeeper].Position));
        }

        [Fact]
        public void OutfieldPlayers_CannotParkInTheirOwnBox()
        {
            var sim = InPlay();
            sim.PlacePlayer(HomeDefender, new Vec2(6f, 10f), new Vec2(-1f, 0f));
            var inputs = NoInput();
            inputs[HomeDefender].Move = new Vec2(-1f, 0f);
            for (int i = 0; i < 60; i++) Step(sim, inputs);

            Assert.False(MatchSimulation.InKeeperBox(sim.Config, 0, sim.Players[HomeDefender].Position));
        }

        [Fact]
        public void Sprinting_IsFasterAndDrainsStamina()
        {
            var sim = InPlay();
            sim.PlacePlayer(1, new Vec2(10f, 6f), new Vec2(1f, 0f));
            sim.PlacePlayer(2, new Vec2(10f, 14f), new Vec2(1f, 0f));
            var inputs = NoInput();
            inputs[1].Move = new Vec2(1f, 0f);
            inputs[2].Move = new Vec2(1f, 0f);
            inputs[2].Sprint = true;
            // Both accelerate identically up to jogging pace; the sprinter pulls away after that.
            for (int i = 0; i < 180; i++) Step(sim, inputs);

            Assert.True(sim.Players[2].Position.X > sim.Players[1].Position.X + 1f);
            Assert.True(sim.Players[2].Stamina < sim.Players[1].Stamina);
        }

        [Fact]
        public void Clock_EndsMatchOnceAndThenFreezes()
        {
            var sim = new MatchSimulation(new MatchConfig { MatchDurationSeconds = 2f }, seed: 1);
            int ends = 0;
            for (int i = 0; i < 180; i++)
            {
                Step(sim);
                ends += sim.Events.Count(e => e.Type == MatchEventType.MatchEnded);
            }
            Assert.Equal(1, ends);
            Assert.Equal(Phase.Ended, sim.Phase);
        }

        [Fact]
        public void LevelCupTie_GoesToGoldenGoalAndEndsOnTheNextGoal()
        {
            var config = Precise();
            config.MatchDurationSeconds = 1f;
            config.GoldenGoalIfLevel = true;
            var sim = InPlay(config);

            bool goldenGoal = false;
            RunUntil(sim, () => goldenGoal |= sim.Events.Any(e => e.Type == MatchEventType.GoldenGoal), 2f);
            Assert.True(goldenGoal);
            Assert.True(sim.IsGoldenGoal);
            Assert.NotEqual(Phase.Ended, sim.Phase);

            // Play carries on past full time until someone scores.
            for (int i = 0; i < 90; i++) Step(sim);
            Assert.NotEqual(Phase.Ended, sim.Phase);

            GiveBall(sim, HomeStriker, new Vec2(28f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35.5f, 3f), new Vec2(-1f, 0f));
            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = 0.4f;
            Step(sim, inputs);

            Assert.True(RunUntil(sim, () => sim.Phase == Phase.Ended, 5f));
            Assert.Equal(1, sim.Score(0));
        }

        [Fact]
        public void LevelFriendly_EndsAsADraw()
        {
            var sim = new MatchSimulation(new MatchConfig { MatchDurationSeconds = 1f }, seed: 1);
            Assert.True(RunUntil(sim, () => sim.Phase == Phase.Ended, 2f));
            Assert.False(sim.IsGoldenGoal);
        }

        [Theory]
        [InlineData(-0.03f, true)]
        [InlineData(0.08f, false)]
        public void MaxSafePower_PredictsWhetherAShotGoesOver(float powerOffset, bool expectGoal)
        {
            var sim = InPlay();
            var ballStart = new Vec2(26.5f, 10f);
            float safe = ShotPhysics.MaxSafePower(sim.Config, sim.Config.PitchLength - ballStart.X);
            Assert.InRange(safe, 0.3f, 0.95f);

            GiveBall(sim, HomeStriker, new Vec2(26f, 10f), new Vec2(1f, 0f));
            sim.PlacePlayer(AwayKeeper, new Vec2(35.5f, 3f), new Vec2(-1f, 0f));
            var inputs = NoInput();
            inputs[HomeStriker].Shoot = true;
            inputs[HomeStriker].Power = safe + powerOffset;
            Step(sim, inputs);

            RunUntil(sim, () => sim.Phase == Phase.GoalScored || sim.Ball.Velocity.X < 0f);
            Assert.Equal(expectGoal, sim.Phase == Phase.GoalScored);
        }

        [Fact]
        public void Step_RejectsWrongInputCount()
        {
            var sim = new MatchSimulation(new MatchConfig(), seed: 1);
            Assert.Throws<ArgumentException>(() => sim.Step(new PlayerInput[3], Dt));
        }
    }
}
