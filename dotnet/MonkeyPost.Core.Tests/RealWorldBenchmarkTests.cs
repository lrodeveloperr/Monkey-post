using System;
using Xunit;

namespace MonkeyPost.Core.Tests
{
    /// <summary>
    /// The physics checked against real-world measurements. Each test names its reference; the
    /// sources are listed in docs/PHYSICS.md. If a tuning change breaks one of these, the change is
    /// wrong, not the test.
    /// </summary>
    public class RealWorldBenchmarkTests
    {
        const float H = BallPhysics.MaxStep;
        const float Dt = 1f / 60f;
        static readonly MatchConfig Config = new MatchConfig();
        static float R => Config.BallRadius;

        /// <summary>Drops the ball from <paramref name="dropHeight"/> (underside) and returns its first rebound height (underside).</summary>
        static float ReboundHeight(PitchSurface surface, float dropHeight)
        {
            var s = SurfaceProfile.For(surface);
            var b = new BallBody { Centre = new Vec3(0f, 0f, R + dropHeight) };
            bool bounced = false;
            float peak = 0f;
            for (int i = 0; i < 4000; i++)
            {
                bounced |= BallPhysics.Integrate(ref b, Config, s, null, H);
                if (!bounced) continue;
                peak = Math.Max(peak, b.Centre.Z - R);
                if (b.Velocity.Z < 0f) break;
            }
            return peak;
        }

        /// <summary>
        /// FIFA reduced ball roll: released from a 1 m ramp, the ball reaches the surface rolling.
        /// A rolling hollow sphere converts mgh into ½mv²(1 + 2/3), so v = √(2gh / (5/3)).
        /// </summary>
        static float RampRollDistance(PitchSurface surface)
        {
            float v = (float)Math.Sqrt(2f * Config.Gravity * 1f / (1f + BallPhysics.InertiaFactor));
            var s = SurfaceProfile.For(surface);
            var velocity = new Vec2(v, 0f);
            var b = new BallBody { Centre = new Vec3(0f, 0f, R), Velocity = new Vec3(velocity, 0f), Spin = BallPhysics.RollingSpin(velocity, R) };
            for (int i = 0; i < 20 * 240 && b.Velocity.LengthSquared > 1e-6f; i++)
                BallPhysics.Integrate(ref b, Config, s, null, H);
            return b.Centre.X;
        }

        // ---- Ball and surfaces (FIFA Quality Programme test methods) -------------------------

        [Fact]
        public void FifaBallCalibration_TwoMetreDropOnConcreteRebounds_1_35m()
        {
            // FIFA sets ball pressure so a 2.0 m drop on concrete rebounds 1.35 ± 0.03 m.
            Assert.InRange(ReboundHeight(PitchSurface.PortHarcourtWaterfront, 2f), 1.32f, 1.38f);
        }

        [Fact]
        public void LagosTurf_VerticalRebound_IsInFifaQualityProRange()
        {
            // FIFA Quality Pro football turf: 0.60–0.85 m from a 2.0 m drop.
            Assert.InRange(ReboundHeight(PitchSurface.LagosCage, 2f), 0.60f, 0.85f);
        }

        [Fact]
        public void LagosTurf_BallRoll_IsInFifaQualityProRange()
        {
            // FIFA Quality Pro football turf: 4–8 m reduced ball roll.
            Assert.InRange(RampRollDistance(PitchSurface.LagosCage), 4f, 8f);
        }

        [Fact]
        public void EnuguGrass_IsInFifaQualityRange()
        {
            // FIFA Quality grade: rebound 0.60–1.00 m, ball roll 4–10 m.
            Assert.InRange(ReboundHeight(PitchSurface.EnuguHillside, 2f), 0.60f, 1.00f);
            Assert.InRange(RampRollDistance(PitchSurface.EnuguHillside), 4f, 10f);
        }

