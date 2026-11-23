using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Level 3, The Fracture Chamber: free three mechanism parts from their stone casings with Compression,
    /// turn each so its connector faces the frame, slot them in, then face The Formation.
    /// </summary>
    public class Level3Chamber : LevelBase
    {
        Chamber ch;

        protected override Func<IEnumerator>[] Steps() => new Func<IEnumerator>[] { Transition, Mechanism, Reshape, Boss, Collapse };

        protected override void OnBegin() { Hud.Section("LEVEL 3 · THE FRACTURE CHAMBER"); Hud.Accent(new Color(1f, 0.55f, 0.15f)); }

        void Ensure() { if (ch == null) { ch = Env.Create<Chamber>("Chamber"); ch.Construct(); } }

        IEnumerator Transition()
        {
            Hud.Objective("", "", null);
            if (Env.Current != null && !(Env.Current is Chamber)) yield return Env.Current.FoldOut(2f);
            Cleanup.Level();
            Ensure();
            yield return ch.FoldIn(4f);
            Hud.Say("A hollow chamber over nothing. Something enormous turns overhead.", 4f);
            yield return Wait(2.5f);
        }

        class Casing : MonoBehaviour, ICompressible
        {
            public Throwable inner;
            public Vector3 Point => transform.position;
            public bool CanCompress => PowerSet.Compress;
            void OnEnable() => Compressibles.All.Add(this);
            void OnDisable() => Compressibles.All.Remove(this);
            public void Compress()
            {
                Impact.Spawn(transform.position, Vector3.up, Palette.Yellow, ImpactKind.Heavy, 1.6f);
                inner.heavy = false;
                inner.rb.isKinematic = false;
                inner.transform.SetParent(null, true);
                Destroy(gameObject);
            }
        }

        IEnumerator Mechanism()
        {
            Ensure();
            PowerSet.Compress = true;
            var steel = Mats.Lit(new Color(0.34f, 0.35f, 0.38f), Color.black, 0.55f);
            var frame = Build.Box("MechFrame", steel, R, new Vector3(0, 1.4f, 3.4f), new Vector3(2.2f, 0.12f, 0.3f), true);
            Build.Box("MechL", steel, R, new Vector3(-1.1f, 0.7f, 3.4f), new Vector3(0.12f, 1.4f, 0.3f), true);
            Build.Box("MechR", steel, R, new Vector3(1.1f, 0.7f, 3.4f), new Vector3(0.12f, 1.4f, 0.3f), true);
            Color[] cols = { Palette.Yellow, Palette.Blue, new Color(1f, 0.45f, 0.2f) };
            Vector3[] partAt = { new Vector3(-2.3f, 0.55f, 2.8f), new Vector3(0, 0.55f, 5.6f), new Vector3(2.3f, 0.55f, 2.8f) };
            int placed = 0;
            for (int i = 0; i < 3; i++)
            {
                var sock = Socket.Create(R, new Vector3(-0.65f + i * 0.65f, 1.05f, 3.22f), Quaternion.identity, "m" + i, cols[i], 0.2f);
                sock.radius = 0.32f;
                sock.requireYawWithin = 25f;
                sock.OnFilled += (_, __) => placed++;
                // the part: a machined block with one glowing connector face (-Z = must face the player side)
                var part = Throwable.CreateWith("Part" + i, Meshes.Cube, steel, R.TransformPoint(partAt[i]), new Vector3(0.3f, 0.3f, 0.3f),
                    R.rotation * Quaternion.Euler(0, 90f + i * 70f, 0));
                part.keyId = "m" + i; part.twistable = true; part.yawOnly = true; part.heavy = true; part.rb.isKinematic = true;
                Build.Box("Connector", Mats.Glow(cols[i] * 1.4f), part.transform, new Vector3(0, 0, -0.51f), new Vector3(0.6f, 0.6f, 0.02f));
                var casing = Build.Shape("Casing", Meshes.Rock(40 + i), Mats.Stone(Palette.Stone, Palette.Yellow * 0.1f), R, partAt[i], Vector3.one * 0.85f, Quaternion.Euler(0, i * 50, 0), true);
                var c = casing.AddComponent<Casing>(); c.inner = part;
                part.transform.SetParent(casing.transform, true);
            }
            StartRockSupply(2, 0.8f, 2.4f, 1.5f);
            while (placed < 3 && !skip)
            {
                Hud.Objective("THE MECHANISM", "Each part is sealed in rock: face it, close both hands and crush them together. Then lift the part, twist it so its glowing face points at you, and slot it into the frame.",
                    new[] { Hud.Check(placed >= 3, $"Mechanism parts ({placed}/3)") });
                Hud.Progress(placed / 3f);
                yield return null;
            }
            StopRockSupply();
            Sfx.Play(Sound.Unlock, R.TransformPoint(new Vector3(0, 1.4f, 3.4f)), 1f);
            yield return Wait(1f);
        }

        IEnumerator Reshape()
        {
            Ensure();
            Hud.Say("The chamber is changing shape.", 3f);
            Sfx.Play(Sound.Rumble, Arena.W(0, 3, 10), 1f);
            float t = 0;
            var start = new List<Vector3>(); foreach (var f in ch.formations) start.Add(f.localPosition);
            while (t < 4f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < ch.formations.Count; i++)
                {
                    var f = ch.formations[i];
                    if (f == null) continue;
                    float k = Mathf.SmoothStep(0, 1, t / 4f);
                    f.localPosition = start[i] + new Vector3(Mathf.Sign(start[i].x) * 3f, (i % 3 - 1) * 4f, 0) * k;
                }
                Feel.Shake(0.004f);
                yield return null;
            }
            foreach (var n in new[] { "MechFrame", "MechL", "MechR" })
            {
                var o = R.Find(n);
                if (o) { Impact.Spawn(o.position, Vector3.up, Palette.Yellow, ImpactKind.Stone, 1f); Destroy(o.gameObject); }
            }
            foreach (var s in Find.All<Socket>()) Destroy(s.gameObject);
            foreach (var th in Throwable.All.ToArray()) if (th && th.keyId.StartsWith("m")) Destroy(th.gameObject);
        }

        IEnumerator Boss()
        {
            Ensure();
            Hud.Section("THE FORMATION");
            Player.I.forgiving = false;
            Player.I.Heal();
            Vector3 pos = Arena.W(0, 3.2f, 7f);
            var boss = Formation.Spawn(pos, Quaternion.LookRotation(Vec.FlatNorm(Arena.Feet - pos)));
            bool won = false;
            boss.OnDefeated += () => won = true;
            Action died = () => { Hud.Say("Resonance lost. It reforms. Again.", 3f); Player.I.Heal(); };
            Player.I.OnDied += died;
            StartRockSupply(4, 0.8f, 2.6f, 1.6f);
            while (!won && !skip)
            {
                string phase = boss.FinalPhase ? "FINAL PHASE" : $"Core integrity {boss.Hp}/{boss.maxHp}";
                string hint = boss.Status != "" ? boss.Status : boss.Exposed ? "Core exposed — throw everything you have at it!"
                    : "Break its armour plates: throw stones, fire shards, catch its rocks, or crush plates with Compression.";
                Hud.Objective("THE FORMATION", hint, new[] { Hud.Check(false, phase) });
                Hud.Progress(1f - (boss.Hp - 1f) / (boss.maxHp - 1f));
                yield return null;
            }
            Player.I.OnDied -= died;
            Player.I.forgiving = true;
            StopRockSupply();
            if (!won && boss != null) Destroy(boss.gameObject);
            yield return Wait(2f);
        }

        IEnumerator Collapse()
        {
            Ensure();
            Hud.Section("THE FRACTURE CHAMBER");
            Hud.Say("The chamber is coming apart.", 3f);
            Sfx.Play(Sound.Rumble, Arena.W(0, 3, 10), 1f);
            // An impossible administrative complex rises out of the dark.
            var conc = Mats.Stone(new Color(0.38f, 0.37f, 0.36f), Color.black, 0.1f);
            var window = Mats.Glow(new Color(1f, 0.9f, 0.75f) * 1.1f);
            var complex = new GameObject("AdministrativeComplex").transform;
            complex.SetParent(R, false);
            complex.localPosition = new Vector3(0, -60f, 60f);
            var rnd = new System.Random(77);
            for (int i = 0; i < 18; i++)
            {
                float x = (float)(rnd.NextDouble() * 80 - 40), z = (float)(rnd.NextDouble() * 40);
                float h = 30 + (float)rnd.NextDouble() * 60, w = 8 + (float)rnd.NextDouble() * 14;
                var b = Build.Box("Block", conc, complex, new Vector3(x, h * 0.5f, z), new Vector3(w, h, w * 0.8f));
                for (int k = 0; k < 6; k++)
                    Build.Box("Windows", window, b.transform, new Vector3(0, -0.4f + k * 0.15f, -0.51f), new Vector3(0.8f, 0.012f, 0.01f));
            }
            float t = 0;
            while (t < 8f)
            {
                t += Time.deltaTime;
                complex.localPosition = Vector3.Lerp(new Vector3(0, -60f, 60f), new Vector3(0, -12f, 60f), Mathf.SmoothStep(0, 1, t / 8f));
                if (UnityEngine.Random.value < Time.deltaTime * 3f)
                {
                    var rock = Throwable.Create(Arena.W(UnityEngine.Random.Range(-5f, 5f), 9f, UnityEngine.Random.Range(4f, 10f)), UnityEngine.Random.Range(0.3f, 0.6f));
                    Destroy(rock.gameObject, 6f);
                }
                Feel.Shake(0.003f);
                yield return null;
            }
            Hud.Say("Beyond the quarry: something that was built to hold all of this.", 4f);
            yield return Wait(4f);
        }
    }
}
