using System;
using System.Collections.Generic;

namespace VibeRun.Core
{
    public enum VibeShift { None, OppositeDay, MirrorMode, SpeedBlink, SoundOnly }

    public enum Swipe { None = -1, Left = 0, Right = 1, Up = 2 }

    public enum Outcome { Correct, Wrong, Freeze }

    public enum RunMode { Classic, Daily, Tutorial }

    /// <summary>What happened on one beat. Kept for the result card, the daily grid and analytics.</summary>
    public sealed class BeatResult
    {
        public int beat;
        public string promptId;
        public string category;
        public int difficulty;
        public string promptText;
        public string pickedLabel;
        public string rightLabel;
        public Outcome outcome;
        public float reactionFraction; // 0..1 of the decision window used
        public int reactionMs;
        public VibeShift shift;
        public int points;
        public bool fast;
    }

    /// <summary>
    /// Pure rules for a run: aura (health), passive drain, speed ramp, combos, scoring, vibe shift schedule.
    /// No Unity types — the view layer calls Tick/Resolve and reads the numbers.
    /// </summary>
    public sealed class RunState
    {
        readonly Tuning _t;
        public RunMode Mode { get; }
        public uint Seed { get; }

        public float Aura { get; private set; }
        public int Beat { get; private set; }          // 1-based index of the current/last beat
        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Multiplier => MultiplierFor(Streak);
        public bool Alive => Aura > 0f;
        public bool ReviveUsed { get; private set; }
        public float Elapsed { get; private set; }
        public readonly List<BeatResult> History = new List<BeatResult>();

        public bool IsLowAura => Aura < _t.lowAuraThreshold;
        public int MaxMultiplier => _t.comboMultipliers[_t.comboMultipliers.Length - 1];

        public RunState(Tuning tuning, RunMode mode, uint seed)
        {
            _t = tuning;
            Mode = mode;
            Seed = seed;
            Aura = tuning.startAura;
        }

        // ---------- timing ----------

        public float DecisionWindow(int beat)
        {
            float w = _t.windowSeconds[0];
            for (int i = 0; i < _t.windowBeatStarts.Length; i++)
                if (beat >= _t.windowBeatStarts[i]) w = _t.windowSeconds[i];
            return w;
        }

        /// <summary>Passive drain. Call every frame while a beat is live (not during shift banners or pauses).</summary>
        public void Tick(float dt)
        {
            if (!Alive || dt <= 0) return;
            Elapsed += dt;
            Aura = Math.Max(0f, Aura - _t.drainPerSecond * dt);
        }

        public void BeginBeat() { Beat++; }

        // ---------- shifts ----------

        /// <summary>
        /// Shift schedule: every <c>shiftEvery</c> beats a shift starts (beats 11, 21, 31…) and lasts
        /// <c>shiftLength</c> beats (longer after beat 46). Which shift is picked is seeded so challenge runs match.
        /// </summary>
        public VibeShift ShiftFor(int beat)
        {
            if (Mode == RunMode.Tutorial || beat <= _t.shiftEvery || beat < _t.firstShiftBeat) return VibeShift.None;
            int block = (beat - 1) / _t.shiftEvery;          // 1 for beats 11..20
            int offset = (beat - 1) % _t.shiftEvery;          // 0 on beat 11
            if (block * _t.shiftEvery + 1 < _t.firstShiftBeat) return VibeShift.None;
            int len = beat >= 56 ? _t.shiftLengthLate : _t.shiftLength;
            if (offset >= len) return VibeShift.None;
            var pool = new List<VibeShift>();
            foreach (var s in _t.shiftPool)
            {
                var v = ParseShift(s);
                if (v == VibeShift.None) continue;
                if (v == VibeShift.SpeedBlink && block * _t.shiftEvery + 1 < _t.blinkFromBeat) continue;
                pool.Add(v);
            }
            if (pool.Count == 0) return VibeShift.None;
            var r = new Rng(Seed ^ (uint)(block * 7919));
            return pool[r.Range(pool.Count)];
        }

        public bool IsShiftStart(int beat) => ShiftFor(beat) != VibeShift.None && ShiftFor(beat - 1) != ShiftFor(beat);

        public static VibeShift ParseShift(string s)
        {
            switch (s)
            {
                case "opposite": return VibeShift.OppositeDay;
                case "mirror": return VibeShift.MirrorMode;
                case "blink": return VibeShift.SpeedBlink;
                case "sound": return VibeShift.SoundOnly;
                default: return VibeShift.None;
            }
        }

        // ---------- picks ----------

