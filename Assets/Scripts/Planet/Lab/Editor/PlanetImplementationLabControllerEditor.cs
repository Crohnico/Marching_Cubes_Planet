using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetImplementationLabController))]
    public sealed class PlanetImplementationLabControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetImplementationLabController controller = (PlanetImplementationLabController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Lab Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Scene"))
            {
                controller.ValidateScene();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Refresh Modules"))
            {
                controller.RefreshModules();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Init Lab"))
            {
                controller.InitLab();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Capture Metrics"))
            {
                controller.CaptureMetrics();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Force GC Check"))
            {
                controller.ForceGcCheck();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Force GPU Release"))
            {
                controller.ForceGpuRelease();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Run Smoke Test"))
            {
                controller.RunSmokeTest();
                MarkDirty(controller);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stress", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Stress Low"))
            {
                controller.RunStressLow();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Run Stress Medium"))
            {
                controller.RunStressMedium();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Run Stress High"))
            {
                controller.RunStressHigh();
                MarkDirty(controller);
            }

            GUI.backgroundColor = new Color(1f, 0.72f, 0.55f);
            if (GUILayout.Button("Run Stress Extreme"))
            {
                if (EditorUtility.DisplayDialog(
                        "Run Stress Extreme",
                        "This is a manual heavy stress action. Run it only when you are ready to inspect memory and release behavior.",
                        "Run",
                        "Cancel"))
                {
                    controller.RunStressExtreme();
                    MarkDirty(controller);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            if (GUILayout.Button("Release All"))
            {
                controller.ReleaseAll();
                MarkDirty(controller);
            }

            if (GUILayout.Button("Reset Lab State"))
            {
                controller.ResetLabState();
                MarkDirty(controller);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            PlanetLabDiagnostic diagnostic = controller.LastDiagnostic;
            EditorGUILayout.LabelField("Severity", diagnostic.severity.ToString());
            EditorGUILayout.LabelField("Title", diagnostic.title);
            EditorGUILayout.HelpBox(diagnostic.probableCause + "\n\n" + diagnostic.recommendedAction, MessageType.Info);
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
