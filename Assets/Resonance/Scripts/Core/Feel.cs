using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Resonance
{
    /// <summary>
    /// Game-feel layer: hit-stop, environment shake (the world shakes, never the player's head),
    /// light flashes and layered impact effects. Used by every power and enemy so hits land with weight.
    /// </summary>
    public class Feel : MonoBehaviour
    {
        static Feel I;
        float stopUntil, shake;
        readonly List<(Light l, float t0, float dur, float peak)> flashes = new List<(Light, float, float, float)>();
        public static Transform ShakeRoot;   // set by the active environment
        Vector3 shakeOffset;

        void Awake() { I = this; }

        /// <summary>Freeze time briefly. 40-90 ms sells a heavy hit without feeling laggy.</summary>
        public static void HitStop(float seconds)
        {
            if (I == null) return;
            I.stopUntil = Mathf.Max(I.stopUntil, Time.unscaledTime + seconds);
            Time.timeScale = 0.04f;
        }

        public static void Shake(float amount) { if (I) I.shake = Mathf.Min(0.12f, I.shake + amount); }

        public static void Flash(Vector3 pos, Color c, float intensity, float range, float duration)
        {
            if (I == null) return;
            var go = new GameObject("Flash");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            I.flashes.Add((l, Time.unscaledTime, duration, intensity));
        }

        void Update()
        {
            if (stopUntil > 0 && Time.unscaledTime >= stopUntil) { Time.timeScale = 1f; stopUntil = 0; }

            if (ShakeRoot != null)
            {
                ShakeRoot.localPosition -= shakeOffset;
                shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 0.25f);
                shakeOffset = shake > 0.0005f ? Random.insideUnitSphere * shake : Vector3.zero;
                ShakeRoot.localPosition += shakeOffset;
            }

            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                float k = (Time.unscaledTime - f.t0) / f.dur;
                if (f.l == null || k >= 1f) { if (f.l) Destroy(f.l.gameObject); flashes.RemoveAt(i); continue; }
                f.l.intensity = f.peak * (1f - k) * (1f - k);
            }
        }
    }

    public enum ImpactKind { Stone, Heavy, Shield, Energy, Death }

    /// <summary>Layered impact: core flash, stretched sparks, chunky debris, dust puff, light, sound, shake.</summary>
    public static class Impact
    {
        public static void Spawn(Vector3 pos, Vector3 normal, Color c, ImpactKind kind, float power = 1f)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            int sparks = (int)(kind == ImpactKind.Death ? 160 : kind == ImpactKind.Heavy ? 90 : 45 * power);
            Sparks(pos, normal, c, sparks, kind == ImpactKind.Death ? 6f : 4f * power);
            Glow(pos, c, kind == ImpactKind.Death ? 0.9f : 0.35f * power);
            if (kind != ImpactKind.Energy && kind != ImpactKind.Shield) Debris(pos, normal, kind == ImpactKind.Death ? 14 : (int)(5 * power), power);
            Dust(pos, kind == ImpactKind.Death ? 2.2f : 1f * power);
            Feel.Flash(pos + normal * 0.2f, c, kind == ImpactKind.Death ? 6f : 3f * power, kind == ImpactKind.Death ? 6f : 3f, 0.25f);
            switch (kind)
            {
                case ImpactKind.Stone: Sfx.Play(Sound.Hit, pos, 0.7f + 0.3f * power); Feel.Shake(0.004f * power); break;
                case ImpactKind.Heavy: Sfx.Play(Sound.Hit, pos, 1f, 0.8f); Sfx.Play(Sound.Debris, pos, 0.6f); Feel.Shake(0.012f); Feel.HitStop(0.06f); break;
                case ImpactKind.Shield: Sfx.Play(Sound.Block, pos, 1f); break;
                case ImpactKind.Energy: Sfx.Play(Sound.Crack, pos, 0.7f, 1.2f); break;
                case ImpactKind.Death: Sfx.Play(Sound.Death, pos, 1f); Feel.Shake(0.02f); Feel.HitStop(0.09f); break;
            }
        }

        public static void Sparks(Vector3 pos, Vector3 dir, Color c, int count, float speed)
        {
            var ps = Build.Particles("Sparks", null, c * 1.6f, 0.012f, speed, 0.45f, 0, 0.03f, false);
            ps.transform.position = pos;
            ps.transform.rotation = Quaternion.LookRotation(dir);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.gravityModifier = 0.8f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.5f;
            ps.Emit(count);
            Object.Destroy(ps.gameObject, 1.5f);
        }

        static void Glow(Vector3 pos, Color c, float size)
        {
            var ps = Build.Particles("Glow", null, c, size, 0f, 0.18f, 0, 0.01f, false);
            ps.transform.position = pos;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.4f, 1, 1.6f));
            ps.Emit(1);
            Object.Destroy(ps.gameObject, 0.6f);
        }

        static void Dust(Vector3 pos, float scale)
        {
            var ps = Build.Particles("Dust", null, new Color(0.55f, 0.6f, 0.7f, 0.35f), 0.25f * scale, 0.6f * scale, 1.4f, 0, 0.1f, false);
            ps.transform.position = pos;
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.45f * scale);
            main.gravityModifier = -0.02f;
            ps.Emit((int)(10 * scale));
            Object.Destroy(ps.gameObject, 2f);
        }

        public static void Debris(Vector3 pos, Vector3 normal, int count, float power)
        {
            var mat = Mats.Lit(Palette.Stone, Palette.Blue * 0.04f);
            for (int i = 0; i < count; i++)
            {
                var go = Build.Shape("Chip", Meshes.Rock(Random.Range(0, 30)), mat, null, pos + Random.insideUnitSphere * 0.05f,
                    Vector3.one * Random.Range(0.025f, 0.07f) * Mathf.Sqrt(power), Random.rotation, true);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Phys.SetVel(rb, (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(1.5f, 4.5f) * power);
                rb.angularVelocity = Random.insideUnitSphere * 20f;
                Object.Destroy(go, Random.Range(1.8f, 3.2f));
            }
        }
    }
}

