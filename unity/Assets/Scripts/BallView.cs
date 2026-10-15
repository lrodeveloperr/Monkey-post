using MonkeyPost.Core;
using UnityEngine;

namespace MonkeyPost.Game
{
    sealed class BallView
    {
        // Drawn larger than a real ball so it reads clearly from the side-on camera.
        const float VisualDiameter = 0.38f;
        const float PhysicalRadius = 0.11f;

        readonly Transform _ball;
        readonly Transform _shadow;

        public BallView(Transform parent)
        {
            _shadow = Art.Shape(PrimitiveType.Cylinder, "Ball shadow", parent, Vector3.zero, new Vector3(0.4f, 0.004f, 0.4f), Art.Shadow);
            _ball = Art.Shape(PrimitiveType.Sphere, "Ball", parent, Vector3.zero, Vector3.one * VisualDiameter, Color.white);
            Art.Shape(PrimitiveType.Cube, "Panel", _ball, Vector3.zero, new Vector3(1.02f, 0.25f, 0.25f), new Color(0.1f, 0.1f, 0.1f));
        }

        /// <param name="spin">The ball's real angular velocity from the simulation (rad/s, pitch axes).</param>
        public void Show(Vector3 groundPosition, float height, Vec3 spin, float dt)
        {
            _ball.localPosition = groundPosition + Vector3.up * (VisualDiameter / 2f + height);
            _shadow.localPosition = groundPosition + Vector3.up * 0.015f;
            float shadowScale = Mathf.Lerp(0.4f, 0.2f, Mathf.Clamp01(height / 2f));
            _shadow.localScale = new Vector3(shadowScale, 0.004f, shadowScale);

            // Pitch axes (X along, Y across, Z up; right-handed) map to world (X, Z, Y). Swapping two
            // axes mirrors the space, which reverses the sense of rotation.
            float rate = spin.Length;
            if (rate > 0.01f)
            {
                var axis = new Vector3(spin.X, spin.Z, spin.Y) / rate;
                // The drawn ball is bigger than a real one; scale the spin so its roll still
                // matches its speed across the ground.
                float visualRate = rate * PhysicalRadius / (VisualDiameter / 2f);
                _ball.Rotate(axis, -visualRate * dt * Mathf.Rad2Deg, Space.World);
            }
        }
    }
}
