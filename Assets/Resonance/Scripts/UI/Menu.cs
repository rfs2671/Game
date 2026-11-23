using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>World-space button operated by hand: aim and pinch, or poke it with a fingertip. Mouse click in the simulator.</summary>
    public class MenuButton : MonoBehaviour
    {
        public Action onClick;
        public bool Hovered { get; private set; }
        TextMesh label;
        MeshRenderer bg;
        BoxCollider col;
        Material idle, hover;
        float cooldown;

        public static MenuButton Create(Transform parent, Vector3 local, string text, float width = 0.34f)
        {
            var go = new GameObject("Btn_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var b = go.AddComponent<MenuButton>();
            b.idle = Mats.Additive(Palette.White * 0.07f);
            b.hover = Mats.Additive(Menu.Accent * 0.35f);
            b.bg = Build.Box("Bg", b.idle, go.transform, Vector3.zero, new Vector3(width, 0.048f, 0.002f)).GetComponent<MeshRenderer>();
            Build.Box("Edge", Mats.Additive(Menu.Accent * 0.8f), go.transform, new Vector3(-width * 0.5f + 0.002f, 0, -0.002f), new Vector3(0.003f, 0.048f, 0.001f));
            b.col = go.AddComponent<BoxCollider>();
            b.col.size = new Vector3(width, 0.048f, 0.03f);
            b.label = Menu.Text(go.transform, new Vector3(-width * 0.5f + 0.018f, 0, -0.004f), text, 0.0042f, Palette.White, TextAnchor.MiddleLeft, FontStyle.Bold);
            return b;
        }

        public void SetText(string t) => label.text = t;

        void Update()
        {
            cooldown -= Time.unscaledDeltaTime;
            bool hov = false, click = false;
            foreach (var h in new[] { HandInput.Right, HandInput.Left })
            {
                if (h == null || !h.tracked) continue;
                var ray = new Ray(h.aimOrigin - h.aimDir * 0.05f, h.aimDir);
                bool aimed = col.Raycast(ray, out RaycastHit hit, 6f);
                bool poked = col.bounds.Contains(h.indexTip);
                if (aimed) Menu.ReportHover(h, hit.point);
                if (aimed || poked) { hov = true; if (h.pinchDown || (poked && !Hovered)) click = true; }
            }
            Hovered = hov;
            bg.sharedMaterial = hov ? hover : idle;
            label.color = hov ? Color.Lerp(Palette.White, Menu.Accent, 0.5f) : Palette.White;
            if (click && cooldown <= 0)
            {
                cooldown = 0.35f;
                Sfx.Play(Sound.Button, transform.position, 0.9f);
                onClick?.Invoke();
            }
        }
    }

    /// <summary>
    /// Title screen, level select, settings, how-to-play, pause and credits. All world-space, hand-operated,
    /// usable while the game is paused (timeScale 0).
    /// </summary>
    public static class Menu
    {
        public static Color Accent = new Color(1f, 0.25f, 0.12f);
        /// <summary>True while any menu is open: gameplay input is ignored.</summary>
        public static bool Blocking { get; private set; }

        static Transform root;
        static LineRenderer pointer;
        static GameObject cursor;
        static float hoverFrame = -1;
        static Font font;

        public static TextMesh Text(Transform parent, Vector3 pos, string text, float size, Color c, TextAnchor anchor, FontStyle style)
        {
            if (font == null)
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMesh>();
            t.font = font; t.fontSize = 64; t.characterSize = size; t.color = c; t.anchor = anchor; t.fontStyle = style; t.text = text;
            t.alignment = anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.UpperCenter ? TextAlignment.Center : TextAlignment.Left;
            if (font) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return t;
        }

        public static void ReportHover(HandState h, Vector3 point)
        {
            hoverFrame = Time.frameCount;
            if (pointer == null || cursor == null) return;
            pointer.enabled = !h.simulated;
            pointer.SetPosition(0, h.aimOrigin);
            pointer.SetPosition(1, point);
            cursor.SetActive(true);
            cursor.transform.position = point;
        }

        static void Open(string title, string body, out Transform panel, float bodyLines = 0)
        {
            Close();
            Blocking = true;
            var head = HandInput.Head;
            root = new GameObject("Menu").transform;
            Vector3 fwd = Vec.FlatNorm(head.forward);
            root.position = head.position + fwd * 1.1f + Vector3.down * 0.08f;
            root.rotation = Quaternion.LookRotation(fwd);
            panel = root;
            Text(root, new Vector3(-0.17f, 0.25f, 0), title, 0.011f, Palette.White, TextAnchor.UpperLeft, FontStyle.Bold);
            Build.Box("Bar", Mats.Additive(Accent), root, new Vector3(-0.135f, 0.268f, 0), new Vector3(0.07f, 0.004f, 0.001f));
            if (!string.IsNullOrEmpty(body))
                Text(root, new Vector3(-0.17f, 0.17f, 0), body, 0.0036f, Palette.White * 0.85f, TextAnchor.UpperLeft, FontStyle.Normal);
            pointer = Build.Line("Pointer", root, Accent * 0.7f, 0.003f);
            cursor = Build.Shape("Cursor", Meshes.Sphere, Mats.Glow(Accent * 1.4f), root, Vector3.zero, Vector3.one * 0.012f);
            cursor.SetActive(false);
            root.gameObject.AddComponent<MenuPointerReset>();
        }

        class MenuPointerReset : MonoBehaviour
        {
            void LateUpdate()
            {
                if (Time.frameCount - hoverFrame > 1) { if (pointer) pointer.enabled = false; if (cursor) cursor.SetActive(false); }
            }
        }

        public static void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
            Blocking = false;
        }

        /// <summary>Show a list of options; yields until one is chosen. result receives its index.</summary>
        public static IEnumerator Choose(string title, string body, string[] options, Action<int> result, float startY = 0.1f)
        {
            Open(title, body, out var panel);
            int chosen = -1;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = MenuButton.Create(panel, new Vector3(0, startY - i * 0.06f, 0), options[i]);
                b.onClick = () => chosen = idx;
            }
            while (chosen < 0) yield return null;
            Close();
            result(chosen);
        }

        // ---------------------------------------------------------------- screens
        public static IEnumerator Title(Action<int> startLevel)
        {
            var vortex = Vortex.Spawn(HandInput.Head.position + Vec.FlatNorm(HandInput.Head.forward) * 3.2f + Vector3.up * 0.2f,
                -Vec.FlatNorm(HandInput.Head.forward), new Color(1f, 0.25f, 0.1f), 1.3f, 0f, 80, true);
            Sfx.Ambience("amb_station", 0.3f);
            while (true)
            {
                var opts = new List<string>();
                var acts = new List<int>();
                if (Save.HasProgress) { opts.Add(Save.Completed ? "REPLAY ACT I" : $"CONTINUE  ·  LEVEL {Save.LastLevel}"); acts.Add(0); }
                opts.Add("NEW GAME"); acts.Add(1);
                if (Save.Unlocked > 0) { opts.Add("LEVEL SELECT"); acts.Add(2); }
                opts.Add("HOW TO PLAY"); acts.Add(3);
                opts.Add("SETTINGS"); acts.Add(4);
                opts.Add("SYSTEM CHECK"); acts.Add(5);
                opts.Add("QUIT"); acts.Add(6);
                int pick = -1;
                yield return Choose("RESONANCE", "ACT I  ·  THE BLACK QUARRY\nAim with your hand and pinch, or touch a button.", opts.ToArray(), i => pick = acts[i], 0.06f);
                switch (pick)
                {
                    case 0: Finish(vortex); startLevel(Save.Completed ? 0 : Save.LastLevel); yield break;
                    case 1:
                        if (Save.HasProgress)
                        {
                            int c = -1;
                            yield return Choose("NEW GAME", "This clears your progress.", new[] { "START OVER", "BACK" }, i => c = i);
                            if (c != 0) break;
                        }
                        Save.ResetProgress();
                        Finish(vortex); startLevel(0); yield break;
                    case 2:
                        {
                            string[] names = { "0 · THE QUARRY", "1 · THE SURVEY TUNNELS", "2 · THE RESEARCH STATION", "3 · THE FRACTURE CHAMBER" };
                            var list = new List<string>();
                            for (int l = 0; l <= Save.Unlocked; l++) list.Add(names[l]);
                            list.Add("BACK");
                            int c = -1;
                            yield return Choose("LEVEL SELECT", "", list.ToArray(), i => c = i);
                            if (c >= 0 && c < list.Count - 1) { Finish(vortex); startLevel(c); yield break; }
                            break;
                        }
                    case 3: yield return HowToPlay(); break;
                    case 4: yield return Settings(); break;
                    case 5: Finish(vortex); startLevel(-1); yield break;   // system check
                    case 6:
                        Application.Quit();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                        break;
                }
            }
        }

        static void Finish(Vortex v) { if (v != null) UnityEngine.Object.Destroy(v.gameObject); }

        public static IEnumerator HowToPlay()
        {
            int c = -1;
            yield return Choose("HOW TO PLAY",
                "Clear a 2 x 2 m space. Keep your hands in front of you at chest height.\n\n" +
                "PINCH + RELEASE ......... lift and throw (telekinesis)\n" +
                "OPEN PALM + THRUST ...... Force Push (both hands = heavy)\n" +
                "HOLD PALM STILL ......... Rock Shield (thrust to fire shards)\n" +
                "OPEN PALM → FIST ........ Gravity Pull (thrust to launch)\n" +
                "BOTH FISTS TOGETHER ..... Compression\n" +
                "TWIST WRIST ............. rotate held puzzle pieces\n" +
                "LOOK AT YOUR LEFT PALM .. pause (hold 1 s)",
                new[] { "BACK" }, i => c = i, -0.2f);
        }

        public static IEnumerator Settings()
        {
            while (true)
            {
                string[] sens = { "RELAXED", "NORMAL", "STRICT" };
                string[] look = { "AUTO", "CINEMATIC", "GLASSES" };
                var opts = new[]
                {
                    $"VOLUME  ·  {Mathf.RoundToInt(Save.Master * 100)}%",
                    $"AMBIENCE  ·  {Mathf.RoundToInt(Save.Ambience * 100)}%",
                    $"GESTURES  ·  {sens[Save.Sensitivity]}",
                    $"SUBTITLES  ·  {(Save.Subtitles ? "ON" : "OFF")}",
                    $"LOOK  ·  {look[Save.LookChoice + 1]}  (restart)",
                    "BACK"
                };
                int c = -1;
                yield return Choose("SETTINGS", "Tap an option to change it.", opts, i => c = i);
                switch (c)
                {
                    case 0: Save.Master = Save.Master >= 0.99f ? 0.2f : Save.Master + 0.1f; break;
                    case 1: Save.Ambience = Save.Ambience >= 0.99f ? 0f : Save.Ambience + 0.1f; break;
                    case 2: Save.Sensitivity = (Save.Sensitivity + 1) % 3; break;
                    case 3: Save.Subtitles = !Save.Subtitles; break;
                    case 4: Save.LookChoice = Save.LookChoice >= 1 ? -1 : Save.LookChoice + 1; break;
                    default: yield break;
                }
            }
        }

        public static IEnumerator Credits()
        {
            int c = -1;
            yield return Choose("ACT I COMPLETE",
                "RESONANCE  ·  THE BLACK QUARRY\n\nDesign  ·  Roy\nCode, sound and effects  ·  built with Claude\nHand model  ·  W3C WebXR generic hand (W3C licence)\n\nThank you for playing.",
                new[] { "BACK TO TITLE" }, i => c = i, -0.12f);
        }
    }

    /// <summary>Pause: look at your left palm (palm facing your eyes) for a second, or press Esc in the simulator.</summary>
    public class PauseWatcher : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        public static Action OnRestart, OnQuitToTitle;
        float hold;
        LineRenderer ring;

        void Start()
        {
            ring = Build.Line("PauseRing", transform, Menu.Accent, 0.004f);
            ring.positionCount = 0;
        }

        void Update()
        {
            if (Paused || Menu.Blocking || !Arena.Placed) { hold = 0; ring.positionCount = 0; return; }
            var h = HandInput.Left;
            bool look = false;
            if (h.tracked && !h.simulated)
            {
                Vector3 toEye = (HandInput.Head.position - h.palm).normalized;
                look = h.openPalm && Vector3.Dot(h.palmNormal, toEye) > 0.75f && h.velocity.magnitude < 0.3f;
            }
            if (HandInput.Simulated && SimIn.Down(KeyCode.Escape)) hold = 1f;
            hold = look ? hold + Time.unscaledDeltaTime : (hold >= 1f ? hold : 0f);
            DrawRing(h, Mathf.Clamp01(hold));
            if (hold >= 1f) { hold = 0; StartCoroutine(PauseMenu()); }
        }

        void DrawRing(HandState h, float k)
        {
            if (k <= 0.05f) { ring.positionCount = 0; return; }
            int n = Mathf.Max(2, Mathf.RoundToInt(32 * k));
            ring.positionCount = n;
            var rot = Quaternion.LookRotation(h.palmNormal);
            for (int i = 0; i < n; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                ring.SetPosition(i, h.palm + h.palmNormal * 0.04f + rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.06f);
            }
        }

        IEnumerator PauseMenu()
        {
            Paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ring.positionCount = 0;
            while (true)
            {
                int c = -1;
                yield return Menu.Choose("PAUSED", "", new[] { "RESUME", "RESTART LEVEL", "HOW TO PLAY", "SETTINGS", "QUIT TO TITLE" }, i => c = i);
                if (c == 2) { yield return Menu.HowToPlay(); continue; }
                if (c == 3) { yield return Menu.Settings(); continue; }
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Paused = false;
                if (c == 1) OnRestart?.Invoke();
                if (c == 4) OnQuitToTitle?.Invoke();
                yield break;
            }
        }
    }
}
