using NUnit.Framework;
using UnityEngine;

namespace Resonance.Tests
{
    /// <summary>Fast checks that run without Play mode: Window > General > Test Runner > EditMode > Run All.</summary>
    public class ContentTests
    {
        [Test]
        public void EverySoundHasAClip()
        {
            foreach (var kv in Sfx.Map)
                foreach (var n in kv.Value)
                    Assert.IsNotNull(Sfx.Clip(n), $"Sound.{kv.Key} -> Resources/Audio/{n}.wav is missing");
        }

        [Test]
        public void EverySoundEnumIsMapped()
        {
            var mapped = new System.Collections.Generic.HashSet<Sound>();
            foreach (var kv in Sfx.Map) mapped.Add(kv.Key);
            foreach (Sound s in System.Enum.GetValues(typeof(Sound)))
                Assert.IsTrue(mapped.Contains(s), $"Sound.{s} has no clip mapping");
        }

        [Test]
        public void LoopsAndAmbienceExist()
        {
            foreach (var n in new[] { "tk_loop", "amb_quarry", "amb_tunnel", "amb_station", "amb_chamber" })
            {
                var c = Sfx.Clip(n);
                Assert.IsNotNull(c, n);
                Assert.Greater(c.length, 1f, n + " too short to loop");
            }
        }

        [Test]
        public void ClipsAreNotSilent()
        {
            foreach (var kv in Sfx.Map)
                foreach (var n in kv.Value)
                {
                    var c = Sfx.Clip(n);
                    if (c == null || c.loadType == AudioClipLoadType.Streaming) continue;
                    var data = new float[Mathf.Min(c.samples * c.channels, 44100 * 2)];
                    if (!c.GetData(data, 0)) continue;   // compressed-in-memory clips can't be read; skip
                    float peak = 0; foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                    Assert.Greater(peak, 0.05f, n + " is silent");
                }
        }

        [Test]
        public void RockMeshesAreValid()
        {
            for (int i = 0; i < 30; i++)
            {
                var m = Meshes.Rock(i);
                Assert.Greater(m.triangles.Length, 300);
                foreach (var v in m.vertices) Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
                Assert.Less(m.bounds.extents.magnitude, 1.5f);
            }
            Assert.IsNotNull(Meshes.Basalt);
        }

        [Test]
        public void SensitivityPresetsStayInSaneRanges()
        {
            for (int s = 0; s < 3; s++)
            {
                Save.Sensitivity = s;
                Assert.Less(Tuning.PinchOn, Tuning.PinchOff, "pinch hysteresis inverted");
                Assert.Greater(Tuning.ThrustSpeed, 0.5f);
            }
            Save.Sensitivity = 1;
        }
    }
}
