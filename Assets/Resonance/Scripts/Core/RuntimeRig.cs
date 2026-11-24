using UnityEngine;
using UnityEngine.SceneManagement;
#if RESONANCE_XRMGMT
using UnityEngine.XR.Management;
#endif
#if RESONANCE_ARF
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
#endif
#if RESONANCE_INPUTSYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace Resonance
{
    /// <summary>
    /// Zero-setup boot: when any scene loads without a GameBootstrap, this builds what the game needs at runtime.
    /// On the device (an XR loader is active) it builds a full XR Origin (camera offset, head tracking, AR session,
    /// plane / raycast / anchor managers) around the scene's camera. In the editor it sets up the simulator camera.
    /// </summary>
    public static class RuntimeRig
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return;   // Unity Test Runner scenes
            if (Find.One<GameBootstrap>() != null) return;
            EnsureRig();
            new GameObject("Resonance").AddComponent<GameBootstrap>();
        }

        public static bool XRActive
        {
            get
            {
#if RESONANCE_XRMGMT
                var s = XRGeneralSettings.Instance;
                return s != null && s.Manager != null && s.Manager.activeLoader != null;
#else
                return false;
#endif
            }
        }

        /// <summary>Solid clear to (0,0,0,0): additive glasses show black as the real room, passthrough uses alpha 0.</summary>
        public static void ClearTransparent(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        public static void EnsureRig()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var c = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = c.AddComponent<Camera>();
                c.AddComponent<AudioListener>();
            }
            ClearTransparent(cam);
            if (!XRActive)
            {
                // Editor / simulator: put the camera at standing eye height under a rig root the simulator can move.
                if (cam.transform.parent == null)
                {
                    var rig = new GameObject("SimulatorRig").transform;
                    cam.transform.SetParent(rig, false);
                }
                cam.transform.localPosition = new Vector3(0, 1.6f, 0);
                cam.transform.localRotation = Quaternion.identity;
                return;
            }
#if RESONANCE_ARF
            if (Find.One<XROrigin>() != null) return;
            var originGo = new GameObject("XR Origin");
            originGo.SetActive(false);
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);
            cam.transform.SetParent(offset.transform, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;
            // Camera feature (OpenXR/Meta passthrough). Disables itself quietly on runtimes without a camera subsystem.
            if (cam.GetComponent<ARCameraManager>() == null) cam.gameObject.AddComponent<ARCameraManager>();
#if RESONANCE_INPUTSYSTEM
            if (cam.GetComponent<TrackedPoseDriver>() == null)
            {
                var tpd = cam.gameObject.AddComponent<TrackedPoseDriver>();
                var pos = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                pos.AddBinding("<HandheldARInputDevice>/devicePosition");
                var rot = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
                tpd.positionInput = new InputActionProperty(pos);
                tpd.rotationInput = new InputActionProperty(rot);
            }
#endif
            var origin = originGo.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;
            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARRaycastManager>();
            originGo.AddComponent<ARAnchorManager>();
            originGo.SetActive(true);
            if (Find.One<ARSession>() == null)
            {
                var session = new GameObject("AR Session");
                session.AddComponent<ARSession>();
                session.AddComponent<ARInputManager>();
            }
#endif
        }
    }
}