namespace Resonance
{
    /// <summary>
    /// Swirling vortex: stone fragments spiralling around an axis (faster near the centre), orbital spark streaks
    /// and turning arcs. Used for Force Push shockwaves, the Mimic, deaths and Compression. Persistent or timed.
    /// </summary>
    public class Vortex : MonoBehaviour
    {
        class Frag { public Transform t; public float r, a, h, speed, size; }
        readonly List<Frag> frags = new List<Frag>();
        readonly List<LineRenderer> arcs = new List<LineRenderer>();
        ParticleSystem streaks;
        float life, age, radius, inward;
        bool persistent;
        Color color;

        public static Vortex Spawn(Vector3 pos, Vector3 axis, Color c, float radius, float life, int rocks = 36, bool persistent = false,
            Transform follow = null, float inward = 0f)
        {
            var go = new GameObject("Vortex");
            if (follow != null) go.transform.SetParent(follow, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(axis.sqrMagnitude > 0.01f ? axis : Vector3.forward);
            var v = go.AddComponent<Vortex>();
            v.radius = radius; v.life = life; v.persistent = persistent; v.color = c; v.inward = inward;
            v.Construct(rocks);
            return v;
        }

        void Construct(int rocks)
        {
            var mat = Mats.Stone(Palette.Stone, color * 0.06f);
            for (int i = 0; i < rocks; i++)
            {
                float k = Mathf.Pow(Random.value, 0.7f);
                var f = new Frag { r = radius * (0.15f + 0.85f * k), a = Random.value * Mathf.PI * 2f, h = Random.Range(-0.12f, 0.12f) * radius, size = Random.Range(0.02f, 0.07f) * (0.6f + k) };
                f.speed = Random.Range(2.2f, 3.4f) / Mathf.Max(0.3f, f.r / radius + 0.2f);
                f.t = Build.Shape("Frag", Meshes.Rock(Random.Range(0, 30)), mat, transform, Vector3.zero, Vector3.one * f.size, Random.rotation).transform;
                frags.Add(f);
            }
            for (int i = 0; i < 6; i++)
            {
                var lr = Build.Line("Arc", transform, color * 0.9f, 0.006f);
                lr.useWorldSpace = false;
                lr.positionCount = 10;
                arcs.Add(lr);
            }
            streaks = Build.Particles("Streaks", transform, color * 1.4f, 0.012f, 0f, 0.6f, persistent ? 90f : 0f, radius, persistent);
            var sh = streaks.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.radiusThickness = 1f;
            var vel = streaks.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(5f, 8f);
            vel.radial = new ParticleSystem.MinMaxCurve(-0.6f - inward * 3f);
            var main = streaks.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var r = streaks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 1.2f;
            if (persistent) streaks.Play(); else streaks.Emit(160);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float k = persistent ? 1f : Mathf.Clamp01(age / life);
            float fade = persistent ? 1f : 1f - k * k;
            float spread = persistent ? 1f + Mathf.Sin(age * 0.8f) * 0.05f : 1f + k * (inward > 0 ? -0.8f : 0.9f);
            foreach (var f in frags)
            {
                f.a += f.speed * dt * (1.2f - k * 0.6f);
                float rr = f.r * spread;
                f.t.localPosition = new Vector3(Mathf.Cos(f.a) * rr, Mathf.Sin(f.a) * rr, f.h + Mathf.Sin(f.a * 2f) * 0.02f);
                f.t.Rotate(200f * dt, 140f * dt, 0, Space.Self);
                f.t.localScale = Vector3.one * f.size * Mathf.Clamp01(fade * 1.5f);
            }
            for (int i = 0; i < arcs.Count; i++)
            {
                float baseR = radius * (0.3f + 0.12f * i) * spread;
                float a0 = age * (3.5f - i * 0.35f) + i * 1.7f;
                for (int s = 0; s < 10; s++)
                {
                    float a = a0 + s * 0.09f;
                    arcs[i].SetPosition(s, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * baseR * (1f + s * 0.01f));
                }
                Color c = color * (0.9f * fade);
                arcs[i].startColor = new Color(c.r, c.g, c.b, 0f);
                arcs[i].endColor = c;
            }
            if (!persistent && age >= life) Destroy(gameObject);
        }
    }
}
