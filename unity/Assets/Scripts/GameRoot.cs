using MonkeyPost.Core;
using MonkeyPost.Core.Bots;
using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Entry point. Builds everything from code (no scene content needed) and runs the prototype
    /// flow: pick a difficulty → 4-minute match vs bots → full time → play again.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        enum State
        {
            Menu,
            Playing,
            Paused,
            FullTime,
        }

        readonly MatchConfig _config = new MatchConfig();
        readonly TouchControls _controls = new TouchControls();
        MatchPresenter _presenter;
        Camera _camera;
        State _state = State.Menu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GameRoot>() == null) new GameObject("Monkey Post").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Input.multiTouchEnabled = true;
            Input.simulateMouseWithTouches = false;

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Art.Sky;
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 150f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

            PitchBuilder.Build(_config, transform);
            _presenter = new MatchPresenter(_config, transform);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_state == State.Playing)
            {
                var input = _controls.Read(_presenter.HumanHasBall, dt);
                _presenter.Update(dt, input);
                if (_presenter.Finished) _state = State.FullTime;
            }
            _presenter.Render(_camera, _state == State.Paused ? 0f : dt);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && _state == State.Playing) Pause();
        }

        void Pause()
        {
            _state = State.Paused;
            _controls.Reset();
        }

        void StartMatch(BotDifficulty difficulty)
        {
            _controls.Reset();
            _presenter.StartMatch(difficulty, (ulong)System.DateTime.UtcNow.Ticks);
            _state = State.Playing;
        }

        void OnGUI()
        {
            float u = GuiKit.Unit;
            float w = Screen.width, h = Screen.height;

            switch (_state)
            {
                case State.Menu:
                    DrawMenu(u, w, h);
                    break;
                case State.Playing:
                    DrawHud(u, w);
                    _controls.Draw(_presenter.HumanHasBall, _presenter.SafeShotPower);
                    if (GuiKit.Button(new Rect(0.03f * u, 0.03f * u, 0.12f * u, 0.1f * u), "II")) Pause();
                    break;
                case State.Paused:
                    DrawHud(u, w);
                    GuiKit.Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.5f));
                    GuiKit.Label(new Rect(0, h * 0.25f, w, 0.12f * u), "PAUSED", 1.4f);
                    if (GuiKit.Button(new Rect(w / 2f - 0.35f * u, h * 0.45f, 0.7f * u, 0.13f * u), "Resume")) _state = State.Playing;
                    if (GuiKit.Button(new Rect(w / 2f - 0.35f * u, h * 0.45f + 0.17f * u, 0.7f * u, 0.13f * u), "Quit to menu")) _state = State.Menu;
                    break;
                case State.FullTime:
                    DrawHud(u, w);
                    DrawFullTime(u, w, h);
                    break;
            }
        }

        void DrawMenu(float u, float w, float h)
        {
            GuiKit.Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.35f));
            GuiKit.Label(new Rect(0, h * 0.1f, w, 0.15f * u), "MONKEY POST", 2.2f, new Color(1f, 0.85f, 0.1f));
            GuiKit.Label(new Rect(0, h * 0.1f + 0.15f * u, w, 0.08f * u), "Street Football", 1.1f);
            GuiKit.Label(new Rect(0, h * 0.42f, w, 0.08f * u), "Quick match: choose your opponents", 0.8f);

            float bw = 0.42f * u, bh = 0.14f * u, gap = 0.05f * u;
            float x = w / 2f - (bw * 3f + gap * 2f) / 2f, y = h * 0.55f;
            if (GuiKit.Button(new Rect(x, y, bw, bh), "Easy")) StartMatch(BotDifficulty.Easy);
            if (GuiKit.Button(new Rect(x + bw + gap, y, bw, bh), "Medium")) StartMatch(BotDifficulty.Medium);
            if (GuiKit.Button(new Rect(x + 2f * (bw + gap), y, bw, bh), "Hard")) StartMatch(BotDifficulty.Hard);

            GuiKit.Label(new Rect(0, h - 0.1f * u, w, 0.07f * u), "Prototype build: placeholder art", 0.6f, new Color(1f, 1f, 1f, 0.7f));
        }

        void DrawHud(float u, float w)
        {
            var sim = _presenter.Simulation;
            if (sim == null) return;

            var bar = new Rect(w / 2f - 0.75f * u, 0.02f * u, 1.5f * u, 0.11f * u);
            GuiKit.Rect(bar, new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Label(new Rect(bar.x, bar.y, bar.width * 0.4f, bar.height), "YOU", 0.75f);
            GuiKit.Label(new Rect(bar.x + bar.width * 0.4f, bar.y, bar.width * 0.2f, bar.height), $"{sim.Score(0)} - {sim.Score(1)}", 1.1f);
            GuiKit.Label(new Rect(bar.x + bar.width * 0.6f, bar.y, bar.width * 0.4f, bar.height), _presenter.OpponentName.ToUpperInvariant(), 0.6f);

            string clock = sim.IsGoldenGoal ? "GOLDEN GOAL" : FormatClock(sim.TimeRemaining);
            GuiKit.Label(new Rect(bar.x, bar.yMax, bar.width, 0.07f * u), clock, 0.7f);

            if (_presenter.GoalBanner > 0f)
            {
                bool ours = _presenter.LastGoalTeam == MatchPresenter.HumanTeam;
                GuiKit.Label(new Rect(0, Screen.height * 0.3f, w, 0.2f * u), ours ? "GOAL!" : "THEY SCORE", 2.4f,
                    ours ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.4f, 0.3f));
            }
            else if (_presenter.SkillBannerTime > 0f && _presenter.SkillBanner != null)
            {
                GuiKit.Label(new Rect(0, Screen.height * 0.22f, w, 0.12f * u), _presenter.SkillBanner, 1.5f, new Color(0.4f, 1f, 0.5f));
            }
            else if (sim.Phase == Phase.Kickoff)
            {
                GuiKit.Label(new Rect(0, Screen.height * 0.3f, w, 0.12f * u), "KICK-OFF", 1.3f);
            }
        }

        void DrawFullTime(float u, float w, float h)
        {
            var sim = _presenter.Simulation;
            GuiKit.Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.55f));
            int ours = sim.Score(0), theirs = sim.Score(1);
            string result = ours > theirs ? "YOU WIN!" : ours < theirs ? "YOU LOSE" : "DRAW";
            GuiKit.Label(new Rect(0, h * 0.18f, w, 0.1f * u), "FULL TIME", 1.2f);
            GuiKit.Label(new Rect(0, h * 0.18f + 0.13f * u, w, 0.16f * u), $"{ours} - {theirs}", 2.2f);
            GuiKit.Label(new Rect(0, h * 0.18f + 0.3f * u, w, 0.1f * u), result, 1.3f,
                ours > theirs ? new Color(1f, 0.85f, 0.1f) : Color.white);

            float bw = 0.6f * u, bh = 0.13f * u, y = h * 0.68f;
            if (GuiKit.Button(new Rect(w / 2f - bw - 0.03f * u, y, bw, bh), "Play again")) StartMatch(_presenter.Difficulty);
            if (GuiKit.Button(new Rect(w / 2f + 0.03f * u, y, bw, bh), "Menu")) _state = State.Menu;
        }

        static string FormatClock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
