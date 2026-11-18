using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VibeRun.Core;
using VibeRun.Services;
using VibeRun.UI;
using Random = UnityEngine.Random;

namespace VibeRun.Gameplay
{
    /// <summary>
    /// Runs one Vibe Run: prompt → doors approach → tap a door → react → next, with vibe shifts, combos, aura drain,
    /// an in-run tutorial on first launch, and a revive hook. Owns the in-run view and HUD.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        enum State { Idle, Banner, Window, Resolve, Dying, Done }

        // hooks set by Bootstrap
        public Action<RunState> OnRunEnded;
        public Action<RunState, Action<bool>> RequestRevive;

        GameConfig _cfg;
        Tuning T => _cfg.Tuning;
        RunState _run;
        PromptDeck _deck;
        BeatSpec _spec;
        State _state = State.Idle;
        float _timer, _elapsed, _window, _windowStart, _hitstop, _trailTimer;
        bool _paused;
        int _tutorialLeft;
        bool _tutorialBeat;
        bool _shownThreeDoorHint;
        Rng _capRng;

        // view
        Canvas _canvas;
        RectTransform _root, _world, _hud;
        Hallway _hall;
        CharacterView _char;
        FxLayer _fx;
        SwipeInput _input;
        readonly List<DoorView> _doors = new List<DoorView>();
        Image _doorLine, _vignette, _promptFill, _auraFill, _auraEdge;
        Text _promptText, _catText, _shiftChip, _auraText, _comboText, _scoreText, _caption, _bannerTitle, _bannerSub, _hint, _pauseText;
        RectTransform _banner, _promptCard, _comboCard, _hintFinger;
        GameObject _pauseOverlay;
        float _captionT, _bannerT, _comboPunch;
        CosmeticDef _trail;

        static readonly Vector2 CharHome = new Vector2(0, -640);
        const float LineY = -400f;
        const float Travel = 420f;

        public bool Active => _state != State.Idle && _state != State.Done;
        public RunState Run => _run;

        public void Build(Canvas canvas, Transform parent)
        {
            _cfg = GameConfig.Instance;
            _canvas = canvas;
            _root = UIKit.Rect(parent, "RunView");
            UIKit.Fill(_root);
            _world = UIKit.Rect(_root, "World");
            UIKit.Fill(_world);
            _hall = Hallway.Create(_world);

            _doorLine = UIKit.Image(_world, "doorLine", Color.white.A(0.25f), Art.RoundedSmall);
            UIKit.Place(_doorLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, LineY - 10), new Vector2(980, 14));

            for (int i = 0; i < 3; i++)
            {
                var d = DoorView.Create(_world);
                int slot = i;
                d.OnPressed = _ => OnDoorTapped(slot);
                d.gameObject.SetActive(false);
                _doors.Add(d);
            }
            _char = CharacterView.Create(_world, 230);
            _char.Snap(CharHome);
            _fx = FxLayer.Create(_world);
            _fx.ShakeTarget = _world;

            _vignette = UIKit.Image(_root, "lowAura", new Color(0.85f, 0.1f, 0.25f, 0f));
            UIKit.Fill(_vignette.rectTransform);

            BuildHud();

