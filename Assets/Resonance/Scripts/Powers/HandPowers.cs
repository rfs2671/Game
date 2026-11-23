using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>Unlock flags plus two-hand coordination (merged pushes, both-palm hold).</summary>
    public partial class PowerSet : MonoBehaviour
    {
        public static bool Telekinesis, Push, Shield, Gravity, Compress;
        public static HandPowers LeftHand, RightHand;
        public static System.Action<bool> OnPush;          // heavy?
        public static System.Action OnShardVolley;

        static readonly float[] pending = { -1, -1 };
        const float MergeWindow = 0.12f;

        /// <summary>Both open palms facing forward and held still (used to hold the passage open).</summary>
        public static bool BothPalmsHeld => HandInput.Left.tracked && HandInput.Right.tracked
                                            && HandInput.Left.shieldPose && HandInput.Right.shieldPose;

        public static void QueuePush(Hand h) => pending[(int)h] = Time.time;

        void Update()
        {
            if (Menu.Blocking) return;
            if (Compress && HandInput.CompressGesture) DoCompress(HandInput.CompressPoint);
            float now = Time.time;
            bool l = pending[0] >= 0, r = pending[1] >= 0;
            if (!l && !r) return;
            if (l && r)
            {
                pending[0] = pending[1] = -1;
                var a = HandInput.Left; var b = HandInput.Right;
                Vector3 origin = (a.palm + b.palm) * 0.5f;
                Vector3 dir = ((a.palmNormal + b.palmNormal) * 0.5f + HandInput.Head.forward).normalized;
                HandPowers.DoPush(origin, dir, 13f, true);
                pending[0] = pending[1] = -1;
                return;
            }
            int i = l ? 0 : 1;
            if (now - pending[i] < MergeWindow) return;
            pending[i] = -1;
            var s = HandInput.Get((Hand)i);
            Vector3 d = (s.palmNormal + s.aimDir + HandInput.Head.forward * 0.6f).normalized;
            HandPowers.DoPush(s.palm, d, 7.5f, false);
        }
    }

    public interface ICompressible { Vector3 Point { get; } bool CanCompress { get; } void Compress(); }

    public static class Compressibles
    {
        public static readonly List<ICompressible> All = new List<ICompressible>();
    }

    public partial class PowerSet
    {
        /// <summary>Compression: crush a held stone into a dense shard, or crush armour/heavy rock in front of you.</summary>
        static void DoCompress(Vector3 point)
        {
            HandPowers holder = null;
            foreach (var h in HandPowers.All)
            {
                var ht = h.HeldObject;
                if (ht != null && !ht.anchored && !ht.isKey && !ht.twistable && !ht.dense && ht.keyId == "") { holder = h; break; }
            }
            Vector3 fx = point;
            if (holder != null)
            {
                var t = holder.HeldObject;
                fx = t.transform.position;
                holder.ReleaseSilently();
                Destroy(t.gameObject);
                var dense = Throwable.CreateDense(fx);
                holder.ForceHold(dense);
            }
            else
            {
                ICompressible best = null; float bestA = 22f;
                var head = HandInput.Head;
                foreach (var c in Compressibles.All)
                {
                    if (c == null || (c is Object o && o == null) || !c.CanCompress) continue;
                    Vector3 to = c.Point - head.position;
                    if (to.magnitude > 7f) continue;
                    float a = Vector3.Angle(head.forward, to);
                    if (a < bestA) { bestA = a; best = c; }
                }
                if (best == null) { Implode(point, 0.4f); Sfx.Play(Sound.Compress, point, 0.4f); return; }
                fx = best.Point;
                best.Compress();
            }
            Implode(fx, 1f);
            Sfx.Play(Sound.Compress, fx, 1f);
            Feel.HitStop(0.07f);
            Feel.Shake(0.01f);
        }

        static void Implode(Vector3 p, float power)
        {
            var ps = Build.Particles("Implode", null, new Color(1f, 0.55f, 0.25f), 0.02f, -3f * power, 0.3f, 0, 0.6f * power, false);
            ps.transform.position = p;
            ps.Emit((int)(90 * power));
            Object.Destroy(ps.gameObject, 1f);
            Feel.Flash(p, new Color(1f, 0.6f, 0.3f), 4f * power, 3f, 0.3f);
            Impact.Sparks(p, Vector3.up, new Color(1f, 0.6f, 0.3f), (int)(40 * power), 3f);
            Vortex.Spawn(p, HandInput.Head.forward, new Color(1f, 0.45f, 0.2f), 0.5f * power + 0.2f, 0.5f, (int)(24 * power), false, null, 1f);
        }
    }

    /// <summary>
    /// Per-hand powers. Pinch = telekinesis grab, release = throw. Open-palm thrust = Force Push.
    /// Still open palm = Rock Shield; thrusting through a raised shield fires its shards.
    /// </summary>
    public class HandPowers : MonoBehaviour
    {
        public static readonly List<HandPowers> All = new List<HandPowers>();
        public Hand hand;

        // Telekinesis
        Throwable target, held;
        float holdDist, reachAtGrab;
        LineRenderer tether;
        ParticleSystem aura;
        public bool Holding => held != null;
        public Throwable HeldObject => held;
        Quaternion twistOffset;
        AudioSource hum;

        // Gravity Pull
        readonly List<Throwable> pulled = new List<Throwable>();
        float pullGrace;
        public bool Pulling => pulled.Count > 0;

        // Shield
        public bool ShieldUp { get; private set; }
        public Vector3 ShieldCenter { get; private set; }
        readonly List<Transform> shards = new List<Transform>();
        int charge;
        float shieldGrace;
        LineRenderer shieldRing;

        void Start()
        {
            All.Add(this);
            if (hand == Hand.Left) PowerSet.LeftHand = this; else PowerSet.RightHand = this;
            tether = Build.Line("Tether", transform, Palette.Blue, 0.008f);
            tether.positionCount = 8;
            tether.enabled = false;
            aura = Build.Particles("HandAura", transform, Palette.Blue, 0.018f, 0.05f, 0.5f, 25f, 0.05f, true);
            aura.Play();
            shieldRing = Build.Line("ShieldRing", transform, Palette.Yellow, 0.006f);
            shieldRing.positionCount = 32;
            shieldRing.loop = true;
            shieldRing.enabled = false;
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            if (Menu.Blocking) return;
            var s = HandInput.Get(hand);
            if (!s.tracked)
            {
                if (held != null) Release(s, false);
                SetTarget(null);
                if (ShieldUp) DropShield(false);
                SetAura(false, Palette.Blue, 0);
                return;
            }
            transform.position = s.palm;

            // Thrust: either fires the shield's shards or queues a Force Push.
            if (s.thrust && held == null)
            {
                if (pulled.Count > 0) LaunchPulled(s);
                else if (ShieldUp) Volley(s);
                else if (PowerSet.Push) PowerSet.QueuePush(hand);
            }

            UpdateGravity(s);
            UpdateShield(s);
            if (!ShieldUp && pulled.Count == 0) UpdateTelekinesis(s);
            if (hum != null) hum.volume = Mathf.MoveTowards(hum.volume, held != null || pulled.Count > 0 ? 0.35f : 0f, Time.deltaTime * 2f);

            Color c = ShieldUp ? Palette.Yellow : Palette.Blue;
            float intensity = held != null ? 1f : ShieldUp ? 0.8f : target != null ? 0.6f : 0.25f;
            SetAura(true, c, intensity);
        }

        void SetAura(bool on, Color c, float intensity)
        {
            var em = aura.emission;
            em.rateOverTime = on ? 10f + 60f * intensity : 0f;
            var main = aura.main;
            main.startColor = c * (0.5f + intensity);
        }

        // ---------------------------------------------------------------- telekinesis
        void UpdateTelekinesis(HandState s)
        {
            if (!PowerSet.Telekinesis) { SetTarget(null); tether.enabled = false; return; }

            if (held == null)
            {
                SetTarget(FindTarget(s));
                if (s.pinchDown && target == null)
                {
                    var caught = Projectile.TryCatch(s.pinchPoint, s.aimDir);
                    if (caught != null) target = caught;
                }
                if (s.pinchDown && target != null) Grab(s, target);
                tether.enabled = false;
                return;
            }

            if (held.locked || held == null) { held = null; tether.enabled = false; return; }

            float reach = Vector3.Distance(HandInput.Head.position, s.pinchPoint);
            float dist = Mathf.Clamp(holdDist + (reach - reachAtGrab) * 5f, 0.35f, 7f);
            Vector3 goal = s.aimOrigin + s.aimDir * dist;
            var rb = held.rb;
            if (!held.anchored) Phys.SetVel(rb, Vector3.ClampMagnitude((goal - rb.position) * 12f, 18f));
            if (held.twistable)
            {
                // Puzzle pieces follow the hand's rotation (yaw only when constrained).
                Quaternion want = s.palmRot * twistOffset;
                if (s.simulated)
                {
                    float spin = (SimIn.Held(KeyCode.Z) ? -90f : 0f) + (SimIn.Held(KeyCode.X) ? 90f : 0f);
                    want = Quaternion.Euler(0, spin * Time.deltaTime, 0) * rb.rotation;
                }
                if (held.yawOnly) want = Quaternion.Euler(0, want.eulerAngles.y, 0);
                if (!rb.isKinematic) rb.angularVelocity = Vector3.zero;
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, want, Time.deltaTime * 10f));
            }
            else
            {
                float shiver = Mathf.Sin(Time.time * 37f) * 0.6f;
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, new Vector3(0.4f + shiver, 0.8f, 0.2f), Time.deltaTime * 2f);
            }

            DrawTether(s.pinchPoint, rb.position);
            if (s.pinchUp) Release(s, s.IsThrowRelease);
        }

        Throwable FindTarget(HandState s)
        {
            Throwable best = null;
            float bestScore = 9f; // degrees
            foreach (var t in Throwable.All)
            {
                if (!t.Grabbable || t.heavy) continue;
                Vector3 to = t.transform.position - s.aimOrigin;
                float d = to.magnitude;
                if (d > 14f) continue;
                if (d < 0.3f) return t;
                float a = Vector3.Angle(s.aimDir, to) + d * 0.15f; // prefer nearer when similar
                if (a < bestScore) { bestScore = a; best = t; }
            }
            return best;
        }

        void SetTarget(Throwable t)
        {
            if (target == t) return;
            if (target != null) target.SetHighlight(false);
            target = t;
            if (target != null) target.SetHighlight(true);
        }

        void Grab(HandState s, Throwable t)
        {
            SetTarget(null);
            held = t;
            t.SetHeld(true);
            holdDist = Mathf.Clamp(Vector3.Distance(s.aimOrigin, t.transform.position), 0.5f, 3f);
            reachAtGrab = Vector3.Distance(HandInput.Head.position, s.pinchPoint);
            twistOffset = Quaternion.Inverse(s.palmRot) * t.transform.rotation;
            Sfx.Play(Sound.Grab, t.transform.position, 0.8f);
            Impact.Sparks(t.transform.position, -s.aimDir, Palette.Blue, 30, 2f);
            Feel.Flash(t.transform.position, Palette.Blue, 2f, 2.5f, 0.2f);
            EnsureHum(t.transform);
        }

        void Release(HandState s, bool doThrow)
        {
            var t = held;
            held = null;
            tether.enabled = false;
            if (t == null || t.locked) return;
            t.SetHeld(false);
            if (t.anchored) return;
            if (doThrow)
            {
                Vector3 dir = s.aimDir;
                var aim = Targets.Best(t.transform.position, dir, 14f);
                if (aim != null) dir = (aim.AimPoint - t.transform.position).normalized;
                float speed = (t.dense ? 18f : 11f) + s.ForwardSpeed * 5f;
                Phys.SetVel(t.rb, dir * speed);
                t.rb.angularVelocity = Random.insideUnitSphere * 12f;
                t.thrownUntil = Time.time + 2f;
                t.ShowTrail(true);
                Sfx.Play(Sound.Whoosh, t.transform.position, 0.9f);
            }
            else
            {
                Phys.SetVel(t.rb, Vector3.ClampMagnitude(s.velocity * 1.5f, 3f));
            }
        }

        public void ForceHold(Throwable t)
        {
            var s = HandInput.Get(hand);
            if (held != null) Release(s, false);
            Grab(s, t);
        }

        public void ReleaseSilently()
        {
            if (held != null) held.SetHeld(false);
            held = null;
            tether.enabled = false;
        }

        void EnsureHum(Transform t)
        {
            if (hum == null) hum = Sfx.Loop("tk_loop", transform, 0f);
        }

        // ---------------------------------------------------------------- gravity pull
        void UpdateGravity(HandState s)
        {
            if (!PowerSet.Gravity) return;
            if (s.grabAir && held == null && !ShieldUp && pulled.Count == 0)
            {
                foreach (var t in Throwable.All)
                {
                    if (pulled.Count >= 6) break;
                    if (t.held || t.locked || t.isKey || t.rb.isKinematic || t.heavy) continue;
                    Vector3 to = t.transform.position - s.palm;
                    if (to.magnitude > 10f || Vector3.Angle(s.aimDir, to) > 40f) continue;
                    pulled.Add(t);
                }
                foreach (var t in pulled) { t.SetHeld(true); Impact.Sparks(t.transform.position, -s.aimDir, Palette.Blue, 12, 2f); }
                if (pulled.Count > 0) { Sfx.Play(Sound.Pull, s.palm, 1f); EnsureHum(transform); }
            }
            if (pulled.Count == 0) return;
            pulled.RemoveAll(t => t == null || t.locked);
            Vector3 center = s.palm + s.aimDir * 0.75f + Vector3.up * 0.05f;
            for (int i = 0; i < pulled.Count; i++)
            {
                float a = Time.time * 3f + i * Mathf.PI * 2f / pulled.Count;
                Vector3 goal = center + Quaternion.LookRotation(s.aimDir) * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.32f;
                var rb = pulled[i].rb;
                Phys.SetVel(rb, Vector3.ClampMagnitude((goal - rb.position) * 9f, 16f));
            }
            DrawTether(s.palm, center);
            if (s.fist) pullGrace = 0.4f;
            else
            {
                pullGrace -= Time.deltaTime;
                if (pullGrace <= 0) { foreach (var t in pulled) t.SetHeld(false); pulled.Clear(); tether.enabled = false; }
            }
        }

        void LaunchPulled(HandState s)
        {
            int i = 0;
            foreach (var t in pulled)
            {
                t.SetHeld(false);
                Vector3 dir = (s.palmNormal + s.aimDir).normalized;
                var aim = Targets.Best(t.transform.position, dir, 30f);
                if (aim != null) dir = (aim.AimPoint - t.transform.position).normalized;
                dir = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0) * dir;
                Phys.SetVel(t.rb, dir * (14f + i * 0.8f));
                t.thrownUntil = Time.time + 2f;
                t.ShowTrail(true);
                i++;
            }
            pulled.Clear();
            tether.enabled = false;
            Sfx.Play(Sound.Volley, s.palm, 1f);
            Sfx.Play(Sound.Push, s.palm, 0.6f);
            Feel.Shake(0.006f);
        }

        void DrawTether(Vector3 a, Vector3 b)
        {
            tether.enabled = true;
            int n = tether.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (n - 1f);
                Vector3 p = Vector3.Lerp(a, b, k);
                float wob = Mathf.Sin(k * Mathf.PI) * 0.03f;
                p += new Vector3(Mathf.Sin(Time.time * 23f + i * 1.7f), Mathf.Cos(Time.time * 19f + i * 2.3f), 0) * wob;
                tether.SetPosition(i, p);
            }
        }

        // ---------------------------------------------------------------- shield
        void UpdateShield(HandState s)
        {
            bool want = PowerSet.Shield && held == null && s.shieldPose;
            if (want && !ShieldUp) RaiseShield(s);
            if (!ShieldUp) return;

            if (want) shieldGrace = 0.35f;
            else
            {
                shieldGrace -= Time.deltaTime;
                if (shieldGrace <= 0) { DropShield(false); return; }
            }

            Vector3 n = s.palmNormal.sqrMagnitude > 0.1f ? s.palmNormal : HandInput.Head.forward;
            ShieldCenter = s.palm + n * 0.26f;
            Quaternion rot = Quaternion.LookRotation(n, Vector3.up);
            float radius = 0.17f + charge * 0.012f;
            for (int i = 0; i < shards.Count; i++)
            {
                Vector3 local = i == 0 ? Vector3.zero
                    : Quaternion.Euler(0, 0, i * (360f / (shards.Count - 1)) + Time.time * 25f) * new Vector3(radius, 0, 0);
                Vector3 goal = ShieldCenter + rot * local;
                shards[i].position = Vector3.Lerp(shards[i].position, goal, Time.deltaTime * 14f);
                shards[i].rotation = Quaternion.Slerp(shards[i].rotation, rot * Quaternion.Euler(0, 0, i * 40f), Time.deltaTime * 8f);
            }
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                shieldRing.SetPosition(i, ShieldCenter + rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (radius + 0.07f));
            }
            Color rc = Palette.Yellow * (0.4f + 0.6f * Mathf.Clamp01(charge / 4f));
            shieldRing.startColor = shieldRing.endColor = rc;
        }

        void RaiseShield(HandState s)
        {
            ShieldUp = true;
            shieldRing.enabled = true;
            charge = 0;
            for (int i = 0; i < 9; i++)
            {
                var go = Build.Shape("ShieldShard", Meshes.Rock(i + 3), Mats.Lit(Palette.StoneLight, Palette.Yellow * 0.35f), null,
                    s.palm + Random.insideUnitSphere * 0.8f + Vector3.down * 0.4f,
                    Vector3.one * (i == 0 ? 0.13f : 0.09f), Random.rotation);
                shards.Add(go.transform);
            }
            Sfx.Play(Sound.Shield, s.palm, 0.9f);
        }

        void DropShield(bool silent)
        {
            ShieldUp = false;
            shieldRing.enabled = false;
            foreach (var t in shards)
            {
                if (t == null) continue;
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                t.gameObject.AddComponent<BoxCollider>();
                Destroy(t.gameObject, 2.5f);
            }
            shards.Clear();
            charge = 0;
            if (!silent) Sfx.Play(Sound.Hit, ShieldCenter, 0.3f);
        }

        /// <summary>Called by enemy projectiles. Returns true if this shield caught it.</summary>
        public bool TryBlock(Vector3 p, float radius)
        {
            if (!ShieldUp) return false;
            if (Vector3.Distance(p, ShieldCenter) > 0.3f + radius + charge * 0.012f) return false;
            charge = Mathf.Min(charge + 1, 6);
            Impact.Spawn(p, (p - ShieldCenter).normalized, Palette.Yellow, ImpactKind.Shield);
            Feel.HitStop(0.04f);
            Projectile.OnBlocked?.Invoke();
            return true;
        }

        void Volley(HandState s)
        {
            int count = Mathf.Clamp(2 + charge, 2, 8);
            Vector3 dir = (s.palmNormal + s.aimDir + HandInput.Head.forward).normalized;
            var aim = Targets.Best(ShieldCenter, dir, 25f);
            for (int i = 0; i < count; i++)
            {
                Vector3 d = aim != null ? (aim.AimPoint - ShieldCenter).normalized : dir;
                d = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0) * d;
                var p = Projectile.Spawn(ShieldCenter + Random.insideUnitSphere * 0.1f, d, 16f + i, true, Palette.Yellow, 0.07f);
                p.homing = aim;
            }
            foreach (var t in shards) if (t != null) Destroy(t.gameObject);
            shards.Clear();
            ShieldUp = false;
            shieldRing.enabled = false;
            charge = 0;
            Sfx.Play(Sound.Volley, ShieldCenter, 1f);
            Feel.Shake(0.006f);
            PowerSet.OnShardVolley?.Invoke();
        }

        // ---------------------------------------------------------------- force push
        public static void DoPush(Vector3 origin, Vector3 dir, float strength, bool heavy)
        {
            float range = heavy ? 9f : 6f;
            float cos = Mathf.Cos((heavy ? 42f : 32f) * Mathf.Deg2Rad);
            var done = new HashSet<object>();
            foreach (var c in Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 to = c.bounds.center - origin;
                float dist = to.magnitude;
                if (dist > 0.4f && Vector3.Dot(to / dist, dir) < cos) continue;
                float fall = 1f - Mathf.Clamp01(dist / range) * 0.6f;
                var pushable = c.GetComponentInParent<IPushable>();
                if (pushable != null)
                {
                    if (done.Add(pushable)) pushable.Push(dir * strength * fall, heavy);
                    continue;
                }
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || !done.Add(rb)) continue;
                var t = rb.GetComponent<Throwable>();
                if (t != null && (t.held || t.locked)) continue;
                rb.AddForce(dir * strength * fall * 1.3f + Vector3.up * 1.2f, ForceMode.VelocityChange);
                if (t != null) t.thrownUntil = Time.time + 1f;
            }
            PushWave.Spawn(origin, dir, range * 0.55f, heavy);
            Impact.Sparks(origin + dir * 0.3f, dir, Palette.Blue, heavy ? 120 : 50, heavy ? 9f : 6f);
            Feel.Flash(origin + dir * 0.5f, Palette.Blue, heavy ? 6f : 3f, heavy ? 6f : 3.5f, 0.3f);
            Sfx.Play(heavy ? Sound.PushHeavy : Sound.Push, origin, 1f);
            Feel.Shake(heavy ? 0.02f : 0.008f);
            if (heavy) Feel.HitStop(0.05f);
            Vortex.Spawn(origin + dir * (heavy ? 1.6f : 1.1f), dir, Palette.Blue, heavy ? 1.1f : 0.6f, heavy ? 0.9f : 0.6f, heavy ? 44 : 20);
            PowerSet.OnPush?.Invoke(heavy);
        }
    }

    /// <summary>Expanding ring that visualises a Force Push.</summary>
    public class PushWave : MonoBehaviour
    {
        LineRenderer lr;
        Vector3 origin, dir;
        float maxR, t;
        bool heavy;

        public static void Spawn(Vector3 origin, Vector3 dir, float maxR, bool heavy)
        {
            var w = new GameObject("PushWave").AddComponent<PushWave>();
            w.origin = origin; w.dir = dir; w.maxR = maxR; w.heavy = heavy;
            w.lr = Build.Line("Ring", w.transform, Palette.Blue, heavy ? 0.04f : 0.025f);
            w.lr.positionCount = 40;
            w.lr.loop = true;
            var ps = Build.Particles("Dust", w.transform, Palette.Blue * 0.8f, 0.03f, heavy ? 6f : 4f, 0.5f, 0, 0.1f, false);
            ps.transform.position = origin;
            ps.transform.rotation = Quaternion.LookRotation(dir);
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = heavy ? 40f : 28f;
            ps.Emit(heavy ? 120 : 60);
        }

        void Update()
        {
            t += Time.deltaTime / 0.35f;
            if (t >= 1f) { Destroy(gameObject); return; }
            float e = 1f - (1f - t) * (1f - t);
            Vector3 c = origin + dir * (e * maxR);
            float r = 0.1f + e * maxR * 0.6f;
            var rot = Quaternion.LookRotation(dir);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                lr.SetPosition(i, c + rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r);
            }
            Color col = (heavy ? Palette.White : Palette.Blue) * (1f - t);
            lr.startColor = lr.endColor = col;
        }
    }
}
