using UnityEngine;

namespace Brooklyneir.Audio
{
    /// <summary>
    /// Runtime synthesis used only if a WAV is missing from Resources/Audio. Produces short arcade
    /// sounds (clicks, pops, sweeps, chords) and a simple musical loop. Mono, 22.05 kHz.
    /// </summary>
    public static class SynthFallback
    {
        const int Rate = 22050;

        public static AudioClip Make(Sfx s)
        {
            switch (s)
            {
                case Sfx.HitClean: return Pop("hit_clean", 1350f, 0.07f, 0.9f, 0.25f);
                case Sfx.HitSoft: return Pop("hit_soft", 900f, 0.06f, 0.6f, 0.15f);
                case Sfx.HitSmash: return Pop("hit_smash", 700f, 0.16f, 1f, 0.6f);
                case Sfx.Bounce: return Pop("bounce", 1900f, 0.05f, 0.7f, 0.1f);
                case Sfx.Net: return Noise("net", 0.12f, 0.6f, 0.05f);
                case Sfx.Whoosh: return Noise("whoosh", 0.22f, 0.35f, 0.5f);
                case Sfx.Serve: return Pop("serve", 1150f, 0.06f, 0.7f, 0.2f);
                case Sfx.Point: return Arp("point", new[] { 660f, 880f }, 0.08f, 0.5f);
                case Sfx.PowerFire: return Sweep("power_fire", 300f, 900f, 0.35f, 0.7f, true);
                case Sfx.PowerSlow: return Sweep("power_slow", 800f, 200f, 0.5f, 0.6f, false);
                case Sfx.PowerCurve: return Sweep("power_curve", 400f, 1200f, 0.3f, 0.6f, false);
                case Sfx.PowerSmash: return Sweep("power_smash", 120f, 600f, 0.4f, 0.8f, true);
                case Sfx.PowerShield: return Arp("power_shield", new[] { 523f, 659f, 784f }, 0.07f, 0.6f);
                case Sfx.PowerFreeze: return Arp("power_freeze", new[] { 1568f, 1319f, 1047f }, 0.06f, 0.5f);
                case Sfx.PowerRandom: return Arp("power_random", new[] { 440f, 880f, 660f, 990f }, 0.05f, 0.5f);
                case Sfx.Pickup: return Arp("pickup", new[] { 784f, 1175f }, 0.06f, 0.6f);
                case Sfx.Special: return Sweep("special", 200f, 1600f, 0.5f, 0.8f, true);
                case Sfx.Unlock: return Arp("unlock", new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.6f);
                case Sfx.Coin: return Arp("coin", new[] { 988f, 1319f }, 0.06f, 0.5f);
                case Sfx.Win: return Arp("win", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.12f, 0.7f);
                case Sfx.Lose: return Arp("lose", new[] { 392f, 330f, 262f }, 0.18f, 0.6f);
                case Sfx.UiClick: return Pop("ui_click", 2200f, 0.03f, 0.4f, 0f);
                case Sfx.UiBack: return Pop("ui_back", 1400f, 0.04f, 0.4f, 0f);
                case Sfx.UiError: return Arp("ui_error", new[] { 220f, 185f }, 0.07f, 0.5f);
                case Sfx.Countdown: return Arp("countdown", new[] { 880f }, 0.1f, 0.5f);
                case Sfx.CountdownGo: return Arp("countdown_go", new[] { 1320f }, 0.25f, 0.6f);
                case Sfx.ShieldSave: return Sweep("shield_save", 1200f, 500f, 0.3f, 0.7f, false);
                case Sfx.Cheer: return Noise("cheer", 1.2f, 0.35f, 0.8f);
                default: return Pop(s.ToString(), 1000f, 0.05f, 0.5f, 0f);
            }
        }