        /// <summary>
        /// Keyboard shortcut → door slot (left / middle / right). Players normally tap the door itself.
        /// Mirror Mode mirrors the door text, it no longer remaps input.
        /// </summary>
        public static int SlotForSwipe(Swipe swipe, VibeShift shift, int doorCount)
        {
            if (swipe == Swipe.None) return -1;
            if (doorCount == 2)
            {
                if (swipe == Swipe.Up) return -1; // no middle door on 2-door beats
                return swipe == Swipe.Left ? 0 : 1;
            }
            // 3 doors: 0 = left, 1 = middle, 2 = right
            if (swipe == Swipe.Up) return 1;
            return swipe == Swipe.Left ? 0 : 2;
        }

        /// <summary>Is entering this slot a win? Opposite Day flips it: any non-answer door wins.</summary>
        public static bool IsCorrect(int slot, int answerSlot, VibeShift shift)
        {
            if (slot < 0) return false;
            bool matches = slot == answerSlot;
            return shift == VibeShift.OppositeDay ? !matches : matches;
        }

        public int MultiplierFor(int streak)
        {
            int m = 1;
            for (int i = 0; i < _t.comboStreaks.Length; i++)
                if (streak >= _t.comboStreaks[i]) m = _t.comboMultipliers[i];
            return m;
        }

        public int SpeedBonus(float reactionFraction)
        {
            if (reactionFraction >= _t.speedBonusUnder) return 0;
            float k = 1f - Math.Max(0f, reactionFraction) / _t.speedBonusUnder;
            return (int)Math.Round(_t.speedBonusMax * k);
        }

        public BeatResult ResolveCorrect(BeatSpec spec, int slot, float reactionFraction, float windowSeconds)
        {
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            int pts = (_t.basePoints + SpeedBonus(reactionFraction)) * Multiplier;
            Score += pts;
            Aura = Math.Min(_t.maxAura, Aura + _t.correctGain);
            return Record(spec, slot, Outcome.Correct, reactionFraction, windowSeconds, pts);
        }

        public BeatResult ResolveWrong(BeatSpec spec, int slot, float reactionFraction, float windowSeconds)
        {
            Streak = 0;
            Aura = Math.Max(0f, Aura - _t.wrongCost);
            return Record(spec, slot, Outcome.Wrong, reactionFraction, windowSeconds, 0);
        }

        public BeatResult ResolveFreeze(BeatSpec spec, float windowSeconds)
        {
            Streak = 0;
            Aura = Math.Max(0f, Aura - _t.freezeCost);
            return Record(spec, -1, Outcome.Freeze, 1f, windowSeconds, 0);
        }

        /// <summary>Resolve a keyboard pick (or a freeze when <paramref name="swipe"/> is None).</summary>
        public BeatResult Resolve(BeatSpec spec, Swipe swipe, float reactionFraction, float windowSeconds)
            => ResolveSlot(spec, SlotForSwipe(swipe, spec.shift, spec.DoorCount), reactionFraction, windowSeconds);

        /// <summary>Resolve a tap on door <paramref name="slot"/> (-1 = no pick, counts as a freeze).</summary>
        public BeatResult ResolveSlot(BeatSpec spec, int slot, float reactionFraction, float windowSeconds)
        {
            if (slot < 0 || slot >= spec.DoorCount) return ResolveFreeze(spec, windowSeconds);
            return IsCorrect(slot, spec.answerSlot, spec.shift)
                ? ResolveCorrect(spec, slot, reactionFraction, windowSeconds)
                : ResolveWrong(spec, slot, reactionFraction, windowSeconds);
        }

        BeatResult Record(BeatSpec spec, int slot, Outcome o, float frac, float window, int pts)
        {
            var r = new BeatResult
            {
                beat = Beat,
                promptId = spec.prompt.id,
                category = spec.prompt.cat,
                difficulty = spec.prompt.d,
                promptText = spec.prompt.p,
                pickedLabel = slot >= 0 ? spec.Label(slot) : null,
                rightLabel = spec.prompt.a,
                outcome = o,
                reactionFraction = frac,
                reactionMs = (int)(frac * window * 1000f),
                shift = spec.shift,
                points = pts,
                fast = o == Outcome.Correct && frac < _t.speedBonusUnder,
            };
            History.Add(r);
            return r;
        }

        public bool CanRevive => !Alive && !ReviveUsed && Mode == RunMode.Classic;

        public void Revive()
        {
            if (ReviveUsed) return;
            ReviveUsed = true;
            Aura = Math.Min(_t.maxAura, _t.reviveAura);
        }

        /// <summary>Daily Drop ends after a fixed number of beats even if aura remains.</summary>
        public bool IsComplete => !Alive || (Mode == RunMode.Daily && Beat >= _t.dailyBeats && History.Count >= _t.dailyBeats);

        public int CorrectCount { get { int n = 0; foreach (var h in History) if (h.outcome == Outcome.Correct) n++; return n; } }

        public float Accuracy => History.Count == 0 ? 0f : CorrectCount / (float)History.Count;

        /// <summary>For tests and tools: set aura directly.</summary>
        public void DebugSetAura(float v) => Aura = v;
    }
}
