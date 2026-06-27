using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetLabModule), true)]
    public sealed class PlanetLabModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetLabModule module = (PlanetLabModule)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(module.ModuleName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Has Live Resources", module.HasLiveResources.ToString());

            if (GUILayout.Button("Validate Module"))
            {
                bool ok = module.ValidateModule();
                Debug.Log(module.ModuleName + " ValidateModule: " + ok, module);
                MarkDirty(module);
            }

            if (GUILayout.Button("Init Module"))
            {
                module.InitModule();
                MarkDirty(module);
            }

            if (GUILayout.Button("Run Module Test"))
            {
                module.RunModuleTest();
                MarkDirty(module);
            }

            if (GUILayout.Button("Run Module Stress"))
            {
                module.RunModuleStress();
                MarkDirty(module);
            }

            if (GUILayout.Button("Capture Module Metrics"))
            {
                PlanetLabMetricsSnapshot snapshot = module.CaptureMetrics();
                Debug.Log(module.ModuleName + " CaptureMetrics: " + snapshot.diagnostic.title, module);
                MarkDirty(module);
            }

            if (GUILayout.Button("Release Module"))
            {
                module.ReleaseModule();
                MarkDirty(module);
            }
        }

        private static void MarkDirty(Object targetObject)
        {
            EditorUtility.SetDirty(targetObject);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(((Component)targetObject).gameObject.scene);
            }
        }
    }
}
