using System;
using UnityEngine;

namespace VibeRun.Services
{
    /// <summary>Ad network abstraction. Wire AppLovin MAX / Unity LevelPlay behind this interface.</summary>
    public interface IAdProvider
    {
        bool RewardedReady { get; }
        void ShowRewarded(string placement, Action<bool> onDone);
        void ShowInterstitial(string placement, Action onDone);
    }

    /// <summary>Default provider: grants every reward instantly so the full flow is testable without an SDK.</summary>
    public sealed class NoOpAdProvider : IAdProvider
    {
        public bool RewardedReady => true;

        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            Debug.Log("[ads] (no-op) rewarded: " + placement);
            onDone?.Invoke(true);
        }

        public void ShowInterstitial(string placement, Action onDone)
        {
            Debug.Log("[ads] (no-op) interstitial: " + placement);
            onDone?.Invoke();
        }
    }

    /// <summary>
    /// Cadence rules from the design doc: rewarded ads are always opt-in; interstitials only between runs,
    /// at most once every <c>interstitialCooldown</c> seconds, never mid-run, and never for Ad-Free owners.
    /// </summary>
    public sealed class AdManager
    {
        static AdManager _instance;
        public static AdManager Instance => _instance ?? (_instance = new AdManager(new NoOpAdProvider()));

        public IAdProvider Provider { get; set; }
        float _lastInterstitial = -9999f;
        public bool InRun { get; set; }

        public AdManager(IAdProvider provider) { Provider = provider; }

        public bool CanShowInterstitial(float now, float cooldown, bool adFree) => !adFree && !InRun && now - _lastInterstitial >= cooldown;

        /// <summary>Call between runs. Shows an interstitial only if the cap allows; always continues.</summary>
        public void MaybeInterstitial(string placement, float cooldown, bool adFree, Action onDone)
        {
            float now = Time.realtimeSinceStartup;
            // the very first break of a session is free: start the cooldown instead of showing an ad right away
            if (_lastInterstitial < -1000f) { _lastInterstitial = now; onDone?.Invoke(); return; }
            if (!CanShowInterstitial(now, cooldown, adFree)) { onDone?.Invoke(); return; }
            _lastInterstitial = now;
            Analytics.Log("ad_watched", ("type", "interstitial"), ("placement", placement));
            Provider.ShowInterstitial(placement, onDone);
        }

        public void Rewarded(string placement, Action<bool> onDone)
        {
            if (!Provider.RewardedReady) { onDone?.Invoke(false); return; }
            Provider.ShowRewarded(placement, ok =>
            {
                if (ok) Analytics.Log("ad_watched", ("type", "rewarded"), ("placement", placement));
                onDone?.Invoke(ok);
            });
        }
    }
}
