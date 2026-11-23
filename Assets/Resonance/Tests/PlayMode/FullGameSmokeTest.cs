using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Resonance.Tests
{
    /// <summary>
    /// Plays the entire game in the simulator: every step of all four levels runs its setup, spawns, effects and audio.
    /// SystemCheck counts every exception or Debug.LogError; the test fails if any occurred.
    /// Window > General > Test Runner > PlayMode > Run All (about 3-4 minutes).
    /// </summary>
    public class FullGameSmokeTest
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryLevelAndStepRunsWithoutErrors()
        {
            PlayerPrefs.DeleteAll();
            // Errors are counted by SystemCheck (which ignores the editor-only XREAL native-plugin load failure)
            // and asserted below with the exact messages, instead of failing on the first log line.
            LogAssert.ignoreFailingMessages = true;
            GameFlow.TestMode = true;
            var go = new GameObject("Resonance");
            go.AddComponent<GameBootstrap>();
            float t = 0;
            while ((GameFlow.I == null || !GameFlow.I.Finished) && t < 540f) { t += Time.unscaledDeltaTime; yield return null; }
            GameFlow.TestMode = false;
            Assert.IsNotNull(GameFlow.I, "GameFlow never started");
            Assert.IsTrue(GameFlow.I.Finished, $"Game did not reach the end (stuck at {LevelBase.Current?.StepName})");
            Assert.IsNotNull(SystemCheck.I);
            Assert.AreEqual(0, SystemCheck.I.MissingAudio.Count, "Missing audio: " + string.Join(", ", SystemCheck.I.MissingAudio));
            Assert.AreEqual(0, SystemCheck.I.Errors, "Errors during play:\n" + string.Join("\n", SystemCheck.I.ErrorLog));
            Assert.GreaterOrEqual(SystemCheck.I.StepsVisited, 35, "Not every step ran (35 expected)");
            Object.Destroy(go);
        }
    }
}
