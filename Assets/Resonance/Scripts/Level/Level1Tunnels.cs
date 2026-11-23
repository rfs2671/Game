using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Level 1, The Survey Tunnels. Sections slide toward the player so the whole level fits a 2 x 2 m room:
    /// control panel → three energy cores → jammed gate (two-hand push) → dark section with the hand light,
    /// hidden markings and lurkers → gravity room bridge for the drone → three-pillar beam puzzle → fold → Gravity Pull.
    /// </summary>
    public class Level1Tunnels : LevelBase
    {
        Tunnels tun;
        static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);

        protected override Func<IEnumerator>[] Steps() => new Func<IEnumerator>[]
        {
            Transition, ControlPanel, Cores, Gate, DarkSection, GravityRoom, BeamPuzzle, GravityPull
        };

        protected override void OnBegin()
        {
            Hud.Section("LEVEL 1 · THE SURVEY TUNNELS");
            Hud.Accent(Cyan);
            Player.I.forgiving = true;
        }

        IEnumerator Transition()
        {
            Hud.Objective("", "", null);
            if (Quarry.I != null) yield return Quarry.I.FoldOut(2.5f);
            Cleanup.Level();
            tun = Env.Create<Tunnels>("Tunnels");
            tun.Construct();
            yield return tun.FoldIn(3.2f);
            Hud.Say("An old survey facility. Nobody has worked here in a very long time.", 4f);
            if (startStep > 2) PlaceBasics();
            yield return Wait(2f);
        }

        void PlaceBasics() { if (tun == null) { tun = Env.Create<Tunnels>("Tunnels"); tun.Construct(); } }

        // ---------------------------------------------------------------- control panel
        IEnumerator ControlPanel()
        {
            PlaceBasics();
            var panel = Build.Box("Panel", Mats.Lit(new Color(0.3f, 0.31f, 0.33f), Color.black, 0.5f), R,
                new Vector3(0, Chest - 0.05f, 0.62f), new Vector3(0.7f, 0.45f, 0.06f), true);
            Build.Box("PanelScreen", Mats.Glow(Cyan * 0.6f), panel.transform, new Vector3(0, 0.33f, -0.6f), new Vector3(0.8f, 0.2f, 0.1f));
            var buttons = new List<PressButton>();
            string[] labels = { "I", "II", "III" };
            for (int i = 0; i < 3; i++)
                buttons.Add(PressButton.Create(R, new Vector3(-0.22f + i * 0.22f, Chest - 0.08f, 0.58f), Quaternion.identity, i, Cyan, labels[i]));
            int next = 0;
            foreach (var b in buttons)
                b.OnPressed += pb =>
                {
                    if (pb.id == next) { next++; }
                    else { next = 0; foreach (var x in buttons) x.Reset(); Sfx.Play(Sound.Hurt, pb.transform.position, 0.4f, 1.5f); }
                };
            while (next < 3 && !skip)
            {
                for (int i = 0; i < 3; i++) buttons[i].SetHint(i == next);
                Hud.Objective("CONTROL PANEL", "Reach out and press the panel's buttons in order: I, II, III.",
                    new[] { Hud.Check(next >= 3, $"Restart the panel ({next}/3)") });
                yield return null;
            }
            Sfx.Play(Sound.LightOn, panel.transform.position, 1f);
            Hud.Say("Partial power. Three energy cores are missing from the board.", 3.5f);
            yield return Wait(1f);
        }

        // ---------------------------------------------------------------- energy cores
        IEnumerator Cores()
        {
            PlaceBasics();
            var coreMat = Mats.Lit(Cyan, Cyan * 2.2f, 0.7f);
            Vector3[] coreAt = { new Vector3(-1.2f, 0.25f, 3.4f), new Vector3(1.3f, 1.25f, 4.4f), new Vector3(0.2f, 2.55f, 5.0f) };
            foreach (var p in coreAt)
            {
                var c = Throwable.CreateWith("EnergyCore", Meshes.Cylinder, coreMat, tun.T(p.x, p.y, p.z), new Vector3(0.12f, 0.16f, 0.12f), Quaternion.Euler(80, 20, 0));
                c.keyId = "core";
                var l = c.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.color = Cyan; l.range = 1.6f; l.intensity = 1.5f;
                if (p.y > 2f) { c.rb.isKinematic = true; c.tearable = true; }   // jammed in the ceiling pipes
            }
            var board = Build.Box("CoreBoard", Mats.Lit(new Color(0.22f, 0.23f, 0.25f), Color.black, 0.4f), tun.Root,
                new Vector3(0, 1.5f, 2.6f), new Vector3(1.6f, 0.7f, 0.08f), true);
            int filled = 0;
            for (int i = 0; i < 3; i++)
            {
                var s = Socket.Create(tun.Root, new Vector3(-0.5f + i * 0.5f, 1.5f, 2.53f), Quaternion.identity, "core", Cyan, 0.12f);
                s.radius = 0.3f;
                s.OnFilled += (_, __) => filled++;
            }
            while (filled < 3 && !skip)
            {
                Hud.Objective("ENERGY CORES", "Find the three glowing cores. Lift them with telekinesis and set them into the board.",
                    new[] { Hud.Check(filled >= 3, $"Insert cores ({filled}/3)") });
                Hud.Progress(filled / 3f);
                yield return null;
            }
            Hud.Say("The gate ahead is jammed.", 2.5f);
            yield return Wait(1f);
        }

        // ---------------------------------------------------------------- jammed gate
        class JammedGate : MonoBehaviour, IPushable
        {
            public Func<bool> cleared;
            public bool Open;
            float sink;
            public void Push(Vector3 impulse, bool heavy)
            {
                if (Open) return;
                if (!cleared()) { Sfx.Play(Sound.Thump, transform.position, 0.6f); Hud.Say("Rocks are still jamming it.", 2f); return; }
                if (!heavy) { Feel.Shake(0.01f); Sfx.Play(Sound.Thump, transform.position, 0.8f); Hud.Say("It moves. Push with both hands.", 2f); return; }
                Open = true;
                Impact.Spawn(transform.position, -transform.forward, Palette.Blue, ImpactKind.Heavy, 2f);
                Sfx.Play(Sound.Door, transform.position, 1f);
            }
            void Update()
            {
                if (!Open || sink >= 1f) return;
                sink = Mathf.Min(1f, sink + Time.deltaTime / 1.2f);
                transform.localPosition += Vector3.up * Time.deltaTime * 3.2f / 1.2f;
            }
        }

        IEnumerator Gate()
        {
            PlaceBasics();
            var rust = Mats.Lit(new Color(0.35f, 0.18f, 0.09f), Color.black, 0.3f);
            var gateGo = Build.Box("Gate", rust, tun.Root, new Vector3(0, 1.6f, 6.6f), new Vector3(3.8f, 3.2f, 0.25f), true);
            for (int i = 0; i < 9; i++)
                Build.Box("Bar", Mats.Lit(new Color(0.3f, 0.3f, 0.32f), Color.black, 0.5f), gateGo.transform, new Vector3(-0.42f + i * 0.105f, 0, -0.6f), new Vector3(0.015f, 1f, 0.3f));
            var rocks = new List<Throwable>();
            for (int i = 0; i < 6; i++)
                rocks.Add(Throwable.Create(tun.T(UnityEngine.Random.Range(-1.4f, 1.4f), 0.3f + (i % 2) * 0.35f, 6.2f), UnityEngine.Random.Range(0.3f, 0.45f)));
            bool Cleared()
            {
                foreach (var r in rocks) if (r != null && Vector3.Distance(r.transform.position, tun.T(r.transform.position.x, 0.5f, 6.2f)) < 99
                                            && Mathf.Abs(tun.Root.InverseTransformPoint(r.transform.position).z - 6.2f) < 0.9f) return false;
                return true;
            }
            var gate = gateGo.AddComponent<JammedGate>();
            gate.cleared = Cleared;
            while (!gate.Open && !skip)
            {
                int left = 0; foreach (var r in rocks) if (r != null && Mathf.Abs(tun.Root.InverseTransformPoint(r.transform.position).z - 6.2f) < 0.9f) left++;
                Hud.Objective("THE GATE", left > 0 ? "Pull the fallen rocks away from the gate." : "Thrust both palms at once to force it open.",
                    new[] { Hud.Check(left == 0, $"Clear the rocks ({6 - left}/6)"), Hud.Check(false, "Two-handed push") });
                yield return null;
            }
            yield return Wait(1.5f);
            ClearLooseStones();
            yield return tun.Advance(7f);
        }

        // ---------------------------------------------------------------- dark section
        class Marking : MonoBehaviour { public float seen; }

        IEnumerator DarkSection()
        {
            PlaceBasics();
            if (startStep > 4) { tun.Root.localPosition = Vector3.back * 7f; }
            tun.SetSectionDark(true);
            HandLight.SetOn(true);
            Hud.Say("Dark. Raise your hand.", 2.5f);
            var glyph = Mats.Glow(Cyan * 1.4f);
            var marks = new List<Marking>();
            Vector3[] at = { new Vector3(-2.0f, 1.3f, 8.6f), new Vector3(2.0f, 1.6f, 9.4f), new Vector3(-1.95f, 1.9f, 10.4f) };
            for (int i = 0; i < 3; i++)
            {
                var g = new GameObject("Marking" + i);
                g.transform.SetParent(tun.Root, false);
                g.transform.localPosition = at[i];
                g.transform.localRotation = Quaternion.Euler(0, at[i].x < 0 ? 90 : -90, 0);
                Build.Box("V", glyph, g.transform, Vector3.zero, new Vector3(0.03f, 0.35f, 0.01f));
                Build.Box("H", glyph, g.transform, new Vector3(0, 0.17f, 0), new Vector3(0.26f, 0.03f, 0.01f));
                Build.Box("D", glyph, g.transform, new Vector3(0.07f, -0.07f, 0), new Vector3(0.2f, 0.025f, 0.01f), false, Quaternion.Euler(0, 0, 40 + i * 30));
                g.AddComponent<RevealByLight>().radius = 2.6f;
                marks.Add(g.AddComponent<Marking>());
            }
            int found = 0;
            while (found < 3 && !skip)
            {
                found = 0;
                foreach (var m in marks)
                {
                    if (HandLight.On && Vector3.Distance(HandLight.Position, m.transform.position) < 2.6f) m.seen += Time.deltaTime;
                    if (m.seen > 0.6f) found++;
                }
                Hud.Objective("SURVEY MARKINGS", "Hold your hand toward the walls. The light reveals what was written there.",
                    new[] { Hud.Check(found >= 3, $"Find the markings ({found}/3)") });
                yield return null;
            }
            Hud.Say("Something moved. It only exists where the light touches it.", 3.5f);
            StartRockSupply(3, 0.8f, 3f, 1.6f);
            yield return Fight(CreatureKind.Lurker, new Vector3(-0.8f, 0, 4.5f), "LURKER", "Keep your light on it. Throw stones or push it away.", Cyan);
            yield return Fight(CreatureKind.Lurker, new Vector3(0.9f, 0, 5f), "LURKER", "Another one. Find it with the light.", Cyan);
            StopRockSupply();
            tun.SetSectionDark(false);
            HandLight.SetOn(false);
            ClearLooseStones();
            yield return tun.Advance(7f);
        }

        // ---------------------------------------------------------------- gravity room
        IEnumerator GravityRoom()
        {
            PlaceBasics();
            if (startStep > 5) tun.Root.localPosition = Vector3.back * 14f;
            var stone = Mats.Stone(new Color(0.25f, 0.26f, 0.29f), Palette.Blue * 0.04f);
            Build.Box("LedgeA", stone, tun.Root, new Vector3(-1.6f, 0.45f, 16.5f), new Vector3(0.9f, 0.9f, 0.9f), true);
            Build.Box("LedgeB", stone, tun.Root, new Vector3(1.6f, 0.45f, 16.5f), new Vector3(0.9f, 0.9f, 0.9f), true);
            var drone = Drone.Spawn(tun.T(-1.6f, 1.05f, 16.5f));
            drone.transform.SetParent(tun.Root, true);
            int placed = 0;
            for (int i = 0; i < 3; i++)
            {
                var s = Socket.Create(tun.Root, new Vector3(-0.8f + i * 0.8f, 0.85f, 16.5f), Quaternion.Euler(90, 0, 0), "", Palette.Blue, 0.22f);
                s.radius = 0.35f;
                s.OnFilled += (_, __) => placed++;
            }
            for (int i = 0; i < 6; i++)
            {
                var t = Throwable.Create(tun.T(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(1.4f, 2.8f), UnityEngine.Random.Range(17.5f, 19.5f)), 0.32f);
                t.zeroG = true; t.rb.useGravity = false; Phys.SetDrag(t.rb, 3f);
                t.rb.angularVelocity = UnityEngine.Random.insideUnitSphere;
            }
            Hud.Say("Gravity doesn't hold here. The drone needs a way across.", 3.5f);
            while (placed < 3 && !skip)
            {
                Hud.Objective("GRAVITY ANOMALY", "Guide three floating rocks into the gaps between the ledges to build a bridge.",
                    new[] { Hud.Check(placed >= 3, $"Bridge stones ({placed}/3)") });
                Hud.Progress(placed / 3f);
                yield return null;
            }
            drone.path = new[] { tun.T(-0.8f, 1.05f, 16.5f), tun.T(0f, 1.05f, 16.5f), tun.T(0.8f, 1.05f, 16.5f), tun.T(1.6f, 1.05f, 16.5f) };
            drone.go = true;
            yield return Until(() => drone.Arrived);
            Hud.Say("The drone opens the way to the reactor.", 2.5f);
            ClearLooseStones();
            yield return tun.Advance(7f);
        }

        // ---------------------------------------------------------------- beam puzzle
        IEnumerator BeamPuzzle()
        {
            PlaceBasics();
            if (startStep > 6) tun.Root.localPosition = Vector3.back * 21f;
            var stone = Mats.Stone(new Color(0.28f, 0.29f, 0.32f), Palette.Blue * 0.03f);
            var mirror = Mats.Lit(new Color(0.8f, 0.9f, 1f), Cyan * 0.6f, 0.95f);
            // emitter
            var em = Build.Box("Emitter", Mats.Lit(new Color(0.3f, 0.3f, 0.32f), Color.black, 0.5f), tun.Root, new Vector3(-1.45f, 1.4f, 22.5f), new Vector3(0.3f, 0.3f, 0.4f), true);
            var emGo = new GameObject("EmitterSource");
            emGo.transform.SetParent(tun.Root, false); emGo.transform.localPosition = new Vector3(-1.25f, 1.4f, 22.5f);
            emGo.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var beam = emGo.AddComponent<BeamEmitter>();
            // receiver
            var rx = Build.Box("Reactor", Mats.Lit(new Color(0.25f, 0.26f, 0.3f), Cyan * 0.3f, 0.5f), tun.Root, new Vector3(1.2f, 1.4f, 23.4f), new Vector3(0.6f, 0.9f, 0.4f), true);
            beam.receiver = rx.transform;
            // pillars: correct yaws are 135/135/45 (mod 180); start them wrong
            Vector3[] at = { new Vector3(0, 1.4f, 22.5f), new Vector3(0, 1.4f, 25.5f), new Vector3(1.2f, 1.4f, 25.5f) };
            float[] startYaw = { 0f, 90f, 157.5f };
            float[] correct = { 135f, 135f, 45f };
            for (int i = 0; i < 3; i++)
            {
                var p = Throwable.CreateWith("Pillar", Meshes.Cube, stone, tun.T(at[i].x, at[i].y, at[i].z), new Vector3(0.5f, 2.2f, 0.35f),
                    tun.Root.rotation * Quaternion.Euler(0, startYaw[i], 0));
                p.rb.isKinematic = true; p.anchored = true; p.twistable = true; p.yawOnly = true;
                p.transform.SetParent(tun.Root, true);
                Build.Box("Mirror", mirror, p.transform, new Vector3(0, 0.05f, -0.52f), new Vector3(0.85f, 0.7f, 0.02f));
                Build.Box("Mirror2", mirror, p.transform, new Vector3(0, 0.05f, 0.52f), new Vector3(0.85f, 0.7f, 0.02f));
                p.gameObject.AddComponent<BeamMirror>();
                // Hidden hint behind each pillar: an arrow on the back wall showing the face angle, only visible from one side of the room.
                var hint = new GameObject("Hint" + i);
                hint.transform.SetParent(tun.Root, false);
                hint.transform.localPosition = new Vector3(at[i].x, 2.9f, 27.2f);
                hint.transform.localRotation = Quaternion.Euler(0, 0, -correct[i] + 90f);
                Build.Box("Arrow", Mats.Glow(Palette.Yellow), hint.transform, Vector3.zero, new Vector3(0.5f, 0.05f, 0.02f));
                Build.Box("Head", Mats.Glow(Palette.Yellow), hint.transform, new Vector3(0.22f, 0.06f, 0), new Vector3(0.16f, 0.04f, 0.02f), false, Quaternion.Euler(0, 0, -35));
                var vr = hint.AddComponent<ViewReveal>();
                vr.minX = i == 0 ? -1.2f : i == 1 ? -0.35f : 0.35f;
                vr.maxX = i == 0 ? -0.35f : i == 1 ? 0.35f : 1.2f;
            }
            bool powered = false;
            beam.OnPowered += () => powered = true;
            Hud.Say("A reactor. The beam has to find its way in.", 3f);
            while (!powered && !skip)
            {
                Hud.Objective("THE REACTOR", "Grab a pillar and twist your hand to turn it. Step left, centre and right to see the marks on the far wall.",
                    new[] { Hud.Check(false, "Route the beam into the reactor") });
                yield return null;
            }
            Feel.Shake(0.03f);
            Hud.Say("Power. The tunnel is folding.", 3f);
            yield return Wait(1.5f);
        }

        IEnumerator GravityPull()
        {
            PowerSet.Gravity = true;
            yield return Unlock("GRAVITY PULL", "Open your palm at loose objects, then snap it shut. Open and thrust to launch them.");
            for (int i = 0; i < 6; i++) Throwable.Create(Arena.W(UnityEngine.Random.Range(-1.2f, 1.2f), 0.3f, UnityEngine.Random.Range(2f, 3f)), 0.2f);
            int broken = 0;
            for (int i = 0; i < 3; i++) Crystal.Create(Arena.W(-1.2f + i * 1.2f, Eye + 0.1f, 5f)).OnBroken += _ => broken++;
            while (broken < 3 && !skip)
            {
                Hud.Objective("GRAVITY PULL", "Snap your open hand into a fist to pull several stones at once. Open and thrust to launch them.",
                    new[] { Hud.Check(false, $"Shatter the crystals ({broken}/3)") });
                yield return null;
            }
            Hud.Toast("LEVEL 1 COMPLETE", "Unlocked: Gravity Pull", 4f);
            yield return Wait(2f);
            yield return tun.FoldOut(2.5f);
        }
    }
}
