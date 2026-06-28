using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetMarchingCubesPaintLab))]
    public sealed class PlanetMarchingCubesPaintLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetMarchingCubesPaintLab lab = (PlanetMarchingCubesPaintLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Marching Cubes Paint Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Paint Setup"))
            {
                lab.ValidateModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Reset Paint Settings"))
            {
                lab.ResetPaintSettings();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Cycle Color Mode"))
            {
                lab.CycleColorMode();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Paint Last Extraction"))
            {
                lab.PaintLastExtraction();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Snapshot"))
            {
                lab.CaptureMetrics();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Release Painted Mesh"))
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
