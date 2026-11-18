using System;
using System.Collections.Generic;

namespace VibeRun.Core
{
    /// <summary>Saved player profile. Plain fields so JsonUtility can persist it.</summary>
    [Serializable]
    public sealed class Profile
    {
        public int version = 1;
        public int xp;
        public int vibes;
        public int bestScore;
        public int runs;
        public bool tutorialDone;
        public List<string> owned = new List<string>();
        public string fit = "fit_blob";
        public string trail = "trail_sparkle";
        public string frame = "frame_polaroid";

        // daily
        public string lastLoginDay = "";   // yyyy-MM-dd
        public string lastDailyDay = "";   // day the Daily Drop was last played
        public int dailyStreak;
        public int bestDailyStreak;
        public string reviveTokenDay = ""; // day the free revive token was used
        public string lastDailyGrid = "";
        public int lastDailyScore;

        public bool adFree;
        public bool muted;
    }

    public static class Progression
    {
        /// <summary>XP needed to go from <paramref name="level"/> to level+1.</summary>
        public static int XpForLevel(int level) => 100 + 40 * (Math.Max(1, level) - 1);

        public static int LevelFor(int xp)
        {
            int level = 1;
            while (xp >= XpForLevel(level)) { xp -= XpForLevel(level); level++; }
            return level;
        }

        public static float LevelProgress(int xp)
        {
            int level = 1;
            while (xp >= XpForLevel(level)) { xp -= XpForLevel(level); level++; }
            return xp / (float)XpForLevel(level);
        }

        public static int XpForRun(int score, int beats) => score / 50 + beats * 2;

        public static int VibesForRun(int score, int vibesPerScore) => Math.Max(1, score / Math.Max(1, vibesPerScore));

        /// <summary>Titles unlock every 5 levels: level 1–4 = titles[0], 5–9 = titles[1]…</summary>
        public static string TitleFor(int level, IList<string> titles)
        {
            if (titles == null || titles.Count == 0) return "";
            int i = Math.Min(titles.Count - 1, level / 5);
            return titles[i];
        }

        /// <summary>Apply a finished run. Returns (vibes earned, levels gained).</summary>
        public static (int vibes, int levelsGained) ApplyRun(Profile p, int score, int beats, int vibesPerScore)
        {
            int before = LevelFor(p.xp);
            int v = VibesForRun(score, vibesPerScore);
            p.vibes += v;
            p.xp += XpForRun(score, beats);
            p.runs++;
            if (score > p.bestScore) p.bestScore = score;
            return (v, LevelFor(p.xp) - before);
        }

        public static bool Owns(Profile p, CosmeticDef c) => c.price <= 0 || p.owned.Contains(c.id);

        public static bool TryBuy(Profile p, CosmeticDef c)
        {
            if (Owns(p, c)) return true;
            if (p.vibes < c.price) return false;
            p.vibes -= c.price;
            p.owned.Add(c.id);
            return true;
        }
    }

    /// <summary>Daily systems: login gift, Daily Drop seed, streak and the free revive token. All date-in, no clock reads.</summary>
    public static class Daily
    {
        public static string DayKey(DateTime localDate) => localDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Same seed for everyone on the same calendar date.</summary>
        public static uint DropSeed(DateTime localDate) => Rng.Hash("viberun-daily-" + DayKey(localDate));

        public static bool CanPlayDrop(Profile p, DateTime today) => p.lastDailyDay != DayKey(today);

        /// <summary>Mark the Drop played and update the streak (consecutive calendar days).</summary>
        public static void CompleteDrop(Profile p, DateTime today)
        {
            string key = DayKey(today);
            if (p.lastDailyDay == key) return;
            string yesterday = DayKey(today.AddDays(-1));
            p.dailyStreak = p.lastDailyDay == yesterday ? p.dailyStreak + 1 : 1;
            p.bestDailyStreak = Math.Max(p.bestDailyStreak, p.dailyStreak);
            p.lastDailyDay = key;
        }

        /// <summary>Current streak as shown on the home screen: drops to 0 once a whole day is missed.</summary>
        public static int VisibleStreak(Profile p, DateTime today)
        {
            if (p.lastDailyDay == DayKey(today) || p.lastDailyDay == DayKey(today.AddDays(-1))) return p.dailyStreak;
            return 0;
        }

        /// <summary>Returns the gift amount if today's login gift has not been claimed yet (and claims it).</summary>
        public static int ClaimLoginGift(Profile p, DateTime today, int amount = 25)
        {
            string key = DayKey(today);
            if (p.lastLoginDay == key) return 0;
            p.lastLoginDay = key;
            p.vibes += amount;
            return amount;
        }

        public static bool HasReviveToken(Profile p, DateTime today) => p.reviveTokenDay != DayKey(today);

        public static void UseReviveToken(Profile p, DateTime today) => p.reviveTokenDay = DayKey(today);
    }
}
