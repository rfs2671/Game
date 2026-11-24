using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Resonance.EditorTools
{
    /// <summary>
    /// CI build entry points (game-ci/unity-builder buildMethod). Exits Unity with 0 on success, 1 on failure.
    /// Before building it loads the OpenXR settings (when the OpenXR package is present), and retries once if the first
    /// attempt fails: on a fresh checkout OpenXR creates its settings asset during import and refuses the first build
    /// with "OpenXR Settings found in project but not yet loaded. Please build again."
    /// </summary>
    public static class CIBuild
    {
        /// <summary>XREAL APK.</summary>
        public static void Build() => Run(openXR: false);

        public static void Run(bool openXR)
        {
            try
            {
                if (openXR) OpenXRVariant.Apply();
                else OpenXRVariant.OpenXRSettingsFor(BuildTargetGroup.Android);   // warm-up only; loader untouched
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string path = ArgValue("-customBuildPath") ?? (openXR ? "build/Android/Resonance-openxr.apk" : "build/Android/Resonance.apk");
                var version = ArgValue("-buildVersion");
                if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
                if (int.TryParse(ArgValue("-androidVersionCode"), out int code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));

                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                    locationPathName = path,
                    target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None,
                };
                var sum = BuildPipeline.BuildPlayer(options).summary;
                if (sum.result != BuildResult.Succeeded)
                {
                    Debug.Log("[Resonance/CI] First build attempt did not succeed; retrying once (OpenXR first-import settings load).");
                    if (openXR) OpenXRVariant.Apply();
                    sum = BuildPipeline.BuildPlayer(options).summary;
                }
                Debug.Log($"[Resonance/CI] Build {sum.result}: {path} ({sum.totalSize / 1048576} MB, {sum.totalErrors} errors)");
                EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[Resonance/CI] " + e);
                EditorApplication.Exit(1);
            }
        }

        static string ArgValue(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
