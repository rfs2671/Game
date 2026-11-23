using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Resonance.Tests
{
    /// <summary>
    /// Plays the entire game in the simulator: every step of all four levels runs its setup, spawns, effects and audio.
    /// Unity fails the test automatically on any exception or Debug.LogError.
    /// Window > General > Test Runner > PlayMode > Run All (about 3-4 minutes).
    /// </summary>
    public class FullGameSmokeTest
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryLevelAndStepRunsWithoutErrors()
        {
            PlayerPrefs.DeleteAll();
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
            Assert.GreaterOrEqual(SystemCheck.I.StepsVisited, 35, "Not every step ran (35 expected)");
            Object.Destroy(go);
        }
    }
}
