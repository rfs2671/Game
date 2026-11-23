using UnityEngine;

namespace Resonance
{
    /// <summary>Manually stepped projectile. Enemy shots are caught by Rock Shields and hurt the player's head;
    /// player shards damage anything implementing IDamageable.</summary>
    public class Projectile : MonoBehaviour
    {
        public static System.Action OnBlocked;
        public static readonly System.Collections.Generic.List<Projectile> All = new System.Collections.Generic.List<Projectile>();
        public bool catchable;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Pinch an incoming rock out of the air: it becomes a throwable you can send back.</summary>
        public static Throwable TryCatch(Vector3 pinch, Vector3 aim)
        {
            foreach (var p in All)
            {
                if (p == null || p.fromPlayer || !p.catchable) continue;
                Vector3 to = p.transform.position - pinch;
                if (to.magnitude > 2.2f || Vector3.Angle(aim, to) > 30f) continue;
                var t = Throwable.Create(p.transform.position, p.radius * 2.6f);
                Impact.Sparks(p.transform.position, -to.normalized, Palette.Blue, 40, 3f);
                Sfx.Play(Sound.Grab, p.transform.position, 1f, 0.8f);
                Feel.HitStop(0.05f);
                Destroy(p.gameObject);
                return t;
            }
            return null;
        }

        public bool fromPlayer;
        public float radius = 0.07f, gravity;
        public int damage = 1;
        public ITarget homing;
        public Transform owner;
        Vector3 vel;
        float life = 7f;

        public static Projectile Spawn(Vector3 pos, Vector3 dir, float speed, bool fromPlayer, Color glow, float size,
            Transform owner = null, float gravity = 0f)
        {
            var go = Build.Shape(fromPlayer ? "Shard" : "EnemyShot", Meshes.Rock(Random.Range(0, 10)),
                Mats.Lit(Palette.StoneLight, glow * 1.2f), null, pos, Vector3.one * size * 2f, Random.rotation);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Mats.Additive(Color.white);
            trail.startColor = glow;
            trail.endColor = new Color(glow.r, glow.g, glow.b, 0);
            trail.time = 0.25f;
            trail.startWidth = size * 1.5f;
            trail.endWidth = 0;
            var p = go.AddComponent<Projectile>();
            p.fromPlayer = fromPlayer;
            p.vel = dir.normalized * speed;
            p.radius = size;
            p.owner = owner;
            p.gravity = gravity;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life < 0) { Destroy(gameObject); return; }

            if (homing != null && homing.Alive && fromPlayer)
            {
                Vector3 want = (homing.AimPoint - transform.position).normalized * vel.magnitude;
                vel = Vector3.RotateTowards(vel, want, 3f * dt, 0);
            }
            vel += Vector3.down * gravity * dt;
            Vector3 step = vel * dt;
            Vector3 pos = transform.position;

            if (!fromPlayer)
            {
                for (int k = 1; k <= 3; k++)
                {
                    Vector3 p = pos + step * (k / 3f);
                    foreach (var h in HandPowers.All)
                        if (h.TryBlock(p, radius)) { Destroy(gameObject); return; }
                    if (Vector3.Distance(p, HandInput.Head.position) < 0.2f + radius)
                    {
                        Player.I.Damage(damage, p);
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            if (Physics.SphereCast(pos, radius, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner == null || !hit.collider.transform.IsChildOf(owner))
                {
                    if (fromPlayer)
                    {
                        var d = hit.collider.GetComponentInParent<IDamageable>();
                        d?.Damage(damage, hit.point, vel.normalized);
                    }
                    Impact.Spawn(hit.point, hit.normal, fromPlayer ? Palette.Yellow : Palette.Red, fromPlayer ? ImpactKind.Stone : ImpactKind.Energy, 0.8f);
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position = pos + step;
            transform.Rotate(400f * dt, 250f * dt, 0);
        }
    }
}
