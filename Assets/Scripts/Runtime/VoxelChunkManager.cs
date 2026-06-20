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
    public sealed class VoxelChunkManager : MonoBehaviour
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
        private const int MaxCombinedMeshBucketCount = 32;
        private static readonly ProfilerMarker RebuildDesiredMarker = new ProfilerMarker("VoxelEngine.RebuildDesiredChunks");
        private static readonly ProfilerMarker BuildRequestsMarker = new ProfilerMarker("VoxelEngine.BuildOctreeRequests");
        private static readonly ProfilerMarker StartChunkBuildMarker = new ProfilerMarker("VoxelEngine.StartChunkBuild");
        private static readonly ProfilerMarker CompleteChunkBuildMarker = new ProfilerMarker("VoxelEngine.CompleteChunkBuild");
        private static readonly ProfilerMarker UploadMeshMarker = new ProfilerMarker("VoxelEngine.UploadMesh");
        private static readonly ProfilerMarker CombineMeshMarker = new ProfilerMarker("VoxelEngine.CombineMesh");

        [SerializeField] private VoxelEngineConfig config;
        [SerializeField] private PlayerChunkTracker playerChunkTracker;
        [SerializeField] private VoxelSphereGenerator sphereGenerator;
        [SerializeField] private Transform fallbackAnchor;
        [SerializeField] private Material material;
        [SerializeField] private bool generateOnEnable = true;
        [SerializeField] private bool completeInitialBuildSynchronously = true;
        [SerializeField] private bool useChunkCullingForRendering = true;
        [SerializeField] private bool usePlanetActionRadius = true;
        [SerializeField] private bool useChunkCullingForBuildQueue = true;
        [SerializeField] private bool useSegmentedCombinedMeshesNearPlanet = true;
        [SerializeField, Range(1, MaxCombinedMeshBucketCount)] private int nearCombinedMeshBucketCount = MaxCombinedMeshBucketCount;
        [SerializeField, Min(1)] private int maxCombinedMeshBucketsRebuiltPerFrame = 2;
        [SerializeField] private bool useRadialLayerCulling = true;
        [SerializeField, Min(0)] private int neverLayerCullChunkDistance = 3;
        [SerializeField, Min(1)] private int maxChunkBuildsStartedPerFrame = 8;
        [SerializeField, Min(1)] private int maxChunkSwapsPerFrame = 16;
        [SerializeField, Min(1)] private int maxConcurrentChunkBuilds = 32;

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
        private readonly List<CombinedMeshBucket> nearCombinedMeshBuckets = new List<CombinedMeshBucket>();
        private CombineInstance[] combineInstanceBuffer = new CombineInstance[0];
        private int3 priorityCenterChunk;
        private int queuedBuildSequence;
        private bool chunkBuildQueueNeedsSort;
        private bool completedInitialSynchronousBuild;
        private bool combinedMeshesDirty = true;
        private bool combinedMeshLayoutDirty = true;
        private bool farCombinedMeshDirty = true;
        private bool useNearCombinedMeshes;
        private bool nearCombinedMeshesBuiltOnce;
        private int activeCombinedMeshBucketCount;
        private int nextCombinedMeshBucketIndex;
        private CombinedMeshBucket farCombinedMeshBucket;
        private readonly Plane[] chunkCullingFrustumPlanes = new Plane[6];
        private bool chunkVisibilityDirty = true;
        private bool lastShouldCullRenderedChunks;
        private bool hasPlanetActionRadiusState;
        private bool isInsidePlanetActionRadius = true;
        private MarchingCubesCaseTable caseTable;

        public int DeclaredChunkCount => declaredChunks.Count;
        public int DesiredChunkCount => desiredChunkStates.Count;
        public int QueuedChunkBuildCount => chunkBuildQueue.Count;
        public int PendingChunkBuildCount => pendingChunkBuilds.Count;
        public int ActiveChunkCount => activeChunks.Count;
        public int VisibleChunkCount => CountVisibleChunks();
        public int CombinedVertexCount => GetCombinedVertexCount();
        public int CombinedTriangleCount => GetCombinedTriangleCount();
        public bool IsRenderCullingActive => ShouldCullRenderedChunks();

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();
            completedInitialSynchronousBuild = false;
            hasPlanetActionRadiusState = false;

            if (playerChunkTracker != null)
            {
                playerChunkTracker.OnChunkChanged += HandleChunkChanged;
                playerChunkTracker.OnViewChanged += HandleViewChanged;
            }

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
            maxChunkBuildsStartedPerFrame = Mathf.Max(1, maxChunkBuildsStartedPerFrame);
            maxChunkSwapsPerFrame = Mathf.Max(1, maxChunkSwapsPerFrame);
            maxConcurrentChunkBuilds = Mathf.Max(1, maxConcurrentChunkBuilds);
            nearCombinedMeshBucketCount = Mathf.Clamp(nearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
            maxCombinedMeshBucketsRebuiltPerFrame = Mathf.Max(1, maxCombinedMeshBucketsRebuiltPerFrame);
            neverLayerCullChunkDistance = Mathf.Max(0, neverLayerCullChunkDistance);
        }

        private void Update()
        {
            CompleteReadyChunkBuilds();
            ProcessChunkBuildQueue();
            UpdateChunkVisibilityIfNeeded();
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
            if (playerChunkTracker != null)
            {
                playerChunkTracker.OnChunkChanged -= HandleChunkChanged;
                playerChunkTracker.OnViewChanged -= HandleViewChanged;
            }

            ClearChunks();
            DestroyCombinedMesh();
            if (caseTable.IsCreated)
            {
                caseTable.Dispose();
            }
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
            bool shouldCompleteSynchronously = !Application.isPlaying
                || (completeInitialBuildSynchronously && !completedInitialSynchronousBuild);
            if (shouldCompleteSynchronously)
            {
                CompleteAllQueuedChunkBuilds();
            }

            completedInitialSynchronousBuild = true;
            UpdateCombinedMesh(true);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
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

        private void HandleChunkChanged(int3 currentChunk)
        {
            bool actionRadiusChanged = RefreshPlanetActionRadiusState();
            if (ShouldThrottlePlanetUpdatesOutsideActionRadius() && !actionRadiusChanged)
            {
                return;
            }

            RefreshDeclaredChunks();
            RebuildDesiredChunkSet(currentChunk);
        }

        private void HandleViewChanged()
        {
            bool actionRadiusChanged = RefreshPlanetActionRadiusState();
            if (actionRadiusChanged)
            {
                RefreshDeclaredChunks();
                RebuildDesiredChunkSet(GetCurrentCenterChunk());
            }

            if (!ShouldCullRenderedChunks())
            {
                return;
            }

            MarkChunkVisibilityDirty();
            if (ShouldCullChunkBuildQueue()
                && (!ShouldThrottlePlanetUpdatesOutsideActionRadius() || actionRadiusChanged))
            {
                EnqueueVisibleDesiredChunks();
                ReprioritizeChunkBuildQueue();
            }
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
                    int cellSize = config.CoarsestCellSize;
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
            bool shouldCullBuildQueue = ShouldCullChunkBuildQueue();
            if (shouldCullBuildQueue)
            {
                GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            }

            foreach (KeyValuePair<int3, DesiredChunkState> pair in desiredChunkStates)
            {
                if (shouldCullBuildQueue && !IsChunkBuildAllowedWithCurrentPlanes(pair.Key))
                {
                    continue;
                }

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
            while (startedBuilds < maxChunkBuildsStartedPerFrame
                && pendingChunkBuilds.Count < maxConcurrentChunkBuilds
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

                if (!IsChunkBuildAllowed(chunkCoord))
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

        private void CompleteReadyChunkBuilds()
        {
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, PendingChunkBuild> pair in pendingChunkBuilds)
            {
                if (pair.Value.jobHandle.IsCompleted)
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            scratchChunkCoords.Sort(CompareChunkCoordsByPriority);
            int swapCount = math.min(maxChunkSwapsPerFrame, scratchChunkCoords.Count);
            for (int i = 0; i < swapCount; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                PendingChunkBuild pendingBuild = pendingChunkBuilds[chunkCoord];
                pendingChunkBuilds.Remove(chunkCoord);
                CompleteChunkBuild(pendingBuild);
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
            bool shouldCullBuildQueue = ShouldCullChunkBuildQueue();
            if (shouldCullBuildQueue)
            {
                GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            }

            for (int i = chunkBuildQueue.Count - 1; i >= 0; i--)
            {
                QueuedChunkBuild queuedBuild = chunkBuildQueue[i];
                if (!desiredChunkStates.TryGetValue(queuedBuild.chunkCoord, out DesiredChunkState desiredState)
                    || IsChunkReady(queuedBuild.chunkCoord, desiredState)
                    || (shouldCullBuildQueue && !IsChunkBuildAllowedWithCurrentPlanes(queuedBuild.chunkCoord)))
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
            bool shouldCull = ShouldCullRenderedChunks();
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

        private void UpdateChunkVisibility()
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

        private bool IsChunkBuildAllowed(int3 chunkCoord)
        {
            if (!ShouldCullChunkBuildQueue())
            {
                return true;
            }

            GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            return IsChunkBuildAllowedWithCurrentPlanes(chunkCoord);
        }

        private bool IsChunkBuildAllowedWithCurrentPlanes(int3 chunkCoord)
        {
            int3 chunkSize = config.ChunkSize;
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Bounds chunkBounds = BuildChunkBounds(chunkOrigin, chunkSize);
            Camera camera = playerChunkTracker.ChunkCullingCamera;
            return IsChunkInCameraRange(chunkBounds, camera)
                && TestAabbAgainstFrustumCoherent(
                    chunkBounds,
                    chunkCullingFrustumPlanes,
                    0,
                    out _);
        }

        private bool IsChunkRenderVisible(int3 chunkCoord)
        {
            return !ShouldCullRenderedChunks()
                || (activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state) && state.visible.IsVisible);
        }

        private bool UseChunkCulling()
        {
            return playerChunkTracker != null && playerChunkTracker.EnableChunkCulling;
        }

        private bool ShouldUsePlanetActionRadius()
        {
            return usePlanetActionRadius && sphereGenerator != null && config != null;
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

            return sphereGenerator.ContainsActionPoint(GetCurrentDetailFocusVector3(), config);
        }

        private bool ShouldThrottlePlanetUpdatesOutsideActionRadius()
        {
            return ShouldUsePlanetActionRadius()
                && hasPlanetActionRadiusState
                && !isInsidePlanetActionRadius;
        }

        private bool ShouldCullRenderedChunks()
        {
            return useChunkCullingForRendering && UseChunkCulling();
        }

        private bool ShouldCullChunkBuildQueue()
        {
            return useChunkCullingForBuildQueue && UseChunkCulling();
        }

        private static bool IsChunkInCameraRange(Bounds bounds, Camera camera)
        {
            Vector3 closestPoint = bounds.ClosestPoint(camera.transform.position);
            float farClip = camera.farClipPlane;
            return (closestPoint - camera.transform.position).sqrMagnitude <= farClip * farClip;
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
                List<VoxelCellBuildRequest> cellRequests;
                using (BuildRequestsMarker.Auto())
                {
                    cellRequests = BuildCellRequests(
                        chunkOrigin,
                        chunkSize,
                        config.CoarsestCellSize,
                        detailFocus,
                        config,
                        desiredState.boundaryRefinement);
                }

                int cellCount = cellRequests.Count;

                NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
                NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
                NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<float3> normals = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> interiorIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> transitionIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> surfaceIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                ScalarFieldSettings scalarField = GetScalarFieldSettings();

                for (int i = 0; i < cellRequests.Count; i++)
                {
                    requests[i] = cellRequests[i];
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

                    if (!IsChunkBuildAllowed(pendingBuild.chunkCoord))
                    {
                        return;
                    }

                    VoxelChunkAltIndices altIndices;
                    Mesh mesh = BuildMesh(
                        BuildChunkName(pendingBuild.chunkCoord, pendingBuild.cellSize),
                        pendingBuild.vertices,
                        pendingBuild.normals,
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

            int neighborCellSize = config.CoarsestCellSize;
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

        private static List<VoxelCellBuildRequest> BuildCellRequests(
            int3 chunkOrigin,
            int3 chunkSize,
            int maximumCellSize,
            float3 detailFocus,
            VoxelEngineConfig config,
            BoundaryRefinement boundaryRefinement)
        {
            int normalizedMaximumCellSize = math.max(1, maximumCellSize);
            normalizedMaximumCellSize = NormalizeCellSizeForChunk(normalizedMaximumCellSize, chunkSize);
            List<VoxelCellBuildRequest> requests = new List<VoxelCellBuildRequest>();

            if (chunkSize.x != chunkSize.y || chunkSize.x != chunkSize.z || !IsPowerOfTwo(chunkSize.x))
            {
                AddCells(
                    requests,
                    chunkOrigin,
                    chunkOrigin,
                    chunkSize,
                    chunkSize,
                    normalizedMaximumCellSize,
                    boundaryRefinement);
                return requests;
            }

            AddOctreeCells(
                requests,
                chunkOrigin,
                chunkOrigin,
                chunkSize.x,
                chunkSize,
                normalizedMaximumCellSize,
                detailFocus,
                config,
                boundaryRefinement);

            return requests;
        }

        private static void AddOctreeCells(
            List<VoxelCellBuildRequest> requests,
            int3 origin,
            int3 chunkOrigin,
            int nodeSize,
            int3 chunkSize,
            int maximumCellSize,
            float3 detailFocus,
            VoxelEngineConfig config,
            BoundaryRefinement boundaryRefinement)
        {
            int3 localOrigin = origin - chunkOrigin;
            byte chunkBoundarySides = GetChunkBoundarySides(localOrigin, nodeSize, chunkSize);
            int boundaryCellSize = boundaryRefinement.GetSmallestCellSizeForSides(chunkBoundarySides, maximumCellSize);
            int targetCellSize = math.min(
                boundaryCellSize,
                GetOctreeTargetCellSize(origin, nodeSize, maximumCellSize, detailFocus, config));

            if (nodeSize <= targetCellSize || nodeSize <= 1 || (nodeSize & 1) != 0)
            {
                requests.Add(new VoxelCellBuildRequest
                {
                    origin = origin,
                    size = nodeSize
                });
                return;
            }

            int childSize = nodeSize / 2;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        AddOctreeCells(
                            requests,
                            origin + new int3(x, y, z) * childSize,
                            chunkOrigin,
                            childSize,
                            chunkSize,
                            maximumCellSize,
                            detailFocus,
                            config,
                            boundaryRefinement);
                    }
                }
            }
        }

        private static int GetOctreeTargetCellSize(
            int3 origin,
            int nodeSize,
            int maximumCellSize,
            float3 detailFocus,
            VoxelEngineConfig config)
        {
            float distance = GetEuclideanDistanceToCube(origin, nodeSize, detailFocus);
            int target = config.GetCellSizeForWorldDistance(distance);
            return math.min(maximumCellSize, NormalizePowerOfTwoCellSize(target, maximumCellSize));
        }

        private static float GetEuclideanDistanceToCube(int3 origin, int size, float3 point)
        {
            float3 min = new float3(origin.x, origin.y, origin.z);
            float3 max = min + new float3(size, size, size);
            float dx = math.max(math.max(min.x - point.x, 0f), point.x - max.x);
            float dy = math.max(math.max(min.y - point.y, 0f), point.y - max.y);
            float dz = math.max(math.max(min.z - point.z, 0f), point.z - max.z);
            return math.sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static int NormalizePowerOfTwoCellSize(int requestedSize, int maximumCellSize)
        {
            int size = 1;
            int requested = math.max(1, requestedSize);
            while (size < requested && size < maximumCellSize)
            {
                size *= 2;
            }

            return math.min(size, maximumCellSize);
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

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
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

        private static Mesh BuildMesh(
            string meshName,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices,
            out VoxelChunkAltIndices altIndices)
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

                Bounds bounds = CalculateBounds(vertices);

                Vector3[] managedVertices = new Vector3[vertices.Length];
                Vector3[] managedNormals = new Vector3[normals.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    float3 vertex = vertices[i];
                    managedVertices[i] = new Vector3(vertex.x, vertex.y, vertex.z);
                }

                for (int i = 0; i < normals.Length; i++)
                {
                    float3 normal = normals[i];
                    managedNormals[i] = new Vector3(normal.x, normal.y, normal.z);
                }

                mesh.vertices = managedVertices;
                mesh.normals = managedNormals;
                mesh.subMeshCount = LayerSubMeshCount;
                mesh.SetTriangles(ToManagedIndices(interiorIndices), InteriorSubMesh, false);
                mesh.SetTriangles(ToManagedIndices(transitionIndices), TransitionSubMesh, false);
                mesh.SetTriangles(ToManagedIndices(surfaceIndices), SurfaceSubMesh, false);
                mesh.bounds = bounds;
                return mesh;
            }
        }

        private static int[] ToManagedIndices(NativeList<int> indices)
        {
            int[] managedIndices = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                managedIndices[i] = indices[i];
            }

            return managedIndices;
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

        private void EnsureCombinedRenderer()
        {
            RefreshPlanetActionRadiusState();
            EnsureFarCombinedMeshBucket();
            EnsureNearCombinedMeshBucketCount(nearCombinedMeshBucketCount);

            bool shouldUseNearMeshes = ShouldUseNearCombinedMeshes();
            int desiredBucketCount = shouldUseNearMeshes
                ? Mathf.Clamp(nearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount)
                : 0;

            if (useNearCombinedMeshes != shouldUseNearMeshes
                || activeCombinedMeshBucketCount != desiredBucketCount)
            {
                useNearCombinedMeshes = shouldUseNearMeshes;
                activeCombinedMeshBucketCount = desiredBucketCount;
                nextCombinedMeshBucketIndex = 0;
                combinedMeshLayoutDirty = true;
                if (useNearCombinedMeshes)
                {
                    nearCombinedMeshesBuiltOnce = false;
                    MarkAllNearCombinedMeshesDirty();
                }
            }

            ApplyCombinedRendererVisibility();
        }

        private void UpdateCombinedMeshForView()
        {
            if (!combinedMeshesDirty && !combinedMeshLayoutDirty)
            {
                return;
            }

            UpdateCombinedMesh(false);
        }

        private void UpdateCombinedMesh(bool force)
        {
            if (!force && !combinedMeshesDirty && !combinedMeshLayoutDirty)
            {
                return;
            }

            using (CombineMeshMarker.Auto())
            {
                EnsureCombinedRenderer();

                if (force || combinedMeshLayoutDirty)
                {
                    if (useNearCombinedMeshes)
                    {
                        ClearActiveNearCombinedMeshes();
                        MarkAllNearCombinedMeshesDirty();
                    }
                    else if (force || farCombinedMeshBucket == null || farCombinedMeshBucket.mesh == null)
                    {
                        farCombinedMeshDirty = true;
                    }

                    combinedMeshLayoutDirty = false;
                }

                bool needsFarBridge = useNearCombinedMeshes && !nearCombinedMeshesBuiltOnce;
                if ((!useNearCombinedMeshes || needsFarBridge || force)
                    && (force || farCombinedMeshDirty || !IsFarCombinedMeshCached()))
                {
                    RebuildFarCombinedMesh();
                }

                int rebuiltBucketCount = 0;
                int rebuildBudget = force
                    ? activeCombinedMeshBucketCount
                    : Mathf.Max(1, maxCombinedMeshBucketsRebuiltPerFrame);
                int bucketVisitCount = force ? activeCombinedMeshBucketCount : activeCombinedMeshBucketCount;
                int startBucketIndex = force
                    ? 0
                    : Mathf.Clamp(nextCombinedMeshBucketIndex, 0, activeCombinedMeshBucketCount - 1);
                for (int visitIndex = 0; visitIndex < bucketVisitCount; visitIndex++)
                {
                    int bucketIndex = force
                        ? visitIndex
                        : (startBucketIndex + visitIndex) % activeCombinedMeshBucketCount;
                    CombinedMeshBucket bucket = nearCombinedMeshBuckets[bucketIndex];
                    if (!force && !bucket.dirty)
                    {
                        continue;
                    }

                    if (!force && rebuiltBucketCount >= rebuildBudget)
                    {
                        break;
                    }

                    combineInstances.Clear();
                    foreach (VoxelChunkState state in activeChunks.Values)
                    {
                        if (GetCombinedMeshBucketIndex(state.chunkCoord) != bucketIndex)
                        {
                            continue;
                        }

                        if (!state.generated || state.mesh == null || state.mesh.vertexCount == 0)
                        {
                            continue;
                        }

                        if (!ShouldRenderChunk(state))
                        {
                            continue;
                        }

                        AddChunkCombineInstances(state);
                    }

                    bucket.mesh.Clear();
                    bucket.mesh.indexFormat = IndexFormat.UInt32;
                    if (combineInstances.Count > 0)
                    {
                        EnsureCombineInstanceBuffer(combineInstances.Count);
                        for (int i = 0; i < combineInstances.Count; i++)
                        {
                            combineInstanceBuffer[i] = combineInstances[i];
                        }

                        bucket.mesh.CombineMeshes(combineInstanceBuffer, true, true, false);
                        bucket.mesh.RecalculateBounds();
                    }

                    bucket.meshFilter.sharedMesh = bucket.mesh;
                    bucket.meshRenderer.sharedMaterial = material;
                    bucket.dirty = false;
                    rebuiltBucketCount++;
                    nextCombinedMeshBucketIndex = activeCombinedMeshBucketCount > 0
                        ? (bucketIndex + 1) % activeCombinedMeshBucketCount
                        : 0;
                }

                combinedMeshesDirty = HasDirtyCombinedMeshBucket();
                if (useNearCombinedMeshes && !combinedMeshesDirty)
                {
                    nearCombinedMeshesBuiltOnce = true;
                }

                ApplyCombinedRendererVisibility();
            }
        }

        private bool ShouldUseNearCombinedMeshes()
        {
            if (!useSegmentedCombinedMeshesNearPlanet || ShouldThrottlePlanetUpdatesOutsideActionRadius())
            {
                return false;
            }

            return true;
        }

        private void EnsureFarCombinedMeshBucket()
        {
            if (farCombinedMeshBucket == null)
            {
                farCombinedMeshBucket = CreateCombinedMeshBucket(gameObject, "VoxelCombinedMesh_Far");
            }

            farCombinedMeshBucket.meshRenderer.sharedMaterial = material;
            farCombinedMeshBucket.meshFilter.sharedMesh = farCombinedMeshBucket.mesh;
        }

        private void EnsureNearCombinedMeshBucketCount(int requiredBucketCount)
        {
            int safeCount = Mathf.Clamp(requiredBucketCount, 1, MaxCombinedMeshBucketCount);
            while (nearCombinedMeshBuckets.Count < safeCount)
            {
                int bucketIndex = nearCombinedMeshBuckets.Count;
                nearCombinedMeshBuckets.Add(CreateCombinedMeshBucket(
                    CreateCombinedMeshBucketObject(bucketIndex),
                    $"VoxelCombinedMesh_Near_{bucketIndex:00}"));
            }
        }

        private CombinedMeshBucket CreateCombinedMeshBucket(GameObject bucketOwner, string meshName)
        {
            if (!bucketOwner.TryGetComponent(out MeshFilter meshFilter))
            {
                meshFilter = bucketOwner.AddComponent<MeshFilter>();
            }

            if (!bucketOwner.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer = bucketOwner.AddComponent<MeshRenderer>();
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            return new CombinedMeshBucket
            {
                owner = bucketOwner,
                meshFilter = meshFilter,
                meshRenderer = meshRenderer,
                mesh = mesh,
                dirty = true
            };
        }

        private GameObject CreateCombinedMeshBucketObject(int bucketIndex)
        {
            string bucketName = $"VoxelCombinedMesh_Near_{bucketIndex:00}";
            Transform existing = transform.Find(bucketName);
            GameObject bucketOwner = existing != null
                ? existing.gameObject
                : new GameObject(bucketName);
            bucketOwner.transform.SetParent(transform, false);
            bucketOwner.transform.localPosition = Vector3.zero;
            bucketOwner.transform.localRotation = Quaternion.identity;
            bucketOwner.transform.localScale = Vector3.one;
            return bucketOwner;
        }

        private void ApplyCombinedRendererVisibility()
        {
            bool showNearMeshes = useNearCombinedMeshes && nearCombinedMeshesBuiltOnce;
            bool showFarMesh = !showNearMeshes;

            if (farCombinedMeshBucket != null)
            {
                farCombinedMeshBucket.owner.SetActive(true);
                farCombinedMeshBucket.meshRenderer.enabled = showFarMesh;
                farCombinedMeshBucket.meshRenderer.sharedMaterial = material;
                farCombinedMeshBucket.meshFilter.sharedMesh = farCombinedMeshBucket.mesh;
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                bool active = showNearMeshes && i < activeCombinedMeshBucketCount;
                bucket.owner.SetActive(active);
                bucket.meshRenderer.enabled = active;
                bucket.meshRenderer.sharedMaterial = material;
                bucket.meshFilter.sharedMesh = bucket.mesh;
            }
        }

        private void RebuildFarCombinedMesh()
        {
            EnsureFarCombinedMeshBucket();
            RebuildCombinedMeshBucket(farCombinedMeshBucket, 0, false);
            farCombinedMeshDirty = false;
        }

        private void RebuildCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, bool nearBucket)
        {
            combineInstances.Clear();
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                if (nearBucket && GetCombinedMeshBucketIndex(state.chunkCoord) != bucketIndex)
                {
                    continue;
                }

                if (!state.generated || state.mesh == null || state.mesh.vertexCount == 0)
                {
                    continue;
                }

                if (!ShouldRenderChunk(state))
                {
                    continue;
                }

                AddChunkCombineInstances(state);
            }

            bucket.mesh.Clear();
            bucket.mesh.indexFormat = IndexFormat.UInt32;
            if (combineInstances.Count > 0)
            {
                EnsureCombineInstanceBuffer(combineInstances.Count);
                for (int i = 0; i < combineInstances.Count; i++)
                {
                    combineInstanceBuffer[i] = combineInstances[i];
                }

                bucket.mesh.CombineMeshes(combineInstanceBuffer, true, true, false);
                bucket.mesh.RecalculateBounds();
            }

            bucket.meshFilter.sharedMesh = bucket.mesh;
            bucket.meshRenderer.sharedMaterial = material;
        }

        private int GetCombinedMeshBucketIndex(int3 chunkCoord)
        {
            if (!useNearCombinedMeshes || activeCombinedMeshBucketCount <= 1)
            {
                return 0;
            }

            if (!TryGetCombinedMeshBucketGrid(activeCombinedMeshBucketCount, out int3 grid))
            {
                return GetHashedCombinedMeshBucketIndex(chunkCoord);
            }

            int3 chunkSize = config.ChunkSize;
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Vector3 chunkCenter = ToVector3(chunkOrigin) + ToVector3(chunkSize) * 0.5f;
            Vector3 localCenter = sphereGenerator != null
                ? chunkCenter - sphereGenerator.Center
                : chunkCenter;
            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.Radius)
                : Mathf.Max(chunkSize.x, Mathf.Max(chunkSize.y, chunkSize.z));
            Vector3 normalized = (localCenter + Vector3.one * radius) / (radius * 2f);

            int x = Mathf.Clamp((int)(normalized.x * grid.x), 0, grid.x - 1);
            int y = Mathf.Clamp((int)(normalized.y * grid.y), 0, grid.y - 1);
            int z = Mathf.Clamp((int)(normalized.z * grid.z), 0, grid.z - 1);
            return x + grid.x * (y + grid.y * z);
        }

        private static bool TryGetCombinedMeshBucketGrid(int bucketCount, out int3 grid)
        {
            switch (bucketCount)
            {
                case 2:
                    grid = new int3(2, 1, 1);
                    return true;
                case 4:
                    grid = new int3(2, 2, 1);
                    return true;
                case 8:
                    grid = new int3(2, 2, 2);
                    return true;
                case 16:
                    grid = new int3(4, 2, 2);
                    return true;
                case 32:
                    grid = new int3(4, 4, 2);
                    return true;
                default:
                    grid = default;
                    return false;
            }
        }

        private int GetHashedCombinedMeshBucketIndex(int3 chunkCoord)
        {
            unchecked
            {
                uint hash = (uint)chunkCoord.x * 73856093u
                    ^ (uint)chunkCoord.y * 19349663u
                    ^ (uint)chunkCoord.z * 83492791u;
                return (int)(hash % (uint)activeCombinedMeshBucketCount);
            }
        }

        private int GetCombinedVertexCount()
        {
            int count = 0;
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.meshRenderer.enabled && farCombinedMeshBucket.mesh != null)
            {
                count += farCombinedMeshBucket.mesh.vertexCount;
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                Mesh mesh = nearCombinedMeshBuckets[i].mesh;
                if (mesh != null && nearCombinedMeshBuckets[i].meshRenderer.enabled)
                {
                    count += mesh.vertexCount;
                }
            }

            return count;
        }

        private int GetCombinedTriangleCount()
        {
            int count = 0;
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.meshRenderer.enabled && farCombinedMeshBucket.mesh != null)
            {
                count += GetMeshTriangleCount(farCombinedMeshBucket.mesh);
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                Mesh mesh = nearCombinedMeshBuckets[i].mesh;
                if (mesh == null || !nearCombinedMeshBuckets[i].meshRenderer.enabled)
                {
                    continue;
                }

                count += GetMeshTriangleCount(mesh);
            }

            return count;
        }

        private static int GetMeshTriangleCount(Mesh mesh)
        {
            int count = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                count += (int)mesh.GetIndexCount(subMesh) / 3;
            }

            return count;
        }

        private bool ShouldRenderChunk(VoxelChunkState state)
        {
            return IsChunkRenderVisible(state.chunkCoord);
        }

        private void AddChunkCombineInstances(VoxelChunkState state)
        {
            int layerMask = GetChunkLayerMask(state);
            Matrix4x4 chunkTransform = transform.worldToLocalMatrix * Matrix4x4.Translate(ToVector3(state.chunkOrigin));
            AddChunkCombineInstance(state, InteriorSubMesh, VoxelChunkLayerMask.Interior, layerMask, chunkTransform);
            AddChunkCombineInstance(state, TransitionSubMesh, VoxelChunkLayerMask.Transition, layerMask, chunkTransform);
            AddChunkCombineInstance(state, SurfaceSubMesh, VoxelChunkLayerMask.Surface, layerMask, chunkTransform);
        }

        private void AddChunkCombineInstance(
            VoxelChunkState state,
            int subMeshIndex,
            int layerFlag,
            int layerMask,
            Matrix4x4 chunkTransform)
        {
            if ((layerMask & layerFlag) == 0 || !state.altIndices.HasSubMesh(subMeshIndex))
            {
                return;
            }

            combineInstances.Add(new CombineInstance
            {
                mesh = state.mesh,
                subMeshIndex = subMeshIndex,
                transform = chunkTransform
            });
        }

        private int GetChunkLayerMask(VoxelChunkState state)
        {
            if (!useRadialLayerCulling || sphereGenerator == null)
            {
                return VoxelChunkLayerMask.All;
            }

            if (IsChunkInsideNeverLayerCullDistance(state.chunkCoord))
            {
                return VoxelChunkLayerMask.All;
            }

            Vector3 playerPosition = GetCurrentDetailFocusVector3();
            Vector3 sphereCenter = sphereGenerator.Center;
            float sphereRadius = sphereGenerator.Radius;
            bool playerInside = (playerPosition - sphereCenter).sqrMagnitude < sphereRadius * sphereRadius;
            if (playerInside)
            {
                return VoxelChunkLayerMask.Interior | VoxelChunkLayerMask.Transition;
            }

            return IsChunkOnPlayerHemisphere(state.chunkBounds, sphereCenter, playerPosition)
                ? VoxelChunkLayerMask.Surface | VoxelChunkLayerMask.Transition
                : VoxelChunkLayerMask.None;
        }

        private bool IsChunkInsideNeverLayerCullDistance(int3 chunkCoord)
        {
            if (neverLayerCullChunkDistance <= 0)
            {
                return false;
            }

            int3 centerChunk = GetCurrentCenterChunk();
            int3 delta = chunkCoord - centerChunk;
            return math.lengthsq(delta) < neverLayerCullChunkDistance * neverLayerCullChunkDistance;
        }

        private static bool IsChunkOnPlayerHemisphere(Bounds chunkBounds, Vector3 sphereCenter, Vector3 playerPosition)
        {
            Vector3 playerDirection = playerPosition - sphereCenter;
            if (playerDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 chunkDirection = chunkBounds.center - sphereCenter;
            return Vector3.Dot(chunkDirection, playerDirection) >= 0f;
        }

        private void MarkCombinedMeshDirty()
        {
            if (useNearCombinedMeshes)
            {
                MarkAllNearCombinedMeshesDirty();
                return;
            }

            combinedMeshesDirty = true;
            if (!IsFarCombinedMeshCached())
            {
                farCombinedMeshDirty = true;
            }
        }

        private void MarkCombinedMeshDirty(int3 chunkCoord)
        {
            combinedMeshesDirty = true;
            if (!useNearCombinedMeshes)
            {
                if (!IsFarCombinedMeshCached())
                {
                    farCombinedMeshDirty = true;
                }

                return;
            }

            if (combinedMeshLayoutDirty || activeCombinedMeshBucketCount <= 1 || nearCombinedMeshBuckets.Count == 0)
            {
                MarkAllNearCombinedMeshesDirty();
                return;
            }

            int bucketIndex = GetCombinedMeshBucketIndex(chunkCoord);
            if (bucketIndex >= 0 && bucketIndex < nearCombinedMeshBuckets.Count)
            {
                nearCombinedMeshBuckets[bucketIndex].dirty = true;
            }
        }

        private void MarkAllNearCombinedMeshesDirty()
        {
            combinedMeshesDirty = true;
            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                nearCombinedMeshBuckets[i].dirty = true;
            }
        }

        private bool HasDirtyCombinedMeshBucket()
        {
            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                if (nearCombinedMeshBuckets[i].dirty)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFarCombinedMeshCached()
        {
            return farCombinedMeshBucket != null
                && farCombinedMeshBucket.mesh != null
                && farCombinedMeshBucket.mesh.vertexCount > 0;
        }

        private void ClearActiveNearCombinedMeshes()
        {
            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                Mesh mesh = nearCombinedMeshBuckets[i].mesh;
                if (mesh != null)
                {
                    mesh.Clear();
                }
            }
        }

        private void MarkChunkVisibilityDirty()
        {
            chunkVisibilityDirty = true;
        }

        private void EnsureCombineInstanceBuffer(int requiredLength)
        {
            if (combineInstanceBuffer.Length != requiredLength)
            {
                combineInstanceBuffer = new CombineInstance[requiredLength];
            }
        }

        private void ClearCombinedMesh()
        {
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.mesh != null)
            {
                farCombinedMeshBucket.mesh.Clear();
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                if (nearCombinedMeshBuckets[i].mesh != null)
                {
                    nearCombinedMeshBuckets[i].mesh.Clear();
                }
            }

            farCombinedMeshDirty = true;
            nearCombinedMeshesBuiltOnce = false;
            MarkAllNearCombinedMeshesDirty();
        }

        private void DestroyCombinedMesh()
        {
            if (farCombinedMeshBucket != null)
            {
                if (farCombinedMeshBucket.meshFilter != null)
                {
                    farCombinedMeshBucket.meshFilter.sharedMesh = null;
                }

                if (farCombinedMeshBucket.mesh != null)
                {
                    DestroyUnityObject(farCombinedMeshBucket.mesh);
                }

                farCombinedMeshBucket = null;
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.meshFilter != null)
                {
                    bucket.meshFilter.sharedMesh = null;
                }

                if (bucket.mesh != null)
                {
                    DestroyUnityObject(bucket.mesh);
                }

                if (bucket.owner != null && bucket.owner != gameObject)
                {
                    DestroyUnityObject(bucket.owner);
                }
            }

            nearCombinedMeshBuckets.Clear();
            activeCombinedMeshBucketCount = 0;
            useNearCombinedMeshes = false;
            nearCombinedMeshesBuiltOnce = false;
            farCombinedMeshDirty = true;
            combinedMeshLayoutDirty = true;
            combinedMeshesDirty = true;
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
            if (playerChunkTracker != null && playerChunkTracker.HasCurrentChunk)
            {
                return playerChunkTracker.CurrentChunk;
            }

            Transform anchor = fallbackAnchor != null ? fallbackAnchor : transform;
            Vector3 position = anchor.position;
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
            int quantization = math.max(1, (int)math.round(config.OctreeFocusRebuildDistance));
            return (int3)math.floor(GetCurrentDetailFocus() / quantization);
        }

        private void EnsureConfig()
        {
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
            public bool dirty;
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
