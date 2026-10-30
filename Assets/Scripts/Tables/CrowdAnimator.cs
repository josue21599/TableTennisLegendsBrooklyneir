using System.Collections.Generic;
using Brooklyneir.Art;
using UnityEngine;

namespace Brooklyneir.Tables
{
    /// <summary>
    /// Lightweight animation for scenery: the crowd (idle bobbing, heads following the ball,
    /// applause, cheering jumps and a stadium wave, each fan reacting with its own small delay),
    /// spinning holograms, swaying palms, bobbing water and flickering torches. One component, no
    /// per-object scripts, no allocations per frame. Curves live in CrowdMotion (pure, tested) and
    /// the values in animation.json.
    /// </summary>
    public class CrowdAnimator : MonoBehaviour
    {
        struct Fan
        {
            public Transform Root, ArmL, ArmR, Head;
            public Vector3 Base;
            public float Phase, Delay, HeadSpeed, Angle, Yaw, Pitch;
        }
        struct Spinner { public Transform T; public Vector3 Speed; }
        struct Bobber { public Transform T; public Vector3 Base; public float Amp, Freq; }
        struct Sway { public Transform T; public Quaternion Base; public float Deg, Freq, Phase; }

        readonly List<Fan> _fans = new List<Fan>();
        readonly List<Spinner> _spinners = new List<Spinner>();
        readonly List<Bobber> _bobbers = new List<Bobber>();
        readonly List<Sway> _sways = new List<Sway>();
        readonly List<Transform> _flickers = new List<Transform>();
        bool _crowdVisible = true;
        CrowdReaction _reaction = CrowdReaction.None;
        float _reactionTime;
        Vector3 _focus;
        bool _hasFocus;

        public int FanCount { get { return _fans.Count; } }
        public CrowdReaction CurrentReaction { get { return _reaction; } }

        public void AddFan(Transform root, Transform armL, Transform armR, float phase)
        {
            AddFan(root, armL, armR, null, phase);
        }

        /// <param name="phase">Per-fan hash in [0, 1): sets its rhythm, reaction delay and head speed.</param>
        public void AddFan(Transform root, Transform armL, Transform armR, Transform head, float phase)
        {
            AnimationConfig c = AnimationSettings.Config;
            float h2 = Mathf.Repeat(phase * 7.31f + 0.17f, 1f);
            _fans.Add(new Fan
            {
                Root = root, ArmL = armL, ArmR = armR, Head = head,
                Base = root.localPosition,
                Phase = phase * 10f,
                Delay = CrowdMotion.ReactionDelay(h2, c.crowdReactionMaxDelay),
                HeadSpeed = CrowdMotion.HeadSpeed(Mathf.Repeat(phase * 3.7f, 1f), c.crowdHeadSpeedMin, c.crowdHeadSpeedMax),
                Angle = CrowdMotion.AngleAround(root.position)
            });
        }

        public void AddSpinner(Transform t, Vector3 degreesPerSecond) { _spinners.Add(new Spinner { T = t, Speed = degreesPerSecond }); }
        public void AddBobber(Transform t, float amplitude, float frequency) { _bobbers.Add(new Bobber { T = t, Base = t.localPosition, Amp = amplitude, Freq = frequency }); }
        public void AddSway(Transform t, float degrees, float frequency) { _sways.Add(new Sway { T = t, Base = t.localRotation, Deg = degrees, Freq = frequency, Phase = Random.value * 6f }); }
        public void AddFlicker(Transform t) { _flickers.Add(t); }

        /// <summary>Kept for compatibility: a strong cheer jumps, a mild one claps.</summary>
        public void Cheer(float intensity)
        {
            React(intensity >= 0.75f ? CrowdReaction.Cheer : CrowdReaction.Clap);
        }

        /// <summary>Starts a crowd reaction. A running wave is not interrupted by smaller reactions.</summary>
        public void React(CrowdReaction reaction)
        {
            if (reaction == CrowdReaction.None) return;
            AnimationConfig c = AnimationSettings.Config;
            if (_reaction == CrowdReaction.Wave && reaction != CrowdReaction.Wave &&
                _reactionTime < c.waveSeconds + c.crowdReactionMaxDelay) return;
            _reaction = reaction;
            _reactionTime = 0f;
        }

        /// <summary>Where the heads look (the ball); without focus they look around the table.</summary>
        public void SetFocus(Vector3 worldPosition, bool active)
        {
            _focus = worldPosition;
            _hasFocus = active;
        }

