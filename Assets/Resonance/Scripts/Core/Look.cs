using UnityEngine;
#if RESONANCE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Resonance
{
    public enum LookMode
    {
        /// <summary>Dark brutalist concrete, crushed blacks, one hot accent, grain and lens fringing. For the editor,
        /// recordings, capture and passthrough devices.</summary>
        Cinematic,
        /// <summary>Tuned for optical see-through glasses: same art direction, but surfaces lifted so they don't vanish
        /// (black is transparent on the Air 2 Ultra) and only cheap post effects.</summary>
        Glasses
    }

    /// <summary>Art direction switch: palette + URP post-processing volume.</summary>
    public static class Look
    {
        public static LookMode Mode { get; private set; } = LookMode.Cinematic;

        public static void Apply(LookMode mode, Camera cam)
        {
            Mode = mode;
            bool cine = mode == LookMode.Cinematic;
            // Concrete greys: darker and colder in cinematic; lifted on glasses so geometry stays visible.
            Palette.Stone = cine ? new Color(0.16f, 0.165f, 0.175f) : new Color(0.30f, 0.32f, 0.36f);
            Palette.StoneLight = cine ? new Color(0.26f, 0.265f, 0.28f) : new Color(0.48f, 0.50f, 0.55f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = cine ? new Color(0.06f, 0.065f, 0.075f) : new Color(0.2f, 0.21f, 0.24f);
            if (cine)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = new Color(0.05f, 0.052f, 0.058f);
                RenderSettings.fogDensity = 0.045f;
            }
            else RenderSettings.fog = false;
            BuildVolume(cine, cam);
        }

        static void BuildVolume(bool cine, Camera cam)
        {
#if RESONANCE_URP
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var go = new GameObject("ResonanceLook");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(cine ? 0.9f : 1.0f);
            bloom.intensity.Override(cine ? 1.4f : 0.9f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.88f));

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var col = p.Add<ColorAdjustments>(true);
            col.saturation.Override(cine ? -38f : -15f);   // concrete goes grey; saturated glows survive bloom
            col.contrast.Override(cine ? 28f : 10f);
            col.postExposure.Override(cine ? -0.35f : 0f);

            if (cine)
            {
                var lgg = p.Add<LiftGammaGain>(true);
                lgg.lift.Override(new Vector4(0.98f, 1.0f, 1.04f, -0.06f));   // cold, crushed shadows
                lgg.gain.Override(new Vector4(1.03f, 1.0f, 0.97f, 0f));
                var ca = p.Add<ChromaticAberration>(true); ca.intensity.Override(0.28f);
                var grain = p.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Medium3); grain.intensity.Override(0.32f);
                var vig = p.Add<Vignette>(true); vig.intensity.Override(0.38f); vig.smoothness.Override(0.45f);
            }
#endif
        }
    }
}
