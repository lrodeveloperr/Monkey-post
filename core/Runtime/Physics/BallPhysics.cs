using System;

namespace MonkeyPost.Core
{
    /// <summary>The ball's rigid-body state: centre, velocity and angular velocity (rad/s).</summary>
    public struct BallBody
    {
        public Vec3 Centre;
        public Vec3 Velocity;
        public Vec3 Spin;
    }

    /// <summary>
    /// Football ball physics, shared by the match and the shot predictor so they always agree.
    ///
    /// In the air: gravity, quadratic drag with a drag crisis (hard-hit balls knuckle and carry),
    /// Magnus lift from spin (curl, topspin dip, backspin float) and slow spin decay.
    /// On the ground: the ball skids until friction locks spin to the roll, then rolls with the
    /// surface's rolling resistance. Bounces and wall/post/body contacts use impulses with
    /// friction, so spin and velocity trade off (backspin checks up, topspin kicks on).
    /// The ball is modelled as a hollow sphere (I = 2/3 m r²).
    /// </summary>
    public static class BallPhysics
    {
        /// <summary>I / (m r²) for a hollow sphere.</summary>
        public const float InertiaFactor = 2f / 3f;

        /// <summary>Largest physics step for the ball, so fast shots can't pass through players or posts.</summary>
        public const float MaxStep = 1f / 240f;

        const float GroundTolerance = 1e-3f;
        const float MinBounceSpeed = 0.35f;
        const float SlipTolerance = 0.02f;
        const float GroundSideSpinDecay = 4f;

        /// <summary>Resting or rolling on the ground (not landing: a falling ball must bounce first).</summary>
        public static bool IsGrounded(in BallBody b, float radius) =>
            b.Centre.Z <= radius + GroundTolerance && Math.Abs(b.Velocity.Z) <= 0.01f;

        /// <summary>Advances the ball by <paramref name="h"/> seconds. Returns true if it bounced.</summary>
        /// <param name="rng">Used for surface bumpiness; pass null for a perfectly even surface (predictions).</param>
        public static bool Integrate(ref BallBody b, MatchConfig c, SurfaceProfile s, DeterministicRandom rng, float h)
        {
            float r = c.BallRadius;
            if (!IsGrounded(b, r)) return StepAir(ref b, c, s, rng, h);
            StepGround(ref b, c, s, rng, h);
            return false;
        }

        static bool StepAir(ref BallBody b, MatchConfig c, SurfaceProfile s, DeterministicRandom rng, float h)
        {
            float r = c.BallRadius;
            var v = b.Velocity;
            var acceleration = new Vec3(0f, 0f, -c.Gravity);

            float speed = v.Length;
            if (speed > 0.01f)
            {
                float area = (float)Math.PI * r * r;
                float pressurePerMass = 0.5f * c.AirDensity * area * speed * speed / c.BallMass;
                acceleration += -(v / speed) * (pressurePerMass * DragCoefficient(c, speed));

                float spinRate = b.Spin.Length;
                if (spinRate > 0.01f)
                {
                    float spinParameter = r * spinRate / speed;
                    float lift = Math.Min(c.LiftCoefficientMax, spinParameter);
                    acceleration += Vec3.Cross(b.Spin, v).Normalized * (pressurePerMass * lift);
                }
            }

            b.Velocity += acceleration * h;
            b.Centre += b.Velocity * h;
            b.Spin *= (float)Math.Exp(-h / c.SpinDecaySeconds);

            if (b.Centre.Z >= r) return false;

            b.Centre = new Vec3(b.Centre.X, b.Centre.Y, r);
            if (b.Velocity.Z > -c.BounceThreshold)
            {
                b.Velocity = new Vec3(b.Velocity.X, b.Velocity.Y, 0f);
                return false;
            }

            Contact(ref b, Vec3.Up, Vec3.Zero, s.Restitution, s.SlidingFriction, r);
            if (rng != null && s.Bumpiness > 0f)
            {
                // Uneven ground knocks the bounce off line a little.
                var flat = b.Velocity.XY.Rotate(rng.Range(-1f, 1f) * s.Bumpiness * 0.15f);
                float up = b.Velocity.Z * (1f + rng.Range(-1f, 1f) * s.Bumpiness * 0.3f);
                b.Velocity = new Vec3(flat, up);
            }
            if (b.Velocity.Z < MinBounceSpeed) b.Velocity = new Vec3(b.Velocity.X, b.Velocity.Y, 0f);
            return true;
        }

