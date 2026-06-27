using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarchingCubesPlanet.Lab.Editor
{
    public static class PlanetImplementationLabSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PlanetImplementationLab.unity";

        [MenuItem("Tools/Planet Lab/Create Or Refresh PlanetImplementationLab Scene")]
        public static void CreateOrRefreshScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PlanetImplementationLab";

            GameObject root = new GameObject("PlanetImplementationLab");
            PlanetLabResourceRegistry registry = root.AddComponent<PlanetLabResourceRegistry>();
            PlanetImplementationLabController controller = root.AddComponent<PlanetImplementationLabController>();
            CreateComputeShaderLab(root.transform, registry);

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject cameraObject = new GameObject("PlanetLabCameraRig");
            cameraObject.transform.position = new Vector3(0f, 2f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 10000f;
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = new GameObject("PlanetLabDirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = root;

            Debug.Log("Created PlanetImplementationLab scene at " + ScenePath);
        }

        private static PlanetComputeShaderRunnerLab CreateComputeShaderLab(Transform parent, PlanetLabResourceRegistry registry)
        {
            GameObject computeObject = new GameObject("PlanetComputeShaderRunnerLab");
            computeObject.transform.SetParent(parent, false);
            computeObject.transform.localPosition = new Vector3(0f, 0f, 0f);

            PlanetComputeShaderRunnerLab computeLab = computeObject.AddComponent<PlanetComputeShaderRunnerLab>();
            ComputeShader computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Shaders/Compute/PlanetComputeDebug.compute");

            SerializedObject serializedComputeLab = new SerializedObject(computeLab);
            serializedComputeLab.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedComputeLab.FindProperty("computeShader").objectReferenceValue = computeShader;
            serializedComputeLab.ApplyModifiedPropertiesWithoutUndo();

            return computeLab;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path == scenePath)
                {
                    currentScenes[i].enabled = true;
                    EditorBuildSettings.scenes = currentScenes;
                    return;
                }
            }

            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            for (int i = 0; i < currentScenes.Length; i++)
            {
                newScenes[i] = currentScenes[i];
            }

            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
