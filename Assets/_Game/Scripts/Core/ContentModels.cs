using System;
using System.Collections.Generic;

namespace VibeRun.Core
{
    /// <summary>Prompt categories from the design doc. Sound is defined but excluded from the MVP deck.</summary>
    public static class Categories
    {
        public const string Energy = "energy", Avoid = "avoid", Aesthetic = "aesthetic", Situational = "situational",
            Rank = "rank", Sound = "sound", Ick = "ick";

        public static readonly string[] All = { Energy, Avoid, Aesthetic, Situational, Rank, Sound, Ick };
        public static readonly string[] Mvp = { Energy, Avoid, Aesthetic, Situational, Rank, Ick };

        public static bool IsValid(string c) => Array.IndexOf(All, c) >= 0;

        public static string Display(string c)
        {
            switch (c)
            {
                case Energy: return "Energy";
                case Avoid: return "Avoid";
                case Aesthetic: return "Aesthetic";
                case Situational: return "Situational";
                case Rank: return "Rank";
                case Sound: return "Sound";
                case Ick: return "Ick";
                default: return c;
            }
        }
    }

    /// <summary>
    /// One prompt. Door <c>a</c> is always the right answer in data; the deck shuffles door order at runtime.
    /// <c>c</c> is an optional third (wrong) door used for 3-door beats.
    /// </summary>
    [Serializable]
    public sealed class PromptDef
    {
        public string id;
        public string cat;
        public int d = 1;     // difficulty 1..5
        public string p;      // prompt text
        public string a;      // correct door
        public string b;      // wrong door
        public string c;      // optional extra wrong door
        public bool tut;      // tutorial-grade prompt (obvious)

        public bool HasThird => !string.IsNullOrEmpty(c);

        public string Door(int i) => i == 0 ? a : i == 1 ? b : c;
    }

    [Serializable] public sealed class PromptFile { public int version; public List<PromptDef> prompts = new List<PromptDef>(); }

    [Serializable]
    public sealed class Archetype
    {
        public string name;
        public string cat;   // category the player was best at
        public int band;     // 0 = low accuracy, 1 = mid, 2 = high
    }

    [Serializable]
    public sealed class Captions
    {
        public List<string> fail = new List<string>();     // generic wrong-pick captions, may use {wrong} {right} {prompt}
        public List<string> freeze = new List<string>();   // no-input captions
        public List<string> hype = new List<string>();     // correct-pick shoutouts at combo milestones
        public List<string> roastWrong = new List<string>(); // result-card roast for worst wrong pick: {wrong} {prompt}
        public List<string> roastFreeze = new List<string>();
        public List<string> roastPerfect = new List<string>();
        public List<Archetype> archetypes = new List<Archetype>();
        public List<string> titles = new List<string>();   // one per 5 player levels
    }

    [Serializable]
    public sealed class CosmeticDef
    {
        public string id;
        public string kind;   // "fit" | "trail" | "frame"
        public string name;
        public int price;     // in Vibes; 0 = owned from start
        public string color;  // primary hex
        public string accent; // secondary hex
        public string style;  // trail style / frame style / fit accessory
    }

    [Serializable] public sealed class CosmeticFile { public List<CosmeticDef> items = new List<CosmeticDef>(); }

    /// <summary>All the numbers a designer tunes. Defaults match the design doc.</summary>
    [Serializable]
    public sealed class Tuning
    {
        public float startAura = 100, maxAura = 100;
        public float wrongCost = 25, freezeCost = 20, correctGain = 5;
        public float drainPerSecond = 1;
        public float lowAuraThreshold = 25;
        public float reviveAura = 50;

        // long warm-up so players learn the rules, then a steady ramp to the floor
        public int[] windowBeatStarts = { 1, 11, 21, 31, 41, 56, 71 };
        public float[] windowSeconds = { 3.0f, 2.5f, 2.0f, 1.6f, 1.3f, 1.0f, 0.8f };
        public int threeDoorFromBeat = 31;
        public float threeDoorChance = 0.35f;

        public int basePoints = 100, speedBonusMax = 50;
        public float speedBonusUnder = 0.4f;
        public int[] comboStreaks = { 0, 5, 10, 20 };
        public int[] comboMultipliers = { 1, 2, 3, 4 };

        public int shiftEvery = 10;        // a shift can start every shiftEvery beats…
        public int firstShiftBeat = 21;    // …but never before this beat
        public int shiftLength = 5;        // beats a shift lasts
        public int shiftLengthLate = 7;    // from beat 46
        public float shiftBannerSeconds = 0.8f;
        public string[] shiftPool = { "opposite", "mirror", "blink" };
        public int blinkFromBeat = 41;

        public float bufferSeconds = 0.15f;

        public int dailyBeats = 30;
        public float interstitialCooldown = 180f;
        public int vibesPerScore = 100;
        public string remoteContentUrl = ""; // optional: JSON prompt bundle fetched at boot
    }
}
