using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.Preview;
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
            CreateMemoryLab(root.transform, registry, controller);
            CreateRecipeLab(root.transform, registry);
            CreateGpuShapeLab(root.transform, registry);
            PlanetRecipePayloadPreview payloadPreview = CreateRecipePayloadPreview();

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PlanetMinimalXrRigSceneUtility.CreateMinimalRig(Vector3.zero, Quaternion.identity);
            PlanetMinimalXrRigSceneUtility.EnsureEventSystem();
            PlanetRecipePayloadPreviewVrPanel.CreateDefault(payloadPreview);

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

        private static PlanetMemoryLab CreateMemoryLab(
            Transform parent,
            PlanetLabResourceRegistry registry,
            PlanetImplementationLabController controller)
        {
            GameObject memoryObject = new GameObject("PlanetMemoryLab");
            memoryObject.transform.SetParent(parent, false);
            memoryObject.transform.localPosition = new Vector3(0f, 0f, 0f);

            PlanetMemoryLab memoryLab = memoryObject.AddComponent<PlanetMemoryLab>();

            SerializedObject serializedMemoryLab = new SerializedObject(memoryLab);
            serializedMemoryLab.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedMemoryLab.FindProperty("labController").objectReferenceValue = controller;
            serializedMemoryLab.ApplyModifiedPropertiesWithoutUndo();

            return memoryLab;
        }

        private static PlanetRecipeLab CreateRecipeLab(Transform parent, PlanetLabResourceRegistry registry)
        {
            GameObject recipeObject = new GameObject("PlanetRecipeLab");
            recipeObject.transform.SetParent(parent, false);
            recipeObject.transform.localPosition = new Vector3(0f, 0f, 0f);

            PlanetRecipeLab recipeLab = recipeObject.AddComponent<PlanetRecipeLab>();

            SerializedObject serializedRecipeLab = new SerializedObject(recipeLab);
            serializedRecipeLab.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedRecipeLab.ApplyModifiedPropertiesWithoutUndo();

            return recipeLab;
        }

        private static PlanetGpuShapeLab CreateGpuShapeLab(Transform parent, PlanetLabResourceRegistry registry)
        {
            GameObject shapeObject = new GameObject("PlanetGpuShapeLab");
            shapeObject.transform.SetParent(parent, false);
            shapeObject.transform.localPosition = new Vector3(0f, 0f, 0f);

            PlanetGpuShapeLab shapeLab = shapeObject.AddComponent<PlanetGpuShapeLab>();
            ComputeShader shapeComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Shaders/Compute/PlanetShapeDensity.compute");

            SerializedObject serializedShapeLab = new SerializedObject(shapeLab);
            serializedShapeLab.FindProperty("resourceRegistry").objectReferenceValue = registry;
            serializedShapeLab.FindProperty("shapeComputeShader").objectReferenceValue = shapeComputeShader;
            serializedShapeLab.ApplyModifiedPropertiesWithoutUndo();

            return shapeLab;
        }

        private static PlanetRecipePayloadPreview CreateRecipePayloadPreview()
        {
            GameObject previewObject = new GameObject("PlanetRecipePayloadPreview");
            previewObject.transform.position = Vector3.zero;
            previewObject.transform.rotation = Quaternion.identity;
            previewObject.transform.localScale = Vector3.one;
            return previewObject.AddComponent<PlanetRecipePayloadPreview>();
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
