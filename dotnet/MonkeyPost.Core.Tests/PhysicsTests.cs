using System;
using System.Linq;
using Xunit;

namespace MonkeyPost.Core.Tests
{
    /// <summary>Checks the ball and player physics behave like real football.</summary>
    public class PhysicsTests
    {
        const float H = BallPhysics.MaxStep;
        const float Dt = 1f / 60f;

        static readonly MatchConfig Config = new MatchConfig();
        static float R => Config.BallRadius;

        static BallBody Ball(Vec3 velocity, Vec3 spin = default, float height = 0f) =>
            new BallBody { Centre = new Vec3(0f, 10f, R + height), Velocity = velocity, Spin = spin };

        /// <summary>Flies the ball until it first lands (its first bounce); returns where.</summary>
        static Vec3 FirstLanding(BallBody b, PitchSurface surface = PitchSurface.LagosCage)
        {
            var s = SurfaceProfile.For(surface);
            for (int i = 0; i < 5000; i++)
            {
                if (BallPhysics.Integrate(ref b, Config, s, null, H)) return b.Centre;
            }
            throw new InvalidOperationException("never landed");
        }

        static BallBody Run(BallBody b, float seconds, PitchSurface surface = PitchSurface.LagosCage)
        {
            var s = SurfaceProfile.For(surface);
            for (int i = 0; i < seconds / H; i++) BallPhysics.Integrate(ref b, Config, s, null, H);
            return b;
        }

        static Vec3 Lofted(float speed, float degrees)
        {
            double a = degrees * Math.PI / 180.0;
            return new Vec3((float)(speed * Math.Cos(a)), 0f, (float)(speed * Math.Sin(a)));
        }

        [Fact]
        public void AirDrag_ShortensALoftedBall()
        {
            var landing = FirstLanding(Ball(Lofted(20f, 30f)));
            float vacuumRange = 20f * 20f * (float)Math.Sin(Math.PI / 3) / Config.Gravity;
            Assert.InRange(landing.X, vacuumRange * 0.6f, vacuumRange * 0.95f);
        }

        [Fact]
        public void DragCrisis_HardHitBallsMeetLessDragPerUnitSpeed()
        {
            Assert.True(BallPhysics.DragCoefficient(Config, 25f) < BallPhysics.DragCoefficient(Config, 5f));
        }

        [Fact]
        public void Sidespin_CurlsTheBall()
        {
            // Spinning anticlockwise seen from above, moving along +X: curls towards +Y.
            var curled = Run(Ball(Lofted(25f, 5f), new Vec3(0f, 0f, 60f)), 0.8f);
            var straight = Run(Ball(Lofted(25f, 5f)), 0.8f);
            float bend = curled.Centre.Y - straight.Centre.Y;
            Assert.InRange(bend, 0.5f, 5f);

            var other = Run(Ball(Lofted(25f, 5f), new Vec3(0f, 0f, -60f)), 0.8f);
            Assert.True(other.Centre.Y < straight.Centre.Y);
        }

        [Fact]
        public void Topspin_Dips_AndBackspin_Floats()
        {
            var plain = FirstLanding(Ball(Lofted(18f, 25f)));
            // Topspin: spin axis +Y for a ball moving along +X.
            var topspin = FirstLanding(Ball(Lofted(18f, 25f), new Vec3(0f, 40f, 0f)));
            var backspin = FirstLanding(Ball(Lofted(18f, 25f), new Vec3(0f, -40f, 0f)));
            Assert.True(topspin.X < plain.X - 1f, $"topspin {topspin.X} vs plain {plain.X}");
            Assert.True(backspin.X > plain.X + 1f, $"backspin {backspin.X} vs plain {plain.X}");
        }

        [Fact]
        public void SkiddingBall_SettlesIntoARoll()
        {
            // Struck along the ground with no spin: it skids, friction spins it up, then it rolls.
            var skidding = Run(Ball(new Vec3(10f, 0f, 0f)), 0.3f);
            Assert.True(skidding.Velocity.X - R * skidding.Spin.Y > 1f, "should still be skidding early on");

            var b = Run(Ball(new Vec3(10f, 0f, 0f)), 1.5f);
            float speed = b.Velocity.Length;
            // A hollow sphere keeps 3/5 of its speed once rolling (minus some rolling resistance).
            Assert.InRange(speed, 4.8f, 6.1f);
            Assert.Equal(speed / R, b.Spin.Y, 1);
        }

