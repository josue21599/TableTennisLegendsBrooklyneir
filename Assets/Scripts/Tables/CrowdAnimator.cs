using System.Collections.Generic;
using UnityEngine;

namespace Brooklyneir.Tables
{
    /// <summary>
    /// Lightweight animation for scenery: crowd idle bobbing and cheering, spinning holograms,
    /// swaying palms, bobbing water and flickering torches. One component, no per-object scripts.
    /// </summary>
    public class CrowdAnimator : MonoBehaviour
    {
        struct Fan { public Transform Root, ArmL, ArmR; public Vector3 Base; public float Phase; }
        struct Spinner { public Transform T; public Vector3 Speed; }
        struct Bobber { public Transform T; public Vector3 Base; public float Amp, Freq; }
        struct Sway { public Transform T; public Quaternion Base; public float Deg, Freq, Phase; }

        readonly List<Fan> _fans = new List<Fan>();
        readonly List<Spinner> _spinners = new List<Spinner>();
        readonly List<Bobber> _bobbers = new List<Bobber>();
        readonly List<Sway> _sways = new List<Sway>();
        readonly List<Transform> _flickers = new List<Transform>();
        float _cheer;
        bool _crowdVisible = true;

        public int FanCount { get { return _fans.Count; } }

        public void AddFan(Transform root, Transform armL, Transform armR, float phase)
        {
            _fans.Add(new Fan { Root = root, ArmL = armL, ArmR = armR, Base = root.localPosition, Phase = phase * 10f });
        }

        public void AddSpinner(Transform t, Vector3 degreesPerSecond) { _spinners.Add(new Spinner { T = t, Speed = degreesPerSecond }); }
        public void AddBobber(Transform t, float amplitude, float frequency) { _bobbers.Add(new Bobber { T = t, Base = t.localPosition, Amp = amplitude, Freq = frequency }); }
        public void AddSway(Transform t, float degrees, float frequency) { _sways.Add(new Sway { T = t, Base = t.localRotation, Deg = degrees, Freq = frequency, Phase = Random.value * 6f }); }
        public void AddFlicker(Transform t) { _flickers.Add(t); }

        /// <summary>Makes the crowd jump and raise arms (0..1), decays over ~2 s.</summary>
        public void Cheer(float intensity)
        {
            _cheer = Mathf.Max(_cheer, Mathf.Clamp01(intensity));
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
            _cheer = Mathf.Max(0f, _cheer - dt * 0.5f);
            if (_crowdVisible)
            {
                for (int i = 0; i < _fans.Count; i++)
                {
                    Fan f = _fans[i];
                    if (f.Root == null) continue;
                    float bob = Mathf.Sin(t * 2.2f + f.Phase) * 0.015f;
                    float jump = _cheer > 0f ? Mathf.Abs(Mathf.Sin(t * 9f + f.Phase)) * 0.18f * _cheer : 0f;
                    f.Root.localPosition = f.Base + new Vector3(0f, bob + jump, 0f);
                    float arm = Mathf.Lerp(15f, 160f, _cheer * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(f.Phase))));
                    if (f.ArmL != null) f.ArmL.localEulerAngles = new Vector3(0f, 0f, -arm);
                    if (f.ArmR != null) f.ArmR.localEulerAngles = new Vector3(0f, 0f, arm);
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
    }
}
