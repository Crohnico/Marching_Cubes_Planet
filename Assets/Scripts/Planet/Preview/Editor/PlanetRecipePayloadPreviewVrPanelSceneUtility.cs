using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarchingCubesPlanet.Preview.Editor
{
    public static class PlanetRecipePayloadPreviewVrPanelSceneUtility
    {
        private const string ScenePath = "Assets/Scenes/PlanetImplementationLab.unity";
        private const string AutoCreateSessionKey = "PlanetRecipePayloadPreviewVrPanelSceneUtility.AutoCreate";

        [InitializeOnLoadMethod]
        private static void RegisterAutoCreateForOpenLabScene()
        {
            EditorApplication.delayCall += AutoCreateOnceForOpenLabScene;
        }

        [MenuItem("Tools/Planet Lab/Payload Preview/Create Or Refresh VR Deadline Panels In Scene")]
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
                    Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Planet Payload VR Deadline Panels");
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

        private static void AutoCreateOnceForOpenLabScene()
        {
            if (SessionState.GetBool(AutoCreateSessionKey, false))
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                return;
            }

            if (Object.FindFirstObjectByType<PlanetRecipePayloadPreviewVrPanel>() != null)
            {
                SessionState.SetBool(AutoCreateSessionKey, true);
                return;
            }

            PlanetRecipePayloadPreview preview = Object.FindFirstObjectByType<PlanetRecipePayloadPreview>();
            if (preview == null)
            {
                return;
            }

            CreateOrRefreshOpenScene(useUndo: false, selectPanel: false);
            SessionState.SetBool(AutoCreateSessionKey, true);
        }
    }
}
