using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Resonance.OpenXRSupport
{
    /// <summary>
    /// Asks the OpenXR runtime for ADDITIVE environment blending (optical see-through glasses), then ALPHA_BLEND
    /// (video passthrough). OpenXR only applies a mode the runtime supports, otherwise it keeps the runtime's
    /// preference, so on opaque-only headsets (Quest) this changes nothing and Meta passthrough shows the room.
    /// Works with the camera's transparent-black clear: black adds nothing / alpha 0 shows the room.
    /// Enabled for the OpenXR APK by OpenXRVariant; inert in the XREAL APK (OpenXR loader not active).
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "Resonance: See-through Blend",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        Company = "rfs2671",
        Desc = "Requests ADDITIVE, then ALPHA_BLEND environment blend mode when the runtime offers it.",
        Version = "1.0.0",
        FeatureId = FeatureId)]
#endif
    public class SeeThroughBlendFeature : OpenXRFeature
    {
        public const string FeatureId = "com.rfs2671.resonance.seethroughblend";

        protected override void OnSessionCreate(ulong xrSession) => Request();
        protected override void OnSessionBegin(ulong xrSession) => Request();
        protected override void OnEnvironmentBlendModeChange(XrEnvironmentBlendMode mode) => Report(mode);

        static void Request()
        {
            SetEnvironmentBlendMode(XrEnvironmentBlendMode.Additive);
            if (GetEnvironmentBlendMode() != XrEnvironmentBlendMode.Additive)
                SetEnvironmentBlendMode(XrEnvironmentBlendMode.AlphaBlend);
            Report(GetEnvironmentBlendMode());
        }

        static void Report(XrEnvironmentBlendMode mode)
        {
            if (Platform.BlendMode == mode.ToString()) return;
            Platform.BlendMode = mode.ToString();
            Debug.Log("[Resonance/OpenXR] Environment blend mode: " + mode);
        }
    }
}
