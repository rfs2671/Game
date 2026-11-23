using UnityEngine;

namespace Resonance
{
    /// <summary>Progress and settings, persisted with PlayerPrefs.</summary>
    public static class Save
    {
        const string K = "resonance.";

        public static int Unlocked { get => PlayerPrefs.GetInt(K + "unlocked", 0); set { PlayerPrefs.SetInt(K + "unlocked", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); } }
        public static int LastLevel { get => PlayerPrefs.GetInt(K + "last", 0); set { PlayerPrefs.SetInt(K + "last", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); } }
        public static bool HasProgress => PlayerPrefs.HasKey(K + "last");
        public static bool Completed { get => PlayerPrefs.GetInt(K + "done", 0) == 1; set { PlayerPrefs.SetInt(K + "done", value ? 1 : 0); PlayerPrefs.Save(); } }

        /// <summary>Set before a scene reload to jump straight into a level (Restart Level / Continue).</summary>
        public static int PendingLevel { get => PlayerPrefs.GetInt(K + "pending", -1); set { PlayerPrefs.SetInt(K + "pending", value); PlayerPrefs.Save(); } }

        public static float Master { get => PlayerPrefs.GetFloat(K + "vol", 0.9f); set { PlayerPrefs.SetFloat(K + "vol", Mathf.Clamp01(value)); Apply(); } }
        public static float Ambience { get => PlayerPrefs.GetFloat(K + "amb", 0.5f); set { PlayerPrefs.SetFloat(K + "amb", Mathf.Clamp01(value)); Apply(); } }
        public static bool Subtitles { get => PlayerPrefs.GetInt(K + "subs", 1) == 1; set { PlayerPrefs.SetInt(K + "subs", value ? 1 : 0); Apply(); } }
        /// <summary>0 = relaxed (easier to trigger), 1 = normal, 2 = strict.</summary>
        public static int Sensitivity { get => PlayerPrefs.GetInt(K + "sens", 1); set { PlayerPrefs.SetInt(K + "sens", Mathf.Clamp(value, 0, 2)); Apply(); } }
        /// <summary>-1 = automatic (Glasses on device, Cinematic in editor), 0 = Cinematic, 1 = Glasses.</summary>
        public static int LookChoice { get => PlayerPrefs.GetInt(K + "look", -1); set { PlayerPrefs.SetInt(K + "look", value); PlayerPrefs.Save(); } }

        static readonly float pinchOn = Tuning.PinchOn, pinchOff = Tuning.PinchOff, thrust = Tuning.ThrustSpeed,
            shieldSpeed = Tuning.ShieldMaxSpeed, throwSpeed = Tuning.ThrowSpeed;

        public static void Apply()
        {
            PlayerPrefs.Save();
            AudioListener.volume = Master;
            Sfx.AmbienceLevel = Ambience;
            float k = Sensitivity == 0 ? 1.25f : Sensitivity == 2 ? 0.85f : 1f;   // >1 = easier
            Tuning.PinchOn = pinchOn * k;
            Tuning.PinchOff = pinchOff * k;
            Tuning.ThrustSpeed = thrust / k;
            Tuning.ShieldMaxSpeed = shieldSpeed * k;
            Tuning.ThrowSpeed = throwSpeed / k;
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(K + "unlocked"); PlayerPrefs.DeleteKey(K + "last"); PlayerPrefs.DeleteKey(K + "done");
            PlayerPrefs.Save();
        }
    }
}
