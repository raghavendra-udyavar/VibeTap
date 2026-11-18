using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VibeRun.UI
{
    /// <summary>Palette (dark mode by default, colorblind-safe good/bad: blue vs orange) and code-built uGUI helpers.</summary>
    public static class Pal
    {
        public static readonly Color Bg = Hex("#14121C");
        public static readonly Color Bg2 = Hex("#221E31");
        public static readonly Color Ink = Hex("#14121C");
        public static readonly Color Paper = Hex("#FFF8EC");
        public static readonly Color Text = Hex("#FFF8EC");
        public static readonly Color Muted = Hex("#A49EB8");
        public static readonly Color Good = Hex("#3DA5FF");   // blue = right (colorblind-safe pair with orange)
        public static readonly Color Bad = Hex("#FF8A1F");    // orange = wrong
        public static readonly Color Pink = Hex("#FF4FA3");
        public static readonly Color Yellow = Hex("#FFD23F");
        public static readonly Color Mint = Hex("#4BE3B0");
        public static readonly Color Purple = Hex("#8B5CF6");
        public static readonly Color[] DoorColors =
        {
            Hex("#FFD23F"), Hex("#4BE3B0"), Hex("#FF8FD8"), Hex("#7CC6FE"), Hex("#C3F584"), Hex("#FFB17A"), Hex("#C9B6FF"),
        };

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        public static Color A(this Color c, float a) { c.a = a; return c; }
    }

    public static class UIKit
    {
        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas CreateCanvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(es);
            }
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchor at a normalized point with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            return img;
        }

        public static Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            var rt = Rect(parent, name);
            Fill(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Thick dark outline for sticker-style text.</summary>
        public static Text Sticker(Text t, Color outline, float px = 4f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = outline; o.effectDistance = new Vector2(px, -px);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = outline; s.effectDistance = new Vector2(-px * 0.6f, px * 0.6f);
            return t;
        }

        /// <summary>
        /// Sticker card: dark rounded base (the outline), offset shadow, and a coloured rounded fill inset by
        /// <paramref name="border"/>. Returns the fill image; its parent is the root.
        /// </summary>
        public static Image Card(Transform parent, string name, Color fill, float border = 10f, Color? edge = null, bool shadow = true)
        {
            var root = Rect(parent, name);
            if (shadow)
            {
                var sh = Image(root, "shadow", new Color(0, 0, 0, 0.45f), Art.Rounded);
                Fill(sh.rectTransform); sh.rectTransform.offsetMin = new Vector2(6, -14); sh.rectTransform.offsetMax = new Vector2(6, -14);
            }
            var e = Image(root, "edge", edge ?? Pal.Ink, Art.Rounded);
            Fill(e.rectTransform);
            var f = Image(root, "fill", fill, Art.Rounded);
            Fill(f.rectTransform); f.rectTransform.offsetMin = new Vector2(border, border); f.rectTransform.offsetMax = new Vector2(-border, -border);
            return f;
        }

        public static Button Button(Transform parent, string name, string label, Color bg, Color fg, Action onClick, int fontSize = 48)
        {
            var fill = Card(parent, name, bg, 8f);
            var root = fill.transform.parent;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            var colors = btn.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f); btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                Services.AudioManager.Instance?.PlayClick();
                onClick?.Invoke();
            });
            root.gameObject.AddComponent<PressBounce>();
            var t = Text(fill.transform, "label", label, fontSize, fg);
            return btn;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t) t.text = text;
        }

        public static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (es.IsPointerOverGameObject()) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return false;
        }
    }

    /// <summary>Squash on press, overshoot on release. Juice for every button.</summary>
    public sealed class PressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        float _target = 1f, _scale = 1f, _vel;

        public void OnPointerDown(PointerEventData e) => _target = 0.92f;
        public void OnPointerUp(PointerEventData e) { _target = 1f; _vel += 3f; }

        void Update()
        {
            float k = 300f, d = 18f;
            float a = (_target - _scale) * k - _vel * d;
            _vel += a * Time.unscaledDeltaTime;
            _scale += _vel * Time.unscaledDeltaTime;
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }
}
