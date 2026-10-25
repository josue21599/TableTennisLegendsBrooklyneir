using Brooklyneir.Art;
using Brooklyneir.Physics;
using UnityEngine;

namespace Brooklyneir.Gameplay
{
    /// <summary>Ring on the table showing where the ball in flight is going to bounce.</summary>
    public class LandingMarker : MonoBehaviour
    {
        Material _mat;
        Color _color = Color.white;
        float _t;
        bool _shown;

        public void Init()
        {
            gameObject.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Disc(0.5f, 32);
            MeshRenderer r = gameObject.AddComponent<MeshRenderer>();
            _mat = MaterialLibrary.UnlitTransparentInstance(Color.white, ProceduralTextures.RingMarker());
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Hide();
        }

        public void Show(Vector3 point, Color color)
        {
            transform.position = new Vector3(point.x, TableGeometry.TopY + 0.002f, point.z);
            _color = color;
            _t = 0f;
            _shown = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _shown = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_shown) return;
            _t += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(_t * 14f) * 0.08f;
            float s = Mathf.Lerp(0.34f, 0.16f, Mathf.Clamp01(_t * 3f)) * pulse;
            transform.localScale = new Vector3(s, 1f, s);
            Color c = _color;
            c.a = Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(_t * 4f));
            MaterialLibrary.SetColorOn(_mat, c);
        }
    }

    /// <summary>Translucent hexagon wall behind a player while their shield is active.</summary>
    public class ShieldView : MonoBehaviour
    {
        Material _mat;
        Color _color;
        bool _active;
        float _flash;
        float _t;

        public void Init(int player, Color color)
        {
            _color = color;
            float z = TableGeometry.ForwardSign(player) * -(TableGeometry.HalfLength + 0.95f);
            transform.position = new Vector3(0f, TableGeometry.TopY - 0.1f, z);
            gameObject.AddComponent<MeshFilter>().sharedMesh = MeshFactory.DoubleSidedQuad(2.6f, 1.05f, 5f, 2f);
            MeshRenderer r = gameObject.AddComponent<MeshRenderer>();
            _mat = MaterialLibrary.UnlitTransparentInstance(new Color(color.r, color.g, color.b, 0f), ProceduralTextures.Hexagons());
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            gameObject.SetActive(false);
        }

        /// <summary>World z of the shield plane.</summary>
        public float PlaneZ { get { return transform.position.z; } }

        public void SetActive(bool on)
        {
            _active = on;
            if (on) gameObject.SetActive(true);
        }

        public void Flash()
        {
            _flash = 1f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            _t += Time.deltaTime;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 2.5f);
            float baseAlpha = _active ? 0.28f + Mathf.Sin(_t * 4f) * 0.06f : 0f;
            float a = Mathf.Clamp01(baseAlpha + _flash * 0.7f);
            MaterialLibrary.SetColorOn(_mat, new Color(_color.r, _color.g, _color.b, a));
            if (!_active && _flash <= 0f) gameObject.SetActive(false);
        }
    }
}
