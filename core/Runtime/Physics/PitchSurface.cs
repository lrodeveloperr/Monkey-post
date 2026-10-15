using System;

namespace MonkeyPost.Core
{
    /// <summary>The six Nigerian pitches, each with its own ground.</summary>
    public enum PitchSurface
    {
        /// <summary>Artificial turf: fast, true roll, medium bounce. The free starting pitch.</summary>
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

    /// <summary>How a surface treats the ball.</summary>
    public sealed class SurfaceProfile
    {
        /// <summary>Fraction of vertical speed kept on a bounce.</summary>
        public float Restitution;

        /// <summary>Friction while the ball skids (before it settles into a roll), and during bounces.</summary>
        public float SlidingFriction;

        /// <summary>Deceleration of a rolling ball, m/s².</summary>
        public float RollingDeceleration;

        /// <summary>0..1: random deviation in rolls and bounces from an uneven surface.</summary>
        public float Bumpiness;

        /// <summary>Fraction of speed kept off the walls around this pitch.</summary>
        public float WallRestitution;

        public static SurfaceProfile For(PitchSurface surface)
        {
            switch (surface)
            {
                case PitchSurface.LagosCage:
                    return new SurfaceProfile { Restitution = 0.6f, SlidingFriction = 0.45f, RollingDeceleration = 1.2f, Bumpiness = 0f, WallRestitution = 0.65f };
                case PitchSurface.IbadanSchoolField:
                    return new SurfaceProfile { Restitution = 0.5f, SlidingFriction = 0.55f, RollingDeceleration = 2.1f, Bumpiness = 0.35f, WallRestitution = 0.55f };
                case PitchSurface.AbujaEstateCourt:
                    return new SurfaceProfile { Restitution = 0.72f, SlidingFriction = 0.5f, RollingDeceleration = 0.85f, Bumpiness = 0.05f, WallRestitution = 0.65f };
                case PitchSurface.PortHarcourtWaterfront:
                    return new SurfaceProfile { Restitution = 0.76f, SlidingFriction = 0.5f, RollingDeceleration = 0.7f, Bumpiness = 0.1f, WallRestitution = 0.7f };
                case PitchSurface.KanoSandCourt:
                    return new SurfaceProfile { Restitution = 0.3f, SlidingFriction = 0.7f, RollingDeceleration = 3.4f, Bumpiness = 0.25f, WallRestitution = 0.5f };
                case PitchSurface.EnuguHillside:
                    return new SurfaceProfile { Restitution = 0.55f, SlidingFriction = 0.45f, RollingDeceleration = 1.6f, Bumpiness = 0.2f, WallRestitution = 0.6f };
                default:
                    throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }
    }
}
