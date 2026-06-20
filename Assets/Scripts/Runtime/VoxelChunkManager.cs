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
        [SerializeField] private bool useChunkCullingForRendering;
        [SerializeField, Min(1)] private int maxChunkBuildsStartedPerFrame = 8;
        [SerializeField, Min(1)] private int maxChunkSwapsPerFrame = 16;
        [SerializeField, Min(1)] private int maxConcurrentChunkBuilds = 32;

        private readonly Dictionary<int3, VoxelChunkState> activeChunks = new Dictionary<int3, VoxelChunkState>();
        private readonly Dictionary<int3, int> declaredChunkRefCounts = new Dictionary<int3, int>();
        private readonly Dictionary<int3, DesiredChunkState> desiredChunkStates = new Dictionary<int3, DesiredChunkState>();
        private readonly Dictionary<int3, PendingChunkBuild> pendingChunkBuilds = new Dictionary<int3, PendingChunkBuild>();
        private readonly Dictionary<int3, bool> chunkVisibility = new Dictionary<int3, bool>();
        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> desiredChunks = new HashSet<int3>();
        private readonly HashSet<int3> queuedChunkBuilds = new HashSet<int3>();
        private readonly List<QueuedChunkBuild> chunkBuildQueue = new List<QueuedChunkBuild>();
        private readonly List<int3> scratchChunkCoords = new List<int3>();
        private readonly List<int3> cullingChunkCoords = new List<int3>();
        private readonly List<CombineInstance> combineInstances = new List<CombineInstance>();
        private CombineInstance[] combineInstanceBuffer = new CombineInstance[0];
        private int3 priorityCenterChunk;
        private int queuedBuildSequence;
        private bool chunkBuildQueueNeedsSort;
        private bool completedInitialSynchronousBuild;
        private bool combinedMeshDirty = true;
        private MeshFilter combinedMeshFilter;
        private MeshRenderer combinedMeshRenderer;
        private Mesh combinedMesh;
        private CullingGroup chunkCullingGroup;
        private BoundingSphere[] cullingSpheres = new BoundingSphere[1];
        private readonly Plane[] chunkCullingFrustumPlanes = new Plane[6];
        private MarchingCubesCaseTable caseTable;

        public int DeclaredChunkCount => declaredChunks.Count;
        public int DesiredChunkCount => desiredChunkStates.Count;
        public int QueuedChunkBuildCount => chunkBuildQueue.Count;
        public int PendingChunkBuildCount => pendingChunkBuilds.Count;
        public int ActiveChunkCount => activeChunks.Count;
        public int VisibleChunkCount => CountVisibleChunks();
        public int CombinedVertexCount => combinedMesh != null ? combinedMesh.vertexCount : 0;
        public int CombinedTriangleCount => combinedMesh != null && combinedMesh.subMeshCount > 0 ? (int)combinedMesh.GetIndexCount(0) / 3 : 0;
        public bool IsRenderCullingActive => false;

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();
            EnsureChunkCullingGroup();
            completedInitialSynchronousBuild = false;

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
        }

        private void Update()
        {
            EnsureChunkCullingGroup();
            CompleteReadyChunkBuilds();
            ProcessChunkBuildQueue();
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
            DisposeChunkCullingGroup();
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
            RefreshDeclaredChunks();
            RebuildDesiredChunkSet(currentChunk);
        }

        private void HandleViewChanged()
        {
            EnqueueVisibleDesiredChunks();
            ReprioritizeChunkBuildQueue();
            MarkCombinedMeshDirty();
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

                RebuildChunkCullingGroup();
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

        private void EnsureChunkCullingGroup()
        {
            if (!UseChunkCulling())
            {
                DisposeChunkCullingGroup();
                return;
            }

            Camera targetCamera = playerChunkTracker.ChunkCullingCamera;
            if (chunkCullingGroup == null)
            {
                chunkCullingGroup = new CullingGroup();
                chunkCullingGroup.onStateChanged = HandleChunkCullingStateChanged;
            }

            if (chunkCullingGroup.targetCamera != targetCamera)
            {
                chunkCullingGroup.targetCamera = targetCamera;
                RebuildChunkCullingGroup();
            }
        }

        private void RebuildChunkCullingGroup()
        {
            chunkVisibility.Clear();
            cullingChunkCoords.Clear();

            if (!UseChunkCulling())
            {
                DisposeChunkCullingGroup();
                MarkCombinedMeshDirty();
                return;
            }

            EnsureChunkCullingGroup();
            int requiredCount = desiredChunkStates.Count;
            if (requiredCount == 0)
            {
                chunkCullingGroup.SetBoundingSphereCount(0);
                MarkCombinedMeshDirty();
                return;
            }

            if (cullingSpheres.Length < requiredCount)
            {
                cullingSpheres = new BoundingSphere[requiredCount];
            }

            int index = 0;
            GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            foreach (int3 chunkCoord in desiredChunkStates.Keys)
            {
                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, config.ChunkSize);
                Bounds bounds = BuildChunkBounds(chunkOrigin, config.ChunkSize);
                cullingSpheres[index] = new BoundingSphere(bounds.center, bounds.extents.magnitude);
                cullingChunkCoords.Add(chunkCoord);
                chunkVisibility[chunkCoord] = IsChunkVisibleToPlayerCamera(bounds, chunkCullingFrustumPlanes);
                index++;
            }

            chunkCullingGroup.SetBoundingSpheres(cullingSpheres);
            chunkCullingGroup.SetBoundingSphereCount(index);
            MarkCombinedMeshDirty();
        }

        private void HandleChunkCullingStateChanged(CullingGroupEvent cullingEvent)
        {
            if (cullingEvent.index < 0 || cullingEvent.index >= cullingChunkCoords.Count)
            {
                return;
            }

            int3 chunkCoord = cullingChunkCoords[cullingEvent.index];
            chunkVisibility[chunkCoord] = cullingEvent.isVisible;
            MarkCombinedMeshDirty();

            if (cullingEvent.isVisible)
            {
                EnqueueChunkStateIfStillDesired(chunkCoord);
                return;
            }

            ReprioritizeChunkBuildQueue();
        }

        private bool IsChunkBuildAllowed(int3 chunkCoord)
        {
            return true;
        }

        private bool IsChunkRenderVisible(int3 chunkCoord)
        {
            return true;
        }

        private bool UseChunkCulling()
        {
            return playerChunkTracker != null && playerChunkTracker.EnableChunkCulling;
        }

        private static bool IsChunkVisibleToPlayerCamera(Bounds bounds, Plane[] frustumPlanes)
        {
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        private void DisposeChunkCullingGroup()
        {
            if (chunkCullingGroup != null)
            {
                chunkCullingGroup.Dispose();
                chunkCullingGroup = null;
            }

            chunkVisibility.Clear();
            cullingChunkCoords.Clear();
        }

        private int CountVisibleChunks()
        {
            if (!UseChunkCulling())
            {
                return activeChunks.Count;
            }

            int count = 0;
            foreach (bool visible in chunkVisibility.Values)
            {
                if (visible)
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
                NativeList<int> indices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
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
                    declaredNeighborSides = GetDeclaredNeighborSides(chunkCoord),
                    scalarField = scalarField,
                    vertices = vertices,
                    normals = normals,
                    indices = indices
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
                    indices = indices,
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

                    Mesh mesh = BuildMesh(
                        BuildChunkName(pendingBuild.chunkCoord, pendingBuild.cellSize),
                        pendingBuild.vertices,
                        pendingBuild.normals,
                        pendingBuild.indices);

                    VoxelChunkState nextState = new VoxelChunkState
                    {
                        chunkCoord = pendingBuild.chunkCoord,
                        cellSize = pendingBuild.cellSize,
                        boundaryRefinement = pendingBuild.boundaryRefinement,
                        detailFocusKey = pendingBuild.detailFocusKey,
                        owner = gameObject,
                        mesh = mesh,
                        chunkOrigin = pendingBuild.chunkOrigin,
                        chunkBounds = BuildChunkBounds(pendingBuild.chunkOrigin, pendingBuild.chunkSize),
                        generated = true,
                        dirty = false
                    };

                    if (activeChunks.TryGetValue(pendingBuild.chunkCoord, out VoxelChunkState oldState))
                    {
                        DestroyChunk(oldState);
                        activeChunks[pendingBuild.chunkCoord] = nextState;
                        MarkCombinedMeshDirty();
                        return;
                    }

                    activeChunks.Add(pendingBuild.chunkCoord, nextState);
                    MarkCombinedMeshDirty();
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

        private byte GetDeclaredNeighborSides(int3 chunkCoord)
        {
            byte sides = 0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(-1, 0, 0)) ? BoundaryXMin : (byte)0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(1, 0, 0)) ? BoundaryXMax : (byte)0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(0, -1, 0)) ? BoundaryYMin : (byte)0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(0, 1, 0)) ? BoundaryYMax : (byte)0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(0, 0, -1)) ? BoundaryZMin : (byte)0;
            sides |= declaredChunks.Contains(chunkCoord + new int3(0, 0, 1)) ? BoundaryZMax : (byte)0;
            return sides;
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
                    size = nodeSize,
                    boundarySides = chunkBoundarySides
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
                        int3 localOrigin = refinedOrigin - chunkOrigin;
                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = refinedOrigin,
                            size = cellSize,
                            boundarySides = (byte)(GetChunkBoundarySides(localOrigin, cellSize, chunkSize) & boundaryRefinement.sides)
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
            NativeList<int> indices)
        {
            using (UploadMeshMarker.Auto())
            {
                Mesh mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                if (vertices.Length == 0 || indices.Length == 0)
                {
                    mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                    return mesh;
                }

                Bounds bounds = CalculateBounds(vertices);

                Vector3[] managedVertices = new Vector3[vertices.Length];
                Vector3[] managedNormals = new Vector3[normals.Length];
                int[] managedIndices = new int[indices.Length];
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

                for (int i = 0; i < indices.Length; i++)
                {
                    managedIndices[i] = indices[i];
                }

                mesh.vertices = managedVertices;
                mesh.normals = managedNormals;
                mesh.triangles = managedIndices;
                mesh.bounds = bounds;
                return mesh;
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

        private void EnsureCombinedRenderer()
        {
            if (combinedMeshFilter == null && !TryGetComponent(out combinedMeshFilter))
            {
                combinedMeshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (combinedMeshRenderer == null && !TryGetComponent(out combinedMeshRenderer))
            {
                combinedMeshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (combinedMesh == null)
            {
                combinedMesh = new Mesh
                {
                    name = "VoxelCombinedMesh",
                    indexFormat = IndexFormat.UInt32
                };
                combinedMesh.MarkDynamic();
            }

            combinedMeshFilter.sharedMesh = combinedMesh;
            combinedMeshRenderer.sharedMaterial = material;
        }

        private void UpdateCombinedMeshForView()
        {
            if (!combinedMeshDirty)
            {
                return;
            }

            UpdateCombinedMesh(false);
        }

        private void UpdateCombinedMesh(bool force)
        {
            if (!force && !combinedMeshDirty)
            {
                return;
            }

            using (CombineMeshMarker.Auto())
            {
                EnsureCombinedRenderer();

                combineInstances.Clear();
                int vertexCount = 0;
                foreach (VoxelChunkState state in activeChunks.Values)
                {
                    if (!state.generated || state.mesh == null || state.mesh.vertexCount == 0)
                    {
                        continue;
                    }

                    if (!ShouldRenderChunk(state))
                    {
                        continue;
                    }

                    vertexCount += state.mesh.vertexCount;
                    combineInstances.Add(new CombineInstance
                    {
                        mesh = state.mesh,
                        transform = transform.worldToLocalMatrix * Matrix4x4.Translate(ToVector3(state.chunkOrigin))
                    });
                }

                combinedMesh.Clear();
                combinedMesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                if (combineInstances.Count > 0)
                {
                    EnsureCombineInstanceBuffer(combineInstances.Count);
                    for (int i = 0; i < combineInstances.Count; i++)
                    {
                        combineInstanceBuffer[i] = combineInstances[i];
                    }

                    combinedMesh.CombineMeshes(combineInstanceBuffer, true, true, false);
                    combinedMesh.RecalculateBounds();
                }

                combinedMeshFilter.sharedMesh = combinedMesh;
                combinedMeshRenderer.sharedMaterial = material;
                combinedMeshDirty = false;
            }
        }

        private bool ShouldRenderChunk(VoxelChunkState state)
        {
            return IsChunkRenderVisible(state.chunkCoord);
        }

        private void MarkCombinedMeshDirty()
        {
            combinedMeshDirty = true;
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
            if (combinedMesh != null)
            {
                combinedMesh.Clear();
            }

            combinedMeshDirty = true;
        }

        private void DestroyCombinedMesh()
        {
            if (combinedMeshFilter != null)
            {
                combinedMeshFilter.sharedMesh = null;
            }

            if (combinedMesh != null)
            {
                DestroyUnityObject(combinedMesh);
                combinedMesh = null;
            }

            combinedMeshDirty = true;
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

        private void RemoveUndesiredChunks()
        {
            List<int3> chunksToRemove = new List<int3>();
            foreach (KeyValuePair<int3, VoxelChunkState> pair in activeChunks)
            {
                if (!desiredChunks.Contains(pair.Key))
                {
                    chunksToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < chunksToRemove.Count; i++)
            {
                int3 chunkCoord = chunksToRemove[i];
                DestroyChunk(activeChunks[chunkCoord]);
                activeChunks.Remove(chunkCoord);
                MarkCombinedMeshDirty();
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

            if (pendingBuild.indices.IsCreated)
            {
                pendingBuild.indices.Dispose();
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
            public int3 chunkOrigin;
            public Bounds chunkBounds;
            public bool generated;
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
            public NativeList<int> indices;
            public JobHandle jobHandle;
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
