using System.Collections.Generic;
using Godot;
using MonkeyPost.Core;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Virtual joystick (left thumb) and three buttons (right thumb), context-sensitive:
    /// A = Pass / Switch (double-tap for a through ball), B = Shoot (hold to charge) / Tackle,
    /// plus Sprint. Swipe anywhere else on the right half for skill moves: forward = nutmeg,
    /// back = drag-back, sideways = step-over.
    /// Keyboard for desktop testing: WASD/arrows, Shift, Space (A), K (B), 1–4 skills.
    /// Screen right/up map to pitch +X/+Y.
    /// </summary>
    public partial class TouchPad : Control
    {
        const float ChargeSeconds = 1.1f;
        const float DoubleTapSeconds = 0.18f;
        const float SwipeMaxSeconds = 0.4f;
        const int None = int.MinValue;

        int _joyId = None, _shootId = None, _sprintId = None, _swipeId = None;
        Vector2 _joyOrigin, _joyPosition, _swipeStart;
        double _swipeStartTime;
        bool _keyCharging;
        bool _aPressed, _tacklePressed, _shootReleased;
        float _passWaiting = -1f;
        Vector2 _swipe;
        readonly HashSet<Key> _keysDown = new HashSet<Key>();
        readonly HashSet<Key> _keysPressed = new HashSet<Key>();
        readonly HashSet<Key> _keysReleased = new HashSet<Key>();

        public bool HasBall;
        public float SafePower = 1f;
        public float Charge { get; private set; }
        public bool Charging => _shootId != None || _keyCharging;

        Vector2 Screen => GetViewportRect().Size;
        float U => Mathf.Min(Screen.X, Screen.Y);
        float JoyRadius => 0.13f * U;
        Vector2 ButtonA => new Vector2(Screen.X - 0.37f * U, Screen.Y - 0.15f * U);
        float RadiusA => 0.085f * U;
        Vector2 ButtonB => new Vector2(Screen.X - 0.16f * U, Screen.Y - 0.25f * U);
        float RadiusB => 0.11f * U;
        Vector2 ButtonSprint => new Vector2(Screen.X - 0.17f * U, Screen.Y - 0.52f * U);
        float RadiusSprint => 0.075f * U;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void ResetState()
        {
            _joyId = _shootId = _sprintId = _swipeId = None;
            _keyCharging = false;
            _passWaiting = -1f;
            Charge = 0f;
            _keysDown.Clear();
        }

        public override void _Input(InputEvent e)
        {
            if (!Visible) return;
            switch (e)
            {
                case InputEventScreenTouch touch when touch.Pressed:
                    Began(touch.Index, touch.Position);
                    break;
                case InputEventScreenTouch touch:
                    Ended(touch.Index, touch.Position);
                    break;
                case InputEventScreenDrag drag:
                    if (drag.Index == _joyId) _joyPosition = drag.Position;
                    break;
                case InputEventKey key when !key.Echo:
                    if (key.Pressed) { _keysDown.Add(key.PhysicalKeycode); _keysPressed.Add(key.PhysicalKeycode); }
                    else { _keysDown.Remove(key.PhysicalKeycode); _keysReleased.Add(key.PhysicalKeycode); }
                    break;
            }
        }

        void Began(int id, Vector2 p)
        {
            if (Within(p, ButtonA, RadiusA * 1.2f)) _aPressed = true;
            else if (Within(p, ButtonB, RadiusB * 1.2f))
            {
                if (HasBall)
                {
                    _shootId = id;
                    Charge = 0f;
                }
                else _tacklePressed = true;
            }
            else if (Within(p, ButtonSprint, RadiusSprint * 1.3f)) _sprintId = id;
            else if (p.X < Screen.X * 0.5f && _joyId == None)
            {
                _joyId = id;
                _joyOrigin = _joyPosition = p;
            }
            else if (p.X >= Screen.X * 0.5f && _swipeId == None)
            {
                _swipeId = id;
                _swipeStart = p;
                _swipeStartTime = Time.GetTicksMsec() / 1000.0;
            }
        }

        void Ended(int id, Vector2 p)
        {
            if (id == _joyId) _joyId = None;
            if (id == _sprintId) _sprintId = None;
            if (id == _shootId)
            {
                _shootId = None;
                _shootReleased = true;
            }
            if (id == _swipeId)
            {
                _swipeId = None;
                var flick = p - _swipeStart;
                double held = Time.GetTicksMsec() / 1000.0 - _swipeStartTime;
                // Screen y grows downwards; pitch +Y is up the screen.
                if (flick.Length() > 0.08f * U && held < SwipeMaxSeconds)
                    _swipe = new Vector2(flick.X, -flick.Y).Normalized();
            }
        }

        static bool Within(Vector2 p, Vector2 centre, float radius) => (p - centre).LengthSquared() <= radius * radius;

        bool Key(Key k) => _keysDown.Contains(k);

        /// <summary>This frame's input for the controlled footballer. Call once per frame.</summary>
        public PlayerInput Read(float dt)
        {
            var move = Vector2.Zero;
            if (_joyId != None)
            {
                var d = (_joyPosition - _joyOrigin) / JoyRadius;
                move = new Vector2(d.X, -d.Y).LimitLength(1f);
            }
            if (Key(Godot.Key.A) || Key(Godot.Key.Left)) move.X -= 1f;
            if (Key(Godot.Key.D) || Key(Godot.Key.Right)) move.X += 1f;
            if (Key(Godot.Key.S) || Key(Godot.Key.Down)) move.Y -= 1f;
            if (Key(Godot.Key.W) || Key(Godot.Key.Up)) move.Y += 1f;
            move = move.LimitLength(1f);

            bool sprint = _sprintId != None || Key(Godot.Key.Shift);
            if (_keysPressed.Contains(Godot.Key.Space)) _aPressed = true;
            if (_keysPressed.Contains(Godot.Key.K))
            {
                if (HasBall) { _keyCharging = true; Charge = 0f; }
                else _tacklePressed = true;
            }
            if (_keysReleased.Contains(Godot.Key.K) && _keyCharging)
            {
                _keyCharging = false;
                _shootReleased = true;
            }
            var heading = move.LengthSquared() > 0.01f ? move.Normalized() : Vector2.Right;
            if (_keysPressed.Contains(Godot.Key.Key1)) _swipe = heading;
            if (_keysPressed.Contains(Godot.Key.Key2)) _swipe = -heading;
            if (_keysPressed.Contains(Godot.Key.Key3)) _swipe = new Vector2(-heading.Y, heading.X);
            if (_keysPressed.Contains(Godot.Key.Key4)) _swipe = new Vector2(heading.Y, -heading.X);

            if (Charging) Charge = Mathf.Min(1f, Charge + dt / ChargeSeconds);

            // A tap on PASS waits briefly for a second tap: double-tap = through ball.
            bool pass = false, through = false;
            if (_aPressed && HasBall)
            {
                if (_passWaiting >= 0f)
                {
                    through = true;
                    _passWaiting = -1f;
                }
                else _passWaiting = DoubleTapSeconds;
            }
            if (_passWaiting >= 0f)
            {
                _passWaiting -= dt;
                if (_passWaiting < 0f) pass = true;
            }

            var input = new PlayerInput
            {
                Move = new Vec2(move.X, move.Y),
                Sprint = sprint,
                Pass = pass || through,
                ThroughBall = through,
                Switch = _aPressed && !HasBall,
                Tackle = _tacklePressed && !HasBall,
                Shoot = _shootReleased && HasBall,
                Power = Charge,
                // Joystick up aims at the far post, down at the near post.
                Aim = move.Y,
                Skill = new Vec2(_swipe.X, _swipe.Y),
            };

            if (_shootReleased) Charge = 0f;
            _aPressed = _tacklePressed = _shootReleased = false;
            _swipe = Vector2.Zero;
            _keysPressed.Clear();
            _keysReleased.Clear();
            QueueRedraw();
            return input;
        }

        public override void _Draw()
        {
            var faint = new Color(1f, 1f, 1f, 0.18f);
            var strong = new Color(1f, 1f, 1f, 0.45f);
            var font = ThemeDB.FallbackFont;

            var joyCentre = _joyId != None ? _joyOrigin : new Vector2(0.2f * U, Screen.Y - 0.22f * U);
            DrawCircle(joyCentre, JoyRadius, faint);
            var knob = _joyId != None ? joyCentre + (_joyPosition - _joyOrigin).LimitLength(JoyRadius) : joyCentre;
            DrawCircle(knob, JoyRadius * 0.45f, strong);

            DrawButton(font, ButtonA, RadiusA, HasBall ? "PASS" : "SWITCH", new Color(0.2f, 0.6f, 1f, 0.55f));
            DrawButton(font, ButtonB, RadiusB, HasBall ? "SHOOT" : "TACKLE",
                HasBall ? new Color(1f, 0.35f, 0.25f, 0.6f) : new Color(1f, 0.7f, 0.2f, 0.55f));
            DrawButton(font, ButtonSprint, RadiusSprint, "SPRINT", new Color(0.3f, 0.9f, 0.4f, _sprintId != None ? 0.75f : 0.45f));

            if (Charging) DrawPowerBar();
            if (HasBall)
            {
                DrawString(font, new Vector2(Screen.X * 0.55f, Screen.Y * 0.32f), "SWIPE HERE: SKILLS",
                    HorizontalAlignment.Center, Screen.X * 0.4f, (int)(0.03f * U), new Color(1f, 1f, 1f, 0.35f));
            }
        }

        void DrawButton(Font font, Vector2 centre, float radius, string label, Color colour)
        {
            DrawCircle(centre, radius, colour);
            int size = (int)(radius * 0.38f);
            DrawString(font, centre + new Vector2(-radius, size * 0.35f), label, HorizontalAlignment.Center, radius * 2f, size, Colors.White);
        }

        void DrawPowerBar()
        {
            float height = 0.32f * U, width = 0.035f * U;
            var frame = new Rect2(ButtonB.X - RadiusB - 0.07f * U, ButtonB.Y - height, width, height);
            DrawRect(frame, new Color(0f, 0f, 0f, 0.5f));

            // Green up to the knee-height limit for this distance, red above it.
            float safeHeight = height * SafePower;
            DrawRect(new Rect2(frame.Position.X, frame.End.Y - safeHeight, width, safeHeight), new Color(0.2f, 0.8f, 0.3f, 0.35f));
            DrawRect(new Rect2(frame.Position.X, frame.Position.Y, width, height - safeHeight), new Color(0.9f, 0.2f, 0.2f, 0.35f));

            float fill = height * Charge;
            var colour = Charge <= SafePower ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f);
            DrawRect(new Rect2(frame.Position.X, frame.End.Y - fill, width, fill), colour);
        }
    }
}