        static void StepGround(ref BallBody b, MatchConfig c, SurfaceProfile s, DeterministicRandom rng, float h)
        {
            float r = c.BallRadius;
            b.Centre = new Vec3(b.Centre.X, b.Centre.Y, r);
            b.Velocity = new Vec3(b.Velocity.X, b.Velocity.Y, 0f);

            // Velocity of the contact patch: zero when rolling, otherwise the ball is skidding.
            var slipVelocity = new Vec2(b.Velocity.X - r * b.Spin.Y, b.Velocity.Y + r * b.Spin.X);
            float slip = slipVelocity.Length;
            if (slip > SlipTolerance)
            {
                float k = InertiaFactor;
                float change = Math.Min(s.SlidingFriction * c.Gravity * h, slip * k / (1f + k));
                var dv = new Vec3(-slipVelocity.X / slip * change, -slipVelocity.Y / slip * change, 0f);
                b.Velocity += dv;
                b.Spin += Vec3.Cross(new Vec3(0f, 0f, -r), dv) / (k * r * r);
            }
            else
            {
                var flat = b.Velocity.XY;
                float speed = flat.Length;
                if (speed > 0f)
                {
                    flat *= Math.Max(0f, speed - s.RollingDeceleration * h) / speed;
                    if (rng != null && s.Bumpiness > 0f && speed > 0.5f)
                        flat += flat.Normalized.Perpendicular * (rng.Range(-1f, 1f) * s.Bumpiness * 3f * h);
                }
                b.Velocity = new Vec3(flat, 0f);
                b.Spin = new Vec3(-flat.Y / r, flat.X / r, b.Spin.Z);
            }

            b.Spin = new Vec3(b.Spin.X, b.Spin.Y, b.Spin.Z * (float)Math.Exp(-h * GroundSideSpinDecay));
            b.Centre += new Vec3(b.Velocity.X, b.Velocity.Y, 0f) * h;
        }

        /// <summary>
        /// Impulse response against a surface with outward normal <paramref name="normal"/>
        /// (pointing from the surface towards the ball) moving at <paramref name="surfaceVelocity"/>.
        /// Friction at the contact point trades linear and angular velocity. Returns false if the
        /// ball was already moving away.
        /// </summary>
        public static bool Contact(ref BallBody b, Vec3 normal, Vec3 surfaceVelocity, float restitution, float friction, float radius)
        {
            var contactOffset = -normal * radius;
            var contactVelocity = b.Velocity + Vec3.Cross(b.Spin, contactOffset) - surfaceVelocity;
            float normalSpeed = Vec3.Dot(contactVelocity, normal);
            if (normalSpeed >= 0f) return false;

            float normalImpulse = -(1f + restitution) * normalSpeed;
            b.Velocity += normal * normalImpulse;

            var tangential = contactVelocity - normal * normalSpeed;
            float tangentialSpeed = tangential.Length;
            if (tangentialSpeed > 1e-4f)
            {
                float k = InertiaFactor;
                float impulse = Math.Min(friction * normalImpulse, tangentialSpeed * k / (1f + k));
                var dv = tangential / tangentialSpeed * -impulse;
                b.Velocity += dv;
                b.Spin += Vec3.Cross(contactOffset, dv) / (k * radius * radius);
            }
            return true;
        }

        /// <summary>Drag coefficient with the drag crisis: high at low speed, dropping for hard-hit balls.</summary>
        public static float DragCoefficient(MatchConfig c, float speed)
        {
            float t = (speed - c.DragCrisisStartSpeed) / (c.DragCrisisEndSpeed - c.DragCrisisStartSpeed);
            t = Math.Max(0f, Math.Min(1f, t));
            t = t * t * (3f - 2f * t);
            return c.DragCoefficientSlow + (c.DragCoefficientFast - c.DragCoefficientSlow) * t;
        }

        /// <summary>Spin that makes a ball moving at <paramref name="velocity"/> roll without slipping.</summary>
        public static Vec3 RollingSpin(Vec2 velocity, float radius) => new Vec3(-velocity.Y / radius, velocity.X / radius, 0f);
    }
}