        [Fact]
        public void Surfaces_RankFromSandToConcrete()
        {
            float sand = RampRollDistance(PitchSurface.KanoSandCourt);
            float earth = RampRollDistance(PitchSurface.IbadanSchoolField);
            float grass = RampRollDistance(PitchSurface.EnuguHillside);
            float turf = RampRollDistance(PitchSurface.LagosCage);
            float asphalt = RampRollDistance(PitchSurface.AbujaEstateCourt);
            float concrete = RampRollDistance(PitchSurface.PortHarcourtWaterfront);
            Assert.True(sand < earth && earth < grass && grass < turf && turf < asphalt && asphalt < concrete,
                $"sand {sand}, earth {earth}, grass {grass}, turf {turf}, asphalt {asphalt}, concrete {concrete}");
            Assert.True(ReboundHeight(PitchSurface.KanoSandCourt, 2f) < 0.3f, "sand should barely bounce");
        }

        [Fact]
        public void HardImpacts_BounceLessThanSoftOnes()
        {
            // Restitution falls with impact speed: a ball dropped from higher keeps a smaller fraction.
            float fromTwo = ReboundHeight(PitchSurface.PortHarcourtWaterfront, 2f) / 2f;
            float fromTen = ReboundHeight(PitchSurface.PortHarcourtWaterfront, 10f) / 10f;
            Assert.True(fromTen < fromTwo);
        }

        // ---- Aerodynamics -------------------------------------------------------------------

        [Fact]
        public void DragCrisis_IsCentredNear12MetresPerSecond()
        {
            // Below the crisis Cd ≈ 0.45; above it, it roughly halves. Crisis near 12 m/s (Physics Today).
            Assert.InRange(BallPhysics.DragCoefficient(Config, 6f), 0.43f, 0.47f);
            Assert.InRange(BallPhysics.DragCoefficient(Config, 22f), 0.18f, 0.25f);
            float mid = BallPhysics.DragCoefficient(Config, 12f);
            Assert.InRange(mid, 0.28f, 0.37f);
        }

        [Fact]
        public void Spin_IncreasesDrag()
        {
            Assert.True(BallPhysics.DragCoefficient(Config, 22f, 0.25f) > BallPhysics.DragCoefficient(Config, 22f, 0f));
        }

        [Fact]
        public void LiftCoefficient_MatchesMeasuredFreeKickRegion()
        {
            // Goff & Carré: Cl ≈ 0.25 around S ≈ 0.22 (a typical curled free kick); lift saturates at high spin.
            Assert.InRange(BallPhysics.LiftCoefficient(Config, 0.22f), 0.22f, 0.28f);
            Assert.InRange(BallPhysics.LiftCoefficient(Config, 1f), 0.3f, 0.45f);
            Assert.True(BallPhysics.LiftCoefficient(Config, 0.05f) < 0.1f);
        }

        [Fact]
        public void CurledFreeKick_BendsMetresOverTwentyFive()
        {
            // A 25 m/s free kick with ~8 rev/s of sidespin, struck over 25 m: about a second in the
            // air and a bend of a few metres, as in the trajectory studies.
            var v = new Vec3(25f * (float)Math.Cos(0.28), 0f, 25f * (float)Math.Sin(0.28));
            var curled = new BallBody { Centre = new Vec3(0f, 0f, R), Velocity = v, Spin = new Vec3(0f, 0f, 50f) };
            var straight = new BallBody { Centre = new Vec3(0f, 0f, R), Velocity = v };
            var s = Config.SurfaceProfile;
            float time = 0f;
            while (curled.Centre.X < 25f && time < 3f)
            {
                BallPhysics.Integrate(ref curled, Config, s, null, H);
                time += H;
            }
            while (straight.Centre.X < 25f) BallPhysics.Integrate(ref straight, Config, s, null, H);

            Assert.InRange(time, 0.95f, 1.4f);
            Assert.InRange(curled.Centre.Y - straight.Centre.Y, 1.5f, 5f);
        }

        [Fact]
        public void LongKick_DragCutsTheVacuumRangeSubstantially()
        {
            // 30 m/s at 40°: about 90 m in a vacuum; a real football lands far shorter.
            double a = 40 * Math.PI / 180;
            var b = new BallBody { Centre = new Vec3(0f, 0f, R), Velocity = new Vec3(30f * (float)Math.Cos(a), 0f, 30f * (float)Math.Sin(a)) };
            float vacuum = 30f * 30f * (float)Math.Sin(2 * a) / Config.Gravity;
            var s = Config.SurfaceProfile;
            for (int i = 0; i < 6000; i++)
                if (BallPhysics.Integrate(ref b, Config, s, null, H)) break;
            Assert.InRange(b.Centre.X, vacuum * 0.5f, vacuum * 0.8f);
        }

