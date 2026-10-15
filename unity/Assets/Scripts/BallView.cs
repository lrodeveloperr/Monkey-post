using UnityEngine;

namespace MonkeyPost.Game
{
    sealed class BallView
    {
        // Drawn larger than a real ball so it reads clearly from the side-on camera.
        const float VisualDiameter = 0.38f;

        readonly Transform _ball;
        readonly Transform _shadow;

        public BallView(Transform parent)
        {
            _shadow = Art.Shape(PrimitiveType.Cylinder, "Ball shadow", parent, Vector3.zero, new Vector3(0.4f, 0.004f, 0.4f), Art.Shadow);
            _ball = Art.Shape(PrimitiveType.Sphere, "Ball", parent, Vector3.zero, Vector3.one * VisualDiameter, Color.white);
            Art.Shape(PrimitiveType.Cube, "Panel", _ball, Vector3.zero, new Vector3(1.02f, 0.25f, 0.25f), new Color(0.1f, 0.1f, 0.1f));
        }

        public void Show(Vector3 groundPosition, float height, Vector3 velocity, float dt)
        {
            _ball.localPosition = groundPosition + Vector3.up * (VisualDiameter / 2f + height);
            _shadow.localPosition = groundPosition + Vector3.up * 0.015f;
            float shadowScale = Mathf.Lerp(0.4f, 0.2f, Mathf.Clamp01(height / 2f));
            _shadow.localScale = new Vector3(shadowScale, 0.004f, shadowScale);

            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed > 0.05f)
            {
                var axis = Vector3.Cross(Vector3.up, flat / speed);
                _ball.Rotate(axis, speed * dt / (VisualDiameter / 2f) * Mathf.Rad2Deg, Space.World);
            }
        }
    }
}
