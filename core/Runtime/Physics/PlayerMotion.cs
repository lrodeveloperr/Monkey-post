using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Footballer locomotion from sports-science data (see docs/PHYSICS.md):
    /// <list type="bullet">
    /// <item>Speeding up follows the mono-exponential sprint model: a = (vmax − v) / τ.</item>
    /// <item>Braking is limited to a realistic maximum deceleration.</item>
    /// <item>Speeding up, braking and turning share one grip budget (the friction circle), so
    /// tight turns at speed are impossible: a sprinter curves on a radius of about v² / grip.</item>
    /// <item>A sharp change of direction at speed means planting and braking first.</item>
    /// </list>
    /// Inputs act immediately (no artificial delay); the momentum is the physics.
    /// </summary>
    public static class PlayerMotion
    {
        const float SwivelSpeed = 0.8f;
        const float HardTurnAngle = 0.5f;

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

            if (desiredSpeed < 0.01f)
            {
                speed = Math.Max(0f, speed - c.Deceleration * dt);
                p.Velocity = heading * speed;
                return;
            }

            var direction = move.Normalized;
            float angle = SignedAngle(heading, direction);

            if (speed < SwivelSpeed)
            {
                // Nearly still: swivel on the spot, then drive off.
                float swivel = c.TurnRateStill * dt;
                heading = heading.Rotate(Math.Max(-swivel, Math.Min(swivel, angle)));
                speed = Math.Min(desiredSpeed, speed + Propulsion(c, speed) * dt);
            }
            else if (Math.Abs(angle) > c.PlantTurnAngle && speed > c.JogSpeed * 0.55f)
            {
                // Too sharp to turn at this speed: plant and brake first.
                speed = Math.Max(0f, speed - c.Deceleration * dt);
            }
            else
            {
                // Longitudinal acceleration first: don't accelerate while turning hard.
                float along = desiredSpeed > speed
                    ? Math.Min(Propulsion(c, speed), (desiredSpeed - speed) / dt)
                    : -Math.Min(c.Deceleration, (speed - desiredSpeed) / dt);
                if (Math.Abs(angle) > HardTurnAngle && along > 0f) along = 0f;

                // Whatever grip is left turns the player: lateral acceleration = v · ω.
                float lateral = (float)Math.Sqrt(Math.Max(0f, c.GripAcceleration * c.GripAcceleration - along * along));
                float maxTurn = Math.Min(c.TurnRateStill, lateral / speed) * dt;
                heading = heading.Rotate(Math.Max(-maxTurn, Math.Min(maxTurn, angle)));
                speed = Math.Max(0f, speed + along * dt);
            }

            p.Velocity = heading * speed;
            p.Facing = heading;
        }

        /// <summary>Forward acceleration available at a given speed (mono-exponential sprint model).</summary>
        public static float Propulsion(MatchConfig c, float speed) => Math.Max(0f, (c.SprintSpeed - speed) / c.SprintTau);

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