        static AudioClip Create(string name, float[] data)
        {
            AudioClip c = AudioClip.Create("synth_" + name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Pop(string name, float freq, float dur, float vol, float body)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            float[] d = new float[n];
            uint seed = 1234567u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t / (dur * 0.25f));
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 9) / 8388608f - 1f) * Mathf.Exp(-t * 400f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t * (1f - t * 2f)) * env;
                float thump = Mathf.Sin(2f * Mathf.PI * freq * 0.25f * t) * env * body;
                d[i] = (tone * 0.6f + noise * 0.5f + thump) * vol * 0.8f;
            }
            return Create(name, d);
        }

        static AudioClip Noise(string name, float dur, float vol, float smooth)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            float[] d = new float[n];
            uint seed = 987654u;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                seed = seed * 1664525u + 1013904223u;
                float w = (seed >> 9) / 8388608f - 1f;
                lp = Mathf.Lerp(w, lp, smooth);
                d[i] = lp * env * vol;
            }
            return Create(name, d);
        }

        static AudioClip Sweep(string name, float f0, float f1, float dur, float vol, bool grit)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            float[] d = new float[n];
            float phase = 0f;
            uint seed = 4242u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / dur;
                float f = Mathf.Lerp(f0, f1, k * k * (3f - 2f * k));
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * (1f - k * 0.3f);
                float s = Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f);
                if (grit)
                {
                    seed = seed * 1664525u + 1013904223u;
                    s += ((seed >> 9) / 8388608f - 1f) * 0.25f;
                }
                d[i] = s * env * vol * 0.5f;
            }
            return Create(name, d);
        }

        static AudioClip Arp(string name, float[] notes, float noteDur, float vol)
        {
            int per = Mathf.CeilToInt(noteDur * Rate);
            int tail = Mathf.CeilToInt(0.15f * Rate);
            float[] d = new float[per * notes.Length + tail];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = k * per;
                for (int i = 0; i < per + tail && start + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 400f);
                    float f = notes[k];
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f + (Mathf.Repeat(f * t, 1f) - 0.5f) * 0.15f;
                    d[start + i] += s * env * vol * 0.6f;
                }
            }
            return Create(name, d);
        }

        /// <summary>Four-bar loop: bass, chords and drums. Faster and brighter for matches.</summary>
        public static AudioClip MusicLoop(bool match)
        {
            float bpm = match ? 118f : 92f;
            float beat = 60f / bpm;
            int bars = 4;
            int n = Mathf.CeilToInt(beat * 4 * bars * Rate);
            float[] d = new float[n];
            float[] roots = match ? new[] { 110f, 87.31f, 130.81f, 98f } : new[] { 98f, 82.41f, 110f, 73.42f };
            uint seed = 777u;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = Mathf.Min(bars - 1, (int)(t / (beat * 4)));
                float inBeat = Mathf.Repeat(t, beat);
                float root = roots[bar];
                float bass = Mathf.Sin(2f * Mathf.PI * root * t) * Mathf.Exp(-inBeat * 3f) * 0.35f;
                float chord = (Mathf.Sin(2f * Mathf.PI * root * 2f * t) + Mathf.Sin(2f * Mathf.PI * root * 2.52f * t) + Mathf.Sin(2f * Mathf.PI * root * 3f * t)) * 0.06f;
                float kick = Mathf.Sin(2f * Mathf.PI * (60f + 90f * Mathf.Exp(-inBeat * 30f)) * inBeat) * Mathf.Exp(-inBeat * 12f) * 0.5f;
                seed = seed * 1664525u + 1013904223u;
                float w = (seed >> 9) / 8388608f - 1f;
                float half = Mathf.Repeat(t, beat * 0.5f);
                float hat = w * Mathf.Exp(-half * 60f) * (match ? 0.12f : 0.06f);
                int beatIndex = (int)(t / beat) % 4;
                float snare = (beatIndex == 1 || beatIndex == 3) ? w * Mathf.Exp(-inBeat * 18f) * 0.22f : 0f;
                d[i] = Mathf.Clamp(bass + chord + kick + hat + snare, -1f, 1f) * 0.6f;
            }
            AudioClip c = AudioClip.Create(match ? "synth_match_loop" : "synth_menu_loop", n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
