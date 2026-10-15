using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>A chunky placeholder footballer with a simple run cycle.</summary>
    sealed class FootballerView
    {
        static readonly Color Marker = new Color(1f, 0.85f, 0.1f);

        readonly Transform _root;
        readonly Transform _body;
        readonly Transform _legLeft, _legRight, _armLeft, _armRight;
        readonly GameObject _marker;
        float _phase;
        float _yaw;

        public FootballerView(Transform parent, string name, Color kit, Color shorts, Color skin)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);

            Art.Shape(PrimitiveType.Cylinder, "Shadow", _root, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.005f, 0.8f), Art.Shadow);
            _marker = Art.Shape(PrimitiveType.Cylinder, "Control ring", _root, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.006f, 1.1f), Marker).gameObject;

            _body = Art.Pivot("Body", _root, Vector3.zero);
            _legLeft = Limb("Leg L", new Vector3(-0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            _legRight = Limb("Leg R", new Vector3(0.13f, 0.8f, 0f), new Vector3(0.18f, 0.8f, 0.2f), shorts);
            Art.Box("Shorts", _body, new Vector3(0f, 0.88f, 0f), new Vector3(0.48f, 0.28f, 0.3f), shorts);
            Art.Box("Shirt", _body, new Vector3(0f, 1.3f, 0f), new Vector3(0.56f, 0.62f, 0.32f), kit);
            _armLeft = Limb("Arm L", new Vector3(-0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            _armRight = Limb("Arm R", new Vector3(0.37f, 1.56f, 0f), new Vector3(0.15f, 0.56f, 0.15f), kit);
            Art.Shape(PrimitiveType.Sphere, "Head", _body, new Vector3(0f, 1.86f, 0f), new Vector3(0.4f, 0.42f, 0.4f), skin);
        }

        Transform Limb(string name, Vector3 joint, Vector3 size, Color colour)
        {
            var pivot = Art.Pivot(name, _body, joint);
            Art.Box(name, pivot, new Vector3(0f, -size.y / 2f, 0f), size, colour);
            return pivot;
        }

        public void Show(Vector3 position, Vector2 facing, float speed, bool controlled, bool stunned, float dt)
        {
            _root.localPosition = position;
            if (facing.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, target, 720f * dt);
                _root.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }

            float stride = Mathf.Clamp01(speed / 5f);
            _phase += dt * (4f + speed * 1.6f);
            float swing = Mathf.Sin(_phase) * 40f * stride;
            _legLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            _armLeft.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
            _armRight.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * stride;
            _body.localPosition = new Vector3(0f, bob, 0f);
            _body.localRotation = stunned ? Quaternion.Euler(25f, 0f, 0f) : Quaternion.Euler(8f * stride, 0f, 0f);

            _marker.SetActive(controlled);
        }
    }
}
