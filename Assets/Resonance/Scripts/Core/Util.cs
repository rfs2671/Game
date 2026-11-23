using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Resonance
{
    /// <summary>Colour palette pulled from the concept art. Optical see-through glasses are additive:
    /// black renders as transparent, so everything readable must be bright or emissive.</summary>
    public static class Palette
    {
        public static readonly Color Blue = new Color(0.35f, 0.75f, 1.00f);
        public static readonly Color BlueDeep = new Color(0.10f, 0.30f, 0.55f);
        public static readonly Color Yellow = new Color(1.00f, 0.78f, 0.20f);
        public static readonly Color Red = new Color(1.00f, 0.18f, 0.10f);
        public static Color Stone = new Color(0.30f, 0.32f, 0.36f);        // set by Look.Apply
        public static Color StoneLight = new Color(0.48f, 0.50f, 0.55f);
        public static readonly Color White = new Color(0.92f, 0.96f, 1.00f);
    }

    /// <summary>Runtime material factory. Works in URP or the built-in pipeline.
    /// Run Resonance > Setup Project once so these shaders are included in device builds.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static bool URP => GraphicsSettings.currentRenderPipeline != null;

        public static readonly string[] ShaderNamesURP =
        {
            "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit"
        };
        public static readonly string[] ShaderNamesBuiltin =
        {
            "Standard", "Unlit/Color", "Legacy Shaders/Particles/Additive"
        };

        static Shader Find(int i)
        {
            Shader s = Shader.Find(URP ? ShaderNamesURP[i] : ShaderNamesBuiltin[i]);
            if (s == null) s = Shader.Find(ShaderNamesBuiltin[i]);
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        static Texture2D stoneTex;
        /// <summary>Tileable greyscale grain so stone surfaces aren't flat colour.</summary>
        public static Texture2D StoneTex
        {
            get
            {
                if (stoneTex != null) return stoneTex;
                const int n = 128;
                stoneTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 6.2832f, v = y / (float)n * 6.2832f;
                    // tileable by sampling noise on a torus
                    float a = Mathf.PerlinNoise(Mathf.Cos(u) * 1.5f + 10, Mathf.Sin(u) * 1.5f + Mathf.Cos(v) * 1.5f + 3);
                    float b = Mathf.PerlinNoise(Mathf.Cos(u) * 5f + 4 + Mathf.Sin(v) * 5f, Mathf.Sin(u) * 5f + 7);
                    float g = 0.62f + 0.3f * a + 0.18f * (b - 0.5f);
                    px[y * n + x] = new Color(g, g, g * 1.02f, 1);
                }
                stoneTex.SetPixels(px); stoneTex.Apply(true);
                return stoneTex;
            }
        }

        /// <summary>Stone: lit, grain texture, low smoothness, optional emission.</summary>
        public static Material Stone(Color baseColor, Color emission, float smooth = 0.18f)
        {
            string key = "ST" + baseColor + emission + smooth;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit(baseColor, emission, smooth));
            m.SetTexture("_BaseMap", StoneTex);
            m.SetTexture("_MainTex", StoneTex);
            cache[key] = m;
            return m;
        }

        /// <summary>Lit surface with optional emission.</summary>
        public static Material Lit(Color baseColor, Color emission, float smooth = 0.25f)
        {
            string key = "L" + baseColor + emission + smooth;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Find(0));
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            if (emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Flat bright colour. Reads well on additive displays.</summary>
        public static Material Glow(Color c)
        {
            string key = "G" + c;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Find(1));
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            cache[key] = m;
            return m;
        }

        static Texture2D soft;
        /// <summary>Radial falloff texture so particles read as soft glows instead of squares.</summary>
        public static Texture2D SoftTex
        {
            get
            {
                if (soft != null) return soft;
                const int n = 64;
                soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a;
                    px[y * n + x] = new Color(a, a, a, a);
                }
                soft.SetPixels(px);
                soft.Apply();
                return soft;
            }
        }

        /// <summary>Additive with a soft round texture, for particles.</summary>
        public static Material Soft(Color c)
        {
            string key = "S" + c;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Additive(c));
            m.SetTexture("_BaseMap", SoftTex);
            m.SetTexture("_MainTex", SoftTex);
            cache[key] = m;
            return m;
        }

        /// <summary>Additive transparent: particles, beams, mist, HUD panels.</summary>
        public static Material Additive(Color c)
        {
            string key = "A" + c;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Find(2));
            if (URP)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetColor("_BaseColor", c);
            }
            else
            {
                m.SetColor("_TintColor", c * 0.5f);
            }
            m.SetColor("_Color", c);
            m.renderQueue = 3000;
            cache[key] = m;
            return m;
        }
    }

    /// <summary>Procedural meshes so the slice needs no imported art.</summary>
    public static class Meshes
    {
        static Mesh cube, sphere;
        static readonly List<Mesh> rocks = new List<Mesh>();

        static Mesh Primitive(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }

        public static Mesh Cube => cube != null ? cube : (cube = Primitive(PrimitiveType.Cube));
        public static Mesh Sphere => sphere != null ? sphere : (sphere = Primitive(PrimitiveType.Sphere));
        static Mesh cylinder;
        public static Mesh Cylinder => cylinder != null ? cylinder : (cylinder = Primitive(PrimitiveType.Cylinder));

        /// <summary>Faceted boulder: subdivided icosahedron with fractal noise displacement, flat-shaded.
        /// 30 cached variants; ~320 triangles each so dozens can be on screen on a mobile GPU.</summary>
        public static Mesh Rock(int seed)
        {
            if (rocks.Count == 0)
                for (int s = 0; s < 30; s++) rocks.Add(BuildRock(s));
            return rocks[Mathf.Abs(seed) % rocks.Count];
        }

        static Mesh BuildRock(int s)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v0 = { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            int[] f0 = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            foreach (var v in v0) verts.Add(v.normalized);
            tris.AddRange(f0);
            var cache = new Dictionary<long, int>();
            for (int it = 0; it < 2; it++)
            {
                var nt = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b, verts, cache), bc = Mid(b, c, verts, cache), ca = Mid(c, a, verts, cache);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
                cache.Clear();
            }
            var rnd = new System.Random(s * 7919 + 13);
            Vector3 stretch = new Vector3(0.75f + (float)rnd.NextDouble() * 0.5f, 0.65f + (float)rnd.NextDouble() * 0.45f, 0.75f + (float)rnd.NextDouble() * 0.5f);
            Vector3 off = new Vector3((float)rnd.NextDouble() * 50f, (float)rnd.NextDouble() * 50f, (float)rnd.NextDouble() * 50f);
            // a couple of flat "cleavage planes" make it read as fractured stone rather than a lumpy ball
            Vector3 RandDir() => new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
            Vector3 cut1 = RandDir(), cut2 = RandDir();
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                float n = Fbm(p * 1.7f + off) * 0.38f + Fbm(p * 4.3f + off * 2f) * 0.12f;
                p *= 0.5f * (1f + n);
                float d1 = Vector3.Dot(p, cut1); if (d1 > 0.28f) p -= cut1 * (d1 - 0.28f);
                float d2 = Vector3.Dot(p, cut2); if (d2 > 0.33f) p -= cut2 * (d2 - 0.33f);
                verts[i] = Vector3.Scale(p, stretch);
            }
            // unweld for flat shading
            var fv = new Vector3[tris.Count];
            var fi = new int[tris.Count];
            var uv = new Vector2[tris.Count];
            for (int i = 0; i < tris.Count; i++) { fv[i] = verts[tris[i]]; fi[i] = i; uv[i] = new Vector2(fv[i].x + 0.5f, fv[i].y + 0.5f); }
            var m = new Mesh { name = "Rock" + s, vertices = fv, triangles = fi, uv = uv };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out int i)) return i;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        static float Fbm(Vector3 p)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < 3; o++)
            {
                sum += (Perlin3(p) - 0.5f) * 2f * amp; norm += amp;
                p *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Perlin3(Vector3 p)
        {
            float xy = Mathf.PerlinNoise(p.x, p.y), yz = Mathf.PerlinNoise(p.y, p.z), xz = Mathf.PerlinNoise(p.x, p.z);
            float yx = Mathf.PerlinNoise(p.y, p.x), zy = Mathf.PerlinNoise(p.z, p.y), zx = Mathf.PerlinNoise(p.z, p.x);
            return (xy + yz + xz + yx + zy + zx) / 6f;
        }

        static Mesh hex;
        /// <summary>Basalt column: tapered hexagonal prism with a chipped, uneven top. Unit height, ~1 wide.</summary>
        public static Mesh Basalt
        {
            get
            {
                if (hex != null) return hex;
                var v = new List<Vector3>(); var tr = new List<int>();
                var rnd = new System.Random(5);
                var bottom = new Vector3[6]; var top = new Vector3[6];
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    bottom[i] = new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f);
                    top[i] = new Vector3(Mathf.Cos(a) * 0.46f, 0.5f - (float)rnd.NextDouble() * 0.06f, Mathf.Sin(a) * 0.46f);
                }
                Vector3 topC = new Vector3(0.03f, 0.47f, -0.02f);
                for (int i = 0; i < 6; i++)
                {
                    int j = (i + 1) % 6;
                    Quad(v, tr, bottom[i], bottom[j], top[j], top[i]);
                    Tri(v, tr, top[i], top[j], topC);
                }
                hex = new Mesh { name = "Basalt", vertices = v.ToArray(), triangles = tr.ToArray() };
                hex.RecalculateNormals(); hex.RecalculateBounds();
                return hex;
            }
        }

        static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count; v.Add(a); v.Add(c); v.Add(b); t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(v, t, a, b, c); Tri(v, t, a, c, d);
        }

        static float Hash(Vector3 p, int seed, int axis)
        {
            int x = Mathf.RoundToInt(p.x * 2f), y = Mathf.RoundToInt(p.y * 2f), z = Mathf.RoundToInt(p.z * 2f);
            int h = x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ (seed + 1) * 2654435 ^ axis * 97;
            h = (h << 13) ^ h;
            return ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }

    public static class Build
    {
        /// <summary>Make a mesh object with a renderer and optional collider.</summary>
        public static GameObject Shape(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos,
            Vector3 scale, Quaternion? rot = null, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (collider)
            {
                if (mesh == Meshes.Sphere) go.AddComponent<SphereCollider>();
                else go.AddComponent<BoxCollider>();
            }
            return go;
        }

        public static GameObject Box(string name, Material mat, Transform parent, Vector3 localPos, Vector3 scale,
            bool collider = false, Quaternion? rot = null)
            => Shape(name, Meshes.Cube, mat, parent, localPos, scale, rot, collider);

        public static LineRenderer Line(string name, Transform parent, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Additive(Color.white);
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = width;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>Small additive particle burst or emitter.</summary>
        public static ParticleSystem Particles(string name, Transform parent, Color c, float size, float speed,
            float lifetime, float rate, float radius, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.duration = loop ? 5f : 0.5f;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = false;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = radius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Soft(Color.white);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        public static void Burst(Vector3 pos, Color c, int count, float speed = 2f, float size = 0.04f)
        {
            var ps = Particles("Burst", null, c, size, speed, 0.6f, 0, 0.05f, false);
            ps.transform.position = pos;
            ps.Emit(count);
            Object.Destroy(ps.gameObject, 1.5f);
        }
    }

    public static class Find
    {
        public static T[] All<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        public static T One<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }

    public static class Vec
    {
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        public static Vector3 FlatNorm(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
    }
    public static class Phys
    {
        public static Vector3 Vel(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDrag(Rigidbody rb, float d)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = d;
#else
            rb.drag = d;
#endif
        }
    }
}
