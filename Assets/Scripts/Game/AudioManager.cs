using System.Collections.Generic;
using AutoBattler.Core;
using UnityEngine;

namespace AutoBattler.Game
{
    public enum Sfx
    {
        WarriorSwing,
        LancerSwing,
        MeleeHit,
        HeavyHit,
        BowRelease,
        ArrowImpact,
        Heal,
        Death,
        Buy,
        Sell,
        Reroll,
        Xp,
        Click,
        Confirm,
        Error,
    }

    public enum Jingle
    {
        RoundStart,
        BossRound,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Plays Kenney sound effects and jingles loaded from Resources/Audio. Each effect has a few variants; combat
    /// sounds also get a small random pitch shift so repeated hits don't sound identical.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int VoiceCount = 12;
        private const int CombatPitchSpread = 8; // percent
        /// <summary>The same effect won't restart within this window, so simultaneous hits don't stack into a spike.</summary>
        private const float RepeatGuardSeconds = 0.035f;

        private static readonly Dictionary<Sfx, string[]> SfxFiles = new Dictionary<Sfx, string[]>
        {
            { Sfx.WarriorSwing, new[] { "drawKnife1", "drawKnife2", "drawKnife3" } },
            { Sfx.LancerSwing, new[] { "knifeSlice", "knifeSlice2" } },
            { Sfx.MeleeHit, new[] { "impactPunch_medium_000", "impactPunch_medium_001", "impactPunch_medium_002" } },
            { Sfx.HeavyHit, new[] { "impactPunch_heavy_000", "impactPunch_heavy_001" } },
            { Sfx.BowRelease, new[] { "pluck_001", "pluck_002" } },
            { Sfx.ArrowImpact, new[] { "impactWood_light_000", "impactWood_light_001", "impactWood_light_002" } },
            { Sfx.Heal, new[] { "glass_004" } },
            { Sfx.Death, new[] { "impactSoft_heavy_000", "impactSoft_heavy_001" } },
            { Sfx.Buy, new[] { "handleCoins2" } },
            { Sfx.Sell, new[] { "handleCoins" } },
            { Sfx.Reroll, new[] { "bookFlip2" } },
            { Sfx.Xp, new[] { "maximize_004" } },
            { Sfx.Click, new[] { "click_001" } },
            { Sfx.Confirm, new[] { "confirmation_001" } },
            { Sfx.Error, new[] { "error_004" } },
        };

        // Pizzicato set from Music Jingles: 02 rises, 07 falls, 10 is a short upbeat lift, 03 is a tense trill.
        private static readonly Dictionary<Jingle, string> JingleFiles = new Dictionary<Jingle, string>
        {
            { Jingle.RoundStart, "jingles_PIZZI10" },
            { Jingle.BossRound, "jingles_PIZZI03" },
            { Jingle.Victory, "jingles_PIZZI02" },
            { Jingle.Defeat, "jingles_PIZZI07" },
        };

        private readonly Dictionary<Sfx, AudioClip[]> _sfx = new Dictionary<Sfx, AudioClip[]>();
        private readonly Dictionary<Jingle, AudioClip> _jingles = new Dictionary<Jingle, AudioClip>();
        private readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        private AudioSource[] _voices;
        private AudioSource _jingleSource;
        private int _nextVoice;
        private IRandom _rng;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<AudioManager>();
            manager.Load();
            return manager;
        }

        private void Load()
        {
            _rng = new SeededRandom(System.Environment.TickCount);
            foreach (var pair in SfxFiles)
            {
                var clips = new List<AudioClip>();
                foreach (var file in pair.Value)
                {
                    var clip = Resources.Load<AudioClip>("Audio/Sfx/" + file);
                    if (clip != null) clips.Add(clip);
                    else Debug.LogWarning($"Missing sound Resources/Audio/Sfx/{file}");
                }
                _sfx[pair.Key] = clips.ToArray();
            }
            foreach (var pair in JingleFiles)
            {
                var clip = Resources.Load<AudioClip>("Audio/Jingles/" + pair.Value);
                if (clip != null) _jingles[pair.Key] = clip;
                else Debug.LogWarning($"Missing jingle Resources/Audio/Jingles/{pair.Value}");
            }

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) _voices[i] = NewSource();
            _jingleSource = NewSource();
        }

        private AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D: the listener position doesn't matter
            return source;
        }

        /// <summary>Plays a sound effect. Combat sounds get pitch variation.</summary>
        public void Play(Sfx sfx, float volume = 1f)
        {
            if (!_sfx.TryGetValue(sfx, out var clips) || clips.Length == 0) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < RepeatGuardSeconds) return;
            _lastPlayed[sfx] = now;

            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.pitch = IsCombat(sfx) ? SoundVariation.Pitch(_rng, CombatPitchSpread) : 1f;
            voice.PlayOneShot(clips[_rng.Next(clips.Length)], volume);
        }

        public void PlayJingle(Jingle jingle)
        {
            if (!_jingles.TryGetValue(jingle, out var clip)) return;
            _jingleSource.Stop();
            _jingleSource.clip = clip;
            _jingleSource.Play();
        }

        /// <summary>Master volume for everything, as a percentage (0 mutes).</summary>
        public static void SetMasterVolume(int percent) => AudioListener.volume = Mathf.Clamp01(percent / 100f);

        private static bool IsCombat(Sfx sfx) => sfx <= Sfx.Death;
    }
}
