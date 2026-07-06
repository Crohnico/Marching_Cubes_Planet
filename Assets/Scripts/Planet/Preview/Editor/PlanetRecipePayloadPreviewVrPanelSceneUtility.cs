using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarchingCubesPlanet.Preview.Editor
{
    public static class PlanetRecipePayloadPreviewVrPanelSceneUtility
    {
        [MenuItem("Tools/Planet Preview/Create Or Refresh VR Panels In Scene")]
        public static void CreateOrRefreshOpenSceneFromMenu()
        {
            CreateOrRefreshOpenScene(useUndo: true, selectPanel: true);
        }

        public static PlanetRecipePayloadPreviewVrPanel CreateOrRefreshOpenScene(bool useUndo, bool selectPanel)
        {
            PlanetRecipePayloadPreview preview = Object.FindFirstObjectByType<PlanetRecipePayloadPreview>();
            if (preview == null)
            {
                Debug.LogError("PlanetRecipePayloadPreview not found in the open scene.");
                return null;
            }

            PlanetRecipePayloadPreviewVrPanel panel = Object.FindFirstObjectByType<PlanetRecipePayloadPreviewVrPanel>();
            if (panel == null)
            {
                panel = PlanetRecipePayloadPreviewVrPanel.CreateDefault(preview);
                if (useUndo)
                {
                    Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Planet Payload VR Panels");
                }
            }

            panel.Bind(preview);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);

            if (selectPanel)
            {
                Selection.activeGameObject = panel.gameObject;
            }

            return panel;
        }
    }
}
