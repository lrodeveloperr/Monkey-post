using System.Collections.Generic;
using MonkeyPost.Core;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Placeholder low-poly art built from primitives at runtime, until the CC0 asset packs are in.
    /// One flat-shaded material, tinted per colour.
    /// </summary>
    static class Art
    {
        static Material _baseMaterial;
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        public static readonly Color Sky = new Color(0.62f, 0.78f, 0.92f);
        public static readonly Color Turf = new Color(0.30f, 0.47f, 0.26f);
        public static readonly Color Dust = new Color(0.62f, 0.47f, 0.33f);
        public static readonly Color Concrete = new Color(0.66f, 0.65f, 0.61f);
        public static readonly Color Line = new Color(0.93f, 0.93f, 0.90f);
        public static readonly Color Shadow = new Color(0.12f, 0.16f, 0.10f);

        /// <summary>Converts a pitch position (metres) to the world: pitch X → X, pitch Y → Z, height → Y.</summary>
        public static Vector3 ToWorld(Vec2 p, float height = 0f) => new Vector3(p.X, height, p.Y);

        public static Material Mat(Color colour)
        {
            if (Materials.TryGetValue(colour, out var material)) return material;
            if (_baseMaterial == null)
            {
                _baseMaterial = Resources.Load<Material>("MonkeyPostFlat");
                if (_baseMaterial == null)
                {
                    var shader = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Unlit/Color");
                    _baseMaterial = new Material(shader);
                }
            }
            material = new Material(_baseMaterial) { color = colour };
            Materials[colour] = material;
            return material;
        }

        public static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Color colour)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat(colour);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return t;
        }

        public static Transform Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Color colour) =>
            Shape(PrimitiveType.Cube, name, parent, localPosition, scale, colour);

        public static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }
    }
}
