using System;
using System.Collections;
using UnityEngine;
using VibeRun.Core;
using VibeRun.Gameplay;
using VibeRun.Services;
using VibeRun.UI;

namespace VibeRun
{
    /// <summary>
    /// The only scene object. Builds camera, canvas, screens and the run controller at runtime and drives the session
    /// loop: open → tap to vibe → run → result card → share / play again → daily drop and shop.
    /// Dev flags: -vr-autoplay (bot plays), -vr-shot &lt;path&gt; (screenshot then quit), -vr-screen home|run|result|shop.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [Tooltip("Skip the home screen and start a Classic run (editor testing).")]
        public bool debugStartRun;
        [Tooltip("Let a bot answer (80% correct) so you can watch shifts, combos and the result card.")]
        public bool debugAutoplay;

        Canvas _canvas;
        HomeScreen _home;
        ResultScreen _result;
        RevivePopup _revive;
        ShopScreen _shop;
        ChallengePopup _challenge;
        Toast _toast;
        RunController _run;
        RunMode _lastMode = RunMode.Classic;
        uint _lastSeed;
        bool _lastWasChallenge;
        string _lastRunCode = "";
        float _autoT;
        System.Random _botRng = new System.Random(4);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Input.multiTouchEnabled = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var cfg = GameConfig.Instance;
            var profile = SaveService.Profile;

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Bg;

            AudioManager.Create();
            AudioManager.Instance.SetMuted(profile.muted);

            _canvas = UIKit.CreateCanvas("Canvas");
            var safe = UIKit.Rect(_canvas.transform, "SafeArea");
            UIKit.Fill(safe);
            safe.gameObject.AddComponent<SafeArea>();

            _run = new GameObject("Run").AddComponent<RunController>();
            _run.Build(_canvas, safe);
            _run.OnRunEnded = OnRunEnded;
            _run.RequestRevive = (state, cb) => _revive.Show(cb);

            _home = new HomeScreen(safe, () => StartRun(RunMode.Classic, NewSeed(), false), StartDaily, ShowShop,
                () => _challenge.Show(_lastRunCode));
            _result = new ResultScreen(safe, this, PlayAgain, GoHome, msg => _toast.Show(msg));
            _shop = new ShopScreen(safe, GoHome, msg => _toast.Show(msg));
            _revive = new RevivePopup(safe);
            _challenge = new ChallengePopup(safe, seed => StartRun(RunMode.Classic, seed, true), msg => _toast.Show(msg));
            _toast = new Toast(safe);

            Application.deepLinkActivated += OnDeepLink;
            StartCoroutine(cfg.FetchRemoteContent());

            ParseDevArgs();
            if (!string.IsNullOrEmpty(Application.absoluteURL) && Application.absoluteURL.Contains("seed=")) OnDeepLink(Application.absoluteURL);
            else if (debugStartRun) StartRun(RunMode.Classic, NewSeed(), false);
            else GoHome();

