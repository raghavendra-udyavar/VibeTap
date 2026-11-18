using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VibeRun.Core;
using VibeRun.Gameplay;
using VibeRun.Services;

namespace VibeRun.UI
{
    public abstract class ScreenBase
    {
        public RectTransform Root;
        public bool Visible => Root.gameObject.activeSelf;
        public virtual void Hide() => Root.gameObject.SetActive(false);

        protected RectTransform MakeRoot(Transform parent, string name, Color? bg)
        {
            Root = UIKit.Rect(parent, name);
            UIKit.Fill(Root);
            if (bg.HasValue)
            {
                var img = Root.gameObject.AddComponent<Image>();
                img.color = bg.Value;
                img.raycastTarget = true;
            }
            Root.gameObject.SetActive(false);
            return Root;
        }

        protected static Button Btn(Transform p, string label, Color bg, Color fg, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, int font = 48)
        {
            var b = UIKit.Button(p, label, label, bg, fg, onClick, font);
            UIKit.Place((RectTransform)b.transform, anchor, pos, size);
            return b;
        }

        protected static Text Label(Transform p, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, bool sticker = false)
        {
            var t = UIKit.Text(p, "label", text, size, color);
            UIKit.Place(t.rectTransform, anchor, pos, box);
            if (sticker) UIKit.Sticker(t, Pal.Ink, Mathf.Max(3f, size / 14f));
            return t;
        }
    }

    // ===================================================================== HOME

    public sealed class HomeScreen : ScreenBase
    {
        readonly Action _onPlay, _onDaily, _onShop, _onChallenge;
        Text _level, _vibes, _dailySub, _best, _soundLabel;
        Image _xpFill;
        Button _dailyBtn;
        CharacterView _char;
        RectTransform _logo;

        public HomeScreen(Transform parent, Action onPlay, Action onDaily, Action onShop, Action onChallenge)
        {
            _onPlay = onPlay; _onDaily = onDaily; _onShop = onShop; _onChallenge = onChallenge;
            MakeRoot(parent, "Home", null);
            Hallway.Create(Root).Speed = 0.35f;

            // top bar: level/title + xp, vibes
            var lvl = UIKit.Card(Root, "levelCard", Pal.Bg2, 6f);
            UIKit.Place((RectTransform)lvl.transform.parent, new Vector2(0, 1), new Vector2(60 + 260, -100), new Vector2(520, 110));
            _level = UIKit.Text(lvl.transform, "t", "", 34, Pal.Paper, TextAnchor.UpperLeft);
            _level.rectTransform.offsetMin = new Vector2(24, 0); _level.rectTransform.offsetMax = new Vector2(-16, -12);
            var xpTrack = UIKit.Image(lvl.transform, "xp", Pal.Ink, Art.RoundedSmall);
            UIKit.Stretch(xpTrack.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 16), new Vector2(-24, 36));
            _xpFill = UIKit.Image(xpTrack.transform, "fill", Pal.Yellow, Art.RoundedSmall);
            UIKit.Stretch(_xpFill.rectTransform, Vector2.zero, new Vector2(0.3f, 1), Vector2.zero, Vector2.zero);

            var vib = UIKit.Card(Root, "vibesCard", Pal.Bg2, 6f);
            UIKit.Place((RectTransform)vib.transform.parent, new Vector2(1, 1), new Vector2(-60 - 160, -100), new Vector2(320, 110));
            var star = UIKit.Image(vib.transform, "star", Pal.Yellow, Art.Star);
            UIKit.Place(star.rectTransform, new Vector2(0, 0.5f), new Vector2(52, 0), new Vector2(64, 64));
            _vibes = UIKit.Text(vib.transform, "t", "0", 48, Pal.Paper, TextAnchor.MiddleLeft);
            _vibes.rectTransform.offsetMin = new Vector2(96, 0);

