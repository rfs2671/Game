using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    public enum CreatureKind { Walker, Charger, Thrower, Lurker }

    /// <summary>
    /// Rock creature assembled from floating stone with a glowing core.
    /// Walker: closes in and swipes (push interrupts the wind-up).
    /// Charger: armoured; push it during its charge to stagger it and expose the core.
    /// Thrower: keeps distance and lobs rocks; block with Rock Shield and return fire.
    /// Encounters stay 2-5 m away so they fit the glasses' narrow field of view.
    /// </summary>
    public class Creature : MonoBehaviour, IDamageable, IPushable, ITarget
    {
        enum State { Assembling, Moving, Windup, Charging, Recover, Stagger, Dead }

        public CreatureKind kind;
        /// <summary>Lurkers are only visible inside the hand light's reach.</summary>
        public bool lightOnly;
        /// <summary>Attack this instead of the player (e.g. the unstable machine).</summary>
        public Transform targetOverride;
        public IDamageable targetDamage;
        Renderer[] rends;
        bool visible = true;
        public System.Action<Creature> OnDied;
        public bool Alive => state != State.Dead;
        public Vector3 AimPoint => core != null ? core.position : transform.position + Vector3.up;
        public bool Exposed => exposedTimer > 0;

        class Part { public Transform t; public Vector3 home; public Vector3 start; }

        State state;
        float stateTime, attackCooldown, exposedTimer, flashTimer, strafeDir = 1f;
        int hp;
        Vector3 knock, chargeDir;
        Transform core, body, armL, armR, legL, legR, carried;
        Material coreMat;
        Color coreColor;
        readonly List<Part> parts = new List<Part>();
        float scale = 0.85f;

        public static Creature Spawn(CreatureKind kind, Vector3 pos, Color core)
        {
            var go = new GameObject("Creature_" + kind);
            go.transform.position = pos;
            var c = go.AddComponent<Creature>();
            c.kind = kind;
            c.coreColor = core;
            c.BuildBody();
            Targets.All.Add(c);
            return c;
        }

        void BuildBody()
        {
            hp = kind == CreatureKind.Charger ? 4 : kind == CreatureKind.Lurker ? 2 : 3;
            if (kind == CreatureKind.Lurker) { lightOnly = true; scale = 0.7f; }
            transform.localScale = Vector3.one * scale;
            var stone = Mats.Stone(Palette.Stone, Palette.Blue * 0.04f);
            var crack = Mats.Glow(coreColor * 0.9f);
            coreMat = new Material(Mats.Lit(coreColor, coreColor * 2f));

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            AddPart(Build.Shape("Hips", Meshes.Rock(1), stone, body, new Vector3(0, 0.95f, 0), new Vector3(0.38f, 0.2f, 0.24f)));
            AddPart(Build.Shape("Torso", Meshes.Rock(2), stone, body, new Vector3(0, 1.28f, 0), new Vector3(0.5f, 0.45f, 0.3f)));
            AddPart(Build.Shape("Head", Meshes.Rock(4), stone, body, new Vector3(0, 1.68f, 0.02f), new Vector3(0.22f, 0.24f, 0.22f)));
            if (kind == CreatureKind.Charger)
                AddPart(Build.Shape("Armour", Meshes.Rock(6), stone, body, new Vector3(0, 1.33f, 0.18f), new Vector3(0.62f, 0.5f, 0.14f)));
            AddPart(Build.Box("Crack1", crack, body, new Vector3(0.08f, 1.36f, -0.16f), new Vector3(0.03f, 0.3f, 0.02f), false, Quaternion.Euler(0, 0, 20)));
            AddPart(Build.Box("Crack2", crack, body, new Vector3(-0.1f, 1.2f, -0.16f), new Vector3(0.02f, 0.22f, 0.02f), false, Quaternion.Euler(0, 0, -30)));
            var coreGo = Build.Shape("Core", Meshes.Sphere, coreMat, body, new Vector3(0, 1.3f, -0.17f), Vector3.one * 0.13f);
            core = coreGo.transform;
            AddPart(coreGo);

            armL = Limb("ArmL", new Vector3(-0.33f, 1.45f, 0), 0.55f, 0.11f, stone);
            armR = Limb("ArmR", new Vector3(0.33f, 1.45f, 0), 0.55f, 0.11f, stone);
            legL = Limb("LegL", new Vector3(-0.13f, 0.88f, 0), 0.82f, 0.13f, stone);
            legR = Limb("LegR", new Vector3(0.13f, 0.88f, 0), 0.82f, 0.13f, stone);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.95f, 0);
            col.height = 1.9f;
            col.radius = 0.32f;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            foreach (var p in parts)
            {
                p.start = transform.position + Random.insideUnitSphere * 1.6f;
                p.start.y = transform.position.y + Random.Range(0f, 0.3f);
            }
            state = State.Assembling;
            Sfx.Play(Sound.Assemble, transform.position, 1f);
            rends = GetComponentsInChildren<Renderer>();
        }

        Transform Limb(string name, Vector3 pivot, float length, float thick, Material m)
        {
            var piv = new GameObject(name).transform;
            piv.SetParent(body, false);
            piv.localPosition = pivot;
            AddPart(Build.Shape(name + "Upper", Meshes.Rock(name.Length + 3), m, piv, new Vector3(0, -length * 0.27f, 0), new Vector3(thick, length * 0.5f, thick)));
            AddPart(Build.Shape(name + "Lower", Meshes.Rock(name.Length + 5), m, piv, new Vector3(0, -length * 0.75f, 0), new Vector3(thick * 0.9f, length * 0.48f, thick * 0.9f)));
            return piv;
        }

        void AddPart(GameObject g) => parts.Add(new Part { t = g.transform, home = g.transform.localPosition });

        void Update()
        {
            if (state == State.Dead || !Arena.Placed) return;
            float dt = Time.deltaTime;
            stateTime += dt;
            attackCooldown -= dt;
            exposedTimer -= dt;
            flashTimer -= dt;

            Vector3 feet = targetOverride != null ? targetOverride.position : Arena.Feet;
            Vector3 to = Vec.Flat(feet - transform.position);
            float dist = to.magnitude;
            Vector3 dirToPlayer = dist > 0.01f ? to / dist : transform.forward;

            switch (state)
            {
                case State.Assembling: Assemble(); break;
                case State.Moving: Move(dirToPlayer, dist, dt); break;
                case State.Windup: Windup(dirToPlayer, dist); break;
                case State.Charging: Charge(dist, dt); break;
                case State.Recover: if (stateTime > 1.1f) Go(State.Moving); break;
                case State.Stagger: if (stateTime > (Exposed ? 2.4f : 0.8f)) Go(State.Moving); break;
            }

            // Knockback, ground clamp and arena limits (they can enter the room but not stand on the player).
            transform.position += knock * dt;
            knock = Vector3.Lerp(knock, Vector3.zero, dt * 3.5f);
            Vector3 local = Arena.Local(transform.position);
            local.x = Mathf.Clamp(local.x, -5f, 5f);
            local.z = Mathf.Clamp(local.z, -0.5f, 12f);
            local.y = 0;
            transform.position = Arena.Root.TransformPoint(local);
            if (state != State.Charging && state != State.Assembling && dist > 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToPlayer), dt * 5f);

            Animate(dt);
            if (lightOnly)
            {
                bool lit = (HandLight.On && Vector3.Distance(HandLight.Position, AimPoint) < 3.2f) || flashTimer > 0;
                if (lit != visible) { visible = lit; foreach (var r in rends) if (r) r.enabled = lit || r.transform == core; }
            }
            float glow = flashTimer > 0 ? 4f : Exposed ? 3f + Mathf.Sin(Time.time * 20f) : state == State.Windup ? 2f + stateTime * 3f : 1.4f;
            Color cc = flashTimer > 0 ? Palette.White : coreColor;
            coreMat.SetColor("_EmissionColor", cc * glow);
            coreMat.SetColor("_BaseColor", cc);
            coreMat.SetColor("_Color", cc);
        }

        void Go(State s) { state = s; stateTime = 0; }

        void Assemble()
        {
            float k = Mathf.Clamp01(stateTime / 1.8f);
            foreach (var p in parts)
            {
                float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.3f - Random.Range(0f, 0.02f)));
                Vector3 goal = p.t.parent.TransformPoint(p.home);
                Vector3 pos = Vector3.Lerp(p.start, goal, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.5f;
                p.t.position = pos;
            }
            if (k >= 1f)
            {
                foreach (var p in parts) p.t.localPosition = p.home;
                Build.Burst(AimPoint, coreColor, 40, 2f);
                Go(State.Moving);
                attackCooldown = 1f;
            }
        }

        float DesiredRange => kind == CreatureKind.Walker || kind == CreatureKind.Lurker ? 1.6f : kind == CreatureKind.Charger ? 3.6f : 4.5f;

        void Move(Vector3 dir, float dist, float dt)
        {
            float speed = kind == CreatureKind.Charger ? 1.1f : 0.8f;
            if (kind == CreatureKind.Thrower)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir) * strafeDir;
                Vector3 v = side * 0.5f + dir * Mathf.Clamp(dist - DesiredRange, -1f, 1f) * 0.8f;
                transform.position += v * dt;
                if (Random.value < dt * 0.3f) strafeDir = -strafeDir;
            }
            else if (dist > DesiredRange) transform.position += dir * speed * dt;

            if (attackCooldown <= 0 && dist < DesiredRange + 0.6f) Go(State.Windup);
        }

        void Windup(Vector3 dir, float dist)
        {
            float time = kind == CreatureKind.Charger ? 1.1f : kind == CreatureKind.Thrower ? 0.8f : 0.9f;
            if (stateTime < 0.02f) Sfx.Play(Sound.Growl, AimPoint, 0.9f);
            if (kind == CreatureKind.Thrower && carried == null)
            {
                carried = Build.Shape("Carried", Meshes.Rock(7), Mats.Lit(Palette.StoneLight, Palette.Red * 0.8f), transform,
                    new Vector3(0, 2.15f, 0), Vector3.one * 0.25f).transform;
            }
            if (stateTime < time) return;

            switch (kind)
            {
                case CreatureKind.Walker:
                case CreatureKind.Lurker:
                    if (dist < 2.1f)
                    {
                        if (targetDamage != null) targetDamage.Damage(8, AimPoint, transform.forward);
                        else Player.I.Damage(1, AimPoint);
                    }
                    Sfx.Play(Sound.Thump, AimPoint, 0.8f);
                    attackCooldown = 2.2f;
                    Go(State.Recover);
                    break;
                case CreatureKind.Charger:
                    chargeDir = dir;
                    Go(State.Charging);
                    break;
                case CreatureKind.Thrower:
                    Vector3 from = carried != null ? carried.position : AimPoint + Vector3.up * 0.6f;
                    if (carried != null) Destroy(carried.gameObject);
                    carried = null;
                    // Lead the lob so gravity drops it onto the player's head.
                    Vector3 target = HandInput.Head.position;
                    float flight = Vector3.Distance(from, target) / 4.2f;
                    Vector3 aim = target + Vector3.up * (0.5f * 0.6f * flight * flight) - from;
                    Projectile.Spawn(from, aim, 4.2f, false, Palette.Red, 0.09f, transform, 0.6f).catchable = true;
                    attackCooldown = 3.2f;
                    Go(State.Recover);
                    break;
            }
        }

        void Charge(float dist, float dt)
        {
            transform.position += chargeDir * 4.5f * dt;
            transform.rotation = Quaternion.LookRotation(chargeDir);
            if (dist < 0.75f)
            {
                if (targetDamage != null) targetDamage.Damage(15, AimPoint, chargeDir);
                else Player.I.Damage(1, AimPoint);
                knock = -chargeDir * 3f;
                attackCooldown = 2.5f;
                Go(State.Recover);
            }
            else if (stateTime > 1.2f)
            {
                attackCooldown = 2f;
                Go(State.Recover);
            }
        }

        void Animate(float dt)
        {
            if (state == State.Assembling) return;
            float t = Time.time * (state == State.Charging ? 14f : 7f);
            bool moving = state == State.Moving || state == State.Charging;
            float swing = moving ? 28f : 4f;
            legL.localRotation = Quaternion.Euler(Mathf.Sin(t) * swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * swing, 0, 0);
            float armRaise = state == State.Windup ? -Mathf.Lerp(0, 150f, stateTime * 1.5f) : -Mathf.Sin(t) * swing * 0.7f;
            armL.localRotation = Quaternion.Euler(armRaise, 0, -8f);
            armR.localRotation = Quaternion.Euler(state == State.Windup ? armRaise : Mathf.Sin(t) * swing * 0.7f, 0, 8f);
            float lean = state == State.Charging ? 25f : state == State.Stagger ? -20f : state == State.Windup && kind == CreatureKind.Charger ? 15f : 0f;
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(lean, 0, 0), dt * 8f);
            body.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(t)) * 0.04f : Mathf.Sin(Time.time * 2f) * 0.015f, 0);
        }

        public void Push(Vector3 impulse, bool heavy)
        {
            if (state == State.Dead || state == State.Assembling) return;
            knock += Vec.Flat(impulse) * (heavy ? 0.9f : 0.6f);
            bool interrupt = state == State.Windup || state == State.Charging;
            if (kind == CreatureKind.Charger && interrupt)
            {
                exposedTimer = 3f;
                Build.Burst(AimPoint, coreColor, 50, 3f);
            }
            if (carried != null) { Destroy(carried.gameObject); carried = null; }
            Go(State.Stagger);
            Impact.Sparks(AimPoint, impulse.normalized, Palette.Blue, 40, 4f);
            attackCooldown = Mathf.Max(attackCooldown, 1.5f);
            Damage(heavy ? 2 : 1, AimPoint, impulse.normalized, true);
        }

        public void Damage(int amount, Vector3 point, Vector3 dir) => Damage(amount, point, dir, false);

        void Damage(int amount, Vector3 point, Vector3 dir, bool fromPush)
        {
            if (state == State.Dead || state == State.Assembling) return;
            if (kind == CreatureKind.Charger && !fromPush) amount = Exposed ? amount + 1 : Mathf.Max(0, amount - 1);
            flashTimer = 0.12f;
            knock += Vec.Flat(dir) * 1.2f;
            Sfx.Play(Sound.Hit, point, 1f);
            if (amount <= 0)
            {
                Build.Burst(point, Palette.White, 10, 1.5f); // armour deflect
                return;
            }
            hp -= amount;
            if (hp <= 0) Die(dir);
        }

        void Die(Vector3 dir)
        {
            Go(State.Dead);
            Targets.All.Remove(this);
            GetComponent<Collider>().enabled = false;
            Impact.Spawn(AimPoint, -dir, coreColor, ImpactKind.Death);
            Vortex.Spawn(AimPoint, -dir, coreColor, 0.8f, 0.9f, 30);
            foreach (var p in parts)
            {
                if (p.t == null) continue;
                p.t.SetParent(null, true);
                var rb = p.t.gameObject.AddComponent<Rigidbody>();
                p.t.gameObject.AddComponent<BoxCollider>();
                rb.AddForce((Random.insideUnitSphere + dir + Vector3.up) * 3f, ForceMode.VelocityChange);
                Destroy(p.t.gameObject, Random.Range(2.5f, 4f));
            }
            if (carried != null) Destroy(carried.gameObject);
            OnDied?.Invoke(this);
            Destroy(gameObject, 0.1f);
        }
    }
}