            int gift = Daily.ClaimLoginGift(profile, DateTime.Now);
            if (gift > 0) { SaveService.Save(); _toast.Show("daily gift: +" + gift + " vibes"); _home.Refresh(); }
        }

        static uint NewSeed() => (uint)UnityEngine.Random.Range(1, int.MaxValue) ^ (uint)DateTime.Now.Ticks;

        // ------------------------------------------------------------------ flow

        void GoHome()
        {
            AdManager.Instance.MaybeInterstitial("between_runs_home", GameConfig.Instance.Tuning.interstitialCooldown, SaveService.Profile.adFree, () =>
            {
                HideAll();
                _home.Show();
                AudioManager.Instance?.SetMusic(0.35f);
            });
        }

        void ShowShop() { HideAll(); _shop.Show(); }

        void HideAll()
        {
            _home.Hide(); _result.Hide(); _shop.Hide(); _revive.Hide(); _challenge.Hide();
            _run.Hide();
        }

        void StartDaily()
        {
            var p = SaveService.Profile;
            if (!Daily.CanPlayDrop(p, DateTime.Now))
            {
                _toast.Show(string.IsNullOrEmpty(p.lastDailyGrid) ? "you already did today's drop" : "today's drop is done. copied your grid.");
                if (!string.IsNullOrEmpty(p.lastDailyGrid)) ShareService.CopyText("Vibe Run Daily Drop " + p.lastDailyDay + "\n" + p.lastDailyGrid);
                return;
            }
            StartRun(RunMode.Daily, Daily.DropSeed(DateTime.Now), false);
        }

        void StartRun(RunMode mode, uint seed, bool challenge)
        {
            HideAll();
            _lastMode = mode;
            _lastSeed = seed;
            _lastWasChallenge = challenge;
            bool tutorial = !SaveService.Profile.tutorialDone && mode == RunMode.Classic;
            _run.Begin(mode, seed, tutorial);
            if (challenge) _toast.Show("challenge run " + ResultLogic.SeedCode(seed) + ". same doors as your friend.");
        }

        void PlayAgain()
        {
            if (_lastMode == RunMode.Daily) { GoHome(); return; }
            AdManager.Instance.MaybeInterstitial("between_runs_again", GameConfig.Instance.Tuning.interstitialCooldown, SaveService.Profile.adFree,
                () => StartRun(RunMode.Classic, _lastWasChallenge ? _lastSeed : NewSeed(), _lastWasChallenge));
        }

        void OnRunEnded(RunState run)
        {
            var cfg = GameConfig.Instance;
            var p = SaveService.Profile;
            var card = ResultLogic.Build(run, cfg.Captions, new Rng(run.Seed ^ (uint)run.Score));
            int oldBest = p.bestScore;
            var (vibes, levels) = Progression.ApplyRun(p, run.Score, run.History.Count, cfg.Tuning.vibesPerScore);
            if (run.Mode == RunMode.Daily)
            {
                Daily.CompleteDrop(p, DateTime.Now);
                p.lastDailyGrid = card.grid;
                p.lastDailyScore = run.Score;
            }
            SaveService.Save();
            _lastRunCode = card.seedCode;

            string rewards = "+" + vibes + " vibes  ·  +" + Progression.XpForRun(run.Score, run.History.Count) + " xp";
            if (levels > 0) rewards += "  ·  LEVEL UP!";
            if (run.Score > oldBest && oldBest > 0) rewards = "NEW BEST!  " + rewards;
            if (run.Mode == RunMode.Daily) rewards += "  ·  streak " + p.dailyStreak;

            _run.Hide();
            _result.Show(run, card, rewards, run.Mode != RunMode.Daily);
            AudioManager.Instance?.PlayCoin();
        }

        void OnDeepLink(string url)
        {
            // viberun://run?seed=ABC2DEF
            int i = url.IndexOf("seed=", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return;
            string code = url.Substring(i + 5);
            int amp = code.IndexOf('&'); if (amp >= 0) code = code.Substring(0, amp);
            if (ResultLogic.TryParseSeedCode(code, out uint seed)) StartRun(RunMode.Classic, seed, true);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _home.Tick(dt);
            _result.Tick(dt);
            _revive.Tick(dt);
            _toast.Tick(dt);
            if (Input.GetKeyDown(KeyCode.Escape) && (_shop.Visible || _challenge.Visible)) GoHome();

            if (debugAutoplay)
            {
                if (_run.InDecisionWindow)
                {
                    _autoT += dt;
                    if (_autoT > 0.32f) { _autoT = 0; _run.DebugAutoAnswer(_botRng.NextDouble() < 0.82); }
                }
                else _autoT = 0;
                if (_revive.Visible) { /* let the popup time out */ }
            }
        }

        // ------------------------------------------------------------------ dev harness

        void ParseDevArgs()
        {
            var args = Environment.GetCommandLineArgs();
            string screen = null, shot = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-vr-autoplay") debugAutoplay = true;
                if (args[i] == "-vr-run") debugStartRun = true;
                if (i + 1 < args.Length && args[i] == "-vr-shot") shot = args[i + 1];
                if (i + 1 < args.Length && args[i] == "-vr-screen") screen = args[i + 1];
                if (args[i] == "-vr-reset") SaveService.Reset();
            }
            if (shot != null) StartCoroutine(ShotAndQuit(shot, screen));
        }

        IEnumerator ShotAndQuit(string path, string screen)
        {
            yield return null;
            if (screen == "run" || screen == "result") { debugAutoplay = true; StartRun(RunMode.Classic, 424242, false); }
            if (screen == "shop") ShowShop();
            if (screen == "daily") { debugAutoplay = true; StartRun(RunMode.Daily, Daily.DropSeed(DateTime.Now), false); }
            float wait = screen == "run" ? 9f : screen == "result" || screen == "daily" ? 90f : 2f;
            float t = 0;
            while (t < wait)
            {
                t += Time.unscaledDeltaTime;
                if ((screen == "result" || screen == "daily") && _result.Visible) { yield return new WaitForSecondsRealtime(1.2f); break; }
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[VibeRun] screenshot saved: " + path);
            Application.Quit();
        }
    }

    /// <summary>Keeps UI inside the device safe area (notches, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last || Screen.width == 0) return;
            _last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
