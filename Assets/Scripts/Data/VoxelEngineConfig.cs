using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [CreateAssetMenu(menuName = "Voxel Engine/Voxel Engine Config")]
    public sealed class VoxelEngineConfig : ScriptableObject
    {
        public const string ResourceName = "VoxelEngineConfig";
        public const int MaxCombinedMeshBucketCount = 64;
        public const int MinDeferredSegmentLodChunksBuiltPerFrame = 400;

        [SerializeField] private Vector3Int chunkSize = new Vector3Int(64, 64, 64);
        [SerializeField] private int[] cellSizes =
        {
            8,
            32,
            64
        };
        [SerializeField] private float[] lodDistances =
        {
            256f,
            768f,
            2048f
        };
        [SerializeField, Min(1)] private int defaultCellSize = 8;
        [Header("Runtime")]
        [SerializeField] private bool useChunkCullingForRendering = true;
        [SerializeField] private bool usePlanetActionRadius = true;
        [SerializeField, Range(1, MaxCombinedMeshBucketCount)] private int nearCombinedMeshBucketCount = MaxCombinedMeshBucketCount;
        [Header("Segment LOD")]
        [SerializeField] private bool useRadialLayerCulling = true;
        [SerializeField, Min(0)] private int neverLayerCullChunkDistance = 3;
        [SerializeField, Min(MinDeferredSegmentLodChunksBuiltPerFrame)] private int maxDeferredSegmentLodChunksBuiltPerFrame = MinDeferredSegmentLodChunksBuiltPerFrame;

        public int3 ChunkSize => new int3(chunkSize.x, chunkSize.y, chunkSize.z);
        public int DefaultCellSize => NormalizeCellSize(defaultCellSize);
        public int FinestCellSize => GetFinestCellSize();
        public int CoarsestCellSize => GetCoarsestCellSize();
        public float CoarsestLodStartDistance => GetLodStartDistanceForCellSize(CoarsestCellSize);
        public int LodCount => GetLodCount();
        public bool UseChunkCullingForRendering => useChunkCullingForRendering;
        public bool UsePlanetActionRadius => usePlanetActionRadius;
        public int NearCombinedMeshBucketCount => Mathf.Clamp(nearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
        public bool UseRadialLayerCulling => useRadialLayerCulling;
        public int NeverLayerCullChunkDistance => Mathf.Max(0, neverLayerCullChunkDistance);
        public int MaxDeferredSegmentLodChunksBuiltPerFrame => Mathf.Max(
            MinDeferredSegmentLodChunksBuiltPerFrame,
            maxDeferredSegmentLodChunksBuiltPerFrame);

        public ScalarFieldSettings ScalarFieldSettings => new ScalarFieldSettings
        {
            debugSphereCenter = new float3(chunkSize.x, chunkSize.y, chunkSize.z) * 0.5f,
            debugSphereRadius = math.min(chunkSize.x, math.min(chunkSize.y, chunkSize.z)) * 0.45f,
            isoLevel = 0f
        };

        public int GetCellSizeForWorldDistance(float worldDistance)
        {
            int lodIndex = GetLodIndexForWorldDistance(worldDistance);
            return NormalizeCellSize(GetCellSizeAtLod(lodIndex));
        }

        private void OnValidate()
        {
            chunkSize = new Vector3Int(
                Mathf.Max(1, chunkSize.x),
                Mathf.Max(1, chunkSize.y),
                Mathf.Max(1, chunkSize.z));
            EnsureCellSizes();
            ValidateLodDistances();
            defaultCellSize = Mathf.Max(1, defaultCellSize);
            nearCombinedMeshBucketCount = Mathf.Clamp(nearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
            neverLayerCullChunkDistance = Mathf.Max(0, neverLayerCullChunkDistance);
            maxDeferredSegmentLodChunksBuiltPerFrame = Mathf.Max(
                MinDeferredSegmentLodChunksBuiltPerFrame,
                maxDeferredSegmentLodChunksBuiltPerFrame);
        }

        private void EnsureCellSizes()
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                cellSizes = new[] { 8, 32, 64 };
            }
        }

        private void ValidateLodDistances()
        {
            if (lodDistances == null || lodDistances.Length == 0)
            {
                lodDistances = new[] { 256f, 768f, 2048f };
            }

            float previous = 0f;
            for (int i = 0; i < lodDistances.Length; i++)
            {
                lodDistances[i] = Mathf.Max(previous, lodDistances[i]);
                previous = lodDistances[i];
            }
        }

        private int GetLodIndexForWorldDistance(float worldDistance)
        {
            float normalizedDistance = Mathf.Max(0f, worldDistance);
            int cellSizeCount = GetLodCount();
            for (int i = 0; i < cellSizeCount; i++)
            {
                if (normalizedDistance <= GetMaxWorldDistanceForLod(i))
                {
                    return i;
                }
            }

            return cellSizeCount - 1;
        }

        public float GetMaxWorldDistanceForLod(int lodIndex)
        {
            if (lodDistances != null && lodIndex < lodDistances.Length)
            {
                return Mathf.Max(0f, lodDistances[lodIndex]);
            }

            return float.PositiveInfinity;
        }

        public int GetCellSizeAtLod(int lodIndex)
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                return DefaultCellSize;
            }

            return cellSizes[math.clamp(lodIndex, 0, cellSizes.Length - 1)];
        }

        private int GetLodCount()
        {
            int cellSizeCount = cellSizes != null ? cellSizes.Length : 0;
            int distanceCount = lodDistances != null ? lodDistances.Length : 0;
            return math.max(1, math.min(cellSizeCount, distanceCount));
        }

        private float GetLodStartDistanceForCellSize(int targetCellSize)
        {
            int cellSizeCount = GetLodCount();
            int normalizedTarget = NormalizeCellSize(targetCellSize);
            for (int i = 0; i < cellSizeCount; i++)
            {
                if (NormalizeCellSize(GetCellSizeAtLod(i)) == normalizedTarget)
                {
                    return i == 0 ? 0f : GetMaxWorldDistanceForLod(i - 1);
                }
            }

            return 0f;
        }

        private int GetFinestCellSize()
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                return DefaultCellSize;
            }

            int finest = int.MaxValue;
            for (int i = 0; i < cellSizes.Length; i++)
            {
                finest = math.min(finest, NormalizeCellSize(cellSizes[i]));
            }

            return finest == int.MaxValue ? DefaultCellSize : finest;
        }

        private int GetCoarsestCellSize()
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                return DefaultCellSize;
            }

            int coarsest = 1;
            for (int i = 0; i < cellSizes.Length; i++)
            {
                coarsest = math.max(coarsest, NormalizeCellSize(cellSizes[i]));
            }

            return coarsest;
        }

        private int NormalizeCellSize(int requestedSize)
        {
            int requested = Mathf.Max(1, requestedSize);
            int maxValidSize = Mathf.Min(chunkSize.x, Mathf.Min(chunkSize.y, chunkSize.z));
            requested = Mathf.Min(requested, maxValidSize);

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

    }
}
