using UnityEngine;

namespace VibeRun.Services
{
    /// <summary>
    /// Synthesised placeholder audio: a lo-fi loop with an extra layer that fades in at max combo, a "pop" pitched up
    /// with the streak, slapstick fail stingers and a sad trombone. Low aura muffles the music with a low-pass filter.
    /// Replace clips with recorded/licensed audio by assigning the public fields.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int Rate = 44100;
        public AudioClip pop, bonk, oop, trombone, whoosh, banner, click, coin, musicBase, musicLayer;

        AudioSource _music, _layer, _sfx;
        AudioLowPassFilter _lp;
        float _layerTarget, _lpTarget = 22000f, _musicTarget;
        public bool Muted { get; private set; }

        public static AudioManager Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            Instance = this;
            pop = Make("pop", 0.09f, (t, d) => Sine(t, Mathf.Lerp(880, 1500, t / d)) * Env(t, 0.002f, 0.08f) * 0.6f);
            bonk = Make("bonk", 0.28f, (t, d) => (Sine(t, Mathf.Lerp(190, 55, t / d)) * 0.9f + Noise() * 0.3f * Env(t, 0.001f, 0.04f)) * Env(t, 0.002f, 0.25f));
            oop = Make("oop", 0.32f, (t, d) => Saw(t, t < 0.13f ? 520 : 340) * Env(t, 0.01f, 0.3f) * 0.25f * (0.6f + 0.4f * Sine(t, 6)));
            trombone = Make("trombone", 1.9f, Trombone);
            whoosh = Make("whoosh", 0.16f, (t, d) => Noise() * Mathf.Sin(Mathf.PI * t / d) * 0.18f);
            banner = Make("banner", 0.5f, (t, d) => Square(t, 392f * Mathf.Pow(2f, Mathf.Floor(t / 0.1f) * 4f / 12f)) * Env(t % 0.1f, 0.003f, 0.09f) * 0.18f);
            click = Make("click", 0.03f, (t, d) => Noise() * Env(t, 0.0005f, 0.025f) * 0.3f);
            coin = Make("coin", 0.25f, (t, d) => Square(t, t < 0.07f ? 988 : 1319) * Env(t, 0.002f, 0.23f) * 0.15f);
            musicBase = MakeMusic(false);
            musicLayer = MakeMusic(true);

            _music = gameObject.AddComponent<AudioSource>();
            _layer = gameObject.AddComponent<AudioSource>();
            _music.clip = musicBase; _layer.clip = musicLayer;
            _music.loop = _layer.loop = true;
            _music.volume = 0f; _layer.volume = 0f;
            _music.playOnAwake = _layer.playOnAwake = false;
            _lp = gameObject.AddComponent<AudioLowPassFilter>();
            _lp.cutoffFrequency = 22000f;

