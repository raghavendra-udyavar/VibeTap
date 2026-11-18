using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VibeRun.Core;
using VibeRun.UI;

namespace VibeRun.Gameplay
{
    public enum Face { Idle, Happy, Shock, Dead, Cool }

    /// <summary>The blob avatar: big face, outline, and a fit accessory. Animated with simple springs.</summary>
    public sealed class CharacterView : MonoBehaviour
    {
        RectTransform _rt, _body, _accessory;
        Image _fill, _eyeL, _eyeR, _pupilL, _pupilR, _mouth, _cheekL, _cheekR;
        Text _xEyes;
        float _bob;
        public Vector2 Home;
        public Vector2 Target;
        public float Squash = 1f;
        float _squashVel;
        public float Spin;
        Face _face = Face.Idle;
        float _faceTimer;
        public bool Bob = true;

        public static CharacterView Create(Transform parent, float size)
        {
            var rt = UIKit.Rect(parent, "Character");
            rt.sizeDelta = new Vector2(size, size);
            var cv = rt.gameObject.AddComponent<CharacterView>();
            cv.Build(size);
            return cv;
        }

        void Build(float size)
        {
            _rt = (RectTransform)transform;
            var shadow = UIKit.Image(_rt, "shadow", new Color(0, 0, 0, 0.35f), Art.Circle);
            UIKit.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.52f), new Vector2(size * 0.9f, size * 0.22f));
            _body = UIKit.Rect(_rt, "body");
            UIKit.Fill(_body);
            _body.pivot = new Vector2(0.5f, 0f);
            _body.anchorMin = new Vector2(0, 0); _body.anchorMax = new Vector2(1, 1);
            _accessory = UIKit.Rect(_body, "accessory_back");
            UIKit.Fill(_accessory);
            var edge = UIKit.Image(_body, "edge", Pal.Ink, Art.Circle);
            UIKit.Fill(edge.rectTransform);
            _fill = UIKit.Image(_body, "fill", Pal.Yellow, Art.Circle);
            UIKit.Fill(_fill.rectTransform); _fill.rectTransform.offsetMin = new Vector2(10, 10); _fill.rectTransform.offsetMax = new Vector2(-10, -10);
            float e = size * 0.2f;
            _eyeL = Eye("eyeL", new Vector2(-size * 0.17f, size * 0.08f), e, out _pupilL);
            _eyeR = Eye("eyeR", new Vector2(size * 0.17f, size * 0.08f), e, out _pupilR);
            _cheekL = UIKit.Image(_body, "cheekL", Pal.Pink.A(0.45f), Art.Circle);
            UIKit.Place(_cheekL.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-size * 0.3f, -size * 0.1f), new Vector2(e * 0.8f, e * 0.5f));
            _cheekR = UIKit.Image(_body, "cheekR", Pal.Pink.A(0.45f), Art.Circle);
            UIKit.Place(_cheekR.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.3f, -size * 0.1f), new Vector2(e * 0.8f, e * 0.5f));
            _mouth = UIKit.Image(_body, "mouth", Pal.Ink, Art.RoundedSmall);
            UIKit.Place(_mouth.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.15f), new Vector2(size * 0.22f, size * 0.09f));
            _xEyes = UIKit.Text(_body, "xeyes", "x   x", (int)(size * 0.28f), Pal.Ink);
            _xEyes.rectTransform.offsetMin = new Vector2(0, size * 0.05f);
            _xEyes.gameObject.SetActive(false);
        }

        Image Eye(string name, Vector2 pos, float s, out Image pupil)
        {
            var eye = UIKit.Image(_body, name, Color.white, Art.Circle);
            UIKit.Place(eye.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(s, s * 1.15f));
            var ring = UIKit.Image(eye.rectTransform, "ring", Pal.Ink, Art.Ring);
            UIKit.Fill(ring.rectTransform); ring.rectTransform.offsetMin = new Vector2(-3, -3); ring.rectTransform.offsetMax = new Vector2(3, 3);
            pupil = UIKit.Image(eye.rectTransform, "pupil", Pal.Ink, Art.Circle);
            UIKit.Place(pupil.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -s * 0.05f), new Vector2(s * 0.5f, s * 0.55f));
            return eye;
        }

        /// <summary>Apply a fit: body colour plus an accessory drawn from primitive shapes.</summary>
        public void ApplyFit(CosmeticDef fit)
        {
            if (fit == null) return;
            _fill.color = Art.Hex(fit.color, Pal.Yellow);
            var accent = Art.Hex(fit.accent, Pal.Ink);
            for (int i = _accessory.childCount - 1; i >= 0; i--) Destroy(_accessory.GetChild(i).gameObject);
            var front = _body.Find("accessory_front");
            if (front != null) Destroy(front.gameObject);
            var fr = UIKit.Rect(_body, "accessory_front");
            UIKit.Fill(fr);
            float s = _rt.sizeDelta.x;
            Image Add(Transform p, Sprite sp, Color c, Vector2 pos, Vector2 size, float rot = 0)
            {
                var im = UIKit.Image(p, "acc", c, sp);
                UIKit.Place(im.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
                im.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                return im;
            }
            switch (fit.style)
            {
                case "hood":
                    Add(_accessory, Art.Circle, Pal.Ink, new Vector2(0, s * 0.06f), new Vector2(s * 1.22f, s * 1.18f));
                    Add(_accessory, Art.Circle, accent, new Vector2(0, s * 0.06f), new Vector2(s * 1.14f, s * 1.1f));
                    break;
                case "butterfly":
                    Add(fr, Art.Heart, accent, new Vector2(-s * 0.22f, s * 0.46f), new Vector2(s * 0.24f, s * 0.24f), 35);
                    Add(fr, Art.Heart, accent, new Vector2(s * 0.22f, s * 0.46f), new Vector2(s * 0.24f, s * 0.24f), -35);
                    break;
                case "beanie":
                    Add(fr, Art.Rounded, Pal.Ink, new Vector2(0, s * 0.4f), new Vector2(s * 0.8f, s * 0.32f));
                    Add(fr, Art.Rounded, accent, new Vector2(0, s * 0.4f), new Vector2(s * 0.74f, s * 0.26f));
                    Add(fr, Art.Circle, Color.white, new Vector2(0, s * 0.58f), new Vector2(s * 0.16f, s * 0.16f));
                    break;
                case "flower":
                    Add(fr, Art.Star, accent, new Vector2(s * 0.28f, s * 0.38f), new Vector2(s * 0.3f, s * 0.3f), 10);
                    Add(fr, Art.Circle, Pal.Yellow, new Vector2(s * 0.28f, s * 0.38f), new Vector2(s * 0.1f, s * 0.1f));
                    break;
                case "visor":
                    Add(fr, Art.Rounded, accent.A(0.9f), new Vector2(0, s * 0.09f), new Vector2(s * 0.74f, s * 0.2f));
                    break;
                case "glasses":
                    Add(fr, Art.Ring, accent, new Vector2(-s * 0.17f, s * 0.08f), new Vector2(s * 0.27f, s * 0.27f));
                    Add(fr, Art.Ring, accent, new Vector2(s * 0.17f, s * 0.08f), new Vector2(s * 0.27f, s * 0.27f));
                    Add(fr, Art.Square, accent, new Vector2(0, s * 0.1f), new Vector2(s * 0.08f, s * 0.025f));
                    break;
                case "crown":
                    for (int i = -1; i <= 1; i++)
                        Add(fr, Art.Triangle, accent, new Vector2(i * s * 0.14f, s * 0.53f + (i == 0 ? s * 0.04f : 0)), new Vector2(s * 0.2f, s * 0.24f));
                    Add(fr, Art.Rounded, accent, new Vector2(0, s * 0.43f), new Vector2(s * 0.5f, s * 0.1f));
                    break;
                case "halo":
                    Add(fr, Art.Ring, accent, new Vector2(0, s * 0.62f), new Vector2(s * 0.56f, s * 0.18f));
                    break;
                case "horns":
                    Add(fr, Art.Triangle, accent, new Vector2(-s * 0.28f, s * 0.48f), new Vector2(s * 0.18f, s * 0.24f), 18);
                    Add(fr, Art.Triangle, accent, new Vector2(s * 0.28f, s * 0.48f), new Vector2(s * 0.18f, s * 0.24f), -18);
                    break;
            }
        }

        public void SetFace(Face f, float hold = 0.5f)
        {
            _face = f;
            _faceTimer = hold;
            float s = _rt.sizeDelta.x;
            bool dead = f == Face.Dead;
            _xEyes.gameObject.SetActive(dead);
            _eyeL.gameObject.SetActive(!dead); _eyeR.gameObject.SetActive(!dead);
            var m = _mouth.rectTransform;
            switch (f)
            {
                case Face.Happy: m.sizeDelta = new Vector2(s * 0.3f, s * 0.16f); _pupilScale = 1.1f; break;
                case Face.Shock: m.sizeDelta = new Vector2(s * 0.14f, s * 0.18f); _pupilScale = 0.55f; break;
                case Face.Dead: m.sizeDelta = new Vector2(s * 0.26f, s * 0.05f); break;
                case Face.Cool: m.sizeDelta = new Vector2(s * 0.24f, s * 0.06f); _pupilScale = 1f; break;
                default: m.sizeDelta = new Vector2(s * 0.22f, s * 0.09f); _pupilScale = 1f; break;
            }
        }

        float _pupilScale = 1f;

        public void Kick(float squashImpulse) => _squashVel += squashImpulse;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _bob += dt * 6f;
            var pos = Vector2.Lerp(_rt.anchoredPosition, Target, 1f - Mathf.Exp(-dt * 18f));
            _rt.anchoredPosition = pos;
            float a = (1f - Squash) * 260f - _squashVel * 12f;
            _squashVel += a * dt;
            Squash += _squashVel * dt;
            float bob = Bob ? Mathf.Sin(_bob) * 0.04f : 0f;
            _body.localScale = new Vector3(1f / Mathf.Max(0.6f, Squash + bob), Squash + bob, 1f);
            Spin = Mathf.Lerp(Spin, 0f, 1f - Mathf.Exp(-dt * 5f));
            _body.localRotation = Quaternion.Euler(0, 0, Spin);
            if (_faceTimer > 0 && (_faceTimer -= dt) <= 0 && _face != Face.Dead) SetFace(Face.Idle);
            // pupils glance toward where we're heading
            Vector2 look = Vector2.ClampMagnitude((Target - pos) * 0.03f, 6f);
            float ps = _pupilScale;
            _pupilL.rectTransform.anchoredPosition = look; _pupilR.rectTransform.anchoredPosition = look;
            _pupilL.rectTransform.localScale = _pupilR.rectTransform.localScale = new Vector3(ps, ps, 1);
        }

        public void Snap(Vector2 p) { _rt.anchoredPosition = p; Target = p; }
    }

    /// <summary>A sticker-style door card: coloured fill, chunky outline, label, and a little doorknob.</summary>
    public sealed class DoorView : MonoBehaviour, IPointerDownHandler
    {
        public RectTransform Rt;
        public System.Action<DoorView> OnPressed;
        public bool Interactable;

        // fire on press (not release) so taps feel instant and reaction time is honest
        public void OnPointerDown(PointerEventData e) { if (Interactable) OnPressed?.Invoke(this); }
        Image _fill, _edge;
        Text _label;
        CanvasGroup _group;
        public string LabelText;
        public float Lift; // used for burst animation
        Color _base;

        public static DoorView Create(Transform parent)
        {
            var root = UIKit.Rect(parent, "Door");
            var d = root.gameObject.AddComponent<DoorView>();
            d.Rt = root;
            d._group = root.gameObject.AddComponent<CanvasGroup>();
            d._group.blocksRaycasts = true;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var fill = UIKit.Card(root, "card", Pal.Yellow, 12f);
            UIKit.Fill((RectTransform)fill.transform.parent);
            d._fill = fill;
            d._edge = fill.transform.parent.Find("edge").GetComponent<Image>();
            // door panel lines + knob for a "door" read at thumbnail size
            var inner = UIKit.Image(fill.transform, "panel", new Color(1, 1, 1, 0.22f), Art.Rounded);
            UIKit.Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(26, 26), new Vector2(-26, -26));
            var knob = UIKit.Image(fill.transform, "knob", Pal.Ink, Art.Circle);
            UIKit.Place(knob.rectTransform, new Vector2(0.85f, 0.42f), Vector2.zero, new Vector2(28, 28));
            d._label = UIKit.Text(fill.transform, "label", "", 56, Pal.Ink);
            d._label.rectTransform.offsetMin = new Vector2(34, 40); d._label.rectTransform.offsetMax = new Vector2(-34, -40);
            d._label.resizeTextForBestFit = true; d._label.resizeTextMinSize = 28; d._label.resizeTextMaxSize = 60;
            d._label.verticalOverflow = VerticalWrapMode.Truncate;
            return d;
        }

        public void Setup(string label, Color color, Vector2 size)
        {
            LabelText = label;
            _label.text = label;
            _base = color;
            _fill.color = color;
            _edge.color = Pal.Ink;
            Rt.sizeDelta = size;
            Rt.localScale = Vector3.one;
            Rt.localRotation = Quaternion.identity;
            _group.alpha = 1f;
            _label.enabled = true;
            _label.rectTransform.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        /// <summary>Mirror Mode: the door text is drawn back-to-front.</summary>
        public void MirrorLabel(bool on) => _label.rectTransform.localScale = new Vector3(on ? -1f : 1f, 1f, 1f);

        public void SetAlpha(float a) => _group.alpha = a;
        public void HideLabel(bool hidden) => _label.enabled = !hidden;
        public void Tint(Color edge) => _edge.color = edge;
        public void Dim(float k) => _fill.color = Color.Lerp(_base, Pal.Bg2, k);
    }

    /// <summary>Lightweight UI particles, floating text, screen shake and flashes. Pooled, unscaled time.</summary>
    public sealed class FxLayer : MonoBehaviour
    {
        sealed class P
        {
            public Image img; public Vector2 v; public float life, max, spin, grav, size; public bool shrink;
        }

        sealed class Pop { public Text t; public float life, max; public Vector2 v; }

        readonly List<P> _ps = new List<P>();
        readonly Stack<P> _pool = new Stack<P>();
        readonly List<Pop> _pops = new List<Pop>();
        RectTransform _rt;
        public RectTransform ShakeTarget;
        float _shake;
        Image _flash;
        float _flashA;

        public static FxLayer Create(Transform parent)
        {
            var rt = UIKit.Rect(parent, "Fx");
            UIKit.Fill(rt);
            var fx = rt.gameObject.AddComponent<FxLayer>();
            fx._rt = rt;
            fx._flash = UIKit.Image(rt, "flash", new Color(1, 1, 1, 0));
            UIKit.Fill(fx._flash.rectTransform);
            return fx;
        }

        public void Burst(Vector2 at, int count, Color a, Color b, Sprite sprite = null, float speed = 900f, float size = 28f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Get(sprite ?? Art.Star);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(0.35f, 1f) * speed;
                p.v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
                p.img.rectTransform.anchoredPosition = at;
                p.size = Random.Range(0.6f, 1.3f) * size;
                p.img.color = Color.Lerp(a, b, Random.value);
                p.life = p.max = Random.Range(0.35f, 0.7f);
                p.spin = Random.Range(-540f, 540f);
                p.grav = -1600f;
                p.shrink = true;
            }
        }

        /// <summary>Trail puff behind the character at high combos.</summary>
        public void Trail(Vector2 at, CosmeticDef trail)
        {
            Sprite sp = Art.Star; float grav = 0; Vector2 v = new Vector2(Random.Range(-60f, 60f), Random.Range(-260f, -120f));
            switch (trail?.style)
            {
                case "flame": sp = Art.Drop; grav = 900f; v = new Vector2(Random.Range(-50f, 50f), Random.Range(-80f, 40f)); break;
                case "pixel": sp = Art.Square; break;
                case "rain": sp = Art.Drop; grav = -1400f; v.y = -300f; break;
                case "hearts": sp = Art.Heart; grav = 300f; break;
                case "sparkle": sp = Art.Star; break;
            }
            var p = Get(sp);
            p.img.rectTransform.anchoredPosition = at + new Vector2(Random.Range(-50f, 50f), Random.Range(-20f, 30f));
            p.v = v; p.grav = grav; p.spin = Random.Range(-200f, 200f);
            p.size = Random.Range(22f, 40f);
            var c1 = Art.Hex(trail?.color, Color.white); var c2 = Art.Hex(trail?.accent, Color.white);
            p.img.color = Color.Lerp(c1, c2, Random.value);
            p.life = p.max = Random.Range(0.4f, 0.7f);
            p.shrink = true;
        }

        P Get(Sprite sprite)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : new P { img = UIKit.Image(_rt, "p", Color.white) };
            p.img.sprite = sprite;
            p.img.type = Image.Type.Simple;
            p.img.gameObject.SetActive(true);
            p.img.transform.SetAsLastSibling();
            _ps.Add(p);
            return p;
        }

        public void Popup(Vector2 at, string text, Color color, int size = 64, float life = 0.8f)
        {
            var t = UIKit.Text(_rt, "pop", text, size, color);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(900, 160));
            UIKit.Sticker(t, Pal.Ink, 5f);
            _pops.Add(new Pop { t = t, life = life, max = life, v = new Vector2(0, 260f) });
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);
        public void Flash(Color c, float a) { _flash.color = c; _flashA = a; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _ps.Count - 1; i >= 0; i--)
            {
                var p = _ps[i];
                p.life -= dt;
                if (p.life <= 0) { p.img.gameObject.SetActive(false); _ps.RemoveAt(i); _pool.Push(p); continue; }
                p.v.y += p.grav * dt * (p.grav < 0 ? 0.6f : 1f);
                p.v *= 1f - dt * 1.5f;
                var rt = p.img.rectTransform;
                rt.anchoredPosition += p.v * dt;
                rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + p.spin * dt);
                float k = p.shrink ? p.life / p.max : 1f;
                rt.sizeDelta = Vector2.one * p.size * Mathf.Max(0.05f, k);
                var c = p.img.color; c.a = Mathf.Clamp01(k * 1.6f); p.img.color = c;
            }
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.life -= dt;
                if (p.life <= 0) { Destroy(p.t.gameObject); _pops.RemoveAt(i); continue; }
                float k = 1f - p.life / p.max;
                p.t.rectTransform.anchoredPosition += p.v * dt;
                p.v *= 1f - dt * 4f;
                float s = k < 0.15f ? Mathf.Lerp(0.4f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) * 4f);
                p.t.rectTransform.localScale = Vector3.one * s;
                var c = p.t.color; c.a = p.life < 0.25f ? p.life / 0.25f : 1f; p.t.color = c;
            }
            if (ShakeTarget != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0, dt * 120f);
                ShakeTarget.anchoredPosition = _shake > 0.1f ? Random.insideUnitCircle * _shake : Vector2.zero;
            }
            if (_flashA > 0)
            {
                _flashA = Mathf.MoveTowards(_flashA, 0, dt * 3f);
                var c = _flash.color; c.a = _flashA; _flash.color = c;
            }
        }

        public void Clear()
        {
            foreach (var p in _ps) { p.img.gameObject.SetActive(false); _pool.Push(p); }
            _ps.Clear();
            foreach (var p in _pops) Destroy(p.t.gameObject);
            _pops.Clear();
        }
    }

    /// <summary>Looping hallway: scrolling striped floor between two walls; palette shifts with combo level and season.</summary>
    public sealed class Hallway : MonoBehaviour
    {
        RawImage _floor;
        Image _bg, _wallL, _wallR, _glow;
        float _scroll;
        public float Speed = 0.6f;
        Color _targetA, _targetB, _a, _b;
        int _level = -1;

        static readonly Color[][] Palettes =
        {
            new[] { Pal.Hex("#2A2340"), Pal.Hex("#231D36") }, // x1
            new[] { Pal.Hex("#1F3A4D"), Pal.Hex("#1A3040") }, // x2
            new[] { Pal.Hex("#3D2350"), Pal.Hex("#331D44") }, // x3
            new[] { Pal.Hex("#4D1F3F"), Pal.Hex("#421A36") }, // x4
        };

        public static Hallway Create(Transform parent)
        {
            var rt = UIKit.Rect(parent, "Hallway");
            UIKit.Fill(rt);
            var h = rt.gameObject.AddComponent<Hallway>();
            h._bg = UIKit.Image(rt, "bg", Pal.Bg);
            UIKit.Fill(h._bg.rectTransform);
            var floorGo = UIKit.Rect(rt, "floor");
            UIKit.Fill(floorGo);
            h._floor = floorGo.gameObject.AddComponent<RawImage>();
            h._floor.texture = Art.Stripes(Color.white, new Color(0.86f, 0.86f, 0.9f));
            h._floor.raycastTarget = false;
            h._floor.uvRect = new Rect(0, 0, 3f, 5.3f);
            h._wallL = UIKit.Image(rt, "wallL", Pal.Hex("#0E0C15"));
            UIKit.Stretch(h._wallL.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(46, 0));
            h._wallR = UIKit.Image(rt, "wallR", Pal.Hex("#0E0C15"));
            UIKit.Stretch(h._wallR.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-46, 0), new Vector2(0, 0));
            h._glow = UIKit.Image(rt, "glow", new Color(1, 1, 1, 0.0f), Art.Soft);
            UIKit.Place(h._glow.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1600, 1300));
            h.SetComboLevel(0, true);
            return h;
        }

        public void SetComboLevel(int level, bool instant = false)
        {
            level = Mathf.Clamp(level, 0, Palettes.Length - 1);
            if (level == _level && !instant) return;
            _level = level;
            _targetA = Palettes[level][0]; _targetB = Palettes[level][1];
            if (instant) { _a = _targetA; _b = _targetB; }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _scroll += dt * Speed;
            _floor.uvRect = new Rect(0, _scroll, 3f, 5.3f);
            _a = Color.Lerp(_a, _targetA, dt * 3f); _b = Color.Lerp(_b, _targetB, dt * 3f);
            _floor.color = _a;
            _bg.color = _b;
            var g = Color.Lerp(Pal.Purple, Pal.Pink, _level / 3f);
            g.a = 0.12f + 0.08f * _level + Mathf.Sin(Time.unscaledTime * 2f) * 0.03f;
            _glow.color = g;
        }
    }
}
