using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Footballer locomotion with momentum: acceleration and braking take time, turning circles
    /// widen with speed, and a sharp change of direction at speed means planting and braking first.
    /// Tuned to feel realistic but stay responsive on a touchscreen.
    /// </summary>
    public static class PlayerMotion
    {
        const float NearlyStill = 0.6f;

        /// <summary>Updates <paramref name="p"/>'s velocity and facing towards the desired movement.</summary>
        public static void Steer(PlayerState p, Vec2 move, bool wantsSprint, bool dribbling, MatchConfig c, float dt)
        {
            move = move.ClampLength(1f);
            bool sprinting = wantsSprint && p.Stamina > 0f && move.LengthSquared > 0.01f;
            float topSpeed = sprinting ? c.SprintSpeed : c.JogSpeed;
            if (dribbling) topSpeed *= c.DribbleSpeedFactor;
            float desiredSpeed = move.Length * topSpeed;

            p.Stamina = sprinting
                ? Math.Max(0f, p.Stamina - c.StaminaDrainPerSecond * dt)
                : Math.Min(1f, p.Stamina + c.StaminaRegenPerSecond * dt);
            p.IsSprinting = sprinting;

            float speed = p.Velocity.Length;
            var heading = speed > 1e-3f ? p.Velocity / speed : p.Facing;

            if (desiredSpeed > 0.01f)
            {
                var desiredDirection = move.Normalized;
                if (speed < NearlyStill)
                {
                    heading = desiredDirection;
                }
                else
                {
                    float angle = SignedAngle(heading, desiredDirection);
                    if (Math.Abs(angle) > c.PlantTurnAngle && speed > c.JogSpeed * 0.6f)
                    {
                        // Too sharp to turn at this speed: plant and brake first.
                        desiredSpeed = 0f;
                    }
                    else
                    {
                        float speedFraction = Math.Min(1f, speed / c.SprintSpeed);
                        float maxTurn = (c.TurnRateStill + (c.TurnRateSprint - c.TurnRateStill) * speedFraction) * dt;
                        float turn = Math.Max(-maxTurn, Math.Min(maxTurn, angle));
                        heading = heading.Rotate(turn);
                        // Turning hard scrubs a little speed.
                        speed *= 1f - Math.Abs(turn) * 0.15f;
                    }
                }
            }

            if (desiredSpeed > speed)
            {
                float acceleration = speed < c.JogSpeed ? c.Acceleration : c.SprintAcceleration;
                speed = Math.Min(desiredSpeed, speed + acceleration * dt);
            }
            else
            {
                speed = Math.Max(desiredSpeed, speed - c.Deceleration * dt);
            }

            p.Velocity = heading * speed;
            if (speed > 0.1f || desiredSpeed > 0.01f) p.Facing = heading;
        }

        /// <summary>Slows a stunned or frozen player to a stop.</summary>
        public static void Brake(PlayerState p, MatchConfig c, float dt)
        {
            float speed = p.Velocity.Length;
            if (speed <= 0f) return;
            p.Velocity *= Math.Max(0f, speed - c.Deceleration * dt) / speed;
            p.IsSprinting = false;
            p.Stamina = Math.Min(1f, p.Stamina + c.StaminaRegenPerSecond * dt);
        }

        static float SignedAngle(Vec2 from, Vec2 to) =>
            (float)Math.Atan2(from.X * to.Y - from.Y * to.X, Vec2.Dot(from, to));
    }
}
