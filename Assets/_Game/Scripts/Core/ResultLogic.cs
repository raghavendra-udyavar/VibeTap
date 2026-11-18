using System;
using System.Collections.Generic;
using System.Text;

namespace VibeRun.Core
{
    /// <summary>Everything that goes on the shareable result card, computed from a finished run.</summary>
    public sealed class ResultCard
    {
        public string archetype;
        public int archetypePercent;
        public string roast;
        public int score;
        public int bestCombo;
        public int beats;
        public int correct;
        public string grid;      // emoji grid (Daily Drop and share text)
        public int percentile;   // local estimate until a leaderboard backend exists
        public string seedCode;
    }

    public static class ResultLogic
    {
        public static ResultCard Build(RunState run, Captions caps, Rng rng)
        {
            var card = new ResultCard
            {
                score = run.Score,
                bestCombo = run.BestStreak,
                beats = run.History.Count,
                correct = run.CorrectCount,
                seedCode = SeedCode(run.Seed),
                percentile = Percentile(run.Score),
            };
            PickArchetype(run, caps, rng, out card.archetype, out card.archetypePercent);
            card.roast = Roast(run, caps, rng);
            card.grid = EmojiGrid(run.History, run.Mode == RunMode.Daily ? 30 : 0);
            return card;
        }

        /// <summary>Best category (most correct, tie → higher accuracy) picks the archetype; overall accuracy picks the band.</summary>
        public static void PickArchetype(RunState run, Captions caps, Rng rng, out string name, out int percent)
        {
            var hits = new Dictionary<string, int>();
            var seen = new Dictionary<string, int>();
            foreach (var h in run.History)
            {
                seen.TryGetValue(h.category, out int s); seen[h.category] = s + 1;
                if (h.outcome == Outcome.Correct) { hits.TryGetValue(h.category, out int c); hits[h.category] = c + 1; }
            }
            string best = null; int bestHits = -1; float bestAcc = -1;
            foreach (var kv in seen)
            {
                hits.TryGetValue(kv.Key, out int hc);
                float acc = hc / (float)kv.Value;
                if (hc > bestHits || (hc == bestHits && acc > bestAcc) || (hc == bestHits && acc == bestAcc && string.CompareOrdinal(kv.Key, best) < 0))
                { best = kv.Key; bestHits = hc; bestAcc = acc; }
            }
            float accAll = run.Accuracy;
            int band = accAll >= 0.8f ? 2 : accAll >= 0.5f ? 1 : 0;
            percent = Math.Max(51, Math.Min(99, (int)Math.Round(50 + accAll * 49 + (rng.Value() * 6 - 3))));

            var pool = new List<Archetype>();
            foreach (var a in caps.archetypes) if (a.cat == best && a.band == band) pool.Add(a);
            if (pool.Count == 0) foreach (var a in caps.archetypes) if (a.band == band) pool.Add(a);
            if (pool.Count == 0) pool.AddRange(caps.archetypes);
            name = pool.Count == 0 ? "Mysterious NPC" : pool[rng.Range(pool.Count)].name;
        }

        /// <summary>Roast the worst miss: a wrong pick on the easiest prompt beats a freeze; a clean run gets its own line.</summary>
        public static string Roast(RunState run, Captions caps, Rng rng)
        {
            BeatResult worst = null;
            foreach (var h in run.History)
            {
                if (h.outcome == Outcome.Correct) continue;
                if (worst == null) { worst = h; continue; }
                int Rank(BeatResult b) => (b.outcome == Outcome.Wrong ? 0 : 10) + b.difficulty;
                if (Rank(h) < Rank(worst)) worst = h;
            }
            if (worst == null) return Pick(caps.roastPerfect, rng, "no misses. suspicious honestly.");
            if (worst.outcome == Outcome.Freeze)
                return Fill(Pick(caps.roastFreeze, rng, "froze on \"{prompt}\". buffering irl."), worst);
            return Fill(Pick(caps.roastWrong, rng, "picked \"{wrong}\" for \"{prompt}\". seek help."), worst);
        }

        public static string Fill(string template, BeatResult b)
        {
            return template
                .Replace("{wrong}", (b.pickedLabel ?? "nothing").ToLowerInvariant())
                .Replace("{right}", (b.rightLabel ?? "").ToLowerInvariant())
                .Replace("{prompt}", (b.promptText ?? "").ToLowerInvariant());
        }

        public static string Pick(List<string> pool, Rng rng, string fallback) => pool == null || pool.Count == 0 ? fallback : pool[rng.Range(pool.Count)];

        /// <summary>✅ correct, ⚡ fast correct, ❌ wrong or freeze, ⬛ not reached (Daily Drop only). Rows of 5.</summary>
        public static string EmojiGrid(List<BeatResult> history, int padTo)
        {
            var sb = new StringBuilder();
            int n = Math.Max(history.Count, padTo);
            for (int i = 0; i < n; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('\n');
                if (i >= history.Count) { sb.Append("⬛"); continue; }
                var h = history[i];
                sb.Append(h.outcome == Outcome.Correct ? (h.fast ? "⚡" : "✅") : "❌");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Placeholder percentile from a logistic curve around a median score. Replace with leaderboard data once the
        /// backend exists; kept here so the card copy works offline.
        /// </summary>
        public static int Percentile(int score, float median = 2500f, float spread = 1200f)
        {
            double p = 1.0 / (1.0 + Math.Exp(-(score - median) / spread));
            return Math.Max(1, Math.Min(99, (int)Math.Round(p * 100)));
        }

        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string SeedCode(uint seed)
        {
            var sb = new StringBuilder();
            uint s = seed;
            for (int i = 0; i < 7; i++) { sb.Append(Alphabet[(int)(s % 32)]); s /= 32; }
            return sb.ToString();
        }

        public static bool TryParseSeedCode(string code, out uint seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(code)) return false;
            code = code.Trim().ToUpperInvariant();
            if (code.Length != 7) return false;
            ulong s = 0, mul = 1;
            foreach (char ch in code)
            {
                int i = Alphabet.IndexOf(ch);
                if (i < 0) return false;
                s += (ulong)i * mul; mul *= 32;
            }
            if (s > uint.MaxValue) return false;
            seed = (uint)s;
            return true;
        }

        public static string ShareText(ResultCard c, RunMode mode, string title, string dateLabel)
        {
            var sb = new StringBuilder();
            if (mode == RunMode.Daily)
            {
                sb.Append("Vibe Run Daily Drop ").Append(dateLabel).Append('\n');
                sb.Append(c.grid).Append('\n');
                sb.Append(c.correct).Append("/30 · more unhinged than ").Append(c.percentile).Append("% of players\n");
            }
            else
            {
                sb.Append("my aura: ").Append(c.archetypePercent).Append("% ").Append(c.archetype).Append('\n');
                sb.Append("score ").Append(c.score).Append(" · best combo ").Append(c.bestCombo).Append(" · ").Append(title).Append('\n');
                sb.Append("\"").Append(c.roast).Append("\"\n");
                sb.Append("play my exact run: viberun://run?seed=").Append(c.seedCode).Append('\n');
            }
            return sb.ToString();
        }
    }
}
