// Compile-only declarations mirroring the UnityEngine API surface used by the project.
// Purpose: let Mono type-check the whole project outside Unity. Bodies are dummies.
// Signatures follow the Unity 6 (6000.0) scripting reference. NOT shipped.
#pragma warning disable 0067, 0649, 0169, 0414
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum QualityLevel { Fastest, Fast, Simple, Good, Beautiful, Fantastic }
}
namespace UnityEngine
{
    public static partial class MathfExt { }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static bool operator ==(Vector2Int a, Vector2Int b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2Int a, Vector2Int b) { return !(a == b); }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public struct Quaternion
    {
        public static bool operator ==(Quaternion a, Quaternion b) { return false; }
        public static bool operator !=(Quaternion a, Quaternion b) { return true; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }

        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 e) { return identity; }
        public static Quaternion LookRotation(Vector3 forward) { return identity; }
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) { return identity; }
        public static Quaternion FromToRotation(Vector3 from, Vector3 to) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Quaternion AngleAxis(float angle, Vector3 axis) { return identity; }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
        public static Vector3 operator *(Quaternion r, Vector3 v) { return v; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
        public static Color HSVToRGB(float h, float s, float v) { return white; }
        public static Color operator *(Color a, float b) { return a; }
        public static Color operator *(float b, Color a) { return a; }
        public static Color operator *(Color a, Color b) { return a; }
        public static Color operator +(Color a, Color b) { return a; }
        public static Color operator -(Color a, Color b) { return a; }
        public static implicit operator Color32(Color c) { return new Color32(); }
        public static implicit operator Vector4(Color c) { return new Vector4(); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) { return new Color(); }
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { xMin = x; yMin = y; xMax = x + width; yMax = y + height; }
        public float xMin, yMin, xMax, yMax;
        public float width { get { return xMax - xMin; } }
        public float height { get { return yMax - yMin; } }
        public static bool operator ==(Rect a, Rect b) { return false; }
        public static bool operator !=(Rect a, Rect b) { return true; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public struct Ray
    {
        public Ray(Vector3 origin, Vector3 direction) { }
        public Vector3 GetPoint(float distance) { return Vector3.zero; }
    }

    public struct Plane
    {
        public Plane(Vector3 inNormal, Vector3 inPoint) { }
        public bool Raycast(Ray ray, out float enter) { enter = 0f; return true; }
    }

    public struct Bounds { }

    public static partial class MathfExtra { }

    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T Instantiate<T>(T original) where T : Object { return original; }
        public static implicit operator bool(Object exists) { return exists != null; }
        public int GetInstanceID() { return 0; }
    }

    [Flags] public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
        public Transform transform { get { return null; } }
        public string tag { get; set; }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
        public T[] GetComponentsInChildren<T>() { return null; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
        public bool isActiveAndEnabled { get { return true; } }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) { return null; }
        public void StopAllCoroutines() { }
    }

    public class Coroutine { }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform { get { return null; } }
        public int layer { get; set; }
        public string tag { get; set; }
        public bool activeSelf { get { return true; } }
        public bool activeInHierarchy { get { return true; } }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component { return null; }
        public Component AddComponent(Type t) { return null; }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
    }

    public enum Space { World, Self }

    public class Transform : Component, IEnumerable
    {
        public void SetSiblingIndex(int index) { }
        public int GetSiblingIndex() { return 0; }
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 up { get; set; }
        public Vector3 right { get; set; }
        public Transform parent { get; set; }
        public int childCount { get { return 0; } }
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
        public void Rotate(Vector3 eulers, Space relativeTo) { }
        public void Rotate(Vector3 axis, float angle, Space relativeTo) { }
        public void LookAt(Vector3 worldPosition) { }
        public void LookAt(Vector3 worldPosition, Vector3 worldUp) { }
        public Transform GetChild(int index) { return null; }
        public Transform Find(string n) { return null; }
        public void SetAsFirstSibling() { }
        public void SetAsLastSibling() { }
        public IEnumerator GetEnumerator() { return null; }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Rect rect { get { return new Rect(); } }
    }

    public static class Time
    {
        public static float time { get { return 0f; } }
        public static float deltaTime { get { return 0f; } }
        public static float unscaledTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0f; } }
        public static float timeScale { get; set; }
    }

    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }

    public static class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }

    public static class Screen
    {
        public static int width { get { return 1920; } }
        public static int height { get { return 1080; } }
        public static Rect safeArea { get { return new Rect(); } }
        public static ScreenOrientation orientation { get; set; }
        public static bool autorotateToPortrait { get; set; }
        public static bool autorotateToPortraitUpsideDown { get; set; }
        public static bool autorotateToLandscapeLeft { get; set; }
        public static bool autorotateToLandscapeRight { get; set; }
        public static int sleepTimeout { get; set; }
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public int fingerId { get { return 0; } }
        public Vector2 position { get { return Vector2.zero; } }
        public TouchPhase phase { get { return TouchPhase.Began; } }
    }

    public enum KeyCode { None, Escape, Space, Return, UpArrow, DownArrow, LeftArrow, RightArrow, A, D, S, W }

    public static class Input
    {
        public static int touchCount { get { return 0; } }
        public static bool multiTouchEnabled { get; set; }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static Touch GetTouch(int index) { return new Touch(); }
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath { get { return ""; } }
        public static string dataPath { get { return ""; } }
        public static string version { get { return ""; } }
        public static bool isEditor { get { return false; } }
        public static void Quit() { }
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
        public static T[] FindObjectsOfTypeAll<T>() where T : Object { return null; }
    }

    public static class Random
    {
        public static float value { get { return 0f; } }
        public static float Range(float a, float b) { return a; }
        public static int Range(int a, int b) { return a; }
    }

    public static class SystemInfo
    {
        public static int systemMemorySize { get { return 0; } }
        public static int graphicsMemorySize { get { return 0; } }
    }

    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }

    public static class RenderSettings
    {
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static Color fogColor { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
        public static UnityEngine.Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static Light sun { get; set; }
    }

    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static int antiAliasing { get; set; }
        public static float shadowDistance { get; set; }
        public static string[] names { get { return null; } }
        public static UnityEngine.Rendering.RenderPipelineAsset renderPipeline { get; set; }
        public static int GetQualityLevel() { return 0; }
        public static void SetQualityLevel(int index, bool applyExpensiveChanges) { }
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj, bool prettyPrint) { return ""; }
        public static T FromJson<T>(string json) { return default(T); }
        public static void FromJsonOverwrite(string json, object objectToOverwrite) { }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string htmlString, out Color color) { color = new Color(); return true; }
        public static string ToHtmlStringRGB(Color c) { return ""; }
        public static string ToHtmlStringRGBA(Color c) { return ""; }
    }

    public static class StaticBatchingUtility
    {
        public static void Combine(GameObject staticBatchRoot) { }
    }

    public static class Handheld
    {
        public static void Vibrate() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    public class AnimationCurve
    {
        public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd) { return null; }
        public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd) { return null; }
    }

    public struct GradientColorKey { public GradientColorKey(Color col, float time) { } }
    public struct GradientAlphaKey { public GradientAlphaKey(float alpha, float time) { } }
    public class Gradient { public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys) { } }

    // ------------------------------------------------------------------ rendering objects

    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
        public static int PropertyToID(string name) { return 0; }
    }

    public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, EmissiveIsBlack = 4 }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public Material(Material source) { }
        public Shader shader { get; set; }
        public int renderQueue { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; }
        public bool HasProperty(string name) { return true; }
        public bool HasProperty(int id) { return true; }
        public void SetColor(string name, Color c) { }
        public void SetColor(int id, Color c) { }
        public void SetFloat(string name, float v) { }
        public void SetFloat(int id, float v) { }
        public void SetInt(string name, int v) { }
        public void SetTexture(string name, Texture t) { }
        public void SetTexture(int id, Texture t) { }
        public void SetTextureScale(int id, Vector2 s) { }
        public void SetTextureScale(string name, Vector2 s) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public void SetOverrideTag(string tag, string val) { }
    }

    public class Texture : Object
    {
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public int anisoLevel { get; set; }
        public int width { get { return 0; } }
        public int height { get { return 0; } }
    }

    public enum TextureFormat { RGBA32 = 4, ARGB32 = 5 }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum FilterMode { Point, Bilinear, Trilinear }

    public class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
        public void SetPixels32(Color32[] colors) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }

    public enum RenderTextureFormat { ARGB32 = 0, Depth = 1 }

    public class RenderTexture : Texture
    {
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format) { }
        public int antiAliasing { get; set; }
    }

    public enum SpriteMeshType { FullRect, Tight }

    public class Sprite : Object
    {
        public Texture2D texture { get { return null; } }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) { return null; }
    }

    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string[] fontNames, int size) { return null; }
        public static string[] GetOSInstalledFontNames() { return new string[0]; }
    }

    public struct CombineInstance { public Mesh mesh; }

    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public int[] triangles { get; set; }
        public UnityEngine.Rendering.IndexFormat indexFormat { get; set; }
        public void SetVertices(List<Vector3> v) { }
        public void SetNormals(List<Vector3> n) { }
        public void SetUVs(int channel, List<Vector2> uvs) { }
        public void SetTriangles(List<int> triangles, int submesh) { }
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public void RecalculateTangents() { }
        public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes, bool useMatrices) { }
    }

    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } public Mesh mesh { get; set; } }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public bool enabled { get; set; }
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public float sortingFudge { get; set; }
    }

    public class MeshRenderer : Renderer { }

    public class TrailRenderer : Renderer
    {
        public float time { get; set; }
        public float minVertexDistance { get; set; }
        public float widthMultiplier { get; set; }
        public AnimationCurve widthCurve { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public int numCapVertices { get; set; }
        public void Clear() { }
    }

    public enum LightType { Spot = 0, Directional = 1, Point = 2 }
    public enum LightShadows { None = 0, Hard = 1, Soft = 2 }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
        public float shadowBias { get; set; }
        public float shadowNormalBias { get; set; }
        public int cullingMask { get; set; }
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public class Camera : Behaviour
    {
        public float aspect { get; set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public bool allowHDR { get; set; }
        public int cullingMask { get; set; }
        public RenderTexture targetTexture { get; set; }
        public Ray ScreenPointToRay(Vector3 pos) { return new Ray(); }
    }

    public class AudioListener : Behaviour { }

    public class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) { return null; }
        public bool SetData(float[] data, int offsetSamples) { return true; }
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public int priority { get; set; }
        public void Play() { }
        public void Stop() { }
    }

    public class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string className, params object[] args) { }
        public void Call(string methodName, params object[] args) { }
        public T Call<T>(string methodName, params object[] args) { return default(T); }
        public T GetStatic<T>(string fieldName) { return default(T); }
        public T CallStatic<T>(string methodName, params object[] args) { return default(T); }
        public void Dispose() { }
    }

    public class AndroidJavaClass : AndroidJavaObject
    {
        public AndroidJavaClass(string className) : base(className) { }
    }

    // ------------------------------------------------------------------ particles

    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Circle = 10 }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }

    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float constant) { }
            public MinMaxCurve(float min, float max) { }
            public MinMaxCurve(float multiplier, AnimationCurve curve) { }
            public static implicit operator MinMaxCurve(float constant) { return new MinMaxCurve(constant); }
        }

        public struct MinMaxGradient
        {
            public MinMaxGradient(Color color) { }
            public MinMaxGradient(Color min, Color max) { }
            public MinMaxGradient(Gradient gradient) { }
            public static implicit operator MinMaxGradient(Color color) { return new MinMaxGradient(color); }
        }

        public struct MainModule
        {
            public float duration { get; set; }
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public int maxParticles { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
            public ParticleSystemScalingMode scalingMode { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxGradient startColor { get; set; }
            public MinMaxCurve gravityModifier { get; set; }
        }

        public struct EmissionModule
        {
            public MinMaxCurve rateOverTime { get; set; }
            public MinMaxCurve rateOverDistance { get; set; }
        }

        public struct ShapeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemShapeType shapeType { get; set; }
            public float radius { get; set; }
            public float angle { get; set; }
            public Vector3 rotation { get; set; }
        }

        public struct ColorOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxGradient color { get; set; }
        }

        public struct SizeOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve size { get; set; }
        }

        public struct EmitParams
        {
            public Vector3 position { get; set; }
            public Vector3 velocity { get; set; }
            public Color32 startColor { get; set; }
            public float startSize { get; set; }
            public float startLifetime { get; set; }
            public bool applyShapeToPosition { get; set; }
        }

        public MainModule main { get { return new MainModule(); } }
        public EmissionModule emission { get { return new EmissionModule(); } }
        public ShapeModule shape { get { return new ShapeModule(); } }
        public ColorOverLifetimeModule colorOverLifetime { get { return new ColorOverLifetimeModule(); } }
        public SizeOverLifetimeModule sizeOverLifetime { get { return new SizeOverLifetimeModule(); } }
        public bool isPlaying { get { return true; } }
        public void Play() { }
        public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior) { }
        public void Clear(bool withChildren) { }
        public void Emit(int count) { }
        public void Emit(EmitParams emitParams, int count) { }
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode { get; set; }
        public float velocityScale { get; set; }
        public float lengthScale { get; set; }
    }

    // ------------------------------------------------------------------ UI basics in UnityEngine

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public bool pixelPerfect { get; set; }
    }

    public sealed class CanvasGroup : Behaviour
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }

    public struct RectOffset
    {
        public RectOffset(int left, int right, int top, int bottom) { }
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum BlendMode { Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10 }
    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, Transparent = 3000, Overlay = 4000 }
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public enum IndexFormat { UInt16, UInt32 }

    public abstract class RenderPipelineAsset : ScriptableObject { }

    public static class GraphicsSettings
    {
        public static RenderPipelineAsset defaultRenderPipeline { get; set; }
        public static RenderPipelineAsset currentRenderPipeline { get { return null; } }
    }
}

namespace UnityEngine
{
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject { return null; }
    }

    public static partial class MathfPerlin { }
}

namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject { }
    public class UniversalRendererData : ScriptableRendererData { }

    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) { return null; }
        public float renderScale { get; set; }
        public int msaaSampleCount { get; set; }
        public float shadowDistance { get; set; }
        public bool supportsHDR { get; set; }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { }
}
namespace UnityEngine
{
    public class TextAsset : Object { public string text { get { return ""; } } }
}
