using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetMarchingCubesLab))]
    public sealed class PlanetMarchingCubesLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetMarchingCubesLab lab = (PlanetMarchingCubesLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Marching Cubes Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Marching Cubes Setup"))
            {
                lab.ValidateModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Reset Demo Settings"))
            {
                lab.ResetDemoSettings();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Init Marching Cubes GPU"))
            {
                lab.InitMarchingCubesGpu();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Extract Cartesian Planet Surface"))
            {
                lab.ExtractPlanetSurface();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Marching Cubes Smoke Test"))
            {
                lab.RunMarchingCubesSmokeTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Snapshot"))
            {
                lab.CaptureMetrics();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Release Marching Cubes GPU"))
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
