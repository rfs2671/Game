using UnityEngine;

namespace Resonance
{
    /// <summary>Quarry machine that lobs slow, readable projectiles at the player's head.</summary>
    public class Launcher : MonoBehaviour, IDamageable, IPushable, ITarget
    {
        public System.Action OnDestroyed;
        public bool firing;
        public float interval = 2.4f;
        public bool Alive => hp > 0;
        public Vector3 AimPoint => headT != null ? headT.position : transform.position;

        int hp = 3;
        float timer, rise, flash;
        Transform headT, eye;
        Material eyeMat;
        bool charging;

        public static Launcher Spawn(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Launcher");
            go.transform.SetPositionAndRotation(pos, rot);
            var l = go.AddComponent<Launcher>();
            var stone = Mats.Lit(Palette.Stone, Palette.Blue * 0.05f);
            Build.Box("Base", stone, go.transform, new Vector3(0, 0.6f, 0), new Vector3(0.7f, 1.2f, 0.7f), true);
            Build.Box("Band", Mats.Glow(Palette.Yellow * 0.8f), go.transform, new Vector3(0, 1.0f, 0), new Vector3(0.72f, 0.04f, 0.72f));
            l.headT = Build.Box("Head", stone, go.transform, new Vector3(0, 1.55f, 0), new Vector3(0.55f, 0.4f, 0.65f), true).transform;
            l.eyeMat = new Material(Mats.Lit(Palette.Red, Palette.Red * 1.5f));
            l.eye = Build.Shape("Eye", Meshes.Sphere, l.eyeMat, l.headT, new Vector3(0, 0, -0.5f), new Vector3(0.35f, 0.35f, 0.2f)).transform;
            Build.Box("BarrelL", stone, l.headT, new Vector3(-0.32f, 0, -0.4f), new Vector3(0.12f, 0.12f, 0.5f));
            Build.Box("BarrelR", stone, l.headT, new Vector3(0.32f, 0, -0.4f), new Vector3(0.12f, 0.12f, 0.5f));
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Targets.All.Add(l);
            go.transform.position = pos + Vector3.down * 2f;
            l.riseTarget = pos;
            Sfx.Play(Sound.Rumble, pos, 0.8f);
            return l;
        }

        Vector3 riseTarget;

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (rise < 1f)
            {
                rise = Mathf.Min(1f, rise + dt / 1.6f);
                transform.position = Vector3.Lerp(riseTarget + Vector3.down * 2f, riseTarget, Mathf.SmoothStep(0, 1, rise));
                return;
            }

            Vector3 to = HandInput.Head.position - headT.position;
            // Eye faces -Z in head space, so look away from the target.
            headT.rotation = Quaternion.Slerp(headT.rotation, Quaternion.LookRotation(-to), dt * 3f);

            flash -= dt;
            float glow = flash > 0 ? 5f : charging ? 1.5f + timer * 6f : 1.2f;
            eyeMat.SetColor("_EmissionColor", (flash > 0 ? Palette.White : Palette.Red) * glow);

            if (!firing) { timer = 0; charging = false; return; }
            timer += dt;
            if (!charging && timer > interval - 0.7f) { charging = true; Sfx.Play(Sound.Charge, AimPoint, 0.8f); }
            if (timer >= interval)
            {
                timer = 0;
                charging = false;
                Vector3 from = eye.position;
                Projectile.Spawn(from, HandInput.Head.position - from, 3.4f, false, Palette.Red, 0.08f, transform).catchable = true;
                Sfx.Play(Sound.Fire, from, 0.9f);
                Feel.Flash(from, Palette.Red, 3f, 3f, 0.15f);
            }
        }

        public void Damage(int amount, Vector3 point, Vector3 dir)
        {
            if (!Alive) return;
            hp -= amount;
            flash = 0.15f;
            Sfx.Play(Sound.Hit, point, 1f);
            Build.Burst(point, Palette.Yellow, 25, 2.5f);
            if (hp <= 0) Die();
        }

        public void Push(Vector3 impulse, bool heavy) => Damage(1, AimPoint, impulse.normalized);

        void Die()
        {
            Targets.All.Remove(this);
            Impact.Spawn(AimPoint, Vector3.up, Palette.Red, ImpactKind.Death);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                r.transform.SetParent(null, true);
                var rb = r.gameObject.AddComponent<Rigidbody>();
                if (r.GetComponent<Collider>() == null) r.gameObject.AddComponent<BoxCollider>();
                rb.AddForce((Random.insideUnitSphere + Vector3.up) * 4f, ForceMode.VelocityChange);
                Destroy(r.gameObject, 3.5f);
            }
            OnDestroyed?.Invoke();
            Destroy(gameObject, 0.1f);
        }
    }
}
