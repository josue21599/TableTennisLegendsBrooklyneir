// Functional mini-engine: enough of UnityEngine to RUN the project's procedural model builders
// under Mono and export the resulting scene for an offline preview render. NOT shipped.
#pragma warning disable 0067, 0649, 0414
using System; using System.Collections; using System.Collections.Generic;
namespace UnityEngine {
  public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
  public struct Quaternion {
    public float x, y, z, w;
    public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
    public static Quaternion AngleAxis(float deg, Vector3 axis) { axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h); return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h)); }
    public static Quaternion Euler(float x, float y, float z) { return AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward); }
    public static Quaternion Euler(Vector3 e) { return Euler(e.x, e.y, e.z); }
    public static Quaternion operator *(Quaternion a, Quaternion b) {
      return new Quaternion(a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z); }
    public static Vector3 operator *(Quaternion q, Vector3 v) {
      Vector3 u = new Vector3(q.x, q.y, q.z); Vector3 t = 2f * Vector3.Cross(u, v); return v + q.w * t + Vector3.Cross(u, t); }
    public static Quaternion LookRotation(Vector3 f) { return LookRotation(f, Vector3.up); }
    public static Quaternion LookRotation(Vector3 f, Vector3 up) {
      f = f.normalized; Vector3 r = Vector3.Cross(up, f).normalized; Vector3 u = Vector3.Cross(f, r);
      float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z; float tr = m00 + m11 + m22; Quaternion q;
      if (tr > 0) { float s = Mathf.Sqrt(tr + 1f) * 2; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
      else if (m00 > m11 && m00 > m22) { float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
      else if (m11 > m22) { float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
      else { float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
      return q; }
    public static Quaternion FromToRotation(Vector3 a, Vector3 b) {
      a = a.normalized; b = b.normalized; Vector3 c = Vector3.Cross(a, b); float d = Vector3.Dot(a, b);
      if (d < -0.9999f) { Vector3 ax = Vector3.Cross(Vector3.right, a); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(Vector3.up, a); return AngleAxis(180f, ax); }
      Quaternion q = new Quaternion(c.x, c.y, c.z, 1f + d); float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w); return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }
    public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return b; }
  }
  public struct Color {
    public float r, g, b, a;
    public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
    public static Color white { get { return new Color(1, 1, 1, 1); } } public static Color black { get { return new Color(0, 0, 0, 1); } }
    public static Color red { get { return new Color(1, 0, 0, 1); } } public static Color magenta { get { return new Color(1, 0, 1, 1); } }
    public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    public static Color operator *(Color a, float f) { return new Color(a.r * f, a.g * f, a.b * f, a.a * f); }
    public static Color operator *(float f, Color a) { return a * f; }
    public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
    public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
    public static Color HSVToRGB(float h, float s, float v) { return white; }
    public static implicit operator Color32(Color c) { return new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(c.a) * 255)); }
  }
  public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
  public struct Rect { public Rect(float x, float y, float w, float h) { } }
  public static class ColorUtility {
    public static bool TryParseHtmlString(string s, out Color c) { c = Color.magenta; if (s == null || s.Length != 7) return false;
      c = new Color(Convert.ToInt32(s.Substring(1, 2), 16) / 255f, Convert.ToInt32(s.Substring(3, 2), 16) / 255f, Convert.ToInt32(s.Substring(5, 2), 16) / 255f, 1f); return true; }
    public static string ToHtmlStringRGB(Color c) { Color32 k = c; return k.r.ToString("X2") + k.g.ToString("X2") + k.b.ToString("X2"); }
    public static string ToHtmlStringRGBA(Color c) { Color32 k = c; return ToHtmlStringRGB(c) + k.a.ToString("X2"); }
  }
  public class Object { public string name { get; set; }
    public static void Destroy(Object o) { } public static void DontDestroyOnLoad(Object o) { }
    public static T Instantiate<T>(T o) where T : Object { var m = o as Mesh; if (m != null) { var c = new Mesh(); c.vertices = (Vector3[])m.vertices.Clone(); c.normals = (Vector3[])m.normals.Clone(); c.uv = (Vector2[])m.uv.Clone(); c.triangles = (int[])m.triangles.Clone(); return c as T; } return o; }
    public static implicit operator bool(Object o) { return !ReferenceEquals(o, null); } }
  public class Component : Object { internal GameObject go; public GameObject gameObject { get { return go; } } public Transform transform { get { return go.transform; } } public T GetComponent<T>() { return go.GetComponent<T>(); } }
  public class Behaviour : Component { public bool enabled { get; set; } }
  public class MonoBehaviour : Behaviour { }
  public sealed class GameObject : Object {
    public static List<GameObject> All = new List<GameObject>();
    readonly List<Component> comps = new List<Component>(); Transform t; bool active = true;
    public GameObject() : this("GameObject") { }
    public GameObject(string name) { this.name = name; t = new Transform(); t.go = this; comps.Add(t); All.Add(this); }
    public Transform transform { get { return t; } } public int layer { get; set; } public string tag { get; set; }
    public bool activeSelf { get { return active; } } public void SetActive(bool v) { active = v; }
    public bool ActiveInHierarchy { get { return active && (t.parent == null || t.parent.gameObject.ActiveInHierarchy); } }
    public T AddComponent<T>() where T : Component { T c = (T)Activator.CreateInstance(typeof(T)); c.go = this; comps.Add(c); return c; }
    public T GetComponent<T>() { foreach (var c in comps) if (c is T) return (T)(object)c; return default(T); }
  }
  public enum Space { World, Self }
  public class Transform : Component, IEnumerable {
    Transform p; readonly List<Transform> kids = new List<Transform>();
    public Vector3 localPosition = Vector3.zero; public Quaternion localRotation = Quaternion.identity; public Vector3 localScale = Vector3.one;
    public Transform parent { get { return p; } set { SetParent(value, true); } }
    public void SetParent(Transform np) { SetParent(np, true); }
    public void SetParent(Transform np, bool keep) { if (p != null) p.kids.Remove(this); p = np; if (np != null) np.kids.Add(this); }
    public int childCount { get { return kids.Count; } } public Transform GetChild(int i) { return kids[i]; }
    public Transform Find(string n) { foreach (var k in kids) if (k.name == n || k.go.name == n) return k; return null; }
    public new string name { get { return go.name; } }
    public Vector3 TransformPoint(Vector3 v) { Vector3 r = localRotation * Vector3.Scale(v, localScale) + localPosition; return p != null ? p.TransformPoint(r) : r; }
    public Quaternion rotation { get { return p != null ? p.rotation * localRotation : localRotation; } set { localRotation = p != null ? Inverse(p.rotation) * value : value; } }
    public Vector3 position { get { return p != null ? p.TransformPoint(localPosition) : localPosition; } set { localPosition = p != null ? p.InverseTransformPoint(value) : value; } }
    public Vector3 InverseTransformPoint(Vector3 w) { Vector3 l = p != null ? p.InverseTransformPoint(w) : w; Vector3 d = Inverse(localRotation) * (l - localPosition); return new Vector3(d.x / localScale.x, d.y / localScale.y, d.z / localScale.z); }
    static Quaternion Inverse(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }
    public Vector3 localEulerAngles { get { return Vector3.zero; } set { localRotation = Quaternion.Euler(value); } }
    public void LookAt(Vector3 target) { Vector3 d = target - position; if (d.sqrMagnitude > 1e-8f) rotation = Quaternion.LookRotation(d, Vector3.up); }
    public void Rotate(Vector3 axis, float angle, Space s) { } public void Rotate(Vector3 e, Space s) { }
    public IEnumerator GetEnumerator() { return kids.GetEnumerator(); }
  }
  public class MeshFilter : Component { public Mesh sharedMesh; }
  public class Renderer : Component { public Material sharedMaterial; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; } public bool receiveShadows { get; set; } }
  public class MeshRenderer : Renderer { }
  public enum LightType { Spot, Directional, Point } public enum LightShadows { None, Hard, Soft }
  public class Light : Behaviour { public LightType type { get; set; } public Color color { get; set; } public float intensity { get; set; } public float range { get; set; } public LightShadows shadows { get; set; } public float shadowStrength { get; set; } public float shadowBias { get; set; } public float shadowNormalBias { get; set; } public int cullingMask { get; set; } }
  public enum FogMode { Linear = 1 }
  public static class RenderSettings { public static bool fog; public static FogMode fogMode; public static Color fogColor; public static float fogStartDistance, fogEndDistance;
    public static UnityEngine.Rendering.AmbientMode ambientMode; public static Color ambientSkyColor, ambientEquatorColor, ambientGroundColor; public static Light sun; }
  public static class StaticBatchingUtility { public static void Combine(GameObject g) { } }
  public static class Time { public static float time = 0, deltaTime = 0.016f; }
  public static class Random { public static float value { get { return 0.3f; } } }
  public static class Resources { public static T Load<T>(string p) where T : Object { return null; } }
  public static class Debug { public static void LogError(object o) { Console.WriteLine("ERR " + o); } public static void LogWarning(object o) { } public static void Log(object o) { } }
  public class Shader : Object { public static Shader Find(string n) { return new Shader { name = n }; } public static int PropertyToID(string n) { return n.GetHashCode(); } }
  public enum MaterialGlobalIlluminationFlags { None }
  public class Texture : Object { public TextureWrapMode wrapMode { get; set; } public FilterMode filterMode { get; set; } public int anisoLevel { get; set; } }
  public enum TextureFormat { RGBA32 = 4 } public enum TextureWrapMode { Repeat, Clamp } public enum FilterMode { Point, Bilinear, Trilinear }
  public class Texture2D : Texture { public int W, H; public Color32[] Px; public Texture2D(int w, int h, TextureFormat f, bool m) { W = w; H = h; } public void SetPixels32(Color32[] c) { Px = c; } public void Apply(bool a, bool b) { } }
  public class Material : Object {
    public Dictionary<int, Color> Colors = new Dictionary<int, Color>(); public Texture Tex; public bool Emissive;
    public Material(Shader s) { } public Material(Material m) { Colors = new Dictionary<int, Color>(m.Colors); Tex = m.Tex; }
    public int renderQueue { get; set; } public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; } public Vector2 mainTextureScale { get; set; }
    public bool HasProperty(int id) { return true; } public bool HasProperty(string n) { return true; }
    public void SetColor(int id, Color c) { Colors[id] = c; } public void SetColor(string n, Color c) { Colors[n.GetHashCode()] = c; }
    public void SetFloat(string n, float v) { } public void SetFloat(int id, float v) { } public void SetInt(string n, int v) { }
    public void SetTexture(int id, Texture t) { Tex = t; } public void SetTexture(string n, Texture t) { Tex = t; } public void SetTextureScale(int id, Vector2 s) { }
    public void EnableKeyword(string k) { if (k == "_EMISSION") Emissive = true; } public void SetOverrideTag(string a, string b) { } }
  public class Mesh : Object {
    public Vector3[] vertices { get; set; } public Vector3[] normals { get; set; } public Vector2[] uv { get; set; } public int[] triangles { get; set; }
    public UnityEngine.Rendering.IndexFormat indexFormat { get; set; }
    public void SetVertices(List<Vector3> v) { vertices = v.ToArray(); } public void SetNormals(List<Vector3> n) { normals = n.ToArray(); }
    public void SetUVs(int c, List<Vector2> u) { uv = u.ToArray(); } public void SetTriangles(List<int> t, int s) { triangles = t.ToArray(); }
    public void RecalculateBounds() { } public void RecalculateTangents() { } public void CombineMeshes(CombineInstance[] c, bool a, bool b) { } }
  public struct CombineInstance { public Mesh mesh; }
}
namespace UnityEngine.Rendering {
  public enum ShadowCastingMode { Off, On } public enum IndexFormat { UInt16, UInt32 } public enum AmbientMode { Skybox, Trilight, Flat }
  public enum BlendMode { One = 1, SrcAlpha = 5, OneMinusSrcAlpha = 10 } public enum RenderQueue { Transparent = 3000 }
  public class RenderPipelineAsset { }
  public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline { get { return new RenderPipelineAsset(); } } }
}
