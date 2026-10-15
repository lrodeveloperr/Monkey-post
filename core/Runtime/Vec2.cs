using System;

namespace MonkeyPost.Core
{
    /// <summary>A 2D vector on the pitch, in metres. X runs along the pitch, Y across it.</summary>
    [Serializable]
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSquared);

        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        /// <summary>This vector rotated 90 degrees anticlockwise.</summary>
        public Vec2 Perpendicular => new Vec2(-Y, X);

        public Vec2 ClampLength(float max)
        {
            float lengthSquared = LengthSquared;
            if (lengthSquared <= max * max) return this;
            return this * (max / (float)Math.Sqrt(lengthSquared));
        }

        public Vec2 Rotate(float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDelta)
        {
            var delta = target - current;
            float distance = delta.Length;
            return distance <= maxDelta || distance < 1e-6f ? target : current + delta * (maxDelta / distance);
        }

        /// <summary>Shortest distance from <paramref name="point"/> to the segment <paramref name="a"/>–<paramref name="b"/>.</summary>
        public static float DistanceToSegment(Vec2 point, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            float lengthSquared = ab.LengthSquared;
            if (lengthSquared < 1e-9f) return Distance(point, a);
            float t = Math.Max(0f, Math.Min(1f, Dot(point - a, ab) / lengthSquared));
            return Distance(point, a + ab * t);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }
}
