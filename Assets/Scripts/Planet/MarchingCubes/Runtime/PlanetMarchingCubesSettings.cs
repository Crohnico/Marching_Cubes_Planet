using System;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSettings
    {
        public const int DefaultOutputVertexCapacity = 3000000;
        public const int CountOnlyOutputVertexCapacity = 3;
        public const int Lod2OutputVertexCapacityBudget = 8000;
        public const int Lod1OutputVertexCapacityBudget = 62000;
        public const int Lod0OutputVertexCapacityBudget = 500000;
        private const int MaxTrianglesPerCell = 5;
        private const int VerticesPerTriangle = 3;

        [FormerlySerializedAs("surfaceRange")]
        public PlanetMarchingCubesChunkRange chunkRange;
        [FormerlySerializedAs("maxPlanetSurfaceTriangles")]
        [FormerlySerializedAs("maxValidationTriangles")]
        public int outputVertexCapacity;

        public static PlanetMarchingCubesSettings Default()
        {
            return new PlanetMarchingCubesSettings
            {
                chunkRange = PlanetMarchingCubesChunkRange.Default(),
                outputVertexCapacity = DefaultOutputVertexCapacity
            };
        }

        public long EstimatedVertexBytes => (long)Math.Max(0, outputVertexCapacity) * PlanetMarchingCubesVertex.Stride;

        public static int CalculateMaxOutputVertexCapacityForChunkSize(int chunkSize)
        {
            long safeChunkSize = Math.Max(1, chunkSize);
            long cellCount = safeChunkSize * safeChunkSize * safeChunkSize;
            long vertexCapacity = cellCount * MaxTrianglesPerCell * VerticesPerTriangle;
            return vertexCapacity > int.MaxValue ? int.MaxValue : (int)vertexCapacity;
        }

        public static int GetOutputVertexCapacityBudgetForLod(PlanetChunkLod lod)
        {
            switch (lod)
            {
                case PlanetChunkLod.LOD0:
                    return Lod0OutputVertexCapacityBudget;
                case PlanetChunkLod.LOD1:
                    return Lod1OutputVertexCapacityBudget;
                case PlanetChunkLod.LOD2:
                    return Lod2OutputVertexCapacityBudget;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }
        }

        public void EnsureDefaults()
        {
            PlanetMarchingCubesSettings defaults = Default();
            if (chunkRange.chunkSize <= 0 && chunkRange.cellSizeGrid <= 0 && chunkRange.safetyMargin == 0 && chunkRange.maxCandidateChunks == 0)
            {
                chunkRange = defaults.chunkRange;
            }

            chunkRange.EnsureDefaults();

            if (outputVertexCapacity <= 0)
            {
                outputVertexCapacity = defaults.outputVertexCapacity;
            }
        }

        public bool Validate(out string message)
        {
            if (!chunkRange.Validate(out message))
            {
                return false;
            }

            if (outputVertexCapacity <= 0)
            {
                message = "outputVertexCapacity must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return chunkRange +
                   "\noutputVertexCapacity=" + outputVertexCapacity +
                   "\nestimatedVertexBytes=" + EstimatedVertexBytes;
        }
    }
}
