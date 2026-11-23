using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Level 2, The Abandoned Research Station: three repair stations inside the play area,
    /// the containment breach and the Mimic, Compression, and defending the unstable machine.
    /// </summary>
    public class Level2Station : LevelBase
    {
        Station st;
        static readonly Color Amber = new Color(1f, 0.6f, 0.25f);

        protected override Func<IEnumerator>[] Steps() => new Func<IEnumerator>[] { Transition, Stations, Breach, MimicFight, Machine, Finish };

        protected override void OnBegin() { Hud.Section("LEVEL 2 · THE RESEARCH STATION"); Hud.Accent(Palette.Red); }

        IEnumerator Transition()
        {
            Hud.Objective("", "", null);
            if (Env.Current != null && !(Env.Current is Station)) yield return Env.Current.FoldOut(2f);
            Cleanup.Level();
            Ensure();
            yield return st.FoldIn(3.2f);
            Hud.Say("A research station, built into the rock. The power is out.", 3.5f);
            yield return Wait(2f);
        }

        void Ensure() { if (st == null) { st = Env.Create<Station>("Station"); st.Construct(); } }

        // ---------------------------------------------------------------- stations
        IEnumerator Stations()
        {
            Ensure();
            bool a = false, b = false, c = false;
            StartCoroutine(StationA(() => a = true));
            StartCoroutine(StationB(() => b = true));
            StartCoroutine(StationC(() => c = true));
            Hud.Say("Three stations. Each one has to come back online.", 3f);
            while (!(a && b && c) && !skip)
            {
                Hud.Objective("RESTORE POWER", "Walk to each station in your space and repair it.", new[]
                {
                    Hud.Check(a, "A · Align the floating contacts"),
                    Hud.Check(b, "B · Throw the switches in the right order"),
                    Hud.Check(c, "C · Hold the machine together and reconnect it")
                });
                yield return null;
            }
            yield return Wait(1f);
        }

        GameObject Pedestal(Vector3 local, string label)
        {
            var steel = Mats.Lit(new Color(0.36f, 0.37f, 0.4f), Color.black, 0.5f);
            var p = Build.Box("Station" + label, steel, R, new Vector3(local.x, (Chest - 0.25f) * 0.5f, local.z), new Vector3(0.36f, Chest - 0.25f, 0.36f), true);
            Build.Box("Band", Mats.Glow(Amber), p.transform, new Vector3(0, 0.45f, -0.51f), new Vector3(1.02f, 0.03f, 0.02f));
            Impact.Spawn(R.TransformPoint(local + Vector3.up * Chest), Vector3.up, Amber, ImpactKind.Energy, 1f);
            return p;
        }

        IEnumerator StationA(Action done)
        {
            Vector3 c = new Vector3(-0.75f, 0, 0.75f);
            Pedestal(c, "A");
            var frameMat = Mats.Lit(new Color(0.3f, 0.31f, 0.33f), Color.black, 0.5f);
            var partMat = Mats.Stone(Palette.StoneLight, Amber * 0.2f);
            var parts = new List<Throwable>();
            float[] start = { 40f, 135f, 260f };
            for (int i = 0; i < 3; i++)
            {
                float y = Chest + 0.05f + i * 0.17f;
                Build.Box("Contact", Mats.Glow(Amber), R, new Vector3(c.x, y, c.z - 0.2f), new Vector3(0.03f, 0.03f, 0.08f));
                var p = Throwable.CreateWith("Component", Meshes.Cube, partMat, R.TransformPoint(new Vector3(c.x, y, c.z)), new Vector3(0.26f, 0.06f, 0.26f),
                    R.rotation * Quaternion.Euler(0, start[i], 0));
                p.rb.isKinematic = true; p.anchored = true; p.twistable = true; p.yawOnly = true;
                Build.Box("Pin", Mats.Glow(Amber * 0.6f), p.transform, new Vector3(0, 0, -0.5f), new Vector3(0.12f, 1.1f, 0.12f));
                parts.Add(p);
            }
            while (!skip)
            {
                int ok = 0;
                foreach (var p in parts)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(p.transform.eulerAngles.y, R.eulerAngles.y));
                    if (d < 10f && !p.held) { ok++; if (p.canGrab == null) { p.canGrab = () => false; Impact.Spawn(p.transform.position, Vector3.up, Amber, ImpactKind.Energy, 0.6f); } }
                }
                if (ok == 3) break;
                yield return null;
            }
            Sfx.Play(Sound.Unlock, R.TransformPoint(c + Vector3.up * Chest), 0.7f);
            done();
        }

        IEnumerator StationB(Action done)
        {
            Vector3 c = new Vector3(0, 0, 1.0f);
            Pedestal(c, "B");
            string[] sym = { "▲", "■", "●", "◆" };
            int[] order = { 2, 0, 3, 1 };   // ● ▲ ◆ ■
            var memo = new GameObject("Memo").AddComponent<TextMesh>();
            memo.transform.SetParent(R, false);
            memo.transform.localPosition = new Vector3(-1.4f, Eye + 0.3f, 3.2f);
            memo.text = "INCIDENT MEMO 7-B\nRestart sequence for\nauxiliary bus:\n\n●   ▲   ◆   ■\n\nDo not deviate.";
            memo.fontSize = 64; memo.characterSize = 0.012f; memo.anchor = TextAnchor.UpperLeft; memo.color = new Color(0.95f, 0.92f, 0.85f);
#if UNITY_2022_2_OR_NEWER
            memo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            memo.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            if (memo.font) memo.GetComponent<MeshRenderer>().sharedMaterial = memo.font.material;
            Build.Box("MemoBoard", Mats.Lit(new Color(0.2f, 0.2f, 0.21f), Color.black, 0.3f), R, new Vector3(-1.1f, Eye + 0.05f, 3.25f), new Vector3(0.75f, 0.65f, 0.02f));
            var buttons = new List<PressButton>();
            for (int i = 0; i < 4; i++)
                buttons.Add(PressButton.Create(R, new Vector3(c.x - 0.18f + (i % 2) * 0.36f, Chest - 0.03f + (i / 2) * 0.2f, c.z - 0.2f), Quaternion.identity, i, Amber, sym[i]));
            int step = 0;
            foreach (var b in buttons) b.OnPressed += pb =>
            {
                if (pb.id == order[step]) step++;
                else { step = 0; foreach (var x in buttons) x.Reset(); Sfx.Play(Sound.Hurt, pb.transform.position, 0.5f, 1.4f); Hud.Say("Wrong order. Read the memo.", 2f); }
            };
            while (step < 4 && !skip) yield return null;
            Sfx.Play(Sound.Unlock, R.TransformPoint(c + Vector3.up * Chest), 0.7f);
            done();
        }

        IEnumerator StationC(Action done)
        {
            Vector3 c = new Vector3(0.75f, 0, 0.75f);
            Pedestal(c, "C");
            var steel = Mats.Lit(new Color(0.36f, 0.37f, 0.4f), Color.black, 0.55f);
            var core = Throwable.CreateWith("MachineCore", Meshes.Sphere, Mats.Lit(Amber, Amber * 1.8f), R.TransformPoint(new Vector3(c.x, Chest + 0.2f, c.z)),
                Vector3.one * 0.12f, Quaternion.identity);
            core.rb.isKinematic = true; core.anchored = true;
            var bits = new List<Transform>();
            for (int i = 0; i < 6; i++)
                bits.Add(Build.Box("Part", steel, R, Vector3.zero, new Vector3(0.08f, 0.2f, 0.04f), false, Quaternion.Euler(0, i * 60, 0)).transform);
            var plug = Throwable.CreateWith("Plug", Meshes.Cylinder, Mats.Lit(new Color(0.2f, 0.2f, 0.22f), Amber * 0.4f), R.TransformPoint(new Vector3(c.x + 0.45f, 0.3f, c.z + 0.6f)),
                new Vector3(0.06f, 0.08f, 0.06f), Quaternion.identity);
            plug.keyId = "plug";
            var sock = Socket.Create(R, new Vector3(c.x, Chest + 0.2f, c.z - 0.22f), Quaternion.identity, "plug", Amber, 0.07f);
            sock.radius = 0.18f;
            sock.accept = () => core.held;
            bool plugged = false;
            sock.OnFilled += (_, __) => plugged = true;
            Vector3 centre = R.TransformPoint(new Vector3(c.x, Chest + 0.2f, c.z));
            float together = 0;
            while (!plugged && !skip)
            {
                together = Mathf.MoveTowards(together, core.held ? 1f : 0f, Time.deltaTime * 1.5f);
                for (int i = 0; i < bits.Count; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f + Time.time * 0.3f;
                    float rad = Mathf.Lerp(0.38f, 0.11f, together);
                    bits[i].position = centre + R.rotation * new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a * 2f) * 0.08f * (1 - together), Mathf.Sin(a) * rad);
                    bits[i].rotation = R.rotation * Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                }
                yield return null;
            }
            Sfx.Play(Sound.Unlock, centre, 0.7f);
            done();
        }

        // ---------------------------------------------------------------- breach + mimic
        IEnumerator Breach()
        {
            Ensure();
            Hud.Say("Power restored. Containment door 7 is opening.", 3f);
            Sfx.Play(Sound.Door, st.ChamberPos, 1f);
            float t = 0;
            while (t < 2.5f && st.containmentDoor != null) { t += Time.deltaTime; st.containmentDoor.localPosition += Vector3.up * Time.deltaTime * 1.4f; yield return null; }
            PowerSet.Compress = true;
            yield return Unlock("COMPRESSION", "Close both hands and bring them together fast. Crush a held stone into a dense shard, or crush armour in front of you.");
        }

        IEnumerator MimicFight()
        {
            Ensure();
            PowerSet.Compress = true;
            var head = HandInput.Head;
            Vector3 pos = Arena.W(0.2f, 0, 4.2f);
            var m = Mimic.Spawn(pos, Quaternion.LookRotation(Vec.FlatNorm(Arena.Feet - pos)));
            bool dead = false;
            m.OnDied += () => dead = true;
            StartRockSupply(3, 0.8f, 2.6f, 1.6f);
            float t = 0;
            while (!dead && !skip)
            {
                t += Time.deltaTime;
                string hint = t < 18f ? "It copies your hands. Its core is shielded."
                    : t < 40f ? "It mirrors everything you do. What would make it open up? Raise both hands above your head."
                    : "Raise both hands high to make it expose its core, then throw. Or crush its plates with Compression.";
                Hud.Objective("THE MIMIC", hint, new[] { Hud.Check(false, m.Exposed ? "Core exposed — strike now!" : "Expose the core") });
                yield return null;
            }
            StopRockSupply();
            if (!dead && m != null) Destroy(m.gameObject);
            yield return Wait(1.5f);
        }

        // ---------------------------------------------------------------- the unstable machine
        IEnumerator Machine()
        {
            Ensure();
            Hud.Say("The station's generator is failing. Keep it alive.", 3f);
            while (!skip)
            {
                var machine = UnstableMachine.Spawn(R, new Vector3(0, 0, 2.6f));
                StartRockSupply(4, 0.8f, 2.2f, 1.8f);
                float timer = 40f;
                int spawned = 0;
                var live = new List<Creature>();
                while (timer > 0 && machine.Alive && !skip)
                {
                    timer -= Time.deltaTime;
                    live.RemoveAll(c => c == null || !c.Alive);
                    if (live.Count < 2 && spawned < 6)
                    {
                        var kind = spawned % 3 == 2 ? CreatureKind.Charger : CreatureKind.Walker;
                        var c = Creature.Spawn(kind, Arena.W(UnityEngine.Random.Range(-2.5f, 2.5f), 0, UnityEngine.Random.Range(5.5f, 7f)), Palette.Red);
                        c.targetOverride = machine.transform;
                        c.targetDamage = machine;
                        live.Add(c);
                        spawned++;
                    }
                    Hud.Objective("UNSTABLE MACHINE", "Stop the forms from reaching the generator.",
                        new[] { Hud.Check(false, $"Hold out: {Mathf.CeilToInt(timer)} s"), Hud.Check(false, $"Generator: {Mathf.CeilToInt(machine.health)}%") });
                    Hud.Progress(1f - timer / 40f);
                    yield return null;
                }
                StopRockSupply();
                foreach (var c in live) if (c != null) Destroy(c.gameObject);
                if (machine.Alive || skip) { Destroy(machine.gameObject, 3f); break; }
                Impact.Spawn(machine.transform.position + Vector3.up, Vector3.up, Palette.Red, ImpactKind.Death);
                Destroy(machine.gameObject);
                Hud.Say("It's gone. The station resets it. Again.", 3f);
                yield return Wait(3f);
            }
            Sfx.Play(Sound.Unlock, HandInput.Head.position, 1f);
        }

        IEnumerator Finish()
        {
            Hud.Toast("LEVEL 2 COMPLETE", "Unlocked: Compression", 4f);
            yield return Wait(2.5f);
            if (st != null) yield return st.FoldOut(2.5f);
        }
    }
}
