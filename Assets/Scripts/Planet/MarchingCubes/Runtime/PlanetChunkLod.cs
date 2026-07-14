using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public enum PlanetChunkLod
    {
        LOD0 = 0,
        LOD1 = 1,
        LOD2 = 2
    }

    public static class PlanetChunkLodUtility
    {
        public const PlanetChunkLod BaseRecipeLod = PlanetChunkLod.LOD1;
        public const PlanetChunkLod InitialFallbackLod = PlanetChunkLod.LOD2;

        public static PlanetRecipe BuildRecipeForLod(in PlanetRecipe lod1Recipe, PlanetChunkLod lod)
        {
            PlanetRecipe result = lod1Recipe;
            int lod1GridRadius = Mathf.Max(1, lod1Recipe.GridRadius);
            float lod1WorldRadius = lod1Recipe.WorldRadius;
            int targetGridRadius;

            switch (lod)
            {
                case PlanetChunkLod.LOD0:
                    targetGridRadius = lod1GridRadius * 2;
                    break;
                case PlanetChunkLod.LOD1:
                    targetGridRadius = lod1GridRadius;
                    break;
                case PlanetChunkLod.LOD2:
                    targetGridRadius = Mathf.Max(1, Mathf.RoundToInt(lod1GridRadius * 0.5f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }

            result.GridRadius = targetGridRadius;
            result.WorldScale = lod1WorldRadius / targetGridRadius;
            return result;
        }

        public static float CalculateBaseChunkWorldSize(in PlanetRecipe lod1Recipe)
        {
            return Mathf.Max(0.0001f, PlanetMarchingCubesChunkRange.CanonicalChunkSize * lod1Recipe.WorldScale);
        }

        public static float CalculateBaseChunkWorldSize(
            in PlanetRecipe lod1Recipe,
            in PlanetChunkLodActivationConfig activationConfig)
        {
            return Mathf.Max(0.0001f, activationConfig.CanonicalChunkSize * lod1Recipe.WorldScale);
        }

        public static int GetChunkSizeForLod(PlanetChunkLod lod)
        {
            return GetChunkSizeForLod(lod, PlanetMarchingCubesChunkRange.CanonicalChunkSize);
        }

        public static int GetChunkSizeForLod(PlanetChunkLod lod, in PlanetChunkLodActivationConfig activationConfig)
        {
            return GetChunkSizeForLod(lod, activationConfig.CanonicalChunkSize);
        }

        private static int GetChunkSizeForLod(PlanetChunkLod lod, int canonicalChunkSize)
        {
            switch (lod)
            {
                case PlanetChunkLod.LOD0:
                    return canonicalChunkSize * 2;
                case PlanetChunkLod.LOD1:
                    return canonicalChunkSize;
                case PlanetChunkLod.LOD2:
                    return Mathf.Max(1, canonicalChunkSize / 2);
                default:
                    throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }
        }
    }

    [Serializable]
    public struct PlanetChunkLodActivationConfig
    {
        public const int DefaultCanonicalChunkSize = PlanetMarchingCubesChunkRange.CanonicalChunkSize;
        public const float DefaultLod0MaxDistanceWorld = 900f;
        public const float DefaultLod1MaxDistanceWorld = 1800f;
        public const int DefaultMaxRuntimeLodRequestsPerUpdate = 1;

        public int canonicalChunkSize;
        public bool enableLod0;
        public float lod0MaxDistanceWorld;
        public float lod1MaxDistanceWorld;
        public int maxRuntimeLodRequestsPerUpdate;

        public int CanonicalChunkSize => Mathf.Max(1, canonicalChunkSize);

        public static PlanetChunkLodActivationConfig Default()
        {
            return new PlanetChunkLodActivationConfig
            {
                canonicalChunkSize = DefaultCanonicalChunkSize,
                enableLod0 = false,
                lod0MaxDistanceWorld = DefaultLod0MaxDistanceWorld,
                lod1MaxDistanceWorld = DefaultLod1MaxDistanceWorld,
                maxRuntimeLodRequestsPerUpdate = DefaultMaxRuntimeLodRequestsPerUpdate
            };
        }

        public void EnsureValid()
        {
            if (canonicalChunkSize <= 0)
            {
                canonicalChunkSize = DefaultCanonicalChunkSize;
            }

            if (lod0MaxDistanceWorld <= 0f)
            {
                lod0MaxDistanceWorld = DefaultLod0MaxDistanceWorld;
            }

            if (lod1MaxDistanceWorld <= lod0MaxDistanceWorld)
            {
                lod1MaxDistanceWorld = Mathf.Max(lod0MaxDistanceWorld + 1f, DefaultLod1MaxDistanceWorld);
            }

            if (maxRuntimeLodRequestsPerUpdate <= 0)
            {
                maxRuntimeLodRequestsPerUpdate = DefaultMaxRuntimeLodRequestsPerUpdate;
            }
        }
    }

    public readonly struct PlanetChunkLodResolutionContext
    {
        public PlanetChunkLodResolutionContext(
            Vector3 playerPositionWorld,
            float baseChunkWorldSize,
            PlanetChunkLodActivationConfig activationConfig)
        {
            PlayerPositionWorld = playerPositionWorld;
            BaseChunkWorldSize = Mathf.Max(0.0001f, baseChunkWorldSize);
            activationConfig.EnsureValid();
            EnableLod0 = activationConfig.enableLod0;
            Lod0MaxDistanceWorld = activationConfig.lod0MaxDistanceWorld;
            Lod1MaxDistanceWorld = activationConfig.lod1MaxDistanceWorld;
        }

        public Vector3 PlayerPositionWorld { get; }
        public float BaseChunkWorldSize { get; }
        public bool EnableLod0 { get; }
        public float Lod0MaxDistanceWorld { get; }
        public float Lod1MaxDistanceWorld { get; }
    }

    public struct PlanetChunkLodSummary
    {
        public int chunkCount;
        public int lod0Count;
        public int lod1Count;
        public int lod2Count;

        public void Record(PlanetChunkLod desiredLod)
        {
            chunkCount++;
            switch (desiredLod)
            {
                case PlanetChunkLod.LOD0:
                    lod0Count++;
                    break;
                case PlanetChunkLod.LOD1:
                    lod1Count++;
                    break;
                default:
                    lod2Count++;
                    break;
            }
        }
    }

    public struct PlanetChunkLodRuntimeEntry
    {
        public const int NoRequestedLod = -1;

        public PlanetChunkLodRuntimeEntry(int chunkId, Vector3 centerWorld)
            : this(chunkId, default, centerWorld)
        {
        }

        public PlanetChunkLodRuntimeEntry(int chunkId, PlanetMarchingCubesChunkOrigin chunkOrigin, Vector3 centerWorld)
        {
            ChunkId = chunkId;
            ChunkOrigin = chunkOrigin;
            CenterWorld = centerWorld;
            IsInitialized = false;
            CurrentLod = -1;
            DesiredLod = PlanetChunkLodUtility.InitialFallbackLod;
            RequestedLod = NoRequestedLod;
        }

        public int ChunkId { get; }
        public PlanetMarchingCubesChunkOrigin ChunkOrigin { get; }
        public Vector3 CenterWorld { get; }
        public bool IsInitialized { get; private set; }
        public int CurrentLod { get; private set; }
        public PlanetChunkLod DesiredLod { get; private set; }
        public int RequestedLod { get; private set; }
        public bool HasRequestedLod => RequestedLod != NoRequestedLod;
        public bool NeedsLodChange => !IsInitialized || CurrentLod != (int)DesiredLod;
        public bool NeedsLodRequest => NeedsLodChange && !HasRequestedLod;

        public bool ForceDesiredLod(PlanetChunkLod desiredLod)
        {
            bool changed = DesiredLod != desiredLod;
            DesiredLod = desiredLod;
            return changed || NeedsLodChange;
        }

        public bool ApplyDesiredLod(PlanetChunkLod desiredLod)
        {
            bool changed = DesiredLod != desiredLod;
            DesiredLod = desiredLod;
            return changed;
        }

        public void MarkRequested(PlanetChunkLod requestedLod)
        {
            RequestedLod = (int)requestedLod;
        }

        public void ClearRequested()
        {
            RequestedLod = NoRequestedLod;
        }

        public void MarkInitialized(PlanetChunkLod lod)
        {
            IsInitialized = true;
            CurrentLod = (int)lod;
            ClearRequested();
        }
    }

    public readonly struct PlanetChunkLodRequest
    {
        public PlanetChunkLodRequest(int entryIndex, PlanetChunkLod requestedLod, int sequence)
        {
            EntryIndex = entryIndex;
            RequestedLod = requestedLod;
            Sequence = sequence;
        }

        public int EntryIndex { get; }
        public PlanetChunkLod RequestedLod { get; }
        public int Sequence { get; }
    }

    public sealed class PlanetChunkLodRequestQueue
    {
        private readonly List<PlanetChunkLodRequest> pendingRequests = new List<PlanetChunkLodRequest>();
        private int nextSequence;

        public int PendingCount => pendingRequests.Count;

        public void Clear()
        {
            pendingRequests.Clear();
            nextSequence = 0;
        }

        public void Enqueue(int entryIndex, PlanetChunkLod requestedLod)
        {
            pendingRequests.Add(new PlanetChunkLodRequest(entryIndex, requestedLod, nextSequence));
            nextSequence++;
        }

        public bool Cancel(int entryIndex)
        {
            bool cancelled = false;
            for (int i = pendingRequests.Count - 1; i >= 0; i--)
            {
                if (pendingRequests[i].EntryIndex != entryIndex)
                {
                    continue;
                }

                pendingRequests.RemoveAt(i);
                cancelled = true;
            }

            return cancelled;
        }

        public bool TryDequeue(out PlanetChunkLodRequest request)
        {
            int bestIndex = -1;
            int bestLod = int.MaxValue;
            int bestSequence = int.MaxValue;
            for (int i = 0; i < pendingRequests.Count; i++)
            {
                PlanetChunkLodRequest candidate = pendingRequests[i];
                int candidateLod = (int)candidate.RequestedLod;
                if (candidateLod > bestLod)
                {
                    continue;
                }

                if (candidateLod == bestLod && candidate.Sequence >= bestSequence)
                {
                    continue;
                }

                bestIndex = i;
                bestLod = candidateLod;
                bestSequence = candidate.Sequence;
            }

            if (bestIndex < 0)
            {
                request = default;
                return false;
            }

            request = pendingRequests[bestIndex];
            pendingRequests.RemoveAt(bestIndex);
            return true;
        }
    }

    public static class PlanetChunkLodResolver
    {
        public static PlanetChunkLod ResolveDesiredLod(float distanceWorld, in PlanetChunkLodResolutionContext context)
        {
            return ResolveDesiredLod(distanceWorld, in context, -1);
        }

        public static PlanetChunkLod ResolveDesiredLod(
            float distanceWorld,
            in PlanetChunkLodResolutionContext context,
            int currentLod)
        {
            if (context.EnableLod0 && distanceWorld < context.Lod0MaxDistanceWorld)
            {
                return PlanetChunkLod.LOD0;
            }

            float hysteresisWorld = context.BaseChunkWorldSize;
            if (context.EnableLod0 &&
                currentLod == (int)PlanetChunkLod.LOD0 &&
                distanceWorld < context.Lod0MaxDistanceWorld + hysteresisWorld)
            {
                return PlanetChunkLod.LOD0;
            }

            if (distanceWorld < context.Lod1MaxDistanceWorld)
            {
                return PlanetChunkLod.LOD1;
            }

            if (currentLod == (int)PlanetChunkLod.LOD1 &&
                distanceWorld < context.Lod1MaxDistanceWorld + hysteresisWorld)
            {
                return PlanetChunkLod.LOD1;
            }

            return PlanetChunkLod.LOD2;
        }
    }

    public static class PlanetChunkLodRuntimePlanner
    {
        public static void CollectEntriesFromExtraction(
            PlanetMarchingCubesExtractionResult extraction,
            in PlanetRecipe renderRecipe,
            in PlanetPlacement placement,
            List<PlanetChunkLodRuntimeEntry> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (extraction == null || extraction.VertexCount <= 0)
            {
                return;
            }

            Dictionary<int, ChunkCenterAccumulator> chunks = CollectChunkCentersFromExtraction(extraction);
            foreach (KeyValuePair<int, ChunkCenterAccumulator> chunk in chunks)
            {
                if (chunk.Value.SampleCount <= 0)
                {
                    continue;
                }

                Vector3 centerWorld = PlanetCoordinateConverter.GridToWorld(
                    chunk.Value.AverageGridCenter,
                    in renderRecipe,
                    in placement);
                results.Add(new PlanetChunkLodRuntimeEntry(chunk.Key, centerWorld));
            }
        }

        public static void CollectEntriesFromCachedChunks(
            IList<PlanetCachedChunkMesh> cachedChunks,
            Transform meshParentTransform,
            List<PlanetChunkLodRuntimeEntry> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (cachedChunks == null || cachedChunks.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < cachedChunks.Count; i++)
            {
                PlanetCachedChunkMesh cachedChunk = cachedChunks[i];
                if (cachedChunk == null ||
                    !TryGetCachedChunkCenterWorld(cachedChunk, meshParentTransform, out Vector3 centerWorld))
                {
                    continue;
                }

                results.Add(new PlanetChunkLodRuntimeEntry(cachedChunk.ChunkId, centerWorld));
            }
        }

        public static PlanetChunkLodSummary EvaluateEntries(
            List<PlanetChunkLodRuntimeEntry> entries,
            in PlanetRecipe lod1Recipe,
            Vector3 playerPositionWorld,
            PlanetChunkLodActivationConfig activationConfig,
            out int changedLodCount)
        {
            changedLodCount = 0;
            if (entries == null || entries.Count <= 0)
            {
                return default;
            }

            PlanetChunkLodResolutionContext context = BuildContext(
                in lod1Recipe,
                playerPositionWorld,
                activationConfig);
            PlanetChunkLodSummary summary = default;
            for (int i = 0; i < entries.Count; i++)
            {
                PlanetChunkLodRuntimeEntry entry = entries[i];
                float distanceWorld = (entry.CenterWorld - context.PlayerPositionWorld).magnitude;
                PlanetChunkLod desiredLod = PlanetChunkLodResolver.ResolveDesiredLod(
                    distanceWorld,
                    in context,
                    entry.CurrentLod);
                if (entry.ApplyDesiredLod(desiredLod))
                {
                    changedLodCount++;
                }

                entries[i] = entry;
                summary.Record(desiredLod);
            }

            return summary;
        }

        public static PlanetChunkLodSummary BuildSummaryFromExtraction(
            PlanetMarchingCubesExtractionResult extraction,
            in PlanetRecipe renderRecipe,
            in PlanetRecipe lod1Recipe,
            in PlanetPlacement placement,
            Vector3 playerPositionWorld)
        {
            if (extraction == null || extraction.VertexCount <= 0)
            {
                return default;
            }

            Dictionary<int, ChunkCenterAccumulator> chunks = CollectChunkCentersFromExtraction(extraction);

            PlanetChunkLodResolutionContext context = BuildContext(
                in lod1Recipe,
                playerPositionWorld,
                PlanetChunkLodActivationConfig.Default());
            PlanetChunkLodSummary summary = default;
            foreach (KeyValuePair<int, ChunkCenterAccumulator> chunk in chunks)
            {
                if (chunk.Value.SampleCount <= 0)
                {
                    continue;
                }

                Vector3 centerGrid = chunk.Value.AverageGridCenter;
                Vector3 centerWorld = PlanetCoordinateConverter.GridToWorld(centerGrid, in renderRecipe, in placement);
                float distanceWorld = (centerWorld - context.PlayerPositionWorld).magnitude;
                summary.Record(PlanetChunkLodResolver.ResolveDesiredLod(distanceWorld, in context));
            }

            return summary;
        }

        public static PlanetChunkLodSummary BuildSummaryFromCachedChunks(
            IList<PlanetCachedChunkMesh> cachedChunks,
            Transform meshParentTransform,
            in PlanetRecipe lod1Recipe,
            Vector3 playerPositionWorld)
        {
            if (cachedChunks == null || cachedChunks.Count <= 0)
            {
                return default;
            }

            PlanetChunkLodResolutionContext context = BuildContext(
                in lod1Recipe,
                playerPositionWorld,
                PlanetChunkLodActivationConfig.Default());
            PlanetChunkLodSummary summary = default;
            for (int i = 0; i < cachedChunks.Count; i++)
            {
                PlanetCachedChunkMesh cachedChunk = cachedChunks[i];
                if (cachedChunk == null || !TryGetCachedChunkCenterWorld(cachedChunk, meshParentTransform, out Vector3 centerWorld))
                {
                    continue;
                }

                float distanceWorld = (centerWorld - context.PlayerPositionWorld).magnitude;
                summary.Record(PlanetChunkLodResolver.ResolveDesiredLod(distanceWorld, in context));
            }

            return summary;
        }

        private static PlanetChunkLodResolutionContext BuildContext(
            in PlanetRecipe lod1Recipe,
            Vector3 playerPositionWorld,
            PlanetChunkLodActivationConfig activationConfig)
        {
            float baseChunkWorldSize = PlanetChunkLodUtility.CalculateBaseChunkWorldSize(in lod1Recipe, in activationConfig);
            return new PlanetChunkLodResolutionContext(
                playerPositionWorld,
                baseChunkWorldSize,
                activationConfig);
        }

        private static Dictionary<int, ChunkCenterAccumulator> CollectChunkCentersFromExtraction(
            PlanetMarchingCubesExtractionResult extraction)
        {
            Dictionary<int, ChunkCenterAccumulator> chunks = new Dictionary<int, ChunkCenterAccumulator>();
            PlanetMarchingCubesVertex[] vertices = extraction.Vertices;
            int triangleCount = extraction.TriangleCount;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < triangleCount; sourceTriangleIndex++)
            {
                int vertexIndex = sourceTriangleIndex * 3;
                if (vertexIndex + 2 >= extraction.VertexCount)
                {
                    break;
                }

                int chunkId = ReadSourceChunkIndex(vertices, vertexIndex);
                Vector3 centerGrid =
                    (ReadGridPosition(vertices[vertexIndex]) +
                     ReadGridPosition(vertices[vertexIndex + 1]) +
                     ReadGridPosition(vertices[vertexIndex + 2])) * 0.33333334f;

                chunks.TryGetValue(chunkId, out ChunkCenterAccumulator accumulator);
                accumulator.Add(centerGrid);
                chunks[chunkId] = accumulator;
            }

            return chunks;
        }

        private static bool TryGetCachedChunkCenterWorld(
            PlanetCachedChunkMesh cachedChunk,
            Transform meshParentTransform,
            out Vector3 centerWorld)
        {
            Mesh mesh = cachedChunk.SurfaceMesh != null ? cachedChunk.SurfaceMesh : cachedChunk.WaterMesh;
            if (mesh == null)
            {
                centerWorld = Vector3.zero;
                return false;
            }

            Vector3 centerLocal = mesh.bounds.center;
            centerWorld = meshParentTransform != null
                ? meshParentTransform.TransformPoint(centerLocal)
                : centerLocal;
            return true;
        }

        private static Vector3 ReadGridPosition(PlanetMarchingCubesVertex vertex)
        {
            Vector4 packedPosition = vertex.positionAndMaterial;
            return new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
        }

        private static int ReadSourceChunkIndex(PlanetMarchingCubesVertex[] vertices, int vertexIndex)
        {
            if (vertices == null || vertexIndex < 0 || vertexIndex >= vertices.Length)
            {
                return 0;
            }

            float packedChunkIndex = vertices[vertexIndex].normalAndDiagnostic.w;
            if (packedChunkIndex <= 0f)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.RoundToInt(packedChunkIndex));
        }

        private struct ChunkCenterAccumulator
        {
            private Vector3 gridCenterSum;

            public int SampleCount { get; private set; }
            public Vector3 AverageGridCenter => SampleCount > 0 ? gridCenterSum / SampleCount : Vector3.zero;

            public void Add(Vector3 gridCenter)
            {
                gridCenterSum += gridCenter;
                SampleCount++;
            }
        }
    }

    public enum PlanetChunkWorkPackageMode
    {
        ImmediateLod2Shell = 0,
        RefinementPackages = 1
    }

    public readonly struct PlanetChunkWorkPackageSummary
    {
        public PlanetChunkWorkPackageSummary(
            int lod,
            int chunkCount,
            int packageSize,
            int packageCount,
            PlanetChunkWorkPackageMode mode)
        {
            Lod = lod;
            ChunkCount = chunkCount;
            PackageSize = packageSize;
            PackageCount = packageCount;
            Mode = mode;
        }

        public int Lod { get; }
        public int ChunkCount { get; }
        public int PackageSize { get; }
        public int PackageCount { get; }
        public PlanetChunkWorkPackageMode Mode { get; }
        public bool IsImmediateShell => Mode == PlanetChunkWorkPackageMode.ImmediateLod2Shell;
    }

    public static class PlanetChunkWorkPackagePlanner
    {
        public const int DefaultRefinementPackageSize = 6;

        public static PlanetChunkWorkPackageSummary BuildSummary(
            int lod,
            int chunkCount,
            int refinementPackageSize = DefaultRefinementPackageSize)
        {
            int safeLod = Mathf.Max(0, lod);
            int safeChunkCount = Mathf.Max(0, chunkCount);
            if (safeLod == (int)PlanetChunkLodUtility.InitialFallbackLod)
            {
                return new PlanetChunkWorkPackageSummary(
                    safeLod,
                    safeChunkCount,
                    safeChunkCount,
                    safeChunkCount > 0 ? 1 : 0,
                    PlanetChunkWorkPackageMode.ImmediateLod2Shell);
            }

            int safePackageSize = Mathf.Max(1, refinementPackageSize);
            int packageCount = safeChunkCount > 0
                ? Mathf.CeilToInt(safeChunkCount / (float)safePackageSize)
                : 0;
            return new PlanetChunkWorkPackageSummary(
                safeLod,
                safeChunkCount,
                safePackageSize,
                packageCount,
                PlanetChunkWorkPackageMode.RefinementPackages);
        }
    }
}
