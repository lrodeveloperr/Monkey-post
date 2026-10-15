using System;
using Godot;
using MonkeyPost.Core;
using MonkeyPost.Core.Bots;

namespace MonkeyPost.Game
{
    /// <summary>Score bar, clock, banners, and the menu / pause / full-time screens.</summary>
    public partial class Hud : Control
    {
        static readonly Color Gold = new Color(1f, 0.85f, 0.1f);

        Control _screen;
        bool _showMatch;
        int _home, _away;
        string _opponent = "", _clock = "", _banner = "";
        Color _bannerColour = Colors.White;
        float _bannerScale = 1f;

        Vector2 Size2 => GetViewportRect().Size;
        float U => Mathf.Min(Size2.X, Size2.Y);

        internal void Init(GameRoot game)
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        internal void Refresh(MatchPresenter presenter, bool showMatch)
        {
            _showMatch = showMatch && presenter.Simulation != null;
            if (_showMatch)
            {
                var sim = presenter.Simulation;
                _home = sim.Score(0);
                _away = sim.Score(1);
                _opponent = presenter.OpponentName.ToUpperInvariant();
                int seconds = Mathf.CeilToInt(sim.TimeRemaining);
                _clock = sim.IsGoldenGoal ? "GOLDEN GOAL" : $"{seconds / 60}:{seconds % 60:00}";

                _banner = "";
                if (presenter.GoalBanner > 0f)
                {
                    bool ours = presenter.LastGoalTeam == MatchPresenter.HumanTeam;
                    _banner = ours ? "GOAL!" : "THEY SCORE";
                    _bannerColour = ours ? Gold : new Color(1f, 0.4f, 0.3f);
                    _bannerScale = 2.4f;
                }
                else if (presenter.SkillBannerTime > 0f && presenter.SkillBanner != null)
                {
                    _banner = presenter.SkillBanner;
                    _bannerColour = new Color(0.4f, 1f, 0.5f);
                    _bannerScale = 1.5f;
                }
                else if (sim.Phase == Phase.Kickoff)
                {
                    _banner = "KICK-OFF";
                    _bannerColour = Colors.White;
                    _bannerScale = 1.3f;
                }
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (!_showMatch) return;
            var font = ThemeDB.FallbackFont;
            int size = (int)(0.05f * U);
            var bar = new Rect2(Size2.X / 2f - 0.75f * U, 0.02f * U, 1.5f * U, 0.11f * U);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.45f));
            float baseline = bar.Position.Y + bar.Size.Y * 0.68f;
            DrawString(font, new Vector2(bar.Position.X, baseline), "YOU", HorizontalAlignment.Center, bar.Size.X * 0.4f, (int)(size * 0.75f));
            DrawString(font, new Vector2(bar.Position.X + bar.Size.X * 0.4f, baseline), $"{_home} - {_away}", HorizontalAlignment.Center, bar.Size.X * 0.2f, (int)(size * 1.1f));
            DrawString(font, new Vector2(bar.Position.X + bar.Size.X * 0.6f, baseline), _opponent, HorizontalAlignment.Center, bar.Size.X * 0.4f, (int)(size * 0.6f));
            DrawString(font, new Vector2(bar.Position.X, bar.End.Y + size * 0.85f), _clock, HorizontalAlignment.Center, bar.Size.X, (int)(size * 0.7f));

            if (_banner.Length > 0)
            {
                int bannerSize = (int)(size * _bannerScale);
                var at = new Vector2(0f, Size2.Y * 0.33f);
                DrawString(font, at + new Vector2(3f, 3f), _banner, HorizontalAlignment.Center, Size2.X, bannerSize, new Color(0f, 0f, 0f, 0.6f));
                DrawString(font, at, _banner, HorizontalAlignment.Center, Size2.X, bannerSize, _bannerColour);
            }
        }

        // ---- Screens (buttons are real Controls, so touch works natively) -----------------------

