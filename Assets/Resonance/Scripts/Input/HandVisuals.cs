using UnityEngine;
#if RESONANCE_XRHANDS
using System.Collections.Generic;
using UnityEngine.XR.Hands;
#endif

namespace Resonance
{
    /// <summary>
    /// Draws the tracked hands: 26 joint spheres plus thin bones per hand, built once and only moved afterwards.
    /// Poses go to world space with the same transform HandInput.TryPose uses. Hidden when a hand is not tracked.
    /// Without XR Hands (editor simulator) it does nothing.
    /// </summary>
    public class HandVisuals : MonoBehaviour
    {
        /// <summary>Master toggle, default on.</summary>
        public static bool Visible = true;

#if RESONANCE_XRHANDS
        const int JointCount = 26;   // Wrist .. LittleTip
        const float JointRadius = 0.007f, TipRadius = 0.005f, BoneWidth = 0.004f;

        // Bone chains as joint-id pairs (parent, child).
        static readonly XRHandJointID[,] Bones =
        {
            { XRHandJointID.Wrist, XRHandJointID.Palm },
            { XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal },
            { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal },
            { XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal },
            { XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            { XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal },
            { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal },
            { XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate },
            { XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal },
            { XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            { XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal },
            { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal },
            { XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate },
            { XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal },
            { XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            { XRHandJointID.Wrist, XRHandJointID.RingMetacarpal },
            { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal },
            { XRHandJointID.RingProximal, XRHandJointID.RingIntermediate },
            { XRHandJointID.RingIntermediate, XRHandJointID.RingDistal },
            { XRHandJointID.RingDistal, XRHandJointID.RingTip },
            { XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal },
            { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal },
            { XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate },
            { XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal },
            { XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };

        class Rig
        {
            public GameObject root;
            public Transform[] joints = new Transform[JointCount];
            public MeshRenderer[] jointRenders = new MeshRenderer[JointCount];
            public LineRenderer[] lines;
            public Vector3[] pos = new Vector3[JointCount];
            public bool[] have = new bool[JointCount];
            public int mode = -1;     // 0 normal, 1 active (pinch / fist)
            public bool shown;
        }

        readonly Rig[] rigs = new Rig[2];
        XRHandSubsystem sub;
        static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
        Material[,] jointMats;   // [hand, mode]
        Color[,] lineCols;

        static int Idx(XRHandJointID id) => (int)id - (int)XRHandJointID.Wrist;

        void Awake()
        {
            jointMats = new Material[2, 2];
            lineCols = new Color[2, 2];
            Color[] baseCol = { Palette.Blue, Palette.Yellow };
            for (int h = 0; h < 2; h++)
            {
                Color bright = Color.Lerp(baseCol[h], Palette.White, 0.5f);
                jointMats[h, 0] = Mats.Additive(baseCol[h] * 0.45f);
                jointMats[h, 1] = Mats.Additive(bright * 0.9f);
                lineCols[h, 0] = baseCol[h] * 0.4f;
                lineCols[h, 1] = bright * 0.85f;
                rigs[h] = BuildRig(h == 0 ? "LeftHandVisual" : "RightHandVisual", h);
            }
        }

        Rig BuildRig(string name, int h)
        {
            var r = new Rig { root = new GameObject(name) };
            r.root.transform.SetParent(transform, false);
            for (int i = 0; i < JointCount; i++)
            {
                var id = (XRHandJointID)(i + (int)XRHandJointID.Wrist);
                bool tip = id == XRHandJointID.ThumbTip || id == XRHandJointID.IndexTip || id == XRHandJointID.MiddleTip
                           || id == XRHandJointID.RingTip || id == XRHandJointID.LittleTip;
                float d = (tip ? TipRadius : JointRadius) * 2f;
                var go = Build.Shape("J" + i, Meshes.Sphere, jointMats[h, 0], r.root.transform, Vector3.zero, Vector3.one * d);
                r.joints[i] = go.transform;
                r.jointRenders[i] = go.GetComponent<MeshRenderer>();
            }
            r.lines = new LineRenderer[Bones.GetLength(0)];
            for (int b = 0; b < r.lines.Length; b++)
                r.lines[b] = Build.Line("B" + b, r.root.transform, lineCols[h, 0], BoneWidth);
            r.root.SetActive(false);
            return r;
        }

        void LateUpdate()
        {
            var hi = HandInput.I;
            if (!Visible || hi == null || hi.PoseSpace == null) { HideAll(); return; }
            if (sub == null || !sub.running)
            {
                sub = null;
                SubsystemManager.GetSubsystems(subs);
                foreach (var s in subs) if (s.running) { sub = s; break; }
            }
            if (sub == null) { HideAll(); return; }
            Draw(0, sub.leftHand, hi.PoseSpace);
            Draw(1, sub.rightHand, hi.PoseSpace);
        }

        void HideAll()
        {
            for (int h = 0; h < 2; h++) SetShown(rigs[h], false);
        }

        static void SetShown(Rig r, bool on)
        {
            if (r == null || r.shown == on) return;
            r.shown = on;
            r.root.SetActive(on);
        }

        void Draw(int h, XRHand hand, Transform space)
        {
            var r = rigs[h];
            if (!hand.isTracked) { SetShown(r, false); return; }

            // Same transform as HandInput.TryPose: space.TransformPoint(joint position).
            int got = 0;
            for (int i = 0; i < JointCount; i++)
            {
                var id = (XRHandJointID)(i + (int)XRHandJointID.Wrist);
                if (hand.GetJoint(id).TryGetPose(out Pose p))
                {
                    Vector3 w = space.TransformPoint(p.position);
                    r.pos[i] = w;
                    r.have[i] = true;
                    r.joints[i].position = w;
                    if (!r.jointRenders[i].enabled) r.jointRenders[i].enabled = true;
                    got++;
                }
                else
                {
                    r.have[i] = false;
                    if (r.jointRenders[i].enabled) r.jointRenders[i].enabled = false;
                }
            }
            if (got == 0) { SetShown(r, false); return; }
            SetShown(r, true);

            for (int b = 0; b < r.lines.Length; b++)
            {
                int a = Idx(Bones[b, 0]), c = Idx(Bones[b, 1]);
                bool ok = r.have[a] && r.have[c];
                if (r.lines[b].enabled != ok) r.lines[b].enabled = ok;
                if (ok) { r.lines[b].SetPosition(0, r.pos[a]); r.lines[b].SetPosition(1, r.pos[c]); }
            }

            var st = HandInput.Get(h == 0 ? Hand.Left : Hand.Right);
            int mode = st != null && (st.pinching || st.fist) ? 1 : 0;
            if (mode != r.mode)
            {
                r.mode = mode;
                for (int i = 0; i < JointCount; i++) r.jointRenders[i].sharedMaterial = jointMats[h, mode];
                for (int b = 0; b < r.lines.Length; b++) r.lines[b].startColor = r.lines[b].endColor = lineCols[h, mode];
            }
        }
#endif
    }
}
