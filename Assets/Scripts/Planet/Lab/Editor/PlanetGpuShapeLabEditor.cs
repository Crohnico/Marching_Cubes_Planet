using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetGpuShapeLab))]
    public sealed class PlanetGpuShapeLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetGpuShapeLab lab = (PlanetGpuShapeLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GPU Shape Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Shape Setup"))
            {
                lab.ValidateModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Reset Demo Settings"))
            {
                lab.ResetDemoRecipe();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Generate Voronoi Cells"))
            {
                lab.GenerateVoronoiCells();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Init Shape GPU"))
            {
                lab.InitShapeGpu();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Evaluate Debug Samples"))
            {
                lab.EvaluateDebugSamples();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Shape Smoke Test"))
            {
                lab.RunShapeSmokeTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stress", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Stress Low"))
            {
                lab.RunStressLow();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Stress Medium"))
            {
                lab.RunStressMedium();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Snapshot"))
            {
                lab.CaptureMetrics();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Release Shape GPU"))
            {
                lab.ReleaseModule();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            PlanetLabDiagnostic diagnostic = lab.LastDiagnostic;
            EditorGUILayout.LabelField("Severity", diagnostic.severity.ToString());
            EditorGUILayout.LabelField("Title", diagnostic.title);
            EditorGUILayout.LabelField("Last Action", lab.LastAction);
            EditorGUILayout.HelpBox(diagnostic.probableCause + "\n\n" + diagnostic.recommendedAction, MessageType.Info);
        }

        private static void MarkDirty(UnityEngine.Object targetObject)
        {
            EditorUtility.SetDirty(targetObject);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(((Component)targetObject).gameObject.scene);
            }
        }
    }
}
