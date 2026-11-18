<div align="center">

# ✨ Vibe Tap

**A one-thumb swipe runner where you pick the right "vibe" door in under a second.**

Runs last 30–60 seconds and end in a result card made to be screenshotted and shared.

![Unity](https://img.shields.io/badge/Unity-6000.3-black?logo=unity)
![Platforms](https://img.shields.io/badge/platforms-iOS%20%7C%20Android%20%7C%20WebGL%20%7C%20macOS-blue)
![Genre](https://img.shields.io/badge/genre-hyper--casual-ff69b4)
![Status](https://img.shields.io/badge/status-prototype-orange)

</div>

---

> **The pitch:** A prompt flashes (*"pick the main character energy"*), two doors slide in, you swipe into one. Right picks speed you up and grow your aura; wrong picks drain it. When your aura hits zero, you get a shareable result card that roasts your choices.

## 📑 Contents

- [Getting started](#-getting-started)
- [Design pillars](#-design-pillars)
- [At a glance](#-at-a-glance)
- [Core gameplay loop and controls](#-core-gameplay-loop-and-controls)
- [Game systems](#%EF%B8%8F-game-systems)
- [Prompt and door content](#-prompt-and-door-content)
- [Game modes](#-game-modes)
- [Progression, cosmetics and seasons](#-progression-cosmetics-and-seasons)
- [Social and virality](#-social-and-virality)
- [Art, audio and UI/UX](#-art-audio-and-uiux)
- [Monetization](#-monetization)
- [Live ops and moderation](#%EF%B8%8F-live-ops-and-moderation)
- [Technical plan and KPIs](#-technical-plan-and-kpis)
- [MVP scope, roadmap and risks](#%EF%B8%8F-mvp-scope-roadmap-and-risks)

---

## 🚀 Getting started

This repo contains a playable **Unity 6** prototype (project codename *Vibe Run*) alongside the full game design document below.

The prototype includes the aura meter, speed ramp, combos, vibe shifts, Classic and Daily Drop modes, a shareable result card, a cosmetics shop, challenge codes, procedural art and audio, and EditMode tests.

### Requirements

- Unity **6000.3.12f1** (or a compatible Unity 6 release)
- Android / iOS / WebGL build modules for the platforms you want to target

### Run it

1. Clone the repo and open the folder in Unity Hub.
2. Open `Assets/_Game/Scenes/Main.unity`.
3. Press **Play**.

### Editor tools

All build helpers live under **Tools → Vibe Run** in the Unity menu bar:

| Menu item | What it does |
|---|---|
| Build Main Scene | Regenerates the main scene |
| Apply Player Settings | Applies the project's player settings |
| Build macOS (dev) / Android (apk) / iOS (Xcode project) / WebGL | Platform builds |
| Reset Save Data | Clears local progress |

### Tests

Run the EditMode tests from **Window → General → Test Runner → EditMode**.

### Project layout

```
Assets/_Game/
├── Editor/               # Build tools and menu items
├── Resources/Config/     # Data-driven content: prompts, captions, cosmetics, tuning
├── Scenes/               # Main.unity
├── Scripts/
│   ├── Core/             # Engine-agnostic logic: run state, prompt deck, progression, RNG
│   └── Runtime/
│       ├── Gameplay/     # Run controller, swipe input, views
│       ├── Services/     # Ads, audio, config, sharing
│       └── UI/           # Screens, UI kit, procedural art
└── Tests/EditMode/       # Core logic tests
```

Gameplay content and balance are tuned in `Assets/_Game/Resources/Config/*.json`, with no code changes needed.

---

# 📘 Game Design Document

*Last updated: 17 Nov 2026*

## 🎯 Design pillars

| Pillar | Meaning |
|---|---|
| ⚡ **Instant** | Playable within 3 seconds of opening the app. No tutorial screens, one gesture. |
| 😂 **Funny when you fail** | Wrong answers trigger jokes and reactions, so losing still feels good. |
| 🔄 **Always current** | Prompts and aesthetics rotate weekly so the game speaks the internet's language right now. |
| 📤 **Made to share** | Every run ends in something worth posting to a group chat or TikTok. |

## 📋 At a glance

| Attribute | Target |
|---|---|
| **Audience** | Ages 16–27; heavy TikTok, Instagram and Discord users |
| **Platforms** | iOS and Android first; web (HTML5) build for testing and virality |
| **Session length** | 30–60 s per run, 3–5 min per session |
| **Orientation** | Portrait, one-handed |
| **Genre** | Hyper-casual reflex + reaction humor |
| **Comparable titles** | Subway Surfers (pace), Wordle (daily share), BeReal (social ritual) |

---

## 🎮 Core gameplay loop and controls

Each run is a chain of 1-second decisions: read the prompt, judge two doors, swipe, react, repeat faster.

### Moment-to-moment loop (one "beat")

1. A **prompt** appears at the top of the screen (e.g. *"Avoid the NPC"*).
2. **Two doors** slide in from the bottom, each showing a vibe: an illustrated reaction face, an aesthetic scene, or a short sound cue.
3. The player **swipes left or right** before the door line reaches the character (the decision window).
4. ✅ **Correct:** the character bursts through with a juicy hit effect. Aura +, speed +.
5. ❌ **Wrong:** a comedic fail animation and caption (*"bestie that was the NPC"*). Aura drains.
6. The next prompt appears instantly. Every 10 beats a **vibe shift** changes the rules.

### Session loop

```
Open app → Tap to start → Run (30–60 s) → Result card → Share or play again → Daily challenge & unlocks
```

### Controls

| Input | Action |
|---|---|
| Swipe left / right | Enter the left or right door |
| Tap and hold *(later modes)* | Charge a "Rizz Dash" through 3 doors at once |
| Swipe up *(3-door beats)* | Enter the middle door |
| No input before door line | Counts as a miss (*"you froze"*) |

> Swipe threshold is **40 px** with **150 ms** input buffering, so early swipes still register. Lanes are big enough to play with a thumb on a 6-inch phone.

---

## ⚙️ Game systems

Four systems drive tension: an **aura meter** that is your health, a **speed ramp** that shrinks your decision window, **combos** that multiply score, and **vibe shifts** that break autopilot.

### Aura meter (health)

| Event | Aura change |
|---|---|
| Start | 100 |
| Correct pick | **+5** (capped at 100) |
| Wrong pick | **−25** |
| Freeze (no input) | **−20** |
| Passive drain | **−2 / second** |

- Passive drain means standing still is never safe, and runs naturally end around 30–60 s.
- At **0** the run ends. Below **25** the screen tints and the music muffles (the "low aura" state).

### Speed ramp

| Beat range | Decision window | Notes |
|---|---|---|
| 1–10 | 1.4 s | Warm-up, obvious prompts only |
| 11–25 | 1.1 s | Mixed difficulty, first vibe shift |
| 26–45 | 0.85 s | 3-door beats introduced |
| 46+ | 0.65 s (floor) | Max speed, harder prompts, more shifts |

### Scoring and combos

- **Base score:** 100 per correct pick, plus up to +50 for swipe speed (if under 40% of the window).
- **Combo multiplier:**

  | Streak | Multiplier |
  |---|---|
  | 0–4 | ×1 |
  | 5 | ×2 |
  | 10 | ×3 |
  | 20 | ×4 *"Unhinged Streak"* |

  A miss resets it. At ×4 the character gets a glowing trail and the soundtrack adds a layer.

### Vibe shifts (rule changes every 10 beats)

| Shift | Rule |
|---|---|
| 🔁 **Opposite Day** | Pick the WRONG answer to the prompt |
| 🪞 **Mirror Mode** | Swipe direction is flipped |
| 🔊 **Sound Only** | Doors are blank; pick by audio cue |
| 👁️ **Speed Blink** | Doors are visible for only half the window |

Each shift is announced with a 0.8 s banner so it feels fair, not cheap.

### Fail states and second chances

- **One revive per run** (rewarded ad or a free daily token) restores 50 aura.
- Fail captions rotate from a pool of **200+** so failure stays fresh.

---

## 🚪 Prompt and door content

Content is the game: every prompt pairs a short instruction with two or three doors, and each pairing has one answer most players agree on within a second.

### Content rules

- Prompts are **2–6 words**, readable in under 0.5 s.
- The "right" answer must hit **≥ 75% agreement** in playtests; ambiguous pairings get cut or moved to Community mode.
- Doors are drawn in a consistent house style: **no real people, no brands, no copyrighted characters**, so content is safe to produce fast.
- Slang is used lightly and refreshed often; anything that ages badly is retired on a schedule.

### Prompt categories

| Category | How it works | Sample prompt | Door A vs Door B |
|---|---|---|---|
| **Energy** | Pick the vibe that matches | "Main character energy" | Person strutting in slow-mo rain vs person waiting for the bus |
| **Avoid** | Dodge the bad vibe | "Avoid the NPC" | Character saying "nice weather" on loop vs character doing a backflip |
| **Aesthetic** | Match a style | "Find the cottagecore" | Mushroom picnic vs neon gaming setup |
| **Situational** | Pick the right reaction | "Your crush texts back" | Calm phone face vs screaming into pillow |
| **Rank** | Pick the more extreme one | "More unhinged" | Cereal with water vs cereal with orange juice |
| **Sound** | Pick by audio | "Find the villain arc" | Ominous bass drop vs ukulele jingle |
| **Ick** | Spot the red flag | "Which one's the ick?" | Chewing loudly vs holding the door |

- **Launch content target:** 300 prompts across 7 categories, 600+ door illustrations, then 30–50 new prompts per week.
- **Difficulty tagging:** every prompt gets a difficulty score (1–5) from playtest agreement rate and average reaction time; the speed ramp pulls harder prompts as the run progresses.

---

## 🕹️ Game modes

Launch with **Classic** and **Daily Drop**; add the social modes once retention is proven.

| Mode | Description | Purpose | Release |
|---|---|---|---|
| **Classic** | Endless run until aura hits 0; chase your high score | Core loop, skill mastery | 🟢 Launch |
| **Daily Drop** | Same 30 prompts for everyone, once per day, one attempt | Daily habit + Wordle-style comparison | 🟢 Launch |
| **Duo Vibe** | Two friends play the same seeded run on separate phones; results show where you disagreed | Social spread, friend invites | 🟡 Update 1 |
| **Community Vibes** | Player-submitted prompts, voted on; "right" answer = majority vote | UGC, fresh content, debate | 🟡 Update 2 |
| **Squad Battle** | 4–8 players race the same run live; lowest aura each round is out | Competitive, streamable | 🟡 Update 3 |

**Daily Drop:** resets at midnight local time. The result is a compact emoji grid (✅❌⚡) plus a percentile (*"you were more unhinged than 82% of players"*). A streak counter rewards consecutive days.

**Duo Vibe:** after the run, a split screen shows each prompt where the two players chose differently, with banter captions (*"we need to talk about door #14"*). This is the main source of screenshot-worthy disagreement.

---

## 🎨 Progression, cosmetics and seasons

Progression is purely **cosmetic and identity-driven**: players earn ways to show off, never power.

### Currencies

| Currency | Type | Earned from | Buys |
|---|---|---|---|
| 💜 **Vibes** | Soft | Every run (~1 per 100 score), daily challenges | Standard cosmetics |
| ✨ **Sparkles** | Premium | Real money, or slowly through the season pass | Rare cosmetics |

### Player level and titles

XP from runs raises a player level. Every 5 levels unlocks a title shown on the result card, like **"Certified Yapper,"** **"Lowkey Legend,"** or **"Final Boss."** Titles are a cheap, high-impact identity reward.

### Cosmetic categories

| Category | Examples | Where it shows |
|---|---|---|
| **Fits** (outfits) | Thrift-store hoodie, Y2K butterfly set, goblin-mode sweats | Character in run and on result card |
| **Aura trails** | Sparkle dust, flame, pixel glitch, rain cloud | Behind character at high combos |
| **Door skins** | Holographic, vaporwave, hand-drawn sketch | All doors during runs |
| **Result card frames** | Polaroid, receipt, Notes app screenshot | Share card |
| **Emotes** | Victory dances after a run | Result screen, Duo Vibe |

### Seasons (6 weeks each)

Each season has a theme (e.g. *"Main Character Summer," "Spooky Szn," "Delulu Winter"*) with themed prompts, a **40-tier free track**, and a premium track. Daily and weekly challenges (*"get a ×4 combo 3 times," "finish 5 Daily Drops"*) feed season XP.

**Retention hooks:** daily login gift, Daily Drop streak, weekly challenge reset every Monday, and limited-time cosmetics that create FOMO without paywalling gameplay.

---

## 📲 Social and virality

The result card is the game's main marketing channel, so it gets as much design attention as the gameplay itself.

### Result card (end of every run)

- **Aura personality read:** *"Your aura: 94% Chaotic Neutral"*, from a pool of 40+ archetypes chosen by the categories you nailed or failed.
- **Roast line** based on your worst miss (*"picked cereal with water as 'normal'. seek help"*).
- Score, best combo, player title, equipped fit and frame.
- Sized **9:16** for Stories/TikTok and **1:1** for group chats; one-tap share with a deep link back to the game.

### Built-in sharing loops

- **Challenge a friend:** a share link opens the exact same seeded run, so friends play your run and compare.
- **Daily Drop grid:** spoiler-free emoji grid, pasteable anywhere.
- **Auto clip:** optional 10-second replay of your best combo, exported as video with a game watermark.
- **Duo Vibe disagreements:** shareable *"we disagreed on 7 doors"* card.

### Creator hooks

- Streamer-friendly Squad Battle with a spectator view.
- Creator codes that give followers a free cosmetic, tracked for partnership deals.
- Weekly "most debated prompt" stat published on socials to spark comment-section arguments.

**Referral reward:** both inviter and friend get a limited fit when the friend completes 3 runs.

---

## 🖌️ Art, audio and UI/UX

The look is bold, flat and sticker-like, so doors read in a fraction of a second and new content is cheap to draw.

### Art direction

- Thick outlines, flat colors, slightly wobbly hand-drawn edges, like stickers on a phone case.
- **Character:** a small, expressive blob-like avatar with a big face, easy to dress and animate.
- **Background:** a looping hallway whose palette shifts with the season and with combo level.
- Each door must be recognizable as a silhouette at 50% size; every illustration is tested at thumbnail scale.

### Game feel ("juice")

- Screen shake and a short freeze-frame (60 ms) on every correct pick.
- Particle bursts that grow with the combo multiplier.
- Slapstick fail animations: the character bonks off the door, faceplants, or gets sucked into the NPC door.
- Haptic tick on swipe, stronger buzz on miss.

### Audio

- Lo-fi / hyperpop-style loops, original or licensed, that add layers as the combo grows.
- A short, satisfying "pop" on each correct pick, pitched up as the streak climbs.
- Funny vocal stingers on fail (*"oop," "bruh,"* a sad trombone), recorded in-house.
- Fully playable on mute; Sound-category prompts show a caption fallback.

### UI/UX

- **Home screen:** one big "tap to vibe" button over the hallway; modes and shop in a bottom bar.
- **In-run HUD:** prompt (top), aura bar (top-left), combo (top-right), nothing else.
- Dark mode by default with high-contrast text; colorblind-safe palette for good/bad feedback.
- First-time players get a 3-beat interactive tutorial inside a real run, no text walls.

---

## 💰 Monetization

Revenue comes from opt-in rewarded ads plus cosmetics. **Nothing sold affects score**, so leaderboards stay fair.

| Stream | How it works | Guardrail |
|---|---|---|
| **Rewarded ads** | Watch to revive once, double run rewards, or get a daily bonus spin | Always optional; max 1 revive per run |
| **Interstitial ads** | Shown between runs, at most once every 3 minutes | Never mid-run; removed by Ad-Free purchase |
| **Season pass** | Premium cosmetic track each 6-week season, low price point | Free track stays meaningful |
| **Cosmetic shop** | Rotating fits, trails, frames for Sparkles | No loot boxes; prices shown upfront |
| **Ad-Free** | One-time purchase removes interstitials | Rewarded ads remain opt-in |
| **Brand collabs** *(later)* | Sponsored themed weeks with original art | Clearly labeled; no forced brand prompts |

> [!NOTE]
> Pricing should be set per region after soft-launch testing. Avoid loot boxes and gacha mechanics: they put off much of this audience and face growing regulation.

---

## 🛡️ Live ops and moderation

A weekly content drop keeps the game current; it needs a lightweight pipeline and firm moderation rules.

### Weekly content pipeline

| Day | Step |
|---|---|
| **Monday** | Trend scan (what's being said on social, what's dying); pick 30–50 prompt ideas |
| **Tue–Wed** | Write prompts and illustrate doors in the house style |
| **Thursday** | Internal playtest for agreement rate and reaction time; cut anything under 75% agreement |
| **Friday** | Push via server-side content bundle (no app update needed) |
| **Next week** | Review analytics; retire prompts with low agreement or high skip rates |

### Content and moderation rules

- No real people, brands, politics, religion, body shaming, or jokes punching at identity groups.
- Humor targets **situations and behaviors** (*"chewing loudly"*), never who someone is.
- Community submissions pass an automated filter, then human review before entering voting.
- Players can report any prompt; 3+ reports auto-hide it pending review.
- Age rating target: **12+**. No chat between strangers, only preset emotes in multiplayer.

**Live events:** monthly 3-day events (*"Delulu Weekend"*) with special rules and event-only cosmetics.

---

## 🧰 Technical plan and KPIs

Build in Unity (or Godot for a smaller team) with server-driven content so prompts ship without app updates.

### Tech stack

| Layer | Choice | Why |
|---|---|---|
| **Engine** | Unity 2D (alt: Godot 4) | Mature mobile export, ad SDKs, fast iteration |
| **Web prototype** | HTML5 / Phaser | Test the core loop in days, share via link |
| **Backend** | Firebase or Supabase | Auth, leaderboards, Daily Drop seeds, remote config |
| **Content delivery** | Remote JSON + addressable art bundles | Weekly drops without store review |
| **Ads** | AppLovin MAX or Unity LevelPlay mediation | Rewarded + interstitial with mediation |
| **Analytics** | Firebase Analytics + GameAnalytics | Funnels, retention, A/B tests |
| **Multiplayer** *(later)* | Photon or Nakama | Squad Battle real-time rooms |

### Performance targets

| Metric | Target |
|---|---|
| Frame rate | 60 fps on mid-range Android (3 GB RAM) |
| Install size | < 80 MB |
| Cold start to playable | < 3 s |
| Input latency | < 50 ms |

**Key events to track:** run start, run end (score, beats, cause), each pick (prompt id, choice, reaction ms), share tapped, revive used, ad watched, purchase.

### KPI targets (soft launch)

| Metric | Target | Why it matters |
|---|---|---|
| CPI (US) | < $0.50 | Validate with ad creative tests before full build |
| Day 1 retention | 40%+ | Core loop and onboarding health |
| Day 7 retention | 15%+ | Daily Drop and challenges working |
| Avg. session length | 5+ min | Runs-per-session pull |
| Share rate | 8%+ of runs | Result card virality |
| Prompt agreement | 75%+ average | Content quality signal |

---

## 🗺️ MVP scope, roadmap and risks

Prove the fun and the ad cost before building anything social: a **2-week prototype**, a **6-week MVP**, then a **soft launch** that must hit the retention targets.

> [!IMPORTANT]
> If a gate fails, iterate inside that phase (new prompts, retuned ramp, new ad creatives) rather than moving on. Week counts assume a team of 2–4 people.

### MVP feature checklist

- [ ] Swipe controls with input buffering
- [ ] Aura meter, passive drain, speed ramp, combo multiplier
- [ ] Two vibe shifts (Opposite Day, Mirror Mode)
- [ ] 300 prompts across 7 categories, server-delivered
- [ ] Classic and Daily Drop modes
- [ ] Result card with aura archetype, roast line and one-tap share
- [ ] 10 unlockable fits and 5 aura trails (soft currency only)
- [ ] Rewarded revive and capped interstitials
- [ ] Analytics events and remote config

**Deliberately cut from MVP:** Duo Vibe, Community Vibes, Squad Battle, season pass, premium currency, and Sound-only prompts. Each waits until retention is proven.

### Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Slang and trends age quickly | Game feels cringe within months | Base library of timeless situational prompts; slang used lightly and retired on schedule |
| Subjective prompts feel unfair | Players quit after "wrong" answers they disagree with | 75% agreement gate; debatable prompts moved to Community mode |
| Humor offends a group | Bad press, store removal | Moderation rules, human review, report button |
| Weekly content is costly to make | Live ops stalls | Modular door art kit, templated prompts, community submissions later |
| Low hyper-casual retention | High CPI never pays back | Daily Drop, streaks, seasons and social modes as retention layers |
| Ad fatigue | Reviews drop, players churn | Interstitial cap, never mid-run, Ad-Free option |

---

<div align="center">

*Made with ✨ vibes ✨ and Unity*

</div>
