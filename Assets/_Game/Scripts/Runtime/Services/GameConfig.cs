using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using VibeRun.Core;

namespace VibeRun.Services
{
    /// <summary>
    /// Loads designer JSON from Resources/Config. Prompts can be overridden by a server-side bundle
    /// (<c>remoteContentUrl</c> in tuning.json) cached to persistentDataPath, so weekly drops ship without a store update.
    /// </summary>
    public sealed class GameConfig
    {
        static GameConfig _instance;
        public static GameConfig Instance => _instance ?? (_instance = Load());

        public Tuning Tuning { get; private set; }
        public List<PromptDef> Prompts { get; private set; }
        public Captions Captions { get; private set; }
        public List<CosmeticDef> Cosmetics { get; private set; }
        public int ContentVersion { get; private set; }
        public string ContentSource { get; private set; } = "builtin";

        static string CachePath => Path.Combine(Application.persistentDataPath, "content_prompts.json");

        public static GameConfig Load()
        {
            var c = new GameConfig
            {
                Tuning = FromResource<Tuning>("Config/tuning") ?? new Tuning(),
                Captions = FromResource<Captions>("Config/captions") ?? new Captions(),
                Cosmetics = (FromResource<CosmeticFile>("Config/cosmetics") ?? new CosmeticFile()).items,
            };
            var builtin = FromResource<PromptFile>("Config/prompts") ?? new PromptFile();
            c.Prompts = builtin.prompts;
            c.ContentVersion = builtin.version;

            // a newer cached server bundle wins over the built-in one
            try
            {
                if (File.Exists(CachePath))
                {
                    var cached = JsonUtility.FromJson<PromptFile>(File.ReadAllText(CachePath));
                    if (cached != null && cached.version > builtin.version && cached.prompts != null && cached.prompts.Count >= 20)
                    {
                        c.Prompts = cached.prompts;
                        c.ContentVersion = cached.version;
                        c.ContentSource = "remote-cache";
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[VibeRun] ignoring bad content cache: " + e.Message); }
            return c;
        }

        static T FromResource<T>(string path) where T : class
        {
            var ta = Resources.Load<TextAsset>(path);
            if (ta == null) { Debug.LogError("[VibeRun] missing config " + path); return null; }
            return JsonUtility.FromJson<T>(ta.text);
        }

        /// <summary>Fetch the remote prompt bundle in the background; applied on next launch (never mid-session).</summary>
        public IEnumerator FetchRemoteContent()
        {
            if (string.IsNullOrEmpty(Tuning.remoteContentUrl)) yield break;
            using (var req = UnityWebRequest.Get(Tuning.remoteContentUrl))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Debug.Log("[VibeRun] remote content fetch failed: " + req.error); yield break; }
                try
                {
                    var file = JsonUtility.FromJson<PromptFile>(req.downloadHandler.text);
                    if (file != null && file.version > ContentVersion && file.prompts != null && file.prompts.Count >= 20)
                    {
                        File.WriteAllText(CachePath, req.downloadHandler.text);
                        Debug.Log("[VibeRun] cached remote content v" + file.version);
                    }
                }
                catch (Exception e) { Debug.LogWarning("[VibeRun] bad remote content: " + e.Message); }
            }
        }

        public CosmeticDef Cosmetic(string id)
        {
            foreach (var c in Cosmetics) if (c.id == id) return c;
            return null;
        }

        public CosmeticDef FirstOfKind(string kind)
        {
            foreach (var c in Cosmetics) if (c.kind == kind) return c;
            return null;
        }
    }

    /// <summary>Profile persistence in PlayerPrefs (JSON). Small and synchronous; swap for cloud save later.</summary>
    public static class SaveService
    {
        const string Key = "viberun.profile.v1";
        static Profile _p;

        public static Profile Profile
        {
            get
            {
                if (_p != null) return _p;
                try
                {
                    var json = PlayerPrefs.GetString(Key, "");
                    _p = string.IsNullOrEmpty(json) ? new Profile() : JsonUtility.FromJson<Profile>(json);
                }
                catch { _p = new Profile(); }
                if (_p == null) _p = new Profile();
                if (_p.owned == null) _p.owned = new List<string>();
                return _p;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            _p = null;
        }
    }

    /// <summary>
    /// Analytics facade. Events from the design doc: run_start, run_end, pick, share_tapped, revive_used, ad_watched,
    /// purchase. The default sink logs to the console; plug Firebase/GameAnalytics in via <see cref="Sink"/>.
    /// </summary>
    public static class Analytics
    {
        public interface ISink { void Log(string name, IDictionary<string, object> data); }

        sealed class ConsoleSink : ISink
        {
            public void Log(string name, IDictionary<string, object> data)
            {
                if (!Debug.isDebugBuild) return;
                var sb = new System.Text.StringBuilder("[analytics] ").Append(name);
                if (data != null) foreach (var kv in data) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                Debug.Log(sb.ToString());
            }
        }

        public static ISink Sink = new ConsoleSink();
        public static bool Verbose = false; // "pick" events are chatty

        public static void Log(string name, params (string key, object value)[] fields)
        {
            if (name == "pick" && !Verbose) return;
            var d = new Dictionary<string, object>();
            foreach (var f in fields) d[f.key] = f.value;
            Sink?.Log(name, d);
        }
    }

    /// <summary>Haptics: a buzz on miss. Light ticks need a native plugin (Taptic/VibrationEffect); hook it here.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        public static void Tick() { /* light tick: requires native plugin, intentionally a no-op */ }

        public static void Miss()
        {
#if UNITY_IOS || UNITY_ANDROID
            if (Enabled) Handheld.Vibrate();
#endif
        }
    }
}
