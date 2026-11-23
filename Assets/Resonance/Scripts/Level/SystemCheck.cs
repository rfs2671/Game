using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Automated run-through: verifies every sound clip loads, then plays every step of every level
    /// (spending a moment in each so its setup code, spawns, effects and audio run), counting any errors or exceptions.
    /// Launch it from the title menu (SYSTEM CHECK) on the glasses, or via the PlayMode test in the editor.
    /// </summary>
    public class SystemCheck : MonoBehaviour
    {
        public static SystemCheck I { get; private set; }
        public float dwell = 1.6f;
        public int Errors { get; private set; }
        public int StepsVisited { get; private set; }
        public readonly List<string> MissingAudio = new List<string>();
        public readonly List<string> ErrorLog = new List<string>();
        string lastStep = "";
        float stepTime;

        void OnEnable() { I = this; Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Errors++;
            if (ErrorLog.Count < 12) ErrorLog.Add($"{lastStep}: {msg}");
        }

        void Start()
        {
            foreach (var kv in Sfx.Map)
                foreach (var n in kv.Value)
                    if (Sfx.Clip(n) == null) MissingAudio.Add(n);
            foreach (var n in new[] { "tk_loop", "amb_quarry", "amb_tunnel", "amb_station", "amb_chamber" })
                if (Sfx.Clip(n) == null) MissingAudio.Add(n);
            if (MissingAudio.Count > 0) Debug.LogError("[SystemCheck] Missing audio: " + string.Join(", ", MissingAudio));
            StartCoroutine(Drive());
        }

        IEnumerator Drive()
        {
            while (GameFlow.I == null || !GameFlow.I.Finished)
            {
                var lv = LevelBase.Current;
                if (lv != null)
                {
                    if (lv.StepName != lastStep) { lastStep = lv.StepName; stepTime = 0; StepsVisited++; Debug.Log("[SystemCheck] " + lastStep); }
                    stepTime += Time.unscaledDeltaTime;
                    Hud.Warning(null);
                    Hud.Toast("SYSTEM CHECK", $"{lastStep}   ·   errors {Errors}", 0.5f);
                    if (stepTime > dwell) { lv.SkipStep(); stepTime = -0.5f; }
                }
                yield return null;
            }
            string result = Errors == 0 && MissingAudio.Count == 0 ? "PASS" : "FAIL";
            Debug.Log($"[SystemCheck] {result}: {StepsVisited} steps, {Errors} errors, {MissingAudio.Count} missing clips");
            if (GameFlow.TestMode) yield break;
            string body = $"{result}\n\nSteps run: {StepsVisited}\nErrors: {Errors}\nMissing audio: {MissingAudio.Count}\n\n" + string.Join("\n", ErrorLog);
            int c = -1;
            yield return Menu.Choose("SYSTEM CHECK", body, new[] { "BACK TO TITLE" }, i => c = i, -0.25f);
            GameFlow.Reload();
        }
    }
}
