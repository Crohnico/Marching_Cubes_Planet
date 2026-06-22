using MarchingCubesPlanet.VoxelEngine.Runtime;
using MarchingCubesPlanet.VoxelEngine.Data;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    public static class VoxelDemoMenu
    {
        private const string ConfigPath = "Assets/Resources/VoxelEngineConfig.asset";

        [MenuItem("GameObject/Voxel Engine/Complete Sphere Demo", false, 9)]
        public static void CreateCompleteSphereDemo()
        {
            VoxelEngineConfig config = GetOrCreateConfig();

            GameObject player = new GameObject("Voxel Demo Player");
            player.transform.position = Vector3.zero;
            Camera playerCamera = CreatePlayerCamera(player.transform);
            PlayerChunkTracker tracker = player.AddComponent<PlayerChunkTracker>();
            player.AddComponent<VoxelDemoPlayerMover>();
            tracker.Configure(config, player.transform, playerCamera);

            GameObject engine = new GameObject("Voxel Sphere Engine");
            VoxelChunkManager manager = engine.AddComponent<VoxelChunkManager>();
            VoxelSphereGenerator sphereGenerator = engine.AddComponent<VoxelSphereGenerator>();
            sphereGenerator.Configure(manager);
            manager.Configure(config, tracker, sphereGenerator, player.transform);

            Undo.RegisterCreatedObjectUndo(player, "Create Voxel Demo Player");
            Undo.RegisterCreatedObjectUndo(engine, "Create Voxel Sphere Engine");
            Selection.activeGameObject = engine;
        }

        [MenuItem("GameObject/Voxel Engine/Demo Player", false, 10)]
        public static void CreateDemoPlayer()
        {
            VoxelEngineConfig config = GetOrCreateConfig();
            GameObject player = new GameObject("Voxel Demo Player");
            player.transform.position = Vector3.zero;
            Camera playerCamera = CreatePlayerCamera(player.transform);
            PlayerChunkTracker tracker = player.AddComponent<PlayerChunkTracker>();
            player.AddComponent<VoxelDemoPlayerMover>();
            tracker.Configure(config, player.transform, playerCamera);

            Selection.activeGameObject = player;
            Undo.RegisterCreatedObjectUndo(player, "Create Voxel Demo Player");
        }

        [MenuItem("GameObject/Voxel Engine/Voxel Sphere Engine", false, 11)]
        public static void CreateVoxelSphereEngine()
        {
            VoxelEngineConfig config = GetOrCreateConfig();
            GameObject engine = new GameObject("Voxel Sphere Engine");
            VoxelChunkManager manager = engine.AddComponent<VoxelChunkManager>();
            VoxelSphereGenerator sphereGenerator = engine.AddComponent<VoxelSphereGenerator>();
            PlayerChunkTracker tracker = Object.FindFirstObjectByType<PlayerChunkTracker>();
            Transform fallbackAnchor = tracker != null ? tracker.transform : engine.transform;

            sphereGenerator.Configure(manager);
            manager.Configure(config, tracker, sphereGenerator, fallbackAnchor);

            Selection.activeGameObject = engine;
            Undo.RegisterCreatedObjectUndo(engine, "Create Voxel Sphere Engine");
        }

        private static VoxelEngineConfig GetOrCreateConfig()
        {
            const string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            VoxelEngineConfig config = AssetDatabase.LoadAssetAtPath<VoxelEngineConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<VoxelEngineConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static Camera CreatePlayerCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Voxel Player Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2f, -8f);
            cameraObject.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2048f;
            camera.fieldOfView = 70f;
            return camera;
        }
    }
}