            // logo
            _logo = UIKit.Rect(Root, "logo");
            UIKit.Place(_logo, new Vector2(0.5f, 0.5f), new Vector2(0, 600), new Vector2(1000, 360));
            var l1 = UIKit.Text(_logo, "vibe", "VIBE", 230, Pal.Yellow);
            l1.rectTransform.offsetMin = new Vector2(-60, 120);
            UIKit.Sticker(l1, Pal.Ink, 12f);
            var l2 = UIKit.Text(_logo, "run", "RUN", 230, Pal.Pink);
            l2.rectTransform.offsetMax = new Vector2(80, -120);
            UIKit.Sticker(l2, Pal.Ink, 12f);
            _logo.localRotation = Quaternion.Euler(0, 0, -4f);

            _char = CharacterView.Create(Root, 300);
            _char.Snap(new Vector2(0, 120));

            _best = Label(Root, "", 36, Pal.Muted, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(900, 60));

            var play = Btn(Root, "TAP TO VIBE", Pal.Yellow, Pal.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(780, 210), () => _onPlay(), 84);
            _dailyBtn = Btn(Root, "DAILY DROP", Pal.Mint, Pal.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -540), new Vector2(780, 170), () => _onDaily(), 60);
            var dl = _dailyBtn.GetComponentInChildren<Text>();
            dl.rectTransform.offsetMin = new Vector2(0, 40);
            var fill = dl.transform.parent;
            _dailySub = UIKit.Text(fill, "sub", "", 30, Pal.Ink);
            _dailySub.fontStyle = FontStyle.Normal;
            _dailySub.rectTransform.offsetMax = new Vector2(0, -98);

