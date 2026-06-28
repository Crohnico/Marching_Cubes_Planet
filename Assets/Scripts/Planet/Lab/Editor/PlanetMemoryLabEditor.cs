using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetMemoryLab))]
    public sealed class PlanetMemoryLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetMemoryLab memoryLab = (PlanetMemoryLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Memory Lab Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Memory Setup"))
            {
                memoryLab.ValidateModule();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Capture Snapshot"))
            {
                memoryLab.CaptureSnapshot();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Before"))
            {
                memoryLab.CaptureBefore();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Capture After"))
            {
                memoryLab.CaptureAfter();
                MarkDirty(memoryLab);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Compare Last Snapshots"))
            {
                memoryLab.CompareLastSnapshots();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity Memory Profiler", EditorStyles.boldLabel);

            if (GUILayout.Button("Capture Unity Memory Snapshot"))
            {
                memoryLab.CaptureUnityMemorySnapshot();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Unity Memory Before"))
            {
                memoryLab.CaptureUnityMemoryBefore();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Capture Unity Memory After"))
            {
                memoryLab.CaptureUnityMemoryAfter();
                MarkDirty(memoryLab);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Exports And Budget", EditorStyles.boldLabel);

            if (GUILayout.Button("Export Last Own Snapshot"))
            {
                memoryLab.ExportLastOwnSnapshot();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Export Last Own Snapshot Comparison"))
            {
                memoryLab.ExportLastOwnSnapshotComparison();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Reload Test Budget"))
            {
                memoryLab.ReloadTestBudget();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.SelectableLabel(memoryLab.OverrideFilePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Smoke", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Allocation Smoke Test"))
            {
                memoryLab.RunAllocationSmokeTest();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Run Registry Smoke Test"))
            {
                memoryLab.RunRegistrySmokeTest();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Run Release Smoke Test"))
            {
                memoryLab.RunReleaseSmokeTest();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stress", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Stress Low"))
            {
                memoryLab.RunStressLow();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Run Stress Medium"))
            {
                memoryLab.RunStressMedium();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Run Stress High"))
            {
                memoryLab.RunStressHigh();
                MarkDirty(memoryLab);
            }

            GUI.backgroundColor = new Color(1f, 0.72f, 0.55f);
            if (GUILayout.Button("Run Stress Extreme"))
            {
                if (EditorUtility.DisplayDialog(
                        "Run Memory Stress Extreme",
                        "This action deliberately pushes the Memory Lab harder. It uses mock registered resources, but it should still be treated as a manual memory validation step.",
                        "Run",
                        "Cancel"))
                {
                    memoryLab.RunStressExtreme();
                    MarkDirty(memoryLab);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Release And Diagnostics", EditorStyles.boldLabel);

            if (GUILayout.Button("Release All"))
            {
                memoryLab.ReleaseAll();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Show Live Resources"))
            {
                memoryLab.ShowLiveResources();
                MarkDirty(memoryLab);
            }

            if (GUILayout.Button("Reset Memory Lab State"))
            {
                memoryLab.ResetMemoryLabState();
                MarkDirty(memoryLab);
            }

            EditorGUILayout.Space();
            PlanetLabDiagnostic diagnostic = memoryLab.LastDiagnostic;
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
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
