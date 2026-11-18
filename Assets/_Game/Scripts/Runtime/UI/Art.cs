using System;
using System.Collections.Generic;
using UnityEngine;

namespace VibeRun.UI
{
    /// <summary>
    /// Procedural sprites so the prototype ships with zero imported art: rounded rects (9-sliced), circles, soft dots,
    /// hearts, stars and a hallway stripe texture. Swap for the real sticker art kit later.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Rounded => Get("rounded", () => MakeRounded(128, 44));
        public static Sprite RoundedSmall => Get("rounded_s", () => MakeRounded(64, 18));
        public static Sprite Circle => Get("circle", () => MakeShape(128, (x, y) => 1f - Mathf.Sqrt(x * x + y * y)));
        public static Sprite Soft => Get("soft", () => MakeSoft(64));
        public static Sprite Square => Get("square", () => MakeShape(16, (x, y) => 1f));
        public static Sprite Heart => Get("heart", () => MakeShape(128, HeartSdf));
        public static Sprite Star => Get("star", () => MakeShape(128, StarSdf));
        public static Sprite Ring => Get("ring", () => MakeShape(128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return 0.18f - Mathf.Abs(r - 0.78f); }));
        public static Sprite Drop => Get("drop", () => MakeShape(128, DropSdf));
        public static Sprite Triangle => Get("tri", () => MakeShape(128, (x, y) => Mathf.Min(-y * 0.9f + 0.6f, Mathf.Min(0.9f * y + 1.7f * x + 0.6f, 0.9f * y - 1.7f * x + 0.6f)) * 0.8f));

        static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        /// <summary>sdf(x,y) in [-1,1] space; positive = inside. Edge is anti-aliased.</summary>
        static Sprite MakeShape(int size, Func<float, float, float> sdf)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float aa = 2f / size * 1.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(sdf(u, v) / aa + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        static Sprite MakeSoft(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        static float HeartSdf(float x, float y)
        {
            x *= 1.25f; y = y * 1.25f + 0.15f;
            float a = x * x + y * y - 1f;
            float f = a * a * a - x * x * y * y * y;
            return -f * 1.5f;
        }

        static float StarSdf(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x) + Mathf.PI / 2f;
            float k = Mathf.Cos(5f * ang);
            float edge = 0.55f + 0.38f * k;
            return (edge - r) * 0.8f;
        }

        static float DropSdf(float x, float y)
        {
            float circle = 0.55f - Mathf.Sqrt(x * x + (y + 0.25f) * (y + 0.25f));
            float cone = Mathf.Min(0.85f - y, 0.5f * (0.85f - y) - Mathf.Abs(x));
            return Mathf.Max(circle, y > -0.25f ? cone : circle);
        }

        /// <summary>Diagonal stripe texture for the scrolling hallway floor.</summary>
        public static Texture2D Stripes(Color a, Color b, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = ((x + y) / (size / 2)) % 2 == 0 ? a : b;
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        public static Color Hex(string hex, Color fallback = default)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback == default ? Color.white : fallback;
        }
    }
}
