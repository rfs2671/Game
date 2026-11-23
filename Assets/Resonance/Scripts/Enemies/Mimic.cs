using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Faceless creature of floating rock that copies the player's hands. Its core sits behind armour plates
    /// that only part when its "arms" go up — i.e. when the player raises both hands above their head.
    /// Plates can also be crushed one by one with Compression.
    /// </summary>
    public class Mimic : MonoBehaviour, IDamageable, ITarget
    {
        public System.Action OnDied;
        public int hp = 6;
        public bool Alive => hp > 0;
        public Vector3 AimPoint => core != null ? core.position : transform.position;
        public bool Exposed { get; private set; }

        class Orbit { public Transform t; public Vector3 home; public float phase, speed; }
        readonly List<Orbit> body = new List<Orbit>();
        readonly List<Orbit> armL = new List<Orbit>(), armR = new List<Orbit>();
        readonly List<Plate> plates = new List<Plate>();
        Transform core;
        Material coreMat;
        float attackTimer = 3f, flash, open;

        class Plate : MonoBehaviour, ICompressible
        {
            public Mimic owner; public Vector3 home; public bool gone;
            public Vector3 Point => transform.position;
            public bool CanCompress => PowerSet.Compress && !gone;
            void OnEnable() => Compressibles.All.Add(this);
            void OnDisable() => Compressibles.All.Remove(this);
            public void Compress()
            {
                gone = true;
                Impact.Spawn(transform.position, Vector3.up, Palette.Red, ImpactKind.Heavy, 1.2f);
                gameObject.SetActive(false);
                owner.Growl();
            }
        }

        public static Mimic Spawn(Vector3 pos, Quaternion facing)
        {
            var go = new GameObject("Mimic");
            go.transform.SetPositionAndRotation(pos, facing);
            var m = go.AddComponent<Mimic>();
            m.BuildBody();
            Targets.All.Add(m);
            return m;
        }

        void BuildBody()
        {
            var stone = Mats.Stone(Palette.Stone, Palette.Red * 0.05f);
            var rnd = new System.Random(4);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < 70; i++)
            {
                float h = R(0.2f, 2.4f);
                float spread = 0.32f + 0.18f * Mathf.Sin(h * 2.2f);
                var home = new Vector3(R(-1, 1) * spread, h, R(-1, 1) * 0.22f);
                body.Add(Part(stone, home, R(0.06f, 0.19f), i));
            }
            for (int i = 0; i < 12; i++) armL.Add(Part(stone, Vector3.zero, R(0.07f, 0.13f), 100 + i));
            for (int i = 0; i < 12; i++) armR.Add(Part(stone, Vector3.zero, R(0.07f, 0.13f), 200 + i));
            coreMat = new Material(Mats.Lit(new Color(1f, 0.3f, 0.12f), new Color(1f, 0.25f, 0.1f) * 3f));
            core = Build.Shape("Core", Meshes.Sphere, coreMat, transform, new Vector3(0, 1.45f, 0.05f), Vector3.one * 0.22f, null, true).transform;
            var l = core.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.3f, 0.12f); l.range = 3.5f; l.intensity = 2.5f;
            var plateMat = Mats.Stone(Palette.StoneLight * 0.8f, Palette.Red * 0.25f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var go = Build.Shape("Plate", Meshes.Rock(300 + i), plateMat, transform, Vector3.zero, new Vector3(0.24f, 0.24f, 0.1f), null, true);
                var p = go.AddComponent<Plate>();
                p.owner = this;
                p.home = new Vector3(Mathf.Cos(a) * 0.14f, 1.45f + Mathf.Sin(a) * 0.14f, 0.22f);
                go.transform.localPosition = p.home;
                plates.Add(p);
            }
            // the Control-style tell: stone torn into a slow vortex around its core
            Vortex.Spawn(transform.position + Vector3.up * 1.45f, transform.forward, new Color(1f, 0.22f, 0.08f), 1.25f, 0f, 70, true, transform);
            var col = gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1.3f, 0); col.height = 2.4f; col.radius = 0.45f;
            var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
            Sfx.Play(Sound.Assemble, transform.position, 1f);
        }

        Orbit Part(Material m, Vector3 home, float size, int seed)
        {
            var go = Build.Shape("Rock", Meshes.Rock(seed), m, transform, home, Vector3.one * size, Random.rotation);
            return new Orbit { t = go.transform, home = home, phase = Random.value * 10f, speed = Random.Range(0.5f, 1.5f) };
        }

        public void Growl() { Sfx.Play(Sound.Growl, AimPoint, 1f, 0.8f); }

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            flash -= dt;
            foreach (var o in body)
                o.t.localPosition = o.home + new Vector3(Mathf.Sin(Time.time * o.speed + o.phase), Mathf.Cos(Time.time * o.speed * 1.3f + o.phase), 0) * 0.04f;

            // Mirror the player's hands: their right hand drives our left arm, like a reflection.
            var head = HandInput.Head;
            Arm(armL, HandInput.Right, head, -1f);
            Arm(armR, HandInput.Left, head, 1f);

            bool armsUp = HandInput.Left.tracked && HandInput.Right.tracked
                          && HandInput.Left.palm.y > head.position.y + 0.02f && HandInput.Right.palm.y > head.position.y + 0.02f;
            bool platesGone = plates.TrueForAll(p => p.gone);
            Exposed = armsUp || platesGone;
            open = Mathf.MoveTowards(open, Exposed ? 1f : 0f, dt * 3f);
            foreach (var p in plates)
            {
                if (p.gone) continue;
                Vector3 outDir = new Vector3(p.home.x, p.home.y - 1.45f, 0).normalized;
                p.transform.localPosition = p.home + (outDir * 0.45f + Vector3.up * 0.1f) * open;
            }
            float glow = flash > 0 ? 6f : Exposed ? 4f + Mathf.Sin(Time.time * 16f) : 1.5f;
            coreMat.SetColor("_EmissionColor", (flash > 0 ? Palette.White : new Color(1f, 0.25f, 0.1f)) * glow);

            attackTimer -= dt;
            if (attackTimer <= 0)
            {
                attackTimer = Exposed ? 5f : 3.6f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 from = AimPoint + transform.right * (i == 0 ? -0.5f : 0.5f) + Vector3.up * 0.3f;
                    Projectile.Spawn(from, head.position - from, 3.8f, false, Palette.Red, 0.09f, transform).catchable = true;
                }
                Sfx.Play(Sound.Fire, AimPoint, 0.8f);
            }
        }

        void Arm(List<Orbit> arm, HandState h, Transform head, float side)
        {
            Vector3 shoulder = new Vector3(0.38f * side, 1.95f, 0);
            Vector3 hand;
            if (h.tracked)
            {
                Vector3 rel = head.InverseTransformPoint(h.palm);   // player's hand relative to their face
                hand = new Vector3(-rel.x * 2.4f, 1.6f + rel.y * 2.4f, 0.25f);
            }
            else hand = new Vector3(0.55f * side, 1.0f, -0.1f);
            for (int i = 0; i < arm.Count; i++)
            {
                float k = i / (arm.Count - 1f);
                Vector3 goal = Vector3.Lerp(shoulder, hand, k) + new Vector3(Mathf.Sin(Time.time * 2f + i), Mathf.Cos(Time.time * 1.7f + i), 0) * 0.03f;
                arm[i].t.localPosition = Vector3.Lerp(arm[i].t.localPosition, goal, Time.deltaTime * 6f);
            }
        }

        public void Damage(int amount, Vector3 point, Vector3 dir)
        {
            if (!Alive) return;
            if (!Exposed)
            {
                Impact.Sparks(point, -dir, Palette.White, 20, 3f);
                Sfx.Play(Sound.Crack, point, 0.8f);
                return;
            }
            hp -= amount;
            flash = 0.15f;
            Growl();
            if (hp <= 0) Die(dir);
        }

        void Die(Vector3 dir)
        {
            Targets.All.Remove(this);
            Impact.Spawn(AimPoint, -dir, new Color(1f, 0.3f, 0.12f), ImpactKind.Death);
            foreach (var t in GetComponentsInChildren<MeshRenderer>())
            {
                t.transform.SetParent(null, true);
                var rb = t.gameObject.AddComponent<Rigidbody>();
                if (t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();
                rb.AddForce((Random.insideUnitSphere + Vector3.up) * 4f, ForceMode.VelocityChange);
                Destroy(t.gameObject, Random.Range(2.5f, 4f));
            }
            OnDied?.Invoke();
            Destroy(gameObject, 0.1f);
        }
    }
}
