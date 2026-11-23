using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Drop this on one empty GameObject in a scene that has an XR Origin (AR) from the XREAL SDK.
    /// It creates every system the slice needs. With no camera in the scene it builds a simulator rig,
    /// so you can press Play in an empty scene and test with mouse and keyboard.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("-1 = title menu (shipping). 0 Quarry · 1 Survey Tunnels · 2 Research Station · 3 Fracture Chamber skip the menu (dev).")]
        public int startLevel = -1;
        [Tooltip("Jump to a step inside the start level (testing).")]
        public int startStep = 0;
        public bool forceSimulator = false;
        [Tooltip("Cinematic = dark Control-style grade (editor, recordings). Glasses = lifted for the Air 2 Ultra's see-through display.")]
        public LookMode look = LookMode.Cinematic;
        [Tooltip("Use the Glasses look automatically when running on the device.")]
        public bool glassesLookOnDevice = true;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            var cam = Camera.main;
            if (cam == null)
            {
                var rig = new GameObject("SimulatorRig").transform;
                var c = new GameObject("Main Camera");
                c.tag = "MainCamera";
                c.transform.SetParent(rig, false);
                c.transform.localPosition = new Vector3(0, 1.6f, 0);
                cam = c.AddComponent<Camera>();
                c.AddComponent<AudioListener>();
            }
            // Optical see-through: black is transparent, so clear to black.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            var lookMode = Save.LookChoice == 0 ? LookMode.Cinematic : Save.LookChoice == 1 ? LookMode.Glasses
                : glassesLookOnDevice && !Application.isEditor ? LookMode.Glasses : look;
            Look.Apply(lookMode, cam);
            cam.farClipPlane = 120f;

            var hi = gameObject.AddComponent<HandInput>();
            hi.forceSimulator = forceSimulator || GameFlow.TestMode;
            gameObject.AddComponent<Hud>();
            gameObject.AddComponent<Player>();
            gameObject.AddComponent<Arena>();
            gameObject.AddComponent<PowerSet>();
            gameObject.AddComponent<Feel>();
            gameObject.AddComponent<PauseWatcher>();
            new GameObject("LeftHandPowers").AddComponent<HandPowers>().hand = Hand.Left;
            new GameObject("RightHandPowers").AddComponent<HandPowers>().hand = Hand.Right;
            var flow = gameObject.AddComponent<GameFlow>();
            flow.startLevel = startLevel;
            flow.startStep = startStep;
        }
    }
}
