using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Procedural quarry built beyond the front edge of the play area: rising basalt columns with
    /// yellow fault-lines, blue mist, floodlights, a floating rock ring and a cable car.
    /// Also owns the opening "fracture" of the room and the end-of-level collapse.
    /// </summary>
    public class Quarry : Env
    {
        public static Quarry I { get; private set; }
        protected override string AmbienceClip => "amb_quarry";
        protected override AudioReverbPreset ReverbPreset => AudioReverbPreset.Quarry;
        protected override void BuildEnv() => BuildQuarry();

        class Column { public Transform t; public float height, delay, yaw; public Vector3 home; public float tilt; }

        readonly List<Column> columns = new List<Column>();
        readonly List<LineRenderer> cracks = new List<LineRenderer>();
        Transform ring, car;
        ParticleSystem mist;
        float revealT = -1f, collapseT = -1f;

        public static Quarry Create(Transform arena)
        {
            var q = Create<Quarry>("Quarry");
            I = q;
            return q;
        }

        // ---------------------------------------------------------------- fracture reveal
        /// <summary>Cracks spread across a virtual wall at the front of the room, it shatters, the quarry rises.</summary>
        public void Reveal()
        {
            Construct();
            revealT = 0f;
            for (int i = 0; i < 9; i++) cracks.Add(Crack(new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.9f), 1.25f), i));
            Sfx.Play(Sound.Rumble, Arena.W(0, 1.5f, 1.3f), 1f);
        }

        LineRenderer Crack(Vector3 start, int seed)
        {
            var lr = Build.Line("Crack", root, seed % 3 == 0 ? Palette.Blue : Palette.Yellow, 0.012f);
            lr.useWorldSpace = false;
            var pts = new List<Vector3> { start };
            Vector3 dir = Quaternion.Euler(0, 0, Random.Range(0, 360f)) * Vector3.right;
            Vector3 p = start;
            for (int i = 0; i < 14; i++)
            {
                dir = Quaternion.Euler(0, 0, Random.Range(-40f, 40f)) * dir;
                p += dir * Random.Range(0.08f, 0.22f);
                p.z = 1.25f;
                p.x = Mathf.Clamp(p.x, -1.6f, 1.6f);
                p.y = Mathf.Clamp(p.y, 0.05f, 2.7f);
                pts.Add(p);
            }
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.enabled = false;
            return lr;
        }

        void Shatter()
        {
            var mat = Mats.Additive(Palette.Blue * 0.6f);
            for (int i = 0; i < 40; i++)
            {
                Vector3 lp = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.1f, 2.6f), 1.25f);
                var shard = Build.Box("WallShard", mat, null, Arena.Root.TransformPoint(lp),
                    new Vector3(Random.Range(0.08f, 0.3f), Random.Range(0.08f, 0.3f), 0.01f), false, Random.rotation);
                var rb = shard.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.AddForce(Arena.Root.TransformDirection(new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1.5f), Random.Range(1f, 3f))), ForceMode.VelocityChange);
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                Destroy(shard, 2.5f);
            }
            Build.Burst(Arena.W(0, 1.4f, 1.3f), Palette.Blue, 200, 3f, 0.05f);
            Sfx.Play(Sound.Thump, Arena.W(0, 1.4f, 1.3f), 1f);

            // Torn edge of the opening stays as a glowing jagged frame.
            var frame = Build.Line("FractureEdge", root, Palette.Blue * 0.9f, 0.015f);
            frame.useWorldSpace = false;
            frame.loop = true;
            var pts = new List<Vector3>();
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                float r = 1f + Random.Range(-0.12f, 0.12f);
                pts.Add(new Vector3(Mathf.Cos(a) * 1.75f * r, 1.35f + Mathf.Sin(a) * 1.45f * r, 1.3f));
            }
            frame.positionCount = pts.Count;
            frame.SetPositions(pts.ToArray());
            foreach (var c in cracks) Destroy(c.gameObject);
            cracks.Clear();
        }

        // ---------------------------------------------------------------- environment
        void BuildQuarry()
        {
            var stone = Mats.Stone(Palette.Stone, Palette.Blue * 0.05f);
            var stoneB = Mats.Stone(Palette.StoneLight * 0.8f, Palette.Blue * 0.08f);
            var crack = Mats.Glow(Palette.Yellow);
            var rng = new System.Random(3);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            for (int i = 0; i < 34; i++)
            {
                float z = R(3.5f, 20f);
                float x = R(-10f, 10f);
                if (Mathf.Abs(x) < 2.4f && z < 11f) x = Mathf.Sign(x == 0 ? 1 : x) * R(2.4f, 6f);
                float h = R(2.5f, 11f) * (z > 12 ? 1.3f : 1f);
                float w = R(0.9f, 2.6f), d = R(0.9f, 2.4f);
                float yaw = R(-20, 20);
                var col = Build.Shape("Column", Meshes.Basalt, i % 3 == 0 ? stoneB : stone, root, new Vector3(x, -h * 0.5f, z),
                    new Vector3(w, h, d), Quaternion.Euler(0, yaw, 0), true);
                if (i % 3 == 0)
                    Build.Box("Fault", crack, col.transform, new Vector3(R(-0.3f, 0.3f), R(-0.3f, 0.1f), -0.51f),
                        new Vector3(0.03f, R(0.15f, 0.4f), 0.02f), false, Quaternion.Euler(0, 0, R(-15, 15)));
                if (i % 5 == 0)
                    Build.Box("FaultH", crack, col.transform, new Vector3(0, R(-0.2f, 0.3f), -0.51f),
                        new Vector3(R(0.3f, 0.8f), 0.012f, 0.02f), false, Quaternion.Euler(0, 0, R(-8, 8)));
                columns.Add(new Column { t = col.transform, height = h, yaw = yaw, delay = z * 0.08f + R(0, 0.4f), home = new Vector3(x, h * 0.5f, z) });
            }

            // Low slabs and rubble on the quarry floor.
            for (int i = 0; i < 18; i++)
            {
                float x = R(-6f, 6f), z = R(2f, 12f);
                if (Mathf.Abs(x) < 1.6f && z < 7f) continue;
                Build.Shape("Rubble", Meshes.Rock(i), stone, root, new Vector3(x, 0.15f, z), new Vector3(R(0.3f, 1.2f), R(0.2f, 0.5f), R(0.3f, 1.2f)),
                    Quaternion.Euler(0, R(0, 360), 0), true);
            }

            // Floor guide lines so the ground reads as extending past the room.
            for (int i = -4; i <= 4; i++)
            {
                var l = Build.Line("FloorLine", root, Palette.Blue * 0.12f, 0.006f);
                l.useWorldSpace = false;
                l.SetPositions(new[] { new Vector3(i * 1.5f, 0.005f, 1.4f), new Vector3(i * 2.5f, 0.005f, 20f) });
            }

            // Floodlights.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(-7f + i * 7f, 0, 17f + i % 2 * 3f);
                Build.Box("Pole", stone, root, p + new Vector3(0, 5f, 0), new Vector3(0.12f, 10f, 0.12f));
                Build.Box("Lamp", Mats.Glow(Palette.White), root, p + new Vector3(0, 10.1f, -0.1f), new Vector3(0.9f, 0.45f, 0.1f));
                var glow = Build.Particles("LampGlow", root, Palette.White * 0.5f, 2.2f, 0f, 1f, 6f, 0.1f, true);
                glow.transform.localPosition = p + new Vector3(0, 10.1f, -0.3f);
                glow.Play();
            }

            // Floating ring of rocks with a yellow fracture core, as in the concept art.
            ring = new GameObject("RockRing").transform;
            ring.SetParent(root, false);
            ring.localPosition = new Vector3(3.2f, 5.2f, 13f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var r = Build.Shape("RingRock", Meshes.Rock(i), stoneB, ring, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 2.1f,
                    new Vector3(0.5f, 0.9f, 0.4f) * R(0.7f, 1.3f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90));
                Build.Box("Glow", crack, r.transform, new Vector3(0, 0, -0.52f), new Vector3(0.5f, 0.06f, 0.02f));
            }
            var ringGlow = Build.Particles("RingGlow", ring, Palette.Yellow * 0.6f, 0.35f, 0.05f, 2f, 30f, 2f, true);
            var rs = ringGlow.shape;
            rs.shapeType = ParticleSystemShapeType.Circle;
            ringGlow.Play();

            // Cable car.
            var cable = Build.Line("Cable", root, Palette.White * 0.25f, 0.01f);
            cable.useWorldSpace = false;
            cable.SetPositions(new[] { new Vector3(-12f, 9f, 15f), new Vector3(12f, 8f, 11f) });
            car = Build.Box("CableCar", stoneB, root, new Vector3(0, 8.2f, 13f), new Vector3(0.6f, 0.5f, 0.5f)).transform;
            Build.Box("CarLight", Mats.Glow(Palette.White), car, new Vector3(0, 0, -0.51f), new Vector3(0.4f, 0.15f, 0.02f));

            // Mist: wide, low and slow.
            mist = Build.Particles("Mist", root, new Color(0.25f, 0.5f, 0.8f, 0.18f), 2.5f, 0.08f, 9f, 18f, 1f, true);
            mist.transform.localPosition = new Vector3(0, 0.4f, 9f);
            var ms = mist.shape;
            ms.shapeType = ParticleSystemShapeType.Box;
            ms.scale = new Vector3(18f, 0.6f, 16f);
            var mm = mist.main;
            mm.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            mm.maxParticles = 250;
            mist.Play();

            if (Find.One<Light>() == null)
            {
                var l = new GameObject("QuarryLight").AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(0.6f, 0.75f, 1f);
                l.intensity = 0.9f;
                l.transform.rotation = Quaternion.Euler(40, 160, 0);
            }
        }

        // ---------------------------------------------------------------- collapse
        public void Collapse() { collapseT = 0f; Sfx.Play(Sound.Rumble, Arena.W(0, 2, 6), 1f); }

        void Update()
        {
            float dt = Time.deltaTime;
            if (revealT >= 0f)
            {
                revealT += dt;
                // Cracks grow, then the wall shatters at 2.2 s.
                for (int i = 0; i < cracks.Count; i++)
                {
                    var c = cracks[i];
                    if (c == null) continue;
                    float k = Mathf.Clamp01((revealT - i * 0.15f) / 1.2f);
                    c.enabled = k > 0;
                    c.widthMultiplier = 0.004f + k * 0.012f;
                }
                if (cracks.Count > 0 && revealT > 2.2f) Shatter();
                foreach (var c in columns)
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((revealT - 2.2f - c.delay) / 2.2f));
                    c.t.localPosition = new Vector3(c.home.x, Mathf.Lerp(-c.height * 0.5f - 0.1f, c.home.y, k), c.home.z);
                }
                if (revealT > 7f) revealT = -2f;
            }

            if (ring != null) ring.Rotate(0, 0, 9f * dt, Space.Self);
            if (car != null)
            {
                float k = Mathf.PingPong(Time.time * 0.02f, 1f);
                car.localPosition = Vector3.Lerp(new Vector3(-12f, 8.7f, 15f), new Vector3(12f, 7.7f, 11f), k);
            }

            if (collapseT >= 0f)
            {
                collapseT += dt;
                foreach (var c in columns)
                {
                    c.tilt = Mathf.Min(c.tilt + dt * (c.home.z < 9f ? 4f : 2f), c.home.x > 0 ? 18f : -18f);
                    c.t.localRotation = Quaternion.Euler(c.tilt * 0.3f, c.yaw, c.tilt);
                    c.t.localPosition = c.home + Random.insideUnitSphere * 0.03f + Vector3.down * collapseT * 0.08f;
                }
                if (Random.value < dt * 4f)
                {
                    var rock = Throwable.Create(Arena.W(Random.Range(-4f, 4f), 7f, Random.Range(3f, 9f)), Random.Range(0.2f, 0.45f));
                    Destroy(rock.gameObject, 6f);
                }
            }
        }
    }

    /// <summary>Colossal black stone hand that pushes out of the quarry wall during the collapse.</summary>
    public class StoneHand : MonoBehaviour
    {
        Transform[] fingers;
        float t;
        Vector3 from, to;

        public static StoneHand Spawn(Transform parent)
        {
            var go = new GameObject("StoneHand");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<StoneHand>();
            var stone = Mats.Lit(Palette.Stone * 0.8f, Palette.Blue * 0.03f);
            var crack = Mats.Glow(Palette.Yellow);
            Build.Shape("Palm", Meshes.Rock(2), stone, go.transform, Vector3.zero, new Vector3(2.4f, 2.6f, 0.8f));
            Build.Box("PalmCrack", crack, go.transform, new Vector3(0.2f, 0.1f, -0.42f), new Vector3(0.06f, 1.6f, 0.02f), false, Quaternion.Euler(0, 0, 15));
            Build.Shape("Wrist", Meshes.Rock(5), stone, go.transform, new Vector3(0, -2.2f, 0.4f), new Vector3(1.8f, 2.2f, 1.0f));
            h.fingers = new Transform[5];
            for (int i = 0; i < 5; i++)
            {
                bool thumb = i == 4;
                var piv = new GameObject("Finger" + i).transform;
                piv.SetParent(go.transform, false);
                piv.localPosition = thumb ? new Vector3(1.3f, -0.4f, 0) : new Vector3(-0.9f + i * 0.6f, 1.3f, 0);
                piv.localRotation = thumb ? Quaternion.Euler(0, 0, -50) : Quaternion.identity;
                for (int s = 0; s < 3; s++)
                    Build.Shape("Seg", Meshes.Rock(i * 3 + s), stone, piv, new Vector3(0, 0.45f + s * 0.75f, 0), new Vector3(0.45f, 0.7f, 0.45f));
                Build.Box("Knuckle", crack, piv, new Vector3(0, 0.82f, -0.24f), new Vector3(0.3f, 0.03f, 0.02f));
                h.fingers[i] = piv;
            }
            h.from = new Vector3(0, 10f, 16f);
            h.to = new Vector3(0, 4.2f, 6.5f);
            go.transform.localPosition = h.from;
            go.transform.localRotation = Quaternion.Euler(35, 180, 0);
            return h;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 6f));
            transform.localPosition = Vector3.Lerp(from, to, k) + Random.insideUnitSphere * 0.02f;
            float curl = Mathf.Lerp(0, 55, Mathf.Clamp01((t - 3f) / 5f));
            for (int i = 0; i < 4; i++) fingers[i].localRotation = Quaternion.Euler(-curl, 0, 0);
        }
    }

    /// <summary>Two walls closing the escape passage. The player holds both palms out to force them apart.</summary>
    public class Passage : MonoBehaviour
    {
        public float Progress { get; private set; }
        public bool Holding { get; private set; }
        Transform left, right;
        float gap = 1.3f;
        LineRenderer lightBeam;

        public static Passage Spawn(Transform parent)
        {
            var go = new GameObject("Passage");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0, 2.6f);
            var p = go.AddComponent<Passage>();
            var stone = Mats.Lit(Palette.StoneLight, Palette.Blue * 0.08f);
            p.left = Build.Box("WallL", stone, go.transform, Vector3.zero, new Vector3(2.4f, 3.2f, 0.5f), true).transform;
            p.right = Build.Box("WallR", stone, go.transform, Vector3.zero, new Vector3(2.4f, 3.2f, 0.5f), true).transform;
            Build.Box("EdgeL", Mats.Glow(Palette.Yellow), p.left, new Vector3(0.51f, 0, -0.51f), new Vector3(0.02f, 0.95f, 0.02f));
            Build.Box("EdgeR", Mats.Glow(Palette.Yellow), p.right, new Vector3(-0.51f, 0, -0.51f), new Vector3(0.02f, 0.95f, 0.02f));
            p.lightBeam = Build.Line("ExitLight", go.transform, Palette.White, 0.05f);
            p.lightBeam.useWorldSpace = false;
            p.lightBeam.SetPositions(new[] { new Vector3(0, 0, 0.4f), new Vector3(0, 3f, 0.4f) });
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Holding = PowerSet.BothPalmsHeld;
            if (Holding) { gap = Mathf.Min(1.4f, gap + dt * 0.6f); Progress = Mathf.Min(1f, Progress + dt / 8f); }
            else { gap = Mathf.Max(0.12f, gap - dt * 0.25f); Progress = Mathf.Max(0f, Progress - dt / 20f); }
            float shake = Holding ? 0.015f : 0.005f;
            left.localPosition = new Vector3(-(gap * 0.5f + 1.2f), 1.6f, 0) + Random.insideUnitSphere * shake;
            right.localPosition = new Vector3(gap * 0.5f + 1.2f, 1.6f, 0) + Random.insideUnitSphere * shake;
            lightBeam.widthMultiplier = Mathf.Max(0.02f, gap * 0.7f);
            Color c = Color.Lerp(Palette.Blue, Palette.White, Progress) * (0.4f + Progress * 0.8f);
            lightBeam.startColor = lightBeam.endColor = c;
        }
    }
}
