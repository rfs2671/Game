using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if RESONANCE_ARF
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
#endif

namespace Resonance.EditorTools
{
    /// <summary>Keeps the runtime shaders in Always Included on every editor load and before every build (CI included).</summary>
    [InitializeOnLoad]
    public class ResonanceShaderGuard : UnityEditor.Build.IPreprocessBuildWithReport
    {
        static ResonanceShaderGuard() { EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) ResonanceSetup.IncludeShaders(); }; }
        public int callbackOrder => 0;
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report) => ResonanceSetup.IncludeShaders();
    }

    public static class ResonanceSetup
    {
        [MenuItem("Resonance/Setup Current Scene")]
        public static void SetupScene()
        {
            IncludeShaders();

#if RESONANCE_ARF
            var origin = Find.One<XROrigin>();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Resonance",
                    "No XR Origin found.\n\nAdd one first: GameObject > XR > XR Origin (AR), or use the XR Origin from the XREAL SDK samples. " +
                    "Then run this again.\n\n(You can still press Play without one to use the keyboard/mouse simulator.)", "OK");
            }
            else
            {
                var go = origin.gameObject;
                if (go.GetComponent<ARPlaneManager>() == null) Undo.AddComponent<ARPlaneManager>(go);
                if (go.GetComponent<ARRaycastManager>() == null) Undo.AddComponent<ARRaycastManager>(go);
                if (go.GetComponent<ARAnchorManager>() == null) Undo.AddComponent<ARAnchorManager>(go);
                if (Find.One<ARSession>() == null)
                    Undo.RegisterCreatedObjectUndo(new GameObject("AR Session", typeof(ARSession)), "AR Session");
            }
#endif
            if (Find.One<GameBootstrap>() == null)
            {
                var g = new GameObject("Resonance");
                g.AddComponent<GameBootstrap>();
                Undo.RegisterCreatedObjectUndo(g, "Resonance");
                Selection.activeGameObject = g;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Resonance] Scene ready. Press Play to test with the simulator, or build to the Beam Pro.");
        }

        [MenuItem("Resonance/Include Runtime Shaders")]
        public static void IncludeShaders()
        {
            var gs = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            var names = new System.Collections.Generic.List<string>(Mats.ShaderNamesBuiltin);
            if (GraphicsSettings.currentRenderPipeline != null) names.AddRange(Mats.ShaderNamesURP);
            // (GUI/Text Shader is internal editor data — Android builds reject it; the built-in font brings its own material)
            int added = 0;
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool present = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { present = true; break; }
                if (present) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                added++;
            }
            so.ApplyModifiedProperties();
            if (added > 0) { AssetDatabase.SaveAssets(); Debug.Log($"[Resonance] Always-included shaders updated (+{added})."); }
        }
    }
}
