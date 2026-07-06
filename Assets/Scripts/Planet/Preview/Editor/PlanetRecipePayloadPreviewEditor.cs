using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            EditorGUILayout.LabelField("Planet Anchor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("World Center", preview.Placement.PlanetWorldCenter.ToString("0.###"));
            EditorGUILayout.LabelField("Rotation", preview.Placement.PlanetRotation.eulerAngles.ToString("0.###"));
            EditorGUILayout.LabelField("Grid Radius", preview.Recipe.GridRadius.ToString());
            EditorGUILayout.LabelField("World Scale", preview.Recipe.WorldScale.ToString("0.###"));
            EditorGUILayout.LabelField("World Radius", preview.Recipe.WorldRadius.ToString("0.###"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VR UI", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Or Refresh VR Panels In Scene"))
            {
                PlanetRecipePayloadPreviewVrPanelSceneUtility.CreateOrRefreshOpenScene(
                    useUndo: true,
                    selectPanel: true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commands", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate"))
            {
                Apply(preview, p => p.Generate());
            }

            if (GUILayout.Button("Generate Random"))
            {
                Apply(preview, p => p.GenerateRandomSeed());
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
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(preview.LastDiagnostic ?? string.Empty, MessageType.Info);
        }

        private static void Apply(PlanetRecipePayloadPreview preview, System.Action<PlanetRecipePayloadPreview> action)
        {
            Undo.RecordObject(preview, "Planet Recipe Preview");
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
