using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VibeRun.Core;
using VibeRun.Services;

namespace VibeRun.Tests
{
    public class RunRulesTests
    {
        static Tuning T => new Tuning();

        static BeatSpec Spec(int doors = 2, VibeShift shift = VibeShift.None, int answer = 0)
        {
            var p = new PromptDef { id = "t", cat = Categories.Energy, d = 1, p = "Main character energy", a = "yes", b = "no", c = doors == 3 ? "maybe" : null };
            int[] order;
            if (doors == 2) order = answer == 0 ? new[] { 0, 1 } : new[] { 1, 0 };
            else { order = new int[3]; int k = 1; for (int i = 0; i < 3; i++) order[i] = i == answer ? 0 : k++; }
            return new BeatSpec { prompt = p, order = order, answerSlot = answer, shift = shift };
        }

        [Test]
        public void AuraStartsAt100AndDrains1PerSecond()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            Assert.AreEqual(100f, r.Aura);
            r.Tick(5f);
            Assert.AreEqual(95f, r.Aura, 0.001f);
        }

        [Test]
        public void WrongCosts25_FreezeCosts20_CorrectGains5Capped()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            r.BeginBeat();
            r.Resolve(Spec(), Swipe.Right, 0.5f, 1.4f);   // wrong (answer is slot 0 = left)
            Assert.AreEqual(75f, r.Aura, 0.001f);
            r.BeginBeat();
            r.Resolve(Spec(), Swipe.None, 1f, 1.4f);      // freeze
            Assert.AreEqual(55f, r.Aura, 0.001f);
            r.BeginBeat();
            r.Resolve(Spec(), Swipe.Left, 0.5f, 1.4f);    // correct
            Assert.AreEqual(60f, r.Aura, 0.001f);
            r.DebugSetAura(98f);
            r.Resolve(Spec(), Swipe.Left, 0.5f, 1.4f);
            Assert.AreEqual(100f, r.Aura, 0.001f, "capped at max");
        }

        [Test]
        public void RunEndsAtZeroAura()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            for (int i = 0; i < 4; i++) { r.BeginBeat(); r.Resolve(Spec(), Swipe.Right, 0.5f, 1.4f); }
            Assert.IsFalse(r.Alive);
            Assert.AreEqual(0f, r.Aura);
        }

        [Test]
        public void DecisionWindowFollowsSpeedRamp()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            Assert.AreEqual(3.0f, r.DecisionWindow(1), 1e-4);
            Assert.AreEqual(3.0f, r.DecisionWindow(10), 1e-4);
            Assert.AreEqual(2.5f, r.DecisionWindow(11), 1e-4);
            Assert.AreEqual(2.0f, r.DecisionWindow(21), 1e-4);
            Assert.AreEqual(1.6f, r.DecisionWindow(31), 1e-4);
            Assert.AreEqual(1.3f, r.DecisionWindow(41), 1e-4);
            Assert.AreEqual(1.0f, r.DecisionWindow(56), 1e-4);
            Assert.AreEqual(0.8f, r.DecisionWindow(71), 1e-4);
            Assert.AreEqual(0.8f, r.DecisionWindow(400), 1e-4);
        }

        [Test]
        public void ComboMultiplierThresholds()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            Assert.AreEqual(1, r.MultiplierFor(0));
            Assert.AreEqual(1, r.MultiplierFor(4));
            Assert.AreEqual(2, r.MultiplierFor(5));
            Assert.AreEqual(3, r.MultiplierFor(10));
            Assert.AreEqual(3, r.MultiplierFor(19));
            Assert.AreEqual(4, r.MultiplierFor(20));
        }

        [Test]
        public void ScoringBaseSpeedBonusAndMultiplier()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            Assert.AreEqual(50, r.SpeedBonus(0f));
            Assert.AreEqual(25, r.SpeedBonus(0.2f));
            Assert.AreEqual(0, r.SpeedBonus(0.4f));
            Assert.AreEqual(0, r.SpeedBonus(0.9f));
            r.BeginBeat();
            var res = r.Resolve(Spec(), Swipe.Left, 0.9f, 1.4f);
            Assert.AreEqual(100, res.points);
            for (int i = 0; i < 4; i++) { r.BeginBeat(); r.Resolve(Spec(), Swipe.Left, 0.9f, 1.4f); }
            Assert.AreEqual(5, r.Streak);
            r.BeginBeat();
            res = r.Resolve(Spec(), Swipe.Left, 0f, 1.4f); // streak 6 → x2, full speed bonus
            Assert.AreEqual((100 + 50) * 2, res.points);
            Assert.IsTrue(res.fast);
        }

        [Test]
        public void MissResetsStreak()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            for (int i = 0; i < 7; i++) { r.BeginBeat(); r.Resolve(Spec(), Swipe.Left, 0.5f, 1.4f); }
            Assert.AreEqual(2, r.Multiplier);
            r.BeginBeat(); r.Resolve(Spec(), Swipe.Right, 0.5f, 1.4f);
            Assert.AreEqual(0, r.Streak);
            Assert.AreEqual(1, r.Multiplier);
            Assert.AreEqual(7, r.BestStreak);
        }

        [Test]
        public void KeyboardShortcutsMapToDoorsAndMirrorDoesNotRemap()
        {
            Assert.AreEqual(0, RunState.SlotForSwipe(Swipe.Left, VibeShift.None, 2));
            Assert.AreEqual(0, RunState.SlotForSwipe(Swipe.Left, VibeShift.MirrorMode, 2));
            Assert.AreEqual(2, RunState.SlotForSwipe(Swipe.Right, VibeShift.MirrorMode, 3));
            Assert.AreEqual(1, RunState.SlotForSwipe(Swipe.Up, VibeShift.None, 3));
            Assert.AreEqual(-1, RunState.SlotForSwipe(Swipe.Up, VibeShift.None, 2));
        }

        [Test]
        public void TappingADoorResolvesBySlot()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            r.BeginBeat();
            Assert.AreEqual(Outcome.Correct, r.ResolveSlot(Spec(3, VibeShift.None, 2), 2, 0.5f, 2f).outcome);
            r.BeginBeat();
            Assert.AreEqual(Outcome.Wrong, r.ResolveSlot(Spec(3, VibeShift.None, 2), 0, 0.5f, 2f).outcome);
            r.BeginBeat();
            Assert.AreEqual(Outcome.Freeze, r.ResolveSlot(Spec(), -1, 1f, 2f).outcome);
        }

        [Test]
        public void OppositeDayRewardsTheWrongDoor()
        {
            Assert.IsTrue(RunState.IsCorrect(0, 0, VibeShift.None));
            Assert.IsFalse(RunState.IsCorrect(0, 0, VibeShift.OppositeDay));
            Assert.IsTrue(RunState.IsCorrect(1, 0, VibeShift.OppositeDay));
            Assert.IsTrue(RunState.IsCorrect(2, 0, VibeShift.OppositeDay));
        }

        [Test]
        public void ThreeDoorSwipeUpEntersMiddle()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            r.BeginBeat();
            var res = r.Resolve(Spec(3, VibeShift.None, 1), Swipe.Up, 0.5f, 0.85f);
            Assert.AreEqual(Outcome.Correct, res.outcome);
        }

        [Test]
        public void ShiftsStartAfterWarmUpAndAreAnnounced()
        {
            var r = new RunState(T, RunMode.Classic, 99);
            for (int b = 1; b <= 20; b++) Assert.AreEqual(VibeShift.None, r.ShiftFor(b));
            Assert.AreNotEqual(VibeShift.None, r.ShiftFor(21));
            Assert.IsTrue(r.IsShiftStart(21));
            Assert.IsFalse(r.IsShiftStart(22));
            Assert.AreEqual(r.ShiftFor(21), r.ShiftFor(25));
            Assert.AreEqual(VibeShift.None, r.ShiftFor(26));
            Assert.AreNotEqual(VibeShift.None, r.ShiftFor(31));
            // speed blink only from beat 41+
            for (uint s = 1; s < 200; s++)
            {
                var rr = new RunState(T, RunMode.Classic, s);
                Assert.AreNotEqual(VibeShift.SpeedBlink, rr.ShiftFor(21));
                Assert.AreNotEqual(VibeShift.SpeedBlink, rr.ShiftFor(31));
            }
            // late shifts last longer
            Assert.AreNotEqual(VibeShift.None, r.ShiftFor(67));
        }

        [Test]
        public void ReviveOncePerClassicRun()
        {
            var r = new RunState(T, RunMode.Classic, 1);
            r.DebugSetAura(0);
            Assert.IsTrue(r.CanRevive);
            r.Revive();
            Assert.AreEqual(50f, r.Aura);
            r.DebugSetAura(0);
            Assert.IsFalse(r.CanRevive);
            var d = new RunState(T, RunMode.Daily, 1);
            d.DebugSetAura(0);
            Assert.IsFalse(d.CanRevive, "no revives in Daily Drop");
        }

        [Test]
        public void DailyDropCompletesAt30Beats()
        {
            var r = new RunState(T, RunMode.Daily, 1);
            for (int i = 0; i < 30; i++) { r.BeginBeat(); r.Resolve(Spec(), Swipe.Left, 0.5f, 1.4f); }
            Assert.IsTrue(r.Alive);
            Assert.IsTrue(r.IsComplete);
        }

        [Test]
        public void PerfectPlayerSurvivesButIdlePlayerDiesInUnder60s()
        {
            // a player who never swipes loses 20 per beat plus drain: dead well inside a minute
            var r = new RunState(T, RunMode.Classic, 1);
            float t = 0;
            while (r.Alive && t < 60f)
            {
                r.BeginBeat();
                float w = r.DecisionWindow(r.Beat);
                r.Tick(w); t += w;
                if (r.Alive) r.Resolve(Spec(), Swipe.None, 1f, w);
            }
            Assert.IsFalse(r.Alive);
            Assert.Less(t, 15f);
        }
    }

    public class DeckTests
    {
        static List<PromptDef> MakePrompts(int n)
        {
            var l = new List<PromptDef>();
            for (int i = 0; i < n; i++)
                l.Add(new PromptDef { id = "p" + i.ToString("000"), cat = Categories.Mvp[i % Categories.Mvp.Length], d = 1 + i % 5, p = "Prompt number " + i, a = "A" + i, b = "B" + i, c = i % 2 == 0 ? "C" + i : null, tut = i < 5 });
            return l;
        }

        [Test]
        public void SameSeedSameRun()
        {
            var p = MakePrompts(120);
            var a = new PromptDeck(p, new Tuning(), 777);
            var b = new PromptDeck(p.AsEnumerable().Reverse(), new Tuning(), 777);
            for (int beat = 1; beat <= 60; beat++)
            {
                var x = a.Next(beat, VibeShift.None); var y = b.Next(beat, VibeShift.None);
                Assert.AreEqual(x.prompt.id, y.prompt.id);
                Assert.AreEqual(x.answerSlot, y.answerSlot);
                CollectionAssert.AreEqual(x.order, y.order);
            }
        }

        [Test]
        public void NoRepeatsAndDifficultyBandsRespected()
        {
            var deck = new PromptDeck(MakePrompts(150), new Tuning(), 5);
            var seen = new HashSet<string>();
            for (int beat = 1; beat <= 60; beat++)
            {
                var s = deck.Next(beat, VibeShift.None);
                Assert.IsTrue(seen.Add(s.prompt.id), "repeat " + s.prompt.id);
                PromptDeck.DifficultyBand(beat, out int min, out int max);
                Assert.That(s.prompt.d, Is.InRange(min, max));
                Assert.AreEqual(0, s.order[s.answerSlot], "answer slot must hold door a");
            }
        }

        [Test]
        public void ThreeDoorBeatsOnlyFromBeat31()
        {
            var deck = new PromptDeck(MakePrompts(400), new Tuning(), 9);
            int threes = 0;
            for (int beat = 1; beat <= 80; beat++)
            {
                var s = deck.Next(beat, VibeShift.None);
                if (beat < 31) Assert.AreEqual(2, s.DoorCount);
                else if (s.DoorCount == 3) threes++;
            }
            Assert.Greater(threes, 0);
        }

        [Test]
        public void TutorialPicksTutorialPrompts()
        {
            var deck = new PromptDeck(MakePrompts(50), new Tuning(), 3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(deck.Next(1, VibeShift.None, tutorial: true).prompt.tut);
        }
    }

    public class ResultAndProgressionTests
    {
        [Test]
        public void SeedCodeRoundTrips()
        {
            foreach (uint s in new uint[] { 1, 42, 12345, 424242, uint.MaxValue, 0x9E3779B9 })
            {
                string code = ResultLogic.SeedCode(s);
                Assert.AreEqual(7, code.Length);
                Assert.IsTrue(ResultLogic.TryParseSeedCode(code.ToLowerInvariant(), out uint back));
                Assert.AreEqual(s, back);
            }
            Assert.IsFalse(ResultLogic.TryParseSeedCode("bad", out _));
            Assert.IsFalse(ResultLogic.TryParseSeedCode("OOOOOOO", out _), "O and 0 are excluded to avoid typos");
        }

        [Test]
        public void EmojiGridRowsOfFiveAndPadsDaily()
        {
            var h = new List<BeatResult>
            {
                new BeatResult { outcome = Outcome.Correct, fast = true }, new BeatResult { outcome = Outcome.Correct },
                new BeatResult { outcome = Outcome.Wrong }, new BeatResult { outcome = Outcome.Freeze },
                new BeatResult { outcome = Outcome.Correct }, new BeatResult { outcome = Outcome.Correct },
            };
            string g = ResultLogic.EmojiGrid(h, 30);
            var rows = g.Split('\n');
            Assert.AreEqual(6, rows.Length);
            StringAssert.StartsWith("⚡✅❌❌✅", rows[0]);
            StringAssert.StartsWith("✅⬛", rows[1]);
        }

        [Test]
        public void RoastPicksWorstMissAndFillsTemplate()
        {
            var t = new Tuning();
            var r = new RunState(t, RunMode.Classic, 1);
            var easy = new PromptDef { id = "e", cat = "rank", d = 1, p = "More unhinged", a = "Cereal with orange juice", b = "Cereal with milk" };
            var hard = new PromptDef { id = "h", cat = "ick", d = 5, p = "Spot the ick", a = "x", b = "y" };
            r.BeginBeat(); r.Resolve(new BeatSpec { prompt = hard, order = new[] { 0, 1 }, answerSlot = 0 }, Swipe.Right, 0.5f, 1f);
            r.BeginBeat(); r.Resolve(new BeatSpec { prompt = easy, order = new[] { 0, 1 }, answerSlot = 0 }, Swipe.Right, 0.5f, 1f);
            var caps = new Captions { roastWrong = new List<string> { "picked \"{wrong}\" for \"{prompt}\". seek help." } };
            Assert.AreEqual("picked \"cereal with milk\" for \"more unhinged\". seek help.", ResultLogic.Roast(r, caps, new Rng(1)));
        }

        [Test]
        public void LevelsTitlesAndVibes()
        {
            Assert.AreEqual(1, Progression.LevelFor(0));
            Assert.AreEqual(2, Progression.LevelFor(100));
            Assert.AreEqual(3, Progression.LevelFor(240));
            var titles = new List<string> { "A", "B", "C" };
            Assert.AreEqual("A", Progression.TitleFor(4, titles));
            Assert.AreEqual("B", Progression.TitleFor(5, titles));
            Assert.AreEqual("C", Progression.TitleFor(99, titles));
            var p = new Profile();
            var (v, _) = Progression.ApplyRun(p, 2500, 30, 100);
            Assert.AreEqual(25, v);
            Assert.AreEqual(2500, p.bestScore);
        }

        [Test]
        public void ShopBuyAndOwn()
        {
            var p = new Profile { vibes = 100 };
            var free = new CosmeticDef { id = "f", price = 0 };
            var cheap = new CosmeticDef { id = "c", price = 60 };
            var dear = new CosmeticDef { id = "d", price = 500 };
            Assert.IsTrue(Progression.Owns(p, free));
            Assert.IsTrue(Progression.TryBuy(p, cheap));
            Assert.AreEqual(40, p.vibes);
            Assert.IsFalse(Progression.TryBuy(p, dear));
            Assert.AreEqual(40, p.vibes);
        }

        [Test]
        public void DailyStreakSeedAndTokens()
        {
            var p = new Profile();
            var d1 = new DateTime(2026, 11, 17, 9, 0, 0);
            Assert.AreEqual(Daily.DropSeed(d1), Daily.DropSeed(d1.AddHours(10)), "same seed all day");
            Assert.AreNotEqual(Daily.DropSeed(d1), Daily.DropSeed(d1.AddDays(1)));
            Assert.IsTrue(Daily.CanPlayDrop(p, d1));
            Daily.CompleteDrop(p, d1);
            Assert.IsFalse(Daily.CanPlayDrop(p, d1));
            Daily.CompleteDrop(p, d1.AddDays(1));
            Assert.AreEqual(2, p.dailyStreak);
            Assert.AreEqual(0, Daily.VisibleStreak(p, d1.AddDays(3)));
            Daily.CompleteDrop(p, d1.AddDays(3));
            Assert.AreEqual(1, p.dailyStreak);
            Assert.AreEqual(2, p.bestDailyStreak);

            Assert.IsTrue(Daily.HasReviveToken(p, d1));
            Daily.UseReviveToken(p, d1);
            Assert.IsFalse(Daily.HasReviveToken(p, d1));
            Assert.IsTrue(Daily.HasReviveToken(p, d1.AddDays(1)));

            Assert.AreEqual(25, Daily.ClaimLoginGift(p, d1));
            Assert.AreEqual(0, Daily.ClaimLoginGift(p, d1));
        }
    }

    /// <summary>Validates the shipped JSON content against the design doc's content rules.</summary>
    public class ContentTests
    {
        static GameConfig Cfg => GameConfig.Load();

        [Test]
        public void PromptsFollowContentRules()
        {
            var prompts = Cfg.Prompts;
            Assert.GreaterOrEqual(prompts.Count, 150);
            var ids = new HashSet<string>();
            foreach (var p in prompts)
            {
                Assert.IsTrue(ids.Add(p.id), "duplicate id " + p.id);
                Assert.IsTrue(Categories.IsValid(p.cat), p.id + " bad category " + p.cat);
                Assert.That(p.d, Is.InRange(1, 5), p.id);
                int words = p.p.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.That(words, Is.InRange(2, 6), p.id + " prompt must be 2–6 words: " + p.p);
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.a), p.id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.b), p.id);
                Assert.AreNotEqual(p.a, p.b, p.id);
                Assert.LessOrEqual(p.a.Length, 34, p.id + " door label too long for a door: " + p.a);
                Assert.LessOrEqual(p.b.Length, 34, p.id + " door label too long for a door: " + p.b);
            }
            foreach (var cat in Categories.Mvp) Assert.GreaterOrEqual(prompts.Count(p => p.cat == cat), 20, cat);
            Assert.GreaterOrEqual(prompts.Count(p => p.tut), 3, "need tutorial prompts");
            Assert.GreaterOrEqual(prompts.Count(p => p.HasThird && p.d >= 2), 15, "need 3-door prompts");
            for (int d = 1; d <= 5; d++) Assert.Greater(prompts.Count(p => p.d == d && p.cat != Categories.Sound), 0, "difficulty " + d);
        }

        [Test]
        public void CaptionsArchetypesAndTitles()
        {
            var c = Cfg.Captions;
            Assert.GreaterOrEqual(c.archetypes.Count, 40);
            foreach (var cat in Categories.All) for (int band = 0; band < 3; band++)
                Assert.IsTrue(c.archetypes.Any(a => a.cat == cat && a.band == band), cat + " band " + band);
            Assert.GreaterOrEqual(c.fail.Count, 80);
            Assert.GreaterOrEqual(c.freeze.Count, 20);
            Assert.GreaterOrEqual(c.roastWrong.Count, 10);
            Assert.GreaterOrEqual(c.titles.Count, 10);
        }

        [Test]
        public void CosmeticsMatchMvp()
        {
            var items = Cfg.Cosmetics;
            Assert.AreEqual(10, items.Count(i => i.kind == "fit"));
            Assert.AreEqual(5, items.Count(i => i.kind == "trail"));
            Assert.IsTrue(items.Any(i => i.kind == "fit" && i.price == 0));
            Assert.IsTrue(items.Any(i => i.kind == "trail" && i.price == 0));
            Assert.IsTrue(items.Any(i => i.kind == "frame" && i.price == 0));
            Assert.AreEqual(items.Count, items.Select(i => i.id).Distinct().Count());
        }

        [Test]
        public void TuningMatchesDesignDoc()
        {
            var t = Cfg.Tuning;
            Assert.AreEqual(100, t.startAura); Assert.AreEqual(25, t.wrongCost); Assert.AreEqual(20, t.freezeCost);
            Assert.AreEqual(5, t.correctGain); Assert.AreEqual(1, t.drainPerSecond);
            CollectionAssert.AreEqual(new[] { 3.0f, 2.5f, 2.0f, 1.6f, 1.3f, 1.0f, 0.8f }, t.windowSeconds);
            Assert.AreEqual(t.windowSeconds.Length, t.windowBeatStarts.Length);
            Assert.AreEqual(21, t.firstShiftBeat);
        }

        [Test]
        public void FullSimulatedRunOverRealContent()
        {
            var cfg = Cfg;
            var run = new RunState(cfg.Tuning, RunMode.Classic, 2026);
            var deck = new PromptDeck(cfg.Prompts, cfg.Tuning, 2026);
            var rng = new Rng(5);
            while (run.Alive && run.Beat < 300)
            {
                run.BeginBeat();
                var spec = deck.Next(run.Beat, run.ShiftFor(run.Beat));
                float w = run.DecisionWindow(run.Beat);
                run.Tick(w * 0.5f);
                bool right = rng.Value() < 0.85f;
                int want = spec.shift == VibeShift.OppositeDay ? (right ? (spec.answerSlot + 1) % spec.DoorCount : spec.answerSlot) : (right ? spec.answerSlot : (spec.answerSlot + 1) % spec.DoorCount);
                var res = run.ResolveSlot(spec, want, 0.5f, w);
                Assert.AreEqual(right ? Outcome.Correct : Outcome.Wrong, res.outcome, "beat " + run.Beat + " shift " + spec.shift);
            }
            var card = ResultLogic.Build(run, cfg.Captions, new Rng(1));
            Assert.IsFalse(string.IsNullOrEmpty(card.archetype));
            Assert.IsFalse(string.IsNullOrEmpty(card.roast));
            Assert.IsFalse(card.roast.Contains("{"), card.roast);
        }
    }
}
