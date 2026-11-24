using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
#if RESONANCE_XRMGMT
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
#endif

namespace Resonance.EditorTools
{
    /// <summary>
    /// Second build variant: Android XR loader = OpenXR (instead of XREAL), with hand tracking and the
    /// AR Foundation session / camera / planes / anchors / raycasts features switched on.
    ///
    /// CI builds it with  buildMethod: Resonance.EditorTools.OpenXRVariant.BuildCI  (see build.yml): the variant is
    /// applied in a fully loaded editor, then the player is built, so no build-callback ordering is involved.
    /// Locally: Resonance > Build Variant > OpenXR (this rewrites XR + player settings; revert with git).
    ///
    /// OpenXR APIs are reached by reflection so this file compiles whether or not the OpenXR packages are present.
    /// </summary>
    public static class OpenXRVariant
    {
        public const string AppId = "com.rfs2671.resonance.openxr";
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

        // OpenXR feature classes to enable (matched by class name):
        // XR Hands' hand-tracking feature, Meta Quest support (required by the Meta features), and the AR Foundation features.
        static readonly string[] Features =
        {
            "HandTracking",
            "MetaQuestFeature",
            "ARSessionFeature", "ARCameraFeature", "ARPlaneFeature", "ARAnchorFeature", "ARRaycastFeature",
            "SeeThroughBlendFeature",   // Resonance: ADDITIVE / ALPHA_BLEND environment blend when offered
        };

        /// <summary>game-ci buildMethod for the OpenXR APK.</summary>
        public static void BuildCI() => CIBuild.Run(openXR: true);

        [MenuItem("Resonance/Build Variant/OpenXR (Android)")]
        public static void Apply()
        {
#if RESONANCE_XRMGMT
            const BuildTargetGroup android = BuildTargetGroup.Android;

            // 1) XR loader: OpenXR only.
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(android);
            if (general == null || general.Manager == null)
                throw new BuildFailedException("[Resonance/OpenXR] No XR Plug-in Management settings for Android.");
            var manager = general.Manager;
            manager.TrySetLoaders(new List<XRLoader>());   // drops XREAL (and any missing-script loader)
            if (!XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, android))
                throw new BuildFailedException("[Resonance/OpenXR] Could not assign the OpenXR loader. Is com.unity.xr.openxr installed?");
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(general);

            // 2) OpenXR features.
            var settings = OpenXRSettingsFor(android);
            if (settings == null)
                throw new BuildFailedException("[Resonance/OpenXR] OpenXR settings for Android not found.");
            var getFeatures = settings.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == "GetFeatures" && !m.IsGenericMethod && m.GetParameters().Length == 0);   // not GetFeatures<T>()
            var all = (Array)getFeatures.Invoke(settings, null);
            var missing = new List<string>(Features);
            foreach (var f in all)
            {
                if (f == null) continue;
                var name = f.GetType().Name;
                if (!Features.Contains(name)) continue;
                f.GetType().GetProperty("enabled").SetValue(f, true);
                EditorUtility.SetDirty((UnityEngine.Object)f);
                missing.Remove(name);
            }
            EditorUtility.SetDirty((UnityEngine.Object)settings);
            if (missing.Count > 0)
            {
                var have = string.Join(", ", all.Cast<object>().Where(x => x != null).Select(x => x.GetType().FullName));
                throw new BuildFailedException($"[Resonance/OpenXR] Features not found: {string.Join(", ", missing)}. Available: {have}");
            }

            // 3) Vulkan first (what Unity's OpenXR samples use), GLES3 fallback for devices without Vulkan.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // 4) Apply OpenXR's own automatic project fixes (API level, graphics APIs, etc).
            AutoFix(android);

            // 5) Install side by side with the XREAL build.
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.productName = "Resonance OpenXR";

            AssetDatabase.SaveAssets();
            Debug.Log("[Resonance/OpenXR] Variant applied: loader=OpenXR, features=" + string.Join(", ", Features) +
                      $", appId={AppId}, graphics={string.Join("/", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}. Enabled now: " + string.Join(", ", all.Cast<object>().Where(IsEnabled).Select(x => x.GetType().Name)));
#else
            throw new BuildFailedException("[Resonance/OpenXR] XR Plug-in Management is not installed.");
#endif
        }

        static bool IsEnabled(object f) => f != null && (bool)f.GetType().GetProperty("enabled").GetValue(f);

        static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

        internal static object OpenXRSettingsFor(BuildTargetGroup group)
        {
            // Make sure every feature class has its settings sub-asset before reading them.
            FindType("UnityEditor.XR.OpenXR.Features.FeatureHelpers")
                ?.GetMethod("RefreshFeatures", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null)
                ?.Invoke(null, new object[] { group });
            var t = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
            return t?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null)
                    ?.Invoke(null, new object[] { group });
        }

        static void AutoFix(BuildTargetGroup group)
        {
            var validation = FindType("UnityEditor.XR.OpenXR.OpenXRProjectValidation");
            var ruleType = FindType("UnityEngine.XR.OpenXR.Features.OpenXRFeature+ValidationRule");
            var get = validation?.GetMethod("GetCurrentValidationIssues", BindingFlags.Public | BindingFlags.Static);
            if (get == null || ruleType == null) { Debug.LogWarning("[Resonance/OpenXR] OpenXR validation API not found; skipping auto-fix."); return; }

            for (int pass = 0; pass < 4; pass++)
            {
                var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ruleType));
                get.Invoke(null, new object[] { list, group });
                int fixedCount = 0;
                foreach (var r in list)
                {
                    var fix = Field<Action>(r, "fixIt");
                    if (!Field<bool>(r, "fixItAutomatic") || fix == null) continue;
                    try { fix(); fixedCount++; Debug.Log("[Resonance/OpenXR] Fixed: " + Field<string>(r, "message")); }
                    catch (Exception e) { Debug.LogWarning($"[Resonance/OpenXR] Fix failed ({Field<string>(r, "message")}): {e.Message}"); }
                }
                if (fixedCount > 0) continue;
                foreach (var r in list)   // what's left needs a human; listed in the build log
                    Debug.Log($"[Resonance/OpenXR] Validation {(Field<bool>(r, "error") ? "ERROR" : "warning")}: {Field<string>(r, "message")}");
                break;
            }
        }

        static T Field<T>(object o, string name)
        {
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.GetValue(o) is T v) return v;
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.GetValue(o) is T w) return w;
            return default;
        }
    }
}
