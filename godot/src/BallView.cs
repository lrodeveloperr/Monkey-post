using Godot;
using MonkeyPost.Core;

namespace MonkeyPost.Game
{
    sealed class BallView
    {
        // Drawn larger than a real ball so it reads clearly from the side-on camera.
        const float VisualDiameter = 0.38f;
        const float PhysicalRadius = 0.11f;

        readonly MeshInstance3D _ball;
        readonly MeshInstance3D _shadow;

        public BallView(Node3D parent)
        {
            _shadow = Art.Cylinder("Ball shadow", parent, Vector3.Zero, new Vector3(0.4f, 0.004f, 0.4f), Art.Shadow);
            _ball = Art.Sphere("Ball", parent, Vector3.Zero, Vector3.One * VisualDiameter, Colors.White);
            Art.Box("Panel", _ball, Vector3.Zero, new Vector3(1.02f, 0.25f, 0.25f), new Color(0.1f, 0.1f, 0.1f));
        }

        /// <param name="spin">The ball's real angular velocity from the simulation (rad/s, pitch axes).</param>
        public void Show(Vector3 groundPosition, float height, Vec3 spin, float dt)
        {
            _ball.Position = groundPosition + Vector3.Up * (VisualDiameter / 2f + height);
            _shadow.Position = groundPosition + Vector3.Up * 0.015f;
            float shadowScale = Mathf.Lerp(0.4f, 0.2f, Mathf.Clamp(height / 2f, 0f, 1f));
            _shadow.Scale = new Vector3(shadowScale, 0.004f, shadowScale);

            // Pitch→world is a proper rotation, so the spin axis maps like any vector.
            var omega = Art.ToWorld(spin);
            float rate = omega.Length();
            if (rate > 0.01f && dt > 0f)
            {
                // The drawn ball is bigger than a real one; scale the spin so its roll still matches
                // its speed across the ground.
                float visualRate = rate * PhysicalRadius / (VisualDiameter / 2f);
                _ball.GlobalRotate(omega / rate, visualRate * dt);
            }
        }
    }
}
