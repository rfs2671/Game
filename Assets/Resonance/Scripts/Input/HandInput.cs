using System.Collections.Generic;
using UnityEngine;
#if RESONANCE_XRHANDS
using UnityEngine.XR.Hands;
#endif
#if RESONANCE_ARF
using Unity.XR.CoreUtils;
#endif

namespace Resonance
{
    public enum Hand { Left = 0, Right = 1 }

    /// <summary>All gesture thresholds in one place. Tune these on device.</summary>
    public static class Tuning
    {
        public static float PinchOn = 0.022f;          // thumb-index distance to start a pinch (m)
        public static float PinchOff = 0.042f;         // distance to release (hysteresis)
        public static float FingerExtended = 0.072f;   // fingertip-to-palm distance counted as extended (m)
        public static float FingerCurled = 0.052f;     // fingertip-to-palm distance counted as curled (fist)
        public static float GrabAirWindow = 0.4f;      // open palm -> fist within this time = Gravity Pull
        public static float CompressStart = 0.32f, CompressEnd = 0.16f, CompressWindow = 0.7f;
        public static float ThrustSpeed = 1.1f;        // palm speed along palm normal for Force Push (m/s)
        public static float ThrustCooldown = 0.45f;
        public static float ShieldMaxSpeed = 0.45f;    // palm must be roughly still to hold a shield
        public static float ShieldFacing = 0.55f;      // dot(palm normal, head forward)
        public static float ShieldHoldTime = 0.25f;
        public static float ThrowSpeed = 0.55f;        // forward hand speed at release that counts as a throw
        public static float ShoulderDrop = 0.18f, ShoulderWidth = 0.17f;
        public static bool InvertPalmNormal = false;   // flip if push/shield fire backwards on your runtime
    }

    public class HandState
    {
        public Hand side;
        public bool tracked, simulated;
        public Vector3 palm, palmNormal, wrist, indexTip, thumbTip, pinchPoint;
        public Vector3 aimOrigin, aimDir = Vector3.forward, velocity;
        public float pinchDistance = 1f;
        public bool pinching, pinchDown, pinchUp;
        public bool openPalm, shieldPose, thrust;
        public bool fist, fistDown, fistUp, grabAir;
        public Quaternion palmRot = Quaternion.identity;
        public float lastOpenTime = -10f;
        public float shieldHeld, lastThrustTime = -10f, lastTrackedTime = -10f;
        public bool simPlace;               // simulator: release without throwing
        internal Vector3 prevPalm;
        internal bool hadPrev;

        public bool IsThrowRelease => simulated ? !simPlace : Vector3.Dot(velocity, aimDir) > Tuning.ThrowSpeed;
        public float ForwardSpeed => Mathf.Max(0, Vector3.Dot(velocity, aimDir));
    }

    /// <summary>
    /// Reads XR Hands joints (XREAL SDK 3.x feeds XR Hands on Air 2 Ultra) and turns them into
    /// game gestures: pinch, open palm, thrust (Force Push) and held palm (Rock Shield).
    /// With no hand subsystem running (Unity editor) it falls back to a mouse/keyboard simulator.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HandInput : MonoBehaviour
    {
        public static HandInput I { get; private set; }
        public static HandState Left => I.hands[0];
        public static HandState Right => I.hands[1];
        public static HandState Get(Hand h) => I.hands[(int)h];
        public static Transform Head => I.head;
        public static bool Simulated => I.simulating;
        /// <summary>True on the frame both hands are brought together while closed (Compression).</summary>
        public static bool CompressGesture { get; private set; }
        public static Vector3 CompressPoint { get; private set; }
        float compressArmedTime = -10f;

        [Tooltip("Use mouse/keyboard even if a hand subsystem is running.")]
        public bool forceSimulator = false;

        readonly HandState[] hands = { new HandState { side = Hand.Left }, new HandState { side = Hand.Right } };
        Transform head, space;
        bool simulating;
        float simReach = 0.5f, simPitch;

#if RESONANCE_XRHANDS
        XRHandSubsystem sub;
        static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
#endif

        void Awake() { I = this; }

        void Update()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            ResolveSpace();

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var h in hands) { h.pinchDown = h.pinchUp = h.thrust = h.fistDown = h.fistUp = h.grabAir = false; }
            CompressGesture = false;

