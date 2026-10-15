using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>Shared IMGUI drawing helpers. Positions here use GUI coordinates (origin top-left).</summary>
    static class GuiKit
    {
        static Texture2D _disc;
        static Texture2D _white;
        static GUIStyle _label;
        static GUIStyle _button;
        static int _styledForHeight;

        /// <summary>Base unit: the short side of the screen, so layout scales on any phone.</summary>
        public static float Unit => Mathf.Min(Screen.width, Screen.height);

        public static Texture2D Disc
        {
            get
            {
                if (_disc != null) return _disc;
                const int size = 128;
                _disc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    _disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
                _disc.Apply();
                return _disc;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                return _white;
            }
        }

        static void EnsureStyles()
        {
            if (_label != null && _styledForHeight == Screen.height) return;
            _styledForHeight = Screen.height;
            _label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(Unit * 0.05f),
                wordWrap = false,
            };
            _label.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(Unit * 0.05f),
            };
        }

        public static void Circle(Vector2 centre, float radius, Color colour)
        {
            var old = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), Disc);
            GUI.color = old;
        }

        public static void Rect(Rect rect, Color colour)
        {
            var old = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, White);
            GUI.color = old;
        }

        public static void Label(Rect rect, string text, float scale = 1f, Color? colour = null)
        {
            EnsureStyles();
            int baseSize = _label.fontSize;
            _label.fontSize = Mathf.RoundToInt(baseSize * scale);
            // Drop shadow for readability over the pitch.
            var shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
            _label.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(shadowRect, text, _label);
            _label.normal.textColor = colour ?? Color.white;
            GUI.Label(rect, text, _label);
            _label.fontSize = baseSize;
        }

        public static bool Button(Rect rect, string text)
        {
            EnsureStyles();
            return GUI.Button(rect, text, _button);
        }

        /// <summary>Converts a screen position (origin bottom-left) to GUI space (origin top-left).</summary>
        public static Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);
    }
}
