using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using Unity.Profiling;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public enum PlanetChunkFace : byte
    {
        NegativeX = 0,
        PositiveX = 1,
        NegativeY = 2,
        PositiveY = 3,
        NegativeZ = 4,
        PositiveZ = 5
    }

    internal readonly struct PlanetChunkComponentLink
    {
        public PlanetChunkComponentLink(
            PlanetGridCoordinates coordinates,
            int component,
            bool targetIsCompletelyAir)
        {
            Coordinates = coordinates;
            Component = component;
            TargetIsCompletelyAir = targetIsCompletelyAir;
        }

        public PlanetGridCoordinates Coordinates { get; }
        public int Component { get; }
        public bool TargetIsCompletelyAir { get; }
    }

    public sealed class PlanetChunkTopologyDebugData
    {
        public const int ChunkSize = PlanetMarchingCubesChunkRange.CanonicalChunkSize;
        public const int CellCount = ChunkSize * ChunkSize * ChunkSize;

        private readonly int[] cellComponents;
        private readonly int[] componentRepresentativeCells;
        private readonly List<PlanetChunkComponentLink>[] componentLinks;

        public PlanetChunkTopologyDebugData(
            PlanetGridCoordinates coordinates,
            int[] components,
            int[] representatives,
            int airCellCount)
        {
            Coordinates = coordinates;
            cellComponents = components ?? throw new ArgumentNullException(nameof(components));
            componentRepresentativeCells = representatives ?? throw new ArgumentNullException(nameof(representatives));
            componentLinks = new List<PlanetChunkComponentLink>[componentRepresentativeCells.Length];
            AirCellCount = airCellCount;
        }

        public PlanetGridCoordinates Coordinates { get; }
        public int ComponentCount => componentRepresentativeCells.Length;
        public int AirCellCount { get; }
        public bool IsCompletelyAir => AirCellCount == CellCount;
        public bool ContainsSurface => AirCellCount > 0 && AirCellCount < CellCount;

        public int GetLinkCount(int component)
        {
            List<PlanetChunkComponentLink> links = GetLinks(component);
            return links != null ? links.Count : 0;
        }

        public int GetComponentAtCell(int x, int y, int z)
        {
            if ((uint)x >= ChunkSize || (uint)y >= ChunkSize || (uint)z >= ChunkSize)
            {
                return -1;
            }

            return cellComponents[Flatten(x, y, z)];
        }

        public int GetBoundaryComponent(PlanetChunkFace face, int u, int v)
        {
            if ((uint)u >= ChunkSize || (uint)v >= ChunkSize)
            {
                return -1;
            }

            switch (face)
            {
                case PlanetChunkFace.NegativeX:
                    return GetComponentAtCell(0, u, v);
                case PlanetChunkFace.PositiveX:
                    return GetComponentAtCell(ChunkSize - 1, u, v);
                case PlanetChunkFace.NegativeY:
                    return GetComponentAtCell(u, 0, v);
                case PlanetChunkFace.PositiveY:
                    return GetComponentAtCell(u, ChunkSize - 1, v);
                case PlanetChunkFace.NegativeZ:
                    return GetComponentAtCell(u, v, 0);
                default:
                    return GetComponentAtCell(u, v, ChunkSize - 1);
            }
        }

        public int FindNearestComponent(Vector3 localGridPosition)
        {
            int startX = Mathf.Clamp(Mathf.FloorToInt(localGridPosition.x), 0, ChunkSize - 1);
            int startY = Mathf.Clamp(Mathf.FloorToInt(localGridPosition.y), 0, ChunkSize - 1);
            int startZ = Mathf.Clamp(Mathf.FloorToInt(localGridPosition.z), 0, ChunkSize - 1);
            int direct = GetComponentAtCell(startX, startY, startZ);
            if (direct >= 0)
            {
                return direct;
            }

            float bestDistanceSquared = float.PositiveInfinity;
            int bestComponent = -1;
            for (int component = 0; component < componentRepresentativeCells.Length; component++)
            {
                Unflatten(componentRepresentativeCells[component], out int x, out int y, out int z);
                Vector3 delta = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - localGridPosition;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestComponent = component;
                }
            }

            return bestComponent;
        }

        public Vector3 GetComponentRepresentativeGrid(int component)
        {
            if ((uint)component >= componentRepresentativeCells.Length)
            {
                return Vector3.zero;
            }

            Unflatten(componentRepresentativeCells[component], out int x, out int y, out int z);
            return new Vector3(
                Coordinates.x * ChunkSize + x + 0.5f,
                Coordinates.y * ChunkSize + y + 0.5f,
                Coordinates.z * ChunkSize + z + 0.5f);
        }

        internal List<PlanetChunkComponentLink> GetLinks(int component)
        {
            return (uint)component < componentLinks.Length ? componentLinks[component] : null;
        }

        internal void AddLink(int component, PlanetChunkComponentLink link)
        {
            if ((uint)component >= componentLinks.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(component));
            }

            List<PlanetChunkComponentLink> links = componentLinks[component];
            if (links == null)
            {
                links = new List<PlanetChunkComponentLink>(4);
                componentLinks[component] = links;
            }

            links.Add(link);
        }

        internal static int Flatten(int x, int y, int z)
        {
            return x + ChunkSize * (y + ChunkSize * z);
        }

        internal static void Unflatten(int index, out int x, out int y, out int z)
        {
            x = index % ChunkSize;
            y = (index / ChunkSize) % ChunkSize;
            z = index / (ChunkSize * ChunkSize);
        }
    }

    public static class PlanetChunkTopologyDebugBuilder
    {
        private static readonly int[] NeighborX = { -1, 1, 0, 0, 0, 0 };
        private static readonly int[] NeighborY = { 0, 0, -1, 1, 0, 0 };
        private static readonly int[] NeighborZ = { 0, 0, 0, 0, -1, 1 };

        public static PlanetChunkTopologyDebugData Build(
            PlanetGridCoordinates coordinates,
            Vector4[] densitySamples,
            float airDensityThreshold)
        {
            return Build(
                coordinates,
                densitySamples,
                airDensityThreshold,
                new int[PlanetChunkTopologyDebugData.CellCount],
                new List<int>(8));
        }

        internal static PlanetChunkTopologyDebugData Build(
            PlanetGridCoordinates coordinates,
            Vector4[] densitySamples,
            float airDensityThreshold,
            int[] floodQueue,
            List<int> representatives)
        {
            if (densitySamples == null || densitySamples.Length < PlanetChunkTopologyDebugData.CellCount)
            {
                throw new ArgumentException("A complete canonical chunk density grid is required.", nameof(densitySamples));
            }

            if (floodQueue == null || floodQueue.Length < PlanetChunkTopologyDebugData.CellCount)
            {
                throw new ArgumentException("The flood queue is too small.", nameof(floodQueue));
            }

            if (representatives == null)
            {
                throw new ArgumentNullException(nameof(representatives));
            }

            int cellCount = PlanetChunkTopologyDebugData.CellCount;
            int chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            int[] components = new int[cellCount];
            Array.Fill(components, -1);
            representatives.Clear();
            int airCellCount = 0;

            for (int index = 0; index < cellCount; index++)
            {
                if (densitySamples[index].x <= airDensityThreshold)
                {
                    airCellCount++;
                }
                else
                {
                    components[index] = -2;
                }
            }

            for (int seed = 0; seed < cellCount; seed++)
            {
                if (components[seed] != -1)
                {
                    continue;
                }

                int component = representatives.Count;
                representatives.Add(seed);
                int representative = seed;
                float representativeDistanceSquared = DistanceFromChunkCenterSquared(seed);
                int read = 0;
                int write = 0;
                floodQueue[write++] = seed;
                components[seed] = component;
                while (read < write)
                {
                    int current = floodQueue[read++];
                    float centerDistanceSquared = DistanceFromChunkCenterSquared(current);
                    if (centerDistanceSquared < representativeDistanceSquared)
                    {
                        representative = current;
                        representativeDistanceSquared = centerDistanceSquared;
                    }

                    PlanetChunkTopologyDebugData.Unflatten(current, out int x, out int y, out int z);
                    for (int direction = 0; direction < 6; direction++)
                    {
                        int neighborX = x + NeighborX[direction];
                        int neighborY = y + NeighborY[direction];
                        int neighborZ = z + NeighborZ[direction];
                        if ((uint)neighborX >= chunkSize ||
                            (uint)neighborY >= chunkSize ||
                            (uint)neighborZ >= chunkSize)
                        {
                            continue;
                        }

                        int neighbor = PlanetChunkTopologyDebugData.Flatten(neighborX, neighborY, neighborZ);
                        if (components[neighbor] != -1)
                        {
                            continue;
                        }

                        components[neighbor] = component;
                        floodQueue[write++] = neighbor;
                    }
                }

                representatives[component] = representative;
            }

            return new PlanetChunkTopologyDebugData(
                coordinates,
                components,
                representatives.ToArray(),
                airCellCount);
        }

        private static float DistanceFromChunkCenterSquared(int index)
        {
            PlanetChunkTopologyDebugData.Unflatten(index, out int x, out int y, out int z);
            float center = (PlanetChunkTopologyDebugData.ChunkSize - 1) * 0.5f;
            float deltaX = x - center;
            float deltaY = y - center;
            float deltaZ = z - center;
            return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
        }
    }

    public static class PlanetChunkTopologyDebugLinker
    {
        public static void LinkOppositeFaces(
            PlanetChunkTopologyDebugData topology,
            PlanetChunkTopologyDebugData neighbor,
            PlanetChunkFace topologyFace)
        {
            LinkOppositeFaces(topology, neighbor, topologyFace, new HashSet<ulong>());
        }

        internal static void LinkOppositeFaces(
            PlanetChunkTopologyDebugData topology,
            PlanetChunkTopologyDebugData neighbor,
            PlanetChunkFace topologyFace,
            HashSet<ulong> componentPairScratch)
        {
            if (topology == null)
            {
                throw new ArgumentNullException(nameof(topology));
            }

            if (neighbor == null)
            {
                throw new ArgumentNullException(nameof(neighbor));
            }

            if (componentPairScratch == null)
            {
                throw new ArgumentNullException(nameof(componentPairScratch));
            }

            PlanetChunkFace oppositeFace = (PlanetChunkFace)(((int)topologyFace) ^ 1);
            componentPairScratch.Clear();
            for (int v = 0; v < PlanetChunkTopologyDebugData.ChunkSize; v++)
            {
                for (int u = 0; u < PlanetChunkTopologyDebugData.ChunkSize; u++)
                {
                    int component = topology.GetBoundaryComponent(topologyFace, u, v);
                    int neighborComponent = neighbor.GetBoundaryComponent(oppositeFace, u, v);
                    if (component < 0 || neighborComponent < 0)
                    {
                        continue;
                    }

                    ulong pair = ((ulong)(uint)component << 32) | (uint)neighborComponent;
                    if (!componentPairScratch.Add(pair))
                    {
                        continue;
                    }

                    topology.AddLink(
                        component,
                        new PlanetChunkComponentLink(
                            neighbor.Coordinates,
                            neighborComponent,
                            neighbor.IsCompletelyAir));
                    neighbor.AddLink(
                        neighborComponent,
                        new PlanetChunkComponentLink(
                            topology.Coordinates,
                            component,
                            topology.IsCompletelyAir));
                }
            }
        }
    }

    public sealed class PlanetChunkTopologyDebugCatalog : IDisposable
    {
        private const string ShapeShaderResource = "Compute/PlanetShapeDensity";
        private static readonly ProfilerMarker CompleteChunkMarker =
            new ProfilerMarker("Planet.Topology.CompleteChunk");
        private static readonly PlanetGridCoordinates[] NeighborOffsets =
        {
            new PlanetGridCoordinates(-1, 0, 0),
            new PlanetGridCoordinates(1, 0, 0),
            new PlanetGridCoordinates(0, -1, 0),
            new PlanetGridCoordinates(0, 1, 0),
            new PlanetGridCoordinates(0, 0, -1),
            new PlanetGridCoordinates(0, 0, 1)
        };

        private readonly Dictionary<PlanetGridCoordinates, PlanetChunkTopologyDebugData> chunks =
            new Dictionary<PlanetGridCoordinates, PlanetChunkTopologyDebugData>();
        private readonly Queue<PlanetGridCoordinates> urgentQueue = new Queue<PlanetGridCoordinates>();
        private readonly Queue<PlanetGridCoordinates> backgroundQueue = new Queue<PlanetGridCoordinates>();
        private readonly HashSet<PlanetGridCoordinates> queued = new HashSet<PlanetGridCoordinates>();
        private readonly PlanetGpuShapeEvaluator evaluator = new PlanetGpuShapeEvaluator();
        private readonly Vector4[] samplePositions = new Vector4[PlanetChunkTopologyDebugData.CellCount];
        private readonly Vector4[] sampleResults = new Vector4[PlanetChunkTopologyDebugData.CellCount];
        private readonly int[] floodQueue = new int[PlanetChunkTopologyDebugData.CellCount];
        private readonly List<int> representativeScratch = new List<int>(8);
        private readonly HashSet<ulong> componentPairScratch = new HashSet<ulong>();

        private string recipeId;
        private float densityThreshold;
        private float maximumCenterDistanceSquared;
        private PlanetGridCoordinates pendingCoordinates;
        private bool hasPendingEvaluation;
        private bool initialized;

        public int Count => chunks.Count;
        public int Version { get; private set; }

        public bool TryGet(PlanetGridCoordinates coordinates, out PlanetChunkTopologyDebugData data)
        {
            return chunks.TryGetValue(coordinates, out data);
        }

        public void EnsureRecipe(in PlanetRecipe recipe, float airDensityThreshold)
        {
            string nextRecipeId = PlanetChunkMeshCache.BuildPlanetId(in recipe);
            if (initialized &&
                string.Equals(recipeId, nextRecipeId, StringComparison.Ordinal) &&
                Mathf.Approximately(densityThreshold, airDensityThreshold))
            {
                return;
            }

            Reset();
            PlanetGpuShapeCell[] shapeCells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, shapeCells);
            ComputeShader shader = Resources.Load<ComputeShader>(ShapeShaderResource);
            if (shader == null)
            {
                throw new InvalidOperationException("Planet shape compute shader was not found for topology debug.");
            }

            evaluator.Initialize(shader, in recipe, shapeCells, PlanetGpuBufferMode.ComputeBuffer);
            PlanetMarchingCubesChunkRange.CalculateSurfaceShell(in recipe, 0, out _, out float outerRadius);
            float halfDiagonal = PlanetChunkTopologyDebugData.ChunkSize * 0.8660254f;
            float maximumCenterDistance = outerRadius + halfDiagonal;
            maximumCenterDistanceSquared = maximumCenterDistance * maximumCenterDistance;
            recipeId = nextRecipeId;
            densityThreshold = airDensityThreshold;
            initialized = true;
        }

        public void Prioritize(PlanetGridCoordinates focus)
        {
            Enqueue(urgentQueue, focus);
            for (int index = 0; index < NeighborOffsets.Length; index++)
            {
                Enqueue(urgentQueue, Add(focus, NeighborOffsets[index]));
            }
        }

        public bool Tick(in PlanetRecipe recipe, int chunksPerFrame, float airDensityThreshold)
        {
            EnsureRecipe(in recipe, airDensityThreshold);
            bool changed = false;
            if (hasPendingEvaluation)
            {
                if (!evaluator.TryCompleteDensitySamplesAsync(sampleResults))
                {
                    return false;
                }

                using (CompleteChunkMarker.Auto())
                {
                    PlanetChunkTopologyDebugData topology = PlanetChunkTopologyDebugBuilder.Build(
                        pendingCoordinates,
                        sampleResults,
                        airDensityThreshold,
                        floodQueue,
                        representativeScratch);
                    chunks.Add(pendingCoordinates, topology);
                    LinkWithCataloguedNeighbors(topology);
                }

                hasPendingEvaluation = false;
                Version++;
                changed = true;
                for (int direction = 0; direction < NeighborOffsets.Length; direction++)
                {
                    Enqueue(backgroundQueue, Add(pendingCoordinates, NeighborOffsets[direction]));
                }
            }

            if (chunksPerFrame > 0 && TryDequeue(out PlanetGridCoordinates coordinates))
            {
                if (!chunks.ContainsKey(coordinates) && IsInsidePlanetDomain(coordinates))
                {
                    FillSamplePositions(coordinates);
                    evaluator.BeginEvaluateDensitySamplesAsync(samplePositions);
                    pendingCoordinates = coordinates;
                    hasPendingEvaluation = true;
                }
            }

            return changed;
        }

        public void Reset()
        {
            evaluator.Release();
            chunks.Clear();
            urgentQueue.Clear();
            backgroundQueue.Clear();
            queued.Clear();
            recipeId = null;
            densityThreshold = 0f;
            maximumCenterDistanceSquared = 0f;
            pendingCoordinates = default;
            hasPendingEvaluation = false;
            initialized = false;
            Version++;
        }

        public void Dispose()
        {
            Reset();
        }

        private bool TryDequeue(out PlanetGridCoordinates coordinates)
        {
            Queue<PlanetGridCoordinates> source = urgentQueue.Count > 0 ? urgentQueue : backgroundQueue;
            while (source.Count > 0)
            {
                coordinates = source.Dequeue();
                queued.Remove(coordinates);
                if (!chunks.ContainsKey(coordinates))
                {
                    return true;
                }

                source = urgentQueue.Count > 0 ? urgentQueue : backgroundQueue;
            }

            coordinates = default;
            return false;
        }

        private void Enqueue(Queue<PlanetGridCoordinates> queue, PlanetGridCoordinates coordinates)
        {
            if (chunks.ContainsKey(coordinates) || queued.Contains(coordinates) || !IsInsidePlanetDomain(coordinates))
            {
                return;
            }

            queued.Add(coordinates);
            queue.Enqueue(coordinates);
        }

        private bool IsInsidePlanetDomain(PlanetGridCoordinates coordinates)
        {
            Vector3 center = PlanetChunkPathDebugSearch.CalculateChunkCenterGrid(coordinates);
            return center.sqrMagnitude <= maximumCenterDistanceSquared;
        }

        private void LinkWithCataloguedNeighbors(PlanetChunkTopologyDebugData topology)
        {
            for (int direction = 0; direction < NeighborOffsets.Length; direction++)
            {
                PlanetGridCoordinates neighborCoordinates = Add(
                    topology.Coordinates,
                    NeighborOffsets[direction]);
                if (!chunks.TryGetValue(neighborCoordinates, out PlanetChunkTopologyDebugData neighbor))
                {
                    continue;
                }

                PlanetChunkTopologyDebugLinker.LinkOppositeFaces(
                    topology,
                    neighbor,
                    (PlanetChunkFace)direction,
                    componentPairScratch);
            }
        }

        private void FillSamplePositions(PlanetGridCoordinates coordinates)
        {
            int chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            Vector3 origin = new Vector3(
                coordinates.x * chunkSize,
                coordinates.y * chunkSize,
                coordinates.z * chunkSize);
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int sample = PlanetChunkTopologyDebugData.Flatten(x, y, z);
                        Vector3 position = origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                        samplePositions[sample] = new Vector4(position.x, position.y, position.z, 0f);
                    }
                }
            }
        }

        private static PlanetGridCoordinates Add(PlanetGridCoordinates a, PlanetGridCoordinates b)
        {
            return new PlanetGridCoordinates(a.x + b.x, a.y + b.y, a.z + b.z);
        }
    }

    public readonly struct PlanetChunkPathDebugResult
    {
        public PlanetChunkPathDebugResult(
            PlanetGridCoordinates coordinates,
            PlanetGridCoordinates parent,
            float cost,
            bool hasParent,
            bool isStraightPriority)
        {
            Coordinates = coordinates;
            Parent = parent;
            Cost = cost;
            HasParent = hasParent;
            IsStraightPriority = isStraightPriority;
        }

        public PlanetGridCoordinates Coordinates { get; }
        public PlanetGridCoordinates Parent { get; }
        public float Cost { get; }
        public bool HasParent { get; }
        public bool IsStraightPriority { get; }
    }

    public sealed class PlanetChunkPathDebugSearch
    {
        private readonly Dictionary<NodeKey, float> distances = new Dictionary<NodeKey, float>();
        private readonly Dictionary<NodeKey, NodeKey> parents = new Dictionary<NodeKey, NodeKey>();
        private readonly Dictionary<PlanetGridCoordinates, Candidate> chunks =
            new Dictionary<PlanetGridCoordinates, Candidate>();
        private readonly List<Candidate> candidateScratch = new List<Candidate>();
        private readonly List<PlanetChunkPathDebugResult> results = new List<PlanetChunkPathDebugResult>();
        private readonly List<PlanetChunkPathDebugResult> publishedScratch = new List<PlanetChunkPathDebugResult>();
        private readonly HashSet<PlanetGridCoordinates> selectedCoordinates =
            new HashSet<PlanetGridCoordinates>();
        private readonly MinHeap frontier = new MinHeap();

        public IReadOnlyList<PlanetChunkPathDebugResult> Results => results;

        public void Rebuild(
            PlanetChunkTopologyDebugCatalog catalog,
            Vector3 playerGridPosition,
            int maxChunks,
            int maxExpandedComponents,
            int straightRadiusChunks,
            float airTraversalCost,
            float mixedTraversalCost,
            float stepHeuristicCost,
            float maxPathCost,
            bool preservePublishedSelection)
        {
            publishedScratch.Clear();
            if (preservePublishedSelection)
            {
                for (int index = 0; index < results.Count; index++)
                {
                    publishedScratch.Add(results[index]);
                }
            }

            results.Clear();
            if (catalog == null || maxChunks <= 0)
            {
                return;
            }

            int chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            PlanetGridCoordinates playerChunk = new PlanetGridCoordinates(
                Mathf.FloorToInt(playerGridPosition.x / chunkSize),
                Mathf.FloorToInt(playerGridPosition.y / chunkSize),
                Mathf.FloorToInt(playerGridPosition.z / chunkSize));
            if (!catalog.TryGet(playerChunk, out PlanetChunkTopologyDebugData playerData))
            {
                return;
            }

            Vector3 playerLocal = playerGridPosition - new Vector3(
                playerChunk.x * chunkSize,
                playerChunk.y * chunkSize,
                playerChunk.z * chunkSize);
            int playerComponent = playerData.FindNearestComponent(playerLocal);
            if (playerComponent < 0)
            {
                return;
            }

            distances.Clear();
            parents.Clear();
            chunks.Clear();
            candidateScratch.Clear();
            frontier.Clear();
            NodeKey start = new NodeKey(playerChunk, playerComponent);
            distances.Add(start, 0f);
            frontier.Push(start, 0f);
            int expanded = 0;
            int safeExpansionLimit = Mathf.Max(maxChunks, maxExpandedComponents);
            float safeMaxPathCost = Mathf.Max(0f, maxPathCost);
            while (frontier.TryPop(out NodeKey current, out float currentCost) && expanded < safeExpansionLimit)
            {
                if (!distances.TryGetValue(current, out float bestCost) || currentCost > bestCost + 0.00001f)
                {
                    continue;
                }

                if (currentCost > safeMaxPathCost + 0.00001f)
                {
                    break;
                }

                expanded++;
                RegisterCandidate(catalog, current, currentCost, playerGridPosition, straightRadiusChunks);
                Expand(
                    catalog,
                    current,
                    currentCost,
                    Mathf.Max(0f, airTraversalCost),
                    Mathf.Max(0f, mixedTraversalCost),
                    Mathf.Max(0f, stepHeuristicCost),
                    safeMaxPathCost);
            }

            foreach (KeyValuePair<PlanetGridCoordinates, Candidate> entry in chunks)
            {
                candidateScratch.Add(entry.Value);
            }

            candidateScratch.Sort(CompareCandidates);
            selectedCoordinates.Clear();
            for (int index = 0; index < publishedScratch.Count && results.Count < maxChunks; index++)
            {
                PlanetChunkPathDebugResult published = publishedScratch[index];
                results.Add(published);
                selectedCoordinates.Add(published.Coordinates);
            }

            for (int index = 0; index < candidateScratch.Count && results.Count < maxChunks; index++)
            {
                Candidate candidate = candidateScratch[index];
                if (!selectedCoordinates.Add(candidate.Coordinates))
                {
                    continue;
                }

                results.Add(new PlanetChunkPathDebugResult(
                    candidate.Coordinates,
                    candidate.Parent,
                    candidate.Cost,
                    candidate.HasParent,
                    candidate.IsStraightPriority));
            }
        }

        public static Vector3 CalculateChunkCenterGrid(PlanetGridCoordinates coordinates)
        {
            float chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            return new Vector3(
                (coordinates.x + 0.5f) * chunkSize,
                (coordinates.y + 0.5f) * chunkSize,
                (coordinates.z + 0.5f) * chunkSize);
        }

        private void RegisterCandidate(
            PlanetChunkTopologyDebugCatalog catalog,
            NodeKey node,
            float cost,
            Vector3 playerGridPosition,
            int straightRadiusChunks)
        {
            bool isStraight = false;
            if (!catalog.TryGet(node.Coordinates, out PlanetChunkTopologyDebugData data) || !data.ContainsSurface)
            {
                return;
            }

            int deltaX = node.Coordinates.x - Mathf.FloorToInt(playerGridPosition.x / PlanetChunkTopologyDebugData.ChunkSize);
            int deltaY = node.Coordinates.y - Mathf.FloorToInt(playerGridPosition.y / PlanetChunkTopologyDebugData.ChunkSize);
            int deltaZ = node.Coordinates.z - Mathf.FloorToInt(playerGridPosition.z / PlanetChunkTopologyDebugData.ChunkSize);
            if (deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= straightRadiusChunks * straightRadiusChunks &&
                data.ContainsSurface)
            {
                isStraight = CanReachStraight(
                    catalog,
                    playerGridPosition,
                    data.GetComponentRepresentativeGrid(node.Component));
            }

            bool hasParent = TryFindRenderableParent(catalog, node, out PlanetGridCoordinates parentCoordinates);

            if (!chunks.TryGetValue(node.Coordinates, out Candidate existing) ||
                (isStraight && !existing.IsStraightPriority) ||
                (isStraight == existing.IsStraightPriority && cost < existing.Cost))
            {
                chunks[node.Coordinates] = new Candidate(
                    node.Coordinates,
                    parentCoordinates,
                    cost,
                    hasParent && !parentCoordinates.Equals(node.Coordinates),
                    isStraight);
            }
        }

        private bool TryFindRenderableParent(
            PlanetChunkTopologyDebugCatalog catalog,
            NodeKey node,
            out PlanetGridCoordinates parentCoordinates)
        {
            NodeKey cursor = node;
            while (parents.TryGetValue(cursor, out NodeKey parent))
            {
                if (!parent.Coordinates.Equals(node.Coordinates) &&
                    catalog.TryGet(parent.Coordinates, out PlanetChunkTopologyDebugData parentData) &&
                    parentData.ContainsSurface)
                {
                    parentCoordinates = parent.Coordinates;
                    return true;
                }

                cursor = parent;
            }

            parentCoordinates = default;
            return false;
        }

        private void Expand(
            PlanetChunkTopologyDebugCatalog catalog,
            NodeKey current,
            float currentCost,
            float airTraversalCost,
            float mixedTraversalCost,
            float stepHeuristicCost,
            float maxPathCost)
        {
            if (!catalog.TryGet(current.Coordinates, out PlanetChunkTopologyDebugData currentData))
            {
                return;
            }

            List<PlanetChunkComponentLink> links = currentData.GetLinks(current.Component);
            if (links == null)
            {
                return;
            }

            for (int index = 0; index < links.Count; index++)
            {
                PlanetChunkComponentLink link = links[index];
                float traversalCost = stepHeuristicCost +
                                      (link.TargetIsCompletelyAir ? airTraversalCost : mixedTraversalCost);
                float tentativeCost = currentCost + traversalCost;
                if (tentativeCost > maxPathCost + 0.00001f)
                {
                    continue;
                }

                NodeKey neighbor = new NodeKey(link.Coordinates, link.Component);
                if (distances.TryGetValue(neighbor, out float previousCost) &&
                    tentativeCost >= previousCost - 0.00001f)
                {
                    continue;
                }

                distances[neighbor] = tentativeCost;
                parents[neighbor] = current;
                frontier.Push(neighbor, tentativeCost);
            }
        }

        private static bool CanReachStraight(
            PlanetChunkTopologyDebugCatalog catalog,
            Vector3 start,
            Vector3 destination)
        {
            Vector3 delta = destination - start;
            float distance = delta.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance * 2f));
            for (int step = 0; step <= steps; step++)
            {
                Vector3 position = Vector3.Lerp(start, destination, step / (float)steps);
                int chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
                PlanetGridCoordinates coordinates = new PlanetGridCoordinates(
                    Mathf.FloorToInt(position.x / chunkSize),
                    Mathf.FloorToInt(position.y / chunkSize),
                    Mathf.FloorToInt(position.z / chunkSize));
                if (!catalog.TryGet(coordinates, out PlanetChunkTopologyDebugData data))
                {
                    return false;
                }

                int cellX = Mathf.Clamp(Mathf.FloorToInt(position.x - coordinates.x * chunkSize), 0, chunkSize - 1);
                int cellY = Mathf.Clamp(Mathf.FloorToInt(position.y - coordinates.y * chunkSize), 0, chunkSize - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(position.z - coordinates.z * chunkSize), 0, chunkSize - 1);
                if (data.GetComponentAtCell(cellX, cellY, cellZ) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            if (a.IsStraightPriority != b.IsStraightPriority)
            {
                return a.IsStraightPriority ? -1 : 1;
            }

            int costComparison = a.Cost.CompareTo(b.Cost);
            if (costComparison != 0)
            {
                return costComparison;
            }

            int xComparison = a.Coordinates.x.CompareTo(b.Coordinates.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = a.Coordinates.y.CompareTo(b.Coordinates.y);
            return yComparison != 0 ? yComparison : a.Coordinates.z.CompareTo(b.Coordinates.z);
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            public NodeKey(PlanetGridCoordinates coordinates, int component)
            {
                Coordinates = coordinates;
                Component = component;
            }

            public PlanetGridCoordinates Coordinates { get; }
            public int Component { get; }

            public bool Equals(NodeKey other)
            {
                return Coordinates.Equals(other.Coordinates) && Component == other.Component;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Coordinates.GetHashCode() * 397) ^ Component;
                }
            }
        }

        private readonly struct Candidate
        {
            public Candidate(
                PlanetGridCoordinates coordinates,
                PlanetGridCoordinates parent,
                float cost,
                bool hasParent,
                bool isStraightPriority)
            {
                Coordinates = coordinates;
                Parent = parent;
                Cost = cost;
                HasParent = hasParent;
                IsStraightPriority = isStraightPriority;
            }

            public PlanetGridCoordinates Coordinates { get; }
            public PlanetGridCoordinates Parent { get; }
            public float Cost { get; }
            public bool HasParent { get; }
            public bool IsStraightPriority { get; }
        }

        private sealed class MinHeap
        {
            private readonly List<Entry> entries = new List<Entry>();

            public void Clear()
            {
                entries.Clear();
            }

            public void Push(NodeKey node, float priority)
            {
                int index = entries.Count;
                entries.Add(new Entry(node, priority));
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (entries[parent].Priority <= priority)
                    {
                        break;
                    }

                    entries[index] = entries[parent];
                    index = parent;
                }

                entries[index] = new Entry(node, priority);
            }

            public bool TryPop(out NodeKey node, out float priority)
            {
                if (entries.Count == 0)
                {
                    node = default;
                    priority = 0f;
                    return false;
                }

                Entry root = entries[0];
                int lastIndex = entries.Count - 1;
                Entry tail = entries[lastIndex];
                entries.RemoveAt(lastIndex);
                if (lastIndex > 0)
                {
                    int index = 0;
                    while (true)
                    {
                        int left = index * 2 + 1;
                        if (left >= lastIndex)
                        {
                            break;
                        }

                        int right = left + 1;
                        int child = right < lastIndex && entries[right].Priority < entries[left].Priority
                            ? right
                            : left;
                        if (entries[child].Priority >= tail.Priority)
                        {
                            break;
                        }

                        entries[index] = entries[child];
                        index = child;
                    }

                    entries[index] = tail;
                }

                node = root.Node;
                priority = root.Priority;
                return true;
            }

            private readonly struct Entry
            {
                public Entry(NodeKey node, float priority)
                {
                    Node = node;
                    Priority = priority;
                }

                public NodeKey Node { get; }
                public float Priority { get; }
            }
        }
    }
}