        void ClearScreen()
        {
            _screen?.QueueFree();
            _screen = new Control { MouseFilter = MouseFilterEnum.Ignore };
            _screen.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_screen);
        }

        void Dim(float alpha)
        {
            var dim = new ColorRect { Color = new Color(0f, 0f, 0f, alpha), MouseFilter = MouseFilterEnum.Ignore };
            dim.SetAnchorsPreset(LayoutPreset.FullRect);
            _screen.AddChild(dim);
        }

        Label Text(string text, float y, float scale, Color colour)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(0f, y),
                Size = new Vector2(Size2.X, 0.2f * U),
                Modulate = colour,
            };
            label.AddThemeFontSizeOverride("font_size", (int)(0.05f * U * scale));
            label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.6f));
            _screen.AddChild(label);
            return label;
        }

        void ButtonAt(string text, Vector2 position, Vector2 size, Action onPressed)
        {
            var button = new Button { Text = text, Position = position, Size = size };
            button.AddThemeFontSizeOverride("font_size", (int)(0.05f * U));
            button.Pressed += onPressed;
            _screen.AddChild(button);
        }

        internal void ShowMenu(Action<BotDifficulty> start)
        {
            ClearScreen();
            Dim(0.35f);
            float h = Size2.Y;
            Text("MONKEY POST", h * 0.1f, 2.2f, Gold);
            Text("Street Football", h * 0.1f + 0.17f * U, 1.1f, Colors.White);
            Text("Quick match: choose your opponents", h * 0.42f, 0.8f, Colors.White);

            float bw = 0.42f * U, bh = 0.14f * U, gap = 0.05f * U;
            float x = Size2.X / 2f - (bw * 3f + gap * 2f) / 2f, y = h * 0.55f;
            ButtonAt("Easy", new Vector2(x, y), new Vector2(bw, bh), () => start(BotDifficulty.Easy));
            ButtonAt("Medium", new Vector2(x + bw + gap, y), new Vector2(bw, bh), () => start(BotDifficulty.Medium));
            ButtonAt("Hard", new Vector2(x + 2f * (bw + gap), y), new Vector2(bw, bh), () => start(BotDifficulty.Hard));
            Text("Prototype build: placeholder art", h - 0.1f * U, 0.6f, new Color(1f, 1f, 1f, 0.7f));
        }

        internal void ShowPlaying(Action pause)
        {
            ClearScreen();
            ButtonAt("II", new Vector2(0.03f * U, 0.03f * U), new Vector2(0.12f * U, 0.1f * U), pause);
        }

        internal void ShowPaused(Action resume, Action quit)
        {
            ClearScreen();
            Dim(0.5f);
            Text("PAUSED", Size2.Y * 0.22f, 1.4f, Colors.White);
            var size = new Vector2(0.7f * U, 0.13f * U);
            ButtonAt("Resume", new Vector2(Size2.X / 2f - size.X / 2f, Size2.Y * 0.45f), size, resume);
            ButtonAt("Quit to menu", new Vector2(Size2.X / 2f - size.X / 2f, Size2.Y * 0.45f + 0.17f * U), size, quit);
        }

        internal void ShowFullTime(int ours, int theirs, Action again, Action menu)
        {
            ClearScreen();
            Dim(0.55f);
            float y = Size2.Y * 0.16f;
            Text("FULL TIME", y, 1.2f, Colors.White);
            Text($"{ours} - {theirs}", y + 0.13f * U, 2.2f, Colors.White);
            string result = ours > theirs ? "YOU WIN!" : ours < theirs ? "YOU LOSE" : "DRAW";
            Text(result, y + 0.32f * U, 1.3f, ours > theirs ? Gold : Colors.White);

            var size = new Vector2(0.6f * U, 0.13f * U);
            float by = Size2.Y * 0.68f;
            ButtonAt("Play again", new Vector2(Size2.X / 2f - size.X - 0.03f * U, by), size, again);
            ButtonAt("Menu", new Vector2(Size2.X / 2f + 0.03f * U, by), size, menu);
        }
    }
}
