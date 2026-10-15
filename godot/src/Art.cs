using System.Collections.Generic;
using Godot;
using MonkeyPost.Core;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Placeholder low-poly art built from primitives, and the mapping from pitch coordinates to the
    /// Godot world.
    /// </summary>
    static class Art
    {
        public static readonly Color Sky = new Color(0.62f, 0.78f, 0.92f);
        public static readonly Color Turf = new Color(0.30f, 0.47f, 0.26f);
        public static readonly Color Dust = new Color(0.62f, 0.47f, 0.33f);
        public static readonly Color Line = new Color(0.93f, 0.93f, 0.90f);
        public static readonly Color Shadow = new Color(0.12f, 0.16f, 0.10f);

        static readonly Dictionary<Color, StandardMaterial3D> Materials = new Dictionary<Color, StandardMaterial3D>();
        static readonly BoxMesh UnitBox = new BoxMesh { Size = Vector3.One };
        static readonly CylinderMesh UnitCylinder = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 1 };
        static readonly SphereMesh UnitSphere = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 6 };

        /// <summary>
        /// Pitch (X along, Y across, Z up) to world (X, Y up, −Z). A proper rotation, so spin vectors
        /// map the same way. Camera looks along −Z, so "away from the camera" is pitch +Y.
        /// </summary>
        public static Vector3 ToWorld(Vec2 p, float height = 0f) => new Vector3(p.X, height, -p.Y);

        public static Vector3 ToWorld(Vec3 v) => new Vector3(v.X, v.Z, -v.Y);

        /// <summary>A world-space point on the pitch plane: pitch X, height, pitch Y.</summary>
        public static Vector3 P(float x, float height, float pitchY) => new Vector3(x, height, -pitchY);

        public static StandardMaterial3D Mat(Color colour)
        {
            if (Materials.TryGetValue(colour, out var material)) return material;
            material = new StandardMaterial3D
            {
                AlbedoColor = colour,
                Roughness = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
            };
            Materials[colour] = material;
            return material;
        }

        public static MeshInstance3D Shape(Mesh mesh, string name, Node3D parent, Vector3 position, Vector3 size, Color colour)
        {
            var instance = new MeshInstance3D
            {
                Name = name,
                Mesh = mesh,
                MaterialOverride = Mat(colour),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = position,
                Scale = size,
            };
            parent.AddChild(instance);
            return instance;
        }

        public static MeshInstance3D Box(string name, Node3D parent, Vector3 position, Vector3 size, Color colour) =>
            Shape(UnitBox, name, parent, position, size, colour);

        public static MeshInstance3D Cylinder(string name, Node3D parent, Vector3 position, Vector3 size, Color colour) =>
            Shape(UnitCylinder, name, parent, position, size, colour);

        public static MeshInstance3D Sphere(string name, Node3D parent, Vector3 position, Vector3 size, Color colour) =>
            Shape(UnitSphere, name, parent, position, size, colour);

        public static Node3D Pivot(string name, Node3D parent, Vector3 position)
        {
            var node = new Node3D { Name = name, Position = position };
            parent.AddChild(node);
            return node;
        }

        /// <summary>Yaw (about +Y) that turns a node's −Z forward to face the pitch direction <paramref name="facing"/>.</summary>
        public static float Yaw(Vector2 facing) => Mathf.Atan2(-facing.X, facing.Y);
    }
}
