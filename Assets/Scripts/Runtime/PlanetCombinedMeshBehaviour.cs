using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class PlanetCombinedMeshBehaviour
    {
        public static readonly ProfilerMarker CombineMeshMarker = new ProfilerMarker("VoxelEngine.CombineMesh");
        public static readonly ProfilerMarker BuildCombineInstancesMarker = new ProfilerMarker("VoxelEngine.BuildCombineInstances");
        public static readonly ProfilerMarker CombineBucketMeshMarker = new ProfilerMarker("VoxelEngine.CombineBucketMesh");

        public readonly List<CombineInstance> combineInstances = new List<CombineInstance>();
        public readonly List<Mesh> scratchSegmentLodChunkMeshes = new List<Mesh>();
        public readonly List<int3> deferredSegmentLodChunkCoords = new List<int3>();
        public readonly List<int3> deferredSegmentLodBuiltChunkCoords = new List<int3>();
        public readonly List<Mesh> deferredSegmentLodChunkMeshes = new List<Mesh>();
        public readonly List<DeferredSegmentLodKey> deferredSegmentLodBuildQueue = new List<DeferredSegmentLodKey>();
        public readonly HashSet<DeferredSegmentLodKey> queuedDeferredSegmentLodBuilds = new HashSet<DeferredSegmentLodKey>();
        public readonly List<CombinedMeshBucket> nearCombinedMeshBuckets = new List<CombinedMeshBucket>();

        public bool combinedMeshesDirty = true;
        public bool combinedMeshLayoutDirty = true;
        public bool combinedRendererVisibilityDirty = true;
        public bool farCombinedMeshDirty = true;
        public bool useNearCombinedMeshes;
        public bool nearCombinedMeshesBuiltOnce;
        public bool combinedMeshRenderModeInitialized;
        public int3 lastNearVisibilityFocusKey;
        public bool hasLastNearVisibilityFocusKey;
        public bool lastNearVisibilityCullRenderedChunks;
        public int activeCombinedMeshBucketCount;
        public CombinedMeshBucket visibleCombinedMeshBucket;
        public CombinedMeshBucket farCombinedMeshBucket;
        public GameObject nearMeshesRoot;
        public Vector3 lastFarHemisphereDirection;
        public bool hasLastFarHemisphereDirection;
        public DeferredSegmentLodBuild activeDeferredSegmentLodBuild;

        public void Tick(
            Action processDeferredSegmentLodBuilds,
            Action refreshFarHemisphereIfNeeded,
            Action updateCombinedMeshForView)
        {
            processDeferredSegmentLodBuilds();
            refreshFarHemisphereIfNeeded();
            updateCombinedMeshForView();
        }

        public void AdvanceDeferredSegmentLodBuild()
        {
            activeDeferredSegmentLodBuild.nextChunkIndex++;
        }
    }
}
