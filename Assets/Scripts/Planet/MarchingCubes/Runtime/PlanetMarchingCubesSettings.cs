using System;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSettings
    {
        public const int DefaultOutputVertexCapacity = 3000000;

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
