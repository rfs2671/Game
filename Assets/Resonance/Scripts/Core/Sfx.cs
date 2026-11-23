using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    public enum Sound
    {
        Hit, Whoosh, Shield, Charge, Thump, Chime, Rumble, Grab, Hurt,
        Crack, Debris, Push, PushHeavy, Block, Volley, Assemble, Growl, Death, Fire, Door,
        Unlock, Button, Insert, LightOn, Pull, Compress, Roar, Rupture
    }

    /// <summary>
    /// Sound playback using the designed WAV library in Resources/Audio (layered impacts, granular debris,
    /// sub-bass pushes, reverberant tails). Pooled 3D sources, random pitch/variant per hit, looping beds,
    /// and an ambience crossfader. Falls back to silence-safe behaviour if a clip is missing.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<Sound, string[]> files = new Dictionary<Sound, string[]>
        {
            { Sound.Hit, new[] { "rock_impact_1", "rock_impact_2", "rock_impact_3" } },
            { Sound.Crack, new[] { "rock_crack" } }, { Sound.Debris, new[] { "debris_tail" } },
            { Sound.Whoosh, new[] { "throw" } }, { Sound.Push, new[] { "push" } }, { Sound.PushHeavy, new[] { "push_heavy" } },
            { Sound.Grab, new[] { "grab" } }, { Sound.Shield, new[] { "shield_form" } }, { Sound.Block, new[] { "shield_block" } },
            { Sound.Volley, new[] { "shard_volley" } }, { Sound.Assemble, new[] { "creature_assemble" } },
            { Sound.Growl, new[] { "creature_growl" } }, { Sound.Death, new[] { "creature_death" } },
            { Sound.Charge, new[] { "launcher_charge" } }, { Sound.Fire, new[] { "launcher_fire" } },
            { Sound.Rumble, new[] { "rumble" } }, { Sound.Door, new[] { "door_open" } }, { Sound.Thump, new[] { "thump" } },
            { Sound.Unlock, new[] { "unlock" } }, { Sound.Chime, new[] { "chime" } }, { Sound.Button, new[] { "button" } },
            { Sound.Hurt, new[] { "hurt" } }, { Sound.Insert, new[] { "core_insert" } }, { Sound.LightOn, new[] { "light_on" } },
            { Sound.Pull, new[] { "pull" } }, { Sound.Compress, new[] { "compress" } }, { Sound.Roar, new[] { "boss_roar" } },
            { Sound.Rupture, new[] { "rupture" } },
        };
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static readonly Dictionary<Sound, float> lastPlay = new Dictionary<Sound, float>();
        static Transform poolRoot;
        static AudioSource ambA, ambB;
        static bool ambUseA = true;
        static float ambFade = 1f;

        public static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
            return c;
        }

        static AudioSource Source()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("SfxPool").transform;
                Object.DontDestroyOnLoad(poolRoot.gameObject);
                pool.Clear();
            }
            foreach (var s in pool) if (s != null && !s.isPlaying) return s;
            if (pool.Count >= 28) return pool[Random.Range(0, pool.Count)];
            var go = new GameObject("Src");
            go.transform.SetParent(poolRoot, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0.9f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 1.2f;
            a.maxDistance = 40f;
            a.dopplerLevel = 0f;
            pool.Add(a);
            return a;
        }

        public static void Play(Sound s, Vector3 pos, float vol = 0.8f, float pitch = 1f)
        {
            // Avoid machine-gunning the same sound in a single frame burst.
            if (lastPlay.TryGetValue(s, out float t) && Time.unscaledTime - t < 0.03f) return;
            lastPlay[s] = Time.unscaledTime;
            if (!files.TryGetValue(s, out var names)) return;
            var clip = Clip(names[Random.Range(0, names.Length)]);
            if (clip == null) return;
            var a = Source();
            a.transform.position = pos;
            a.clip = clip;
            a.loop = false;
            a.volume = Mathf.Clamp01(vol);
            a.pitch = pitch * Random.Range(0.93f, 1.07f);
            a.ignoreListenerPause = PauseWatcher.Paused;   // menu clicks still play while paused
            a.Play();
        }

        /// <summary>Looping source attached to a transform (e.g. telekinesis hum on the held stone).</summary>
        public static AudioSource Loop(string clipName, Transform follow, float vol)
        {
            var clip = Clip(clipName);
            var go = new GameObject("Loop_" + clipName);
            go.transform.SetParent(follow, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.volume = vol;
            a.spatialBlend = 0.9f;
            a.minDistance = 0.8f;
            a.dopplerLevel = 0f;
            if (clip != null) a.Play();
            return a;
        }

        /// <summary>Crossfade to a new 2D ambience bed ("amb_quarry", "amb_tunnel", "amb_station", "amb_chamber").</summary>
        public static void Ambience(string clipName, float vol = 0.45f)
        {
            if (ambA == null)
            {
                var go = new GameObject("Ambience");
                Object.DontDestroyOnLoad(go);
                ambA = go.AddComponent<AudioSource>(); ambB = go.AddComponent<AudioSource>();
                foreach (var a in new[] { ambA, ambB }) { a.loop = true; a.spatialBlend = 0f; a.volume = 0f; }
                go.AddComponent<AmbienceFader>();
            }
            var next = ambUseA ? ambB : ambA;
            next.clip = Clip(clipName);
            next.volume = 0f;
            next.Play();
            ambUseA = !ambUseA;
            ambFade = 0f;
            targetVol = vol;
        }

        static float targetVol = 0.45f;
        /// <summary>User ambience setting (0-1, 0.5 = designed level).</summary>
        public static float AmbienceLevel = 0.5f;
        public static System.Collections.Generic.IEnumerable<KeyValuePair<Sound, string[]>> Map => files;

        class AmbienceFader : MonoBehaviour
        {
            void Update()
            {
                var cur = ambUseA ? ambA : ambB;
                var old = ambUseA ? ambB : ambA;
                float v = targetVol * AmbienceLevel * 2f;
                if (ambFade >= 1f) { cur.volume = v; return; }
                ambFade = Mathf.Min(1f, ambFade + Time.unscaledDeltaTime / 3f);
                cur.volume = v * ambFade;
                old.volume = v * (1f - ambFade);
                if (ambFade >= 1f) old.Stop();
            }
        }
    }
}
