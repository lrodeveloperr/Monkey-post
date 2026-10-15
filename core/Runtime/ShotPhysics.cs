using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Predicts shot flight with the same ball physics as <see cref="MatchSimulation"/>, so the
    /// power bar can show how hard you can hit it before it flies over knee height.
    /// </summary>
    public static class ShotPhysics
    {
        // Matches the simulation's ball substep (30 Hz tick, 3 substeps).
        const float Step = 1f / 90f;
        const int MaxSteps = 2000;

        /// <summary>
        /// Height of a shot of <paramref name="power"/> once it has travelled
        /// <paramref name="distance"/> metres, ignoring walls and players. Returns 0 if the ball
        /// stops rolling before it gets there.
        /// </summary>
        public static float HeightAfter(MatchConfig config, float power, float distance)
        {
            power = Math.Max(0f, Math.Min(1f, power));
            float speed = config.ShotMinSpeed + (config.ShotMaxSpeed - config.ShotMinSpeed) * power;
            float verticalVelocity = config.ShotMaxLift * power * power;
            float height = 0f;
            float travelled = 0f;

            for (int i = 0; i < MaxSteps && travelled < distance; i++)
            {
                if (height > 0f || verticalVelocity > 0f)
                {
                    verticalVelocity -= config.Gravity * Step;
                    height += verticalVelocity * Step;
                    if (height <= 0f)
                    {
                        height = 0f;
                        verticalVelocity = verticalVelocity < -1f ? -verticalVelocity * config.GroundRestitution : 0f;
                    }
                }

                float drag = height <= 0f ? config.GroundFriction : config.AirDrag;
                speed = Math.Max(0f, speed - drag * Step);
                if (speed <= 0f) return 0f;
                travelled += speed * Step;
            }
            return height;
        }

        /// <summary>The highest power (in steps of 0.01) that still crosses the goal line under knee height.</summary>
        public static float MaxSafePower(MatchConfig config, float distanceToGoalLine)
        {
            float safe = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float power = i / 100f;
                if (HeightAfter(config, power, distanceToGoalLine) > config.GoalHeight) break;
                safe = power;
            }
            return safe;
        }
    }
}
