using System;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetLabMetricsSnapshot
    {
        public string operationName;
        public int frame;
        public float realtimeSinceStartup;
        public float frameDeltaMs;
        public float fpsApprox;
        public long managedHeapBytes;
        public long ownedCpuEstimatedBytes;
        public long ownedGpuEstimatedBytes;
        public int liveGraphicsBuffers;
        public int liveComputeBuffers;
        public int liveRenderTextures;
        public int liveRuntimeMeshes;
        public int liveRuntimeTextures;
        public int liveRuntimeMaterials;
        public int liveResourceCount;
        public double operationMs;
        public PlanetLabDiagnostic diagnostic;

        public static PlanetLabMetricsSnapshot Capture(string operationName, PlanetLabResourceRegistry registry, double operationMs)
        {
            PlanetLabMetricsSnapshot snapshot = new PlanetLabMetricsSnapshot
            {
                operationName = operationName,
                frame = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                frameDeltaMs = Time.unscaledDeltaTime * 1000f,
                fpsApprox = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f,
                managedHeapBytes = GC.GetTotalMemory(false),
                operationMs = operationMs
            };

            if (registry != null)
            {
                snapshot.ownedCpuEstimatedBytes = registry.OwnedCpuEstimatedBytes;
                snapshot.ownedGpuEstimatedBytes = registry.OwnedGpuEstimatedBytes;
                snapshot.liveGraphicsBuffers = registry.LiveGraphicsBuffers;
                snapshot.liveComputeBuffers = registry.LiveComputeBuffers;
                snapshot.liveRenderTextures = registry.LiveRenderTextures;
                snapshot.liveRuntimeMeshes = registry.LiveRuntimeMeshes;
                snapshot.liveRuntimeTextures = registry.LiveRuntimeTextures;
                snapshot.liveRuntimeMaterials = registry.LiveRuntimeMaterials;
                snapshot.liveResourceCount = registry.LiveResourceCount;
            }

            snapshot.diagnostic = BuildDiagnostic(snapshot);
            return snapshot;
        }

        private static PlanetLabDiagnostic BuildDiagnostic(PlanetLabMetricsSnapshot snapshot)
        {
            string metrics =
                "Frame delta ms: " + snapshot.frameDeltaMs +
                "\nFPS approx: " + snapshot.fpsApprox +
                "\nManaged heap bytes: " + snapshot.managedHeapBytes +
                "\nCPU owned bytes: " + snapshot.ownedCpuEstimatedBytes +
                "\nGPU owned bytes: " + snapshot.ownedGpuEstimatedBytes +
                "\nLive resources: " + snapshot.liveResourceCount +
                "\nGraphics buffers: " + snapshot.liveGraphicsBuffers +
                "\nCompute buffers: " + snapshot.liveComputeBuffers +
                "\nRender textures: " + snapshot.liveRenderTextures +
                "\nRuntime meshes: " + snapshot.liveRuntimeMeshes +
                "\nRuntime textures: " + snapshot.liveRuntimeTextures +
                "\nRuntime materials: " + snapshot.liveRuntimeMaterials;

            if (snapshot.liveResourceCount > 0)
            {
                return PlanetLabDiagnostic.Warning(
                    "Registered resources are still alive",
                    "At least one registered resource has not been marked as released.",
                    "Run Release All and inspect the owning module if the count does not return to zero.",
                    metrics);
            }

            return PlanetLabDiagnostic.Ok("Lab snapshot is clean", metrics);
        }
    }
}
