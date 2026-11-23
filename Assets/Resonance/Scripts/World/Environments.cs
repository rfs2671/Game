using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>Level 1: abandoned mining facility. A corridor running forward from the room:
    /// pipes, ribs, blue strip lights, a dark section, a gravity anomaly room and a reactor hall.</summary>
    public class Tunnels : Env
    {
        protected override string AmbienceClip => "amb_tunnel";
        protected override AudioReverbPreset ReverbPreset => AudioReverbPreset.SewerPipe;
        public readonly List<Light> stripLights = new List<Light>();
        public readonly List<MeshRenderer> stripRenderers = new List<MeshRenderer>();
        Material stripOn, stripOff;

        public const float DarkStart = 8f, DarkEnd = 13.5f, GravityStart = 14f, ReactorStart = 21f, Length = 30f;

        protected override void BuildEnv()
        {
            var rock = Mats.Stone(new Color(0.2f, 0.21f, 0.24f), Palette.Blue * 0.03f);
            var steel = Mats.Lit(new Color(0.35f, 0.36f, 0.38f), Color.black, 0.55f);
            var rust = Mats.Lit(new Color(0.35f, 0.18f, 0.09f), Color.black, 0.3f);
            stripOn = Mats.Glow(Palette.Blue * 1.3f);
            stripOff = Mats.Glow(Palette.Blue * 0.04f);
            var conc = Mats.Stone(new Color(0.28f, 0.28f, 0.29f), Color.black, 0.1f);
            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Build.Box("Floor", conc, root, new Vector3(0, -0.02f, Length * 0.5f + 1.2f), new Vector3(4.4f, 0.04f, Length), true);
            for (int i = 0; i < 80; i++)
            {
                float z = R(1.4f, Length); float side = rng.NextDouble() < 0.5 ? -1 : 1;
                Build.Shape("Wall", Meshes.Rock(i), rock, root, new Vector3(side * R(2.35f, 2.8f), R(0.1f, 3.2f), z),
                    Vector3.one * R(0.7f, 1.3f), Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)), true);
            }
            for (int i = 0; i < 40; i++)
                Build.Shape("Roof", Meshes.Rock(i + 40), rock, root, new Vector3(R(-2.1f, 2.1f), R(3.4f, 3.8f), R(1.4f, Length)),
                    Vector3.one * R(0.8f, 1.5f), Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)));
            for (int k = 0; k < 10; k++)
            {
                float z = 2f + k * 3f;
                Build.Box("RibL", steel, root, new Vector3(-1.95f, 1.6f, z), new Vector3(0.22f, 3.2f, 0.28f), true);
                Build.Box("RibR", steel, root, new Vector3(1.95f, 1.6f, z), new Vector3(0.22f, 3.2f, 0.28f), true);
                Build.Box("RibT", steel, root, new Vector3(0, 3.25f, z), new Vector3(4.1f, 0.22f, 0.28f));
                var strip = Build.Box("Strip", stripOn, root, new Vector3(0, 3.1f, z + 0.2f), new Vector3(1.2f, 0.06f, 0.05f));
                stripRenderers.Add(strip.GetComponent<MeshRenderer>());
                if (k % 2 == 0)
                {
                    var l = new GameObject("StripLight").AddComponent<Light>();
                    l.transform.SetParent(root, false);
                    l.transform.localPosition = new Vector3(0, 2.8f, z + 0.2f);
                    l.type = LightType.Point; l.color = Palette.Blue; l.range = 5f; l.intensity = 1.4f; l.shadows = LightShadows.None;
                    stripLights.Add(l);
                }
            }
            foreach (var (y, r, m) in new[] { (0.6f, 0.12f, steel), (0.95f, 0.08f, steel), (2.6f, 0.18f, rust) })
                for (int side = -1; side <= 1; side += 2)
                    Build.Shape("Pipe", Meshes.Cylinder, m, root, new Vector3(side * 1.75f, y, Length * 0.5f + 1.2f),
                        new Vector3(r * 2, Length * 0.5f, r * 2), Quaternion.Euler(90, 0, 0));
            Build.Box("Tray", steel, root, new Vector3(1.55f, 2.0f, Length * 0.5f + 1.2f), new Vector3(0.3f, 0.04f, Length));
            Build.Box("Machine", steel, root, new Vector3(-1.3f, 0.6f, 5.5f), new Vector3(0.9f, 1.2f, 1.4f), true);
            Build.Box("MachineScreen", Mats.Glow(new Color(0.2f, 0.7f, 0.9f)), root, new Vector3(-0.84f, 0.85f, 5.5f), new Vector3(0.02f, 0.35f, 0.6f));
            // gravity anomaly room: debris hanging in the air
            for (int i = 0; i < 28; i++)
            {
                var g = Build.Shape("Floating", Meshes.Rock(i + 7), rock, root, new Vector3(R(-1.8f, 1.8f), R(0.6f, 3f), R(GravityStart, ReactorStart - 1f)),
                    Vector3.one * R(0.1f, 0.4f), Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)));
                var f = g.AddComponent<Floaty>(); f.amp = 0.15f; f.speed = R(0.3f, 0.8f); f.spin = R(5, 25);
            }
            var haze = Build.Particles("Haze", root, new Color(0.35f, 0.6f, 0.9f, 0.12f), 1.6f, 0.03f, 8f, 10f, 1f, true);
            haze.transform.localPosition = new Vector3(0, 1.2f, Length * 0.5f);
            var hs = haze.shape; hs.shapeType = ParticleSystemShapeType.Box; hs.scale = new Vector3(3.6f, 2f, Length);
            haze.Play();
        }

        /// <summary>Tunnel-space point to world (sections are laid out along tunnel z).</summary>
        public Vector3 T(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
        public Transform Root => root;

        /// <summary>Slide the whole tunnel toward the player: the next section arrives while they stay in their play area.</summary>
        public System.Collections.IEnumerator Advance(float dist, float duration = 3.5f)
        {
            Sfx.Play(Sound.Rumble, Arena.W(0, 1.5f, 3f), 0.9f);
            float moved = 0;
            while (moved < dist)
            {
                float k = Mathf.Sin(Mathf.Clamp01(moved / dist) * Mathf.PI) * 0.8f + 0.2f;
                float step = Mathf.Min(dist - moved, dist / duration * 1.6f * k * Time.deltaTime);
                root.localPosition += Vector3.back * step;
                moved += step;
                yield return null;
            }
        }

        public void SetSectionDark(bool dark)
        {
            for (int i = 0; i < stripRenderers.Count; i++)
            {
                float z = root.InverseTransformPoint(stripRenderers[i].transform.position).z;
                bool inDark = dark && z > DarkStart - 1f && z < DarkEnd + 1f;
                stripRenderers[i].sharedMaterial = inDark ? stripOff : stripOn;
            }
            foreach (var l in stripLights)
            {
                float z = root.InverseTransformPoint(l.transform.position).z;
                l.enabled = !(dark && z > DarkStart - 2f && z < DarkEnd + 2f);
            }
        }
    }

    /// <summary>Level 2: research station inside the quarry. Brutalist concrete, coffered ceiling,
    /// flickering fluorescents, red emergency slots, abandoned office, floating papers, containment chamber.</summary>
    public class Station : Env
    {
        protected override string AmbienceClip => "amb_station";
        protected override AudioReverbPreset ReverbPreset => AudioReverbPreset.Hallway;
        public Transform containmentDoor;
        public Vector3 ChamberPos => Arena.W(2.6f, 0, 6.5f);

        protected override void BuildEnv()
        {
            var conc = Mats.Stone(Palette.StoneLight * 1.15f, Color.black, 0.12f);
            var concD = Mats.Stone(Palette.Stone, Color.black, 0.1f);
            var steel = Mats.Lit(new Color(0.4f, 0.41f, 0.43f), Color.black, 0.5f);
            var paper = Mats.Lit(new Color(0.85f, 0.83f, 0.78f), Color.black, 0.1f);
            var red = Mats.Glow(Palette.Red * 1.2f);
            var fluoOn = Mats.Glow(new Color(0.88f, 0.94f, 1f) * 1.4f);
            var fluoOff = Mats.Glow(new Color(0.1f, 0.1f, 0.1f));
            var rng = new System.Random(23);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Build.Box("Floor", Mats.Lit(new Color(0.12f, 0.12f, 0.13f), Color.black, 0.75f), root, new Vector3(0, -0.02f, 8f), new Vector3(11f, 0.04f, 16f), true);
            Build.Box("WallL", conc, root, new Vector3(-5.5f, 4f, 8f), new Vector3(0.4f, 8f, 16f), true);
            Build.Box("WallR", conc, root, new Vector3(5.5f, 4f, 8f), new Vector3(0.4f, 8f, 16f), true);
            Build.Box("WallB", conc, root, new Vector3(0, 4f, 16f), new Vector3(11f, 8f, 0.4f), true);
            Build.Box("Ceil", concD, root, new Vector3(0, 8.1f, 8f), new Vector3(11f, 0.3f, 16f));
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                var pos = new Vector3(-3.75f + j * 2.5f, 7.75f, 2.5f + i * 3.5f);
                Build.Box("Coffer", concD, root, pos, new Vector3(2.1f, 0.5f, 3f));
                var tube = Build.Box("Fluo", fluoOn, root, pos + Vector3.down * 0.3f, new Vector3(1.4f, 0.04f, 0.18f));
                var fl = tube.AddComponent<Flicker>(); fl.on = fluoOn; fl.off = fluoOff; fl.rate = (i + j) % 3 == 0 ? 0.35f : 0.04f;
                if ((i + j) % 2 == 0)
                {
                    var l = new GameObject("FluoLight").AddComponent<Light>();
                    l.transform.SetParent(root, false); l.transform.localPosition = pos + Vector3.down * 0.6f;
                    l.type = LightType.Point; l.color = new Color(0.85f, 0.9f, 1f); l.range = 7f; l.intensity = 1.1f; l.shadows = LightShadows.None;
                    fl.lightRef = l; fl.baseIntensity = 1.1f;
                }
            }
            for (int k = 0; k < 4; k++)
            {
                float z = 3f + k * 3.5f;
                foreach (float x in new[] { -3.6f, 3.6f }) Build.Box("Column", conc, root, new Vector3(x, 4f, z), new Vector3(0.9f, 8f, 0.9f), true);
                foreach (float x in new[] { -5.28f, 5.28f })
                {
                    Build.Box("Slot", red, root, new Vector3(x, 3.2f, z + 1.6f), new Vector3(0.05f, 2.6f, 0.25f));
                    var l = new GameObject("SlotLight").AddComponent<Light>();
                    l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(x * 0.9f, 3.2f, z + 1.6f);
                    l.type = LightType.Point; l.color = Palette.Red; l.range = 5f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                }
            }
            for (int i = 0; i < 5; i++) Build.Box("Cabinet", steel, root, new Vector3(-4.6f, 0.7f, 4f + i * 0.62f), new Vector3(0.6f, 1.4f, 0.6f), true);
            Build.Box("Desk", Mats.Lit(new Color(0.3f, 0.2f, 0.12f), Color.black, 0.4f), root, new Vector3(3.2f, 0.74f, 4f), new Vector3(1.6f, 0.05f, 0.8f), true);
            Build.Box("DeskBase", steel, root, new Vector3(3.2f, 0.36f, 4f), new Vector3(1.5f, 0.72f, 0.7f), true);
            for (int i = 0; i < 40; i++)
            {
                var p = Build.Box("Paper", paper, root, new Vector3(R(-3f, 3f), R(0.6f, 4.5f), R(2.5f, 12f)), new Vector3(0.15f, 0.21f, 0.003f), false,
                    Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)));
                var f = p.AddComponent<Floaty>(); f.amp = 0.2f; f.speed = R(0.2f, 0.6f); f.spin = R(10, 40);
            }
            var chair = Build.Box("Chair", steel, root, new Vector3(-1.6f, 2.6f, 5.5f), new Vector3(0.5f, 0.08f, 0.5f), false, Quaternion.Euler(25, 40, 10));
            chair.AddComponent<Floaty>();
            // containment chamber
            Build.Box("ContFrame", concD, root, new Vector3(2.6f, 2.0f, 7f), new Vector3(1.8f, 4f, 0.5f), true);
            Build.Box("ContInner", Mats.Glow(Palette.Red * 0.6f), root, new Vector3(2.6f, 1.8f, 7.28f), new Vector3(1.3f, 3.4f, 0.05f));
            containmentDoor = Build.Box("ContDoor", steel, root, new Vector3(2.6f, 1.8f, 6.7f), new Vector3(1.3f, 3.4f, 0.12f), true).transform;
            var haze = Build.Particles("Haze", root, new Color(0.6f, 0.6f, 0.64f, 0.12f), 1.8f, 0.03f, 8f, 12f, 1f, true);
            haze.transform.localPosition = new Vector3(0, 2f, 8f);
            var hs = haze.shape; hs.shapeType = ParticleSystemShapeType.Box; hs.scale = new Vector3(10f, 3f, 14f);
            haze.Play();
        }
    }

    /// <summary>Level 3: the Fracture Chamber. A stone platform over an abyss, colossal formations,
    /// an impossibly large rotating structure overhead, intense yellow and cold blue light.</summary>
    public class Chamber : Env
    {
        protected override string AmbienceClip => "amb_chamber";
        protected override AudioReverbPreset ReverbPreset => AudioReverbPreset.Arena;
        Transform ringA, ringB;
        public readonly List<Transform> formations = new List<Transform>();

        protected override void BuildEnv()
        {
            var basalt = Mats.Stone(Palette.Stone, Palette.Blue * 0.04f);
            var basaltB = Mats.Stone(Palette.StoneLight * 0.75f, Palette.Blue * 0.06f);
            var fault = Mats.Glow(Palette.Yellow * 1.2f);
            var rng = new System.Random(31);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Build.Box("Platform", Mats.Lit(new Color(0.1f, 0.105f, 0.115f), Palette.Blue * 0.02f, 0.7f), root, new Vector3(0, -0.3f, 4.5f), new Vector3(14f, 0.6f, 13f), true);
            for (int i = 0; i < 26; i++)
                Build.Shape("Edge", Meshes.Rock(i), basalt, root, new Vector3(R(-7f, 7f), R(-0.4f, 0f), 11f + R(-0.4f, 0.4f)), Vector3.one * R(0.6f, 1.5f),
                    Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)));
            for (int i = 0; i < 34; i++)
            {
                float z = R(0f, 50f); float side = rng.NextDouble() < 0.5 ? -1 : 1;
                float x = side * R(7f + z * 0.1f, 13f + z * 0.35f);
                float h = R(20f, 45f);
                var col = Build.Shape("Formation", Meshes.Basalt, i % 3 == 0 ? basaltB : basalt, root, new Vector3(x, h * 0.5f - 10f, z),
                    new Vector3(R(2f, 5f), h, R(2f, 5f)), Quaternion.Euler(0, R(-20, 20), 0));
                if (i % 2 == 0)
                    Build.Box("Fault", fault, col.transform, new Vector3(R(-0.3f, 0.3f), R(-0.2f, 0.3f), -0.51f), new Vector3(0.02f, R(0.15f, 0.4f), 0.02f));
                formations.Add(col.transform);
            }
            ringA = Ring("RingA", new Vector3(0, 15f, 40f), 11f, 40, basalt, Palette.Yellow);
            ringB = Ring("RingB", new Vector3(0, 15f, 40f), 7f, 28, basaltB, Palette.Blue);
            Build.Box("AbyssGlow", Mats.Glow(new Color(0.04f, 0.1f, 0.25f)), root, new Vector3(0, -30f, 50f), new Vector3(200f, 1f, 80f));
            var keyL = new GameObject("BlueKey").AddComponent<Light>();
            keyL.transform.SetParent(root, false); keyL.transform.localRotation = Quaternion.Euler(50, 30, 0);
            keyL.type = LightType.Directional; keyL.color = new Color(0.45f, 0.6f, 1f); keyL.intensity = 0.7f;
            var yel = new GameObject("YellowCore").AddComponent<Light>();
            yel.transform.SetParent(root, false); yel.transform.localPosition = new Vector3(0, 15f, 39f);
            yel.type = LightType.Point; yel.color = Palette.Yellow; yel.range = 60f; yel.intensity = 2f; yel.shadows = LightShadows.None;
            var haze = Build.Particles("Haze", root, new Color(0.5f, 0.6f, 0.9f, 0.1f), 4f, 0.05f, 10f, 14f, 1f, true);
            haze.transform.localPosition = new Vector3(0, 5f, 25f);
            var hs = haze.shape; hs.shapeType = ParticleSystemShapeType.Box; hs.scale = new Vector3(40f, 14f, 40f);
            haze.Play();
        }

        Transform Ring(string name, Vector3 pos, float radius, int count, Material m, Color glow)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false); t.localPosition = pos; t.localRotation = Quaternion.Euler(-20, 0, 0);
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                var r = Build.Shape("Seg", Meshes.Rock(i), m, t, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius,
                    new Vector3(1.2f, 2.4f, 1.0f) * Random.Range(0.7f, 1.3f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg));
                if (i % 3 == 0) Build.Box("Glow", Mats.Glow(glow), r.transform, new Vector3(0, 0, -0.52f), new Vector3(0.5f, 0.06f, 0.02f));
            }
            return t;
        }

        void Update()
        {
            if (ringA) ringA.Rotate(0, 0, 4f * Time.deltaTime, Space.Self);
            if (ringB) ringB.Rotate(0, 0, -6f * Time.deltaTime, Space.Self);
        }
    }
}
