using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.CameraSystem
{
    public enum CameraMode { Menu, Match, Showcase }

    /// <summary>
    /// Match camera: broadcast-style view from the side of the table, elevated, with the table
    /// length along the screen's long (horizontal) axis: player 1 on the left, player 2 on the
    /// right. The horizontal field of view is fixed and the distance is computed from it (and
    /// checked against the vertical one) so both racket zones always fit on any aspect ratio. Dynamic touches are deliberately small (gentle follow of the ball
    /// along the table, a short zoom punch and shake on power shots) and can be reduced in the
    /// settings; they never rotate the view, so the ball's path stays readable.
    /// In menus the camera orbits the table slowly.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public CameraMode Mode { get; private set; }
        public bool ReducedEffects { get; set; }

        const float Fov = 38f;            // menus / fallback
        const float HorizontalFov = 60f;  // match: across the screen's long axis
        const float Elevation = 52f;
        const float HalfCoverage = 3.1f;  // metres along the table kept visible each side (racket zones + perspective margin)
        const float HalfDepth = 1.25f;    // metres across the table (plus rackets/ball) that must fit vertically

        Vector3 _focus = new Vector3(0f, TableGeometry.TopY, 0f);
        Vector3 _followOffset;
        float _shake;
        float _punch;
        float _orbitAngle = 200f;
        float _blend = 1f;
        Vector3 _fromPos;
        Quaternion _fromRot;
        float _fromFov;
        Vector3 _ball;
        bool _hasBall;

        public void Init()
        {
            Cam = gameObject.GetComponent<Camera>();
            if (Cam == null) Cam = gameObject.AddComponent<Camera>();
            Cam.fieldOfView = Fov;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 140f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.05f, 0.06f, 0.1f);
            Cam.allowHDR = false;
            Cam.cullingMask = ~(1 << Brooklyneir.Core.Layers.Preview);
            gameObject.tag = "MainCamera";
            Mode = CameraMode.Menu;
        }

        public void SetMode(CameraMode mode, bool instant = false)
        {
            if (mode == Mode && !instant) return;
            _fromPos = transform.position;
            _fromRot = transform.rotation;
            _fromFov = Cam.fieldOfView;
            _blend = instant ? 1f : 0f;
            Mode = mode;
        }

        public void TrackBall(Vector3 position, bool active)
        {
            _ball = position;
            _hasBall = active;
        }

        /// <summary>Camera shake (0..1). Ignored with reduced effects except a tiny amount.</summary>
        public void Shake(float amount)
        {
            float scale = ReducedEffects ? 0.15f : 1f;
            _shake = Mathf.Max(_shake, Mathf.Clamp01(amount) * scale);
        }

        /// <summary>Short zoom-in for highlights (0..1).</summary>
        public void Punch(float amount)
        {
            if (ReducedEffects) return;
            _punch = Mathf.Max(_punch, Mathf.Clamp01(amount));
        }

        /// <summary>Vertical field of view that gives <see cref="HorizontalFov"/> at this aspect ratio.</summary>
        public static float VerticalFovFor(float aspect)
        {
            aspect = Mathf.Max(0.5f, aspect);
            float half = Mathf.Atan(Mathf.Tan(HorizontalFov * 0.5f * Mathf.Deg2Rad) / aspect);
            return half * 2f * Mathf.Rad2Deg;
        }

        /// <summary>Camera distance so the table length fits horizontally and its width vertically.</summary>
        public static float DistanceFor(float aspect)
        {
            float vfov = VerticalFovFor(aspect);
            float byLength = HalfCoverage / Mathf.Tan(HorizontalFov * 0.5f * Mathf.Deg2Rad);
            float byDepth = HalfDepth / Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(byLength, byDepth);
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 pos;
            Quaternion rot;
            float fov = Fov;

            if (Mode == CameraMode.Match)
            {
                float aspect = Cam.aspect;
                float matchFov = VerticalFovFor(aspect);
                float dist = DistanceFor(aspect);
                Vector3 desiredFollow = Vector3.zero;
                if (_hasBall && !ReducedEffects)
                    desiredFollow = new Vector3(0f, 0f, Mathf.Clamp(_ball.z, -TableGeometry.HalfLength, TableGeometry.HalfLength) * 0.06f);
                _followOffset = Vector3.Lerp(_followOffset, desiredFollow, 1f - Mathf.Exp(-3f * dt));
                Vector3 focus = _focus + _followOffset;
                float e = Elevation * Mathf.Deg2Rad;
                // Camera on the +x side looking towards -x: screen right = +z (player 2), left = -z (player 1).
                pos = focus + new Vector3(Mathf.Cos(e) * dist, Mathf.Sin(e) * dist, 0f);
                rot = Quaternion.LookRotation(focus - pos, Vector3.up);
                fov = matchFov * (1f - _punch * 0.08f);
            }
            else if (Mode == CameraMode.Showcase)
            {
                _orbitAngle += dt * 6f;
                float a = _orbitAngle * Mathf.Deg2Rad;
                pos = new Vector3(Mathf.Cos(a) * 3.2f, 1.9f, Mathf.Sin(a) * 3.2f);
                rot = Quaternion.LookRotation(new Vector3(0f, TableGeometry.TopY, 0f) - pos, Vector3.up);
                fov = 45f;
            }
            else
            {
                _orbitAngle += dt * 4f;
                float a = _orbitAngle * Mathf.Deg2Rad;
                pos = new Vector3(Mathf.Cos(a) * 5.2f, 2.7f + Mathf.Sin(Time.unscaledTime * 0.2f) * 0.2f, Mathf.Sin(a) * 5.2f);
                rot = Quaternion.LookRotation(new Vector3(0f, TableGeometry.TopY + 0.1f, 0f) - pos, Vector3.up);
                fov = 42f;
            }

            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / 0.9f);
                float k = Mathf.SmoothStep(0f, 1f, _blend);
                pos = Vector3.Lerp(_fromPos, pos, k);
                rot = Quaternion.Slerp(_fromRot, rot, k);
                fov = Mathf.Lerp(_fromFov, fov, k);
            }

            if (_shake > 0f)
            {
                float t = Time.unscaledTime * 40f;
                Vector3 offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * 0.09f * _shake;
                pos += rot * offset;
                _shake = Mathf.Max(0f, _shake - dt * 2.8f);
            }
            _punch = Mathf.Max(0f, _punch - dt * 3f);

            transform.SetPositionAndRotation(pos, rot);
            Cam.fieldOfView = fov;
        }
    }
}
