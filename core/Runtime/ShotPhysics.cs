using System;

namespace MonkeyPost.Core
{
    /// <summary>The velocity and spin a kick puts on the ball.</summary>
    public struct KickPlan
    {
        public Vec3 Velocity;
        public Vec3 Spin;
    }

    /// <summary>
    /// Plans kicks with the real ball physics, so curl, dip and drag are accounted for, and
    /// predicts shot height for the power bar. Uses <see cref="BallPhysics"/>, the same code as the
    /// match, so predictions always agree with what happens.
    /// </summary>
    public static class ShotPhysics
    {
        const int MaxPredictionSteps = 1200;
        const int AimIterations = 4;

        /// <summary>
        /// Plans a shot from <paramref name="ballCentre"/> that passes through
        /// <paramref name="target"/> on the goal line. Spin is automatic: power shots get topspin
        /// (they dip); softer shots aimed towards a post get sidespin and curl in, and the launch
        /// direction is corrected so the curl brings the ball onto target.
        /// </summary>
        /// <param name="aim">-1..1 across the goal mouth; sets how much the shot curls.</param>
        public static KickPlan PlanShot(MatchConfig c, Vec3 ballCentre, Vec2 target, float power, float aim)
        {
            power = Clamp(power, 0f, 1f);
            aim = Clamp(aim, -1f, 1f);
            float speed = c.ShotMinSpeed + (c.ShotMaxSpeed - c.ShotMinSpeed) * power;
            float lift = c.ShotMaxLift * power * power;

            var toTarget = target - ballCentre.XY;
            float angle = (float)Math.Atan2(toTarget.Y, toTarget.X);
            float distance = toTarget.Length;
            float towardsXSign = toTarget.X >= 0f ? 1f : -1f;

            // Curl back towards the middle of the goal, so a shot at a post bends in.
            float midY = c.PitchWidth * 0.5f;
            float inward = target.Y > midY ? -1f : 1f;
            float curl = c.ShotCurlSpin * Math.Abs(aim) * (1f - 0.6f * power);
            float sideSpin = curl * inward * towardsXSign;

            KickPlan plan = default;
            for (int i = 0; i < AimIterations; i++)
            {
                var direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
                plan = new KickPlan
                {
                    Velocity = new Vec3(direction * speed, lift),
                    // Topspin axis is horizontal and perpendicular to the flight: it makes the ball dip.
                    Spin = new Vec3(-direction.Y, direction.X, 0f) * (c.ShotTopspin * power) + new Vec3(0f, 0f, sideSpin),
                };
                if (sideSpin == 0f || distance < 0.5f) break;

                var body = new BallBody { Centre = ballCentre, Velocity = plan.Velocity, Spin = plan.Spin };
                if (!FlyToLine(c, ref body, target.X, out _)) break;
                float miss = body.Centre.Y - target.Y;
                angle -= (float)Math.Atan2(miss, distance) * (direction.X >= 0f ? 1f : -1f);
            }
            return plan;
        }

        /// <summary>
        /// Plans a ground pass that reaches <paramref name="target"/> at about the configured
        /// arrival speed, already rolling (no skid), so it's easy to control.
        /// </summary>
        /// <param name="firmness">Multiplies the arrival speed: 1 for a normal pass, 2 for a keeper's firm clearance.</param>
        public static KickPlan PlanPass(MatchConfig c, Vec2 from, Vec2 target, float firmness = 1f)
        {
            var offset = target - from;
            float distance = offset.Length;
            // Rolling deceleration is a + b·v: invert it numerically for the launch speed that
            // arrives at the target at the chosen pace.
            var surface = c.SurfaceProfile;
            float arrival = c.PassArrivalSpeed * firmness;
            float speed = LaunchSpeedForArrival(surface.RollingDeceleration, surface.RollingDragPerSpeed, distance, arrival);
            speed = Clamp(speed, c.PassMinSpeed, c.PassMaxSpeed);
            var velocity = offset.Normalized * speed;
            return new KickPlan { Velocity = new Vec3(velocity, 0f), Spin = BallPhysics.RollingSpin(velocity, c.BallRadius) };
        }

        /// <summary>Launch speed so a ball rolling with deceleration a + b·v covers <paramref name="distance"/> and arrives at <paramref name="arrival"/>.</summary>
        static float LaunchSpeedForArrival(float a, float b, float distance, float arrival)
        {
            // Distance to slow from v0 to v1 under dv/dx = -(a + b v)/v:
            // x = (v0 − v1)/b − (a/b²)·ln((a + b v0)/(a + b v1)).
            if (b < 1e-4f) return (float)Math.Sqrt(arrival * arrival + 2f * a * distance);
            double low = arrival, high = arrival + 60.0;
            for (int i = 0; i < 40; i++)
            {
                double mid = (low + high) * 0.5;
                double x = (mid - arrival) / b - a / (b * b) * Math.Log((a + b * mid) / (a + b * arrival));
                if (x < distance) low = mid;
                else high = mid;
            }
            return (float)((low + high) * 0.5);
        }

        /// <summary>
        /// Height of the bottom of the ball when a straight shot of <paramref name="power"/> has
        /// travelled <paramref name="distance"/> metres, ignoring walls and players. Returns 0 if
        /// the ball stops before getting there.
        /// </summary>
        public static float HeightAfter(MatchConfig c, float power, float distance)
        {
            var start = new Vec3(0f, c.PitchWidth * 0.5f, c.BallRadius);
            var plan = PlanShot(c, start, new Vec2(distance, start.Y), power, 0f);
            var body = new BallBody { Centre = start, Velocity = plan.Velocity, Spin = plan.Spin };
            return FlyToLine(c, ref body, distance, out _) ? body.Centre.Z - c.BallRadius : 0f;
        }

        /// <summary>The highest power (to 0.01) that still crosses the goal line under knee height.</summary>
        public static float MaxSafePower(MatchConfig c, float distanceToGoalLine)
        {
            // Coarse scan, then refine: height isn't monotonic in power at very short range.
            float safe = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float power = i * 0.05f;
                if (HeightAfter(c, power, distanceToGoalLine) > c.GoalHeight) break;
                safe = power;
            }
            for (int i = 1; i < 5; i++)
            {
                float power = safe + i * 0.01f;
                if (power > 1f || HeightAfter(c, power, distanceToGoalLine) > c.GoalHeight) break;
                safe = power;
            }
            return safe;
        }

        /// <summary>Flies the ball (on an even surface, no walls) until it crosses X = <paramref name="lineX"/>.</summary>
        static bool FlyToLine(MatchConfig c, ref BallBody body, float lineX, out float time)
        {
            var surface = c.SurfaceProfile;
            float startSide = Math.Sign(lineX - body.Centre.X);
            time = 0f;
            for (int i = 0; i < MaxPredictionSteps; i++)
            {
                BallPhysics.Integrate(ref body, c, surface, null, BallPhysics.MaxStep);
                time += BallPhysics.MaxStep;
                if (Math.Sign(lineX - body.Centre.X) != startSide) return true;
                if (body.Velocity.LengthSquared < 0.01f) return false;
            }
            return false;
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
