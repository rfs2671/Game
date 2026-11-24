using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;   // OpenXRFeatureAttribute (editor-only, in the runtime assembly)
#endif

namespace Resonance.OpenXRSupport
{
    /// <summary>
    /// READ-ONLY: reports the environment blend mode the runtime selected (shown in SYSTEM CHECK and the log).
    /// It never sets a mode. Build 9 requested ADDITIVE from OnSessionCreate and left Unity submitting an invalid
    /// mode (0x7FFFFFFF) that the runtime rejected every frame; the runtime's own choice plus the transparent-black
    /// clear is what works.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "Resonance: Blend Mode Report",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        Company = "rfs2671",
        Desc = "Logs the environment blend mode in use. Does not change it.",
        Version = "1.0.1",
        FeatureId = FeatureId)]
#endif
    public class BlendModeReportFeature : OpenXRFeature
    {
        public const string FeatureId = "com.rfs2671.resonance.blendmodereport";

        protected override void OnSessionBegin(ulong xrSession) => Report(GetEnvironmentBlendMode());
        protected override void OnEnvironmentBlendModeChange(XrEnvironmentBlendMode mode) => Report(mode);

        static void Report(XrEnvironmentBlendMode mode)
        {
            string name = mode == XrEnvironmentBlendMode.Opaque || mode == XrEnvironmentBlendMode.Additive || mode == XrEnvironmentBlendMode.AlphaBlend
                ? mode.ToString() : $"invalid ({(int)mode})";
            if (Platform.BlendMode == name) return;
            Platform.BlendMode = name;
            Debug.Log("[Resonance/OpenXR] Environment blend mode (runtime's choice): " + name);
        }
    }
}
