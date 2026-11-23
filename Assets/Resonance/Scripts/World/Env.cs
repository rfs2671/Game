using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Base for a level's procedural environment, built under the anchored arena.
    /// Handles ambience, reverb, the shake root, and the "fold" transition where the old space
    /// rotates and collapses into itself while the next one unfolds piece by piece.
    /// </summary>
    public abstract class Env : MonoBehaviour
    {
        public static Env Current { get; private set; }
        protected Transform root;
        AudioReverbZone reverb;

        protected abstract string AmbienceClip { get; }
        protected abstract AudioReverbPreset ReverbPreset { get; }
        protected abstract void BuildEnv();

        public static T Create<T>(string name) where T : Env
        {
            var go = new GameObject(name);
            go.transform.SetParent(Arena.Root, false);
            var e = go.AddComponent<T>();
            e.root = go.transform;
            return e;
        }

        public void Construct()
        {
            BuildEnv();
            Activate();
        }

        public void Activate()
        {
            Current = this;
            Feel.ShakeRoot = root;
            Sfx.Ambience(AmbienceClip);
            if (reverb == null)
            {
                reverb = gameObject.AddComponent<AudioReverbZone>();
                reverb.minDistance = 30f;
                reverb.maxDistance = 60f;
            }
            reverb.reverbPreset = ReverbPreset;
        }

        // ---------------------------------------------------------------- fold transitions
        struct Piece { public Transform t; public Vector3 pos, scale; public Quaternion rot; public float delay; }
        List<Piece> pieces;

        void Capture()
        {
            pieces = new List<Piece>();
            foreach (Transform c in root)
            {
                float d = Vec.Flat(c.localPosition).magnitude;
                pieces.Add(new Piece { t = c, pos = c.localPosition, rot = c.localRotation, scale = c.localScale, delay = d * 0.05f });
            }
        }

        static Vector3 FoldedPos(Piece p)
        {
            // Pieces swing around the vertical axis toward a seam far ahead, and sink.
            Vector3 v = Quaternion.Euler(0, p.pos.x > 0 ? 70f : -70f, 0) * p.pos;
            return v * 0.3f + Vector3.forward * 6f + Vector3.down * 2f;
        }

        public IEnumerator FoldIn(float duration = 3f)
        {
            Capture();
            foreach (var p in pieces) { p.t.localPosition = FoldedPos(p); p.t.localScale = Vector3.zero; }
            Sfx.Play(Sound.Rumble, Arena.W(0, 1.5f, 4f), 1f);
            float t = 0, maxDelay = 0;
            foreach (var p in pieces) maxDelay = Mathf.Max(maxDelay, p.delay);
            while (t < duration + maxDelay)
            {
                t += Time.deltaTime;
                foreach (var p in pieces)
                {
                    if (p.t == null) continue;
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - p.delay) / duration));
                    p.t.localPosition = Vector3.Lerp(FoldedPos(p), p.pos, k);
                    p.t.localRotation = Quaternion.Slerp(p.rot * Quaternion.Euler(0, 0, 90f), p.rot, k);
                    p.t.localScale = Vector3.Lerp(Vector3.zero, p.scale, k);
                }
                yield return null;
            }
            foreach (var p in pieces) if (p.t) { p.t.localPosition = p.pos; p.t.localRotation = p.rot; p.t.localScale = p.scale; }
        }

        public IEnumerator FoldOut(float duration = 2.5f)
        {
            Capture();
            float t = 0;
            Feel.Shake(0.02f);
            while (t < duration)
            {
                t += Time.deltaTime;
                foreach (var p in pieces)
                {
                    if (p.t == null) continue;
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - (0.6f - p.delay * 0.3f)) / (duration * 0.7f)));
                    p.t.localPosition = Vector3.Lerp(p.pos, FoldedPos(p), k);
                    p.t.localRotation = Quaternion.Slerp(p.rot, p.rot * Quaternion.Euler(0, 0, -90f), k);
                    p.t.localScale = Vector3.Lerp(p.scale, Vector3.zero, k);
                }
                yield return null;
            }
            if (Current == this) Current = null;
            Destroy(gameObject);
        }
    }

    /// <summary>Slow bob/drift for floating debris, papers and the like.</summary>
    public class Floaty : MonoBehaviour
    {
        public float amp = 0.08f, speed = 0.6f, spin = 10f;
        Vector3 home; float phase; Vector3 axis;
        void Start() { home = transform.localPosition; phase = Random.value * 10f; axis = Random.onUnitSphere; }
        void Update()
        {
            transform.localPosition = home + new Vector3(Mathf.Sin(Time.time * speed * 0.7f + phase) * 0.4f,
                Mathf.Sin(Time.time * speed + phase), Mathf.Cos(Time.time * speed * 0.8f + phase) * 0.4f) * amp;
            transform.Rotate(axis, spin * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>Fluorescent tube flicker: swaps between lit/unlit materials and drives an optional light.</summary>
    public class Flicker : MonoBehaviour
    {
        public Material on, off;
        public Light lightRef;
        public float baseIntensity = 1f, rate = 0.08f;
        MeshRenderer r; float next; bool lit = true;
        void Start() { r = GetComponent<MeshRenderer>(); }
        void Update()
        {
            if (Time.time < next) return;
            lit = Random.value > (lit ? rate : 0.35f);
            next = Time.time + (lit ? Random.Range(0.3f, 3f) : Random.Range(0.03f, 0.15f));
            if (r) r.sharedMaterial = lit ? on : off;
            if (lightRef) lightRef.intensity = lit ? baseIntensity : 0.05f;
        }
    }

    /// <summary>Makes a renderer visible only while the player's hand light is near (Level 1 dark sections).</summary>
    public class RevealByLight : MonoBehaviour
    {
        public float radius = 2.2f;
        Renderer[] rends;
        void Start() { rends = GetComponentsInChildren<Renderer>(); Set(false); }
        void Set(bool v) { foreach (var r in rends) if (r) r.enabled = v; }
        void Update()
        {
            bool lit = HandLight.On && Vector3.Distance(HandLight.Position, transform.position) < radius;
            Set(lit);
        }
    }

    /// <summary>The blue light the left hand generates in dark places.</summary>
    public class HandLight : MonoBehaviour
    {
        public static bool On { get; private set; }
        public static Vector3 Position { get; private set; }
        static HandLight I;
        Light l;
        ParticleSystem motes;
        float level;

        public static void SetOn(bool on)
        {
            if (I == null) I = new GameObject("HandLight").AddComponent<HandLight>();
            if (on && !On) Sfx.Play(Sound.LightOn, HandInput.Head.position, 0.8f);
            On = on;
        }

        void Start()
        {
            l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 0.8f, 1f);
            l.range = 4f;
            l.shadows = LightShadows.None;
            motes = Build.Particles("Motes", transform, new Color(0.5f, 0.85f, 1f), 0.012f, 0.03f, 0.8f, 30f, 0.06f, true);
            motes.Play();
        }

        void Update()
        {
            var h = HandInput.Left.tracked ? HandInput.Left : HandInput.Right;
            Position = h.palm + h.palmNormal * 0.06f;
            transform.position = Position;
            level = Mathf.MoveTowards(level, On ? 1f : 0f, Time.deltaTime * 2f);
            l.intensity = level * (2.2f + Mathf.Sin(Time.time * 9f) * 0.15f);
            var em = motes.emission; em.rateOverTime = level * 30f;
        }
    }
}