        // ---- Players (sports-science sprint and change-of-direction data) --------------------

        static PlayerState Runner() => new PlayerState { Facing = new Vec2(1f, 0f) };

        [Fact]
        public void Sprint_FollowsTheMonoExponentialModel()
        {
            var p = Runner();
            float t = 0f, distance = 0f, tenMetres = -1f, tauTime = -1f, firstAcceleration = -1f;
            for (int i = 0; i < 6 * 60; i++)
            {
                float before = p.Velocity.Length;
                PlayerMotion.Steer(p, new Vec2(1f, 0f), true, false, Config, Dt);
                if (i == 0) firstAcceleration = (p.Velocity.Length - before) / Dt;
                t += Dt;
                distance += p.Velocity.Length * Dt;
                if (tauTime < 0f && p.Velocity.Length >= 0.632f * Config.SprintSpeed) tauTime = t;
                if (tenMetres < 0f && distance >= 10f) tenMetres = t;
            }

            // Initial acceleration within professionals' measured 6.2–8.4 m/s².
            Assert.InRange(firstAcceleration, 6.2f, 8.4f);
            // τ is the time to 63% of top speed.
            Assert.InRange(tauTime, Config.SprintTau - 0.05f, Config.SprintTau + 0.05f);
            // Street players: 10 m from a standing start in about 2.0–2.4 s (pros ≈ 1.7–1.9 s).
            Assert.InRange(tenMetres, 2.0f, 2.4f);
            // Top speed reached, below professionals' ≈ 9.2–9.5 m/s.
            Assert.InRange(p.Velocity.Length, Config.SprintSpeed - 0.1f, 9.2f);
        }

        [Fact]
        public void Braking_IsWithinHumanLimits()
        {
            var p = Runner();
            p.Velocity = new Vec2(Config.SprintSpeed, 0f);
            float t = 0f, distance = 0f;
            while (p.Velocity.Length > 0.01f && t < 5f)
            {
                PlayerMotion.Steer(p, Vec2.Zero, false, false, Config, Dt);
                distance += p.Velocity.Length * Dt;
                t += Dt;
            }
            // From 8 m/s at ≈6.5 m/s²: ≈1.2 s and ≈5 m to stop. No instant stops.
            Assert.InRange(t, 1.1f, 1.4f);
            Assert.InRange(distance, 4.3f, 5.5f);
        }

        [Fact]
        public void CurvedSprint_TurnsOnARealisticRadius()
        {
            // Curved sprints near top speed are run on radii of roughly 7–11 m.
            var p = Runner();
            p.Velocity = new Vec2(Config.SprintSpeed, 0f);
            float minRadius = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                var before = p.Velocity;
                PlayerMotion.Steer(p, new Vec2(0f, 1f).Rotate(-0.6f), true, false, Config, Dt);
                float turned = (float)Math.Acos(Math.Min(1f, Vec2.Dot(before.Normalized, p.Velocity.Normalized)));
                if (turned > 1e-4f) minRadius = Math.Min(minRadius, p.Velocity.Length * Dt / turned);
            }
            Assert.InRange(minRadius, 7f, 11f);
        }

        [Fact]
        public void TotalAcceleration_NeverExceedsGrip()
        {
            var p = Runner();
            p.Velocity = new Vec2(6f, 0f);
            var rng = new DeterministicRandom(3);
            for (int i = 0; i < 600; i++)
            {
                var before = p.Velocity;
                var move = new Vec2(1f, 0f).Rotate(rng.Range(-3.1f, 3.1f));
                PlayerMotion.Steer(p, move, rng.Chance(0.5f), false, Config, Dt);
                float acceleration = (p.Velocity - before).Length / Dt;
                Assert.True(acceleration <= Config.GripAcceleration + 0.5f, $"{acceleration} m/s² at step {i}");
            }
        }
    }
}
