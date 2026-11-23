using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// World-space HUD that lazily follows the head (no head-locked UI: it is uncomfortable on glasses).
    /// Layout echoes the concept art: section tag, power name, hint, checklist and progress bar on the right.
    /// Kept inside the Air 2 Ultra's ~52° diagonal field of view.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static Hud I;
        const float Distance = 1.4f;

        TextMesh header, title, hint, list, warning, subtitle, toastBig, toastSmall, health, readout, location;
        readonly List<LineRenderer> frame = new List<LineRenderer>();
        readonly List<MeshRenderer> segments = new List<MeshRenderer>();
        LineRenderer reticle;
        Material segOn, segOff;
        Color accent = Palette.Yellow;
        float energy, readoutTimer;
        Transform panel, barFill, barBack;
        Vector3 anchor;
        Quaternion anchorRot = Quaternion.identity;
        readonly Queue<(string, float)> lines = new Queue<(string, float)>();
        float lineTimer, toastTimer;
        bool recentre = true;
        Font font;

        void Awake()
        {
            I = this;
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            panel = new GameObject("HudPanel").transform;
            panel.SetParent(transform, false);

            header = Text("Header", new Vector3(0.20f, 0.20f, 0), 0.0045f, Palette.Blue, TextAnchor.UpperLeft, FontStyle.Normal);
            title = Text("Title", new Vector3(0.20f, 0.18f, 0), 0.009f, Palette.White, TextAnchor.UpperLeft, FontStyle.Bold);
            hint = Text("Hint", new Vector3(0.20f, 0.135f, 0), 0.0042f, Palette.White * 0.85f, TextAnchor.UpperLeft, FontStyle.Normal);
            list = Text("List", new Vector3(0.20f, 0.085f, 0), 0.0042f, Palette.Yellow, TextAnchor.UpperLeft, FontStyle.Normal);
            warning = Text("Warning", new Vector3(0, 0.03f, 0), 0.007f, Palette.Red, TextAnchor.MiddleCenter, FontStyle.Bold);
            subtitle = Text("Subtitle", new Vector3(0, -0.17f, 0), 0.0048f, Palette.White, TextAnchor.MiddleCenter, FontStyle.Italic);
            toastBig = Text("ToastBig", new Vector3(0, 0.07f, 0), 0.012f, Palette.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastSmall = Text("ToastSmall", new Vector3(0, 0.02f, 0), 0.005f, Palette.White, TextAnchor.MiddleCenter, FontStyle.Normal);
            health = Text("Health", new Vector3(-0.42f, -0.2f, 0), 0.006f, Palette.Blue, TextAnchor.LowerLeft, FontStyle.Bold);

            readout = Text("Readout", new Vector3(0.30f, -0.12f, 0), 0.0028f, Palette.White * 0.55f, TextAnchor.UpperLeft, FontStyle.Normal);
            location = Text("Location", new Vector3(-0.44f, 0.215f, 0), 0.0036f, Palette.White * 0.8f, TextAnchor.UpperLeft, FontStyle.Bold);
            BuildFrame();
            barBack = Build.Box("BarBack", Mats.Additive(Palette.White * 0.15f), panel, new Vector3(0.32f, 0.228f, 0), new Vector3(0.24f, 0.003f, 0.001f)).transform;
            barFill = Build.Box("BarFill", Mats.Additive(Palette.Yellow), panel, new Vector3(0.2f, 0.228f, 0), new Vector3(0.001f, 0.004f, 0.001f)).transform;
            SetProgress(-1f);
            warning.text = subtitle.text = toastBig.text = toastSmall.text = "";
        }

        /// <summary>Control-style chrome: corner brackets, centre reticle, segmented resonance meter.</summary>
        void BuildFrame()
        {
            float w = 0.47f, h = 0.25f, l = 0.05f;
            Vector3[][] corners =
            {
                new[] { new Vector3(-w, h - l, 0), new Vector3(-w, h, 0), new Vector3(-w + l, h, 0) },
                new[] { new Vector3(w - l, h, 0), new Vector3(w, h, 0), new Vector3(w, h - l, 0) },
                new[] { new Vector3(-w, -h + l, 0), new Vector3(-w, -h, 0), new Vector3(-w + l, -h, 0) },
                new[] { new Vector3(w - l, -h, 0), new Vector3(w, -h, 0), new Vector3(w, -h + l, 0) },
            };
            foreach (var c in corners)
            {
                var lr = Build.Line("Bracket", panel, Palette.White * 0.35f, 0.0012f);
                lr.useWorldSpace = false; lr.positionCount = 3; lr.SetPositions(c);
                frame.Add(lr);
            }
            reticle = Build.Line("Reticle", panel, Palette.White * 0.45f, 0.0008f);
            reticle.useWorldSpace = false; reticle.loop = true; reticle.positionCount = 32;
            for (int i = 0; i < 32; i++) { float a = i / 32f * Mathf.PI * 2f; reticle.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.018f); }
            for (int i = 0; i < 4; i++)
            {
                var t = Build.Line("Tick", panel, Palette.White * 0.45f, 0.0008f);
                t.useWorldSpace = false;
                Vector3 d = Quaternion.Euler(0, 0, i * 90f) * Vector3.right;
                t.SetPositions(new[] { d * 0.024f, d * 0.034f });
            }
            segOn = Mats.Additive(Palette.Blue * 0.9f);
            segOff = Mats.Additive(Palette.White * 0.08f);
            for (int i = 0; i < 12; i++)
            {
                var b = Build.Box("Seg", segOff, panel, new Vector3(-0.077f + i * 0.014f, 0.22f, 0), new Vector3(0.011f, 0.006f, 0.001f));
                segments.Add(b.GetComponent<MeshRenderer>());
            }
        }

        TextMesh Text(string name, Vector3 pos, float size, Color c, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panel, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMesh>();
            t.font = font;
            t.fontSize = 64;
            t.characterSize = size;
            t.color = c;
            t.anchor = anchor;
            t.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            t.fontStyle = style;
            var r = go.GetComponent<MeshRenderer>();
            if (font != null) r.sharedMaterial = font.material;
            return t;
        }

        void LateUpdate()
        {
            var head = HandInput.Head;
            if (head == null) return;
            Vector3 fwd = Vec.FlatNorm(head.forward);
            Vector3 want = head.position + fwd * Distance + Vector3.down * 0.05f;
            // Only recentre when the view has drifted, so text doesn't swim with every head motion.
            float off = anchor == Vector3.zero ? 180f : Vector3.Angle(anchor - head.position, head.forward);
            if (off > 18f) recentre = true;
            else if (off < 3f && Vector3.Distance(anchor, want) < 0.1f) recentre = false;
            if (recentre)
            {
                anchor = Vector3.Lerp(anchor == Vector3.zero ? want : anchor, want, Time.deltaTime * 2.5f);
                anchorRot = Quaternion.Slerp(anchorRot, Quaternion.LookRotation(fwd), Time.deltaTime * 2.5f);
            }
            panel.SetPositionAndRotation(anchor, anchorRot);

            if (lineTimer > 0) lineTimer -= Time.deltaTime;
            else if (lines.Count > 0) { var (l, s) = lines.Dequeue(); subtitle.text = Wrap(l, 52); lineTimer = s; }
            else subtitle.text = "";

            if (toastTimer > 0) { toastTimer -= Time.deltaTime; if (toastTimer <= 0) toastBig.text = toastSmall.text = ""; }

            // resonance meter: how much power is in your hands right now
            float power = 0f;
            foreach (var hp in HandPowers.All) power = Mathf.Max(power, hp.Holding ? 0.75f : hp.Pulling ? 0.9f : hp.ShieldUp ? 1f : 0.15f);
            energy = Mathf.MoveTowards(energy, power, Time.deltaTime * 2f);
            int lit = Mathf.RoundToInt(energy * segments.Count);
            for (int i = 0; i < segments.Count; i++) segments[i].sharedMaterial = i < lit ? segOn : segOff;

            readoutTimer -= Time.deltaTime;
            if (readoutTimer <= 0)
            {
                readoutTimer = 0.35f;
                var hl = Arena.Placed ? Arena.HeadLocal : Vector3.zero;
                readout.text = $"RES FIELD   {Random.Range(92, 99)}.{Random.Range(0, 9)}%\nPOS  {hl.x:+0.00;-0.00} {hl.z:+0.00;-0.00}\nLINK XR-HANDS  {(HandInput.Left.tracked ? "L" : "-")}{(HandInput.Right.tracked ? "R" : "-")}\nANOMALY  {Random.Range(310, 999)} Hz";
            }
        }

        static string Wrap(string s, int width)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var words = s.Split(' ');
            var sb = new System.Text.StringBuilder();
            int line = 0;
            foreach (var w in words)
            {
                if (line + w.Length > width && line > 0) { sb.Append('\n'); line = 0; }
                if (line > 0) { sb.Append(' '); line++; }
                sb.Append(w);
                line += w.Length;
            }
            return sb.ToString();
        }

        void SetProgress(float p)
        {
            bool on = p >= 0;
            barBack.gameObject.SetActive(on);
            barFill.gameObject.SetActive(on);
            if (!on) return;
            p = Mathf.Clamp01(p);
            barFill.localScale = new Vector3(Mathf.Max(0.001f, 0.24f * p), 0.004f, 0.001f);
            barFill.localPosition = new Vector3(0.2f + 0.12f * p, 0.228f, 0);
        }

        // ---------------------------------------------------------------- static API
        public static void Section(string tag) { if (I) { I.header.text = tag; I.location.text = tag; } }

        /// <summary>Per-level accent colour (title bar, checklist, meter).</summary>
        public static void Accent(Color c)
        {
            if (!I) return;
            I.accent = c;
            I.list.color = c;
            I.header.color = c;
            I.segOn = Mats.Additive(c * 0.9f);
            I.barFill.GetComponent<MeshRenderer>().sharedMaterial = Mats.Additive(c);
        }

        public static void Objective(string name, string hintText, string[] items)
        {
            if (!I) return;
            I.title.text = name ?? "";
            I.hint.text = Wrap(hintText ?? "", 34);
            I.list.text = items == null ? "" : string.Join("\n", items);
            I.list.transform.localPosition = new Vector3(0.20f, 0.135f - 0.018f * Mathf.Max(1, I.hint.text.Split('\n').Length) - 0.012f, 0);
        }

        public static void Progress(float p) { if (I) I.SetProgress(p); }
        public static void Warning(string text) { if (I) I.warning.text = text ?? ""; }
        public static void Say(string line, float seconds = 4f) { if (I && Save.Subtitles) I.lines.Enqueue((line, seconds)); }
        public static void ClearSay() { if (I) { I.lines.Clear(); I.lineTimer = 0; } }

        public static void Toast(string big, string small, float seconds = 4f)
        {
            if (!I) return;
            I.toastBig.text = big;
            I.toastSmall.text = Wrap(small, 50);
            I.toastTimer = seconds;
        }

        public static void Health(int hp, int max)
        {
            if (!I) return;
            I.health.text = new string('■', Mathf.Max(0, hp)) + new string('□', Mathf.Max(0, max - hp));
            I.health.color = hp <= 1 ? Palette.Red : Palette.Blue;
        }

        public static string Check(bool done, string text) => (done ? "■ " : "□ ") + text;
    }
}
