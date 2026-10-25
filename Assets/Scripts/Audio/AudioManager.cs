using System.Collections.Generic;
using UnityEngine;

namespace Brooklyneir.Audio
{
    public enum Sfx
    {
        HitClean, HitSoft, HitSmash, Bounce, Net, Whoosh, Serve, Point, PowerFire, PowerSlow, PowerCurve,
        PowerSmash, PowerShield, PowerFreeze, PowerRandom, Pickup, Special, Unlock, Coin, Win, Lose, UiClick, UiBack,
        UiError, Countdown, CountdownGo, ShieldSave, Cheer
    }

    public enum MusicTrack { None, Menu, Match }

    /// <summary>
    /// Music and effects with independent volumes. Clips are loaded from Resources/Audio
    /// (WAV files generated for the project). If a clip is missing, a synthesised fallback of the
    /// same character is generated at runtime, so the game never references a missing file.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int VoiceCount = 10;
        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        AudioSource _musicA, _musicB;
        bool _musicAActive = true;
        MusicTrack _current = MusicTrack.None;
        float _musicVolume = 0.7f, _sfxVolume = 0.9f;
        float _fade = 1f;
        int _nextVoice;
        AudioClip _menuMusic, _matchMusic;
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();

        static string FileName(Sfx s)
        {
            switch (s)
            {
                case Sfx.HitClean: return "hit_clean";
                case Sfx.HitSoft: return "hit_soft";
                case Sfx.HitSmash: return "hit_smash";
                case Sfx.Bounce: return "bounce";
                case Sfx.Net: return "net";
                case Sfx.Whoosh: return "whoosh";
                case Sfx.Serve: return "serve";
                case Sfx.Point: return "point";
                case Sfx.PowerFire: return "power_fire";
                case Sfx.PowerSlow: return "power_slow";
                case Sfx.PowerCurve: return "power_curve";
                case Sfx.PowerSmash: return "power_smash";
                case Sfx.PowerShield: return "power_shield";
                case Sfx.PowerFreeze: return "power_freeze";
                case Sfx.PowerRandom: return "power_random";
                case Sfx.Pickup: return "pickup";
                case Sfx.Special: return "special";
                case Sfx.Unlock: return "unlock";
                case Sfx.Coin: return "coin";
                case Sfx.Win: return "win";
                case Sfx.Lose: return "lose";
                case Sfx.UiClick: return "ui_click";
                case Sfx.UiBack: return "ui_back";
                case Sfx.UiError: return "ui_error";
                case Sfx.Countdown: return "countdown";
                case Sfx.CountdownGo: return "countdown_go";
                case Sfx.ShieldSave: return "shield_save";
                case Sfx.Cheer: return "cheer";
                default: return s.ToString().ToLowerInvariant();
            }
        }

        public void Init(float musicVolume, float sfxVolume)
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                _voices[i] = a;
            }
            _musicA = gameObject.AddComponent<AudioSource>();
            _musicB = gameObject.AddComponent<AudioSource>();
            _musicA.loop = _musicB.loop = true;
            _musicA.playOnAwake = _musicB.playOnAwake = false;
            _musicA.priority = _musicB.priority = 0;

            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
            {
                AudioClip c = Resources.Load<AudioClip>("Audio/SFX/" + FileName(s));
                if (c == null)
                {
                    c = SynthFallback.Make(s);
                    Debug.LogWarning("[TTLB] Missing SFX 'Audio/SFX/" + FileName(s) + "', using synthesised fallback.");
                }
                _clips[s] = c;
            }
            _menuMusic = Resources.Load<AudioClip>("Audio/Music/menu_loop");
            _matchMusic = Resources.Load<AudioClip>("Audio/Music/match_loop");
            if (_menuMusic == null) { _menuMusic = SynthFallback.MusicLoop(false); Debug.LogWarning("[TTLB] Missing menu music, using synthesised loop."); }
            if (_matchMusic == null) { _matchMusic = SynthFallback.MusicLoop(true); Debug.LogWarning("[TTLB] Missing match music, using synthesised loop."); }
            SetVolumes(musicVolume, sfxVolume);
        }

        public void SetVolumes(float music, float sfx)
        {
            _musicVolume = Mathf.Clamp01(music);
            _sfxVolume = Mathf.Clamp01(sfx);
            ApplyMusicVolume();
        }

        public float MusicVolume { get { return _musicVolume; } }
        public float SfxVolume { get { return _sfxVolume; } }

        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (_sfxVolume <= 0.001f) return;
            AudioClip c;
            if (!_clips.TryGetValue(s, out c) || c == null) return;
            // Avoid stacking the same sound in the same frame (two contacts in one physics step).
            float last;
            if (_lastPlayed.TryGetValue(s, out last) && Time.unscaledTime - last < 0.03f) return;
            _lastPlayed[s] = Time.unscaledTime;
            AudioSource v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            v.Stop();
            v.clip = c;
            v.volume = Mathf.Clamp01(volume) * _sfxVolume;
            v.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            v.Play();
        }

        public void PlayMusic(MusicTrack track)
        {
            if (track == _current) return;
            _current = track;
            AudioClip clip = track == MusicTrack.Menu ? _menuMusic : (track == MusicTrack.Match ? _matchMusic : null);
            AudioSource next = _musicAActive ? _musicB : _musicA;
            _musicAActive = !_musicAActive;
            if (clip != null)
            {
                next.clip = clip;
                next.volume = 0f;
                next.Play();
            }
            _fade = 0f;
        }

        /// <summary>Lowers the music while paused.</summary>
        public void SetDucked(bool ducked)
        {
            _duck = ducked ? 0.35f : 1f;
            ApplyMusicVolume();
        }

        float _duck = 1f;

        void Update()
        {
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / 0.8f);
                ApplyMusicVolume();
                if (_fade >= 1f)
                {
                    AudioSource old = _musicAActive ? _musicB : _musicA;
                    old.Stop();
                }
            }
        }

        void ApplyMusicVolume()
        {
            if (_musicA == null) return;
            AudioSource active = _musicAActive ? _musicA : _musicB;
            AudioSource old = _musicAActive ? _musicB : _musicA;
            float v = _musicVolume * 0.55f * _duck;
            active.volume = v * _fade;
            old.volume = v * (1f - _fade);
        }

        public void StopAllSfx()
        {
            for (int i = 0; i < VoiceCount; i++) if (_voices[i] != null) _voices[i].Stop();
        }
    }
}
