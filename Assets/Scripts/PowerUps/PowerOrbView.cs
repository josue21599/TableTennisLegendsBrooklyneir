using Brooklyneir.Art;
using UnityEngine;

namespace Brooklyneir.PowerUps
{
    public static class PowerVisuals
    {
        public static Color ColorOf(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fire: return new Color(1f, 0.45f, 0.12f);
                case PowerUpType.Slow: return new Color(0.35f, 0.75f, 1f);
                case PowerUpType.Curve: return new Color(0.72f, 0.45f, 1f);
                case PowerUpType.SuperSmash: return new Color(1f, 0.25f, 0.3f);
                case PowerUpType.Shield: return new Color(0.3f, 1f, 0.6f);
                case PowerUpType.Freeze: return new Color(0.7f, 0.95f, 1f);
                case PowerUpType.Random: return new Color(1f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }

        public static IconId IconOf(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fire: return IconId.Fire;
                case PowerUpType.Slow: return IconId.Slow;
                case PowerUpType.Curve: return IconId.Curve;
                case PowerUpType.SuperSmash: return IconId.Smash;
                case PowerUpType.Shield: return IconId.Shield;
                case PowerUpType.Freeze: return IconId.Freeze;
                default: return IconId.Random;
            }
        }
    }

    /// <summary>A collectible floating over the table: glowing core, rotating ring, icon billboard.</summary>
    public class PowerOrbView : MonoBehaviour
    {
        public PowerUpType Type { get; private set; }
        public float Age { get; private set; }
        public bool Alive { get; private set; }
        public Vector3 Centre { get; private set; }

        Transform _core, _ring, _icon, _halo;
        Material _coreMat, _iconMat, _haloMat;
        Camera _cam;
        float _lifetime;

        public void Init(Camera cam)
        {
            _cam = cam;
            GameObject core = new GameObject("Core");
            core.transform.SetParent(transform, false);
            core.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere(0.05f, 20, 14);
            MeshRenderer cr = core.AddComponent<MeshRenderer>();
            _coreMat = MaterialLibrary.EmissiveInstance(Color.white, Color.white);
            cr.sharedMaterial = _coreMat;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _core = core.transform;

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Torus(0.085f, 0.008f, 32, 6);
            MeshRenderer rr = ring.AddComponent<MeshRenderer>();
            rr.sharedMaterial = _coreMat;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring = ring.transform;

            GameObject halo = new GameObject("Halo");
            halo.transform.SetParent(transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(0.32f, 0.32f);
            MeshRenderer hr = halo.AddComponent<MeshRenderer>();
            _haloMat = MaterialLibrary.UnlitTransparentInstance(Color.white, ProceduralTextures.SoftCircle(0.1f));
            hr.sharedMaterial = _haloMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _halo = halo.transform;

            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(transform, false);
            icon.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(0.12f, 0.12f);
            MeshRenderer ir = icon.AddComponent<MeshRenderer>();
            _iconMat = MaterialLibrary.UnlitTransparentInstance(Color.white, null);
            ir.sharedMaterial = _iconMat;
            ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _icon = icon.transform;
            gameObject.SetActive(false);
        }

        public void Spawn(PowerUpType type, Vector3 position, float lifetime)
        {
            Type = type;
            Centre = position;
            Age = 0f;
            Alive = true;
            _lifetime = lifetime;
            Color c = PowerVisuals.ColorOf(type);
            MaterialLibrary.SetBaseColor(_coreMat, c);
            MaterialLibrary.SetEmission(_coreMat, c * 2.2f);
            MaterialLibrary.SetColorOn(_haloMat, new Color(c.r, c.g, c.b, 0.45f));
            Sprite icon = IconFactory.Get(PowerVisuals.IconOf(type));
            if (_iconMat.HasProperty("_BaseMap")) _iconMat.SetTexture("_BaseMap", icon.texture);
            if (_iconMat.HasProperty("_MainTex")) _iconMat.SetTexture("_MainTex", icon.texture);
            transform.position = position;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        public bool Expired { get { return Alive && Age >= _lifetime; } }

        void Update()
        {
            if (!Alive) return;
            Age += Time.deltaTime;
            float appear = Mathf.Clamp01(Age * 4f);
            float vanish = Mathf.Clamp01((_lifetime - Age) * 2f);
            float s = Mathf.Min(appear, vanish);
            transform.localScale = Vector3.one * s;
            transform.position = Centre + new Vector3(0f, Mathf.Sin(Age * 3f) * 0.03f, 0f);
            _ring.localRotation = Quaternion.Euler(70f, Age * 120f, 0f);
            _core.localRotation = Quaternion.Euler(0f, Age * 90f, 0f);
            if (_cam != null)
            {
                _halo.rotation = _cam.transform.rotation;
                _icon.rotation = _cam.transform.rotation;
                _icon.position = transform.position - _cam.transform.forward * 0.09f + _cam.transform.up * 0.0f;
            }
        }
    }
}
