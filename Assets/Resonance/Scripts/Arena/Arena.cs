using System;
using System.Collections.Generic;
using UnityEngine;
#if RESONANCE_ARF
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace Resonance
{
    /// <summary>
    /// Finds the floor with AR plane detection, lets the player pinch to lock a 2x2 m play area,
    /// anchors it, and warns when the player walks out of bounds.
    /// Arena local space: origin = floor under the player at placement, +Z = the way they faced.
    /// The quarry is built beyond +Z so virtual space extends past the room.
    /// </summary>
    public class Arena : MonoBehaviour
    {
        public static Arena I { get; private set; }
        public static Transform Root { get; private set; }
        public static bool Placed => Root != null;
        public static float EyeHeight { get; private set; } = 1.6f;
        public static Vector3 W(float x, float y, float z) => Root.TransformPoint(new Vector3(x, y, z));
        public static Vector3 Local(Vector3 world) => Root.InverseTransformPoint(world);
        public static Vector3 HeadLocal => Local(HandInput.Head.position);
        /// <summary>Player's feet in world space.</summary>
        public static Vector3 Feet { get { var p = HeadLocal; p.y = 0; return Root.TransformPoint(p); } }

        public float halfSize = 1f;
        /// <summary>Floor search only starts once the title menu hands over.</summary>
        public bool armed;
        public event Action OnPlaced;

        GameObject reticle;
        LineRenderer border;
        float searchTime, floorY;
        bool haveFloor, outOfBounds;
#if RESONANCE_ARF
        ARRaycastManager raycaster;
        ARPlaneManager planes;
        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
#endif

        void Awake() { I = this; }

        void Start()
        {
#if RESONANCE_ARF
            raycaster = Find.One<ARRaycastManager>();
            planes = Find.One<ARPlaneManager>();
#endif
            reticle = new GameObject("FloorReticle");
            var ring = reticle.AddComponent<LineRenderer>();
            ring.sharedMaterial = Mats.Additive(Color.white);
            ring.startColor = ring.endColor = Palette.Blue;
            ring.widthMultiplier = 0.012f;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = 48;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.25f);
            }
        }

        void Update()
        {
            if (HandInput.I == null || HandInput.Head == null) return;
            if (!Placed) { if (armed && !Menu.Blocking) Search(); else if (reticle) reticle.SetActive(false); }
            else Bounds();
        }

        void Search()
        {
            searchTime += Time.deltaTime;
            Transform head = HandInput.Head;
            Ray ray = new Ray(head.position, head.forward);
            var r = HandInput.Right;
            if (r.tracked && !r.simulated) ray = new Ray(r.aimOrigin, r.aimDir);

            bool hitFloor = false;
            Vector3 hitPoint = Vector3.zero;
#if RESONANCE_ARF
            if (raycaster != null && raycaster.Raycast(ray, hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var h in hits)
                {
                    if (h.pose.up.y > 0.85f && head.position.y - h.pose.position.y > 0.8f)
                    {
                        hitFloor = true;
                        hitPoint = h.pose.position;
                        break;
                    }
                }
            }
#endif
            if (hitFloor) { floorY = hitPoint.y; haveFloor = true; }
            else if (!haveFloor && (searchTime > 6f || HandInput.Simulated))
            {
                // Fallback: estimated standing eye height, or the rig floor in the simulator.
                floorY = HandInput.Simulated ? Mathf.Min(0f, head.position.y - 1.2f) : head.position.y - 1.6f;
                var plane = new Plane(Vector3.up, new Vector3(0, floorY, 0));
                if (plane.Raycast(ray, out float t) && t < 6f) hitPoint = ray.GetPoint(t);
                else hitPoint = head.position + Vec.FlatNorm(head.forward) * 1.2f;
                hitPoint.y = floorY;
            }
            else if (haveFloor)
            {
                var plane = new Plane(Vector3.up, new Vector3(0, floorY, 0));
                if (plane.Raycast(ray, out float t) && t < 6f) hitPoint = ray.GetPoint(t);
                else hitPoint = new Vector3(head.position.x, floorY, head.position.z) + Vec.FlatNorm(head.forward);
            }

            bool ready = haveFloor || searchTime > 6f || HandInput.Simulated;
            reticle.SetActive(ready);
            if (ready) reticle.transform.position = hitPoint + Vector3.up * 0.01f;

            Hud.Objective("SET PLAY AREA",
                ready ? "Clear a 2 x 2 m space. Stand in its centre, face the open side, then pinch."
                      : "Look at the floor and move your head slowly…",
                null);

            if (ready && (HandInput.Left.pinchDown || HandInput.Right.pinchDown)) Place(head);
        }

        /// <summary>Tests / system check: place immediately at the estimated floor.</summary>
        public void ForcePlace()
        {
            var head = HandInput.Head;
            floorY = HandInput.Simulated ? Mathf.Min(0f, head.position.y - 1.2f) : head.position.y - 1.6f;
            Place(head);
        }

        void Place(Transform head)
        {
            var go = new GameObject("Arena");
            go.transform.position = new Vector3(head.position.x, floorY, head.position.z);
            go.transform.rotation = Quaternion.LookRotation(Vec.FlatNorm(head.forward), Vector3.up);
#if RESONANCE_ARF
            go.AddComponent<ARAnchor>();
            if (planes != null)
            {
                foreach (var p in planes.trackables) p.gameObject.SetActive(false);
                planes.enabled = false;
            }
#endif
            Root = go.transform;
            armed = false;
            EyeHeight = Mathf.Clamp(head.position.y - floorY, 1.1f, 2.0f);
            if (reticle) Destroy(reticle);

            // Invisible floor for physics, extended forward under the quarry.
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(Root, false);
            floor.transform.localPosition = new Vector3(0, -0.1f, 8f);
            floor.AddComponent<BoxCollider>().size = new Vector3(40f, 0.2f, 40f);

            border = Build.Line("PlayBorder", Root, Palette.Blue * 0.6f, 0.01f);
            border.useWorldSpace = false;
            border.loop = true;
            border.positionCount = 4;
            float s = halfSize;
            border.SetPositions(new[] { new Vector3(-s, 0.01f, -s), new Vector3(-s, 0.01f, s), new Vector3(s, 0.01f, s), new Vector3(s, 0.01f, -s) });

            Sfx.Play(Sound.Chime, head.position, 0.6f);
            OnPlaced?.Invoke();
        }

        void Bounds()
        {
            Vector3 h = HeadLocal;
            bool outNow = Mathf.Abs(h.x) > halfSize - 0.05f || Mathf.Abs(h.z) > halfSize - 0.05f;
            if (outNow != outOfBounds)
            {
                outOfBounds = outNow;
                border.startColor = border.endColor = outNow ? Palette.Red : Palette.Blue * 0.6f;
                border.widthMultiplier = outNow ? 0.03f : 0.01f;
                Hud.Warning(outNow ? "STEP BACK INTO THE PLAY AREA" : null);
            }
        }
    }
}
