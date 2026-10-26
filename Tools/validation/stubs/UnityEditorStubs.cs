// Compile-only declarations for the UnityEditor API used by the editor scripts. NOT shipped.
#pragma warning disable 0067, 0649
using System;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public static class EditorApplication
    {
        public delegate void CallbackFunction();
        public static CallbackFunction delayCall;
        public static bool isPlayingOrWillChangePlaymode { get { return false; } }
        public static void Exit(int returnValue) { }
    }

    public static class EditorPrefs
    {
        public static int GetInt(string key, int defaultValue) { return 0; }
        public static void SetInt(string key, int value) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
        public static void SetDirty(UnityEngine.Object target) { }
        public static void RevealInFinder(string path) { }
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) { return true; }
        public static string CreateFolder(string parentFolder, string newFolderName) { return ""; }
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object { return null; }
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
    }

    public class AssetImporter : UnityEngine.Object
    {
        public static AssetImporter GetAtPath(string path) { return null; }
        public void SaveAndReimport() { }
    }

    public enum TextureImporterCompression { Uncompressed = 0, Compressed = 1, CompressedHQ = 2, CompressedLQ = 3 }

    public class TextureImporter : AssetImporter
    {
        public bool alphaIsTransparency { get; set; }
        public TextureImporterCompression textureCompression { get; set; }
        public bool mipmapEnabled { get; set; }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public SerializedProperty FindProperty(string propertyPath) { return null; }
        public bool ApplyModifiedProperties() { return true; }
    }

    public class SerializedProperty { public int intValue { get; set; } }

    public enum BuildTarget { Android = 13, StandaloneWindows64 = 19 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, Android = 7 }
    [Flags] public enum BuildOptions { None = 0, Development = 1 }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTarget target { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildOptions options { get; set; }
    }

    public static class BuildPipeline
    {
        public static bool isBuildingPlayer { get { return false; } }
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) { return null; }
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget { get { return BuildTarget.Android; } }
        public static bool exportAsGoogleAndroidProject { get; set; }
        public static bool buildAppBundle { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target) { return true; }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }
    public enum ManagedStrippingLevel { Disabled = 0, Low = 1, Medium = 2, High = 3, Minimal = 4 }
    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0, AndroidApiLevel23 = 23, AndroidApiLevel24 = 24, AndroidApiLevel25 = 25, AndroidApiLevel26 = 26 }
    [Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 8 }
    public enum IconKind { Any = -1, Application = 0, Settings = 1, Notification = 2, Spotlight = 3, Store = 4 }
    public class PlatformIconKind { }
    public class PlatformIcon
    {
        public int maxLayerCount { get { return 1; } }
        public int minLayerCount { get { return 1; } }
        public int layerCount { get; set; }
        public int width { get { return 0; } }
        public int height { get { return 0; } }
        public PlatformIconKind kind { get { return null; } }
        public void SetTexture(Texture2D texture, int layer = 0) { }
        public void SetTextures(params Texture2D[] textures) { }
    }

    public sealed class PlayerSettings : UnityEngine.Object
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static bool runInBackground { get; set; }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget, string identifier) { }
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { }
        public static void SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget buildTarget, ManagedStrippingLevel level) { }
        public static void SetIcons(UnityEditor.Build.NamedBuildTarget buildTarget, Texture2D[] icons, IconKind kind) { }
        public static PlatformIconKind[] GetSupportedIconKinds(UnityEditor.Build.NamedBuildTarget buildTarget) { return null; }
        public static PlatformIcon[] GetPlatformIcons(UnityEditor.Build.NamedBuildTarget buildTarget, PlatformIconKind kind) { return null; }
        public static void SetPlatformIcons(UnityEditor.Build.NamedBuildTarget buildTarget, PlatformIconKind kind, PlatformIcon[] icons) { }

        public static class Android
        {
            public static int bundleVersionCode { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static bool forceInternetPermission { get; set; }
            public static bool forceSDCardPermission { get; set; }
            public static bool startInFullscreen { get; set; }
            public static bool renderOutsideSafeArea { get; set; }
            public static bool optimizedFramePacing { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static string keystoreName { get; set; }
        }
    }
}

namespace UnityEngine
{
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Unknown;
        public static readonly NamedBuildTarget Android;
        public static readonly NamedBuildTarget Standalone;
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }
    public struct BuildSummary
    {
        public BuildResult result { get { return BuildResult.Unknown; } }
        public ulong totalSize { get { return 0; } }
        public TimeSpan totalTime { get { return TimeSpan.Zero; } }
        public int totalErrors { get { return 0; } }
    }
    public class BuildReport { public BuildSummary summary { get { return new BuildSummary(); } } }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new UnityEngine.SceneManagement.Scene(); }
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string dstScenePath) { return true; }
    }
}
