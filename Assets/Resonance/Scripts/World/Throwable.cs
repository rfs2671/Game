using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    public interface IDamageable { void Damage(int amount, Vector3 point, Vector3 dir); }
    public interface IPushable { void Push(Vector3 impulse, bool heavy); }
    public interface ITarget { Vector3 AimPoint { get; } bool Alive { get; } }

    /// <summary>Registry used for aim assist on throws and shard volleys.</summary>
    public static class Targets
    {
        public static readonly List<ITarget> All = new List<ITarget>();

        public static ITarget Best(Vector3 origin, Vector3 dir, float maxAngle, float maxDist = 20f)
        {
            ITarget best = null;
            float bestAngle = maxAngle;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var t = All[i];
                if (t == null || (t is Object o && o == null)) { All.RemoveAt(i); continue; }
                if (!t.Alive) continue;
                Vector3 to = t.AimPoint - origin;
                if (to.magnitude > maxDist) continue;
                float a = Vector3.Angle(dir, to);
                if (a < bestAngle) { bestAngle = a; best = t; }
            }
            return best;
        }
    }

    /// <summary>Anything the player can lift with telekinesis.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Throwable : MonoBehaviour
    {
        public static readonly List<Throwable> All = new List<Throwable>();

        public Rigidbody rb;
        public bool held, locked, isKey;
        public bool twistable, yawOnly, heavy, dense;
        /// <summary>anchored: rotates in place when held (puzzle pillars). zeroG: floats after release.
        /// tearable: kinematic until grabbed, then rips free (armour plates).</summary>
        public bool anchored, zeroG, tearable;
        public System.Action<Throwable> OnTorn, OnReleased;
        public System.Func<bool> canGrab;
        public string keyId = "";
        TrailRenderer trail;
        public float thrownUntil;
        public int damage = 2;
        public Vector3 spawnPoint;

        MeshRenderer rend;
        Material normalMat, hiMat, heldMat;
        bool highlighted;
        float lastImpactSfx;

        public static Throwable Create(Vector3 pos, float size, bool key = false, int seed = -1)
        {
            if (seed < 0) seed = Random.Range(0, 1000);
            var go = Build.Shape(key ? "Keystone" : "Rock", key ? Meshes.Cube : Meshes.Rock(seed),
                Mats.Lit(Palette.Stone, Palette.Blue * 0.06f), null, pos,
                key ? new Vector3(size * 1.4f, size, size) : Vector3.one * size, Random.rotation, true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.5f, size * 10f);
            Phys.SetDrag(rb, 0.15f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var t = go.AddComponent<Throwable>();
            t.rb = rb;
            t.isKey = key;
            t.spawnPoint = pos;
            if (key)
            {
                go.transform.rotation = Quaternion.identity;
                var glyph = Build.Box("Glyph", Mats.Glow(Palette.Yellow), go.transform, new Vector3(0, 0, -0.51f), new Vector3(0.5f, 0.12f, 0.02f));
                Build.Box("Glyph2", Mats.Glow(Palette.Yellow), go.transform, new Vector3(0, 0, 0.51f), new Vector3(0.5f, 0.12f, 0.02f));
                Build.Box("Glyph3", Mats.Glow(Palette.Yellow), go.transform, new Vector3(0, 0.51f, 0), new Vector3(0.5f, 0.02f, 0.12f));
                glyph.name = "Glyph";
            }
            else if (seed % 3 == 0)
            {
                Build.Box("Crack", Mats.Glow(Palette.Yellow * 0.9f), go.transform, new Vector3(0, 0.1f, -0.45f),
                    new Vector3(0.7f, 0.05f, 0.06f), false, Quaternion.Euler(0, 0, 25f));
            }
            return t;
        }

        /// <summary>Compression result: a small, white-hot, very heavy projectile that punches through armour.</summary>
        public static Throwable CreateDense(Vector3 pos)
        {
            var t = Create(pos, 0.12f, false, 3);
            var r = t.GetComponent<MeshRenderer>();
            t.dense = true;
            t.damage = 5;
            t.rb.mass = 6f;
            r.sharedMaterial = Mats.Lit(new Color(1f, 0.7f, 0.4f), new Color(1f, 0.45f, 0.15f) * 2.5f);
            var glow = Build.Particles("DenseGlow", t.transform, new Color(1f, 0.5f, 0.2f), 0.03f, 0.05f, 0.4f, 40f, 0.05f, true);
            glow.Play();
            return t;
        }

        public void ShowTrail(bool on)
        {
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = Mats.Additive(Color.white);
                Color c = dense ? new Color(1f, 0.5f, 0.2f) : Palette.Blue;
                trail.startColor = c * 0.8f; trail.endColor = new Color(c.r, c.g, c.b, 0f);
                trail.time = 0.22f;
                trail.startWidth = transform.localScale.x * 0.6f; trail.endWidth = 0f;
            }
            trail.emitting = on;
        }

        void Awake()
        {
            rend = GetComponent<MeshRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            normalMat = rend.sharedMaterial;
            hiMat = Mats.Lit(Palette.StoneLight, Palette.Blue * 0.9f);
            heldMat = Mats.Lit(Palette.StoneLight, Palette.Blue * 1.6f);
        }

        public void SetHighlight(bool on)
        {
            if (on == highlighted || held) return;
            highlighted = on;
            rend.sharedMaterial = on ? hiMat : normalMat;
        }

        public void SetHeld(bool on)
        {
            if (on && tearable && rb.isKinematic)
            {
                tearable = false;
                rb.isKinematic = false;
                transform.SetParent(null, true);
                Impact.Spawn(transform.position, Vector3.up, Palette.Yellow, ImpactKind.Stone, 1f);
                OnTorn?.Invoke(this);
            }
            held = on;
            highlighted = false;
            rend.sharedMaterial = on ? heldMat : normalMat;
            if (!anchored)
            {
                rb.useGravity = !on && !zeroG;
                Phys.SetDrag(rb, on ? 2f : zeroG ? 3f : 0.15f);
            }
            if (!on) OnReleased?.Invoke(this);
        }

        public bool Grabbable => !held && !locked && (!rb.isKinematic || anchored || tearable) && (canGrab == null || canGrab());

        /// <summary>Custom-shaped liftable object (energy cores, plugs, machine parts).</summary>
        public static Throwable CreateWith(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = Build.Shape(name, mesh, mat, null, pos, scale, rot, true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var t = go.AddComponent<Throwable>();
            t.rb = rb; t.spawnPoint = pos; t.damage = 1;
            return t;
        }

        public void Lock(Vector3 pos, Quaternion rot)
        {
            locked = true;
            if (held) SetHeld(false);
            rb.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            rend.sharedMaterial = Mats.Lit(Palette.StoneLight, Palette.Yellow * 0.8f);
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            Vector3 n = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            bool thrown = Time.time < thrownUntil;
            if (v > 3f && Time.time - lastImpactSfx > 0.15f)
            {
                lastImpactSfx = Time.time;
                if (thrown && v > 7f) Impact.Spawn(p, n, Palette.Blue * 0.8f, ImpactKind.Stone, Mathf.Clamp(v / 14f, 0.4f, 1.2f));
                else Sfx.Play(Sound.Hit, p, Mathf.Clamp01(v / 12f) * 0.7f);
            }
            if (!thrown || v < 2.5f) return;
            if (trail) trail.emitting = false;
            var d = c.collider.GetComponentInParent<IDamageable>();
            if (d != null && !(d is Object o && o == this))
            {
                thrownUntil = 0;
                d.Damage(damage, p, Phys.Vel(rb).normalized);
                Impact.Spawn(p, n, dense ? new Color(1f, 0.55f, 0.2f) : Palette.Yellow, ImpactKind.Heavy, dense ? 1.5f : 1f);
                if (dense) Destroy(gameObject, 0.05f);
            }
        }

        void Update()
        {
            if (Arena.Placed && !rb.isKinematic && transform.position.y < Arena.Root.position.y - 4f)
            {
                // Fell out of the world: put it back where it started.
                rb.isKinematic = false;
                Phys.SetVel(rb, Vector3.zero);
                transform.position = spawnPoint + Vector3.up * 0.5f;
            }
        }
    }

    /// <summary>Boulder too heavy to lift. Compression cracks it into liftable pieces.</summary>
    public class HeavyRock : MonoBehaviour, ICompressible
    {
        public System.Action OnCrushed;
        public Vector3 Point => transform.position;
        public bool CanCompress => PowerSet.Compress;
        void OnEnable() => Compressibles.All.Add(this);
        void OnDisable() => Compressibles.All.Remove(this);

        public static HeavyRock Create(Vector3 pos, float size)
        {
            var go = Build.Shape("HeavyRock", Meshes.Rock(Random.Range(0, 30)), Mats.Stone(Palette.Stone, Palette.Yellow * 0.08f), null, pos,
                Vector3.one * size, Random.rotation, true);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true;
            Build.Box("Vein", Mats.Glow(Palette.Yellow), go.transform, new Vector3(0, 0, -0.45f), new Vector3(0.6f, 0.03f, 0.03f), false, Quaternion.Euler(0, 0, 30));
            return go.AddComponent<HeavyRock>();
        }

        public void Compress()
        {
            float s = transform.localScale.x;
            for (int i = 0; i < 4; i++)
                Throwable.Create(transform.position + Random.insideUnitSphere * s * 0.3f, s * 0.35f);
            Impact.Spawn(transform.position, Vector3.up, Palette.Yellow, ImpactKind.Heavy, 1.3f);
            OnCrushed?.Invoke();
            Destroy(gameObject);
        }
    }

    /// <summary>Floating crystal for throw practice.</summary>
    public class Crystal : MonoBehaviour, IDamageable, ITarget
    {
        public System.Action<Crystal> OnBroken;
        public bool Alive { get; private set; } = true;
        public Vector3 AimPoint => transform.position;
        Vector3 home;
        float phase;

        public static Crystal Create(Vector3 pos)
        {
            var go = new GameObject("Crystal");
            go.transform.position = pos;
            for (int i = 0; i < 3; i++)
            {
                Build.Shape("Shard", Meshes.Cube, Mats.Lit(Palette.White, Palette.Blue * 1.4f, 0.9f), go.transform,
                    new Vector3((i - 1) * 0.06f, 0, 0), new Vector3(0.07f, 0.32f - i % 2 * 0.1f, 0.07f),
                    Quaternion.Euler(0, 45, (i - 1) * 18f));
            }
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.22f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var c = go.AddComponent<Crystal>();
            c.home = pos;
            c.phase = Random.value * 6f;
            Targets.All.Add(c);
            return c;
        }

        void Update()
        {
            transform.position = home + Vector3.up * Mathf.Sin(Time.time * 1.4f + phase) * 0.06f;
            transform.Rotate(0, 40f * Time.deltaTime, 0);
        }

        public void Damage(int amount, Vector3 point, Vector3 dir)
        {
            if (!Alive) return;
            Alive = false;
            Impact.Spawn(transform.position, -dir, Palette.Blue, ImpactKind.Energy, 1.2f);
            Sfx.Play(Sound.Chime, transform.position, 0.8f);
            OnBroken?.Invoke(this);
            Targets.All.Remove(this);
            Destroy(gameObject);
        }
    }

    /// <summary>Glowing mineral the player reaches out and touches.</summary>
    public class Touchable : MonoBehaviour
    {
        public System.Action OnTouched;
        bool done;

        void Update()
        {
            if (done || HandInput.I == null || Menu.Blocking) return;
            foreach (var h in new[] { HandInput.Left, HandInput.Right })
            {
                if (!h.tracked) continue;
                Vector3 p = transform.position;
                bool touch = Vector3.Distance(h.indexTip, p) < 0.11f || Vector3.Distance(h.palm, p) < 0.11f;
                if (h.simulated && h.pinchDown && Vector3.Angle(h.aimDir, p - h.aimOrigin) < 6f
                    && Vector3.Distance(h.aimOrigin, p) < 1.6f) touch = true;
                if (touch)
                {
                    done = true;
                    Build.Burst(p, Palette.Blue, 80, 2.5f, 0.05f);
                    Sfx.Play(Sound.Chime, p, 1f);
                    OnTouched?.Invoke();
                    return;
                }
            }
        }
    }
}
