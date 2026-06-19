using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [CreateAssetMenu(menuName = "Voxel Engine/Voxel Engine Config")]
    public sealed class VoxelEngineConfig : ScriptableObject
    {
        [SerializeField] private Vector3Int chunkSize = new Vector3Int(32, 32, 32);
        [SerializeField, Min(0)] private int activeChunkRadius = 4;
        [SerializeField, Min(0)] private int highDetailMaxChunkDistance = 1;
        [SerializeField, Min(0)] private int mediumDetailMaxChunkDistance = 3;
        [SerializeField, Min(1)] private int highDetailCellSize = 1;
        [SerializeField, Min(1)] private int mediumDetailCellSize = 4;
        [SerializeField, Min(1)] private int lowDetailCellSize = 16;
        [SerializeField, Min(1)] private int defaultCellSize = 1;
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
            if (chunkDistance <= highDetailMaxChunkDistance)
            {
                return NormalizeCellSize(highDetailCellSize);
            }

            if (chunkDistance <= mediumDetailMaxChunkDistance)
            {
                return NormalizeCellSize(mediumDetailCellSize);
            }

            return NormalizeCellSize(lowDetailCellSize);
        }

        private void OnValidate()
        {
            chunkSize = new Vector3Int(
                Mathf.Max(1, chunkSize.x),
                Mathf.Max(1, chunkSize.y),
                Mathf.Max(1, chunkSize.z));
            activeChunkRadius = Mathf.Max(0, activeChunkRadius);
            highDetailMaxChunkDistance = Mathf.Max(0, highDetailMaxChunkDistance);
            mediumDetailMaxChunkDistance = Mathf.Max(highDetailMaxChunkDistance, mediumDetailMaxChunkDistance);
            highDetailCellSize = NormalizeCellSize(highDetailCellSize);
            mediumDetailCellSize = NormalizeCellSize(mediumDetailCellSize);
            lowDetailCellSize = NormalizeCellSize(lowDetailCellSize);
            defaultCellSize = Mathf.Max(1, defaultCellSize);
            debugSphereRadius = Mathf.Max(0.01f, debugSphereRadius);
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
