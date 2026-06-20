using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [CreateAssetMenu(menuName = "Voxel Engine/Voxel Engine Config")]
    public sealed class VoxelEngineConfig : ScriptableObject
    {
        [SerializeField] private Vector3Int chunkSize = new Vector3Int(32, 32, 32);
        [SerializeField] private int[] cellSizes =
        {
            1,
            4,
            8,
            16,
            32,
            64,
            128,
            256
        };
        [SerializeField] private float[] octreeDetailDistances =
        {
            16f,
            32f,
            64f,
            128f,
            256f,
            512f,
            1024f,
            2048f
        };
        [SerializeField, Min(1)] private int defaultCellSize = 8;
        [SerializeField, Min(1f)] private float octreeFocusRebuildDistance = 1f;

        public int3 ChunkSize => new int3(chunkSize.x, chunkSize.y, chunkSize.z);
        public int DefaultCellSize => NormalizeCellSize(defaultCellSize);
        public int FinestCellSize => GetFinestCellSize();
        public int CoarsestCellSize => GetCoarsestCellSize();
        public float CoarsestLodStartDistance => GetLodStartDistanceForCellSize(CoarsestCellSize);
        public float OctreeFocusRebuildDistance => Mathf.Max(1f, octreeFocusRebuildDistance);

        public ScalarFieldSettings ScalarFieldSettings => new ScalarFieldSettings
        {
            debugSphereCenter = new float3(chunkSize.x, chunkSize.y, chunkSize.z) * 0.5f,
            debugSphereRadius = math.min(chunkSize.x, math.min(chunkSize.y, chunkSize.z)) * 0.45f,
            isoLevel = 0f
        };

        public int GetCellSizeForWorldDistance(float worldDistance)
        {
            int lodIndex = GetLodIndexForWorldDistance(worldDistance);
            return NormalizeCellSize(GetCellSizeAt(lodIndex));
        }

        private void OnValidate()
        {
            chunkSize = new Vector3Int(
                Mathf.Max(1, chunkSize.x),
                Mathf.Max(1, chunkSize.y),
                Mathf.Max(1, chunkSize.z));
            ValidateCellSizes();
            ValidateOctreeDetailDistances();
            defaultCellSize = NormalizeCellSize(defaultCellSize);
            octreeFocusRebuildDistance = Mathf.Max(1f, octreeFocusRebuildDistance);
        }

        private void ValidateCellSizes()
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                cellSizes = new[] { 1, 4, 8, 16, 32, 64, 128, 256 };
            }

            for (int i = 0; i < cellSizes.Length; i++)
            {
                cellSizes[i] = NormalizeCellSize(cellSizes[i]);
            }
        }

        private void ValidateOctreeDetailDistances()
        {
            if (octreeDetailDistances == null || octreeDetailDistances.Length == 0)
            {
                octreeDetailDistances = new[] { 16f, 32f, 64f, 128f, 256f, 512f, 1024f, 2048f };
            }

            float previous = 0f;
            for (int i = 0; i < octreeDetailDistances.Length; i++)
            {
                octreeDetailDistances[i] = Mathf.Max(previous, octreeDetailDistances[i]);
                previous = octreeDetailDistances[i];
            }
        }

        private int GetLodIndexForWorldDistance(float worldDistance)
        {
            float normalizedDistance = Mathf.Max(0f, worldDistance);
            int cellSizeCount = GetOctreeCellSizeCount();
            for (int i = 0; i < cellSizeCount; i++)
            {
                if (normalizedDistance <= GetMaxWorldDistanceForLodIndex(i))
                {
                    return i;
                }
            }

            return cellSizeCount - 1;
        }

        private float GetMaxWorldDistanceForLodIndex(int lodIndex)
        {
            if (octreeDetailDistances != null && lodIndex < octreeDetailDistances.Length)
            {
                return Mathf.Max(0f, octreeDetailDistances[lodIndex]);
            }

            return float.PositiveInfinity;
        }

        private int GetCellSizeAt(int lodIndex)
        {
            if (cellSizes == null || cellSizes.Length == 0)
            {
                return DefaultCellSize;
            }

            return cellSizes[math.clamp(lodIndex, 0, cellSizes.Length - 1)];
        }

        private int GetOctreeCellSizeCount()
        {
            int cellSizeCount = cellSizes != null ? cellSizes.Length : 0;
            int distanceCount = octreeDetailDistances != null ? octreeDetailDistances.Length : 0;
            return math.max(1, math.min(cellSizeCount, distanceCount));
        }

        private float GetLodStartDistanceForCellSize(int targetCellSize)
        {
            int cellSizeCount = GetOctreeCellSizeCount();
            int normalizedTarget = NormalizeCellSize(targetCellSize);
            for (int i = 0; i < cellSizeCount; i++)
            {
                if (NormalizeCellSize(GetCellSizeAt(i)) == normalizedTarget)
                {
                    return i == 0 ? 0f : GetMaxWorldDistanceForLodIndex(i - 1);
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
