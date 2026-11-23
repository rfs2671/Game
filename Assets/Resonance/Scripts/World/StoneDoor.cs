using UnityEngine;

namespace Resonance
{
    /// <summary>Huge stone door. Place both keystones in the frame sockets with telekinesis, then Force Push it open.</summary>
    public class StoneDoor : MonoBehaviour, IPushable
    {
        public System.Action OnOpened;
        public System.Action<int> OnSocketFilled;
        public int Filled { get; private set; }
        public bool Ready => Filled >= sockets.Length;
        public bool Open { get; private set; }

        Transform slab;
        Transform[] sockets;
        bool[] full;
        LineRenderer[] rings;
        GameObject glyphs;
        float rise, sink, shake;
        Vector3 slabHome;

        public static StoneDoor Spawn(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("StoneDoor");
            go.transform.SetPositionAndRotation(pos + Vector3.down * 3.2f, rot);
            var d = go.AddComponent<StoneDoor>();
            d.riseTarget = pos;
            var stone = Mats.Lit(Palette.Stone, Palette.Blue * 0.05f);
            Build.Box("PillarL", stone, go.transform, new Vector3(-1.45f, 1.6f, 0), new Vector3(0.6f, 3.2f, 0.7f), true);
            Build.Box("PillarR", stone, go.transform, new Vector3(1.45f, 1.6f, 0), new Vector3(0.6f, 3.2f, 0.7f), true);
            Build.Box("Lintel", stone, go.transform, new Vector3(0, 3.35f, 0), new Vector3(3.5f, 0.5f, 0.8f), true);
            d.slab = Build.Box("Slab", Mats.Lit(Palette.StoneLight, Palette.Blue * 0.08f), go.transform, new Vector3(0, 1.55f, 0),
                new Vector3(2.3f, 3.1f, 0.35f), true).transform;
            d.slabHome = d.slab.localPosition;

            d.glyphs = new GameObject("Glyphs");
            d.glyphs.transform.SetParent(d.slab, false);
            var gm = Mats.Glow(Palette.Yellow);
            Build.Box("G1", gm, d.glyphs.transform, new Vector3(0, 0.1f, -0.52f), new Vector3(0.04f, 0.5f, 0.02f));
            Build.Box("G2", gm, d.glyphs.transform, new Vector3(-0.12f, -0.06f, -0.52f), new Vector3(0.04f, 0.3f, 0.02f), false, Quaternion.Euler(0, 0, 50));
            Build.Box("G3", gm, d.glyphs.transform, new Vector3(0.12f, -0.06f, -0.52f), new Vector3(0.04f, 0.3f, 0.02f), false, Quaternion.Euler(0, 0, -50));
            d.glyphs.SetActive(false);

            d.sockets = new Transform[2];
            d.full = new bool[2];
            d.rings = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var s = new GameObject("Socket" + i).transform;
                s.SetParent(go.transform, false);
                s.localPosition = new Vector3(i == 0 ? -1.45f : 1.45f, 1.25f, -0.5f);
                d.sockets[i] = s;
                Build.Box("Recess", Mats.Glow(Palette.BlueDeep), s, new Vector3(0, 0, 0.13f), new Vector3(0.42f, 0.32f, 0.02f));
                var lr = Build.Line("Ring", s, Palette.Blue, 0.01f);
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = 4;
                lr.SetPositions(new[] { new Vector3(-0.24f, -0.18f, 0.1f), new Vector3(-0.24f, 0.18f, 0.1f), new Vector3(0.24f, 0.18f, 0.1f), new Vector3(0.24f, -0.18f, 0.1f) });
                d.rings[i] = lr;
            }
            Sfx.Play(Sound.Rumble, pos, 1f);
            return d;
        }

        Vector3 riseTarget;

        void Update()
        {
            float dt = Time.deltaTime;
            if (rise < 1f)
            {
                rise = Mathf.Min(1f, rise + dt / 2.5f);
                transform.position = Vector3.Lerp(riseTarget + Vector3.down * 3.2f, riseTarget, Mathf.SmoothStep(0, 1, rise));
            }

            if (Open)
            {
                sink = Mathf.Min(1f, sink + dt / 2.2f);
                slab.localPosition = slabHome + Vector3.down * 3.3f * Mathf.SmoothStep(0, 1, sink);
                if (sink >= 1f && slab.gameObject.activeSelf) slab.gameObject.SetActive(false);
                return;
            }

            shake -= dt;
            slab.localPosition = slabHome + (shake > 0 ? Random.insideUnitSphere * 0.02f : Vector3.zero);

            for (int i = 0; i < sockets.Length; i++)
            {
                if (full[i]) continue;
                Vector3 sp = sockets[i].position;
                bool near = false;
                foreach (var t in Throwable.All)
                {
                    if (!t.isKey || t.locked) continue;
                    float d = Vector3.Distance(t.transform.position, sp);
                    if (d < 0.6f) near = true;
                    if (d < 0.42f)
                    {
                        t.Lock(sp, sockets[i].rotation);
                        full[i] = true;
                        Filled++;
                        rings[i].startColor = rings[i].endColor = Palette.Yellow;
                        Build.Burst(sp, Palette.Yellow, 50, 2f);
                        Sfx.Play(Sound.Insert, sp, 1f);
                        OnSocketFilled?.Invoke(Filled);
                        if (Ready) { glyphs.SetActive(true); Sfx.Play(Sound.Thump, slab.position, 0.8f); }
                        break;
                    }
                }
                if (!full[i])
                {
                    float pulse = near ? 1.5f + Mathf.Sin(Time.time * 12f) * 0.5f : 0.6f + Mathf.Sin(Time.time * 3f) * 0.2f;
                    rings[i].startColor = rings[i].endColor = Palette.Blue * pulse;
                }
            }
        }

        public void Push(Vector3 impulse, bool heavy)
        {
            if (Open || rise < 1f) return;
            if (!Ready)
            {
                shake = 0.3f;
                Sfx.Play(Sound.Thump, slab.position, 0.5f);
                return;
            }
            Open = true;
            Impact.Spawn(slab.position, -transform.forward, Palette.Blue, ImpactKind.Heavy, 2f);
            Sfx.Play(Sound.Door, slab.position, 1f);
            OnOpened?.Invoke();
        }
    }
}
