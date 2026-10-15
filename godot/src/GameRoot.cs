using System;
using Godot;
using MonkeyPost.Core;
using MonkeyPost.Core.Bots;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Entry point. Builds everything from code and runs the prototype flow:
    /// pick a difficulty → 4-minute match vs bots → full time → play again.
    /// </summary>
    public partial class GameRoot : Node3D
    {
        enum State
        {
            Menu,
            Playing,
            Paused,
            FullTime,
        }

        readonly MatchConfig _config = new MatchConfig();
        MatchPresenter _presenter;
        Camera3D _camera;
        TouchPad _pad;
        Hud _hud;
        State _state = State.Menu;

        public override void _Ready()
        {
            Engine.MaxFps = 30;
            DisplayServer.ScreenSetKeepOn(true);

            _camera = new Camera3D { Name = "Camera", Fov = 44f, Near = 0.5f, Far = 150f };
            AddChild(_camera);
            _camera.MakeCurrent();

            var sun = new DirectionalLight3D { Name = "Sun", LightEnergy = 1.1f, ShadowEnabled = false };
            AddChild(sun);
            sun.RotationDegrees = new Vector3(-55f, 35f, 0f);

            var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Art.Sky,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.55f, 0.58f),
                AmbientLightEnergy = 1f,
            };
            AddChild(new WorldEnvironment { Environment = environment });

            PitchBuilder.Build(_config, this);
            _presenter = new MatchPresenter(_config, this);

            var ui = new CanvasLayer { Name = "UI" };
            AddChild(ui);
            _pad = new TouchPad { Name = "TouchPad", Visible = false };
            ui.AddChild(_pad);
            _hud = new Hud { Name = "Hud" };
            ui.AddChild(_hud);
            _hud.Init(this);
            ShowMenu();

            // Automated checks: `-- --demo[=Easy|Medium|Hard] [--fast]` plays computer v computer.
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--demo"))
                {
                    var level = arg.Contains("=") && Enum.TryParse(arg.Split('=')[1], out BotDifficulty parsed) ? parsed : BotDifficulty.Medium;
                    StartDemo(level);
                }
                if (arg == "--fast") Engine.TimeScale = 8.0;
                // `--screenshot=/path/shot.png@12` saves the screen after 12 s of game time (repeatable).
                if (arg.StartsWith("--screenshot=") && arg.Contains("@"))
                {
                    var parts = arg.Substring("--screenshot=".Length).Split('@');
                    if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float at))
                        _screenshots.Add((at, parts[0]));
                }
            }
            _screenshots.Sort((a, b) => a.At.CompareTo(b.At));
        }

        readonly System.Collections.Generic.List<(float At, string Path)> _screenshots = new System.Collections.Generic.List<(float, string)>();
        float _elapsed;

        void TakeScreenshots(float dt)
        {
            if (_screenshots.Count == 0) return;
            _elapsed += dt;
            if (_elapsed < _screenshots[0].At) return;
            GetViewport().GetTexture().GetImage().SavePng(_screenshots[0].Path);
            GD.Print($"[MonkeyPost] Screenshot {_screenshots[0].Path}");
            _screenshots.RemoveAt(0);
            if (_screenshots.Count == 0) GetTree().Quit();
        }

        void StartDemo(BotDifficulty difficulty)
        {
            _presenter.StartMatch(difficulty, 1, demo: true);
            _state = State.Playing;
            _hud.ShowPlaying(Pause);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (_state == State.Playing)
            {
                _pad.HasBall = _presenter.HumanHasBall;
                _pad.SafePower = _pad.Charging ? _presenter.SafeShotPower : 1f;
                var input = _pad.Read(dt);
                _presenter.Update(dt, input);
                if (_presenter.Finished)
                {
                    var sim = _presenter.Simulation;
                    GD.Print($"[MonkeyPost] Full time {sim.Score(0)}-{sim.Score(1)}");
                    ShowFullTime();
                }
            }
            _presenter.Render(_camera, _state == State.Paused ? 0f : dt, Time.GetTicksMsec() / 1000.0);
            _hud.Refresh(_presenter, _state == State.Playing || _state == State.Paused || _state == State.FullTime);
            TakeScreenshots(dt);
        }

        public override void _Notification(int what)
        {
            // Pause when the app goes to the background (calls, notifications, home button).
            if ((what == NotificationApplicationPaused || what == NotificationApplicationFocusOut) && _state == State.Playing)
                Pause();
        }

        internal void StartMatch(BotDifficulty difficulty)
        {
            _pad.ResetState();
            _presenter.StartMatch(difficulty, (ulong)DateTime.UtcNow.Ticks);
            _state = State.Playing;
            _pad.Visible = true;
            _hud.ShowPlaying(Pause);
        }

        internal void Pause()
        {
            if (_state != State.Playing) return;
            _state = State.Paused;
            _pad.ResetState();
            _pad.Visible = false;
            _hud.ShowPaused(Resume, ShowMenu);
        }

        void Resume()
        {
            _state = State.Playing;
            _pad.Visible = true;
            _hud.ShowPlaying(Pause);
        }

        void ShowMenu()
        {
            _state = State.Menu;
            _pad.Visible = false;
            _hud.ShowMenu(StartMatch);
        }

        void ShowFullTime()
        {
            _state = State.FullTime;
            _pad.Visible = false;
            var sim = _presenter.Simulation;
            _hud.ShowFullTime(sim.Score(0), sim.Score(1), () => StartMatch(_presenter.Difficulty), ShowMenu);
        }
    }
}