        public void SetCrowdVisible(bool visible)
        {
            _crowdVisible = visible;
            for (int i = 0; i < _fans.Count; i++)
                if (_fans[i].Root != null) _fans[i].Root.gameObject.SetActive(visible);
        }

        void Update()
        {
            float t = Time.time;
            float dt = Time.deltaTime;
            AnimationConfig c = AnimationSettings.Config;
            if (_reaction != CrowdReaction.None)
            {
                _reactionTime += dt;
                if (_reactionTime > CrowdMotion.Duration(_reaction, c) + c.crowdReactionMaxDelay) _reaction = CrowdReaction.None;
            }
            if (_crowdVisible)
            {
                for (int i = 0; i < _fans.Count; i++)
                {
                    Fan f = _fans[i];
                    if (f.Root == null) continue;
                    AnimateFan(ref f, t, dt, c);
                    _fans[i] = f;
                }
            }
            for (int i = 0; i < _spinners.Count; i++)
                if (_spinners[i].T != null) _spinners[i].T.Rotate(_spinners[i].Speed * dt, Space.World);
            for (int i = 0; i < _bobbers.Count; i++)
            {
                Bobber b = _bobbers[i];
                if (b.T != null) b.T.localPosition = b.Base + new Vector3(0f, Mathf.Sin(t * b.Freq * 6.283f) * b.Amp, 0f);
            }
            for (int i = 0; i < _sways.Count; i++)
            {
                Sway s = _sways[i];
                if (s.T != null) s.T.localRotation = s.Base * Quaternion.Euler(Mathf.Sin(t * s.Freq * 6.283f + s.Phase) * s.Deg, 0f, Mathf.Cos(t * s.Freq * 4.1f + s.Phase) * s.Deg * 0.6f);
            }
            for (int i = 0; i < _flickers.Count; i++)
            {
                Transform f = _flickers[i];
                if (f == null) continue;
                float k = 1f + Mathf.Sin(t * 17f + i) * 0.08f + Mathf.Sin(t * 29f + i * 3f) * 0.06f;
                f.localScale = new Vector3(1f / k, k, 1f / k);
            }
        }

        void AnimateFan(ref Fan f, float t, float dt, AnimationConfig c)
        {
            float lt = _reactionTime - f.Delay;
            float jump = 0f, raise = 0f, forward = 0f, close = 0f;
            switch (_reaction)
            {
                case CrowdReaction.Clap:
                    CrowdMotion.Clap(lt, c.clapSeconds, c.clapHz, out forward, out close);
                    break;
                case CrowdReaction.Cheer:
                    CrowdMotion.Cheer(lt, c.cheerSeconds, f.Phase, out jump, out raise);
                    break;
                case CrowdReaction.Wave:
                    float w = CrowdMotion.Wave(f.Angle, _reactionTime, c.waveSeconds, c.waveLaps, c.waveWidthDeg);
                    jump = w * 0.25f;
                    raise = w;
                    break;
            }
            float bob = Mathf.Sin(t * 2.2f + f.Phase) * 0.015f;
            f.Root.localPosition = f.Base + new Vector3(0f, bob + jump, 0f);

            // Arms: rest 15°, raised up to 160°, or forward and closing while clapping.
            float side = Mathf.Lerp(15f, 160f * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(f.Phase))), raise);
            side = Mathf.Lerp(side, 8f, forward / 70f);
            float inward = close * 25f;
            if (f.ArmL != null) f.ArmL.localEulerAngles = new Vector3(-forward, inward, -side);
            if (f.ArmR != null) f.ArmR.localEulerAngles = new Vector3(-forward, -inward, side);

            if (f.Head != null)
            {
                float yaw, pitch;
                if (_hasFocus)
                {
                    Vector3 local = f.Root.InverseTransformDirection(_focus - f.Head.position);
                    CrowdMotion.LookAngles(local, c.crowdHeadMaxYaw, c.crowdHeadMaxPitch, out yaw, out pitch);
                }
                else
                {
                    yaw = Mathf.Sin(t * 0.35f + f.Phase) * 15f;
                    pitch = 6f;
                }
                if (raise > 0.5f) pitch = Mathf.Lerp(pitch, -15f, (raise - 0.5f) * 2f); // look up when cheering
                float k = 1f - Mathf.Exp(-f.HeadSpeed * dt);
                f.Yaw = Mathf.Lerp(f.Yaw, yaw, k);
                f.Pitch = Mathf.Lerp(f.Pitch, pitch, k);
                f.Head.localRotation = Quaternion.Euler(f.Pitch, f.Yaw, 0f);
            }
        }
    }
}
