using System.Diagnostics;
using System.IO;
using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    public static class VoxelEngineBenchmark
    {
        private const string DefaultScenePath = "Assets/Scenes/PlanetGeneratorScene.unity";
        private const string ResultPath = "Temp/VoxelEngineBenchmark.txt";

        [MenuItem("Tools/Voxel Engine/Benchmark Current Scene")]
        public static void BenchmarkCurrentScene()
        {
            RunBenchmark(false);
        }

        public static void RunFromCommandLine()
        {
            RunBenchmark(true);
        }

        private static void RunBenchmark(bool exitWhenDone)
        {
            if (!EditorSceneManager.GetActiveScene().isLoaded
                || EditorSceneManager.GetActiveScene().path != DefaultScenePath)
            {
                EditorSceneManager.OpenScene(DefaultScenePath);
            }

            VoxelChunkManager manager = Object.FindFirstObjectByType<VoxelChunkManager>();
            if (manager == null)
            {
                Finish("Voxel benchmark failed: no VoxelChunkManager found.", exitWhenDone, 1);
                return;
            }

            manager.ClearGeneratedChunks();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            long memoryBefore = System.GC.GetTotalMemory(true);
            Stopwatch stopwatch = Stopwatch.StartNew();
            manager.Generate();
            stopwatch.Stop();
            long memoryAfter = System.GC.GetTotalMemory(false);

            Mesh combinedMesh = manager.GetComponent<MeshFilter>() != null
                ? manager.GetComponent<MeshFilter>().sharedMesh
                : null;
            int vertices = combinedMesh != null ? combinedMesh.vertexCount : 0;
            int indices = combinedMesh != null ? (int)combinedMesh.GetIndexCount(0) : 0;
            string result =
                $"Voxel benchmark\n" +
                $"Scene: {DefaultScenePath}\n" +
                $"Generate ms: {stopwatch.Elapsed.TotalMilliseconds:0.###}\n" +
                $"Vertices: {vertices}\n" +
                $"Triangles: {indices / 3}\n" +
                $"Managed memory delta KB: {(memoryAfter - memoryBefore) / 1024f:0.###}\n";

            Finish(result, exitWhenDone, 0);
        }

        private static void Finish(string message, bool exitWhenDone, int exitCode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, message);
            UnityEngine.Debug.Log(message);
            if (exitWhenDone)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
