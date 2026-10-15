using MonkeyPost.Core;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Virtual joystick (left thumb) and three buttons (right thumb), context-sensitive:
    /// A = Pass / Switch, B = Shoot (hold to charge) / Tackle, plus Sprint.
    /// Keyboard works too, for testing in the editor: WASD/arrows, Shift, Space (A), K (B).
    /// </summary>
    sealed class TouchControls
    {
        const float ChargeSeconds = 1.1f;
        const int MouseId = -100;

        int _joyId = int.MinValue;
        Vector2 _joyOrigin, _joyPosition;
        int _shootId = int.MinValue;
        int _sprintId = int.MinValue;
        bool _keyboardCharging;

        bool _aPressed, _tacklePressed, _shootReleased;

        public float Charge { get; private set; }
        public bool Charging => _shootId != int.MinValue || _keyboardCharging;

        // Layout, in screen coordinates (origin bottom-left).
        static float U => GuiKit.Unit;
        static float JoyRadius => 0.13f * U;
        static Vector2 ButtonA => new Vector2(Screen.width - 0.37f * U, 0.15f * U);
        static float RadiusA => 0.085f * U;
        static Vector2 ButtonB => new Vector2(Screen.width - 0.16f * U, 0.25f * U);
        static float RadiusB => 0.11f * U;
        static Vector2 ButtonSprint => new Vector2(Screen.width - 0.17f * U, 0.52f * U);
        static float RadiusSprint => 0.075f * U;

        public void Reset()
        {
            _joyId = _shootId = _sprintId = int.MinValue;
            _keyboardCharging = false;
            Charge = 0f;
        }

        /// <summary>Reads touches and keys and returns this frame's input for the controlled footballer.</summary>
        public PlayerInput Read(bool hasBall, float dt)
        {
            _aPressed = _tacklePressed = _shootReleased = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                HandlePointer(touch.fingerId, touch.position, touch.phase, hasBall);
            }
            if (Input.touchCount == 0)
            {
                if (Input.GetMouseButtonDown(0)) HandlePointer(MouseId, Input.mousePosition, TouchPhase.Began, hasBall);
                else if (Input.GetMouseButton(0)) HandlePointer(MouseId, Input.mousePosition, TouchPhase.Moved, hasBall);
                else if (Input.GetMouseButtonUp(0)) HandlePointer(MouseId, Input.mousePosition, TouchPhase.Ended, hasBall);
            }

            var move = Vector2.zero;
            if (_joyId != int.MinValue) move = Vector2.ClampMagnitude((_joyPosition - _joyOrigin) / JoyRadius, 1f);
            move += KeyboardMove();
            move = Vector2.ClampMagnitude(move, 1f);

            bool sprint = _sprintId != int.MinValue || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.Space)) _aPressed = true;
            if (Input.GetKeyDown(KeyCode.K))
            {
                if (hasBall) { _keyboardCharging = true; Charge = 0f; }
                else _tacklePressed = true;
            }
            if (Input.GetKeyUp(KeyCode.K) && _keyboardCharging)
            {
                _keyboardCharging = false;
                _shootReleased = true;
            }

            if (Charging) Charge = Mathf.Min(1f, Charge + dt / ChargeSeconds);

            var input = new PlayerInput
            {
                Move = new Vec2(move.x, move.y),
                Sprint = sprint,
                Pass = _aPressed && hasBall,
                Switch = _aPressed && !hasBall,
                Tackle = _tacklePressed && !hasBall,
                Shoot = _shootReleased && hasBall,
                Power = Charge,
                // Joystick up aims at the far post, down at the near post.
                Aim = move.y,
            };
            if (_shootReleased) Charge = 0f;
            return input;
        }

        void HandlePointer(int id, Vector2 position, TouchPhase phase, bool hasBall)
        {
            switch (phase)
            {
                case TouchPhase.Began:
                    if (Within(position, ButtonA, RadiusA * 1.2f)) _aPressed = true;
                    else if (Within(position, ButtonB, RadiusB * 1.2f))
                    {
                        if (hasBall)
                        {
                            _shootId = id;
                            Charge = 0f;
                        }
                        else _tacklePressed = true;
                    }
                    else if (Within(position, ButtonSprint, RadiusSprint * 1.3f)) _sprintId = id;
                    else if (position.x < Screen.width * 0.5f && _joyId == int.MinValue)
                    {
                        _joyId = id;
                        _joyOrigin = _joyPosition = position;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (id == _joyId) _joyPosition = position;
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (id == _joyId) _joyId = int.MinValue;
                    if (id == _sprintId) _sprintId = int.MinValue;
                    if (id == _shootId)
                    {
                        _shootId = int.MinValue;
                        _shootReleased = true;
                    }
                    break;
            }
        }

        static bool Within(Vector2 p, Vector2 centre, float radius) => (p - centre).sqrMagnitude <= radius * radius;

        static Vector2 KeyboardMove()
        {
            var v = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
            return v;
        }

        /// <summary>Draws the controls. Call from OnGUI.</summary>
        public void Draw(bool hasBall, float safePower)
        {
            var faint = new Color(1f, 1f, 1f, 0.18f);
            var strong = new Color(1f, 1f, 1f, 0.45f);

            // Joystick: shows where it was grabbed, or a hint in the corner.
            var joyCentre = _joyId != int.MinValue ? _joyOrigin : new Vector2(0.2f * U, 0.22f * U);
            GuiKit.Circle(GuiKit.ToGui(joyCentre), JoyRadius, faint);
            var knob = _joyId != int.MinValue
                ? joyCentre + Vector2.ClampMagnitude(_joyPosition - _joyOrigin, JoyRadius)
                : joyCentre;
            GuiKit.Circle(GuiKit.ToGui(knob), JoyRadius * 0.45f, strong);

            DrawButton(ButtonA, RadiusA, hasBall ? "PASS" : "SWITCH", new Color(0.2f, 0.6f, 1f, 0.55f));
            DrawButton(ButtonB, RadiusB, hasBall ? "SHOOT" : "TACKLE",
                hasBall ? new Color(1f, 0.35f, 0.25f, 0.6f) : new Color(1f, 0.7f, 0.2f, 0.55f));
            DrawButton(ButtonSprint, RadiusSprint, "SPRINT",
                new Color(0.3f, 0.9f, 0.4f, _sprintId != int.MinValue ? 0.75f : 0.45f));

            if (Charging) DrawPowerBar(safePower);
        }

        static void DrawButton(Vector2 centre, float radius, string label, Color colour)
        {
            var gui = GuiKit.ToGui(centre);
            GuiKit.Circle(gui, radius, colour);
            GuiKit.Label(new Rect(gui.x - radius, gui.y - radius, radius * 2f, radius * 2f), label, 0.55f);
        }

        void DrawPowerBar(float safePower)
        {
            var b = GuiKit.ToGui(ButtonB);
            float height = 0.32f * U, width = 0.035f * U;
            var frame = new Rect(b.x - RadiusB - 0.07f * U, b.y - height, width, height);
            GuiKit.Rect(frame, new Color(0f, 0f, 0f, 0.5f));

            // Green up to the knee-height limit for this distance, red above it.
            float safeHeight = height * safePower;
            GuiKit.Rect(new Rect(frame.x, frame.yMax - safeHeight, width, safeHeight), new Color(0.2f, 0.8f, 0.3f, 0.35f));
            GuiKit.Rect(new Rect(frame.x, frame.y, width, height - safeHeight), new Color(0.9f, 0.2f, 0.2f, 0.35f));

            float fill = height * Charge;
            var fillColour = Charge <= safePower ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f);
            GuiKit.Rect(new Rect(frame.x, frame.yMax - fill, width, fill), fillColour);
        }
    }
}