        [Fact]
        public void Backspin_ChecksUpOnTheBounce_TopspinKicksOn()
        {
            var falling = new Vec3(8f, 0f, -4f);
            var top = Ball(falling, new Vec3(0f, 120f, 0f), height: 0.01f);
            var back = Ball(falling, new Vec3(0f, -120f, 0f), height: 0.01f);
            var s = SurfaceProfile.For(PitchSurface.LagosCage);
            BallPhysics.Integrate(ref top, Config, s, null, 0.01f);
            BallPhysics.Integrate(ref back, Config, s, null, 0.01f);

            Assert.True(top.Velocity.X > 8f, $"topspin should kick on, got {top.Velocity.X}");
            Assert.True(back.Velocity.X < 7f, $"backspin should check up, got {back.Velocity.X}");
            Assert.True(top.Velocity.Z > 0f && back.Velocity.Z > 0f);
        }

        [Fact]
        public void Surfaces_ChangeHowFarTheBallRolls()
        {
            var rolling = Ball(new Vec3(8f, 0f, 0f), BallPhysics.RollingSpin(new Vec2(8f, 0f), Config.BallRadius));
            float concrete = Run(rolling, 6f, PitchSurface.PortHarcourtWaterfront).Centre.X;
            float turf = Run(rolling, 6f, PitchSurface.LagosCage).Centre.X;
            float sand = Run(rolling, 6f, PitchSurface.KanoSandCourt).Centre.X;
            Assert.True(concrete > turf && turf > sand, $"concrete {concrete}, turf {turf}, sand {sand}");
        }

        [Fact]
        public void Surfaces_ChangeHowHighTheBallBounces()
        {
            float BounceHeight(PitchSurface surface)
            {
                var b = Ball(Vec3.Zero, height: 1.5f);
                var s = SurfaceProfile.For(surface);
                bool bounced = false;
                float peak = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    bounced |= BallPhysics.Integrate(ref b, Config, s, null, H);
                    if (bounced) peak = Math.Max(peak, b.Centre.Z - R);
                }
                return peak;
            }

            Assert.True(BounceHeight(PitchSurface.PortHarcourtWaterfront) > BounceHeight(PitchSurface.LagosCage));
            Assert.True(BounceHeight(PitchSurface.LagosCage) > BounceHeight(PitchSurface.KanoSandCourt));
        }

        [Fact]
        public void PlannedCurlingShot_ArrivesWhereItWasAimed()
        {
            var start = new Vec3(26f, 12f, R);
            var target = new Vec2(36f, 10.8f);
            var plan = ShotPhysics.PlanShot(Config, start, target, 0.4f, 0.9f);
            Assert.NotEqual(0f, plan.Spin.Z);

            var b = new BallBody { Centre = start, Velocity = plan.Velocity, Spin = plan.Spin };
            var s = Config.SurfaceProfile;
            while (b.Centre.X < target.X) BallPhysics.Integrate(ref b, Config, s, null, H);
            Assert.InRange(b.Centre.Y, target.Y - 0.15f, target.Y + 0.15f);
        }

        [Fact]
        public void PlannedPass_ArrivesAtAControllablePace()
        {
            var plan = ShotPhysics.PlanPass(Config, new Vec2(5f, 10f), new Vec2(20f, 10f));
            var b = new BallBody { Centre = new Vec3(5f, 10f, R), Velocity = plan.Velocity, Spin = plan.Spin };
            var s = Config.SurfaceProfile;
            while (b.Centre.X < 20f) BallPhysics.Integrate(ref b, Config, s, null, H);
            Assert.InRange(b.Velocity.Length, Config.PassArrivalSpeed - 0.5f, Config.PassArrivalSpeed + 0.5f);
        }