            var sfxGo = new GameObject("sfx");
            sfxGo.transform.SetParent(transform, false);
            _sfx = sfxGo.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
        }

        void Start()
        {
            _music.Play();
            _layer.Play();
            _layer.timeSamples = _music.timeSamples;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _music.volume = Mathf.MoveTowards(_music.volume, Muted ? 0 : _musicTarget, dt * 0.8f);
            _layer.volume = Mathf.MoveTowards(_layer.volume, Muted ? 0 : _layerTarget, dt * 0.8f);
            _lp.cutoffFrequency = Mathf.Lerp(_lp.cutoffFrequency, _lpTarget, dt * 6f);
            if (Mathf.Abs(_layer.timeSamples - _music.timeSamples) > 2048) _layer.timeSamples = _music.timeSamples;
        }

        public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0f : 1f; }
        public void SetMusic(float volume) => _musicTarget = volume;
        public void SetComboLayer(bool on) => _layerTarget = on ? 0.45f : 0f;
        public void SetLowAura(bool low) => _lpTarget = low ? 700f : 22000f;

        public void PlayPop(int streak) => Play(pop, 0.8f, 1f + Mathf.Min(streak, 24) * 0.035f);
        public void PlayFail() { Play(bonk, 0.9f, 1f); Play(oop, 0.7f, Random.Range(0.9f, 1.1f)); }
        public void PlayFreeze() => Play(oop, 0.7f, 0.75f);
        public void PlayTrombone() => Play(trombone, 0.8f, 1f);
        public void PlayWhoosh() => Play(whoosh, 0.6f, Random.Range(0.9f, 1.2f));
        public void PlayBanner() => Play(banner, 0.7f, 1f);
        public void PlayClick() => Play(click, 0.6f, 1f);
        public void PlayCoin() => Play(coin, 0.6f, 1f);

        void Play(AudioClip c, float vol, float pitch)
        {
            if (c == null || _sfx == null) return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(c, vol);
        }

        // ---------------- synthesis ----------------

        static System.Random _rng = new System.Random(7);

        static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Saw(float t, float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
        static float Square(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t) >= 0 ? 1f : -1f;
        static float Noise() => (float)(_rng.NextDouble() * 2 - 1);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay) * 3f);

        static float Trombone(float t, float d)
        {
            float[] notes = { 293.66f, 277.18f, 261.63f, 246.94f };
            float[] lens = { 0.35f, 0.35f, 0.35f, 0.85f };
            float start = 0;
            for (int i = 0; i < 4; i++)
            {
                if (t < start + lens[i])
                {
                    float lt = t - start;
                    float vib = i == 3 ? Mathf.Sin(lt * 2 * Mathf.PI * 5.5f) * 6f : 0;
                    float f = notes[i] + vib - (i == 3 ? lt * 15f : 0);
                    float env = Mathf.Min(1, lt / 0.03f) * (i == 3 ? Mathf.Exp(-lt * 1.6f) : Mathf.Exp(-lt * 2f));
                    return (Saw(t, f) * 0.6f + Sine(t, f * 2) * 0.2f) * env * 0.35f;
                }
                start += lens[i];
            }
            return 0;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>4 bars at 96 bpm, Am–F–C–G. Base = drums, bass, pad. Layer = sparkly 16th arpeggio.</summary>
        static AudioClip MakeMusic(bool layer)
        {
            const float bpm = 96f;
            float beat = 60f / bpm;
            int bars = 4;
            float len = bars * 4 * beat;
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f },
                new[] { 130.81f, 164.81f, 196f }, new[] { 196f, 246.94f, 293.66f },
            };
            var rng = new System.Random(layer ? 11 : 3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = Mathf.Min(bars - 1, (int)(t / (4 * beat)));
                float inBar = t - bar * 4 * beat;
                float inBeat = t % beat;
                int beatIdx = (int)(inBar / beat);
                var ch = chords[bar];
                float s = 0;
                if (!layer)
                {
                    // kick on 1 and 3 (and the "and" of 3 for swing)
                    if (beatIdx == 0 || beatIdx == 2) s += Mathf.Sin(2 * Mathf.PI * (50 + 90 * Mathf.Exp(-inBeat * 30)) * inBeat) * Mathf.Exp(-inBeat * 9) * 0.55f;
                    // snare-ish on 2 and 4
                    if (beatIdx == 1 || beatIdx == 3) s += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-inBeat * 18) * 0.22f;
                    // hats on 8ths
                    float in8 = t % (beat / 2);
                    s += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-in8 * 90) * 0.06f;
                    // bass: root, an octave down
                    float bf = ch[0] / 2f;
                    s += Mathf.Sin(2 * Mathf.PI * bf * t) * 0.22f * (0.6f + 0.4f * Mathf.Exp(-inBeat * 4));
                    // warm pad with slow wobble
                    float pad = 0;
                    foreach (var f in ch) pad += Mathf.Sin(2 * Mathf.PI * f * t * (1 + 0.002f * Mathf.Sin(t * 3))) ;
                    s += pad * 0.045f;
                }
                else
                {
                    float sixteenth = beat / 4f;
                    int step = (int)(inBar / sixteenth);
                    float inStep = inBar - step * sixteenth;
                    float f = ch[step % 3] * (step % 8 < 4 ? 2f : 4f);
                    float tri = 2f * Mathf.Abs(2f * (t * f - Mathf.Floor(t * f + 0.5f))) - 1f;
                    s += tri * Mathf.Exp(-inStep * 22) * 0.16f;
                }
                // tiny fade at loop edges to avoid clicks
                float edge = Mathf.Min(1f, Mathf.Min(t, len - t) / 0.01f);
                data[i] = Mathf.Clamp(s * edge, -1, 1);
            }
            var clip = AudioClip.Create(layer ? "music_layer" : "music_base", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
