using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// The Formation — Level 3 boss. A constantly shifting geological mass around a molten core.
    /// Armour plates must be knocked off to expose the core. Attack patterns:
    ///  Rock barrage (shield it or pinch the rocks out of the air and throw them back),
    ///  Ground rupture (a warning pattern on one half of the play area — physically step aside),
    ///  Compression (it drags rocks into itself — push them away before it regenerates).
    /// Final phase: tear the last plates off with telekinesis, then shatter it with a two-handed Force Push.
    /// </summary>
    public class Formation : MonoBehaviour, IDamageable, IPushable, ITarget
    {
        public System.Action OnDefeated;
        public int maxHp = 9;
        public int Hp { get; private set; }
        public bool Alive => Hp > 0;
        public bool Exposed { get; private set; }
        public bool FinalPhase => Hp <= 1;
        public string Status { get; private set; } = "";
        public Vector3 AimPoint => core.position;

        class Bit { public Transform t; public Vector3 dir; public float r, speed, phase; }
        readonly List<Bit> bits = new List<Bit>();
        readonly List<ArmorPlate> plates = new List<ArmorPlate>();
        Transform core;
        Material coreMat;
        Light coreLight;
        float flash, exposedTimer, attackTimer = 3f, absorbPulse;
        int absorbed;
        bool busy;

        public class ArmorPlate : MonoBehaviour, IDamageable, ICompressible
        {
            public Formation owner; public Vector3 home; public bool off;
            public Vector3 Point => transform.position;
            public bool CanCompress => PowerSet.Compress && !off && !owner.FinalPhase;
            void OnEnable() => Compressibles.All.Add(this);
            void OnDisable() => Compressibles.All.Remove(this);
            public void Damage(int amount, Vector3 point, Vector3 dir) { if (!owner.FinalPhase) Break(dir); }
            public void Compress() => Break(Vector3.up);
            public void Break(Vector3 dir)
            {
                if (off) return;
                off = true;
                Impact.Spawn(transform.position, -dir, Palette.Yellow, ImpactKind.Heavy, 1.2f);
                gameObject.SetActive(false);
                owner.PlateLost();
            }
        }

        /// <summary>Rock dragged toward the boss during its compression attack.</summary>
        public class PullRock : MonoBehaviour, IPushable
        {
            public Formation owner; public float speed = 1.4f; bool pushed; Vector3 vel;
            public void Push(Vector3 impulse, bool heavy)
            {
                if (pushed) return;
                pushed = true; vel = impulse.normalized * 12f + Vector3.up * 3f;
                Impact.Spawn(transform.position, impulse.normalized, Palette.Blue, ImpactKind.Stone, 0.8f);
                Destroy(gameObject, 1.5f);
            }
            void Update()
            {
                if (pushed) { transform.position += vel * Time.deltaTime; vel += Vector3.down * 9f * Time.deltaTime; return; }
                Vector3 to = owner.AimPoint - transform.position;
                if (to.magnitude < 0.8f) { owner.Absorb(); Destroy(gameObject); return; }
                transform.position += to.normalized * speed * Time.deltaTime;
                transform.Rotate(60f * Time.deltaTime, 40f * Time.deltaTime, 0);
            }
        }

        public static Formation Spawn(Vector3 pos, Quaternion facing)
        {
            var go = new GameObject("TheFormation");
            go.transform.SetPositionAndRotation(pos, facing);
            var f = go.AddComponent<Formation>();
            f.Hp = f.maxHp;
            f.BuildBody();
            Targets.All.Add(f);
            return f;
        }

        void BuildBody()
        {
            var stone = Mats.Stone(Palette.Stone, Palette.Yellow * 0.06f);
            var stoneB = Mats.Stone(Palette.StoneLight * 0.7f, Palette.Yellow * 0.12f);
            var rnd = new System.Random(9);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < 110; i++)
            {
                Vector3 d = new Vector3(R(-1, 1), R(-0.9f, 1.3f), R(-0.6f, 0.6f)).normalized;
                float r = i < 60 ? R(0.9f, 1.6f) : R(1.6f, 2.8f);
                var go = Build.Shape("Bit", Meshes.Rock(i), i % 4 == 0 ? stoneB : stone, transform, d * r, Vector3.one * R(0.18f, 0.6f), Random.rotation);
                bits.Add(new Bit { t = go.transform, dir = d, r = r, speed = R(0.1f, 0.4f) * (rnd.NextDouble() < 0.5 ? -1 : 1), phase = R(0, 6) });
            }
            coreMat = new Material(Mats.Lit(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.6f, 0.15f) * 3f));
            core = Build.Shape("Core", Meshes.Sphere, coreMat, transform, Vector3.zero, Vector3.one * 0.75f, null, true).transform;
            coreLight = core.gameObject.AddComponent<Light>();
            coreLight.type = LightType.Point; coreLight.color = Palette.Yellow; coreLight.range = 14f; coreLight.intensity = 3f;
            var glow = Build.Particles("CoreGlow", core, Palette.Yellow, 0.06f, 0.4f, 1.2f, 60f, 0.5f, true);
            glow.Play();
            var plateMat = Mats.Stone(Palette.StoneLight, Palette.Yellow * 0.3f);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                var home = new Vector3(Mathf.Cos(a) * 0.65f, Mathf.Sin(a) * 0.65f, 0.35f);
                var go = Build.Shape("Plate", Meshes.Rock(500 + i), plateMat, transform, home, new Vector3(0.55f, 0.5f, 0.25f), null, true);
                var p = go.AddComponent<ArmorPlate>();
                p.owner = this; p.home = home;
                plates.Add(p);
            }
            Sfx.Play(Sound.Roar, transform.position, 1f);
        }

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            flash -= dt;
            absorbPulse = Mathf.MoveTowards(absorbPulse, 0, dt * 2f);
            foreach (var b in bits)
            {
                float a = Time.time * b.speed + b.phase;
                Vector3 d = Quaternion.Euler(0, a * Mathf.Rad2Deg, Mathf.Sin(a * 0.7f) * 15f) * b.dir;
                float breathe = 1f + Mathf.Sin(Time.time * 0.8f + b.phase) * 0.05f + absorbPulse * 0.25f;
                b.t.localPosition = d * b.r * breathe;
                b.t.Rotate(b.speed * 30f * dt, b.speed * 20f * dt, 0);
            }
            transform.position += Vector3.up * Mathf.Sin(Time.time * 0.6f) * 0.002f;

            if (Exposed && !FinalPhase)
            {
                exposedTimer -= dt;
                if (exposedTimer <= 0) Regenerate();
            }
            float glow = flash > 0 ? 7f : Exposed ? 5f + Mathf.Sin(Time.time * 18f) * 1.5f : 2.5f;
            coreMat.SetColor("_EmissionColor", (flash > 0 ? Palette.White : new Color(1f, 0.6f, 0.15f)) * glow);
            coreLight.intensity = glow * 0.8f;

            if (busy) return;
            attackTimer -= dt;
            if (attackTimer > 0) return;
            int phase = Hp > 6 ? 0 : Hp > 3 ? 1 : 2;
            float roll = Random.value;
            if (phase >= 2 && roll < 0.33f) StartCoroutine(CompressionAttack());
            else if (phase >= 1 && roll < 0.66f) StartCoroutine(Rupture());
            else StartCoroutine(Barrage(3 + phase));
        }

        IEnumerator Barrage(int count)
        {
            busy = true; Status = "Rock barrage — shield it, or catch the rocks and throw them back.";
            Sfx.Play(Sound.Charge, AimPoint, 1f, 0.6f);
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < count; i++)
            {
                Vector3 from = AimPoint + Random.insideUnitSphere * 1.2f + Vector3.up * 0.5f;
                Vector3 target = HandInput.Head.position + Random.insideUnitSphere * 0.25f;
                var p = Projectile.Spawn(from, target - from, 4.8f, false, Palette.Yellow, 0.14f, transform);
                p.catchable = true;
                Sfx.Play(Sound.Fire, from, 0.9f, 0.7f);
                yield return new WaitForSeconds(0.45f);
            }
            attackTimer = FinalPhase ? 3f : 4.2f; busy = false; Status = "";
        }

        IEnumerator Rupture()
        {
            busy = true;
            float side = Arena.HeadLocal.x >= 0 ? 1f : -1f;
            Status = "Ground rupture! Step to the other side of your space.";
            var warn = new List<LineRenderer>();
            for (int i = 0; i < 9; i++)
            {
                var lr = Build.Line("Warn", Arena.Root, new Color(1f, 0.35f, 0.08f), 0.02f);
                lr.useWorldSpace = false;
                lr.positionCount = 8;
                Vector3 p = new Vector3(side * Random.Range(0.05f, 0.95f), 0.01f, Random.Range(-0.95f, 0.95f));
                Vector3 d = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                for (int k = 0; k < 8; k++)
                {
                    lr.SetPosition(k, p);
                    p += d * 0.12f + new Vector3(Random.Range(-0.05f, 0.05f), 0, Random.Range(-0.05f, 0.05f));
                    p.x = side > 0 ? Mathf.Clamp(p.x, 0.02f, 1f) : Mathf.Clamp(p.x, -1f, -0.02f);
                }
                warn.Add(lr);
            }
            Sfx.Play(Sound.Rumble, Arena.W(side * 0.5f, 0, 0), 1f);
            float t = 0;
            while (t < 1.9f)
            {
                t += Time.deltaTime;
                foreach (var lr in warn) lr.widthMultiplier = 0.01f + Mathf.Abs(Mathf.Sin(t * 12f)) * 0.025f * (t / 1.9f);
                yield return null;
            }
            foreach (var lr in warn) Destroy(lr.gameObject);
            Sfx.Play(Sound.Rupture, Arena.W(side * 0.5f, 0, 0), 1f);
            Feel.Shake(0.03f);
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = Arena.W(side * Random.Range(0.1f, 0.9f), 0, Random.Range(-0.9f, 0.9f));
                var spike = Build.Shape("Spike", Meshes.Rock(Random.Range(0, 30)), Mats.Stone(Palette.Stone, Palette.Yellow * 0.2f), null, p,
                    new Vector3(0.25f, 0.9f, 0.25f), Quaternion.Euler(Random.Range(-15, 15), Random.Range(0, 360), Random.Range(-15, 15)));
                Impact.Spawn(p, Vector3.up, new Color(1f, 0.4f, 0.1f), ImpactKind.Stone, 1f);
                Destroy(spike, 1.6f);
            }
            if (Mathf.Sign(Arena.HeadLocal.x) == side && Mathf.Abs(Arena.HeadLocal.x) > 0.05f) Player.I.Damage(2, Arena.Feet);
            attackTimer = 3.5f; busy = false; Status = "";
        }

        IEnumerator CompressionAttack()
        {
            busy = true; absorbed = 0;
            Status = "It's pulling the ground into itself — Force Push the rocks away!";
            Sfx.Play(Sound.Pull, AimPoint, 1f, 0.6f);
            var rocks = new List<PullRock>();
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = Arena.W(Random.Range(-1.8f, 1.8f), 0.3f, Random.Range(1.5f, 3.5f));
                var go = Build.Shape("Pulled", Meshes.Rock(Random.Range(0, 30)), Mats.Stone(Palette.Stone, Palette.Yellow * 0.15f), null, p, Vector3.one * 0.35f, Random.rotation, true);
                var pr = go.AddComponent<PullRock>(); pr.owner = this;
                rocks.Add(pr);
                Impact.Spawn(p, Vector3.up, Palette.Yellow, ImpactKind.Stone, 0.6f);
            }
            yield return new WaitUntil(() => rocks.TrueForAll(r => r == null));
            if (absorbed >= 3) { Regenerate(); Hp = Mathf.Min(maxHp, Hp + 1); Hud.Say("It drank them in and healed.", 2.5f); }
            attackTimer = 3.5f; busy = false; Status = "";
        }

        public void Absorb() { absorbed++; absorbPulse = 1f; Sfx.Play(Sound.Compress, AimPoint, 0.6f, 0.7f); }

        void PlateLost()
        {
            if (plates.TrueForAll(p => p.off))
            {
                Exposed = true;
                exposedTimer = 7f;
                Vortex.Spawn(AimPoint, Arena.Feet - AimPoint, Palette.Yellow, 2.2f, 2f, 60);
                Sfx.Play(Sound.Roar, AimPoint, 1f, 1.2f);
                Feel.HitStop(0.08f);
                Status = FinalPhase ? "The core is bare. Shatter it with a TWO-HANDED Force Push!" : "Core exposed — hit it now!";
            }
        }

        void Regenerate()
        {
            Exposed = false;
            foreach (var p in plates)
            {
                p.off = false;
                p.gameObject.SetActive(true);
                p.transform.localPosition = p.home;
            }
            Sfx.Play(Sound.Assemble, AimPoint, 1f, 0.8f);
        }

        /// <summary>Final phase: plates become tearable — grab them with telekinesis to rip them off.</summary>
        void EnterFinal()
        {
            Regenerate();
            foreach (var p in plates)
            {
                var col = p.GetComponent<Collider>();
                var rb = p.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
                var t = p.gameObject.AddComponent<Throwable>();
                t.rb = rb; t.tearable = true; t.damage = 1;
                var plate = p;
                t.OnTorn += _ => { plate.off = true; PlateLost(); Destroy(plate); };
            }
            Status = "Final phase: grab its armour with telekinesis and RIP it away.";
            Sfx.Play(Sound.Roar, AimPoint, 1f, 0.7f);
        }

        public void Damage(int amount, Vector3 point, Vector3 dir)
        {
            if (!Alive || FinalPhase) return;
            if (!Exposed) { Impact.Sparks(point, -dir, Palette.White, 20, 3f); return; }
            Hp = Mathf.Max(1, Hp - amount);
            flash = 0.15f;
            Sfx.Play(Sound.Roar, AimPoint, 0.7f, 1.4f);
            Feel.HitStop(0.06f);
            if (Hp <= 1) EnterFinal();
            else if (Hp % 3 == 0) Regenerate();
        }

        public void Push(Vector3 impulse, bool heavy)
        {
            if (!Alive) return;
            if (FinalPhase && Exposed && heavy) { StartCoroutine(Shatter()); return; }
            if (FinalPhase && Exposed) Hud.Say("Both hands. All at once.", 2f);
        }

        IEnumerator Shatter()
        {
            Hp = 0;
            Targets.All.Remove(this);
            Sfx.Play(Sound.Roar, AimPoint, 1f, 0.5f);
            Feel.HitStop(0.15f);
            for (int i = 0; i < 6; i++)
            {
                Impact.Spawn(AimPoint + Random.insideUnitSphere * 1.2f, Random.onUnitSphere, Palette.Yellow, ImpactKind.Death);
                yield return new WaitForSeconds(0.15f);
            }
            foreach (var b in bits)
            {
                b.t.SetParent(null, true);
                var rb = b.t.gameObject.AddComponent<Rigidbody>();
                b.t.gameObject.AddComponent<BoxCollider>();
                rb.AddForce((b.t.position - AimPoint).normalized * Random.Range(6f, 14f) + Vector3.up * 2f, ForceMode.VelocityChange);
                Destroy(b.t.gameObject, Random.Range(3f, 5f));
            }
            Feel.Flash(AimPoint, Palette.White, 12f, 20f, 1.2f);
            OnDefeated?.Invoke();
            Destroy(gameObject, 0.2f);
        }
    }
}