        // ---- In-match physics ---------------------------------------------------------------

        static MatchSimulation InPlay(MatchConfig config = null)
        {
            var sim = new MatchSimulation(config ?? new MatchConfig { ShotSpreadBase = 0f, ShotSpreadPerPower = 0f }, seed: 1);
            sim.SkipKickoff();
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
                sim.PlacePlayer(id, new Vec2(10f + id * 1.5f, 0.6f), new Vec2(1f, 0f));
            return sim;
        }

        static PlayerInput[] Inputs(Action<PlayerInput[]> set = null)
        {
            var inputs = new PlayerInput[MatchSimulation.PlayerCount];
            set?.Invoke(inputs);
            return inputs;
        }

        [Fact]
        public void Dribbling_TheBallLeavesTheFootButStaysUnderControl()
        {
            var sim = InPlay();
            sim.PlacePlayer(3, new Vec2(6f, 10f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(6.5f, 10f), Vec2.Zero, owner: 3);

            float maxGap = 0f, minGap = float.MaxValue;
            for (int i = 0; i < 180; i++)
            {
                sim.Step(Inputs(x => x[3].Move = new Vec2(1f, 0f)), Dt);
                if (i < 30) continue;
                float gap = Vec2.Distance(sim.Players[3].Position, sim.Ball.Position);
                maxGap = Math.Max(maxGap, gap);
                minGap = Math.Min(minGap, gap);
            }
            Assert.Equal(3, sim.Ball.Owner);
            Assert.True(maxGap - minGap > 0.15f, "the ball should move relative to the feet between touches");
            Assert.True(maxGap < 1.6f, $"ball got {maxGap} m away while jogging");
            Assert.True(sim.Players[3].Position.X > 15f);
        }

        [Fact]
        public void Sprinting_PushesTheBallFurtherAhead()
        {
            float MaxGap(bool sprint)
            {
                var sim = InPlay();
                sim.PlacePlayer(3, new Vec2(4f, 10f), new Vec2(1f, 0f));
                sim.PlaceBall(new Vec2(4.5f, 10f), Vec2.Zero, owner: 3);
                float gap = 0f;
                for (int i = 0; i < 150; i++)
                {
                    sim.Step(Inputs(x => { x[3].Move = new Vec2(1f, 0f); x[3].Sprint = sprint; }), Dt);
                    if (i > 60) gap = Math.Max(gap, Vec2.Distance(sim.Players[3].Position, sim.Ball.Position));
                }
                return gap;
            }
            Assert.True(MaxGap(true) > MaxGap(false) + 0.2f);
        }

        [Fact]
        public void ShotPressedJustOutOfReach_StepsUpAndStrikesIt()
        {
            var sim = InPlay();
            sim.PlacePlayer(4, new Vec2(24f, 10f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(25.4f, 10f), Vec2.Zero);

            bool shot = false;
            sim.Step(Inputs(x => { x[4].Shoot = true; x[4].Power = 0.5f; }), Dt);
            for (int i = 0; i < 24 && !shot; i++)
            {
                shot = sim.Events.Any(e => e.Type == MatchEventType.Shot);
                if (!shot) sim.Step(Inputs(), Dt);
            }
            Assert.True(shot || sim.Events.Any(e => e.Type == MatchEventType.Shot));
        }

        [Fact]
        public void FastBall_DeflectsOffAPlayerInsteadOfBeingControlled()
        {
            var sim = InPlay();
            sim.PlacePlayer(1, new Vec2(15f, 10f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(18f, 10f), new Vec2(-22f, 0f), spin: BallPhysics.RollingSpin(new Vec2(-22f, 0f), 0.11f));

            bool blocked = false;
            for (int i = 0; i < 30; i++)
            {
                sim.Step(Inputs(), Dt);
                blocked |= sim.Events.Any(e => e.Type == MatchEventType.Block);
            }
            Assert.True(blocked);
            Assert.NotEqual(1, sim.Ball.Owner);
        }

        [Fact]
        public void SlowPass_IsControlledCleanly()
        {
            var sim = InPlay();
            sim.PlacePlayer(1, new Vec2(15f, 10f), new Vec2(1f, 0f));
            var v = new Vec2(-5f, 0f);
            sim.PlaceBall(new Vec2(18f, 10f), v, spin: BallPhysics.RollingSpin(v, 0.11f));

            for (int i = 0; i < 60; i++) sim.Step(Inputs(), Dt);
            Assert.Equal(1, sim.Ball.Owner);
            Assert.True(Vec2.Distance(sim.Players[1].Position, sim.Ball.Position) < 1.2f);
        }

        [Fact]
        public void BallHittingAPost_ReboundsAndRaisesAPostEvent()
        {
            var sim = InPlay();
            float postY = 10f + sim.Config.GoalWidth / 2f + sim.Config.PostRadius;
            var v = new Vec2(15f, 0f);
            sim.PlaceBall(new Vec2(33f, postY), v, spin: BallPhysics.RollingSpin(v, 0.11f));

            bool post = false;
            for (int i = 0; i < 40 && !post; i++)
            {
                sim.Step(Inputs(), Dt);
                post = sim.Events.Any(e => e.Type == MatchEventType.Post);
            }
            Assert.True(post);
            Assert.True(sim.Ball.Velocity.X < 0f);
            Assert.Equal(0, sim.Score(0));
        }

        [Fact]
        public void CurledShotAtThePost_BeatsAKeeperStandingInTheMiddle()
        {
            var sim = InPlay();
            sim.PlacePlayer(4, new Vec2(27f, 12f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(27.5f, 12f), Vec2.Zero, owner: 4);
            sim.PlacePlayer(5, new Vec2(35.3f, 10f), new Vec2(-1f, 0f));

            sim.Step(Inputs(x => { x[4].Shoot = true; x[4].Power = 0.45f; x[4].Aim = -0.85f; }), Dt);
            int scorer = -1;
            for (int i = 0; i < 90 && sim.Phase == Phase.Play; i++)
            {
                sim.Step(Inputs(), Dt);
                foreach (var e in sim.Events) if (e.Type == MatchEventType.Goal) scorer = e.PlayerA;
            }
            Assert.Equal(1, sim.Score(0));
            // Even if it clips the keeper on the way in, it's the shooter's goal.
            Assert.Equal(4, scorer);
        }

        [Fact]
        public void SprintingPlayer_PlantsBeforeReversing()
        {
            var sim = InPlay();
            sim.PlacePlayer(2, new Vec2(10f, 10f), new Vec2(1f, 0f));
            for (int i = 0; i < 120; i++) sim.Step(Inputs(x => { x[2].Move = new Vec2(1f, 0f); x[2].Sprint = true; }), Dt);
            float topSpeed = sim.Players[2].Velocity.X;
            Assert.True(topSpeed > sim.Config.JogSpeed);

            for (int i = 0; i < 6; i++) sim.Step(Inputs(x => x[2].Move = new Vec2(-1f, 0f)), Dt);
            // Still travelling the original way, but braking hard.
            Assert.True(sim.Players[2].Velocity.X > 0f && sim.Players[2].Velocity.X < topSpeed - 1f);

            for (int i = 0; i < 60; i++) sim.Step(Inputs(x => x[2].Move = new Vec2(-1f, 0f)), Dt);
            Assert.True(sim.Players[2].Velocity.X < -2f);
        }

        [Fact]
        public void Keeper_TakesTheirFirstTouchAwayFromGoal()
        {
            var sim = InPlay();
            sim.PlacePlayer(0, new Vec2(1.2f, 10f), new Vec2(-1f, 0f));
            var v = new Vec2(-4f, 0f);
            sim.PlaceBall(new Vec2(3f, 10f), v, spin: BallPhysics.RollingSpin(v, 0.11f));

            for (int i = 0; i < 60; i++) sim.Step(Inputs(x => x[0].Move = new Vec2(-0.3f, 0f)), Dt);
            Assert.Equal(0, sim.Score(1));
            Assert.True(sim.Ball.Position.X > sim.Players[0].Position.X);
        }
    }
}
