using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Brooklyneir.EditorTools
{
    /// <summary>
    /// Build menu and command-line entry points:
    ///  - Debug APK (installable, development build)
    ///  - Release APK (signed with YOUR keystore, configured in Player Settings &gt; Publishing)
    ///  - Export of a Gradle project that opens in Android Studio
    /// Command line:  Unity -batchmode -quit -projectPath . -executeMethod Brooklyneir.EditorTools.BuildTools.CommandLineDebugApk
    /// </summary>
    public static class BuildTools
    {
        public const string DebugApk = "Builds/Android/TableTennisLegendsBrooklyneir-debug.apk";
        public const string ReleaseApk = "Builds/Android/TableTennisLegendsBrooklyneir-release.apk";
        public const string StudioProject = "Builds/AndroidStudioProject";

        [MenuItem("Brooklyneir/Compilar APK de depuración", false, 20)]
        public static void BuildDebugApk()
        {
            Build(DebugApk, BuildOptions.Development, false, true);
        }

        [MenuItem("Brooklyneir/Compilar APK de lanzamiento (firmada)", false, 21)]
        public static void BuildReleaseApk()
        {
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                EditorUtility.DisplayDialog("Falta la clave de firma",
                    "Configura tu keystore en Project Settings > Player > Android > Publishing Settings (Custom Keystore) " +
                    "y vuelve a intentarlo. Consulta BUILD_ANDROID.md. Las contraseñas nunca se guardan en el proyecto.", "OK");
                return;
            }
            Build(ReleaseApk, BuildOptions.None, false, true);
        }

        [MenuItem("Brooklyneir/Exportar proyecto para Android Studio", false, 22)]
        public static void ExportAndroidStudio()
        {
            Build(StudioProject, BuildOptions.None, true, true);
        }

        public static void CommandLineDebugApk()
        {
            bool ok = Build(DebugApk, BuildOptions.Development, false, false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void CommandLineExport()
        {
            bool ok = Build(StudioProject, BuildOptions.None, true, false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string path, BuildOptions options, bool export, bool interactive)
        {
            if (!ProjectSetup.LooksComplete()) ProjectSetup.Run(false);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Report(interactive, "No se pudo cambiar a Android. ¿Está instalado 'Android Build Support' (con OpenJDK y Android SDK & NDK Tools) en Unity Hub?");
                return false;
            }
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
            EditorUserBuildSettings.buildAppBundle = false;
            try
            {
                string dir = export ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                BuildPlayerOptions o = new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options
                };
                BuildReport report = BuildPipeline.BuildPlayer(o);
                BuildSummary s = report.summary;
                if (s.result == BuildResult.Succeeded)
                {
                    string msg = (export ? "Proyecto Android exportado en " : "APK generada en ") + Path.GetFullPath(path) +
                                 "\nTamaño: " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB · Tiempo: " + s.totalTime;
                    Debug.Log("[TTLB] " + msg);
                    if (interactive)
                    {
                        EditorUtility.DisplayDialog("Compilación correcta", msg, "OK");
                        EditorUtility.RevealInFinder(path);
                    }
                    return true;
                }
                Report(interactive, "La compilación terminó con estado " + s.result + " y " + s.totalErrors + " errores. Revisa la consola y BUILD_ANDROID.md (sección de errores habituales).");
                return false;
            }
            catch (Exception e)
            {
                Report(interactive, "Error de compilación: " + e.Message);
                return false;
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
            }
        }

        static void Report(bool interactive, string message)
        {
            Debug.LogError("[TTLB] " + message);
            if (interactive) EditorUtility.DisplayDialog("Table Tennis Legends Brooklyneir", message, "OK");
        }
    }
}