            _input = _root.gameObject.AddComponent<SwipeInput>();
            _root.gameObject.SetActive(false);
        }

        void BuildHud()
        {
            _hud = UIKit.Rect(_root, "HUD");
            UIKit.Fill(_hud);

            // prompt card (top)
            var pf = UIKit.Card(_hud, "prompt", Pal.Paper, 10f);
            _promptFill = pf;
            _promptCard = (RectTransform)pf.transform.parent;
            UIKit.Place(_promptCard, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(960, 230));
            _catText = UIKit.Text(pf.transform, "cat", "", 30, Pal.Muted);
            _catText.alignment = TextAnchor.UpperCenter; _catText.rectTransform.offsetMax = new Vector2(0, -16);
            _promptText = UIKit.Text(pf.transform, "text", "", 74, Pal.Ink);
            _promptText.rectTransform.offsetMin = new Vector2(30, 10); _promptText.rectTransform.offsetMax = new Vector2(-30, -36);
            _promptText.resizeTextForBestFit = true; _promptText.resizeTextMinSize = 40; _promptText.resizeTextMaxSize = 76;

            _shiftChip = UIKit.Text(_hud, "shiftChip", "", 40, Pal.Yellow);
            UIKit.Place(_shiftChip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -480), new Vector2(1000, 60));
            UIKit.Sticker(_shiftChip, Pal.Ink, 3f);

            // aura bar (top-left)
            var auraRoot = UIKit.Rect(_hud, "aura");
            UIKit.Place(auraRoot, new Vector2(0f, 1f), new Vector2(60 + 210, -110), new Vector2(420, 70));
            var edge = UIKit.Image(auraRoot, "edge", Pal.Ink, Art.Rounded); UIKit.Fill(edge.rectTransform);
            _auraEdge = edge;
            var track = UIKit.Image(auraRoot, "track", Pal.Bg2, Art.Rounded);
            UIKit.Stretch(track.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            _auraFill = UIKit.Image(track.rectTransform, "fill", Pal.Mint, Art.Rounded);
            UIKit.Stretch(_auraFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _auraText = UIKit.Text(auraRoot, "label", "AURA 100", 34, Pal.Paper);
            UIKit.Sticker(_auraText, Pal.Ink, 3f);

            // combo (top-right)
            _comboCard = UIKit.Rect(_hud, "combo");
            UIKit.Place(_comboCard, new Vector2(1f, 1f), new Vector2(-60 - 130, -120), new Vector2(260, 130));
            _comboText = UIKit.Text(_comboCard, "mult", "x1", 84, Pal.Paper, TextAnchor.UpperRight);
            UIKit.Sticker(_comboText, Pal.Ink, 5f);
            _scoreText = UIKit.Text(_comboCard, "score", "0", 34, Pal.Muted, TextAnchor.LowerRight);
            _scoreText.rectTransform.offsetMin = new Vector2(0, -44);

            // caption (mid)
            _caption = UIKit.Text(_hud, "caption", "", 64, Pal.Paper);
            UIKit.Place(_caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(980, 200));
            UIKit.Sticker(_caption, Pal.Ink, 6f);

            // vibe shift banner
            _banner = UIKit.Rect(_hud, "banner");
            UIKit.Place(_banner, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 300));
            var bf = UIKit.Card(_banner, "band", Pal.Pink, 12f, null, true);
            UIKit.Fill((RectTransform)bf.transform.parent);
            _bannerTitle = UIKit.Text(bf.transform, "title", "VIBE SHIFT", 96, Pal.Paper);
            _bannerTitle.rectTransform.offsetMin = new Vector2(0, 70);
            UIKit.Sticker(_bannerTitle, Pal.Ink, 6f);
            _bannerSub = UIKit.Text(bf.transform, "sub", "", 44, Pal.Ink);
            _bannerSub.rectTransform.offsetMax = new Vector2(0, -150);
            _banner.localRotation = Quaternion.Euler(0, 0, -4f);
            _banner.gameObject.SetActive(false);

            // tutorial hint
            _hint = UIKit.Text(_hud, "hint", "", 48, Pal.Paper);
            UIKit.Place(_hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -820), new Vector2(1000, 120));
            UIKit.Sticker(_hint, Pal.Ink, 4f);
            var finger = UIKit.Image(_hud, "finger", Color.white.A(0.85f), Art.Circle);
            _hintFinger = finger.rectTransform;
            UIKit.Place(_hintFinger, new Vector2(0.5f, 0.5f), CharHome, new Vector2(90, 90));
            var fr = UIKit.Image(_hintFinger, "ring", Pal.Ink, Art.Ring); UIKit.Fill(fr.rectTransform);
            _hintFinger.gameObject.SetActive(false);

            // pause overlay
            var po = UIKit.Image(_root, "pause", new Color(0, 0, 0, 0.7f), null, true);
            UIKit.Fill(po.rectTransform);
            _pauseOverlay = po.gameObject;
            var pbtn = po.gameObject.AddComponent<Button>();
            pbtn.onClick.AddListener(Resume);
            _pauseText = UIKit.Text(po.transform, "t", "PAUSED\n<size=44>tap to resume</size>", 110, Pal.Paper);
            _pauseOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle

        public void Begin(RunMode mode, uint seed, bool tutorial)
        {
            _root.gameObject.SetActive(true);
            _run = new RunState(T, mode, seed);
            _deck = new PromptDeck(_cfg.Prompts, T, seed);
            _capRng = new Rng(seed ^ 0xC0FFEEu);
            _tutorialLeft = tutorial ? 3 : 0;
            _shownThreeDoorHint = false;
            _paused = false;
            _pauseOverlay.SetActive(false);
            _hitstop = 0;
            _fx.Clear();
            _char.Snap(CharHome);
            _char.SetFace(Face.Idle);
            _char.Spin = 0;
            _char.ApplyFit(_cfg.Cosmetic(SaveService.Profile.fit) ?? _cfg.FirstOfKind("fit"));
            _trail = _cfg.Cosmetic(SaveService.Profile.trail) ?? _cfg.FirstOfKind("trail");
            _hall.SetComboLevel(0, true);
            _caption.text = ""; _hint.text = "";
            _shiftChip.text = "";
            _input.Clear();
            _input.Enabled = true;
            AdManager.Instance.InRun = true;
            AudioManager.Instance?.SetMusic(0.55f);
            AudioManager.Instance?.SetComboLayer(false);
            AudioManager.Instance?.SetLowAura(false);
            Analytics.Log("run_start", ("mode", mode.ToString()), ("seed", ResultLogic.SeedCode(seed)), ("content", _cfg.ContentVersion));
            UpdateHud(true);
            NextBeat();
        }

        public void Hide()
        {
            _state = State.Idle;
            _root.gameObject.SetActive(false);
            AdManager.Instance.InRun = false;
        }

        void NextBeat()
        {
            _char.Target = CharHome;
            _tutorialBeat = _tutorialLeft > 0;
            VibeShift shift = VibeShift.None;
            if (_tutorialBeat)
            {
                _spec = _deck.Next(1, VibeShift.None, tutorial: true);
            }
            else
            {
                _run.BeginBeat();
                shift = _run.ShiftFor(_run.Beat);
                _spec = _deck.Next(_run.Beat, shift);
            }

            SetupDoors();
            _promptText.text = _spec.prompt.p;
            _catText.text = Categories.Display(_spec.prompt.cat).ToUpperInvariant();
            _promptFill.color = shift == VibeShift.OppositeDay ? Pal.Hex("#FFD0A8") : shift == VibeShift.MirrorMode ? Pal.Hex("#CFE6FF") : Pal.Paper;
            _shiftChip.text = ShiftChip(shift);
            _promptCard.localScale = Vector3.one * 1.08f;

            if (!_tutorialBeat && _run.IsShiftStart(_run.Beat))
            {
                ShowBanner(shift);
                _state = State.Banner;
                _timer = T.shiftBannerSeconds;
                foreach (var d in _doors) d.SetAlpha(0f);
                return;
            }
            StartWindow();
        }

        void SetupDoors()
        {
            int n = _spec.DoorCount;
            Vector2 size = n == 2 ? new Vector2(440, 560) : new Vector2(320, 470);
            float[] xs = n == 2 ? new[] { -245f, 245f } : new[] { -350f, 0f, 350f };
            int colorBase = Random.Range(0, Pal.DoorColors.Length);
            for (int i = 0; i < _doors.Count; i++)
            {
                var d = _doors[i];
                if (i >= n) { d.gameObject.SetActive(false); continue; }
                d.Setup(_spec.Label(i), Pal.DoorColors[(colorBase + i * 2) % Pal.DoorColors.Length], size);
                d.Rt.anchoredPosition = new Vector2(xs[i], DoorY(0f, size.y));
                d.Lift = 0;
                d.Interactable = false;
                d.MirrorLabel(_spec.shift == VibeShift.MirrorMode);
            }
            _input.DoorCount = n;
            if (n == 3 && !_shownThreeDoorHint)
            {
                _shownThreeDoorHint = true;
                _fx.Popup(new Vector2(0, -200), "3 DOORS now. tap the one that fits", Pal.Yellow, 52, 1.6f);
            }
        }

        static float DoorY(float frac, float h) => LineY + h * 0.5f + Travel * (1f - frac);

        void StartWindow()
        {
            _state = State.Window;
            _elapsed = 0f;
            _window = _tutorialBeat ? 2.2f : _run.DecisionWindow(_run.Beat);
            _windowStart = Time.unscaledTime;
            for (int i = 0; i < _doors.Count; i++) { _doors[i].SetAlpha(1f); _doors[i].Interactable = i < _spec.DoorCount; }
            if (_tutorialBeat)
            {
                _hint.text = _tutorialLeft == 3 ? "tap the door that matches the prompt" : _tutorialLeft == 2 ? "nice. again!" : "last one. it starts slow, then speeds up";
                _hintFinger.gameObject.SetActive(true);
            }
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (_state == State.Idle || _state == State.Done) return;
            float dt = Time.unscaledDeltaTime;
            AnimateHud(dt);
            if (Input.GetKeyDown(KeyCode.Escape) && _state != State.Dying) { if (_paused) Resume(); else Pause(); }
            if (_paused) return;
            if (_hitstop > 0) { _hitstop -= dt; return; }

            switch (_state)
            {
                case State.Banner:
                    _timer -= dt;
                    if (_timer <= 0) { _banner.gameObject.SetActive(false); StartWindow(); }
                    break;

                case State.Window:
                    if (!_tutorialBeat) _run.Tick(dt);
                    _elapsed += dt;
                    float frac = Mathf.Clamp01(_elapsed / _window);
                    float ease = 1f - (1f - frac) * (1f - frac) * 0.35f - 0.65f * (1f - frac); // mostly linear with a soft start
                    for (int i = 0; i < _spec.DoorCount; i++)
                    {
                        var d = _doors[i];
                        d.Rt.anchoredPosition = new Vector2(d.Rt.anchoredPosition.x, DoorY(ease, d.Rt.sizeDelta.y));
                        float s = Mathf.Lerp(0.84f, 1f, ease);
                        d.Rt.localScale = new Vector3(s, s, 1);
                        if (_spec.shift == VibeShift.SpeedBlink) d.HideLabel(frac > 0.5f);
                    }
                    _doorLine.color = Color.Lerp(Color.white.A(0.25f), Pal.Bad.A(0.9f), frac * frac);
                    if (_tutorialBeat) AnimateFinger();

                    // keyboard: anything pressed during the window, or up to bufferSeconds before it opened
                    if (_input.Last != Swipe.None && _input.LastTime >= _windowStart - T.bufferSeconds)
                    {
                        var sw = _input.Last;
                        bool early = _input.LastTime < _windowStart;
                        _input.Clear();
                        int ks = RunState.SlotForSwipe(sw, _spec.shift, _spec.DoorCount);
                        if (ks >= 0) OnPick(ks, Mathf.Clamp01((early ? 0f : _elapsed) / _window));
                    }
                    else
                    {
                        if (_input.Last != Swipe.None) _input.Clear(); // stale
                        if (_elapsed >= _window && !_tutorialBeat) OnFreeze();
                    }
                    if (_state == State.Window && !_run.Alive) BeginDying();
                    break;

                case State.Resolve:
                    if (!_tutorialBeat) _run.Tick(dt);
                    _timer -= dt;
                    AnimateResolve(dt);
                    if (_timer <= 0)
                    {
                        if (!_run.Alive) BeginDying();
                        else if (_run.IsComplete) EndRun("complete");
                        else NextBeat();
                    }
                    break;
            }

            // trail at high combos
            if (_run != null && _run.Multiplier >= 3 && _state != State.Dying)
            {
                _trailTimer -= dt;
                if (_trailTimer <= 0)
                {
                    _trailTimer = _run.Multiplier >= 4 ? 0.03f : 0.07f;
                    _fx.Trail(((RectTransform)_char.transform).anchoredPosition + new Vector2(0, -40), _trail);
                }
            }
            UpdateHud(false);
        }

        void OnDoorTapped(int slot)
        {
            if (_state != State.Window || _paused || _hitstop > 0 || _spec == null || slot >= _spec.DoorCount) return;
            OnPick(slot, Mathf.Clamp01(_elapsed / _window));
        }

        void OnPick(int slot, float reactionFraction)
        {
            if (slot < 0) return;
            foreach (var d in _doors) d.Interactable = false;
            AudioManager.Instance?.PlayWhoosh();

            if (_tutorialBeat)
            {
                if (slot == _spec.answerSlot) { TutorialCorrect(slot); }
                else
                {
                    ShowCaption("other door, bestie");
                    for (int i = 0; i < _spec.DoorCount; i++) _doors[i].Interactable = true; // let them retry
                    _fx.Shake(12f);
                    _char.Kick(-4f);
                    AudioManager.Instance?.PlayFail();
                }
                return;
            }

            var res = _run.ResolveSlot(_spec, slot, reactionFraction, _window);
            Analytics.Log("pick", ("prompt", _spec.prompt.id), ("choice", slot), ("ms", res.reactionMs), ("outcome", res.outcome.ToString()));
            if (res.outcome == Outcome.Correct) PlayCorrect(slot, res);
            else PlayWrong(slot, res);
        }

        void OnFreeze()
        {
            var res = _run.ResolveFreeze(_spec, _window);
            Analytics.Log("pick", ("prompt", _spec.prompt.id), ("choice", -1), ("ms", res.reactionMs), ("outcome", "Freeze"));
            _char.SetFace(Face.Shock, 0.6f);
            _char.Kick(-3f);
            _fx.Shake(10f);
            _fx.Popup(CharHome + new Vector2(0, 220), "-" + (int)T.freezeCost, Pal.Bad, 60);
            ShowCaption(ResultLogic.Pick(_cfg.Captions.freeze, _capRng, "you froze"));
            AudioManager.Instance?.PlayFreeze();
            Haptics.Miss();
            _resolveKind = 2; _resolveSlot = -1;
            _state = State.Resolve;
            _timer = 0.35f;
        }

        int _resolveKind, _resolveSlot; // 0 correct, 1 wrong, 2 freeze

        void PlayCorrect(int slot, BeatResult res)
        {
            var door = _doors[slot];
            var at = door.Rt.anchoredPosition;
            _char.Target = new Vector2(at.x, LineY + 60);
            _char.SetFace(_run.Multiplier >= 4 ? Face.Cool : Face.Happy, 0.5f);
            _char.Kick(5f);
            int mult = _run.Multiplier;
            _fx.Burst(at, 14 + mult * 8, Pal.Paper, Pal.DoorColors[Random.Range(0, Pal.DoorColors.Length)], Art.Star, 900 + mult * 200, 30 + mult * 4);
            if (mult >= 3) _fx.Burst(at, 10, Pal.Pink, Pal.Yellow, Art.Heart, 700, 34);
            _fx.Popup(at + new Vector2(0, 120), "+" + res.points + (res.fast ? "  FAST" : ""), res.fast ? Pal.Yellow : Pal.Good, res.fast ? 70 : 62);
            _fx.Shake(10f + mult * 3f);
            _fx.Flash(Color.white, 0.18f);
            _hitstop = 0.06f;
            AudioManager.Instance?.PlayPop(_run.Streak);

            // combo milestones
            if (_run.Streak == 5 || _run.Streak == 10 || _run.Streak == 20)
            {
                string big = _run.Streak == 20 ? "UNHINGED STREAK x4" : "COMBO x" + _run.Multiplier;
                _fx.Popup(new Vector2(0, 220), big, Pal.Pink, 92, 1.2f);
                ShowCaption(ResultLogic.Pick(_cfg.Captions.hype, _capRng, "on fire"));
                AudioManager.Instance?.PlayBanner();
                _comboPunch = 1f;
            }
            else if (_run.Streak > 20 && _run.Streak % 10 == 0) { ShowCaption(ResultLogic.Pick(_cfg.Captions.hype, _capRng, "on fire")); _comboPunch = 1f; }
            _hall.SetComboLevel(_run.Multiplier - 1);
            AudioManager.Instance?.SetComboLayer(_run.Multiplier >= _run.MaxMultiplier);
            _resolveKind = 0; _resolveSlot = slot;
            _state = State.Resolve;
            _timer = 0.2f;
        }

        void PlayWrong(int slot, BeatResult res)
        {
            var door = _doors[slot];
            var at = door.Rt.anchoredPosition;
            _char.Target = new Vector2(at.x * 0.6f, LineY + 10);
            _char.SetFace(Face.Shock, 0.7f);
            _char.Spin = Random.value < 0.5f ? 28f : -28f;
            _char.Kick(-6f);
            door.Tint(Pal.Bad);
            _fx.Burst(at + new Vector2(0, -door.Rt.sizeDelta.y * 0.4f), 10, Pal.Bad, Pal.Paper, Art.Circle, 600, 26);
            _fx.Popup(CharHome + new Vector2(0, 240), "-" + (int)T.wrongCost, Pal.Bad, 64);
            _fx.Shake(24f);
            _fx.Flash(Pal.Bad, 0.25f);
            string cap = ResultLogic.Pick(_cfg.Captions.fail, _capRng, "bruh");
            ShowCaption(ResultLogic.Fill(cap, res));
            AudioManager.Instance?.PlayFail();
            Haptics.Miss();
            _hall.SetComboLevel(0);
            AudioManager.Instance?.SetComboLayer(false);
            _resolveKind = 1; _resolveSlot = slot;
            _state = State.Resolve;
            _timer = 0.4f;
        }

        void TutorialCorrect(int slot)
        {
            var at = _doors[slot].Rt.anchoredPosition;
            _char.Target = new Vector2(at.x, LineY + 60);
            _char.SetFace(Face.Happy, 0.5f);
            _char.Kick(5f);
            _fx.Burst(at, 22, Pal.Paper, Pal.Yellow, Art.Star, 1000, 32);
            _fx.Shake(10f);
            _fx.Flash(Color.white, 0.2f);
            _hitstop = 0.06f;
            AudioManager.Instance?.PlayPop(3 - _tutorialLeft);
            _tutorialLeft--;
            _hintFinger.gameObject.SetActive(false);
            if (_tutorialLeft == 0)
            {
                _hint.text = "";
                ShowCaption("now it's real. don't freeze.");
                SaveService.Profile.tutorialDone = true;
                SaveService.Save();
            }
            _resolveKind = 0; _resolveSlot = slot;
            _state = State.Resolve;
            _timer = 0.3f;
        }

        void AnimateResolve(float dt)
        {
            for (int i = 0; i < _spec.DoorCount; i++)
            {
                var d = _doors[i];
                if (_resolveKind == 0 && i == _resolveSlot)
                {
                    // burst open: scale up and fade
                    d.Rt.localScale = Vector3.Lerp(d.Rt.localScale, Vector3.one * 1.35f, dt * 14f);
                    d.SetAlpha(Mathf.MoveTowards(d.GetComponent<CanvasGroup>().alpha, 0f, dt * 6f));
                }
                else if (_resolveKind == 1 && i == _resolveSlot)
                {
                    // the wrong door wobbles and stays
                    d.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 50f) * 4f);
                }
                else
                {
                    // the rest slide past and fade
                    var p = d.Rt.anchoredPosition;
                    d.Rt.anchoredPosition = new Vector2(p.x + Mathf.Sign(p.x) * dt * 900f, p.y - dt * 600f);
                    d.SetAlpha(Mathf.MoveTowards(d.GetComponent<CanvasGroup>().alpha, 0f, dt * 5f));
                }
            }
            if (_resolveKind == 1) _char.Target = Vector2.Lerp(_char.Target, CharHome + new Vector2(0, -30), dt * 6f);
        }

        void AnimateFinger()
        {
            // pulsing "tap here" ring over the right door
            var door = _doors[_spec.answerSlot].Rt;
            float t = (Time.unscaledTime * 1.4f) % 1f;
            _hintFinger.anchoredPosition = door.anchoredPosition + new Vector2(0, -door.sizeDelta.y * 0.15f);
            _hintFinger.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 2f)));
            _hintFinger.GetComponent<Image>().color = Color.white.A(t < 0.6f ? 0.85f : (1f - t) * 2f);
        }

        // ------------------------------------------------------------------ death / revive / end

        void BeginDying()
        {
            if (_state == State.Dying) return;
            _state = State.Dying;
            _input.Enabled = false;
            _char.SetFace(Face.Dead, 99f);
            _char.Spin = 80f;
            _char.Kick(-8f);
            _fx.Shake(30f);
            _fx.Flash(Pal.Bad, 0.35f);
            if (_run.CanRevive && RequestRevive != null) StartCoroutine(AskRevive());
            else EndRun("aura_zero");
        }

        IEnumerator AskRevive()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            RequestRevive(_run, revived =>
            {
                if (!revived) { EndRun("aura_zero"); return; }
                _run.Revive();
                Analytics.Log("revive_used", ("beat", _run.Beat));
                _char.SetFace(Face.Happy, 0.8f);
                _char.Kick(8f);
                _fx.Burst(CharHome, 40, Pal.Mint, Pal.Yellow, Art.Heart, 1200, 36);
                _fx.Popup(CharHome + new Vector2(0, 260), "REVIVED +" + (int)T.reviveAura, Pal.Mint, 80, 1.2f);
                StartCoroutine(ResumeAfter(0.7f));
            });
        }

        IEnumerator ResumeAfter(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            _input.Clear();
            _input.Enabled = true;
            NextBeat();
        }

        void EndRun(string cause)
        {
            if (_state == State.Done) return;
            _state = State.Done;
            _input.Enabled = false;
            AdManager.Instance.InRun = false;
            AudioManager.Instance?.SetComboLayer(false);
            AudioManager.Instance?.SetLowAura(false);
            AudioManager.Instance?.SetMusic(0.3f);
            if (cause == "complete")
            {
                _char.SetFace(Face.Cool, 99f);
                _fx.Burst(CharHome, 50, Pal.Yellow, Pal.Pink, Art.Star, 1400, 36);
                _fx.Popup(new Vector2(0, 200), "DROP COMPLETE", Pal.Yellow, 96, 1.3f);
                AudioManager.Instance?.PlayBanner();
            }
            else AudioManager.Instance?.PlayTrombone();
            Analytics.Log("run_end", ("mode", _run.Mode.ToString()), ("score", _run.Score), ("beats", _run.History.Count), ("cause", cause), ("best_combo", _run.BestStreak));
            StartCoroutine(FinishAfter(1.3f));
        }

        IEnumerator FinishAfter(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            OnRunEnded?.Invoke(_run);
        }

        // ------------------------------------------------------------------ HUD

        void ShowCaption(string text)
        {
            _caption.text = text;
            _captionT = 1.1f;
            _caption.rectTransform.localScale = Vector3.one * 0.5f;
        }

        void ShowBanner(VibeShift s)
        {
            _banner.gameObject.SetActive(true);
            _bannerT = 0f;
            _bannerTitle.text = "VIBE SHIFT: " + ShiftName(s);
            _bannerSub.text = ShiftRule(s);
            AudioManager.Instance?.PlayBanner();
            Analytics.Log("vibe_shift", ("shift", s.ToString()), ("beat", _run.Beat));
        }

        public static string ShiftName(VibeShift s)
        {
            switch (s)
            {
                case VibeShift.OppositeDay: return "OPPOSITE DAY";
                case VibeShift.MirrorMode: return "MIRROR MODE";
                case VibeShift.SpeedBlink: return "SPEED BLINK";
                case VibeShift.SoundOnly: return "SOUND ONLY";
                default: return "";
            }
        }

        public static string ShiftRule(VibeShift s)
        {
            switch (s)
            {
                case VibeShift.OppositeDay: return "pick the WRONG answer";
                case VibeShift.MirrorMode: return "the door text is mirrored. read it anyway";
                case VibeShift.SpeedBlink: return "doors vanish halfway. read fast";
                case VibeShift.SoundOnly: return "listen, don't look";
                default: return "";
            }
        }

        static string ShiftChip(VibeShift s) => s == VibeShift.None ? "" : ShiftName(s) + " · " + ShiftRule(s);

        void AnimateHud(float dt)
        {
            if (_captionT > 0)
            {
                _captionT -= dt;
                float k = _caption.rectTransform.localScale.x;
                _caption.rectTransform.localScale = Vector3.one * Mathf.Lerp(k, 1f, dt * 16f);
                var c = _caption.color; c.a = Mathf.Clamp01(_captionT / 0.25f); _caption.color = c;
            }
            if (_banner.gameObject.activeSelf)
            {
                _bannerT += dt;
                float x = _bannerT < 0.15f ? Mathf.Lerp(1400f, 0f, _bannerT / 0.15f) : _bannerT > 0.68f ? Mathf.Lerp(0f, -1400f, (_bannerT - 0.68f) / 0.12f) : 0f;
                _banner.anchoredPosition = new Vector2(x, 120);
            }
            _promptCard.localScale = Vector3.Lerp(_promptCard.localScale, Vector3.one, dt * 12f);
            _comboPunch = Mathf.MoveTowards(_comboPunch, 0, dt * 3f);
            _comboCard.localScale = Vector3.one * (1f + _comboPunch * 0.35f);
        }

        void UpdateHud(bool instant)
        {
            if (_run == null) return;
            float k = Mathf.Clamp01(_run.Aura / T.maxAura);
            var rt = _auraFill.rectTransform;
            float cur = instant ? k : Mathf.Lerp(rt.anchorMax.x, k, Time.unscaledDeltaTime * 12f);
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, cur), 1);
            _auraFill.enabled = cur > 0.01f;
            bool low = _run.IsLowAura;
            _auraFill.color = low ? Pal.Bad : k > 0.6f ? Pal.Mint : Pal.Yellow;
            _auraText.text = "AURA " + Mathf.CeilToInt(_run.Aura);
            _auraEdge.color = low ? Color.Lerp(Pal.Ink, Pal.Bad, (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f) : Pal.Ink;
            _comboText.text = "x" + _run.Multiplier;
            _comboText.color = _run.Multiplier >= 4 ? Pal.Pink : _run.Multiplier >= 2 ? Pal.Yellow : Pal.Paper;
            _scoreText.text = _run.Score.ToString("N0") + (_run.Streak > 1 ? "  ·  " + _run.Streak + " streak" : "");
            float va = low && _run.Alive ? 0.14f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f : 0f;
            _vignette.color = new Color(0.85f, 0.1f, 0.25f, Mathf.Lerp(_vignette.color.a, va, Time.unscaledDeltaTime * 8f));
            AudioManager.Instance?.SetLowAura(low && _run.Alive);
            _hall.Speed = 0.5f + (1.4f / Mathf.Max(0.3f, _run.DecisionWindow(Mathf.Max(1, _run.Beat)))) * 0.4f;
        }

        // ------------------------------------------------------------------ pause

        public void Pause()
        {
            if (!Active || _state == State.Dying) return;
            _paused = true;
            _pauseOverlay.SetActive(true);
            AudioManager.Instance?.SetMusic(0.15f);
        }

        void Resume()
        {
            _paused = false;
            _pauseOverlay.SetActive(false);
            _input.Clear();
            AudioManager.Instance?.SetMusic(0.55f);
        }

        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnApplicationFocus(bool focus) { if (!focus && !Application.isEditor) Pause(); }

        // ------------------------------------------------------------------ test hooks

        /// <summary>Smoke-test/autoplay hook: answer the current beat correctly (respecting shifts).</summary>
        public void DebugAutoAnswer(bool correct)
        {
            if (_state != State.Window) return;
            int want = _spec.answerSlot;
            if (_spec.shift == VibeShift.OppositeDay) want = (want + 1) % _spec.DoorCount;
            if (!correct) want = _spec.shift == VibeShift.OppositeDay ? _spec.answerSlot : (want + 1) % _spec.DoorCount;
            OnPick(want, Mathf.Clamp01(_elapsed / _window));
        }

        public bool InDecisionWindow => _state == State.Window;
    }
}
