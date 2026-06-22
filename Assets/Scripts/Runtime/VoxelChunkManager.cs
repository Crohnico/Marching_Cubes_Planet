using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class VoxelChunkManager : MonoBehaviour
    {
        private const int MaxVerticesPerCell = 36;
        private const byte BoundaryXMin = 1 << 0;
        private const byte BoundaryXMax = 1 << 1;
        private const byte BoundaryYMin = 1 << 2;
        private const byte BoundaryYMax = 1 << 3;
        private const byte BoundaryZMin = 1 << 4;
        private const byte BoundaryZMax = 1 << 5;
        private const int InteriorSubMesh = 0;
        private const int TransitionSubMesh = 1;
        private const int SurfaceSubMesh = 2;
        private const int LayerSubMeshCount = 3;
        private const int MaxCombinedMeshBucketCount = VoxelEngineConfig.MaxCombinedMeshBucketCount;
        private const int SegmentLodCount = 3;
        private const int ActiveSegmentLodIndex = 2;
        private static readonly ProfilerMarker RebuildDesiredMarker = new ProfilerMarker("VoxelEngine.RebuildDesiredChunks");
        private static readonly ProfilerMarker BuildRequestsMarker = new ProfilerMarker("VoxelEngine.BuildCellRequests");
        private static readonly ProfilerMarker StartChunkBuildMarker = new ProfilerMarker("VoxelEngine.StartChunkBuild");
        private static readonly ProfilerMarker CompleteChunkBuildMarker = new ProfilerMarker("VoxelEngine.CompleteChunkBuild");
        private static readonly ProfilerMarker UploadMeshMarker = new ProfilerMarker("VoxelEngine.UploadMesh");
        private static readonly ProfilerMarker CombineMeshMarker = new ProfilerMarker("VoxelEngine.CombineMesh");
        private static readonly ProfilerMarker UpdateVisibilityMarker = new ProfilerMarker("VoxelEngine.UpdateChunkVisibility");
        private static readonly ProfilerMarker BuildCombineInstancesMarker = new ProfilerMarker("VoxelEngine.BuildCombineInstances");
        private static readonly ProfilerMarker CombineBucketMeshMarker = new ProfilerMarker("VoxelEngine.CombineBucketMesh");

        [SerializeField] private PlayerChunkTracker playerChunkTracker;
        [SerializeField] private VoxelSphereGenerator sphereGenerator;
        [SerializeField] private Transform fallbackAnchor;
        [SerializeField] private bool generateOnEnable = true;
        [Header("Debug")]
        [SerializeField] private bool drawSegmentGizmos = true;
        [SerializeField, Range(0f, 90f)] private float farHemisphereRefreshAngle = 3f;

        private readonly Dictionary<int3, VoxelChunkState> activeChunks = new Dictionary<int3, VoxelChunkState>();
        private readonly Dictionary<int3, int> declaredChunkRefCounts = new Dictionary<int3, int>();
        private readonly Dictionary<int3, DesiredChunkState> desiredChunkStates = new Dictionary<int3, DesiredChunkState>();
        private readonly Dictionary<int3, PendingChunkBuild> pendingChunkBuilds = new Dictionary<int3, PendingChunkBuild>();
        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> desiredChunks = new HashSet<int3>();
        private readonly HashSet<int3> queuedChunkBuilds = new HashSet<int3>();
        private readonly List<QueuedChunkBuild> chunkBuildQueue = new List<QueuedChunkBuild>();
        private readonly List<int3> scratchChunkCoords = new List<int3>();
        private readonly List<CombineInstance> combineInstances = new List<CombineInstance>();
        private readonly List<Vector3> meshUploadVertices = new List<Vector3>(65536);
        private readonly List<Vector3> meshUploadNormals = new List<Vector3>(65536);
        private readonly List<Vector2> meshUploadUvs = new List<Vector2>(65536);
        private readonly List<int> meshUploadInteriorIndices = new List<int>(65536);
        private readonly List<int> meshUploadTransitionIndices = new List<int>(65536);
        private readonly List<int> meshUploadSurfaceIndices = new List<int>(65536);
        private readonly List<VoxelCellBuildRequest> cellRequestBuffer = new List<VoxelCellBuildRequest>(32768);
        private readonly List<Mesh> scratchSegmentLodChunkMeshes = new List<Mesh>();
        private readonly List<int3> deferredSegmentLodChunkCoords = new List<int3>();
        private readonly List<int3> deferredSegmentLodBuiltChunkCoords = new List<int3>();
        private readonly List<Mesh> deferredSegmentLodChunkMeshes = new List<Mesh>();
        private readonly List<DeferredSegmentLodKey> deferredSegmentLodBuildQueue = new List<DeferredSegmentLodKey>();
        private readonly HashSet<DeferredSegmentLodKey> queuedDeferredSegmentLodBuilds = new HashSet<DeferredSegmentLodKey>();
        private readonly List<CombinedMeshBucket> nearCombinedMeshBuckets = new List<CombinedMeshBucket>();
        private int3 priorityCenterChunk;
        private int queuedBuildSequence;
        private bool chunkBuildQueueNeedsSort;
        private bool combinedMeshesDirty = true;
        private bool combinedMeshLayoutDirty = true;
        private bool farCombinedMeshDirty = true;
        private bool useNearCombinedMeshes;
        private bool nearCombinedMeshesBuiltOnce;
        private int activeCombinedMeshBucketCount;
        private int nextCombinedMeshBucketIndex;
        private CombinedMeshBucket farCombinedMeshBucket;
        private Vector3 lastFarHemisphereDirection;
        private bool hasLastFarHemisphereDirection;
        private readonly Plane[] chunkCullingFrustumPlanes = new Plane[6];
        private bool chunkVisibilityDirty = true;
        private bool lastShouldCullRenderedChunks;
        private bool hasPlanetActionRadiusState;
        private bool isInsidePlanetActionRadius = true;
        private DeferredSegmentLodBuild activeDeferredSegmentLodBuild;
        private MarchingCubesCaseTable caseTable;
        private VoxelEngineConfig config;

        public int DeclaredChunkCount => declaredChunks.Count;
        public int DesiredChunkCount => desiredChunkStates.Count;
        public int QueuedChunkBuildCount => chunkBuildQueue.Count;
        public int PendingChunkBuildCount => pendingChunkBuilds.Count;
        public int ActiveChunkCount => activeChunks.Count;
        public int VisibleChunkCount => CountVisibleChunks();
        public int CombinedVertexCount => GetCombinedVertexCount();
        public int CombinedTriangleCount => GetCombinedTriangleCount();
        public bool IsRenderCullingActive => ShouldCullRenderedChunks();
        public bool IsFarBridgeActive => IsFarBridgeVisible();
        public bool IsNearCombinedRenderingActive => useNearCombinedMeshes && nearCombinedMeshesBuiltOnce;
        public int NearSegmentCount => NearCombinedMeshBucketCount;
        public Vector3 DetailFocusPosition => GetCurrentDetailFocusVector3();
        private Material TerrainMaterial => sphereGenerator != null ? sphereGenerator.TerrainMaterial : null;
        private bool UseChunkCullingForRendering => config == null || config.UseChunkCullingForRendering;
        private bool UsePlanetActionRadius => config == null || config.UsePlanetActionRadius;
        private bool UseSegmentedCombinedMeshesNearPlanet => config == null || config.UseSegmentedCombinedMeshesNearPlanet;
        private int NearCombinedMeshBucketCount => config != null ? config.NearCombinedMeshBucketCount : MaxCombinedMeshBucketCount;
        private int MaxCombinedMeshBucketsRebuiltPerFrame => config != null ? config.MaxCombinedMeshBucketsRebuiltPerFrame : 2;
        private bool UseSegmentLodSelection => config == null || config.UseSegmentLodSelection;
        private bool UseRadialLayerCulling => config == null || config.UseRadialLayerCulling;
        private int NeverLayerCullChunkDistance => config != null ? config.NeverLayerCullChunkDistance : 3;
        private int MaxChunkBuildsStartedPerFrame => config != null ? config.MaxChunkBuildsStartedPerFrame : 8;
        private int MaxConcurrentChunkBuilds => config != null ? config.MaxConcurrentChunkBuilds : 32;
        private int MaxDeferredSegmentLodChunksBuiltPerFrame => config != null
            ? config.MaxDeferredSegmentLodChunksBuiltPerFrame
            : VoxelEngineConfig.MinDeferredSegmentLodChunksBuiltPerFrame;

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();
            hasPlanetActionRadiusState = false;

            if (generateOnEnable)
            {
                RebuildAroundCurrentAnchor();
            }
        }

        private void Reset()
        {
            TryGetComponent(out sphereGenerator);
            fallbackAnchor = transform;
        }

        private void OnValidate()
        {
            farHemisphereRefreshAngle = Mathf.Clamp(farHemisphereRefreshAngle, 0f, 90f);
        }

        private void Update()
        {
            UpdateChunkVisibilityIfNeeded();
            UpdateNearSegmentVisibility();
            ProcessDeferredSegmentLodBuilds();
            RefreshFarHemisphereIfNeeded();
            UpdateCombinedMeshForView();
        }

        public void Configure(
            VoxelEngineConfig nextConfig,
            PlayerChunkTracker nextPlayerChunkTracker,
            VoxelSphereGenerator nextSphereGenerator,
            Transform nextFallbackAnchor)
        {
            config = nextConfig;
            playerChunkTracker = nextPlayerChunkTracker;
            sphereGenerator = nextSphereGenerator;
            fallbackAnchor = nextFallbackAnchor;
        }

        private void OnDisable()
        {
            ClearChunks();
            DestroyCombinedMesh();
            if (caseTable.IsCreated)
            {
                caseTable.Dispose();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawSegmentGizmos)
            {
                return;
            }

            int segmentCount = Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(segmentCount, out int3 grid))
            {
                return;
            }

            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : 1f;
            Vector3 center = sphereGenerator != null ? sphereGenerator.Center : Vector3.zero;
            Vector3 size = new Vector3(
                (radius * 2f) / grid.x,
                (radius * 2f) / grid.y,
                (radius * 2f) / grid.z);
            int focusSegment = GetSegmentIndexForWorldPosition(GetCurrentDetailFocusVector3(), segmentCount);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, Vector3.one * radius * 2f);

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                Vector3 segmentCenter = GetSegmentLocalCenter(segmentIndex, segmentCount);
                Gizmos.color = segmentIndex == focusSegment
                    ? new Color(0f, 1f, 0.25f, 0.95f)
                    : new Color(0.1f, 0.65f, 1f, 0.45f);
                Gizmos.DrawWireCube(segmentCenter, size);

                Gizmos.color = segmentIndex == focusSegment
                    ? new Color(0f, 1f, 0.25f, 1f)
                    : new Color(1f, 0.9f, 0.1f, 0.85f);
                Gizmos.DrawSphere(segmentCenter, radius * 0.0125f);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        [ContextMenu("Rebuild Active Chunks")]
        public void RebuildAroundCurrentAnchor()
        {
            EnsureConfig();
            EnsureCaseTable();
            RefreshPlanetActionRadiusState();
            int3 centerChunk = GetCurrentCenterChunk();
            RefreshDeclaredChunks();

            RebuildDesiredChunkSet(centerChunk);
            CompleteAllQueuedChunkBuilds();
            UpdateCombinedMesh(true);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            ClearCombinedMesh();
            MarkAllChunksDirty();
            RebuildAroundCurrentAnchor();
        }

        [ContextMenu("Clear Generated Chunks")]
        public void ClearGeneratedChunks()
        {
            ClearChunks();
        }

        public void DeclareChunk(int3 chunkCoord)
        {
            if (declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                declaredChunkRefCounts[chunkCoord] = refCount + 1;
                return;
            }

            declaredChunkRefCounts.Add(chunkCoord, 1);
            declaredChunks.Add(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            if (!declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                return;
            }

            if (refCount > 1)
            {
                declaredChunkRefCounts[chunkCoord] = refCount - 1;
                return;
            }

            declaredChunkRefCounts.Remove(chunkCoord);
            declaredChunks.Remove(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            declaredChunkRefCounts.Clear();
            declaredChunks.Clear();
        }

        public void DeclareChunkBounds(int3 minInclusive, int3 maxInclusive)
        {
            for (int x = minInclusive.x; x <= maxInclusive.x; x++)
            {
                for (int y = minInclusive.y; y <= maxInclusive.y; y++)
                {
                    for (int z = minInclusive.z; z <= maxInclusive.z; z++)
                    {
                        DeclareChunk(new int3(x, y, z));
                    }
                }
            }
        }

        public bool IsChunkDeclared(int3 chunkCoord)
        {
            return declaredChunks.Contains(chunkCoord);
        }

        public GameObject GetChunkObjectOrNull(int3 chunkCoord)
        {
            return activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state)
                ? state.owner
                : null;
        }

        public Transform GetNearSegmentTransformOrNull(int segmentIndex)
        {
            if (segmentIndex < 0)
            {
                return null;
            }

            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);
            if (segmentIndex >= nearCombinedMeshBuckets.Count || nearCombinedMeshBuckets[segmentIndex].owner == null)
            {
                return null;
            }

            CombinedMeshBucket bucket = nearCombinedMeshBuckets[segmentIndex];
            EnsureSegmentLodCache(bucket);
            return bucket.lodCache != null ? bucket.lodCache.PivotTransform : bucket.owner.transform;
        }

        public int GetNearSegmentIndexForWorldPosition(Vector3 worldPosition)
        {
            return GetSegmentIndexForWorldPosition(worldPosition, Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount));
        }

        private void RebuildDesiredChunkSet(int3 centerChunk)
        {
            using (RebuildDesiredMarker.Auto())
            {
                priorityCenterChunk = centerChunk;
                desiredChunks.Clear();
                desiredChunkStates.Clear();
                int3 detailFocusKey = GetCurrentDetailFocusKey();

                foreach (int3 chunkCoord in declaredChunks)
                {
                    int cellSize = ResolveCellSizeForChunk(chunkCoord);
                    BoundaryRefinement boundaryRefinement = GetBoundaryRefinement(chunkCoord, cellSize);
                    desiredChunks.Add(chunkCoord);
                    desiredChunkStates[chunkCoord] = new DesiredChunkState(cellSize, boundaryRefinement, detailFocusKey);
                }

                MarkChunkVisibilityDirty();
                EnqueueVisibleDesiredChunks();
                RemoveUndesiredChunks();
                ReprioritizeChunkBuildQueue();
            }
        }

        private void EnqueueVisibleDesiredChunks()
        {
            foreach (KeyValuePair<int3, DesiredChunkState> pair in desiredChunkStates)
            {
                EnqueueChunkState(pair.Key, pair.Value);
            }
        }

        private void EnqueueChunkState(int3 chunkCoord, DesiredChunkState desiredState)
        {
            if (IsChunkReady(chunkCoord, desiredState))
            {
                return;
            }

            if (queuedChunkBuilds.Add(chunkCoord))
            {
                chunkBuildQueue.Add(new QueuedChunkBuild(
                    chunkCoord,
                    GetChunkDistance(chunkCoord - priorityCenterChunk),
                    desiredState.cellSize,
                    queuedBuildSequence++));
                chunkBuildQueueNeedsSort = true;
            }
        }

        private bool IsChunkReady(int3 chunkCoord, DesiredChunkState desiredState)
        {
            return activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state)
                && state.generated
                && !state.dirty
                && state.cellSize == desiredState.cellSize
                && state.boundaryRefinement.Equals(desiredState.boundaryRefinement)
                && state.detailFocusKey.Equals(desiredState.detailFocusKey);
        }

        private void ProcessChunkBuildQueue()
        {
            SortChunkBuildQueueIfNeeded();

            int startedBuilds = 0;
            int attempts = chunkBuildQueue.Count;
            while (startedBuilds < MaxChunkBuildsStartedPerFrame
                && pendingChunkBuilds.Count < MaxConcurrentChunkBuilds
                && attempts > 0
                && chunkBuildQueue.Count > 0)
            {
                attempts--;
                int queuedBuildIndex = chunkBuildQueue.Count - 1;
                QueuedChunkBuild queuedBuild = chunkBuildQueue[queuedBuildIndex];
                chunkBuildQueue.RemoveAt(queuedBuildIndex);
                int3 chunkCoord = queuedBuild.chunkCoord;
                queuedChunkBuilds.Remove(chunkCoord);

                if (!desiredChunkStates.TryGetValue(chunkCoord, out DesiredChunkState desiredState))
                {
                    continue;
                }

                if (IsChunkReady(chunkCoord, desiredState))
                {
                    continue;
                }

                if (pendingChunkBuilds.ContainsKey(chunkCoord))
                {
                    if (queuedChunkBuilds.Add(chunkCoord))
                    {
                        chunkBuildQueue.Add(new QueuedChunkBuild(
                        chunkCoord,
                        GetChunkDistance(chunkCoord - priorityCenterChunk),
                        desiredState.cellSize,
                        queuedBuild.sequence));
                        chunkBuildQueueNeedsSort = true;
                    }

                    continue;
                }

                pendingChunkBuilds.Add(chunkCoord, StartChunkBuild(chunkCoord, desiredState));
                startedBuilds++;
            }
        }

        private void CompleteAllQueuedChunkBuilds()
        {
            while (chunkBuildQueue.Count > 0 || pendingChunkBuilds.Count > 0)
            {
                ProcessChunkBuildQueue();
                CompleteAllPendingChunkBuilds();
            }
        }

        private void ReprioritizeChunkBuildQueue()
        {
            for (int i = chunkBuildQueue.Count - 1; i >= 0; i--)
            {
                QueuedChunkBuild queuedBuild = chunkBuildQueue[i];
                if (!desiredChunkStates.TryGetValue(queuedBuild.chunkCoord, out DesiredChunkState desiredState)
                    || IsChunkReady(queuedBuild.chunkCoord, desiredState))
                {
                    queuedChunkBuilds.Remove(queuedBuild.chunkCoord);
                    chunkBuildQueue.RemoveAt(i);
                    continue;
                }

                queuedBuild.distanceToPriorityCenter = GetChunkDistance(queuedBuild.chunkCoord - priorityCenterChunk);
                queuedBuild.cellSize = desiredState.cellSize;
                chunkBuildQueue[i] = queuedBuild;
            }

            SortChunkBuildQueue();
            chunkBuildQueueNeedsSort = false;
        }

        private void SortChunkBuildQueue()
        {
            chunkBuildQueue.Sort(CompareQueuedChunkBuilds);
        }

        private void SortChunkBuildQueueIfNeeded()
        {
            if (!chunkBuildQueueNeedsSort)
            {
                return;
            }

            SortChunkBuildQueue();
            chunkBuildQueueNeedsSort = false;
        }

        private void UpdateChunkVisibilityIfNeeded()
        {
            if (UseSegmentLodSelection && useNearCombinedMeshes)
            {
                chunkVisibilityDirty = false;
                lastShouldCullRenderedChunks = ShouldCullRenderedChunks();
                return;
            }

            bool shouldCull = ShouldCullRenderedChunks();
            if (!shouldCull && !lastShouldCullRenderedChunks)
            {
                chunkVisibilityDirty = false;
                return;
            }

            if (shouldCull != lastShouldCullRenderedChunks)
            {
                lastShouldCullRenderedChunks = shouldCull;
                MarkChunkVisibilityDirty();
            }

            if (!chunkVisibilityDirty)
            {
                return;
            }

            UpdateChunkVisibility();
            chunkVisibilityDirty = false;
        }

        private void UpdateNearSegmentVisibility()
        {
            if (!UseSegmentLodSelection || !useNearCombinedMeshes || !nearCombinedMeshesBuiltOnce)
            {
                return;
            }

            bool shouldCullFrustum = ShouldCullRenderedChunks() && playerChunkTracker.ChunkCullingCamera != null;
            if (shouldCullFrustum)
            {
                GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.owner != null && !bucket.owner.activeSelf)
                {
                    bucket.owner.SetActive(true);
                }

                if (bucket.lodCache == null)
                {
                    continue;
                }

                bucket.lodCache.SetPivotActive(IsNearSegmentVisible(i, shouldCullFrustum));
            }
        }

        private void UpdateChunkVisibility()
        {
            using (UpdateVisibilityMarker.Auto())
            {
                bool visibilityChanged = false;
                bool shouldCull = ShouldCullRenderedChunks();
                if (shouldCull)
                {
                    GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
                }

                foreach (VoxelChunkState state in activeChunks.Values)
                {
                    bool inRange = true;
                    bool inFrustum = true;
                    int lastPlaneIndex = state.frustumLastPlaneIndex;
                    if (shouldCull)
                    {
                        inRange = IsChunkInCameraRange(state.chunkBounds, playerChunkTracker.ChunkCullingCamera);
                        inFrustum = TestAabbAgainstFrustumCoherent(
                            state.chunkBounds,
                            chunkCullingFrustumPlanes,
                            state.frustumLastPlaneIndex,
                            out lastPlaneIndex);
                    }

                    if (state.visible.inRange != inRange
                        || state.visible.inFrustum != inFrustum
                        || state.frustumLastPlaneIndex != lastPlaneIndex)
                    {
                        state.visible = new ChunkVisibility
                        {
                            inRange = inRange,
                            inFrustum = inFrustum
                        };
                        state.frustumLastPlaneIndex = lastPlaneIndex;
                        visibilityChanged = true;
                        MarkCombinedMeshDirty(state.chunkCoord);
                    }
                }

                if (visibilityChanged)
                {
                    combinedMeshesDirty = true;
                }
            }
        }

        private bool IsChunkRenderVisible(int3 chunkCoord)
        {
            return !ShouldCullRenderedChunks()
                || (activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state) && state.visible.IsVisible);
        }

        private bool UseChunkCulling()
        {
            return playerChunkTracker != null
                && playerChunkTracker.EnableChunkCulling;
        }

        private bool ShouldUsePlanetActionRadius()
        {
            return UsePlanetActionRadius && sphereGenerator != null && config != null;
        }

        private bool RefreshPlanetActionRadiusState()
        {
            bool nextInside = IsCurrentFocusInsidePlanetActionRadius();
            if (hasPlanetActionRadiusState && nextInside == isInsidePlanetActionRadius)
            {
                return false;
            }

            isInsidePlanetActionRadius = nextInside;
            hasPlanetActionRadiusState = true;
            combinedMeshLayoutDirty = true;
            if (nextInside)
            {
                MarkAllNearCombinedMeshesDirty();
            }
            else
            {
                combinedMeshesDirty = true;
            }

            return true;
        }

        private bool IsCurrentFocusInsidePlanetActionRadius()
        {
            if (!ShouldUsePlanetActionRadius())
            {
                return true;
            }

            if (UseSegmentLodSelection)
            {
                return IsCurrentFocusInsideSegmentLodActivationRadius();
            }

            return sphereGenerator.ContainsActionPoint(GetCurrentDetailFocusVector3(), config);
        }

        private bool IsCurrentFocusInsideSegmentLodActivationRadius()
        {
            if (sphereGenerator == null)
            {
                return true;
            }

            Vector3 focus = GetCurrentDetailFocusVector3();
            float effectiveRadius = Mathf.Max(0f, sphereGenerator.Radius + GetMaximumConfiguredLodDistance());
            return (focus - sphereGenerator.Center).sqrMagnitude <= effectiveRadius * effectiveRadius;
        }

        private float GetMaximumConfiguredLodDistance()
        {
            return config.GetMaxWorldDistanceForLod(GetSegmentLodCount() - 1);
        }

        private bool ShouldThrottlePlanetUpdatesOutsideActionRadius()
        {
            return ShouldUsePlanetActionRadius()
                && hasPlanetActionRadiusState
                && !isInsidePlanetActionRadius;
        }

        private bool ShouldCullRenderedChunks()
        {
            return UseChunkCullingForRendering && UseChunkCulling();
        }

        private static bool IsChunkInCameraRange(Bounds bounds, Camera camera)
        {
            Vector3 point = camera.transform.position;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - extents.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - extents.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - center.z) - extents.z, 0f);
            float farClip = camera.farClipPlane;
            return dx * dx + dy * dy + dz * dz <= farClip * farClip;
        }

        private static bool TestAabbAgainstFrustumCoherent(
            Bounds bounds,
            Plane[] frustumPlanes,
            int startPlaneIndex,
            out int lastPlaneIndex)
        {
            int planeCount = frustumPlanes.Length;
            int safeStart = planeCount == 0 ? 0 : Mathf.Clamp(startPlaneIndex, 0, planeCount - 1);
            for (int offset = 0; offset < planeCount; offset++)
            {
                int planeIndex = (safeStart + offset) % planeCount;
                if (IsAabbOutsidePlane(bounds, frustumPlanes[planeIndex]))
                {
                    lastPlaneIndex = planeIndex;
                    return false;
                }
            }

            lastPlaneIndex = safeStart;
            return true;
        }

        private static bool IsAabbOutsidePlane(Bounds bounds, Plane plane)
        {
            Vector3 positive = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3 normal = plane.normal;
            positive.x += normal.x >= 0f ? extents.x : -extents.x;
            positive.y += normal.y >= 0f ? extents.y : -extents.y;
            positive.z += normal.z >= 0f ? extents.z : -extents.z;
            return plane.GetDistanceToPoint(positive) < 0f;
        }

        private int CountVisibleChunks()
        {
            if (!ShouldCullRenderedChunks())
            {
                return activeChunks.Count;
            }

            int count = 0;
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                if (state.visible.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private int CompareChunkCoordsByPriority(int3 a, int3 b)
        {
            int distanceComparison = GetChunkDistance(a - priorityCenterChunk).CompareTo(GetChunkDistance(b - priorityCenterChunk));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return CompareInt3(a, b);
        }

        private static int CompareQueuedChunkBuilds(QueuedChunkBuild a, QueuedChunkBuild b)
        {
            int distanceComparison = b.distanceToPriorityCenter.CompareTo(a.distanceToPriorityCenter);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            int cellSizeComparison = b.cellSize.CompareTo(a.cellSize);
            if (cellSizeComparison != 0)
            {
                return cellSizeComparison;
            }

            return b.sequence.CompareTo(a.sequence);
        }

        private static int CompareInt3(int3 a, int3 b)
        {
            int xComparison = a.x.CompareTo(b.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = a.y.CompareTo(b.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return a.z.CompareTo(b.z);
        }

        private void CompleteAllPendingChunkBuilds()
        {
            scratchChunkCoords.Clear();
            foreach (int3 chunkCoord in pendingChunkBuilds.Keys)
            {
                scratchChunkCoords.Add(chunkCoord);
            }

            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                PendingChunkBuild pendingBuild = pendingChunkBuilds[chunkCoord];
                pendingChunkBuilds.Remove(chunkCoord);
                CompleteChunkBuild(pendingBuild);
            }
        }

        private PendingChunkBuild StartChunkBuild(int3 chunkCoord, DesiredChunkState desiredState)
        {
            using (StartChunkBuildMarker.Auto())
            {
                int3 chunkSize = config.ChunkSize;
                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
                float3 detailFocus = GetCurrentDetailFocus();
                using (BuildRequestsMarker.Auto())
                {
                    BuildCellRequests(
                        cellRequestBuffer,
                        chunkOrigin,
                        chunkSize,
                        desiredState.cellSize,
                        detailFocus,
                        config,
                        desiredState.boundaryRefinement);
                }

                int cellCount = cellRequestBuffer.Count;

                NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
                NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
                NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<float3> normals = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<float2> uvs = new NativeList<float2>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> interiorIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> transitionIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> surfaceIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                ScalarFieldSettings scalarField = GetScalarFieldSettings();

                for (int i = 0; i < cellRequestBuffer.Count; i++)
                {
                    requests[i] = cellRequestBuffer[i];
                }

                EvaluateVoxelCellsJob evaluateJob = new EvaluateVoxelCellsJob
                {
                    scalarField = scalarField,
                    requests = requests,
                    cells = cells
                };

                JobHandle evaluateHandle = evaluateJob.Schedule(cellCount, 64);

                GenerateChunkMeshJob meshJob = new GenerateChunkMeshJob
                {
                    cells = cells,
                    cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                    cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                    triangulation = caseTable.triangulation,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    scalarField = scalarField,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices
                };

                return new PendingChunkBuild
                {
                    chunkCoord = chunkCoord,
                    cellSize = desiredState.cellSize,
                    boundaryRefinement = desiredState.boundaryRefinement,
                    detailFocusKey = desiredState.detailFocusKey,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    requests = requests,
                    cells = cells,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices,
                    jobHandle = meshJob.Schedule(evaluateHandle)
                };
            }
        }

        private void CompleteChunkBuild(PendingChunkBuild pendingBuild)
        {
            using (CompleteChunkBuildMarker.Auto())
            {
                pendingBuild.jobHandle.Complete();

                try
                {
                    DesiredChunkState completedState = new DesiredChunkState(
                        pendingBuild.cellSize,
                        pendingBuild.boundaryRefinement,
                        pendingBuild.detailFocusKey);
                    bool isStillDesired = desiredChunkStates.TryGetValue(pendingBuild.chunkCoord, out DesiredChunkState desiredState)
                        && desiredState.Equals(completedState);
                    if (!isStillDesired)
                    {
                        EnqueueChunkStateIfStillDesired(pendingBuild.chunkCoord);
                        return;
                    }

                    VoxelChunkAltIndices altIndices;
                    Mesh mesh = BuildMesh(
                        BuildChunkName(pendingBuild.chunkCoord, pendingBuild.cellSize),
                        pendingBuild.vertices,
                        pendingBuild.normals,
                        pendingBuild.uvs,
                        pendingBuild.interiorIndices,
                        pendingBuild.transitionIndices,
                        pendingBuild.surfaceIndices,
                        out altIndices);

                    VoxelChunkState nextState = new VoxelChunkState
                    {
                        chunkCoord = pendingBuild.chunkCoord,
                        cellSize = pendingBuild.cellSize,
                        boundaryRefinement = pendingBuild.boundaryRefinement,
                        detailFocusKey = pendingBuild.detailFocusKey,
                        owner = gameObject,
                        mesh = mesh,
                        altIndices = altIndices,
                        chunkOrigin = pendingBuild.chunkOrigin,
                        chunkBounds = BuildChunkBounds(pendingBuild.chunkOrigin, pendingBuild.chunkSize),
                        visible = ChunkVisibility.Visible,
                        generated = true,
                        dirty = false
                    };

                    if (activeChunks.TryGetValue(pendingBuild.chunkCoord, out VoxelChunkState oldState))
                    {
                        DestroyChunk(oldState);
                        activeChunks[pendingBuild.chunkCoord] = nextState;
                        MarkChunkVisibilityDirty();
                        MarkCombinedMeshDirty(pendingBuild.chunkCoord);
                        return;
                    }

                    activeChunks.Add(pendingBuild.chunkCoord, nextState);
                    MarkChunkVisibilityDirty();
                    MarkCombinedMeshDirty(pendingBuild.chunkCoord);
                }
                finally
                {
                    DisposePendingChunkBuild(pendingBuild);
                }
            }
        }

        private void EnqueueChunkStateIfStillDesired(int3 chunkCoord)
        {
            if (desiredChunkStates.TryGetValue(chunkCoord, out DesiredChunkState desiredState))
            {
                EnqueueChunkState(chunkCoord, desiredState);
            }
        }

        private BoundaryRefinement GetBoundaryRefinement(int3 chunkCoord, int cellSize)
        {
            BoundaryRefinement refinement = BoundaryRefinement.Empty;
            if (cellSize <= 1)
            {
                return refinement;
            }

            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(-1, 0, 0), cellSize, BoundaryXMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(1, 0, 0), cellSize, BoundaryXMax);
            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(0, -1, 0), cellSize, BoundaryYMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(0, 1, 0), cellSize, BoundaryYMax);
            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(0, 0, -1), cellSize, BoundaryZMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, new int3(0, 0, 1), cellSize, BoundaryZMax);
            return refinement;
        }

        private void AddBoundaryRefinement(
            ref BoundaryRefinement refinement,
            int3 chunkCoord,
            int3 direction,
            int cellSize,
            byte side)
        {
            int3 neighborCoord = chunkCoord + direction;
            if (!declaredChunks.Contains(neighborCoord))
            {
                return;
            }

            int neighborCellSize = ResolveCellSizeForChunk(neighborCoord);
            if (neighborCellSize >= cellSize)
            {
                return;
            }

            refinement.Set(side, GetSharedBoundaryCellSize(cellSize, neighborCellSize));
        }

        private static int GetChunkDistance(int3 chunkOffset)
        {
            return math.max(math.abs(chunkOffset.x), math.max(math.abs(chunkOffset.y), math.abs(chunkOffset.z)));
        }

        private static int GetSharedBoundaryCellSize(int cellSize, int neighborCellSize)
        {
            return math.max(1, GreatestCommonDivisor(cellSize, neighborCellSize));
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            a = math.abs(a);
            b = math.abs(b);
            while (b != 0)
            {
                int remainder = a % b;
                a = b;
                b = remainder;
            }

            return math.max(1, a);
        }

        private static void BuildCellRequests(
            List<VoxelCellBuildRequest> requests,
            int3 chunkOrigin,
            int3 chunkSize,
            int maximumCellSize,
            float3 detailFocus,
            VoxelEngineConfig config,
            BoundaryRefinement boundaryRefinement,
            bool clearRequests = true)
        {
            int normalizedMaximumCellSize = math.max(1, maximumCellSize);
            normalizedMaximumCellSize = NormalizeCellSizeForChunk(normalizedMaximumCellSize, chunkSize);
            if (clearRequests)
            {
                requests.Clear();
            }

            AddCells(
                requests,
                chunkOrigin,
                chunkOrigin,
                chunkSize,
                chunkSize,
                normalizedMaximumCellSize,
                boundaryRefinement);
        }

        private static int NormalizeCellSizeForChunk(int requestedSize, int3 chunkSize)
        {
            int requested = math.max(1, math.min(requestedSize, math.min(chunkSize.x, math.min(chunkSize.y, chunkSize.z))));
            for (int size = requested; size >= 1; size--)
            {
                if (chunkSize.x % size == 0
                    && chunkSize.y % size == 0
                    && chunkSize.z % size == 0)
                {
                    return size;
                }
            }

            return 1;
        }

        private static void AddCells(
            List<VoxelCellBuildRequest> requests,
            int3 origin,
            int3 chunkOrigin,
            int3 size,
            int3 chunkSize,
            int cellSize,
            BoundaryRefinement boundaryRefinement)
        {
            for (int x = 0; x < size.x; x += cellSize)
            {
                for (int y = 0; y < size.y; y += cellSize)
                {
                    for (int z = 0; z < size.z; z += cellSize)
                    {
                        int3 refinedOrigin = origin + new int3(x, y, z);
                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = refinedOrigin,
                            size = cellSize
                        });
                    }
                }
            }
        }

        private static byte GetChunkBoundarySides(int3 localOrigin, int cellSize, int3 chunkSize)
        {
            byte sides = 0;
            if (localOrigin.x == 0)
            {
                sides |= BoundaryXMin;
            }

            if (localOrigin.x + cellSize == chunkSize.x)
            {
                sides |= BoundaryXMax;
            }

            if (localOrigin.y == 0)
            {
                sides |= BoundaryYMin;
            }

            if (localOrigin.y + cellSize == chunkSize.y)
            {
                sides |= BoundaryYMax;
            }

            if (localOrigin.z == 0)
            {
                sides |= BoundaryZMin;
            }

            if (localOrigin.z + cellSize == chunkSize.z)
            {
                sides |= BoundaryZMax;
            }

            return sides;
        }

        private Mesh BuildMesh(
            string meshName,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices,
            out VoxelChunkAltIndices altIndices,
            bool transformVerticesToLocal = false,
            Matrix4x4 worldToLocalMatrix = default)
        {
            using (UploadMeshMarker.Auto())
            {
                altIndices = new VoxelChunkAltIndices(
                    interiorIndices.Length,
                    transitionIndices.Length,
                    surfaceIndices.Length);
                Mesh mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                if (vertices.Length == 0 || altIndices.TotalIndexCount == 0)
                {
                    mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                    return mesh;
                }

                if (transformVerticesToLocal)
                {
                    CopyToVector3List(vertices, meshUploadVertices, worldToLocalMatrix);
                    CopyNormalsToVector3List(normals, meshUploadNormals, worldToLocalMatrix);
                }
                else
                {
                    CopyToVector3List(vertices, meshUploadVertices);
                    CopyToVector3List(normals, meshUploadNormals);
                }

                Bounds bounds = CalculateBounds(meshUploadVertices);
                CopyToVector2List(uvs, meshUploadUvs);
                CopyToIntList(interiorIndices, meshUploadInteriorIndices);
                CopyToIntList(transitionIndices, meshUploadTransitionIndices);
                CopyToIntList(surfaceIndices, meshUploadSurfaceIndices);

                mesh.SetVertices(meshUploadVertices);
                mesh.SetNormals(meshUploadNormals);
                mesh.SetUVs(0, meshUploadUvs);
                mesh.subMeshCount = LayerSubMeshCount;
                mesh.SetTriangles(meshUploadInteriorIndices, InteriorSubMesh, false);
                mesh.SetTriangles(meshUploadTransitionIndices, TransitionSubMesh, false);
                mesh.SetTriangles(meshUploadSurfaceIndices, SurfaceSubMesh, false);
                mesh.bounds = bounds;
                return mesh;
            }
        }

        private static Bounds CalculateBounds(List<Vector3> vertices)
        {
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                Vector3 vertex = vertices[i];
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void CopyToVector3List(NativeList<float3> source, List<Vector3> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float3 value = source[i];
                destination.Add(new Vector3(value.x, value.y, value.z));
            }
        }

        private static void CopyToVector3List(NativeList<float3> source, List<Vector3> destination, Matrix4x4 transformMatrix)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float3 value = source[i];
                destination.Add(transformMatrix.MultiplyPoint3x4(new Vector3(value.x, value.y, value.z)));
            }
        }

        private static void CopyNormalsToVector3List(NativeList<float3> source, List<Vector3> destination, Matrix4x4 transformMatrix)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float3 value = source[i];
                destination.Add(transformMatrix.MultiplyVector(new Vector3(value.x, value.y, value.z)).normalized);
            }
        }

        private static void CopyToVector2List(NativeList<float2> source, List<Vector2> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float2 value = source[i];
                destination.Add(new Vector2(value.x, value.y));
            }
        }

        private static void CopyToIntList(NativeList<int> source, List<int> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void EnsureListCapacity<T>(List<T> list, int requiredCapacity)
        {
            if (list.Capacity < requiredCapacity)
            {
                list.Capacity = requiredCapacity;
            }
        }

        private static Bounds CalculateBounds(NativeList<float3> vertices)
        {
            float3 min = vertices[0];
            float3 max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                float3 vertex = vertices[i];
                min = math.min(min, vertex);
                max = math.max(max, vertex);
            }

            float3 center = (min + max) * 0.5f;
            float3 size = max - min;
            return new Bounds(
                new Vector3(center.x, center.y, center.z),
                new Vector3(size.x, size.y, size.z));
        }

        private void MarkChunkVisibilityDirty()
        {
            chunkVisibilityDirty = true;
        }

        private static Bounds BuildChunkBounds(int3 chunkOrigin, int3 chunkSize)
        {
            Vector3 size = ToVector3(chunkSize);
            return new Bounds(ToVector3(chunkOrigin) + size * 0.5f, size);
        }

        private static Vector3 ToVector3(int3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private Vector3 GetCurrentDetailFocusVector3()
        {
            float3 focus = GetCurrentDetailFocus();
            return new Vector3(focus.x, focus.y, focus.z);
        }

        private void RemoveUndesiredChunks()
        {
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, VoxelChunkState> pair in activeChunks)
            {
                if (!desiredChunks.Contains(pair.Key))
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                DestroyChunk(activeChunks[chunkCoord]);
                activeChunks.Remove(chunkCoord);
                MarkChunkVisibilityDirty();
                MarkCombinedMeshDirty(chunkCoord);
            }
        }

        private void ClearChunks()
        {
            CompleteAndDisposePendingChunkBuilds();
            chunkBuildQueue.Clear();
            queuedChunkBuilds.Clear();
            queuedBuildSequence = 0;
            chunkBuildQueueNeedsSort = false;

            foreach (VoxelChunkState state in activeChunks.Values)
            {
                DestroyChunk(state);
            }

            activeChunks.Clear();
            desiredChunks.Clear();
            desiredChunkStates.Clear();
            MarkChunkVisibilityDirty();
            ClearCombinedMesh();
        }

        private void CompleteAndDisposePendingChunkBuilds()
        {
            foreach (PendingChunkBuild pendingBuild in pendingChunkBuilds.Values)
            {
                pendingBuild.jobHandle.Complete();
                DisposePendingChunkBuild(pendingBuild);
            }

            pendingChunkBuilds.Clear();
        }

        private static void DisposePendingChunkBuild(PendingChunkBuild pendingBuild)
        {
            if (pendingBuild.requests.IsCreated)
            {
                pendingBuild.requests.Dispose();
            }

            if (pendingBuild.cells.IsCreated)
            {
                pendingBuild.cells.Dispose();
            }

            if (pendingBuild.vertices.IsCreated)
            {
                pendingBuild.vertices.Dispose();
            }

            if (pendingBuild.normals.IsCreated)
            {
                pendingBuild.normals.Dispose();
            }

            if (pendingBuild.uvs.IsCreated)
            {
                pendingBuild.uvs.Dispose();
            }

            if (pendingBuild.interiorIndices.IsCreated)
            {
                pendingBuild.interiorIndices.Dispose();
            }

            if (pendingBuild.transitionIndices.IsCreated)
            {
                pendingBuild.transitionIndices.Dispose();
            }

            if (pendingBuild.surfaceIndices.IsCreated)
            {
                pendingBuild.surfaceIndices.Dispose();
            }
        }

        private static void DisposeIfCreated<T>(NativeArray<T> array)
            where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        private static void DisposeIfCreated<T>(NativeList<T> list)
            where T : unmanaged
        {
            if (list.IsCreated)
            {
                list.Dispose();
            }
        }

        private void RefreshDeclaredChunks()
        {
            if (sphereGenerator != null)
            {
                sphereGenerator.DeclareOccupiedChunks(this, config.ChunkSize);
            }
        }

        private void MarkAllChunksDirty()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                state.dirty = true;
            }
        }

        private ScalarFieldSettings GetScalarFieldSettings()
        {
            return sphereGenerator != null
                ? sphereGenerator.BuildScalarFieldSettings()
                : config.ScalarFieldSettings;
        }

        private static void DestroyChunk(VoxelChunkState state)
        {
            if (state.mesh != null)
            {
                DestroyUnityObject(state.mesh);
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private int3 GetCurrentCenterChunk()
        {
            Vector3 position = playerChunkTracker != null
                ? playerChunkTracker.TrackedPosition
                : (fallbackAnchor != null ? fallbackAnchor.position : transform.position);
            return VoxelChunkUtility.GetChunkCoords(new float3(position.x, position.y, position.z), config.ChunkSize);
        }

        private float3 GetCurrentDetailFocus()
        {
            if (playerChunkTracker != null)
            {
                Vector3 trackedPosition = playerChunkTracker.TrackedPosition;
                return new float3(trackedPosition.x, trackedPosition.y, trackedPosition.z);
            }

            Transform anchor = fallbackAnchor != null ? fallbackAnchor : transform;
            Vector3 position = anchor.position;
            return new float3(position.x, position.y, position.z);
        }

        private int3 GetCurrentDetailFocusKey()
        {
            int quantization = math.max(1, math.min(config.ChunkSize.x, math.min(config.ChunkSize.y, config.ChunkSize.z)));
            return (int3)math.floor(GetCurrentDetailFocus() / quantization);
        }

        private void EnsureConfig()
        {
            if (config != null)
            {
                return;
            }

            config = Resources.Load<VoxelEngineConfig>(VoxelEngineConfig.ResourceName);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<VoxelEngineConfig>();
            }
        }

        private void EnsureCaseTable()
        {
            if (!caseTable.IsCreated)
            {
                caseTable = MarchingCubesCaseTableBuilder.Build(Allocator.Persistent);
            }
        }

        private static string BuildChunkName(int3 chunkCoord, int cellSize)
        {
            return $"VoxelChunk_{chunkCoord.x}_{chunkCoord.y}_{chunkCoord.z}_S{cellSize}";
        }

        private sealed class VoxelChunkState
        {
            public int3 chunkCoord;
            public int cellSize;
            public BoundaryRefinement boundaryRefinement;
            public int3 detailFocusKey;
            public GameObject owner;
            public Mesh mesh;
            public VoxelChunkAltIndices altIndices;
            public int3 chunkOrigin;
            public Bounds chunkBounds;
            public ChunkVisibility visible;
            public int frustumLastPlaneIndex;
            public bool generated;
            public bool dirty;
        }

        private sealed class CombinedMeshBucket
        {
            public GameObject owner;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public Mesh mesh;
            public VoxelSegmentLodMeshCache lodCache;
            public Mesh[] lodMeshes;
            public bool[] lodDirty;
            public bool[] lodCached;
            public int activeLodIndex;
            public CombineInstance[] combineInstanceBuffer;
            public bool dirty;
        }

        private struct DeferredSegmentLodBuild
        {
            public bool active;
            public DeferredSegmentLodKey key;
            public int nextChunkIndex;
            public int cellSize;
        }

        private readonly struct DeferredSegmentLodKey : System.IEquatable<DeferredSegmentLodKey>
        {
            public readonly int bucketIndex;
            public readonly int lodIndex;

            public DeferredSegmentLodKey(int bucketIndex, int lodIndex)
            {
                this.bucketIndex = bucketIndex;
                this.lodIndex = lodIndex;
            }

            public bool Equals(DeferredSegmentLodKey other)
            {
                return bucketIndex == other.bucketIndex && lodIndex == other.lodIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is DeferredSegmentLodKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (bucketIndex * 397) ^ lodIndex;
                }
            }
        }

        private struct QueuedChunkBuild
        {
            public int3 chunkCoord;
            public int distanceToPriorityCenter;
            public int cellSize;
            public int sequence;

            public QueuedChunkBuild(int3 chunkCoord, int distanceToPriorityCenter, int cellSize, int sequence)
            {
                this.chunkCoord = chunkCoord;
                this.distanceToPriorityCenter = distanceToPriorityCenter;
                this.cellSize = cellSize;
                this.sequence = sequence;
            }
        }

        private struct DesiredChunkState
        {
            public int cellSize;
            public BoundaryRefinement boundaryRefinement;
            public int3 detailFocusKey;

            public DesiredChunkState(int cellSize, BoundaryRefinement boundaryRefinement, int3 detailFocusKey)
            {
                this.cellSize = cellSize;
                this.boundaryRefinement = boundaryRefinement;
                this.detailFocusKey = detailFocusKey;
            }

            public bool Equals(DesiredChunkState other)
            {
                return cellSize == other.cellSize
                    && boundaryRefinement.Equals(other.boundaryRefinement)
                    && detailFocusKey.Equals(other.detailFocusKey);
            }
        }

        private sealed class PendingChunkBuild
        {
            public int3 chunkCoord;
            public int cellSize;
            public BoundaryRefinement boundaryRefinement;
            public int3 detailFocusKey;
            public int3 chunkOrigin;
            public int3 chunkSize;
            public NativeArray<VoxelCellBuildRequest> requests;
            public NativeArray<VoxelCell> cells;
            public NativeList<float3> vertices;
            public NativeList<float3> normals;
            public NativeList<float2> uvs;
            public NativeList<int> interiorIndices;
            public NativeList<int> transitionIndices;
            public NativeList<int> surfaceIndices;
            public JobHandle jobHandle;
        }

        private struct ChunkVisibility
        {
            public bool inRange;
            public bool inFrustum;

            public static ChunkVisibility Visible => new ChunkVisibility
            {
                inRange = true,
                inFrustum = true
            };

            public bool IsVisible => inRange && inFrustum;
        }

        private struct VoxelChunkAltIndices
        {
            public int interiorIndexCount;
            public int transitionIndexCount;
            public int surfaceIndexCount;

            public VoxelChunkAltIndices(int interiorIndexCount, int transitionIndexCount, int surfaceIndexCount)
            {
                this.interiorIndexCount = interiorIndexCount;
                this.transitionIndexCount = transitionIndexCount;
                this.surfaceIndexCount = surfaceIndexCount;
            }

            public int TotalIndexCount => interiorIndexCount + transitionIndexCount + surfaceIndexCount;

            public bool HasSubMesh(int subMeshIndex)
            {
                switch (subMeshIndex)
                {
                    case InteriorSubMesh:
                        return interiorIndexCount > 0;
                    case TransitionSubMesh:
                        return transitionIndexCount > 0;
                    case SurfaceSubMesh:
                        return surfaceIndexCount > 0;
                    default:
                        return false;
                }
            }
        }

        private static class VoxelChunkLayerMask
        {
            public const int None = 0;
            public const int Interior = 1 << 0;
            public const int Transition = 1 << 1;
            public const int Surface = 1 << 2;
            public const int All = Interior | Transition | Surface;
        }

        private struct BoundaryRefinement
        {
            public byte sides;
            public int xMinCellSize;
            public int xMaxCellSize;
            public int yMinCellSize;
            public int yMaxCellSize;
            public int zMinCellSize;
            public int zMaxCellSize;

            public static BoundaryRefinement Empty => default;

            public void Set(byte side, int cellSize)
            {
                sides |= side;
                switch (side)
                {
                    case BoundaryXMin:
                        xMinCellSize = cellSize;
                        break;
                    case BoundaryXMax:
                        xMaxCellSize = cellSize;
                        break;
                    case BoundaryYMin:
                        yMinCellSize = cellSize;
                        break;
                    case BoundaryYMax:
                        yMaxCellSize = cellSize;
                        break;
                    case BoundaryZMin:
                        zMinCellSize = cellSize;
                        break;
                    case BoundaryZMax:
                        zMaxCellSize = cellSize;
                        break;
                }
            }

            public int GetSmallestCellSizeForSides(byte touchedSides, int fallbackCellSize)
            {
                int result = fallbackCellSize;
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryXMin, xMinCellSize, result);
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryXMax, xMaxCellSize, result);
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryYMin, yMinCellSize, result);
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryYMax, yMaxCellSize, result);
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryZMin, zMinCellSize, result);
                result = GetSmallestCellSizeForSide(touchedSides, BoundaryZMax, zMaxCellSize, result);
                return result;
            }

            public bool Equals(BoundaryRefinement other)
            {
                return sides == other.sides
                    && xMinCellSize == other.xMinCellSize
                    && xMaxCellSize == other.xMaxCellSize
                    && yMinCellSize == other.yMinCellSize
                    && yMaxCellSize == other.yMaxCellSize
                    && zMinCellSize == other.zMinCellSize
                    && zMaxCellSize == other.zMaxCellSize;
            }

            private static int GetSmallestCellSizeForSide(byte touchedSides, byte side, int cellSize, int current)
            {
                if ((touchedSides & side) == 0 || cellSize <= 0)
                {
                    return current;
                }

                return math.min(current, cellSize);
            }
        }
    }
}