            bool real = !forceSimulator && ReadReal();
            simulating = !real;
            if (simulating) Simulate(dt);

            foreach (var h in hands)
            {
                UpdateVelocity(h, dt);
                if (!h.simulated) DetectGestures(h, dt);
            }
            if (!simulating) DetectCompress();
        }

        void ResolveSpace()
        {
            if (space != null) return;
#if RESONANCE_ARF
            var origin = Find.One<XROrigin>();
            if (origin != null)
                space = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
#endif
            if (space == null) space = head.parent != null ? head.parent : head;
        }

        Transform RigRoot()
        {
            Transform t = head;
            while (t.parent != null) t = t.parent;
            return t;
        }

        // ---------------------------------------------------------------- real hands
        bool ReadReal()
        {
#if RESONANCE_XRHANDS
            if (sub == null || !sub.running)
            {
                sub = null;
                SubsystemManager.GetSubsystems(subs);
                foreach (var s in subs) if (s.running) { sub = s; break; }
            }
            if (sub == null) return false;
            Read(sub.leftHand, hands[0]);
            Read(sub.rightHand, hands[1]);
            // In the editor, only trust the subsystem once it has actually tracked a hand.
            if (Application.isEditor && Time.time - Mathf.Max(hands[0].lastTrackedTime, hands[1].lastTrackedTime) > 5f)
                return false;
            return true;
#else
            return false;
#endif
        }

#if RESONANCE_XRHANDS
        void Read(XRHand hand, HandState s)
        {
            s.simulated = false;
            bool wasTracked = s.tracked;
            s.tracked = hand.isTracked;
            if (!s.tracked)
            {
                if (s.pinching) { s.pinching = false; s.pinchUp = true; }
                s.openPalm = s.shieldPose = false;
                s.shieldHeld = 0;
                s.hadPrev = false;
                return;
            }
            if (!wasTracked) s.hadPrev = false;
            s.lastTrackedTime = Time.time;

            if (TryPose(hand, XRHandJointID.Palm, out var palm))
            {
                s.palm = palm.position;
                Vector3 n = -(palm.rotation * Vector3.up);
                s.palmNormal = Tuning.InvertPalmNormal ? -n : n;
            }
            if (TryPose(hand, XRHandJointID.Wrist, out var wrist)) s.wrist = wrist.position;
            if (TryPose(hand, XRHandJointID.ThumbTip, out var thumb)) s.thumbTip = thumb.position;
            if (TryPose(hand, XRHandJointID.IndexTip, out var index)) s.indexTip = index.position;

            int extended = 0;
            if (Extended(hand, XRHandJointID.IndexTip, s.palm)) extended++;
            if (Extended(hand, XRHandJointID.MiddleTip, s.palm)) extended++;
            if (Extended(hand, XRHandJointID.RingTip, s.palm)) extended++;
            if (Extended(hand, XRHandJointID.LittleTip, s.palm)) extended++;

            int curled = 0;
            if (Curled(hand, XRHandJointID.IndexTip, s.palm)) curled++;
            if (Curled(hand, XRHandJointID.MiddleTip, s.palm)) curled++;
            if (Curled(hand, XRHandJointID.RingTip, s.palm)) curled++;
            if (Curled(hand, XRHandJointID.LittleTip, s.palm)) curled++;
            if (TryPose(hand, XRHandJointID.Palm, out var pr)) s.palmRot = pr.rotation;

            s.pinchDistance = Vector3.Distance(s.thumbTip, s.indexTip);
            s.pinchPoint = (s.thumbTip + s.indexTip) * 0.5f;
            s.openPalm = extended >= 3 && s.pinchDistance > Tuning.PinchOff;
            bool fistNow = curled >= 3;
            if (fistNow && !s.fist) { s.fistDown = true; if (Time.time - s.lastOpenTime < Tuning.GrabAirWindow) s.grabAir = true; }
            if (!fistNow && s.fist) s.fistUp = true;
            s.fist = fistNow;
            if (s.openPalm) s.lastOpenTime = Time.time;
        }

        bool TryPose(XRHand hand, XRHandJointID id, out Pose world)
        {
            if (hand.GetJoint(id).TryGetPose(out Pose p))
            {
                world = new Pose(space.TransformPoint(p.position), space.rotation * p.rotation);
                return true;
            }
            world = default;
            return false;
        }

        bool Extended(XRHand hand, XRHandJointID tip, Vector3 palm)
            => TryPose(hand, tip, out var p) && Vector3.Distance(p.position, palm) > Tuning.FingerExtended;

        bool Curled(XRHand hand, XRHandJointID tip, Vector3 palm)
            => TryPose(hand, tip, out var p) && Vector3.Distance(p.position, palm) < Tuning.FingerCurled;