            // bottom bar
            Btn(Root, "SHOP", Pal.Pink, Pal.Paper, new Vector2(0.5f, 0), new Vector2(-330, 170), new Vector2(300, 140), () => _onShop(), 46);
            Btn(Root, "CHALLENGE", Pal.Good, Pal.Paper, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(320, 140), () => _onChallenge(), 42);
            var snd = Btn(Root, "SOUND", Pal.Bg2, Pal.Paper, new Vector2(0.5f, 0), new Vector2(330, 170), new Vector2(300, 140), ToggleSound, 42);
            _soundLabel = snd.GetComponentInChildren<Text>();
        }

        void ToggleSound()
        {
            var p = SaveService.Profile;
            p.muted = !p.muted;
            SaveService.Save();
            AudioManager.Instance?.SetMuted(p.muted);
            Refresh();
        }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Refresh();
        }

        public void Refresh()
        {
            var cfg = GameConfig.Instance;
            var p = SaveService.Profile;
            int level = Progression.LevelFor(p.xp);
            _level.text = "LV " + level + "  ·  " + Progression.TitleFor(level, cfg.Captions.titles);
            _xpFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, Progression.LevelProgress(p.xp)), 1);
            _vibes.text = p.vibes.ToString("N0");
            _best.text = p.bestScore > 0 ? "best " + p.bestScore.ToString("N0") + "  ·  " + p.runs + " runs" : "one thumb. pick the vibe. don't freeze.";
            _char.ApplyFit(cfg.Cosmetic(p.fit) ?? cfg.FirstOfKind("fit"));
            _soundLabel.text = p.muted ? "MUTED" : "SOUND";
            var today = DateTime.Now;
            int streak = Daily.VisibleStreak(p, today);
            if (Daily.CanPlayDrop(p, today))
                _dailySub.text = (streak > 0 ? "streak " + streak + "  ·  " : "") + "today's 30 prompts are live";
            else
            {
                var left = today.Date.AddDays(1) - today;
                _dailySub.text = "done  ·  streak " + streak + "  ·  next in " + left.Hours + "h " + left.Minutes + "m";
            }
        }

        public void Tick(float dt)
        {
            if (!Visible) return;
            float t = Time.unscaledTime;
            _logo.localRotation = Quaternion.Euler(0, 0, -4f + Mathf.Sin(t * 1.6f) * 1.5f);
            _logo.localScale = Vector3.one * (1f + Mathf.Sin(t * 3.2f) * 0.015f);
            if (Mathf.Repeat(t, 3.5f) < Time.unscaledDeltaTime) _char.Kick(3f);
            if (Mathf.Repeat(t, 1f) < Time.unscaledDeltaTime && !Daily.CanPlayDrop(SaveService.Profile, DateTime.Now)) Refresh();
        }
    }

    // ===================================================================== RESULT

    public sealed class ResultScreen : ScreenBase
    {
        readonly Action _onAgain, _onHome;
        readonly MonoBehaviour _host;
        RectTransform _card, _gridRoot;
        Image _cardFill, _frameStrip;
        Text _mode, _percent, _archetype, _roast, _stats, _titleChip, _footer, _rewards, _gridLabel, _shareLabel3;
        CharacterView _char;
        Button _copyBtn;
        RectTransform _buttons;
        string _shareText;
        RunMode _modeShown;
        Action<string> _toast;
        float _t;

        public ResultScreen(Transform parent, MonoBehaviour host, Action onAgain, Action onHome, Action<string> toast)
        {
            _host = host; _onAgain = onAgain; _onHome = onHome; _toast = toast;
            MakeRoot(parent, "Result", new Color(0.05f, 0.04f, 0.08f, 0.94f));

            // the shareable card, 9:16
            _card = UIKit.Rect(Root, "card");
            UIKit.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(680, 1209));
            _cardFill = UIKit.Card(_card, "frame", Pal.Paper, 10f);
            UIKit.Fill((RectTransform)_cardFill.transform.parent);
            var c = _cardFill.transform;
            _frameStrip = UIKit.Image(c, "strip", Pal.Yellow, Art.Rounded);
            UIKit.Stretch(_frameStrip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, 0));
            _mode = UIKit.Text(c, "mode", "VIBE RUN", 36, Pal.Ink);
            UIKit.Stretch(_mode.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -88), new Vector2(0, 0));
            var inner = UIKit.Image(c, "photo", Pal.Bg2, Art.Rounded);
            UIKit.Stretch(inner.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(34, -720), new Vector2(-34, -110));
            Label(inner.transform, "your aura:", 34, Pal.Muted, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 50));
            _percent = Label(inner.transform, "94%", 120, Pal.Yellow, new Vector2(0.5f, 1), new Vector2(0, -145), new Vector2(560, 140), true);
            _archetype = Label(inner.transform, "Chaotic Neutral", 60, Pal.Pink, new Vector2(0.5f, 1), new Vector2(0, -255), new Vector2(580, 90), true);
            _archetype.resizeTextForBestFit = true; _archetype.resizeTextMinSize = 32; _archetype.resizeTextMaxSize = 62;
            _char = CharacterView.Create(inner.transform, 200);
            var crt = (RectTransform)_char.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0);
            _char.Snap(new Vector2(0, 160));
            _char.Bob = true;
            _gridRoot = UIKit.Rect(inner.transform, "grid");
            UIKit.Place(_gridRoot, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(330, 400));
            _gridLabel = Label(inner.transform, "", 30, Pal.Paper, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(580, 60));

            var bubble = UIKit.Card(c, "roast", Color.white, 6f, null, false);
            UIKit.Stretch((RectTransform)bubble.transform.parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -930), new Vector2(-40, -750));
            _roast = UIKit.Text(bubble.transform, "t", "", 36, Pal.Ink);
            _roast.fontStyle = FontStyle.BoldAndItalic;
            _roast.rectTransform.offsetMin = new Vector2(24, 12); _roast.rectTransform.offsetMax = new Vector2(-24, -12);
            _roast.resizeTextForBestFit = true; _roast.resizeTextMinSize = 24; _roast.resizeTextMaxSize = 38;

            _stats = UIKit.Text(c, "stats", "", 40, Pal.Ink);
            UIKit.Stretch(_stats.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -1060), new Vector2(-30, -950));
            _titleChip = UIKit.Text(c, "title", "", 32, Pal.Purple);
            UIKit.Stretch(_titleChip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -1120), new Vector2(-30, -1060));
            _footer = UIKit.Text(c, "footer", "", 26, Pal.Muted);
            _footer.fontStyle = FontStyle.Normal;
            UIKit.Stretch(_footer.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 14), new Vector2(-30, 70));

            _rewards = Label(Root, "", 40, Pal.Yellow, new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(1000, 60), true);

            _buttons = UIKit.Rect(Root, "buttons");
            UIKit.Fill(_buttons);
            Btn(_buttons, "STORY 9:16", Pal.Pink, Pal.Paper, new Vector2(0.5f, 0.5f), new Vector2(-330, -520), new Vector2(310, 130), () => Share(false), 38);
            Btn(_buttons, "CHAT 1:1", Pal.Purple, Pal.Paper, new Vector2(0.5f, 0.5f), new Vector2(0, -520), new Vector2(310, 130), () => Share(true), 38);
            _copyBtn = Btn(_buttons, "COPY LINK", Pal.Good, Pal.Paper, new Vector2(0.5f, 0.5f), new Vector2(330, -520), new Vector2(310, 130), CopyText, 38);
            _shareLabel3 = _copyBtn.GetComponentInChildren<Text>();
            Btn(_buttons, "PLAY AGAIN", Pal.Yellow, Pal.Ink, new Vector2(0.5f, 0.5f), new Vector2(-150, -720), new Vector2(620, 170), () => _onAgain(), 66);
            Btn(_buttons, "HOME", Pal.Bg2, Pal.Paper, new Vector2(0.5f, 0.5f), new Vector2(355, -720), new Vector2(300, 170), () => _onHome(), 48);
        }

        public void Show(RunState run, ResultCard card, string rewardsLine, bool replayAllowed)
        {
            var cfg = GameConfig.Instance;
            var p = SaveService.Profile;
            Root.gameObject.SetActive(true);
            _modeShown = run.Mode;
            _t = 0;
            ApplyFrame(cfg.Cosmetic(p.frame) ?? cfg.FirstOfKind("frame"));
            int level = Progression.LevelFor(p.xp);
            string title = Progression.TitleFor(level, cfg.Captions.titles);
            bool daily = run.Mode == RunMode.Daily;
            string date = Daily.DayKey(DateTime.Now);
            _mode.text = daily ? "VIBE RUN  ·  DAILY DROP " + date : "VIBE RUN  ·  CLASSIC";
            _percent.text = card.archetypePercent + "%";
            _archetype.text = card.archetype;
            _roast.text = "\"" + card.roast + "\"";
            _stats.text = daily
                ? card.correct + "/30 correct  ·  " + card.score.ToString("N0") + " pts"
                : card.score.ToString("N0") + " pts  ·  best combo " + card.bestCombo + "  ·  " + card.beats + " doors";
            _titleChip.text = title.ToUpperInvariant() + "  ·  LV " + level;
            _footer.text = daily ? "more unhinged than " + card.percentile + "% of players*  ·  viberun" : "run code " + card.seedCode + "  ·  play my exact run in Vibe Run";
            _rewards.text = rewardsLine;
            _char.gameObject.SetActive(!daily);
            _char.ApplyFit(cfg.Cosmetic(p.fit) ?? cfg.FirstOfKind("fit"));
            _char.SetFace(card.correct >= card.beats * 0.8f ? Face.Cool : Face.Happy, 99f);
            BuildGrid(daily ? run.History : null);
            _gridLabel.text = daily ? "" : "";
            _shareLabel3.text = daily ? "COPY GRID" : "COPY LINK";
            _shareText = ResultLogic.ShareText(card, run.Mode, title, date);
            _buttons.Find("PLAY AGAIN").gameObject.SetActive(replayAllowed);
            _card.localScale = Vector3.one * 0.6f;
        }

        void ApplyFrame(CosmeticDef frame)
        {
            _cardFill.color = Art.Hex(frame?.color, Pal.Paper);
            _frameStrip.color = frame?.style == "notes" ? Art.Hex(frame.accent, Pal.Yellow) : frame?.style == "receipt" ? Pal.Hex("#E9E4D8") : Pal.Yellow;
        }

        void BuildGrid(List<BeatResult> history)
        {
            for (int i = _gridRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_gridRoot.GetChild(i).gameObject);
            _gridRoot.gameObject.SetActive(history != null);
            if (history == null) return;
            const int cols = 5, rows = 6; const float cell = 58f, gap = 10f;
            for (int i = 0; i < cols * rows; i++)
            {
                Color c = Pal.Hex("#3A3550");
                if (i < history.Count)
                {
                    var h = history[i];
                    c = h.outcome == Outcome.Correct ? (h.fast ? Pal.Yellow : Pal.Good) : Pal.Bad;
                }
                var sq = UIKit.Image(_gridRoot, "c" + i, c, Art.RoundedSmall);
                int x = i % cols, y = i / cols;
                UIKit.Place(sq.rectTransform, new Vector2(0.5f, 1f), new Vector2((x - 2) * (cell + gap), -cell * 0.5f - y * (cell + gap)), new Vector2(cell, cell));
            }
        }

        public void Tick(float dt)
        {
            if (!Visible) return;
            _t += dt;
            float k = Mathf.Clamp01(_t / 0.35f);
            float s = k < 1 ? Mathf.LerpUnclamped(0.6f, 1f, 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2)) : 1f;
            _card.localScale = Vector3.one * s;
            _card.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-6f, -1.5f, k));
        }

        void Share(bool square)
        {
            _card.localRotation = Quaternion.identity;
            _card.localScale = Vector3.one;
            _host.StartCoroutine(ShareService.CaptureAndShare(_host, _card, square, _shareText, path =>
                _toast(ShareService.NativeShare != null ? "sharing..." : "card saved + text copied")));
            _t = 10f; // keep the card upright while capturing
        }

        void CopyText()
        {
            ShareService.CopyText(_shareText);
            _toast(_modeShown == RunMode.Daily ? "grid copied. paste it in the group chat" : "link copied. make them play your run");
        }
    }

    // ===================================================================== REVIVE

    public sealed class RevivePopup : ScreenBase
    {
        Action<bool> _cb;
        Button _token, _ad;
        Image _timerFill;
        float _timeLeft;
        const float Timeout = 6f;

        public RevivePopup(Transform parent)
        {
            MakeRoot(parent, "Revive", new Color(0, 0, 0, 0.72f));
            var panel = UIKit.Card(Root, "panel", Pal.Bg2, 10f);
            UIKit.Place((RectTransform)panel.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(880, 900));
            var pt = panel.transform;
            Label(pt, "AURA DEPLETED", 80, Pal.Bad, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 110), true);
            Label(pt, "one revive per run.\nback in with +50 aura?", 42, Pal.Paper, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(800, 130));
            _token = Btn(pt, "FREE TOKEN (daily)", Pal.Mint, Pal.Ink, new Vector2(0.5f, 1), new Vector2(0, -420), new Vector2(720, 140), () => Choose("token"), 44);
            _ad = Btn(pt, "WATCH AD TO REVIVE", Pal.Yellow, Pal.Ink, new Vector2(0.5f, 1), new Vector2(0, -580), new Vector2(720, 140), () => Choose("ad"), 44);
            Btn(pt, "nah, show my card", Pal.Bg, Pal.Muted, new Vector2(0.5f, 1), new Vector2(0, -730), new Vector2(720, 110), () => Choose("no"), 38);
            var track = UIKit.Image(pt, "timer", Pal.Ink, Art.RoundedSmall);
            UIKit.Stretch(track.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 40), new Vector2(-60, 62));
            _timerFill = UIKit.Image(track.transform, "fill", Pal.Pink, Art.RoundedSmall);
            UIKit.Fill(_timerFill.rectTransform);
        }

        public void Show(Action<bool> cb)
        {
            _cb = cb;
            _timeLeft = Timeout;
            Root.gameObject.SetActive(true);
            bool token = Daily.HasReviveToken(SaveService.Profile, DateTime.Now);
            _token.gameObject.SetActive(token);
            _ad.gameObject.SetActive(AdManager.Instance.Provider.RewardedReady);
        }

        void Choose(string which)
        {
            if (_cb == null) return;
            var cb = _cb; _cb = null;
            Hide();
            if (which == "token")
            {
                Daily.UseReviveToken(SaveService.Profile, DateTime.Now);
                SaveService.Save();
                cb(true);
            }
            else if (which == "ad") AdManager.Instance.Rewarded("revive", ok => cb(ok));
            else cb(false);
        }

        public void Tick(float dt)
        {
            if (!Visible || _cb == null) return;
            _timeLeft -= dt;
            _timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_timeLeft / Timeout), 1);
            if (_timeLeft <= 0) Choose("no");
        }
    }

    // ===================================================================== SHOP

    public sealed class ShopScreen : ScreenBase
    {
        readonly Action _onBack;
        readonly Action<string> _toast;
        RectTransform _grid;
        Text _vibes;
        string _tab = "fit";
        readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>();

        public ShopScreen(Transform parent, Action onBack, Action<string> toast)
        {
            _onBack = onBack; _toast = toast;
            MakeRoot(parent, "Shop", Pal.Bg);
            Label(Root, "SHOP", 110, Pal.Pink, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 140), true);
            _vibes = Label(Root, "", 44, Pal.Yellow, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(900, 60));
            Label(Root, "cosmetic only. no loot boxes. prices upfront.", 30, Pal.Muted, new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(900, 50));
            string[] kinds = { "fit", "trail", "frame" };
            string[] names = { "FITS", "TRAILS", "FRAMES" };
            for (int i = 0; i < 3; i++)
            {
                string k = kinds[i];
                _tabs[k] = Btn(Root, names[i], Pal.Bg2, Pal.Paper, new Vector2(0.5f, 1), new Vector2((i - 1) * 320, -400), new Vector2(300, 110), () => { _tab = k; Rebuild(); }, 42);
            }
            var content = UIKit.Rect(Root, "content");
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(470, 400); grid.spacing = new Vector2(30, 30);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(0, 0, 20, 40);
            var scrollGo = UIKit.Rect(Root, "scroll");
            UIKit.Stretch(scrollGo, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 240), new Vector2(-40, -470));
            var bg = scrollGo.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);
            scrollGo.gameObject.AddComponent<RectMask2D>();
            var sr = scrollGo.gameObject.AddComponent<ScrollRect>();
            content.SetParent(scrollGo, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped;
            _grid = content;
            Btn(Root, "BACK", Pal.Yellow, Pal.Ink, new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(500, 140), () => _onBack(), 54);
        }

        public void Show() { Root.gameObject.SetActive(true); Rebuild(); }

        void Rebuild()
        {
            var cfg = GameConfig.Instance;
            var p = SaveService.Profile;
            _vibes.text = p.vibes.ToString("N0") + " vibes";
            foreach (var kv in _tabs)
                kv.Value.targetGraphic.color = kv.Key == _tab ? Pal.Purple : Pal.Bg2;
            for (int i = _grid.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_grid.GetChild(i).gameObject);
            foreach (var item in cfg.Cosmetics)
            {
                if (item.kind != _tab) continue;
                BuildItem(item, p);
            }
        }

        void BuildItem(CosmeticDef item, Profile p)
        {
            var cell = UIKit.Card(_grid, item.id, Pal.Bg2, 6f, null, false);
            var root = (RectTransform)cell.transform.parent;
            var t = cell.transform;
            // preview
            var prev = UIKit.Rect(t, "preview");
            UIKit.Place(prev, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(200, 200));
            if (item.kind == "fit")
            {
                var cv = CharacterView.Create(prev, 160);
                cv.Snap(Vector2.zero);
                cv.ApplyFit(item);
            }
            else if (item.kind == "trail")
            {
                Sprite sp = item.style == "hearts" ? Art.Heart : item.style == "pixel" ? Art.Square : item.style == "flame" || item.style == "rain" ? Art.Drop : Art.Star;
                for (int i = 0; i < 6; i++)
                {
                    var im = UIKit.Image(prev, "s", i % 2 == 0 ? Art.Hex(item.color) : Art.Hex(item.accent), sp);
                    UIKit.Place(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(i * 1.05f) * 60, Mathf.Sin(i * 1.05f) * 60), Vector2.one * (40 + i * 6));
                }
            }
            else
            {
                var fr = UIKit.Card(prev, "frame", Art.Hex(item.color), 6f);
                UIKit.Place((RectTransform)fr.transform.parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 200));
                var strip = UIKit.Image(fr.transform, "strip", Art.Hex(item.accent), Art.RoundedSmall);
                UIKit.Stretch(strip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), Vector2.zero);
            }
            var name = UIKit.Text(t, "name", item.name, 36, Pal.Paper);
            UIKit.Stretch(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 130), new Vector2(-10, 190));
            bool owned = Progression.Owns(p, item);
            bool equipped = Equipped(p, item);
            string label = equipped ? "EQUIPPED" : owned ? "EQUIP" : item.price + " vibes";
            Color bg = equipped ? Pal.Bg : owned ? Pal.Mint : p.vibes >= item.price ? Pal.Yellow : Pal.Hex("#5A5470");
            var b = UIKit.Button(t, "buy", label, bg, Pal.Ink, () => OnItem(item), 38);
            UIKit.Stretch((RectTransform)b.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 20), new Vector2(-30, 120));
        }

        static bool Equipped(Profile p, CosmeticDef c) => c.kind == "fit" ? p.fit == c.id : c.kind == "trail" ? p.trail == c.id : p.frame == c.id;

        void OnItem(CosmeticDef c)
        {
            var p = SaveService.Profile;
            if (!Progression.Owns(p, c))
            {
                if (!Progression.TryBuy(p, c)) { _toast("need " + (c.price - p.vibes) + " more vibes. go run."); return; }
                Analytics.Log("purchase", ("item", c.id), ("currency", "vibes"), ("price", c.price));
                AudioManager.Instance?.PlayCoin();
                _toast("unlocked " + c.name);
            }
            if (c.kind == "fit") p.fit = c.id; else if (c.kind == "trail") p.trail = c.id; else p.frame = c.id;
            SaveService.Save();
            Rebuild();
        }
    }

    // ===================================================================== CHALLENGE

    public sealed class ChallengePopup : ScreenBase
    {
        readonly Action<uint> _onPlay;
        readonly Action<string> _toast;
        InputField _input;
        Text _last;
        string _lastCode;

        public ChallengePopup(Transform parent, Action<uint> onPlay, Action<string> toast)
        {
            _onPlay = onPlay; _toast = toast;
            MakeRoot(parent, "Challenge", new Color(0, 0, 0, 0.75f));
            var panel = UIKit.Card(Root, "panel", Pal.Bg2, 10f);
            UIKit.Place((RectTransform)panel.transform.parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 980));
            var pt = panel.transform;
            Label(pt, "CHALLENGE", 90, Pal.Good, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 120), true);
            Label(pt, "enter a friend's run code to play\ntheir exact doors, in their exact order", 36, Pal.Paper, new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(820, 110));

            var field = UIKit.Card(pt, "field", Pal.Paper, 6f);
            var frt = (RectTransform)field.transform.parent;
            UIKit.Place(frt, new Vector2(0.5f, 1), new Vector2(0, -380), new Vector2(700, 130));
            var text = UIKit.Text(field.transform, "text", "", 64, Pal.Ink);
            text.supportRichText = false;
            var ph = UIKit.Text(field.transform, "placeholder", "ABC2DEF", 64, Pal.Ink.A(0.25f));
            _input = field.gameObject.AddComponent<InputField>();
            field.raycastTarget = true;
            _input.textComponent = text;
            _input.placeholder = ph;
            _input.characterLimit = 7;
            _input.contentType = InputField.ContentType.Alphanumeric;
            _input.characterValidation = InputField.CharacterValidation.Alphanumeric;
            _input.onValueChanged.AddListener(v => { var u = v.ToUpperInvariant(); if (u != v) _input.text = u; });

            Btn(pt, "PLAY THEIR RUN", Pal.Yellow, Pal.Ink, new Vector2(0.5f, 1), new Vector2(0, -540), new Vector2(700, 140), Play, 50);
            _last = Label(pt, "", 34, Pal.Muted, new Vector2(0.5f, 1), new Vector2(0, -670), new Vector2(820, 60));
            Btn(pt, "COPY MY LAST RUN LINK", Pal.Good, Pal.Paper, new Vector2(0.5f, 1), new Vector2(0, -770), new Vector2(700, 110), CopyMine, 38);
            Btn(pt, "CLOSE", Pal.Bg, Pal.Muted, new Vector2(0.5f, 1), new Vector2(0, -900), new Vector2(400, 100), Hide, 38);
        }

        public void Show(string lastCode)
        {
            _lastCode = lastCode;
            _last.text = string.IsNullOrEmpty(lastCode) ? "finish a run to get your own code" : "your last run: " + lastCode;
            Root.gameObject.SetActive(true);
        }

        void Play()
        {
            if (!ResultLogic.TryParseSeedCode(_input.text, out uint seed)) { _toast("codes are 7 letters/numbers, like " + ResultLogic.SeedCode(12345)); return; }
            Hide();
            _onPlay(seed);
        }

        void CopyMine()
        {
            if (string.IsNullOrEmpty(_lastCode)) { _toast("no run yet. go vibe first."); return; }
            ShareService.CopyText("beat my Vibe Run: viberun://run?seed=" + _lastCode + "  (or enter code " + _lastCode + ")");
            _toast("link copied");
        }
    }

    // ===================================================================== TOAST

    public sealed class Toast
    {
        readonly Text _t;
        readonly RectTransform _rt;
        float _life;

        public Toast(Transform parent)
        {
            var card = UIKit.Card(parent, "toast", Pal.Ink, 4f, Pal.Paper, true);
            _rt = (RectTransform)card.transform.parent;
            UIKit.Place(_rt, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(940, 120));
            _t = UIKit.Text(card.transform, "t", "", 38, Pal.Paper);
            _t.rectTransform.offsetMin = new Vector2(20, 0); _t.rectTransform.offsetMax = new Vector2(-20, 0);
            _rt.gameObject.SetActive(false);
        }

        public void Show(string text, float seconds = 2.2f)
        {
            _t.text = text;
            _life = seconds;
            _rt.gameObject.SetActive(true);
            _rt.SetAsLastSibling();
        }

        public void Tick(float dt)
        {
            if (_life <= 0) return;
            _life -= dt;
            float y = _life > 0.25f ? -260f : Mathf.Lerp(80f, -260f, _life / 0.25f);
            _rt.anchoredPosition = new Vector2(0, y);
            if (_life <= 0) _rt.gameObject.SetActive(false);
        }
    }
}
