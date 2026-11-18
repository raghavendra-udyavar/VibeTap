using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace VibeRun.Services
{
    /// <summary>
    /// Saves the result card as a PNG (9:16 Story or 1:1 chat crop) and copies the share text + deep link.
    /// <see cref="NativeShare"/> is the hook for a native share sheet plugin (e.g. NativeShare for iOS/Android).
    /// </summary>
    public static class ShareService
    {
        /// <summary>(pngPath, text) → open the OS share sheet. Null = save + clipboard only.</summary>
        public static Action<string, string> NativeShare;

        public static string LastSavedPath { get; private set; }

        public static IEnumerator CaptureAndShare(MonoBehaviour host, RectTransform card, bool square, string text, Action<string> done)
        {
            yield return new WaitForEndOfFrame();
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            // overlay canvas: world corners are screen pixels
            float x0 = Mathf.Clamp(corners[0].x, 0, Screen.width), y0 = Mathf.Clamp(corners[0].y, 0, Screen.height);
            float x1 = Mathf.Clamp(corners[2].x, 0, Screen.width), y1 = Mathf.Clamp(corners[2].y, 0, Screen.height);
            int w = Mathf.Max(8, (int)(x1 - x0)), h = Mathf.Max(8, (int)(y1 - y0));
            if (square)
            {
                int s = Mathf.Min(w, h);
                y0 += (h - s) * 0.62f; // keep the archetype + face, crop the footer
                h = s; x0 += (w - s) * 0.5f; w = s;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
            tex.Apply();
            string dir = Path.Combine(Application.persistentDataPath, "share");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "viberun_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + (square ? "_1x1" : "_9x16") + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            LastSavedPath = path;
            GUIUtility.systemCopyBuffer = text;
            Analytics.Log("share_tapped", ("format", square ? "1x1" : "9x16"));
            if (NativeShare != null) NativeShare(path, text);
            done?.Invoke(path);
        }

        public static void CopyText(string text)
        {
            GUIUtility.systemCopyBuffer = text;
            Analytics.Log("share_tapped", ("format", "text"));
        }
    }
}
