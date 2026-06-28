using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MarchingCubesPlanet.Lab;

namespace MarchingCubesPlanet.Preview.Editor
{
    [CustomEditor(typeof(PlanetRecipePayloadPreview))]
    public sealed class PlanetRecipePayloadPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetRecipePayloadPreview preview = (PlanetRecipePayloadPreview)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Payload", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("WorldRadius", preview.DerivedWorldRadius.ToString("0.###"));
            EditorGUILayout.LabelField("IsoLevel", preview.IsoLevel.ToString("0.###"));
            EditorGUILayout.LabelField("surface radius", preview.DerivedSurfaceRadius.ToString("0.###"));
            EditorGUILayout.LabelField("PlanetWorldCenter", preview.TransformPlanetWorldCenter.ToString("0.###"));
            EditorGUILayout.LabelField("PlanetRotation", preview.TransformPlanetRotation.eulerAngles.ToString("0.###"));
            EditorGUILayout.LabelField("color mode", preview.ColorMode.ToString());
            EditorGUILayout.LabelField("requested triangles", preview.RequestedTrianglePayload.ToString());
            EditorGUILayout.LabelField("icosphere frequency", preview.DerivedGeodesicFrequency.ToString());
            EditorGUILayout.LabelField("triangles", preview.DerivedTriangleCount.ToString());
            EditorGUILayout.LabelField("vertices", preview.DerivedVertexCount.ToString());
            EditorGUILayout.LabelField("indices", preview.DerivedIndexCount.ToString());
            EditorGUILayout.LabelField("mesh live", preview.HasLiveMesh ? "yes" : "no");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VR Deadline UI", EditorStyles.boldLabel);

            if (GUILayout.Button("Create Or Refresh VR Deadline Panels In Scene"))
            {
                PlanetRecipePayloadPreviewVrPanelSceneUtility.CreateOrRefreshOpenScene(
                    useUndo: true,
                    selectPanel: true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Payload Presets", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 126k"))
                {
                    Apply(preview, p => p.ApplyPayload126k());
                }

                if (GUILayout.Button("Apply Payload 250k"))
                {
                    Apply(preview, p => p.ApplyPayload250k());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 500k"))
                {
                    Apply(preview, p => p.ApplyPayload500k());
                }

                if (GUILayout.Button("Apply Payload 1M"))
                {
                    Apply(preview, p => p.ApplyPayload1M());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 2M"))
                {
                    Apply(preview, p => p.ApplyPayload2M());
                }

                if (GUILayout.Button("Apply Payload 5M"))
                {
                    Apply(preview, p => p.ApplyPayload5M());
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate"))
            {
                Apply(preview, p => p.Generate());
            }

            if (GUILayout.Button("Release"))
            {
                Apply(preview, p => p.Release());
            }

            if (GUILayout.Button("Reset Demo Recipe"))
            {
                Apply(preview, p => p.ResetDemoRecipe());
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Memory Snapshot", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Registry found", preview.HasResourceRegistry ? "yes" : "no");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Before Snapshot"))
                {
                    Apply(preview, p => p.CaptureBeforeSnapshot());
                }

                if (GUILayout.Button("After Snapshot"))
                {
                    Apply(preview, p => p.CaptureAfterSnapshot());
                }
            }

            DrawMemoryComparison(preview);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(preview.LastDiagnostic ?? string.Empty, MessageType.Info);
        }

        private static void DrawMemoryComparison(PlanetRecipePayloadPreview preview)
        {
            PlanetMemorySnapshot before = preview.BeforeSnapshot;
            PlanetMemorySnapshot after = preview.AfterSnapshot;
            PlanetMemorySnapshotComparison comparison = preview.SnapshotComparison;
            PlanetMemoryBudget budget = preview.MemoryBudget;

            EditorGUILayout.LabelField("Before owned GPU bytes", before.ownedGpuEstimatedBytes.ToString());
            EditorGUILayout.LabelField("After owned GPU bytes", after.ownedGpuEstimatedBytes.ToString());
            EditorGUILayout.LabelField("GPU delta bytes", comparison.ownedGpuDeltaBytes.ToString());
            EditorGUILayout.LabelField("After GPU soft budget", FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuSoftBytes));
            EditorGUILayout.LabelField("After GPU hard budget", FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuHardBytes));
            EditorGUILayout.LabelField("After combined soft budget", FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedSoftBytes));
            EditorGUILayout.LabelField("After combined hard budget", FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedHardBytes));
            EditorGUILayout.LabelField("After runtime meshes", after.liveRuntimeMeshes.ToString());
            EditorGUILayout.LabelField("After live resources", after.liveResourceCount.ToString());
            EditorGUILayout.LabelField("Largest resource", string.IsNullOrEmpty(after.largestSingleResourceName) ? "-" : after.largestSingleResourceName);
            EditorGUILayout.LabelField("Largest resource bytes", after.largestSingleResourceBytes.ToString());

            string summary = preview.SnapshotComparisonSummary;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                MessageType messageType = comparison.diagnostic.severity == PlanetLabDiagnosticSeverity.Critical
                    ? MessageType.Error
                    : comparison.diagnostic.severity == PlanetLabDiagnosticSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(summary, messageType);
            }
        }

        private static string FormatBudgetPercent(long bytes, long budgetBytes)
        {
            if (budgetBytes <= 0)
            {
                return "unavailable";
            }

            double percent = bytes * 100.0 / budgetBytes;
            double mib = bytes / (1024.0 * 1024.0);
            double budgetMib = budgetBytes / (1024.0 * 1024.0);
            return percent.ToString("0.0") + "% (" + mib.ToString("0.00") + " / " + budgetMib.ToString("0.00") + " MiB)";
        }

        private static void Apply(PlanetRecipePayloadPreview preview, System.Action<PlanetRecipePayloadPreview> action)
        {
            Undo.RecordObject(preview, "Planet Recipe Payload Preview");
            action(preview);
            EditorUtility.SetDirty(preview);
            RefreshScenePanels(preview);

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
        }

        private static void RefreshScenePanels(PlanetRecipePayloadPreview preview)
        {
            PlanetRecipePayloadPreviewVrPanel[] panels = Object.FindObjectsByType<PlanetRecipePayloadPreviewVrPanel>(FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++)
            {
                PlanetRecipePayloadPreviewVrPanel panel = panels[i];
                if (panel.Preview == null || panel.Preview == preview)
                {
                    panel.Bind(preview);
                    EditorUtility.SetDirty(panel);
                }
            }
        }
    }
}
