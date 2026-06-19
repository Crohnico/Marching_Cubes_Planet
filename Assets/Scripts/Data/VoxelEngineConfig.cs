using System;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [CreateAssetMenu(menuName = "Voxel Engine/Voxel Engine Config")]
    public sealed class VoxelEngineConfig : ScriptableObject
    {
        [SerializeField] private Vector3Int chunkSize = new Vector3Int(32, 32, 32);
        [SerializeField, Min(0)] private int activeChunkRadius = 4;
        [SerializeField] private VoxelLodLevel[] lodLevels =
        {
            new VoxelLodLevel(1, 1),
            new VoxelLodLevel(3, 4),
            new VoxelLodLevel(5, 8),
            new VoxelLodLevel(7, 16),
            new VoxelLodLevel(8, 32)
        };
        [SerializeField, Min(1)] private int defaultCellSize = 32;
        [SerializeField] private Vector3 debugSphereCenter = new Vector3(16f, 16f, 16f);
        [SerializeField, Min(0.01f)] private float debugSphereRadius = 14f;
        [SerializeField] private float isoLevel;

        public int3 ChunkSize => new int3(chunkSize.x, chunkSize.y, chunkSize.z);
        public int ActiveChunkRadius => activeChunkRadius;
        public int DefaultCellSize => NormalizeCellSize(defaultCellSize);

        public ScalarFieldSettings ScalarFieldSettings => new ScalarFieldSettings
        {
            debugSphereCenter = new float3(debugSphereCenter.x, debugSphereCenter.y, debugSphereCenter.z),
            debugSphereRadius = debugSphereRadius,
            isoLevel = isoLevel
        };

        public int GetCellSizeForChunkDistance(int chunkDistance)
        {
            if (lodLevels == null || lodLevels.Length == 0)
            {
                return DefaultCellSize;
            }

            int normalizedDistance = math.max(0, chunkDistance);
            for (int i = 0; i < lodLevels.Length; i++)
            {
                VoxelLodLevel level = lodLevels[i];
                if (normalizedDistance <= level.maxChunkDistance)
                {
                    return NormalizeCellSize(level.cellSize);
                }
            }

            return NormalizeCellSize(lodLevels[lodLevels.Length - 1].cellSize);
        }

        private void OnValidate()
        {
            chunkSize = new Vector3Int(
                Mathf.Max(1, chunkSize.x),
                Mathf.Max(1, chunkSize.y),
                Mathf.Max(1, chunkSize.z));
            activeChunkRadius = Mathf.Max(0, activeChunkRadius);
            ValidateLodLevels();
            defaultCellSize = NormalizeCellSize(defaultCellSize);
            debugSphereRadius = Mathf.Max(0.01f, debugSphereRadius);
        }

        private void ValidateLodLevels()
        {
            if (lodLevels == null || lodLevels.Length == 0)
            {
                lodLevels = new[]
                {
                    new VoxelLodLevel(1, 1),
                    new VoxelLodLevel(3, 4),
                    new VoxelLodLevel(5, 8),
                    new VoxelLodLevel(7, 16),
                    new VoxelLodLevel(activeChunkRadius, 32)
                };
            }

            for (int i = 0; i < lodLevels.Length; i++)
            {
                lodLevels[i] = new VoxelLodLevel(
                    Mathf.Max(0, lodLevels[i].maxChunkDistance),
                    NormalizeCellSize(lodLevels[i].cellSize));
            }

            Array.Sort(lodLevels, (a, b) => a.maxChunkDistance.CompareTo(b.maxChunkDistance));
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

        [Serializable]
        public struct VoxelLodLevel
        {
            [Min(0)] public int maxChunkDistance;
            [Min(1)] public int cellSize;

            public VoxelLodLevel(int maxChunkDistance, int cellSize)
            {
                this.maxChunkDistance = maxChunkDistance;
                this.cellSize = cellSize;
            }
        }
    }
}
