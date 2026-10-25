using Brooklyneir.Art;
using Brooklyneir.Core;
using Brooklyneir.Rackets;
using Brooklyneir.Tables;
using UnityEngine;

namespace Brooklyneir.UI
{
    /// <summary>
    /// Off-screen 3D stage that renders a rotating racket or a miniature table into a
    /// RenderTexture for the selection screens. Lives far below the arena on its own layer and
    /// renders only when a preview is visible.
    /// </summary>
    public class PreviewStage : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(0f, -200f, 0f);

        public RenderTexture Texture { get; private set; }
        Camera _cam;
        Transform _pivot;
        GameObject _current;
        float _spin;
        bool _isTable;

        public void Init()
        {
            transform.position = Origin;
            Texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            Texture.name = "PreviewRT";
            Texture.antiAliasing = 2;
            GameObject camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = Texture;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.cullingMask = 1 << Layers.Preview;
            _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 20f;
            _cam.enabled = false;

            GameObject lightGo = new GameObject("PreviewKey");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(35f, -40f, 0f);
            Light key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.cullingMask = 1 << Layers.Preview;
            key.shadows = LightShadows.None;

            GameObject rimGo = new GameObject("PreviewRim");
            rimGo.transform.SetParent(transform, false);
            rimGo.transform.localPosition = new Vector3(0.6f, 0.6f, 1.2f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 4f;
            rim.intensity = 2f;
            rim.color = new Color(0.6f, 0.8f, 1f);
            rim.cullingMask = 1 << Layers.Preview;

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);
        }

        public void ShowRacket(RacketDefinition def)
        {
            Clear();
            _isTable = false;
            _current = RacketModelBuilder.Build(def, _pivot, Layers.Preview);
            _current.transform.localScale = Vector3.one * 2.4f;
            _current.transform.localPosition = new Vector3(0f, -RacketModelBuilder.BladeCentreY * 2.4f * 0.55f, 0f);
            _cam.transform.localPosition = new Vector3(0f, 0.02f, -1.45f);
            _cam.transform.localRotation = Quaternion.identity;
            _cam.enabled = true;
        }

        public void ShowTable(TableDefinition def)
        {
            Clear();
            _isTable = true;
            GameObject root = new GameObject("TablePreview");
            root.transform.SetParent(_pivot, false);
            root.transform.localScale = Vector3.one * 0.72f;
            SetLayer(root);
            GameObject table = TableModelBuilder.Build(def, root.transform, Layers.Preview);
            table.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            GameObject floor = new GameObject("Floor");
            floor.layer = Layers.Preview;
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            floor.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Disc(2.1f, 48);
            floor.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Surface(SurfaceKind.Ground, ProceduralTextures.Hex(def.FloorColor));
            GameObject rim = new GameObject("FloorRim");
            rim.layer = Layers.Preview;
            rim.transform.SetParent(root.transform, false);
            rim.transform.localPosition = new Vector3(0f, -0.448f, 0f);
            rim.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ring(2.05f, 2.15f, 48);
            rim.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Emissive(ProceduralTextures.Hex(def.AccentColor), ProceduralTextures.Hex(def.AccentColor), 1.2f);
            _current = root;
            _cam.transform.localPosition = new Vector3(0f, 2.2f, -3.6f);
            _cam.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            _cam.enabled = true;
        }

        void SetLayer(GameObject g)
        {
            g.layer = Layers.Preview;
        }

        public void Clear()
        {
            if (_current != null) Destroy(_current);
            _current = null;
            if (_cam != null) _cam.enabled = false;
        }

        void Update()
        {
            if (_current == null) return;
            _spin += Time.unscaledDeltaTime * (_isTable ? 18f : 50f);
            _pivot.localRotation = _isTable
                ? Quaternion.Euler(0f, _spin, 0f)
                : Quaternion.Euler(Mathf.Sin(_spin * 0.02f) * 12f, _spin, 0f);
        }
    }
}