#endif

        void DetectGestures(HandState s, float dt)
        {
            if (!s.tracked) return;

            // Pinch with hysteresis.
            if (!s.pinching && s.pinchDistance < Tuning.PinchOn) { s.pinching = true; s.pinchDown = true; }
            else if (s.pinching && s.pinchDistance > Tuning.PinchOff) { s.pinching = false; s.pinchUp = true; }

            // Aim from an estimated shoulder through the pinch point: steadier than wrist rotation.
            Vector3 right = Vec.FlatNorm(head.right);
            Vector3 shoulder = head.position + Vector3.down * Tuning.ShoulderDrop
                               + right * (s.side == Hand.Right ? Tuning.ShoulderWidth : -Tuning.ShoulderWidth);
            s.aimOrigin = s.pinchPoint;
            Vector3 d = s.pinchPoint - shoulder;
            if (d.sqrMagnitude > 1e-4f) s.aimDir = d.normalized;

            Vector3 fwd = head.forward;
            float facing = Vector3.Dot(s.palmNormal, fwd);
            bool inFront = Vector3.Dot(s.palm - head.position, fwd) > 0.12f;

            // Thrust: open palm driven quickly along its own normal, roughly away from the face.
            float alongNormal = Vector3.Dot(s.velocity, s.palmNormal);
            if (s.openPalm && inFront && facing > 0.2f && alongNormal > Tuning.ThrustSpeed
                && Vector3.Dot(s.velocity, fwd) > Tuning.ThrustSpeed * 0.7f
                && Time.time - s.lastThrustTime > Tuning.ThrustCooldown)
            {
                s.thrust = true;
                s.lastThrustTime = Time.time;
                s.shieldHeld = 0;
            }

            // Shield: open palm facing forward and held still.
            bool hold = s.openPalm && inFront && facing > Tuning.ShieldFacing && s.velocity.magnitude < Tuning.ShieldMaxSpeed;
            s.shieldHeld = hold ? s.shieldHeld + dt : Mathf.Max(0, s.shieldHeld - dt * 3f);
            s.shieldPose = s.shieldHeld > Tuning.ShieldHoldTime;
        }

        void DetectCompress()
        {
            var a = hands[0]; var b = hands[1];
            if (!a.tracked || !b.tracked) return;
            bool closed = (a.fist || a.pinching) && (b.fist || b.pinching);
            float d = Vector3.Distance(a.palm, b.palm);
            if (closed && d > Tuning.CompressStart) compressArmedTime = Time.time;
            if (closed && d < Tuning.CompressEnd && Time.time - compressArmedTime < Tuning.CompressWindow)
            {
                CompressGesture = true;
                CompressPoint = (a.palm + b.palm) * 0.5f;
                compressArmedTime = -10f;
            }
        }

        void UpdateVelocity(HandState s, float dt)
        {
            if (!s.tracked) return;
            if (!s.hadPrev) { s.prevPalm = s.palm; s.velocity = Vector3.zero; s.hadPrev = true; return; }
            Vector3 raw = (s.palm - s.prevPalm) / dt;
            s.velocity = Vector3.Lerp(s.velocity, raw, 1f - Mathf.Exp(-dt * 25f));
            s.prevPalm = s.palm;
        }

        // ---------------------------------------------------------------- simulator
        // WASD walk · RMB drag look · mouse aims right hand · scroll = reach
        // LMB hold = pinch (release throws; hold Alt while releasing to place gently)
        // Space = Force Push · Shift hold = Rock Shield · F hold = both palms (hold) · G = two-hand push
        void Simulate(float dt)
        {
            var cam = head.GetComponent<Camera>();
            if (cam == null) return;
            var rig = RigRoot();

            Vector3 move = Vector3.zero;
            if (SimIn.Held(KeyCode.W)) move += Vec.FlatNorm(head.forward);
            if (SimIn.Held(KeyCode.S)) move -= Vec.FlatNorm(head.forward);
            if (SimIn.Held(KeyCode.D)) move += Vec.FlatNorm(head.right);
            if (SimIn.Held(KeyCode.A)) move -= Vec.FlatNorm(head.right);
            if (rig != head) rig.position += move * 1.4f * dt;
            else head.position += move * 1.4f * dt;

            if (SimIn.RMB)
            {
                Vector2 md = SimIn.MouseDelta;
                if (rig != head) rig.Rotate(0, md.x * 0.15f, 0, Space.World);
                simPitch = Mathf.Clamp(simPitch - md.y * 0.15f, -70, 70);
                head.localRotation = Quaternion.Euler(simPitch, rig != head ? 0 : head.localEulerAngles.y + md.x * 0.15f, 0);
            }
            simReach = Mathf.Clamp(simReach + SimIn.Scroll * 0.0008f, 0.25f, 0.95f);

            Ray ray = cam.ScreenPointToRay(SimIn.MousePos);
            bool both = SimIn.Held(KeyCode.F);
            bool shield = SimIn.Held(KeyCode.LeftShift) || both;
            bool push = SimIn.Down(KeyCode.Space);
            bool doublePush = SimIn.Down(KeyCode.G);
            bool pullDown = SimIn.Down(KeyCode.Q), pullHeld = SimIn.Held(KeyCode.Q);
            if (SimIn.Down(KeyCode.C)) { CompressGesture = true; CompressPoint = ray.origin + ray.direction * simReach; }

            for (int i = 0; i < 2; i++)
            {
                var s = hands[i];
                s.simulated = true;
                s.tracked = true;
                s.lastTrackedTime = Time.time;
                Vector3 side = Vec.FlatNorm(head.right) * (i == 0 ? -0.16f : 0.02f);
                s.pinchPoint = ray.origin + ray.direction * simReach + side + (i == 0 ? Vector3.down * 0.08f : Vector3.zero);
                s.palm = s.wrist = s.indexTip = s.thumbTip = s.pinchPoint;
                s.aimOrigin = s.pinchPoint;
                s.aimDir = ray.direction;
                s.palmNormal = ray.direction;

                bool active = i == 1;
                bool wantPinch = active && SimIn.LMB;
                if (wantPinch && !s.pinching) { s.pinching = true; s.pinchDown = true; }
                if (!wantPinch && s.pinching) { s.pinching = false; s.pinchUp = true; s.simPlace = SimIn.Held(KeyCode.LeftAlt); }

                bool wasFist = s.fist;
                s.fist = active && pullHeld;
                s.fistDown = s.fist && !wasFist; s.fistUp = !s.fist && wasFist;
                s.grabAir = active && pullDown;
                s.palmRot = Quaternion.LookRotation(ray.direction, head.up);
                bool sh = (active && shield) || (i == 0 && both);
                s.openPalm = sh || (active && push) || doublePush;
                s.shieldHeld = sh ? s.shieldHeld + dt : 0;
                s.shieldPose = s.shieldHeld > Tuning.ShieldHoldTime;
                if (((active && push) || doublePush) && Time.time - s.lastThrustTime > Tuning.ThrustCooldown)
                {
                    s.thrust = true;
                    s.lastThrustTime = Time.time;
                }
            }
        }

        void OnGUI()
        {
            if (!simulating) return;
            GUI.color = new Color(1, 1, 1, 0.8f);
            GUI.Label(new Rect(10, 10, 900, 22),
                "SIMULATOR  WASD walk · RMB look · Mouse aim · Scroll reach · LMB pinch/throw (Alt=place) · Space push · Shift shield · F both palms · G two-hand push · Q gravity pull · C compress · Z/X twist held · N skip");
        }
    }
}
