using UnityEngine;

namespace Resonance
{
    /// <summary>Runtime facts shown in SYSTEM CHECK (set by platform-specific code such as the OpenXR blend feature).</summary>
    public static class Platform
    {
        /// <summary>OpenXR environment blend mode in use ("Additive", "AlphaBlend", "Opaque"), or "n/a" when not on OpenXR.</summary>
        public static string BlendMode = "n/a";

        public static string Summary =>
            $"XR: {(RuntimeRig.XRActive ? "on" : "off (flat)")}   ·   Graphics: {SystemInfo.graphicsDeviceType}   ·   Blend: {BlendMode}";
    }
}
