using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Geometry of the ring. Angles are in laps, in [0, 1), increasing anticlockwise,
    /// which is the direction everyone runs.
    /// </summary>
    public static class Ring
    {
        public static float Wrap(float angle)
        {
            angle %= 1f;
            if (angle < 0f) angle += 1f;
            // Guard against -epsilon % 1 + 1 rounding up to exactly 1.
            return angle >= 1f ? 0f : angle;
        }

        /// <summary>Distance travelling anticlockwise from <paramref name="from"/> to <paramref name="to"/>, in [0, 1).</summary>
        public static float ForwardDistance(float from, float to) => Wrap(to - from);

        /// <summary>Shortest signed offset of <paramref name="angle"/> relative to <paramref name="reference"/>, in [-0.5, 0.5).</summary>
        public static float SignedOffset(float reference, float angle) => Wrap(angle - reference + 0.5f) - 0.5f;

        public static float SeatAngle(int seat, int seatCount) => (float)seat / seatCount;

        public static int NearestSeat(float angle, int seatCount) =>
            (int)Math.Round(Wrap(angle) * seatCount, MidpointRounding.AwayFromZero) % seatCount;

        /// <summary>The seat at or most recently passed when moving anticlockwise to <paramref name="angle"/>.</summary>
        public static int SeatJustPassed(float angle, int seatCount) =>
            (int)Math.Floor(Wrap(angle) * seatCount) % seatCount;
    }
}
