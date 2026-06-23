using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public sealed partial class PlanetManager
    {
        private Dictionary<int3, VoxelChunkState> activeChunks => chunkBehaviour.ActiveChunks;
        private Dictionary<int3, DesiredChunkState> desiredChunkStates => chunkBehaviour.DesiredChunkStates;
        private HashSet<int3> declaredChunks => chunkBehaviour.DeclaredChunks;
        private List<VoxelCellBuildRequest> cellRequestBuffer => chunkBehaviour.CellRequestBuffer;
        private Plane[] chunkCullingFrustumPlanes => chunkBehaviour.CullingFrustumPlanes;

        private List<CombineInstance> combineInstances => combinedMeshBehaviour.combineInstances;
        private List<Mesh> scratchSegmentLodChunkMeshes => combinedMeshBehaviour.scratchSegmentLodChunkMeshes;
        private List<int3> deferredSegmentLodChunkCoords => combinedMeshBehaviour.deferredSegmentLodChunkCoords;
        private List<int3> deferredSegmentLodBuiltChunkCoords => combinedMeshBehaviour.deferredSegmentLodBuiltChunkCoords;
        private List<Mesh> deferredSegmentLodChunkMeshes => combinedMeshBehaviour.deferredSegmentLodChunkMeshes;
        private List<DeferredSegmentLodKey> deferredSegmentLodBuildQueue => combinedMeshBehaviour.deferredSegmentLodBuildQueue;
        private HashSet<DeferredSegmentLodKey> queuedDeferredSegmentLodBuilds => combinedMeshBehaviour.queuedDeferredSegmentLodBuilds;
        private List<CombinedMeshBucket> nearCombinedMeshBuckets => combinedMeshBehaviour.nearCombinedMeshBuckets;
        private bool combinedMeshesDirty { get => combinedMeshBehaviour.combinedMeshesDirty; set => combinedMeshBehaviour.combinedMeshesDirty = value; }
        private bool combinedMeshLayoutDirty { get => combinedMeshBehaviour.combinedMeshLayoutDirty; set => combinedMeshBehaviour.combinedMeshLayoutDirty = value; }
        private bool combinedRendererVisibilityDirty { get => combinedMeshBehaviour.combinedRendererVisibilityDirty; set => combinedMeshBehaviour.combinedRendererVisibilityDirty = value; }
        private bool farCombinedMeshDirty { get => combinedMeshBehaviour.farCombinedMeshDirty; set => combinedMeshBehaviour.farCombinedMeshDirty = value; }
        private bool useNearCombinedMeshes { get => combinedMeshBehaviour.useNearCombinedMeshes; set => combinedMeshBehaviour.useNearCombinedMeshes = value; }
        private bool nearCombinedMeshesBuiltOnce { get => combinedMeshBehaviour.nearCombinedMeshesBuiltOnce; set => combinedMeshBehaviour.nearCombinedMeshesBuiltOnce = value; }
        private bool combinedMeshRenderModeInitialized { get => combinedMeshBehaviour.combinedMeshRenderModeInitialized; set => combinedMeshBehaviour.combinedMeshRenderModeInitialized = value; }
        private int3 lastNearVisibilityFocusKey { get => combinedMeshBehaviour.lastNearVisibilityFocusKey; set => combinedMeshBehaviour.lastNearVisibilityFocusKey = value; }
        private bool hasLastNearVisibilityFocusKey { get => combinedMeshBehaviour.hasLastNearVisibilityFocusKey; set => combinedMeshBehaviour.hasLastNearVisibilityFocusKey = value; }
        private bool lastNearVisibilityCullRenderedChunks { get => combinedMeshBehaviour.lastNearVisibilityCullRenderedChunks; set => combinedMeshBehaviour.lastNearVisibilityCullRenderedChunks = value; }
        private int activeCombinedMeshBucketCount { get => combinedMeshBehaviour.activeCombinedMeshBucketCount; set => combinedMeshBehaviour.activeCombinedMeshBucketCount = value; }
        private CombinedMeshBucket visibleCombinedMeshBucket { get => combinedMeshBehaviour.visibleCombinedMeshBucket; set => combinedMeshBehaviour.visibleCombinedMeshBucket = value; }
        private CombinedMeshBucket farCombinedMeshBucket { get => combinedMeshBehaviour.farCombinedMeshBucket; set => combinedMeshBehaviour.farCombinedMeshBucket = value; }
        private GameObject nearMeshesRoot { get => combinedMeshBehaviour.nearMeshesRoot; set => combinedMeshBehaviour.nearMeshesRoot = value; }
        private Vector3 lastFarHemisphereDirection { get => combinedMeshBehaviour.lastFarHemisphereDirection; set => combinedMeshBehaviour.lastFarHemisphereDirection = value; }
        private bool hasLastFarHemisphereDirection { get => combinedMeshBehaviour.hasLastFarHemisphereDirection; set => combinedMeshBehaviour.hasLastFarHemisphereDirection = value; }
        private DeferredSegmentLodBuild activeDeferredSegmentLodBuild { get => combinedMeshBehaviour.activeDeferredSegmentLodBuild; set => combinedMeshBehaviour.activeDeferredSegmentLodBuild = value; }
    }
}
