using System;
using System.Collections.Generic;

namespace VibeRun.Core
{
    /// <summary>A concrete beat: a prompt, its doors in screen order, and which slot is the answer.</summary>
    public sealed class BeatSpec
    {
        public PromptDef prompt;
        public int[] order;      // order[slot] = door index in the prompt (0 = a, 1 = b, 2 = c)
        public int answerSlot;
        public VibeShift shift;
        public int DoorCount => order.Length;
        public string Label(int slot) => prompt.Door(order[slot]);
    }

    /// <summary>
    /// Seeded prompt picker. Pulls harder prompts as the run goes on, never repeats a prompt until the pool runs dry,
    /// and introduces 3-door beats from beat 26. Same seed + same content = same run (Daily Drop, challenge links).
    /// </summary>
    public sealed class PromptDeck
    {
        readonly List<PromptDef> _all;
        readonly Tuning _t;
        readonly Rng _rng;
        readonly HashSet<string> _used = new HashSet<string>();

        public PromptDeck(IEnumerable<PromptDef> prompts, Tuning tuning, uint seed, bool includeSound = false)
        {
            _all = new List<PromptDef>();
            foreach (var p in prompts)
            {
                if (p == null || string.IsNullOrEmpty(p.p)) continue;
                if (!includeSound && p.cat == Categories.Sound) continue;
                _all.Add(p);
            }
            // stable order regardless of file order so seeds survive content reshuffles of the same set
            _all.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
            _t = tuning;
            _rng = new Rng(seed);
        }

        public int Count => _all.Count;

        /// <summary>Difficulty band [min,max] for a beat.</summary>
        public static void DifficultyBand(int beat, out int min, out int max)
        {
            if (beat <= 20) { min = 1; max = 2; }
            else if (beat <= 35) { min = 1; max = 3; }
            else if (beat <= 55) { min = 2; max = 4; }
            else { min = 3; max = 5; }
        }

        public BeatSpec Next(int beat, VibeShift shift, bool tutorial = false)
        {
            DifficultyBand(beat, out int min, out int max);
            bool wantThree = !tutorial && beat >= _t.threeDoorFromBeat && _rng.Value() < _t.threeDoorChance;

            var candidates = tutorial
                ? Collect(p => !_used.Contains(p.id) && p.tut)   // tutorial prompts are easy by definition; ignore the band
                : Collect(p => !_used.Contains(p.id) && p.d >= min && p.d <= max && (!wantThree || p.HasThird));
            if (candidates.Count == 0 && wantThree) { wantThree = false; candidates = Collect(p => !_used.Contains(p.id) && p.d >= min && p.d <= max); }
            if (candidates.Count == 0) candidates = Collect(p => !_used.Contains(p.id));
            if (candidates.Count == 0) { _used.Clear(); candidates = Collect(p => p.d >= min && p.d <= max); }
            if (candidates.Count == 0) candidates = new List<PromptDef>(_all);
            if (candidates.Count == 0) throw new InvalidOperationException("No prompts loaded");

            var prompt = candidates[_rng.Range(candidates.Count)];
            _used.Add(prompt.id);

            int n = wantThree && prompt.HasThird ? 3 : 2;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = _rng.Range(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            int answer = Array.IndexOf(order, 0);
            return new BeatSpec { prompt = prompt, order = order, answerSlot = answer, shift = shift };
        }

        List<PromptDef> Collect(Predicate<PromptDef> f)
        {
            var l = new List<PromptDef>();
            foreach (var p in _all) if (f(p)) l.Add(p);
            return l;
        }
    }
}
