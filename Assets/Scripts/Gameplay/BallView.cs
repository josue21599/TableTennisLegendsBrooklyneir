using Brooklyneir.Art;
using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    public enum BallLook { Normal, Fire, Slow, Curve, SuperSmash, Special }

    /// <summary>
    /// Renders the simulated ball: a real sphere mesh at the exact simulated position, a soft
    /// halo for legibility on small screens, a trail coloured by state, a contact shadow on the
    /// table/floor (directly below the ball, scaled with height) and visual spin.
    /// </summary>
    public class BallView : MonoBehaviour
    {
        Transform _ball, _halo, _shadow;
        Material _ballMat, _haloMat, _shadowMat;
        TrailRenderer _trail;
        Vector3 _spinAxis = Vector3.right;
        float _spinSpeed;
        BallLook _look = BallLook.Normal;
        Camera _camera;
        float _radius;

        public Transform BallTransform { get { return _ball; } }

        public void Init(float radius, Camera cam)
        {
            _radius = radius;
            _camera = cam;
            GameObject ball = new GameObject("Ball");
            ball.transform.SetParent(transform, false);
            ball.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere(radius, 28, 18);
            MeshRenderer mr = ball.AddComponent<MeshRenderer>();
            _ballMat = MaterialLibrary.EmissiveInstance(new Color(1f, 0.97f, 0.92f), new Color(0.25f, 0.22f, 0.18f));
            mr.sharedMaterial = _ballMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _ball = ball.transform;

            // Seam ring so the spin is visible.
            GameObject seam = new GameObject("Seam");
            seam.transform.SetParent(_ball, false);
            seam.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Torus(radius * 1.005f, radius * 0.06f, 28, 6);
            seam.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Surface(SurfaceKind.Ball, new Color(1f, 0.6f, 0.15f));

            GameObject halo = new GameObject("Halo");
            halo.transform.SetParent(transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(1f, 1f);
            MeshRenderer hr = halo.AddComponent<MeshRenderer>();
            _haloMat = MaterialLibrary.UnlitTransparentInstance(new Color(1f, 1f, 0.9f, 0.35f), ProceduralTextures.SoftCircle(0.05f));
            hr.sharedMaterial = _haloMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _halo = halo.transform;
            _halo.localScale = Vector3.one * radius * 5f;

            GameObject shadow = new GameObject("ContactShadow");
            shadow.transform.SetParent(transform, false);
            shadow.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Disc(0.5f, 24);
            MeshRenderer sr = shadow.AddComponent<MeshRenderer>();
            _shadowMat = MaterialLibrary.UnlitTransparentInstance(new Color(0f, 0f, 0f, 0.5f), ProceduralTextures.SoftCircle(0.3f));
            sr.sharedMaterial = _shadowMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shadow = shadow.transform;

            _trail = ball.AddComponent<TrailRenderer>();
            _trail.time = 0.14f;
            _trail.minVertexDistance = 0.01f;
            _trail.widthMultiplier = radius * 1.8f;
            _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            _trail.sharedMaterial = MaterialLibrary.Additive(Color.white, ProceduralTextures.SoftCircle(0.4f));
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.numCapVertices = 2;
            SetLook(BallLook.Normal);
        }

        public void SetLook(BallLook look)
        {
            _look = look;
            Color trailA, trailB, emission, halo;
            switch (look)
            {
                case BallLook.Fire:
                    trailA = new Color(1f, 0.85f, 0.3f, 0.95f); trailB = new Color(1f, 0.2f, 0.05f, 0f);
                    emission = new Color(1f, 0.45f, 0.1f) * 1.6f; halo = new Color(1f, 0.55f, 0.2f, 0.55f);
                    _trail.time = 0.22f;
                    break;
                case BallLook.Slow:
                    trailA = new Color(0.5f, 0.85f, 1f, 0.9f); trailB = new Color(0.2f, 0.4f, 1f, 0f);
                    emission = new Color(0.3f, 0.6f, 1f) * 0.8f; halo = new Color(0.5f, 0.8f, 1f, 0.5f);
                    _trail.time = 0.3f;
                    break;
                case BallLook.Curve:
                    trailA = new Color(0.8f, 0.5f, 1f, 0.95f); trailB = new Color(0.5f, 0.2f, 1f, 0f);
                    emission = new Color(0.6f, 0.3f, 1f) * 0.9f; halo = new Color(0.75f, 0.5f, 1f, 0.5f);
                    _trail.time = 0.35f;
                    break;
                case BallLook.SuperSmash:
                    trailA = new Color(1f, 0.3f, 0.3f, 1f); trailB = new Color(1f, 0.9f, 0.3f, 0f);
                    emission = new Color(1f, 0.25f, 0.2f) * 1.4f; halo = new Color(1f, 0.4f, 0.3f, 0.6f);
                    _trail.time = 0.28f;
                    break;
                case BallLook.Special:
                    trailA = new Color(1f, 0.85f, 0.3f, 1f); trailB = new Color(1f, 0.95f, 0.8f, 0f);
                    emission = new Color(1f, 0.8f, 0.3f) * 1.4f; halo = new Color(1f, 0.85f, 0.4f, 0.6f);
                    _trail.time = 0.3f;
                    break;
                default:
                    trailA = new Color(1f, 1f, 1f, 0.7f); trailB = new Color(1f, 1f, 1f, 0f);
                    emission = new Color(0.25f, 0.22f, 0.18f); halo = new Color(1f, 1f, 0.9f, 0.3f);
                    _trail.time = 0.14f;
                    break;
            }
            _trail.startColor = trailA;
            _trail.endColor = trailB;
            MaterialLibrary.SetEmission(_ballMat, emission);
            MaterialLibrary.SetColorOn(_haloMat, halo);
        }

        public BallLook Look { get { return _look; } }

        public void SetSpin(Vector3 velocity, Vector3 spinAccel)
        {
            // Visual only: roll axis perpendicular to travel, faster with spin.
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            _spinAxis = flat.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            _spinSpeed = 900f + (Mathf.Abs(spinAccel.x) + Mathf.Abs(spinAccel.y)) * 350f;
        }

        public void ClearTrail()
        {
            if (_trail != null) _trail.Clear();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>Places every element from the simulated position. Called every frame.</summary>
        public void Sync(Vector3 position, float dt)
        {
            _ball.position = position;
            _ball.Rotate(_spinAxis, _spinSpeed * dt, Space.World);
            _halo.position = position;
            if (_camera != null) _halo.rotation = _camera.transform.rotation;

            bool overTable = TableGeometry.IsOverTable(position.x, position.z) && position.y >= TableGeometry.TopY - 0.01f;
            float groundY = overTable ? TableGeometry.TopY + 0.0015f : 0.004f;
            float h = Mathf.Max(0f, position.y - _radius - groundY);
            _shadow.position = new Vector3(position.x, groundY + 0.0007f, position.z);
            float s = _radius * 2.6f * (1f + h * 0.8f);
            _shadow.localScale = new Vector3(s, 1f, s);
            float alpha = Mathf.Lerp(0.55f, 0.15f, Mathf.Clamp01(h / 1.2f));
            MaterialLibrary.SetColorOn(_shadowMat, new Color(0f, 0f, 0f, alpha));
        }
    }
}
