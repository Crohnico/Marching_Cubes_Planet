using System;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetMemorySnapshot
    {
        public string operationName;
        public int frame;
        public float realtimeSinceStartup;
        public long ownedCpuEstimatedBytes;
        public long ownedGpuEstimatedBytes;
        public long ownedCombinedEstimatedBytes;
        public int liveResourceCount;
        public int liveGraphicsBuffers;
        public int liveComputeBuffers;
        public int liveRenderTextures;
        public int liveRuntimeMeshes;
        public int liveRuntimeTextures;
        public int liveRuntimeMaterials;
        public long largestSingleResourceBytes;
        public string largestSingleResourceName;
        public string largestSingleResourceOwner;
        public long unityTotalUsedMemoryBytes;
        public long unityTotalReservedMemoryBytes;
        public long unityAppResidentMemoryBytes;
        public long unityAppCommittedMemoryBytes;
        public long unitySystemUsedMemoryBytes;
        public long unitySystemTotalUsedMemoryBytes;
        public long unityGcUsedBytes;
        public long unityGcReservedBytes;
        public long unityGcAllocFrameBytes;
        public long unityGcAllocFrameCount;
        public long unityGraphicsDriverBytes;
        public long unityGfxReservedBytes;
        public long unityRenderUsedBuffersBytes;
        public long unityRenderUsedBuffersCount;
        public long unityRenderTextureBytes;
        public long unityRenderTextureCount;
        public long unityUsedTextureBytes;
        public long unityUsedTextureCount;
        public long unityTextureMemoryBytes;
        public long unityMeshMemoryBytes;
        public long unityDrawCalls;
        public long unitySetPassCalls;
        public long unityTriangles;
        public long unityVertices;
        public long unityVertexBufferUploadFrameBytes;
        public long unityIndexBufferUploadFrameBytes;
        public double operationMs;
        public PlanetMemoryProfilerCounterValue[] profilerCounters;
        public PlanetLabDiagnostic diagnostic;

        public static PlanetMemorySnapshot Capture(
            string operationName,
            PlanetLabResourceRegistry registry,
            PlanetMemoryBudget budget,
            PlanetMemoryProfilerRecorderSet recorderSet,
            double operationMs,
            bool requireCleanRelease)
        {
            PlanetMemorySnapshot snapshot = new PlanetMemorySnapshot
            {
                operationName = operationName,
                frame = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                operationMs = operationMs,
                profilerCounters = recorderSet != null ? recorderSet.CaptureValues() : Array.Empty<PlanetMemoryProfilerCounterValue>()
            };

            if (registry != null)
            {
                registry.RecalculateLiveTotals();
                snapshot.ownedCpuEstimatedBytes = registry.OwnedCpuEstimatedBytes;
                snapshot.ownedGpuEstimatedBytes = registry.OwnedGpuEstimatedBytes;
                snapshot.ownedCombinedEstimatedBytes = registry.OwnedCpuEstimatedBytes + registry.OwnedGpuEstimatedBytes;
                snapshot.liveResourceCount = registry.LiveResourceCount;
                snapshot.liveGraphicsBuffers = registry.LiveGraphicsBuffers;
                snapshot.liveComputeBuffers = registry.LiveComputeBuffers;
                snapshot.liveRenderTextures = registry.LiveRenderTextures;
                snapshot.liveRuntimeMeshes = registry.LiveRuntimeMeshes;
                snapshot.liveRuntimeTextures = registry.LiveRuntimeTextures;
                snapshot.liveRuntimeMaterials = registry.LiveRuntimeMaterials;
                FindLargestResource(registry, ref snapshot);
            }

            ApplyProfilerCounters(ref snapshot);
            snapshot.diagnostic = PlanetMemoryDiagnostics.Evaluate(snapshot, budget, requireCleanRelease);
            return snapshot;
        }

        private static void FindLargestResource(PlanetLabResourceRegistry registry, ref PlanetMemorySnapshot snapshot)
        {
            for (int i = 0; i < registry.Records.Count; i++)
            {
                PlanetLabResourceRecord record = registry.Records[i];
                if (!record.isAlive || record.estimatedBytes <= snapshot.largestSingleResourceBytes)
                {
                    continue;
                }

                snapshot.largestSingleResourceBytes = record.estimatedBytes;
                snapshot.largestSingleResourceName = record.resourceName;
                snapshot.largestSingleResourceOwner = record.ownerModule;
            }
        }

        private static void ApplyProfilerCounters(ref PlanetMemorySnapshot snapshot)
        {
            if (snapshot.profilerCounters == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.profilerCounters.Length; i++)
            {
                PlanetMemoryProfilerCounterValue counter = snapshot.profilerCounters[i];
                if (!counter.isAvailable)
                {
                    continue;
                }

                switch (counter.counterName)
                {
                    case "App Resident Memory":
                        snapshot.unityAppResidentMemoryBytes = counter.lastValue;
                        break;
                    case "App Committed Memory":
                        snapshot.unityAppCommittedMemoryBytes = counter.lastValue;
                        break;
                    case "Total Used Memory":
                        snapshot.unityTotalUsedMemoryBytes = counter.lastValue;
                        break;
                    case "Total Reserved Memory":
                        snapshot.unityTotalReservedMemoryBytes = counter.lastValue;
                        break;
                    case "System Used Memory":
                        snapshot.unitySystemUsedMemoryBytes = counter.lastValue;
                        break;
                    case "System Total Used Memory":
                        snapshot.unitySystemTotalUsedMemoryBytes = counter.lastValue;
                        break;
                    case "GC Used Memory":
                        snapshot.unityGcUsedBytes = counter.lastValue;
                        break;
                    case "GC Reserved Memory":
                        snapshot.unityGcReservedBytes = counter.lastValue;
                        break;
                    case "GC Allocated In Frame":
                        snapshot.unityGcAllocFrameBytes = counter.lastValue;
                        break;
                    case "GC Allocation In Frame Count":
                        snapshot.unityGcAllocFrameCount = counter.lastValue;
                        break;
                    case "Used Buffers Bytes":
                        snapshot.unityRenderUsedBuffersBytes = counter.lastValue;
                        break;
                    case "Used Buffers Count":
                        snapshot.unityRenderUsedBuffersCount = counter.lastValue;
                        break;
                    case "Render Textures Bytes":
                        snapshot.unityRenderTextureBytes = counter.lastValue;
                        break;
                    case "Render Textures Count":
                        snapshot.unityRenderTextureCount = counter.lastValue;
                        break;
                    case "Used Textures Bytes":
                        snapshot.unityUsedTextureBytes = counter.lastValue;
                        break;
                    case "Used Textures Count":
                        snapshot.unityUsedTextureCount = counter.lastValue;
                        break;
                    case "Gfx Used Memory":
                        snapshot.unityGraphicsDriverBytes = counter.lastValue;
                        break;
                    case "Gfx Reserved Memory":
                        snapshot.unityGfxReservedBytes = counter.lastValue;
                        break;
                    case "Texture Memory":
                        snapshot.unityTextureMemoryBytes = counter.lastValue;
                        break;
                    case "Mesh Memory":
                        snapshot.unityMeshMemoryBytes = counter.lastValue;
                        break;
                    case "Draw Calls Count":
                        snapshot.unityDrawCalls = counter.lastValue;
                        break;
                    case "SetPass Calls Count":
                        snapshot.unitySetPassCalls = counter.lastValue;
                        break;
                    case "Triangles Count":
                        snapshot.unityTriangles = counter.lastValue;
                        break;
                    case "Vertices Count":
                        snapshot.unityVertices = counter.lastValue;
                        break;
                    case "Vertex Buffer Upload In Frame Bytes":
                        snapshot.unityVertexBufferUploadFrameBytes = counter.lastValue;
                        break;
                    case "Index Buffer Upload In Frame Bytes":
                        snapshot.unityIndexBufferUploadFrameBytes = counter.lastValue;
                        break;
                }
            }
        }
    }
}
