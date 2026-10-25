using System.IO;
using Brooklyneir.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Brooklyneir.EditorTools
{
    /// <summary>
    /// One-click (and automatic on first open) project configuration:
    ///  - URP pipeline asset + renderer, assigned to Graphics and every Quality level
    ///  - template materials in Resources/Materials (guarantee shader variants in the build)
    ///  - Player settings for Android (id, name, landscape, IL2CPP ARM64, min API 26, no Internet)
    ///  - legacy input enabled (multi-touch API used by the game)
    ///  - the bootstrap scene and the build scene list
    ///  - the application icon
    /// Safe to run again at any time: Brooklyneir &gt; Configurar proyecto.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string RenderingDir = "Assets/Settings/Rendering";
        public const string MaterialsDir = "Assets/Resources/Materials";
        public const string IconPath = "Assets/Art/Icon/app_icon.png";
        public const string AppId = "com.brooklyneir.tabletennislegends";
        public const string ProductName = "Table Tennis Legends Brooklyneir";
        const int SetupVersion = 2;

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        static string PrefKey { get { return "TTLB_Setup_" + Application.dataPath.GetHashCode(); } }

        static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            if (EditorPrefs.GetInt(PrefKey, 0) >= SetupVersion && LooksComplete()) return;
            Run(false);
        }

        public static bool LooksComplete()
        {
            return File.Exists(ScenePath) && GraphicsSettings.defaultRenderPipeline != null &&
                   File.Exists(MaterialsDir + "/TTLB_Lit.mat") && File.Exists(MaterialsDir + "/TTLB_LitEmissive.mat");
        }

        [MenuItem("Brooklyneir/Configurar proyecto", false, 0)]
        public static void RunFromMenu()
        {
            Run(true);
        }

        public static void Run(bool verbose)
        {
            try
            {
                EnsureFolder("Assets/Settings");
                EnsureFolder(RenderingDir);
                EnsureFolder("Assets/Resources");
                EnsureFolder(MaterialsDir);
                EnsureFolder("Assets/Scenes");
                UniversalRenderPipelineAsset urp = SetupUrp();
                SetupMaterials(urp != null);
                SetupPlayer();
                SetupInputHandling();
                SetupScene();
                SetupIcon();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorPrefs.SetInt(PrefKey, SetupVersion);
                Debug.Log("[TTLB] Proyecto configurado: URP, materiales, ajustes Android, escena e icono.");
                if (verbose)
                    EditorUtility.DisplayDialog("Table Tennis Legends Brooklyneir",
                        "Proyecto configurado.\n\nPulsa Play para probar en el editor (ratón = jugador 1, flechas/WASD = jugadores).\n" +
                        "Para el móvil: Brooklyneir > Compilar APK de depuración.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TTLB] Error configurando el proyecto: " + e);
                if (verbose) EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ URP

        static UniversalRenderPipelineAsset SetupUrp()
        {
            string assetPath = RenderingDir + "/TTLB_URP.asset";
            string rendererPath = RenderingDir + "/TTLB_Renderer.asset";
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.renderScale = 0.85f;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 10f;
            asset.supportsHDR = false;
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            return asset;
        }

        // ------------------------------------------------------------------ materials

        static void SetupMaterials(bool urp)
        {
            string lit = urp ? "Universal Render Pipeline/Lit" : "Standard";
            string unlit = urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture";
            string particles = urp ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Alpha Blended";
            Write("TTLB_Lit", lit, m => { });
            Write("TTLB_LitEmissive", lit, m =>
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Write("TTLB_LitTransparent", lit, m => MaterialLibrary.MakeTransparentLit(m));
            Write("TTLB_Unlit", unlit, m => { });
            Write("TTLB_UnlitTransparent", unlit, m => MaterialLibrary.MakeTransparentUnlit(m, false));
            Write("TTLB_Additive", particles, m => MaterialLibrary.MakeTransparentUnlit(m, true));
            Write("TTLB_ParticleAlpha", particles, m => MaterialLibrary.MakeTransparentUnlit(m, false));
        }

        static void Write(string name, string shaderName, System.Action<Material> configure)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[TTLB] Shader not found: " + shaderName);
                return;
            }
            string path = MaterialsDir + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m = existing != null ? existing : new Material(shader);
            m.shader = shader;
            configure(m);
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------ player

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "Brooklyneir";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // Set through reflection: these switches have moved between Unity versions, and a
            // missing one must never stop the rest of the setup from compiling or running.
            TrySetAndroid("forceInternetPermission", false);   // no Internet permission: fully offline game
            TrySetAndroid("forceSDCardPermission", false);
            TrySetAndroid("startInFullscreen", true);
            TrySetAndroid("renderOutsideSafeArea", true);      // UI handles notches with Screen.safeArea
            TrySetAndroid("optimizedFramePacing", true);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
        }

        static void TrySetAndroid(string property, object value)
        {
            System.Reflection.PropertyInfo p = typeof(PlayerSettings.Android).GetProperty(property,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (p != null && p.CanWrite)
            {
                try { p.SetValue(null, value, null); }
                catch (System.Exception e) { Debug.LogWarning("[TTLB] PlayerSettings.Android." + property + ": " + e.Message); }
            }
        }

        /// <summary>
        /// The game reads touches through the Input System package (EnhancedTouch) and falls back to
        /// the classic Input Manager when the package is not active. If the project is still set to
        /// the classic manager only, switch to 'Both' so the package is used (Unity asks to restart).
        /// </summary>
        static void SetupInputHandling()
        {
            Object[] ps = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (ps == null || ps.Length == 0) return;
            SerializedObject so = new SerializedObject(ps[0]);
            SerializedProperty p = so.FindProperty("activeInputHandler");
            if (p == null) return;
            if (p.intValue == 0)
            {
                p.intValue = 1; // Input System only (Android does not support "Both")
                so.ApplyModifiedProperties();
                Debug.LogWarning("[TTLB] 'Active Input Handling' cambiado a 'Input System Package (New)'. Cierra y vuelve a abrir Unity.");
            }
        }

        // ------------------------------------------------------------------ scene

        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Enables linear fog in the scene so the fog shader variants are kept in the build.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 16f;
                RenderSettings.fogEndDistance = 55f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------------ icon

        const string IconRoundPath = "Assets/Art/Icon/app_icon_round.png";
        const string IconAdaptiveBgPath = "Assets/Art/Icon/app_icon_adaptive_bg.png";
        const string IconAdaptiveFgPath = "Assets/Art/Icon/app_icon_adaptive_fg.png";

        static Texture2D LoadIcon(string path)
        {
            if (!File.Exists(path)) return null;
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !imp.alphaIsTransparency))
            {
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Default icon plus every Android icon kind (legacy, round and adaptive). Android uses its
        /// own platform icons when they exist, and on Android 8+ launchers show the adaptive icon,
        /// so setting only the default one can leave Unity's logo on the phone.
        /// </summary>
        static void SetupIcon()
        {
            Texture2D icon = LoadIcon(IconPath);
            if (icon == null)
            {
                Debug.LogWarning("[TTLB] No se encontró el icono en " + IconPath);
                return;
            }
            Texture2D round = LoadIcon(IconRoundPath) ?? icon;
            Texture2D bg = LoadIcon(IconAdaptiveBgPath) ?? icon;
            Texture2D fg = LoadIcon(IconAdaptiveFgPath) ?? icon;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            try
            {
                NamedBuildTarget android = NamedBuildTarget.Android;
                PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKinds(android);
                int set = 0;
                foreach (PlatformIconKind kind in kinds)
                {
                    PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(android, kind);
                    if (icons == null || icons.Length == 0) continue;
                    string name = kind.ToString().ToLowerInvariant();
                    foreach (PlatformIcon pi in icons)
                    {
                        if (pi.maxLayerCount >= 2)
                        {
                            pi.SetTextures(bg, fg); // adaptive: background, foreground (the background already contains the artwork, so the result is the same in either layer order)
                        }
                        else if (name.Contains("round"))
                        {
                            pi.SetTexture(round);
                        }
                        else
                        {
                            pi.SetTexture(icon);
                        }
                    }
                    PlayerSettings.SetPlatformIcons(android, kind, icons);
                    set++;
                }
                Debug.Log("[TTLB] Iconos de Android asignados (" + set + " tipos).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TTLB] No se pudieron asignar los iconos de Android: " + e.Message);
            }
        }
    }
}
