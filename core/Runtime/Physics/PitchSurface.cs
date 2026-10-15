using System;

namespace MonkeyPost.Core
{
    /// <summary>The six Nigerian pitches, each with its own ground.</summary>
    public enum PitchSurface
    {
        /// <summary>Artificial turf: true roll, medium bounce. The free starting pitch.</summary>
        LagosCage,
        /// <summary>Dusty earth: slow, bumpy, dead bounce.</summary>
        IbadanSchoolField,
        /// <summary>Asphalt court: quick, lively bounce.</summary>
        AbujaEstateCourt,
        /// <summary>Concrete: fastest roll, highest bounce.</summary>
        PortHarcourtWaterfront,
        /// <summary>Sand: the ball dies quickly and barely bounces.</summary>
        KanoSandCourt,
        /// <summary>Natural grass on a slope-side pitch: medium pace, a little uneven.</summary>
        EnuguHillside,
    }

    /// <summary>
    /// How a surface treats the ball. Calibrated against the FIFA Quality Programme test methods
    /// (see docs/PHYSICS.md):
    /// <list type="bullet">
    /// <item>Vertical rebound: ball dropped from 2.0 m, rebound height of its underside.
    /// FIFA turf: 0.60–0.85 m (Quality Pro). FIFA calibrates the ball to 1.35 m on concrete.</item>
    /// <item>Reduced ball roll: ball released from a 1 m ramp, distance rolled.
    /// FIFA turf: 4–8 m (Quality Pro), 4–10 m (Quality).</item>
    /// </list>
    /// </summary>
    public sealed class SurfaceProfile
    {
        /// <summary>Coefficient of restitution for a ground bounce at low impact speed (~6 m/s).</summary>
        public float Restitution;

        /// <summary>Friction while the ball skids along the ground (before it settles into a roll).</summary>
        public float SlidingFriction;

        /// <summary>
        /// Friction available during a bounce. Footballs grip during oblique bounces (measured
        /// friction-to-normal ratio about 1.0; Cross), so they usually leave rolling.
        /// </summary>
        public float BounceFriction;

        /// <summary>Constant part of rolling deceleration, m/s².</summary>
        public float RollingDeceleration;

        /// <summary>Speed-proportional part of rolling deceleration, 1/s (grass and infill drag).</summary>
        public float RollingDragPerSpeed;

        /// <summary>0..1: random deviation in rolls and bounces from an uneven surface.</summary>
        public float Bumpiness;

        /// <summary>Coefficient of restitution off the cage walls around this pitch, at low speed.</summary>
        public float WallRestitution;

        public static SurfaceProfile For(PitchSurface surface)
        {
            switch (surface)
            {
                case PitchSurface.LagosCage:
                    // FIFA-grade turf: rebound ~0.7 m, ramp roll ~6 m (mid FIFA Pro range). Concrete walls.
                    return new SurfaceProfile { Restitution = 0.6f, SlidingFriction = 0.45f, BounceFriction = 0.9f, RollingDeceleration = 0.72f, RollingDragPerSpeed = 0.06f, Bumpiness = 0f, WallRestitution = 0.8f };
                case PitchSurface.IbadanSchoolField:
                    // Dusty earth: dead and slow, below FIFA minimums (~3 m roll), bumpy.
                    return new SurfaceProfile { Restitution = 0.5f, SlidingFriction = 0.55f, BounceFriction = 1f, RollingDeceleration = 1.5f, RollingDragPerSpeed = 0.12f, Bumpiness = 0.35f, WallRestitution = 0.75f };
                case PitchSurface.AbujaEstateCourt:
                    // Asphalt: lively (rebound ~1.2 m) and quick (~11 m roll).
                    return new SurfaceProfile { Restitution = 0.8f, SlidingFriction = 0.5f, BounceFriction = 0.8f, RollingDeceleration = 0.42f, RollingDragPerSpeed = 0.02f, Bumpiness = 0.05f, WallRestitution = 0.8f };
                case PitchSurface.PortHarcourtWaterfront:
                    // Smooth concrete: FIFA's own ball calibration surface (1.35 m rebound), ~14 m roll.
                    return new SurfaceProfile { Restitution = 0.86f, SlidingFriction = 0.5f, BounceFriction = 0.8f, RollingDeceleration = 0.33f, RollingDragPerSpeed = 0.02f, Bumpiness = 0.1f, WallRestitution = 0.8f };
                case PitchSurface.KanoSandCourt:
                    // Sand: barely bounces (~0.2 m), dies within ~1.5 m.
                    return new SurfaceProfile { Restitution = 0.3f, SlidingFriction = 0.7f, BounceFriction = 1f, RollingDeceleration = 2.6f, RollingDragPerSpeed = 0.45f, Bumpiness = 0.25f, WallRestitution = 0.7f };
                case PitchSurface.EnuguHillside:
                    // Natural grass, slightly uneven: rebound ~0.65 m, ~5 m roll.
                    return new SurfaceProfile { Restitution = 0.58f, SlidingFriction = 0.45f, BounceFriction = 0.9f, RollingDeceleration = 0.9f, RollingDragPerSpeed = 0.1f, Bumpiness = 0.2f, WallRestitution = 0.75f };
                default:
                    throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }
    }
}
