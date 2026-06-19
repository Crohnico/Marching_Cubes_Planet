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

        private readonly Dictionary<int3, VoxelChunkState> activeChunks = new Dictionary<int3, VoxelChunkState>();
        private readonly Dictionary<int3, int> declaredChunkRefCounts = new Dictionary<int3, int>();
        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> desiredChunks = new HashSet<int3>();
        private MarchingCubesCaseTable caseTable;

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();

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
                ? state.gameObject
                : null;
        }

        private void HandleChunkChanged(int3 currentChunk)
        {
            RefreshDeclaredChunks();
            RebuildDesiredChunkSet(currentChunk);
        }

        private void RebuildDesiredChunkSet(int3 centerChunk)
        {
            desiredChunks.Clear();

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
                desiredChunks.Add(chunkCoord);
                EnsureChunkState(chunkCoord, cellSize, GetBoundaryRefinement(chunkCoord, centerChunk, cellSize, activeRadius));
            }

            RemoveUndesiredChunks();
        }

        private void EnsureChunkState(int3 chunkCoord, int desiredCellSize, BoundaryRefinement desiredBoundaryRefinement)
        {
            if (IsChunkReady(chunkCoord, desiredCellSize, desiredBoundaryRefinement))
            {
                return;
            }

            if (activeChunks.TryGetValue(chunkCoord, out VoxelChunkState oldState))
            {
                DestroyChunk(oldState);
                activeChunks.Remove(chunkCoord);
            }

            activeChunks.Add(chunkCoord, GenerateChunk(chunkCoord, desiredCellSize, desiredBoundaryRefinement));
        }

        private bool IsChunkReady(int3 chunkCoord, int desiredCellSize, BoundaryRefinement desiredBoundaryRefinement)
        {
            return activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state)
                && state.generated
                && !state.dirty
                && state.cellSize == desiredCellSize
                && state.boundaryRefinement.Equals(desiredBoundaryRefinement);
        }

        private VoxelChunkState GenerateChunk(int3 chunkCoord, int cellSize, BoundaryRefinement boundaryRefinement)
        {
            int3 chunkSize = config.ChunkSize;
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            List<VoxelCellBuildRequest> cellRequests = BuildCellRequests(chunkOrigin, chunkSize, cellSize, boundaryRefinement);
            int cellCount = cellRequests.Count;

            NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.TempJob);
            NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.TempJob);
            NativeArray<byte> cornersByUnitCell = new NativeArray<byte>(GetChunkUnitCellCount(chunkSize), Allocator.TempJob);
            NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.TempJob);
            NativeList<int> indices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.TempJob);
            ScalarFieldSettings scalarField = GetScalarFieldSettings();

            try
            {
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

                meshJob.Schedule(lookupHandle).Complete();

                GameObject chunkObject = new GameObject(BuildChunkName(chunkCoord, cellSize));
                chunkObject.transform.SetParent(transform, false);
                chunkObject.transform.position = new Vector3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z);

                Mesh mesh = BuildMesh(chunkObject.name, vertices, indices);
                MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;

                return new VoxelChunkState
                {
                    chunkCoord = chunkCoord,
                    cellSize = cellSize,
                    boundaryRefinement = boundaryRefinement,
                    gameObject = chunkObject,
                    mesh = mesh,
                    generated = true,
                    dirty = false
                };
            }
            finally
            {
                if (requests.IsCreated)
                {
                    requests.Dispose();
                }

                if (cells.IsCreated)
                {
                    cells.Dispose();
                }

                if (cornersByUnitCell.IsCreated)
                {
                    cornersByUnitCell.Dispose();
                }

                if (vertices.IsCreated)
                {
                    vertices.Dispose();
                }

                if (indices.IsCreated)
                {
                    indices.Dispose();
                }
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
            }
        }

        private void ClearChunks()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                DestroyChunk(state);
            }

            activeChunks.Clear();
            desiredChunks.Clear();
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

            if (state.gameObject != null)
            {
                DestroyUnityObject(state.gameObject);
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
            public GameObject gameObject;
            public Mesh mesh;
            public bool generated;
            public bool dirty;
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
