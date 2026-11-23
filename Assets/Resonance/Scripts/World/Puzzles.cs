using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>Large virtual button pressed by reaching out (fingertip or palm) — reacts to proximity, no fake resistance.</summary>
    public class PressButton : MonoBehaviour
    {
        public System.Action<PressButton> OnPressed;
        public int id;
        public bool latched;
        Transform cap;
        Material idle, lit, hint;
        float cooldown;
        bool hinting;

        public static PressButton Create(Transform parent, Vector3 local, Quaternion rot, int id, Color c, string label = null)
        {
            var go = new GameObject("Button" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localRotation = rot;
            var b = go.AddComponent<PressButton>();
            b.id = id;
            b.idle = Mats.Lit(new Color(0.25f, 0.27f, 0.3f), c * 0.15f, 0.5f);
            b.lit = Mats.Lit(c, c * 2f, 0.5f);
            b.hint = Mats.Lit(c * 0.6f, c * 0.8f, 0.5f);
            Build.Box("Bezel", Mats.Lit(new Color(0.15f, 0.15f, 0.17f), Color.black, 0.4f), go.transform, new Vector3(0, 0, 0.02f), new Vector3(0.16f, 0.16f, 0.03f));
            b.cap = Build.Shape("Cap", Meshes.Cylinder, b.idle, go.transform, new Vector3(0, 0, -0.01f), new Vector3(0.11f, 0.015f, 0.11f), Quaternion.Euler(90, 0, 0)).transform;
            if (label != null)
            {
                var tm = new GameObject("Label").AddComponent<TextMesh>();
                tm.transform.SetParent(go.transform, false);
                tm.transform.localPosition = new Vector3(0, 0.12f, -0.01f);
                tm.text = label; tm.fontSize = 64; tm.characterSize = 0.006f; tm.anchor = TextAnchor.MiddleCenter; tm.color = c;
#if UNITY_2022_2_OR_NEWER
                tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                tm.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                if (tm.font) tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            }
            return b;
        }

        public void SetHint(bool on) { hinting = on; if (!latched) cap.GetComponent<MeshRenderer>().sharedMaterial = on ? hint : idle; }
        public void Reset() { latched = false; cap.GetComponent<MeshRenderer>().sharedMaterial = hinting ? hint : idle; }

        void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0 || HandInput.I == null || Menu.Blocking) return;
            Vector3 p = cap.position;
            foreach (var h in new[] { HandInput.Left, HandInput.Right })
            {
                if (!h.tracked) continue;
                bool press = Vector3.Distance(h.indexTip, p) < 0.07f || Vector3.Distance(h.palm, p) < 0.08f;
                if (h.simulated && h.pinchDown && Vector3.Angle(h.aimDir, p - h.aimOrigin) < 5f && Vector3.Distance(h.aimOrigin, p) < 2.5f) press = true;
                if (!press) continue;
                cooldown = 0.6f;
                latched = true;
                cap.GetComponent<MeshRenderer>().sharedMaterial = lit;
                cap.localPosition = new Vector3(0, 0, 0.005f);
                Sfx.Play(Sound.Button, p, 1f);
                Impact.Sparks(p, -transform.forward, Palette.Blue, 12, 1f);
                OnPressed?.Invoke(this);
                Invoke(nameof(Raise), 0.25f);
                return;
            }
        }

        void Raise() { cap.localPosition = new Vector3(0, 0, -0.01f); }
    }

    /// <summary>Receptacle for a throwable with a matching key. Optional yaw alignment requirement.</summary>
    public class Socket : MonoBehaviour
    {
        public System.Action<Socket, Throwable> OnFilled;
        public string keyId = "";
        public float radius = 0.4f;
        public float requireYawWithin = -1f;    // degrees; <0 = any rotation
        public System.Func<bool> accept;
        public bool Filled { get; private set; }
        public Throwable Content { get; private set; }
        LineRenderer ring;

        public static Socket Create(Transform parent, Vector3 local, Quaternion rot, string key, Color c, float size = 0.3f)
        {
            var go = new GameObject("Socket_" + key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localRotation = rot;
            var s = go.AddComponent<Socket>();
            s.keyId = key;
            s.ring = Build.Line("Ring", go.transform, c, 0.01f);
            s.ring.useWorldSpace = false; s.ring.loop = true; s.ring.positionCount = 24;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                s.ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * size);
            }
            return s;
        }

        void Update()
        {
            if (Filled) return;
            bool near = false;
            foreach (var t in Throwable.All)
            {
                if (t.locked) continue;
                if (keyId != "" && t.keyId != keyId) continue;
                float d = Vector3.Distance(t.transform.position, transform.position);
                if (d < radius * 1.6f) near = true;
                if (d > radius || t.held) continue;
                if (requireYawWithin >= 0)
                {
                    float dy = Mathf.Abs(Mathf.DeltaAngle(t.transform.eulerAngles.y, transform.eulerAngles.y));
                    if (dy > requireYawWithin) continue;
                }
                if (accept != null && !accept()) continue;
                Fill(t);
                return;
            }
            float pulse = near ? 1.6f + Mathf.Sin(Time.time * 12f) * 0.5f : 0.6f + Mathf.Sin(Time.time * 3f) * 0.2f;
            ring.widthMultiplier = 0.006f * pulse;
        }

        public void Fill(Throwable t)
        {
            Filled = true; Content = t;
            t.Lock(transform.position, transform.rotation);
            t.transform.SetParent(transform, true);   // travels with its section
            ring.startColor = ring.endColor = Palette.Yellow;
            Impact.Spawn(transform.position, -transform.forward, Palette.Yellow, ImpactKind.Energy, 0.8f);
            Sfx.Play(Sound.Insert, transform.position, 1f);
            OnFilled?.Invoke(this, t);
        }
    }

    /// <summary>Energy beam that reflects off BeamMirror pillars until it reaches the receiver.</summary>
    public class BeamEmitter : MonoBehaviour
    {
        public Transform receiver;
        public bool Powered { get; private set; }
        public System.Action OnPowered;
        LineRenderer lr;
        readonly List<Vector3> pts = new List<Vector3>();

        void Start()
        {
            lr = Build.Line("Beam", transform, new Color(0.4f, 0.9f, 1f), 0.03f);
            lr.positionCount = 0;
        }

        void Update()
        {
            pts.Clear();
            Vector3 o = transform.position, d = transform.forward;
            pts.Add(o);
            bool hitReceiver = false;
            for (int bounce = 0; bounce < 6; bounce++)
            {
                if (!Physics.Raycast(o + d * 0.05f, d, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)) { pts.Add(o + d * 30f); break; }
                pts.Add(hit.point);
                if (receiver != null && hit.collider.transform.IsChildOf(receiver)) { hitReceiver = true; break; }
                var m = hit.collider.GetComponentInParent<BeamMirror>();
                if (m == null) break;
                Vector3 n = m.Normal;
                if (Vector3.Dot(n, d) > 0) n = -n;
                d = Vector3.Reflect(d, n);
                o = hit.point;
            }
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.widthMultiplier = 0.025f + Mathf.Sin(Time.time * 30f) * 0.004f;
            if (hitReceiver && !Powered)
            {
                Powered = true;
                Impact.Spawn(pts[pts.Count - 1], -d, new Color(0.4f, 0.9f, 1f), ImpactKind.Energy, 2f);
                Sfx.Play(Sound.Unlock, receiver.position, 1f);
                OnPowered?.Invoke();
            }
        }
    }

    /// <summary>Pillar with a mirrored face. Grab it with telekinesis and twist to aim the beam; snaps to 22.5°.</summary>
    public class BeamMirror : MonoBehaviour
    {
        public Vector3 Normal => transform.forward;
        Throwable t;
        void Start()
        {
            t = GetComponent<Throwable>();
            if (t != null) t.OnReleased += _ =>
            {
                float y = Mathf.Round(transform.localEulerAngles.y / 22.5f) * 22.5f;
                Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                t.rb.MoveRotation(parent * Quaternion.Euler(0, y, 0));
                Sfx.Play(Sound.Thump, transform.position, 0.5f, 1.4f);
            };
        }
    }

    /// <summary>Renderer only shows when the player stands in a given part of the play area (hidden symbols).</summary>
    public class ViewReveal : MonoBehaviour
    {
        public float minX = -9f, maxX = 9f, minZ = -9f, maxZ = 9f;
        Renderer[] rends; bool shown = true;
        void Start() { rends = GetComponentsInChildren<Renderer>(); }
        void Update()
        {
            if (!Arena.Placed) return;
            var h = Arena.HeadLocal;
            bool v = h.x >= minX && h.x <= maxX && h.z >= minZ && h.z <= maxZ;
            if (v == shown) return;
            shown = v;
            foreach (var r in rends) if (r) r.enabled = v;
        }
    }

    /// <summary>Little maintenance drone that waits for a bridge, then crosses it.</summary>
    public class Drone : MonoBehaviour
    {
        public Vector3[] path;  // world points
        public bool go;
        public bool Arrived { get; private set; }
        int i;
        void Update()
        {
            transform.Rotate(0, 90f * Time.deltaTime, 0);
            if (!go || path == null || Arrived) { transform.position += Vector3.up * Mathf.Sin(Time.time * 3f) * 0.002f; return; }
            transform.position = Vector3.MoveTowards(transform.position, path[i], Time.deltaTime * 0.7f);
            if (Vector3.Distance(transform.position, path[i]) < 0.02f && ++i >= path.Length) { Arrived = true; Sfx.Play(Sound.Chime, transform.position, 1f); }
        }

        public static Drone Spawn(Vector3 pos)
        {
            var go = new GameObject("Drone");
            go.transform.position = pos;
            Build.Box("Body", Mats.Lit(new Color(0.4f, 0.42f, 0.45f), Color.black, 0.6f), go.transform, Vector3.zero, new Vector3(0.22f, 0.12f, 0.22f));
            Build.Shape("Eye", Meshes.Sphere, Mats.Glow(new Color(0.4f, 0.9f, 1f) * 1.5f), go.transform, new Vector3(0, 0, -0.12f), Vector3.one * 0.06f);
            for (int k = 0; k < 4; k++)
                Build.Box("Rotor", Mats.Lit(new Color(0.2f, 0.2f, 0.22f), Color.black, 0.5f), go.transform,
                    new Vector3(k % 2 == 0 ? -0.14f : 0.14f, 0.07f, k < 2 ? -0.14f : 0.14f), new Vector3(0.12f, 0.01f, 0.02f));
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.4f, 0.9f, 1f); l.range = 1.5f; l.intensity = 1.5f;
            return go.AddComponent<Drone>();
        }
    }

    /// <summary>Unstable machine the player keeps alive while creatures attack it.</summary>
    public class UnstableMachine : MonoBehaviour, IDamageable
    {
        public float health = 100f;
        public bool Alive => health > 0;
        Material coreMat; Transform core;

        public static UnstableMachine Spawn(Transform parent, Vector3 local)
        {
            var go = new GameObject("UnstableMachine");
            go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var m = go.AddComponent<UnstableMachine>();
            var steel = Mats.Lit(new Color(0.38f, 0.39f, 0.41f), Color.black, 0.5f);
            Build.Box("Base", steel, go.transform, new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1f, 1.2f), true);
            for (int i = 0; i < 4; i++)
                Build.Box("Coil", steel, go.transform, new Vector3(i % 2 == 0 ? -0.45f : 0.45f, 1.4f, i < 2 ? -0.45f : 0.45f), new Vector3(0.12f, 0.8f, 0.12f), true);
            m.coreMat = new Material(Mats.Lit(new Color(1f, 0.5f, 0.2f), new Color(1f, 0.4f, 0.15f) * 2f));
            m.core = Build.Shape("Core", Meshes.Sphere, m.coreMat, go.transform, new Vector3(0, 1.45f, 0), Vector3.one * 0.35f).transform;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.2f); l.range = 4f; l.intensity = 2f;
            return m;
        }

        public void Damage(int amount, Vector3 point, Vector3 dir)
        {
            if (!Alive) return;
            health = Mathf.Max(0, health - amount);
            Impact.Spawn(point, -dir, new Color(1f, 0.5f, 0.2f), ImpactKind.Energy, 1f);
        }

        void Update()
        {
            float k = 1f - health / 100f;
            core.localScale = Vector3.one * (0.35f + Mathf.Sin(Time.time * (6f + k * 20f)) * 0.03f * (1 + k * 3));
            coreMat.SetColor("_EmissionColor", Color.Lerp(new Color(1f, 0.45f, 0.15f), Palette.Red, k) * (2f + k * 3f));
            if (Random.value < Time.deltaTime * (0.5f + k * 4f)) Impact.Sparks(core.position, Random.onUnitSphere, new Color(1f, 0.6f, 0.3f), 10, 2f);
        }
    }

    /// <summary>Removes everything a level spawned, keeping the arena, floor and current environment.</summary>
    public static class Cleanup
    {
        public static void Level(Env keep = null)
        {
            foreach (var t in Throwable.All.ToArray()) if (t) Object.Destroy(t.gameObject);
            foreach (var p in Projectile.All.ToArray()) if (p) Object.Destroy(p.gameObject);
            foreach (var c in Find.All<Creature>()) Object.Destroy(c.gameObject);
            foreach (var l in Find.All<Launcher>()) Object.Destroy(l.gameObject);
            foreach (var d in Find.All<StoneDoor>()) Object.Destroy(d.gameObject);
            foreach (var c in Find.All<Crystal>()) Object.Destroy(c.gameObject);
            if (!Arena.Placed) return;
            foreach (Transform child in Arena.Root)
            {
                if (child.name == "FloorCollider" || child.name == "PlayBorder") continue;
                var env = child.GetComponent<Env>();
                if (env != null) continue;
                Object.Destroy(child.gameObject);
            }
        }
    }
}
