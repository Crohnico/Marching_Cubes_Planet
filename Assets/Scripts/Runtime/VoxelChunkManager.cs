using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

        [SerializeField] private VoxelEngineConfig config;
        [SerializeField] private PlayerChunkTracker playerChunkTracker;
        [SerializeField] private VoxelSphereGenerator sphereGenerator;
        [SerializeField] private Transform fallbackAnchor;
        [SerializeField] private Material material;
        [SerializeField] private bool generateOnEnable = true;
        [SerializeField] private bool completeInitialBuildSynchronously = true;
        [SerializeField] private bool enableViewConeCulling = true;
        [SerializeField] private Camera renderCullingCamera;
        [SerializeField] private Transform renderCullingAnchor;
        [SerializeField, Range(1f, 179f)] private float renderConeVerticalFov = 70f;
        [SerializeField, Min(0.1f)] private float renderConeAspect = 1.7777778f;
        [SerializeField, Min(1f)] private float renderConeFarDistance = 1024f;
        [SerializeField, Min(0f)] private float renderConePositionUpdateThreshold = 8f;
        [SerializeField, Range(0f, 30f)] private float renderConeAngleUpdateThreshold = 2f;
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
        private int3 priorityCenterChunk;
        private int queuedBuildSequence;
        private bool chunkBuildQueueNeedsSort;
        private bool completedInitialSynchronousBuild;
        private bool combinedMeshDirty = true;
        private MeshFilter combinedMeshFilter;
        private MeshRenderer combinedMeshRenderer;
        private Mesh combinedMesh;
        private readonly Plane[] renderFrustumPlanes = new Plane[6];
        private Vector3 lastRenderCullingPosition;
        private Quaternion lastRenderCullingRotation = Quaternion.identity;
        private bool hasLastRenderCullingPose;
        private MarchingCubesCaseTable caseTable;

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();
            completedInitialSynchronousBuild = false;

            if (playerChunkTracker != null)
            {
                playerChunkTracker.OnChunkChanged += HandleChunkChanged;
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
            renderConeAspect = Mathf.Max(0.1f, renderConeAspect);
            renderConeFarDistance = Mathf.Max(1f, renderConeFarDistance);
            renderConePositionUpdateThreshold = Mathf.Max(0f, renderConePositionUpdateThreshold);
            maxChunkBuildsStartedPerFrame = Mathf.Max(1, maxChunkBuildsStartedPerFrame);
            maxChunkSwapsPerFrame = Mathf.Max(1, maxChunkSwapsPerFrame);
            maxConcurrentChunkBuilds = Mathf.Max(1, maxConcurrentChunkBuilds);
        }

        private void Update()
        {
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
            RefreshDeclaredChunks();
            int3 centerChunk = GetCurrentCenterChunk();
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

        private void RebuildDesiredChunkSet(int3 centerChunk)
        {
            priorityCenterChunk = centerChunk;
            desiredChunks.Clear();
            desiredChunkStates.Clear();

            int activeRadius = config.ActiveChunkRadius;
            foreach (int3 chunkCoord in declaredChunks)
            {
                int3 chunkOffset = chunkCoord - centerChunk;
                int distance = GetChunkDistance(chunkOffset);
                if (distance > activeRadius)
                {
                    continue;
                }

                int cellSize = config.GetCellSizeForChunkDistance(distance);
                BoundaryRefinement boundaryRefinement = GetBoundaryRefinement(chunkCoord, centerChunk, cellSize, activeRadius);
                DesiredChunkState desiredState = new DesiredChunkState(cellSize, boundaryRefinement);
                desiredChunks.Add(chunkCoord);
                desiredChunkStates[chunkCoord] = desiredState;
                EnqueueChunkState(chunkCoord, desiredState);
            }

            RemoveUndesiredChunks();
            ReprioritizeChunkBuildQueue();
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
                && state.boundaryRefinement.Equals(desiredState.boundaryRefinement);
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
            int3 chunkSize = config.ChunkSize;
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            List<VoxelCellBuildRequest> cellRequests = BuildCellRequests(chunkOrigin, chunkSize, desiredState.cellSize, desiredState.boundaryRefinement);
            int cellCount = cellRequests.Count;

            NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
            NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
            NativeArray<byte> cornersByUnitCell = new NativeArray<byte>(GetChunkUnitCellCount(chunkSize), Allocator.Persistent);
            NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
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

            BuildVoxelCellLookupJob lookupJob = new BuildVoxelCellLookupJob
            {
                cells = cells,
                chunkOrigin = chunkOrigin,
                chunkSize = chunkSize,
                cornersByUnitCell = cornersByUnitCell
            };

            JobHandle lookupHandle = lookupJob.Schedule(evaluateHandle);

            GenerateChunkMeshJob meshJob = new GenerateChunkMeshJob
            {
                cells = cells,
                cornersByUnitCell = cornersByUnitCell,
                cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                triangulation = caseTable.triangulation,
                chunkOrigin = chunkOrigin,
                chunkSize = chunkSize,
                declaredNeighborSides = GetDeclaredNeighborSides(chunkCoord),
                scalarField = scalarField,
                vertices = vertices,
                indices = indices
            };

            return new PendingChunkBuild
            {
                chunkCoord = chunkCoord,
                cellSize = desiredState.cellSize,
                boundaryRefinement = desiredState.boundaryRefinement,
                chunkOrigin = chunkOrigin,
                requests = requests,
                cells = cells,
                cornersByUnitCell = cornersByUnitCell,
                vertices = vertices,
                indices = indices,
                jobHandle = meshJob.Schedule(lookupHandle)
            };
        }

        private void CompleteChunkBuild(PendingChunkBuild pendingBuild)
        {
            pendingBuild.jobHandle.Complete();

            try
            {
                DesiredChunkState completedState = new DesiredChunkState(pendingBuild.cellSize, pendingBuild.boundaryRefinement);
                bool isStillDesired = desiredChunkStates.TryGetValue(pendingBuild.chunkCoord, out DesiredChunkState desiredState)
                    && desiredState.Equals(completedState);
                if (!isStillDesired)
                {
                    EnqueueChunkStateIfStillDesired(pendingBuild.chunkCoord);
                    return;
                }

                Mesh mesh = BuildMesh(BuildChunkName(pendingBuild.chunkCoord, pendingBuild.cellSize), pendingBuild.vertices, pendingBuild.indices);

                VoxelChunkState nextState = new VoxelChunkState
                {
                    chunkCoord = pendingBuild.chunkCoord,
                    cellSize = pendingBuild.cellSize,
                    boundaryRefinement = pendingBuild.boundaryRefinement,
                    owner = gameObject,
                    mesh = mesh,
                    chunkOrigin = pendingBuild.chunkOrigin,
                    chunkBounds = BuildChunkBounds(pendingBuild.chunkOrigin, config.ChunkSize),
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

        private void EnqueueChunkStateIfStillDesired(int3 chunkCoord)
        {
            if (desiredChunkStates.TryGetValue(chunkCoord, out DesiredChunkState desiredState))
            {
                EnqueueChunkState(chunkCoord, desiredState);
            }
        }

        private BoundaryRefinement GetBoundaryRefinement(int3 chunkCoord, int3 centerChunk, int cellSize, int activeRadius)
        {
            BoundaryRefinement refinement = BoundaryRefinement.Empty;
            if (cellSize <= 1)
            {
                return refinement;
            }

            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(-1, 0, 0), cellSize, activeRadius, BoundaryXMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(1, 0, 0), cellSize, activeRadius, BoundaryXMax);
            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(0, -1, 0), cellSize, activeRadius, BoundaryYMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(0, 1, 0), cellSize, activeRadius, BoundaryYMax);
            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(0, 0, -1), cellSize, activeRadius, BoundaryZMin);
            AddBoundaryRefinement(ref refinement, chunkCoord, centerChunk, new int3(0, 0, 1), cellSize, activeRadius, BoundaryZMax);
            return refinement;
        }

        private void AddBoundaryRefinement(
            ref BoundaryRefinement refinement,
            int3 chunkCoord,
            int3 centerChunk,
            int3 direction,
            int cellSize,
            int activeRadius,
            byte side)
        {
            int3 neighborCoord = chunkCoord + direction;
            if (!declaredChunks.Contains(neighborCoord))
            {
                return;
            }

            int3 neighborOffset = neighborCoord - centerChunk;
            if (GetChunkDistance(neighborOffset) > activeRadius)
            {
                return;
            }

            int neighborCellSize = config.GetCellSizeForChunkDistance(GetChunkDistance(neighborOffset));
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

        private static int GetChunkUnitCellCount(int3 chunkSize)
        {
            return chunkSize.x * chunkSize.y * chunkSize.z;
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
            int cellSize,
            BoundaryRefinement boundaryRefinement)
        {
            int normalizedCellSize = math.max(1, cellSize);
            List<VoxelCellBuildRequest> requests = new List<VoxelCellBuildRequest>();

            if (normalizedCellSize == 1)
            {
                AddCells(
                    requests,
                    chunkOrigin,
                    chunkOrigin,
                    chunkSize,
                    chunkSize,
                    1,
                    boundaryRefinement);
                return requests;
            }

            int3 coarseCounts = math.max(new int3(1, 1, 1), chunkSize / normalizedCellSize);
            for (int x = 0; x < coarseCounts.x; x++)
            {
                for (int y = 0; y < coarseCounts.y; y++)
                {
                    for (int z = 0; z < coarseCounts.z; z++)
                    {
                        int3 localOrigin = new int3(x, y, z) * normalizedCellSize;
                        int3 origin = chunkOrigin + localOrigin;
                        byte chunkBoundarySides = GetChunkBoundarySides(localOrigin, normalizedCellSize, chunkSize);
                        int refinedCellSize = boundaryRefinement.GetSmallestCellSizeForSides(chunkBoundarySides, normalizedCellSize);
                        if (refinedCellSize < normalizedCellSize)
                        {
                            AddCells(
                                requests,
                                origin,
                                chunkOrigin,
                                new int3(normalizedCellSize, normalizedCellSize, normalizedCellSize),
                                chunkSize,
                                refinedCellSize,
                                boundaryRefinement);
                            continue;
                        }

                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = origin,
                            size = normalizedCellSize,
                            boundarySides = chunkBoundarySides
                        });
                    }
                }
            }

            return requests;
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

        private static Mesh BuildMesh(string meshName, NativeList<float3> vertices, NativeList<int> indices)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            Vector3[] managedVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 vertex = vertices[i];
                managedVertices[i] = new Vector3(vertex.x, vertex.y, vertex.z);
            }

            int[] managedIndices = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                managedIndices[i] = indices[i];
            }

            mesh.vertices = managedVertices;
            mesh.triangles = managedIndices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
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
            if (!combinedMeshDirty && !HasRenderCullingPoseChanged())
            {
                return;
            }

            UpdateCombinedMesh(false);
        }

        private void UpdateCombinedMesh(bool force)
        {
            if (!force && !combinedMeshDirty && !HasRenderCullingPoseChanged())
            {
                return;
            }

            EnsureCombinedRenderer();
            CaptureRenderCullingPose();

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
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
                combinedMesh.RecalculateBounds();
            }

            combinedMeshFilter.sharedMesh = combinedMesh;
            combinedMeshRenderer.sharedMaterial = material;
            combinedMeshDirty = false;
        }

        private bool ShouldRenderChunk(VoxelChunkState state)
        {
            if (!enableViewConeCulling)
            {
                return true;
            }

            Camera cullingCamera = ResolveRenderCullingCamera();
            if (cullingCamera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(cullingCamera, renderFrustumPlanes);
                return GeometryUtility.TestPlanesAABB(renderFrustumPlanes, state.chunkBounds);
            }

            Transform cullingTransform = ResolveRenderCullingTransform();
            return cullingTransform == null || IsBoundsInsideRenderCone(state.chunkBounds, cullingTransform);
        }

        private Camera ResolveRenderCullingCamera()
        {
            if (renderCullingCamera != null)
            {
                return renderCullingCamera;
            }

            return Camera.main;
        }

        private Transform ResolveRenderCullingTransform()
        {
            if (renderCullingAnchor != null)
            {
                return renderCullingAnchor;
            }

            Camera cullingCamera = ResolveRenderCullingCamera();
            if (cullingCamera != null)
            {
                return cullingCamera.transform;
            }

            return fallbackAnchor != null ? fallbackAnchor : transform;
        }

        private bool IsBoundsInsideRenderCone(Bounds bounds, Transform cullingTransform)
        {
            Vector3 localCenter = Quaternion.Inverse(cullingTransform.rotation) * (bounds.center - cullingTransform.position);
            float radius = bounds.extents.magnitude;
            if (localCenter.z < -radius || localCenter.z > renderConeFarDistance + radius)
            {
                return false;
            }

            float depth = Mathf.Max(0f, localCenter.z);
            float verticalTan = Mathf.Tan(renderConeVerticalFov * 0.5f * Mathf.Deg2Rad);
            float horizontalTan = verticalTan * renderConeAspect;
            return Mathf.Abs(localCenter.x) <= depth * horizontalTan + radius
                && Mathf.Abs(localCenter.y) <= depth * verticalTan + radius;
        }

        private bool HasRenderCullingPoseChanged()
        {
            if (!enableViewConeCulling)
            {
                return false;
            }

            Transform cullingTransform = ResolveRenderCullingTransform();
            if (cullingTransform == null)
            {
                return false;
            }

            if (!hasLastRenderCullingPose)
            {
                return true;
            }

            float positionThreshold = renderConePositionUpdateThreshold;
            bool positionChanged = (cullingTransform.position - lastRenderCullingPosition).sqrMagnitude >= positionThreshold * positionThreshold;
            bool rotationChanged = Quaternion.Angle(lastRenderCullingRotation, cullingTransform.rotation) >= renderConeAngleUpdateThreshold;
            return positionChanged || rotationChanged;
        }

        private void CaptureRenderCullingPose()
        {
            Transform cullingTransform = ResolveRenderCullingTransform();
            if (cullingTransform == null)
            {
                hasLastRenderCullingPose = false;
                return;
            }

            lastRenderCullingPosition = cullingTransform.position;
            lastRenderCullingRotation = cullingTransform.rotation;
            hasLastRenderCullingPose = true;
        }

        private void MarkCombinedMeshDirty()
        {
            combinedMeshDirty = true;
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

            if (pendingBuild.cornersByUnitCell.IsCreated)
            {
                pendingBuild.cornersByUnitCell.Dispose();
            }

            if (pendingBuild.vertices.IsCreated)
            {
                pendingBuild.vertices.Dispose();
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

            public DesiredChunkState(int cellSize, BoundaryRefinement boundaryRefinement)
            {
                this.cellSize = cellSize;
                this.boundaryRefinement = boundaryRefinement;
            }

            public bool Equals(DesiredChunkState other)
            {
                return cellSize == other.cellSize
                    && boundaryRefinement.Equals(other.boundaryRefinement);
            }
        }

        private sealed class PendingChunkBuild
        {
            public int3 chunkCoord;
            public int cellSize;
            public BoundaryRefinement boundaryRefinement;
            public int3 chunkOrigin;
            public NativeArray<VoxelCellBuildRequest> requests;
            public NativeArray<VoxelCell> cells;
            public NativeArray<byte> cornersByUnitCell;
            public NativeList<float3> vertices;
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
