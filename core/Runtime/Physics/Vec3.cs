using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// A 3D vector for ball physics, right-handed: X along the pitch, Y across it, Z up.
    /// </summary>
    [Serializable]
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3(Vec2 xy, float z)
        {
            X = xy.X;
            Y = xy.Y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 0f, 1f);

        public Vec2 XY => new Vec2(X, Y);
        public float LengthSquared => X * X + Y * Y + Z * Z;
        public float Length => (float)Math.Sqrt(LengthSquared);

        public Vec3 Normalized
        {
            get
            {
                float length = Length;
                return length > 1e-6f ? this / length : Zero;
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public override string ToString() => $"({X:0.00}, {Y:0.00}, {Z:0.00})";
    }
}
